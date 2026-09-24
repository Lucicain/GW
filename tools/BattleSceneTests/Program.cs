using System;
using System.Collections.Generic;
using System.Linq;
using GreyWardenPolicePurity;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
static class Program
{
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 static BasicCharacterObject Troop(bool grey,float power=1,bool hero=false)=>new(){StringId=grey?"gwarcher":"plain",Power=power,IsHero=hero};
 static (Mission mission,DefaultBattleMissionAgentSpawnLogic spawn,GwpBattleSceneContext context,GwpBattleReinforcementBehavior support) Battle(int ours,int enemy,bool greyOurs=true,bool greyEnemy=false)
 {
  Campaign.Current=null;MBRandom.Rolls.Clear();MBRandom.Calls=0;SoundEvent.Active=0;
  var m=new Mission{PlayerTeam=new Team{Side=BattleSideEnum.Defender}};
  var sp=new DefaultBattleMissionAgentSpawnLogic();var c=new GwpBattleSceneContext();
  var support=new GwpBattleReinforcementBehavior(Troop(true,2),Troop(true,3),Troop(true,5));
  m.Add(sp);m.Add(c);m.Add(support);m.Add(new GwpSyndicateMusicBehavior());
  void Add(int count,Team team,bool grey)
  {
   for(int i=0;i<count;i++)
   {
    var t=Troop(grey);var o=new BasicBattleAgentOrigin(t){IsUnderPlayersCommand=team.Side==BattleSideEnum.Defender};
    sp.Rosters[(int)team.Side].Add(o);var a=new Agent{Mission=m,Team=team,Character=t,Origin=o};m.Agents.Add(a);
    c.OnAgentBuild(a,new Banner());if(team.Side==BattleSideEnum.Defender&&i==0)m.MainAgent=a;
   }
  }
  Add(ours,m.PlayerTeam,greyOurs);Add(enemy,new Team{Side=BattleSideEnum.Attacker},greyEnemy);
  // This fixture starts observing a depleted battle. Opening rosters also
  // retain the original casualties; they are not unspawned reserves.
  foreach(var roster in sp.Rosters)
   while(roster.Count<100)roster.Add(new BasicBattleAgentOrigin(roster[0].Troop));
  return(m,sp,c,support);
 }
 static void Main()
 {
  Check(!GwpBattleScenePolicy.MusicEligible(0,0),"no soldiers is not a majority");
  Check(!GwpBattleScenePolicy.MusicEligible(49,100)&&GwpBattleScenePolicy.MusicEligible(50,100),"music threshold exact boundary");
  for(int reputation=20;reputation<=100;reputation+=10)
  {
   Check(GwpBattleScenePolicy.Chance(reputation,2)>GwpBattleScenePolicy.Chance(reputation,1)
    &&GwpBattleScenePolicy.Chance(reputation,1)>GwpBattleScenePolicy.Chance(reputation,0),"each later chance is higher");
   Check(GwpBattleScenePolicy.Chance(reputation,2)>=GwpBattleScenePolicy.Chance(reputation-10,2),"reputation improves chance");
   Check(GwpBattleScenePolicy.PowerShare(true,reputation)>=GwpBattleScenePolicy.PowerShare(true,reputation-10),"reputation improves player power");
   Check(Math.Abs(GwpBattleScenePolicy.PowerShare(false,reputation)-.3f)<.00001,"NPC power is fixed regardless of reputation");
  }
  Check(GwpBattleScenePolicy.PowerShare(true,0)==.2f&&GwpBattleScenePolicy.PowerShare(true,999)==.5f,"player power clamps twenty to fifty percent");
  foreach(float enemyPower in new[]{0f,1f,10f,100f,1000f,15000f})
  foreach(float share in new[]{.2f,.3f,.5f})
  {
   var p=GwpBattleScenePolicy.Plan(enemyPower,share,2,3,5);float[] costs={2,3,5};float power=p.Sum(k=>costs[k]);
   Check(power<=enemyPower*share+.0001f,"support never exceeds selected power budget");
   Check(enemyPower*share-power<2.001f,"rounding leaves less than the cheapest troop");
  }
  Check(GwpBattleScenePolicy.Plan(float.NaN,.3f,2,3,5).Count==0,"invalid power cannot loop");
  var attempts=new GwpBattleSupportChecks();int rolls=0;
  Check(!attempts.Try(100,20,1,true,()=>{rolls++;return 0;})&&rolls==0,"reserves count toward first threshold");
  Check(!attempts.Try(100,20,0,true,()=>{rolls++;return .9f;})&&attempts.Attempts==1,"twenty percent first chance may fail");
  attempts.Try(100,11,0,true,()=>{rolls++;return 0;});Check(rolls==1,"no repeated first rolls above ten percent");
  Check(!attempts.Try(100,10,0,true,()=>{rolls++;return .9f;})&&attempts.Attempts==2,"ten percent second chance may fail");
  attempts.Try(100,1,1,true,()=>{rolls++;return 0;});Check(rolls==2,"last survivor cannot trigger with reserves");
  Check(attempts.Try(100,1,0,true,()=>{rolls++;return .6f;})&&rolls==3,"last survivor third chance succeeds at higher probability");
  attempts.Try(100,1,0,true,()=>{rolls++;return 0;});Check(rolls==3,"success prevents further rescue");
  attempts=new();rolls=0;
  Check(attempts.Try(100,1,0,true,()=>++rolls<3?.9f:0)&&rolls==3,"jump straight to one gets all three ordered chances");
  attempts=new();Check(!attempts.Try(100,0,0,true,()=>0)&&attempts.Attempts==0,"dead side cannot summon");
  attempts=new();Check(!attempts.Try(9,2,0,true,()=>0)&&attempts.Attempts==0,"nonintegral twenty percent threshold rounds down");
  Check(attempts.Try(9,1,0,true,()=>0),"tiny battle last survivor is reachable");
  attempts=new();rolls=0;attempts.Try(100,1,0,true,()=>{rolls++;return .99f;});attempts.Try(100,1,0,true,()=>{rolls++;return 0;});
  Check(rolls==3&&attempts.Complete&&!attempts.Succeeded,"all three failed stages are never retried");
  attempts=new();Check(attempts.Try(1000,200,0,true,()=>0),"large battle triggers at twenty percent rather than twenty people");
  attempts=new();Check(!attempts.Try(50,11,0,true,()=>0),"small battle does not trigger just because fewer than twenty remain");
  attempts=new();Check(!attempts.Try(100,10,0,false,()=>throw new Exception("ineligible roll"))&&attempts.Attempts==0,"ineligible side does not consume stages");

  var (m,sp,c,support)=Battle(10,100);
  m.MainAgent=null;MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  Check(!c.Ready&&m.Spawned.Count==0&&MBRandom.Calls==0,"no player present means no music capture or support rolls");
  m.MainAgent=m.Agents[0];sp.IsDeploymentOver=false;support.OnMissionTick(1);
  Check(c.Ready&&c.MusicEligible&&m.Spawned.Count==0,"music captures during deployment while support waits");
  // Snapshot must not change as agents and rosters change later.
  foreach(var o in sp.Rosters[0])o.Troop.StringId="plain";
  Check(c.TryCapture()&&c.MusicEligible,"opening music decision remains fixed");
  support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,100,false,true);MBRandom.Rolls.Enqueue(0);
  Check(c.TryCapture()&&c.MusicEligible,"enemy majority can enable music");
  support.OnMissionTick(1);Check(m.Spawned.Count==0,"unaffiliated player side cannot rescue");
  foreach(var a in m.Agents.Where(a=>a.Team.Side==BattleSideEnum.Attacker).Skip(10))a.Active=false;
  support.OnAgentRemoved(m.Agents.Last(),m.MainAgent!,AgentState.Killed,new());
  support.OnMissionTick(1);
  Check(m.Spawned.Count==1&&m.Spawned.All(a=>a.Team.Side==BattleSideEnum.Attacker),"enemy support belongs to enemy and uses opposing power");
  Check(m.GetMissionBehavior<GwpSyndicateMusicBehavior>()!.Arrivals.SequenceEqual(new[]{BattleSideEnum.Attacker}),"enemy music arrival exactly once");
  support.OnRemoveBehavior();Check(SoundEvent.Active==0,"mission exit releases horn");

  (m,sp,c,support)=Battle(10,100);MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  for(int i=0;i<8;i++)support.OnMissionTick(1);
  Check(m.Spawned.Sum(a=>a.CharacterPowerCached)<=50.0001f&&m.Spawned.Sum(a=>a.CharacterPowerCached)>48,"custom player uses maximum fifty percent power budget");
  Check(m.Spawned.All(a=>a.Origin.IsUnderPlayersCommand),"friendly support keeps command affiliation");
  bool depleted=true;GwpBattleSupportDepletionPatch.After(sp,BattleSideEnum.Defender,ref depleted);
  Check(!depleted,"living rescue troops prevent native supplier's premature defeat");
  bool enemyDepleted=true;GwpBattleSupportDepletionPatch.After(sp,BattleSideEnum.Attacker,ref enemyDepleted);
  Check(enemyDepleted,"support does not postpone the other side's defeat");
  foreach(var a in m.Spawned)a.Active=false;
  depleted=true;GwpBattleSupportDepletionPatch.After(sp,BattleSideEnum.Defender,ref depleted);
  Check(depleted,"native end condition restored when rescue troops are gone");
  foreach(var a in m.Spawned)a.Active=true;
  var own=(BasicBattleAgentOrigin)m.MainAgent!.Origin;var summoned=m.Spawned[0].Origin;
  summoned.SetKilled();summoned.SetWounded();summoned.SetRouted(false);summoned.OnAgentRemoved(0);
  Check(own.Mutations==0,"support loss never mutates source campaign roster");
  Check(m.Spawned.Select(a=>a.Origin.UniqueSeed).Distinct().Count()==m.Spawned.Count,"support origins have unique descriptors");
  support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,10,true,true);MBRandom.Rolls.Enqueue(0);MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  Check(m.Spawned.Any(a=>a.Team.Side==BattleSideEnum.Defender)&&m.Spawned.Any(a=>a.Team.Side==BattleSideEnum.Attacker),"both sides independently rescue");
  Check(MBRandom.Calls==2,"one successful roll per side");support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(1,100,false,false);Campaign.Current=new();Campaign.Current.Bounty.IsRecruitedByGreyWardens=true;PlayerBehaviorPool.Reputation=19;
  MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);Check(m.Spawned.Count==0&&MBRandom.Calls==0,"campaign player reputation gate retained");
  PlayerBehaviorPool.Reputation=20;support.OnAgentRemoved(new Agent{Team=m.PlayerTeam},m.MainAgent!,AgentState.Killed,new());
  for(int i=0;i<8;i++)support.OnMissionTick(1);
  Check(m.Spawned.Count>0&&m.Spawned.Sum(a=>a.CharacterPowerCached)<=20.0001f,"entry reputation player rescues with twenty percent budget");support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,100);Campaign.Current=new();Campaign.Current.Bounty.IsRecruitedByGreyWardens=true;PlayerBehaviorPool.Reputation=100;
  m.MainAgent!.Active=false;MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  for(int i=0;i<8;i++)support.OnMissionTick(1);
  Check(m.Spawned.Sum(a=>a.CharacterPowerCached)>48&&m.Spawned.Sum(a=>a.CharacterPowerCached)<=50.0001f,"fallen player keeps reputation budget when a Warden receives the rescue");
  Check(c.OpeningCount(BattleSideEnum.Defender)==100,"manual reinforcements do not inflate opening threshold denominator");support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,100,false,true);foreach(var a in m.Agents.Where(a=>a.Team.Side==BattleSideEnum.Attacker).Skip(20))a.Active=false;
  MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  Check(m.Spawned.Sum(a=>a.CharacterPowerCached)<=3.0001f&&m.Spawned.Sum(a=>a.CharacterPowerCached)>1,"NPC rescue uses thirty percent of opposite living force");support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,100);sp.NumberOfRemainingAttackerTroops=100;
  for(int i=0;i<100;i++)sp.Rosters[1].Add(new BasicBattleAgentOrigin(Troop(false,2)));
  c.TryCapture();Check(Math.Abs(c.RemainingPower(BattleSideEnum.Attacker)-300)<.001,"unspawned enemy reserves contribute exact roster power");
  var arriving=new Agent{Team=m.Agents.Last().Team,Character=Troop(false,2),Origin=new BasicBattleAgentOrigin(Troop(false,2))};m.Agents.Add(arriving);c.OnAgentBuild(arriving,new());sp.NumberOfRemainingAttackerTroops--;
  Check(Math.Abs(c.RemainingPower(BattleSideEnum.Attacker)-300)<.001,"moving a reserve into scene does not double count power");support.OnRemoveBehavior();

  (m,sp,c,support)=Battle(10,100);m.Add(new GreyWardenFieldSparringMissionController());support.OnMissionTick(1);
  Check(!c.Ready&&MBRandom.Calls==0,"friendly sparring excluded");support.OnRemoveBehavior();
  (m,sp,c,support)=Battle(10,100);m.MissionResult=new();support.OnMissionTick(1);
  Check(m.Spawned.Count==0,"no support after result");
  (m,sp,c,support)=Battle(10,100);DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=110;MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  Check(m.Spawned.Count==0&&support.HasPendingReinforcementSpawn(BattleSideEnum.Defender),"capacity defers rather than drops budget");
  DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=2000;support.OnMissionTick(1);
  Check(m.Spawned.Count>0,"deferred support resumes when capacity opens");support.OnRemoveBehavior();
  (m,sp,c,support)=Battle(10,100);m.FailSpawn=true;MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);support.OnMissionTick(1);
  Check(!support.HasPendingReinforcementSpawn(BattleSideEnum.Defender),"failed spawn attempts do not retry forever");support.OnRemoveBehavior();
  // The regression is a new recruit joining a formation already ordered to
  // retreat. Native calls Retreat immediately, without testing morale first.
  (m,sp,c,support)=Battle(10,100);MBRandom.Rolls.Enqueue(0);support.OnMissionTick(1);
  var reinforcement=m.Spawned.First();
  reinforcement.Formation=new Formation{Order=new MovementOrder{OrderEnum=MovementOrder.MovementOrderEnum.Retreat}};
  reinforcement.HumanAIComponent!.Value=HumanAIComponent.BehaviorValueSet.DefaultMove;
  Check(!GwpWardenRetreatPatch.Before(reinforcement)&&reinforcement.HumanAIComponent.Value==HumanAIComponent.BehaviorValueSet.Charge,
   "reinforcement joining an already retreating formation stays and fights before native escape flags are set");
  var retreatOrder=MovementOrder.MovementOrderEnum.Retreat;
  GwpWardenRetreatBehaviorPatch.Before(reinforcement,ref retreatOrder);
  Check(retreatOrder==MovementOrder.MovementOrderEnum.Charge,"subsequent native behavior refresh cannot overwrite combat response");
  var ordinary=m.Agents.First(a=>!GwpCommon.IsGreyWardenAffiliatedCharacter(a.Character));ordinary.Formation=reinforcement.Formation;
  Check(GwpWardenRetreatPatch.Before(ordinary),"ordinary soldier in the SAME formation may still retreat");
  Check(reinforcement.Formation.Order.OrderEnum==MovementOrder.MovementOrderEnum.Retreat,"shared formation order is untouched");
  Check(!GwpWardenPanicPatch.Before(reinforcement),"direct panic is blocked before broadcast to nearby troops");
  foreach(var side in new[]{BattleSideEnum.Attacker,BattleSideEnum.Defender})
  {
   var warden=new Agent{Mission=m,Team=new Team{Side=side},Character=Troop(true)};
   Check(!GwpWardenPanicPatch.Before(warden)&&!GwpWardenRetreatPatch.Before(warden),"starting wardens on either side keep fighting through panic and retreat");
  }
  Check(GwpWardenPanicPatch.Before(ordinary),"ordinary soldiers retain native panic");
  Check(typeof(GwpWardenResolve).Assembly.GetType("GreyWardenPolicePurity.GwpWardenMoralePatch")==null,"warden morale is native, not held at 100");
  foreach(var order in new[]{MovementOrder.MovementOrderEnum.Move,MovementOrder.MovementOrderEnum.FallBack,MovementOrder.MovementOrderEnum.Stop})
  {
   var copy=order;GwpWardenRetreatBehaviorPatch.Before(reinforcement,ref copy);Check(copy==order,"ordinary tactical movement is preserved");
  }
  reinforcement.IsHuman=false;Check(GwpWardenPanicPatch.Before(reinforcement),"riderless horse panic is unaffected");reinforcement.IsHuman=true;
  reinforcement.IsAIControlled=false;Check(GwpWardenRetreatPatch.Before(reinforcement),"human player control is unaffected");reinforcement.IsAIControlled=true;
  m.Mode=MissionMode.Conversation;Check(GwpWardenPanicPatch.Before(reinforcement),"nonbattle missions are unaffected");m.Mode=MissionMode.Battle;
  GameNetwork.IsMultiplayer=true;Check(GwpWardenPanicPatch.Before(reinforcement),"multiplayer is unaffected");GameNetwork.IsMultiplayer=false;
  m.MissionResult=new();Check(GwpWardenRetreatPatch.Before(reinforcement),"battle result releases protection for cleanup");m.MissionResult=null;
  m.MissionEnded=true;Check(GwpWardenPanicPatch.Before(reinforcement),"mission end releases protection");m.MissionEnded=false;
  support.OnRemoveBehavior();
  Console.WriteLine($"PASS {checks} battle-scene checks using production mission/context/origin/policy/resolve sources; native AI and spawn physics still require live validation.");
 }
}
