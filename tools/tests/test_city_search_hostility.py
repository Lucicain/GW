"""Production armed eligibility and hostile exit decisions, with old-code controls."""
import unittest
from test_city_search import read
from test_duty_banner import method
from test_city_search_flow import run


class CitySearchHostilityTests(unittest.TestCase):
    def test_armed_defenders_and_physical_exit(self):
        mission = read('GwpCitySearchMission.cs')
        progress = read('GwpCitySearchMission.Progress.cs')
        bodies = '\n'.join(method(mission, n) for n in ['IsDefender'])
        bodies += '\n' + method(progress, 'CanLeaveSearch')
        code = r'''
using System;
class Hero {public static Hero MainHero=new();public object MapFaction=new();}
class CharacterObject {public bool IsHero;public Hero? HeroObject;}
class Vec {public float X;public float Distance(Vec v)=>Math.Abs(X-v.X);}
class Agent {public static Agent Main=new();public bool IsHuman=true,Armed,Watch,Ours,Active=true;public CharacterObject Character=new();public Vec Position=new();public bool HasWeapon()=>Armed;public bool IsActive()=>Active;}
class Conversation {public bool IsConversationFlowActive;}
class Campaign {public static Campaign Current=new();public Conversation ConversationManager=new();}
class Settlement {public object MapFaction=new();}
class SearchState {public bool SearchWar;}
static class GwpTuning {public static class CitySearch {public const float GateExitDistance=6;}}
enum Stage {Entering,Allowed,Fighting,FightOver,Duel,Failed,HallDone}
class Controller {
 public bool InHall;public Agent? _hallTarget;public Settlement _settlement=new();public SearchState Search=new();
 public Stage _stage=Stage.Allowed;public string? _pendingFight,_pendingHallAction;public Vec _gate=new();public bool _gateExitAvailable=true;
 bool IsWatch(Agent a)=>a.Watch&&!a.Ours;bool IsOurs(Agent a)=>a.Ours||a==Agent.Main;
 public bool Defender(Agent a)=>IsDefender(a);public bool Leave()=>CanLeaveSearch();
 BODIES
}
static class Program {
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
 var c=new Controller();var guard=new Agent{Watch=true};
 Check(!c.Defender(guard),"unarmed guard must flee");guard.Armed=true;Check(c.Defender(guard),"armed guard fights");
 var lord=new Agent{Character=new(){IsHero=true,HeroObject=new(){MapFaction=c._settlement.MapFaction}}};
 Check(!c.Defender(lord),"unarmed local lord must flee");lord.Armed=true;Check(c.Defender(lord),"armed local lord fights");
 c.InHall=true;c._hallTarget=new Agent();Check(!c.Defender(c._hallTarget),"unarmed hall target must flee");
 c._hallTarget.Armed=true;Check(c.Defender(c._hallTarget),"armed foreign target fights");
 guard.Ours=true;Check(!c.Defender(guard),"armed ally never becomes defender");
 Check(!c.Defender(new Agent{Armed=true}),"foreign armed guest remains neutral");
 c.InHall=false;Agent.Main.Position.X=100;Check(c.Leave(),"peaceful walking shortcut remains available");
 c.Search.SearchWar=true;c._stage=Stage.FightOver;Check(!c.Leave(),"hostile city blocks Tab even after wave defeated");
 c.InHall=true;Agent.Main.Position.X=0;Check(!c.Leave(),"hostile hall blocks Tab even beside its spawn");
 c.InHall=false;c._stage=Stage.Fighting;Check(c.Leave(),"actual outside exit permits retreat during fighting");
 Agent.Main.Position.X=6.01f;Check(!c.Leave(),"city exit boundary enforced");Agent.Main.Position.X=6;Check(c.Leave(),"outside boundary is inclusive");
 c._pendingFight="strike";Check(!c.Leave(),"queued hostility cannot be escaped by Tab");c._pendingFight=null;
 c._pendingHallAction="door";Check(!c.Leave(),"queued passage cannot race Tab");c._pendingHallAction=null;
 Campaign.Current.ConversationManager.IsConversationFlowActive=true;Check(!c.Leave(),"conversation blocks exit");
 Campaign.Current.ConversationManager.IsConversationFlowActive=false;c._stage=Stage.Duel;Check(!c.Leave(),"duel cannot be escaped");
 c._stage=Stage.Failed;Check(!c.Leave(),"defeat cannot award escape");
 Console.WriteLine("CITY_SEARCH_HOSTILITY_ASSERTIONS="+n);
 }
}
'''.replace('BODIES', bodies)
        run(code)
        old_defender = bodies.replace('agent.IsHuman && !IsOurs(agent) && agent.HasWeapon() && (', '(')
        if old_defender != bodies:
            with self.assertRaisesRegex(AssertionError, 'unarmed guard must flee'):
                run(code.replace(bodies, old_defender))
        old_tab = bodies[:bodies.index('private bool CanLeaveSearch')] + '''private bool CanLeaveSearch() => _stage != Stage.Fighting && _stage != Stage.Duel
            && _stage != Stage.Failed && _pendingFight == null && _pendingHallAction == null
            && !Campaign.Current.ConversationManager.IsConversationFlowActive;'''
        with self.assertRaisesRegex(AssertionError, 'hostile city blocks Tab'):
            run(code.replace(bodies, old_tab))


if __name__ == '__main__':
    unittest.main()
