"""Cross-component investigation: real person records, task ending and envoy calls.

Campaign UI, diplomacy, army teardown and
negotiation outcomes are controlled boundaries; the lifecycle bodies are real.
"""
import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_battle_peace import extract
from test_assistance import member

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
public enum AiBehavior {None,Hold} public enum GwpCrimeCategory{Unknown,VillageViolence,CaravanAttack}
enum PoliceTaskFlowState{None,PreparingDispatch,Pursuit,WarPursuit,EscortingPlayer,PlayerBountyEscort}
public struct Vec2{public float X,Y;public Vec2(float x=0,float y=0){X=x;Y=y;}public float Distance(Vec2 v)=>Math.Abs(X-v.X);} public class Settlement{public bool IsFortification;public List<Hero> HeroesWithoutParty=new();} public interface IFaction{string StringId{get;}} public class Clan:IFaction{public string StringId{get;set;}="clan";}
public struct CampaignTime{public static CampaignTime Now=new(){ToHours=10};public double ToHours;public static CampaignTime Hours(float h)=>new(){ToHours=h};}
class Campaign{public static Campaign Current=new();public List<IFaction> Factions=new();}
interface IDataStore{bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public bool IsSaving{get;set;}public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new();public void SyncData<T>(string key,ref T value){if(IsSaving)Data[key]=value!;else value=(T)Data[key];}}
public class Hero{public string StringId="",Name="";public bool IsAlive=true,IsPrisoner,IsFugitive,IsDisabled;public bool IsDead=>!IsAlive;
 public MobileParty? PartyBelongedTo;public Settlement? CurrentSettlement;public static List<Hero> All=new();public static Hero? FindFirst(Func<Hero,bool> f)=>All.FirstOrDefault(f);}
public class MobileParty{public string StringId="";public bool IsActive=true,IsMainParty;public Hero? LeaderHero;public object? MapEvent;public Settlement? CurrentSettlement;
 public Vec2 GetPosition2D=new();public AiBehavior DefaultBehavior;public IFaction MapFaction=new Clan();public static List<MobileParty> All=new();public static MobileParty MainParty=new(){StringId="player",IsMainParty=true};}
 RECORD_TYPES
static class CrimePool{public const string PlayerCrimeId="PLAYER_WANTED";public static IEnumerable<CrimeRecord> LedgerRecords=>CrimeState.Records.Values;public static CrimeRecord? GetRecordByKey(string id)=>CrimeState.Records.GetValueOrDefault(id);}
static class CrimeState{
 internal static Dictionary<string,PoliceTask> _tasks=new();internal static Dictionary<string,CrimeRecord> Records=new();
 private static Dictionary<string,CrimeRecord> _ledger=>Records;private const string PlayerCrimeId=CrimePool.PlayerCrimeId;
 public static IReadOnlyDictionary<string,PoliceTask> ActiveTasks=>_tasks;public static void RefreshAccepting(){}
 public static void ReopenCase(CrimeRecord? c){if(c!=null){c.HasOpenCase=true;Records[c.CrimeId]=c;}}
 DATA_METHODS
 DATA_EXTRA
 public static CrimeRecord RoundTrip(CrimeRecord c){var store=new Store{IsSaving=true};SyncRecord(store,0,c,true);store.IsSaving=false;var restored=new CrimeRecord();SyncRecord(store,0,restored,false);return restored;}
}
static class GwpCrimeCategoryClassifier{public static GwpCrimeCategory FromCrimeType(string t,string id)=>GwpCrimeCategory.VillageViolence;}
static class GwpPlayerCaseAssignment{public static bool IsCaseHeldByPlayer(string id)=>false;}
static class PoliceStats{public static Clan? Clan=new();public static Clan? GetPoliceClan()=>Clan;}
static class FactionManager{public static bool War;public static bool IsAtWarAgainstFaction(IFaction a,IFaction b)=>War;}
static class GwpCommon{public static void TryResetAi(MobileParty p){}}
static class GwpPeace{public static void EndPoliceWar(IFaction? f){}}
static class PoliceResourceManager{public static int Rewards;public static void CreditSuccessfulCaseCompletion()=>Rewards++;}
static class GwpPlayerRequestDeferral{public static void NotifyDutyCompleted(MobileParty? p,string reason){}}
static class GreyWardenPartyDesireBehavior{public static void ClearIntent(MobileParty p){}public static void RequestImmediateRethink(MobileParty? p){}}
static class GwpAiDiagnostics{public static void WriteAction(MobileParty p,string a,string d){}public static void WriteFieldArrest(string a,string d){}}
class Quest{public void MarkReadyForTurnIn(){}public void WriteLog(string t){}}
class ReportsState{public bool HasPendingReports;}
static class GwpText{public static string Get(string k,params object[] args)=>k;public static string Create(string s)=>s;}
static class GwpTextCatalog{public const string gwp_case_closed_elsewhere="closed";}
class InformationMessage{public InformationMessage(string s,object c){}}
static class Colors{public static object Yellow=new();}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
class PlayerBountyBehavior{
 public bool HasBountyTask=true,IsTrackingBountyTarget=true,_caseTargetDefeated,_caseClosedElsewhere;
 public Hero? CaseHero;private int _pendingPrisonerAssessed,_assignedCaseFine,_bountyPlayerCasualties;
 private string _pendingPrisonerHeroId="",_activeBountyTargetHeroId="",_activeBountyTargetName="",_activeBountyTargetId="";
 private double _activeBountyDeadlineHours;private Quest? _activeQuest;private ReportsState? Reports;
 private bool RegisterCaptureInCustody()=>false;private void StopBountyEscortAfterTargetDefeat(){}private void MakePeaceWithCriminalFaction(){}private void EnterBountyCollectionState(){}
 PLAYER_METHODS
 public void Run()=>ReconcileAssignedCase();
}
class PoliceEnforcementBehavior{
 internal enum CaseTaskEnd{Solved,Closed,Released}
 private enum EnvoyStage{Travelling,Failed,Grace,Duel,DuelDecided,Aftermath,WaitingForDuel}
 private class EnvoyCase{public string CrimeId="",WardenPartyId="",EnvoyPartyId="",TownId="",TargetPartyId="";public bool CollectOnly,WardenWon;public int PendingDesire=-1;public double DueHours=100;public EnvoyStage Stage;}
 private class EnvoyReport{public double Hours;public string OffenderHeroId="",WardenHeroId="",Outcome="",TownId="";public int Amount;}
 private class DelayPatrolState{public bool Returning;}
 private static PoliceEnforcementBehavior? _instance;private Dictionary<string,EnvoyCase> _envoyCases=new();private Dictionary<string,DelayPatrolState> _delayPatrolStates=new();
 private List<EnvoyReport> _envoyReports=new();private Dictionary<string,double> _caseStrengthCooldownHours=new(),_lastRetargetHourByParty=new();private static Dictionary<IFaction,double> _peaceRetryTargets=new();
 private HashSet<string> _groups=new();private int Followed,ReportsCount,Negotiated;private bool Reached,NegotiationSucceeded=true;private MobileParty? LastFollowed;
 private void CompleteAssistanceTasks(string id)=>_groups.Remove(id);private void ReleaseAssistanceGroup(string id,string reason)=>_groups.Remove(id);
 private void ClearTaskWarTracking(string id,bool returning){}private void ReleaseEnvoysToPlayer(string id){}
 internal static void ReleaseWardensFromCase(string? id){}
 private void MarkDelayPatrolReturning(string id){if(_delayPatrolStates.TryGetValue(id,out var s))s.Returning=true;}
 private string LastOutcome="";private void RecordEnvoyReport(EnvoyCase e,string outcome,int amount){ReportsCount++;LastOutcome=outcome;}
 private bool EnvoyReached(MobileParty e,MobileParty t)=>Reached;
 private void FollowTarget(MobileParty e,MobileParty t){Followed++;LastFollowed=t;}
 private void Negotiate(EnvoyCase e,CrimeRecord c,MobileParty p,Hero h,MobileParty envoy,MobileParty target,Hero offender){Negotiated++;if(NegotiationSucceeded){RecordEnvoyReport(e,"paid",1000);ResolveEnvoyCase(e,c,true,"envoy_paid");}else FailEnvoy(e,"envoy_layer_one_failed");}
 POLICE_METHODS
 static int Checks;static void Check(bool b,string why){if(!b)throw new Exception(why);Checks++;}
 static (PoliceEnforcementBehavior,MobileParty,MobileParty,CrimeRecord,CrimeRecord,EnvoyCase) Setup(){
  CrimeState._tasks.Clear();CrimeState.Records.Clear();MobileParty.All.Clear();Hero.All.Clear();FactionManager.War=false;
  var e=new PoliceEnforcementBehavior();_instance=e;
  var chief=new MobileParty{StringId="chief",LeaderHero=new Hero{StringId="chief_hero"}};
  var helper=new MobileParty{StringId="helper",LeaderHero=new Hero{StringId="helper_hero"}};
  var targetHero=new Hero{StringId="target"};var target=new MobileParty{StringId="target_party",LeaderHero=targetHero};targetHero.PartyBelongedTo=target;Hero.All.Add(targetHero);
  var ownHero=new Hero{StringId="own"};var ownTarget=new MobileParty{StringId="own_party",LeaderHero=ownHero};ownHero.PartyBelongedTo=ownTarget;Hero.All.Add(ownHero);
  var envoy=new MobileParty{StringId="envoy"};MobileParty.All.AddRange(new[]{chief,helper,target,ownTarget,envoy});
  var main=new CrimeRecord{CrimeId="target",Offender=target,HasOpenCase=true};var own=new CrimeRecord{CrimeId="own",Offender=ownTarget,HasOpenCase=true};
  CrimeState.Records[main.CrimeId]=main;CrimeState.Records[own.CrimeId]=own;
  CrimeState._tasks["chief"]=new PoliceTask{PolicePartyId="chief",TargetCrime=main};CrimeState._tasks["helper"]=new PoliceTask{PolicePartyId="helper",TargetCrime=own};e._groups.Add("chief");
  var ec=new EnvoyCase{CrimeId="own",WardenPartyId="helper",EnvoyPartyId="envoy"};e._envoyCases["own"]=ec;e._delayPatrolStates["envoy"]=new();
  return(e,chief,helper,main,own,ec);
 }
 static void Main(){
  var(e,chief,helper,main,own,ec)=Setup();e.TendTravellingEnvoy(ec,own);
  Check(e.Followed==1&&e.LastFollowed==own.Offender&&e._envoyCases.ContainsKey("own"),"own envoy continues original target while helper's task exists");
  e.FailEnvoy(ec,"envoy_lost");Check(ec.Stage==EnvoyStage.Failed&&e.ReportsCount==1,"own envoy failure records result");
  Check(e._groups.Contains("chief")&&CrimeState._tasks.ContainsKey("helper"),"own envoy failure keeps cooperative group and own pending case");
  (e,chief,helper,main,own,ec)=Setup();e.ResolveEnvoyCase(ec,own,true,"envoy_paid");
  Check(!own.HasOpenCase&&!CrimeState.Records.ContainsKey("own"),"own envoy settlement actually closes own record");
  Check(main.HasOpenCase&&e._groups.Contains("chief")&&CrimeState._tasks.ContainsKey("chief"),"own settlement does not close chief's different case/group");
  Check(!CrimeState._tasks.ContainsKey("helper"),"own task removed after own settlement");
  (e,chief,helper,main,own,ec)=Setup();
  var newPerson=new Hero{StringId="next"};var newParty=new MobileParty{StringId="next_party",LeaderHero=newPerson};newPerson.PartyBelongedTo=newParty;Hero.All.Add(newPerson);MobileParty.All.Add(newParty);
  var next=new CrimeRecord{CrimeId="next",Offender=newParty,HasOpenCase=true};CrimeState.Records["next"]=next;
  CrimeState.RetargetTask("helper",next);e.TendTravellingEnvoy(ec,own);
  Check(e._envoyCases.ContainsKey("own")&&!e._delayPatrolStates["envoy"].Returning&&e.Followed==1&&e.LastFollowed==own.Offender,"retarget preserves original envoy and its target");
  Check(own.HasOpenCase,"retarget itself preserves prior person's liability");
  (e,chief,helper,main,own,ec)=Setup();CrimeState._tasks.Remove("helper");ec.CollectOnly=true;e.TendTravellingEnvoy(ec,own);
  Check(e.Followed==1&&e._envoyCases.ContainsKey("own"),"grace collector can operate with no assigned task");
  (e,chief,helper,main,own,ec)=Setup();foreach(double hours in new[]{101d,1000d,10000d}){CampaignTime.Now.ToHours=hours;e.TendTravellingEnvoy(ec,own);}
  Check(ec.Stage==EnvoyStage.Travelling&&e.ReportsCount==0&&e.Followed==3,"long journey continues beyond any old deadline without inventing a failed outcome");
  Check(own.HasOpenCase&&!e._delayPatrolStates["envoy"].Returning,"long travel leaves negotiation pending and envoy outbound");
  e.Reached=true;e.TendTravellingEnvoy(ec,own);CampaignTime.Now.ToHours=10;
  Check(e.Negotiated==1&&e.LastOutcome=="paid"&&!own.HasOpenCase,"after long travel actual contact reaches negotiation and can settle the case");
  (e,chief,helper,main,own,ec)=Setup();CampaignTime.Now.ToHours=10000;e.Reached=true;e.NegotiationSucceeded=false;e.TendTravellingEnvoy(ec,own);CampaignTime.Now.ToHours=10;
  Check(e.Negotiated==1&&ec.Stage==EnvoyStage.Failed&&e.LastOutcome=="talks_failed"&&own.HasOpenCase,"after long travel only the actual persuasion failure enables pursuit");
  (e,chief,helper,main,own,ec)=Setup();MobileParty.All.RemoveAll(p=>p.StringId=="envoy");e.TendTravellingEnvoy(ec,own);
  Check(ec.Stage==EnvoyStage.Failed&&e.LastOutcome=="lost"&&own.HasOpenCase,"disappeared envoy is failed negotiation with the case still open");
  Check(e._delayPatrolStates["envoy"].Returning,"lost envoy leaves no active travelling negotiation");
  (e,chief,helper,main,own,ec)=Setup();FactionManager.War=true;e.TendTravellingEnvoy(ec,own);
  Check(ec.Stage==EnvoyStage.Failed&&e.LastOutcome=="war"&&own.HasOpenCase,"already at war is distinct from the deadline failure");
  (e,chief,helper,main,own,ec)=Setup();own.OffenderHero!.PartyBelongedTo=null;CampaignTime.Now.ToHours=101;e.TendTravellingEnvoy(ec,own);CampaignTime.Now.ToHours=10;
  Check(ec.Stage==EnvoyStage.Travelling&&e.ReportsCount==0&&own.HasOpenCase,"temporarily missing target does not expire at the old deadline");
  (e,chief,helper,main,own,ec)=Setup();var oldParty=own.Offender!;var person=own.OffenderHero!;person.IsPrisoner=true;person.PartyBelongedTo=null;e.TendTravellingEnvoy(ec,own);
  Check(own.Offender==null&&own.HasOpenCase&&e._envoyCases.ContainsKey("own"),"temporary target party loss preserves envoy and liability");
  var rebuilt=new MobileParty{StringId="new_party",LeaderHero=person};person.IsPrisoner=false;person.PartyBelongedTo=rebuilt;MobileParty.All.Add(rebuilt);
  Check(own.Offender==rebuilt&&own.OffenderPartyId=="new_party","real record resolves same person's rebuilt party");
  (e,chief,helper,main,own,ec)=Setup();main.OffenderHero!.PartyBelongedTo=null;main.OffenderHero.IsPrisoner=true;e.EndCaseTask("chief",CaseTaskEnd.Released,"target_invalid");
  Check(main.HasOpenCase&&CrimeState.Records.ContainsKey("target")&&!e._groups.Contains("chief"),"ordinary release after third-party defeat retains person's record");
  (e,chief,helper,main,own,ec)=Setup();e.FailTaskBecauseOwnerCannotLead("chief","owner_cannot_lead");
  Check(main.HasOpenCase&&CrimeState.Records.ContainsKey("target"),"owner failure preserves live offender liability");
  foreach(bool prisoner in new[]{true,false}){
   (e,chief,helper,main,own,ec)=Setup();var person2=main.OffenderHero!;person2.IsPrisoner=prisoner;person2.PartyBelongedTo=null;
   var bounty=new PlayerBountyBehavior{CaseHero=person2};bounty.Run();
   Check(!bounty._caseClosedElsewhere&&main.HasOpenCase&&CrimeState.Records.ContainsKey("target"),"player commission preserves third-party captive/temporarily partyless person's record");
  }
  (e,chief,helper,main,own,ec)=Setup();var ownPerson=main.OffenderHero!;ownPerson.PartyBelongedTo=null;
  var completed=new PlayerBountyBehavior{CaseHero=ownPerson,_caseTargetDefeated=true};completed.Run();Check(!completed._caseClosedElsewhere&&main.HasOpenCase,"player's own defeat outcome avoids third-party-withdraw branch");

  (e,chief,helper,main,own,ec)=Setup();var pursued=main.OffenderHero!;int unchangedCount=main.IncidentCount=7;main.CivilianCasualties=497;
  pursued.IsPrisoner=true;pursued.PartyBelongedTo=null;CrimeState.ReleaseTask("chief","third_party_defeat");CrimeState.RemoveDeadOffenderRecords();
  Check(CrimeState.Records.ContainsKey("target")&&main.IncidentCount==unchangedCount&&main.CivilianCasualties==497,"third party defeat preserves severe unpaid person liability");
  Check(CrimeState.GetNearest(new Vec2(),c=>c.CrimeId=="target")==null,"temporarily captive person does not consume actionable queue");
  var restoredParty=new MobileParty{StringId="restored_party",LeaderHero=pursued};pursued.PartyBelongedTo=restoredParty;MobileParty.All.Add(restoredParty);
  Check(!main.IsOffenderPursuable(),"captive cannot be selected through a lingering party reference");pursued.IsPrisoner=false;
  Check(CrimeState.GetNearest(new Vec2(),c=>c.CrimeId=="target")==main,"same person automatically returns to candidate queue without another crime");
  pursued.IsDisabled=true;CrimeState.RemoveDeadOffenderRecords();
  Check(CrimeState.Records.ContainsKey("target")&&main.IncidentCount==7&&main.CivilianCasualties==497,"disabled state is not proof of permanent exit and does not erase liability");
  Check(!main.IsOffenderPursuable()&&CrimeState.GetNearest(new Vec2(),c=>c.CrimeId=="target")==null,"disabled person cannot be dispatched against even with lingering active party reference");
  pursued.IsDisabled=false;Check(CrimeState.GetNearest(new Vec2(),c=>c.CrimeId=="target")==main,"reactivated person re-enters selection without new offense");
  CrimeState.BeginTask("new_owner",main);Check(CrimeState.GetTask("new_owner")?.TargetCrime==main&&main.IncidentCount==7,"reassignment preserves original person record without TryAdd");
  var unresolved=new CrimeRecord{CrimeId="unresolved",OffenderHeroId="unresolved",OffenderPartyId="missing",HasOpenCase=true,IncidentCount=9};
  CrimeState.Records[unresolved.CrimeId]=unresolved;CrimeState._tasks["pending_owner"]=new PoliceTask{PolicePartyId="pending_owner",TargetCrime=unresolved};
  CrimeState.ReleaseTask("pending_owner","hero_temporarily_unresolved");CrimeState.RemoveDeadOffenderRecords();
  Check(unresolved.HasOpenCase&&CrimeState.Records.ContainsKey("unresolved")&&!CrimeState._tasks.ContainsKey("pending_owner"),"unresolved hero is not evidence of permanent death");
  pursued.IsAlive=false;CrimeState.RemoveDeadOffenderRecords();Check(!CrimeState.Records.ContainsKey("target")&&CrimeState.Records.ContainsKey("unresolved"),"only confirmed dead person removed by passive cleanup");

  unresolved.OccurredTime=unresolved.LastCrimeTime=CampaignTime.Now;unresolved.Location=new Vec2(3,4);unresolved.CivilianCasualties=497;
  var savedUnresolved=CrimeState.RoundTrip(unresolved);CrimeState.Records["unresolved"]=savedUnresolved;
  Check(savedUnresolved.HasOpenCase&&savedUnresolved.OffenderHeroId=="unresolved"&&savedUnresolved.IncidentCount==9&&savedUnresolved.CivilianCasualties==497,"real person record serializer retains unpaid severe liability without resolvable hero");
  Check(!savedUnresolved.IsOffenderPursuable(),"loaded unresolved person stays deferred");
  var returnedHero=new Hero{StringId="unresolved"};var returnedParty=new MobileParty{StringId="after_load_party",LeaderHero=returnedHero};returnedHero.PartyBelongedTo=returnedParty;Hero.All.Add(returnedHero);MobileParty.All.Add(returnedParty);
  Check(savedUnresolved.Offender==returnedParty&&savedUnresolved.IncidentCount==9,"loaded person record resolves returned hero without another crime");
  Check(CrimeState.GetNearest(new Vec2(),c=>c.CrimeId=="unresolved")==savedUnresolved,"loaded deferred record re-enters selection automatically");
  foreach(EnvoyStage stage in Enum.GetValues(typeof(EnvoyStage))){
   (e,chief,helper,main,own,ec)=Setup();ec.Stage=stage;ec.CollectOnly=true;ec.TargetPartyId="own_party";ec.PendingDesire=4;ec.DueHours=123.5;
   var saved=new Store{IsSaving=true};e.SyncEnvoyData(saved);var loaded=new PoliceEnforcementBehavior();saved.IsSaving=false;loaded.SyncEnvoyData(saved);
   var restored=loaded._envoyCases["own"];Check(restored.Stage==stage&&restored.DueHours==123.5,"real envoy stage and deadline survive roundtrip: "+stage);
   Check(restored.CollectOnly&&restored.TargetPartyId=="own_party"&&restored.PendingDesire==4,"collector/target/pending aftermath survive roundtrip: "+stage);
  }
  (e,chief,helper,main,own,ec)=Setup();ec.DueHours=0;ec.CollectOnly=true;ec.TargetPartyId="own_party";
  var travelSave=new Store{IsSaving=true};e.SyncEnvoyData(travelSave);var travelLoad=new PoliceEnforcementBehavior();travelSave.IsSaving=false;travelLoad.SyncEnvoyData(travelSave);
  var loadedTravel=travelLoad._envoyCases["own"];CampaignTime.Now.ToHours=10000;travelLoad.TendTravellingEnvoy(loadedTravel,own);
  Check(loadedTravel.Stage==EnvoyStage.Travelling&&travelLoad.ReportsCount==0&&travelLoad.Followed==1,"loaded collector keeps travelling without a deadline");
  own.OffenderHero!.PartyBelongedTo=null;CampaignTime.Now.ToHours=20000;travelLoad.TendTravellingEnvoy(loadedTravel,own);
  Check(loadedTravel.Stage==EnvoyStage.Travelling&&travelLoad.ReportsCount==0,"loaded missing target remains pending after a further long wait");
  var resumedHero=own.OffenderHero!;var resumedParty=new MobileParty{StringId="resumed_party",LeaderHero=resumedHero};resumedHero.PartyBelongedTo=resumedParty;MobileParty.All.Add(resumedParty);
  travelLoad.Reached=true;travelLoad.TendTravellingEnvoy(loadedTravel,own);CampaignTime.Now.ToHours=10;
  Check(travelLoad.Negotiated==1&&travelLoad.LastOutcome=="paid"&&!own.HasOpenCase,"restored target is negotiated with after save/load and a long absence");
  (e,chief,helper,main,own,ec)=Setup();e._caseStrengthCooldownHours["target"]=9;e._lastRetargetHourByParty["chief"]=9;
  _peaceRetryTargets.Clear();var faction=new Clan{StringId="enemy"};Campaign.Current.Factions=new(){faction};_peaceRetryTargets[faction]=123;
  var saveClocks=new Store{IsSaving=true};e.SyncCooldownsAndRetries(saveClocks);var loadClocks=new PoliceEnforcementBehavior();saveClocks.IsSaving=false;loadClocks.SyncCooldownsAndRetries(saveClocks);
  Check(loadClocks._caseStrengthCooldownHours["target"]==9&&_peaceRetryTargets[faction]==123,"strength failure cooldown and peace deadline survive actual serializer");
  Check(loadClocks._lastRetargetHourByParty["chief"]==9,"retarget cooldown restored after loading into new behavior");
  Console.WriteLine("CASE_ENVOY_SCENARIO_ASSERTIONS="+Checks+"; PASS; lifecycle defects repaired");
 }
}
}
'''


def type_source(source, name):
    spec = importlib.util.spec_from_file_location('audit_hygiene', ROOT / 'tools/Check-CodeHygiene.py')
    hygiene = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(hygiene)
    start = re.search(r'    public class ' + name + r'\b', source).start()
    brace = source.index('{', start)
    return source[start:hygiene.match_brace(source, hygiene.code_mask(source), brace) + 1]


class CaseEnvoyScenarios(unittest.TestCase):
    def test_real_person_record_envoy_and_lifecycle_sequences(self):
        data = (MODULE / 'GwpData.cs').read_text(encoding='utf-8-sig')
        envoy = (MODULE / 'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig')
        lifecycle = (MODULE / 'PoliceEnforcementBehavior.CaseLifecycle.cs').read_text(encoding='utf-8-sig')
        assistance = (MODULE / 'PoliceEnforcementBehavior.Assistance.cs').read_text(encoding='utf-8-sig')
        player = (MODULE / 'PlayerBountyBehavior.CaseSettlement.cs').read_text(encoding='utf-8-sig')
        code = HARNESS.replace('RECORD_TYPES', type_source(data, 'CrimeRecord') + type_source(data, 'PoliceTask'))
        code = code.replace('DATA_METHODS', '\n'.join(extract(data, n) for n in ['RetargetTask', 'GetTask', 'EndTask', 'ReleaseTask', 'CloseCase', 'RemoveDeadOffenderRecords', 'GetUnassignedOpenCases', 'BeginTask', 'SyncRecord']))
        nearest_start = data.index('        public static CrimeRecord? GetNearest(Vec2 pos, Func<CrimeRecord, bool> predicate)')
        nearest = data[nearest_start:data.index(';', nearest_start) + 1]
        code = code.replace(' DATA_EXTRA', nearest)
        methods = [member(envoy, n) for n in ['FailureOutcome', 'TendTravellingEnvoy', 'TaskForCrime', 'FailEnvoy', 'ForgetEnvoyCase', 'ResolveEnvoyCase', 'SyncEnvoyData']]
        methods += [extract(lifecycle, n) for n in ['EndCaseTask', 'CloseCase']]
        methods += [member(assistance, n) for n in ['FindActiveParty', 'FailTaskBecauseOwnerCannotLead']]
        behavior = (MODULE / 'PoliceEnforcementBehavior.cs').read_text(encoding='utf-8-sig')
        methods.append(extract(behavior, 'SyncCooldownsAndRetries'))
        code = code.replace('POLICE_METHODS', '\n'.join(methods))
        code = code.replace('PLAYER_METHODS', '\n'.join(extract(player, n) for n in ['ReconcileAssignedCase', 'WithdrawCommissionClosedElsewhere']))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-case-envoy-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith(('KNOWN_DEFECT:', 'CASE_ENVOY_SCENARIO_ASSERTIONS='))))


if __name__ == '__main__':
    unittest.main()
