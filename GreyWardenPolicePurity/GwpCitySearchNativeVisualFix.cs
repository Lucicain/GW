using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 本机1.4.8两次WER：人物离场时，渲染队列访问已清空的Agent_visuals实体。
    /// 只补+0x5AEAB6漏掉的实体判空，空值进入原函数已有的空资源分支；非空保留原指令。
    /// 这是进程内指令修复，不改磁盘上的游戏DLL。其他二进制只有完整指令签名相同时才适用。
    /// </summary>
    internal static class GwpCitySearchNativeVisualFix
    {
        internal const int PatchRva = 0x5AEAB6;
        private const int OverwrittenLength = 20;
        private const int PageSize = 4096;
        private const int AbsoluteJumpLength = 14;
        private static IntPtr _guardPage;

        // 从空指针读取到后续空资源调用的完整83字节；同时核对所有续接点及原生call的位移。
        private static readonly byte[] Expected =
        {
            0x48, 0x8B, 0x81, 0x70, 0x01, 0x00, 0x00, 0x48, 0x85, 0xC0, 0x74, 0x25,
            0xC7, 0x44, 0x24, 0x28, 0x27, 0x00, 0x00, 0x00, 0xC6, 0x44, 0x24, 0x20,
            0x00, 0x41, 0xB1, 0x01, 0x45, 0x33, 0xC0, 0x48, 0x8B, 0xD1, 0x48, 0x8B,
            0xC8, 0xE8, 0x50, 0xAD, 0xD9, 0xFF, 0x48, 0x8B, 0x8B, 0xB8, 0x08, 0x00,
            0x00, 0xE8, 0x84, 0xA8, 0xFF, 0xFF, 0xC6, 0x44, 0x24, 0x20, 0x01, 0x41,
            0xB9, 0x08, 0x00, 0x00, 0x00, 0x4C, 0x8B, 0x83, 0xB8, 0x08, 0x00, 0x00,
            0x41, 0x8B, 0xD7, 0x49, 0x8B, 0xCC, 0xE8, 0xE7, 0x08, 0x00, 0x00
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
            if (imageSize < PatchRva + Expected.Length) return false;
            IntPtr site = IntPtr.Add(imageBase, PatchRva);
            var original = new byte[Expected.Length];
            Marshal.Copy(site, original, 0, original.Length);
            if (!original.SequenceEqual(Expected)) return false;

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
            var code = new List<byte> { 0x48, 0x85, 0xC9, 0x0F, 0x85, 0, 0, 0, 0 }; // test rcx,rcx; jne valid
#if GWP_DIAGNOSTICS
            // 只保存原生外观地址和序号；不调托管回调，不持有实体。短锁只保护这两项诊断数据。
            code.AddRange(new byte[] { 0x50, 0x48, 0xB8 }); // push rax; mov rax,page
            code.AddRange(BitConverter.GetBytes(page.ToInt64()));
            code.AddRange(new byte[] { 0xF0, 0x48, 0x0F, 0xBA, 0xA8 }); // lock bts [rax+lock],0
            code.AddRange(BitConverter.GetBytes(TraceLockOffset));
            code.AddRange(new byte[] { 0, 0x72, 0xF4 }); // jc acquire，锁内无调用、无引擎操作
            code.AddRange(new byte[] { 0x48, 0x89, 0x98 }); // mov [rax+visual],rbx
            code.AddRange(BitConverter.GetBytes(LastVisualOffset));
            code.AddRange(new byte[] { 0xF0, 0x48, 0xFF, 0x80 }); // lock inc [rax+count]
            code.AddRange(BitConverter.GetBytes(CounterOffset));
            code.AddRange(new byte[] { 0xF0, 0x48, 0x0F, 0xBA, 0xB0 }); // lock btr [rax+lock],0
            code.AddRange(BitConverter.GetBytes(TraceLockOffset));
            code.AddRange(new byte[] { 0, 0x58 }); // pop rax
#endif
            AppendJump(code, IntPtr.Add(site, 0x36)); // +0x5AEAEC：原有空资源处理
            byte[] validOffset = BitConverter.GetBytes(code.Count - 9);
            for (int i = 0; i < validOffset.Length; i++) code[5 + i] = validOffset[i];
            code.AddRange(Expected.Take(10)); // mov rax,[rcx+170h]; test rax,rax
            code.AddRange(new byte[] { 0x75, AbsoluteJumpLength }); // jne parent_present
            AppendJump(code, IntPtr.Add(site, 0x31)); // +0x5AEAE7：原有无父实体路径
            code.AddRange(Expected.Skip(12).Take(8)); // mov dword [rsp+28h],27h
            AppendJump(code, IntPtr.Add(site, OverwrittenLength)); // +0x5AEACA：原有带父实体路径
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
