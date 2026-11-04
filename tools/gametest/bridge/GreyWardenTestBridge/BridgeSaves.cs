using System;
using Newtonsoft.Json.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GreyWardenTestBridge
{
    /// <summary>存档名校验：前缀与字符集同 Python 端同一条正则，桥接永远不读写其他存档（PROTOCOL.md §5、§7.3）。</summary>
    internal static class BridgeSaveName
    {
        internal static string? Read(JObject args, out BridgeReply? error)
        {
            JToken? token = args["name"];
            if (token == null || token.Type != JTokenType.String)
            {
                error = BridgeReply.Fail("bad_request", "需要字符串参数 name");
                return null;
            }

            string name = token.Value<string>()!;
            if (!BridgeProtocol.SaveName.IsMatch(name))
            {
                error = BridgeReply.Fail("refused", "存档名必须匹配 ^gwp_test_[A-Za-z0-9_-]{1,64}$，拒绝读写其他存档：" + name);
                return null;
            }

            error = null;
            return name;
        }
    }

    /// <summary>
    /// 读档：在主菜单或战役地图上可用。战役中先经原版 <c>MBGameManager.EndGame</c> 回到主菜单，
    /// 再走原版 <c>SandBoxSaveHelper.TryLoadSave</c>；进入战役地图并跑完第一帧后才回复（PROTOCOL.md §5）。
    /// 原版要玩家确认的弹窗（模组不符、存档损坏等）不替玩家点，直接回 load_failed 并带上弹窗内容。
    /// </summary>
    internal sealed class BridgeLoadSave : BridgePending
    {
        private enum Phase
        {
            EndingGame,
            Loading,
            WaitingForMap,
        }

        private readonly BridgeGameState _game;
        private readonly SaveGameFileInfo _file;
        private readonly string _name;
        private Phase _phase;
        private bool _started;
        private bool _cancelled;
        private SandBoxSaveHelper.SaveHelperState? _helperState;

        private BridgeLoadSave(BridgeRequest request, BridgeGameState game, SaveGameFileInfo file, string name, Phase phase)
            : base(request)
        {
            _game = game;
            _file = file;
            _name = name;
            _phase = phase;
        }

        internal override bool IsLoading => true;

        internal static (BridgePending?, BridgeReply?) Create(BridgeRequest request, BridgeGameState game)
        {
            string? name = BridgeSaveName.Read(request.Args, out BridgeReply? error);
            if (name == null) return (null, error);

            bool atMenu = game.IsMainMenu;
            if (!atMenu && !game.IsCampaignMap)
                return (null, BridgeReply.Fail("invalid_state", "只能在主菜单或战役地图上读档，当前 " + game.Describe()));

            SaveGameFileInfo[] files = MBSaveLoad.GetSaveFiles(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (files.Length == 0) return (null, BridgeReply.Fail("not_found", "没有存档 " + name));
            SaveGameFileInfo file = files[0];
            if (file.IsCorrupted)
                return (null, BridgeReply.Fail("load_failed", "原版把存档 " + name + " 标为损坏", new JObject { ["reason"] = "corrupted" }));
            if (SandBoxSaveHelper.GetIsDisabledWithReason(file, out TextObject reason))
                return (null, BridgeReply.Fail("load_failed", "原版不允许读这个存档：" + reason,
                    new JObject { ["reason"] = "disabled", ["text"] = reason?.ToString() }));

            var command = new BridgeLoadSave(request, game, file, name, atMenu ? Phase.Loading : Phase.EndingGame);
            if (atMenu) command.BeginLoad();
            else MBGameManager.EndGame();
            // TODO(本地核对)：在 OnApplicationTick 里直接调 MBGameManager.EndGame 能否像暂停菜单「退回主菜单」一样
            // 干净地回到主菜单；若原版要求先关地图界面，改成照抄暂停菜单按钮的调用序列。
            return (command, null);
        }

        private void BeginLoad()
        {
            _phase = Phase.Loading;
            SandBoxSaveHelper.OnStateChange += OnHelperStateChange;
            SandBoxSaveHelper.TryLoadSave(_file, OnStartGame, OnCancel);
        }

        private void OnHelperStateChange(SandBoxSaveHelper.SaveHelperState state) => _helperState = state;

        // TODO(本地核对)：照原版存档界面的读档回调写——先 MBSaveLoad.OnStartGame 再 StartNewGame(SandBoxGameManager)。
        // 原版 SavedGameVM.StartGame 是私有的，没法直接调用；本地对照原版源码库确认两步的顺序与是否缺步骤。
        private void OnStartGame(LoadResult result)
        {
            _started = true;
            MBSaveLoad.OnStartGame(result);
            MBGameManager.StartNewGame(new SandBoxGameManager(result));
        }

        private void OnCancel() => _cancelled = true;

        internal override BridgeReply? Tick()
        {
            switch (_phase)
            {
                case Phase.EndingGame:
                    if (_game.IsMainMenu) BeginLoad();
                    return null;
                case Phase.Loading:
                    if (_started)
                    {
                        Detach();
                        _phase = Phase.WaitingForMap;
                        return null;
                    }

                    JObject? modal = _game.ModalBlocker();
                    if (_cancelled || _helperState == SandBoxSaveHelper.SaveHelperState.Inquiry || modal != null)
                    {
                        Detach();
                        var data = modal ?? new JObject { ["reason"] = _cancelled ? "cancelled" : "inquiry" };
                        return BridgeReply.Fail("load_failed",
                            "原版读档没有开始：" + (data.Value<string>("title") ?? data.Value<string>("reason")), data);
                    }

                    return null;
                default:
                    // 进入地图并跑完第一帧：Observe 在本帧开头已计入，>= 2 表示上一帧地图已经 tick 过。
                    return _game.MapFrames >= 2 ? BridgeReply.Ok(new JObject { ["name"] = _name }) : null;
            }
        }

        internal override void Abandon() => Detach();

        private void Detach() => SandBoxSaveHelper.OnStateChange -= OnHelperStateChange;
    }

    /// <summary>
    /// 存档：仅战役地图上。经原版 <c>SaveHandler.SaveAs</c>，等原版 <c>OnSaveOverEvent</c> 报告写完才回复（PROTOCOL.md §5）。
    /// </summary>
    internal sealed class BridgeSave : BridgePending
    {
        private readonly string _name;
        private bool _over;
        private bool _succeeded;
        private string? _savedAs;

        private BridgeSave(BridgeRequest request, string name)
            : base(request)
        {
            _name = name;
        }

        internal static (BridgePending?, BridgeReply?) Create(BridgeRequest request, BridgeGameState game)
        {
            string? name = BridgeSaveName.Read(request.Args, out BridgeReply? error);
            if (name == null) return (null, error);
            JToken? overwriteToken = request.Args["overwrite"];
            if (overwriteToken != null && overwriteToken.Type != JTokenType.Boolean && overwriteToken.Type != JTokenType.Null)
                return (null, BridgeReply.Fail("bad_request", "overwrite 必须是布尔值"));
            bool overwrite = overwriteToken?.Type == JTokenType.Boolean && overwriteToken.Value<bool>();

            if (!game.IsCampaignMap)
                return (null, BridgeReply.Fail("invalid_state", "只能在战役地图上存档，当前 " + game.Describe()));
            Campaign campaign = Campaign.Current;
            if (campaign.SaveHandler.IsSaving)
                return (null, BridgeReply.Fail("invalid_state", "原版正在存档"));
            // TODO(本地核对)：IsSaveGameFileExists 收的是不带扩展名的存档名（与 SaveGameFileInfo.Name 相同）。
            if (MBSaveLoad.IsSaveGameFileExists(name) && !overwrite)
                return (null, BridgeReply.Fail("exists", "存档 " + name + " 已存在，未允许覆盖"));

            var command = new BridgeSave(request, name);
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(command, command.OnSaveOver);
            campaign.SaveHandler.SaveAs(name);
            return (command, null);
        }

        private void OnSaveOver(bool succeeded, string savedAs)
        {
            _over = true;
            _succeeded = succeeded;
            _savedAs = savedAs;
        }

        internal override BridgeReply? Tick()
        {
            if (!_over) return null;
            Detach();
            if (!_succeeded)
                return BridgeReply.Fail("save_failed", "原版报告存档失败：" + _name,
                    new JObject { ["saved_as"] = _savedAs });
            return BridgeReply.Ok(new JObject { ["name"] = _name, ["saved_as"] = _savedAs });
        }

        internal override void Abandon() => Detach();

        private void Detach() => CampaignEvents.OnSaveOverEvent.ClearListeners(this);
    }
}
