"""Regress actual duty admission, movement ownership and work pausing.

Production tables and methods execute against controlled duty/engine inputs.
Assertions preserve the approved ownership and pause/resume rules.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
class MobileParty {public string StringId="warden";public bool IsActive=true;public HashSet<WardenDuty> Duties=new();public MapEvent? MapEvent;public static MobileParty MainParty=new(){StringId="player"};}
class Settlement {}
class MapEvent {public bool IsFinalized;}
class PoliceTask {public string TargetCrimeId="criminal";public bool IsEscortingPlayer,IsPlayerBountyEscort;public Settlement? EscortSettlement;public CrimeRecord? TargetCrime;}
class CrimeRecord {public MobileParty? Offender;}
class CrimeOwner {public PoliceTask? Task;public PoliceTask? GetTask(string id)=>Task;}
static class GwpRuntimeState {public static CrimeOwner Crime=new();}
static class CrimePool {public const string PlayerCrimeId="PLAYER_WANTED";public static PoliceTask? GetTask(string id)=>GwpRuntimeState.Crime.Task;}
static class GreyWardenPlayerRequestBehavior {public static bool IsPartyReservedForPlayerRequest(MobileParty p)=>p.Duties.Contains(WardenDuty.PlayerRequest);}
static class GreyWardenTroopRequestBehavior {public static bool IsTrainerReservedForPlayerOrder(MobileParty p)=>p.Duties.Contains(WardenDuty.PlayerTroopOrder);}
static class GreyWardenVillageAdoptionBehavior {public static Settlement Village=new();public static bool TryGetReliefVisit(MobileParty p,out Settlement? s,out float f){s=Village;f=10;return IsVillageReliefParty(p); }public static bool IsVillageReliefParty(MobileParty p)=>p.Duties.Contains(WardenDuty.VillageRelief);}
static class GreyWardenVillageReconstructionBehavior {public static bool IsReconstructionParty(MobileParty p)=>p.Duties.Contains(WardenDuty.Reconstruction);public static bool ShouldReserveFromOrdinaryCases(MobileParty p)=>IsReconstructionParty(p)||p.Duties.Contains(WardenDuty.ReconstructionReserve);}
static class GreyWardenIssueResolutionBehavior {public static bool IsIssueDutyParty(MobileParty p)=>p.Duties.Contains(WardenDuty.Issue);public static bool ShouldReserveFromOrdinaryCases(MobileParty p)=>IsIssueDutyParty(p)||p.Duties.Contains(WardenDuty.IssueReserve);}
enum AiBehavior {EscortParty,GoAroundParty}
static class PoliceEnforcementBehavior {
 public static bool IsPartyHeldForDuel(MobileParty p)=>p.Duties.Contains(WardenDuty.Duel);public static Settlement Venue=new();public static Settlement? GetDuelVenue(MobileParty p)=>IsPartyHeldForDuel(p)?Venue:null;
 public static MobileParty Target=new(){StringId="chief"};public static bool PlayerEscort;
 public static bool IsPartyOccupiedByAssistance(MobileParty p)=>p.Duties.Contains(WardenDuty.Assistance);
 public static bool TryGetAssistanceDuty(MobileParty p,out MobileParty? target,out AiBehavior behavior,out bool player,out bool offender){target=Target;behavior=AiBehavior.EscortParty;player=PlayerEscort;offender=false;return IsPartyOccupiedByAssistance(p);}
}
static class PlayerBountyBehavior {public static bool Awaiting;public static bool AwaitingSupportRequest(PoliceTask? t)=>Awaiting;}
static class GwpPlayerCaseAssignment {public static bool IsReservedTarget(MobileParty? p)=>false;}
class CampaignTime {public static CampaignTime Now=new();public double ToHours=100;}
class GreyWardenPartyDesireBehavior {
 enum IntentKind {Approach,Pursue,Escort,Visit,Beat}
 class Intent {public IntentKind Kind;public MobileParty? Party;public Settlement? Settlement;public float Priority;public double ExpiresAt;public bool PursuesOffender;}
 SCORES
 static Dictionary<string,Intent> Intents=new();static int Rethinks;
 static void RequestImmediateRethink(MobileParty p){Rethinks++;}
 static bool IsSameIntent(Intent a,Intent b)=>a.Kind==b.Kind&&a.Party==b.Party&&a.Settlement==b.Settlement;
 static bool IsValid(Intent i)=>i.Kind==IntentKind.Visit||i.Kind==IntentKind.Beat?i.Settlement!=null:i.Party?.IsActive==true;
 static Intent ResolvePatrolBeat(MobileParty p)=>new(){Kind=IntentKind.Beat,Priority=AssignedDutyScore};
 static IntentKind ResolveUndeclaredPursuitKind(PoliceTask t,MobileParty p)=>IntentKind.Escort;
 METHODS
 static int Checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);Checks++;}
 static void Main(){
  var p=new MobileParty();
  GwpRuntimeState.Crime.Task=new PoliceTask{TargetCrime=new CrimeRecord{Offender=new MobileParty{StringId="offender"}}};
  Check(!GwpWardenDuties.IsFreeFor(p,WardenDuty.OrdinaryCase),"existing ordinary case blocks another assignment");
  Check(GwpWardenDuties.IsFreeFor(p,WardenDuty.Assistance),"ordinary case can be paused for assistance");
  Check(GwpWardenDuties.IsFreeFor(p,WardenDuty.VillageRelief),"relief admission permits replacing ordinary case");
  GwpRuntimeState.Crime.Task=null;
  foreach(var duty in new[]{WardenDuty.PlayerRequest,WardenDuty.PlayerTroopOrder,WardenDuty.VillageRelief,WardenDuty.Assistance,WardenDuty.Reconstruction,WardenDuty.Issue,WardenDuty.Duel}){
   p.Duties.Clear();p.Duties.Add(duty);Check(!GwpWardenDuties.IsFreeFor(p,WardenDuty.Assistance),"assistance cannot take "+duty);
  }
  p.Duties.Clear();p.Duties.Add(WardenDuty.Assistance);
  foreach(var wanted in new[]{WardenDuty.VillageRelief,WardenDuty.PlayerCase,WardenDuty.Reconstruction,WardenDuty.Issue,WardenDuty.OrdinaryCase})Check(!GwpWardenDuties.IsFreeFor(p,wanted),"existing assistance blocks "+wanted);
  p.Duties.Clear();p.Duties.Add(WardenDuty.Reconstruction);Check(!GwpWardenDuties.IsFreeFor(p,WardenDuty.VillageRelief),"relief cannot take reconstruction");
  p.Duties.Clear();p.Duties.Add(WardenDuty.Issue);Check(!GwpWardenDuties.IsFreeFor(p,WardenDuty.VillageRelief),"relief cannot take issue");
  p.Duties.Clear();GwpRuntimeState.Crime.Task=new PoliceTask{IsPlayerBountyEscort=true};Check(GwpWardenDuties.BlockingDuty(p,WardenDuty.PlayerRequest)==WardenDuty.PlayerEscort,"accepted support pauses a later troop order");
  GwpRuntimeState.Crime.Task=null;p.Duties.Add(WardenDuty.PlayerTroopOrder);Check(GwpWardenDuties.BlockingDuty(p,WardenDuty.PlayerEscort)==WardenDuty.PlayerTroopOrder,"existing troop order cannot be taken by later support");
  p.Duties.Clear();p.Duties.Add(WardenDuty.ReconstructionReserve);Check(GwpWardenDuties.IsFreeFor(p,WardenDuty.Assistance),"unassigned reconstruction backlog permits assistance");
  p.Duties.Clear();p.Duties.Add(WardenDuty.IssueReserve);Check(GwpWardenDuties.IsFreeFor(p,WardenDuty.Assistance),"unassigned issue backlog permits assistance");
  p.Duties.Clear();p.Duties.Add(WardenDuty.Duel);foreach(var wanted in new[]{WardenDuty.OrdinaryCase,WardenDuty.Assistance,WardenDuty.VillageRelief,WardenDuty.PlayerCase,WardenDuty.PlayerRequest,WardenDuty.Reconstruction,WardenDuty.Issue})Check(!GwpWardenDuties.IsFreeFor(p,wanted),"duel blocks "+wanted);
  p.Duties.Clear();var a=new Settlement();var b=new Settlement();
  SetIntent(p,new Intent{Kind=IntentKind.Visit,Settlement=a,Priority=10,ExpiresAt=108});
  SetIntent(p,new Intent{Kind=IntentKind.Visit,Settlement=b,Priority=8,ExpiresAt=108});
  Check(ResolveIntent(p)!.Settlement==b&&ResolveIntent(p)!.Priority==8,"external slot replacement retained; owned duty resolved separately");
  int before=Rethinks;SetIntent(p,new Intent{Kind=IntentKind.Visit,Settlement=b,Priority=10,ExpiresAt=110});
  Check(ResolveIntent(p)!.Priority==10&&Rethinks==before,"same intent renews score without arbitration or rethink");
  p.Duties.Add(WardenDuty.Assistance);
  Check(ResolveIntent(p)!.Party==PoliceEnforcementBehavior.Target&&ResolveIntent(p)!.Priority==AssignedDutyScore,"assistance resolver precedes external visit10 with ordinary score");
  PoliceEnforcementBehavior.PlayerEscort=true;Check(ResolveIntent(p)!.Priority==PlayerRequestScore,"player assistance score10");
  PlayerBountyBehavior.Awaiting=true;Check(ResolveIntent(p)!.Party==MobileParty.MainParty,"awaiting player support precedes assistance");
  PlayerBountyBehavior.Awaiting=false;p.Duties.Clear();p.Duties.Add(WardenDuty.VillageRelief);CampaignTime.Now.ToHours=111;
  Check(ResolveIntent(p)!.Settlement==GreyWardenVillageAdoptionBehavior.Village,"saved relief target survives expired external slot");
  p.Duties.Add(WardenDuty.Duel);Check(ResolveIntent(p)!.Settlement==PoliceEnforcementBehavior.Venue,"duel precedes suspended relief");
  p.Duties.Remove(WardenDuty.Duel);Check(ResolveIntent(p)!.Settlement==GreyWardenVillageAdoptionBehavior.Village,"relief resumes after duel");
  p.Duties.Clear();double end=120,pause=-1;CampaignTime.Now.ToHours=100;
  p.Duties.Add(WardenDuty.Duel);Check(GwpWardenDuties.PauseWork(p,true,ref end,ref pause)&&pause==100,"duel pauses work");
  CampaignTime.Now.ToHours=130;Check(GwpWardenDuties.RemainingWorkHours(end,pause)==20,"paused work remaining does not elapse");
  p.Duties.Clear();Check(GwpWardenDuties.PauseWork(p,false,ref end,ref pause),"return travel still paused");
  CampaignTime.Now.ToHours=140;Check(!GwpWardenDuties.PauseWork(p,true,ref end,ref pause)&&end==160&&pause<0,"worksite return resumes only remaining20 hours");
  p.MapEvent=new MapEvent();Check(GwpWardenDuties.PauseWork(p,true,ref end,ref pause),"battle pauses existing work");
  Check(AssignedDutyScore==.99f&&PlayerRequestScore==10f&&PlayerEnforcementScore==1f,"production score constants");
  Console.WriteLine("DUTY_PRIORITY_CHARACTERIZATION="+Checks+"; current admission and intent behavior; engine/duty state inputs controlled");
 }
}
}
'''


class DutyPriorityCharacterization(unittest.TestCase):
    def test_admission_and_intent_resolution(self):
        duty=(MODULE/'GwpWardenDuties.cs').read_text(encoding='utf-8-sig')
        duty=re.sub(r'^using TaleWorlds[^\n]*\n','',duty,flags=re.M)
        desire=(MODULE/'GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        scores='\n'.join(re.findall(r'(?:private|internal) const float (?:AssignedDutyScore|PlayerRequestScore|PlayerEnforcementScore)\s*=\s*[^;]+;',desire))
        code=HARNESS.replace('SCORES',scores).replace('METHODS','\n'.join(extract(desire,n) for n in ['SetIntent','ResolveIntent']))
        # NormalizePriority is expression-bodied, so preserve its actual source separately.
        code=code.replace(' static int Checks;',re.search(r'private static float NormalizePriority\(float priority\) =>[\s\S]*?;',desire)[0]+'\n static int Checks;')
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-duty-priority-') as temp:
            p=Path(temp)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            (p/'Duties.cs').write_text(duty,encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],capture_output=True,text=True)
            self.assertEqual(0,r.returncode,r.stdout+r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('DUTY_PRIORITY_CHARACTERIZATION=')))


if __name__ == '__main__':
    unittest.main()
