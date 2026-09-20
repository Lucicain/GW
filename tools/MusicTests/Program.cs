using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        var p=new GwpMusicBattlePolicy();p.Initialize(.5f);Assert(p.Tier=="low","initial ratio");
        for(int i=0;i<20;i++)p.Update(2,.5f);Assert(p.Tier=="high","can rise");
        for(int i=0;i<20;i++)p.Update(.5f,.5f);Assert(p.Tier=="low","can fall");
        for(int i=0;i<30;i++)p.Update(i%2==0?.81f:.79f,.5f);Assert(p.Tier=="low","hysteresis avoids thrashing");
        // Real Windows device exercise: volume stays ZERO from before the first submitted buffer.
        using(var output=new GwpMusicOutput(dir))
        {
            output.Volume=0;output.Paused=false;Thread.Sleep(300);output.Paused=true;Thread.Sleep(80);
            output.Want("high");output.Paused=false;Thread.Sleep(200);output.Paused=true;
            Assert(output.Failure==null,"muted device open/write/pause/resume");
        }
        Console.WriteLine($"PASS {Checks} assertions; 35 rules; PCM unity; dynamic events; real device MUTED lifecycle");
    }
}

