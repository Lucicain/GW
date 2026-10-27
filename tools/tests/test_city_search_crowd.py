"""Crowd census and removal diagnostics must remain read-only and safe after removal."""
import unittest
from test_city_search import read
from test_duty_banner import method
from test_city_search_flow import run


class CitySearchCrowdTests(unittest.TestCase):
    def test_census_keeps_armed_fighters_and_named_civilians_distinct(self):
        diagnostic = read('GwpCitySearchMission.CrowdDiagnostics.cs')
        mission = read('GwpCitySearchMission.cs')
        bodies = '\n'.join(method(diagnostic, n) for n in ['CrowdWeapons', 'CaptureCrowd', 'WriteCrowdSnapshot', 'WriteCrowdRemoval'])
        bodies += '\n' + method(mission, 'IsDefender') + method(mission, 'ShouldFlee') + method(mission, 'WriteCivilianFlight')
        bodies += '\n' + method(read('GwpCitySearchMission.VisualDiagnostics.cs'), 'WasSearchVisualRemoved')
        code = r'''
using System;using System.Collections.Generic;using System.Linq;
class Hero{public bool IsNotable;}
class CharacterObject{public string StringId="",Name="",Occupation="";public Hero? HeroObject;}
struct Vec3{public float X;public float Distance(Vec3 b)=>Math.Abs(X-b.X);public override string ToString()=>X.ToString();}
enum EquipmentIndex{NumAllWeaponSlots=5}enum AgentState{Deleted,Routed,Killed}
class Item{public string StringId="short_sword_t3";}struct Weapon{public Item? Item;public bool IsEmpty=>Item==null;}
class Equipment{public Weapon[] Slots=new Weapon[5];public Weapon this[int i]=>Slots[i];}
class FightBehavior{}class FleeBehavior{}class Passage{}
class Group{public object? ScriptedBehavior;}
class Navigator{public object? Behavior,TargetUsableMachine;public Group Group=new();public Group GetActiveBehaviorGroup()=>Group;public object? GetActiveBehavior()=>Behavior;}
class CampaignAgentComponent{public Navigator? AgentNavigator;}
class Agent{
 public int Index;public bool Active=true,IsHuman=true,IsAIControlled=true,Ours,Armed,NativeStateGone;public CharacterObject Data=new();public Vec3 Point;public Equipment Gear=new();public CampaignAgentComponent Component=new();
 void RequireActive(){if(!Active)throw new Exception("accessed removed agent native state");}
 public object Character{get{RequireActive();return Data;}}public Vec3 Position{get{RequireActive();return Point;}}
 public Equipment Equipment{get{RequireActive();return Gear;}}public string CurrentWatchState{get{RequireActive();return "Alarmed";}}
 public bool IsActive(){if(NativeStateGone)throw new Exception("accessed removed native state pointer");return Active;}public bool HasWeapon(){RequireActive();return Armed;}public T GetComponent<T>(){RequireActive();return (T)(object)Component;}
}
class Mission{public List<Agent> Agents=new();}
static class GwpAiDiagnostics{public static List<(string Stage,string Text)> Lines=new();public static void WriteCitySearch(string s,string d)=>Lines.Add((s,d));}
class Controller{
 public Mission Mission=new();public Agent? _hallTarget;string _place="Street";
 readonly List<(Agent Agent,Vec3 Start,string Identity,bool Flee)> _crowdAtFight=new();
 readonly Dictionary<Agent,string> _visualRemovalStates=new();
 bool IsOurs(Agent a)=>a.Ours;
 public void Capture()=>CaptureCrowd();public void Sample(string s)=>WriteCrowdSnapshot(s);public void Remove(Agent a){_visualRemovalStates[a]="Deleted";WriteCrowdRemoval(a,AgentState.Deleted);}public void Flight()=>WriteCivilianFlight();
 BODIES
}
static class Program{static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
static void Main(){
 var worker=new Agent{Index=1,Armed=true,Data=new(){StringId="shop_worker",Occupation="ShopWorker"}};worker.Gear.Slots[0]=new(){Item=new()};worker.Component.AgentNavigator=new(){Behavior=new FightBehavior()};
 var gang=new Agent{Index=2,Armed=true,Data=new(){StringId="gangster",Occupation="Gangster"}};
 var notable=new Agent{Index=3,Armed=true,Data=new(){StringId="named_merchant",HeroObject=new(){IsNotable=true}}};notable.Component.AgentNavigator=new(){Behavior=new FleeBehavior(),TargetUsableMachine=new Passage()};notable.Component.AgentNavigator.Group.ScriptedBehavior=notable.Component.AgentNavigator.Behavior;
 var civilian=new Agent{Index=4,Data=new(){StringId="townsman"}};
 var c=new Controller();c.Mission.Agents.AddRange(new[]{worker,gang,notable,civilian,new Agent{Ours=true},new Agent{IsHuman=false},new Agent{Active=false}});c.Capture();c.Sample("fight_start");
 Check(GwpAiDiagnostics.Lines.Count==4,"census includes armed defenders and civilians but excludes allies animals and inactive agents");
 Check(GwpAiDiagnostics.Lines.Any(x=>x.Text.Contains("index=1")&&x.Text.Contains("weapons=0:short_sword_t3")&&x.Text.Contains("initialDefender=True")),"civilian clothing cannot hide actual weapon and defender classification");
 Check(GwpAiDiagnostics.Lines.Any(x=>x.Text.Contains("index=3")&&x.Text.Contains("initialFlee=True")&&x.Text.Contains("scripted=FleeBehavior")&&x.Text.Contains("target=Passage")),"named civilian flight includes actual behavior and passage target");
 gang.Active=false;c.Remove(gang);gang.NativeStateGone=true;c.Sample("after_8s");
 Check(GwpAiDiagnostics.Lines.Any(x=>x.Stage=="CROWD_REMOVED"&&x.Text.Contains("index=2")&&x.Text.Contains("state=Deleted")),"gang disappearance is tracked by original identity and removal state");
 Check(GwpAiDiagnostics.Lines.Any(x=>x.Text.Contains("sample=after_8s")&&x.Text.Contains("index=2")&&x.Text.Contains("active=False")),"inactive snapshots never read removed native state");
 civilian.Point=new(){X=6};c.Flight();Check(GwpAiDiagnostics.Lines.Any(x=>x.Stage=="CIVILIAN_FLIGHT"&&x.Text=="civilians=2; movedOrGone=1"),"flight count excludes armed fighters and tracks only intended runners");
 c.Mission.Agents.Clear();c.Capture();int before=GwpAiDiagnostics.Lines.Count;c.Sample("new_fight");Check(GwpAiDiagnostics.Lines.Count==before,"next fight clears stale crowd identities");
 Console.WriteLine("CITY_SEARCH_CROWD_ASSERTIONS="+n);
}}
'''.replace('BODIES', bodies)
        run(code)
        old = code.replace('!WasSearchVisualRemoved(agent) && agent.IsActive()', 'agent.IsActive()')
        with self.assertRaisesRegex(AssertionError, 'removed native state pointer'):
            run(old)


if __name__ == '__main__':
    unittest.main()
