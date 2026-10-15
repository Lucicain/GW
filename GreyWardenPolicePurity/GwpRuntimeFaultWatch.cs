using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

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
            // 2026-10-16 08:37 实机无报错闪退：rgl 日志没有正常退出的托管卸载记录，看门狗没收到异常事件，
            // 没有 WER 记录或转储，首次/未处理异常观察也都没触发。用退出记录区分托管退出与进程被直接结束，
            // 用内存采样核对内存耗尽的猜测（开局时系统空闲内存只剩 17.9%）。问题查清后随本诊断一起退休。
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
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
                // Observe native/other-mod managed exceptions too: a deferred
                // engine callback need not contain our namespace on its stack.
                GwpFaultTrace.Write(ours ? "FIRST_CHANCE_MOD" : "FIRST_CHANCE_OBSERVED",
                    details: exception.ToString().Replace(Environment.NewLine, " | ")
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

        private static readonly Stopwatch MemoryClock = Stopwatch.StartNew();
        private const long MemorySampleMilliseconds = 60_000;
        private static long _nextMemorySample;

        /// <summary>主线程应用心跳调用；每分钟写一条内存采样。</summary>
        internal static void SampleMemory()
        {
            if (MemoryClock.ElapsedMilliseconds < _nextMemorySample) return;
            _nextMemorySample = MemoryClock.ElapsedMilliseconds + MemorySampleMilliseconds;
            GwpFaultTrace.Write("MEMORY", details: MemoryState());
        }

        private static void OnProcessExit(object sender, EventArgs args) =>
            GwpFaultTrace.Write("PROCESS_EXIT", details: "exitCode=" + Environment.ExitCode + "; " + MemoryState()
                + "; stack=" + Environment.StackTrace.Replace(Environment.NewLine, " | "));

        private static string MemoryState()
        {
            Process process = Process.GetCurrentProcess();
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            bool system = GlobalMemoryStatusEx(ref status);
            const double mb = 1024d * 1024d;
            return "privateMb=" + (process.PrivateMemorySize64 / mb).ToString("0")
                + "; workingSetMb=" + (process.WorkingSet64 / mb).ToString("0")
                + "; managedMb=" + (GC.GetTotalMemory(false) / mb).ToString("0")
                + (system
                    ? "; systemLoad=" + status.MemoryLoad + "%; availPhysMb=" + (status.AvailPhys / mb).ToString("0")
                      + "/" + (status.TotalPhys / mb).ToString("0")
                      + "; availCommitMb=" + (status.AvailPageFile / mb).ToString("0")
                      + "/" + (status.TotalPageFile / mb).ToString("0")
                      + "; availVirtualMb=" + (status.AvailVirtual / mb).ToString("0")
                      + "/" + (status.TotalVirtual / mb).ToString("0")
                      + "; extVirtual=" + status.AvailExtendedVirtual
                    : "; system=unavailable");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

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

        internal static void SampleMemory() { }

#endif
    }
}
