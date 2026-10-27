"""Follow actual scene snapshots through spawn restoration and campaign save/load.

Native spawning and enemy bookkeeping are seams; follower health, filtering,
scene-end persistence, and SyncData are production bodies.
"""
import re
import unittest

from test_city_search import read, block
from test_city_search_flow import STUBS, run


class CitySearchHealthTests(unittest.TestCase):
    def test_selected_followers_keep_wounds_and_departed_hall_followers_across_scenes(self):
        flow = re.sub(r'(?m)^using .*;\n', '', read('GwpCitySearchBehavior.Flow.cs'))
        progress = read('GwpCitySearchMission.Progress.cs')
        bodies = block(progress, r'private void RestoreTroopHealth\(')
        bodies += block(progress, r'private void EndSearchScene\(')
        bodies += block(read('GwpCitySearchMission.HallDuel.cs'),
                        r'private IEnumerable<\(CharacterObject Character, float Health\)> SearchSurvivors\(')
        stubs = STUBS.replace('public int Defeats;', 'public void RecordCitySearchLosses(Hero h,int count){}public int Defeats;')
        scenario = r'''
namespace GreyWardenPolicePurity{
class Agent{
 public CharacterObject Character;public float Health=100,HealthLimit=100;public bool Active=true,NativeGone;
 public Agent(CharacterObject c){Character=c;}public bool IsActive(){if(NativeGone)throw new Exception("departed follower native state was read");return Active;}
}
class FollowerScene{
 public Dictionary<Agent,CharacterObject> _ours=new();public Dictionary<Agent,float> _hallExitedTroops=new();public HashSet<Agent> _hallRemoved=new();
 private Dictionary<string,int> _spawnedTroops=new();private Dictionary<CharacterObject,int> _killed=new();private Hero _target=new("target");
 private bool _transitioning=true,_outsideExitRequested;private GwpCitySearchBehavior Search=>Campaign.Current.Search;
 void FinishHallDuel(){}void SaveCitySoldiers(){}bool CanLeaveSearch()=>false;
 BODIES
 public Agent Spawn(CharacterObject c){var a=new Agent(c);_ours[a]=c;RestoreTroopHealth(a,c);return a;}
 public void Close()=>EndSearchScene();
}
static class Program{
 static int n;static void Check(bool value,string why){n++;if(!value)throw new Exception(why);}
 static void Reload(){var save=new Store();Campaign.Current.Search.Sync(save);var after=new GwpCitySearchBehavior();after.Sync(new Store{Loading=true,Data=save.Data});Campaign.Current.Search=after;}
 static void Main(){
 var settlement=new Settlement();Settlement.CurrentSettlement=settlement;var target=new Hero("lord");CrimeRecord.Shelter=settlement;
 var foot=new CharacterObject("foot");var bow=new CharacterObject("bow");var roster=TroopRoster.CreateDummyTroopRoster();roster.AddToCounts(foot,3);roster.AddToCounts(bow,1);
 Campaign.Current.Search.Start(settlement,target,roster);var entry=new FollowerScene();
 var a=entry.Spawn(foot);var b=entry.Spawn(foot);var dead=entry.Spawn(foot);var archer=entry.Spawn(bow);
 Check(a.Health==100&&b.Health==100,"first appearance uses native health");a.Health=13;b.Health=37;dead.Active=false;archer.Health=62;
 entry.Close();Reload();Check(Campaign.Current.Search.SavedHealth("foot",0)==13&&Campaign.Current.Search.SavedHealth("foot",1)==37,"scene end saves each survivor's actual wounds");
 var hall=new FollowerScene();a=hall.Spawn(foot);b=hall.Spawn(foot);archer=hall.Spawn(bow);
 Check(a.Health==13&&b.Health==37&&archer.Health==62,"entering hall restores individual health instead of full health");
 bool missing=false;try{Campaign.Current.Search.SavedHealth("foot",2);}catch(InvalidOperationException){missing=true;}Check(missing,"dead third soldier is not included in next scene");
 a.Health=9;b.Health=41;hall._hallExitedTroops[b]=41;hall._hallRemoved.Add(b);b.NativeGone=true;archer.Active=false;
 hall.Close();Reload();var escape=new FollowerScene();a=escape.Spawn(foot);b=escape.Spawn(foot);
 Check(a.Health==9&&b.Health==41,"outgoing scene retains new wounds and offscreen hall follower health");
 missing=false;try{Campaign.Current.Search.SavedHealth("bow",0);}catch(InvalidOperationException){missing=true;}Check(missing,"wounded-out archer is not regenerated after leaving hall");
 escape.Close();Reload();var resumed=new FollowerScene();Check(resumed.Spawn(foot).Health==9&&resumed.Spawn(foot).Health==41,"another save and scene reload does not heal followers");
 Console.WriteLine("CITY_SEARCH_FOLLOWER_HEALTH_ASSERTIONS="+n);
 }
}}
'''.replace('BODIES', bodies)
        code = stubs + flow + scenario
        run(code)
        with self.assertRaisesRegex(AssertionError, 'entering hall restores individual health'):
            run(code.replace('agent.Health = System.Math.Min(agent.HealthLimit, health);', 'agent.Health = agent.HealthLimit;'))
        with self.assertRaisesRegex(AssertionError, "scene end saves each survivor's actual wounds"):
            run(code.replace('Search.SaveSurvivors(SearchSurvivors());', ''))
        with self.assertRaisesRegex(AssertionError, 'Search survivor missing: foot'):
            run(code.replace('_hallExitedTroops.TryGetValue(entry.Key, out float health)',
                             '_hallExitedTroops.TryGetValue(new Agent(entry.Value), out float health)'))


if __name__ == '__main__':
    unittest.main()
