"""Installed mission teardown order with production search hooks; no scene engine."""
import re
import subprocess
import unittest
from pathlib import Path
from test_city_search import read
from test_city_search_flow import run
from test_duty_banner import method


class CitySearchLifecycleTests(unittest.TestCase):
    def test_native_team_clear_before_search_behavior_removal(self):
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        native = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.MountAndBlade.Mission',
            str(game / 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')], text=True, encoding='utf-8-sig')
        end = method(native, 'EndMissionInternal')
        self.assertLess(end.index('missionBehavior.OnEndMissionInternal();'), end.index('Teams.Clear();'))
        finalize = method(native, 'OnMissionStateFinalize')
        remove = method(native, 'RemoveMissionBehavior')
        # Keep installed managed sequencing; native scene/agent cleanup boundaries are no-ops.
        source = read('GwpCitySearchMission.cs').replace('protected override void OnEndMission', 'public override void OnEndMission')
        hooks = '\n'.join(method(source, name) for name in ['OnRemoveBehavior', 'OnEndMission'])
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
enum MissionBehaviorType{Logic,Other}
class MissionBehavior{
 public Mission Mission=null!;public MissionBehaviorType BehaviorType=>MissionBehaviorType.Logic;public int BaseRemovals;
 public virtual void OnRemoveBehavior(){BaseRemovals++;}public virtual void OnEndMission(){}
 public void OnEndMissionInternal()=>OnEndMission();public virtual void OnMissionStateFinalized(){}
}
class MissionLogic:MissionBehavior{}class MissionNetworkComponent:MissionBehavior{}
class OrderController{public event Action? OnOrderIssued;public int Subscribers=>OnOrderIssued?.GetInvocationList().Length??0;public void Issue()=>OnOrderIssued?.Invoke();}
class Team{public OrderController PlayerOrderController=new();}
class Teams{public Team? Player=new();public void Clear(){Player=null;}}
static class GameNetwork{public static bool IsSessionActive;}
interface IMissionListener{void OnEndMission();}class Deployment{public void ClearAll(){}}
class Agent{public void OnRemove(){}public void OnDelete(){}public void Clear(){}}
class FocusProvider{public void OnFinalize(){}}class MissionObject{public void OnEndMission(){}}
static class MBDebug{public static void Print(string s,int a,object c,ulong u){}}
namespace TaleWorlds.Library{static class Debug{public enum DebugColor{White}public static void FailedAssert(params object[] a){throw new Exception("unexpected native assertion");}}}
class Mission{
 public enum State{Over}public State CurrentState;public Teams Teams=new();public Team? PlayerTeam=>Teams.Player;
 public static Mission? Current;public object? Scene=new();Deployment? _deploymentPlan=new();List<IMissionListener> _listeners=new();
 public List<Agent> Agents=new(),AllAgents=new();public FocusProvider FocusableObjectInformationProvider=new();public List<MissionObject> MissionObjects=new();
 public List<MissionBehavior> MissionBehaviors=new(),MissionLogics=new(),_otherMissionBehaviors=new();public int ClearedResources;
 public T? GetMissionBehavior<T>() where T:class=>MissionBehaviors.OfType<T>().FirstOrDefault();
 void StopSoundEvents(){}void FreeResources(){}void FinalizeMission(){}
 void ClearUnreferencedResources(bool gpu){ClearedResources++;}
 public void End()=>EndMissionInternal();END FINALIZE REMOVE
}
class GwpCitySearchMissionController:MissionLogic{
 public static GwpCitySearchMissionController? Current;public int Orders,Ends;public bool Permission=true;public int Survivors=14;
 void EndSearchScene(){Ends++;if(!Permission||Survivors!=14)throw new Exception("search progress lost at end");}
 void ReleaseHallPosts(){Orders++;}public void Subscribe(){Mission.PlayerTeam!.PlayerOrderController.OnOrderIssued+=ReleaseHallPosts;}
 HOOKS
}
static class Program{static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}static void Main(){
 var m=new Mission();Mission.Current=m;var c=new GwpCitySearchMissionController{Mission=m};GwpCitySearchMissionController.Current=c;m.MissionBehaviors.Add(c);m.MissionLogics.Add(c);c.Subscribe();var orders=m.PlayerTeam!.PlayerOrderController;
 orders.Issue();Check(c.Orders==1&&orders.Subscribers==1,"peaceful hall posting listens before exit");
 m.End();Check(c.Ends==1&&m.PlayerTeam==null,"installed end hook saves search before clearing native teams");
 Check(orders.Subscribers==0,"order callback must detach while its native team still exists");
 m.OnMissionStateFinalize(false);Check(c.BaseRemovals==1&&GwpCitySearchMissionController.Current==null,"final removal clears current controller after native teams were released");
 Check(m.MissionBehaviors.Count==0&&m.MissionLogics.Count==0&&Mission.Current==null&&m.ClearedResources==1,"native finalize finishes so map shortcuts can resume");
 Check(c.Permission&&c.Survivors==14,"negotiated permission and selected survivors survive peaceful Tab");orders.Issue();Check(c.Orders==1,"removed search no longer receives old orders");
 Console.WriteLine("CITY_SEARCH_LIFECYCLE_ASSERTIONS="+n);
}}
'''.replace('END', end).replace('FINALIZE', finalize).replace('REMOVE', remove).replace('HOOKS', hooks)
        run(code)
        old = hooks.replace('            Mission.PlayerTeam.PlayerOrderController.OnOrderIssued -= ReleaseHallPosts;\n', '')
        # Restore the previous late teardown lookup exactly; the installed sequence reproduces the log stack.
        old = old.replace('public override void OnRemoveBehavior()\n        {',
            'public override void OnRemoveBehavior()\n        {\n            Mission.PlayerTeam.PlayerOrderController.OnOrderIssued -= ReleaseHallPosts;')
        old_code = code.replace(hooks, old)
        # Skip only the new early-detach check, to reach the original null lookup during native finalize.
        old_code = old_code.replace('Check(orders.Subscribers==0,"order callback must detach while its native team still exists");', '')
        with self.assertRaisesRegex(AssertionError, 'Object reference not set'):
            run(old_code)


if __name__ == '__main__':
    unittest.main()
