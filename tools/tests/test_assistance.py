"""Replay production formation lifecycle/movement and native visit-statistic boundaries."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'


def member(source, name):
    match = re.search(r'(?m)^        (?:private|internal)[^\n]*\b' + name + r'\s*\(', source)
    if not match:
        raise AssertionError(name)
    start = match.start()
    brace, arrow = source.find('{', match.end()), source.find('=>', match.end())
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[start:source.index(';', arrow) + 1]
    return extract(source, name)


STUBS = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Globalization;
namespace GreyWardenPolicePurity {
enum AiBehavior { None,EscortParty,GoAroundParty }
class Hero { public bool IsActive=true,IsPrisoner,IsFugitive; }
class Vec { public float X;public float Distance(Vec v)=>Math.Abs(X-v.X);public float LengthSquared=>X*X;public static Vec operator -(Vec a,Vec b)=>new Vec{X=a.X-b.X}; }
class Roster { public int TotalManCount=99,TotalWounded; }
class PartyBase { public int NumberOfHealthyMembers=99; }
class MobileParty {
 public enum NavigationType { None,Default }
 public string StringId="";public bool IsActive=true,IsWarden=true,IsCurrentlyAtSea,Reserved,Reachable=true,IsCaravan;public float Passers;public string? EnvoyBlocked="stub_no_envoy";
 public Hero? LeaderHero=new Hero();public object? MapEvent,CurrentSettlement;public bool IsMainParty;
 public Kingdom MapFaction=new Kingdom();
 public Roster MemberRoster=new Roster();public PartyBase Party=new PartyBase();public float PartySizeRatio=.62f,NormalLimit=1f,Strength=40,LastCalculatedBaseSpeed=4;
 public float TargetSpeed=3;public Vec Position=new Vec();public Vec GetPosition2D=>Position;
 public List<MobileParty> AttachedParties=new List<MobileParty>();private MobileParty? attached;
 public MobileParty? AttachedTo {get=>attached;set{attached?.AttachedParties.Remove(this);attached=value;if(value!=null&&!value.AttachedParties.Contains(this))value.AttachedParties.Add(this);}}
 private Army? army;public Army? Army {get=>army;set{army?.Parties.Remove(this);AttachedTo=null;army=value;if(value!=null&&!value.Parties.Contains(this))value.Parties.Add(this);}}
 public static List<MobileParty> All=new List<MobileParty>();public static MobileParty MainParty=new MobileParty{StringId="player",IsMainParty=true,IsWarden=false};
}
interface IFaction {bool IsAtWarWith(IFaction f);}
class Kingdom : IFaction {public string StringId="kingdom";public bool IsAtWarWith(IFaction k)=>false;}
class Settlement {public bool IsVillage=true,IsRaided,IsUnderRaid;public Kingdom MapFaction=new Kingdom();}
class Army {
 public enum ArmyTypes {Patrolling,Defender} public ArmyTypes ArmyType;
 public enum ArmyDispersionReason { Unknown,NoActiveWar,LeaderPartyRemoved,ArmyLeaderIsDead }
 public Kingdom? Kingdom;public MobileParty LeaderParty;public List<MobileParty> Parties=new List<MobileParty>();
 public Army(object? k,MobileParty l,ArmyTypes t){Kingdom=k==null?null:new Kingdom();LeaderParty=l;ArmyType=t;l.Army=this;}
 public void AddPartyToMergedParties(MobileParty p){p.AttachedTo=LeaderParty;}
 private object? _hourlyTickEvent,_tickEvent;public int TickRestores;private void OnAfterLoad(){_hourlyTickEvent=new object();_tickEvent=new object();TickRestores++;}public bool HasTicks=>_hourlyTickEvent!=null&&_tickEvent!=null;
}
class CrimeRecord { public bool HasOpenCase=true;public string CrimeId="crime";public MobileParty Offender=null!; }
class PoliceTask { public string PolicePartyId="leader",TargetCrimeId="crime";public bool IsPlayerBountyEscort,IsEscortingPlayer,WarDeclared,AssistanceRequested;public float LeaderSoloSpeedAtAssignment=4; public CrimeRecord TargetCrime=new CrimeRecord(); }
static class CrimeState { public static Dictionary<string,PoliceTask> Tasks=new();public static PoliceTask? GetTask(string id)=>Tasks.TryGetValue(id,out var t)?t:null;public static void RefreshAccepting(){} }
static class CrimePool {public const int MaxTaskPoolEntries=10;public static void TrimOpenCasesToCapacity(int n){} }
class CampaignTime {public const int HoursInDay=24;public double ToHours;public static CampaignTime Now=new CampaignTime{ToHours=100};}
interface IDataStore {bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore {public Dictionary<string,object> Data=new();public bool IsSaving{get;set;}public bool IsLoading=>!IsSaving;public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v!;else v=(T)Data[k];}}
class EncounterModel {public float GetEncounterJoiningRadius=3,MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy=2,MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy=2;}
class Models {public EncounterModel EncounterModel=new EncounterModel();}
class Campaign {public static Campaign Current=new Campaign();public Models Models=new Models();}
static class AiHelper {public static void GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(MobileParty a,MobileParty b,out MobileParty.NavigationType n,out float d){n=b.Reachable?MobileParty.NavigationType.Default:MobileParty.NavigationType.None;d=a.Position.Distance(b.Position);}}
static class PartyBaseHelper {public static float FindPartySizeNormalLimit(MobileParty p)=>p.NormalLimit;}
static class GwpCommon {public static bool IsGreyWardenLordParty(MobileParty p)=>p.IsWarden&&p.LeaderHero!=null;public static bool IsGreyWardenClanParty(MobileParty p)=>p.IsWarden;public static bool IsEnforcementDelayPatrolParty(MobileParty p)=>p.StringId.StartsWith("support");public static void TryResetAi(MobileParty p){}public static bool IsShelteredOffender(MobileParty? p)=>p?.CurrentSettlement!=null;}
static class PoliceStats {public static IEnumerable<MobileParty> GetAllPoliceParties()=>MobileParty.All.Where(p=>p.IsWarden&&p.LeaderHero!=null);}
enum WardenDuty{Assistance}
static class GwpWardenDuties {public static bool IsFreeFor(MobileParty p,WardenDuty d)=>!p.Reserved&&!PoliceEnforcementBehavior.Occupied(p)&&!PoliceEnforcementBehavior.IsPartyHeldForDuel(p);}
static class PlayerBountyBehavior {public static bool SupportPending;public static bool AwaitingSupportRequest(PoliceTask? t)=>t?.IsPlayerBountyEscort==true&&SupportPending;}
static class GwpStrength {
 public class Threat {public float Strength,JoiningRadius=3,ThreatRadius=6,MaximumSpeed=3;public string FastestCombatGroupId="enemy";public List<string> CombatGroups=new();}
 public class Balance {public bool StrengthReady;}
 public static float Party(MobileParty? p)=>p?.Strength??0;
 public static MobileParty CombatGroupOf(MobileParty p)=>p.AttachedTo??p;
 public static float CombatGroup(MobileParty p)=>p.Strength+p.AttachedParties.Sum(x=>x.Strength);
 public static float TheoreticalBaseSpeed(MobileParty p)=>p.TargetSpeed;
 public static Threat EnemyAround(MobileParty a,MobileParty b)=>new Threat{Strength=b.Strength+b.Passers};
 public static float CaseTarget(CrimeRecord? c)=>c?.Offender==null?0:CombatGroup(CombatGroupOf(c.Offender));
 public static Balance Compare(MobileParty a,MobileParty b)=>new Balance{StrengthReady=CombatGroup(a)+MobileParty.All.Where(p=>p!=a&&p.IsWarden&&p.AttachedTo==null&&p.Position.Distance(a.Position)<=3).Sum(p=>p.Strength)>b.Strength};
}
static class GwpTuning {public static class Enforcement {public const float AssistanceReleaseMargin=1.5f,AssistanceMinimumMemberHours=12,AssistanceMaximumTravelDays=1;public const int AssistanceSurplusConfirmTicks=4;}}
static class GreyWardenPartyDesireBehavior {public static Dictionary<string,(string,string)> Intent=new();public static void ClearIntent(MobileParty p)=>Intent.Remove(p.StringId);public static void RequestImmediateRethink(MobileParty? p){}public static void RequestEscort(MobileParty p,MobileParty t,float priority)=>Intent[p.StringId]=("escort",t.StringId);public static void RequestPursuit(MobileParty p,MobileParty t,float priority)=>Intent[p.StringId]=("pursue",t.StringId);}
static class GwpAiDiagnostics {public static void WriteAction(MobileParty p,string a,string d){}}
static class GwpFaultTrace {public static void WriteQuiet(Exception e)=>throw e;}
static class GwpAssistanceArmyDisbandGuardPatch {public static int Disbands;public static void ApplyAuthorizedObjectiveFinished(Army a){Disbands++;foreach(var p in a.Parties.ToList())p.Army=null;}}
public partial class PoliceEnforcementBehavior {
 private sealed class LordAssistanceGroup {public string LeaderPartyId="leader",CrimeId="crime",TargetPartyId="enemy";public List<string> MemberPartyIds=new(),SpeedDetachedPartyIds=new();public bool DispersedForSpeed;public float LastArmySpeedAtDispersal;public string SpeedCatcherPartyId="",PendingReleaseReason="";}
 private sealed class DelayPatrolState {public string PatrolPartyId="",SourceTaskPolicePartyId="leader",TargetPartyId="enemy";public bool Returning,IsEnvoy,IsImmediateInterceptor;}
 private static PoliceEnforcementBehavior? _instance;
 private Dictionary<string,LordAssistanceGroup> _assistanceGroups=new();private Dictionary<string,DelayPatrolState> _delayPatrolStates=new();
 private Dictionary<string,double> _assistanceAssignedHours=new();private Dictionary<string,int> _assistanceSurplusTicks=new();private HashSet<string> _playerBountyEscortGroups=new();
 private static readonly System.Reflection.FieldInfo? ArmyHourlyTickField=typeof(Army).GetField("_hourlyTickEvent",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 private static readonly System.Reflection.MethodInfo? ArmyOnAfterLoad=typeof(Army).GetMethod("OnAfterLoad",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 public int CaseFailures;private void FailAssistanceCase(MobileParty p,PoliceTask t,LordAssistanceGroup? g,float n,string r){if(t.TargetCrime.Offender.MapEvent!=null||p.MapEvent!=null)return;CaseFailures++;ReleaseAssistanceGroup(p.StringId,r);}
 private void CleanupInvalidAssistanceGroups(){foreach(var g in _assistanceGroups.Values.ToList())if(!IsAssistanceGroupCaseStillActive(g,CrimeState.GetTask(g.LeaderPartyId)))ReleaseAssistanceGroup(g.LeaderPartyId,"case_ended");}
 private bool TryCaptureAssistanceLeaderSoloSpeed(MobileParty p,PoliceTask t)=>true;
 private void ExitAssistancePlayerBountyEscort(MobileParty p,LordAssistanceGroup g)=>_playerBountyEscortGroups.Remove(g.LeaderPartyId);
 private static string FormatStrengthDiagnostic(MobileParty a,MobileParty b,float c,float d)=>"";
 internal enum EnvoyStage { Travelling,Failed,Grace,Duel,DuelDecided,Aftermath }
 private sealed class EnvoyCase {public string WardenPartyId="",TownId="";public EnvoyStage Stage;}
 private Dictionary<string,EnvoyCase> _envoyCases=new();
 private static string? EnvoyBlocker(MobileParty police,MobileParty criminal)=>police.EnvoyBlocked;
 private bool IsAssistanceMember(string id)=>_assistanceGroups.Values.Any(g=>g.MemberPartyIds.Contains(id));
 internal static bool Occupied(MobileParty p)=>_instance?._assistanceGroups.ContainsKey(p.StringId)==true||_instance?.IsAssistanceMember(p.StringId)==true;
 private static void DetachDelayPatrolFromArmy(MobileParty p){p.Army=null;}

 private bool TryGetDelaySupportAssistanceArmy(string s,string t,out MobileParty? leader,out Army? army){leader=FindActiveParty(s);army=null;if(leader==null||!_assistanceGroups.TryGetValue(s,out var g)||g.DispersedForSpeed)return false;army=MaintainAssistanceArmy(leader,g);return army!=null;}
 METHODS
 public static int Checks;
 static void Check(bool condition,string why){Checks++;if(!condition)throw new Exception(why);}
 static PoliceEnforcementBehavior Setup(out MobileParty leader,out MobileParty helper,out MobileParty enemy){
  var e=new PoliceEnforcementBehavior();_instance=e;MobileParty.All.Clear();CrimeState.Tasks.Clear();GreyWardenPartyDesireBehavior.Intent.Clear();GwpAssistanceArmyDisbandGuardPatch.Disbands=0;
  MobileParty.MainParty=new MobileParty{StringId="player",IsMainParty=true,IsWarden=false,Strength=0};PlayerBountyBehavior.SupportPending=false;
  leader=new MobileParty{StringId="leader"};helper=new MobileParty{StringId="helper",Strength=120,Position=new Vec{X=1}};enemy=new MobileParty{StringId="enemy",IsWarden=false,Strength=100,Position=new Vec{X=100}};
  MobileParty.All.AddRange(new[]{leader,helper,enemy});CrimeState.Tasks["leader"]=new PoliceTask{TargetCrime=new CrimeRecord{Offender=enemy}};return e;}
 LordAssistanceGroup Group(MobileParty l,MobileParty? h=null){var g=new LordAssistanceGroup{LeaderPartyId=l.StringId};if(h!=null)g.MemberPartyIds.Add(h.StringId);_assistanceGroups[l.StringId]=g;return g;}
 MobileParty Support(string name="support",bool returning=false,bool envoy=false,bool immediate=false){var p=new MobileParty{StringId=name,LeaderHero=null,Strength=120,Position=new Vec{X=1}};MobileParty.All.Add(p);_delayPatrolStates[name]=new DelayPatrolState{PatrolPartyId=name,Returning=returning,IsEnvoy=envoy,IsImmediateInterceptor=immediate};return p;}
 public static void Main(){
  var e=Setup(out var l,out var h,out var t);var g=e.Group(l);var a=new Army(null,l,Army.ArmyTypes.Patrolling);
  Check(!IsActiveAssistanceArmy(a),"empty formation has no native disband protection");e.ReconcileAssistanceFormations();
  Check(l.Army==null&&e._assistanceGroups.Count==0,"empty group and army released");Check(CrimeState.GetTask("leader")!=null&&e.CaseFailures==0,"formation release preserves case");
  Check(NativeDisbandGuardProbe.Run(a,Army.ArmyDispersionReason.NoActiveWar),"native disband allowed without active group");
  e=Setup(out l,out h,out t);g=e.Group(l,h);a=e.MaintainAssistanceArmy(l,g)!;
  Check(!NativeDisbandGuardProbe.Run(a,Army.ArmyDispersionReason.NoActiveWar),"valid assembly keeps native protection");
  Check(NativeDisbandGuardProbe.Run(a,Army.ArmyDispersionReason.LeaderPartyRemoved)&&NativeDisbandGuardProbe.Run(a,Army.ArmyDispersionReason.ArmyLeaderIsDead),"destroyed commander not protected");
  e.RestoreEnforcementArmyTicks();Check(a.HasTicks&&a.TickRestores==1,"load restoration recreates both native timers");e.RestoreEnforcementArmyTicks();Check(a.TickRestores==1,"native timers not registered twice");
  CrimeState.Tasks.Clear();e.CleanupInvalidAssistanceGroups();Check(l.Army==null&&h.Army==null&&e._assistanceGroups.Count==0,"case end releases all formation duties");
  e=Setup(out l,out h,out t);g=e.Group(l);a=new Army(null,l,Army.ArmyTypes.Patrolling);l.MapEvent=new object();e.ReconcileAssistanceFormations();Check(l.Army==a,"ongoing battle not detached");l.MapEvent=null;e.ReconcileAssistanceFormations();Check(l.Army==null,"release after battle");
  e=Setup(out l,out h,out t);a=new Army(null,l,Army.ArmyTypes.Patrolling);e.ReleaseUnownedAssistanceArmy(l);Check(l.Army==null,"orphan without logical group released");
  e=Setup(out l,out h,out t);a=new Army(new object(),l,Army.ArmyTypes.Patrolling);e.ReleaseUnownedAssistanceArmy(l);Check(l.Army==a,"kingdom army untouched");
  foreach(int lost in new[]{0,1,2}){e=Setup(out l,out h,out t);g=e.Group(l,h);a=e.MaintainAssistanceArmy(l,g)!;
   if(lost==0)h.IsActive=false;if(lost==1)h.LeaderHero!.IsPrisoner=true;if(lost==2)h.Army=new Army(new object(),h,Army.ArmyTypes.Defender);
   e.ReconcileAssistanceFormations();Check(l.Army==null&&e._assistanceGroups.Count==0,"invalid helper releases source formation");}
  e=Setup(out l,out h,out t);g=e.Group(l,h);a=e.MaintainAssistanceArmy(l,g)!;ReleasePartyFromAssistance("helper","reassigned");Check(l.Army==null&&h.Army==null,"reassignment of last helper closes formation");
  e=Setup(out l,out h,out t);g=e.Group(l);var support=e.Support();a=e.MaintainAssistanceArmy(l,g)!;Check(a!=null&&support.Army==a&&IsActiveAssistanceArmy(a),"ordinary temporary support keeps formation alive");
  Check(GetCommittedAssistanceStrength(l,g)==160,"ordinary support contributes committed force");e._delayPatrolStates["support"].Returning=true;e.ReconcileAssistanceFormations();Check(l.Army==null&&support.Army==null,"last returning support releases formation");
  foreach(int kind in new[]{0,1,2}){e=Setup(out l,out h,out t);g=e.Group(l);e.Support(returning:kind==0,envoy:kind==1,immediate:kind==2);Check(!e.HasFormationParticipants(g),"returning/envoy/interceptor do not keep shell");Check(e.MaintainAssistanceArmy(l,g)==null,"no shell created for excluded support");}
  e=Setup(out l,out h,out t);g=e.Group(l,h);
  Check(TryGetAssistanceDuty(l,out var goal,out var behavior,out _,out var pursues)&&goal==t&&behavior==AiBehavior.EscortParty&&pursues,"chief follows offender rather than helper");
  Check(TryGetAssistanceDuty(h,out goal,out behavior,out _,out _)&&goal==l,"helper moves toward chief");
  h.Position.X=50;Check(e.MaintainAssistanceArmy(l,g)!=null&&h.Army==l.Army&&h.AttachedTo==null,"real responding helper keeps gathering army marker without remote attachment");
  h.Position.X=1;Check(e.MaintainAssistanceArmy(l,g)!=null&&h.Army==l.Army,"arrived helper actually merges into gathering army");
  e=Setup(out l,out h,out t);g=e.Group(l,h);g.DispersedForSpeed=true;g.SpeedDetachedPartyIds.AddRange(new[]{"leader","helper"});
  e.MaintainSpeedDispersedPursuit(l,g,t);Check(l.Army==null&&h.Army==null,"speed dispersion clears native army");
  Check(GreyWardenPartyDesireBehavior.Intent["leader"]==("escort","enemy")&&GreyWardenPartyDesireBehavior.Intent["helper"]==("escort","leader"),"dispersed chief follows offender and helper follows chief");
  e=Setup(out l,out h,out t);g=e.Group(l);CrimeState.Tasks["leader"].IsPlayerBountyEscort=true;e.MaintainAssistancePlayerBountyEscort(l,g);
  Check(g.MemberPartyIds.Contains("helper")&&l.Army!=null,"first player reinforcement can be recruited without preexisting army");Check(TryGetAssistanceDuty(l,out goal,out _,out var escort,out _)&&escort&&goal==MobileParty.MainParty,"player support keeps following player");
  e=Setup(out l,out h,out t);g=e.Group(l);h.Reserved=true;CrimeState.Tasks["leader"].IsPlayerBountyEscort=true;e.MaintainAssistancePlayerBountyEscort(l,g);Check(l.Army==null&&CrimeState.Tasks.ContainsKey("leader")&&e.CaseFailures==0,"exhausted player support keeps case without empty shell");h.Reserved=false;e.UpdateLordAssistance();Check(l.Army!=null&&e._assistanceGroups["leader"].MemberPartyIds.Count==1,"player support can retry recruitment after empty release");
  // Player-requested assistance commits the player's real group before deciding how many helpers to draft.
  e=Setup(out l,out h,out t);CrimeState.Tasks["leader"].IsPlayerBountyEscort=true;MobileParty.MainParty.Strength=80;
  e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0&&h.Army==null,"player plus chief already sufficient: no redundant helper");
  Check(CrimeState.Tasks.ContainsKey("leader")&&e.CaseFailures==0,"sufficient player force preserves commission");
  g=e.Group(l);Check(GetCommittedAssistanceStrength(l,g)==120,"player commits strength even before physically reaching chief");
  MobileParty.MainParty.Position.X=100000;Check(GetCommittedAssistanceStrength(l,g)==120,"commitment is not a local declaration permission");
  MobileParty.MainParty.Strength=10;Check(GetCommittedAssistanceStrength(l,g)==50,"player losses immediately change commitment");
  e.MaintainAssistancePlayerBountyEscort(l,g);Check(g.MemberPartyIds.SequenceEqual(new[]{"helper"}),"loss of combined advantage drafts a reachable helper");
  e=Setup(out l,out h,out t);CrimeState.Tasks["leader"].IsPlayerBountyEscort=true;MobileParty.MainParty.Strength=20;t.Strength=150;
  MobileParty.All.Add(new MobileParty{StringId="second",Strength=120,Position=new Vec{X=2}});e.UpdateLordAssistance();
  Check(e._assistanceGroups["leader"].MemberPartyIds.SequenceEqual(new[]{"helper"}),"draft stops once player plus chief plus nearest helper exceed target");
  e=Setup(out l,out h,out t);CrimeState.Tasks["leader"].IsPlayerBountyEscort=true;MobileParty.MainParty.Strength=10;g=e.Group(l);
  var playerArmy=new Army(new object(),MobileParty.MainParty,Army.ArmyTypes.Patrolling);
  var playerAttached=new MobileParty{StringId="player_attached",IsWarden=false,Strength=80};playerAttached.Army=playerArmy;playerAttached.AttachedTo=MobileParty.MainParty;
  var playerRemote=new MobileParty{StringId="player_remote",IsWarden=false,Strength=999};playerRemote.Army=playerArmy;
  Check(GetCommittedAssistanceStrength(l,g)==130,"player's actually attached army counts but remote nominal member does not");
  g.MemberPartyIds.Add("helper");h.Army=playerArmy;h.AttachedTo=MobileParty.MainParty;
  Check(GetCommittedAssistanceStrength(l,g)==250,"helper already in player's actual group is counted exactly once");
  MobileParty.MainParty.Strength=0;PlayerBountyBehavior.SupportPending=true;Check(GetCommittedAssistanceStrength(l,g)==160,"undelivered request contributes no player army");
  PlayerBountyBehavior.SupportPending=false;MobileParty.MainParty.IsActive=false;Check(GetCommittedAssistanceStrength(l,g)==160,"inactive player contributes no force");
  MobileParty.MainParty.IsActive=true;MobileParty.MainParty.LeaderHero!.IsPrisoner=true;Check(GetCommittedAssistanceStrength(l,g)==160,"captured player contributes no force");
  e=Setup(out l,out h,out t);MobileParty.MainParty.Strength=10000;e.UpdateLordAssistance();
  Check(e._assistanceGroups["leader"].MemberPartyIds.Contains("helper"),"ordinary NPC assistance ignores player strength");
  e=Setup(out l,out h,out t);h.Position.X=24*h.TargetSpeed;
  Check(e.GetAvailableAssistanceCandidates(l).Contains(h),"one-day travel boundary is inclusive");h.Position.X+=.01f;
  Check(!e.GetAvailableAssistanceCandidates(l).Contains(h),"beyond one-day travel is excluded");h.Position.X=1;l.Reachable=false;
  Check(!e.GetAvailableAssistanceCandidates(l).Contains(h),"navigation failure excludes nearby helper");
  e=Setup(out l,out h,out t);e.UpdateLordAssistance();Check(l.Army!=null&&h.Army==l.Army,"full hourly path allocates then forms");Check(TryGetAssistanceDuty(l,out goal,out _,out _,out _)&&goal==t,"hourly chief keeps offender as target");
  e=Setup(out l,out h,out t);g=e.Group(l,h);l.Strength=200;h.Position.X=20;e._assistanceAssignedHours["helper"]=0;e.MaintainAssistanceArmy(l,g);for(int i=0;i<4;i++){e.TryShrinkAssistanceGroup(l,g,100);e.ReconcileAssistanceFormations();}Check(l.Army==null&&e._assistanceGroups.Count==0&&CrimeState.Tasks.ContainsKey("leader"),"surplus last-member shrink releases shell and retains case");
  e=Setup(out l,out h,out t);g=e.Group(l,h);g.DispersedForSpeed=true;g.SpeedDetachedPartyIds.AddRange(new[]{"leader","helper"});e._assistanceAssignedHours["helper"]=42;
  var saved=new Store{IsSaving=true};e.SyncAssistanceData(saved);var restored=new PoliceEnforcementBehavior();_instance=restored;saved.IsSaving=false;restored.SyncAssistanceData(saved);g=restored._assistanceGroups["leader"];
  Check(g.DispersedForSpeed&&g.MemberPartyIds.SequenceEqual(new[]{"helper"})&&g.SpeedDetachedPartyIds.Count==2,"actual saved fields restore dispersed membership");Check(restored._assistanceAssignedHours["helper"]==42,"minimum-member clock persists");Check(TryGetAssistanceDuty(l,out goal,out _,out _,out _)&&goal==t,"offender tracking resumes after state restoration");
  g.DispersedForSpeed=false;g.SpeedDetachedPartyIds.Clear();saved.IsSaving=true;restored.SyncAssistanceData(saved);var gathering=new PoliceEnforcementBehavior();_instance=gathering;saved.IsSaving=false;gathering.SyncAssistanceData(saved);Check(!gathering._assistanceGroups["leader"].DispersedForSpeed&&TryGetAssistanceDuty(l,out goal,out _,out _,out _)&&goal==t,"chief tracking resumes after saved-state restoration");
  g=gathering._assistanceGroups["leader"];g.MemberPartyIds.Clear();l.MapEvent=new object();saved.IsSaving=true;gathering.SyncAssistanceData(saved);var pending=new PoliceEnforcementBehavior();_instance=pending;saved.IsSaving=false;pending.SyncAssistanceData(saved);pending.ReconcileAssistanceFormations();Check(pending._assistanceGroups.Count==1,"battle-deferred empty state persists");l.MapEvent=null;pending.ReconcileAssistanceFormations();Check(pending._assistanceGroups.Count==0,"restored pending empty state released at safe point");
  e=Setup(out l,out h,out t);h.Reserved=true;t.MapEvent=new object();e.UpdateLordAssistance();e.UpdateLordAssistance();
  Check(e._assistanceGroups.Count==0&&e.CaseFailures==0&&CrimeState.Tasks.ContainsKey("leader"),"failure deferred by target battle leaves no empty group to churn");
  e=Setup(out l,out h,out t);l.Strength=150;t.Passers=200;e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0&&e.CaseFailures==0,"regional passers do not trigger recruitment or assignment failure");
  e=Setup(out l,out h,out t);g=e.Group(l,h);l.Strength=200;h.Position.X=20;e._assistanceAssignedHours["helper"]=0;t.Passers=60;e.MaintainAssistanceArmy(l,g);
  for(int i=0;i<4;i++){e.UpdateLordAssistance();}Check(e._assistanceGroups.Count==0,"regional passers do not prevent stable-force surplus release");
  t.Passers=0;for(int i=0;i<4;i++){e.UpdateLordAssistance();}Check(e._assistanceGroups.Count==0,"surplus helper released once passers leave");
  e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0,"released helper not re-requested for the same target");
  foreach(float strength in new[]{100f,150f}){e=Setup(out l,out h,out t);l.Strength=strength;support=e.Support();e.UpdateLordAssistance();
   Check(e._assistanceGroups.Count==0&&l.Army==null&&h.Army==null&&support.Army==null,"ordinary fallback support cannot form assistance for an equal or weaker enemy");
   Check(CrimeState.Tasks.ContainsKey("leader")&&e.CaseFailures==0,"independent fallback support preserves ordinary case");}
  e=Setup(out l,out h,out t);e.Support();e.UpdateLordAssistance();Check(e._assistanceGroups.Count==1,"genuinely stronger enemy can still form assistance with support");
  // 使者有结果之前不调人；谈崩才组协力（用户裁定 2026-10-16）。
  e=Setup(out l,out h,out t);l.EnvoyBlocked=null;e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0&&h.Army==null,"no helper before the envoy is sent");
  l.EnvoyBlocked="stub_no_envoy";e._envoyCases["crime"]=new EnvoyCase{WardenPartyId="leader",Stage=EnvoyStage.Travelling};e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0,"no helper while the envoy travels");
  e._envoyCases["crime"].Stage=EnvoyStage.Duel;e.UpdateLordAssistance();Check(e._assistanceGroups.Count==0,"no helper while a duel is arranged");
  e._envoyCases["crime"].Stage=EnvoyStage.Failed;e.UpdateLordAssistance();Check(l.Army!=null&&h.Army==l.Army,"broken talks raise assistance");
  e=Setup(out l,out h,out t);l.EnvoyBlocked=null;CrimeState.Tasks["leader"].WarDeclared=true;e.UpdateLordAssistance();Check(l.Army!=null,"declared case does not wait for an envoy");
  // 协办人自己的案子留着；自己约了比武的调不走。
  e=Setup(out l,out h,out t);var own=new PoliceTask{PolicePartyId="helper",TargetCrimeId="own",TargetCrime=new CrimeRecord{CrimeId="own",Offender=new MobileParty{StringId="other",IsWarden=false}}};CrimeState.Tasks["helper"]=own;
  e.UpdateLordAssistance();Check(h.Army==l.Army&&CrimeState.GetTask("helper")==own,"helper keeps own case while helping");
  e=Setup(out l,out h,out t);CrimeState.Tasks["helper"]=own;e._envoyCases["own"]=new EnvoyCase{WardenPartyId="helper",Stage=EnvoyStage.DuelDecided};e.UpdateLordAssistance();Check(h.Army==null&&e._assistanceGroups.Count==0,"helper held for a duel is not drafted");
  e=Setup(out l,out h,out t);g=e.Group(l,h);h.Position.X=50;e.MaintainAssistanceArmy(l,g);
  Check(GetImmediateInterceptorTroopSources(l,g).SequenceEqual(new[]{l}),"gathering marker does not teleport remote helper troops into interceptor");
  h.Position.X=1;e.MaintainAssistanceArmy(l,g);Check(GetImmediateInterceptorTroopSources(l,g).Contains(h),"actually attached helper may contribute interceptor troops");
  h.IsActive=false;Check(!GetImmediateInterceptorTroopSources(l,g).Contains(h),"lost helper cannot contribute interceptor troops");h.IsActive=true;
  // An ended case can defer physical disbanding while a remote member is fighting.
  // This proves the lifecycle boundary, not that the historical crash used this army.
  e=Setup(out l,out h,out t);g=e.Group(l,h);support=e.Support();a=e.MaintainAssistanceArmy(l,g)!;
  h.MapEvent=new object();CrimeState.Tasks.Clear();e.CleanupInvalidAssistanceGroups();
  Check(l.MapEvent==null&&l.Army==a&&support.Army==a&&g.PendingReleaseReason=="case_ended","other member battle defers army disband while leader and support remain outside battle");
  Check(!IsActiveAssistanceArmy(a),"ended case stops active-assistance protection before deferred physical disband");
  h.MapEvent=null;e.ReconcileAssistanceFormations();
  Check(l.Army==null&&support.Army==null,"once battle ends ordinary reconciliation removes retained army and support");
  Console.WriteLine("ASSISTANCE_LIFECYCLE_BOUNDARY: ended case + other member fighting -> physical army retained, active protection false -> battle ends -> released; historical army identity unknown");
  e=Setup(out l,out h,out t);VisitChecks(l,h);
  l.Army=null;h.Army=null;var recruitingArmy=new Army(null,l,Army.ArmyTypes.Patrolling);h.Army=recruitingArmy;
  var entry=new NativeRecruiter();var village=new Settlement();entry.OnBeforeSettlementEntered(l,village,l.LeaderHero!);
  Check(entry.Calls.Count==7&&entry.Calls.All(p=>p==l),"native entry recruits for leader but not distant member");
  entry.Calls.Clear();h.AttachedTo=l;entry.OnBeforeSettlementEntered(l,village,l.LeaderHero!);Check(entry.Calls.Count==14&&entry.Calls.Distinct().Count()==2,"native entry dispatches recruitment for both local parties");
  entry.Calls.Clear();village.IsRaided=true;entry.OnBeforeSettlementEntered(l,village,l.LeaderHero!);Check(entry.Calls.Count==0,"native raided-village gate preserved");
  Console.WriteLine("ASSISTANCE_ASSERTIONS="+Checks+"; PASS");
 }
 static void VisitChecks(MobileParty l,MobileParty h){
  l.Army=null;h.Army=null;l.MemberRoster.TotalManCount=99;l.MemberRoster.TotalWounded=9;l.PartySizeRatio=99f/159;
  var q=GwpWardenVisitDesire.LocalPartyParameters(l);Check(q.Item3==9&&q.Item4==99,"standalone statistics include leader");
  var a=new Army(null,l,Army.ArmyTypes.Patrolling);h.Army=a;q=GwpWardenVisitDesire.LocalPartyParameters(l);
  Check(q.Item1==l.PartySizeRatio&&q.Item2==1&&q.Item3==9&&q.Item4==99,"en-route helper not in numerator or denominator");
  h.AttachedTo=l;h.MemberRoster.TotalManCount=30;h.MemberRoster.TotalWounded=6;h.PartySizeRatio=.3f;
  q=GwpWardenVisitDesire.LocalPartyParameters(l);Check(q.Item4==129&&q.Item3==15,"leader and attached helper both included");Check(Math.Abs(q.Item1-(l.PartySizeRatio+.3f)/2)<.0001,"mean uses actual local count");
  h.IsActive=false;q=GwpWardenVisitDesire.LocalPartyParameters(l);Check(q.Item4==99,"inactive attached member excluded");h.IsActive=true;
  a.Kingdom=new Kingdom();var original=(.1f,.2f,3,4);var unchanged=original;GwpWardenVisitPartyParametersPatch.Run(l,ref unchanged);Check(unchanged==original,"kingdom statistics untouched");
  a.Kingdom=null;GwpWardenVisitPartyParametersPatch.Run(l,ref unchanged);Check(unchanged.Item4==129,"production patch applies local statistics");
  l.IsWarden=false;unchanged=original;GwpWardenVisitPartyParametersPatch.Run(l,ref unchanged);Check(unchanged==original,"other family untouched");l.IsWarden=true;
  h.LeaderHero=null;q=GwpWardenVisitDesire.LocalPartyParameters(l);Check(q.Item4==129,"leaderless attached support counted");
  h.AttachedTo=null;var old=NativePartyParameters(l);Check(old.Item4==0&&float.IsNaN((float)old.Item3/old.Item4),"native pre-fix empty-attached negative control");
  q=GwpWardenVisitDesire.LocalPartyParameters(l);Check(!float.IsNaN((float)q.Item3/q.Item4)&&q.Item4==99,"fixed wounded ratio finite during gathering");
 }
 NATIVE_METHOD
}
static class GwpWardenVisitDesire {public static bool AppliesTo(MobileParty p)=>p.IsWarden&&p.LeaderHero!=null;VISIT_METHODS}
 PATCH_CLASS
class NativeDisbandGuardProbe {private static int _authorizedDepth;GUARD_METHOD}
class NativeRecruiter {public List<MobileParty> Calls=new();private void CheckRecruiting(MobileParty p,Settlement s)=>Calls.Add(p);RECRUIT_METHOD}
}
'''


class AssistanceTests(unittest.TestCase):
    def test_production_lifecycle_gathering_support_and_visit_statistics(self):
        source = (MODULE / 'PoliceEnforcementBehavior.Assistance.cs').read_text(encoding='utf-8-sig')
        names = ['UpdateLordAssistance', 'TryFormAssistanceGroup', 'RequestAssistanceDutyIntent', 'GetAssistanceEvaluationTarget', 'GetActiveAssistanceCaseTarget',
                 'IsAssistanceGroupCaseStillActive', 'IsAssistancePlayerBountyEscort',
                 'MaintainAssistancePlayerBountyEscort', 'GrowPlayerBountyAssistanceStrength',
                 'AdvancePlayerBountyAssistanceStrength', 'EnsureCommittedStrengthAdvantage',
                 'GetCommittedAssistanceStrength', 'GetAvailableAssistanceCandidates',
                 'IsAvailableAssistanceCandidate', 'IsPartyHeldForDuel', 'DuelForParty', 'IsAwaitingEnvoyResult', 'MaintainAssistanceArmy', 'CreateOrRecoverAssistanceArmy',
                 'TryAddAssistanceMember', 'TryMergeArmyMember', 'ReleasePartyFromAssistance',
                 'TryShrinkAssistanceGroup', 'RemoveAssistanceMember', 'ReleaseAssistanceGroup',
                 'IsArmyOwnedByGroup', 'IsActiveAssistanceArmy', 'TryGetAssistanceDuty',
                 'IsUndeclaredAssistancePursuit', 'GetAssistanceTravelHours', 'GetAssistanceRequiredStrength', 'FindParty', 'FindActiveParty',
                 'IsActiveGreyWardenLordParty', 'CanContinueLeadingPoliceTask',
                 'MaintainSpeedDispersedPursuit', 'IsSpeedDetached', 'AddSpeedDetachedParty',
                 'RequestAssistanceTargetIntent', 'ShouldDisperseAssistanceArmyForSpeed',
                 'CanReformAssistanceArmyAfterSpeedDispersal', 'DisperseAssistanceArmyForSpeed',
                 'SendDelayPatrolsAfterSpeedDispersal', 'SyncAssistanceData', 'RestoreEnforcementArmyTicks']
        methods = [member(source, name) for name in names]
        delay = (MODULE / 'PoliceEnforcementBehavior.DelayPatrols.cs').read_text(encoding='utf-8-sig')
        methods.append(member(delay, 'TryAssignDelayPatrolToAssistanceArmy'))
        methods.append(member(delay, 'GetImmediateInterceptorTroopSources'))
        visit = (MODULE / 'GwpWardenVisitDesire.cs').read_text(encoding='utf-8-sig')
        patch = visit[visit.index('    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "CalculatePartyParameters")]'):
                      visit.index('    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "FillSettlements')]
        patch = re.sub(r'    \[HarmonyPatch[^\n]+\]\n', '', patch).replace('private static void Postfix', 'public static void Run')
        native = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior',
            'D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll'], text=True, encoding='utf-8-sig')
        native_method = extract(native, 'CalculatePartyParameters').replace('CalculatePartyParameters', 'NativePartyParameters').replace('private (', 'private static (')
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        harness = STUBS.replace(' METHODS\n', '\n'.join(methods) + '\n').replace('VISIT_METHODS', '\n'.join(member(visit, n) for n in ['LocalMembers', 'LocalPartyParameters']))
        harness = harness.replace('PATCH_CLASS', patch).replace('NATIVE_METHOD', native_method)
        guard = (MODULE / 'GwpAssistanceArmyLifecyclePatch.cs').read_text(encoding='utf-8-sig')
        harness = harness.replace('GUARD_METHOD', extract(guard, 'Prefix').replace('private static bool Prefix', 'public static bool Run'))
        recruiting = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior',
            'D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll'], text=True, encoding='utf-8-sig')
        harness = harness.replace('RECRUIT_METHOD', extract(recruiting, 'OnBeforeSettlementEntered'))
        self.assertNotIn('VISIT_', harness)
        formation = (MODULE / 'PoliceEnforcementBehavior.Formation.cs').read_text(encoding='utf-8-sig')
        formation = re.sub(r'^using (?:Helpers|TaleWorlds)[^\n]*\n', '', formation, flags=re.M)
        with tempfile.TemporaryDirectory(prefix='gwp-assistance-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(harness, encoding='utf-8')
            (path / 'Formation.cs').write_text(formation, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, '\n'.join(l for l in result.stdout.splitlines() if ': warning ' not in l) + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith(('ASSISTANCE_LIFECYCLE_BOUNDARY:', 'ASSISTANCE_ASSERTIONS='))))
