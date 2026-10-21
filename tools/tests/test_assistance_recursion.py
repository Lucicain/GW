"""Duty and diagnostic resolution must never enter regional strength evaluation.

The production duty methods run with a strength boundary that rejects reentry.
Native arithmetic and its scoped evaluator have their own production-hook replay.
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
static class GwpStrength {
 public static MobileParty CombatGroupOf(MobileParty p)=>p.AttachedTo??p;
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
  var(e,l,s,t,g)=Setup();Check(!Cycle(()=>TryGetAssistanceDuty(l,out _,out _,out _,out _),true),"leader duty does not reenter regional strength");
  (e,l,s,t,g)=Setup();s.AttachedTo=l;Check(!Cycle(()=>TryGetAssistanceDuty(s,out _,out _,out _,out _)),"attached support duty terminates");
  s.AttachedTo=null;Check(!Cycle(()=>TryGetAssistanceDuty(s,out _,out _,out _,out _)),"detached support duty terminates");
  (e,l,s,t,g)=Setup();s.Ai.AiBehaviorPartyBase=t.Party;Check(!Cycle(()=>TryGetAssistanceDuty(l,out _,out _,out _,out _)),"native combat target does not add duty strength lookup");
  (e,l,s,t,g)=Setup();s.Position.X=7;Check(!Cycle(()=>TryGetAssistanceDuty(l,out _,out _,out _,out _)),"remote logical support duty terminates");
  (e,l,s,t,g)=Setup();l.Strength=2000;Check(!Cycle(()=>TryGetAssistanceDuty(l,out _,out _,out _,out _)),"duty independent of strength advantage");
  (e,l,s,t,g)=Setup();g.PlayerEscort=true;Check(TryGetAssistanceDuty(l,out var target,out _,out _,out _)&&target==MobileParty.MainParty,"player escort resolves to actual player");
  (e,l,s,t,g)=Setup();g.PendingReleaseReason="ended";Check(!TryGetAssistanceDuty(l,out _,out _,out _,out _),"pending release has no duty");
  (e,l,s,t,g)=Setup();g.DispersedForSpeed=true;s.Ai.AiBehaviorPartyBase=l.Party;s.AttachedTo=l;Check(!Cycle(()=>GreyWardenPartyDesireBehavior.GetDiagnosticIntent(l)),"attached support diagnostic resolves without strength evaluation");
  s.AttachedTo=null;Check(!Cycle(()=>GreyWardenPartyDesireBehavior.GetDiagnosticIntent(l)),"detached support diagnostic resolves without strength evaluation");
  Console.WriteLine("ASSISTANCE_RECURSION_ASSERTIONS="+assertions+"; PASS; no reentry");
 }
}
}
"""


class AssistanceRecursionTests(unittest.TestCase):
    def test_actual_duty_and_diagnostic_methods_do_not_reenter(self):
        assistance = (MODULE / "PoliceEnforcementBehavior.Assistance.cs").read_text(encoding="utf-8-sig")
        duty_methods = [extract(assistance, "TryGetAssistanceDuty"),
                        member(assistance, "IsUndeclaredAssistancePursuit")]
        code = HARNESS.replace("ASSISTANCE_METHODS", "\n".join(duty_methods))
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
