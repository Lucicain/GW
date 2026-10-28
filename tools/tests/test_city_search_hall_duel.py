"""Production hall-duel flow, with native movement/spawning as explicit seams."""
import re
import unittest
from pathlib import Path

from test_city_search import read, block, member
from test_city_search_flow import run
from test_duty_banner import method

STUBS = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace GreyWardenPolicePurity{
struct Vec2{public float RotationInRadians=>0;}
struct Vec3{public float x,y,z;public Vec3(float a,float b=0,float c=0){x=a;y=b;z=c;}public Vec2 AsVec2=>new();public float Distance(Vec3 p)=>Math.Abs(x-p.x);public static Vec3 operator-(Vec3 a,Vec3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
struct WorldPosition{public Vec3 Position;public Vec3 GetGroundVec3()=>Position;}
struct Rotation{public Vec3 f;}struct WorldFrame{public WorldPosition Origin;public Rotation Rotation;}
enum EquipmentIndex{WeaponItemBeginSlot=0,Weapon0=0,Weapon1,Weapon2,Weapon3,NumAllWeaponSlots=4}
class Weapon{public bool IsMeleeWeapon;}class ItemObject{public string Id="";public Weapon? PrimaryWeapon;}
struct EquipmentElement{public ItemObject Item;public bool IsEmpty=>Item==null;public EquipmentElement(ItemObject i){Item=i;}}
class Equipment{public EquipmentElement[] Items=new EquipmentElement[4];public Equipment(){}public Equipment(Equipment e){Array.Copy(e.Items,Items,4);}public EquipmentElement this[EquipmentIndex i]{get=>Items[(int)i];set=>Items[(int)i]=value;}}
class CharacterObject{public string Id="";public Equipment FirstBattleEquipment=new();}
class Hero{public string StringId="target";public CharacterObject CharacterObject=new(){Id="target"};}
class AgentData{public object Origin=new();public CharacterObject Character=new();public Equipment AgentOverridenEquipment=null!;public bool AgentCivilianEquipment=true;
 public AgentData Equipment(Equipment e){AgentOverridenEquipment=e;return this;}public AgentData CivilianEquipment(bool b){AgentCivilianEquipment=b;return this;}}
class AgentBuildData{public AgentData AgentData;public AgentBuildData(AgentData d){AgentData=d;}public CharacterObject AgentCharacter=>AgentData.Character;public AgentBuildData Equipment(Equipment e){AgentData.Equipment(e);return this;}public AgentBuildData CivilianEquipment(bool b){AgentData.CivilianEquipment(b);return this;}}
class LocationCharacter{public AgentData AgentData=new();public bool UseCivilianEquipment=true;}
class Location{public string StringId="";public List<LocationCharacter> Characters=new();public LocationCharacter? GetLocationCharacter(object origin)=>Characters.FirstOrDefault(c=>c.AgentData.Origin==origin);public void RemoveLocationCharacter(LocationCharacter c){Characters.Remove(c);}public void AddCharacter(LocationCharacter c){Characters.Add(c);}public void OnAIChangeLocation(Location? from){}}
class LocationComplex{public Location Hall=new(){StringId="lordshall"},Center=new(){StringId="center"};CHANGE_LOCATION}
class Settlement{public LocationComplex LocationComplex=new();}
class CampaignMission{public static CampaignMission Current=new();public Location Location=null!;public void OnCharacterLocationChanged(LocationCharacter c,Location from,Location to){Mission.Current.Handler.SpawnEnteringLocationCharacter(c,from);}}
class LocationEncounter{LOCATION_NOTIFICATION}
static class PlayerEncounter{public static LocationEncounter LocationEncounter=new();}
class Team{public static Team Invalid=new();public bool Hostile;public void SetIsEnemyOf(Team other,bool value){Hostile=value;}}
enum AgentState{Routed,Killed,Unconscious,Deleted}enum MissionMode{StartUp,Duel}enum OrderType{StandYourGround,FollowMe}
class Formation{}class MBReadOnlyList<T>:List<T>{}class OrderController{public List<Formation> SelectedFormations=new();}
static class GwpAiDiagnostics{public static void WriteCitySearch(string kind,string details){}}
enum Stage{Entering,Questioned,Allowed,Leaving,Refused,Withdrawing,Fighting,FightOver,HallReady,HallTalking,Duel,Escaping,Failed,HallDone}
class Agent{
 public enum AIScriptedFrameFlags{None}public static Agent Main=null!;public object Origin=new();public CharacterObject Character=new();public Vec3 Position;public WorldPosition Goal;public Team Team=new();public float Health=100,HealthLimit=100;public bool Active=true,NativeGone,IsHuman=true,Scripted,Fights;public Equipment SpawnEquipment=new();public Agent? FollowTarget;public CampaignAgentComponent? Component;
 public int FadeRequests;public bool FadePending;public int AIStateFlags=>0;public bool IsAlarmed()=>false;
 public bool IsActive(){if(NativeGone)throw new Exception("removed native state was read");return Active;}
 public T? GetComponent<T>() where T:class=>Component as T;public WorldPosition GetWorldPosition()=>new(){Position=Position};
 public void SetTeam(Team t,bool b){Team=t;}public void SetScriptedPositionAndDirection(ref WorldPosition p,float r,bool b){Goal=p;Scripted=true;}public void DisableScriptedMovement(){Scripted=false;}
 public void FadeOut(bool a,bool b){if(Position.Distance(new(10))>1.2f)throw new Exception("actor disappeared before reaching the main door");FadeRequests++;FadePending=true;if(!Mission.Current.DelayRemoval)CompleteFade();}
 public void CompleteFade(){if(!FadePending)throw new Exception("unexpected removal");FadePending=false;Active=false;Mission.Current.Agents.Remove(this);Mission.Current.Controller.Removed(this,AgentState.Routed);NativeGone=true;}
}
class StandingPoint{public WorldFrame GetUserFrameForAgent(Agent a)=>new(){Origin=new(){Position=new(10)}};}
class Passage{public Location? ToLocation;public StandingPoint PilotStandingPoint=new();}
class PassageUsePoint{public string ToLocationId="center";}
class MissionAgentHandler{public List<Passage> TownPassageProps=new();public int Spawns;public Location? EnteredFrom;public bool EquippedBeforeVisible;
 public void SpawnEnteringLocationCharacter(LocationCharacter c,Location from){
  Spawns++;EnteredFrom=from;var data=new AgentBuildData(c.AgentData).CivilianEquipment(c.UseCivilianEquipment);
  var other=new AgentBuildData(new AgentData());Mission.Current.Controller.PrepareHallDuelSpawn(other);if(!other.AgentData.AgentCivilianEquipment||other.AgentData.AgentOverridenEquipment!=null)throw new Exception("reentry override leaked to another character");
  Mission.Current.Controller.PrepareHallDuelSpawn(data);EquippedBeforeVisible=!data.AgentData.AgentCivilianEquipment&&data.AgentData.AgentOverridenEquipment!=null&&data.AgentData.AgentOverridenEquipment[EquipmentIndex.Weapon0].Item.PrimaryWeapon!.IsMeleeWeapon;
  if(Mission.Current.Agents.Any(a=>a.Character==c.AgentData.Character))throw new Exception("target was duplicated before the old actor left");
  var a=new Agent{Character=c.AgentData.Character,Origin=c.AgentData.Origin,Position=new(10),SpawnEquipment=data.AgentData.AgentOverridenEquipment};a.Component=new(a);Mission.Current.Agents.Add(a);
 }}
class MissionConversationLogic{public bool Disabled;public int Talks;public void DisableStartConversation(bool b){Disabled=b;}public void StartConversation(Agent a,bool b){Talks++;Campaign.Current.ConversationManager.OneToOneConversationAgent=a;Campaign.Current.ConversationManager.IsConversationFlowActive=true;}}
class Conversation{public Agent? OneToOneConversationAgent;public bool IsConversationFlowActive;}
class Campaign{public static Campaign Current=new();public Conversation ConversationManager=new();}
class Mission{public static Mission Current=new();public GwpCitySearchMissionController Controller=null!;public List<Agent> Agents=new();public MissionAgentHandler Handler=new();public MissionConversationLogic Talk=new();public Team PlayerEnemyTeam=new(),PlayerTeam=new();public float CurrentTime;public MissionMode Mode;
 public bool DelayRemoval;public T GetMissionBehavior<T>()=>(T)(object)(typeof(T)==typeof(MissionAgentHandler)?Handler:Talk);public void SetMissionMode(MissionMode mode,bool b){Mode=mode;}}
class SearchState{public bool SearchActive=true,DuelAgreed,UsesHallDuel=true,SearchWar,Returning,HallAccessible=true,HallResolved;public bool HallDuelLocked=>SearchActive&&DuelAgreed&&UsesHallDuel&&!SearchWar&&!Returning;public void WonHall(){DuelAgreed=false;Returning=true;}}
class MBObjectManager{public static MBObjectManager Instance=new();public T GetObject<T>(string id)=>(T)(object)new ItemObject{Id=id,PrimaryWeapon=new(){IsMeleeWeapon=true}};}
class CampaignAgentComponent{public AgentNavigator AgentNavigator;public CampaignAgentComponent(Agent a){AgentNavigator=new(a);}}
class AgentNavigator{public Agent Owner;public DailyBehaviorGroup Daily;public AgentNavigator(Agent a){Owner=a;Daily=new(this);}public T GetBehaviorGroup<T>()=>(T)(object)Daily;public void ClearTarget(){Owner.Scripted=false;}
 public void SetTargetFrame(WorldPosition p,float r,float range,float angle,Agent.AIScriptedFrameFlags flags,bool disableClearTargetWhenTargetIsReached){Owner.Goal=p;Owner.Scripted=true;}}
class AgentBehaviorGroup{public AgentNavigator Navigator;public AgentBehaviorGroup(AgentNavigator n){Navigator=n;}}
class AgentBehavior{protected AgentNavigator Navigator;public AgentBehavior(AgentBehaviorGroup g){Navigator=g.Navigator;}public virtual float GetAvailability(bool s)=>0;public virtual void Tick(float dt,bool s){}public virtual string GetDebugInfo()=>"";}
class DailyBehaviorGroup:AgentBehaviorGroup{Dictionary<Type,AgentBehavior> Behaviors=new();public DailyBehaviorGroup(AgentNavigator n):base(n){}
 public T? GetBehavior<T>() where T:class=>Behaviors.TryGetValue(typeof(T),out var b)?b as T:null;public T AddBehavior<T>() where T:class{var b=(AgentBehavior)Activator.CreateInstance(typeof(T),this)!;Behaviors[typeof(T)]=b;return (T)(object)b;}
 public void SetScriptedBehavior<T>(){Behaviors[typeof(T)].Tick(0,false);}public bool HasBehavior<T>()=>Behaviors.ContainsKey(typeof(T));public void RemoveBehavior<T>(){Behaviors.Remove(typeof(T));}}
static class ScriptBehavior{public static void AddAgentTarget(Agent a,Agent target){a.FollowTarget=target;}}
static class GwpTuning{internal static class CitySearch{public const float HallDuelDoorDistance=1.2f,HallDuelChangeSeconds=3f,HallDuelTalkDistance=3f,GateExitDistance=6f;}}
partial class GwpCitySearchMissionController{
 public Settlement _settlement=new();public Hero _target=new();public Dictionary<Agent,CharacterObject> _ours=new();public List<Agent> _hallPosts=new();public bool InHall=true;public SearchState Search=new();public Mission Mission=>Mission.Current;public Stage _stage=Stage.HallDone;public Agent? _hallTarget;public string? _pendingHallAction,_pendingFight;bool _gateExitAvailable;Vec3 _gate;
 void Order(OrderType order){}
 void DisablePlayerPassages(){}void SetSearchFighter(Agent a){a.Fights=true;a.DisableScriptedMovement();}
 public string Phase=>_hallDuelPhase.ToString();public Agent Target=>_hallTarget!;public List<Agent> Departures=>_hallDepartures.Keys.ToList();public int HealthySnapshots()=>SearchSurvivors().Count();public float SnapshotHealth()=>SearchSurvivors().Single().Health;
 public void Begin(){Search.DuelAgreed=true;BeginHallDuel();}public bool Tick(){Mission.CurrentTime+=.5f;return TickHallDuel();}public void Removed(Agent a,AgentState state){NoteHallDuelRemoval(a,state);}
 public bool Leave()=>CanLeaveSearch();public bool Door()=>CanUsePassage(new());public void Confirm(){ChooseHallDuelStart();if(_pendingHallAction=="hall_duel_start"){_pendingHallAction=null;StartHallDuel();}}public void Win(){FinishHallDuel();Search.WonHall();_stage=Stage.HallDone;}
 EXIT_RULES
}
CONTROLLER
POSITION_BEHAVIOR
static class Program{
static int n;static void Check(bool value,string why){n++;if(!value)throw new Exception(why);}
static Agent Actor(GwpCitySearchMissionController c,string name,bool campaign){var a=new Agent{Character=new(){Id=name},Position=new(0)};if(campaign){var ch=new LocationCharacter();ch.AgentData.Character=a.Character;ch.AgentData.Origin=a.Origin;c._settlement.LocationComplex.Hall.AddCharacter(ch);a.Component=new(a);}c.Mission.Agents.Add(a);return a;}
static void Main(){RunScenario(false);RunScenario(true);Console.WriteLine("HALL_DUEL_FLOW_ASSERTIONS="+n);}
static void RunScenario(bool delay){
 Mission.Current=new();Campaign.Current=new();var c=new GwpCitySearchMissionController();c.Mission.Controller=c;CampaignMission.Current.Location=c._settlement.LocationComplex.Hall;
 c.Mission.DelayRemoval=delay;
 c.Mission.Handler.TownPassageProps.Add(new(){ToLocation=c._settlement.LocationComplex.Center});Agent.Main=new(){Position=new(0)};c.Mission.Agents.Add(Agent.Main);
 var target=Actor(c,"target",true);c._hallTarget=target;c._target.CharacterObject=target.Character;target.Health=73;
 var guard=Actor(c,"guard",true);var follower=Actor(c,"follower",false);follower.Health=41;c._ours[follower]=follower.Character;
 c.Begin();Check(c.Phase=="Clearing"&&c.Departures.Count==2,"everyone except player and target is sent to the main door first");Check(target.Active&&!target.Scripted&&c.Mission.Handler.Spawns==0,"target stays while everyone else leaves; no clone or equipment refresh");
 Check(!c.Leave()&&!c.Door(),"accepting the hall duel locks both Tab and player doors");
 c.Tick();Check(guard.Active&&follower.Active,"actors walk to the door instead of vanishing from their seats");
 foreach(var a in c.Departures)a.Position=new(10);c.Tick();
 if(delay){Check(c.Phase=="Clearing"&&target.Active&&!target.Scripted,"exit request must wait for actual removal before ordering target out");c.Tick();Check(guard.FadeRequests==1&&follower.FadeRequests==1,"pending removals must not issue FadeOut twice");guard.CompleteFade();c.Tick();Check(c.Phase=="Clearing","one person still present prevents target exit");follower.CompleteFade();c.Tick();}
 Check(guard.NativeGone&&follower.NativeGone&&c.Phase=="Leaving"&&target.Active,"target is ordered out only after everyone else is gone");
 Check(c.HealthySnapshots()==1&&c.SnapshotHealth()==41,"departed followers keep their roster and health without native reads");
 target.Position=new(10);c.Tick();if(delay){for(int i=0;i<8;i++)c.Tick();Check(c.Phase=="Leaving"&&target.FadeRequests==1&&c.Mission.Handler.Spawns==0,"target reentry waits for its actual removal callback");target.CompleteFade();}
 Check(target.NativeGone&&c.Phase=="Changing"&&!c.Leave(),"target actually leaves through the door and player remains inside");
 Check(c.Mission.Handler.Spawns==0,"replacement is not spawned during the exit callback");
 c.Tick();Check(c.Mission.Handler.Spawns==0,"offscreen equipment interval is retained");
 var late=Actor(c,"late",true);for(int i=0;i<8;i++)c.Tick();Check(c.Mission.Handler.Spawns==0&&late.Active,"delayed NPCs also leave before the target returns");
 late.Position=new(10);c.Tick();if(delay){Check(c.Phase=="Changing"&&c.Mission.Handler.Spawns==0,"late exit request does not count as empty hall");late.CompleteFade();}while(c.Phase=="Changing")c.Tick();Check(late.NativeGone,"late arrival exited at the main door");
 Check(c.Mission.Handler.Spawns==1&&c.Mission.Handler.EnteredFrom==c._settlement.LocationComplex.Center,"explicit native entrance call returns the same target through the main door once");
 Check(c.Mission.Handler.EquippedBeforeVisible&&c.Target.Character==target.Character,"battle gear and a melee weapon are set before reentry becomes visible");
 Check(c.Target.Health==73&&c.Target.FollowTarget==Agent.Main,"return keeps health and walks back toward the player");
 var ch=c._settlement.LocationComplex.Hall.GetLocationCharacter(c.Target.Origin)!;
 Check(ch.AgentData.AgentOverridenEquipment==null&&ch.AgentData.AgentCivilianEquipment,"temporary battle gear does not persist on shared location AgentData");
 Check(c._target.CharacterObject.FirstBattleEquipment[EquipmentIndex.Weapon0].IsEmpty,"practice sword does not mutate hero equipment or inventory");
 Check(c.BlocksHallDuelEntry(new(),c._settlement.LocationComplex.Hall),"new bystanders cannot enter before the duel ends");
 c.Target.Position=new(1);c.Tick();Check(c.Phase=="Ready"&&c.Mission.Talk.Talks==1&&!c.Target.Fights,"returning target opens dialogue, not automatic combat");
 Campaign.Current.ConversationManager.IsConversationFlowActive=false;c.Tick();Check(c.Mission.Talk.Talks==1,"choosing to wait does not reopen dialogue every tick");
 Check(c.IsHallDuelTalk&&!c.Leave(),"the target remains available for the start dialogue while leaving is locked");
 c.Confirm();Check(c.Phase=="Fighting"&&c.Target.Fights&&c.Mission.Mode==MissionMode.Duel,"player dialogue confirmation starts the two-person duel");
 Check(c.Mission.Agents.Count==2&&!c.Leave(),"nobody else remains and player cannot leave the active duel");
 c.Win();Check(c.Leave()&&c.Door()&&!c.BlocksHallDuelEntry(new(),c._settlement.LocationComplex.Hall),"victory unlocks doors, Tab, and normal NPC entry");
 Check(c.HealthySnapshots()==1&&c.SnapshotHealth()==41,"duel cleanup preserves departed followers until scene snapshot");
}}}
'''


class HallDuelTests(unittest.TestCase):
    def test_production_hall_departure_reentry_equipment_dialogue_and_exit_lock(self):
        source = read('GwpCitySearchMission.HallDuel.cs')
        controller = block(source, r'internal sealed partial class GwpCitySearchMissionController')
        position = block(source, r'internal sealed class GwpHallDuelPositionBehavior')
        progress = read('GwpCitySearchMission.Progress.cs')
        location = (Path(__file__).resolve().parents[2] / '.codex_tmp/campaignsystem-full-decompile/TaleWorlds.CampaignSystem.Settlements.Locations/LocationComplex.cs').read_text(encoding='utf-8-sig')
        encounter = (Path(__file__).resolve().parents[2] / '.codex_tmp/campaignsystem-full-decompile/TaleWorlds.CampaignSystem.Encounters/LocationEncounter.cs').read_text(encoding='utf-8-sig')
        code = STUBS.replace('CHANGE_LOCATION', method(location, 'ChangeLocation'))
        code = code.replace('LOCATION_NOTIFICATION', method(encounter, 'OnCharacterLocationChanged'))
        code = code.replace('CONTROLLER', controller).replace('POSITION_BEHAVIOR', position)
        code = code.replace('EXIT_RULES', member(progress, 'CanLeaveSearch') + method(progress, 'CanUsePassage'))
        run(code)
        with self.assertRaisesRegex(AssertionError, 'accepting the hall duel locks'):
            run(code.replace('!Search.HallDuelLocked && _stage != Stage.Duel', '_stage != Stage.Duel').replace('if (Search.HallDuelLocked) return false;', ''))
        with self.assertRaisesRegex(AssertionError, 'temporary battle gear does not persist'):
            run(code.replace('data.Equipment(previousEquipment).CivilianEquipment(previousCivilian);', ''))
        with self.assertRaisesRegex(AssertionError, 'battle gear and a melee weapon are set before reentry'):
            run(code.replace('_hallDuelSpawnEquipment = true;', ''))
        with self.assertRaisesRegex(AssertionError, 'exit request must wait for actual removal'):
            run(code.replace('_hallExiting.Add(agent);', '_hallDepartures.Remove(agent);_hallExiting.Add(agent);'))
        with self.assertRaisesRegex(AssertionError, 'Sequence contains no matching element'):
            run(code.replace('Mission.GetMissionBehavior<MissionAgentHandler>().SpawnEnteringLocationCharacter(character, _hallDuelDoor.ToLocation);', ''))


if __name__ == '__main__':
    unittest.main()
