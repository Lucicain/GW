"""Run the production city-search coordinator and mission progression against explicit engine boundaries.

Mission loading, navigation and animations are stubs. Campaign state, transition
decisions, survivor snapshots and save/load use the actual production bodies.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_duty_banner import method
from test_city_search import read, block


def run(code):
    # Negative controls return a failing process without invoking Windows crash reporting.
    code = re.sub(r'static void Main\(\)\s*\{',
                  'static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}} static void Run(){', code)
    major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
    with tempfile.TemporaryDirectory(prefix='gwp-city-flow-') as temp:
        p = Path(temp)
        (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><NoWarn>CS8618;CS0649;CS0414;CS8600;CS8601;CS8602;CS8603;CS8604</NoWarn><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
        (p / 'Program.cs').write_text(code, encoding='utf-8')
        result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
        if result.returncode:
            raise AssertionError(result.stdout + result.stderr)
        print(result.stdout.strip())


STUBS = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
namespace TaleWorlds.ObjectSystem{class MBObjectManager{
 public static MBObjectManager Instance=new();public T GetObject<T>(string id) where T:class=>GreyWardenPolicePurity.CharacterObject.All.First(c=>c.StringId==id) as T;}}
namespace GreyWardenPolicePurity{
interface IDataStore{void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public Dictionary<string,JsonElement> Data=new();public bool Loading;
 public void SyncData<T>(string key,ref T value){if(Loading){if(Data.TryGetValue(key,out var json))value=json.Deserialize<T>()!;}else Data[key]=JsonSerializer.SerializeToElement(value);}}
class CharacterObject{public static List<CharacterObject> All=new();public static CharacterObject PlayerCharacter=new("player"){IsPlayerCharacter=true};public string StringId;public bool IsPlayerCharacter;public CharacterObject(string id){StringId=id;All.Add(this);}}
class Hero{public static List<Hero> All=new();public static Hero MainHero=new("player");public string StringId;public CharacterObject CharacterObject;public bool IsDead,IsPrisoner,IsWounded;public Hero(string id){StringId=id;CharacterObject=new(id);All.Add(this);}public static Hero FindFirst(Func<Hero,bool> p)=>All.FirstOrDefault(p);}
class Town{public int GetWallLevel()=>1;}
class Location{public string StringId="";public string GetSceneName(int n)=>"arena";}
class LocationCharacter{}class Locations{public Location GetLocationWithId(string s)=>new(){StringId=s};public Location? GetLocationOfCharacter(Hero h)=>null;public LocationCharacter GetLocationCharacterOfHero(Hero h)=>new();public void ChangeLocation(LocationCharacter c,Location a,Location b){}}
class Settlement{public static Settlement? CurrentSettlement;public string StringId="town";public Town Town=new();public Locations LocationComplex=new();}
class TroopRosterElement{public CharacterObject Character;public int Number,WoundedNumber;}
class TroopRoster{List<TroopRosterElement> e=new();public static TroopRoster CreateDummyTroopRoster()=>new();public IEnumerable<TroopRosterElement> GetTroopRoster()=>e;public int Total=>e.Sum(x=>x.Number-x.WoundedNumber);
 public void AddToCounts(CharacterObject c,int n,int wounded=0){var v=e.FirstOrDefault(x=>x.Character==c);if(v==null){v=new(){Character=c};e.Add(v);}v.Number+=n;v.WoundedNumber+=wounded;}}
class PartyBase{public static PartyBase MainParty=new();public TroopRoster MemberRoster=TroopRoster.CreateDummyTroopRoster();}
static class CrimeRecord{public static Settlement? Shelter;public static Settlement? ShelterOf(Hero h)=>h.IsDead||h.IsPrisoner?null:Shelter;}
static class GwpVoluntarySurrender{public static int Calls;public static bool Allowed=true;public static bool TryCapture(PartyBase p,Hero h){Calls++;if(!Allowed)return false;h.IsPrisoner=true;return true;}}
class PlayerBountyBehavior{public int Defeats;public void RecordCitySearchDefeat(Hero h){Defeats++;}public int Registered;public bool RegisterCaptureInCustody(){Registered++;return true;}}
class Conversation{public bool IsConversationInProgress;}
class MenuManager{public Location? NextLocation,PreviousLocation;}
class Campaign{public static Campaign Current=new();public GwpCitySearchBehavior Search=new();public PlayerBountyBehavior Bounty=new();public Conversation ConversationManager=new();public MenuManager GameMenuManager=new();public T GetCampaignBehavior<T>() where T:class=>(typeof(T)==typeof(GwpCitySearchBehavior)?Search:Bounty as object) as T;}
class Game{public static Game Current=new();public object GameType=Campaign.Current;}
class MapState{}
class GameStateManager{public static GameStateManager Current=new();public object ActiveState=new MapState();}
static class PlayerEncounter{public static object? Current=new();public static int Leaves,Finishes;public static void LeaveSettlement(){Leaves++;Settlement.CurrentSettlement=null;}public static void Finish(){Finishes++;Current=null;}}
class MissionLogic{public Mission Mission=>Mission.Current!;public virtual void OnMissionTick(float dt){}protected virtual void OnEndMission(){}public void Close()=>OnEndMission();}
class Mission{public static Mission? Current;public bool Ended;public List<MissionLogic> Behaviors=new();public void AddMissionBehavior(MissionLogic l)=>Behaviors.Add(l);public void EndMission(){Ended=true;}public void Close(){foreach(var b in Behaviors)b.Close();Current=null;}}
static class CampaignMission{public static Action<CharacterObject>? Callback;public static int Arenas;public static Mission? Created;public static Location? PreviousAtArenaOpen;
 public static Mission OpenArenaDuelMission(string s,Location l,CharacterObject c,bool civilian,bool horse,Action<CharacterObject> cb,float hp){Callback=cb;Arenas++;PreviousAtArenaOpen=Campaign.Current.GameMenuManager.PreviousLocation;Created=new();return Created;}
 public static void Initialize(){Mission.Current=Created;}}
static class GwpCitySearchMission{public static int Opens;public static TroopRoster Last;public static GwpCitySearchPlace Place;public static void Open(Settlement s,Hero h,TroopRoster r){Opens++;Last=r;Place=Campaign.Current.Search.SearchPlace;Mission.Current=new();}}
static class GwpAiDiagnostics{public static void WriteCitySearch(string s,string d){}}
partial class GwpCitySearchBehavior{
 static PlayerBountyBehavior Bounty=>Campaign.Current.Bounty;
 public void Start(Settlement s,Hero h,TroopRoster r){PartyBase.MainParty.MemberRoster=r;BeginSearch(s,h,r);}
 public void Sync(IDataStore store)=>SyncSearch(store);
 public bool Pending=>_openPending;public bool Capture=>_captureReady;public bool LeavePending=>_leavePending;
}
}
'''

SCENARIOS = r'''
namespace GreyWardenPolicePurity{
static class Program{
 static int n;static void Check(bool yes,string why){n++;if(!yes)throw new Exception(why);}
 static GwpCitySearchBehavior New(out Settlement s,out Hero h){Mission.Current=null;Hero.MainHero.IsPrisoner=false;Hero.MainHero.IsWounded=false;PlayerEncounter.Current=new();
  s=new();h=new Hero("target"+n);Settlement.CurrentSettlement=s;CrimeRecord.Shelter=s;var b=new GwpCitySearchBehavior();Campaign.Current.Search=b;return b;}
 static GwpCitySearchBehavior Reload(GwpCitySearchBehavior before){var save=new Store();before.Sync(save);
  var load=new Store{Loading=true,Data=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(save.Data))!};
  var after=new GwpCitySearchBehavior();after.Sync(load);Campaign.Current.Search=after;return after;}
 static void Main(){
  var b=New(out var s,out var h);var foot=new CharacterObject("foot");var bow=new CharacterObject("bow");var r=TroopRoster.CreateDummyTroopRoster();r.AddToCounts(foot,3,1);r.AddToCounts(bow,1);r.AddToCounts(CharacterObject.PlayerCharacter,1);
  b.Start(s,h,r);Check(b.SearchPlace==GwpCitySearchPlace.Entry&&b.Pending,"selection queues entry");
  Check(b.SavedHealth("foot",0)==-1&&b.SavedHealth("foot",1)==-1,"wounded excluded and player not duplicated");
  b.SaveEnemySurvivors(new[]{("garrison|foot",24f),("garrison|foot",57f),("militia|foot",16f)});b=Reload(b);
  Check(b.SavedEnemyHealth("garrison|foot",0)==24&&b.SavedEnemyHealth("garrison|foot",1)==57&&b.SavedEnemyHealth("militia|foot",0)==16,"enemy survivor wounds retain source party and ordinal across save/load");
  Check(b.SavedEnemyHealth("other|foot",0)==-1,"new real reserve slot uses native initial health");
  GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Last.Total==3,"entry has exactly three healthy selected followers");
  Check(Campaign.Current.GameMenuManager.PreviousLocation==null,"first entry has no nonexistent previous passage");
  b.GateState=GwpCitySearchGate.Allowed;b.SaveSurvivors(new[]{(foot,37f),(bow,12f)});b.PauseSearch();Mission.Current!.Close();b=Reload(b);
  Check(b.HallAccessible&&!b.Pending,"Tab and save/load preserve gate permission without opening a scene");
  var opens=GwpCitySearchMission.Opens;GwpCitySearchBehavior.Pump();Check(opens==GwpCitySearchMission.Opens,"Tab remains at search menu");
  b.QueuePlace(GwpCitySearchPlace.Hall);Campaign.Current.GameMenuManager.NextLocation=new();GwpCitySearchBehavior.Pump();
  Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Hall&&GwpCitySearchMission.Last.Total==2,"hall receives only survivors");
  Check(b.SavedHealth("foot",0)==37&&b.SavedHealth("bow",0)==12,"individual wounds persist across save/load");
  Check(Campaign.Current.GameMenuManager.NextLocation==null&&Campaign.Current.GameMenuManager.PreviousLocation?.StringId=="center","hall uses native center-to-hall passage provenance");
  b.AgreeDuel();Check(b.DuelAgreed&&!b.Returning&&b.SearchPlace==GwpCitySearchPlace.Hall&&!b.Pending,"agreeing duel leaves player in hall");
  b.QueuePlace(GwpCitySearchPlace.Street);Mission.Current!.Close();GwpCitySearchBehavior.Pump();Check(Campaign.Current.GameMenuManager.PreviousLocation?.StringId=="lordshall","walking out of hall spawns at actual hall door");
  Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Street&&b.DuelAgreed,"street walk preserves agreed duel");
  Mission.Current!.Close();b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();Check(CampaignMission.Arenas==1,"walking to arena opens native duel");
  Check(Mission.Current==null&&CampaignMission.Created!.Behaviors.Count==1,"arena lifecycle attaches return behavior before Current is initialized");CampaignMission.Initialize();
  CampaignMission.Callback!(CharacterObject.PlayerCharacter);Check(b.Capture&&!h.IsPrisoner&&b.Returning,"arena win secures target without instant capture");
  Mission.Current!.Behaviors[0].OnMissionTick(0);Check(!Mission.Current.Ended,"native duel winner stays until player exits");Mission.Current.Close();b=Reload(b);
  Check(b.SearchPlace==GwpCitySearchPlace.Escape&&!b.Pending,"arena Tab returns to menu without forced scene");
  b.QueuePlace(GwpCitySearchPlace.Escape);GwpCitySearchBehavior.Pump();Check(Campaign.Current.GameMenuManager.PreviousLocation?.StringId=="arena","arena exit resumes at actual arena door");Mission.Current!.Close();
  var cap=GwpVoluntarySurrender.Calls;var reg=Campaign.Current.Bounty.Registered;b.FinishSearch(false);
  Check(!h.IsPrisoner&&GwpVoluntarySurrender.Calls==cap&&Campaign.Current.Bounty.Registered==reg,"losing escape grants no prisoner or capture credit");
  b=New(out s,out h);b.Start(s,h,r);b.PauseSearch();b.PeacefulCapture();Check(b.SearchActive&&b.Returning&&!h.IsPrisoner&&!b.Pending,"peaceful surrender lets player walk out first");b=Reload(b);b.FinishSearch(true);
  Check(h.IsPrisoner&&Campaign.Current.Bounty.Registered==reg+1&&!b.SearchActive,"escape captures via existing owner exactly once");
  var leaves=PlayerEncounter.Leaves;Mission.Current=new();GwpCitySearchBehavior.Pump();Check(PlayerEncounter.Leaves==leaves,"no settlement teardown inside scene");Mission.Current.Close();b=Reload(b);GwpCitySearchBehavior.Pump();
  Check(PlayerEncounter.Leaves==leaves+1,"saved exit completes on map");GwpCitySearchBehavior.Pump();Check(PlayerEncounter.Leaves==leaves+1,"exit is idempotent");
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();CampaignMission.Initialize();CampaignMission.Callback!(h.CharacterObject);Check(!b.Capture&&!b.SearchActive,"losing native duel gives no prisoner");Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Hall);CrimeRecord.Shelter=new();GwpCitySearchBehavior.Pump();Check(!b.SearchActive,"target no longer there aborts without spawning clone");
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Hall);b.WonHall();h.IsDead=true;GwpCitySearchBehavior.Pump();Check(b.Returning&&b.SearchActive,"dead target does not erase escape requirement");b.FinishSearch(true);Check(!h.IsPrisoner,"dead target never becomes prisoner");Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Escape);b.WonHall();b.MarkSearchWar();b.EscapeCleared=true;b=Reload(b);Hero.MainHero.IsWounded=true;GwpCitySearchBehavior.Pump();Check(b.SearchActive&&b.SearchWar&&b.EscapeCleared,"war and cleared escape wave survive reload with wounded player");Mission.Current!.Close();b.FinishSearch(false);
  b=New(out s,out h);b.Start(s,h,r);Campaign.Current.ConversationManager.IsConversationInProgress=true;opens=GwpCitySearchMission.Opens;GwpCitySearchBehavior.Pump();Check(opens==GwpCitySearchMission.Opens,"conversation must close before opening");Campaign.Current.ConversationManager.IsConversationInProgress=false;
  b.PauseSearch();Settlement.CurrentSettlement=null;GwpCitySearchBehavior.Pump();Check(!b.SearchActive,"leaving settlement outside search abandons without capture");
  b=New(out s,out h);b.Start(s,h,r);b.PauseSearch();b.PeacefulCapture();CrimeRecord.Shelter=new Settlement();b.FinishSearch(true);Check(!h.IsPrisoner,"paused search cannot capture target who has left settlement");
  b=New(out s,out h);b.Start(s,h,r);b.PauseSearch();PartyBase.MainParty.MemberRoster=TroopRoster.CreateDummyTroopRoster();PartyBase.MainParty.MemberRoster.AddToCounts(bow,1);b.QueuePlace(GwpCitySearchPlace.Hall);GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Last.Total==1&&b.SavedHealth("bow",0)==-1,"paused search cannot spawn troops transferred out of main party");Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Hall);b.PauseSearch();b.AgreeDuel();b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();
  Check(CampaignMission.PreviousAtArenaOpen==null,"hall-to-arena menu shortcut must not claim a nonexistent door");CampaignMission.Initialize();CampaignMission.Callback!(CharacterObject.PlayerCharacter);Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Hall);b.PauseSearch();Check(b.ClaimEntryWave()&&!b.ClaimEntryWave(),"entry wave can only be claimed once");b.RecordEnemyLoss("guard|foot");b.MarkTargetDown(false);b.QueuePlace(GwpCitySearchPlace.Escape);b=Reload(b);
  Check(b.Returning&&b.HasLeftHall&&b.EnemyLossCount("guard|foot")==1&&!b.ClaimEntryWave(),"route, entry-wave and finite losses survive save/load together");
  cap=GwpVoluntarySurrender.Calls;b.FinishSearch(true);Check(h.IsPrisoner&&GwpVoluntarySurrender.Calls==cap+1&&b.LeavePending,"knocked-out target saved across load is taken once the player escapes; no body to carry");
  b=New(out s,out h);b.Start(s,h,r);b.MarkTargetDown(false);b=Reload(b);b.FinishSearch(false);Check(!h.IsPrisoner,"knocked-out target is not taken when the player does not escape");
  b=New(out s,out h);b.Start(s,h,r);h.IsDead=true;b.MarkTargetDown(true);b=Reload(b);var defeats=Campaign.Current.Bounty.Defeats;b.FinishSearch(false);Check(Campaign.Current.Bounty.Defeats==defeats,"killing the target without escaping does not complete the commission");
  b=New(out s,out h);b.Start(s,h,r);h.IsDead=true;b.MarkTargetDown(true);b=Reload(b);b=Reload(b);b.FinishSearch(true);Check(Campaign.Current.Bounty.Defeats==defeats+1&&!h.IsPrisoner,"killing the target and escaping records the existing defeat result without a dead prisoner (ruling 2026-10-22)");
  Console.WriteLine("CITY_SEARCH_FLOW_ASSERTIONS="+n);
 }
}}
'''


class CitySearchFlowTests(unittest.TestCase):
    def test_production_coordinator_roundtrip_and_all_routes(self):
        flow = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Flow.cs'))
        run(STUBS + flow + SCENARIOS)
        old_passage = flow.replace('settlement.LocationComplex.GetLocationWithId(_previousLocationId)', 'null')
        with self.assertRaisesRegex(AssertionError, 'hall uses native center-to-hall passage provenance'):
            run(STUBS + old_passage + SCENARIOS)
        old_capture = flow.replace('&& CrimeRecord.ShelterOf(target) == Settlement.CurrentSettlement', '')
        with self.assertRaisesRegex(AssertionError, 'paused search cannot capture target'):
            run(STUBS + old_capture + SCENARIOS)
        old_roster = flow.replace('if (!available.TryGetValue(character, out int count) || count <= 0) continue;', 'available.TryGetValue(character, out int count);')
        with self.assertRaisesRegex(AssertionError, 'paused search cannot spawn troops'):
            run(STUBS + old_roster + SCENARIOS)
        old_current = flow.replace('mission.AddMissionBehavior(new GwpCitySearchArenaReturn(this));',
                                   'Mission.Current.AddMissionBehavior(new GwpCitySearchArenaReturn(this));')
        with self.assertRaisesRegex(AssertionError, 'Object reference not set'):
            run(STUBS + old_current + SCENARIOS)
        old_arena_door = flow.replace('Campaign.Current.GameMenuManager.PreviousLocation = null;', '')
        with self.assertRaisesRegex(AssertionError, 'hall-to-arena menu shortcut must not claim a nonexistent door'):
            run(STUBS + old_arena_door + SCENARIOS)

    def test_production_scene_progression(self):
        mission = read('GwpCitySearchMission.cs')
        progress = read('GwpCitySearchMission.Progress.cs')
        flow = read('GwpCitySearchBehavior.Flow.cs')
        template = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity{
enum Stage{Entering,Questioned,Allowed,Leaving,Refused,Withdrawing,Fighting,FightOver,HallReady,HallTalking,Duel,Escaping,Failed,HallDone}
enum GwpCitySearchPlace{None,Entry,Hall,Arena,Escape,Street}
enum GwpCitySearchGate{Unchecked,Allowed,Leaving,Refused,Withdrawing,Cleared}
enum AgentState{None,Unconscious,Killed,Routed,Deleted}
class KillingBlow{}
class CharacterObject{public bool IsHero;public Hero? HeroObject;}
struct Vec3{public float X;public Vec3(float x){X=x;}public float Distance(Vec3 v)=>Math.Abs(X-v.X);}
class Team{public static Team Invalid=new();public bool Hostile;public void SetIsEnemyOf(Team other,bool b){Hostile=b;}}
struct WorldPosition{}
class Agent{public static Agent? Main;public CharacterObject Character=new();public float Health=100;public bool Active=true,IsHuman=true,Watch,Armed=true,IsRunningAway;public bool HasWeapon()=>Armed;public AgentState State;public Team Team=new();public Vec3 Position;
 public enum WatchState{Alarmed}public bool IsActive()=>Active;public void DisableScriptedMovement(){}public void SetTeam(Team t,bool b){Team=t;}public WorldPosition GetWorldPosition()=>new();public void SetScriptedPosition(ref WorldPosition p,bool b){}public void SetLookAgent(Agent a){}public void SetWatchState(WatchState s){}public T? GetComponent<T>() where T:class=>null;}
class CampaignAgentComponent{public Navigator AgentNavigator=new();}class Navigator{public T? GetBehaviorGroup<T>() where T:class=>null;}
class AlarmedBehaviorGroup{public bool DisableCalmDown;public void AddBehavior<T>(){}public void SetScriptedBehavior<T>(){}}class FightBehavior{}
class Hero{public static Hero MainHero=new();public object MapFaction=new();public CharacterObject CharacterObject=new();public bool IsDead;}
class Settlement{public bool IsTown;public object MapFaction=new();}
class PlayerBountyBehavior{public int Dead,Defeats;public void RecordCitySearchLosses(Hero h,int n){Dead+=n;}public void RecordCitySearchDefeat(Hero h){Defeats++;}}
class GwpCitySearchBehavior{public GwpCitySearchPlace SearchPlace;public bool SearchActive=true,War,Won,Peaceful,Escaped,Aborted,DuelAgreed,Returning,EscapeCleared,Paused,HallAccessible;public void MarkTargetDown(bool killed){Won=true;Returning=true;}public bool SearchWar=>War;public GwpCitySearchGate GateState;public bool HallResolved=>DuelAgreed||Returning;public List<(CharacterObject,float)> Saved=new();
 public void QueuePlace(GwpCitySearchPlace p){SearchPlace=p;}public void MarkSearchWar(){War=true;}public void WonHall(){Won=true;Returning=true;}public void PeacefulCapture(){Peaceful=true;Returning=true;}public void AgreeDuel(){DuelAgreed=true;}public void WithdrawFromHall(){Returning=true;}public void PauseSearch(){Paused=true;}public void FinishSearch(bool escaped){Escaped=escaped;Aborted=!escaped;SearchActive=false;}public void SaveSurvivors(IEnumerable<(CharacterObject,float)> s){Saved=s.ToList();}}
class Conversation{public bool IsConversationFlowActive;public Agent? OneToOneConversationAgent;}
class Campaign{public static Campaign Current=new();public Conversation ConversationManager=new();public PlayerBountyBehavior Bounty=new();public T GetCampaignBehavior<T>() where T:class=>Bounty as T;}
class MissionConversationLogic{public int Talks;public bool Disabled;public void StartConversation(Agent a,bool b){Talks++;Campaign.Current.ConversationManager.OneToOneConversationAgent=a;}public void DisableStartConversation(bool b){Disabled=b;}}
enum MissionMode{Duel,StartUp}
enum OrderType{FollowMe}
class Mission{public List<Agent> Agents=new();public Team PlayerTeam=new(),PlayerEnemyTeam=new();public bool Ended;public MissionConversationLogic Talk=new();public T GetMissionBehavior<T>() where T:class=>Talk as T;public void EndMission(){Ended=true;}public void SetMissionMode(MissionMode m,bool b){}}
class Frame{public Vec3 origin;}class GameEntity{public Frame GetGlobalFrame()=>new();}class PassageUsePoint{public GameEntity GameEntity=new();public string ToLocationId="";}
static class GwpTuning{public static class CitySearch{public const float GuardChallengeDistance=6,GateExitDistance=6,InwardTolerance=10,SecureTargetDistance=2;}}
static class SandBoxHelpers{public static class MissionHelper{public static void FollowAgent(Agent a,Agent target){a.IsRunningAway=false;}}}
class InformationMessage{public InformationMessage(string s){}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
static class GwpText{public static string Get(string s)=>s;}static class GwpTextCatalog{public const string gwp_cs_hall_notice="hall",gwp_cs_escape_notice="escape",gwp_cs_walk_out_notice="walkout",gwp_cs_walk_arena_notice="walkarena";}
static class GwpAiDiagnostics{public static void WriteCitySearch(string s,string d){}}
class Scene{public Stage _stage;public GwpCitySearchPlace _place;public bool _gateExitAvailable=true;public bool _transitioning,_warDeclared;public string? _pendingHallAction,_pendingFight;public Agent? _hallTarget;public Hero _target=new();public Settlement _settlement=new();public Vec3 _gate;public Mission Mission=new();public GwpCitySearchBehavior Search=new();public float _inwardLimit;public bool InHall=>_place==GwpCitySearchPlace.Hall;public Dictionary<Agent,CharacterObject> _ours=new();public Dictionary<CharacterObject,int> _killed=new();public Dictionary<Agent,Team> _spectators=new();
 public bool IsOurs(Agent a)=>a==Agent.Main||_ours.ContainsKey(a);bool IsWatch(Agent a)=>a.Watch;void DisablePlayerPassages(){}void EndFightIfDefendersDown(){}void Order(OrderType o){}
 bool _outsideExitRequested;void SaveCitySoldiers(){}public List<Agent> _defenders=new();
 void NoteCitySoldierRemoval(Agent a,AgentState s){}void BeginDuelWalk(string destination){}
 bool IsCityEnemyFighter(Agent a)=>a.Active&&!a.IsRunningAway&&a.IsHuman&&a.Armed&&a.Team==Mission.PlayerEnemyTeam;
 METHODS
 public void Begin()=>BeginSearchStage();public bool Tick()=>TickSearchProgress();public bool CanLeave()=>CanLeaveSearch();public void End()=>EndSearchScene();public void DuelRemoval(Agent a)=>NoteDuelRemoval(a);public void FightWon()=>OnSearchFightWon();
}
static class Program{static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}static Scene New(GwpCitySearchPlace p,Stage s){Agent.Main=new(){Position=new Vec3(50)};return new(){_place=p,_stage=s};}
static void Main(){
 var entry=New(GwpCitySearchPlace.Entry,Stage.Entering);Check(entry.CanLeave(),"peaceful entry can withdraw");var door=new PassageUsePoint{ToLocationId="lordshall"};
 entry.Search.GateState=GwpCitySearchGate.Allowed;entry.Search.HallAccessible=true;entry.Search.SearchPlace=GwpCitySearchPlace.Entry;entry.Begin();Check(entry._stage==Stage.Allowed,"resuming entry restores permission without repeat challenge");
 Campaign.Current.ConversationManager.IsConversationFlowActive=true;entry.TryUseSearchPassage(door,Agent.Main!);Check(entry._pendingHallAction=="enter_hall","permission granted inside dialogue opens hall door");entry._pendingHallAction=null;Campaign.Current.ConversationManager.IsConversationFlowActive=false;entry.Search.HallAccessible=false;
 Check(!entry.TryUseSearchPassage(door,new Agent()),"AI doors retain native handling");entry.TryUseSearchPassage(door,Agent.Main!);Check(entry._pendingHallAction==null,"hall locked until guard allows passage");
 foreach(var stage in new[]{Stage.Allowed,Stage.FightOver}){entry=New(GwpCitySearchPlace.Entry,stage);entry.Search.HallAccessible=true;entry.TryUseSearchPassage(door,Agent.Main!);Check(entry.Tick()&&entry._transitioning&&entry.Mission.Ended&&entry.Search.SearchPlace==GwpCitySearchPlace.Hall,"approved or victorious entry leads to hall with transition marked");}
 var war=New(GwpCitySearchPlace.Entry,Stage.FightOver);war._warDeclared=true;war.Search.War=true;Check(!war.CanLeave(),"winning a wave inside city cannot escape by Tab");
 war._stage=Stage.Fighting;war.TryUseSearchPassage(door,Agent.Main!);Check(war._pendingHallAction=="enter_hall","hostile player can break through hall door before killing all reserves");
 var retreat=New(GwpCitySearchPlace.Street,Stage.FightOver);retreat.Search.SearchPlace=GwpCitySearchPlace.Street;retreat.Search.GateState=GwpCitySearchGate.Cleared;retreat.Search.War=true;retreat.Begin();Check(retreat._pendingFight=="street","retreating before target capture preserves hostile street pursuit");
 var resolved=New(GwpCitySearchPlace.Hall,Stage.HallReady);resolved.Search.SearchPlace=GwpCitySearchPlace.Hall;resolved.Search.Returning=true;resolved.Search.War=true;resolved.Begin();Check(resolved._stage==Stage.HallDone&&resolved._pendingFight==null&&resolved._pendingHallAction==null,"return to resolved hall neither clones target nor restarts fight");
 var clear=New(GwpCitySearchPlace.Escape,Stage.Escaping);clear.Search.SearchPlace=GwpCitySearchPlace.Escape;clear.Search.War=true;clear.Search.EscapeCleared=true;clear.Begin();Check(clear._pendingFight==null,"resuming cleared escape wave does not restart combat");
 var hall=New(GwpCitySearchPlace.Hall,Stage.HallReady);hall._hallTarget=new(){Position=new Vec3(52)};hall.Tick();Check(hall._stage==Stage.HallTalking&&hall.Mission.Talk.Talks==1,"approaching actual target starts warrant dialogue");hall.Tick();Check(hall.Mission.Talk.Talks==1,"one hall conversation");Check(hall.CanLeave(),"calm hall permits native Tab pause");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallReady);hall._hallTarget=new(){Position=new Vec3(52)};hall._pendingFight="forced_hall";hall._warDeclared=true;hall.Tick();Check(hall.Mission.Talk.Talks==0&&hall._stage==Stage.HallReady,"forced hall fight cannot open peaceful dialogue even when target spawns beside player");
 var foreign=new Agent{Character=new CharacterObject{IsHero=true,HeroObject=new Hero()}};hall._hallTarget=foreign;Check(hall.IsDefender(foreign),"foreign faction target must fight with hall defenders");var guest=new Agent{Character=new CharacterObject{IsHero=true,HeroObject=new Hero()}};Check(!hall.IsDefender(guest),"other foreign guests are not turned into defenders");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="surrender";hall.Tick();Check(hall.Search.Peaceful&&!hall.Mission.Ended&&hall._stage==Stage.HallDone,"successful hall persuasion leaves player to walk out");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="duel";hall._settlement.IsTown=true;hall.Tick();Check(hall.Search.DuelAgreed&&!hall.Mission.Ended&&hall._stage==Stage.HallDone,"town duel agreement waits for walk to arena");hall.TryUseSearchPassage(new PassageUsePoint{ToLocationId="center"},Agent.Main!);hall.Tick();Check(hall.Search.SearchPlace==GwpCitySearchPlace.Street,"hall exit leads to street before arena");var street=New(GwpCitySearchPlace.Street,Stage.Allowed);street.Search.DuelAgreed=true;street.TryUseSearchPassage(new PassageUsePoint{ToLocationId="arena"},Agent.Main!);street.Tick();Check(street.Search.SearchPlace==GwpCitySearchPlace.Arena,"walking to arena starts agreed duel");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="duel";hall._hallTarget=new();var friend=new Agent();var guard=new Agent();var friendTeam=friend.Team;var guardTeam=guard.Team;hall.Mission.Agents.AddRange(new[]{Agent.Main!,hall._hallTarget,friend,guard});hall._ours[friend]=friend.Character;
 hall.Tick();Check(hall._stage==Stage.Duel&&friend.Team==Team.Invalid&&guard.Team==Team.Invalid,"castle duel leaves companions and guards as spectators");Check(hall._hallTarget.Team==hall.Mission.PlayerEnemyTeam&&hall.Mission.PlayerEnemyTeam.Hostile,"only duelists fight");
 Check(hall.GetAgentState(hall._hallTarget,1,out _)==AgentState.Unconscious,"castle duel cannot kill target");hall.DuelRemoval(hall._hallTarget);hall.Tick();Check(hall.Search.Won&&hall.Search.Returning&&!hall.Mission.Ended&&friend.Team==friendTeam&&guard.Team==guardTeam,"castle victory restores spectator ownership before leaving");
 hall=New(GwpCitySearchPlace.Hall,Stage.Duel);hall.DuelRemoval(Agent.Main!);hall.Tick();Check(hall.Search.Aborted&&!hall.Search.Won,"castle duel loss grants no arrest");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="melee";hall.Tick();Check(hall._pendingFight=="hall_melee","chosen brawl queues war after dialogue closes");
 hall=New(GwpCitySearchPlace.Hall,Stage.FightOver);hall.FightWon();hall.Tick();Check(hall.Search.Won&&hall.Search.Returning&&!hall.Mission.Ended,"hall brawl victory leaves player to walk out");
 hall=New(GwpCitySearchPlace.Hall,Stage.Fighting);hall.Search.War=true;hall._hallTarget=new(){Armed=false,Position=new Vec3(60),IsRunningAway=true};hall.FightWon();Check(!hall.Search.Won,"unarmed distant target cannot be captured merely by defeating guards");
 Agent.Main!.Position=new Vec3(59);var protector=new Agent{Position=new Vec3(60),Team=hall.Mission.PlayerEnemyTeam};hall._defenders.Add(protector);hall.Mission.Agents.Add(protector);hall.Tick();Check(!hall.Search.Won,"armed protector prevents automatic capture of civilian target");protector.Active=false;
 var supportGuard=new Agent{Position=new Vec3(60),Team=hall.Mission.PlayerEnemyTeam};hall.Mission.Agents.Add(supportGuard);hall.Tick();Check(!hall.Search.Won,"temporary enemy support also protects unarmed target");supportGuard.Active=false;
 hall.Tick();Check(hall.Search.Won&&!hall._hallTarget.IsRunningAway,"catching unarmed target starts escorted withdrawal");
 hall=New(GwpCitySearchPlace.Hall,Stage.Fighting);hall.Search.War=true;hall._hallTarget=new(){Active=false,State=AgentState.Unconscious,Position=new Vec3(50)};hall.Tick();Check(hall.Search.Won&&hall.Search.Returning,"downed target can be secured while distant reserves still arrive");
 hall=New(GwpCitySearchPlace.Hall,Stage.Fighting);hall.Search.War=true;hall._hallTarget=new(){Active=false,State=AgentState.Unconscious,Position=new Vec3(500)};hall.OnAgentRemoved(hall._hallTarget,Agent.Main!,AgentState.Unconscious,new());Check(hall.Search.Won&&hall.Search.Returning,"target knockout must be remembered before player leaves hall even when enemies remain");
 var hallDoor=new PassageUsePoint{ToLocationId="center"};hall.TryUseSearchPassage(hallDoor,Agent.Main!);Check(hall._pendingHallAction=="enter_street","downed target does not hold the player in the hall");
 var extraction=New(GwpCitySearchPlace.Escape,Stage.Escaping);extraction.Search.War=true;Agent.Main!.Position=extraction._gate;Check(extraction.CanLeave()&&extraction.Tick()&&extraction.Search.Escaped,"at the gate, Tab and the automatic exit both complete the escape without a body");
 hall=New(GwpCitySearchPlace.Hall,Stage.FightOver);hall._hallTarget=new(){Active=false,State=AgentState.Routed};hall.FightWon();Check(!hall.Search.Won&&hall.Search.Returning,"target who already fled is not captured remotely");
  var escape=New(GwpCitySearchPlace.Escape,Stage.Escaping);Check(escape.CanLeave(),"calm street permits Tab without completing escape");escape.Tick();Check(!escape.Mission.Ended,"must walk toward gate");Agent.Main!.Position=new Vec3(5);escape._pendingFight="escape";escape.Tick();Check(!escape.Mission.Ended,"guard wave starts before checking the exit even at gate");escape._pendingFight=null;escape.Tick();Check(escape.Search.Escaped&&escape.Mission.Ended,"actual gate arrival permits escape");
 var transfer=New(GwpCitySearchPlace.Entry,Stage.Allowed);var alive=new Agent{Health=13};var dead=new Agent{Active=false};transfer._ours[alive]=alive.Character;transfer._ours[dead]=dead.Character;transfer._transitioning=true;transfer._killed[dead.Character]=1;transfer.End();Check(transfer.Search.Saved.Count==1&&transfer.Search.Saved[0].Item2==13,"transition snapshots active survivors and their health only");Check(Campaign.Current.Bounty.Dead==1,"mission casualties enter bounty owner exactly once per scene");
 transfer=New(GwpCitySearchPlace.Hall,Stage.HallReady);transfer.End();Check(transfer.Search.Paused&&!transfer.Search.Aborted,"Tab preserves search and snapshots survivors");
  var failed=New(GwpCitySearchPlace.Hall,Stage.Fighting);failed.Search.FinishSearch(false);failed.FightWon();failed.Tick();Check(failed.Search.Aborted&&!failed.Search.Won&&!failed.Mission.Ended,"late ally victory cannot resurrect search after player fell");
 Console.WriteLine("CITY_SEARCH_PROGRESS_ASSERTIONS="+n);
}}}
'''
        names = ['BeginSearchStage', 'TryUseSearchPassage', 'CanUsePassage', 'TickSearchProgress', 'TransitionTo',
                 'OnSearchFightWon', 'StartHallDuel', 'RestoreSpectators', 'NoteDuelRemoval',
                 'CanLeaveSearch', 'EndSearchScene', 'GetAgentState', 'TrySecureHallTarget']
        bodies = '\n'.join(method(progress, name) for name in names)
        bodies += method(mission, 'IsDefender').replace('private ', 'public ')
        bodies += method(mission, 'OnAgentRemoved').replace('public override ', 'public ')
        run(template.replace('METHODS', bodies))
        controls = [
            ('target knockout must be remembered', bodies.replace('Search.MarkTargetDown(agentState == AgentState.Killed);', '')),
            ('Tab preserves search', bodies.replace('if (!_transitioning) Search.PauseSearch();', 'if (!_transitioning) Search.FinishSearch(false);')),
            ('permission granted inside dialogue', bodies.replace('if (_stage == Stage.Duel || _stage == Stage.Failed || _pendingFight != null || _pendingHallAction != null) return false;', 'if (!CanLeaveSearch()) return false;')),
            ('late ally victory', bodies.replace('if (!Search.SearchActive) return false;', '')
             .replace('if (!Search.SearchActive) return;', '')),
            ('forced hall fight', bodies.replace('&& !_warDeclared && _pendingFight == null', '')),
            ('foreign faction target must fight', bodies.replace('|| InHall && agent == _hallTarget', '')),
            ('guard wave starts before checking the exit', bodies.replace('&& _pendingFight == null && Agent.Main?.IsActive()', '&& Agent.Main?.IsActive()')),
            ('retreating before target capture', bodies.replace('if (_warDeclared) _pendingFight = "street";', '')),
            ('temporary enemy support also protects', bodies.replace('Mission.Agents.Any(agent => IsCityEnemyFighter(agent)', '_defenders.Any(agent => IsCityEnemyFighter(agent)')),
        ]
        for failure, old in controls:
            with self.subTest(negative_control=failure), self.assertRaisesRegex(AssertionError, failure):
                run(template.replace('METHODS', old))

    def test_native_companion_spawn_negative_control(self):
        """The installed location callback used to spawn followers on top of the selected roster."""
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        native = subprocess.check_output(['ilspycmd', '-t', 'SandBox.Missions.MissionLogics.MissionLocationLogic',
            str(game / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        callback = method(native, 'OnPlayerAgentSpawned').replace('private ', 'public ')
        patch = block(read('GwpCitySearchMission.Progress.cs'), r'internal static class GwpCitySearchCompanionsPatch')
        prefix = method(patch, 'Prefix').replace('private ', 'public ')
        run(r'''
using System;
class GwpCitySearchMissionController{public static GwpCitySearchMissionController? Current;}
class NativeLocation{public int Spawned;bool _noHorsesforCharactersAccompanyingPlayer;
 void SpawnCharactersAccompanyingPlayer(bool noHorse){Spawned++;}
 CALLBACK}
static class Patch{PREFIX}
static class Program{static void Main(){var location=new NativeLocation();
 location.OnPlayerAgentSpawned();if(location.Spawned!=1)throw new Exception("old native callback must reproduce extra follower spawn");
 GwpCitySearchMissionController.Current=new();if(Patch.Prefix())location.OnPlayerAgentSpawned();
 if(location.Spawned!=1)throw new Exception("search must use selected roster exclusively");
 GwpCitySearchMissionController.Current=null;if(Patch.Prefix())location.OnPlayerAgentSpawned();
 if(location.Spawned!=2)throw new Exception("ordinary town and castle visits retain native followers");
 Console.WriteLine("CITY_SEARCH_COMPANION_NEGATIVE_CONTROL=PASS");}}
'''.replace('CALLBACK', callback).replace('PREFIX', prefix))

    def test_hall_dialogue_choices_and_exits(self):
        hall = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Hall.cs'))
        keys = set(re.findall(r'GwpTextCatalog\.(\w+)', hall))
        template = r'''
using System;using System.Collections.Generic;
namespace GreyWardenPolicePurity{
enum PersuasionArgumentStrength{ExtremelyHard=-3,VeryHard,Hard,Normal,Easy,VeryEasy,ExtremelyEasy}
enum PersuasionDifficulty{Medium,Hard}enum TraitEffect{Positive,Negative}
static class DefaultSkills{public static object Leadership=new(),Charm=new(),Roguery=new(),Trade=new(),Tactics=new(),Steward=new();}
static class DefaultTraits{public static object Honor=new(),Mercy=new(),Calculating=new(),Generosity=new(),Valor=new();}
class TextObject{public string Key;public TextObject(string k){Key=k;}}
static class GwpTextCatalog{KEYS}
static class GwpText{public static string Get(string k)=>k;public static TextObject Create(string k)=>new(k);}
static class MBTextManager{public static Dictionary<string,TextObject> Text=new();public static void SetTextVariable(string k,TextObject v){Text[k]=v;}}
class Hero{public static Hero MainHero=new();public int Relation,Trait;public int GetRelation(Hero h)=>Relation;public int GetTraitLevel(object t)=>Trait;}
class Faction{public float MainHeroCrimeRating;}class Settlement{public Faction MapFaction=new();}
class GwpCitySearchMissionController{public bool IsHallTalk=true;public string Action="";public Settlement Settlement=new();public void LeaveHall(){Action="leave";}public void AcceptHallSurrender(){Action="surrender";}public void ChooseHallCombat(bool duel){Action=duel?"duel":"melee";}}
class PersuasionTask{public PersuasionArgumentStrength Strength;}
class GwpPersuasionRound{public int Status;public int TaskCount;public PersuasionArgumentStrength Strength;public GwpPersuasionRound(string s){}
 public bool IsSatisfied=>Status==1;public bool IsFailed=>Status>=2;
 public void Begin(IEnumerable<PersuasionTask> tasks,PersuasionDifficulty d){Status=0;TaskCount=0;foreach(var t in tasks){TaskCount++;Strength=t.Strength;}}
 public void End(){Status=3;}public bool PrepareReservation()=>Status==0;public bool PrepareReaction(string a,string b,string c)=>true;
 public void RegisterArguments(CampaignGameStarter s,string id,string input,string output){}
 public sealed record Argument(object Skill,object Trait,TraitEffect Effect,bool GivesCriticalSuccess,TextObject Line);
 public static PersuasionTask Task(TextObject t,PersuasionArgumentStrength strength,TextObject immediate,TextObject final,TextObject later,params Argument[] args)=>new(){Strength=strength};}
class CampaignGameStarter{public class Line{public Func<bool>? Condition;public Action? Consequence;public string Input,Output;public int Priority;}
 public Dictionary<string,Line> Lines=new();
 public void AddDialogLine(string id,string input,string output,string text,Func<bool>? cond,Action? cons,int priority=100){Lines[id]=new(){Input=input,Output=output,Condition=cond,Consequence=cons,Priority=priority};}
 public void AddPlayerLine(string id,string input,string output,string text,Func<bool>? cond,Action? cons){AddDialogLine(id,input,output,text,cond,cons);}
 public void Run(string id){var line=Lines[id];if(line.Condition?.Invoke()==false)throw new Exception("unavailable line: "+id);line.Consequence?.Invoke();}}
static class MBRandom{public static float Roll;public static int Calls;public static float RandomFloat{get{Calls++;return Roll;}}}
static class GwpFieldArrestBehavior{public static int CurrentStandingTier()=>0;}
static class GwpTuning{public static class CitySearch{public const float RelationStep=0.002f,CrimeStep=0.004f,HallPenalty=0.1f,DuelBaseChance=0.5f,DuelTraitStep=0.1f;}}
partial class GwpCitySearchBehavior{public static GwpCitySearchMissionController Scene=new();public Hero SearchTarget=new();
 static PersuasionArgumentStrength GuardStrength(int tier)=>PersuasionArgumentStrength.Normal;
 public CampaignGameStarter Register(){var s=new CampaignGameStarter();RegisterHallDialogue(s);return s;}
 public GwpPersuasionRound Hall=>_hallRound;public GwpPersuasionRound Choice=>_choiceRound;
}
static class Program{static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static void Main(){
 var b=new GwpCitySearchBehavior();var s=b.Register();s.Run("gwp_cs_hall_start");s.Run("gwp_cs_hall_warrant");Check(b.Hall.TaskCount==2&&b.Hall.Strength<PersuasionArgumentStrength.Normal,"hall has two harder concerns");
 Check(s.Lines["gwp_cs_hall_reservation"].Condition!(),"ongoing persuasion has an argument exit");b.Hall.Status=1;s.Run("gwp_cs_hall_surrender");Check(GwpCitySearchBehavior.Scene.Action=="surrender","successful persuasion immediately queues surrender");
 foreach(bool opponentDuel in new[]{false,true})foreach(bool playerDuel in new[]{false,true})foreach(bool convince in new[]{false,true}){
  GwpCitySearchBehavior.Scene=new();b=new();s=b.Register();s.Run("gwp_cs_hall_warrant");b.Hall.Status=2;MBRandom.Roll=opponentDuel?0:0.99f;MBRandom.Calls=0;
  Check(s.Lines["gwp_cs_hall_refuse"].Condition!(),"failed hall persuasion has refusal exit");s.Lines["gwp_cs_hall_refuse"].Condition!();Check(MBRandom.Calls==1,"NPC choice does not reroll on UI refresh");
  Check(MBTextManager.Text["GWP_CS_OPPONENT_CHOICE"].Key==(opponentDuel?GwpTextCatalog.gwp_cs_opponent_duel:GwpTextCatalog.gwp_cs_opponent_melee),"NPC announces its own choice first");
  s.Run("gwp_cs_hall_refuse");s.Run(playerDuel?"gwp_cs_hall_duel":"gwp_cs_hall_melee");
  if(playerDuel==opponentDuel){s.Run("gwp_cs_choice_agreed");Check(GwpCitySearchBehavior.Scene.Action==(playerDuel?"duel":"melee"),"matching choices begin immediately");}
  else{Check(b.Choice.TaskCount==1&&s.Lines["gwp_cs_choice_reservation"].Condition!(),"disagreement opens a separate persuasion round");b.Choice.Status=convince?1:2;s.Run(convince?"gwp_cs_choice_won":"gwp_cs_choice_lost");Check(GwpCitySearchBehavior.Scene.Action==((convince?playerDuel:opponentDuel)?"duel":"melee"),"disagreement outcome obeys winner's choice");}
 }
 s.Run("gwp_cs_hall_leave");Check(GwpCitySearchBehavior.Scene.Action=="leave","initial hall dialogue also has a withdrawal exit");
 Console.WriteLine("CITY_SEARCH_DIALOGUE_ASSERTIONS="+n);
}}}
'''
        constants = '\n'.join(f'public const string {key}="{key}";' for key in sorted(keys))
        run(template.replace('KEYS', constants) + hall)


if __name__ == '__main__':
    unittest.main()
