"""Experiential search menu and installed hall spawn regression, without launching the game."""
from pathlib import Path
import re
import subprocess
import unittest
import xml.etree.ElementTree as ET

from test_city_search import read, block
from test_city_search_flow import STUBS, run
from test_duty_banner import method

GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


class CitySearchNativeFlowTests(unittest.TestCase):
    def test_search_submenu_choices_preserve_experience(self):
        menu = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Menu.cs'))
        flow = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Flow.cs'))
        keys = set(re.findall(r'GwpTextCatalog\.(\w+)', menu))
        stubs = STUBS.replace('public string StringId;public CharacterObject CharacterObject;',
                              'public string Name="target";public string StringId;public CharacterObject CharacterObject;')
        stubs = stubs.replace('public Town Town=new();', 'public bool IsTown=true,IsCastle;public Town Town=new();')
        stubs = stubs.replace('public int Registered;', 'public Hero? Target;public Hero? CitySearchTargetIn(Settlement? s)=>Target;public int Registered;')
        run(stubs + r'''
namespace GreyWardenPolicePurity{
class MenuCallbackArgs{public GameMenuOption.LeaveType optionLeaveType;public bool IsEnabled;public string? Tooltip;}
class GameMenuOption{public enum LeaveType{Mission,Leave,Submenu}}
static class MBTextManager{public static string Text="";public static void SetTextVariable(string k,string v){Text=v;}}
static class GwpText{public static string Create(string s,params object[] a)=>s;}
static class GwpTextCatalog{KEYS}
static class GameMenu{public static string Last="";public static void SwitchToMenu(string id){Last=id;}}
class CampaignGameStarter{
 public class Option{public Func<MenuCallbackArgs,bool> Cond=null!;public Action<MenuCallbackArgs> Run=null!;}
 public Dictionary<string,Option> Options=new();public Action<MenuCallbackArgs>? Init;
 public void AddGameMenu(string id,string text,Action<MenuCallbackArgs> init){Init=init;}
 public void AddGameMenuOption(string menu,string id,string text,Func<MenuCallbackArgs,bool> cond,Action<MenuCallbackArgs> run,bool isLeave=false){Options[id]=new(){Cond=cond,Run=run};}
 public bool Has(string id)=>Options[id].Cond(new());public void Choose(string id){if(!Has(id))throw new Exception("hidden option "+id);Options[id].Run(new());}
}
partial class GwpCitySearchBehavior{
 public int Selections;private static void SelectSearchTroops(MenuCallbackArgs a){Campaign.Current.Search.Selections++;}
 public CampaignGameStarter Menu(){var s=new CampaignGameStarter();RegisterSearchMenu(s);s.Init!(new());return s;}
}
static class Program{
 static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static void Main(){
  var b=Campaign.Current.Search;var town=new Settlement();Settlement.CurrentSettlement=town;var lord=new Hero("lord");Campaign.Current.Bounty.Target=lord;CrimeRecord.Shelter=town;
  var m=b.Menu();Check(m.Has("begin")&&!m.Has("hall")&&!m.Has("resume"),"first submenu offers physical gate entry only");m.Choose("begin");Check(b.Selections==1,"first submenu opens native troop selection");
  b.Start(town,lord,TroopRoster.CreateDummyTroopRoster());b.PauseSearch();m=b.Menu();Check(m.Has("resume")&&!m.Has("hall")&&!m.Has("arena"),"gate cannot be bypassed before negotiation");
  b.GateState=GwpCitySearchGate.Refused;Check(!m.Has("hall"),"refused guard cannot be bypassed by menu");
  b.GateState=GwpCitySearchGate.Allowed;m=b.Menu();Check(!m.Has("begin")&&m.Has("resume")&&m.Has("hall"),"after native Tab both walking and hall shortcut remain available");
  m.Choose("hall");Check(b.Pending&&b.SearchPlace==GwpCitySearchPlace.Hall,"hall shortcut queues the same troop-carrying mission");b.PauseSearch();
  b.AgreeDuel();m=b.Menu();Check(m.Has("resume")&&m.Has("street")&&m.Has("arena")&&!m.Has("hall"),"duel agreement offers hall walkout or arena shortcut");
  m.Choose("street");Check(b.SearchPlace==GwpCitySearchPlace.Street&&b.DuelAgreed,"street option preserves duel appointment");b.PauseSearch();
  b.QueuePlace(GwpCitySearchPlace.Hall);b.PauseSearch();b.WonHall();b.MarkSearchWar();m=b.Menu();Check(!m.Has("leave")&&!m.Has("back")&&m.Has("street")&&m.Has("resume"),"war cannot deliver captive through settlement menu shortcut");
  m.Choose("street");Check(b.SearchPlace==GwpCitySearchPlace.Escape&&!lord.IsPrisoner,"walkout still requires real escape");b.PauseSearch();
  var save=new Store();b.Sync(save);var reload=new GwpCitySearchBehavior();reload.Sync(new Store{Loading=true,Data=save.Data});Campaign.Current.Search=reload;m=reload.Menu();Check(m.Has("resume")&&!m.Has("leave")&&!reload.Pending,"saved war pause stays in submenu");
  reload.FinishSearch(false);b=new();Campaign.Current.Search=b;b.Start(town,lord,TroopRoster.CreateDummyTroopRoster());b.PauseSearch();b.PeacefulCapture();m=b.Menu();Check(m.Has("resume")&&m.Has("leave"),"peaceful escort can walk or use settlement exit");m.Choose("leave");Check(lord.IsPrisoner&&b.LeavePending,"peaceful menu exit captures through existing owner");
  Console.WriteLine("CITY_SEARCH_MENU_ASSERTIONS="+n);
 }
}}
'''.replace('KEYS', '\n'.join(f'public const string {key}="{key}";' for key in keys)) + flow + menu)

    def test_installed_hall_spawn_old_origin_new_real_spawnpoint(self):
        prefabs = ET.parse(GAME / 'Modules/Native/Prefabs/editor_spawnpoints.xml').getroot()
        prefab = prefabs.find(".//game_entity[@name='sp_player']")
        tags = [tag.attrib['name'] for tag in prefab.findall('tags/tag')]
        scene = ET.parse(GAME / 'Modules/SandBox/SceneObj/sturgia_castle_keep_a_l3_interior/scene.xscene').getroot()
        point = scene.find(".//game_entity[@prefab='sp_player']")
        position = point.find('transform').attrib['position']
        self.assertIn('spawnpoint_player', tags)
        self.assertNotIn('sp_player', tags)
        self.assertNotEqual('0.000, 0.000, 0.000', position)
        source = subprocess.check_output(['ilspycmd', '-t', 'SandBox.SandBoxHelpers',
            str(GAME / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        native = method(source, 'SpawnPlayer')
        overload = source[source.index('public static void SpawnPlayer(GameEntity'):]
        prefix = overload[overload.index('MatrixFrame val = MatrixFrame.Identity;'):
                          overload.index('CharacterObject playerCharacter =')]
        prefix = prefix.replace('((Mat3)(ref val.rotation))', 'val.rotation')
        location = subprocess.check_output(['ilspycmd', '-t', 'SandBox.Missions.MissionLogics.MissionLocationLogic',
            str(GAME / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        special = block(method(location, 'OnBeforePlayerAgentSpawn'), r'if \(!string.IsNullOrEmpty\(_playerSpecialSpawnTag\)\)')
        special = special.replace('((WeakGameEntity)(ref val))', 'val').replace(
            '((Mat3)(ref spawnPointFrame.rotation))', 'spawnPointFrame.rotation')
        coordinates = ','.join(v.strip() + 'f' for v in position.split(','))
        production_tag = re.search(r'const string HallSpawnTag = "([^"]+)"', read('GwpCitySearchMission.cs'))[1]
        run(r'''
using System;using System.Collections.Generic;using System.Linq;
struct Vec3{public float X,Y,Z;public Vec3(float x,float y,float z){X=x;Y=y;Z=z;}}
struct Mat3{public void OrthonormalizeAccordingToForwardAndKeepUpAsZAxis(){}}
struct MatrixFrame{public Vec3 origin;public Mat3 rotation;public static MatrixFrame Identity=>new();}
class GameEntity{public MatrixFrame Frame;public WeakGameEntity WeakEntity=>new(this);public MatrixFrame GetGlobalFrame()=>Frame;}
struct WeakGameEntity{GameEntity? Entity;public WeakGameEntity(GameEntity? entity){Entity=entity;}public static WeakGameEntity Invalid=>new();public bool IsValid=>Entity!=null;public MatrixFrame GetGlobalFrame()=>Entity!.Frame;}
class ScriptComponentBehavior{public WeakGameEntity GameEntity;}
class StandingPoint:ScriptComponentBehavior{}
class UsableMachine{public List<StandingPoint> StandingPoints=new();}
class MissionAgentHandler{public IEnumerable<UsableMachine> GetAllUsablePointsWithTag(string tag)=>Array.Empty<UsableMachine>();}
class Scene{public Dictionary<string,GameEntity> Tags=new();public GameEntity? FindEntityWithTag(string tag)=>Tags.GetValueOrDefault(tag);}
class Mission{public static Mission Current=new();public Scene Scene=new();}
class CampaignEventReceiver{public string _playerSpecialSpawnTag="";MissionAgentHandler? _missionAgentHandler;
 public void OnBeforePlayerAgentSpawn(ref MatrixFrame spawnPointFrame){SPECIAL}}
static class CampaignEventDispatcher{public static CampaignEventReceiver Instance=new();}
class Spawn{
 public static MatrixFrame Last;
 NATIVE
 public static void SpawnPlayer(GameEntity? spawnPosition,bool civilianEquipment=false,bool noHorses=false,bool noWeapon=false,bool wieldInitialWeapons=false){PREFIX Last=val;}
}
static class Program{static void Main(){
 Mission.Current.Scene.Tags["spawnpoint_player"]=new(){Frame=new(){origin=new Vec3(COORDINATES)}};
 CampaignEventDispatcher.Instance._playerSpecialSpawnTag="sp_player";
 Spawn.SpawnPlayer(spawnTag:"sp_player");if(Spawn.Last.origin.X!=0||Spawn.Last.origin.Y!=0||Spawn.Last.origin.Z!=0)throw new Exception("old tag must reproduce identity-frame origin even after native location callback");
 CampaignEventDispatcher.Instance._playerSpecialSpawnTag="PRODUCTION_TAG";
 Spawn.SpawnPlayer(spawnTag:"PRODUCTION_TAG");if(Spawn.Last.origin.X==0||Spawn.Last.origin.Y==0)throw new Exception("new tag must use actual hall spawnpoint");
 Console.WriteLine("CITY_SEARCH_HALL_SPAWN_NEGATIVE_CONTROL=PASS; actual installed scene position="+Spawn.Last.origin.X+","+Spawn.Last.origin.Y+","+Spawn.Last.origin.Z);
}}
'''.replace('NATIVE', native).replace('PREFIX', prefix).replace('SPECIAL', special).replace('COORDINATES', coordinates)
            .replace('PRODUCTION_TAG', production_tag))


if __name__ == '__main__':
    unittest.main()
