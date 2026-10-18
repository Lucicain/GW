"""Execute real admission, duel start, movement resolution and pause serialization.

Reuse the production duty table; native tournament/army/party objects are inputs.
No game process or native movement is executed.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_assistance import member
from test_duty_priority import HARNESS, MODULE

EXTRA = r'''
class Hero{public bool IsActive=true;public string Name="hero";}
struct Vec2{public float X;public float Distance(Vec2 b)=>Math.Abs(X-b.X);public float DistanceSquared(Vec2 b)=>(X-b.X)*(X-b.X);public static Vec2 operator +(Vec2 a,Vec2 b)=>new(){X=a.X+b.X};public static Vec2 operator *(Vec2 a,float b)=>new(){X=a.X*b};}
class Clan{public string StringId="gw";}
class Town{public static List<Town> AllTowns=new();public bool IsTown=true,IsUnderSiege,InRebelliousState;public object MapFaction=new();public Settlement Settlement=new();public string Name="venue";}
static class FactionManager{public static bool IsAtWarAgainstFaction(object a,object b)=>false;}
static class PoliceStats{public static Clan GetPoliceClan()=>new();}
class TournamentManager{public int Adds;public object? Game;public object? GetTournamentGame(Town t)=>Game;public void AddTournament(object game){Game=game;Adds++;}}
class TournamentModel{public object CreateTournament(Town t)=>new();}
class Models{public TournamentModel TournamentModel=new();}
class Campaign{public static Campaign Current=new();public TournamentManager TournamentManager=new();public Models Models=new();}
static class GwpTuning{public static class Enforcement{public const int TournamentDuelNearbyTowns=3,TournamentDuelTimeoutDays=3;}}
class InformationMessage{public InformationMessage(string text,object color){}}
static class InformationManager{public static void DisplayMessage(InformationMessage message){}}
static class Colors{public static object Yellow=new();}
static class GwpText{public static string Get(params object[] args)=>"";}
static class GwpTextCatalog{public const string gwp_envoy_duel_called="duel";}
static class GwpAiDiagnostics{public static void WriteAction(MobileParty p,string a,string b){}}
static class CrimeState{public static PoliceTask? GetTask(string id)=>GwpRuntimeState.Crime.Task;}
enum CaseTaskEnd{Released}
'''

POLICE = r'''
 private static PoliceEnforcementBehavior? _instance;
 private enum EnvoyStage{Travelling,Failed,Grace,Duel,DuelDecided,Aftermath,WaitingForDuel}
 private class EnvoyCase{public string WardenPartyId="warden",TownId="",CrimeId="case";public EnvoyStage Stage;public double DueHours;}
 private Dictionary<string,EnvoyCase> _envoyCases=new();private int Ended,ReleasedGroups,Reports;
 private void EndCaseTask(string id,CaseTaskEnd end,string reason){Ended++;GwpRuntimeState.Crime.Task=null;}
 private static MobileParty? RequisitionParty;private static bool PendingRelease;
 private static void ReleasePartyFromAssistance(string id,string reason){var p=RequisitionParty;if(p!=null&&!PendingRelease){p.Duties.Remove(WardenDuty.Assistance);p.Army=null;}_instance!.ReleasedGroups++;}
 private void ReleaseAssistanceGroup(string id,string reason){ReleasedGroups++;}
 private void LeaveAssistanceAsMember(MobileParty p,string reason){p.Duties.Remove(WardenDuty.Assistance);p.Army=null;}
 private void RecordEnvoyReport(EnvoyCase c,string outcome,int amount){Reports++;}
 private void FailEnvoy(EnvoyCase c,string reason){c.Stage=EnvoyStage.Failed;}
 private static void HoldForDuel(MobileParty p,Settlement s,bool warden){if(warden)GreyWardenPartyDesireBehavior.RequestVisit(p,s,8);}
 POLICE_METHODS
 static int Checks;static void Check(bool b,string reason){if(!b)throw new Exception(reason);Checks++;}
 static void Main(){
  var e=new PoliceEnforcementBehavior();_instance=e;var p=new MobileParty();var target=new MobileParty{StringId="offender"};var town=new Town();Town.AllTowns.Add(town);Settlement.All[town.Settlement.StringId]=town.Settlement;
  var ec=new EnvoyCase();e._envoyCases[ec.CrimeId]=ec;
  p.Duties.Add(WardenDuty.VillageRelief);e.StartDuel(ec,p,target);
  Check(ec.Stage==EnvoyStage.Duel&&e.Reports==1&&Campaign.Current.TournamentManager.Adds==1,"background envoy starts exactly one real tournament despite relief occupation");
  Check(GwpWardenDuties.HoldsAny(p,WardenDuty.Duel)&&!GwpWardenDuties.CanContinueRelief(p),"active duel holds shared duty and pauses relief");
  Check(!e.TryPreparePolicePartyForVillageRelief(p,WardenDuty.VillageRelief)&&e.Ended==0,"relief cannot release a duelist case");
  Check(!e.TryPreparePartyForPlayerRequest(p)&&e.ReleasedGroups==1,"player request cannot pull active duelist");
  Check(GreyWardenPartyDesireBehavior.ActualTarget(p)==town.Settlement,"real resolver chooses duel over prior relief");
  e._envoyCases.Clear();Check(GwpWardenDuties.CanContinueRelief(p)&&GreyWardenPartyDesireBehavior.ActualTarget(p)==GreyWardenVillageAdoptionBehavior.Village,"duel conclusion restores saved relief target");
  p.Duties.Clear();p.Duties.Add(WardenDuty.PlayerRequest);ec=new EnvoyCase();e._envoyCases[ec.CrimeId]=ec;int adds=Campaign.Current.TournamentManager.Adds;
  e.StartDuel(ec,p,target);Check(ec.Stage==EnvoyStage.WaitingForDuel&&Campaign.Current.TournamentManager.Adds==adds,"pending duel respects existing player commitment without creating unused tournament");
  p.Duties.Clear();e.StartDuel(ec,p,target);Check(ec.Stage==EnvoyStage.Duel&&Campaign.Current.TournamentManager.Adds==adds,"pending duel resumes using existing tournament once player is free");
  e._envoyCases.Clear();p.Duties.Add(WardenDuty.Assistance);ec=new EnvoyCase();e._envoyCases[ec.CrimeId]=ec;e.StartDuel(ec,p,target);
  Check(ec.Stage==EnvoyStage.Duel&&!p.Duties.Contains(WardenDuty.Assistance),"helper's original envoy duel leaves assistance before movement");
  e._envoyCases.Clear();p.Duties.Clear();GwpRuntimeState.Crime.Task=new PoliceTask{WarDeclared=true};int ended=e.Ended;
  Check(!e.TryPreparePolicePartyForVillageRelief(p,WardenDuty.VillageRelief)&&e.Ended==ended,"adoption cannot interrupt an escalated war pursuit");
  GwpRuntimeState.Crime.Task!.WarDeclared=false;Check(e.TryPreparePolicePartyForVillageRelief(p,WardenDuty.VillageRelief)&&e.Ended==ended+1,"ordinary undeclared pursuit can yield to adoption");
  p.Duties.Add(WardenDuty.Reconstruction);Check(!e.TryPreparePolicePartyForVillageRelief(p,WardenDuty.VillageRelief),"already assigned construction protected from adoption");
  p.Duties.Clear();p.MapEvent=new();ended=e.ReleasedGroups;Check(!e.TryPreparePartyForPlayerRequest(p)&&e.ReleasedGroups==ended,"battle blocks player requisition before any teardown");
  p.MapEvent=null;p.Duties.Clear();p.Duties.Add(WardenDuty.Assistance);RequisitionParty=p;PendingRelease=true;Check(!e.TryPreparePartyForPlayerRequest(p),"pending battle release prevents overlapping player assignment");
  PendingRelease=false;Check(e.TryPreparePartyForPlayerRequest(p)&&!p.Duties.Contains(WardenDuty.Assistance),"completed assistance release permits player assignment");
  Checks+=PlayerBountyBehavior.CheckSupportEligibility(p);
  Console.WriteLine("DUTY_SCENARIO_ASSERTIONS="+Checks+"; real admission, tournament start and movement resolver; native boundaries controlled");
 }
'''


class DutyScenarios(unittest.TestCase):
    def test_construction_and_petition_pause_roundtrip(self):
        common=r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
class CampaignTime {public static CampaignTime Now=new();public double ToHours;}
class MobileParty {public MapEvent? MapEvent;}
class MapEvent {public bool IsFinalized;}
enum WardenDuty {Duel}
interface IDataStore {bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore {public bool IsSaving{get;set;}public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new();public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v!;else v=(T)Data[k];}}
static class GwpWardenDuties {public static bool Duel;static bool HoldsAny(MobileParty p,WardenDuty d)=>Duel;SHARED}
CLASSES
class Program {static void Main(){int checks=GreyWardenVillageReconstructionBehavior.Run()+GreyWardenIssueResolutionBehavior.Run();Console.WriteLine("WORK_PAUSE_SAVE_ASSERTIONS="+checks+"; real serializers and shared pause helper");}}
}
'''
        duty=(MODULE/'GwpWardenDuties.cs').read_text(encoding='utf-8-sig')
        common=common.replace('SHARED','\n'.join(member(duty,n) for n in ['PauseWork','RemainingWorkHours']))
        classes=[]
        for name,collection,entry,started in [
            ('GreyWardenVillageReconstructionBehavior','_tasks','ReconstructionTask','WorkStarted=true,'),
            ('GreyWardenIssueResolutionBehavior','_assignments','IssueAssignment','')]:
            source=(MODULE/(name+'.cs')).read_text(encoding='utf-8-sig')
            sync=member(source.replace('public override void SyncData','internal void SyncData'),'SyncData')
            fields='public string VillageSettlementId="village",VillageName="Village";public double QueuedTimeHours=90;public bool WorkStarted;' if started else 'public string IssueId="issue",OwnerHeroId="notable";'
            fields+='public string PolicePartyId="warden";public double WorkEndHours,PausedAtHours=-1;'
            code='class '+name+'{'+ '\n'.join(re.findall(r'private const [^;]+;',source))+f'\nclass {entry}{{{fields}}}\nreadonly List<{entry}> {collection}=new();\n'+sync
            code+=r'''
 internal static int Run(){
  var b=new NAME();var task=new ENTRY{STARTED WorkEndHours=124};b.LIST.Add(task);var p=new MobileParty();CampaignTime.Now.ToHours=100;GwpWardenDuties.Duel=true;
  if(!GwpWardenDuties.PauseWork(p,true,ref task.WorkEndHours,ref task.PausedAtHours))throw new Exception("duel must pause");
  var store=new Store{IsSaving=true};b.SyncData(store);store.IsSaving=false;var copy=new NAME();copy.SyncData(store);var loaded=copy.LIST.Single();
  if(loaded.PolicePartyId!="warden"||loaded.PausedAtHours!=100||loaded.WorkEndHours!=124)throw new Exception("paused task lost state on load");
  CampaignTime.Now.ToHours=200;if(GwpWardenDuties.RemainingWorkHours(loaded.WorkEndHours,loaded.PausedAtHours)!=24)throw new Exception("time advanced during suspension");
  GwpWardenDuties.Duel=false;if(!GwpWardenDuties.PauseWork(p,false,ref loaded.WorkEndHours,ref loaded.PausedAtHours))throw new Exception("return travel counted as work");
  CampaignTime.Now.ToHours=210;if(GwpWardenDuties.PauseWork(p,true,ref loaded.WorkEndHours,ref loaded.PausedAtHours)||loaded.WorkEndHours!=234||loaded.PausedAtHours>=0)throw new Exception("work did not resume remaining24");
  store.IsSaving=true;copy.SyncData(store);store.IsSaving=false;var final=new NAME();final.SyncData(store);if(final.LIST.Single().WorkEndHours!=234)throw new Exception("resumed deadline lost on save");
  return 6;
 }
}
'''.replace('NAME',name).replace('ENTRY',entry).replace('STARTED',started).replace('LIST',collection)
            classes.append(code)
        common=common.replace('CLASSES','\n'.join(classes))
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-work-save-') as temp:
            p=Path(temp)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(common,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],capture_output=True,text=True)
            self.assertEqual(0,r.returncode,r.stdout+r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('WORK_PAUSE_SAVE_ASSERTIONS=')))

    def test_existing_commitments_duels_and_requisition(self):
        duty=(MODULE/'GwpWardenDuties.cs').read_text(encoding='utf-8-sig')
        duty=re.sub(r'^using TaleWorlds[^\n]*\n','',duty,flags=re.M)
        desire=(MODULE/'GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        helper=(MODULE/'PoliceEnforcementBehavior.Helpers.cs').read_text(encoding='utf-8-sig')
        envoy=(MODULE/'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig')
        support=(MODULE/'PlayerBountyBehavior.Support.cs').read_text(encoding='utf-8-sig')
        assistance=(MODULE/'PoliceEnforcementBehavior.Assistance.cs').read_text(encoding='utf-8-sig')
        code=HARNESS.replace('static class PoliceEnforcementBehavior {','class PoliceEnforcementBehavior {')
        code=re.sub(r' public static bool IsPartyHeldForDuel[^\n]*\n','',code)
        code=code.replace('class Settlement {}','class Settlement {public string StringId="town";public Vec2 GetPosition2D=new();public static Dictionary<string,Settlement> All=new();public static Settlement? Find(string id)=>All.GetValueOrDefault(id);}')
        code=code.replace('public bool IsActive=true;public HashSet','public bool IsActive=true;public Hero? LeaderHero=new();public object? Army;public object MapFaction=new();public Clan ActualClan=new();public static List<MobileParty> All=new();public Vec2 GetPosition2D=new();public HashSet')
        support_checks=r'''
 public static int CheckSupportEligibility(MobileParty p){
  p.MapEvent=null;p.Duties.Clear();if(!IsEligibleSupportResponder(p))throw new Exception("ordinary lord can help player");
  p.Duties.Add(WardenDuty.PlayerRequest);if(IsEligibleSupportResponder(p))throw new Exception("support steals existing player request");
  p.Duties.Clear();p.Duties.Add(WardenDuty.PlayerTroopOrder);if(IsEligibleSupportResponder(p))throw new Exception("support steals existing order");
  p.Duties.Clear();p.MapEvent=new();if(IsEligibleSupportResponder(p))throw new Exception("support steals active battle");
  p.MapEvent=null;p.Duties.Add(WardenDuty.VillageRelief);if(!IsEligibleSupportResponder(p))throw new Exception("player support can suspend relief");
  p.Duties.Clear();p.Duties.Add(WardenDuty.PlayerRequest);var free=new MobileParty{StringId="free"};MobileParty.All=new(){p,free};if(ResolveSupportResponder(p)!=free)throw new Exception("busy responder does not fall back to eligible lord");
  return 6;
 }
'''
        code=code.replace('static class PlayerBountyBehavior {','static class PlayerBountyBehavior {\n'+support_checks+'\n'+'\n'.join(member(support,n) for n in ['IsEligibleSupportResponder','ResolveSupportResponder']))
        code=code.replace('public bool IsEscortingPlayer,IsPlayerBountyEscort;','public bool IsEscortingPlayer,IsPlayerBountyEscort,WarDeclared;')
        code=code.replace('static class GreyWardenVillageReconstructionBehavior {','static class GreyWardenVillageReconstructionBehavior {public static void ReleasePartyForForcedDuty(MobileParty p)=>p.Duties.Remove(WardenDuty.Reconstruction);')
        code=code.replace('static class GreyWardenIssueResolutionBehavior {','static class GreyWardenIssueResolutionBehavior {public static void ReleasePartyForForcedDuty(MobileParty p)=>p.Duties.Remove(WardenDuty.Issue);')
        code=code.replace(' public static MobileParty Target=',POLICE+'\n public static MobileParty Target=')
        # The reused fixture's entry point is not the scenario under test.
        code=code.replace(' static void Main(){\n  var p=', ' static void OtherTests(){\n  var p=')
        code=code.replace('static void RequestImmediateRethink(', 'public static void RequestImmediateRethink(')
        code=code.replace('PoliceEnforcementBehavior.Venue','Town.AllTowns[0].Settlement')
        code=code.replace(' static Dictionary<string,Intent> Intents=', ' public static Settlement? ActualTarget(MobileParty p)=>ResolveIntent(p)?.Settlement;public static void ClearIntent(MobileParty p)=>Intents.Remove(p.StringId);public static void RequestVisit(MobileParty p,Settlement s,float priority)=>SetIntent(p,new Intent{Kind=IntentKind.Visit,Settlement=s,Priority=priority,ExpiresAt=108});\n static Dictionary<string,Intent> Intents=')
        code=code.replace('SCORES','\n'.join(re.findall(r'(?:private|internal) const float (?:AssignedDutyScore|PlayerRequestScore|PlayerEnforcementScore)\s*=\s*[^;]+;',desire)))
        code=code.replace('\n METHODS\n','\n'+'\n'.join(member(desire,n) for n in ['SetIntent','ResolveIntent','NormalizePriority'])+'\n')
        code=code.replace('POLICE_METHODS','\n'.join([member(helper,n) for n in ['TryPreparePolicePartyForVillageRelief','TryPreparePartyForPlayerRequest']]+[member(envoy,n) for n in ['StartDuel','IsDuelVenueOpen','PickDuelVenue','EnsureTournament']]+[member(assistance,n) for n in ['DuelForParty','IsPartyHeldForDuel','GetDuelVenue']]))
        code=code.replace('namespace GreyWardenPolicePurity {','namespace GreyWardenPolicePurity {\n'+EXTRA,1)
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-duty-scenarios-') as temp:
            p=Path(temp)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            (p/'Duties.cs').write_text(duty,encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],capture_output=True,text=True)
            self.assertEqual(0,r.returncode,r.stdout+r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('DUTY_SCENARIO_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
