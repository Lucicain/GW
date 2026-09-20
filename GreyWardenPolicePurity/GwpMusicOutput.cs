using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace GreyWardenPolicePurity
{
    // Uses the Windows output device, not Bannerlord's exhausted/unseekable PSAI channels.
    // Four prepared 20 ms buffers. Only this worker owns headers, score and device calls.
    // No native game API is ever called on the audio thread.
    internal sealed class GwpMusicOutput : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct Format
        {
            public ushort Tag, Channels;
            public uint Rate, BytesPerSecond;
            public ushort Align, Bits, Extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Header
        {
            public IntPtr Data;
            public uint Length, Recorded;
            public UIntPtr User;
            public uint Flags, Loops;
            public IntPtr Next;
            public UIntPtr Reserved;
        }
        [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint device, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutPause(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutRestart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
        private readonly ConcurrentQueue<string> _commands = new();
        public readonly ConcurrentQueue<string> Notices = new();
        private readonly Thread _worker;
        private readonly GwpMusicScore _score;
        private IntPtr _device;
        private volatile bool _stop;
        public volatile bool Paused;
        public volatile float Volume = 1;
        public Exception? Failure { get; private set; }
        private const int FramesPerBuffer = 960;
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));

        public GwpMusicOutput(string directory)
        {
            _score = new GwpMusicScore(directory, s => Notices.Enqueue(s));
            var fmt = new Format { Tag = 3, Channels = 2, Rate = 48000, BytesPerSecond = 384000, Align = 8, Bits = 32 };
            try
            {
                Check(waveOutOpen(out _device, uint.MaxValue, ref fmt, IntPtr.Zero, IntPtr.Zero, 0), "open");
                // Do not submit audio until the caller has silenced the native music manager.
                Paused = true;
                _worker = new Thread(Run) { IsBackground = true, Name = "GreyWarden music", Priority = ThreadPriority.AboveNormal };
                _worker.Start();
            }
            catch { if (_device != IntPtr.Zero) waveOutClose(_device); _score.Dispose(); throw; }
        }
        public void Want(string state) => _commands.Enqueue(state);
        public void Reinforcement() => _commands.Enqueue("reinforcement");
        private static void Check(uint result, string operation)
        { if (result != 0) throw new InvalidOperationException("Music output " + operation + " failed (WinMM " + result + ")"); }
        private void Run()
        {
            var headers = new IntPtr[4]; var buffers = new IntPtr[4]; var prepared = new bool[4]; var queued = new bool[4];
            var samples = new float[FramesPerBuffer * 2];
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    buffers[i] = Marshal.AllocHGlobal(samples.Length * 4);
                    headers[i] = Marshal.AllocHGlobal((int)HeaderSize);
                    Marshal.StructureToPtr(new Header { Data = buffers[i], Length = (uint)samples.Length * 4 }, headers[i], false);
                    Check(waveOutPrepareHeader(_device, headers[i], HeaderSize), "prepare"); prepared[i] = true;
                }
                bool suspended = false;
                int next = 0;
                while (!_stop)
                {
                    if (Paused)
                    {
                        if (!suspended) { Check(waveOutPause(_device), "pause"); suspended = true; }
                        Thread.Sleep(5); continue;
                    }
                    if (suspended) { Check(waveOutRestart(_device), "resume"); suspended = false; }
                    var h = Marshal.PtrToStructure<Header>(headers[next]);
                    if (queued[next] && (h.Flags & 1) == 0) { Thread.Sleep(2); continue; }
                    while (_commands.TryDequeue(out string? command))
                        if (command == "reinforcement") _score.Reinforcement(); else _score.Want(command);
                    _score.Render(samples, FramesPerBuffer);
                    float volume = Math.Max(0, Math.Min(1, Volume));
                    for (int i = 0; i < samples.Length; i++) samples[i] *= volume;
                    Marshal.Copy(samples, 0, buffers[next], samples.Length);
                    Check(waveOutWrite(_device, headers[next], HeaderSize), "write");
                    queued[next] = true; next = (next + 1) % 4;
                }
            }
            catch (Exception e) { Failure = e; }
            finally
            {
                waveOutReset(_device);
                for (int i = 0; i < 4; i++)
                {
                    if (prepared[i]) waveOutUnprepareHeader(_device, headers[i], HeaderSize);
                    if (headers[i] != IntPtr.Zero) Marshal.FreeHGlobal(headers[i]);
                    if (buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(buffers[i]);
                }
                waveOutClose(_device); _device = IntPtr.Zero; _score.Dispose();
            }
        }
        public void Dispose() { _stop = true; if (Thread.CurrentThread != _worker) _worker.Join(); }
    }
}
