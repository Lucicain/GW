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
class Location{public string GetSceneName(int n)=>"arena";}
class Locations{public Location GetLocationWithId(string s)=>new();}
class Settlement{public static Settlement? CurrentSettlement;public string StringId="town";public Town Town=new();public Locations LocationComplex=new();}
class TroopRosterElement{public CharacterObject Character;public int Number,WoundedNumber;}
class TroopRoster{List<TroopRosterElement> e=new();public static TroopRoster CreateDummyTroopRoster()=>new();public IEnumerable<TroopRosterElement> GetTroopRoster()=>e;public int Total=>e.Sum(x=>x.Number-x.WoundedNumber);
 public void AddToCounts(CharacterObject c,int n,int wounded=0){var v=e.FirstOrDefault(x=>x.Character==c);if(v==null){v=new(){Character=c};e.Add(v);}v.Number+=n;v.WoundedNumber+=wounded;}}
class PartyBase{public static PartyBase MainParty=new();}
static class CrimeRecord{public static Settlement? Shelter;public static Settlement? ShelterOf(Hero h)=>h.IsDead||h.IsPrisoner?null:Shelter;}
static class GwpVoluntarySurrender{public static int Calls;public static bool Allowed=true;public static bool TryCapture(PartyBase p,Hero h){Calls++;if(!Allowed)return false;h.IsPrisoner=true;return true;}}
class PlayerBountyBehavior{public int Registered;public bool RegisterCaptureInCustody(){Registered++;return true;}}
class Conversation{public bool IsConversationInProgress;}
class MenuManager{public Location? NextLocation,PreviousLocation;}
class Campaign{public static Campaign Current=new();public GwpCitySearchBehavior Search=new();public PlayerBountyBehavior Bounty=new();public Conversation ConversationManager=new();public MenuManager GameMenuManager=new();public T GetCampaignBehavior<T>() where T:class=>(typeof(T)==typeof(GwpCitySearchBehavior)?Search:Bounty as object) as T;}
class Game{public static Game Current=new();public object GameType=Campaign.Current;}
class MapState{}
class GameStateManager{public static GameStateManager Current=new();public object ActiveState=new MapState();}
static class PlayerEncounter{public static object? Current=new();public static int Leaves,Finishes;public static void LeaveSettlement(){Leaves++;Settlement.CurrentSettlement=null;}public static void Finish(){Finishes++;Current=null;}}
class MissionLogic{public Mission Mission=>Mission.Current!;public virtual void OnMissionTick(float dt){}protected virtual void OnEndMission(){}public void Close()=>OnEndMission();}
class Mission{public static Mission? Current;public bool Ended;public List<MissionLogic> Behaviors=new();public void AddMissionBehavior(MissionLogic l)=>Behaviors.Add(l);public void EndMission(){Ended=true;}public void Close(){foreach(var b in Behaviors)b.Close();Current=null;}}
static class CampaignMission{public static Action<CharacterObject>? Callback;public static int Arenas;public static void OpenArenaDuelMission(string s,Location l,CharacterObject c,bool civilian,bool horse,Action<CharacterObject> cb,float hp){Callback=cb;Arenas++;Mission.Current=new();}}
static class GwpCitySearchMission{public static int Opens;public static TroopRoster Last;public static GwpCitySearchPlace Place;public static void Open(Settlement s,Hero h,TroopRoster r){Opens++;Last=r;Place=Campaign.Current.Search.SearchPlace;Mission.Current=new();}}
static class GwpAiDiagnostics{public static void WriteCitySearch(string s,string d){}}
partial class GwpCitySearchBehavior{
 static PlayerBountyBehavior Bounty=>Campaign.Current.Bounty;
 public void Start(Settlement s,Hero h,TroopRoster r)=>BeginSearch(s,h,r);
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
  GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Opens==1&&GwpCitySearchMission.Last.Total==3,"entry has exactly three healthy selected followers");
  b.SaveSurvivors(new[]{(foot,37f),(bow,12f)});b.MarkSearchWar();b.QueuePlace(GwpCitySearchPlace.Hall);
  var opens=GwpCitySearchMission.Opens;GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Opens==opens,"cannot open hall over current mission");
  Mission.Current!.Close();b=Reload(b);Check(b.SearchPlace==GwpCitySearchPlace.Hall&&b.SearchWar,"route and war survive actual SyncSearch round trip");
  Check(b.SavedHealth("foot",0)==37&&b.SavedHealth("bow",0)==12,"individual wounds persist across save/load");
  Campaign.Current.GameMenuManager.NextLocation=new();Campaign.Current.GameMenuManager.PreviousLocation=new();
  GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Hall&&GwpCitySearchMission.Last.Total==2,"hall receives only survivors");
  Check(Campaign.Current.GameMenuManager.NextLocation==null&&Campaign.Current.GameMenuManager.PreviousLocation==null,"native automatic passage cannot steal transition");
  b.WonHall();Check(!h.IsPrisoner&&b.Capture&&b.SearchPlace==GwpCitySearchPlace.Escape,"hall win does not teleport prisoner to party");
  Mission.Current!.Close();b=Reload(b);GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Escape&&b.Capture,"escape obligation persists");
  Check(Campaign.Current.GameMenuManager.PreviousLocation!=null,"escape uses native hall passage frame, including castles without town guard spawn tag");
  var cap=GwpVoluntarySurrender.Calls;var reg=Campaign.Current.Bounty.Registered;b.FinishSearch(false);
  Check(!h.IsPrisoner&&GwpVoluntarySurrender.Calls==cap&&Campaign.Current.Bounty.Registered==reg,"losing escape grants no prisoner or capture credit");
  Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.WonHall();b=Reload(b);b.FinishSearch(true);
  Check(h.IsPrisoner&&Campaign.Current.Bounty.Registered==reg+1&&!b.SearchActive,"escape captures via existing owner exactly once");
  var leaves=PlayerEncounter.Leaves;Check(b.LeavePending&&leaves==PlayerEncounter.Leaves,"settlement exit is deferred until mission ends");
  Mission.Current=new();GwpCitySearchBehavior.Pump();Check(PlayerEncounter.Leaves==leaves,"no settlement teardown inside scene");Mission.Current.Close();b=Reload(b);GwpCitySearchBehavior.Pump();
  Check(PlayerEncounter.Leaves==leaves+1&&PlayerEncounter.Finishes>0,"saved exit completes on map");GwpCitySearchBehavior.Pump();Check(PlayerEncounter.Leaves==leaves+1,"exit is idempotent");
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();Check(CampaignMission.Arenas==1,"town duel opens native arena");
  CampaignMission.Callback!(CharacterObject.PlayerCharacter);Check(b.Capture&&!h.IsPrisoner,"native arena win queues capture for escape");
  Mission.Current!.Behaviors[0].OnMissionTick(0);Check(Mission.Current.Ended,"arena return occurs on task frame");Mission.Current.Close();GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Escape,"arena returns through center");Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Arena);GwpCitySearchBehavior.Pump();CampaignMission.Callback!(h.CharacterObject);Check(!b.Capture,"losing native duel gives no prisoner");Mission.Current!.Close();b.FinishSearch(false);
  b=New(out s,out h);b.Start(s,h,r);b.QueuePlace(GwpCitySearchPlace.Hall);CrimeRecord.Shelter=new();GwpCitySearchBehavior.Pump();Check(!b.SearchActive,"target no longer there aborts without spawning clone");
  b=New(out s,out h);b.Start(s,h,r);b.WonHall();h.IsDead=true;GwpCitySearchBehavior.Pump();Check(GwpCitySearchMission.Place==GwpCitySearchPlace.Escape,"dead target does not erase escape requirement");b.FinishSearch(true);Check(!h.IsPrisoner,"dead target never becomes a prisoner");Mission.Current!.Close();
  b=New(out s,out h);b.Start(s,h,r);b.WonHall();Hero.MainHero.IsWounded=true;GwpCitySearchBehavior.Pump();Check(b.SearchActive&&!h.IsPrisoner&&GwpCitySearchMission.Place==GwpCitySearchPlace.Escape,"living but wounded player still has to escape at actual remaining health");Mission.Current!.Close();b.FinishSearch(false);
  b=New(out s,out h);b.Start(s,h,r);Campaign.Current.ConversationManager.IsConversationInProgress=true;opens=GwpCitySearchMission.Opens;GwpCitySearchBehavior.Pump();Check(opens==GwpCitySearchMission.Opens,"conversation must close before opening");Campaign.Current.ConversationManager.IsConversationInProgress=false;
  b.PeacefulCapture();Check(h.IsPrisoner&&!b.SearchActive,"peaceful surrender uses same capture owner");
  Console.WriteLine("CITY_SEARCH_FLOW_ASSERTIONS="+n);
 }
}}
'''


class CitySearchFlowTests(unittest.TestCase):
    def test_production_coordinator_roundtrip_and_all_routes(self):
        flow = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Flow.cs'))
        run(STUBS + flow + SCENARIOS)
        old_passage = re.sub(r'Campaign.Current.GameMenuManager.PreviousLocation = SearchPlace == GwpCitySearchPlace.Escape\s*\? settlement.LocationComplex.GetLocationWithId\("lordshall"\) : null;',
                            'Campaign.Current.GameMenuManager.PreviousLocation = null;', flow)
        with self.assertRaisesRegex(AssertionError, 'escape uses native hall passage frame'):
            run(STUBS + old_passage + SCENARIOS)

    def test_production_scene_progression(self):
        mission = read('GwpCitySearchMission.cs')
        progress = read('GwpCitySearchMission.Progress.cs')
        flow = read('GwpCitySearchBehavior.Flow.cs')
        template = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity{
enum Stage{Entering,Questioned,Allowed,Leaving,Refused,Withdrawing,Fighting,FightOver,HallReady,HallTalking,Duel,Escaping}
enum GwpCitySearchPlace{None,Entry,Hall,Arena,Escape}
enum AgentState{None,Unconscious,Killed}
class CharacterObject{public bool IsHero;public Hero? HeroObject;}
struct Vec3{public float X;public Vec3(float x){X=x;}public float Distance(Vec3 v)=>Math.Abs(X-v.X);}
class Team{public static Team Invalid=new();public bool Hostile;public void SetIsEnemyOf(Team other,bool b){Hostile=b;}}
struct WorldPosition{}
class Agent{public static Agent? Main;public CharacterObject Character=new();public float Health=100;public bool Active=true,IsHuman=true,Watch;public Team Team=new();public Vec3 Position;
 public enum WatchState{Alarmed}public bool IsActive()=>Active;public void DisableScriptedMovement(){}public void SetTeam(Team t,bool b){Team=t;}public WorldPosition GetWorldPosition()=>new();public void SetScriptedPosition(ref WorldPosition p,bool b){}public void SetLookAgent(Agent a){}public void SetWatchState(WatchState s){}public T? GetComponent<T>() where T:class=>null;}
class CampaignAgentComponent{public Navigator AgentNavigator=new();}class Navigator{public T? GetBehaviorGroup<T>() where T:class=>null;}
class AlarmedBehaviorGroup{public bool DisableCalmDown;public void AddBehavior<T>(){}public void SetScriptedBehavior<T>(){}}class FightBehavior{}
class Hero{public static Hero MainHero=new();public object MapFaction=new();public CharacterObject CharacterObject=new();public bool IsDead;}
class Settlement{public bool IsTown;public object MapFaction=new();}
class PlayerBountyBehavior{public int Dead,Defeats;public void RecordCitySearchLosses(Hero h,int n){Dead+=n;}public void RecordCitySearchDefeat(Hero h){Defeats++;}}
class GwpCitySearchBehavior{public GwpCitySearchPlace SearchPlace;public bool SearchActive=true,War,Won,Peaceful,Escaped,Aborted;public List<(CharacterObject,float)> Saved=new();
 public void QueuePlace(GwpCitySearchPlace p){SearchPlace=p;}public void MarkSearchWar(){War=true;}public void WonHall(){Won=true;SearchPlace=GwpCitySearchPlace.Escape;}public void PeacefulCapture(){Peaceful=true;SearchActive=false;}public void FinishSearch(bool escaped){Escaped=escaped;Aborted=!escaped;SearchActive=false;}public void SaveSurvivors(IEnumerable<(CharacterObject,float)> s){Saved=s.ToList();}}
class Conversation{public bool IsConversationFlowActive;public Agent? OneToOneConversationAgent;}
class Campaign{public static Campaign Current=new();public Conversation ConversationManager=new();public PlayerBountyBehavior Bounty=new();public T GetCampaignBehavior<T>() where T:class=>Bounty as T;}
class MissionConversationLogic{public int Talks;public bool Disabled;public void StartConversation(Agent a,bool b){Talks++;Campaign.Current.ConversationManager.OneToOneConversationAgent=a;}public void DisableStartConversation(bool b){Disabled=b;}}
enum MissionMode{Duel}
class Mission{public List<Agent> Agents=new();public Team PlayerTeam=new(),PlayerEnemyTeam=new();public bool Ended;public MissionConversationLogic Talk=new();public T GetMissionBehavior<T>() where T:class=>Talk as T;public void EndMission(){Ended=true;}public void SetMissionMode(MissionMode m,bool b){}}
class PassageUsePoint{public string ToLocationId="";}
static class GwpTuning{public static class CitySearch{public const float GuardChallengeDistance=6,GateExitDistance=6;}}
class InformationMessage{public InformationMessage(string s){}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
static class GwpText{public static string Get(string s)=>s;}static class GwpTextCatalog{public const string gwp_cs_hall_notice="hall",gwp_cs_escape_notice="escape";}
static class GwpAiDiagnostics{public static void WriteCitySearch(string s,string d){}}
class Scene{public Stage _stage;public GwpCitySearchPlace _place;public bool _transitioning,_warDeclared;public string? _pendingHallAction,_pendingFight;public Agent? _hallTarget;public Hero _target=new();public Settlement _settlement=new();public Vec3 _gate;public Mission Mission=new();public GwpCitySearchBehavior Search=new();public bool InHall=>_place==GwpCitySearchPlace.Hall;public Dictionary<Agent,CharacterObject> _ours=new();public Dictionary<CharacterObject,int> _killed=new();public Dictionary<Agent,Team> _spectators=new();
 public bool IsOurs(Agent a)=>a==Agent.Main||_ours.ContainsKey(a);bool IsWatch(Agent a)=>a.Watch;void DisablePlayerPassages(){}void EndFightIfDefendersDown(){}
 METHODS
 public bool Tick()=>TickSearchProgress();public bool CanLeave()=>CanLeaveSearch();public void End()=>EndSearchScene();public void DuelRemoval(Agent a)=>NoteDuelRemoval(a);public void FightWon()=>OnSearchFightWon();
}
static class Program{static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}static Scene New(GwpCitySearchPlace p,Stage s){Agent.Main=new(){Position=new Vec3(50)};return new(){_place=p,_stage=s};}
static void Main(){
 var entry=New(GwpCitySearchPlace.Entry,Stage.Entering);Check(entry.CanLeave(),"peaceful entry can withdraw");var door=new PassageUsePoint{ToLocationId="lordshall"};
 Check(!entry.TryUseSearchPassage(door,new Agent()),"AI doors retain native handling");entry.TryUseSearchPassage(door,Agent.Main!);Check(entry._pendingHallAction==null,"hall locked until guard allows passage");
 foreach(var stage in new[]{Stage.Allowed,Stage.FightOver}){entry=New(GwpCitySearchPlace.Entry,stage);entry.TryUseSearchPassage(door,Agent.Main!);Check(entry.Tick()&&entry._transitioning&&entry.Mission.Ended&&entry.Search.SearchPlace==GwpCitySearchPlace.Hall,"approved or victorious entry leads to hall with transition marked");}
 var war=New(GwpCitySearchPlace.Entry,Stage.FightOver);war._warDeclared=true;Check(!war.CanLeave(),"winning at gate cannot skip hall and escape");
 var hall=New(GwpCitySearchPlace.Hall,Stage.HallReady);hall._hallTarget=new(){Position=new Vec3(52)};hall.Tick();Check(hall._stage==Stage.HallTalking&&hall.Mission.Talk.Talks==1,"approaching actual target starts warrant dialogue");hall.Tick();Check(hall.Mission.Talk.Talks==1,"one hall conversation");Check(!hall.CanLeave(),"no Tab teleport from hall");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallReady);hall._hallTarget=new(){Position=new Vec3(52)};hall._pendingFight="forced_hall";hall._warDeclared=true;hall.Tick();Check(hall.Mission.Talk.Talks==0&&hall._stage==Stage.HallReady,"forced hall fight cannot open peaceful dialogue even when target spawns beside player");
 var foreign=new Agent{Character=new CharacterObject{IsHero=true,HeroObject=new Hero()}};hall._hallTarget=foreign;Check(hall.IsDefender(foreign),"foreign faction target must fight with hall defenders");var guest=new Agent{Character=new CharacterObject{IsHero=true,HeroObject=new Hero()}};Check(!hall.IsDefender(guest),"other foreign guests are not turned into defenders");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="surrender";hall.Tick();Check(hall.Search.Peaceful&&hall.Mission.Ended,"successful hall persuasion takes captive and ends scene");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="duel";hall._settlement.IsTown=true;hall.Tick();Check(hall.Search.SearchPlace==GwpCitySearchPlace.Arena,"town duel transfers to arena");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="duel";hall._hallTarget=new();var friend=new Agent();var guard=new Agent();var friendTeam=friend.Team;var guardTeam=guard.Team;hall.Mission.Agents.AddRange(new[]{Agent.Main!,hall._hallTarget,friend,guard});hall._ours[friend]=friend.Character;
 hall.Tick();Check(hall._stage==Stage.Duel&&friend.Team==Team.Invalid&&guard.Team==Team.Invalid,"castle duel leaves companions and guards as spectators");Check(hall._hallTarget.Team==hall.Mission.PlayerEnemyTeam&&hall.Mission.PlayerEnemyTeam.Hostile,"only duelists fight");
 Check(hall.GetAgentState(hall._hallTarget,1,out _)==AgentState.Unconscious,"castle duel cannot kill target");hall.DuelRemoval(hall._hallTarget);hall.Tick();Check(hall.Search.Won&&hall.Search.SearchPlace==GwpCitySearchPlace.Escape&&friend.Team==friendTeam&&guard.Team==guardTeam,"castle victory restores spectator ownership before leaving");
 hall=New(GwpCitySearchPlace.Hall,Stage.Duel);hall.DuelRemoval(Agent.Main!);hall.Tick();Check(hall.Search.Aborted&&!hall.Search.Won,"castle duel loss grants no arrest");
 hall=New(GwpCitySearchPlace.Hall,Stage.HallTalking);hall._pendingHallAction="melee";hall.Tick();Check(hall._pendingFight=="hall_melee","chosen brawl queues war after dialogue closes");
 hall=New(GwpCitySearchPlace.Hall,Stage.FightOver);hall.FightWon();hall.Tick();Check(hall.Search.Won&&hall.Search.SearchPlace==GwpCitySearchPlace.Escape,"hall brawl victory must still fight out");
  var escape=New(GwpCitySearchPlace.Escape,Stage.Escaping);Check(!escape.CanLeave(),"cannot Tab out of center");escape.Tick();Check(!escape.Mission.Ended,"must walk toward gate");Agent.Main!.Position=new Vec3(5);escape._pendingFight="escape";escape.Tick();Check(!escape.Mission.Ended,"guard wave starts before checking the exit even at gate");escape._pendingFight=null;escape.Tick();Check(escape.Search.Escaped&&escape.Mission.Ended,"actual gate arrival permits escape");
 var transfer=New(GwpCitySearchPlace.Entry,Stage.Allowed);var alive=new Agent{Health=13};var dead=new Agent{Active=false};transfer._ours[alive]=alive.Character;transfer._ours[dead]=dead.Character;transfer._transitioning=true;transfer._killed[dead.Character]=1;transfer.End();Check(transfer.Search.Saved.Count==1&&transfer.Search.Saved[0].Item2==13,"transition snapshots active survivors and their health only");Check(Campaign.Current.Bounty.Dead==1,"mission casualties enter bounty owner exactly once per scene");
 transfer=New(GwpCitySearchPlace.Hall,Stage.Fighting);transfer.End();Check(transfer.Search.Aborted,"unplanned mission end never leaves a phantom pending arrest");
  var failed=New(GwpCitySearchPlace.Hall,Stage.Fighting);failed.Search.FinishSearch(false);failed.FightWon();failed.Tick();Check(failed.Search.Aborted&&!failed.Search.Won&&!failed.Mission.Ended,"late ally victory cannot resurrect search after player fell");
 Console.WriteLine("CITY_SEARCH_PROGRESS_ASSERTIONS="+n);
}}}
'''
        names = ['TryUseSearchPassage', 'CanUsePassage', 'TickSearchProgress', 'TransitionTo',
                 'OnSearchFightWon', 'StartHallDuel', 'RestoreSpectators', 'NoteDuelRemoval',
                 'CanLeaveSearch', 'EndSearchScene', 'GetAgentState']
        bodies = '\n'.join(method(progress, name) for name in names)
        bodies += method(mission, 'IsDefender').replace('private ', 'public ')
        run(template.replace('METHODS', bodies))
        controls = [
            ('late ally victory', bodies.replace('if (!Search.SearchActive) return false;', '')
             .replace('if (!Search.SearchActive) return;', '')),
            ('forced hall fight', bodies.replace('&& !_warDeclared && _pendingFight == null', '')),
            ('foreign faction target must fight', bodies.replace('IsWatch(agent) || InHall && agent == _hallTarget ||', 'IsWatch(agent) ||')),
            ('guard wave starts before checking the exit', bodies.replace('_place == GwpCitySearchPlace.Escape && _pendingFight == null', '_place == GwpCitySearchPlace.Escape')),
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
