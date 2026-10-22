"""Production AI carrier decisions; native ragdoll/navmesh remain explicit boundaries."""
import re
import unittest
from test_city_search import read
from test_city_search_flow import run


STUBS = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace GreyWardenPolicePurity{
struct Vec3{public float X;public Vec3(float x){X=x;}public float Distance(Vec3 p)=>Math.Abs(X-p.X);}
struct WorldPosition{public Vec3 Point;public WorldPosition(Vec3 p){Point=p;}}
class Direction{public object AsVec2=>this;}
enum AgentState{Active,Unconscious,Killed,Deleted}enum EquipmentIndex{None}
static class ActionIndexCache{public const int act_none=-1;}
class CharacterObject{public string StringId="soldier";}
class Banner{}class Faction{public Banner Banner=new();public uint Color,Color2;}
class Hero{public CharacterObject CharacterObject=new();public Faction MapFaction=new();public Clan? Clan;}
class Clan{public Banner Banner=new();}
class PartyBase{public static PartyBase MainParty=new();}
class GreyWardenSafePartyAgentOrigin{public GreyWardenSafePartyAgentOrigin(PartyBase p,CharacterObject c,bool b,int i){}}
class GwpBattleSupportOrigin{}
class Team{public static Team Invalid=new();}
class KillingBlow{}
class Agent{
 public enum EventControlFlag{None}[Flags]public enum AIScriptedFrameFlags{None=0,NoAttack=1,Crouch=2,Drag=4,DoNotRun=8}public enum HandIndex{MainHand,OffHand}
 public static Agent Main=null!;public bool Active=true,IsAIControlled=true,IsHuman=true,Crouched,Dragging,Scripted;public int CurrentAction=-1,Wielded,MadeDead;public bool Killed;
 public AgentState State;public Vec3 Position;public Team Team=null!;public object Origin=new();public CharacterObject Character=new();public Direction LookDirection=new();
 public WorldPosition Destination;public AIScriptedFrameFlags Flags;
 public bool IsActive()=>Active;public WorldPosition GetWorldPosition()=>new(Position);
 public void SetWieldedItemIndexAsClient(HandIndex h,EquipmentIndex i,bool a,bool b,int c){Wielded++;}
 public int GetCurrentAction(int channel)=>CurrentAction;
 public void SetCrouchMode(bool b){Crouched=b;}public void SetDraggingMode(bool b){Dragging=b;}
 public void DisableScriptedMovement(){Scripted=false;}public void WieldInitialWeapons(){Wielded++;}
 public void SetScriptedPosition(ref WorldPosition p,bool b,AIScriptedFrameFlags f){Destination=p;Flags=f;Scripted=true;}
 public void MakeDead(bool killed,int action){MadeDead++;Killed=killed;Active=false;State=killed?AgentState.Killed:AgentState.Unconscious;}
}
interface IPlayerInputEffector{Agent.EventControlFlag OnCollectPlayerEventControlFlags();}
class MissionLogic{public Mission Mission=null!;public virtual void OnMissionTick(float dt){}public virtual void OnFixedMissionTick(float dt){}public virtual void OnAgentRemoved(Agent a,Agent b,AgentState s,KillingBlow k){}public virtual bool IsThereAgentAction(Agent a,Agent b)=>true;public virtual void OnRemoveBehavior(){}protected virtual void OnEndMission(){}}
// Native ragdoll, sounds and action progression are engine boundaries, not simulated physics.
class CorpseDraggingMissionLogic:MissionLogic{
 public Agent? NativeBody;public int NativeTicks,NativeFixedTicks;public bool InputPending;
 public void Attach(Agent? a){NativeBody=a;InputPending=true;}
 public Agent.EventControlFlag OnCollectPlayerEventControlFlags(){InputPending=false;return Agent.EventControlFlag.None;}
 public override void OnMissionTick(float dt){NativeTicks++;}public override void OnFixedMissionTick(float dt){NativeFixedTicks++;}
}
class AgentBuildData{public CharacterObject Character;public Vec3 Position;public object Origin=null!;public Team Target=null!;public AgentBuildData(CharacterObject c){Character=c;}
 public AgentBuildData TroopOrigin(object o){Origin=o;return this;}public AgentBuildData Team(Team t){Target=t;return this;}public AgentBuildData NoHorses(bool b)=>this;public AgentBuildData CivilianEquipment(bool b)=>this;public AgentBuildData Banner(Banner b)=>this;public AgentBuildData ClothingColor1(uint c)=>this;public AgentBuildData ClothingColor2(uint c)=>this;public AgentBuildData InitialPosition(Vec3 p){Position=p;return this;}public AgentBuildData InitialDirection(object d)=>this;}
class Mission{public List<Agent> Agents=new();public Team PlayerTeam=new();public float CurrentTime;public int Spawns;
 public Vec3 GetRandomPositionAroundPoint(Vec3 p,float a,float b)=>new(p.X+1);
 public Agent SpawnAgent(AgentBuildData d){Spawns++;var a=new Agent{Position=d.Position,Origin=d.Origin,Team=d.Target,Character=d.Character};Agents.Add(a);return a;}}
class GwpCitySearchBehavior{public bool SearchActive=true,BodyPending=true,BodyKilled;public Hero SearchTarget=new();public int Failures;public void FinishSearch(bool b){SearchActive=false;if(!b)Failures++;}}
class Campaign{public static Campaign Current=new();public GwpCitySearchBehavior Search=new();public T GetCampaignBehavior<T>()=>(T)(object)Search;}
static class GwpTuning{public static class CitySearch{public const float GateExitDistance=6,SecureTargetDistance=2;}}
static class InformationManager{public static void DisplayMessage(InformationMessage m){}}class InformationMessage{public InformationMessage(string s){}}
static class GwpText{public static string Get(string s)=>s;}static class GwpTextCatalog{public const string gwp_cs_transport_target="target",gwp_cs_transport_failed="failed";}
}
'''

SCENARIO = r'''
namespace GreyWardenPolicePurity{
static class Program{static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Main(){
  var m=new Mission();Campaign.Current.Search=new();Agent.Main=new(){Team=m.PlayerTeam,IsAIControlled=false,Position=new(20)};m.Agents.Add(Agent.Main);
  var body=new Agent{Active=false,State=AgentState.Killed,Position=new(0)};
  var wrong=new Agent{Position=new(0),Team=new()};var reinforcement=new Agent{Position=new(0),Team=m.PlayerTeam,Origin=new GwpBattleSupportOrigin()};
  var first=new Agent{Position=new(1),Team=m.PlayerTeam,CurrentAction=4};var second=new Agent{Position=new(10),Team=m.PlayerTeam};
  m.Agents.AddRange(new[]{wrong,reinforcement,first,second});var t=new GwpCitySearchBodyTransport{Mission=m};t.TakeTarget(body);
  t.OnMissionTick(.1f);Check(t.Carrier==first,"nearest actual surviving follower is selected");
  Check(t.NativeBody==null&&first.Scripted&&(first.Flags&Agent.AIScriptedFrameFlags.NoAttack)!=0,"carrier stops attacking before pickup");
  first.CurrentAction=ActionIndexCache.act_none;t.OnMissionTick(.1f);Check(t.NativeBody==body&&first.Crouched,"finished action allows native attachment and crouch");
  Check(!t.InputPending&&((IPlayerInputEffector)t).OnCollectPlayerEventControlFlags()==Agent.EventControlFlag.None,"AI native input never controls player");
  Check(first.Destination.Point.X==20&&(first.Flags&Agent.AIScriptedFrameFlags.Drag)!=0,"attached carrier follows player using native dragging script flags");
  Check(!t.IsTargetNear(new(20)),"player outside while body remains inside cannot complete");
  body.Position=new(15);Check(t.IsTargetNear(new(20)),"actual body reaching gate opens delivery");
  t.OnFixedMissionTick(.02f);Check(t.NativeFixedTicks==1,"attached carrier delegates native fixed physics tick");
  first.Active=false;t.OnAgentRemoved(first,wrong,AgentState.Unconscious,new());Check(t.Carrier==null&&t.NativeBody==null,"fallen carrier releases original corpse immediately");
  m.CurrentTime=1;t.OnMissionTick(.1f);Check(t.Carrier==second&&t.NativeBody==null&&second.Destination.Point.X==15,"replacement approaches the same body without teleporting it");
  second.Position=new(15);t.OnMissionTick(.1f);Check(t.NativeBody==body,"replacement attaches original body");
  t.OnRemoveBehavior();Check(t.Carrier==null&&t.NativeBody==null&&!second.Crouched&&!second.Scripted,"scene removal restores surviving carrier and unbinds native corpse");
  Campaign.Current.Search=new(){BodyKilled=true};second.Position=Agent.Main.Position;var next=new GwpCitySearchBodyTransport{Mission=m};next.OnMissionTick(.1f);
  Check(m.Spawns==1&&next.NativeBody!=null&&next.NativeBody.Killed&&next.NativeBody.MadeDead==1&&next.NativeBody.Origin is GreyWardenSafePartyAgentOrigin,"outgoing scene restores dead body visual once with non-campaign origin");
  next.OnMissionTick(.1f);Check(m.Spawns==1,"mission ticks cannot clone extraction corpse");
  next.NativeBody!.State=AgentState.Deleted;next.OnMissionTick(.1f);next.OnMissionTick(.1f);Check(Campaign.Current.Search.Failures==1,"deleted corpse cannot grant phantom success or repeat failure");next.OnRemoveBehavior();
  Campaign.Current.Search=new();second.Active=false;var none=new GwpCitySearchBodyTransport{Mission=m};none.TakeTarget(body);none.OnMissionTick(.1f);
  Check(Campaign.Current.Search.Failures==1&&none.Carrier==null,"no surviving actual follower means extraction fails, no summoned carrier");
  Console.WriteLine("CITY_SEARCH_BODY_TRANSPORT_ASSERTIONS="+n);
 }
}}
'''


class CitySearchTransportTests(unittest.TestCase):
    def test_production_carrier_replacement_and_physical_delivery(self):
        source = read('GwpCitySearchBodyTransport.cs')
        source = source[source.index('namespace GreyWardenPolicePurity'):source.index('    /// <summary>\n    /// 只改原版')]+ '}\n'
        start = source.index('        private static readonly Action<')
        end = source.index('        private Agent? _body;', start)
        source = source[:start] + r'''
        private static readonly Action<CorpseDraggingMissionLogic,Agent,sbyte> SetBody=(b,a,i)=>b.Attach(a);
        private static Agent? DraggedBody(CorpseDraggingMissionLogic b)=>b.NativeBody;
''' + source[end:]
        code = STUBS + source + SCENARIO
        run(code)
        for mutation, error in [
            (code.replace('if (affectedAgent == Carrier) ReleaseCarrier();', ''), 'fallen carrier releases original corpse'),
            (code.replace('_body.Position.Distance(position)', 'Agent.Main.Position.Distance(position)'), 'player outside while body remains inside'),
            (code.replace('MoveCarrier(_body.GetWorldPosition(), false);', 'MoveCarrier(Agent.Main.GetWorldPosition(), false);'), 'replacement approaches the same body'),
        ]:
            with self.subTest(negative_control=error), self.assertRaisesRegex(AssertionError, error):
                run(mutation)


class CitySearchCorpseFadeTests(unittest.TestCase):
    def test_search_scene_keeps_corpses_like_native_drag_missions(self):
        """Live 2026-10-22: with corpse fade-out on, a registered corpse has no visuals entity and native
        SetDraggedCorpse dereferences it (BODY_ATTACH_TRY corpse=True entity=False, then the crash).
        Every installed mission that adds CorpseDraggingMissionLogic disables corpse fade-out first."""
        import subprocess
        from pathlib import Path
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        native = subprocess.check_output(['ilspycmd', '-t', 'SandBox.SandBoxMissions',
            str(game / 'Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll')], text=True, encoding='utf-8-sig')
        openers = re.split(r'\n\t\[MissionMethod', native)
        dragging = [body for body in openers if 'new CorpseDraggingMissionLogic' in body]
        self.assertGreaterEqual(len(dragging), 2)
        for body in dragging:
            self.assertRegex(body, r'\.DisableCorpseFadeOut = true;')

        def keeps_corpses(source):
            opener = source[source.index('internal static void Open('):source.index('private static MissionBehavior[] Behaviors(')]
            return 'new GwpCitySearchBodyTransport()' in source and re.search(
                r'(\w+)\.DisableCorpseFadeOut = true;[\s\S]*MissionState\.OpenNew\(MissionName, \1,', opener) is not None

        production = read('GwpCitySearchMission.cs')
        self.assertTrue(keeps_corpses(production), 'search scene with body transport must disable corpse fade-out')
        self.assertFalse(keeps_corpses(production.replace('record.DisableCorpseFadeOut = true;', '')),
                         'negative control: without the setting the check must fail')


EARLY_INIT = r'''
using System;using System.Collections.Generic;using HarmonyLib;
// ActionIndexCache shape: beforefieldinit struct whose statics resolve names through the engine's action table.
public struct Cache{public static readonly Cache None=new Cache(-1);public static readonly Cache Spectator=Make("act_arena_spectator");public int Index;
 private Cache(int i){Index=i;}private static Cache Make(string n)=>new Cache(Program.ActionsLoaded?7:-1);}
public class Mission{public object MainAgent=>null;}
// CorpseDraggingMissionLogic.OnMissionTick shape: reads MainAgent and compares with Cache.None.
public class Logic{public Mission Mission=new Mission();public virtual void OnMissionTick(float dt){object m=Mission.MainAgent;if(m!=null||Program.Action!=Cache.None.Index)Program.Ticks++;}}
public static class Carrier{public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> i)=>i;}
public static class Program{public static bool ActionsLoaded;public static int Action=-1,Ticks;
 static void Patch()=>new Harmony("exp").Patch(AccessTools.Method(typeof(Logic),nameof(Logic.OnMissionTick)),transpiler:new HarmonyMethod(typeof(Carrier),nameof(Carrier.Transpiler)));
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]static int Spectator()=>Cache.Spectator.Index;
 public static int Main(string[] a){
  if(a[0]=="load")Patch();      // patched during module initialization
  ActionsLoaded=true;            // the engine reads action_types.xml after module initialization
  if(a[0]=="lazy")Patch();       // patched on first search scene
  Console.WriteLine("SPECTATOR="+Spectator());return 0;}}
'''


class CitySearchCarrierPatchTimingTests(unittest.TestCase):
    def test_carrier_patch_is_applied_after_actions_load(self):
        """Live 2026-10-22: the arena audience crashed natively prefetching act_arena_spectator with index -1.
        Harmony-patching a method that reads ActionIndexCache runs its static initializer at patch time; module
        initialization precedes action_types.xml, so a load-time patch freezes every action index at -1."""
        import shutil
        import subprocess
        import tempfile
        from pathlib import Path
        from test_city_search import MODULE
        with tempfile.TemporaryDirectory(prefix='gwp-early-init-') as temp:
            p = Path(temp)
            shutil.copy(MODULE / 'lib' / '0Harmony.dll', p / '0Harmony.dll')
            (p / 'Exp.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><LangVersion>10.0</LangVersion><PlatformTarget>x64</PlatformTarget></PropertyGroup><ItemGroup><Reference Include="0Harmony"><HintPath>0Harmony.dll</HintPath></Reference></ItemGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(EARLY_INIT, encoding='utf-8')
            build = subprocess.run(['dotnet', 'build', temp, '-c', 'Release', '-v', 'quiet', '-nologo'], capture_output=True, text=True)
            self.assertEqual(0, build.returncode, build.stdout + build.stderr)
            exe = p / 'bin' / 'Release' / 'net472' / 'Exp.exe'
            index = {mode: subprocess.check_output([str(exe), mode], text=True).strip() for mode in ('none', 'load', 'lazy')}
        self.assertEqual('SPECTATOR=7', index['none'])
        self.assertEqual('SPECTATOR=-1', index['load'], 'negative control: a load-time patch freezes the action index')
        self.assertEqual('SPECTATOR=7', index['lazy'])
        transport = read('GwpCitySearchBodyTransport.cs')
        patch = transport[transport.index('    internal static class GwpCitySearchBodyCarrierPatch') - 600:]
        self.assertNotIn('[HarmonyPatch', patch, 'the module-load sweep must not apply the carrier patch')
        self.assertIn('GwpCitySearchBodyCarrierPatch.EnsureApplied();', read('GwpCitySearchMission.cs'))


if __name__ == '__main__':
    unittest.main()
