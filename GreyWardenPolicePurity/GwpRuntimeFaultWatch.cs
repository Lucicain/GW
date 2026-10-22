using System;
using System.Runtime.ExceptionServices;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Development runtime fault observation, including deferred engine callbacks.
    /// First-chance entries are observations, not proof of a crash or its cause.
    /// Deduplicated and bounded per session; unhandled failures remain uncapped.
    /// Observation is compiled out of player builds.
    /// </summary>
    internal static class GwpRuntimeFaultWatch
    {
#if GWP_DIAGNOSTICS
        private const int MaxWritten = 80;
        private static readonly System.Reflection.Assembly Ours =
            typeof(GwpRuntimeFaultWatch).Assembly;
        private static bool _armed;
        private static int _written;
        private static readonly System.Collections.Generic.HashSet<string> Seen =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        [ThreadStatic] private static bool _recording;

        internal static void Arm()
        {
            if (_armed) return;
            _armed = true;
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
            GwpFaultTrace.Write("FAULT_WATCH_ARMED", details: "assembly="
                + typeof(GwpRuntimeFaultWatch).Assembly.GetName().Version
                + "; build=" + typeof(GwpRuntimeFaultWatch).Module.ModuleVersionId
                + "; scope=runtime; firstChance=observed_not_necessarily_fatal");
        }

        private static void OnFirstChance(object sender, FirstChanceExceptionEventArgs args)
        {
            Exception? exception = args?.Exception;
            if (exception == null || _recording || _written >= MaxWritten) return;

            try
            {
                _recording = true;
                string signature = exception.GetType().FullName + "|" + exception.TargetSite + "|" + exception.Message;
                lock (Seen)
                {
                    if (!Seen.Add(signature)) return;
                    _written++;
                }
                bool ours = exception.TargetSite?.DeclaringType?.Assembly == Ours;
                // The throwing frame's IL offset maps a release-built vanilla or
                // Harmony-replaced method without line info to its exact instruction.
                System.Diagnostics.StackFrame? thrower = new System.Diagnostics.StackTrace(exception, false).GetFrame(0);
                // Observe native/other-mod managed exceptions too: a deferred
                // engine callback need not contain our namespace on its stack.
                GwpFaultTrace.Write(ours ? "FIRST_CHANCE_MOD" : "FIRST_CHANCE_OBSERVED",
                    details: "ilOffset=" + (thrower?.GetILOffset().ToString() ?? "-") + " | "
                        + exception.ToString().Replace(Environment.NewLine, " | ")
                        + " | observerStack=" + Environment.StackTrace.Replace(Environment.NewLine, " | "));
            }
            catch
            {
                // 依据：CLR 语义（首次异常处理器里抛出的异常会顶替正在处理的原异常）
                // 诊断永远不能反过来影响游戏。
            }
            finally { _recording = false; }
        }

        internal static void StartSession()
        {
            lock (Seen) { Seen.Clear(); _written = 0; }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            if (args?.ExceptionObject is not Exception exception) return;
            GwpFaultTrace.Write("UNHANDLED",
                details: exception.ToString().Replace(Environment.NewLine, " | "));
        }

#else
        internal static void Arm()
        {
        }

        internal static void StartSession() { }


#endif
    }
}
