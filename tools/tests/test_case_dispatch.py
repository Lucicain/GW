"""Execute real assignment passes with controlled availability and candidate eligibility."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from test_battle_peace import extract

MODULE = Path(__file__).resolve().parents[2] / 'GreyWardenPolicePurity'


class CaseDispatchTests(unittest.TestCase):
    def test_office_then_nearest_without_faith_override(self):
        source = (MODULE / 'PoliceEnforcementBehavior.cs').read_text(encoding='utf-8-sig')
        production = extract(source, 'AssignTasks')
        code = r'''
using System;using System.Collections.Generic;using System.Linq;
enum GwpCrimeCategory{Unknown,CaravanAttack,VillageViolence}
class MobileParty{public string StringId="";public float GetPosition2D;public GwpCrimeCategory Preferred;public bool Available=true;}
class CrimeRecord{public string Id="";public float Position;public GwpCrimeCategory CrimeCategory;public bool Picked,Deferred,CanTake=true;}
static class PoliceStats{public static List<MobileParty> Parties=new();public static IEnumerable<MobileParty> GetAllPoliceParties()=>Parties;}
static class CrimeState{public static bool IsDispatchReady=true;public static List<CrimeRecord> Cases=new();public static CrimeRecord? GetNearest(float p,Func<CrimeRecord,bool> filter)=>Cases.Where(c=>!c.Picked&&filter(c)).OrderBy(c=>Math.Abs(c.Position-p)).FirstOrDefault();}
class PoliceEnforcementBehavior{public Dictionary<string,string> Assigned=new();bool CanAssignOrdinaryCaseNow(MobileParty p)=>p.Available;float GetMaximumMusterableStrength(MobileParty p)=>100;GwpCrimeCategory GetPreferredCrimeCategory(MobileParty p)=>p.Preferred;bool CanPickUpCase(CrimeRecord c,float strength,bool allowDeferred=false)=>c.CanTake&&(!c.Deferred||allowDeferred);void BeginTask(MobileParty p,CrimeRecord c){c.Picked=true;Assigned.Add(p.StringId,c.Id);}
PRODUCTION
public void Run()=>AssignTasks();}
class Program{static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}static PoliceEnforcementBehavior Setup(params CrimeRecord[] cases){CrimeState.Cases=cases.ToList();PoliceStats.Parties=new(){new(){StringId="warden",Preferred=GwpCrimeCategory.CaravanAttack}};return new();}static void Main(){
var near=new CrimeRecord{Id="near",Position=4,CrimeCategory=GwpCrimeCategory.CaravanAttack};var far=new CrimeRecord{Id="faith-far",Position=250,CrimeCategory=GwpCrimeCategory.CaravanAttack};var e=Setup(near,far);e.Run();Check(e.Assigned["warden"]=="near","same office takes nearby case instead of remote faith case");
near=new(){Id="near-other",Position=1,CrimeCategory=GwpCrimeCategory.VillageViolence};var own=new CrimeRecord{Id="own",Position=5,CrimeCategory=GwpCrimeCategory.CaravanAttack};e=Setup(near,own);e.Run();Check(e.Assigned["warden"]=="own","office preference remains before helping another office");
e=Setup(new CrimeRecord(){Id="far",Position=250,CrimeCategory=GwpCrimeCategory.VillageViolence},new(){Id="near",Position=2,CrimeCategory=GwpCrimeCategory.VillageViolence});e.Run();Check(e.Assigned["warden"]=="near","idle office without own eligible work helps closest other case");
e=Setup(new CrimeRecord(){Id="blocked",Position=1,CanTake=false},new(){Id="allowed",Position=5});e.Run();Check(e.Assigned["warden"]=="allowed","shared pickup gate still rejects ineligible nearest case");
e=Setup(new CrimeRecord(){Id="deferred",Position=1,Deferred=true},new(){Id="ordinary",Position=5});e.Run();Check(e.Assigned["warden"]=="ordinary","ordinary cases precede deferred fallback");
e=Setup(new CrimeRecord(){Id="deferred",Position=1,Deferred=true});e.Run();Check(e.Assigned["warden"]=="deferred","existing deferred fallback remains when ordinary work is empty");
e=Setup(new CrimeRecord(){Id="near",Position=1});PoliceStats.Parties[0].Available=false;e.Run();Check(e.Assigned.Count==0,"occupied lord is not reassigned");
e=Setup(new CrimeRecord(){Id="only",Position=1});PoliceStats.Parties.Add(new(){StringId="second"});e.Run();Check(e.Assigned.Count==1,"one case is not assigned twice");
Console.WriteLine("CASE_DISPATCH_ASSERTIONS="+checks);
}}
'''.replace('PRODUCTION', production)
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-dispatch-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('CASE_DISPATCH_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
