using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 本机1.4.8离场转储及整段原生更新负对照：空实体不能进入资源重建，-1会被继续当索引使用。
    /// 保留原有旧资源清理，在新资源分配前跳过空实体；非空仍执行原指令。
    /// 只修改进程内指令，完整3028字节原函数签名相同才安装。
    /// </summary>
    internal static class GwpCitySearchNativeVisualFix
    {
        internal const int FunctionRva = 0x5AE760;
        internal const int PatchRva = 0x5AE8BA;
        private const int FunctionLength = 3028;
        private const int OverwrittenLength = 16;
        private const int PageSize = 4096;
        private static IntPtr _guardPage;

        // 覆盖旧资源清理、插入点、资源重建、栈和寄存器恢复；该范围没有PE基址重定位项。
        private static readonly byte[] FunctionHash =
        {
            0xC8, 0xAE, 0xE6, 0xF8, 0x15, 0x25, 0x75, 0x18, 0x43, 0x92, 0x6B, 0xEE, 0x6A, 0x33, 0xFF, 0xD8,
            0x67, 0x5A, 0xBE, 0x54, 0x6C, 0x84, 0xD7, 0xB3, 0xFA, 0x5D, 0x09, 0xE0, 0xA6, 0x21, 0x7B, 0xC9
        };

        private static readonly byte[] Expected =
        {
            0x49, 0x8D, 0x9C, 0x24, 0x38, 0x2C, 0x00, 0x00,
            0x48, 0x8B, 0x43, 0x18, 0x48, 0x2B, 0x43, 0x10
        };

        internal static void Install()
        {
            // 模块初始化时安装，此时尚无人物渲染任务，避免在执行中的指令上写入跳转。
            ProcessModule? native = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .FirstOrDefault(module => module.ModuleName.Equals("TaleWorlds.Native.dll", StringComparison.OrdinalIgnoreCase));
            if (native != null) TryInstall(native.BaseAddress, native.ModuleMemorySize);
        }

        internal static bool TryInstall(IntPtr imageBase, int imageSize)
        {
            if (_guardPage != IntPtr.Zero) return true;
            if (imageSize < FunctionRva + FunctionLength) return false;
            IntPtr site = IntPtr.Add(imageBase, PatchRva);
            var original = new byte[FunctionLength];
            Marshal.Copy(IntPtr.Add(imageBase, FunctionRva), original, 0, original.Length);
            using (var sha = SHA256.Create())
                if (!sha.ComputeHash(original).SequenceEqual(FunctionHash)) return false;

            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)PageSize, 0x3000, 0x40);
            if (page == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] guard = BuildGuard(site, page);
            Marshal.Copy(guard, 0, page, guard.Length);
            FlushCode(page, guard.Length);

            var patch = new List<byte>();
            AppendJump(patch, page);
            while (patch.Count < OverwrittenLength) patch.Add(0x90);
            if (!VirtualProtect(site, (UIntPtr)OverwrittenLength, 0x40, out uint protection))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Marshal.Copy(patch.ToArray(), 0, site, patch.Count);
            if (!VirtualProtect(site, (UIntPtr)OverwrittenLength, protection, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            FlushCode(site, patch.Count);
            // 跳板随引擎进程存活；原生代码仍引用它，不能随战役或Mission释放。
            _guardPage = page;
            return true;
        }

        private static byte[] BuildGuard(IntPtr site, IntPtr page)
        {
            // 到这里原函数已销毁旧缓存，尚未申请新槽位。RBP保存的外观指针不依赖当前RBX。
            var code = new List<byte>
            {
                0x50, 0x48, 0x8B, 0x45, 0x9F, // push rax; mov rax,[rbp-61h]
                0x48, 0x83, 0xB8, 0xB8, 0x08, 0x00, 0x00, 0x00, // cmp qword [rax+8B8h],0
                0x0F, 0x85, 0, 0, 0, 0 // jne valid
            };
#if GWP_DIAGNOSTICS
            // 只保存原生外观地址和序号；不调托管回调，不持有实体。短锁只保护这两项诊断数据。
            code.AddRange(new byte[] { 0x52, 0x48, 0xBA }); // push rdx; mov rdx,page
            code.AddRange(BitConverter.GetBytes(page.ToInt64()));
            code.AddRange(new byte[] { 0xF0, 0x48, 0x0F, 0xBA, 0xAA }); // lock bts [rdx+lock],0
            code.AddRange(BitConverter.GetBytes(TraceLockOffset));
            code.AddRange(new byte[] { 0, 0x72, 0xF4 }); // jc acquire，锁内无调用、无引擎操作
            code.AddRange(new byte[] { 0x48, 0x89, 0x82 }); // mov [rdx+visual],rax
            code.AddRange(BitConverter.GetBytes(LastVisualOffset));
            code.AddRange(new byte[] { 0xF0, 0x48, 0xFF, 0x82 }); // lock inc [rdx+count]
            code.AddRange(BitConverter.GetBytes(CounterOffset));
            code.AddRange(new byte[] { 0xF0, 0x48, 0x0F, 0xBA, 0xB2 }); // lock btr [rdx+lock],0
            code.AddRange(BitConverter.GetBytes(TraceLockOffset));
            code.AddRange(new byte[] { 0, 0x5A }); // pop rdx
#endif
            code.Add(0x58); // pop rax
            AppendJump(code, IntPtr.Add(site, 0xA5E)); // +0x5AF318：恢复XMM6、栈、全部非易失寄存器后返回
            byte[] validOffset = BitConverter.GetBytes(code.Count - 19);
            for (int i = 0; i < validOffset.Length; i++) code[15 + i] = validOffset[i];
            code.Add(0x58); // pop rax
            code.AddRange(Expected);
            AppendJump(code, IntPtr.Add(site, OverwrittenLength)); // +0x5AE8CA：继续原有资源分配
            return code.ToArray();
        }

        private static void AppendJump(List<byte> code, IntPtr target)
        {
            // RIP间接绝对跳转，不占用寄存器，不要求跳板分配在原函数±2GB内。
            code.AddRange(new byte[] { 0xFF, 0x25, 0, 0, 0, 0 });
            code.AddRange(BitConverter.GetBytes(target.ToInt64()));
        }

        private static void FlushCode(IntPtr start, int length)
        {
            if (!FlushInstructionCache(GetCurrentProcess(), start, (UIntPtr)length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

#if GWP_DIAGNOSTICS
        private const int CounterOffset = 256;
        private const int TraceLockOffset = 264;
        private const int LastVisualOffset = 272;
        private static long _lastEmptyCount = -1;

        internal static long EmptyUpdateCount => _guardPage == IntPtr.Zero ? 0 : Marshal.ReadInt64(_guardPage, CounterOffset);

        internal static bool TryReadLastUpdate(out ulong visual, out long count)
        {
            visual = 0;
            count = EmptyUpdateCount;
            if (_guardPage == IntPtr.Zero || Marshal.ReadInt64(_guardPage, TraceLockOffset) != 0) return false;
            System.Threading.Thread.MemoryBarrier();
            visual = unchecked((ulong)Marshal.ReadInt64(_guardPage, LastVisualOffset));
            System.Threading.Thread.MemoryBarrier();
            // 先读锁、最后复核序号，排除读到更新到一半的数据；不在托管侧等待原生线程。
            return Marshal.ReadInt64(_guardPage, TraceLockOffset) == 0 && count == EmptyUpdateCount;
        }

        internal static void WriteDiagnostics()
        {
            long count = EmptyUpdateCount;
            if (count == _lastEmptyCount) return;
            bool stable = TryReadLastUpdate(out ulong visual, out long sampleCount);
            if (_guardPage != IntPtr.Zero && !stable) return; // 写入中留到下一次采样，不丢掉这条命中记录。
            _lastEmptyCount = sampleCount;
            GwpAiDiagnostics.WriteCitySearch("VISUAL_NATIVE_GUARD", "installed=" + (_guardPage != IntPtr.Zero) + "; emptyEntityUpdates=" + sampleCount
                + "; latestVisual=0x" + visual.ToString("X") + "; latestSequence=" + sampleCount + "; sampleStable=" + stable);
        }
#endif

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint oldProtect);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
