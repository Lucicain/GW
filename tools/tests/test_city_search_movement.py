"""Peaceful posting and agreed-duel native ScriptBehavior route, no engine navigation."""
import re
import subprocess
import unittest
from pathlib import Path
from test_city_search import read
from test_city_search_flow import run
from test_duty_banner import method


class CitySearchMovementTests(unittest.TestCase):
    def test_hall_posts_and_two_native_passage_targets(self):
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        native = subprocess.check_output(['ilspycmd', '-t', 'SandBox.Missions.AgentBehaviors.ScriptBehavior',
            str(game / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        add_target = method(native, 'AddUsableMachineTarget')
        source = read('GwpCitySearchMission.Movement.cs')
        production = source[source.index('namespace GreyWardenPolicePurity'):source.index('    [HarmonyPatch')]+ '}\n'
        code = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace GreyWardenPolicePurity{
struct Vec2{public float Angle;public float RotationInRadians=>Angle;}
struct Vec3{public float X;public Vec3(float x){X=x;}public Vec2 AsVec2=>new(){Angle=X};public float Distance(Vec3 p)=>Math.Abs(X-p.X);}
struct WorldPosition{public Vec3 Position;public WorldPosition(object scene,UIntPtr p,Vec3 pos,bool b){Position=pos;}}
class CharacterObject{public bool IsHero;}
class Hero{public CharacterObject CharacterObject=new(){IsHero=true};}
class Agent{public static Agent Main=new(){Position=new(0)};public bool Active=true,IsAIControlled=true,Watch,Scripted;public CharacterObject Character=new();public Vec3 Position,LookDirection;public float? ScriptedDirection;public CampaignAgentComponent Component=new();public bool IsActive()=>Active;
 public Frame Frame=>new(){rotation=new(){f=LookDirection}};
 public void TeleportToPosition(Vec3 p){Position=p;}public T GetComponent<T>()=>(T)(object)Component;public void SetScriptedPosition(ref WorldPosition p,bool b){Scripted=true;Position=p.Position;}public void SetScriptedPositionAndDirection(ref WorldPosition p,float direction,bool b){SetScriptedPosition(ref p,b);ScriptedDirection=direction;}public void DisableScriptedMovement(){Scripted=false;}}
class CampaignAgentComponent{public AgentNavigator AgentNavigator=new();}class AgentNavigator{public DailyBehaviorGroup Daily=new();public T GetBehaviorGroup<T>()=>(T)(object)Daily;}
class DailyBehaviorGroup{public ScriptBehavior? Behavior,ScriptedBehavior;public T? GetBehavior<T>() where T:class=>Behavior as T;public T AddBehavior<T>() where T:new(){Behavior=new ScriptBehavior();return (T)(object)Behavior;}public void SetScriptedBehavior<T>(){ScriptedBehavior=Behavior;}}
class ScriptBehavior{enum State{GoToUsableMachine}UsableMachine? _targetUsableMachine;State _state;bool _sentToTarget;public UsableMachine? Target=>_targetUsableMachine;NATIVE}
class UsableMachine{}class Location{public string StringId="";}class LocationCharacter{public CharacterObject Character=new();}
class Passage:UsableMachine{public Location? ToLocation;}
class MissionAgentHandler{public List<UsableMachine> TownPassageProps=new();}
class Rotation{public Vec3 f=new(3);}class Frame{public Vec3 origin;public Rotation rotation=new();}class Entity{public Frame GetGlobalFrame()=>new(){origin=new(20)};}
class Scene{public IEnumerable<Entity> FindEntitiesWithTag(string tag)=>new[]{new Entity()};}
class Mission{public List<Agent> Agents=new();public Scene Scene=new();public MissionAgentHandler Handler=new();public Queue<float> Positions=new();
 public Vec3 GetRandomPositionAroundPoint(Vec3 anchor,float min,float max)=>new(Positions.Dequeue());public T GetMissionBehavior<T>()=>(T)(object)Handler;}
enum OrderType{StandYourGround,FollowMe}class Formation{}class OrderController{}class MBReadOnlyList<T>{}
class SearchState{public bool SearchWar,DuelAgreed;}class Settlement{public bool IsCastle;}
static class GwpTuning{public static class CitySearch{public const int HallPostAttempts=12;public const float HallPostRadius=5,HallPostSpacing=1.5f,GuardChallengeDistance=6,HallPostPlayerClearance=2;}}
partial class GwpCitySearchMissionController{
 public Settlement _settlement=new();public int CourtPlans;void PrepareCastleDuel(){CourtPlans++;}public Mission Mission=new();public SearchState Search=new();public bool InHall=true;private Agent? _hallTarget;private Hero _target=new();private Dictionary<Agent,object> _ours=new();public int Orders;
 bool IsWatch(Agent a)=>a.Watch;void Order(OrderType t){Orders++;}
 public void Post()=>PostHallTroops();public void Release()=>ReleaseHallPosts(OrderType.FollowMe,new(),new());public void Walk(string to)=>BeginDuelWalk(to);public void Resume()=>ResumeDuelWalk();
 public void Setup(Agent target,params Agent[] ours){_hallTarget=target;_target.CharacterObject=target.Character;Mission.Agents.Add(target);foreach(var a in ours){_ours[a]=new();Mission.Agents.Add(a);}}
}
static class Program{static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){var c=new GwpCitySearchMissionController();var lord=new Agent{Position=new(100),Character=new(){IsHero=true}};Agent first=new(),second=new();c.Setup(lord,first,second);c.Mission.Agents.Add(new(){Watch=true,Position=new(20),LookDirection=new(1)});c.Mission.Agents.Add(new(){Watch=true,Position=new(40),LookDirection=new(2)});c.Mission.Positions.Enqueue(21);c.Mission.Positions.Enqueue(41);c.Post();
 Check(c.Orders==1&&first.Scripted&&second.Scripted,"peaceful hall posts actual followers instead of following into conversation");
 Check(first.Position.Distance(second.Position)>=1.5f&&first.Position.Distance(lord.Position)>6&&second.Position.Distance(lord.Position)>6,"posted soldiers stand separately and clear of lord conversation");
 Check(first.ScriptedDirection==1&&second.ScriptedDirection==2,"each posted soldier inherits its own guard facing instead of arbitrary arrival heading");
 Check(c.SuppressFollowerTalk(first)&&!c.SuppressFollowerTalk(lord),"only selected ordinary followers lose empty troop dialogue");
 c.Release();Check(!first.Scripted&&!second.Scripted,"player order releases hall posts");
 var door=new Passage{ToLocation=new(){StringId="center"}};var arena=new Passage{ToLocation=new(){StringId="arena"}};c.Mission.Handler.TownPassageProps.AddRange(new[]{door,arena});c.Walk("center");Check(lord.Component.AgentNavigator.Daily.ScriptedBehavior==null,"unagreed encounter cannot send target away");
 c.Search.DuelAgreed=true;c.Walk("center");Check(lord.Component.AgentNavigator.Daily.ScriptedBehavior!.Target==door,"hall stage uses installed normal scripted passage behavior to center");
 c.InHall=false;c.Walk("arena");Check(lord.Component.AgentNavigator.Daily.ScriptedBehavior!.Target==arena,"street stage uses same native passage behavior to arena");
 c.InHall=true;c.Resume();Check(lord.Component.AgentNavigator.Daily.ScriptedBehavior!.Target==door,"resuming paused hall restarts unfinished normal walk to exit");c.InHall=false;c.Mission.Positions.Enqueue(2);c.Resume();Check(lord.Position.X==2&&lord.Component.AgentNavigator.Daily.ScriptedBehavior!.Target==arena,"street resume emerges beside actual player hall doorway then walks to arena");
 Check(c.AllowsDuelArenaEntry(new(){Character=lord.Character},arena.ToLocation!)&&!c.AllowsDuelArenaEntry(new(){Character=first.Character},arena.ToLocation!)&&!c.AllowsDuelArenaEntry(new(){Character=lord.Character},door.ToLocation!),"arena AI access exception is target and destination scoped");
 c.Search.SearchWar=true;Check(!c.AllowsDuelArenaEntry(new(){Character=lord.Character},arena.ToLocation!),"war never grants peaceful duel arena access");
 var fallback=new GwpCitySearchMissionController();var third=new Agent();fallback.Setup(lord,third);fallback.Mission.Positions.Enqueue(21);fallback.Post();Check(third.ScriptedDirection==3,"empty hall guard list uses defender spawn frame facing");
 Console.WriteLine("CITY_SEARCH_MOVEMENT_ASSERTIONS="+n);
 }
}}
'''.replace('NATIVE', add_target) + production
        run(code)
        old = subprocess.check_output(['git', 'show', '8799100:GreyWardenPolicePurity/GwpCitySearchMission.Movement.cs'], text=True, encoding='utf-8-sig').replace('\r\n', '\n')
        old_production = (old[old.index('namespace GreyWardenPolicePurity'):old.index('    [HarmonyPatch')]+ '}\n').replace('GwpTuning.CitySearch.SecureTargetDistance', 'GwpTuning.CitySearch.HallPostPlayerClearance')
        with self.assertRaisesRegex(AssertionError, 'each posted soldier inherits its own guard facing'):
            run(code.replace(production, old_production))


if __name__ == '__main__':
    unittest.main()
