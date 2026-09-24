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
        private readonly Thread _worker;
        private readonly GwpMusicScore _score;
        private IntPtr _device;
        private volatile bool _stop;
        public volatile bool Paused;
        public volatile float Volume = 1;
        public Exception? Failure { get; private set; }
        private const int FramesPerBuffer = 960;
        // The Syndicate score sits about 10 dB below Bannerlord's battle music (EBU R128: our mix
        // -23.5 LUFS, native battle/siege tracks median -13.3). +8 dB stays slightly under native;
        // the limiter keeps the rare peaks this pushes past -1 dBFS from clipping.
        internal const float MakeupDb = 8;
        private static readonly float Makeup = (float)Math.Pow(10, MakeupDb / 20);
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));

        public GwpMusicOutput(string directory)
        {
            _score = new GwpMusicScore(directory);
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
            var limiter = new GwpMusicLimiter();
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
                    limiter.Process(samples, FramesPerBuffer, Math.Max(0, Math.Min(1, Volume)) * Makeup);
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

    // Look-ahead peak limiter after the volume, like the Master bus limiter in the original Wwise
    // chain (-1 dBFS, 10 ms look-ahead, 20 ms release). Output is delayed by the look-ahead. The gain
    // at each sample is the average, over the look-ahead, of the lowest gain any sample within one
    // look-ahead of it needs, so it ramps down before a peak and never lets one past the threshold.
    internal sealed class GwpMusicLimiter
    {
        internal const int Lookahead = 480;
        internal static readonly float Threshold = (float)Math.Pow(10, -1 / 20.0);
        private static readonly double Release = 1 - Math.Exp(-1 / (0.020 * 48000));
        private readonly float[] _delay = new float[(Lookahead + 1) * 2];
        // The minimum spans one sample more than the delay, so every term of the average covers the emitted sample.
        private readonly float[] _need = new float[Lookahead + 2];
        private readonly float[] _mins = new float[Lookahead + 1];
        private readonly int[] _queue = new int[Lookahead + 2];
        private readonly long[] _queueAt = new long[Lookahead + 2];
        private int _head, _count;
        private long _frame;
        private double _sum = Lookahead + 1, _gain = 1;
        public float Reduction { get; private set; } = 1;

        public GwpMusicLimiter() { for (int i = 0; i < _mins.Length; i++) _mins[i] = 1; }

        public void Process(float[] samples, int frames, float gain)
        {
            int n = Lookahead + 1, m = n + 1;
            float lowest = 1;
            for (int i = 0; i < frames; i++, _frame++)
            {
                float l = samples[i * 2] * gain, r = samples[i * 2 + 1] * gain;
                float peak = Math.Max(Math.Abs(l), Math.Abs(r));
                float need = peak > Threshold ? Threshold / peak : 1;
                int slot = (int)(_frame % n), at = (int)(_frame % m);
                // Sliding minimum of the needed gain over the last m input samples (monotonic queue).
                // Expire first: the slot about to be overwritten belongs to the sample leaving the window.
                while (_count > 0 && _queueAt[_head] <= _frame - m) { _head = (_head + 1) % m; _count--; }
                _need[at] = need;
                while (_count > 0 && _need[_queue[(_head + _count - 1) % m]] >= need) _count--;
                _queue[(_head + _count) % m] = at; _queueAt[(_head + _count) % m] = _frame; _count++;
                float min = _need[_queue[_head]];
                // Box average of those minima: full reduction lands exactly on the delayed peak.
                _sum += min - _mins[slot]; _mins[slot] = min;
                double target = Math.Min(1, _sum / n);
                _gain = target < _gain ? target : _gain + (target - _gain) * Release;
                // Emit the sample that entered one look-ahead ago.
                float dl = _delay[slot * 2], dr = _delay[slot * 2 + 1];
                _delay[slot * 2] = l; _delay[slot * 2 + 1] = r;
                samples[i * 2] = (float)(dl * _gain); samples[i * 2 + 1] = (float)(dr * _gain);
                if (_gain < lowest) lowest = (float)_gain;
            }
            Reduction = lowest;
        }
    }
}
