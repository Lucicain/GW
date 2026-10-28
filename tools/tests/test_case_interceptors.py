"""Execute production hourly scheduling, interceptor gates and troop selection.

Party creation, navigation speeds and regional strength are controlled boundaries;
this does not simulate a campaign scene or native map battles.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_assistance import member
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Globalization;using IFaction=GreyWardenPolicePurity.Faction;
namespace GreyWardenPolicePurity {
class Vec {public float X;public float Distance(Vec b)=>Math.Abs(X-b.X);}
class CharacterObject {public string StringId="troop";public bool IsHero,IsMounted,Warden=true;public int Tier=4;}
class TroopRosterElement {public CharacterObject Character=null!;public int Number,WoundedNumber;}
class Roster {public List<TroopRosterElement> Rows=new();public List<TroopRosterElement> GetTroopRoster()=>Rows;}
class PartyBase {public MobileParty Owner=null!;public int NumberOfHealthyMembers=>Owner.MemberRoster.Rows.Sum(x=>x.Number-x.WoundedNumber);}
class Settlement {public string StringId="town";public Vec GetPosition2D=new();public static Settlement? FindFirst(Func<Settlement,bool> f)=>null;}
class Army {public MobileParty LeaderParty=null!;public List<MobileParty> Parties=new();}
class MobileParty {public static List<MobileParty> All=new();public static MobileParty MainParty=new(){IsActive=false};public Clan ActualClan=new();public string StringId="";public bool IsActive=true,IsMainParty,Busy,Stranded;public float Speed=3,Theory=3,Strength=300,StrengthPerTroop;public object? MapEvent;public Settlement? CurrentSettlement;public Army? Army;public MobileParty? AttachedTo;public Roster MemberRoster=new();public PartyBase Party;public Vec GetPosition2D=new();public MobileParty(){Party=new(){Owner=this};}}
enum PoliceTaskFlowState {WarPursuit,PlayerBountyEscort,EscortingPlayer,PreparingDispatch,Pursuit}
class Faction {public string StringId="enemy";}
class Clan:Faction {public Faction MapFaction=>this;}
static class PoliceStats {public static Clan GetPoliceClan()=>new();}
static class FactionManager {public static bool WarExists=true;public static bool IsAtWarAgainstFaction(Faction a,Faction b)=>WarExists;}
static class GwpPeace {public static bool EndPoliceWar(Faction? f)=>true;}
class CrimeRecord {public string OffenderHeroId="criminal";public MobileParty? Offender;public bool HasOpenCase=true;}
class PoliceTask {public string PolicePartyId="chief";public bool WarDeclared=true,IsPlayerBountyEscort,IsPreparingDispatch;public PoliceTaskFlowState FlowState=PoliceTaskFlowState.WarPursuit;public CrimeRecord? TargetCrime;public Faction? WarTarget=new();public Settlement? EscortSettlement;public bool IsTargetValid()=>TargetCrime?.Offender?.IsActive==true;}
static class CrimeState {public static Dictionary<string,PoliceTask> ActiveTasks=new();public static PoliceTask? GetTask(string id)=>ActiveTasks.GetValueOrDefault(id);}
static class PlayerState {public static int Reputation=21;}
static class PlayerCaptivity {public static bool IsCaptive;}
class PlayerBountyBehavior {public static bool Awaiting;public static int Notified;public static bool AwaitingSupportRequest(PoliceTask? t)=>Awaiting;public void NotifySupportWarDeclared(MobileParty p){Notified++;}}
class Campaign {public static Campaign? Current=new();public T? GetCampaignBehavior<T>() where T:class=>new PlayerBountyBehavior() as T;}
static class GwpPlayerCaseAssignment {public static bool Reserved;public static bool IsReservedTarget(MobileParty p)=>Reserved;public static bool IsCaseHeldByPlayer(string? id)=>false;}
static class GwpWardenDuties {public static bool CanContinueCase(MobileParty p)=>!p.Busy;}
static class GwpCommon {public static bool IsGreyWardenTroop(CharacterObject c)=>c.Warden;public static bool IsGreyWardenEnforcementParty(MobileParty p)=>p.StringId=="chief";public static Settlement? FindNearestTown(MobileParty p)=>null;}
static class GwpTuning {public static class Enforcement {public const float WarDistance=3,PlayerWarDistance=3;}}
static class GwpStrength {public static bool LocalReady=true,AfterDrawReady=true;public class LocalBalance {public bool StrengthReady;public float Distance,FriendlyLocalStrength=300,EnemyLocalStrength=100;}
 public static bool IsInNativeEvaluation(MobileParty p)=>false;public static bool NativeAttackReady(MobileParty a,MobileParty b)=>throw new Exception("unexpected native boundary");
 public static MobileParty CombatGroupOf(MobileParty p)=>p;public static float Party(MobileParty p)=>p.Strength;public static float CombatGroup(MobileParty p)=>p.Strength;public static float TheoreticalBaseSpeed(MobileParty p)=>p.Theory;
 public static LocalBalance Compare(MobileParty a,MobileParty b,bool player=false,MobileParty? excludedFriendly=null)=>new(){StrengthReady=LocalReady&&a.Strength>b.Strength&&(excludedFriendly==null||AfterDrawReady),Distance=a.GetPosition2D.Distance(b.GetPosition2D)};}
static class PoliceResourceManager {public static bool IsStrandedAtSea(MobileParty p)=>p.Stranded;public static void OutfitTemporaryDutyParty(MobileParty p,MobileParty source){}}
static class GwpTextCatalog {public const string gwp_policeenforcementbehavior_delaypatrols_005="interceptor";}
static class GwpText {public static string Get(string k)=>k;}
static class GwpAiDiagnostics {public static List<string> Actions=new();public static void WriteAction(MobileParty p,string a,string detail)=>Actions.Add(a+":"+detail);}
static class GreyWardenPartyDesireBehavior {public static List<string> Orders=new();public static void RequestPursuit(MobileParty p,MobileParty t,float priority)=>Orders.Add(p.StringId);public static void RequestVisit(MobileParty p,Settlement t,float priority)=>throw new Exception("unexpected visit during active city pursuit");public static void RequestEscort(MobileParty p,MobileParty t,float priority){}public static void RequestImmediateRethink(MobileParty p){}}
class PoliceEnforcementBehavior {
 const int ImmediateInterceptorMaximumSize=8,ImmediateInterceptorMinimumSize=3;
 class LordAssistanceGroup {public string LeaderPartyId="chief",TargetPartyId="target";public bool DispersedForSpeed;public List<string> MemberPartyIds=new();}
 class DelayPatrolState {public string PatrolPartyId="",SourceTaskPolicePartyId="chief",TargetPartyId="target",WarTargetId="",ReturnSettlementId="",CityCrimeId="";public bool Returning,IsImmediateInterceptor,IsEnvoy;}
 class ImmediateInterceptorTroopTransfer {public MobileParty SourceParty=null!;public CharacterObject Character=null!;public int Count;}
 Dictionary<string,LordAssistanceGroup> _assistanceGroups=new();Dictionary<string,DelayPatrolState> _delayPatrolStates=new();int sequence,WarCalls,WarStarts;bool CreationUnavailable;List<string> Events=new();
 static PoliceEnforcementBehavior? _instance;bool Member,AwaitingEnvoy;int _warStatusCheckDayCounter,SupportRequests;
 enum CaseTaskEnd {Released}
 bool IsAssistanceMember(string id)=>Member;
 static bool CanContinueLeadingPoliceTask(MobileParty? p)=>p?.IsActive==true;
 void FailTaskBecauseOwnerCannotLead(string id,string why)=>CrimeState.ActiveTasks.Remove(id);
 void UpdatePlayerBountyEscortCase(MobileParty p,PoliceTask t,bool x)=>throw new Exception("unused player branch");
 void ClearTaskWarTracking(string id,bool x){}void ExecutePunishment(Settlement? s,PoliceTask t)=>throw new Exception("unused escort branch");
 void ReleaseWardensFromCase(string? id)=>throw new Exception("unused reserved case branch");
 void EndCaseTask(string id,CaseTaskEnd k,string why)=>CrimeState.ActiveTasks.Remove(id);
 bool HoldWarForEnvoy(MobileParty p,PoliceTask t,MobileParty target)=>AwaitingEnvoy;
 float GetAssistanceContactDistance(MobileParty p,MobileParty t,out MobileParty contact){contact=p;return p.GetPosition2D.Distance(t.GetPosition2D);}
 bool IsUndeclaredAssistancePursuit(LordAssistanceGroup g)=>false;float GetNativeMaximumGoAroundDistance()=>6;
 bool HasAssistanceEngagementStrengthAdvantage(MobileParty p,MobileParty t,out float ours,out float theirs){ours=p.Strength;theirs=t.Strength;return GwpStrength.LocalReady;}
 bool TryGetNativeDeclarationCandidate(MobileParty p,MobileParty t,float d,out MobileParty contact,out GwpStrength.LocalBalance b){contact=p;b=GwpStrength.Compare(p,t);return b.Distance<=d&&b.StrengthReady&&p.CurrentSettlement==null&&t.CurrentSettlement==null;}
 string FormatLocalStrengthDeclarationDiagnostic(GwpStrength.LocalBalance b,float a,float c)=>"";
 void DeclareWar(PoliceTask t,MobileParty target){Events.Add("war");WarCalls++;if(!FactionManager.WarExists)WarStarts++;FactionManager.WarExists=true;t.WarDeclared=true;if(!t.IsPlayerBountyEscort)t.FlowState=PoliceTaskFlowState.WarPursuit;}
 void RefreshAssistanceDutyAfterWarDeclaration(MobileParty p){}
 static bool TryGetAssistanceCase(MobileParty a,out PoliceTask? task,out MobileParty? target){task=null;target=null;return false;}
 static bool IsPartyOccupiedByAssistance(MobileParty p)=>false;
 bool IsAwaitingEnvoyResult(MobileParty p,PoliceTask t,MobileParty target)=>AwaitingEnvoy;
 bool IsArmyOwnedByGroup(Army a,LordAssistanceGroup g)=>a.LeaderParty.StringId==g.LeaderPartyId;
 MobileParty? CreateDetachment(MobileParty chief,IEnumerable<ImmediateInterceptorTroopTransfer> batches,int size,int min,string name,List<ImmediateInterceptorTroopTransfer> movedRows,out int moved,out Settlement? town){
  moved=0;town=new();if(CreationUnavailable)return null;Events.Add("prepare");var party=new MobileParty{StringId="interceptor_"+(++sequence),Theory=NewSpeed,Speed=NewSpeed};
  foreach(var b in batches){var row=b.SourceParty.MemberRoster.Rows.Single(x=>x.Character==b.Character);int take=Math.Min(size-moved,row.Number-row.WoundedNumber);if(take<=0)continue;row.Number-=take;b.SourceParty.Strength-=take*b.SourceParty.StrengthPerTroop;party.MemberRoster.Rows.Add(new(){Character=b.Character,Number=take});movedRows.Add(new(){SourceParty=b.SourceParty,Character=b.Character,Count=take});moved+=take;}
  MobileParty.All.Add(party);return party;
 }
 float NewSpeed=5;
 static void RollBackImmediateInterceptorTroops(Roster roster,IEnumerable<ImmediateInterceptorTroopTransfer> transfers){foreach(var b in transfers){b.SourceParty.MemberRoster.Rows.Single(x=>x.Character==b.Character).Number+=b.Count;b.SourceParty.Strength+=b.Count*b.SourceParty.StrengthPerTroop;}roster.Rows.Clear();}
 bool TryDestroyDelayPatrolParty(MobileParty p){p.IsActive=false;return true;}
 void CleanupDelayPatrolsInsideSettlements(){}void CleanupStalePoliceWarsWithoutReasons(Clan c){}void EnsureNearestPoliceForWantedPlayer(){}
 bool SpawnSingleDelayPatrol(MobileParty target,string chief,string war){SupportRequests++;return true;}
 static bool IsActivePlayerBountyInterceptor(DelayPatrolState s,Clan? c)=>false;
 static void DetachDelayPatrolFromArmy(MobileParty p){p.Army=null;}
 bool TryReturnImmediateInterceptorToSource(MobileParty p,DelayPatrolState s)=>throw new Exception("unexpected return during active city pursuit");
 static bool RetireReturningPatrol(MobileParty p,Settlement? s)=>throw new Exception("unexpected retirement during active city pursuit");
 bool TryAssignDelayPatrolToAssistanceArmy(MobileParty p,DelayPatrolState s)=>false;
 void MarkDelayPatrolReturning(string id){_delayPatrolStates[id].Returning=true;}
 void ReconcileAssistanceFormations(){}
 PRODUCTION
 static int checks;static void Check(bool x,string message){checks++;if(!x)throw new Exception(message);}
 static TroopRosterElement Troops(string id,int n,bool mounted=false,int wounded=0)=>new(){Character=new(){StringId=id,IsMounted=mounted},Number=n,WoundedNumber=wounded};
 static PoliceEnforcementBehavior Setup(out MobileParty chief,out MobileParty target,out PoliceTask task){
  chief=new(){StringId="chief"};target=new(){StringId="target",Theory=3.7f,Strength=110,GetPosition2D=new(){X=6}};chief.MemberRoster.Rows.Add(Troops("knight",4,true));chief.MemberRoster.Rows.Add(Troops("infantry",100));target.MemberRoster.Rows.Add(Troops("enemy",50));
  MobileParty.All=new(){chief,target};task=new(){TargetCrime=new(){Offender=target}};CrimeState.ActiveTasks=new(){{"chief",task}};GwpStrength.LocalReady=GwpStrength.AfterDrawReady=true;FactionManager.WarExists=true;PlayerBountyBehavior.Awaiting=false;PlayerBountyBehavior.Notified=0;MobileParty.MainParty=new(){IsActive=false};GwpPlayerCaseAssignment.Reserved=false;GreyWardenPartyDesireBehavior.Orders.Clear();GwpAiDiagnostics.Actions.Clear();var e=new PoliceEnforcementBehavior();_instance=e;return e;
 }
 int Deployments=>GreyWardenPartyDesireBehavior.Orders.Distinct().Count();
 MobileParty Last()=>MobileParty.All.Last(x=>x.StringId.StartsWith("interceptor_"));
 public static void Main(){
  var e=Setup(out var c,out var t,out var task);e.UpdateTasks();var first=e.Last();
  Check(e.Deployments==1&&first.Party.NumberOfHealthyMembers==8,"ordinary post-declaration hour at distance 6 deploys full eight without assistance");
  Check(first.MemberRoster.Rows.Single(x=>x.Character.StringId=="knight").Number==4&&first.MemberRoster.Rows.Single(x=>x.Character.StringId=="infantry").Number==4,"four cavalry supplemented by four infantry");
  Check(c.Party.NumberOfHealthyMembers==96,"eight actual troops leave source");
  Check(GreyWardenPartyDesireBehavior.Orders.SequenceEqual(new[]{first.StringId}),"only interceptor receives pursuit order; native chief desire untouched");
  e.UpdateTasks();Check(e.Deployments==1,"live interceptor prevents duplicate deployment");
  first.MemberRoster.Rows.Clear();e.UpdateTasks();Check(e.Deployments==2,"defeated zero-healthy roster no longer blocks replacement before native party removal");
  var second=e.Last();second.IsActive=false;e.UpdateTasks();Check(e.Deployments==3,"removed interceptor replaced next ordinary update with no cooldown");
  e=Setup(out c,out t,out task);c.MemberRoster.Rows.RemoveAt(0);e.UpdateTasks();Check(e.Deployments==1&&e.Last().Party.NumberOfHealthyMembers==8,"no cavalry still forms eight infantry");
  e=Setup(out c,out t,out task);c.MemberRoster.Rows=new(){Troops("hero",1),Troops("wounded",10,false,10),Troops("infantry",3)};c.MemberRoster.Rows[0].Character.IsHero=true;e.UpdateTasks();Check(e.Last().Party.NumberOfHealthyMembers==3&&c.Party.NumberOfHealthyMembers==1,"heroes and wounded excluded, three healthy troops sufficient");
  e=Setup(out c,out t,out task);c.MemberRoster.Rows=new(){Troops("infantry",2)};e.UpdateTasks();Check(e.Deployments==0,"fewer than minimum troops does not spawn");
  e=Setup(out c,out t,out task);e.NewSpeed=3.5f;int before=c.Party.NumberOfHealthyMembers;e.UpdateTasks();Check(e.Deployments==1&&c.Party.NumberOfHealthyMembers==before-8,"slower mixed detachment is no longer discarded by theoretical speed");
  for(int blocker=0;blocker<9;blocker++){
   e=Setup(out c,out t,out task);
   switch(blocker){case 0:c.CurrentSettlement=new();break;case 1:t.CurrentSettlement=new();break;case 2:GwpStrength.LocalReady=false;break;case 3:e.AwaitingEnvoy=true;break;case 4:e.Member=true;break;case 5:c.Busy=true;break;case 6:GwpPlayerCaseAssignment.Reserved=true;break;case 7:c.Stranded=true;break;case 8:c.MapEvent=new();break;}
   e.UpdateTasks();Check(e.Deployments==0,"existing attack/duty/navigation gate retained: "+blocker);
  }
  foreach(var speeds in new[]{(3.32f,3.32f,3.32f),(3.325f,3.32f,3.325f),(3.32f,3.02f,1.992f)}){
   e=Setup(out c,out t,out task);c.Theory=speeds.Item1;t.Theory=speeds.Item2;c.Speed=speeds.Item3;t.Speed=speeds.Item2;
   task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=3;
   e.UpdateTasks();
   Check(task.WarDeclared&&task.FlowState==PoliceTaskFlowState.WarPursuit,"region-ready declaration can occur at equal or theoretical-only speed advantage");
   Check(e.Deployments==1&&e.Last().Party.NumberOfHealthyMembers==8,"safe declaration deploys at equal, nearly equal and temporarily reduced speed: "+speeds);
   Console.WriteLine($"INTERCEPTOR_SPEED_POLICY: sourceTheory={c.Theory:F3}, targetTheory={t.Theory:F3}, sourceCurrent={c.Speed:F3}, war={task.WarDeclared}, deployed={e.Deployments}");
  }
  e=Setup(out c,out t,out task);c.Theory=8;t.Theory=0;e.NewSpeed=0;
  e.UpdateTasks();Check(e.Deployments==1,"neither source nor interceptor speed is a dispatch gate");
  e=Setup(out c,out t,out task);e.UpdateTasks();first=e.Last();first.IsActive=false;t.Strength=400;before=c.Party.NumberOfHealthyMembers;
  for(int hour=0;hour<48;hour++)e.UpdateTasks();
  Check(task.WarDeclared&&e.Deployments==1&&c.Party.NumberOfHealthyMembers==before,"target growth after declaration prevents replacement and further troop drain");
  t.Strength=110;e.UpdateTasks();Check(e.Deployments==2,"fresh restored advantage permits replacement without another declaration");
  e=Setup(out c,out t,out task);c.StrengthPerTroop=4;
  for(int attempt=0;attempt<20;attempt++){e.UpdateTasks();if(e.Deployments>0)e.Last().IsActive=false;}
  Check(e.Deployments==5&&c.Party.NumberOfHealthyMembers==64&&c.Strength==140,"repeated interceptor losses stop before another detachment would remove regional advantage");
  Check(MobileParty.All.Where(p=>p.StringId.StartsWith("interceptor_")).All(p=>!p.IsActive),"refused post-transfer detachments are retired");
  e=Setup(out c,out t,out task);c.Strength=125;c.StrengthPerTroop=4;before=c.Party.NumberOfHealthyMembers;
  for(int attempt=0;attempt<4;attempt++)e.UpdateTasks();
  Check(e.Deployments==0&&c.Party.NumberOfHealthyMembers==before&&c.Strength==125,"marginal pre-transfer advantage cannot publish a squad and all troops are restored");
  // Negative control restores the old declare-then-prepare sequence below.
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;c.Strength=125;c.StrengthPerTroop=4;before=c.Party.NumberOfHealthyMembers;
  for(int attempt=0;attempt<4;attempt++)e.UpdateTasks();
  Check(!task.WarDeclared&&e.WarCalls==0&&e.Deployments==0,"marginal undeclared case must not cross diplomacy when splitting loses advantage");
  Check(c.Party.NumberOfHealthyMembers==before&&c.Strength==125,"failed declaration preparation restores all actual troops and strength");
  c.Strength=300;e.Events.Clear();e.UpdateTasks();
  Check(task.WarDeclared&&e.WarCalls==1&&e.Deployments==1,"new advantage permits one prepared declaration and one interceptor");
  Check(e.Events.SequenceEqual(new[]{"prepare","war"}),"real detachment preparation precedes declaration within the same call");
  for(int blocker=0;blocker<4;blocker++){
   e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;
   switch(blocker){case 0:c.Stranded=true;break;case 1:c.MemberRoster.Rows=new(){Troops("infantry",2)};break;case 2:e.CreationUnavailable=true;break;case 3:c.MapEvent=new();break;}
   before=c.Party.NumberOfHealthyMembers;e.UpdateTasks();
   Check(e.WarCalls==0&&!task.WarDeclared&&e.Deployments==0&&c.Party.NumberOfHealthyMembers==before,"resource/combat preparation blocker cannot leave an empty declaration: "+blocker);
  }
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;e._assistanceGroups["chief"]=new(){DispersedForSpeed=true};
  e.UpdateTasks();Check(e.WarCalls==1&&e.Deployments==1,"dispersed assistance prepares before declaration too");
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;e._assistanceGroups["chief"]=new();c.Army=new(){LeaderParty=c,Parties=new(){c}};
  e.UpdateTasks();Check(e.WarCalls==1&&e.Deployments==1,"assembled assistance prepares before declaration too");
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.IsPlayerBountyEscort=true;task.FlowState=PoliceTaskFlowState.PlayerBountyEscort;t.GetPosition2D.X=2;MobileParty.MainParty=new(){StringId="player"};c.Strength=125;c.StrengthPerTroop=4;
  e.AdvancePlayerBountyEscortCase(c,task,false);
  Check(!task.WarDeclared&&e.WarCalls==0&&e.Deployments==0&&PlayerBountyBehavior.Notified==0,"player support cannot notify a declaration before interceptor preparation succeeds");
  c.Strength=300;e.Events.Clear();e.AdvancePlayerBountyEscortCase(c,task,false);
  Check(task.WarDeclared&&e.WarCalls==1&&e.Deployments==1&&PlayerBountyBehavior.Notified==1,"player support declares and notifies only with an actual ready interceptor");
  Check(e.Events.SequenceEqual(new[]{"prepare","war"}),"player support uses the same prepare-before-declare order");
  e=Setup(out c,out t,out task);task.WarDeclared=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;c.Strength=125;c.StrengthPerTroop=4;
  e.UpdateTasks();Check(task.WarDeclared&&e.WarStarts==0&&e.Deployments==0,"existing faction war is registered for this case without inventing a new war or unsafe detachment");
  e=Setup(out c,out t,out task);task.WarDeclared=false;task.IsPlayerBountyEscort=true;task.FlowState=PoliceTaskFlowState.PlayerBountyEscort;t.GetPosition2D.X=2;c.Strength=125;c.StrengthPerTroop=4;MobileParty.MainParty=new(){StringId="player"};
  e.AdvancePlayerBountyEscortCase(c,task,false);Check(task.WarDeclared&&e.WarStarts==0&&e.Deployments==0&&PlayerBountyBehavior.Notified==1,"player support joins an existing faction war even when splitting a new squad is unsafe");
  e=Setup(out c,out t,out task);t.CurrentSettlement=new();e.UpdateTasks();t.CurrentSettlement=null;e.UpdateTasks();Check(e.Deployments==1,"target departure resumes ordinary replacement without new declaration");
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;c.GetPosition2D.X=4;e.UpdateTasks();Check(e.Deployments==1,"initial declaration still deploys once despite same-hour repeat check");
  e=Setup(out c,out t,out task);e._assistanceGroups["chief"]=new(){DispersedForSpeed=true};e.UpdateTasks();Check(e.Deployments==1,"dispersed assistance path preserved");
  e=Setup(out c,out t,out task);e._assistanceGroups["chief"]=new();c.Army=new(){LeaderParty=c,Parties=new(){c}};e.UpdateTasks();Check(e.Deployments==1,"assembled assistance path preserved");
  e=Setup(out c,out t,out task);GwpStrength.AfterDrawReady=false;var beforeRegionalRollback=c.MemberRoster.Rows.Sum(x=>x.Number);e.UpdateTasks();
  Check(e.Deployments==0&&GreyWardenPartyDesireBehavior.Orders.Count==0,"post-transfer local disadvantage withholds interception order");
  Check(c.MemberRoster.Rows.Sum(x=>x.Number)==beforeRegionalRollback,"post-transfer regional refusal returns all actually drawn troops");
  e=Setup(out c,out t,out task);task.WarDeclared=false;FactionManager.WarExists=false;task.FlowState=PoliceTaskFlowState.Pursuit;t.GetPosition2D.X=2;
  e.UpdateTasks();first=e.Last();int cityEntryCount=c.Party.NumberOfHealthyMembers;
  Check(task.WarDeclared&&e.Deployments==1&&cityEntryCount==96,"near declaration draws exactly eight once");
  t.CurrentSettlement=new();
  for(int hour=1;hour<=72;hour++){e.UpdateDelayPatrols();e.UpdateTasks();if(hour%24==0)e.OnDailyTick();}
  Check(e.Deployments==1&&c.Party.NumberOfHealthyMembers==cityEntryCount,"72 city hours do not draw more regular interceptors");
  Check(first.IsActive&&!e._delayPatrolStates[first.StringId].Returning&&GreyWardenPartyDesireBehavior.Orders.Last()==first.StringId,"existing interceptor keeps pursuit rather than returning solely because target entered city");
  Check(e.SupportRequests==1,"global day-two scan still requests support during legitimate city wait");
  first.IsActive=false;
  for(int hour=1;hour<=48;hour++){e.UpdateDelayPatrols();e.UpdateTasks();if(hour%24==0)e.OnDailyTick();}
  Check(e.Deployments==1&&c.Party.NumberOfHealthyMembers==cityEntryCount,"dead interceptor is not replaced while offender stays in city");
  Check(e.SupportRequests==2,"another global two-day scan requests support again without progress or duplicate gate");
  t.CurrentSettlement=null;e.UpdateTasks();
  Check(e.Deployments==2&&c.Party.NumberOfHealthyMembers==cityEntryCount-8,"departure rechecks authorization and replaces defeated interceptor");
  e.Last().IsActive=false;GwpStrength.LocalReady=false;e.UpdateTasks();
  Check(e.Deployments==2&&c.Party.NumberOfHealthyMembers==cityEntryCount-8,"lost regional permission prevents repeated draining after chief leaves contact");
  Console.WriteLine("INTERCEPTOR_CITY_SEQUENCE: near declaration -> 72h city wait -> interceptor lost -> 48h city wait -> departure -> region denied; regular deployments=2, periodic support requests=2");
  e=Setup(out c,out t,out task);c.MapEvent=t.MapEvent=new object();
  e.OnDailyTick();e.OnDailyTick();
  Check(e.SupportRequests==1,"confirmed bug: global two-day scan requests fallback even while chief and offender share the same battle");
  Console.WriteLine("INTERCEPTOR_ASSERTIONS="+checks+"; PASS");
 }
}}
'''


class CaseInterceptorTests(unittest.TestCase):
    def test_hourly_replacement_and_real_selection(self):
        delay = (MODULE / 'PoliceEnforcementBehavior.DelayPatrols.cs').read_text(encoding='utf-8-sig')
        behavior = (MODULE / 'PoliceEnforcementBehavior.cs').read_text(encoding='utf-8-sig')
        assistance = (MODULE / 'PoliceEnforcementBehavior.Assistance.cs').read_text(encoding='utf-8-sig')
        methods = [extract(behavior, name) for name in ['UpdateTasks', 'AdvancePlayerBountyEscortCase']]
        methods += [extract(delay, n) for n in ['TryDeclareCaseWarWithInterceptor', 'TrySpawnImmediateCaseInterceptor', 'UpdateDelayPatrols',
            'GetEligibleDelaySupportTasks', 'CheckPersistentWarTargetsEveryTwoDays', 'OnDailyTick']]
        methods += [member(delay, n) for n in ['GetImmediateInterceptorTroopSources', 'DetachmentBatches']]
        healthy_start = delay.index('        private static int HealthyTroopCount(')
        methods.append(delay[healthy_start:delay.index('\n\n', healthy_start)])
        methods += [member(assistance, n) for n in ['FindActiveParty', 'TryGetNativeCaseTarget', 'NativeCaseIncludesPlayer', 'CanInitiateCaseAttack']]
        code = HARNESS.replace('PRODUCTION', '\n'.join(methods))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-interceptor-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('INTERCEPTOR_')))
            legacy_call = "TryDeclareCaseWarWithInterceptor(pp, task, criminal,\n                        declarationPrediction!);"
            legacy = code.replace(legacy_call, "DeclareWar(task, criminal); RefreshAssistanceDutyAfterWarDeclaration(pp); TrySpawnImmediateCaseInterceptor(pp, task, criminal, declarationPrediction!);", 1)
            self.assertNotEqual(code, legacy, 'negative control must restore the old ordering')
            (path / 'Program.cs').write_text(legacy, encoding='utf-8')
            negative = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertNotEqual(0, negative.returncode, 'old declare-first ordering must fail')
            self.assertIn('marginal undeclared case must not cross diplomacy', negative.stdout + negative.stderr)
            print('INTERCEPTOR_DECLARATION_NEGATIVE: restored old ordering declares despite failed troop-split check; repaired production passes')


if __name__ == '__main__':
    unittest.main()
