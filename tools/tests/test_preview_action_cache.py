"""Investigate preview action caches using installed net472 DLLs and real Harmony.

Only the engine's name resolver is replaced: it returns -1 before the simulated
action-table load and 1234 afterwards. No game or render scene is launched.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_city_search import read, block
from test_city_search_flow import run


PROBE = r'''$ErrorActionPreference='Stop'
$game='D:\steam\steamapps\common\Mount & Blade II Bannerlord'
$binaryDir=Join-Path $game 'bin\Win64_Shipping_Client'
$live=Join-Path $game 'Modules\GreyWarden\bin\Win64_Shipping_Client'
$mb=[Reflection.Assembly]::LoadFrom((Join-Path $binaryDir 'TaleWorlds.MountAndBlade.dll'))
$candidates=@(Get-ChildItem $binaryDir -Filter '*.dll' | Where-Object Name -ne 'TaleWorlds.Native.dll')
foreach($directory in Get-ChildItem (Join-Path $game 'Modules') -Directory){
 if($directory.Name -eq 'GreyWarden'){continue}
 $path=Join-Path $directory.FullName 'bin\Win64_Shipping_Client'
 if(Test-Path $path){$candidates+=Get-ChildItem $path -Filter '*.dll'}
}
foreach($file in $candidates){try{[void][Reflection.Assembly]::LoadFrom($file.FullName)}catch{}}
[void][Reflection.Assembly]::LoadFrom((Join-Path $live '0Harmony.dll'))
$module=[Reflection.Assembly]::LoadFrom((Join-Path $live 'GreyWardenPolicePurity.dll'))
Add-Type -ReferencedAssemblies (Join-Path $live '0Harmony.dll') -TypeDefinition '
using System;using System.Reflection;using HarmonyLib;
public static class PreviewCacheProbe{
 public static bool Ready;public static int EarlyCalls,Calls;public static string FirstStack,PatchName;
 public static bool Resolve(string __0,ref int __result){
  Calls++;if(!Ready){EarlyCalls++;if(FirstStack==null)FirstStack="PATCH="+PatchName+"\n"+Environment.StackTrace;}
  __result=Ready?1234:-1;return false;
 }
 public static void Execute(Assembly module,Assembly mb,string mode){
  var harmony=new Harmony("gwp.preview.offline.probe");
  var resolver=mb.GetType("TaleWorlds.MountAndBlade.MBAnimation").GetMethod("GetActionCodeWithName");
  harmony.Patch(resolver,prefix:new HarmonyMethod(typeof(PreviewCacheProbe).GetMethod("Resolve")));
  Console.WriteLine("BEFORE_PATCH_CALLS="+Calls);
  if(mode=="early")new PatchClassProcessor(harmony,module.GetType("GreyWardenPolicePurity.GwpCitySearchFadeDiagnosticsPatch",true)).Patch();
  if(mode=="all")foreach(var type in module.GetTypes()){
   PatchName=type.FullName;
   try{new PatchClassProcessor(harmony,type).Patch();}catch(Exception e){Console.WriteLine("PATCH_FAIL="+type.Name+":"+e.GetType().Name);}
  }
  Console.WriteLine("AFTER_PATCH_EARLY_CALLS="+EarlyCalls);
  var cache=mb.GetType("TaleWorlds.MountAndBlade.ActionIndexCache");
  if(mode=="forced")cache.GetField("act_inventory_idle_start").GetValue(null);
  Ready=true;
  if(mode=="install")module.GetType("GreyWardenPolicePurity.GwpCharacterPreviewDiagnostics",true).GetMethod("Install",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
  var idle=cache.GetField("act_inventory_idle_start").GetValue(null);
  Console.WriteLine("INVENTORY_IDLE_INDEX="+cache.GetProperty("Index").GetValue(idle,null));
  Console.WriteLine("DYNAMIC_AFTER_LOAD_INDEX="+resolver.Invoke(null,new object[]{"act_inventory_idle_start"}));
  if(mode=="late")new PatchClassProcessor(harmony,module.GetType("GreyWardenPolicePurity.GwpCitySearchFadeDiagnosticsPatch",true)).Patch();
  Console.WriteLine("TOTAL_CALLS="+Calls+"; EARLY_CALLS="+EarlyCalls);
  if(FirstStack!=null)Console.WriteLine(FirstStack);
 }
}
'
[PreviewCacheProbe]::Execute($module,$mb,$args[0])
'''


class PreviewActionCacheTests(unittest.TestCase):
    def test_fadeout_patch_before_and_after_engine_action_table(self):
        outputs = {}
        with tempfile.TemporaryDirectory(prefix='gwp-preview-investigation-') as temp:
            path = Path(temp) / 'probe.ps1'
            path.write_text(PROBE, encoding='utf-8-sig')
            for mode in ('early', 'late', 'all', 'forced', 'install'):
                result = subprocess.run(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                                         '-File', str(path), mode], capture_output=True, text=True,
                                        encoding='utf-8', errors='replace')
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                outputs[mode] = result.stdout
                print('MODE=' + mode + '\n' + result.stdout)
        self.assertIn('BEFORE_PATCH_CALLS=0', outputs['early'])
        self.assertIn('INVENTORY_IDLE_INDEX=1234', outputs['early'])
        self.assertIn('DYNAMIC_AFTER_LOAD_INDEX=1234', outputs['early'])
        self.assertIn('INVENTORY_IDLE_INDEX=1234', outputs['late'])
        self.assertIn('EARLY_CALLS=0', outputs['late'])
        self.assertIn('INVENTORY_IDLE_INDEX=1234', outputs['all'])
        self.assertIn('EARLY_CALLS=0', outputs['all'])
        self.assertNotIn('PATCH_FAIL=', outputs['all'])
        self.assertIn('INVENTORY_IDLE_INDEX=-1', outputs['forced'])
        self.assertIn('DYNAMIC_AFTER_LOAD_INDEX=1234', outputs['forced'])
        self.assertIn('INVENTORY_IDLE_INDEX=1234', outputs['install'])

    def test_preview_observer_is_bounded_and_skips_finalized_visuals(self):
        source = block(read('GwpCharacterPreviewDiagnostics.cs'), r'internal static class GwpCharacterPreviewDiagnostics')
        source = source.replace('private static void Observe', 'public static void Observe')
        run(r'''
using System;using System.Runtime.CompilerServices;using Stopwatch=FakeStopwatch;
static class FakeStopwatch{public static long Now;public const long Frequency=1000;public static long GetTimestamp()=>Now;}
class CharacterTableau{public void OnTick(float dt){}}
class Harmony{public static int Calls;public Harmony(string id){}public void Patch(object target,object postfix){Calls++;}}
class HarmonyMethod{public HarmonyMethod(Type type,string method){}}
static class AccessTools{public static object Method(Type t,string method)=>new();}
static class SubModule{public const string HarmonyId="test";}
struct Vec3{public float X,Y,Z;public Vec3(float x,float y,float z){X=x;Y=y;Z=z;}public override string ToString()=>X+","+Y+","+Z;}
struct Mat3{public Vec3 u,f;}struct MatrixFrame{public Mat3 rotation;}
struct MBActionSet{public string GetName()=>"as_human_warrior";}
struct ActionIndexCache{public static ActionIndexCache act_inventory_idle_start=new(){Index=1234};public int Index;}
static class MBAnimation{public static int GetActionCodeWithName(string name)=>1234;}
class Skeleton{public MatrixFrame Root;public MatrixFrame GetBoneEntitialFrame(int bone)=>Root;public ActionIndexCache GetActionAtChannel(int channel)=>new(){Index=1234};}
class Entity{public MatrixFrame Local,Global;public Skeleton Skeleton=new();public MatrixFrame GetFrame()=>Local;public MatrixFrame GetGlobalFrame()=>Global;}
class Visual{public UIntPtr Pointer;}
class AgentVisuals{public Visual Visual=new(){Pointer=(UIntPtr)1};public Entity Entity=new();public int Reads;public bool Gone;public Visual GetVisuals(){Read();return Visual;}public Entity GetEntity(){Read();return Entity;}void Read(){Reads++;if(Gone)throw new Exception("read finalized native visual");}}
static class GwpAiDiagnostics{public static int Lines;public static string Last="";public static void WriteCharacterPreview(string details){Lines++;Last=details;}}
SOURCE
static class Program{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
 static void Sample(CharacterTableau t,AgentVisuals? v,bool dirty=false,bool finalized=false,bool enabled=true,int loading=0,int mountLoading=0)=>GwpCharacterPreviewDiagnostics.Observe(t,v,null,"player",dirty,finalized,enabled,loading,mountLoading,new(),new());
 static void Main(){
  GwpCharacterPreviewDiagnostics.Install();GwpCharacterPreviewDiagnostics.Install();Check(Harmony.Calls==1,"late observer binds only once per process");
  var t=new CharacterTableau();var gone=new AgentVisuals{Gone=true};Sample(t,gone,dirty:true);Sample(t,gone,finalized:true);Sample(t,null);Check(gone.Reads==0&&GwpAiDiagnostics.Lines==0,"dirty/finalized/missing visuals are never read");
  Sample(t,gone,enabled:false);Sample(t,gone,loading:1);Sample(t,gone,mountLoading:1);Check(gone.Reads==0&&GwpAiDiagnostics.Lines==0,"hidden and loading previews are never read");
  var v=new AgentVisuals();v.Entity.Global.rotation.u=new(0,0,1);v.Entity.Local.rotation.u=new(0,0,1);v.Entity.Skeleton.Root.rotation.u=new(1,0,0);
  Sample(t,v);Check(GwpAiDiagnostics.Lines==1,"first settled visual is sampled");Check(GwpAiDiagnostics.Last.Contains("cachedIdle=1234; resolvedIdle=1234")&&GwpAiDiagnostics.Last.Contains("globalUp=0,0,1; rootUp=1,0,0"),"trace distinguishes cached action, entity frame and tilted root pose");
  int reads=v.Reads;FakeStopwatch.Now=100;Sample(t,v);Check(GwpAiDiagnostics.Lines==1&&v.Reads==reads+1,"within interval only visual identity is read");
  FakeStopwatch.Now=500;Sample(t,v);FakeStopwatch.Now=1000;Sample(t,v);Check(GwpAiDiagnostics.Lines==3,"three spaced samples record settled animation");
  FakeStopwatch.Now=1500;Sample(t,v);Check(GwpAiDiagnostics.Lines==3,"per-visual limit stops output");
  v.Visual.Pointer=(UIntPtr)2;Sample(t,v);Check(GwpAiDiagnostics.Lines==4,"a replaced visual gets a fresh bounded window");
  for(int i=0;i<100;i++){FakeStopwatch.Now+=1000;Sample(new(),new());}Check(GwpAiDiagnostics.Lines==60,"process limit bounds all preview windows");
  reads=gone.Reads;Sample(new(),gone);Check(gone.Reads==reads,"exhausted budget does not touch native visuals");
  Check(v.Entity.Global.rotation.u.Z==1&&v.Entity.Skeleton.Root.rotation.u.X==1,"observation preserves entity and skeleton pose");
  Console.WriteLine("PREVIEW_OBSERVER_ASSERTIONS="+n);
 }
}
'''.replace('SOURCE', source))


if __name__ == '__main__':
    unittest.main()
