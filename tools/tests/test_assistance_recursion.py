"""Prevent the production strength/duty cycle from recurring.

Only the spatial-query boundary interrupts after repeated identical searches. The
strength, coordination, duty methods are taken from production.
This is an investigation regression: it requires the previously confirmed call cycle to remain absent.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_battle_peace import extract
from test_assistance import member

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / "GreyWardenPolicePurity"


HARNESS = r"""
using System;using System.Linq;using System.Collections.Generic;using System.Globalization;
namespace GreyWardenPolicePurity {
enum AiBehavior {None,EscortParty,GoAroundParty}
class Vec2 { public float X;public Vec2(float x=0){X=x;}public float Distance(Vec2 p)=>Math.Abs(X-p.X); }
class CampaignVec2 {public float X;public bool IsOnLand=true;public Vec2 ToVec2()=>new Vec2(X);}
class MapEvent {public bool IsFinalized; }
class Settlement {public string StringId="settlement";public object? SiegeEvent;}
class Camp {public MobileParty? LeaderParty;}
class PartyBase {public int NumberOfHealthyMembers=20;public MapEvent? MapEvent;}
class Ai {public PartyBase? AiBehaviorPartyBase;}
class LocatableSearchData<T> {public int Index;}
class CycleFound:Exception {public CycleFound():base("Repeated spatial query interrupted the production call cycle"){} }
class MobileParty {
 public string StringId="";public bool IsActive=true,IsLordParty=true,IsMainParty,IsGarrison,IsMilitia;public float Strength=40,Aggressiveness=1;
 public CampaignVec2 Position=new CampaignVec2();public Vec2 GetPosition2D=>Position.ToVec2();public PartyBase Party=new PartyBase();public Ai Ai=new Ai();
 public Army? Army;public MobileParty? AttachedTo;public MapEvent? MapEvent;public Settlement? CurrentSettlement;public Camp? BesiegerCamp;
 public object MapFaction=SharedFaction;public static object SharedFaction=new object();public static List<MobileParty> All=new();
 public static MobileParty MainParty=new MobileParty{StringId="player"};public enum NavigationType {None,Default}
 public static int Searches;
 public static LocatableSearchData<MobileParty> StartFindingLocatablesAroundPosition(Vec2 p,float radius){
  if(++Searches>6)throw new CycleFound();return new LocatableSearchData<MobileParty>();
 }
 public static MobileParty? FindNextLocatable(ref LocatableSearchData<MobileParty> d)=>d.Index<All.Count?All[d.Index++]:null;
}
class Army {public MobileParty LeaderParty=null!;}
class Encounter {public float GetEncounterJoiningRadius=3;}
class Models {public Encounter EncounterModel=new Encounter();}
class Campaign {public static Campaign Current=new Campaign();public Models Models=new Models();}
static class AiHelper {public static void GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(MobileParty a,MobileParty b,out MobileParty.NavigationType n,out float d){n=MobileParty.NavigationType.Default;d=0;}}
static class GwpRuntimeState {public static CrimeOwner Crime=new CrimeOwner();}
class CrimeOwner {public PoliceTask? GetTask(string id)=>CrimeState.GetTask(id);}
class PoliceTask {public string TargetCrimeId="crime";public CrimeRecord TargetCrime=new CrimeRecord();public bool IsPlayerBountyEscort,WarDeclared,IsEscortingPlayer;public Settlement? EscortSettlement;}
static class GwpCommon {public static bool IsShelteredOffender(MobileParty? p)=>p?.CurrentSettlement!=null;}
class CrimeRecord {public bool HasOpenCase=true;public string CrimeId="crime";public MobileParty? Offender;}
static class CrimeState {public static PoliceTask Task=new PoliceTask();public static PoliceTask? GetTask(string id)=>Task;}
static class CrimePool {public static PoliceTask? GetTask(string id)=>CrimeState.GetTask(id);}
static class PlayerBountyBehavior {public static bool AwaitingSupportRequest(PoliceTask? t)=>false;}
static class GwpWardenDuties {public static bool TryGetVisitDuty(MobileParty p,out Settlement? s,out float f){s=null;f=0;return false;}}
static class GwpPlayerCaseAssignment {public static bool IsReservedTarget(MobileParty? p)=>false;}
class CampaignTime {public static CampaignTime Now=new();public double ToHours=100;}
class GreyWardenPartyDesireBehavior {
 private enum IntentKind {Approach,Pursue,Escort,Visit,Beat}
 private class Intent {public IntentKind Kind;public MobileParty? Party;public Settlement? Settlement;public float Priority;public double ExpiresAt;public bool PursuesOffender;}
 private const float AssignedDutyScore=.99f,PlayerRequestScore=1;
 private static Dictionary<string,Intent> Intents=new();
 private static bool IsValid(Intent i)=>true;
 private static Intent? ResolvePatrolBeat(MobileParty p)=>null;
 private static IntentKind ResolveUndeclaredPursuitKind(PoliceTask t,MobileParty p)=>IntentKind.Escort;
 DIAGNOSTIC_METHODS
}
static class GwpTuning {public static class Enforcement {public const float RegionalThreatRadius=6;}}
static class GwpStrength {
 public class Threat {public float Strength;}
 public class LocalBalance {public MobileParty Actor=null!,MovementTarget=null!;public float FriendlyLocalStrength,EnemyLocalStrength,Distance;public string FriendlyCombatGroups="",Reason="";public bool StrengthReady;}
 // Engine-side inputs are fixed; none of these boundary models calls a duty resolver.
 public static float CombatGroup(MobileParty p)=>p.Strength;
 public static MobileParty CombatGroupOf(MobileParty p)=>p.AttachedTo??p;
 public static string CombatGroupKey(MobileParty p)=>CombatGroupOf(p).StringId;
 public static Threat EnemyAround(MobileParty a,MobileParty b)=>new Threat{Strength=b.Strength};
 STRENGTH_METHODS
}
class PoliceEnforcementBehavior {
 private class LordAssistanceGroup {public string LeaderPartyId="leader",TargetPartyId="enemy",PendingReleaseReason="";public List<string> MemberPartyIds=new(),SpeedDetachedPartyIds=new();public bool DispersedForSpeed,PlayerEscort;}
 private static PoliceEnforcementBehavior? _instance;
 private Dictionary<string,LordAssistanceGroup> _assistanceGroups=new();private MobileParty Support=null!;
 private static MobileParty? FindActiveParty(string id)=>MobileParty.All.FirstOrDefault(p=>p.StringId==id&&p.IsActive);
 private static bool IsAssistanceGroupCaseStillActive(LordAssistanceGroup g,PoliceTask? t)=>t!=null;
 private static bool FormationInBattle(MobileParty p)=>p.MapEvent!=null;
 private static bool IsAssistancePlayerBountyEscort(LordAssistanceGroup g)=>g.PlayerEscort;
 private static bool IsSpeedDetached(LordAssistanceGroup g,string id)=>g.SpeedDetachedPartyIds.Contains(id);
 private IEnumerable<MobileParty> FormationSupports(LordAssistanceGroup g)=>new[]{Support};
 private IEnumerable<MobileParty> FormationParticipants(LordAssistanceGroup g)=>FormationSupports(g);
 ASSISTANCE_METHODS
 static int assertions;
 static void Check(bool b,string name){if(!b)throw new Exception(name);assertions++;}
 static (PoliceEnforcementBehavior,MobileParty,MobileParty,MobileParty,LordAssistanceGroup) Setup(){
  var l=new MobileParty{StringId="leader"};var s=new MobileParty{StringId="support"};var t=new MobileParty{StringId="enemy",Strength=200};
  MobileParty.All=new List<MobileParty>{l,s,t};MobileParty.Searches=0;
  var e=new PoliceEnforcementBehavior{Support=s};_instance=e;var g=new LordAssistanceGroup();e._assistanceGroups[l.StringId]=g;return(e,l,s,t,g);
 }
 static bool Cycle(Action a,bool print=false){try{a();return false;}catch(CycleFound x){
  string stack=x.StackTrace!;
  if(print)Console.WriteLine("PRODUCTION_CYCLE_STACK\n"+string.Join("\n",stack.Split('\n').Take(15)));return true;
 }}
 static void Main(){
  var(e,l,s,t,g)=Setup();Check(!Cycle(()=>GwpStrength.Compare(l,t),true),"detached nearby logical support reenters strength forever");
  (e,l,s,t,g)=Setup();s.AttachedTo=l;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"attached support collapses into leader and does not recurse");
  s.AttachedTo=null;MobileParty.Searches=0;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"detaching same support exposes cycle");
  (e,l,s,t,g)=Setup();s.Ai.AiBehaviorPartyBase=t.Party;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"native target equality avoids duty fallback");
  (e,l,s,t,g)=Setup();s.Position.X=7;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"outside target join radius no recursion");
  (e,l,s,t,g)=Setup();l.Strength=2000;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"strong leader is not a recursion guard");
  (e,l,s,t,g)=Setup();g.PlayerEscort=true;Check(!Cycle(()=>GwpStrength.Compare(l,t)),"player-escort rendezvous early exit avoids cycle");
  (e,l,s,t,g)=Setup();Check(!Cycle(()=>TryGetAssistanceDuty(l,out _,out _,out _,out _)),"duty entry also exposes same cycle");
  (e,l,s,t,g)=Setup();g.DispersedForSpeed=true;s.Ai.AiBehaviorPartyBase=l.Party;s.AttachedTo=l;Check(!Cycle(()=>GreyWardenPartyDesireBehavior.GetDiagnosticIntent(l)),"speed-dispersal logger lookup returns while support attached");
  s.AttachedTo=null;MobileParty.Searches=0;Check(!Cycle(()=>GreyWardenPartyDesireBehavior.GetDiagnosticIntent(l)),"next logger intent lookup recurses after support detaches");
  Console.WriteLine("ASSISTANCE_RECURSION_ASSERTIONS="+assertions+"; PASS; no reentry");
 }
}
}
"""


class AssistanceRecursionTests(unittest.TestCase):
    def test_actual_strength_and_duty_methods_do_not_reenter(self):
        strength = (MODULE / "GwpStrength.cs").read_text(encoding="utf-8-sig")
        # Normalize only line breaks in declarations; all bodies stay verbatim.
        strength = re.sub(r"(?m)^(\s*(?:internal|private) static \w+)\s*\n\s*(\w+\s*\()", r"\1 \2", strength)
        methods = [extract(strength, name) for name in ["Compare", "FriendlyAround",
                   "CanNearbyFriendlyGroupJoinActor"]]
        formation = (MODULE / "PoliceEnforcementBehavior.Formation.cs").read_text(encoding="utf-8-sig")
        assistance = (MODULE / "PoliceEnforcementBehavior.Assistance.cs").read_text(encoding="utf-8-sig")
        duty_methods = [extract(assistance, "TryGetAssistanceDuty"),
                        member(assistance, "IsUndeclaredAssistancePursuit")]
        code = HARNESS.replace("STRENGTH_METHODS", "\n".join(methods)).replace("ASSISTANCE_METHODS", "\n".join(duty_methods))
        desire = (MODULE / "GreyWardenPartyDesireBehavior.cs").read_text(encoding="utf-8-sig")
        code = code.replace("DIAGNOSTIC_METHODS", "\n".join(extract(desire, name) for name in
                            ["GetDiagnosticIntent", "ResolveIntent", "DescribeIntent"]))
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-strength-cycle-") as temp:
            path = Path(temp)
            (path / "Replay.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(code, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", temp, "-c", "Release", "-v", "quiet"], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print("\n".join(line for line in result.stdout.splitlines() if ": warning " not in line))
