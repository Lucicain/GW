using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace GreyWardenTestBridge
{
    /// <summary>跨帧命令（读档、存档、推进时间）：每帧推进一次，完成时才回复（PROTOCOL.md §3）。</summary>
    internal abstract class BridgePending
    {
        protected BridgePending(BridgeRequest request)
        {
            Request = request;
        }

        internal BridgeRequest Request { get; }

        /// <summary>读档进行中时 status 报 loading。</summary>
        internal virtual bool IsLoading => false;

        /// <summary>完成时返回回复；还没完返回 null。</summary>
        internal abstract BridgeReply? Tick();

        /// <summary>Tick 抛异常时调用：解开命令挂上的事件。正常完成时由命令自己在返回回复前收尾。</summary>
        internal virtual void Abandon()
        {
        }
    }

    /// <summary>
    /// 主线程一侧：持有网络服务和 bridge.json，在 <c>OnApplicationTick</c> 里取命令执行。
    /// 所有游戏对象只在这里（主线程）读写。
    /// </summary>
    internal sealed class BridgeHost
    {
        private readonly BridgeServer _server;
        private readonly string _infoPath;
        private readonly BridgeGameState _game = new BridgeGameState();
        private BridgePending? _pending;
        private Stopwatch? _quitRequested;
        private volatile bool _quitReplyFlushed;
        private bool _quitIssued;

        private BridgeHost(BridgeServer server, string infoPath)
        {
            _server = server;
            _infoPath = infoPath;
        }

        internal static BridgeHost Start(string session)
        {
            int pid = Process.GetCurrentProcess().Id;
            // ApplicationVersion.ToString() 为前缀+主.次.修订.变更集，如 v1.4.8.119303（本机 v1.4.8 源码库核对 2026-11-04）。
            string gameVersion = ModuleHelper.GetModuleInfo("Native")?.Version.ToString() ?? string.Empty;
            var hello = new JObject
            {
                ["protocol"] = BridgeProtocol.Version,
                ["pid"] = pid,
                ["game_version"] = gameVersion,
                ["bridge_version"] = BridgeProtocol.BridgeVersion,
            };
            string token = RandomHex(16);
            var server = new BridgeServer(token, session, hello);
            server.Start();

            // 本 DLL 在 Modules/GreyWardenTestBridge/bin/<平台>/，bridge.json 写在模组根目录（PROTOCOL.md §1.4）。
            string binDir = Path.GetDirectoryName(typeof(BridgeHost).Assembly.Location)!;
            string infoPath = Path.Combine(Path.GetFullPath(Path.Combine(binDir, "..", "..")), "bridge.json");
            var host = new BridgeHost(server, infoPath);
            host.WriteInfo(new JObject
            {
                ["protocol"] = BridgeProtocol.Version,
                ["host"] = BridgeProtocol.Host,
                ["port"] = server.Port,
                ["token"] = token,
                ["session"] = session,
                ["pid"] = pid,
                ["game_version"] = gameVersion,
                ["bridge_version"] = BridgeProtocol.BridgeVersion,
            });
            return host;
        }

        /// <summary>先写临时文件再改名，Python 端不会读到半截（PROTOCOL.md §1.4）。</summary>
        private void WriteInfo(JObject info)
        {
            string temp = _infoPath + ".tmp";
            File.WriteAllText(temp, info.ToString(Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(_infoPath)) File.Replace(temp, _infoPath, null);
            else File.Move(temp, _infoPath);
        }

        /// <summary>游戏退出时删掉 bridge.json；删不掉也不影响，Python 端靠 session 区分新旧（PROTOCOL.md §1.4）。</summary>
        internal void DeleteInfoFile()
        {
            if (File.Exists(_infoPath)) File.Delete(_infoPath);
        }

        private static string RandomHex(int bytes)
        {
            byte[] buffer = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buffer);
            return string.Concat(buffer.Select(b => b.ToString("x2")));
        }

        internal void Tick()
        {
            _game.Observe();

            if (_pending != null)
            {
                BridgePending pending = _pending;
                BridgeReply? done = Execute(pending.Request, pending.Tick, pending);
                if (done != null)
                {
                    _pending = null;
                    pending.Request.Reply(done);
                }
            }

            while (_server.TryDequeue(out BridgeRequest request))
            {
                BridgeReply? reply = Execute(request, () => Dispatch(request), null);
                if (reply != null) request.Reply(reply);
            }

            if (_quitRequested != null && !_quitIssued
                && (_quitReplyFlushed || _quitRequested.Elapsed.TotalSeconds > 2.0))
            {
                // 回复已写出（或写不出去）之后的下一帧才退出，不存档（PROTOCOL.md §5 quit）。
                _quitIssued = true;
                DeleteInfoFile();
                TaleWorlds.Engine.Utilities.QuitGame();
            }
        }

        /// <summary>
        /// 执行一条命令。主线程上抛出的异常按协议转成 internal 回复（PROTOCOL.md §3）。
        /// </summary>
        private static BridgeReply? Execute(BridgeRequest request, Func<BridgeReply?> body, BridgePending? pending)
        {
            try
            {
                return body();
            }
            catch (Exception gwpQuietFailure)
            {
                // 依据：设计（PROTOCOL.md §3）——主线程执行命令时的异常是协议规定要回给调用方的 internal 错误，
                // 由 Python 端把类型、消息和堆栈原样交给 AI；不是兜底吞错。
#if GWP_DIAGNOSTICS
                GreyWardenPolicePurity.GwpFaultTrace.WriteQuiet(gwpQuietFailure);
#endif
                pending?.Abandon();
                return BridgeReply.Fail("internal",
                    request.Command + " 执行时异常：" + gwpQuietFailure.GetType().FullName + ": " + gwpQuietFailure.Message,
                    new JObject { ["stack"] = gwpQuietFailure.ToString() });
            }
        }

        private BridgeReply? Dispatch(BridgeRequest request)
        {
            string command = request.Command;
            if (_quitRequested != null)
                return BridgeReply.Fail("invalid_state", "游戏正在退出");
            if (_pending != null && command != "status" && command != "query")
                return BridgeReply.Fail("invalid_state", "上一条命令 " + _pending.Request.Command + " 还没完成");

            switch (command)
            {
                case "status":
                    return BridgeReply.Ok(Status());
                case "load_save":
                    return Begin(BridgeLoadSave.Create(request, _game));
                case "save":
                    return Begin(BridgeSave.Create(request, _game));
                case "advance":
                    return Begin(BridgeAdvance.Create(request, _game));
                case "console":
                    return Console(request.Args);
                case "query":
                    return BridgeQuery.Run(request.Args, _game);
                case "act":
                    return BridgeAct.Run(request.Args, _game);
                case "quit":
                    _quitRequested = Stopwatch.StartNew();
                    request.Connection.Send(BridgeReply.Ok(new JObject { ["quitting"] = true }).ToJson(request.Id),
                        () => _quitReplyFlushed = true);
                    return null;
                default:
                    return BridgeReply.Fail("unknown_command", "桥接不认识命令 " + command);
            }
        }

        /// <summary>跨帧命令：创建时就失败的直接回复；否则挂起，下一帧开始推进。</summary>
        private BridgeReply? Begin((BridgePending? pending, BridgeReply? reply) created)
        {
            if (created.pending == null) return created.reply;
            _pending = created.pending;
            return null;
        }

        private JObject Status()
        {
            var result = new JObject { ["state"] = _pending?.IsLoading == true ? "loading" : _game.Describe() };
            Campaign? campaign = Campaign.Current;
            if (campaign != null && _game.HasLiveCampaign)
            {
                result["campaign_time"] = BridgeGameState.Time(CampaignTime.Now);
                result["save_name"] = TaleWorlds.Core.MBSaveLoad.ActiveSaveSlotName;
                result["paused"] = campaign.TimeControlMode == CampaignTimeControlMode.Stop;
                result["time_control"] = campaign.TimeControlMode.ToString();
            }

            JObject? ui = _game.ModalBlocker();
            if (ui != null) result["ui"] = ui;
            if (_pending != null) result["pending"] = _pending.Request.Command;
            return result;
        }

        /// <summary>
        /// 原版控制台：首个空白前是命令名，其余按空白拆成参数，交给 <c>CommandLineFunctionality.CallFunction</c>。
        /// 存读档与退出类命令名一律拒绝（PROTOCOL.md §5、§7.4）。
        /// </summary>
        private static BridgeReply Console(JObject args)
        {
            JToken? token = args["command"];
            if (token == null || token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
                return BridgeReply.Fail("bad_request", "console 需要非空字符串参数 command");
            string text = token.Value<string>()!;
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                return BridgeReply.Fail("bad_request", "控制台命令不能含换行");

            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string name = parts[0];
            if (BridgeProtocol.ConsoleDeny.IsMatch(name))
                return BridgeReply.Fail("refused", "控制台命令 " + name + " 涉及存读档或退出，改用 save / load_save / quit");
            if (!CommandLineFunctionality.HasFunctionForCommand(name))
                return BridgeReply.Fail("unknown_command", "原版控制台没有命令 " + name);

            string output = CommandLineFunctionality.CallFunction(name, parts.Skip(1).ToList(), out bool found);
            if (!found) return BridgeReply.Fail("unknown_command", "原版控制台没有命令 " + name);
            return BridgeReply.Ok(new JObject { ["output"] = output, ["found"] = true });
        }
    }
}
