using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using GreyWardenPolicePurity;

internal static class Program
{
    static int Checks;
    static void Assert(bool ok, string name) { Checks++; if (!ok) throw new Exception(name); }
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(obj, value);
    static List<GwpMusicScore.Voice> Voices(GwpMusicScore s) => (List<GwpMusicScore.Voice>)typeof(GwpMusicScore).GetField("_voices", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s)!;
    static void Render(GwpMusicScore s, double seconds)
    {
        var samples=new float[1920];
        for(int i=0;i<(int)(seconds/.02);i++) { s.Render(samples,960); Assert(samples.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)),"finite PCM"); Assert(s.VoiceCount<=7,"bounded overlapping voices"); }
    }
    static void Main(string[] args)
    {
        string dir=Path.GetFullPath(args[0]); var data=JObject.Parse(File.ReadAllText(Path.Combine(dir,"score.json")));
        if (args.Contains("--fingerprint")) { Fingerprint(dir, args.Contains("--dump") ? args[Array.IndexOf(args, "--dump") + 1] : null); return; }
        if (args.Contains("--compare-old")) { CompareOld(dir, args[Array.IndexOf(args, "--compare-old") + 1]); return; }
        var playlists=(JObject)data["playlists"]!; var segments=(JObject)data["segments"]!;
        // Verify every explicit transition and the fallback against the source bank manifest.
        foreach(JObject rule in (JArray)data["rules"]!)
        {
            int index=(int)rule["index"]!;
            string src=(string)rule["src"]![0]!, dst=(string)rule["dst"]![0]!;
            string state, id, target;
            if(src=="-1") { state="intro";id=(string)playlists[state]!["items"]![0]!["SegmentID"]!;target="outro"; }
            else
            {
                var sourcePlaylist=playlists.Properties().FirstOrDefault(p=>(string?)p.Value["id"]==src);
                if(sourcePlaylist!=null){state=sourcePlaylist.Name;id=(string)sourcePlaylist.Value["items"]![0]!["SegmentID"]!;}
                else {id=src;state=playlists.Properties().First(p=>p.Value["items"]!.Any(i=>(string?)i["SegmentID"]==id)).Name;}
                target=playlists.Properties().First(p=>(string?)p.Value["id"]==dst).Name;
            }
            var logs=new List<string>(); using(var s=new GwpMusicScore(dir,logs.Add,17))
            {
                foreach(var v in Voices(s))v.Dispose(); Voices(s).Clear();
                long anchor=GwpMusicScore.Frames((double)segments[id]!["entryMs"]!/1000);
                typeof(GwpMusicScore).GetMethod("Add",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,new object?[]{id,anchor,state,false,true,null,0L});
                Set(s,"_state",state);Set(s,"_wanted",target);
                Render(s,20);
                var transition=logs.FirstOrDefault(x=>x.StartsWith("rule="));
                Assert(transition!=null&&transition.StartsWith("rule="+index+" "),$"rule {index} actual {transition}");
                string? bridge=(string?)rule["AkMusicTransitionObject"]?["segmentID"];
                Assert(transition!.Contains("bridge="+(bridge??"none")),"bridge identity");
                var dr=rule["AkMusicTransDstRule"]!;
                if((long)dr["uJumpToID"]! != 0)
                {
                    string paired=(string)playlists[target]!["items"]!.First(i=>(long)i["playlistItemID"]! ==(long)dr["uJumpToID"]!)["SegmentID"]!;
                    Assert(transition.Contains("->"+target+"/"+paired),"paired same-time destination");
                }
                Console.WriteLine("PASS "+transition);
            }
        }
        // Lossless sources: the runtime decoder reproduces the original 16-bit PCM bit for bit.
        var manifest=JObject.Parse(File.ReadAllText(Path.Combine(dir,"manifest.json")));
        foreach(var source in ((JObject)data["sources"]!).Properties())
        {
            var info=(JObject)source.Value;
            using var reader=new GwpFlacReader(Path.Combine(dir,(string)info["file"]!),info["offsets"]!.Select(x=>(long)x).ToArray(),(int)info["block"]!,(long)info["frames"]!);
            using var sha=SHA256.Create();var pcm=new byte[(int)info["block"]!*4];long total=0;
            for(long at=0;at<reader.Frames;at+=reader.Count)
            {
                Assert(reader.Seek(at)==at,"frame-aligned sequential decode");
                for(int i=0;i<reader.Count;i++){short l=(short)reader.Left[i],r=(short)reader.Right[i];Assert(l==reader.Left[i]&&r==reader.Right[i],"16-bit range");pcm[i*4]=(byte)l;pcm[i*4+1]=(byte)(l>>8);pcm[i*4+2]=(byte)r;pcm[i*4+3]=(byte)(r>>8);}
                sha.TransformBlock(pcm,0,reader.Count*4,pcm,0);total+=reader.Count;
            }
            sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
            Assert(total==reader.Frames,"decoded sample count");
            Assert(BitConverter.ToString(sha.Hash!).Replace("-","").ToLowerInvariant()==(string)manifest["sources"]![source.Name]!["pcmSha256"]!,"bit-exact FLAC decode "+source.Name);
            // Random access lands on the same samples as sequential decoding.
            long mid=reader.Frames/2+123;long first=reader.Seek(mid);int left=reader.Left[(int)(mid-first)];
            reader.Seek(0);Assert(reader.Seek(mid)==first&&reader.Left[(int)(mid-first)]==left,"seek repeats sequential decode");
        }
        Console.WriteLine("PASS "+((JObject)data["sources"]!).Count+" lossless sources decode bit-exact");
        // Wwise curve shapes reach their endpoints and move monotonically, as fades must.
        for(int shape=0;shape<=8;shape++)
        {
            double last=GwpMusicScore.Interpolate(0,0,1,shape);
            Assert(Math.Abs(last)<1e-3&&Math.Abs(GwpMusicScore.Interpolate(1,0,1,shape)-1)<1e-3,"curve endpoints "+shape);
            for(int i=1;i<=100;i++){double v=GwpMusicScore.Interpolate(i/100.0,0,1,shape);Assert(v>=last-1e-6,"monotonic curve "+shape);last=v;}
        }
        Assert(Math.Abs(GwpMusicScore.Interpolate(.5,0,1,2)-.625)<1e-12,"Wwise Log1 is t(3-t)/2, not log10");
        // Clip automation: intro 02 fades to silence by its end with the bank's SineRecip curve.
        using(var s=new GwpMusicScore(dir,_=>{},1))
        {
            var intro=(GwpMusicScore.Clip[])Invoke(s,"Clips",segments["749858897"]!);var clip=intro.Single(c=>c.Envelopes.Length>0);
            var fade=clip.Envelopes.Single(); var from=fade.Time[1];var to=fade.Time[2];
            Assert(fade.At(1)==1&&Math.Abs(fade.At(from)-1)<1e-4&&Math.Abs(fade.At(to))<1e-3,"fade-out holds, then reaches silence");
            Assert(Math.Abs(fade.At((from+to)/2)-Math.Cos(Math.PI/4))<1e-4,"SineRecip midpoint is the constant-power cosine");
            foreach(var c in intro)c.Dispose();
            var low=(GwpMusicScore.Clip[])Invoke(s,"Clips",segments["647197112"]!);var dip=low.Single(c=>c.Envelopes.Length>0).Envelopes.Single();
            Assert(dip.Volume&&dip.At(dip.Time[2])==1+dip.Value[2]&&Math.Abs(dip.Value[2]+.247)<1e-3,"volume automation stores gain minus one");
            foreach(var c in low)c.Dispose();
        }
        // A change during the middle of a phrase must seek, not restart the paired track.
        using(var s=new GwpMusicScore(dir,_=>{},17))
        {
            foreach(var v in Voices(s))v.Dispose();Voices(s).Clear();
            const string id="647197112";
            long anchor=GwpMusicScore.Frames((double)segments[id]!["entryMs"]!/1000);
            typeof(GwpMusicScore).GetMethod("Add",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,new object?[]{id,anchor,"low",false,true,null,0L});
            Set(s,"_state","low");Set(s,"_wanted","low");Render(s,15);
            s.Want("medium");var b=new float[1920];s.Render(b,960);
            var dest=Voices(s).Single(v=>v.State=="medium");
            Assert(dest.Id=="317591648"&&dest.Offset==GwpMusicScore.Frames(15.025),"mid-phrase paired seek at 15.025s");
            Render(s,.5);long now=s.Frame;
            var old=Voices(s).Single(v=>v.State=="low");
            // Each voice's unfaded contribution, then the rule's Log1 curve evaluated independently.
            float Raw(GwpMusicScore.Voice v)
            {var buffer=new float[2];long fi=v.FadeIn,fo=v.FadeOutAt;v.FadeIn=0;v.FadeOutAt=long.MaxValue;v.Mix(buffer,1,now);v.FadeIn=fi;v.FadeOutAt=fo;return buffer[0];}
            double fraction=(now-dest.Start)/(double)dest.FadeIn;
            double up=fraction*(3-fraction)/2;
            Assert(dest.FadeInShape==2&&old.FadeOutShape==2,"crossfade uses the rule's Log1 curve");
            float expected=(float)(Raw(old)*(1-up))+(float)(Raw(dest)*up);
            s.Render(b,960);Assert(Math.Abs(b[0]-expected)<1e-6,"mid-phrase sample equals Wwise Log1 crossfade");
            Render(s,3);Assert(Voices(s).All(v=>v.Id!=id),"crossfade releases source voice");
        }
        // Deployment loops intro 02 indefinitely; both sides' repeated waves are serialized.
        var timeline=new List<string>();using(var s=new GwpMusicScore(dir,timeline.Add,43))
        {
            Render(s,40);Assert(s.State=="intro","deployment holds intro");
            s.Want("low");Render(s,16);s.Want("medium");Render(s,4);s.Want("high");Render(s,7);
            s.Reinforcement();s.Want("low");Render(s,5);Assert(s.State=="redraw","actual reinforcement wins tier");
            s.Reinforcement();Render(s,25);Assert(s.State=="low","redraw returns to latest tier");
            s.Want("high");Render(s,.02);s.Want("outro");Render(s,20);Assert(s.State=="outro"&&s.VoiceCount==0,"ending cancels pending switch and plays once");
            s.Reinforcement();s.Want("low");Render(s,10);Assert(s.VoiceCount==0,"outro terminal");
        }
        File.WriteAllLines("build-check/music-rebuild/timeline.txt",timeline);
        LimiterTests(dir);
        BattlePolicyTests();
        // Real Windows device exercise: volume stays ZERO from before the first submitted buffer.
        using(var output=new GwpMusicOutput(dir))
        {
            output.Volume=0;output.Paused=false;Thread.Sleep(300);output.Paused=true;Thread.Sleep(80);
            output.Want("high");output.Paused=false;Thread.Sleep(200);output.Paused=true;
            Assert(output.Failure==null,"muted device open/write/pause/resume");
        }
        Console.WriteLine($"PASS {Checks} assertions; 35 rules; lossless sources; Wwise curves; dynamic events; real device MUTED lifecycle");
    }

    static object Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args)!;

    // One-off check against the retired pre-rendered segments (b68b43c): with clip automation
    // removed, the runtime mix must place every clip where the offline render did.
    private static void CompareOld(string dir,string oldDir)
    {
        var data=JObject.Parse(File.ReadAllText(Path.Combine(dir,"score.json")));
        using var score=new GwpMusicScore(dir,_=>{},1);
        double worst=0;
        foreach(var p in ((JObject)data["segments"]!).Properties())
        {
            var clips=(GwpMusicScore.Clip[])Invoke(score,"Clips",p.Value);
            foreach(var c in clips)c.Envelopes=Array.Empty<GwpMusicScore.Envelope>();
            byte[] raw=File.ReadAllBytes(Path.Combine(oldDir,p.Name+".pcm"));var old=new float[raw.Length/4];Buffer.BlockCopy(raw,0,old,0,raw.Length);
            long frames=(long)p.Value["frames"]!;Assert(old.Length==frames*2,"segment length "+p.Name);
            var buffer=new float[1920];var fade=Enumerable.Repeat(1.0,960).ToArray();double segmentWorst=0;
            for(long at=0;at<frames;at+=960)
            {
                int n=(int)Math.Min(960,frames-at);Array.Clear(buffer,0,1920);
                foreach(var c in clips)c.Mix(buffer,0,at,n,fade);
                for(int i=0;i<n*2;i++)segmentWorst=Math.Max(segmentWorst,Math.Abs(buffer[i]-old[at*2+i]));
            }
            foreach(var c in clips)c.Dispose();
            worst=Math.Max(worst,segmentWorst);
            Console.WriteLine($"{p.Name} {(string)p.Value["name"]!} clips={clips.Length} maxDiff={segmentWorst:E2}");
        }
        Assert(worst<2e-6,"runtime clip placement matches the offline render");
        Console.WriteLine($"PASS {((JObject)data["segments"]!).Count} segments; worst sample difference {worst:E2}");
    }

    // Offline comparison of the complete PCM and transition schedule before
    // and after renderer optimization; this never opens an audio device.
    private static void Fingerprint(string dir, string? dump)
    {
        using var hash = SHA256.Create();
        var samples = new float[1920]; var bytes = new byte[7680];
        var logs = new List<string>();
        using var score = new GwpMusicScore(dir, logs.Add, 43);
        using var raw = dump == null ? null : File.Create(dump); // float32 stereo 48 kHz, for loudness measurement
        for (int i = 0; i < 16000; i++)
        {
            if (i == 2000) score.Want("low");
            if (i == 3200) score.Want("medium");
            if (i == 3500) score.Want("low");
            if (i == 4200) score.Want("high");
            if (i == 5000 || i == 5150 || i == 5300) score.Reinforcement();
            if (i == 6200) score.Want("medium");
            if (i == 8000) score.Want("high");
            if (i == 10000) score.Want("low");
            if (i == 14000) score.Want("outro");
            score.Render(samples, 960);
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            raw?.Write(bytes, 0, bytes.Length);
        }
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        Console.WriteLine("PCM_SHA256=" + BitConverter.ToString(hash.Hash!).Replace("-", ""));
        foreach (string line in logs) Console.WriteLine(line);
    }

    // The output limiter: below the threshold it is a pure 10 ms delay; above it nothing escapes.
    private static void LimiterTests(string dir)
    {
        var quiet=new GwpMusicLimiter();var rng=new Random(5);var input=new List<float>();var output=new List<float>();
        for(int b=0;b<20;b++){var buf=new float[1920];for(int i=0;i<1920;i++)buf[i]=(float)(rng.NextDouble()*1.6-.8);input.AddRange(buf);quiet.Process(buf,960,1);output.AddRange(buf);}
        int lag=(GwpMusicLimiter.Lookahead+1)*2;
        for(int i=lag;i<output.Count;i++)Assert(output[i]==input[i-lag],"sub-threshold signal passes untouched, delayed by the look-ahead");
        // Worst cases: full-scale square bursts and single-sample spikes at +12 dB.
        var hot=new GwpMusicLimiter();double worst=0;
        for(int b=0;b<50;b++)
        {
            var buf=new float[1920];
            for(int i=0;i<960;i++){float v=(b%3==0)?((i/37)%2==0?1:-1):(i==(b*97)%960?1:(float)Math.Sin(i*.05)*.3f);buf[i*2]=v;buf[i*2+1]=-v;}
            hot.Process(buf,960,4);foreach(float v in buf)worst=Math.Max(worst,Math.Abs(v));
        }
        Assert(worst<=GwpMusicLimiter.Threshold*(1+1e-6),"no sample exceeds -1 dBFS at +12 dB");
        // The real score at the shipped +8 dB and full volume: bounded, and limiting stays rare.
        var mix=new GwpMusicLimiter();var gain=(float)Math.Pow(10,GwpMusicOutput.MakeupDb/20);long limited=0,total=0;double peak=0,deepest=1;
        using(var s=new GwpMusicScore(dir,_=>{},43))
        {
            var buf=new float[1920];
            for(int i=0;i<16000;i++)
            {
                if(i==2000)s.Want("low");if(i==3200)s.Want("medium");if(i==4200)s.Want("high");if(i==10000)s.Want("low");if(i==14000)s.Want("outro");
                s.Render(buf,960);mix.Process(buf,960,gain);
                foreach(float v in buf)peak=Math.Max(peak,Math.Abs(v));
                if(mix.Reduction<.999f)limited++;total++;deepest=Math.Min(deepest,mix.Reduction);
            }
        }
        Assert(peak<=GwpMusicLimiter.Threshold*(1+1e-6),"score at makeup gain never exceeds -1 dBFS");
        Console.WriteLine($"PASS limiter: +{GwpMusicOutput.MakeupDb} dB score peak {20*Math.Log10(peak):F2} dBFS; buffers touched {limited}/{total} ({100.0*limited/total:F2}%), deepest {20*Math.Log10(deepest):F2} dB");
        Assert(limited*50<total&&deepest>Math.Pow(10,-4/20.0),"limiting stays rare (<2% of buffers) and shallow (<4 dB) at full volume");
    }

    private static void BattlePolicyTests()
    {
        var lines = new List<string>();
        void Note(GwpMusicBattlePolicy p, float t, string scenario) => lines.Add($"{scenario} t={t:F1} tier={p.Tier} damage20={p.Damage20:F2} losses20={p.Casualties20:F2} exchange={p.Exchange:F3} attrition={p.Attrition:F3} tension={p.Tension:F3}");
        var p = new GwpMusicBattlePolicy(); p.Initialize(500, 500);
        for(int i=0;i<600;i++) p.Update(500,500,.5f);
        Assert(p.Tier=="low", "balanced waiting never climbs on a timer");
        // A lone arrow and scattered skirmish hits do not start medium.
        p.Initialize(800,800);
        for(int i=0;i<120;i++){if(i%10==0)p.RecordDamage(.5f);p.Update(800,800,.5f);}
        Assert(p.Tier=="low","skirmish trickle stays low");
        // A full clash from a standing start: medium first, high only after an established medium phase.
        p.Initialize(800,800);float mediumAt=-1,highAt=-1;
        for(int i=1;i<=400;i++)
        {
            p.RecordDamage(8);p.RecordCasualty(4);
            if(p.Update(800,800,.5f)){if(p.Tier=="medium")mediumAt=i*.5f;if(p.Tier=="high")highAt=i*.5f;}
            Assert(!(p.Tier=="high"&&mediumAt<0),"never jumps from low straight to high");
        }
        Assert(mediumAt>0&&highAt-mediumAt>=GwpMusicBattlePolicy.FirstClimb,$"first climax waits {GwpMusicBattlePolicy.FirstClimb}s in medium (medium {mediumAt}s, high {highAt}s)");
        Console.WriteLine($"PASS pacing: heavy clash medium at {mediumAt}s, high at {highAt}s");
        p.Initialize(50,1000);
        for(int i=0;i<120;i++) p.Update(50,1000,.5f);
        Assert(p.Tier=="low", "outnumbered but not fighting does not invent a climax");

        // Regression for the user's match: both sides lose strength at exactly the
        // same rate, ratio remains 1 throughout. This is a simulation, not a log replay.
        p.Initialize(500,500); var seen = new HashSet<string>{p.Tier}; float side=500;
        for(int i=1;i<=460;i++)
        {
            p.RecordDamage(1.5f); p.RecordCasualty(1.5f); side-=.75f;
            if(p.Update(side,side,.5f)) { Note(p,i*.5f,"equal-losses"); seen.Add(p.Tier); }
            Assert(p.Pressure==0,"symmetric regression keeps power ratio equal");
        }
        Assert(seen.Contains("medium")&&seen.Contains("high"),"equal losses produce contact and climax");
        for(int i=0;i<60;i++) if(p.Update(side,side,.5f)) Note(p,230+i*.5f,"disengage");
        Assert(p.Tier=="low"&&p.Tension==0,"accumulated casualties do not lock high after disengagement");

        // Full-capacity reinforcement battles keep live counts constant. Recent
        // damage/deaths still count; arriving fresh soldiers never reset tempo.
        p.Initialize(500,500);
        // Long enough for the established-medium wait before the first climax.
        for(int i=0;i<160;i++)
        { p.RecordDamage(2.5f);p.RecordCasualty(2);p.AddReinforcementPower(2);p.Update(500,500,.5f); }
        Assert(p.Tier=="high","constant active counts with reinforcements still climax");
        Note(p,80,"continuous-replacement");
        for(int i=0;i<100;i++) {p.RecordDamage(.05f);p.Update(500,500,.5f);}
        Assert(p.Tier=="medium","reduced but continuing contact drops high to medium");
        for(int i=0;i<60;i++) p.Update(500,500,.5f);
        Assert(p.Tier=="low","quiet can return to low");

        p.Initialize(500,500);p.RecordDamage(1);
        bool falseClimax=false;
        for(int i=0;i<120;i++){p.Update(500,500,.5f);falseClimax|=p.Tier=="high";}
        Assert(!falseClimax&&p.Tier=="low","one arrow does not generate a climax or permanent contact");

        // The late-game failure: historical casualties and numerical pressure
        // must not keep high alive when actual exchange has become weak.
        p.Initialize(100,900);p.RecordCasualty(500);
        for(int i=0;i<120;i++){p.RecordDamage(3);p.Update(100,900,.5f);}
        Assert(p.Tier=="high"&&p.Attrition==1&&p.Pressure==1,"late-game regression begins at high with maximum historical pressure");
        for(int i=0;i<100;i++){p.RecordDamage(.75f);p.Update(100,900,.5f);}
        Assert(p.Tier=="medium"&&Math.Abs(p.Exchange-30/(1000*GwpMusicBattlePolicy.DamageScale))<.01f,"weak sustained fighting drops high despite maximum casualties and disadvantage");
        Note(p,110,"late-game-weak-contact");
        for(int i=0;i<60;i++){p.RecordDamage(3);p.Update(100,900,.5f);}
        Assert(p.Tier=="high","renewed sustained fighting can climb again");

        // A short burst remains in the old 20-second window. Waiting for the
        // tier hold must not turn that old burst into a later climax.
        p.Initialize(500,500);p.RecordDamage(100);falseClimax=false;
        for(int i=0;i<40;i++)
        {
            if(i==16) p.RecordDamage(.01f); // fresh timestamp, negligible action
            p.Update(500,500,.5f);falseClimax|=p.Tier=="high";
        }
        Assert(!falseClimax,"stale burst cannot rise late, even when one tiny new hit refreshes contact");
        p.Initialize(500,500);
        Assert(p.RecentExchange==0&&p.Damage20==0,"new battle clears both activity windows");

        // Normalized power makes identical percentage exchanges scale across battle sizes.
        var small=new GwpMusicBattlePolicy();var large=new GwpMusicBattlePolicy();
        small.Initialize(50,50);large.Initialize(5000,5000);
        for(int i=0;i<100;i++)
        {
            small.RecordDamage(.25f);large.RecordDamage(25);
            small.Update(50,50,.5f);large.Update(5000,5000,.5f);
            Assert(small.Tier==large.Tier&&Math.Abs(small.Tension-large.Tension)<1e-5,"battle size invariance");
        }
        // A held threshold cannot rapidly flip on alternating samples.
        p.Initialize(500,500);int changes=0;float last=0;
        for(int i=1;i<=180;i++)
        {
            p.RecordDamage(i%2==0?2f:.5f);
            if(p.Update(500,500,.5f)) { if(changes>0) Assert(i*.5f-last>=12,"minimum audible tier hold");last=i*.5f;changes++; }
        }
        Assert(changes<8,"no rapid threshold oscillation");
        File.WriteAllLines("build-check/music-rebuild/battle-policy.txt",lines);
        foreach(string line in lines)Console.WriteLine("PASS "+line);
    }
}

