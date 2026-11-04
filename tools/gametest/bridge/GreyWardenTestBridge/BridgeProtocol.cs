using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GreyWardenTestBridge
{
    /// <summary>协议常量，与 tools/gametest/PROTOCOL.md 和 Python 端 gametest/fences.py 逐条对应。</summary>
    internal static class BridgeProtocol
    {
        internal const int Version = 1;
        internal const string BridgeVersion = "0.1.0";
        internal const string Host = "127.0.0.1";

        /// <summary>单行上限 16 MiB（PROTOCOL.md §2）。</summary>
        internal const int MaxLineBytes = 16 * 1024 * 1024;

        /// <summary>推进时间时，时间停走超过这么多真实秒数就报 blocked（PROTOCOL.md §5 advance）。</summary>
        internal const double AdvanceStallSeconds = 10.0;

        /// <summary>与 Python 端 fences._SAVE_RE 同一条正则（PROTOCOL.md §5）。</summary>
        internal static readonly Regex SaveName = new Regex(@"^gwp_test_[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

        /// <summary>与 Python 端 fences._CONSOLE_DENY_RE 一致：存读档只走 save/load_save，退出只走 quit。</summary>
        internal static readonly Regex ConsoleDeny = new Regex("(save|load|quit|exit)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>一条命令的结果：成功带 result，失败带错误码（PROTOCOL.md §6）。</summary>
    internal sealed class BridgeReply
    {
        private BridgeReply(JToken? result, string? code, string? message, JToken? data)
        {
            Result = result;
            Code = code;
            Message = message;
            Data = data;
        }

        internal JToken? Result { get; }
        internal string? Code { get; }
        internal string? Message { get; }
        internal JToken? Data { get; }
        internal bool IsOk => Code == null;

        internal static BridgeReply Ok(JToken result) => new BridgeReply(result, null, null, null);

        internal static BridgeReply Fail(string code, string message, JToken? data = null) =>
            new BridgeReply(null, code, message, data);

        internal JObject ToJson(JToken id)
        {
            var reply = new JObject { ["id"] = id, ["ok"] = IsOk };
            if (IsOk)
            {
                reply["result"] = Result ?? JValue.CreateNull();
                return reply;
            }

            var error = new JObject { ["code"] = Code, ["message"] = Message };
            if (Data != null) error["data"] = Data;
            reply["error"] = error;
            return reply;
        }
    }

    /// <summary>网络线程解析好的一条请求，交给主线程执行。</summary>
    internal sealed class BridgeRequest
    {
        internal BridgeRequest(BridgeConnection connection, long id, string command, JObject args)
        {
            Connection = connection;
            Id = id;
            Command = command;
            Args = args;
        }

        internal BridgeConnection Connection { get; }
        internal long Id { get; }
        internal string Command { get; }
        internal JObject Args { get; }

        internal void Reply(BridgeReply reply) => Connection.Send(reply.ToJson(Id));
    }
}
