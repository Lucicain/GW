"""Production armed eligibility and hostile exit decisions, with old-code controls."""
import unittest
import subprocess
from pathlib import Path
from test_city_search import read, block
from test_duty_banner import method
from test_city_search_flow import run


class CitySearchHostilityTests(unittest.TestCase):
    def test_armed_defenders_and_physical_exit(self):
        mission = read('GwpCitySearchMission.cs')
        progress = read('GwpCitySearchMission.Progress.cs')
        bodies = '\n'.join(method(mission, n) for n in ['IsDefender', 'ShouldFlee', 'IsWatch'])
        bodies += '\n' + method(progress, 'CanLeaveSearch') + '\n' + method(progress, 'TargetMustStay')
        bodies += '\n' + method(progress, 'CivilianDanger')
        native = subprocess.check_output(['ilspycmd', '-t', 'SandBox.Missions.MissionLogics.MissionFightHandler', str(Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll'))], text=True, encoding='utf-8-sig')
        native_roles = '\n'.join(method(native, name) for name in ['IsAgentAggressive', 'IsAgentVillian', 'IsAgentJusticeWarrior'])
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
class Hero {public static Hero MainHero=new();public object MapFaction=new();}
class BasicCharacterObject{}
class CharacterObject:BasicCharacterObject {public int Occupation;public bool IsHero;public Hero? HeroObject;}
static class MissionFightHandler{NATIVE_ROLES}
class Vec {public float X;public float Distance(Vec v)=>Math.Abs(X-v.X);}
class Agent {public static Agent Main=new();public bool IsHuman=true,Armed,Watch,Ours,Active=true;public CharacterObject Character=new();public Vec Position=new();public bool HasWeapon()=>Armed;public bool IsActive()=>Active;}
class Mission {public List<Agent> Agents=new(){Agent.Main,new Agent{Ours=true},new Agent{Ours=true,Active=false},new Agent()};}
class Conversation {public bool IsConversationFlowActive;}
class Campaign {public static Campaign Current=new();public Conversation ConversationManager=new();}
class Settlement {public object MapFaction=new();}
class SearchState {public bool SearchWar,DuelAgreed;public bool SearchActive=true;}
static class GwpTuning {public static class CitySearch {public const float GateExitDistance=6;}}
enum Stage {Entering,Allowed,Fighting,FightOver,Duel,Failed,HallDone}
class Controller {
 public bool InHall,_transitioning;public bool IsCourtAppointment;public Agent? _hallTarget;public Settlement _settlement=new();public SearchState Search=new();
 public Stage _stage=Stage.Allowed;public string? _pendingFight,_pendingHallAction;public Vec _gate=new();public bool _gateExitAvailable=true;
 public Mission Mission=new();public int? DangerCount(Agent a)=>CivilianDanger(a)?.Count();
 bool IsOurs(Agent a)=>a.Ours||a==Agent.Main;
 public bool Defender(Agent a)=>IsDefender(a);public bool Flee(Agent a)=>ShouldFlee(a);public bool Stay(Agent a)=>TargetMustStay(a);public bool Leave()=>CanLeaveSearch();
 BODIES
}
static class Program {
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
 var c=new Controller();var guard=new Agent{Watch=true,Character=new(){Occupation=7}};
 foreach(int occupation in new[]{7,23,24}){
  var watch=new Agent{Character=new(){Occupation=occupation}};
  Check(MissionFightHandler.IsAgentAggressive(watch)&&!c.Defender(watch)&&c.Flee(watch),"unarmed native watch must enter flight despite aggressive occupation");
  Check(c.DangerCount(watch)==null,"peaceful unarmed watch has no forced danger source");c._stage=Stage.Fighting;
  Check(c.DangerCount(watch)==2,"unarmed watch receives player and active allies as flee danger despite native aggressive role");c._stage=Stage.Allowed;
  watch.Armed=true;Check(c.Defender(watch)&&!c.Flee(watch),"armed native watch fights instead of fleeing");
  watch.Ours=true;watch.Armed=false;Check(!c.Defender(watch)&&!c.Flee(watch),"unarmed allied watch is never forced to flee");
 }
 Check(!c.Defender(guard),"unarmed guard must flee");guard.Armed=true;Check(c.Defender(guard),"armed guard fights");
 var lord=new Agent{Character=new(){IsHero=true,HeroObject=new(){MapFaction=c._settlement.MapFaction}}};
 Check(!c.Defender(lord),"unarmed local lord must flee");lord.Armed=true;Check(c.Defender(lord),"armed local lord fights");
 c.InHall=true;c._hallTarget=new Agent{Character=new(){Occupation=7}};Check(c.Defender(c._hallTarget)&&!c.Flee(c._hallTarget),"unarmed hall target never becomes a fleeing civilian");
 c._hallTarget.Armed=true;Check(c.Defender(c._hallTarget),"armed foreign target fights");
 guard.Ours=true;Check(!c.Defender(guard),"armed ally never becomes defender");
 var armedGuest=new Agent{Armed=true};
 Check(!c.Defender(armedGuest)&&!c.Flee(armedGuest),"armed foreign guest keeps native behavior instead of forced flight");
 var thug=new Agent{Character=new(){Occupation=15}};
 Check(MissionFightHandler.IsAgentAggressive(thug)&&!c.Flee(thug),"native thug classification is respected even without visible weapon");
 Check(c.Flee(new Agent()),"ordinary unarmed bystander still flees");
 Check(c.Stay(c._hallTarget)&&!c.Stay(armedGuest),"target stay rule never traps another person");
 c.Search.DuelAgreed=true;Check(!c.Stay(c._hallTarget),"city duel appointment allows normal passage");
 c.Search.SearchWar=true;Check(c.Stay(c._hallTarget),"war overrides stale duel appointment");
 c.Search.SearchActive=false;Check(!c.Stay(c._hallTarget),"completed search does not block native movement");c.Search.SearchActive=true;c.Search.SearchWar=false;c.Search.DuelAgreed=false;
 c.InHall=false;Agent.Main.Position.X=100;Check(c.Leave(),"peaceful walking shortcut remains available");
 c.Search.SearchWar=true;c._stage=Stage.FightOver;Check(!c.Leave(),"hostile city blocks Tab even after wave defeated");
 c.InHall=true;Agent.Main.Position.X=0;Check(!c.Leave(),"hostile hall blocks Tab even beside its spawn");
 c.InHall=false;c._stage=Stage.Fighting;Check(c.Leave(),"actual outside exit permits retreat during fighting");
 Agent.Main.Position.X=6.01f;Check(!c.Leave(),"city exit boundary enforced");Agent.Main.Position.X=6;Check(c.Leave(),"outside boundary is inclusive");
 c._pendingFight="strike";Check(!c.Leave(),"queued hostility cannot be escaped by Tab");c._pendingFight=null;
 c._pendingHallAction="door";Check(!c.Leave(),"queued passage cannot race Tab");c._pendingHallAction=null;
 Campaign.Current.ConversationManager.IsConversationFlowActive=true;Check(!c.Leave(),"conversation blocks exit");
 Campaign.Current.ConversationManager.IsConversationFlowActive=false;c._stage=Stage.Duel;Check(!c.Leave(),"duel cannot be escaped");
 c._stage=Stage.Failed;Check(!c.Leave(),"defeat cannot award escape");
 Console.WriteLine("CITY_SEARCH_HOSTILITY_ASSERTIONS="+n);
 }
}
'''.replace('BODIES', bodies).replace('NATIVE_ROLES', native_roles)
        run(code)
        old_defender = bodies.replace('InHall && agent == _hallTarget\n            || agent.HasWeapon() && (IsWatch(agent)', 'agent.HasWeapon() && (IsWatch(agent)')
        with self.assertRaisesRegex(AssertionError, 'unarmed hall target'):
            run(code.replace(bodies, old_defender))
        old_crowd = bodies.replace(method(mission, 'ShouldFlee'), 'private bool ShouldFlee(Agent agent) => agent.IsHuman && !IsOurs(agent) && !IsDefender(agent);')
        with self.assertRaisesRegex(AssertionError, 'armed foreign guest'):
            run(code.replace(bodies, old_crowd))
        old_unarmed_watch = bodies.replace(method(mission, 'ShouldFlee'), '''private bool ShouldFlee(Agent agent) => agent.IsHuman && !IsOurs(agent) && !IsDefender(agent)
            && !MissionFightHandler.IsAgentAggressive(agent);''')
        with self.assertRaisesRegex(AssertionError, 'unarmed native watch must enter flight'):
            run(code.replace(bodies, old_unarmed_watch))
        old_tab = bodies.replace(method(progress, 'CanLeaveSearch'), '''private bool CanLeaveSearch() => _stage != Stage.Fighting && _stage != Stage.Duel
            && _stage != Stage.Failed && _pendingFight == null && _pendingHallAction == null
            && !Campaign.Current.ConversationManager.IsConversationFlowActive;''')
        with self.assertRaisesRegex(AssertionError, 'hostile city blocks Tab'):
            run(code.replace(bodies, old_tab))

    def test_target_stays_before_native_panic_and_retreat(self):
        progress = read('GwpCitySearchMission.Progress.cs')
        common = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.MountAndBlade.CommonAIComponent',
            'D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll'],
            text=True, encoding='utf-8-sig')
        patch = method(block(progress, r'internal static class GwpCitySearchTargetRetreatPatch'), 'Prefix')
        bodies = method(common, 'Panic').replace('void Panic(', 'void NativePanic(')
        bodies += method(common, 'Retreat').replace('void Retreat(', 'void NativeRetreat(')
        template = r'''
using System;
struct WorldPosition{public bool IsValid;public static WorldPosition Invalid=>new();}
class Vec{public Vec AsVec2=>this;}
class Cache{public WorldPosition GetRetreatPositionFromCache(Vec v)=>new(){IsValid=true};public void AddNewPositionToCache(Vec v,WorldPosition p){}}
class Formation{public Cache RetreatPositionCache=new();}
class Mission{public int Panics;public void OnAgentPanicked(Agent a){Panics++;}public WorldPosition GetClosestFleePositionForAgent(Agent a)=>new(){IsValid=true};}
class Agent{public enum AIStateFlag{Alarmed}public enum UsageDirection{None}public Mission Mission=new();public Vec Position=new();public Formation Formation=new();public bool Left;
 public void SetAlarmState(AIStateFlag f){}public void EnforceShieldUsage(UsageDirection d){}public void Retreat(WorldPosition p){Left=true;}}
class SearchState{public bool SearchActive=true,DuelAgreed,SearchWar;}
class GwpCitySearchMissionController{public static GwpCitySearchMissionController? Current;public bool InHall=true,_transitioning;public bool IsCourtAppointment;public Agent? _hallTarget;public SearchState Search=new();STAY}
class Gate{PREFIX public static bool Allows(Agent agent)=>Prefix(agent);}
class Common{public Agent Agent;public bool IsRetreating,IsPanicked;public Common(Agent agent){Agent=agent;}
 public void Panic(){if(Gate.Allows(Agent))NativePanic();}public void Retreat(){if(Gate.Allows(Agent))NativeRetreat();}
 NATIVE}
static class Program{static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}static void Main(){
 var target=new Agent();var controller=new GwpCitySearchMissionController{_hallTarget=target};GwpCitySearchMissionController.Current=controller;
 var ai=new Common(target);ai.Panic();ai.Retreat();Check(!ai.IsPanicked&&!ai.IsRetreating&&!target.Left&&target.Mission.Panics==0,"target is blocked before native panic and retreat flags or callbacks change");
 var other=new Agent();var bystander=new Common(other);bystander.Panic();bystander.Retreat();Check(bystander.IsPanicked&&bystander.IsRetreating&&other.Left&&other.Mission.Panics==1,"other native agents keep their original panic and retreat");
 controller.Search.DuelAgreed=true;Check(Gate.Allows(target),"peaceful city appointment is allowed");controller.Search.SearchWar=true;Check(!Gate.Allows(target),"war cancels the appointment exception");
 controller.Search.SearchActive=false;Check(Gate.Allows(target),"finished search does not intercept normal campaign agents");controller.Search.SearchActive=true;
 controller.InHall=false;Check(Gate.Allows(target),"scope is restricted to this hall mission");controller.InHall=true;
 controller._transitioning=true;Check(Gate.Allows(target),"mission cleanup and transition are allowed");GwpCitySearchMissionController.Current=null;Check(Gate.Allows(target),"all other missions keep native behavior");
 Console.WriteLine("CITY_SEARCH_TARGET_RETREAT_ASSERTIONS="+n);
}}
'''.replace('STAY', method(progress, 'TargetMustStay')).replace('PREFIX', patch).replace('NATIVE', bodies)
        run(template)
        with self.assertRaisesRegex(AssertionError, 'target is blocked before native'):
            run(template.replace(patch, 'private static bool Prefix(Agent ___Agent)=>true;'))


if __name__ == '__main__':
    unittest.main()
