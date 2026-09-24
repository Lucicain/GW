using System;
using System.IO;

namespace GreyWardenPolicePurity
{
    // Decodes the music sources: 16-bit stereo FLAC with a fixed block size, one frame at a time.
    // The exporter records every frame's byte offset, so a seek decodes only the frame it lands in.
    // No game dependencies; MusicTests checks the output bit for bit against the original PCM.
    internal sealed class GwpFlacReader : IDisposable
    {
        private readonly FileStream _stream;
        private readonly long[] _offsets;
        private readonly int _block;
        private byte[] _bytes = new byte[16384];
        private readonly int[] _coefficients = new int[32];
        private int _frame = -1, _count;
        public readonly long Frames;
        // Decoded samples of the current frame, per channel.
        public readonly int[] Left, Right;

        public GwpFlacReader(string path, long[] offsets, int block, long frames)
        {
            _offsets = offsets; _block = block; Frames = frames;
            Left = new int[block]; Right = new int[block];
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
        }

        // Makes the frame holding `position` current and returns where it starts; Count samples are valid.
        public long Seek(long position)
        {
            int frame = checked((int)(position / _block));
            if (frame != _frame) Decode(frame);
            return (long)frame * _block;
        }
        public int Count => _count;

        private void Decode(int frame)
        {
            long start = _offsets[frame], end = frame + 1 < _offsets.Length ? _offsets[frame + 1] : _stream.Length;
            int size = checked((int)(end - start));
            if (_bytes.Length < size + 8) _bytes = new byte[size + 8];
            _stream.Position = start;
            for (int read = 0; read < size;)
            {
                int n = _stream.Read(_bytes, read, size - read);
                if (n == 0) throw new EndOfStreamException(_stream.Name);
                read += n;
            }
            _frame = -1;
            var bits = new Bits(_bytes, size);
            if (bits.Read(15) != 0x7FFC) throw new InvalidDataException("FLAC sync lost at frame " + frame);
            bits.Read(1); // blocking strategy: the exporter guarantees fixed
            int blockCode = (int)bits.Read(4), rateCode = (int)bits.Read(4);
            int channels = (int)bits.Read(4), depthCode = (int)bits.Read(3);
            bits.Read(1);
            uint coded = bits.Read(8); // frame number, UTF-8 style
            for (uint mask = 0x40; (coded & 0x80) != 0 && (coded & mask) != 0; mask >>= 1) { bits.Read(8); coded &= ~mask; }
            int count = blockCode switch
            {
                1 => 192,
                >= 2 and <= 5 => 576 << (blockCode - 2),
                6 => (int)bits.Read(8) + 1,
                7 => (int)bits.Read(16) + 1,
                >= 8 => 256 << (blockCode - 8),
                _ => throw new InvalidDataException("FLAC block size code 0"),
            };
            if (rateCode == 12) bits.Read(8); else if (rateCode == 13 || rateCode == 14) bits.Read(16);
            bits.Read(8); // header CRC; MusicTests verifies the decoded PCM instead
            if (count > _block || (depthCode != 0 && depthCode != 4) || channels < 1 || channels > 10)
                throw new InvalidDataException($"Unsupported FLAC frame {frame}: block {count}, depth {depthCode}, channels {channels}");
            // 8 = left/side, 9 = side/right, 10 = mid/side; the side channel carries one extra bit.
            Subframe(ref bits, Left, count, channels == 9 ? 17 : 16);
            Subframe(ref bits, Right, count, channels == 8 || channels == 10 ? 17 : 16);
            if (channels == 8) for (int i = 0; i < count; i++) Right[i] = Left[i] - Right[i];
            else if (channels == 9) for (int i = 0; i < count; i++) Left[i] += Right[i];
            else if (channels == 10)
                for (int i = 0; i < count; i++)
                {
                    int mid = (Left[i] << 1) | (Right[i] & 1), side = Right[i];
                    Left[i] = (mid + side) >> 1; Right[i] = (mid - side) >> 1;
                }
            else if (channels != 1) throw new InvalidDataException("FLAC music must be stereo");
            _count = count; _frame = frame;
        }

        private void Subframe(ref Bits bits, int[] s, int count, int depth)
        {
            bits.Read(1);
            int type = (int)bits.Read(6), wasted = 0;
            if (bits.Read(1) != 0) wasted = bits.Unary() + 1;
            depth -= wasted;
            if (type == 0)
            {
                int value = bits.Signed(depth);
                for (int i = 0; i < count; i++) s[i] = value;
            }
            else if (type == 1) for (int i = 0; i < count; i++) s[i] = bits.Signed(depth);
            else if (type >= 8 && type <= 12)
            {
                int order = type - 8;
                for (int i = 0; i < order; i++) s[i] = bits.Signed(depth);
                Residual(ref bits, s, count, order);
                for (int i = order; i < count; i++)
                    s[i] += order switch
                    {
                        0 => 0,
                        1 => s[i - 1],
                        2 => 2 * s[i - 1] - s[i - 2],
                        3 => 3 * s[i - 1] - 3 * s[i - 2] + s[i - 3],
                        _ => 4 * s[i - 1] - 6 * s[i - 2] + 4 * s[i - 3] - s[i - 4],
                    };
            }
            else if (type >= 32)
            {
                int order = type - 31;
                for (int i = 0; i < order; i++) s[i] = bits.Signed(depth);
                int precision = (int)bits.Read(4) + 1, shift = bits.Signed(5);
                if (precision == 16 || shift < 0) throw new InvalidDataException("FLAC LPC header");
                int[] coefficients = _coefficients;
                for (int i = 0; i < order; i++) coefficients[i] = bits.Signed(precision);
                Residual(ref bits, s, count, order);
                for (int i = order; i < count; i++)
                {
                    long sum = 0;
                    for (int j = 0; j < order; j++) sum += (long)coefficients[j] * s[i - 1 - j];
                    s[i] += (int)(sum >> shift);
                }
            }
            else throw new InvalidDataException("FLAC reserved subframe type " + type);
            if (wasted > 0) for (int i = 0; i < count; i++) s[i] <<= wasted;
        }

        // Rice-coded residual, written in place after the warm-up samples.
        private static void Residual(ref Bits bits, int[] s, int count, int order)
        {
            int method = (int)bits.Read(2);
            if (method > 1) throw new InvalidDataException("FLAC residual method " + method);
            int parameterBits = method == 0 ? 4 : 5, escape = (1 << parameterBits) - 1;
            int partitionOrder = (int)bits.Read(4), partitions = 1 << partitionOrder, i = order;
            for (int p = 0; p < partitions; p++)
            {
                int end = (count >> partitionOrder) * (p + 1);
                int k = (int)bits.Read(parameterBits);
                if (k == escape)
                {
                    int width = (int)bits.Read(5);
                    for (; i < end; i++) s[i] = width == 0 ? 0 : bits.Signed(width);
                }
                else
                    for (; i < end; i++)
                    {
                        uint v = ((uint)bits.Unary() << k) | bits.Read(k);
                        s[i] = (int)(v >> 1) ^ -(int)(v & 1);
                    }
            }
        }

        public void Dispose() => _stream.Dispose();

        // MSB-first bit reader with a 64-bit cache; valid bits sit at the top of _cache.
        private struct Bits
        {
            private readonly byte[] _data;
            private readonly int _length;
            private int _position, _cached;
            private ulong _cache;
            public Bits(byte[] data, int length) { _data = data; _length = length; _position = 0; _cached = 0; _cache = 0; }
            private void Fill()
            {
                while (_cached <= 56)
                {
                    ulong b = _position < _length ? _data[_position] : 0UL;
                    _position++;
                    _cache |= b << (56 - _cached);
                    _cached += 8;
                }
                if (_position > _length + 8) throw new EndOfStreamException("FLAC frame overrun");
            }
            public uint Read(int n)
            {
                if (n == 0) return 0;
                if (_cached < n) Fill();
                uint value = (uint)(_cache >> (64 - n));
                _cache <<= n; _cached -= n;
                return value;
            }
            public int Signed(int n) => n == 0 ? 0 : (int)Read(n) << (32 - n) >> (32 - n);
            public int Unary()
            {
                int zeros = 0;
                while (true)
                {
                    if (_cached == 0) Fill();
                    if (_cache == 0) { zeros += _cached; _cached = 0; continue; }
                    int lead = LeadingZeros(_cache);
                    if (lead >= _cached) { zeros += _cached; _cache = 0; _cached = 0; continue; }
                    zeros += lead;
                    _cache = lead == 63 ? 0 : _cache << (lead + 1);
                    _cached -= lead + 1;
                    return zeros;
                }
            }
            private static int LeadingZeros(ulong x)
            {
                int n = 0;
                if ((x & 0xFFFFFFFF00000000UL) == 0) { n += 32; x <<= 32; }
                if ((x & 0xFFFF000000000000UL) == 0) { n += 16; x <<= 16; }
                if ((x & 0xFF00000000000000UL) == 0) { n += 8; x <<= 8; }
                if ((x & 0xF000000000000000UL) == 0) { n += 4; x <<= 4; }
                if ((x & 0xC000000000000000UL) == 0) { n += 2; x <<= 2; }
                if ((x & 0x8000000000000000UL) == 0) n += 1;
                return n;
            }
        }
    }
}
