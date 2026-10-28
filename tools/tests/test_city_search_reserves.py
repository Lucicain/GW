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
class BasicCharacterObject{public string StringId="foot";public bool IsHero,IsRanged;public float Power=10;public float GetPower()=>Power;public FormationClass GetFormationClass()=>IsRanged?FormationClass.HorseArcher:FormationClass.Cavalry;}
class CharacterObject:BasicCharacterObject{public List<Equipment> BattleEquipments=new(){new()};}
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
class SearchState{public bool HasLeftHall=true,Claimed;public List<string> Losses=new();public int EnemyLossCount(string key)=>Losses.Count(x=>x==key);public void RecordEnemyLoss(string key){Losses.Add(key);}public bool ClaimEntryWave(){if(Claimed)return false;Claimed=true;return true;}public List<(string Key,float Health)> Saved=new();public float SavedEnemyHealth(string k,int n)=>Saved.Where(s=>s.Key==k).Select(s=>s.Health).Skip(n).DefaultIfEmpty(-1).First();public void SaveEnemySurvivors(IEnumerable<(string Key,float Health)> s){Saved=s.ToList();}}
static class GwpTuning{public static class CitySearch{public const int DefenderLimit=20,ReserveBatch=4,EntryReserveLimit=4;public const float ReserveInterval=3;}}
static class GwpCitySearchMission{public const string HallSpawnTag="spawnpoint_player",GateSpawnTag="sp_outside_near_town_main_gate";}
partial class GwpCitySearchMissionController:MissionLogic{
 public bool InHall;
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
 var c=New();var foot=new CharacterObject();var garrison=Party("garrison",foot,10,2);var militia=Party("militia",foot,3);var visitor=Party("enemyVisitor",foot,4);var neutral=Party("neutral",foot,8);
 var unarmed=new CharacterObject{StringId="servant"};unarmed.BattleEquipments[0].Element.Item=null;
 garrison.MemberRoster.Elements.Add(new(){Character=unarmed,Number=20});garrison.MemberRoster.Elements.Add(new(){Character=new(){IsHero=true},Number=1});
 garrison.MapFaction=militia.MapFaction=c._settlement.MapFaction;FactionManager.Hostiles.Add(visitor.MapFaction);
 c._settlement.Town.GarrisonParty=garrison;c._settlement.MilitiaPartyComponent=new(){MobileParty=militia};c._settlement.Parties.AddRange(new[]{garrison,militia,visitor,neutral});
 var guard=new Agent{Origin=new PartyAgentOrigin(garrison.Party,foot),Team=c.Mission.PlayerEnemyTeam};c.AddGuard(guard);
 c.Search.Saved.Add(("garrison|foot",31));c.Prepare();var source=(IGwpBattleSceneSource)c;
 Check(c.Remaining==14,"finite healthy roster excludes wounded, unarmed, heroes, neutrals and duplicate party entries");
 Check(guard.Origin is PartyAgentOrigin o&&o.Party==garrison.Party&&guard.Health==31,"existing guard occupies one real roster slot and retains wounds");
 Check(source.GetTotalNumberOfTroopsForSide(BattleSideEnum.Defender)==15&&source.NumberOfRemainingDefenderTroops==14,"shared support context includes initial guard plus all reserves exactly once");
 c.Prepare();Check(c.Remaining==14,"preparing twice does not duplicate roster");
 var context=new GwpBattleSceneContext{Mission=c.Mission};c.Mission.MissionBehaviors.Add(context);Check(context.TryCapture(),"city source participates in existing battle context");
 Check(context.OpeningCount(BattleSideEnum.Defender)==15&&context.RemainingPower(BattleSideEnum.Defender)==150,"already spawned neutral guards are subtracted exactly once from reserve power");
 c.Mission.Scene.Points["defender_infantry_reinforcement"]=Point();int arrivals=0,waves=0;source.OnReinforcementsSpawned+=(side,count)=>{Check(side==BattleSideEnum.Defender,"arrival notifies correct side");arrivals+=count;waves++;};
 c.Tick();Check(c.Remaining==10&&arrivals==4,"first limited batch joins combat");Check(waves==1,"one batch notifies music as one arrival wave");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Alarmed&&a.Team==c.Mission.PlayerEnemyTeam&&a.Position.x==77&&a.Position.z==12),"reserves use native defender point and wake combat AI without changing hall floor");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Formation!=null&&!a.Formation.IsAIControlled&&a.Formation.Order==MovementOrder.MovementOrderCharge&&a.AutoTarget),"distant reserves receive a continuing native charge order and automatic enemy selection");
 Check(context.RemainingPower(BattleSideEnum.Defender)==150,"moving reserves to scene cannot inflate shared power");
 Check(c.Mission.Agents.Skip(2).All(a=>a.Color1==((PartyAgentOrigin)a.Origin).Party.Primary&&a.Color2==((PartyAgentOrigin)a.Origin).Party.Secondary),"reinforcements use source party colors rather than generic mission team colors");
 c.Tick();Check(c.Remaining==10,"same-frame reentry cannot repeatedly spawn");
 DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=c.Mission.Agents.Count+1;c.Mission.CurrentTime=4;c.Tick();Check(c.Remaining==10,"engine capacity preserves pending real soldiers");
 DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission=2000;c.Mission.CurrentTime=8;c.Tick();Check(c.Remaining==6,"capacity recovery resumes queue");
 foreach(var a in c.Mission.Agents.Where(a=>a.Team==c.Mission.PlayerEnemyTeam))a.Health=21;
 c.Save();Check(c.Search.Saved.Count==15&&c.Search.Saved.Count(s=>s.Health==21)==9,"scene transition stores living wounds plus untouched reserves");
 foreach(var a in c.Mission.Agents.Where(a=>a.Team==c.Mission.PlayerEnemyTeam))a.Active=false;
 Check(c.Threat,"pending reserves prevent false victory after initial defenders fall");
 c.Mission.CurrentTime=12;c.Tick();c.Mission.CurrentTime=16;c.Tick();Check(c.Remaining==0,"finite reserve roster is exhausted");
 foreach(var a in c.Mission.Agents.Where(a=>a.Team==c.Mission.PlayerEnemyTeam))a.Active=false;Check(!c.Threat,"fully exhausted defenders end fight");
 c.Save();Check(c.Search.Saved.Count==0,"dead and unconscious units cannot be resurrected next scene");
 c=New();garrison=Party("hall",foot,1);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();
 c.Mission.Scene.Points["defender_infantry"]=Point(42,4);c.Tick();Check(c.Remaining==0&&c.Mission.Agents.Last().Position.x==42,"hall uses native initial defender point when reinforcement tag is absent");
 c=New();garrison=Party("far",foot,4);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();
 c.Mission.Scene.Points["defender_infantry_reinforcement"]=Point(2,0);c.Mission.Scene.Extra.Add(("sp_guard_patrol",Point(300,0)));c.Mission.Scene.Extra.Add(("sp_guard",Point(120,0)));
 c.Tick();Check(c.Mission.Agents.Skip(1).All(a=>a.Position.x==300),"reserves enter at the point farthest from the player, not the native siege point beside the gate");
 Check(c.Mission.Agents.Skip(1).All(a=>a.Formation?.Order==MovementOrder.MovementOrderCharge),"farthest spawn still attacks instead of waiting at its spawn point");
 c=New();var archer=new CharacterObject{IsRanged=true};garrison=Party("archers",archer,2);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["defender_infantry"]=Point();c.Tick();Check(c.Mission.Agents.Skip(1).All(a=>a.Formation==c.Mission.PlayerEnemyTeam.GetFormation(FormationClass.Ranged)&&a.AutoTarget),"mounted archers dismount into a ranged formation and retain native weapon choice");
 c=New();garrison=Party("missing",foot,1);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Tick();Check(c.Remaining==1&&c.Mission.Agents.Count==1,"missing point cannot spawn troops at world origin");
 c.Mission.Scene.Points["defender_infantry"]=Point();c.Mission.SpawnArmed=false;c.Mission.CurrentTime=4;c.Tick();Check(c.Mission.Agents.Last().Team==Team.Invalid&&!c.Mission.Agents.Last().Alarmed,"actual unarmed equipment variant never joins fight");
 var routed=c.Mission.Agents.Last();routed.Health=37;c.Removed(routed,AgentState.Routed);c.Save();Check(c.Search.Saved.Count==0&&c.Search.Losses.Count==1,"routed defender consumes task roster without entering next scene again");
 c=New();c.Search.HasLeftHall=false;garrison=Party("entry",foot,30);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();
 Check(c.Remaining==4&&!((IGwpBattleSceneSource)c).AllowsWardenRescue,"fight into city queues one wave and cannot summon random wardens");
 var attempt=c.Search;c=New();c.Search=attempt;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==0,"reentering entry scene cannot request a second wave");
 c=New();c.InHall=true;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==0,"hall never queues reinforcements even with fresh garrison");
 c=New();garrison=Party("return",foot,5);garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();c.Mission.Scene.Points["defender_infantry"]=Point();c.Tick();
 foreach(var a in c.Mission.Agents.Skip(1).ToArray()){c.Removed(a,AgentState.Killed);c.Removed(a,AgentState.Killed);}
 Check(c.Search.Losses.Count==4,"task casualty counts each actual defender once");
 attempt=c.Search;c=New();c.Search=attempt;garrison.MapFaction=c._settlement.MapFaction;c._settlement.Town.GarrisonParty=garrison;c.Prepare();Check(c.Remaining==1&&garrison.MemberRoster.Elements[0].Number==5,"outgoing reserves subtract prior task losses while campaign roster stays intact");
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
            (code.replace('SetCityPursuer(agent);', 'SetSearchFighter(agent);'), 'continuing native charge order'),
            (code.replace('element.Number - element.WoundedNumber', 'element.Number'), 'finite healthy roster'),
            (code.replace('.Distinct().ToArray()', '.ToArray()'), 'finite healthy roster'),
            (code.replace('if (present != null)', 'if (false)'), 'finite healthy roster'),
            (code.replace('if (Spawn.OpeningAgentsAlreadySpawned)', 'if (false)'), 'already spawned neutral guards'),
            (code.replace('.OrderByDescending(point => point.GlobalPosition.Distance(player))',
                          '.OrderBy(point => point.GlobalPosition.Distance(player))'), 'farthest from the player'),
            (code.replace('if (arrived > 0) _cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, arrived);', '')
                 .replace('arrived++;', 'arrived++;_cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, 1);'),
             'one batch notifies music as one arrival wave'),
            (code.replace('.ClothingColor1(reserve.Origin.FactionColor).ClothingColor2(reserve.Origin.FactionColor2)',
                          '.ClothingColor1(Mission.PlayerEnemyTeam.Color).ClothingColor2(Mission.PlayerEnemyTeam.Color2)'),
             'reinforcements use source party colors'),
        ]:
            with self.subTest(negative_control=failure), self.assertRaisesRegex(AssertionError, failure):
                run(old)


if __name__ == '__main__':
    unittest.main()
