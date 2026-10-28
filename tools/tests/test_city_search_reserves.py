"""Execute the full production reserve supplier and shared battle context.

Only scene/agent/roster boundaries are replaced; spawning and native physics
remain game checks. Native origin casualty accounting is separately verified.
"""
import unittest
import subprocess
from pathlib import Path
from test_city_search import read
from test_city_search_flow import run
from test_duty_banner import method


STUBS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
enum BattleSideEnum{None=-1,Defender,Attacker}enum MissionMode{StartUp,Battle}enum GwpCitySearchPlace{Entry,Hall}
enum AgentState{Routed,Killed,Unconscious}
enum FormationClass{Infantry,Ranged,Cavalry,HorseArcher}
static class TroopClassExtensions{public static FormationClass DismountedClass(this FormationClass c)=>c==FormationClass.Cavalry?FormationClass.Infantry:c==FormationClass.HorseArcher?FormationClass.Ranged:c;}
class MovementOrder{public static MovementOrder MovementOrderCharge=new();}
class Formation{public bool IsAIControlled=true;public MovementOrder? Order;public void SetControlledByAI(bool b){IsAIControlled=b;}public void SetMovementOrder(MovementOrder o){Order=o;}}
enum EquipmentIndex{Weapon0} [Flags]enum WeaponFlags{None=0,WeaponMask=3,MeleeWeapon=1}
static class Flags {public static bool HasAnyFlag(this WeaponFlags a,WeaponFlags b)=>(a&b)!=0;}
class Weapon{public WeaponFlags WeaponFlags=WeaponFlags.MeleeWeapon;}
class Item{public List<Weapon> Weapons=new(){new()};}class EquipmentElement{public Item? Item=new();}
class Equipment{public EquipmentElement Element=new();public EquipmentElement this[EquipmentIndex i]=>Element;}
class BasicCharacterObject{public string StringId="foot";public bool IsHero,IsRanged;public int Level=1;public float Power=10;public float GetPower()=>Power;public FormationClass GetFormationClass()=>IsRanged?FormationClass.HorseArcher:FormationClass.Cavalry;}
class CharacterObject:BasicCharacterObject{public List<Equipment> BattleEquipments=new(){new()};}
class FlattenedTroopRosterElement{public CharacterObject Troop=null!;}
class MapEventParty{public PartyBase Party;public List<FlattenedTroopRosterElement> Troops=new();public int HealthyManCountAtStart=>Troops.Count;private MapEventParty(PartyBase party){Party=party;foreach(var e in party.MobileParty.MemberRoster.Elements)for(int i=0;i<e.Number-e.WoundedNumber;i++)Troops.Add(new(){Troop=e.Character});}}
static class AccessTools{public static System.Reflection.ConstructorInfo Constructor(Type type,Type[] args)=>type.GetConstructor(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,args,null)!;}
class PriorityModel{public void EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization(MapEventParty p,object? prior,bool include,int count,bool force,List<(FlattenedTroopRosterElement Troop,MapEventParty Party,float Priority)> target){foreach(var e in p.Troops)target.Add((e,p,e.Troop.Level));}}
class CampaignModels{public PriorityModel TroopSupplierProbabilityModel=new();}class Campaign{public static Campaign Current=new();public CampaignModels Models=new();}
class Banner{}class Faction{}class Hero{public static Hero MainHero=new();public Faction MapFaction=new();}
static class FactionManager{public static HashSet<Faction> Hostiles=new();public static bool IsAtWarAgainstFaction(Faction a,Faction b)=>Hostiles.Contains(a);}
class Element{public CharacterObject Character=new();public int Number,WoundedNumber;}
class Roster{public List<Element> Elements=new();public IEnumerable<Element> GetTroopRoster()=>Elements;}
class PartyBase{public uint Primary=0x12345678,Secondary=0x98765432;public Banner Banner=new();public MobileParty MobileParty=null!;}
class MobileParty{public static MobileParty MainParty=new("main");public string StringId;public bool IsActive=true;public Faction MapFaction=new();public Roster MemberRoster=new();public PartyBase Party;
 public MobileParty(string id){StringId=id;Party=new(){MobileParty=this};}}
class Town{public MobileParty? GarrisonParty;}class Militia{public MobileParty MobileParty=null!;}
class Settlement{public List<MobileParty> Parties=new();public Town Town=new();public Militia? MilitiaPartyComponent;public Faction MapFaction=new();}
interface IAgentOriginBase{BasicCharacterObject Troop{get;}object BattleCombatant{get;}}
class PartyAgentOrigin:IAgentOriginBase{public PartyBase Party;public object BattleCombatant=>Party;public BasicCharacterObject Troop{get;}public PartyAgentOrigin(PartyBase p,CharacterObject c){Party=p;Troop=c;}}
class GwpBattleSupportOrigin:IAgentOriginBase{public object BattleCombatant=>new();public BasicCharacterObject Troop{get;}=new();}
class GreyWardenSafePartyAgentOrigin:PartyAgentOrigin{public uint FactionColor=>Party.Primary;public uint FactionColor2=>Party.Secondary;public Banner Banner=>Party.Banner;public GreyWardenSafePartyAgentOrigin(PartyBase p,CharacterObject c,bool player,int rank):base(p,c){}}
class Vec2{}struct Vec3{public float x,y,z;public Vec3(float a,float height){x=a;y=0;z=height;}public float Distance(Vec3 o)=>MathF.Sqrt((x-o.x)*(x-o.x)+(y-o.y)*(y-o.y)+(z-o.z)*(z-o.z));}
class Direction{public Vec2 AsVec2=new();}class Rotation{public Direction f=new();}
class MatrixFrame{public static MatrixFrame Identity=new();public Vec3 origin;public Rotation rotation=new();}class GameEntity{public MatrixFrame Frame=new();public Vec3 GlobalPosition=>Frame.origin;public MatrixFrame GetGlobalFrame()=>Frame;}
class Scene{public Dictionary<string,GameEntity> Points=new();public List<(string Tag,GameEntity Point)> Extra=new();public List<string> Reads=new();
 public IEnumerable<GameEntity> FindEntitiesWithTag(string s){Reads.Add(s);if(Points.TryGetValue(s,out var e))yield return e;foreach(var x in Extra)if(x.Tag==s)yield return x.Point;}}
class Team{public static Team Invalid=new(){Side=BattleSideEnum.None};public uint Color=0x77777777,Color2=0x88888888;public BattleSideEnum Side;public Dictionary<FormationClass,Formation> Forms=new();public Formation GetFormation(FormationClass c){if(c==FormationClass.Cavalry||c==FormationClass.HorseArcher)throw new Exception("no mounted city pursuers");if(!Forms.ContainsKey(c))Forms[c]=new();return Forms[c];}}
class Agent{public static Agent Main=null!;public IAgentOriginBase Origin=null!;public BasicCharacterObject Character=>Origin.Troop;public float CharacterPowerCached=>Character.GetPower();public bool Active=true,IsHuman=true,IsRunningAway,Armed=true,Alarmed,AutoTarget;public Team Team=null!;public Formation? Formation;public void SetAutomaticTargetSelection(bool b){AutoTarget=b;}public float Health=100,HealthLimit=100;public Vec3 Position;public uint Color1,Color2;public void WieldInitialWeapons(){}public bool IsActive()=>Active;public bool HasWeapon()=>Armed;public void SetTeam(Team team,bool b){Team=team;}}
class MissionBehavior{public Mission Mission=null!;public virtual MissionBehaviorType BehaviorType=>MissionBehaviorType.Other;public virtual void OnAgentBuild(Agent a,Banner b){}}
enum MissionBehaviorType{Other}class MissionLogic:MissionBehavior{}
class GreyWardenFieldSparringMissionController:MissionBehavior{}
static class GameNetwork{public static bool IsMultiplayer;}
static class GwpCommon{public static bool IsGreyWardenAffiliatedCharacter(BasicCharacterObject c)=>false;}
static class GwpBattleScenePolicy{public static bool MusicEligible(int grey,int total)=>false;}
class DefaultBattleMissionAgentSpawnLogic:MissionBehavior{
 public event Action<BattleSideEnum,int>? OnReinforcementsSpawned{add{}remove{}}
 public bool IsDeploymentOver=true;public int NumberOfAgents,NumberOfRemainingDefenderTroops,NumberOfRemainingAttackerTroops;public static int MaxNumberOfAgentsForMission=2000;
 public int GetTotalNumberOfTroopsForSide(BattleSideEnum s)=>0;public IEnumerable<IAgentOriginBase> GetAllTroopsForSide(BattleSideEnum s)=>Array.Empty<IAgentOriginBase>();}
class Mission{
 public List<MissionBehavior> MissionBehaviors=new();public List<Agent> Agents=new();public IEnumerable<Agent> AllAgents=>Agents;public Agent MainAgent=>Agent.Main;
 public Team PlayerTeam=new(){Side=BattleSideEnum.Attacker},PlayerEnemyTeam=new(){Side=BattleSideEnum.Defender};public Scene Scene=new();public bool MissionEnded;public float CurrentTime;public bool SpawnArmed=true;
 public T? GetMissionBehavior<T>() where T:MissionBehavior=>MissionBehaviors.OfType<T>().FirstOrDefault();
 public Vec3 GetRandomPositionAroundPoint(Vec3 p,float min,float max)=>p;
 public Agent SpawnAgent(AgentBuildData data){
  if(!data.NoHorse||!data.Reinforcement||data.Civilian)throw new Exception("invalid reserve spawn flags");
  var a=new Agent{Origin=data.Origin,Team=data.Target,Position=data.Position,Armed=SpawnArmed,Color1=data.Color1,Color2=data.Color2};Agents.Add(a);foreach(var b in MissionBehaviors)b.OnAgentBuild(a,new());return a;}}
class AgentBuildData{public IAgentOriginBase Origin=null!;public Team Target=null!;public Vec3 Position;public uint Color1,Color2;public bool NoHorse,Reinforcement,Civilian;
 public AgentBuildData(BasicCharacterObject c){}public AgentBuildData TroopOrigin(IAgentOriginBase origin){Origin=origin;return this;}public AgentBuildData Team(Team t){Target=t;return this;}public AgentBuildData Banner(Banner b)=>this;
 public AgentBuildData ClothingColor1(uint c){Color1=c;return this;}public AgentBuildData ClothingColor2(uint c){Color2=c;return this;}public AgentBuildData NoHorses(bool b){NoHorse=b;return this;}public AgentBuildData CivilianEquipment(bool b){Civilian=b;return this;}public AgentBuildData IsReinforcement(bool b){Reinforcement=b;return this;}
 public AgentBuildData InitialPosition(Vec3 p){Position=p;return this;}public AgentBuildData InitialDirection(Vec2 d)=>this;}
class SearchState{public bool HasLeftHall=true,Claimed;public List<string> Losses=new();public int EnemyLossCount(string key)=>Losses.Count(x=>x==key);public void RecordEnemyLoss(string key){Losses.Add(key);}public bool ClaimEntryWave(){if(Claimed)return false;Claimed=true;return true;}public List<(string Key,float Health)> Saved=new();public float SavedEnemyHealth(string k,int n)=>Saved.Where(s=>s.Key==k).Select(s=>s.Health).Skip(n).DefaultIfEmpty(-1).First();public void SaveEnemySurvivors(IEnumerable<string> keys,IEnumerable<(string Key,float Health)> s){var changed=keys.ToHashSet();Saved.RemoveAll(entry=>changed.Contains(entry.Key));Saved.AddRange(s);}}
static class GwpTuning{public static class CitySearch{public const int DefenderLimit=20,PursuerTarget=30,ReserveBatch=4,EntryReserveLimit=4;public const float ReserveInterval=3,PursuerCheckInterval=.5f;}}
static class GwpCitySearchMission{public const string HallSpawnTag="spawnpoint_player",GateSpawnTag="sp_outside_near_town_main_gate";}
partial class GwpCitySearchMissionController:MissionLogic{
 public bool InHall;private bool EscapingCity=>!InHall&&Search.HasLeftHall;
 enum Stage{Entering,Fighting,FightOver}private Stage _stage=Stage.Fighting;public Settlement _settlement=new();public SearchState Search=new();
 private List<Agent> _defenders=new();private Dictionary<Agent,object> _ours=new();private bool IsOurs(Agent a)=>a.Team==Mission.PlayerTeam;
 private static void SetSearchFighter(Agent a){a.Alarmed=true;}
 public void Prepare()=>PrepareCityReserves(CityDefenseParties());public void Tick()=>TickCityReserves();public void Save()=>SaveCitySoldiers();public int Remaining=>_cityReserves.Count;public bool Threat=>HasCityDefenders();public void AddGuard(Agent a){_defenders.Add(a);Mission.Agents.Add(a);}
 public void Removed(Agent a,AgentState state){a.Active=false;NoteCitySoldierRemoval(a,state);}
}
'''

SCENARIOS = r'''
static class Program{
 static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static MobileParty Party(string id,CharacterObject c,int number,int wounded=0){var p=new MobileParty(id);p.MemberRoster.Elements.Add(new(){Character=c,Number=number,WoundedNumber=wounded});return p;}
 static GwpCitySearchMissionController New(){var m=new Mission();Agent.Main=new(){Origin=new PartyAgentOrigin(MobileParty.MainParty.Party,new()),Team=m.PlayerTeam};m.Agents.Add(Agent.Main);var c=new GwpCitySearchMissionController{Mission=m};m.MissionBehaviors.Add(c);return c;}
 static GameEntity Point(float x=77,float z=12)=>new(){Frame=new(){origin=new Vec3(x,z)}};
 static void Main(){
 var c=New();var foot=new CharacterObject();var garrison=Party("garrison",foot,10,2);var militia=Party("militia",foot,3);var visitor=Party("alliedLord",foot,4);var outsider=Party("hostileOtherFaction",foot,8);
 var unarmed=new CharacterObject{StringId="servant"};unarmed.BattleEquipments[0].Element.Item=null;
 garrison.MemberRoster.Elements.Add(new(){Character=unarmed,Number=20});garrison.MemberRoster.Elements.Add(new(){Character=new(){IsHero=true},Number=1});
 garrison.MapFaction=militia.MapFaction=visitor.MapFaction=c._settlement.MapFaction;FactionManager.Hostiles.Add(outsider.MapFaction);
 c._settlement.Town.GarrisonParty=garrison;c._settlement.MilitiaPartyComponent=new(){MobileParty=militia};c._settlement.Parties.AddRange(new[]{garrison,militia,visitor,outsider});
 var guard=new Agent{Origin=new PartyAgentOrigin(garrison.Party,foot),Team=c.Mission.PlayerEnemyTeam};c.AddGuard(guard);
 c.Search.Saved.Add(("garrison|foot",31));c.Prepare();var source=(IGwpBattleSceneSource)c;
 Check(c.Remaining==14,"finite healthy roster includes every same-faction lord and excludes other factions, wounded, unarmed, heroes and duplicate parties");
 Check(guard.Origin is PartyAgentOrigin o&&o.Party==garrison.Party&&guard.Health==31,"existing guard occupies one real roster slot and retains wounds");
 Check(source.GetTotalNumberOfTroopsForSide(BattleSideEnum.Defender)==15&&source.NumberOfRemainingDefenderTroops==14,"shared context has each finite soldier once");
 c.Prepare();Check(c.Remaining==14,"preparing twice does not duplicate roster");
 var context=new GwpBattleSceneContext{Mission=c.Mission};c.Mission.MissionBehaviors.Add(context);Check(context.TryCapture(),"city participates in shared battle context");
 Check(context.OpeningCount(BattleSideEnum.Defender)==15&&context.RemainingPower(BattleSideEnum.Defender)==150,"already spawned neutral guard is subtracted once");
 c.Mission.Scene.Points["defender_infantry_reinforcement"]=Point();int arrivals=0,waves=0;source.OnReinforcementsSpawned+=(side,count)=>{Check(side==BattleSideEnum.Defender,"correct reinforcement side");arrivals+=count;waves++;};
 c.Tick();Check(c.Remaining==0&&arrivals==14&&waves==1,"escape immediately fills deficit from finite real roster in one wave");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Alarmed&&a.Team==c.Mission.PlayerEnemyTeam&&a.Position.x==77&&a.Position.z==12),"native point and floor retained");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Formation!=null&&!a.Formation.IsAIControlled&&a.Formation.Order==MovementOrder.MovementOrderCharge&&a.AutoTarget),"every reserve receives continuing native charge order and target selection");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Color1==((PartyAgentOrigin)a.Origin).Party.Primary&&a.Color2==((PartyAgentOrigin)a.Origin).Party.Secondary),"source party colors preserved");
 c=New();garrison=Party("large",foot,45);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Mission.Scene.Points["sp_guard_patrol"]=Point(300,0);c.Prepare();c.Tick();
 Check(c.Remaining==15&&c.Mission.Agents.Count==31,"thirty real pursuers, not twenty or four");c.Tick();Check(c.Remaining==15,"same-frame call cannot spawn repeatedly");
 foreach(var a in c.Mission.Agents.Skip(1).Take(4).ToArray())c.Removed(a,AgentState.Killed);
 DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=28;c.Mission.CurrentTime=1;c.Tick();Check(c.Remaining==15,"engine capacity preserves pending soldiers");
 DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=2000;c.Mission.CurrentTime=2;c.Tick();Check(c.Remaining==11&&c.Mission.Agents.Count(a=>a.Active&&a!=Agent.Main)==30,"four losses are replaced to minimum thirty");
 foreach(var a in c.Mission.Agents.Where(a=>a.Active&&a!=Agent.Main))a.Health=21;c.Save();Check(c.Search.Saved.Count==41&&c.Search.Saved.Count(e=>e.Health==21)==30,"living wounds plus unused reserves saved");
 foreach(var a in c.Mission.Agents.Where(a=>a.Active&&a!=Agent.Main).ToArray())c.Removed(a,AgentState.Killed);Check(c.Threat,"remaining real reserves prevent false victory");
 c.Mission.CurrentTime=3;c.Tick();Check(c.Remaining==0&&c.Mission.Agents.Count(a=>a.Active&&a!=Agent.Main)==11,"finite roster exhaustion does not manufacture more soldiers");
 foreach(var a in c.Mission.Agents.Where(a=>a.Active&&a!=Agent.Main).ToArray())c.Removed(a,AgentState.Unconscious);Check(!c.Threat,"exhausted defenders end fight");c.Save();Check(c.Search.Saved.Count==0,"observed dead roster is removed from old health snapshot");
 c=New();garrison=Party("far",foot,4);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["defender_infantry_reinforcement"]=Point(2,0);c.Mission.Scene.Extra.Add(("sp_guard_patrol",Point(300,0)));c.Mission.Scene.Extra.Add(("sp_guard",Point(120,0)));c.Tick();Check(c.Mission.Agents.Skip(1).All(a=>a.Position.x==300),"farthest original spawn point selected");
 c=New();var archer=new CharacterObject{IsRanged=true};garrison=Party("archers",archer,2);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["defender_infantry"]=Point();c.Tick();Check(c.Mission.Agents.Skip(1).All(a=>a.Formation==c.Mission.PlayerEnemyTeam.GetFormation(FormationClass.Ranged)&&a.AutoTarget),"horse archers dismount to ranged formation");
 c=New();garrison=Party("missing",foot,1);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Tick();Check(c.Remaining==1&&c.Mission.Agents.Count==1,"missing point cannot spawn at origin");
 c.Mission.Scene.Points["defender_infantry"]=Point();c.Mission.SpawnArmed=false;c.Mission.CurrentTime=1;c.Tick();Check(c.Mission.Agents.Last().Team==Team.Invalid&&!c.Mission.Agents.Last().Alarmed,"actual unarmed variant is not a pursuer");var routed=c.Mission.Agents.Last();c.Removed(routed,AgentState.Routed);c.Save();Check(c.Search.Saved.Count==0&&c.Search.Losses.Count==1,"routed soldier consumes task roster");
 c=New();c.Search.HasLeftHall=false;garrison=Party("entry",foot,30);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["sp_guard"]=Point();c.Tick();Check(c.Remaining==0&&c.Mission.Agents.Count==5&&!((IGwpBattleSceneSource)c).AllowsWardenRescue,"entry remains exactly one four-person reinforcement wave");
 var attempt=c.Search;c=New();c.Search=attempt;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==0,"entry cannot request another wave");c=New();c.InHall=true;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==0,"hall never receives reserves");
 c=New();garrison=Party("return",foot,5);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["defender_infantry"]=Point();c.Tick();var killed=c.Mission.Agents[1];c.Removed(killed,AgentState.Killed);c.Removed(killed,AgentState.Killed);Check(c.Search.Losses.Count==1,"duplicate casualty callback cannot count twice");
 attempt=c.Search;c=New();c.Search=attempt;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==4&&garrison.MemberRoster.Elements[0].Number==5,"prior task losses subtracted without changing campaign roster");
 c=New();var high=new CharacterObject{StringId="elite",Level=26};var low=new CharacterObject{StringId="recruit",Level=6};garrison=Party("priority",low,40);garrison.MemberRoster.Elements.Add(new(){Character=high,Number=40});garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["sp_guard"]=Point();c.Tick();Check(c.Mission.Agents.Skip(1).All(a=>a.Character==high),"native priority model is consulted instead of FIFO roster order");
 Console.WriteLine("CITY_SEARCH_RESERVE_ASSERTIONS="+n);
 }
}}
'''


def production(name):
    text = read(name)
    return text[text.index('namespace GreyWardenPolicePurity'):]


class CitySearchReserveTests(unittest.TestCase):
    def test_installed_origin_casualties_return_to_source_only(self):
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        def decompile(assembly, name):
            return subprocess.check_output(['ilspycmd', '-t', name, str(game / assembly)], text=True, encoding='utf-8-sig')
        origin = decompile('bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll', 'TaleWorlds.CampaignSystem.AgentOrigins.PartyAgentOrigin')
        removal = decompile('Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll', 'SandBox.Missions.MissionLogics.BattleAgentLogic')
        bodies = '\n'.join(method(origin, name) for name in ['SetWounded', 'SetKilled', 'SetRouted'])
        safe_bodies = '\n'.join(method(read('GreyWardenSafeTroopSupplier.cs'), name) for name in ['SetWounded', 'SetKilled', 'SetRouted'])
        native = method(removal, 'OnAgentRemoved').replace('override ', '')
        code = r'''
using System;
enum AgentState{Active=1,Routed=2,Unconscious=3,Killed=4}class KillingBlow{}
class Hero{public void MakeWounded(){}}
class CharacterObject{public bool IsHero;public Hero? HeroObject;}
class Roster{public int Number=5,Wounded,Calls;public void AddToCounts(CharacterObject c,int count,bool insertAtFront=false,int wounded=0){Number+=count;Wounded+=wounded;Calls++;}}
class PartyBase{public Roster MemberRoster=new();}
static class KillCharacterAction{public static void ApplyByBattle(Hero h,object? cause){}}
interface IAgentOriginBase{object BattleCombatant{get;}void SetWounded();void SetKilled();void SetRouted(bool retreat);}
class PartyAgentOrigin:IAgentOriginBase{public bool _isInvincible,_alwaysWounded;public CharacterObject _troop=new();public PartyBase Party=new();public object BattleCombatant=>Party;ORIGIN}
class SafeOrigin:IAgentOriginBase{public PartyBase Party=new();public object BattleCombatant=>Party;SAFE_ORIGIN}
class Team{public int Side;}
class Agent{public bool IsMount;public bool IsHuman=true;public CharacterObject Character=new();public IAgentOriginBase Origin=new PartyAgentOrigin();public Team Team=new();}
static class AgentComponentExtensions{public static float GetMorale(Agent a)=>10;}
class CampaignEventReceiver{public void OnCharacterDefeated(Hero a,Hero b){}}
class CampaignEventDispatcher:CampaignEventReceiver{public static CampaignEventDispatcher Instance=new();}
class Native{void CheckUpgrade(int side,PartyBase p,CharacterObject c){}REMOVAL}
static class Program{static void Check(bool b,string why){if(!b)throw new Exception(why);}static void Main(){
 var player=new PartyBase();var guard=new PartyBase();var militia=new PartyBase();var regular=new CharacterObject();var native=new Native();
 Agent Soldier(PartyBase p)=>new(){Character=regular,Origin=new PartyAgentOrigin{Party=p,_troop=regular}};
 native.OnAgentRemoved(Soldier(guard),null!,AgentState.Killed,new());
 Check(guard.MemberRoster.Number==4&&guard.MemberRoster.Wounded==0&&guard.MemberRoster.Calls==1,"native kill decrements real guard roster once");
 native.OnAgentRemoved(Soldier(militia),null!,AgentState.Unconscious,new());
 Check(militia.MemberRoster.Number==5&&militia.MemberRoster.Wounded==1&&militia.MemberRoster.Calls==1,"native wound affects source militia healthy count once");
 native.OnAgentRemoved(Soldier(guard),null!,AgentState.Routed,new());
 Check(guard.MemberRoster.Number==4&&guard.MemberRoster.Calls==1,"routed survivor remains in original roster");
 Check(player.MemberRoster.Number==5&&player.MemberRoster.Calls==0,"enemy casualties cannot consume player's matching troop");
 foreach(var state in new[]{AgentState.Killed,AgentState.Unconscious,AgentState.Routed})native.OnAgentRemoved(new Agent{Character=regular,Origin=new SafeOrigin{Party=guard}},null!,state,new());
 Check(guard.MemberRoster.Number==4&&guard.MemberRoster.Calls==1,"actual safe-origin callbacks leave enemy campaign roster unchanged through native casualty dispatch");
 native.OnAgentRemoved(Soldier(player),null!,AgentState.Killed,new());Check(player.MemberRoster.Number==4&&player.MemberRoster.Calls==1,"player keeps actual native casualty accounting");
 Console.WriteLine("CITY_SEARCH_NATIVE_ROSTER_ASSERTIONS=6");
}}
'''.replace('SAFE_ORIGIN', safe_bodies).replace('ORIGIN', bodies).replace('REMOVAL', native)
        run(code)

    def test_finite_rosters_shared_context_and_native_points(self):
        supplier = production('GwpCitySearchMission.Reinforcements.cs')
        code = STUBS + SCENARIOS + supplier + production('GwpBattleSceneSource.cs') + production('GwpBattleSceneContext.cs')
        run(code)
        for old, failure in [
            (code.replace('priority.OrderByDescending(entry => entry.Priority)', 'priority'), 'native priority model is consulted'),
            (code.replace('GwpTuning.CitySearch.PursuerTarget - living', '20 - living'), 'thirty real pursuers'),
            (code.replace('SetCityPursuer(agent);', 'SetSearchFighter(agent);'), 'continuing native charge order'),
            (code.replace('i<e.Number-e.WoundedNumber', 'i<e.Number'), 'finite healthy roster'),
            (code.replace('.Distinct().ToArray()', '.ToArray()'), 'finite healthy roster'),
            (code.replace('if (present != null)', 'if (false)'), 'finite healthy roster'),
            (code.replace('if (Spawn.OpeningAgentsAlreadySpawned)', 'if (false)'), 'already spawned neutral guard'),
            (code.replace('.OrderByDescending(point => point.GlobalPosition.Distance(player))',
                          '.OrderBy(point => point.GlobalPosition.Distance(player))'), 'farthest original spawn point selected'),
            (code.replace('if (arrived > 0) _cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, arrived);', '')
                 .replace('arrived++;', 'arrived++;_cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, 1);'),
             'escape immediately fills deficit'),
            (code.replace('.ClothingColor1(reserve.Origin.FactionColor).ClothingColor2(reserve.Origin.FactionColor2)',
                          '.ClothingColor1(Mission.PlayerEnemyTeam.Color).ClothingColor2(Mission.PlayerEnemyTeam.Color2)'),
             'source party colors preserved'),
        ]:
            with self.subTest(negative_control=failure), self.assertRaisesRegex(AssertionError, failure):
                run(old)


if __name__ == '__main__':
    unittest.main()
