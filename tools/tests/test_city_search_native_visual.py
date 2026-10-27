"""Execute the installed native fault instructions and the production in-memory repair.

Only the 83-byte fault region runs in an isolated executable page. Continuations
return sentinels; this does not start Bannerlord or exercise its renderer.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

import pefile

ROOT = Path(__file__).resolve().parents[2]
NATIVE = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Native.dll')

HARNESS = r'''
namespace GreyWardenPolicePurity{
 static class GwpAiDiagnostics{public static string Last="";public static void WriteCitySearch(string s,string d){Last=s+";"+d;}}
 static class NativeVisualProbe{
  [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
  delegate int Probe(System.IntPtr entity,System.IntPtr observed);
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]
  static extern System.IntPtr VirtualAlloc(System.IntPtr a,System.UIntPtr n,uint t,uint p);
  [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
  static extern System.IntPtr LoadLibraryEx(string path,System.IntPtr file,uint flags);
  [System.Runtime.InteropServices.DllImport("kernel32.dll")]
  static extern bool FreeLibrary(System.IntPtr module);
  [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
  delegate int EmptyResource(System.IntPtr manager,int index,System.IntPtr entity,int flags);
  static int n;
  static void Check(bool b,string why){n++;if(!b)throw new System.Exception(why);}
  static void Put(System.IntPtr a,byte[] b)=>System.Runtime.InteropServices.Marshal.Copy(b,0,a,b.Length);
  static byte[] Read(System.IntPtr a,int len){var b=new byte[len];System.Runtime.InteropServices.Marshal.Copy(a,b,0,len);return b;}
  static void Jump(System.IntPtr from,System.IntPtr to){var bytes=new System.Collections.Generic.List<byte>{0xE9};bytes.AddRange(System.BitConverter.GetBytes(checked((int)(to.ToInt64()-from.ToInt64()-5))));Put(from,bytes.ToArray());}
  [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
  [System.Security.SecurityCritical]
  static void OldFault(System.IntPtr site){
   var original=(Probe)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(site,typeof(Probe));
   bool reproduced=false;
   try{original(System.IntPtr.Zero,System.IntPtr.Zero);}
   catch(System.AccessViolationException){reproduced=true;} // 依据：两份WER同为读取0x170；此处仅验证旧指令必定重现。
   Check(reproduced,"installed old instruction must reproduce its null-entity access violation");
   System.Console.WriteLine("OLD_NATIVE_NULL_ENTITY_REPRODUCED");
  }
  static int Main(string[] args){
   var context=System.IO.File.ReadAllBytes(args[0]);
   int size=GwpCitySearchNativeVisualFix.PatchRva+4096;
   var image=VirtualAlloc(System.IntPtr.Zero,(System.UIntPtr)size,0x3000,0x40);
   Check(image!=System.IntPtr.Zero,"allocate isolated native image");
   var site=System.IntPtr.Add(image,GwpCitySearchNativeVisualFix.PatchRva);Put(site,context);
   if(args[1]=="old"){OldFault(site);return 0;}
   Check(!GwpCitySearchNativeVisualFix.TryInstall(image,GwpCitySearchNativeVisualFix.PatchRva+1),"undersized images are excluded before native reads");
   System.Runtime.InteropServices.Marshal.WriteByte(site,60,(byte)(context[60]^1));
   Check(!GwpCitySearchNativeVisualFix.TryInstall(image,size),"changed continuation bytes exclude a different binary");
   Check(Read(site,20)[0]==context[0],"signature rejection leaves original instructions untouched");Put(site,context);
   Check(GwpCitySearchNativeVisualFix.TryInstall(image,size),"installed native signature accepts production repair");
   byte[] installed=Read(site,20);Check(installed[0]==0xFF&&installed[1]==0x25,"production installer writes the absolute jump");
   Check(GwpCitySearchNativeVisualFix.TryInstall(image,size)&&System.Linq.Enumerable.SequenceEqual(installed,Read(site,20)),"installation is idempotent");
   // The real continuation code is outside this isolated instruction test. Save RCX and return its branch identity.
   var empty=System.IntPtr.Add(image,0x200);var parentless=System.IntPtr.Add(image,0x220);var parent=System.IntPtr.Add(image,0x240);
   Put(empty,new byte[]{0x48,0x89,0x0A,0xB8,1,0,0,0,0xC3});
   Put(parentless,new byte[]{0x48,0x89,0x0A,0xB8,2,0,0,0,0xC3});
   Put(parent,new byte[]{0x48,0x89,0x0A,0x8B,0x44,0x24,0x28,0xC3});
   Jump(System.IntPtr.Add(site,0x36),empty);Jump(System.IntPtr.Add(site,0x31),parentless);Jump(System.IntPtr.Add(site,20),parent);
   // Provide the original function's local stack area for its [rsp+28h] argument write.
   var wrapper=new System.Collections.Generic.List<byte>{0x48,0x83,0xEC,0x48,0x48,0xB8};
   wrapper.AddRange(System.BitConverter.GetBytes(site.ToInt64()));wrapper.AddRange(new byte[]{0xFF,0xD0,0x48,0x83,0xC4,0x48,0xC3});Put(image,wrapper.ToArray());
   var invoke=(Probe)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(image,typeof(Probe));
   var entity=System.Runtime.InteropServices.Marshal.AllocHGlobal(512);var observed=System.Runtime.InteropServices.Marshal.AllocHGlobal(16);
   System.Runtime.InteropServices.Marshal.WriteIntPtr(entity,0x170,System.IntPtr.Zero);
   Check(invoke(System.IntPtr.Zero,observed)==1,"null entity reaches the native empty-resource continuation without AV");
   Check(System.Runtime.InteropServices.Marshal.ReadIntPtr(observed)==System.IntPtr.Zero,"empty path preserves RCX and RDX");
   Check(invoke(entity,observed)==2&&System.Runtime.InteropServices.Marshal.ReadIntPtr(observed)==entity,"valid entity without parent preserves native branch and registers");
   System.Runtime.InteropServices.Marshal.WriteIntPtr(entity,0x170,new System.IntPtr(1234));
   Check(invoke(entity,observed)==0x27&&System.Runtime.InteropServices.Marshal.ReadIntPtr(observed)==entity,"valid parent preserves native stack argument and registers");
   for(int i=0;i<10000;i++)if(invoke(System.IntPtr.Zero,observed)!=1||invoke(entity,observed)!=0x27)throw new System.Exception("stack or repeated branch corruption");
   Check(true,"repeated empty and valid updates preserve stack and return addresses");
   // Map the installed DLL without resolving imports or running DllMain. Its null-entity resource path touches no engine state.
   var native=LoadLibraryEx(args[2],System.IntPtr.Zero,1);
   Check(native!=System.IntPtr.Zero,"map installed native DLL without initializing the engine");
   var emptyResource=(EmptyResource)System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(System.IntPtr.Add(native,0x5AF3F0),typeof(EmptyResource));
   Check(emptyResource(System.IntPtr.Zero,14,System.IntPtr.Zero,8)==-1,"actual installed resource function already handles a null entity");
   FreeLibrary(native);
#if GWP_DIAGNOSTICS
   GwpCitySearchNativeVisualFix.WriteDiagnostics();
   Check(GwpAiDiagnostics.Last.Contains("installed=True")&&GwpAiDiagnostics.Last.Contains("emptyEntityUpdates=10001"),"diagnostic counter counts only missing entities");
#else
   Check(typeof(GwpCitySearchNativeVisualFix).GetMethod("WriteDiagnostics",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)==null,"player build contains no diagnostic callback");
#endif
   System.Runtime.InteropServices.Marshal.FreeHGlobal(entity);System.Runtime.InteropServices.Marshal.FreeHGlobal(observed);
   System.Console.WriteLine("NATIVE_VISUAL_REPAIR_ASSERTIONS="+n);return 0;
  }
 }
}
'''


class CitySearchNativeVisualTests(unittest.TestCase):
    def test_installed_native_fault_and_production_repair_in_both_builds(self):
        context = pefile.PE(str(NATIVE)).get_data(0x5AEAB6, 0x53)
        source = (ROOT / 'GreyWardenPolicePurity/GwpCitySearchNativeVisualFix.cs').read_text(encoding='utf-8-sig')
        with tempfile.TemporaryDirectory(prefix='gwp-native-visual-') as folder:
            temp = Path(folder)
            (temp / 'native.bin').write_bytes(context)
            (temp / 'Program.cs').write_text(source + HARNESS, encoding='utf-8')
            for diagnostic in (True, False):
                with self.subTest(diagnostic=diagnostic):
                    define = '<DefineConstants>GWP_DIAGNOSTICS</DefineConstants>' if diagnostic else ''
                    (temp / 'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><PlatformTarget>x64</PlatformTarget><LangVersion>10</LangVersion><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers>' + define + '</PropertyGroup></Project>', encoding='utf-8')
                    built = subprocess.run(['dotnet', 'build', str(temp / 'Probe.csproj'), '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
                    self.assertEqual(built.returncode, 0, built.stdout + built.stderr)
                    exe = temp / 'bin/Release/net472/Probe.exe'
                    for mode in ('old', 'new'):
                        result = subprocess.run([str(exe), str(temp / 'native.bin'), mode, str(NATIVE)], capture_output=True, text=True)
                        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                        print(('DIAGNOSTIC ' if diagnostic else 'PLAYER ') + result.stdout.strip())


if __name__ == '__main__':
    unittest.main()
