"""Search-only alley isolation, with installed native entry/start/end bodies.

Native agents and scene objects are seams. The old selected-soldier navigator
shape reproduces the original alley cleanup exception without a game process.
"""
import re
import unittest
from pathlib import Path

from test_city_search import read, block
from test_city_search_flow import run
from test_duty_banner import method


class CitySearchAlleyTests(unittest.TestCase):
    def test_search_has_no_alley_entry_but_normal_town_keeps_native_alley_fights(self):
        native_root = Path('.codex_tmp/sb148-full')
        alley = (native_root / 'SandBox.Missions.MissionLogics/MissionAlleyHandler.cs').read_text(encoding='utf-8-sig')
        campaign = (native_root / 'SandBox.CampaignBehaviors/AlleyCampaignBehavior.cs').read_text(encoding='utf-8-sig')
        entry = method(campaign, 'alley_talk_start_normal_on_condition')
        start = method(alley, 'StartCommonAreaBattle')
        end = method(alley, 'EndFight')
        behaviors = method(read('GwpCitySearchMission.cs'), 'Behaviors')
        kinds = set(re.findall(r'new (\w+)\(', behaviors))
        kinds.add('MissionAlleyHandler')
        special = {'MissionAlleyHandler', 'MissionFightHandler'}
        declarations = '\n'.join('class '+name+':MissionBehavior{public '+name+'(params object[] args){}}'
                                 for name in sorted(kinds - special))
        template = r'''
using System;using System.Collections.Generic;using System.Linq;
interface IAgent{}class Banner{}class TroopRoster{}class MissionBehavior{}
class Vec3{public static Vec3 Invalid=new();}
enum MissionMode{StartUp,Battle}enum GwpCitySearchPlace{Entry,Hall}
class Hero{public static Hero MainHero=new();}class CharacterObject{public bool IsHero;public int Occupation;}
class SettlementArea{public Hero Owner=new();}class Alley:SettlementArea{public int State=1;}
class Location{public string StringId="center";}class LocationCharacter{}
class LocationComplex{public static LocationComplex Current=new();public LocationCharacter FindCharacter(IAgent a)=>new();}
class AccompanyingCharacter{public bool IsFollowingPlayerAtMissionStart;}
class LocationEncounter{public AccompanyingCharacter? GetAccompanyingCharacter(LocationCharacter c)=>null;}
static class PlayerEncounter{public static LocationEncounter LocationEncounter=new();}
class CampaignMission{public static CampaignMission Current=new();public Alley? LastVisitedAlley;}
class GameMenuManager{public Location? PreviousLocation;}
class SearchState{public GwpCitySearchPlace SearchPlace;}
class GwpCitySearchBehavior:SearchState{}
class Campaign{public static Campaign Current=new();public GameMenuManager GameMenuManager=new();public T GetCampaignBehavior<T>() where T:new()=>new();}
class Settlement{public bool IsTown;}
class Agent:IAgent{public static Agent Main=null!;public bool IsHuman=true,IsPlayerControlled;public CharacterObject Character=new();public Vec3 Position=new();public CampaignAgentComponent Component=new();public T GetComponent<T>()=>(T)(object)Component;}
class CampaignAgentComponent{public AgentNavigator? AgentNavigator;}
class AgentNavigator{public Alley? MemberOfAlley;public AlarmedBehaviorGroup Group=new();public T GetBehaviorGroup<T>()=>(T)(object)Group;}
class FightBehavior{public bool IsActive=true;}
class AlarmedBehaviorGroup{public FightBehavior Fight=new();public T GetBehavior<T>()=>(T)(object)Fight;}
class Mission{public static Mission Current=new();public List<Agent> Agents=new();public List<MissionBehavior> Behaviors=new();public T? GetMissionBehavior<T>()=>Behaviors.OfType<T>().FirstOrDefault();public void SetMissionMode(MissionMode mode,bool start){}}
static class ConversationMission{public static Agent? OneToOneConversationAgent;}
class MissionFightHandler:MissionBehavior{public int EndCalls;public void EndFight(){EndCalls++;}public void StartCustomFight(List<Agent> a,List<Agent> b,bool dropWeapons,bool isItemUseDisabled,Action<bool> callback){}}
DECLARATIONS
class MissionAlleyHandler:MissionBehavior{
 public Mission Mission=>Mission.Current;public bool CanThugConversationBeTriggered=>true;
 private static List<Agent> _guardAgents=new();private Dictionary<Alley,bool> _conversationTriggeredAlleys=new();private static Vec3 _fightPosition=Vec3.Invalid;
 private MissionFightHandler _missionFightHandler=>Mission.GetMissionBehavior<MissionFightHandler>()!;
 void OnAlleyFightEnd(bool won){}START END
 public int Guards=>_guardAgents.Count;public void Finish()=>EndFight();
}
class NativeEntry{ENTRY public bool Available()=>alley_talk_start_normal_on_condition();}
static class GwpCitySearchMission{public const string HallSpawnTag="spawnpoint_player",GateSpawnTag="gate";BEHAVIORS public static MissionBehavior[] Build(bool town,string location)=>Behaviors(new(){IsTown=town},new(),new(),new(){StringId=location});}
static class Program{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
 var alley=new Alley();Agent.Main=new(){IsPlayerControlled=true,Character=new(){IsHero=true}};
 var thug=new Agent{Component=new(){AgentNavigator=new(){MemberOfAlley=alley}}};ConversationMission.OneToOneConversationAgent=thug;
 foreach(bool town in new[]{false,true})foreach(string place in new[]{"center","lordshall"}){
  Mission.Current=new(){Behaviors=GwpCitySearchMission.Build(town,place).ToList()};
  Check(!new NativeEntry().Available(),"search cannot start native alley quest");
  Check(Mission.Current.GetMissionBehavior<MissionAlleyHandler>()==null,"search has no competing alley tick or cleanup");
  Check(Mission.Current.GetMissionBehavior<MissionFightHandler>()!=null&&Mission.Current.GetMissionBehavior<MissionAgentHandler>()!=null,"search retains combat service and normal town population");
 }
 var fight=new MissionFightHandler();var handler=new MissionAlleyHandler();var guard=new Agent{Character=new(){Occupation=7},Component=new(){AgentNavigator=new()}};
 Mission.Current=new(){Behaviors=new(){fight,handler},Agents=new(){Agent.Main,thug,guard}};
 Check(new NativeEntry().Available(),"ordinary town walking keeps the native alley entry");handler.StartCommonAreaBattle(alley);handler.Finish();Check(fight.EndCalls==1,"ordinary native guard cleanup still works");
 var selectedSoldier=new Agent{Character=new(){Occupation=7},Component=new(){AgentNavigator=null}};Mission.Current.Agents.Add(selectedSoldier);
 handler.StartCommonAreaBattle(alley);Check(handler.Guards==2,"native alley scans selected search soldiers as guards too");
 bool reproduced=false;try{handler.Finish();}catch(NullReferenceException){reproduced=true;}
 Check(reproduced,"old search soldier without town navigator reproduces native alley cleanup exception");
 Console.WriteLine("CITY_SEARCH_ALLEY_ASSERTIONS="+n);
 }
}
'''
        code = template.replace('DECLARATIONS', declarations).replace('START', start).replace('END', end)
        code = code.replace('ENTRY', entry).replace('BEHAVIORS', behaviors)
        run(code)
        with self.assertRaisesRegex(AssertionError, 'search cannot start native alley quest'):
            run(code.replace('return list.ToArray();', 'list.Add(new MissionAlleyHandler());return list.ToArray();'))

    def test_native_passage_walk_run_flags_are_not_overridden_by_search(self):
        source = Path('.codex_tmp/sb148-full/SandBox.AI/PassageAI.cs').read_text(encoding='utf-8-sig')
        flags = block(source, r'protected override AIScriptedFrameFlags GetScriptedFrameFlags\(').replace('protected override', 'public')
        code = r'''
using System;
enum AIScriptedFrameFlags{None=0,DoNotRun=16}class Agent{public int CurrentWatchState;}
class NativePassage{FLAGS}
static class Program{static void Main(){var passage=new NativePassage();
 if((passage.GetScriptedFrameFlags(new(){CurrentWatchState=0})&AIScriptedFrameFlags.DoNotRun)==0)throw new Exception("native calm passage must retain walking flag");
 if((passage.GetScriptedFrameFlags(new(){CurrentWatchState=2})&AIScriptedFrameFlags.DoNotRun)!=0)throw new Exception("native alarmed passage permits running");
 Console.WriteLine("NATIVE_PASSAGE_WALK_RUN=PASS");}}
'''.replace('FLAGS', flags)
        run(code)
        movement = read('GwpCitySearchMission.Movement.cs')
        self.assertNotIn('GwpCitySearchDuelRunPatch', movement)
        self.assertNotRegex(movement, r'__result\s*&=\s*~Agent\.AIScriptedFrameFlags\.DoNotRun')


if __name__ == '__main__':
    unittest.main()
