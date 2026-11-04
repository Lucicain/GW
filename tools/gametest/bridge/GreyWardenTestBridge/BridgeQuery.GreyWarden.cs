using Newtonsoft.Json.Linq;

namespace GreyWardenTestBridge
{
    internal static partial class BridgeQuery
    {
        private static BridgeReply GreyWardenKind(string kind) =>
            BridgeReply.Fail("unavailable", "query kind " + kind + " 尚未实现");
    }
}
