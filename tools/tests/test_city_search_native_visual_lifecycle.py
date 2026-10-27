"""Run the installed complete native updater in an isolated executable image.

Allocator/hash lookup helpers use controlled fixtures; no engine is initialized.
Unlike the small trampoline test, the old negative control runs through the
actual null-resource return and subsequent index consumption to the crash.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

import pefile

ROOT = Path(__file__).resolve().parents[2]
NATIVE = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Native.dll')

HARNESS = r'''
namespace GreyWardenPolicePurity {
 using System;
 using System.Collections.Generic;
 using System.Runtime.InteropServices;
 static class GwpAiDiagnostics { internal static string Last=""; internal static void WriteCitySearch(string a,string b) { Last=a+";"+b; } }
 static class WholeUpdaterProbe {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Update(IntPtr manager,IntPtr input);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int FaultHandler(IntPtr exception);
  [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr a,UIntPtr n,uint t,uint p);
  [DllImport("kernel32.dll")] static extern IntPtr AddVectoredExceptionHandler(uint first,FaultHandler handler);
  static readonly FaultHandler Observer = OnFault;
  static IntPtr image; static long expectedFault; static int assertions;
  static void Check(bool value,string why){assertions++;if(!value)throw new Exception(why);}
  static IntPtr At(IntPtr p,int offset)=>IntPtr.Add(p,offset);
  static IntPtr Allocate(int size)=>VirtualAlloc(IntPtr.Zero,(UIntPtr)size,0x3000,0x40);
  static void Put(IntPtr p,byte[] bytes)=>Marshal.Copy(bytes,0,p,bytes.Length);
  static void Ptr(IntPtr p,int offset,IntPtr value)=>Marshal.WriteIntPtr(p,offset,value);
  static void I32(IntPtr p,int offset,int value)=>Marshal.WriteInt32(p,offset,value);
  static void Stub(int rva,byte[] bytes)=>Put(At(image,rva),bytes);
  static byte[] Increment(IntPtr counter){var b=new List<byte>{0x48,0xB8};b.AddRange(BitConverter.GetBytes(counter.ToInt64()));b.AddRange(new byte[]{0xFF,0x00,0xC3});return b.ToArray();}
  static int OnFault(IntPtr pointers){
   IntPtr record=Marshal.ReadIntPtr(pointers);int code=Marshal.ReadInt32(record);long address=Marshal.ReadIntPtr(record,16).ToInt64()-image.ToInt64();
   Console.WriteLine("NATIVE_FAULT code=0x"+unchecked((uint)code).ToString("X")+"; rva=0x"+address.ToString("X"));
   Environment.Exit(unchecked((uint)code)==0xC0000005 && address==expectedFault ? 0 : 97);return 0;
  }
  static int Main(string[] args){
   image=Allocate(0xECA000);Put(image,System.IO.File.ReadAllBytes(args[0]));
   string mode=args[1];bool old=mode=="unpatched"||mode=="previous";
   if(!old){
    Check(!GwpCitySearchNativeVisualFix.TryInstall(image,0x5AF333),"undersized function image is rejected");
    byte last=Marshal.ReadByte(image,0x5AF332);Marshal.WriteByte(image,0x5AF332,(byte)(last^1));
    Check(!GwpCitySearchNativeVisualFix.TryInstall(image,0xECA000),"modified native epilogue is rejected before patching");
    Check(Marshal.ReadByte(image,GwpCitySearchNativeVisualFix.PatchRva)==0x49,"failed signature check leaves native instructions untouched");
    Marshal.WriteByte(image,0x5AF332,last);
   }
   if(mode!="unpatched"){
    Check(GwpCitySearchNativeVisualFix.TryInstall(image,0xECA000),"production installer accepts installed native function");
    Check(GwpCitySearchNativeVisualFix.TryInstall(image,0xECA000),"installation is idempotent");
   }
   var manager=Allocate(0x4000);var visual=Allocate(0x1000);var input=Allocate(16);Ptr(input,0,visual);I32(input,8,34);
   var recordPage=Allocate(0x20000);var recordPages=Allocate(16);Ptr(recordPages,0,recordPage);Ptr(manager,0x2C68,recordPages);I32(manager,0x2C88,1023);
   var freeIndices=Allocate(16);Ptr(manager,0x2C48,freeIndices);Ptr(manager,0x2C50,At(freeIndices,4));
   var buckets=Allocate(16);var sentinel=new IntPtr(1);Ptr(buckets,8,sentinel);Ptr(manager,0x2C98,buckets);Marshal.WriteInt64(manager,0x2CA0,1);
   var newNode=Allocate(32);var counters=Allocate(32);
   // The actual updater receives a deterministic existing-cache lookup. All
   // cleanup instructions remain original; only allocator/resource helpers are stubbed.
   var lookup=new List<byte>{0x48,0xB8};lookup.AddRange(BitConverter.GetBytes(buckets.ToInt64()));lookup.AddRange(new byte[]{0x48,0x8B,0x08,0x48,0x85,0xC9,0x75,7,0x48,0xC7,0xC1,1,0,0,0,0x48,0x89,0x0A,0x48,0x89,0x42,8,0xC3});Stub(0x5B9800,lookup.ToArray());
   Stub(0x776090,new byte[]{0xC6,0x02,0,0xC3});
   var alloc=new List<byte>{0x48,0xB8};alloc.AddRange(BitConverter.GetBytes(newNode.ToInt64()));alloc.Add(0xC3);Stub(0x729B40,alloc.ToArray());
   Stub(0x9DF5B0,new byte[]{0xC3}); // security-cookie helper; native null path otherwise runs verbatim
   I32(manager,0x2CA0,1); // map divisor/mask
   Marshal.WriteInt64(At(image,0xDA9D60),1); // installed global enables the first faulting detach path
   // -1 selects page[-1] and slot[1023], exactly as in the 23:23:25 dump.
   var resources=Allocate(24);Marshal.WriteInt64(resources,0x40507613DEDA158A);Ptr(manager,0x2AF8,At(resources,8));I32(manager,0x2B18,1023);
   if(mode=="existing"){
    var node=Allocate(32);Ptr(node,0,visual);I32(node,8,34);I32(node,16,0);Ptr(buckets,0,node);Marshal.WriteInt64(manager,0x2CA8,1);
    Stub(0x5BC9A0,Increment(counters));Stub(0x5B390,Increment(At(counters,4)));Stub(0x57A380,Increment(At(counters,8)));Stub(0x74E270,Increment(At(counters,12)));
   }
   if(mode=="valid"){
    Ptr(visual,0x8B8,new IntPtr(1));
    // Observe the first original instruction after the patched allocation gate.
    // Restoring registers/stack uses the actual updater epilogue, never a leaf sentinel.
    var branch=new List<byte>{0x48,0xB8};branch.AddRange(BitConverter.GetBytes(counters.ToInt64()));branch.AddRange(new byte[]{0xFF,0x00,0xE9});branch.AddRange(BitConverter.GetBytes(0x5AF318-(0x5AE8CA+branch.Count+4)));Stub(0x5AE8CA,branch.ToArray());
   }
   expectedFault=old?(mode=="unpatched"?0x5AEAB6:0x5AF215):-1;
   Check(AddVectoredExceptionHandler(1,Observer)!=IntPtr.Zero,"install isolated fault observer");
   var invoke=(Update)Marshal.GetDelegateForFunctionPointer(At(image,0x5AE760),typeof(Update));
   invoke(manager,input);
   Check(!old,"negative control must fault in the actual native updater");
   if(mode=="valid"){
    Check(Marshal.ReadInt32(counters)==1,"nonnull entity resumes original allocation with original stack/registers");
    for(int j=0;j<1000;j++)invoke(manager,input);
    Check(Marshal.ReadInt32(counters)==1001,"repeated valid calls preserve the native stack and nonvolatile registers");
   }
   else {
    Check(Marshal.ReadInt64(manager,0x2CA8)==0,"null visual has no cache entry after update");
    Check(Marshal.ReadInt32(manager,0x2C38)==0,"null visual allocates no new cache slot");
    Check(Marshal.ReadIntPtr(manager,0x2C50)==At(freeIndices,4),"null visual leaves free list untouched");
    if(mode=="existing"){
     Check(Marshal.ReadInt32(recordPage)==-1&&Marshal.ReadIntPtr(buckets)==IntPtr.Zero,"existing cache record and lookup are removed before null return");
     for(int j=0;j<4;j++)Check(Marshal.ReadInt32(counters,j*4)==1,"original cache cleanup helper executes exactly once: "+j);
    }
    for(int j=0;j<1000;j++)invoke(manager,input);
    Check(Marshal.ReadInt64(manager,0x2CA8)==0&&Marshal.ReadInt32(manager,0x2C38)==0,"repeated invalid updates create no phantom resources");
#if GWP_DIAGNOSTICS
    Check(GwpCitySearchNativeVisualFix.TryReadLastUpdate(out ulong last,out long count)&&last==unchecked((ulong)visual.ToInt64())&&count==1001,"full native update preserves trace pointer and sequence");
    System.Threading.Tasks.Parallel.For(0,4,_=>{for(int j=0;j<500;j++)invoke(manager,input);});
    Check(GwpCitySearchNativeVisualFix.TryReadLastUpdate(out last,out count)&&last==unchecked((ulong)visual.ToInt64())&&count==3001,"parallel native writers publish a coherent record");
    var tracePage=(IntPtr)typeof(GwpCitySearchNativeVisualFix).GetField("_guardPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null);
    Marshal.WriteInt64(tracePage,264,1);Check(!GwpCitySearchNativeVisualFix.TryReadLastUpdate(out _,out _),"managed reader rejects an unpublished record without waiting");
    GwpCitySearchNativeVisualFix.WriteDiagnostics();Check(GwpAiDiagnostics.Last=="","busy native sample is deferred");
    Marshal.WriteInt64(tracePage,264,0);GwpCitySearchNativeVisualFix.WriteDiagnostics();
    Check(GwpAiDiagnostics.Last.Contains("installed=True")&&GwpAiDiagnostics.Last.Contains("emptyEntityUpdates=3001"),"deferred native sample is not lost");
#else
    Check(typeof(GwpCitySearchNativeVisualFix).GetMethod("WriteDiagnostics",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)==null,"player build excludes diagnostic callback");
#endif
   }
   Console.WriteLine("WHOLE_UPDATER_"+mode.ToUpperInvariant()+"_ASSERTIONS="+assertions);return 0;
  }
 }
}
'''


class NativeVisualLifecycleTests(unittest.TestCase):
    def test_complete_native_updater_negative_control_and_fix(self):
        original = pefile.PE(str(NATIVE)).get_memory_mapped_image()
        previous = subprocess.check_output(['git', 'show', 'b242d20:GreyWardenPolicePurity/GwpCitySearchNativeVisualFix.cs'], cwd=ROOT).decode('utf-8-sig')
        current = (ROOT / 'GreyWardenPolicePurity/GwpCitySearchNativeVisualFix.cs').read_text(encoding='utf-8-sig')
        with tempfile.TemporaryDirectory(prefix='gwp-native-lifecycle-') as directory:
            temp = Path(directory)
            (temp / 'native.bin').write_bytes(original)
            for label, source, diagnostic, modes in [
                ('OLD', previous, True, ('unpatched', 'previous')),
                ('DIAGNOSTIC', current, True, ('null', 'existing', 'valid')),
                ('PLAYER', current, False, ('null', 'existing', 'valid')),
            ]:
                (temp / 'Program.cs').write_text(source + HARNESS, encoding='utf-8')
                define = '<DefineConstants>GWP_DIAGNOSTICS</DefineConstants>' if diagnostic else ''
                (temp / 'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><PlatformTarget>x64</PlatformTarget><LangVersion>10</LangVersion><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers>'+define+'</PropertyGroup></Project>', encoding='utf-8')
                built = subprocess.run(['dotnet', 'build', str(temp / 'Probe.csproj'), '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
                self.assertEqual(built.returncode, 0, built.stdout + built.stderr)
                for mode in modes:
                    with self.subTest(label=label, mode=mode):
                        result = subprocess.run([str(temp / 'bin/Release/net472/Probe.exe'), str(temp / 'native.bin'), mode], capture_output=True, text=True)
                        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                        print(label, result.stdout.strip())


if __name__ == '__main__':
    unittest.main()
