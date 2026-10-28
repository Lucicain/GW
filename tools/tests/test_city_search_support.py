"""Production city support lifecycle and attempt policy; map movement/capture are seams."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_city_search import read, block
from test_city_search_flow import run


STUBS = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
namespace GreyWardenPolicePurity{
interface IDataStore{bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public bool Saving=true;public bool IsSaving=>Saving;public bool IsLoading=>!Saving;public Dictionary<string,JsonElement> Data=new();public void SyncData<T>(string key,ref T value){if(Saving)Data[key]=JsonSerializer.SerializeToElement(value);else if(Data.TryGetValue(key,out var v))value=v.Deserialize<T>()!;}}
enum GwpCrimeCategory{Unknown,CaravanAttack,VillageViolence}enum AiBehavior{None,Hold}
class CharacterObject{public string StringId="gwknight";public bool IsHero,Warden=true;public float Score=20;}
class Hero{public static Hero MainHero=new();public string StringId="target",Name="target";public bool IsAlive=true,IsPrisoner,Wounded;public int Gold=10000,Valor;public CharacterObject CharacterObject=new(){IsHero=true};public Settlement? Home;public int GetTraitLevel(object trait)=>Valor;public void MakeWounded(){Wounded=true;}}
static class DefaultTraits{public static object Valor=new();}
struct Vec2{public float x;public Vec2(float a){x=a;}public float Distance(Vec2 o)=>Math.Abs(x-o.x);}
class Clan{}class Settlement{public static Settlement? FindFirst(Func<Settlement,bool> f)=>null;public string StringId="town";public Clan MapFaction=new();public Vec2 GetPosition2D=new(50);}
class Element{public CharacterObject Character=new();public int Number,WoundedNumber;}
class TroopRoster{public List<Element> Rows=new();public int TotalHealthyCount=>Rows.Sum(e=>e.Number-e.WoundedNumber);public IEnumerable<Element> GetTroopRoster()=>Rows;public void AddToCounts(CharacterObject c,int n,int woundedCount=0){var e=Rows.FirstOrDefault(e=>e.Character==c);if(e==null){e=new(){Character=c};Rows.Add(e);}e.Number+=n;e.WoundedNumber+=woundedCount;}}
class PartyBase{public int Gold;public MobileParty MobileParty=null!;}
class Army{public MobileParty LeaderParty=null!;}
class MobileParty{public static List<MobileParty> All=new();public static MobileParty MainParty=new("player");public string StringId;public bool IsActive=true,Available=true;public TroopRoster MemberRoster=new();public PartyBase Party;public object? MapEvent;public Army? Army;public AiBehavior DefaultBehavior;public Settlement? CurrentSettlement;public Clan MapFaction=new();public Vec2 GetPosition2D;public float Aggressiveness=1;public string Intent="";public MobileParty(string id){StringId=id;Party=new(){MobileParty=this};All.Add(this);}}
class CrimeRecord{public string CrimeId="target",OffenderHeroId="target";public bool HasOpenCase=true,CitySearchPersonalUsed,CitySearchSupportUsed;public Hero OffenderHero=new();public GwpCrimeCategory CrimeCategory;public Settlement? ShelterSettlement=>ShelterOf(OffenderHero);public static Settlement? ShelterOf(Hero h)=>h.IsAlive&&!h.IsPrisoner?h.Home:null;public bool IsOffenderSearchableInSettlement()=>HasOpenCase&&ShelterSettlement!=null&&!GwpPlayerCaseAssignment.IsCaseHeldByPlayer(OffenderHeroId);}
static class CrimePool{public static Dictionary<string,CrimeRecord> Records=new();public static IEnumerable<CrimeRecord> LedgerRecords=>Records.Values;public static CrimeRecord? GetRecord(Hero h)=>Records.GetValueOrDefault(h.StringId);public static CrimeRecord? GetRecordByKey(string id)=>Records.GetValueOrDefault(id);}
class Task{public string TargetCrimeId="";}static class CrimeState{public static Task? GetTask(string id)=>null;public static Dictionary<string,Task> ActiveTasks=new();}
static class PoliceStats{public static Clan Clan=new();public static List<MobileParty> Parties=new();public static Clan GetPoliceClan()=>Clan;public static List<MobileParty> GetAllPoliceParties()=>Parties;}
static class GwpTuning{public static class Enforcement{public const int EnvoySize=5;}}
static class GwpPlayerCaseAssignment{public static HashSet<string> Held=new();public static bool IsCaseHeldByPlayer(string id)=>Held.Contains(id);public static bool IsReservedTarget(MobileParty? p)=>false;}
static class GwpCommon{public static bool IsGreyWardenTroop(CharacterObject c)=>c.Warden;public static Settlement? FindNearestTown(MobileParty p)=>null;public static bool Handover=true;public static bool CanCompletePartyHandover(MobileParty a,MobileParty b)=>Handover;}
static class GwpFireswornBehavior{public static bool HasArrived(MobileParty p,Settlement s)=>p.CurrentSettlement==s||p.GetPosition2D.Distance(s.GetPosition2D)<=2;}
static class FactionManager{public static bool War;public static HashSet<Clan> Hostile=new();public static bool IsAtWarAgainstFaction(Clan a,Clan b)=>War||Hostile.Contains(b);}
static class PoliceResourceManager{public static bool IsStrandedAtSea(MobileParty p)=>false;public static int Outfits,Rewards;public static void OutfitTemporaryDutyParty(MobileParty p,MobileParty source){Outfits++;}public static void CreditSuccessfulCaseCompletion(){Rewards++;}}
static class GreyWardenPartyDesireBehavior{public static void RequestPursuit(MobileParty p,MobileParty t,float priority){}public static int Rethinks;public static void RequestImmediateRethink(MobileParty p){Rethinks++;}public static void RequestEscort(MobileParty p,MobileParty t,float priority){p.Intent="escort:"+t.StringId;}public static void RequestVisit(MobileParty p,Settlement s,float priority){p.Intent="visit:"+s.StringId;}}
class TournamentModel{public float GetTournamentSimulationScore(CharacterObject c)=>c.Score;}
class EncounterModel{public float MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy=3;}
class Models{public TournamentModel TournamentModel=new();public EncounterModel EncounterModel=new();}
class Campaign{public static Campaign Current=new();public Models Models=new();public GwpCitySearchBehavior Search=new();public T? GetCampaignBehavior<T>() where T:class=>Search as T;}
class PlayerBountyBehavior{public static bool AwaitingSupportRequest(Task? t)=>false;}class GwpCitySearchBehavior{public bool SearchActive;public PartyBase? TroopSource;public bool UsesSupportParty(string id)=>TroopSource?.MobileParty.StringId==id;public void FinishSearch(bool escaped){SearchActive=false;}}
static class GwpFieldArrestPricing{public static int AssessFine(CrimeRecord c)=>4000;public static int BaseChargeFor(CrimeRecord c)=>1000;}
static class GiveGoldAction{public static void ApplyForCharacterToParty(Hero h,PartyBase p,int amount,bool quiet){h.Gold-=amount;p.Gold+=amount;}}
static class GwpFieldReportLedger{public static int Assessed,Collected,Base;public static void SettleWithWardens(Hero h,int a,int c,int b){Assessed=a;Collected=c;Base=b;}}
static class GwpFieldArrestBehavior{public static int Deterrence;public static void RegisterFieldEnforcement(Hero h,CrimeRecord c){Deterrence++;}}
static class MBRandom{public static float RandomFloat=>1;}
static class GwpVoluntarySurrender{public static bool Allowed=true;public static int Captures;public static bool CanCapture(Hero h)=>Allowed;public static bool TryCapture(PartyBase party,Hero h){if(!Allowed)return false;Captures++;h.IsPrisoner=true;return true;}}
class InformationMessage{public string Text;public InformationMessage(string s){Text=s;}}static class InformationManager{public static List<string> Messages=new();public static void DisplayMessage(InformationMessage m){Messages.Add(m.Text);}}
static class GwpText{public static string Get(string k)=>k;public static string Create(string k,params object[] vars)=>k;}
static class GwpTextCatalog{public const string gwp_cs_support_party_name="party",gwp_cs_support_success="success",gwp_cs_support_failed="failed";}
partial class PoliceEnforcementBehavior{
 static PoliceEnforcementBehavior? _instance;public PoliceEnforcementBehavior(){_instance=this;}
 sealed class DelayPatrolState{public string PatrolPartyId="",SourceTaskPolicePartyId="",ReturnSettlementId="",CityCrimeId="",TargetPartyId="",WarTargetId="";public bool Returning,CityForPlayer,CityDelegated,IsEnvoy,IsImmediateInterceptor;}
 readonly Dictionary<string,DelayPatrolState> _delayPatrolStates=new();
 class ImmediateInterceptorTroopTransfer{public MobileParty Source=null!;public CharacterObject Character=null!;}
 bool CanAssignOrdinaryCaseNow(MobileParty p)=>p.Available;static bool CanContinueLeadingPoliceTask(MobileParty? p)=>p?.IsActive==true;static MobileParty? FindActiveParty(string id)=>MobileParty.All.FirstOrDefault(p=>p.IsActive&&p.StringId==id);
 static GwpCrimeCategory GetPreferredCrimeCategory(MobileParty p)=>GwpCrimeCategory.CaravanAttack;static bool HasEnvoyAgreement(CrimeRecord c)=>false;
 static List<ImmediateInterceptorTroopTransfer> DetachmentBatches(MobileParty source,MobileParty[] parties)=>source.MemberRoster.Rows.Where(e=>e.Number>e.WoundedNumber&&!e.Character.IsHero).Select(e=>new ImmediateInterceptorTroopTransfer{Source=source,Character=e.Character}).ToList();
 static int HealthyTroopCount(IEnumerable<ImmediateInterceptorTroopTransfer> batches)=>batches.Sum(b=>b.Source.MemberRoster.Rows.Where(e=>e.Character==b.Character).Sum(e=>e.Number-e.WoundedNumber));
 static MobileParty? CreateDetachment(MobileParty source,IEnumerable<ImmediateInterceptorTroopTransfer> batches,int size,int minimum,string name,List<ImmediateInterceptorTroopTransfer> moved,out int count,out Settlement? home){var p=new MobileParty("city"+MobileParty.All.Count);count=0;home=new();foreach(var b in batches){var e=source.MemberRoster.Rows.First(e=>e.Character==b.Character);int n=Math.Min(size-count,e.Number-e.WoundedNumber);e.Number-=n;p.MemberRoster.AddToCounts(e.Character,n);count+=n;if(count==size)break;}return p;}
 void MarkDelayPatrolReturning(string id){_delayPatrolStates[id].Returning=true;}
 static void CloseCase(CrimeRecord c,string reason){c.HasOpenCase=false;}
 Dictionary<string,Task> GetEligibleDelaySupportTasks(Clan c)=>new();bool IsActivePlayerBountyInterceptor(DelayPatrolState s,Clan? c)=>false;void DetachDelayPatrolFromArmy(MobileParty p){}bool TryReturnImmediateInterceptorToSource(MobileParty p,DelayPatrolState s)=>true;bool RetireReturningPatrol(MobileParty p,Settlement s)=>true;bool TryDestroyDelayPatrolParty(MobileParty p)=>true;bool TryAssignDelayPatrolToAssistanceArmy(MobileParty p,DelayPatrolState s)=>false;void ReconcileAssistanceFormations(){}
 public void Tick(){UpdateDelayPatrols();TendCitySearchParties();}public void Dispatch()=>DispatchCitySearchCases();internal bool Returning(MobileParty p)=>_delayPatrolStates[p.StringId].Returning;
 internal PoliceEnforcementBehavior Reload(){var save=new Store();SyncDelayPatrolStateData(save);save.Saving=false;var next=new PoliceEnforcementBehavior();next.SyncDelayPatrolStateData(save);return next;}
 SYNC_PATROL
}
}
'''

SCENARIOS = r'''
namespace GreyWardenPolicePurity{static class Program{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static PoliceEnforcementBehavior Setup(out CrimeRecord crime,out MobileParty lord,int healthy=12){MobileParty.All.Clear();MobileParty.All.Add(MobileParty.MainParty);MobileParty.MainParty.GetPosition2D=new(0);MobileParty.MainParty.MapEvent=null;CrimePool.Records.Clear();CrimeState.ActiveTasks.Clear();GwpPlayerCaseAssignment.Held.Clear();FactionManager.War=false;FactionManager.Hostile.Clear();GwpCommon.Handover=true;GwpVoluntarySurrender.Allowed=true;GwpVoluntarySurrender.Captures=0;PoliceResourceManager.Rewards=0;GwpFieldArrestBehavior.Deterrence=0;GwpFieldReportLedger.Collected=0;Campaign.Current=new();lord=new("lord");lord.MemberRoster.AddToCounts(new(),healthy);PoliceStats.Parties=new(){lord};crime=new(){OffenderHero=new(){Home=new()}};CrimePool.Records[crime.CrimeId]=crime;return new();}
 static MobileParty Detachment()=>MobileParty.All.Single(p=>p.StringId.StartsWith("city"));
 static void Main(){
  var e=Setup(out var c,out var lord);Check(GwpCitySearchCasePolicy.CanBegin(c,false)&&!c.CitySearchPersonalUsed,"looking at selection does not consume personal attempt");Check(GwpCitySearchCasePolicy.TryBegin(c,false)&&!GwpCitySearchCasePolicy.TryBegin(c,false),"personal entry happens once per case");c.HasOpenCase=false;c.HasOpenCase=true;Check(!GwpCitySearchCasePolicy.CanBegin(c,false),"abandon and reaccept cannot reset attempt");Check(GwpCitySearchCasePolicy.TryBegin(c,true)&&!GwpCitySearchCasePolicy.TryBegin(c,true),"separate assisted opportunity is also once");
  e=Setup(out c,out lord);GwpPlayerCaseAssignment.Held.Add("target");Check(e.RequestCitySearchParty(c.OffenderHero,true),"player can request real support");var p=Detachment();Check(p.MemberRoster.TotalHealthyCount==5&&lord.MemberRoster.TotalHealthyCount==7,"same five-person cap as fine collector transfers real men");Check(!CrimeState.ActiveTasks.Any()&&lord.Available,"sender has no continuing case duty");Check(p.Aggressiveness==0&&p.Intent=="escort:player","detachment follows player without initiating unrelated attacks");Check(e.CitySearchSupport(c.OffenderHero)==p&&PoliceEnforcementBehavior.IsCitySearchParty(p.Party),"arrived support has distinct party identity");
  p.GetPosition2D=new(10);Check(e.CitySearchSupport(c.OffenderHero)==null&&e.CitySearchSupport(c.OffenderHero,false)==p,"distant support cannot enter scene remotely");p.GetPosition2D=new(0);GwpCommon.Handover=false;Check(e.CitySearchSupport(c.OffenderHero)==null,"sea-land mismatch cannot lend troops across shore");GwpCommon.Handover=true;
  Check(e.RequestCitySearchParty(c.OffenderHero,false)&&MobileParty.All.Count==3&&e.CitySearchDelegated(c.OffenderHero),"same party switches to delegation without duplication");e=e.Reload();Check(e.CitySearchDelegated(c.OffenderHero)&&PoliceEnforcementBehavior.IsCitySearchParty(p.Party),"actual patrol serializer retains delegated city task and uniform identity");p.CurrentSettlement=c.ShelterSettlement;e.Tick();Check(!c.OffenderHero.IsPrisoner&&!c.HasOpenCase&&e.Returning(p)&&PoliceResourceManager.Rewards==1&&c.OffenderHero.Gold==6000&&p.Party.Gold==4000&&GwpFieldReportLedger.Collected==4000&&GwpFieldArrestBehavior.Deterrence==1,"autonomous team actually collects fine registers enforcement and returns without capture");e.Tick();Check(PoliceResourceManager.Rewards==1&&GwpFieldArrestBehavior.Deterrence==1&&p.Party.Gold==4000,"returning party cannot collect or settle twice");e=e.Reload();Check(e.Returning(p),"same-version load keeps completed detachment returning instead of processing twice");
  e=Setup(out c,out lord);lord.Available=false;Check(e.RequestCitySearchParty(c.OffenderHero,true)&&lord.Available==false,"requested support borrows men without canceling sender ongoing job just like collector");
  e=Setup(out c,out lord,2);Check(e.RequestCitySearchParty(c.OffenderHero,true)&&Detachment().MemberRoster.TotalHealthyCount==2,"short roster uses available men just like fine collector");
  e=Setup(out c,out lord,0);Check(!e.RequestCitySearchParty(c.OffenderHero,true)&&MobileParty.All.Count==2,"empty roster neither spawns nor consumes dispatch");
  e=Setup(out c,out lord);lord.MapEvent=new();Check(!e.RequestCitySearchParty(c.OffenderHero,true),"no transfer out of active battle");lord.MapEvent=null;FactionManager.War=true;Check(!e.RequestCitySearchParty(c.OffenderHero,true),"existing war prevents peaceful detachment dispatch");
  e=Setup(out c,out lord);GwpPlayerCaseAssignment.Held.Add("target");e.RequestCitySearchParty(c.OffenderHero,true);p=Detachment();Campaign.Current.Search=new(){SearchActive=true,TroopSource=p.Party};p.Intent="scene";e.Tick();Check(!e.Returning(p)&&GwpVoluntarySurrender.Captures==0&&p.Intent=="scene","paused player scene owns detachment and blocks autonomous processing");Campaign.Current.Search.SearchActive=false;p.DefaultBehavior=AiBehavior.Hold;int thinks=GreyWardenPartyDesireBehavior.Rethinks;e.Tick();Check(GreyWardenPartyDesireBehavior.Rethinks==thinks+1&&p.Intent=="escort:player","post-battle Hold wakes same native request path as collector");e.ReleaseCitySearchParty(p.StringId);Check(e.CitySearchSupport(c.OffenderHero)==null&&e.Returning(p),"completion releases support for existing return machinery");
  e=Setup(out c,out lord);e.Dispatch();p=Detachment();GwpPlayerCaseAssignment.Held.Add("target");e.Tick();Check(e.CitySearchSupport(c.OffenderHero)==p&&GwpVoluntarySurrender.Captures==0,"player taking case turns autonomous team into support instead of canceling it");
  e=Setup(out c,out lord);e.RequestCitySearchParty(c.OffenderHero,false);p=Detachment();c.OffenderHero.Home=null;e.Tick();Check(!e.Returning(p)&&c.HasOpenCase,"temporarily missing target waits without false success");c.OffenderHero.Home=new(){StringId="newtown"};e.Tick();Check(p.Intent=="visit:newtown","target moves town and detachment follows hero current shelter");p.CurrentSettlement=c.ShelterSettlement;p.MapEvent=new();e.Tick();Check(GwpVoluntarySurrender.Captures==0,"detachment battle blocks capture");p.MapEvent=null;c.OffenderHero.Valor=1;c.OffenderHero.CharacterObject.Score=100;e.Tick();Check(e.Returning(p)&&!c.HasOpenCase&&!c.OffenderHero.IsPrisoner&&!c.OffenderHero.Wounded&&p.MemberRoster.TotalHealthyCount==5&&!GwpCitySearchCasePolicy.SupportSpent(c),"pure AI collects without simulated duel or consuming player opportunity even against strong brave target");
  e=Setup(out c,out lord);c.OffenderHero.Gold=1200;e.RequestCitySearchParty(c.OffenderHero,false);p=Detachment();p.CurrentSettlement=c.ShelterSettlement;e.Tick();Check(c.OffenderHero.Gold==0&&p.Party.Gold==1200&&GwpFieldReportLedger.Assessed==4000&&GwpFieldReportLedger.Collected==1200&&GwpFieldReportLedger.Base==1000,"city collection carries actual poor purse and sends exact assessed and paid amounts to existing debt owner");
  e=Setup(out c,out lord);e.RequestCitySearchParty(c.OffenderHero,false);p=Detachment();c.OffenderHero.IsPrisoner=true;e.Tick();Check(e.Returning(p)&&c.HasOpenCase&&PoliceResourceManager.Rewards==0,"third-party captivity returns team without closing unresolved case or credit");
  e=Setup(out c,out lord);GwpPlayerCaseAssignment.Held.Add("target");e.Dispatch();Check(MobileParty.All.Count==2,"NPC dispatch cannot steal player-owned case");
  e=Setup(out c,out lord);FactionManager.Hostile.Add(c.ShelterSettlement!.MapFaction);var second=new CrimeRecord{CrimeId="other",OffenderHeroId="other",OffenderHero=new(){StringId="other",Home=new(){StringId="peaceful",GetPosition2D=new(100)}}};CrimePool.Records["other"]=second;e.Dispatch();Check(Detachment().Intent=="visit:peaceful"&&MobileParty.All.Count==3,"unreachable nearest wartime case cannot starve other peaceful city cases");
  e=Setup(out c,out lord,30);GwpPlayerCaseAssignment.Held.Add("target");e.RequestCitySearchParty(c.OffenderHero,true);p=Detachment();p.IsActive=false;lord.MapEvent=new();e=e.Reload();e.Tick();Check(e.CitySearchSupport(c.OffenderHero,false)==null&&MobileParty.All.Count==3&&!GwpCitySearchCasePolicy.SupportSpent(c),"lost support waits for real available source without consuming player entry");lord.MapEvent=null;e.Tick();var replacement=MobileParty.All.Last();Check(replacement!=p&&replacement.MemberRoster.TotalHealthyCount==5&&lord.MemberRoster.TotalHealthyCount==20&&e.Returning(p)&&e.CitySearchSupport(c.OffenderHero)==replacement,"lost support assignment survives load and transfers five real replacements");e.Tick();Check(MobileParty.All.Count==4,"healthy replacement cannot trigger duplicate dispatch");replacement.IsActive=false;e.Tick();Check(MobileParty.All.Count==5&&lord.MemberRoster.TotalHealthyCount==15,"repeated lost support can dispatch again without permanent quota");
  e=Setup(out c,out lord,30);c.CitySearchPersonalUsed=c.CitySearchSupportUsed=true;e.Dispatch();Check(MobileParty.All.Count==3,"NPC can take over after player quota spent");p=Detachment();Check(p.MemberRoster.TotalHealthyCount==5,"NPC can take over after both player opportunities are spent");p.IsActive=false;e.Tick();Check(MobileParty.All.Count==4&&e.Returning(p),"independent arrest team is redispatched after disappearance");
  e=Setup(out c,out lord,20);e.RequestCitySearchParty(c.OffenderHero,true);p=Detachment();p.MemberRoster.Rows[0].WoundedNumber=5;p.MapEvent=new();e.Tick();Check(MobileParty.All.Count==3,"all wounded support cannot be replaced while its battle still owns the roster");p.MapEvent=null;e.Tick();Check(MobileParty.All.Count==4&&e.Returning(p),"all wounded survivors return while a real fresh team takes over");
  Console.WriteLine("CITY_SUPPORT_ASSERTIONS="+n);
}}}
'''


class CitySearchSupportTests(unittest.TestCase):
    def test_lost_collector_uses_real_send_and_preserves_agreement(self):
        from test_duty_banner import method
        stubs=STUBS.replace('SYNC_PATROL','')
        stubs=stubs.replace('public bool IsActive=true,Available=true;', 'public bool IsActive=true,Available=true,IsLordParty=true;public Hero? LeaderHero=new();')
        stubs=stubs.replace('class CrimeRecord{', 'class CrimeRecord{public MobileParty? Offender;')
        stubs=stubs.replace('gwp_cs_support_party_name="party",', 'gwp_envoy_party_name="envoy",gwp_cs_support_party_name="party",')
        envoy=read('PoliceEnforcementBehavior.Envoy.cs')
        methods='\n'.join(method(envoy,n) for n in ['TrySendEnvoy','TendTravellingEnvoy','SendEnvoyFromNearestWarden','SendGraceCollector'])
        extra=r'''
namespace GreyWardenPolicePurity{
static class GwpAiDiagnostics{public static void WriteAction(MobileParty p,string action,string reason){}}
partial class PoliceEnforcementBehavior{
 enum EnvoyStage{Travelling,Failed,Grace,Aftermath}
 class EnvoyCase{public string CrimeId="",WardenPartyId="",EnvoyPartyId="",TargetPartyId="";public EnvoyStage Stage;public bool CollectOnly;}
 readonly Dictionary<string,EnvoyCase> _envoyCases=new();
 static string? EnvoyBlocker(MobileParty source,MobileParty target)=>source.MapEvent!=null?"battle":null;
 void FailEnvoy(EnvoyCase e,string reason){e.Stage=EnvoyStage.Failed;}
 static bool EnvoyReached(MobileParty a,MobileParty b)=>false;static void FollowTarget(MobileParty a,MobileParty b){}
 static void Negotiate(EnvoyCase e,CrimeRecord c,MobileParty p,Hero h,MobileParty envoy,MobileParty target,Hero offender){}
 METHODS
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void RunCollector(){
  MobileParty.All.Clear();var lord=new MobileParty("lord");lord.MemberRoster.AddToCounts(new(),20);PoliceStats.Parties=new(){lord};FactionManager.War=false;
  var target=new MobileParty("offender");var crime=new CrimeRecord{Offender=target};var e=new PoliceEnforcementBehavior();
  Check(e.TrySendEnvoy(lord,crime,target,true),"initial real collector can depart");var first=e._envoyCases[crime.CrimeId];var p=FindActiveParty(first.EnvoyPartyId)!;Check(p.MemberRoster.TotalHealthyCount==5&&lord.MemberRoster.TotalHealthyCount==15,"actual collector send transfers five from source");
  p.IsActive=false;lord.MapEvent=new();e.TendTravellingEnvoy(first,crime);Check(first.Stage==EnvoyStage.Travelling&&e._envoyCases[crime.CrimeId]==first&&lord.MemberRoster.TotalHealthyCount==15,"lost collector waits when all actual sources are in battle");
  lord.MapEvent=null;e.TendTravellingEnvoy(first,crime);var next=e._envoyCases[crime.CrimeId];var replacement=FindActiveParty(next.EnvoyPartyId)!;
  Check(next!=first&&next.CollectOnly&&replacement.MemberRoster.TotalHealthyCount==5&&lord.MemberRoster.TotalHealthyCount==10,"lost collector sends real replacement and preserves collect-only agreement");
  e.TendTravellingEnvoy(next,crime);Check(lord.MemberRoster.TotalHealthyCount==10,"existing collector does not dispatch duplicates");replacement.IsActive=false;e.TendTravellingEnvoy(next,crime);Check(lord.MemberRoster.TotalHealthyCount==5&&e._envoyCases[crime.CrimeId].CollectOnly,"repeated collector loss continues through actual dispatch");
  Console.WriteLine("CITY_COLLECTOR_REPLACEMENT_ASSERTIONS=6");
 }
}
static class Program{static void Main(){PoliceEnforcementBehavior.RunCollector();}}
}
'''.replace('METHODS',methods)
        def production(name):
            source=read(name)
            return source[source.index('namespace GreyWardenPolicePurity'):]
        # Unused city wrappers still compile against their real bodies.
        stubs=stubs.replace('public void Tick(){UpdateDelayPatrols();TendCitySearchParties();}', 'public void Tick(){TendCitySearchParties();}')
        stubs=stubs.replace('next.SyncDelayPatrolStateData(save);','').replace('SyncDelayPatrolStateData(save);','')
        city=production('PoliceEnforcementBehavior.CitySearch.cs')
        helper='namespace GreyWardenPolicePurity{partial class PoliceEnforcementBehavior{'+method(envoy,'CollectEnvoyFine')+'}}'
        code=stubs+production('GwpCitySearchCasePolicy.cs')+city+helper+extra
        run(code)
        old=code.replace('MobileParty? envoy = FindActiveParty(envoyCase.EnvoyPartyId);', 'MobileParty? envoy = FindActiveParty(envoyCase.EnvoyPartyId);if(envoy==null){FailEnvoy(envoyCase,"envoy_lost");return;}')
        with self.assertRaisesRegex(AssertionError,'lost collector waits'):
            run(old)

    def test_installed_dll_origin_controls_player_team_and_source_casualties(self):
        root=Path(__file__).resolve().parents[2]
        game=Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')
        module=root/'GreyWardenPolicePurity/bin/Release/isolated/GreyWardenPolicePurity.dll'
        source=r'''
using System;using System.IO;using System.Reflection;using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Party;using TaleWorlds.CampaignSystem.Roster;using TaleWorlds.CampaignSystem.AgentOrigins;using TaleWorlds.Core;
class Program{
 static object Empty(Type t)=>FormatterServices.GetUninitializedObject(t);
 static void Set(Type t,object o,string key,object v)=>t.GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string name=new AssemblyName(e.Name).Name+".dll";string p=Path.Combine(args[0],name);if(!File.Exists(p))p=Path.Combine(Path.GetDirectoryName(args[1]),name);return File.Exists(p)?Assembly.LoadFrom(p):null;};try{Probe(args[1]);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Probe(string path){
  Type type=Assembly.Load(File.ReadAllBytes(path)).GetType("GreyWardenPolicePurity.GwpCitySearchSupportOrigin",true);var source=(PartyBase)Empty(typeof(PartyBase));var troop=(CharacterObject)Empty(typeof(CharacterObject));var roster=TroopRoster.CreateDummyTroopRoster();typeof(PartyBase).GetProperty("MemberRoster").SetValue(source,roster);roster.AddToCounts(troop,3);
  var origin=(IAgentOriginBase)Empty(type);Set(typeof(PartyAgentOrigin),origin,"_party",source);Set(typeof(PartyAgentOrigin),origin,"_troop",troop);
  Check(origin.IsUnderPlayersCommand&&origin.IsInSameArmyAsPlayer,"actual DLL interface affiliation is player-controlled");Check(ReferenceEquals(origin.BattleCombatant,source),"actual DLL retains actual support party as combatant");
  origin.SetKilled();Check(roster.TotalManCount==2&&roster.TotalHealthyCount==2,"native inherited kill removes actual support soldier once");origin.SetWounded();Check(roster.TotalManCount==2&&roster.TotalHealthyCount==1,"native inherited wound affects actual source rather than main-party copy");
  Console.WriteLine("CITY_SUPPORT_REAL_ORIGIN_ASSERTIONS=4");
 }
}
'''
        names=['TaleWorlds.CampaignSystem','TaleWorlds.Core','TaleWorlds.Library','TaleWorlds.ObjectSystem','TaleWorlds.Localization']
        refs=''.join(f'<Reference Include="{name}"><HintPath>{str(game/name).replace("&","&amp;")}.dll</HintPath><Private>false</Private></Reference>' for name in names)
        with tempfile.TemporaryDirectory(prefix='gwp-city-support-origin-') as directory:
            p=Path(directory)
            (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup><ItemGroup>'+refs+'</ItemGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(source,encoding='utf-8')
            result=subprocess.run(['dotnet','build',str(p/'Probe.csproj'),'-c','Release','-v','quiet'],capture_output=True,text=True,timeout=90)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            result=subprocess.run([str(p/'bin/Release/net472/Probe.exe'),str(game),str(module)],capture_output=True,text=True,timeout=30)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            print(result.stdout.strip())

    def test_production_support_coordinator_consumes_on_entry_and_keeps_source(self):
        from test_city_search_flow import STUBS as flow_stubs
        support=read('GwpCitySearchBehavior.Support.cs')
        flow=read('GwpCitySearchBehavior.Flow.cs')
        def production(value):
            return value[value.index('namespace GreyWardenPolicePurity'):]
        fake_start=flow_stubs.index('partial class GwpCitySearchBehavior{void SyncStreetBodies')
        fake_end=flow_stubs.index('\nclass SupportEnforcement',fake_start)
        stubs=flow_stubs[:fake_start]+'partial class GwpCitySearchBehavior{void SyncStreetBodies(IDataStore s){}void ClearStreetBodies(){}}'+flow_stubs[fake_end:]
        stubs=stubs.replace('class Campaign{','class Campaign{public PoliceEnforcementBehavior Enforcer=new();')
        stubs=stubs.replace('typeof(T)==typeof(GwpCitySearchBehavior)?Search:Bounty as object','typeof(T)==typeof(GwpCitySearchBehavior)?Search:typeof(T)==typeof(PoliceEnforcementBehavior)?Enforcer:Bounty as object')
        stubs=stubs.replace('static class CrimeRecord{','class CrimeRecord{public bool HasOpenCase=true,CitySearchPersonalUsed,CitySearchSupportUsed;')
        extra=r'''
namespace GreyWardenPolicePurity{
class MobileParty{public static List<MobileParty> All=new();public static MobileParty MainParty=new(){StringId="main",Party=PartyBase.MainParty};public string StringId="support";public bool IsActive=true;public PartyBase Party=new();public MobileParty(){All.Add(this);}}
class PoliceEnforcementBehavior{public MobileParty? Team;public bool Arrived=true,Delegated;public int Releases;public MobileParty? CitySearchSupport(Hero h,bool requireArrival=true)=>!requireArrival||Arrived?Team:null;public bool CitySearchDelegated(Hero h)=>Delegated;public bool RequestCitySearchParty(Hero h,bool follow)=>true;public void ReleaseCitySearchParty(string s){Releases++;}}
static class CrimePool{public static CrimeRecord Record=new();public static CrimeRecord GetRecord(Hero h)=>Record;}
class CampaignGameStarter{public void AddGameMenuOption(string m,string id,string text,Func<MenuCallbackArgs,bool> condition,Action<MenuCallbackArgs> action){}}
class MenuCallbackArgs{public GameMenuOption.LeaveType optionLeaveType;}class GameMenuOption{public enum LeaveType{Submenu}}
static class GameMenu{public static void SwitchToMenu(string s){}}
class InformationMessage{public InformationMessage(string s){}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
static class GwpText{public static string Get(string s)=>s;}
static class GwpTextCatalog{KEYS}
partial class GwpCitySearchBehavior{const string SearchMenu="search";}
}
'''
        keys=set(re.findall(r'GwpTextCatalog\.(\w+)',support))
        extra=extra.replace('KEYS',''.join(f'public const string {key}="{key}";' for key in keys))
        stubs=stubs.replace('public int Registered;public bool RegisterCaptureInCustody()', 'public Hero? CitySearchTargetIn(Settlement? s)=>Hero.All.Last();public int Registered;public bool RegisterCaptureInCustody()')
        support=support.replace('TaleWorlds.CampaignSystem.Settlements.','')
        scenario=r'''
namespace GreyWardenPolicePurity{static class Program{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}static void Close(){Mission.Current?.Close();}
 static void Main(){
  var town=new Settlement();Settlement.CurrentSettlement=town;var target=new Hero("accused");CrimeRecord.Shelter=town;var troop=new CharacterObject("foot");var roster=TroopRoster.CreateDummyTroopRoster();roster.AddToCounts(troop,3);var b=Campaign.Current.Search;
  b.Start(town,target,roster);Check(b.Pending&&!CrimePool.Record.CitySearchPersonalUsed,"selecting troops and queuing scene does not consume opportunity");GwpCitySearchBehavior.Pump();Check(CrimePool.Record.CitySearchPersonalUsed,"first actual entry consumes personal opportunity");b.PauseSearch();Close();b.FinishSearch(false);b.Start(town,target,roster);Check(!b.SearchActive,"same case cannot start again after explicit end");
  var team=new MobileParty();team.Party.MemberRoster.AddToCounts(troop,2);Campaign.Current.Enforcer.Team=team;Campaign.Current.Enforcer.Arrived=false;b.Start(town,target,roster);Check(!b.SearchActive,"support still far away cannot bypass personal limit");Campaign.Current.Enforcer.Arrived=true;b.Start(town,target,roster);Check(b.Supported&&b.TroopSource==team.Party&&!CrimePool.Record.CitySearchSupportUsed,"arrived support queues its actual roster without consuming before entry");
  GwpCitySearchBehavior.Pump();Check(CrimePool.Record.CitySearchSupportUsed&&GwpCitySearchMission.Last.Total==2,"entry clamps selected troops to real support roster rather than player copies");b.SaveSurvivors(new[]{(troop,13f)});b.PauseSearch();Close();var store=new Store();b.Sync(store);store.Loading=true;var loaded=new GwpCitySearchBehavior();loaded.Sync(store);Campaign.Current.Search=loaded;loaded.QueuePlace(GwpCitySearchPlace.Hall);GwpCitySearchBehavior.Pump();Check(loaded.Supported&&loaded.TroopSource==team.Party&&GwpCitySearchMission.Last.Total==1&&loaded.SavedHealth("foot",0)==13,"paused support resumes same source and wounds across actual flow save/load without consuming twice");
  Close();loaded.FinishSearch(false);Check(Campaign.Current.Bounty.Failures==1&&Campaign.Current.Bounty.Agreements==0,"actual supported failure is distinct from duel agreement");Check(Campaign.Current.Enforcer.Releases==1,"support failure releases real detachment once");loaded.Start(town,target,roster);Check(!loaded.SearchActive,"both spent attempts forbid unlimited retries");
  CrimePool.Record=new();b=new();Campaign.Current.Search=b;b.Start(town,target,roster);GwpCitySearchBehavior.Pump();b.PauseSearch();Close();b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();CampaignMission.Initialize();CampaignMission.Callback!(target.CharacterObject);Check(Campaign.Current.Bounty.Agreements==1&&Campaign.Current.Bounty.Failures==1&&!target.IsPrisoner&&!b.SearchActive,"real arena loss callback completes supported agreement without capture or failure");Close();
  CrimePool.Record=new();b=new();Campaign.Current.Search=b;b.Start(town,target,roster);b.FinishSearch(false);Check(Campaign.Current.Bounty.Failures==1,"cancel before actual scene entry does not fail commission");
  Console.WriteLine("CITY_SUPPORT_COORDINATOR_ASSERTIONS="+n);
 }
}}
'''
        run(stubs+extra+production(read('GwpCitySearchCasePolicy.cs'))+production(support)+production(flow)+scenario)

    def test_commander_uniform_is_scoped_and_cloned(self):
        from test_duty_banner import method
        armor=method(block(read('GwpFireswornArmory.cs'),r'internal static class GwpArmorySpawnPatch'),'Prefix')
        uniform=method(read('PoliceResourceManager.cs'),'CitySearchUniform')
        code=r'''
using System;using System.Linq;
class Equipment{public string[] Items=new[]{"black-helm","black-armor","black-shield"};public Equipment Clone()=>new(){Items=(string[])Items.Clone()};}
class CharacterObject{public static CharacterObject Template=new();public bool IsHero;public Equipment FirstBattleEquipment=new();public static CharacterObject Find(string id)=>Template;}
class PartyBase{public bool Support;}
class PartyAgentOrigin{public PartyBase Party=new();public int UniqueSeed=>1;}class PartyGroupAgentOrigin{public PartyBase Party=new();public int UniqueSeed=>1;}class SimpleAgentOrigin{public PartyBase Party=new();public int UniqueSeed=>1;}
class AgentBuildData{public bool AgentCivilianEquipment;public CharacterObject AgentCharacter=new();public dynamic AgentOrigin=new PartyAgentOrigin();public Equipment? AgentOverridenSpawnEquipment;public void Equipment(Equipment e){AgentOverridenSpawnEquipment=e;}}
class Campaign{public static object? Current=new();}
static class PoliceEnforcementBehavior{public static bool IsCitySearchParty(PartyBase? p)=>p?.Support==true;}
static class GwpCommon{public static bool IsGreyWardenTroop(CharacterObject c)=>!c.IsHero;}
static class GwpIds{public const string CommanderEquipmentRosterId="commander";}
class MBEquipmentRoster{public Equipment DefaultEquipment=CharacterObject.Template.FirstBattleEquipment;}
class MBObjectManager{public static MBObjectManager Instance=new();public T GetObject<T>(string id)=>(T)(object)new MBEquipmentRoster();}
static class PoliceResourceManager{__UNIFORM_SOURCE__}
static class GwpFireswornBehavior{public static int Calls;public static Equipment? TakeKitForAgent(PartyBase? p,CharacterObject c,int seed){Calls++;return null;}}
class Patch{__ARMOR_SOURCE__ public static void Spawn(AgentBuildData b)=>Prefix(b);}
class Program{static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}static void Main(){
 var b=new AgentBuildData{AgentOrigin=new PartyAgentOrigin{Party=new(){Support=true}}};Patch.Spawn(b);Check(b.AgentOverridenSpawnEquipment!=null&&b.AgentOverridenSpawnEquipment.Items.Contains("black-shield")&&GwpFireswornBehavior.Calls==0,"support receives commander black kit through existing spawn entry");
 b.AgentOverridenSpawnEquipment!.Items[0]="changed";Check(CharacterObject.Template.FirstBattleEquipment.Items[0]=="black-helm","support kit clone never changes shared commander template");
 b=new();Patch.Spawn(b);Check(b.AgentOverridenSpawnEquipment==null&&GwpFireswornBehavior.Calls==1,"ordinary patrol retains existing armory path");
 b=new(){AgentCivilianEquipment=true,AgentOrigin=new PartyAgentOrigin{Party=new(){Support=true}}};Patch.Spawn(b);Check(b.AgentOverridenSpawnEquipment==null,"civilian and character preview requests are unchanged");
 var original=new Equipment();b=new(){AgentOverridenSpawnEquipment=original,AgentOrigin=new PartyAgentOrigin{Party=new(){Support=true}}};Patch.Spawn(b);Check(ReferenceEquals(b.AgentOverridenSpawnEquipment,original),"explicit corpse or scripted equipment remains authoritative");
 Console.WriteLine("CITY_SUPPORT_UNIFORM_ASSERTIONS="+n);
}}
'''.replace('__UNIFORM_SOURCE__',uniform).replace('__ARMOR_SOURCE__',armor)
        run(code)

    def test_actual_support_lifecycle_and_attempts(self):
        def production(name):
            value=read(name)
            return value[value.index('namespace GreyWardenPolicePurity'):]
        city=production('PoliceEnforcementBehavior.CitySearch.cs')
        from test_duty_banner import method
        stubs=STUBS.replace('SYNC_PATROL','\n'.join(method(read('PoliceEnforcementBehavior.DelayPatrols.cs'),n) for n in ['SyncDelayPatrolStateData','UpdateDelayPatrols'])+'\n'+method(read('PoliceEnforcementBehavior.Envoy.cs'),'CollectEnvoyFine'))
        code=stubs+production('GwpCitySearchCasePolicy.cs')+city+SCENARIOS
        run(code)
        for old, failure in [
            (code.replace('!crime.CitySearchPersonalUsed','true'),'personal entry happens once'),
            (code.replace('CollectEnvoyFine(crime, party, target, assessed)', '0'), 'autonomous team actually collects fine'),
            (code.replace('if (state.Returning || string.IsNullOrEmpty(state.CityCrimeId)) _delayPatrolStates.Remove(kv.Key);', '_delayPatrolStates.Remove(kv.Key);'), 'lost support assignment survives load'),
            (code.replace('|| !crime.HasOpenCase || CityPartyState(crime) != replacing', '|| !crime.HasOpenCase || crime.CitySearchSupportUsed || CityPartyState(crime) != replacing'),'NPC can take over'),
            (code.replace('foreach (MobileParty source in PoliceStats.GetAllPoliceParties().Where(CanContinueLeadingPoliceTask)\n                                 .OrderBy', 'foreach (MobileParty source in Enumerable.Empty<MobileParty>()\n                                 .OrderBy'), 'lost support assignment survives load'),
            (code.replace('GwpTuning.Enforcement.EnvoySize','15'),'same five-person cap'),
            (code.replace('search?.SearchActive == true && search.UsesSupportParty(state.PatrolPartyId)','false'),'paused player scene owns'),
            (code.replace('!requireArrival || CityPartyReached(party, MobileParty.MainParty)', 'true'), 'distant support cannot enter'),
        ]:
            with self.subTest(negative_control=failure),self.assertRaisesRegex(AssertionError,failure):
                run(old)


if __name__=='__main__':
    unittest.main()
