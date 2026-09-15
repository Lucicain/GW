using System;
using System.Runtime.ExceptionServices;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// [GWP_TEST_SCAFFOLD] 启动与读档阶段的异常捕捉。游戏自己的崩溃窗口只在玩家同意
    /// 生成转储时才留下堆栈，取消之后什么都查不到。这里挂上第一时机异常钩子，把任何
    /// 经过本模组代码的异常连同堆栈写进 GreyWarden-Faults.log。
    ///
    /// 只在 GWP_DIAGNOSTICS 下存在，玩家构建里整个类是空的。定位到问题、修好并经用户
    /// 实机确认之后，连同本文件一起退役。
    /// </summary>
    internal static class GwpLoadFaultWatch
    {
#if GWP_DIAGNOSTICS
        private const string OurNamespace = "GreyWardenPolicePurity";
        private const int MaxWritten = 40;
        // 只在启动与读档这段时间里挂着。之后自行摘钩，免得每一次原版内部异常都要
        // 付一次堆栈字符串的代价。
        private const int WatchWindowMilliseconds = 600000;
        private static readonly System.Reflection.Assembly Ours =
            typeof(GwpLoadFaultWatch).Assembly;
        private static bool _armed;
        private static int _written;
        private static int _armedAtTicks;

        internal static void Arm()
        {
            if (_armed) return;
            _armed = true;
            _armedAtTicks = Environment.TickCount;
            try
            {
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
                GwpFaultTrace.Write("FAULT_WATCH_ARMED", details: "assembly="
                    + typeof(GwpLoadFaultWatch).Assembly.GetName().Version);
            }
            catch
            {
                // 诊断永远不能反过来影响游戏。
            }
        }

        private static void OnFirstChance(object sender, FirstChanceExceptionEventArgs args)
        {
            Exception? exception = args?.Exception;
            if (exception == null) return;
            if (_written >= MaxWritten ||
                unchecked(Environment.TickCount - _armedAtTicks) > WatchWindowMilliseconds)
            {
                Disarm();
                return;
            }

            try
            {
                // 先用便宜的判据；抛出点不在本模组时才退回到扫描堆栈字符串。
                bool ours = exception.TargetSite?.DeclaringType?.Assembly == Ours;
                if (!ours && exception.ToString()
                        .IndexOf(OurNamespace, StringComparison.Ordinal) < 0)
                    return;
                _written++;
                GwpFaultTrace.Write("FIRST_CHANCE",
                    details: exception.ToString().Replace(Environment.NewLine, " | "));
            }
            catch
            {
                // 诊断永远不能反过来影响游戏。
            }
        }

        private static void Disarm()
        {
            try { AppDomain.CurrentDomain.FirstChanceException -= OnFirstChance; }
            catch { }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            if (args?.ExceptionObject is not Exception exception) return;
            GwpFaultTrace.Write("UNHANDLED",
                details: exception.ToString().Replace(Environment.NewLine, " | "));
        }

        /// <summary>把一段读档/启动阶段的代码包起来：出事只记录，不把整局游戏带下去。</summary>
        internal static void Guard(string stage, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("GUARDED_FAILURE",
                    details: stage + " | " + exception.ToString().Replace(Environment.NewLine, " | "));
            }
        }
#else
        internal static void Arm()
        {
        }

        internal static void Guard(string stage, Action action) => action();
#endif
    }
}
