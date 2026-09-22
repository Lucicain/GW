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
        if (args.Contains("--fingerprint")) { Fingerprint(dir); return; }
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
        // Neutral source equality: unity output preserves every sample, including pre-entry.
        using(var s=new GwpMusicScore(dir,_=>{},1))
        {
            var v=Voices(s)[0];byte[] raw=File.ReadAllBytes(Path.Combine(dir,(string)v.Segment["file"]!));
            var source=new float[raw.Length/4];Buffer.BlockCopy(raw,0,source,0,raw.Length); var b=new float[1920];
            long begin=v.Start;
            for(int j=0;j<100;j++) {long frame=s.Frame;s.Render(b,960);for(int i=0;i<960;i++){long pos=frame+i-begin;float expected=pos<0?0:source[pos*2];Assert(b[i*2]==expected,"unity PCM equality");}}
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
            float Sample(GwpMusicScore.Voice v,long frame)
            {using(var f=File.OpenRead(Path.Combine(dir,(string)v.Segment["file"]!))){f.Position=(v.Offset+frame-v.Start)*8;var bytes=new byte[4];f.Read(bytes,0,4);return BitConverter.ToSingle(bytes,0);}}
            double fraction=(now-dest.Start)/(double)dest.FadeIn;
            // Independently evaluate the lab's 128-point envelope interpolation.
            double Gain(bool up){double pos=fraction*127;int lo=(int)pos;double F(int i)=>up?Math.Log10(1+9*i/127.0):Math.Log10(10-9*i/127.0);return F(lo)+(F(lo+1)-F(lo))*(pos-lo);}
            float expected=(float)(Sample(old,now)*Gain(false))+(float)(Sample(dest,now)*Gain(true));
            s.Render(b,960);Assert(Math.Abs(b[0]-expected)<1e-7,"mid-phrase sample equals lab envelope mix");
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
        BattlePolicyTests();
        // Real Windows device exercise: volume stays ZERO from before the first submitted buffer.
        using(var output=new GwpMusicOutput(dir))
        {
            output.Volume=0;output.Paused=false;Thread.Sleep(300);output.Paused=true;Thread.Sleep(80);
            output.Want("high");output.Paused=false;Thread.Sleep(200);output.Paused=true;
            Assert(output.Failure==null,"muted device open/write/pause/resume");
        }
        Console.WriteLine($"PASS {Checks} assertions; 35 rules; PCM unity; dynamic events; real device MUTED lifecycle");
    }

    // Offline comparison of the complete PCM and transition schedule before
    // and after renderer optimization; this never opens an audio device.
    private static void Fingerprint(string dir)
    {
        using var hash = SHA256.Create();
        var samples = new float[1920]; var bytes = new byte[7680];
        var logs = new List<string>();
        using var score = new GwpMusicScore(dir, logs.Add, 43);
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
        }
        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        Console.WriteLine("PCM_SHA256=" + BitConverter.ToString(hash.Hash!).Replace("-", ""));
        foreach (string line in logs) Console.WriteLine(line);
    }

    private static void BattlePolicyTests()
    {
        var lines = new List<string>();
        void Note(GwpMusicBattlePolicy p, float t, string scenario) => lines.Add($"{scenario} t={t:F1} tier={p.Tier} damage20={p.Damage20:F2} losses20={p.Casualties20:F2} exchange={p.Exchange:F3} attrition={p.Attrition:F3} tension={p.Tension:F3}");
        var p = new GwpMusicBattlePolicy(); p.Initialize(500, 500);
        for(int i=0;i<600;i++) p.Update(500,500,.5f);
        Assert(p.Tier=="low", "balanced waiting never climbs on a timer");
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
        for(int i=0;i<100;i++)
        { p.RecordDamage(2.5f);p.RecordCasualty(2);p.AddReinforcementPower(2);p.Update(500,500,.5f); }
        Assert(p.Tier=="high","constant active counts with reinforcements still climax");
        Note(p,50,"continuous-replacement");
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
        for(int i=0;i<40;i++){p.RecordDamage(3);p.Update(100,900,.5f);}
        Assert(p.Tier=="high"&&p.Attrition==1&&p.Pressure==1,"late-game regression begins at high with maximum historical pressure");
        for(int i=0;i<100;i++){p.RecordDamage(.75f);p.Update(100,900,.5f);}
        Assert(p.Tier=="medium"&&p.Exchange>.29f&&p.Exchange<.31f,"weak sustained fighting drops high despite maximum casualties and disadvantage");
        Note(p,70,"late-game-weak-contact");
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

