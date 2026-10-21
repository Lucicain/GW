"""Run production regional adapter and Harmony hooks against freshly decompiled native AI.

Only engine objects, path reachability and strength are controlled boundaries.
No game is launched and no native regional arithmetic is copied into production.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract
from test_native_regional_strength import HARNESS, GAME_BIN

MODULE = Path(__file__).resolve().parents[2] / "GreyWardenPolicePurity"

PRODUCTION_BOUNDARIES = r'''
static class GwpCommon {public static bool IsGreyWardenLordParty(MobileParty p)=>p.Id=="chief";}
static class PoliceEnforcementBehavior {
 internal static bool IncludePlayer;internal static MobileParty? CaseTarget;
 internal static bool TryGetNativeCaseTarget(MobileParty p,out MobileParty? target){target=CaseTarget??p.TargetParty;return GwpCommon.IsGreyWardenLordParty(p)&&target!=null;}
 internal static bool NativeCaseIncludesPlayer(MobileParty p)=>IncludePlayer;
}
class CrimeRecord {public MobileParty? Offender;}
internal static partial class GwpStrength {
 internal sealed class Threat {public float Strength,MaximumSpeed,JoiningRadius,ThreatRadius;public string FastestCombatGroupId="";public List<string> CombatGroups=new();}
 internal sealed class LocalBalance {public MobileParty Actor=null!,MovementTarget=null!;public float FriendlyLocalStrength,EnemyLocalStrength,Distance;public string NativeComparisons="",Reason="";public bool StrengthReady;}
 internal static float Party(MobileParty p)=>p.Party.EstimatedStrength;
 internal static float TheoreticalBaseSpeed(MobileParty p)=>p.LastCalculatedBaseSpeed;
 PRODUCTION_METHODS
}
[HarmonyPatch(typeof(DefaultMobilePartyAIModel), nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior))]
static class ReplayObserver {
 internal static AiBehavior LastBehavior;internal static MobileParty? LastTarget;
 [HarmonyPostfix,HarmonyPriority(Priority.Last)]
 static void Postfix(AiBehavior bestInitiativeBehavior,MobileParty bestInitiativeTargetParty){
  if(GwpStrength.IsNativePreview)return;LastBehavior=bestInitiativeBehavior;LastTarget=bestInitiativeTargetParty;
 }
}
static class AdapterReplay {
 static int Checks;static void Check(bool b,string s){if(!b)throw new Exception(s);Checks++;}
 static void Near(float x,float y,string s)=>Check(Math.Abs(x-y)<.002f,s+": "+x+" != "+y);
 static MobileParty New(string id,float x,float strength,Faction f){var p=new MobileParty{Id=id};p.Position.X=x;p.Party=new(){MobileParty=p,EstimatedStrength=strength,MapFaction=f};MobileParty.All.Add(p);return p;}
 static (DefaultMobilePartyAIModel,MobileParty,MobileParty,MobileParty) Setup(float d=1.43f,float supportX=4.5f){
  MobileParty.All.Clear();PoliceEnforcementBehavior.IncludePlayer=false;PoliceEnforcementBehavior.CaseTarget=null;MobileParty.MainParty=new(){IsActive=false};
  var own=new Faction{Id="warden"};var enemy=new Faction{Id="enemy"};own.Enemies.Add(enemy);enemy.Enemies.Add(own);
  var a=New("chief",-d,300,own);var t=New("offender",0,200,enemy);var r=New("enemy_support",supportX,600,enemy);
  a.TargetParty=t;var n=new DefaultMobilePartyAIModel();Campaign.Current=new();Campaign.Current.Models.MobilePartyAIModel=n;return(n,a,t,r);
 }
 static void Main(){
  new HarmonyLib.Harmony("gwp.native.region.replay").PatchAll();
  var(n,a,t,r)=Setup();var balance=GwpStrength.Compare(a,t);
  Check(!balance.StrengthReady,"separate stronger enemy blocks otherwise winning close attack");
  Check(balance.Reason=="native_region_disadvantage:enemy_support","blocking comparison names actual native enemy");
  Near(balance.FriendlyLocalStrength,300.01f,"native own sum captured");
  Near(balance.EnemyLocalStrength,690.01f,"native separate enemy sum includes native weighted offender reinforcement");
  n.GetBestInitiativeBehavior(a,out var behavior,out var target,out _,out _);
  Check(behavior!=AiBehavior.EngageParty||target!=t,"live initiative cannot retain case attack after later enemy comparison reveals disadvantage");
  Check(ReplayObserver.LastBehavior==behavior&&ReplayObserver.LastTarget==target,"diagnostic postfix captures final guarded behavior, not preview or pre-guard winner");
  Check(!GwpStrength.IsInNativeEvaluation(a)&&!GwpStrength.IsNativePreview,"native frame and preview cleaned after calls");
  (n,a,t,r)=Setup(2.51f,-8.5f);balance=GwpStrength.Compare(a,t);
  Check(!balance.StrengthReady,"stronger other lord outside old target six but in native source nine blocks declaration");
  n.GetBestInitiativeBehavior(a,out behavior,out target,out _,out _);
  Check(behavior==AiBehavior.FleeToPoint&&target==r,"native flee remains unchanged");
  (n,a,t,r)=Setup();r.Position.X=30;a.MapFaction.Enemies.Clear();t.MapFaction.Enemies.Clear();
  MobilePartyAi.DangerousPartiesAndTheirVecs.Clear();MobilePartyAi.DangerousPartiesAndTheirVecs.Add((7,new Vec2(2,3)));
  balance=GwpStrength.Compare(a,t);
  Check(balance.StrengthReady,"prospective neutral war uses same regional native arithmetic");
  Near(balance.FriendlyLocalStrength,300.01f,"prospective raw friendly sum");Near(balance.EnemyLocalStrength,200.01f,"prospective raw enemy sum");
  Check(!a.MapFaction.IsAtWarWith(t.MapFaction)&&!t.MapFaction.IsAtWarWith(a.MapFaction),"preview never changes diplomatic state");
  Check(MobilePartyAi.DangerousPartiesAndTheirVecs.Count==1&&MobilePartyAi.DangerousPartiesAndTheirVecs[0].Item1==7&&MobilePartyAi.DangerousPartiesAndTheirVecs[0].Item2.X==2,"native shared danger list restored exactly");
  n.GetBestInitiativeBehavior(a,out behavior,out _,out _,out _);Check(behavior==AiBehavior.None,"preview war relation does not leak into real native call");
  r.MapFaction.Enemies.Add(a.MapFaction);a.MapFaction.Enemies.Add(r.MapFaction);r.Position.X=2;
  balance=GwpStrength.Compare(a,t);Near(balance.EnemyLocalStrength,740.01f,"native allied enemy distance factor and 0.9 retained");Check(!balance.StrengthReady,"native weighted enemy sum blocks");
  (n,a,t,r)=Setup();r.Position.X=30;t.Reachable=false;Check(!GwpStrength.Compare(a,t).StrengthReady,"unreachable target never becomes safe declaration");
  t.Reachable=true;t.Position.X=20;Check(!GwpStrength.Compare(a,t).StrengthReady,"outside native scan does not fabricate readiness");
  (n,a,t,r)=Setup();r.Position.X=2;r.IsCurrentlyAtSea=true;
  Check(GwpStrength.Compare(a,t).StrengthReady,"native navigation eligibility excludes inaccessible sea enemy");
  r.IsCurrentlyAtSea=false;r.AttachedTo=t;t.Army=r.Army=new(){LeaderParty=t,EstimatedStrength=800};t.AttachedParties.Add(r);
  balance=GwpStrength.Compare(a,t);Near(balance.EnemyLocalStrength,800.01f,"attached enemy army counted exactly once");
  Check(GwpStrength.CaseTarget(new(){Offender=r})==800,"stable case target keeps actual attached army");
  (n,a,t,r)=Setup();r.Position.X=2;r.Party.MapFaction=a.MapFaction;r.Party.EstimatedStrength=40;
  a.Party.EstimatedStrength=180;balance=GwpStrength.Compare(a,t);Check(balance.StrengthReady,"present native friend makes regional advantage");Near(balance.FriendlyLocalStrength,220.01f,"actual native friend sum");
  balance=GwpStrength.Compare(a,t,excludedFriendly:r);Check(!balance.StrengthReady,"temporary interception detachment cannot hide weakened chief");Near(balance.FriendlyLocalStrength,180.01f,"excluded detachment removed from native spatial enumeration");
  Check(GwpStrength.Compare(a,t).StrengthReady,"preview exclusion does not leak across evaluations");
  r.Army=new(){LeaderParty=r,EstimatedStrength=500};r.AttachedTo=null;r.Position.X=40;
  Check(!GwpStrength.Compare(a,t).StrengthReady,"far unjoined army promise contributes nothing");
  (n,a,t,r)=Setup();r.Position.X=30;
  Check(GwpStrength.Compare(a,t).StrengthReady,"initial advantage before target grows");
  t.Party.EstimatedStrength=400;
  Check(!GwpStrength.Compare(a,t).StrengthReady,"same pursuit reevaluates target growth rather than reusing declaration advantage");
  n.GetBestInitiativeBehavior(a,out behavior,out target,out _,out _);
  Check(behavior!=AiBehavior.EngageParty||target!=t,"grown target is not authorized for active case attack");
  t.Party.EstimatedStrength=200;
  Check(GwpStrength.Compare(a,t).StrengthReady,"restored strength is freshly evaluated without replacing case identity");
  a.Party.EstimatedStrength=200;Check(!GwpStrength.Compare(a,t).StrengthReady,"equal regional forces do not authorize voluntary attack");
  a.Party.EstimatedStrength=180;var player=New("player",a.Position.X,50,new Faction());MobileParty.MainParty=player;
  Check(!GwpStrength.Compare(a,t).StrengthReady,"unrequested player omitted by vanilla");
  balance=GwpStrength.Compare(a,t,includePlayer:true);Check(balance.StrengthReady,"existing physically present requested player exception retained");Near(balance.FriendlyLocalStrength,230.01f,"requested player counted once");
  player.Position.X=40;Check(!GwpStrength.Compare(a,t,includePlayer:true).StrengthReady,"distant requested player not counted");
  player.Position.X=a.Position.X;player.Party.MapFaction=a.MapFaction;balance=GwpStrength.Compare(a,t,includePlayer:true);Near(balance.FriendlyLocalStrength,230.01f,"native excludes main party so friendly clan does not double count exception");
  a.Army=player.Army=new(){LeaderParty=a,EstimatedStrength=230};player.AttachedTo=a;a.AttachedParties.Add(player);
  balance=GwpStrength.Compare(a,t,includePlayer:true);Near(balance.FriendlyLocalStrength,230.01f,"player already in actual army is not added twice");
  (n,a,t,r)=Setup(3);MobileParty.All.Remove(r);PoliceEnforcementBehavior.CaseTarget=t;
  a.DefaultBehavior=AiBehavior.GoToSettlement;a.TargetParty=null;a.TargetSettlement=new();
  a._lastCalculatedSpeed=a.LastCalculatedBaseSpeed=3;t._lastCalculatedSpeed=t.LastCalculatedBaseSpeed=6;t.IsMoving=true;
  var interceptor=New("interceptor",0,20,a.MapFaction);var battle=new MapEvent();
  battle.AttackerSide.LeaderParty=interceptor.Party;battle.DefenderSide.LeaderParty=t.Party;
  battle.AttackerSide.OtherSide=battle.DefenderSide;battle.DefenderSide.OtherSide=battle.AttackerSide;
  battle.AttackerSide.Parties.Add(new(){Party=interceptor.Party});battle.DefenderSide.Parties.Add(new(){Party=t.Party});
  t.MapEvent=battle;t.MapEventSide=battle.DefenderSide;interceptor.MapEvent=battle;interceptor.MapEventSide=battle.AttackerSide;
  Check(GwpStrength.Compare(a,t).StrengthReady,"existing interceptor battle remains safe while chief long-term behavior visits settlement");
  n.GetBestInitiativeBehavior(a,out behavior,out target,out var score,out _);
  Check(behavior==AiBehavior.EngageParty&&target==t&&score>1,"production region hooks preserve native battle attack above short-term override threshold");
  Check(a.DefaultBehavior==AiBehavior.GoToSettlement,"native case attack does not require forcing long-term command");
  Console.WriteLine($"NATIVE_ADAPTER_INTERCEPTOR_BATTLE: default={a.DefaultBehavior}, initiative={behavior}, score={score:F4}");
  foreach(var speeds in new[]{(3.32f,3.32f),(3.325f,3.32f),(1.992f,3.02f)}){
   (n,a,t,r)=Setup(3);MobileParty.All.Remove(r);PoliceEnforcementBehavior.CaseTarget=t;
   a.Aggressiveness=.95f;a._lastCalculatedSpeed=a.LastCalculatedBaseSpeed=speeds.Item1;
   t._lastCalculatedSpeed=t.LastCalculatedBaseSpeed=speeds.Item2;t.IsMoving=true;
   Check(GwpStrength.Compare(a,t).StrengthReady,"regional advantage is independent of pursuit speed: "+speeds);
   n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
   Near(n.Observed[t].Attack,0,"equal/nearly equal or temporarily slower base speed suppresses attack outside close range");
   Check(behavior!=AiBehavior.EngageParty||score<=1,"safe region does not guarantee active engagement: "+speeds);
   Console.WriteLine($"NATIVE_ADAPTER_SPEED_GAP: sourceBase={speeds.Item1:F3}, targetBase={speeds.Item2:F3}, regionReady=True, attack={n.Observed[t].Attack:F4}, best={behavior}, score={score:F4}");
  }
  (n,a,t,r)=Setup();r.Position.X=30;a.Party.EstimatedStrength=100;n.ThrowOnAvoid=true;bool threw=false;
  MobilePartyAi.DangerousPartiesAndTheirVecs.Clear();MobilePartyAi.DangerousPartiesAndTheirVecs.Add((9,new Vec2(1,2)));
  try{GwpStrength.Compare(a,t);}catch(InvalidOperationException){threw=true;}
  Check(threw&&!GwpStrength.IsNativePreview&&!GwpStrength.IsInNativeEvaluation(a),"evaluation exception propagates and scoped frame is restored");
  Check(MobilePartyAi.DangerousPartiesAndTheirVecs.Count==1&&MobilePartyAi.DangerousPartiesAndTheirVecs[0].Item1==9,"shared native danger list restored on exception too");
  Console.WriteLine("NATIVE_ADAPTER_ASSERTIONS="+Checks+"; PASS; production hooks and freshly decompiled native code");
 }
}
'''


class RegionalThreatTests(unittest.TestCase):
    def test_production_adapter_with_installed_native_policy(self):
        source = subprocess.check_output(["ilspycmd", "-r", str(GAME_BIN), "-t",
            "TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyAIModel",
            str(GAME_BIN / "TaleWorlds.CampaignSystem.dll")], text=True, encoding="utf-8")
        methods = []
        for name in ["GetBestInitiativeBehavior", "IsEnemy", "CalculateInitiativeScoresForEnemy", "GetInitiativeDistanceForAttack", "CalculateStanceScore"]:
            method = extract(source, name).replace("public override void", "public void")
            if name == "CalculateInitiativeScoresForEnemy":
                method = method.replace("void CalculateInitiativeScoresForEnemy(", "void CalculateInitiativeScoresForEnemyImpl(", 1)
            methods.append(method)
        code = HARNESS[:HARNESS.index("static class Replay {")].replace("NATIVE_METHODS", "\n".join(methods))
        code = code.replace("namespace Audit {", "namespace GreyWardenPolicePurity {").replace("NativeAi", "DefaultMobilePartyAIModel")
        code = code.replace("public bool ShouldConsiderAttacking", "public bool ThrowOnAvoid;public bool ShouldConsiderAttacking")
        code = code.replace("=>AvoidAllowed;", '=>ThrowOnAvoid?throw new InvalidOperationException("engine boundary failure"):AvoidAllowed;')
        code = code.replace("public string Id=", "public string StringId=>Id;public string Id=")
        code = code.replace("Math.Clamp(x,a,b)", "Math.Max(a,Math.Min(b,x))")
        code = code.replace("System.MathF.PI", "(float)System.Math.PI")
        code = code.replace("=>AttackAllowed;", "=>AttackAllowed&&(!GwpCommon.IsGreyWardenLordParty(a)||b!=a.TargetParty||GwpStrength.NativeAttackReady(a,b));")
        strength = (MODULE / "GwpStrength.cs").read_text(encoding="utf-8-sig")
        strength = re.sub(r"(?m)^(\s*(?:internal|private) static \w+)\s*\n\s*(\w+\s*\()", r"\1 \2", strength)
        boundaries = PRODUCTION_BOUNDARIES.replace("PRODUCTION_METHODS", "\n".join(extract(strength, n) for n in ["CombatGroupOf", "CombatGroup", "CaseTarget", "Compare"]))
        code += boundaries + "}"
        native = (MODULE / "GwpStrength.Native.cs").read_text(encoding="utf-8-sig")
        native = re.sub(r"(?m)^using TaleWorlds[^;]+;\n", "", native)
        code += "\n" + native
        code = "using System.Reflection;using System.Reflection.Emit;using HarmonyLib;using System.Globalization;using IFaction=GreyWardenPolicePurity.Faction;\n" + code.replace("using System;using System.Linq;using System.Collections.Generic;", "using System;using System.Linq;using System.Collections.Generic;",1)
        # C# imports of the separate production file belong at the compilation-unit start.
        code = code.replace("\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Reflection;\nusing System.Reflection.Emit;\nusing HarmonyLib;\n", "\n")
        with tempfile.TemporaryDirectory(prefix="gwp-native-adapter-") as temp:
            path = Path(temp)
            harmony = MODULE / "lib/0Harmony.dll"
            (path / "Replay.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup><Reference Include="0Harmony"><HintPath>{harmony}</HintPath></Reference></ItemGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(code, encoding="utf-8")
            build = subprocess.run(["dotnet", "build", temp, "-c", "Release", "-v", "quiet"], capture_output=True, text=True)
            self.assertEqual(0, build.returncode, build.stdout+build.stderr)
            run = subprocess.run([str(path / "bin/Release/net472/Replay.exe")], capture_output=True, text=True)
            self.assertEqual(0, run.returncode, run.stdout+run.stderr)
            print(run.stdout)


if __name__ == "__main__":
    unittest.main()
