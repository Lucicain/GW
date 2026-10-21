"""Check failure-only army argument capture and the native null-hero precondition.

Production envoy surrender and installed capture/hourly/cost/relation bodies run together.
Negotiation outcomes, case settlement, entities, event receivers and the trace sink
are controlled boundaries; no campaign is started.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
GAME_DLL = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;using ArmyManagementCalculationModel=NativeArmyModel;using MathF=NativeMathF;
static class NativeMathF{public static float Min(float a,float b)=>System.MathF.Min(a,b);public static float Max(float a,float b)=>System.MathF.Max(a,b);public static float Abs(float a)=>System.MathF.Abs(a);public static float Sqrt(float a)=>System.MathF.Sqrt(a);public static float Pow(float a,float b)=>System.MathF.Pow(a,b);public static int Round(float a)=>(int)System.MathF.Round(a);public static int Ceiling(float a)=>(int)System.MathF.Ceiling(a);}
class Clan {public Hero? Leader;public int Tier=1;public float Influence=100,Renown;public Kingdom? Kingdom;}
class Hero {public string StringId="hero";public string Name=>StringId;public Clan? Clan;public bool IsPlayerCompanion,IsHumanPlayerCharacter;
 public enum CharacterStates{Active,Prisoner}public static Hero MainHero=new();public MobileParty? PartyBelongedTo;public PartyBase? PrisonerParty;public Settlement? StayingInSettlement;public CampaignTime? CaptivityStartTime;public CharacterStates State;public CharacterObject CharacterObject;public Hero(){CharacterObject=new(){HeroObject=this};}public void ChangeState(CharacterStates state){State=state;}
 public int Gold=4925;public int GetSkillValue(object skill)=>241;public int GetTraitLevel(object trait)=>0;
 public float GetRelation(Hero other)=>Campaign.Current.Models.DiplomacyModel.GetEffectiveRelation(this,other);
 public float RandomFloat(float min,float max)=>1;public bool GetPerkValue(Perk p)=>false;}
class Policy{public string Name="policy";}
class Perk{public float PrimaryBonus,SecondaryBonus;public string Name="perk";}
class Feat{public float EffectBonus;}
static class DefaultPolicies {public static Policy Marshals=new(),RoyalCommissions=new(),LordsPrivyCouncil=new(),Senate=new();}
static class DefaultPerks {public static class Leadership{public static Perk InspiringLeader=new();}public static class Tactics{public static Perk CallToArms=new(),Encirclement=new(),HordeLeader=new();}public static class Engineering{public static Perk CampBuilding=new();}}
static class PerkHelper{public static void AddPerkBonusForParty(Perk perk,MobileParty p,bool isPrimaryBonus,ref ExplainedNumber result,bool atSea){}}
static class DefaultCulturalFeats{public static Feat VlandianArmyInfluenceFeat=new(),SturgianArmyInfluenceCostFeat=new();}
static class PartyBaseHelper{public static bool HasFeat(PartyBase p,Feat f)=>false;}
static class CharacterRelationManager{public static int GetHeroRelation(Hero a,Hero b)=>0;}
class Kingdom {public string StringId="kingdom";public List<Policy> ActivePolicies=new();public Hero? Leader;public bool IsKingdomFaction;}
class CharacterObject{public Hero HeroObject=null!;public bool IsHero;}
class TroopRosterElement{public CharacterObject Character=new();public int Number=76;}
class CaptureRoster{public IEnumerable<TroopRosterElement> GetTroopRoster()=>new[]{new TroopRosterElement()};public bool Contains(CharacterObject c)=>true;public void RemoveTroop(CharacterObject c){c.HeroObject.PartyBelongedTo=null;}}
static class Debug{public static void FailedAssert(string message,string path,string method,int line)=>throw new Exception(message);}
class CampaignEventDispatcher{public static CampaignEventDispatcher Instance=new();public void OnPartyLeaderChanged(MobileParty p,Hero? oldLeader){}public void OnHeroPrisonerTaken(PartyBase capturer,Hero prisoner){}}
static class PlayerCaptivity{public static void StartCaptivity(PartyBase p){}}
static class DestroyShipAction{public static void Apply(object ship){}}
class PartyBase {public float EstimatedStrength=100;public Hero Owner=new(){Clan=new()};public bool IsStarving;public int NumberOfHealthyMembers=50;public void AddPrisoner(CharacterObject c,int count){c.HeroObject.PrisonerParty=this;}}
class PartyComponent {public MobileParty MobileParty=null!;public virtual Hero? Leader=>null;public string Name="party";protected virtual void OnChangePartyLeader(Hero? newLeader){}PARTY_LEADER_CHANGE}
class LordPartyComponent:PartyComponent{public Hero? _leader;public override Hero? Leader=>_leader;public void ClearCachedName(){}public void ChangePartyOwner(Hero h){}LORD_LEADER_CHANGE}
static class NativeCapture{PRISONER_ACTION_BODY public static void Capture(PartyBase capturer,Hero prisoner)=>ApplyInternal(capturer,prisoner);}
enum AiBehavior{None,BesiegeSettlement}
class Settlement {public object? SiegeEvent;}
class BesiegerCamp{public bool IsBesiegerSideParty(MobileParty p)=>false;}
class SiegeEvent{public BesiegerCamp BesiegerCamp=new();}
class MobileParty {public static MobileParty MainParty=new();public string StringId="party";private Hero? _leaderHero;public PartyComponent? PartyComponent;public Hero? LeaderHero{get=>PartyComponent==null?_leaderHero:PartyComponent.Leader;set=>_leaderHero=value;}public bool IsActive=true;public Army? Army;
 public CaptureRoster MemberRoster=new();public bool IsDisorganized;public List<object> Ships=new();public void SetDisorganized(bool isDisorganized){IsDisorganized=isDisorganized;}public void SetMoveModeHold(){}REMOVE_PARTY_LEADER
 public PartyBase Party=new();public float PartySizeRatio=1;public bool IsMainParty,IsCurrentlyAtSea,IsTargetingPort;public int NavigationCapability;
 public Kingdom MapFaction=new();public object? MapEvent;public SiegeEvent? SiegeEvent;public Settlement? CurrentSettlement,BesiegedSettlement,TargetSettlement;public AiBehavior DefaultBehavior;public List<MobileParty> AttachedParties=new();public float Morale=100;public bool HasPerk(Perk p)=>false;}
class MBCampaignEvent{}
static class MBRandom{public static float RandomFloat=1;}
class ExplainedNumber {public float ResultNumber;public ExplainedNumber(float n,bool descriptions=false){ResultNumber=n;}public void Add(float n,string name){ResultNumber+=n;}public void AddFactor(float n,string name=""){ResultNumber*=1+n;}}
static class DistanceHelper {public static float GetDistanceBetweenMobilePartyToMobileParty(MobileParty a,MobileParty b,int nav,out float land){land=1;return 10;}}
class MapDistanceModel {public float GetDistance(MobileParty p,Settlement? s,bool port,int nav,out float land){land=1;return 10;}public float GetDistance(Settlement a,Settlement? b,bool sea,bool port,int nav)=>10;}
class TargetModel{public float CurrentObjectiveValue(MobileParty p)=>.2f;}
class Models{public NativeArmyModel ArmyManagementCalculationModel=new();public NativeDiplomacy DiplomacyModel=new();public MapDistanceModel MapDistanceModel=new();public TargetModel TargetScoreCalculatingModel=new();}
class Campaign{public static Campaign Current=new();public static float MapDiagonal=1000;public Models Models=new();public float GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(int nav)=>100;}
class CampaignTime{public const int HoursInDay=24;public static CampaignTime Now=new();public double ToHours=2192119.81;}
class Army {public MobileParty? LeaderParty;public Kingdom? Kingdom;public List<MobileParty> Parties=new();public bool ActiveAssistance;public float Cohesion=49;public float DailyCohesionChange=>Campaign.Current.Models.ArmyManagementCalculationModel.CalculateDailyCohesionChange(this).ResultNumber;int _numberOfBoosts;float _armyGatheringStartTime=1;
 public int Boosts;void BoostCohesionWithInfluence(float amount,int cost){Cohesion+=amount;Boosts++;}
 void RecalculateArmyMorale(){}void CheckAndSetArmyGatheringTime(){}void MoveLeaderToGatheringLocationIfNeeded(){}bool IsAnotherEnemyBesiegingTarget()=>false;void FinishArmyObjective(){}void CheckArmyDispersion(){}void ApplyHostileActionInfluenceAwards(){}
 ARMY_METHODS
 public void RunHour()=>HourlyTick(null!,new object[0]);
}
static class DisbandArmyAction{public static void ApplyByCohesionDepleted(Army a){}}
class NativeArmyModel {
 string _numberOfPartiesText="parties",_numberOfStarvingPartiesText="starving",_numberOfLowMoralePartiesText="morale",_numberOfLessMemberPartiesText="small";
 public float PlayerMobilePartySizeRatioToCallToArmy=>.4f;public float AIMobilePartySizeRatioToCallToArmy=>.6f;public int AverageCallToArmyCost=>20;
 public float GetPartySizeScore(MobileParty p)=>p.PartySizeRatio;
 COST_METHODS
 public int CalculatePartyInfluenceCost(MobileParty a,MobileParty b){int result=0;return CostPatch.Run(a,b,ref result)?NativePartyInfluenceCost(a,b):result;}
 public ExplainedNumber CalculateDailyCohesionChange(Army a,bool includeDescriptions=false){var result=NativeCohesionChange(a,includeDescriptions);CohesionPatch.Run(a,includeDescriptions,ref result);return result;}
}
static class GwpCommon {public static bool IsEnforcementDelayPatrolParty(MobileParty? p)=>p?.StringId.StartsWith("gwp_enf_delay_")==true;}
partial class PoliceEnforcementBehavior {public static bool IsActiveAssistanceArmy(Army? a)=>a?.ActiveAssistance==true;}
enum GwpOffenderDesire{SurrenderSelf,RequestGrace,DemandDuel,BribeTheWarden,DeedsOnly,PayInFull}
static class GwpOffenderDesires{public static GwpOffenderDesire Choice=GwpOffenderDesire.SurrenderSelf;public static GwpOffenderDesire Roll(Hero h,int fine,bool grace,bool troops)=>Choice;public static int OfferedSharePercent(GwpOffenderDesire d)=>100;public static void SacrificeOwnMen(MobileParty p)=>throw new Exception("unexpected sacrifice");}
static class DefaultSkills{public static object Charm=new();}static class DefaultTraits{public static object Honor=new(),Mercy=new(),Generosity=new();}
class CrimeRecord{public int CrimeCategory;public bool Closed;}
class PoliceTask{public string PolicePartyId="warden";}
static class GwpFieldArrestPricing{public static int AssessFine(CrimeRecord c)=>1300;}
static class GwpAssetPayment{public static int Wealth(Hero h,PartyBase p)=>8948;}
static class GwpStrength{public static MobileParty CombatGroupOf(MobileParty p)=>p;public static float CombatGroup(MobileParty p)=>p.Party.EstimatedStrength;}
static class GwpFieldArrestBehavior{public enum WardenStandingTier{Ordinary}public static void RegisterFieldEnforcement(Hero h,CrimeRecord c){}}
static class GwpAiDeterrenceState{public static void ClearArrestRecord(Hero h){}}
static class GwpNegotiationResistance{public const int LayerOneReservations=3,LayerTwoReservations=3;public static int LayerOneResistance(Hero h,int category,float ours,float theirs,GwpFieldArrestBehavior.WardenStandingTier tier,float renown,int wealth,int fine)=>16;public static int LayerTwoResistance(Hero h,GwpOffenderDesire d,int charm,float renown,int wealth,int fine)=>0;public static bool SimulatePersuasion(int reservations,int resistance,bool first,int charm)=>true;}
static class GwpNegotiationPolicy{public static double RefusalChance(int resistance,int honor,int mercy,int generosity,int wealth,int fine)=>0;}
static class GwpTuning{public static class FieldArrest{public const int GraceHours=72,BribeSharePercent=20;}public static class Enforcement{public const int EnvoyAftermathWaitHours=72;}}
static class GiveGoldAction{public static void ApplyBetweenCharacters(Hero from,Hero to,int amount,bool disable)=>throw new Exception("unexpected bribe");}
static class TakePrisonerAction{public static void Apply(PartyBase capturer,Hero prisoner)=>NativeCapture.Capture(capturer,prisoner);}
partial class PoliceEnforcementBehavior{
 enum EnvoyStage{Travelling,Aftermath,Grace}enum CaseTaskEnd{Released}
 class EnvoyCase{public string CrimeId="crime",EnvoyPartyId="envoy",TargetPartyId="offender";public bool CollectOnly;public EnvoyStage Stage;public int PendingDesire=-1;public double DueHours;}
 class DelayPatrolState{public bool Returning;}
 Dictionary<string,EnvoyCase> _envoyCases=new();Dictionary<string,DelayPatrolState> _delayPatrolStates=new();Dictionary<string,MobileParty> _parties=new();public string Report="";public int CapturesBeforeBattleEnd;
 void SettleThroughEnvoy(EnvoyCase c,CrimeRecord crime,MobileParty envoy,Hero offender,int fine,string reason)=>throw new Exception("unexpected payment");
 void FailEnvoy(EnvoyCase c,string reason)=>throw new Exception("unexpected failed negotiation");
 PoliceTask? TaskForCrime(CrimeRecord c)=>null;void EndCaseTask(string id,CaseTaskEnd end,string reason){}void StartDuel(EnvoyCase c,MobileParty police,MobileParty target)=>throw new Exception("unexpected duel");
 void RecordEnvoyReport(EnvoyCase c,string outcome,int amount){Report=outcome;}void ResolveEnvoyCase(EnvoyCase c,CrimeRecord crime,bool clear,string reason){crime.Closed=true;_envoyCases.Remove(c.CrimeId);}
 MobileParty? FindActiveParty(string id)=>_parties.GetValueOrDefault(id);bool EnvoyReached(MobileParty envoy,MobileParty target)=>true;void FollowTarget(MobileParty envoy,MobileParty target){}
 void MarkDelayPatrolReturning(string id){}void ForgetEnvoyCase(EnvoyCase c,string reason){_envoyCases.Remove(c.CrimeId);}
 ENVOY_PRODUCTION_METHODS
 public void Surrender(MobileParty warden,MobileParty envoy,MobileParty target,bool battle){var c=new EnvoyCase{EnvoyPartyId=envoy.StringId,TargetPartyId=target.StringId};_envoyCases[c.CrimeId]=c;_parties[envoy.StringId]=envoy;_parties[target.StringId]=target;var crime=new CrimeRecord();Negotiate(c,crime,warden,warden.LeaderHero!,envoy,target,target.LeaderHero!);if(!crime.Closed||Report!="surrender")throw new Exception("production did not resolve surrender");if(battle){TendAftermath(c);CapturesBeforeBattleEnd=target.LeaderHero==null?1:0;target.MapEvent=null;TendAftermath(c);}}
}
static class GwpFaultTrace {public static List<string> Lines=new();public static void Write(string stage,string details)=>Lines.Add(stage+" | "+details);}
class NativeDiplomacy {
 public int MinRelationLimit=-100,MaxRelationLimit=100;int GetBaseRelationBetweenHeroes(Hero a,Hero b)=>0;void GetPersonalityEffects(ref int value,Hero a,Hero b){}
 NATIVE_METHOD
}
static class MBMath{public static int ClampInt(int value,int min,int max)=>Math.Clamp(value,min,max);}
static class CostPatch {
 COST_PATCH
 public static bool Run(MobileParty a,MobileParty b,ref int result)=>Prefix(a,b,ref result);
}
static class CohesionPatch {
 COHESION_PATCH
 public static void Run(Army a,bool descriptions,ref ExplainedNumber r)=>Postfix(a,descriptions,ref r);
}
static class Capture {
 CAPTURE_METHOD
 public static Exception? Run(MobileParty? a,MobileParty? p,Exception? e)=>Finalizer(a!,p!,e);
}
static class TotalCapture {
 TOTAL_CAPTURE_METHOD
 public static Exception? Run(Army? a,Exception? e)=>Finalizer(a!,25f,e);
}
class Program {
 static int checks;static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
 static void Main(){
  var leader=new MobileParty{StringId="gw_leader_0_party_1",LeaderHero=new(){StringId="gw_leader_0"}};
  var member=new MobileParty{StringId="gwp_enf_delay_1"};
  var army=new Army{LeaderParty=leader};army.Parties.Add(leader);army.Parties.Add(member);leader.Army=member.Army=army;
  Check(Capture.Run(leader,member,null)==null&&GwpFaultTrace.Lines.Count==0,"successful cost calculation remains silent");
  var failure=new NullReferenceException("native relation");
  Check(ReferenceEquals(failure,Capture.Run(leader,member,failure)),"capture preserves original exception");
  string line=GwpFaultTrace.Lines.Single();
  Check(line.Contains("leaderParty=gw_leader_0_party_1")&&line.Contains("leaderHero=gw_leader_0")&&line.Contains("memberParty=gwp_enf_delay_1")&&line.Contains("memberHero=-"),"exact arguments identify which hero is missing");
  Check(line.Contains("activeAssistance=False")&&line.Contains("memberIsDelay=True")&&line.Contains("gw_leader_0_party_1:gw_leader_0,gwp_enf_delay_1:-"),"inactive case and physical membership captured independently");
  member.StringId="native-party";member.Army=null;army.Kingdom=new();Capture.Run(leader,member,failure);
  Check(GwpFaultTrace.Lines.Last().Contains("memberIsDelay=False")&&GwpFaultTrace.Lines.Last().Contains("armyKingdom=kingdom"),"native army failures are identifiable rather than attributed to wardens");
  Check(ReferenceEquals(failure,Capture.Run(null,null,failure)),"missing argument capture cannot replace exception");
  var native=new NativeDiplomacy();
  bool missingMember=false,missingLeader=false;
  try{native.GetHeroesForEffectiveRelation(leader.LeaderHero!,null!,out _,out _);}catch(NullReferenceException){missingMember=true;}
  try{native.GetHeroesForEffectiveRelation(null!,leader.LeaderHero!,out _,out _);}catch(NullReferenceException){missingLeader=true;}
  Check(missingMember&&missingLeader,"installed native relation method rejects either missing hero");
  var other=new Hero{StringId="other"};native.GetHeroesForEffectiveRelation(leader.LeaderHero!,other,out var a,out var b);
  Check(a==leader.LeaderHero&&b==other,"normal native relation resolution remains valid");
  int before=GwpFaultTrace.Lines.Count;TotalCapture.Run(army,null);Check(GwpFaultTrace.Lines.Count==before,"successful total cost remains silent");
  leader.Army=null;
  Check(ReferenceEquals(failure,TotalCapture.Run(army,failure)),"actual army capture preserves original exception");
  Check(GwpFaultTrace.Lines.Last().Contains("leaderParty=gw_leader_0_party_1")&&GwpFaultTrace.Lines.Last().Contains("leaderPointsToThisArmy=False")&&GwpFaultTrace.Lines.Last().Contains("native-party:hero=-:delay=False:pointsToThisArmy=False"),"stale army callback captured even when leader and member no longer reference it");
  Check(ReferenceEquals(failure,TotalCapture.Run(null,failure)),"missing actual army does not replace exception");
  // Run the complete installed managed hourly/cohesion/cost/relation bodies.
  // Cohesion and logical case validity are explicit boundary inputs, not a historical snapshot.
  var clan=new Clan();var chief=new MobileParty{StringId="chief",LeaderHero=new(){Clan=clan}};clan.Leader=chief.LeaderHero;
  var fallback=new MobileParty{StringId="gwp_enf_delay_old"};
  var formation=new Army{LeaderParty=chief,ActiveAssistance=true};formation.Parties.AddRange(new[]{chief,fallback});chief.Army=fallback.Army=formation;
  formation.RunHour();Check(formation.Boosts==0,"valid support formation costs zero through installed native chain");
  formation.ActiveAssistance=false;bool reproduced=false;
  try{formation.RunHour();}catch(NullReferenceException error){reproduced=error.StackTrace!.Contains("GetHeroesForEffectiveRelation")&&error.StackTrace.Contains("ThinkAboutCohesionBoost")&&error.StackTrace.Contains("HourlyTick");}
  Check(reproduced,"inactive case plus still-enrolled leaderless support reproduces observed hourly-to-relation stack");
  formation.Parties.Remove(fallback);fallback.Army=null;formation.RunHour();
  Check(fallback.LeaderHero==null&&fallback.Army==null,"independent fallback no longer enters native army fee loop");
  var helper=new MobileParty{StringId="helper",LeaderHero=new(){Clan=clan},Army=formation};formation.Parties.Add(helper);formation.RunHour();
  Check(formation.Boosts==0,"same-clan lord army still has zero cost without any temporary party");
  formation.Parties.Remove(helper);formation.Parties.Add(fallback);fallback.Army=formation;formation.ActiveAssistance=true;formation.Cohesion=100;
  for(int hour=0;hour<1000;hour++)formation.RunHour();
  Check(formation.Cohesion==100,"1000 hours of valid assistance with leaderless support preserve cohesion100 and do not reproduce crash");
  formation.ActiveAssistance=false;int elapsed=0;bool delayedFailure=false;
  try{for(;elapsed<1000;elapsed++)formation.RunHour();}catch(NullReferenceException){delayedFailure=true;}
  Check(delayedFailure&&elapsed>0,"retaining an invalid formation eventually reaches the same null-hero fee failure through native daily decay");
  Console.WriteLine("NATIVE_ARMY_REPLAY: valid assistance 1000h PASS; invalid retained army failed at hour="+elapsed+"; retained-invalid-state is an explicit fixture, not established historical fact");
  // A current-launch surrender involved a lord registered in a native kingdom army.
  // Execute the installed capture/leader-removal bodies rather than pre-setting a null hero.
  var nativeChief=new MobileParty{StringId="lord_5_1_party_1",LeaderHero=new(){Clan=clan}};
  var offender=new Hero{StringId="lord_B8_s",Clan=clan};
  var offenderParty=new MobileParty{StringId="lord_B8_s_party_1"};offenderParty.PartyComponent=new LordPartyComponent{MobileParty=offenderParty,_leader=offender};offender.PartyBelongedTo=offenderParty;
  var nativeArmy=new Army{LeaderParty=nativeChief,Kingdom=new(),Cohesion=49};nativeArmy.Parties.AddRange(new[]{nativeChief,offenderParty});nativeChief.Army=offenderParty.Army=nativeArmy;
  nativeArmy.RunHour();Check(offenderParty.LeaderHero==offender,"ordinary native army before surrender has valid member hero");
  var envoy=new MobileParty{StringId="gwp_enf_delay_envoy"};var warden=new MobileParty{StringId="gw_leader_5_party_1",LeaderHero=new(){Clan=new()}};var enforcement=new PoliceEnforcementBehavior();enforcement.Surrender(warden,envoy,offenderParty,false);
  Check(offender.State==Hero.CharacterStates.Prisoner&&offender.PrisonerParty==envoy.Party&&offender.PartyBelongedTo==null,"installed capture takes the lord into envoy custody");
  Check(offenderParty.LeaderHero==null&&offenderParty.Army==nativeArmy&&nativeArmy.Parties.Contains(offenderParty),"installed capture leaves original lord party registered in native army with no leader hero");
  bool captureFailure=false;try{nativeArmy.RunHour();}catch(NullReferenceException error){captureFailure=error.StackTrace!.Contains("GetHeroesForEffectiveRelation")&&error.StackTrace.Contains("ThinkAboutCohesionBoost");Console.WriteLine("SURRENDER_FAILURE_STACK: "+error.StackTrace!.Replace(Environment.NewLine," | "));}
  Check(captureFailure,"capture-created leaderless native member reproduces the reported hourly influence/relation stack without any Warden support in army");
  Console.WriteLine("NATIVE_CAPTURE_REPLAY: capture of a registered native army member -> original party hero null, army membership retained -> same hourly fee failure; low cohesion and event/world boundaries controlled, not a historical whole-campaign replay");
  nativeArmy.Parties.Remove(offenderParty);offenderParty.Army=null;nativeArmy.RunHour();
  Check(offender.State==Hero.CharacterStates.Prisoner,"normal member removal control clears crash without undoing surrender or changing army fee model");
  offender=new Hero{Clan=clan};offenderParty=new MobileParty{StringId="battle_offender",MapEvent=new object()};offenderParty.PartyComponent=new LordPartyComponent{MobileParty=offenderParty,_leader=offender};offender.PartyBelongedTo=offenderParty;offenderParty.Army=nativeArmy;nativeArmy.Parties.Add(offenderParty);
  enforcement=new();enforcement.Surrender(warden,envoy,offenderParty,true);
  Check(enforcement.CapturesBeforeBattleEnd==0&&offender.State==Hero.CharacterStates.Prisoner,"production aftermath defers capture during battle then takes lord after battle");
  bool aftermathFailure=false;try{nativeArmy.RunHour();}catch(NullReferenceException e){aftermathFailure=e.StackTrace!.Contains("GetHeroesForEffectiveRelation");}
  Check(aftermathFailure,"production post-battle surrender retains the same native army membership fault");
  // A short high-cohesion control verifies why capture need not crash immediately.
  // Other world/army dispersion callbacks remain outside this harness.
  nativeArmy.Cohesion=50.2f;nativeArmy.RunHour();
  Check(nativeArmy.Cohesion>=50&&offenderParty.LeaderHero==null,"captured member can remain without immediate exception while cohesion stays at least50");
  int thresholdHours=1;bool thresholdFailure=false;
  try{for(;thresholdHours<12;thresholdHours++)nativeArmy.RunHour();}catch(NullReferenceException){thresholdFailure=true;}
  Check(thresholdFailure&&nativeArmy.Cohesion<50,"the same member fails only once native hourly cohesion crosses50 in controlled short sequence");
  clan.Influence=0;bool emptyTreasuryFailure=false;try{nativeArmy.RunHour();}catch(NullReferenceException){emptyTreasuryFailure=true;}
  Check(emptyTreasuryFailure,"zero influence does not prevent failure because fee is calculated before treasury is checked");
  Console.WriteLine("SURRENDER_CONTROLS: valid member PASS; member removed after capture PASS; battle capture deferred PASS then same fault; cohesion50.2 first hour PASS then threshold fault; influence0 still faults");
  Console.WriteLine("ARMY_INFLUENCE_FAULT_ASSERTIONS="+checks+"; PASS; failure capture only, not a campaign crash fix");
 }
}
'''


class ArmyInfluenceFaultTests(unittest.TestCase):
    def test_failure_context_and_native_null_inputs(self):
        patch = (ROOT/'GreyWardenPolicePurity/GwpAssistanceArmyLifecyclePatch.cs').read_text(encoding='utf-8-sig')
        cohesion = patch[patch.index('internal static class GwpAssistanceArmyCohesionPatch'):]
        total = patch[patch.index('internal static class GwpArmyInfluenceFailureTracePatch'):]
        patch = patch[patch.index('internal static class GwpLeaderlessSupportArmyInfluencePatch'):]
        native = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.GameComponents.DefaultDiplomacyModel', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        army = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.Army', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        capture = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.Actions.TakePrisonerAction', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        party = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.Party.MobileParty', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        component = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.Party.PartyComponents.PartyComponent', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        lord = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        cost = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel', str(GAME_DLL)], text=True, encoding='utf-8-sig')
        code = HARNESS.replace('TOTAL_CAPTURE_METHOD', extract(total, 'Finalizer')).replace('CAPTURE_METHOD', extract(patch, 'Finalizer')).replace(
            'NATIVE_METHOD', '\n'.join(extract(native, name).replace('public override ', 'public ') for name in ['GetHeroesForEffectiveRelation','GetEffectiveRelation']))
        code = code.replace('COST_PATCH', extract(patch, 'Prefix'))
        methods = '\n'.join(extract(cost, name).replace('public override ', 'public ') for name in ['CalculatePartyInfluenceCost','CalculateTotalInfluenceCost','CalculateTotalInfluenceCostInternal','CalculateDailyCohesionChange','CalculateCohesionChangeInternal'])
        methods = methods.replace('public int CalculatePartyInfluenceCost(', 'private int NativePartyInfluenceCost(')
        methods = methods.replace('public ExplainedNumber CalculateDailyCohesionChange(', 'private ExplainedNumber NativeCohesionChange(')
        code = code.replace('COHESION_PATCH', extract(cohesion,'Postfix')).replace('COST_METHODS', methods).replace('ARMY_METHODS', '\n'.join(extract(army, name) for name in ['HourlyTick','ThinkAboutCohesionBoost']))
        code = code.replace('PRISONER_ACTION_BODY', extract(capture, 'ApplyInternal')).replace('REMOVE_PARTY_LEADER', extract(party, 'RemovePartyLeader'))
        lord_change = extract(lord.replace('protected override void OnChangePartyLeader', 'internal override void OnChangePartyLeader'), 'OnChangePartyLeader').replace('internal override void', 'protected override void')
        code = code.replace('PARTY_LEADER_CHANGE', extract(component, 'ChangePartyLeader')).replace('LORD_LEADER_CHANGE', lord_change)
        # Frozen source baseline is the negative control. The repaired production
        # entry and resource lifecycle are exercised by test_voluntary_surrender.
        envoy = subprocess.check_output(['git', 'show', 'd5a8d84:GreyWardenPolicePurity/PoliceEnforcementBehavior.Envoy.cs'], text=True, encoding='utf-8-sig')
        code = code.replace('ENVOY_PRODUCTION_METHODS', '\n'.join(extract(envoy, name) for name in ['Negotiate', 'BeginAftermath', 'TendAftermath']))
        # Only the frozen negative control needs the old tuning name and trace
        # boundary. These are not restored to current production or shared fixtures.
        code = code.replace('const int EnvoyAftermathWaitHours=72;',
                            'const int EnvoyAftermathWaitHours=72,EnvoyTimeoutHours=72;')
        code += '\nstatic class GwpAiDiagnostics{public static void WriteAction(MobileParty p,string action,string details){}}\n'
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-army-fault-') as directory:
            p = Path(directory)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p/'Program.cs').write_text(code, encoding='utf-8')
            r = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'], text=True, capture_output=True)
            self.assertEqual(0, r.returncode, r.stdout+r.stderr)
            print('\n'.join(line for line in r.stdout.splitlines() if line.startswith(('NATIVE_ARMY_REPLAY:', 'NATIVE_CAPTURE_REPLAY:', 'SURRENDER_FAILURE_STACK:', 'SURRENDER_CONTROLS:', 'ARMY_INFLUENCE_FAULT_ASSERTIONS='))))


if __name__ == '__main__':
    unittest.main()
