"""Run production street snapshots and corpse reconstruction with explicit engine seams."""
import unittest
from test_city_search import read, block
from test_city_search_flow import run
from test_duty_banner import method


class StreetStateTests(unittest.TestCase):
    def test_partial_same_troop_health_and_exit_order(self):
        behavior = read('GwpCitySearchBehavior.Flow.cs')
        mission = read('GwpCitySearchMission.cs')
        bodies = block(behavior,r'internal void SaveEnemySurvivors') + method(behavior,'SavedEnemyHealth')
        bodies += method(mission,'OrderInitialSearchFollowers')
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
enum OrderType{Move,FollowMe,Charge}
struct WorldPosition{public float Gate;public WorldPosition(object scene,UIntPtr ptr,float gate,bool valid){Gate=gate;}}
class OrderController{public int Orders;public OrderType Last;public float Gate;public void SelectAllFormations(){}public void SetOrderWithPosition(OrderType t,WorldPosition p){Orders++;Last=t;Gate=p.Gate;}}
class Team{public OrderController PlayerOrderController=new();}class Mission{public Team PlayerTeam=new();public object Scene=new();}
class Controller{
 List<string> _enemyKeys=new();List<double> _enemyHealth=new();public bool EscapingCity,_gateExitAvailable=true;public float _gate=77;public Mission Mission=new();void Order(OrderType t){Mission.PlayerTeam.PlayerOrderController.Orders++;Mission.PlayerTeam.PlayerOrderController.Last=t;}
 BODIES
 public void Begin()=>OrderInitialSearchFollowers();public void Save(string[] keys,params (string,float)[] values)=>SaveEnemySurvivors(keys,values);public float HP(int n)=>SavedEnemyHealth("g|foot",n);
}
static class Program{static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}static void Main(){
 var c=new Controller();c.Save(new[]{"g|foot","g|foot","g|foot"},("g|foot",21f),("g|foot",22f),("g|foot",23f));c.Save(Array.Empty<string>());Check(c.HP(0)==21&&c.HP(2)==23,"empty hall preserves street wounds");
 c.Save(new[]{"g|foot"},("g|foot",9f));Check(c.HP(0)==9&&c.HP(1)==22&&c.HP(2)==23,"one hall guard cannot erase two same-key street survivors");
 c.Save(new[]{"g|foot"});Check(c.HP(0)==22&&c.HP(1)==23&&c.HP(2)==-1,"dead hall guard removes one observed slot only");
 c.Save(new[]{"g|foot","g|foot"});Check(c.HP(0)==-1,"full observed death clears all corresponding slots");
 var oc=c.Mission.PlayerTeam.PlayerOrderController;c.Begin();Check(oc.Last==OrderType.FollowMe,"normal entry follows player");c.EscapingCity=true;c.Begin();Check(oc.Last==OrderType.Move&&oc.Gate==77&&oc.Orders==2,"exit starts with native Move to actual city gate");c._gateExitAvailable=false;c.Begin();Check(oc.Last==OrderType.FollowMe,"missing actual gate cannot issue Move to origin");
 Console.WriteLine("STREET_HEALTH_ORDER_ASSERTIONS="+n);
}}
'''.replace('BODIES',bodies)
        run(code)
        old = bodies.replace('var replaced = observedKeys.GroupBy(key => key).ToDictionary(group => group.Key, group => group.Count());', 'var replaced = observedKeys.Distinct().ToDictionary(key=>key,key=>int.MaxValue);')
        with self.assertRaisesRegex(AssertionError,'one hall guard cannot erase'):
            run(code.replace(bodies,old))
        with self.assertRaisesRegex(AssertionError,'exit starts with native Move'):
            run(code.replace('if (!EscapingCity || !_gateExitAvailable)','if (true)'))

    def test_street_roundtrip_and_no_second_casualty(self):
        state = read('GwpCitySearchBehavior.StreetState.cs')
        controller = block(read('GwpCitySearchMission.StreetState.cs'), r'internal sealed partial class GwpCitySearchMissionController')
        code = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
namespace GreyWardenPolicePurity {
struct Vec2{public float x,y;public Vec2(float a,float b){x=a;y=b;}}
struct Vec3{public float x,y,z;public Vec3(float a,float b,float c){x=a;y=b;z=c;}public Vec2 AsVec2=>new(x,y);}
interface IDataStore{void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public Dictionary<string,string> Data=new();public bool Load;public void SyncData<T>(string key,ref T value){if(Load){if(Data.TryGetValue(key,out var data))value=JsonSerializer.Deserialize<T>(data)!;}else Data[key]=JsonSerializer.Serialize(value);}}
class CharacterObject{public string StringId="guard";public bool IsHero;public float GetStepSize()=>1;}
class LocationCharacter{public CharacterObject Character=new();}
class MBObjectManager{public static MBObjectManager Instance=new();public Dictionary<string,CharacterObject> Objects=new();public T GetObject<T>(string id)=>(T)(object)Objects[id];}
class Equipment{public string Code="gear";public string CalculateEquipmentCode()=>Code;public static Equipment CreateFromEquipmentCode(string s)=>new(){Code=s};}
struct BodyProperties{public string Code;public override string ToString()=>Code;public static bool FromString(string s,out BodyProperties b){b=new(){Code=s};return true;}}
struct ActionIndexCache{public int Index;public string Name;public string GetName()=>Name;public static ActionIndexCache Create(string s)=>new(){Name=s,Index=1};}
struct MBActionSet{public string Name;public bool IsValid=>Name!=null;public string GetName()=>Name;public static MBActionSet GetActionSet(string s)=>new(){Name=s};}
struct Animation{public MBActionSet Set;}
class Monster{public Animation FillAnimationSystemData(MBActionSet s,float step,bool hasClippingPlane)=>new(){Set=s};}
class Team{public static Team Invalid=new();}
class SimpleAgentOrigin{public SimpleAgentOrigin(CharacterObject c){}}
enum AgentState{Active,Killed,Unconscious,Routed,Deleted}
class AgentBuildData{
 public CharacterObject Character;public Equipment Gear=new();public BodyProperties Body;public int Seed;public uint C1,C2;public Vec3 Position;public Vec2 Direction;public bool NoHorse;public Team AssignedTeam=null!;
 public AgentBuildData(CharacterObject c){Character=c;}public AgentBuildData TroopOrigin(SimpleAgentOrigin o)=>this;
 public AgentBuildData Team(Team t){AssignedTeam=t;return this;}public AgentBuildData Equipment(Equipment e){Gear=e;return this;}public AgentBuildData EquipmentSeed(int n){Seed=n;return this;}
 public AgentBuildData BodyProperties(BodyProperties b){Body=b;return this;}public AgentBuildData ClothingColor1(uint n){C1=n;return this;}public AgentBuildData ClothingColor2(uint n){C2=n;return this;}
 public AgentBuildData NoHorses(bool b){NoHorse=b;return this;}public AgentBuildData CivilianEquipment(bool b)=>this;public AgentBuildData InitialPosition(Vec3 p){Position=p;return this;}public AgentBuildData InitialDirection(Vec2 d){Direction=d;return this;}
}
class Agent{
 public static Agent Main=new();public bool IsHuman=true,Deleted;public CharacterObject Character=new();public Equipment SpawnEquipment=new();public BodyProperties BodyPropertiesValue=new(){Code="face"};public int BodyPropertiesSeed=42;public uint ClothingColor1=0xFFFFFFFF,ClothingColor2=123;
 Vec3 _position;public Vec3 Position{get{if(Deleted)throw new Exception("read deleted native agent");return _position;}set{_position=value;}}
 public Vec3 LookDirection=new(0,1,0);public ActionIndexCache Action=ActionIndexCache.Create("death");public MBActionSet ActionSet=MBActionSet.GetActionSet("original_set");public Monster Monster=new();public int DeadCalls;public bool? Killed;public bool SetBeforeDead;
 public ActionIndexCache GetCurrentAction(int channel)=>Action;public void SetActionSet(ref Animation a){ActionSet=a.Set;SetBeforeDead=true;}
 public void MakeDead(bool killed,ActionIndexCache action){if(!SetBeforeDead)throw new Exception("corpse action set not restored");DeadCalls++;Killed=killed;Action=action;}
}
class Mission{public List<Agent> Agents=new(),AllAgents=new();public List<AgentBuildData> Builds=new();public Agent SpawnAgent(AgentBuildData b){Builds.Add(b);var a=new Agent{Character=b.Character};Agents.Add(a);AllAgents.Add(a);return a;}}
internal sealed partial class GwpCitySearchBehavior{public void Sync(IDataStore s)=>SyncStreetBodies(s);public void Clear()=>ClearStreetBodies();}
internal sealed partial class GwpCitySearchMissionController{public bool InHall;public GwpCitySearchBehavior Search=new();public Mission Mission=new();public void Prepare()=>PrepareStreetReturn();public void Restore()=>RestoreStreetBodies();public void Defeat(Agent a,AgentState s)=>NoteStreetDefeat(a,s);public void Save()=>SaveStreetScene();}
CONTROLLER
__STATE_SOURCE__
static class Program{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
  var c=new GwpCitySearchMissionController();var guard=new Agent{Position=new(7,8,9)};var wounded=new Agent{Position=new(3,4,5)};wounded.Character=new(){StringId="militia"};
  MBObjectManager.Instance.Objects["guard"]=guard.Character;MBObjectManager.Instance.Objects["militia"]=wounded.Character;
  c.Mission.Agents.Add(guard);c.Mission.AllAgents.Add(guard);c.Restore();c.Defeat(guard,AgentState.Killed);c.Defeat(wounded,AgentState.Unconscious);
  c.Defeat(new(){Character=new(){IsHero=true}},AgentState.Killed);c.Defeat(new(){IsHuman=false},AgentState.Killed);c.Defeat(new(),AgentState.Routed);
  guard.Position=new(10,11,12);guard.Action=ActionIndexCache.Create("final_death");wounded.Deleted=true;c.Save();
  var bodies=c.Search.StreetBodies.ToArray();Check(bodies.Length==2,"only nonhero human casualties are preserved");Check(bodies[0].Position.x==10&&bodies[0].Action=="final_death","final body position and death action captured");
  Check(bodies[1].Position.x==3,"already deleted body uses cached native values");Check(bodies[0].LocationActor&&!bodies[1].LocationActor,"location and reserve casualties remain distinct");
  c.Save();Check(c.Search.StreetBodies.Count()==2,"saving scene twice cannot duplicate bodies");
  var store=new Store();c.Search.Sync(store);store.Load=true;var saved=new GwpCitySearchBehavior();saved.Sync(store);bodies=saved.StreetBodies.ToArray();
  Check(bodies.Length==2&&bodies[0].Color1==0xFFFFFFFF&&bodies[0].Seed==42&&bodies[0].Equipment=="gear"&&bodies[0].Body=="face"&&bodies[0].ActionSet=="original_set","same-version save/load retains exact appearance values");
  var next=new GwpCitySearchMissionController{Search=saved};next.Prepare();Check(next.SuppressDefeatedStreetActor(new(){Character=guard.Character}),"dead location guard suppressed on return");Check(!next.SuppressDefeatedStreetActor(new(){Character=guard.Character}),"only exact casualty count suppressed");
  Check(!next.SuppressDefeatedStreetActor(new(){Character=wounded.Character}),"reserve casualty cannot suppress unrelated native location actor");next.Restore();
  Check(next.Mission.Builds.Count==2&&next.Mission.Builds.All(b=>b.AssignedTeam==Team.Invalid&&b.NoHorse),"street bodies rebuilt outside live combat teams");
  Check(next.Mission.Builds[0].Position.x==10&&next.Mission.Builds[0].Gear.Code=="gear"&&next.Mission.Builds[0].Seed==42,"corpse reconstruction uses original position equipment and seed");
  Check(next.Mission.Agents[0].Killed==true&&next.Mission.Agents[1].Killed==false&&next.Mission.Agents.All(a=>a.DeadCalls==1&&a.ActionSet.Name=="original_set"),"native MakeDead restores killed or unconscious state once with original action set");
  foreach(var a in next.Mission.Agents)next.Defeat(a,AgentState.Killed);next.Save();Check(saved.StreetBodies.Count()==2,"restored corpses cannot become new casualty records");
  var hall=new GwpCitySearchMissionController{InHall=true,Search=saved};hall.Prepare();hall.Restore();hall.Defeat(new(),AgentState.Killed);hall.Save();Check(hall.Mission.Builds.Count==0&&saved.StreetBodies.Count()==2,"empty hall cannot erase or rebuild street bodies");
  saved.Clear();Check(!saved.StreetBodies.Any(),"search completion clears scene snapshot");var old=new GwpCitySearchBehavior();old.Sync(new Store{Load=true});Check(!old.StreetBodies.Any(),"old saves missing new keys start with empty snapshot");
  Console.WriteLine("STREET_STATE_ASSERTIONS="+n);
 }
}}
'''.replace('CONTROLLER',controller).replace('__STATE_SOURCE__',state[state.index('namespace GreyWardenPolicePurity')+len('namespace GreyWardenPolicePurity'):].strip()[1:-1])
        run(code)
        with self.assertRaisesRegex(AssertionError, 'only nonhero human casualties'):
            run(code.replace('Search.SaveStreetBodies(Search.StreetBodies.Concat(_streetDefeatedBodies.Values).ToArray());',''))
        with self.assertRaisesRegex(AssertionError, 'dead location guard suppressed'):
            run(code.replace('if (InHall) return;\n            foreach (GwpCityStreetBody body', 'if (true) return;\n            foreach (GwpCityStreetBody body'))


if __name__ == '__main__':
    unittest.main()
