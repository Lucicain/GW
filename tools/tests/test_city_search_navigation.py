"""Run search combat handoff through native behavior scheduling and patrol use.

The installed managed behavior/usable/detachment methods execute below. Scene
objects, engine agent movement and animation callbacks are explicit seams.
"""
from pathlib import Path
import re
import subprocess
import unittest

from test_city_search import read, block
from test_city_search_flow import run
from test_duty_banner import method

GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CitySearchNavigationTests(unittest.TestCase):
    def test_native_patrol_stops_before_defender_joins_combat_formation(self):
        def native(name, sandbox=True):
            dll = (GAME / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll' if sandbox else
                   GAME / 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')
            return subprocess.check_output(['ilspycmd', '-t', name, str(dll)], text=True, encoding='utf-8-sig')

        navigator = native('SandBox.AgentNavigator')
        daily = native('SandBox.Missions.AgentBehaviors.DailyBehaviorGroup')
        patrol = native('SandBox.Missions.AgentBehaviors.PatrollingGuardBehavior')
        alarmed = native('SandBox.Missions.AgentBehaviors.AlarmedBehaviorGroup')
        group = native('SandBox.Missions.AgentBehaviors.AgentBehaviorGroup')
        behavior = native('SandBox.Missions.AgentBehaviors.AgentBehavior')
        usable = native('TaleWorlds.MountAndBlade.UsableMissionObject', False)
        detachment = native('TaleWorlds.MountAndBlade.DetachmentManager', False)
        production = method(read('GwpCitySearchMission.cs'), 'SetSearchFighter')
        production += method(read('GwpCitySearchMission.Reinforcements.cs'), 'SetCityPursuer')
        native_nav = '\n'.join(block(navigator, r'(?:public|private) void ' + n + r'\(')
            for n in ['RefreshBehaviorGroups', 'ActivateGroup', 'HandleBehaviorGroups',
                      'TickBehaviorGroups', 'SetTarget', 'ClearTarget'])
        native_daily = '\n'.join(method(daily, n) for n in ['Tick', 'Think', 'TickActiveBehaviors',
            'GetScore', 'SetCheckBehaviorTimer'])
        native_daily += block(daily, r'protected override void OnDeactivate\(')
        native_patrol = method(patrol, 'Tick') + method(patrol, 'GetAvailability')
        native_patrol += block(patrol, r'protected override void OnDeactivate\(')
        native_alarm = '\n'.join(method(alarmed, n) for n in ['Tick', 'TickActiveBehaviors', 'GetScore', 'AlarmAgent'])
        code = r'''
#pragma warning disable CS8625
using System;using System.Collections.Generic;using System.Linq;using AIScriptedFrameFlags=Agent.AIScriptedFrameFlags;using WatchState=Agent.WatchState;
enum AgentFlag{None}
static class MBRandom{public static float RandomFloat=>0.25f;}
static class Extensions{public static bool IsEmpty<T>(IEnumerable<T> values)=>!values.Any();public static bool HasAllFlags<T>(T a,T b)=>false;}
class Timer{public float ElapsedTime;public bool Check(float now)=>false;public void Reset(params float[] args){ElapsedTime=0;}public Timer(params object[] args){}}
class Vec3{public static Vec3 Zero=new();}class WorldPosition{public static WorldPosition Invalid=new();}
enum FormationClass{Infantry,Ranged}static class TroopClassExtensions{public static FormationClass DismountedClass(this FormationClass f)=>f;}
class CharacterObject{public FormationClass GetFormationClass()=>FormationClass.Infantry;}
class MovementOrder{public static MovementOrder MovementOrderCharge=new();}
class Formation{public Team Team;public bool AI=true;public MovementOrder? Order;public Formation(Team team){Team=team;}public void SetControlledByAI(bool b){AI=b;}public void SetMovementOrder(MovementOrder order){Order=order;}}
class Team{public DetachmentManager DetachmentManager=new();public Formation Infantry;public Team(){Infantry=new(this);}public Formation GetFormation(FormationClass c)=>Infantry;}
interface IDetachment{void AddAgent(Agent a,int slot,Agent.AIScriptedFrameFlags flags);void RemoveAgent(Agent a);}
class DetachmentData{public int MovingAgentCount;}
class DetachmentManager{public Dictionary<IDetachment,DetachmentData> _detachmentDataDictionary=new();ADD_MOVING}
class UsableMissionObject{public Agent? MovingAgent;public void AddMovingAgent(Agent a){MovingAgent=a;}public object GetTickRequirement()=>this;public void SetScriptComponentToTick(object o){}ON_MOVE}
class UsableMachine:IDetachment{
 public UsableMissionObject Point=new();public bool IsStandingPointAvailableForAgent(Agent a)=>true;public object CreateAIBehaviorObject()=>new();
 public void AddAgent(Agent a,int slot,Agent.AIScriptedFrameFlags flags){a.Detachment=this;Point.OnAIMoveToUse(a,this);}
 public void RemoveAgent(Agent a){if(a.Formation!=null&&Point.MovingAgent==a){a.Formation.Team.DetachmentManager._detachmentDataDictionary[this].MovingAgentCount--;}Point.MovingAgent=null;a.Detachment=null;}
}
class Agent{
 public enum AIScriptedFrameFlags{None}public bool IsAIControlled=true,Alarmed;public enum WatchState{Patrolling,Alarmed}
 public Formation? Formation;public IDetachment? Detachment;public CharacterObject Character=new();public bool AutoTarget;public int DisableCalls;
 public CampaignAgentComponent Component=new();public T? GetComponent<T>() where T:class=>Component as T;public bool IsActive()=>true;public bool IsAlarmed()=>Alarmed;public bool IsPatrollingCautious()=>false;public bool IsCautious()=>false;
 public AgentFlag GetAgentFlags()=>AgentFlag.None;public void DisableScriptedMovement(){DisableCalls++;}public void ClearTargetFrame(){}public void SetWatchState(WatchState s){Alarmed=s==WatchState.Alarmed;}public void SetAutomaticTargetSelection(bool b){AutoTarget=b;}
}
class CampaignAgentComponent{public AgentNavigator? AgentNavigator;}
class Mission{public bool AllowAiTicking=false;public float CurrentTime;public Team PlayerEnemyTeam=new();public MissionAgentHandler Handler=new();public T GetMissionBehavior<T>()=>(T)(object)Handler;}
class MissionAgentHandler{public UsableMachine Point=new();public int PatrolLookups;public UsableMachine FindUnusedPointWithTagForAgent(Agent a,string tag){PatrolLookups++;return Point;}public List<UsableMachine> GetAllUsablePointsWithTag(string s)=>new(){Point};}
enum NavigationState{NoTarget,UseMachine}
class AgentNavigator{
 public Agent OwnerAgent;public Mission Mission;public string SpecialTargetTag="sp_guard_patrol";public List<AgentBehaviorGroup> _behaviorGroups=new();public Timer _checkBehaviorGroupsTimer=new();
 public UsableMachine? TargetUsableMachine;public WorldPosition TargetPosition=WorldPosition.Invalid;public object? TargetEntity,_targetBehavior;public NavigationState _agentState;public bool _targetReached;
 public AgentNavigator(Agent a,Mission m){OwnerAgent=a;Mission=m;_behaviorGroups.Add(new DailyBehaviorGroup(this,m));_behaviorGroups.Add(new AlarmedBehaviorGroup(this,m));}
 public T? GetBehaviorGroup<T>() where T:class=>_behaviorGroups.OfType<T>().FirstOrDefault();
 public void Step(float elapsed){_checkBehaviorGroupsTimer.ElapsedTime=elapsed;HandleBehaviorGroups(false);TickBehaviorGroups(0.01f,false);}
 NATIVE_NAV
}
NATIVE_BEHAVIOR
NATIVE_GROUP
class DailyBehaviorGroup:AgentBehaviorGroup{
 public DailyBehaviorGroup(AgentNavigator nav,Mission mission):base(nav,mission){}
 NATIVE_DAILY
}
class PatrollingGuardBehavior:AgentBehavior{
 MissionAgentHandler _missionAgentHandler;UsableMachine? _target;
 public PatrollingGuardBehavior(AgentBehaviorGroup group):base(group){_missionAgentHandler=Mission.Handler;}
 public override string GetDebugInfo()=>"Guard patrol";
 NATIVE_PATROL
}
class FightBehavior:AgentBehavior{
 public int Ticks;public FightBehavior(AgentBehaviorGroup group):base(group){}public override void Tick(float dt,bool simulation){Ticks++;}public override string GetDebugInfo()=>"Fight";
}
class AlarmedBehaviorGroup:AgentBehaviorGroup{
 public bool DisableCalmDown;Timer _alarmedTimer=new(),_checkCalmDownTimer=new();
 public AlarmedBehaviorGroup(AgentNavigator nav,Mission mission):base(nav,mission){}
 void HandleMissiles(float dt){}void UpdateAgentAlarmState(float dt){}bool IsNearDanger()=>false;
 NATIVE_ALARM
}
class Controller{public Mission Mission=new();PRODUCTION public void Fight(Agent a)=>SetCityPursuer(a);}
static class Program{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
 foreach(bool existingTarget in new[]{false,true})foreach(float elapsed in new[]{0f,0.25f,0.99f,1.01f}){
  var controller=new Controller();var a=new Agent();var nav=new AgentNavigator(a,controller.Mission);a.Component.AgentNavigator=nav;
  var daily=nav.GetBehaviorGroup<DailyBehaviorGroup>()!;var alarm=nav.GetBehaviorGroup<AlarmedBehaviorGroup>()!;daily.AddBehavior<PatrollingGuardBehavior>();daily.IsActive=true;
  if(existingTarget)nav.Step(0f);
  Check(a.Formation==null,"ordinary town patrol has no battle formation");
  Check(!controller.Mission.PlayerEnemyTeam.DetachmentManager._detachmentDataDictionary.ContainsKey(controller.Mission.Handler.Point),"town patrol object is not registered as a battle detachment");
  int lookups=controller.Mission.Handler.PatrolLookups;
  controller.Fight(a);
  // Under the old code this executes daily patrol before the one-second group
  // refresh, reaching the exact native dictionary lookup in OnAIMoveToUse.
  nav.Step(elapsed);
  Check(!daily.IsActive&&alarm.IsActive,"combat handoff immediately replaces daily patrol");
  Check(nav.TargetUsableMachine==null&&a.Detachment==null,"preexisting patrol object is released before formation assignment");
  Check(controller.Mission.Handler.PatrolLookups==lookups,"no patrol target is reacquired after combat handoff");
  Check(alarm.ScriptedBehavior is FightBehavior&&((FightBehavior)alarm.ScriptedBehavior).Ticks==1,"native fight behavior executes on first combat tick");
  Check(a.Formation==controller.Mission.PlayerEnemyTeam.Infantry&&!a.Formation.AI&&a.Formation.Order==MovementOrder.MovementOrderCharge&&a.AutoTarget,"pursuit formation and charge remain intact");
  controller.Fight(a);nav.Step(0f);Check(((FightBehavior)alarm.ScriptedBehavior).Ticks==2,"reapplying combat handoff is safe");
 }
 var raw=new Agent();new Controller().Fight(raw);Check(raw.Alarmed&&raw.Formation!=null&&raw.AutoTarget,"reserve without a town navigator still charges");
 var player=new Agent{IsAIControlled=false};new Controller().Fight(player);Check(!player.Alarmed,"combat helper leaves player watch state alone");
 Console.WriteLine("CITY_SEARCH_NAVIGATION_ASSERTIONS="+n);
 }
}
'''
        code = code.replace('NATIVE_NAV', native_nav).replace('NATIVE_DAILY', native_daily)
        code = code.replace('NATIVE_PATROL', native_patrol).replace('NATIVE_ALARM', native_alarm)
        code = code.replace('NATIVE_BEHAVIOR', block(behavior, r'public abstract class AgentBehavior\b').replace('public abstract class', 'abstract class'))
        code = code.replace('NATIVE_GROUP', block(group, r'public abstract class AgentBehaviorGroup\b').replace('public abstract class', 'abstract class'))
        code = code.replace('ADD_MOVING', method(detachment, 'AddAgentAsMovingToDetachment'))
        code = code.replace('ON_MOVE', method(usable, 'OnAIMoveToUse')).replace('PRODUCTION', production)
        run(code)
        old = re.sub(r'\s*\w+!?\.RefreshBehaviorGroups\(false\);', '', production)
        self.assertNotEqual(old, production, 'handoff must use the native immediate group refresh')
        with self.assertRaisesRegex(AssertionError, 'given key.*not present in the dictionary'):
            run(code.replace(production, old))
        late = old.replace('agent.Formation = formation;',
                           'agent.Formation = formation;agent.Component.AgentNavigator?.RefreshBehaviorGroups(false);')
        with self.assertRaisesRegex(AssertionError, 'given key.*not present in the dictionary'):
            run(code.replace(production, late))


if __name__ == '__main__':
    unittest.main()
