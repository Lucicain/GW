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
        private readonly JObject _data;
        private readonly string _directory;
        private readonly Random _random;
        private readonly List<Voice> _voices = new();
        private readonly Dictionary<string, List<string>> _bags = new();
        private readonly Dictionary<string, string> _last = new();
        private readonly Action<string>? _log;
        private readonly Dictionary<string, long[]> _offsets = new();
        private long _frame, _pendingUntil, _retryAt, _redrawUntil;
        private string _state = "intro", _wanted = "intro";
        private bool _reinforcement;
        public long Frame => _frame;
        public string State => _state;
        public int VoiceCount => _voices.Count;

        // One source clip placed on a segment's timeline, with its track volume and clip automation.
        internal sealed class Clip : IDisposable
        {
            public long PlayAt, Begin, End;
            public double ZeroAt, Gain;
            public Envelope[] Envelopes = Array.Empty<Envelope>();
            public GwpFlacReader Reader = null!;
            public void Mix(float[] output, int target, long position, int count, double[] fade)
            {
                long from = Math.Max(position, Begin), to = Math.Min(position + count, End);
                for (long p = from; p < to;)
                {
                    long source = p - PlayAt, first = Reader.Seek(source);
                    int index = checked((int)(source - first)), n = (int)Math.Min(to - p, Reader.Count - index);
                    int k = checked((int)(p - position)), o = (target + k) * 2;
                    for (int i = 0; i < n; i++, k++, o += 2)
                    {
                        double gain = fade[k] * Gain;
                        foreach (Envelope e in Envelopes) gain *= e.At((p + i - ZeroAt) / Rate);
                        output[o] += (float)(Reader.Left[index + i] / 32768.0 * gain);
                        output[o + 1] += (float)(Reader.Right[index + i] / 32768.0 * gain);
                    }
                    p += n;
                }
            }
            public void Dispose() => Reader.Dispose();
        }

        // Wwise clip automation: points are (seconds from clip start, value, curve to the next point).
        // Volume automation stores linear gain minus one (Wwise ScalingFromLin_dB); fades store 0..1.
        internal sealed class Envelope
        {
            public double[] Time = null!, Value = null!;
            public int[] Shape = null!;
            public bool Volume;
            public double At(double seconds)
            {
                int last = Time.Length - 1;
                double v;
                if (seconds <= Time[0]) v = Value[0];
                else if (seconds >= Time[last]) v = Value[last];
                else
                {
                    int i = 0;
                    while (seconds >= Time[i + 1]) i++;
                    v = Interpolate((seconds - Time[i]) / (Time[i + 1] - Time[i]), Value[i], Value[i + 1], Shape[i]);
                }
                return Volume ? 1 + v : v;
            }
        }

        internal sealed class Voice : IDisposable
        {
            public string Id = "", State = "";
            public JObject Segment = null!;
            public bool Bridge;
            public long Anchor, ControlAt, Start, Offset, Exit, End, Stop = long.MaxValue;
            public long FadeIn, FadeOutAt = long.MaxValue, FadeOut;
            public int FadeInShape = 4, FadeOutShape = 4;
            public Clip[] Clips = Array.Empty<Clip>();
            private double[] _fade = new double[1024];
            public void Mix(float[] output, int frames, long now)
            {
                long begin = Math.Max(now, Start), end = Math.Min(now + frames, Math.Min(End, Stop));
                if (end <= begin) return;
                int count = checked((int)(end - begin));
                if (_fade.Length < count) _fade = new double[count];
                for (int i = 0; i < count; i++)
                {
                    long t = begin + i;
                    double gain = 1;
                    // Transition fades use the rule's own Wwise curve; never a loudness normalization.
                    if (FadeIn > 0 && t < Start + FadeIn) gain *= Fade(true, (t - Start) / (double)FadeIn, FadeInShape);
                    if (t >= FadeOutAt) gain *= Fade(false, (t - FadeOutAt) / (double)FadeOut, FadeOutShape);
                    _fade[i] = gain;
                }
                foreach (Clip c in Clips) c.Mix(output, checked((int)(begin - now)), Offset + begin - Start, count, _fade);
            }
            public void Dispose() { foreach (Clip c in Clips) c.Dispose(); }
        }

        public GwpMusicScore(string directory, Action<string>? log = null, int? seed = null)
        {
            _directory = directory;
            _data = JObject.Parse(File.ReadAllText(Path.Combine(directory, "score.json")));
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _log = log;
            // Validate every asset before taking over native music, including late-game bridges.
            foreach (var p in ((JObject)_data["sources"]!).Properties())
            {
                var src = (JObject)p.Value;
                var file = new FileInfo(Path.Combine(directory, (string)src["file"]!));
                if (!file.Exists || file.Length != (long)src["bytes"]!)
                    throw new InvalidDataException("Missing/truncated music asset: " + file.FullName);
                _offsets[p.Name] = src["offsets"]!.Select(x => (long)x).ToArray();
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
            v.Clips = Clips(s);
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

        private Clip[] Clips(JObject segment)
        {
            var clips = new List<Clip>();
            long frames = (long)segment["frames"]!;
            foreach (JObject track in (JArray)segment["tracks"]!)
            {
                double gain = Math.Pow(10, (double)track["volumeDb"]! / 20);
                var list = (JArray)track["clips"]!;
                for (int i = 0; i < list.Count; i++)
                {
                    JToken c = list[i];
                    double playAt = (double)c["fPlayAt"]!, begin = playAt + (double)c["fBeginTrimOffset"]!;
                    string id = (string)c["sourceID"]!;
                    var source = (JObject)_data["sources"]![id]!;
                    int index = i;
                    var clip = new Clip
                    {
                        PlayAt = Ms(playAt), Begin = Math.Max(0, Ms(begin)), ZeroAt = begin / 1000 * Rate, Gain = gain,
                        Envelopes = track["automation"]!.Where(x => (int)x["clip"]! == index).Select(x => new Envelope
                        {
                            Volume = (string)x["type"]! == "volume",
                            Time = x["points"]!.Select(q => (double)q[0]!).ToArray(),
                            Value = x["points"]!.Select(q => (double)q[1]!).ToArray(),
                            Shape = x["points"]!.Select(q => (int)q[2]!).ToArray(),
                        }).ToArray(),
                    };
                    clip.End = Math.Min(frames, Math.Min(Ms(playAt + (double)c["fSrcDuration"]! + (double)c["fEndTrimOffset"]!),
                        clip.PlayAt + (long)source["frames"]!));
                    clip.Reader = new GwpFlacReader(Path.Combine(_directory, (string)source["file"]!), _offsets[id], (int)source["block"]!, (long)source["frames"]!);
                    clips.Add(clip);
                }
            }
            return clips.ToArray();
        }

        // AkInterpolation::InterpolateNoCheck as the Wwise engine computes it (the engine's own
        // polynomial approximations, recovered by wwiser). Constant (9) holds the starting value.
        internal static double Interpolate(double t, double from, double to, int shape)
        {
            double a, b;
            switch (shape)
            {
                case 0: return (1 - t) * (1 - t) * (1 - t) * (from - to) + to; // Log3
                case 1: // Sine
                    a = 1.5707964 * t * (1.5707964 * t);
                    b = ((a * -0.00018363654 + 0.0083063254) * a + -0.16664828) * a + 0.9999966;
                    return b * (1.5707964 * t) * (to - from) + from;
                case 2: return (t - 3) * t * 0.5 * (from - to) + from; // Log1
                case 3: // InvSCurve
                    if (t > 0.5)
                    {
                        a = 3.1415927 - 3.1415927 * t;
                        b = (a * a * -0.00009181827 + 0.0041531627) * (a * a) + -0.083324142;
                        return (1 - (b * (a * a) + 0.4999983) * a) * (to - from) + from;
                    }
                    a = 3.1415927 * t * (3.1415927 * t);
                    b = (a * -0.00009181827 + 0.0041531627) * a + -0.083324142;
                    return (b * a + 0.4999983) * (3.1415927 * t) * (to - from) + from;
                case 5: // SCurve
                    a = 3.1415927 * t * (3.1415927 * t);
                    return (((a * 0.00048483399 + -0.01961384) * a + 0.24767479) * a + 0.00069670216) * (to - from) + from;
                case 6: return (t + 1) * t * 0.5 * (to - from) + from; // Exp1
                case 7: // SineRecip
                    a = 1.5707964 * t * (1.5707964 * t);
                    return (((a * -0.0012712094 + 0.04148775) * a + -0.49991244) * a + 0.99999332) * (from - to) + to;
                case 8: return t * t * t * (to - from) + from; // Exp3
                case 9: return from;
                default: return (to - from) * t + from; // Linear
            }
        }

        internal static double Fade(bool up, double x, int shape)
        {
            x = Math.Max(0, Math.Min(1, x));
            return up ? Interpolate(x, 0, 1, shape) : Interpolate(x, 1, 0, shape);
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
                next.FadeInShape = (int)dr["eFadeCurve"]!; c.FadeOutShape = (int)sr["eFadeCurve"]!;
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
