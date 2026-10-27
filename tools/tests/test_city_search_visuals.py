"""Execute the production visual-address probe without loading the game engine."""
import unittest

from test_city_search import read
from test_city_search_flow import run


class CitySearchVisualTests(unittest.TestCase):
    def test_visual_identity_changes_and_native_free_removal(self):
        source = read('GwpCitySearchMission.VisualDiagnostics.cs')
        code = '#define GWP_DIAGNOSTICS\n' + source + r'''
namespace HarmonyLib{
 class HarmonyPatch:System.Attribute{public HarmonyPatch(System.Type type,string method){}}
 class Harmony{public static int Calls;public Harmony(string id){}public void Patch(object target,object prefix,object postfix){Calls++;}}
 class HarmonyMethod{public HarmonyMethod(System.Type type,string method){}}
 static class AccessTools{public static object Method(System.Type type,string method)=>new();}
}
namespace TaleWorlds.MountAndBlade{
 class Character{public string StringId="troop",Name="person";}
 struct Entity{public System.UIntPtr Pointer;}
 class Visuals{public System.UIntPtr Pointer;public Entity Entity;public int Reads;public Entity GetWeakEntity(){Reads++;return Entity;}}
 class Agent{
  public bool Active=true,IsHuman=true;public int Index,ActiveReads;public Visuals? Visual;public Character Data=new();
  void RequireActive(){if(!Active)throw new System.Exception("removed native object accessed");}
  public bool IsActive(){ActiveReads++;return Active;}
  public void FadeOut(bool instantly,bool mount){}
  public Visuals? AgentVisuals{get{RequireActive();return Visual;}}
  public Character Character{get{RequireActive();return Data;}}
 }
 class MissionLogic{public virtual void OnAgentDeleted(Agent agent){}}
}
namespace GreyWardenPolicePurity{
 using System;using System.Collections.Generic;using System.Linq;using TaleWorlds.MountAndBlade;
 class Mission{public float CurrentTime;public List<Agent> Agents=new();}
 static class SubModule{public const string HarmonyId="test";}
 static class GwpCitySearchNativeVisualFix{
  public static long EmptyUpdateCount;public static ulong Visual;public static bool Stable=true;
  public static bool TryReadLastUpdate(out ulong v,out long c){v=Visual;c=EmptyUpdateCount;return Stable;}
  public static void WriteDiagnostics(){}
 }
 static class GwpAiDiagnostics{public static List<(string Stage,string Text)> Lines=new();public static void WriteCitySearch(string s,string d)=>Lines.Add((s,d));}
 internal sealed partial class GwpCitySearchMissionController:MissionLogic{
  public static GwpCitySearchMissionController? Current;public Mission Mission=new();string _place="Escape";
  public void Build(Agent a)=>CaptureSearchVisual(a,"build");
  public void Tick()=>TickSearchVisuals();public void Remove(Agent a)=>WriteSearchVisualRemoval(a,"Routed");
 }
 static class Program{
  static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
  static Agent New(int index,ulong visual,ulong entity)=>new(){Index=index,Visual=new(){Pointer=new UIntPtr(visual),Entity=new(){Pointer=new UIntPtr(entity)}}};
  static void Main(){
   var c=new GwpCitySearchMissionController();var player=New(1,0x100,0x1000);var defender=New(2,0x200,0x2000);var mount=New(3,0x300,0x3000);mount.IsHuman=false;
   foreach(var a in new[]{player,defender,mount}){c.Mission.Agents.Add(a);c.Build(a);}
   Check(GwpAiDiagnostics.Lines.Count==3,"catalog includes player defenders and mounts");
   Check(GwpAiDiagnostics.Lines.Any(l=>l.Text.Contains("index=3")&&l.Text.Contains("human=False")&&l.Text.Contains("visual=0x300")&&l.Text.Contains("entity=0x3000")),"native addresses map to the saved actor identity");
   c.Tick();Check(GwpAiDiagnostics.Lines.Count==3,"unchanged addresses do not create repeated log lines");
   int reads=defender.Visual!.Reads;c.Mission.CurrentTime=0.2f;c.Tick();Check(defender.Visual.Reads==reads,"native reads are throttled to twice a second");
   defender.Visual.Entity=new(){Pointer=new UIntPtr(0)};c.Mission.CurrentTime=0.5f;c.Tick();
   Check(GwpAiDiagnostics.Lines.Count==4&&GwpAiDiagnostics.Lines.Last().Text.Contains("entity=0x0")&&GwpAiDiagnostics.Lines.Last().Text.Contains("previousEntity=0x2000"),"entity invalidation retains the prior pointer");
   defender.Active=false;c.Remove(defender);c.OnAgentDeleted(defender);
   Check(GwpAiDiagnostics.Lines.TakeLast(2).All(l=>l.Stage=="VISUAL_REMOVED"&&l.Text.Contains("index=2")&&l.Text.Contains("visual=0x200")),"removed and deleted callbacks use only saved data");
   Check(GwpAiDiagnostics.Lines.Last().Text.Contains("state=Deleted"),"native deletion is distinguished from routing");
   var replacement=New(2,0x400,0x4000);c.Mission.Agents.Add(replacement);c.Build(replacement);
   Check(GwpAiDiagnostics.Lines.Last().Text.Contains("visual=0x400"),"reused agent index does not overwrite a different agent instance");
   player.Visual=null;c.Mission.CurrentTime=1;c.Tick();Check(GwpAiDiagnostics.Lines.Last().Text.Contains("index=1")&&GwpAiDiagnostics.Lines.Last().Text.Contains("visual=0x0"),"missing visuals are captured without reading a null native pointer");
   var missing=New(5,0,0);c.Build(missing);Check(missing.Visual!.Reads==0,"zero visual pointer never reaches native entity getter");
   var uncatalogued=New(6,0x600,0x6000);uncatalogued.Active=false;int before=GwpAiDiagnostics.Lines.Count;c.Remove(uncatalogued);c.OnAgentDeleted(uncatalogued);c.Build(uncatalogued);
   Check(GwpAiDiagnostics.Lines.Count==before,"unknown removed actors never trigger native access");
   Check(GwpAiDiagnostics.Lines.Count(l=>l.Stage=="VISUAL_STATE"&&l.Text.Contains("index=2"))==3,"newly spawned reinforcements are logged immediately");
   GwpCitySearchMissionController.Current=c;
   GwpCitySearchFadeDiagnosticsPatch.EnsureApplied();GwpCitySearchFadeDiagnosticsPatch.EnsureApplied();Check(HarmonyLib.Harmony.Calls==1,"late fade diagnostics install only once");
   var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
   var prefix=typeof(GwpCitySearchFadeDiagnosticsPatch).GetMethod("BeforeFade",flags)!;
   var postfix=typeof(GwpCitySearchFadeDiagnosticsPatch).GetMethod("AfterFade",flags)!;
   int activeReads=defender.ActiveReads;object?[] state={defender,null};prefix.Invoke(null,state);GwpCitySearchNativeVisualFix.EmptyUpdateCount=1;GwpCitySearchNativeVisualFix.Visual=0x200;postfix.Invoke(null,new[]{state[1]});
   Check(GwpAiDiagnostics.Lines.Last().Text.Contains("matchingEmptyUpdateDuringCall=True")&&GwpAiDiagnostics.Lines.Last().Text.Contains("caller="),"matching native update during fade records its managed caller");
   Check(GwpAiDiagnostics.Lines.Any(l=>l.Stage=="VISUAL_FADE_BEGIN"&&l.Text.Contains("index=2")&&l.Text.Contains("removalSeen=Deleted"))&&defender.ActiveReads==activeReads,"repeated fade of removed actor uses cached removal state without even reading native IsActive");
   prefix.Invoke(null,state);GwpCitySearchNativeVisualFix.EmptyUpdateCount=2;GwpCitySearchNativeVisualFix.Visual=0x400;postfix.Invoke(null,new[]{state[1]});
   Check(GwpAiDiagnostics.Lines.Last().Text.Contains("matchingEmptyUpdateDuringCall=False")&&!GwpAiDiagnostics.Lines.Last().Text.Contains("caller="),"another actor's update is not attributed to this fade call");
   prefix.Invoke(null,state);GwpCitySearchNativeVisualFix.Visual=0x200;GwpCitySearchNativeVisualFix.EmptyUpdateCount=3;GwpCitySearchNativeVisualFix.Stable=false;postfix.Invoke(null,new[]{state[1]});
   Check(GwpAiDiagnostics.Lines.Last().Text.Contains("sampleStable=False")&&GwpAiDiagnostics.Lines.Last().Text.Contains("matchingEmptyUpdateDuringCall=False"),"unfinished native sample never becomes overlap evidence");
   int lines=GwpAiDiagnostics.Lines.Count;object?[] unknown={uncatalogued,null};prefix.Invoke(null,unknown);postfix.Invoke(null,new[]{unknown[1]});
   Check(GwpAiDiagnostics.Lines.Count==lines,"unknown agents do not generate fade records");
   Console.WriteLine("CITY_SEARCH_VISUAL_ASSERTIONS="+n);
  }
 }
}
'''
        run(code)


if __name__ == '__main__':
    unittest.main()
