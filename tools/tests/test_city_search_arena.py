"""Installed mission lifecycle and location adjacency regressions for arena entry.

Native scene creation is an explicit boundary. The native OpenNew ordering,
Mission.Initialize assignment and location constructor validation run offline.
"""
from pathlib import Path
import re
import subprocess
import unittest
import xml.etree.ElementTree as ET

from test_city_search import read
from test_city_search_flow import STUBS, run
from test_duty_banner import method

GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CitySearchArenaTests(unittest.TestCase):
    def test_installed_arena_entry_lifecycle_and_passages(self):
        def native(kind, assembly):
            return subprocess.check_output(['ilspycmd', '-t', kind, str(GAME / assembly)], text=True, encoding='utf-8-sig')

        state = native('TaleWorlds.MountAndBlade.MissionState', 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')
        location = native('SandBox.Missions.MissionLogics.MissionLocationLogic', 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')
        mission = native('TaleWorlds.MountAndBlade.Mission', 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')
        # Keep the installed validation, stopping before unrelated campaign event subscriptions.
        ctor = method(location, 'MissionLocationLogic')
        ctor = ctor[:ctor.index('CampaignEvents.LocationCharactersAreReadyToSpawnEvent')] + '}'
        ctor = re.sub(r'(?m)^\s*//IL_.*\n', '', ctor)
        ctor = ctor.replace('string specialPlayerTag = null', 'string? specialPlayerTag = null')
        initialize = method(mission, 'Initialize')
        # Real scene initialization stays a boundary; the Current assignment runs exactly as shipped.
        initialize = initialize[:initialize.index('MBAPI.IMBMission.InitializeMission')] + '}'
        graph = ET.parse(GAME / 'Modules/SandBox/ModuleData/location_complex_templates.xml').getroot()
        town = graph.find("LocationComplexTemplate[@id='town_complex']")
        edges = [(p.attrib['location_1'], p.attrib['location_2']) for p in town.findall('Passages/Passage')]
        self.assertIn(('center', 'arena'), edges)
        self.assertNotIn(('lordshall', 'arena'), edges)
        edge_code = '\n'.join(f'Link("{a}","{b}");' for a, b in edges)
        stubs = STUBS.replace('class Location{public string StringId="",SceneName="arena";',
                              'class Location{public List<Location> LocationsOfPassages=new();public string DoorName=>StringId;public string StringId="",SceneName="arena";')
        stubs = stubs.replace('class Locations{public bool ArenaAvailable=true;public string ArenaScene="arena";public Location? GetLocationWithId(string s)=>s=="arena"&&!ArenaAvailable?null:new(){StringId=s,SceneName=s=="arena"?ArenaScene:s};public Location? GetLocationOfCharacter(Hero h)=>null;public LocationCharacter GetLocationCharacterOfHero(Hero h)=>new();public void ChangeLocation(LocationCharacter c,Location a,Location b){}}', r'''
class Locations{Dictionary<string,Location> LocationsById=new();
 public Location? GetLocationOfCharacter(Hero h)=>null;public LocationCharacter GetLocationCharacterOfHero(Hero h)=>new();public void ChangeLocation(LocationCharacter c,Location a,Location b){}
 public Locations(){EDGES}
 void Link(string a,string b){var first=GetLocationWithId(a);var second=GetLocationWithId(b);first.LocationsOfPassages.Add(second);second.LocationsOfPassages.Add(first);}
 public Location GetLocationWithId(string id){if(!LocationsById.TryGetValue(id,out var value)){value=new(){StringId=id};LocationsById[id]=value;}return value;}}
'''.replace('EDGES', edge_code))
        stubs = stubs.replace('public Conversation ConversationManager=new();', 'public int GameMode=1;public Conversation ConversationManager=new();')
        stubs = stubs.replace('class Game{public static Game Current=new();public object GameType=Campaign.Current;}',
                              'class Game{public static Game Current=new();public object GameType=Campaign.Current;public GameStateManager GameStateManager=>GameStateManager.Current;public void OnMissionIsStarting(string name,MissionInitializerRecord rec){}}')
        stubs = stubs.replace('public object ActiveState=new MapState();',
                              'public object ActiveState=new MapState();public T CreateState<T>() where T:new()=>new();public void PushState(MissionState state){ActiveState=state;}')
        stubs = stubs.replace('class Mission{public static Mission? Current;', r'''
interface IMissionDeploymentPlan{}
class MissionDeploymentPlanningLogic:IMissionDeploymentPlan{}
class DefaultMissionDeploymentPlan:IMissionDeploymentPlan{public DefaultMissionDeploymentPlan(Mission m){}}
class Mission{public enum State{NewlyCreated,Initializing}public State CurrentState;public IMissionDeploymentPlan? _deploymentPlan;public MissionInitializerRecord InitializerRecord;
 public T? GetMissionBehavior<T>() where T:class=>null;
 INITIALIZE
 public static Mission? Current;'''.replace('INITIALIZE', initialize))
        start = stubs.index('static class CampaignMission{')
        end = stubs.index('static class GwpCitySearchMission{', start)
        stubs = stubs[:start] + r'''
class MissionAgentHandler{}
class MissionLocationLogic{Location? _previousLocation;Location _currentLocation;string? _playerSpecialSpawnTag;CTOR}
class MissionInitializerRecord{public string SceneLevels="";}
delegate IEnumerable<MissionLogic> InitializeMissionBehaviorsDelegate(Mission mission);
static class Debug{public static List<string> Assertions=new();public static void Print(string s){}public static void FailedAssert(string message,params object[] caller){Assertions.Add(message);}}
static class MBCommon{public enum GameType{SingleRecord,Single}public static GameType CurrentGameType;}
static class GameNetwork{public static bool IsClientOrReplay,IsServer;}
class MissionState{
 public static Mission? Created;
 protected static bool IsRecordingActive()=>false;
 // Engine construction boundary: the returned Mission is created but has not loaded a scene.
 protected Mission HandleOpenNew(string n,MissionInitializerRecord r,InitializeMissionBehaviorsDelegate handler,bool defaults,bool cleanup){Created=new();foreach(var b in handler(Created))Created.AddMissionBehavior(b);return Created;}
 OPEN_NEW
}
static class CampaignMission{public static Action<CharacterObject>? Callback;
 public static Mission OpenArenaDuelMission(string s,Location l,CharacterObject c,bool civilian,bool horse,Action<CharacterObject> cb,float hp){Callback=cb;return MissionState.OpenNew("ArenaDuelMission",new(),m=>{new MissionLocationLogic(l);return Array.Empty<MissionLogic>();});}}
'''.replace('CTOR', ctor).replace('OPEN_NEW', method(state, 'OpenNew')) + stubs[end:]
        flow = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Flow.cs'))
        scenario = r'''
namespace GreyWardenPolicePurity{
static class Program{static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static void Main(){
  foreach(bool onFoot in new[]{false,true}){
   Mission.Current=null;GameStateManager.Current.ActiveState=new MapState();var town=new Settlement();Settlement.CurrentSettlement=town;CrimeRecord.Shelter=town;var lord=new Hero("lord"+n);var b=new GwpCitySearchBehavior();Campaign.Current.Search=b;
   b.Start(town,lord,TroopRoster.CreateDummyTroopRoster());b.QueuePlace(GwpCitySearchPlace.Hall);b.PauseSearch();b.AgreeDuel();if(onFoot)b.QueuePlace(GwpCitySearchPlace.Street);b.QueuePlace(GwpCitySearchPlace.Arena);
   GwpCitySearchBehavior.Pump();Check(Debug.Assertions.Count==0,"no nonexistent hall-to-arena passage assertion");
   Check(Mission.Current==null&&MissionState.Created!=null,"native OpenNew returns before Mission.Initialize sets Current");
   Check(MissionState.Created!.Behaviors.Single() is GwpCitySearchArenaReturn,"return hook must attach to returned mission during loading");
   MissionState.Created.Initialize();Check(Mission.Current==MissionState.Created,"installed Initialize publishes the same mission instance");
   CampaignMission.Callback!(CharacterObject.PlayerCharacter);Check(b.Returning&&!lord.IsPrisoner,"duel result preserves escort obligation");Mission.Current!.Close();Check(!b.Pending&&b.SearchPlace==GwpCitySearchPlace.Escape,"player exit keeps route and pauses on map");
  }
  var prev=Settlement.CurrentSettlement!.LocationComplex.GetLocationWithId("lordshall");Campaign.Current.GameMenuManager.PreviousLocation=prev;new MissionLocationLogic(Settlement.CurrentSettlement.LocationComplex.GetLocationWithId("arena"));
  Check(Debug.Assertions.Single()=="No passage from lordshall to arena","installed constructor reproduces original popup for old hall-to-arena route");
  Console.WriteLine("CITY_SEARCH_ARENA_NATIVE_ASSERTIONS="+n);
 }
}}
'''
        run(stubs + flow + scenario)
        old_hook = flow.replace('mission.AddMissionBehavior(new GwpCitySearchArenaReturn(this));',
                                'Mission.Current.AddMissionBehavior(new GwpCitySearchArenaReturn(this));')
        with self.assertRaisesRegex(AssertionError, 'Object reference not set'):
            run(stubs + old_hook + scenario)
        old_route = flow.replace('Campaign.Current.GameMenuManager.PreviousLocation = null;', '')
        with self.assertRaisesRegex(AssertionError, 'no nonexistent hall-to-arena passage assertion'):
            run(stubs + old_route + scenario)


if __name__ == '__main__':
    unittest.main()
