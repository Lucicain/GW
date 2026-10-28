"""Execute installed 1.4.8 MapEventParty snapshot and native spawn priority model."""
import subprocess
import tempfile
import unittest
from pathlib import Path


class NativePriorityTests(unittest.TestCase):
    def test_detached_snapshot_and_native_order(self):
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')
        code = r'''
using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Party;using TaleWorlds.CampaignSystem.Roster;using TaleWorlds.CampaignSystem.MapEvents;using TaleWorlds.CampaignSystem.GameComponents;using TaleWorlds.Core;
class Program{
 static object Empty(Type t)=>FormatterServices.GetUninitializedObject(t);
 static void Set(Type t,object o,string name,object value)=>t.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).SetValue(o,value);
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static int Main(string[] args){AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string p=Path.Combine(args[0],new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};try{Probe();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static void Probe(){
  Set(typeof(Game),null,"Current",Empty(typeof(Game)));
  var campaign=(Campaign)Empty(typeof(Campaign));Set(typeof(Campaign),null,"Current",campaign);
  var main=(MobileParty)Empty(typeof(MobileParty));var mainBase=(PartyBase)Empty(typeof(PartyBase));Set(typeof(MobileParty),main,"Party",mainBase);Set(typeof(Campaign),campaign,"MainParty",main);
  var party=(PartyBase)Empty(typeof(PartyBase));var roster=TroopRoster.CreateDummyTroopRoster();Set(typeof(PartyBase),party,"MemberRoster",roster);
  var low=(CharacterObject)Empty(typeof(CharacterObject));var high=(CharacterObject)Empty(typeof(CharacterObject));Set(typeof(BasicCharacterObject),low,"Level",6);Set(typeof(BasicCharacterObject),high,"Level",26);
  roster.AddToCounts(low,40);roster.AddToCounts(high,40,woundedCount:3);
  var ctor=typeof(MapEventParty).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(PartyBase)},null);var snap=(MapEventParty)ctor.Invoke(new object[]{party});
  var list=new List<(FlattenedTroopRosterElement Troop,MapEventParty Party,float Priority)>();new DefaultTroopSupplierProbabilityModel().EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization(snap,null,true,snap.HealthyManCountAtStart,false,list);
  var sorted=list.OrderByDescending(x=>x.Priority).ToArray();
  Check(sorted.Length==77&&snap.HealthyManCountAtStart==77,"actual native snapshot excludes wounded soldiers");
  Check(sorted.Take(37).All(x=>x.Troop.Troop==high)&&sorted.Skip(37).All(x=>x.Troop.Troop==low),"native high-level priority overrides source roster order");
  Check(sorted.All(x=>x.Party.Party==party),"native ordering preserves source party");
  Check(roster.TotalManCount==80&&roster.TotalHealthyCount==77&&party.MapEvent==null,"detached snapshot changes neither campaign roster nor map event");
  Console.WriteLine("NATIVE_CITY_PRIORITY_ASSERTIONS=4");
 }
}
'''
        names = ['TaleWorlds.CampaignSystem','TaleWorlds.Core','TaleWorlds.Library','TaleWorlds.ObjectSystem','TaleWorlds.Localization']
        refs = ''.join(f'<Reference Include="{name}"><HintPath>{str(game/name).replace('&','&amp;')}.dll</HintPath><Private>false</Private></Reference>' for name in names)
        with tempfile.TemporaryDirectory(prefix='gwp-native-city-priority-') as directory:
            p = Path(directory)
            (p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion></PropertyGroup><ItemGroup>'+refs+'</ItemGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            build = subprocess.run(['dotnet','build',str(p/'Probe.csproj'),'-c','Release','-v','quiet'],capture_output=True,text=True,timeout=90)
            self.assertEqual(0,build.returncode,build.stdout+build.stderr)
            result = subprocess.run([str(p/'bin/Release/net472/Probe.exe'),str(game)],capture_output=True,text=True,timeout=30)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            print(result.stdout.strip())


if __name__ == '__main__':
    unittest.main()
