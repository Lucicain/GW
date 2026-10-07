using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GreyWardenPolicePurity
{
    // Same Windows output API as GwpMusicOutput, with one finite buffer instead of a streaming score.
    // No looping flags, playback-rate changes, or FMOD category timeline; reset discards queued audio immediately.
    internal sealed class GwpRitualSoundOutput : IDisposable
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
        [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
        [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);

        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));
        private IntPtr _device, _header, _samples;
        private bool _prepared;
        internal bool IsFinished => _header == IntPtr.Zero ||
            (Marshal.PtrToStructure<Header>(_header).Flags & 1) != 0;

        internal GwpRitualSoundOutput(string path, float gain)
        {
            byte[] samples = ReadPcm(path, out Format format);
            gain = Math.Max(0, Math.Min(1, gain));
            for (int i = 0; i < samples.Length; i += 2)
            {
                short value = (short)((short)(samples[i] | samples[i + 1] << 8) * gain);
                samples[i] = (byte)value;
                samples[i + 1] = (byte)(value >> 8);
            }
            try
            {
                Check(waveOutOpen(out _device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0), "open");
                _samples = Marshal.AllocHGlobal(samples.Length);
                Marshal.Copy(samples, 0, _samples, samples.Length);
                _header = Marshal.AllocHGlobal((int)HeaderSize);
                Marshal.StructureToPtr(new Header { Data = _samples, Length = (uint)samples.Length }, _header, false);
                Check(waveOutPrepareHeader(_device, _header, HeaderSize), "prepare");
                _prepared = true;
                Check(waveOutWrite(_device, _header, HeaderSize), "write");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>这段音频多长（秒）。大地图上的闪卡音效按它放完一遍就停（GwpRitualMapSoundView）。</summary>
        internal static float DurationSeconds(string path)
        {
            byte[] samples = ReadPcm(path, out Format format);
            return samples.Length / (float)format.BytesPerSecond;
        }

        private static byte[] ReadPcm(string path, out Format format)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadUInt32() != 0x46464952)
                throw new InvalidDataException("Ritual audio must be RIFF PCM.");
            reader.ReadUInt32();
            if (reader.ReadUInt32() != 0x45564157)
                throw new InvalidDataException("Ritual audio must be WAVE PCM.");
            format = default;
            byte[] samples = Array.Empty<byte>();
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                uint chunk = reader.ReadUInt32();
                int length = checked((int)reader.ReadUInt32());
                long next = reader.BaseStream.Position + length + (length & 1);
                if (chunk == 0x20746d66)
                {
                    format.Tag = reader.ReadUInt16();
                    format.Channels = reader.ReadUInt16();
                    format.Rate = reader.ReadUInt32();
                    format.BytesPerSecond = reader.ReadUInt32();
                    format.Align = reader.ReadUInt16();
                    format.Bits = reader.ReadUInt16();
                }
                else if (chunk == 0x61746164)
                    samples = reader.ReadBytes(length);
                reader.BaseStream.Position = next;
            }
            if (format.Tag != 1 || format.Bits != 16 || format.Channels != 2 || format.Align != 4 ||
                format.Rate != 48000 || samples.Length == 0 || samples.Length % format.Align != 0)
                throw new InvalidDataException("Ritual audio must be 48000 Hz, stereo, 16-bit PCM.");
            return samples;
        }

        public void Dispose()
        {
            if (_device != IntPtr.Zero)
            {
                waveOutReset(_device);
                if (_prepared)
                    waveOutUnprepareHeader(_device, _header, HeaderSize);
                waveOutClose(_device);
                _device = IntPtr.Zero;
            }
            if (_header != IntPtr.Zero) { Marshal.FreeHGlobal(_header); _header = IntPtr.Zero; }
            if (_samples != IntPtr.Zero) { Marshal.FreeHGlobal(_samples); _samples = IntPtr.Zero; }
        }

        private static void Check(uint result, string operation)
        {
            if (result != 0)
                throw new InvalidOperationException("Ritual sound " + operation + " failed (WinMM " + result + ").");
        }
    }
}
