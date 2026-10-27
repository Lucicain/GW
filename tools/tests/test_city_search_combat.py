"""Compare search combat transitions with installed native city-fight setup.

Runs production start/end methods and native watch-state setters. Native AI
movement/attacks are not simulated; this verifies the required AI state and
command handoff that the failed runtime log was missing.
"""
from pathlib import Path
import re
import subprocess
import unittest

from test_city_search import read, block
from test_city_search_flow import run
from test_duty_banner import method

GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CitySearchCombatTests(unittest.TestCase):
    def test_both_sides_enter_native_combat_state_and_follow_after_victory(self):
        native = subprocess.check_output(['ilspycmd', '-t', 'SandBox.Missions.MissionLogics.MissionFightHandler',
            str(GAME / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        agent = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.MountAndBlade.Agent',
            str(GAME / 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')], text=True, encoding='utf-8-sig')
        watch = block(agent, r'public WatchState CurrentWatchState')
        watch += method(agent, 'SetWatchState') + method(agent, 'SetAlarmState') + method(agent, 'IsAlarmed')
        native_setup = method(native, 'SetTeamsForFightAndDuel').replace('private ', 'public ').replace('(WatchState)', '(Agent.WatchState)')
        native_setup += method(native, 'ForceAgentForFight').replace('private ', 'public ')
        source = read('GwpCitySearchMission.cs')
        names = ['StartFight', 'EndFightIfDefendersDown']
        names += ['SetSearchFighter', 'RestoreSearchFollowers']
        bodies = '\n'.join(method(source, name) for name in names)
        bodies += method(read('GwpCitySearchMission.Reinforcements.cs'), 'SetCityPursuer')
        template = r'''
using System;using System.Collections.Generic;using System.Linq;
enum AIStateFlag{None=0,Cautious=1,PatrollingCautious=2,Alarmed=3}
enum FormationClass{Infantry,Ranged}
static class TroopClassExtensions{public static FormationClass DismountedClass(this FormationClass c)=>c;}
class CharacterObject{public FormationClass GetFormationClass()=>FormationClass.Infantry;}
class MovementOrder{public static MovementOrder MovementOrderCharge=new();}
class Formation{public bool IsAIControlled=true;public MovementOrder? Order;public void SetControlledByAI(bool b){IsAIControlled=b;}public void SetMovementOrder(MovementOrder o){Order=o;}}
static class MBAPI{public static class IMBAgent{public static void SetAIAlarmState(Agent a,AIStateFlag f){a.AIStateFlags=f;a.AlarmCalls++;}}}
namespace TaleWorlds.Library{static class Debug{public static void FailedAssert(params object[] o){throw new Exception("native assertion");}}}
class Agent{
 public enum WatchState{Patrolling,Cautious,Alarmed}
 public static Agent? Main;public bool IsHuman=true,IsAIControlled=true,Active=true;public Team Team=new();public int AlarmCalls;public AIStateFlag AIStateFlags;
 public CampaignAgentComponent Component=new();public T? GetComponent<T>() where T:class=>Component as T;
 public Agent GetPtr()=>this;public bool IsActive()=>Active;public void SetTeam(Team t,bool b){Team=t;}
 public bool Armed;public bool HasWeapon()=>Armed;
 public CharacterObject Character=new();public Formation? Formation;public bool AutoTarget;public void SetAutomaticTargetSelection(bool b){AutoTarget=b;}
 public void DisableScriptedMovement(){}
 WATCH
}
class Team{public static Team Invalid=new();public bool Enemy;public Formation Infantry=new();public Formation GetFormation(FormationClass c)=>Infantry;public void SetIsEnemyOf(Team other,bool b){Enemy=b;}}
class MobileParty{}
class CampaignAgentComponent{public AgentNavigator? AgentNavigator;}
class AgentNavigator{public AlarmedBehaviorGroup Group=new();public T? GetBehaviorGroup<T>() where T:class=>Group as T;public void RefreshBehaviorGroups(bool simulation){}}
class AlarmedBehaviorGroup{public bool DisableCalmDown,ScriptedFight,ScriptedFlee;public int Clears;
 public void AddBehavior<T>(){}public void SetScriptedBehavior<T>(){ScriptedFight=typeof(T)==typeof(FightBehavior);ScriptedFlee=typeof(T)==typeof(FleeBehavior);}
 public void DisableScriptedBehavior(){ScriptedFight=ScriptedFlee=false;Clears++;}}
class FightBehavior{}class FleeBehavior{}
enum Stage{Entering,Fighting,FightOver}enum MissionMode{StartUp,Battle}enum OrderType{FollowMe,Charge}
class MissionConversationLogic{public bool Disabled;public void DisableStartConversation(bool b){Disabled=b;}}
class Mission{public static Mission Current=new();public List<Agent> Agents=new();public Team PlayerTeam=new(),PlayerEnemyTeam=new();public MissionMode Mode;public MissionConversationLogic Talk=new();
 public void SetMissionMode(MissionMode mode,bool start){Mode=mode;}public T? GetMissionBehavior<T>() where T:class=>Talk as T;}
class SearchState{public bool War;public void MarkSearchWar(){War=true;}}
class Settlement{public object MapFaction=new();}
class PlayerBountyBehavior{public int Wars;public string Reason="";public void DeclarePlayerCaseWar(object f,string reason,string text){Wars++;Reason=reason;}}
class Campaign{public static Campaign Current=new();public PlayerBountyBehavior Bounty=new();public T? GetCampaignBehavior<T>() where T:class=>Bounty as T;}
static class GwpTextCatalog{public const string gwp_cs_war_notice="war",gwp_cs_unarmed_target_notice="unarmed";}
static class GwpText{public static string Get(string s)=>s;}class InformationMessage{public InformationMessage(string s){}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
class NativeFight{public List<Agent> _playerSideAgents=new(),_opponentSideAgents=new();NATIVE_SETUP}
class Controller{
 public Mission Mission=>Mission.Current;public SearchState Search=new();public Settlement _settlement=new();public bool _warDeclared;public Stage _stage;
 public bool InHall;public Agent? _hallTarget;
 public List<Agent> _defenders=new();public Dictionary<Agent,object> _ours=new();public HashSet<Agent> Defenders=new();public OrderType Last;public int Wins;
 bool IsOurs(Agent a)=>a==Agent.Main||_ours.ContainsKey(a);bool IsDefender(Agent a)=>Defenders.Contains(a)||InHall&&a==_hallTarget;bool ShouldFlee(Agent a)=>!IsOurs(a)&&!IsDefender(a)&&!a.Armed;
 void Order(OrderType o){Last=o;}void OnSearchFightWon(){Wins++;}
 MobileParty[] CityDefenseParties()=>Array.Empty<MobileParty>();void PrepareCityReserves(IEnumerable<MobileParty> p){}bool HasCityDefenders()=>false;
 BODIES
 public void Start(string cause)=>StartFight(cause);public void End()=>EndFightIfDefendersDown();
}
static class Program{
 static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static void Main(){
  // Native city fights arm BOTH lists. Selected SpawnTroop followers have a campaign component
  // without a navigator; town population agents have a navigator. Exercise both shapes.
  var nativeOurs=new Agent{Component=new(){AgentNavigator=new()}};var nativeEnemy=new Agent{Component=new(){AgentNavigator=new()}};var nativeFight=new NativeFight();nativeFight._playerSideAgents.Add(nativeOurs);nativeFight._opponentSideAgents.Add(nativeEnemy);nativeFight.ForceAgentForFight(nativeOurs);nativeFight.ForceAgentForFight(nativeEnemy);nativeFight.SetTeamsForFightAndDuel();
  Check(nativeOurs.IsAlarmed()&&nativeEnemy.IsAlarmed(),"installed native city fight requires both sides alarmed");
  Check(nativeOurs.Component.AgentNavigator!.Group.ScriptedFight&&nativeEnemy.Component.AgentNavigator!.Group.ScriptedFight,"installed native city fight activates both navigators");
  foreach(var cause in new[]{"chose_fight","hall_melee","escape"}){
   Mission.Current=new();Agent.Main=new(){IsAIControlled=false};Agent.Main.Team=Mission.Current.PlayerTeam;
   var ally=new Agent{Team=Mission.Current.PlayerTeam};var guided=new Agent{Team=Mission.Current.PlayerTeam,Component=new(){AgentNavigator=new()}};var dead=new Agent{Active=false,Team=Mission.Current.PlayerTeam};
   var guard=new Agent{Component=new(){AgentNavigator=new()}};var civilian=new Agent{Component=new(){AgentNavigator=new()}};
   Mission.Current.Agents.AddRange(new[]{Agent.Main,ally,guided,dead,guard,civilian});var c=new Controller();c._ours[ally]=new();c._ours[guided]=new();c._ours[dead]=new();c.Defenders.Add(guard);
   var team=ally.Team;var playerCalls=Agent.Main.AlarmCalls;var wars=Campaign.Current.Bounty.Wars;bool alreadyWar=cause=="escape";c._warDeclared=alreadyWar;c.Start(cause);
   Check(ally.IsAlarmed()&&guided.IsAlarmed(),"our followers must enter combat AI state: "+cause);
   Check(guard.IsAlarmed()&&guard.Component.AgentNavigator!.Group.ScriptedFight,"guard retains native fight behavior");
   Check(guard.Formation==Mission.Current.PlayerEnemyTeam.Infantry&&!guard.Formation.IsAIControlled&&guard.Formation.Order==MovementOrder.MovementOrderCharge&&guard.AutoTarget,"initial defenders actively charge the player force instead of just becoming alarmed");
   Check(ally.Formation==null&&guided.Formation==null,"enemy pursuit cannot replace the player's formation commands");
   Check(civilian.IsAlarmed()&&civilian.Component.AgentNavigator!.Group.ScriptedFlee&&!civilian.Component.AgentNavigator.Group.ScriptedFight,"civilian flees rather than joins fight");
   Check(guided.Component.AgentNavigator!.Group.ScriptedFight&&guided.Component.AgentNavigator.Group.DisableCalmDown,"existing follower navigator stays in fight until victory");
   Check(ally.Team==team&&guided.Team==team&&Mission.Current.PlayerEnemyTeam.Enemy,"fighting does not replace player formation teams");
   Check(c.Last==OrderType.Charge&&Mission.Current.Mode==MissionMode.Battle&&Mission.Current.Talk.Disabled,"combat mode and charge command still issued");
   Check(dead.AlarmCalls==0&&Agent.Main.AlarmCalls==playerCalls,"do not alter removed followers or player controller");
   Check(Campaign.Current.Bounty.Wars==wars+(alreadyWar?0:1)&&c.Search.War,"declaration goes through existing owner once");
   if(!alreadyWar)Check(Campaign.Current.Bounty.Reason=="city_search_"+cause,"declaration retains actual gate or hall cause");
   c.End();Check(c.Wins==0&&ally.IsAlarmed(),"living defenders keep fight active");guard.Active=false;c.End();
   Check(c.Last==OrderType.FollowMe&&Mission.Current.Mode==MissionMode.StartUp&&c.Wins==1,"victory restores follow order and advances once");
   Check(!ally.IsAlarmed()&&!guided.IsAlarmed()&&!guided.Component.AgentNavigator!.Group.ScriptedFight&&!guided.Component.AgentNavigator.Group.DisableCalmDown,"victory releases ally combat state for walking");
   c.End();Check(c.Wins==1,"victory cleanup is idempotent");
  }
  // No weapon is required for the actual arrest target to stay in the fight.
  Mission.Current=new();Agent.Main=new(){IsAIControlled=false};
  var target=new Agent{Component=new(){AgentNavigator=new()}};
  var observer=new Agent{Armed=true,Component=new(){AgentNavigator=new()}};
  Mission.Current.Agents.AddRange(new[]{Agent.Main,target,observer});
  var hall=new Controller{InHall=true,_hallTarget=target};hall.Start("hall_melee");
  Check(hall._defenders.Contains(target)&&target.Component.AgentNavigator!.Group.ScriptedFight&&!target.Component.AgentNavigator.Group.ScriptedFlee,"unarmed target gets actual fight behavior and never civilian flight");
  Check(!observer.Component.AgentNavigator!.Group.ScriptedFlee,"armed observer is not forced into civilian flight");
  hall.End();Check(hall.Wins==0,"living target holds hall combat open even after guards are gone");
  target.Active=false;hall.End();Check(hall.Wins==1,"target down allows hall combat to finish");
  Console.WriteLine("CITY_SEARCH_COMBAT_ASSERTIONS="+n);
 }
}
'''.replace('WATCH', watch).replace('NATIVE_SETUP', native_setup).replace('BODIES', bodies)
        run(template)
        old = re.sub(r'foreach \(Agent ally in _ours.Keys.Where\(agent => agent.IsActive\(\)\)\)\s*SetSearchFighter\(ally\);', '', bodies, count=1)
        with self.assertRaisesRegex(AssertionError, 'our followers must enter combat AI state'):
            run(template.replace(bodies, old))
        old_cleanup = bodies.replace('RestoreSearchFollowers();', '')
        with self.assertRaisesRegex(AssertionError, 'victory releases ally combat state'):
            run(template.replace(bodies, old_cleanup))
        old_pursuit = bodies.replace('SetCityPursuer(defender);', 'SetSearchFighter(defender);')
        with self.assertRaisesRegex(AssertionError, 'initial defenders actively charge'):
            run(template.replace(bodies, old_pursuit))


if __name__ == '__main__':
    unittest.main()
