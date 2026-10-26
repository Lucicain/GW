"""Fixed courtyard site selection and staging. Native navigation/physics remain explicit boundaries."""
import re
import unittest
from test_city_search import read, block
from test_city_search_flow import run
from test_duty_banner import method

VECTOR = r'''
namespace TaleWorlds.Library{
public struct Vec2{public float x,y;public Vec2(float a,float b){x=a;y=b;}public float Distance(Vec2 o)=>MathF.Sqrt((x-o.x)*(x-o.x)+(y-o.y)*(y-o.y));}
public struct Vec3{public float x,y,z;public Vec3(float a,float b,float c){x=a;y=b;z=c;}
 public static Vec3 Zero=>new(0,0,0);public static Vec3 Invalid=>new(float.NaN,float.NaN,float.NaN);public static Vec3 Up=>new(0,0,1);
 public Vec2 AsVec2=>new(x,y);public float DistanceSquared(Vec3 p)=>(x-p.x)*(x-p.x)+(y-p.y)*(y-p.y)+(z-p.z)*(z-p.z);public float Distance(Vec3 p)=>MathF.Sqrt(DistanceSquared(p));
 public static Vec3 operator+(Vec3 a,Vec3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vec3 operator*(Vec3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
}
'''


class CitySearchCourtyardTests(unittest.TestCase):
    def test_fixed_disc_rejects_narrow_steps_holes_furniture_and_no_path(self):
        tuning = block(read('GwpTuning.cs'), r'internal static class CitySearch')
        ground = re.sub(r'(?m)^using .*;\n', '', read('GwpDuelGround.cs'))
        code = r'''
using System;using System.Collections.Generic;using System.Linq;using TaleWorlds.Library;
''' + VECTOR + '\nnamespace GreyWardenPolicePurity{static class GwpTuning{TUNING}}\n' + ground + r'''
namespace GreyWardenPolicePurity{
static class Program{static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}
static void Main(){
 Vec3 origin=new(0,0,0);Vec3? Flat(Vec3 v)=>v;bool Clear(Vec3 a,Vec3 b)=>true;
 bool Pick(IEnumerable<Vec3> candidates,Func<Vec3,Vec3?> surface,Func<Vec3,Vec3,bool> physics,Func<Vec3,bool> reach,out Vec3 selected)=>GwpDuelGround.TryPick(candidates,origin,surface,physics,reach,out selected);
 Check(Pick(new[]{new Vec3(20,0,0),new Vec3(10,0,0)},Flat,Clear,_=>true,out var site)&&site.x==10,"nearest fully clear site is deterministic, not a random spawn marker");
 Check(!Pick(new[]{origin},v=>Math.Abs(v.y)<1? v:null,Clear,_=>true,out _),"narrow corridor has a usable centre but cannot fit the fixed disc");
 Check(!Pick(new[]{origin},v=>v.x>2?new Vec3(v.x,v.y,1):v,Clear,_=>true,out _),"steps and terraces inside circle are rejected");
 Check(!Pick(new[]{origin},v=>Math.Abs(v.x-2)<0.1&&Math.Abs(v.y-2)<0.1?null:v,Clear,_=>true,out _),"interior navmesh hole is rejected even with a clear perimeter");
 Check(!Pick(new[]{origin},Flat,(a,b)=>!(Math.Abs(b.x-2)<0.1&&Math.Abs(b.y-2)<0.1),_=>true,out _),"interior furniture is rejected even with a clear perimeter");
 Check(!Pick(new[]{origin},Flat,Clear,_=>false,out _),"disconnected open ground is rejected");
 Check(Pick(new[]{origin,new Vec3(20,0,0)},v=>Math.Abs(v.x)<8&&Math.Abs(v.y)<1?v.x==0&&v.y==0?v:null:v,Clear,_=>true,out site)&&site.x==20,"invalid nearby candidate does not hide a valid courtyard farther away");
 for(int i=0;i<GwpTuning.CitySearch.CourtDuelSeats;i++){var seat=GwpDuelGround.Seat(origin,i);Check(Math.Abs(seat.Distance(origin)-6)<0.001,"every seat uses the fixed six metre circle");}
 Check(!Pick(Array.Empty<Vec3>(),Flat,Clear,_=>true,out site)&&float.IsNaN(site.x),"no valid site returns an explicit failure, never the scene origin");
 Console.WriteLine("CITY_SEARCH_COURT_GROUND_ASSERTIONS="+n);
}}}
'''.replace('TUNING', tuning)
        code = code.replace('TUNING', tuning)
        run(code)
        old = code.replace('IsClear(center.Value, project, clear) && ', '')
        with self.assertRaisesRegex(AssertionError, 'narrow corridor'):
            run(old)
        old = code.replace('if (x * x + y * y <= radius * radius && !Point', 'if (false && !Point')
        with self.assertRaisesRegex(AssertionError, 'interior navmesh hole'):
            run(old)

    def test_native_boundary_adapter_checks_nav_height_and_two_ray_levels(self):
        court = read('GwpCitySearchMission.CourtyardDuel.cs')
        bodies = method(court, 'CourtGround') + method(court, 'CourtLineClear')
        code = r'''
using System;using System.Collections.Generic;using TaleWorlds.Library;
''' + VECTOR + r'''
namespace GreyWardenPolicePurity{
struct WorldPosition{public Scene Scene;public Vec3 Position;public WorldPosition(Scene s,UIntPtr ptr,Vec3 p,bool valid){Scene=s;Position=p;}
 public UIntPtr GetNavMesh()=>Scene.HasNav?new UIntPtr(1):UIntPtr.Zero;public Vec3 GetNavMeshVec3()=>Scene.Project(Position);}
class Scene{public bool HasNav=true,PathClear=true,LowCollision,HighCollision;public float Radius,Thickness;public List<float> Rays=new();public Func<Vec3,Vec3> Project=p=>p;
 public bool IsLineToPointClear(ref WorldPosition a,ref WorldPosition b,float radius){Radius=radius;return PathClear;}
 public bool RayCastForClosestEntityOrTerrainFixedPhysics(Vec3 a,Vec3 b,out float distance,float thickness){distance=1;Thickness=thickness;Rays.Add(a.z);return a.z>1?HighCollision:LowCollision;}}
class Mission{public Scene Scene=new();public bool Inside=true,Blocked;public bool IsPositionInsideBoundaries(Vec2 p)=>Inside;public bool IsPositionInsideAnyBlockerNavMeshFace2D(Vec2 p)=>Blocked;}
class Court{public Mission Mission=new();BODIES public Vec3? Ground(Vec3 p)=>CourtGround(p);public bool Clear(Vec3 a,Vec3 b)=>CourtLineClear(a,b);}
static class Program{static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}static void Main(){
 var c=new Court();var zero=new Vec3(0,0,0);Check(c.Ground(zero).HasValue,"walkable ground projects to the native navmesh");
 c.Mission.Inside=false;Check(!c.Ground(zero).HasValue,"scene boundary rejects ground");c.Mission.Inside=true;c.Mission.Blocked=true;Check(!c.Ground(zero).HasValue,"disabled or blocker navmesh rejects ground");c.Mission.Blocked=false;
 c.Mission.Scene.HasNav=false;Check(!c.Ground(zero).HasValue,"missing native navmesh is rejected");c.Mission.Scene.HasNav=true;
 c.Mission.Scene.Project=p=>new Vec3(p.x+1,p.y,p.z);Check(!c.Ground(zero).HasValue,"nearest-face projection cannot move an off-mesh point onto a different patch");c.Mission.Scene.Project=p=>p;
 Check(c.Clear(zero,new Vec3(2,0,0))&&c.Mission.Scene.Rays.Count==2&&c.Mission.Scene.Radius==0.5f&&c.Mission.Scene.Thickness==0.25f,"clearance checks native walking width and both thick static ray levels");
 c.Mission.Scene.HighCollision=true;Check(!c.Clear(zero,new Vec3(2,0,0)),"tabletop or railing at torso height rejects the field");c.Mission.Scene.HighCollision=false;c.Mission.Scene.LowCollision=true;Check(!c.Clear(zero,new Vec3(2,0,0)),"low furniture also rejects the field");
 c.Mission.Scene.LowCollision=false;c.Mission.Scene.PathClear=false;Check(!c.Clear(zero,new Vec3(2,0,0)),"straight collision sight alone cannot replace the navmesh line check");
 Console.WriteLine("CITY_SEARCH_COURT_ADAPTER_ASSERTIONS="+n);
}}}
'''.replace('BODIES', bodies)
        run(code)

    def test_duel_equipment_uses_battle_gear_without_mutating_inventory(self):
        body = method(read('GwpCitySearchMission.CourtyardDuel.cs'), 'EquipCourtDuelist')
        code = r'''
using System;using System.Collections.Generic;
namespace GreyWardenPolicePurity{
enum EquipmentIndex{WeaponItemBeginSlot=0,Weapon0=0,Weapon1=1,Weapon2=2,Weapon3=3,NumAllWeaponSlots=4}
class Weapon{public bool IsMeleeWeapon;}class ItemObject{public string Id="";public Weapon? PrimaryWeapon;}
struct EquipmentElement{public ItemObject Item;public bool IsEmpty=>Item==null;public EquipmentElement(ItemObject i){Item=i;}}
class Equipment{private EquipmentElement[] Items=new EquipmentElement[4];public Equipment(){}public Equipment(Equipment e){Array.Copy(e.Items,Items,4);}public EquipmentElement this[EquipmentIndex i]{get=>Items[(int)i];set=>Items[(int)i]=value;}}
class CharacterObject{public Equipment FirstBattleEquipment=new();}
class Agent{public CharacterObject Character=new();public Equipment? Updated;public void UpdateSpawnEquipmentAndRefreshVisuals(Equipment e){Updated=e;}}
class MBObjectManager{public static MBObjectManager Instance=new();public string? LastId;public T GetObject<T>(string id){LastId=id;return (T)(object)new ItemObject{Id=id,PrimaryWeapon=new(){IsMeleeWeapon=true}};}}
class Court{BODY public void Equip(Agent a)=>EquipCourtDuelist(a);}
static class Program{static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}static void Main(){
 var c=new Court();var lord=new Agent();c.Equip(lord);Check(lord.Updated![EquipmentIndex.Weapon0].Item.PrimaryWeapon!.IsMeleeWeapon,"unarmed duelist receives a usable melee weapon");
 Check(lord.Character.FirstBattleEquipment[EquipmentIndex.Weapon0].IsEmpty,"temporary weapon does not enter hero persistent equipment or inventory");
 Check(MBObjectManager.Instance.LastId=="wooden_sword_t1","known native practice sword is used");
 var sword=new ItemObject{Id="own_sword",PrimaryWeapon=new(){IsMeleeWeapon=true}};lord.Character.FirstBattleEquipment[EquipmentIndex.Weapon2]=new EquipmentElement(sword);c.Equip(lord);
 Check(lord.Updated![EquipmentIndex.Weapon2].Item==sword&&lord.Updated[EquipmentIndex.Weapon0].IsEmpty,"existing battle weapon remains in its own slot without adding practice gear");
 Console.WriteLine("CITY_SEARCH_COURT_EQUIPMENT_ASSERTIONS="+n);
}}}
'''.replace('BODY', body)
        run(code)

    def test_patrol_position_behavior_keeps_its_target_until_released(self):
        body = block(read('GwpCitySearchMission.CourtyardDuel.cs'), r'internal sealed class GwpCitySearchPositionBehavior')
        code = r'''
using System;
namespace GreyWardenPolicePurity{
struct WorldPosition{public int Id;}
class Agent{public enum AIScriptedFrameFlags{None}}
class Navigator{public int Calls;public bool Held;public int Id;public void SetTargetFrame(WorldPosition p,float rotation,float range,float angle,Agent.AIScriptedFrameFlags flags,bool disableClearTargetWhenTargetIsReached){Calls++;Held=disableClearTargetWhenTargetIsReached;Id=p.Id;}}
class AgentBehaviorGroup{public Navigator Navigator=new();}
class AgentBehavior{protected Navigator Navigator;public AgentBehavior(AgentBehaviorGroup g){Navigator=g.Navigator;}public virtual float GetAvailability(bool s)=>1;public virtual void Tick(float d,bool s){}public virtual string GetDebugInfo()=>"";}
BODY
static class Program{static void Main(){var group=new AgentBehaviorGroup();var position=new GwpCitySearchPositionBehavior(group);
 position.Set(new WorldPosition{Id=1},0);position.Tick(0.5f,false);position.Tick(0.5f,false);
 if(group.Navigator.Calls!=1||!group.Navigator.Held||position.GetAvailability(false)!=0)throw new Exception("position must persist at arrival and run only as the explicit scripted behavior");
 position.Set(new WorldPosition{Id=2},1);position.Tick(0.5f,false);if(group.Navigator.Calls!=2||group.Navigator.Id!=2)throw new Exception("explicit reassignment must replace the target exactly once");
 Console.WriteLine("CITY_SEARCH_COURT_POSITION_ASSERTIONS=2");
}}}
'''.replace('BODY', body)
        run(code)

    def test_court_preparation_waits_and_restores_every_spectator(self):
        court = read('GwpCitySearchMission.CourtyardDuel.cs')
        progress = read('GwpCitySearchMission.Progress.cs')
        tuning = block(read('GwpTuning.cs'), r'internal static class CitySearch')
        bodies = method(court, 'TickCourtDuel') + method(progress, 'StartCourtDuel')
        bodies += method(progress, 'RestoreSpectators') + method(progress, 'GetAgentState')
        code = r'''
using System;using System.Collections.Generic;using System.Linq;using TaleWorlds.Library;
''' + VECTOR + r'''
namespace GreyWardenPolicePurity{
static class GwpTuning{TUNING}
enum Stage{HallDone,Duel,Failed}enum MissionMode{StartUp,Duel}enum AgentState{None,Unconscious}
class Team{public static Team Invalid=new();public bool Hostile;public void SetIsEnemyOf(Team t,bool b){Hostile=b;}}
class Agent{public static Agent? Main;public bool Active=true,IsHuman=true,IsAIControlled=true,Scripted=true,Fights;public Vec3 Position;public Team Team=new();public bool IsActive()=>Active;public void SetTeam(Team t,bool b){Team=t;}}
class MissionConversationLogic{public bool Disabled;public void DisableStartConversation(bool b){Disabled=b;}}
class Mission{public float CurrentTime;public List<Agent> Agents=new();public Team PlayerTeam=new(),PlayerEnemyTeam=new();public MissionMode Mode;
 public MissionConversationLogic Talk=new();public T GetMissionBehavior<T>()=>(T)(object)Talk;public void SetMissionMode(MissionMode m,bool b){Mode=m;}}
class SearchState{public bool SearchActive=true,DuelAgreed=true;}
static class GwpAiDiagnostics{public static void WriteCitySearch(string s,string d){}}
class Court{public bool _courtPreparing=true;public Stage _stage=Stage.HallDone;public SearchState Search=new();public bool IsCourtAppointment=>Search.DuelAgreed;public Mission Mission=new();
 public Dictionary<Agent,Vec3> _courtSeats=new();public Dictionary<Agent,Team> _spectators=new();public Agent? _hallTarget;public Vec3 _courtCenter=new(0,0,0),_courtTargetStart=new(2,0,0);public float _nextCourtCheck;
 static void ReleaseCourtPosition(Agent a){a.Scripted=false;}static void SetSearchFighter(Agent a){a.Scripted=false;a.Fights=true;}
 public bool Tick(){Mission.CurrentTime++;return TickCourtDuel();}public void Restore()=>RestoreSpectators();
 BODIES}
static class Program{static int n;static void Check(bool v,string why){n++;if(!v)throw new Exception(why);}
static Court Setup(out Agent target,out Agent watcher,out Team old){Agent.Main=new(){Position=new(12,0,0)};target=new(){Position=new(12,0,0)};watcher=new(){Position=new(12,0,0)};old=watcher.Team;
 var c=new Court{_hallTarget=target};c.Mission.Agents.AddRange(new[]{Agent.Main,target,watcher});c._courtSeats[watcher]=new(-6,0,0);c._spectators[watcher]=old;watcher.Team=Team.Invalid;return c;}
static void Main(){
 var c=Setup(out var target,out var watcher,out var team);c.Tick();Check(c._stage==Stage.HallDone,"no fighting before target and watchers arrive");
 target.Position=c._courtTargetStart;c.Tick();Check(!target.Fights,"target arrival alone cannot start duel");
 watcher.Position=new(-6,0,0);c.Tick();Check(!target.Fights,"player must walk into the ring personally");
 Agent.Main!.Position=new(0,0,0);c.Tick();Check(c._stage==Stage.Duel&&target.Fights&&!target.Scripted&&c.Mission.Mode==MissionMode.Duel,"only after everyone is ready does native fighting start");
 Check(watcher.Team==Team.Invalid&&watcher.Scripted,"spectators keep their ring positions while duel runs");
 Check(c.GetAgentState(target,1,out _)==AgentState.Unconscious,"duel cannot kill the target");
 c.Restore();Check(watcher.Team==team&&!watcher.Scripted&&c._courtSeats.Count==0&&!c._courtPreparing,"victory or leaving restores original teams and releases every seat");
 c=Setup(out target,out watcher,out team);c.Search.SearchActive=false;c.Tick();Check(watcher.Team==team&&!watcher.Scripted&&!target.Scripted&&!c._courtPreparing,"aborted search releases preparation instead of trapping followers");
 c=Setup(out target,out watcher,out team);watcher.Active=false;c.Tick();Check(!c._courtPreparing,"removed participant cancels preparation rather than waiting forever or replacing the fixed roster");
 Console.WriteLine("CITY_SEARCH_COURT_STAGE_ASSERTIONS="+n);
}}}
'''.replace('TUNING', tuning).replace('BODIES', bodies.replace('public override ', 'public '))
        run(code)
        with self.assertRaisesRegex(AssertionError, 'player must walk into the ring'):
            run(code.replace('|| Agent.Main.Position.Distance(_courtCenter) > GwpTuning.CitySearch.CourtDuelFightRadius', ''))
        with self.assertRaisesRegex(AssertionError, 'no fighting before target'):
            run(code.replace('|| _hallTarget.Position.Distance(_courtTargetStart) > tolerance', '')
                .replace('|| _courtSeats.Any(entry => entry.Key.Position.Distance(entry.Value) > tolerance)', '')
                .replace('|| Agent.Main.Position.Distance(_courtCenter) > GwpTuning.CitySearch.CourtDuelFightRadius', ''))


if __name__ == '__main__':
    unittest.main()
