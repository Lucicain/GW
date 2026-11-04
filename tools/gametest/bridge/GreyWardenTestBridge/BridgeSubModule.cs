using System;
using TaleWorlds.MountAndBlade;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 游戏测试桥的模组入口（tools/gametest/PROTOCOL.md）。
    /// 只在测试工具经环境变量 <see cref="SessionEnvironmentVariable"/> 启动游戏时启用；
    /// 没有它（例如用户在启动器里误勾了本模组）就什么都不做：不监听、不写文件。（依据：设计，PROTOCOL.md §1、§7）
    /// </summary>
    public sealed class BridgeSubModule : MBSubModuleBase
    {
        internal const string SessionEnvironmentVariable = "GWP_TEST_BRIDGE_SESSION";

        private BridgeHost? _host;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            string? session = Environment.GetEnvironmentVariable(SessionEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(session)) return;
            _host = BridgeHost.Start(session!.Trim());
        }

        /// <summary>主线程：取出网络线程排好的命令执行，推进跨帧命令（PROTOCOL.md §3）。</summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            _host?.Tick();
        }

        protected override void OnSubModuleUnloaded()
        {
            _host?.DeleteInfoFile();
            base.OnSubModuleUnloaded();
        }
    }
}
