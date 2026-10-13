"""Run production watch dispatch methods with roster/engine boundaries replaced.

Regression: 2026-10-09, home=0/outside=33 repeatedly created empty patrols.
The creation boundary fails after 100 calls so that a regression cannot hang CI.
"""
import importlib.util
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]


def production_methods():
    spec = importlib.util.spec_from_file_location("hygiene", ROOT / "tools/Check-CodeHygiene.py")
    hygiene = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(hygiene)
    source = (ROOT / "GreyWardenPolicePurity/GwpFireswornParties.cs").read_text(encoding="utf-8-sig")
    mask = hygiene.code_mask(source)
    result = []
    for name in ("WatchRoom", "PatrolShare", "VillageForce", "MenOut", "DispatchPatrols"):
        match = re.search(r"(?m)^        private [^\n]+\b" + name + r"\([^)]*\)\s*(\{|=>)", source)
        if not match:
            raise AssertionError("Production method missing: " + name)
        end = (hygiene.match_brace(source, mask, match.end() - 1) + 1
               if match.group(1) == "{" else source.index(";", match.end()) + 1)
        result.append(source[match.start():end])
    tuning = (ROOT / "GreyWardenPolicePurity/GwpTuning.cs").read_text(encoding="utf-8-sig")
    constants = [re.search(r"public const [^\n]+\b" + name + r"\s*=\s*[^;]+;", tuning).group()
                 for name in ("WatchShare", "PatrolMinimumSize", "PatrolMaximumSize")]
    return "\n".join(result), "\n".join(constants)


HARNESS = r"""
using System;
using System.Collections.Generic;
using System.Linq;

class TroopRoster { public int TotalManCount; public IEnumerable<TroopRosterElement> GetTroopRoster()
    => new[] { new TroopRosterElement { Number=TotalManCount } }; }
class TroopRosterElement { public object Character; public int Number; }
class MobileParty { public string StringId; public TroopRoster MemberRoster=new TroopRoster(); }
class Settlement { public string StringId="v"; }
class FaithVillage { public string VillageId="v"; }
enum FaithPartyKind { Mission, Market, Balance, Patrol, Response }
enum FaithPartyPhase { Home, Outbound, Returning }
class FaithParty { public string PartyId, HomeId, TargetId; public FaithPartyKind Kind;
    public FaithPartyPhase Phase; public int Men; }
static class GwpTuning { public static class Firesworn { __CONSTANTS__ } }
static class GwpAiDiagnostics { public static void WriteFaith(string stage,string details) { } }

class GwpFireswornBehavior
{
    readonly Dictionary<string,FaithParty> _faithParties=new Dictionary<string,FaithParty>();
    readonly Dictionary<string,MobileParty> parties=new Dictionary<string,MobileParty>();
    int Home, Created;
    readonly List<MobileParty> spawned=new List<MobileParty>();
    int HomeDefenders(FaithVillage row, Settlement home) => Home;
    int Sworn(FaithVillage row) => Home;
    int GuardCap(FaithVillage row) => 999;
    static bool IsGuardTroop(object character) => true;
    MobileParty FindParty(string id) => parties.TryGetValue(id,out var p) ? p : null;
    MobileParty CreateFaithParty(FaithVillage row,Settlement home,FaithPartyKind kind)
    {
        if (++Created>100) throw new Exception("Non-terminating dispatch: 100 creations in one call");
        var p=new MobileParty { StringId="new"+Created };
        parties.Add(p.StringId,p);
        _faithParties.Add(p.StringId,new FaithParty { PartyId=p.StringId,HomeId=row.VillageId,Kind=kind });
        spawned.Add(p);
        return p;
    }
    int ManParty(MobileParty party,FaithVillage row,Settlement home,int requested,bool militiaFirst)
    {
        int take=Math.Min(Home,requested);
        Home-=take;
        party.MemberRoster.TotalManCount=take;
        return 0;
    }
    void SetPhase(FaithParty record,FaithPartyPhase phase,string target)
    { record.Phase=phase;record.TargetId=target; }
    void Patrol(FaithParty record,MobileParty party,Settlement home) { }

__METHODS__

    void Add(FaithPartyKind kind,FaithPartyPhase phase,int men)
    {
        var id="old"+parties.Count;
        parties.Add(id,new MobileParty { StringId=id,MemberRoster=new TroopRoster {TotalManCount=men} });
        _faithParties.Add(id,new FaithParty { PartyId=id,HomeId="v",Kind=kind,Phase=phase });
    }
    static int assertions,scenarios;
    static void Check(bool ok,string description)
    { assertions++; if(!ok) throw new Exception(description); }

    static void Scenario(int home,int outbound,int returning,int response,int responseReturning)
    {
        var b=new GwpFireswornBehavior {Home=home};
        var row=new FaithVillage();var village=new Settlement();
        b.Add(FaithPartyKind.Patrol,FaithPartyPhase.Outbound,outbound);
        b.Add(FaithPartyKind.Patrol,FaithPartyPhase.Returning,returning);
        b.Add(FaithPartyKind.Response,FaithPartyPhase.Outbound,response);
        b.Add(FaithPartyKind.Response,FaithPartyPhase.Returning,responseReturning);
        // Non-watch travellers must not inflate the three-way watch allocation.
        b.Add(FaithPartyKind.Mission,FaithPartyPhase.Outbound,100);
        int force=b.VillageForce(row,village),third=b.PatrolShare(row,village);
        int room=b.WatchRoom(row,village,FaithPartyKind.Patrol,FaithPartyPhase.Outbound);
        b.DispatchPatrols(row,village);
        Check(b.VillageForce(row,village)==force,"Dispatch must conserve village force");
        Check(home>=third ? b.Home>=third : b.Created==0,"Must preserve the home third");
        Check(room>=GwpTuning.Firesworn.PatrolMinimumSize || b.Created==0,"No sub-minimum squad creation");
        Check(b.spawned.All(p=>p.MemberRoster.TotalManCount>=GwpTuning.Firesworn.PatrolMinimumSize &&
            p.MemberRoster.TotalManCount<=GwpTuning.Firesworn.PatrolMaximumSize),"No empty or oversized patrols");
        Check(home-b.Home<=Math.Max(0,room),"Do not consume more than available watch room");
        int created=b.Created;
        b.DispatchPatrols(row,village);
        Check(b.Created==created,"Repeated hourly dispatch without new troops must settle");
        int emergency=b.WatchRoom(row,village,FaithPartyKind.Response);
        Check(emergency<=b.Home-third,"Emergency allocation uses the same home reserve");
        Check(emergency<=third-response-responseReturning,"Returning responses still consume their allocation");
        scenarios++;
    }
    static int Main()
    {
        // The exact live failure, plus both ways the 33 outside soldiers can be occupied.
        Scenario(0,0,33,0,0);
        Scenario(0,0,0,33,0);
        Scenario(0,0,0,0,33);
        // Previously this dispatched all 12 home defenders while other watchmen returned.
        Scenario(12,0,14,10,0);
        foreach(int home in new[] {0,1,9,10,17,18,29,30,60,75,100,120,300})
        foreach(int outbound in new[] {0,10,25})
        foreach(int returning in new[] {0,11,33,75})
        foreach(int response in new[] {0,10,33})
        foreach(int responseReturning in new[] {0,22})
            Scenario(home,outbound,returning,response,responseReturning);
        Console.WriteLine($"DISPATCH_SCENARIOS={scenarios}; ASSERTIONS={assertions}; PASS");
        return 0;
    }
}
"""


class FireswornDispatchTests(unittest.TestCase):
    def test_empty_home_returning_watchmen_and_reserve_boundaries(self):
        self.assertIsNotNone(shutil.which("dotnet"), ".NET SDK required for production C# dispatch regression")
        sdk = subprocess.run(["dotnet", "--version"], check=True, capture_output=True, text=True).stdout.strip()
        major = int(sdk.split(".")[0])
        methods, constants = production_methods()
        program = HARNESS.replace("__METHODS__", methods).replace("__CONSTANTS__", constants)
        with tempfile.TemporaryDirectory(prefix="gwp-watch-regression-") as directory:
            path = Path(directory)
            (path / "Regression.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers>'
                '<NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(path), "-c", "Release", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("DISPATCH_SCENARIOS=940; ASSERTIONS=7520; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
