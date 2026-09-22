using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GreyWardenPolicePurity
{
    // The lab's score is the source of truth. No PSAI theme/intensity approximation.
    // This renderer has no game/device dependencies and is also exercised offline.
    internal sealed class GwpMusicScore : IDisposable
    {
        internal const int Rate = 48000;
        private static readonly double[] FadeUp = BuildCurve(true), FadeDown = BuildCurve(false);
        private readonly JObject _data;
        private readonly string _directory;
        private readonly Random _random;
        private readonly List<Voice> _voices = new();
        private readonly Dictionary<string, List<string>> _bags = new();
        private readonly Dictionary<string, string> _last = new();
        private readonly Action<string>? _log;
        private long _frame, _pendingUntil, _retryAt, _redrawUntil;
        private string _state = "intro", _wanted = "intro";
        private bool _reinforcement;
        public long Frame => _frame;
        public string State => _state;
        public int VoiceCount => _voices.Count;

        internal sealed class Voice : IDisposable
        {
            public string Id = "", State = "";
            public JObject Segment = null!;
            public bool Bridge;
            public long Anchor, ControlAt, Start, Offset, Exit, End, Stop = long.MaxValue;
            public long FadeIn, FadeOutAt = long.MaxValue, FadeOut;
            public FileStream Stream = null!;
            private byte[] _bytes = new byte[8192];
            private float[] _samples = new float[2048];
            public void Mix(float[] output, int frames, long now)
            {
                long begin = Math.Max(now, Start), end = Math.Min(now + frames, Math.Min(End, Stop));
                if (end <= begin) return;
                int count = checked((int)(end - begin)), size = count * 8;
                if (_bytes.Length < size) { _bytes = new byte[size]; _samples = new float[count * 2]; }
                Stream.Position = (Offset + begin - Start) * 8;
                int read = 0;
                while (read < size)
                {
                    int n = Stream.Read(_bytes, read, size - read);
                    if (n == 0) throw new EndOfStreamException(Id);
                    read += n;
                }
                Buffer.BlockCopy(_bytes, 0, _samples, 0, size);
                int target = checked((int)(begin - now)) * 2;
                for (int i = 0; i < count; i++)
                {
                    long t = begin + i;
                    double gain = 1;
                    // Intentionally identical to the accepted lab's sampled Log1 approximation.
                    // This is an envelope, never a loudness normalization or added master gain.
                    if (FadeIn > 0 && t < Start + FadeIn) gain *= Curve(true, (t - Start) / (double)FadeIn);
                    if (t >= FadeOutAt) gain *= Curve(false, (t - FadeOutAt) / (double)FadeOut);
                    output[target + i * 2] += (float)(_samples[i * 2] * gain);
                    output[target + i * 2 + 1] += (float)(_samples[i * 2 + 1] * gain);
                }
            }
            public void Dispose() => Stream.Dispose();
        }

        public GwpMusicScore(string directory, Action<string>? log = null, int? seed = null)
        {
            _directory = directory;
            _data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "score.json")));
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _log = log;
            // Validate every asset before taking over native music, including late-game bridges.
            foreach (var p in ((JObject)_data["segments"]!).Properties())
            {
                var s = (JObject)p.Value;
                var file = new FileInfo(Path.Combine(directory, (string)s["file"]!));
                if (!file.Exists || file.Length != (long)s["frames"]! * 8)
                    throw new InvalidDataException("Missing/truncated music asset: " + file.FullName);
            }
            string id = Pick("intro");
            Add(id, Frames(.08) + Entry(id), "intro");
            _log?.Invoke("start intro " + id);
        }

        private JObject Playlist(string key) => (JObject)_data["playlists"]![key]!;
        private JObject Segment(string id) => (JObject)_data["segments"]![id]!;
        private long Entry(string id) => Ms((double)Segment(id)["entryMs"]!);
        internal static long Frames(double seconds) => (long)Math.Round(seconds * Rate);
        private static long Ms(double ms) => Frames(ms / 1000);
        public void Want(string state) { if (_wanted != "outro") _wanted = state; }
        public void Reinforcement() { if (_wanted != "outro" && _state != "outro") _reinforcement = true; }

        private string Pick(string key, string? forced = null)
        {
            if (forced != null) { _last[key] = forced; return forced; }
            var p = Playlist(key);
            var items = (JArray)p["items"]!;
            var ids = items.Select(x => (string)x["SegmentID"]!).ToList();
            _last.TryGetValue(key, out string? prev);
            string id;
            if ((string?)p["mode"] == "shuffle")
            {
                if (!_bags.TryGetValue(key, out var bag) || bag.Count == 0)
                {
                    bag = ids.ToList();
                    for (int i = bag.Count - 1; i > 0; i--) { int j = _random.Next(i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
                    if (bag.Count > 1 && bag[bag.Count - 1] == prev) (bag[0], bag[bag.Count - 1]) = (bag[bag.Count - 1], bag[0]);
                    _bags[key] = bag;
                }
                id = bag[bag.Count - 1]; bag.RemoveAt(bag.Count - 1);
            }
            else
            {
                int index = ids.IndexOf(prev ?? "");
                id = index >= 0 && (int)items[index]["Loop"]! == 0 ? ids[index] : ids[(index + 1) % ids.Count];
            }
            _last[key] = id;
            return id;
        }

        private Voice Add(string id, long anchor, string state, bool bridge = false, bool preEntry = true, long? position = null, long fade = 0)
        {
            JObject s = Segment(id);
            long entry = Entry(id);
            var v = new Voice { Id = id, State = state, Segment = s, Bridge = bridge, Anchor = anchor,
                Start = preEntry ? anchor - entry : anchor, Offset = preEntry ? 0 : entry, FadeIn = fade };
            if (position.HasValue)
            {
                v.Start = _frame + Frames(.025); v.Offset = position.Value;
                v.Anchor = v.Start - (position.Value - entry);
            }
            v.ControlAt = position.HasValue ? v.Start : anchor;
            v.Exit = v.Anchor + Ms((double)s["exitMs"]!) - entry;
            v.End = v.Start + (long)s["frames"]! - v.Offset;
            v.Stream = new FileStream(Path.Combine(_directory, (string)s["file"]!), FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            _voices.Add(v);
            return v;
        }

        private Voice? Current()
        {
            Voice? current = null, first = null;
            foreach (Voice v in _voices)
            {
                if (v.Bridge || v.Stop <= _frame) continue;
                if (first == null || v.ControlAt < first.ControlAt) first = v;
                if (v.Start < v.Stop && v.End > _frame && v.ControlAt <= _frame
                    && (current == null || v.ControlAt > current.ControlAt)) current = v;
            }
            return current ?? first;
        }

        private static double[] BuildCurve(bool up)
        {
            var curve = new double[128];
            for (int i = 0; i < curve.Length; i++)
                curve[i] = up ? Math.Log10(1 + 9 * i / 127.0) : Math.Log10(10 - 9 * i / 127.0);
            return curve;
        }

        internal static double Curve(bool up, double x)
        {
            x = Math.Max(0, Math.Min(1, x));
            double p = x * 127;
            int lo = (int)Math.Floor(p), hi = Math.Min(127, lo + 1);
            double[] curve = up ? FadeUp : FadeDown;
            double a = curve[lo], b = curve[hi];
            return a + (b - a) * (p - lo);
        }

        private void CancelFuture(Voice current)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice v = _voices[i];
                if (v == current || v.ControlAt <= current.ControlAt || v.ControlAt <= _frame) continue;
                v.Dispose(); _voices.RemoveAt(i);
            }
        }

        private bool HasSuccessor(Voice current)
        {
            foreach (Voice v in _voices)
                if (!v.Bridge && v != current && v.ControlAt > current.ControlAt && v.Stop > _frame) return true;
            return false;
        }

        internal static long Sync(long anchor, long earliest, int kind, double beat, int beats, long exit)
        {
            if (kind == 0) return earliest;
            if (kind == 4 || kind == 7) return Math.Max(earliest, exit);
            double step = Rate * beat * (kind == 2 ? beats : 1);
            return anchor + (long)Math.Round(Math.Ceiling((earliest - anchor) / step - 1e-9) * step);
        }

        private bool Change(Voice c, string state)
        {
            string playlistId = (string)Playlist(c.State)["id"]!, dest = (string)Playlist(state)["id"]!;
            bool Match(JToken list, params string[] ids) => list.Any(x => (string?)x == "-1" || ids.Contains((string)x!));
            JObject rule = ((JArray)_data["rules"]!).Reverse().Cast<JObject>().First(r => Match(r["src"]!, c.Id, playlistId) && Match(r["dst"]!, dest));
            var sr = rule["AkMusicTransSrcRule"]!; var dr = rule["AkMusicTransDstRule"]!;
            string? forced = null;
            if ((long)dr["uJumpToID"]! != 0)
                forced = (string?)Playlist(state)["items"]!.First(x => (long)x["playlistItemID"]! == (long)dr["uJumpToID"]!)["SegmentID"];
            bool sameTime = (int)dr["eEntryType"]! == 1;
            string? bridge = (string?)rule["AkMusicTransitionObject"]?["segmentID"];
            bool preEntry = (int)dr["bPlayPreEntry"]! != 0;
            string id = Pick(state, forced);
            long pre = preEntry ? Entry(id) : 0;
            if (bridge != null) pre = Math.Max(pre, Entry(bridge));
            var meter = c.Segment["meter"]!;
            long earliest = _frame + Frames(.05) + pre;
            if (c.State == "redraw" && state != "outro") earliest = Math.Max(earliest, _redrawUntil);
            long at = Sync(c.Anchor, earliest, (int)sr["eSyncType"]!, 60 / (double)meter["tempo"]! * 4 / (int)meter["beatValue"]!, (int)meter["beats"]!, c.Exit);
            if (!sameTime && at > c.Exit + Frames(.005)) { _retryAt = c.Exit + Frames(.05); return false; }
            CancelFuture(c);
            Voice next;
            if (sameTime)
            {
                at = _frame + Frames(.025);
                long position = at - c.Anchor + Entry(c.Id);
                next = Add(id, 0, state, position: position, fade: Ms((double)dr["transitionTime"]!));
                c.FadeOutAt = at; c.FadeOut = Ms((double)sr["transitionTime"]!); c.Stop = at + c.FadeOut;
                _pendingUntil = at + Math.Max(c.FadeOut, next.FadeIn);
            }
            else
            {
                c.Stop = (int)sr["bPlayPostExit"]! != 0 && Math.Abs(at - c.Exit) <= Frames(.005) ? c.End : at;
                next = Add(id, at, state, preEntry: preEntry);
                if (bridge != null) Add(bridge, at, state, bridge: true);
                _pendingUntil = at;
            }
            if (state == "redraw") { _reinforcement = false; _redrawUntil = next.Exit; }
            _state = state;
            _log?.Invoke($"rule={rule["index"]} {c.State}/{c.Id}->{state}/{id} bridge={bridge ?? "none"} join={at / (double)Rate:F3} sameTime={sameTime}");
            return true;
        }

        public void Render(float[] output, int frames)
        {
            Array.Clear(output, 0, frames * 2);
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice v = _voices[i];
                if (Math.Min(v.End, v.Stop) > _frame) continue;
                v.Dispose(); _voices.RemoveAt(i);
            }
            Voice? current = Current();
            if (current != null && _state != "outro")
            {
                string desired = _wanted == "outro" ? "outro" : _reinforcement ? "redraw" : _wanted;
                bool ending = desired == "outro";
                if (desired != _state && (ending || (_frame >= _pendingUntil && _frame >= _retryAt && (_state != "redraw" || _frame >= _redrawUntil - Frames(8)))))
                    Change(current, desired);
                // A repeated arrival during redraw extends the same loop, never layers another one.
                if (_reinforcement && _state == "redraw" && _frame >= _pendingUntil)
                { _reinforcement = false; _redrawUntil = Math.Max(_redrawUntil, current.Exit); }
                current = Current();
                if (current != null && _state != "outro" && current.State != "outro" && current.Exit - _frame <= Frames(8)
                    && !HasSuccessor(current))
                {
                    string id = Pick(current.State);
                    Add(id, current.Exit, current.State);
                    _log?.Invoke($"continue {current.State}/{id} join={current.Exit / (double)Rate:F3}");
                }
            }
            foreach (var v in _voices) v.Mix(output, frames, _frame);
            _frame += frames;
        }
        public void Dispose() { foreach (var v in _voices) v.Dispose(); _voices.Clear(); }
    }
}
