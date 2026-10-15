"""Execute production equipment allocation and escort reinforcement methods.

Engine roster storage and the unchanged equipment scorer are boundaries.
The two audit counterexamples must become successful operations, not exceptions.
"""
import importlib.util
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


def methods():
    spec = importlib.util.spec_from_file_location("hygiene", ROOT / "tools/Check-CodeHygiene.py")
    hygiene = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(hygiene)
    result = []
    for file, names in (("GwpFireswornParties.cs", ("DrawMen", "ManToBeat")),
                        ("GwpFireswornBehavior.cs", ("JoinGuard",)),
                        ("GwpFireswornArmory.cs", ("TakeBestSets",))):
        source = (ROOT / "GreyWardenPolicePurity" / file).read_text(encoding="utf-8-sig")
        mask = hygiene.code_mask(source)
        for name in names:
            match = re.search(r"(?m)^        private [^\n]+\b" + name + r"\([^)]*\)\s*\{", source)
            if not match:
                raise AssertionError("Missing production method: " + name)
            end = hygiene.match_brace(source, mask, match.end() - 1) + 1
            result.append(source[match.start():end])
    return "\n".join(result)


HARNESS = r"""
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
class CharacterObject { public string StringId; public bool IsHero; public int Tier; }
class TroopRosterElement { public CharacterObject Character; public int Number, WoundedNumber; }
class TroopRoster {
    readonly Dictionary<CharacterObject,int> rows=new Dictionary<CharacterObject,int>();
    public int TotalManCount => rows.Values.Sum();
    public void AddToCounts(CharacterObject troop,int count) {
        rows[troop]=(rows.ContainsKey(troop)?rows[troop]:0)+count;
        if(rows[troop]<0) throw new Exception("negative troop stock");
    }
    public IEnumerable<TroopRosterElement> GetTroopRoster() => rows.Select(r=>new TroopRosterElement {Character=r.Key,Number=r.Value});
}
class MobileParty { public string StringId="p"; public bool IsActive=true;
    public object MapEvent=null; public TroopRoster MemberRoster=new TroopRoster(); }
class MilitiaPartyComponent { public MobileParty MobileParty=new MobileParty(); }
class Settlement { public MilitiaPartyComponent MilitiaPartyComponent=new MilitiaPartyComponent(); }
class FaithVillage { public string VillageId="v"; }
struct EquipmentElement { public int Quality; public bool IsEmpty=>Quality==0; }
class ItemRosterElement { public EquipmentElement EquipmentElement; public int Amount; }
class ItemRoster : IEnumerable<ItemRosterElement> {
    readonly Dictionary<int,int> rows=new Dictionary<int,int>();
    public void Add(ItemRoster other) { foreach(var row in other) AddToCounts(row.EquipmentElement,row.Amount); }
    public void AddToCounts(EquipmentElement item,int count) {
        rows[item.Quality]=(rows.ContainsKey(item.Quality)?rows[item.Quality]:0)+count;
        if(rows[item.Quality]<0) throw new Exception("negative equipment stock");
    }
    public int Total=>rows.Values.Sum();
    public IEnumerator<ItemRosterElement> GetEnumerator()=>rows.Select(r=>new ItemRosterElement {
        EquipmentElement=new EquipmentElement {Quality=r.Key},Amount=r.Value}).Where(r=>r.Amount>0).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
class ArmoryKit {public EquipmentElement[] Items;}
class ArmoryPlan { public List<ArmoryKit> Sets=new List<ArmoryKit>(); }
static class GwpIds { public const string NewRecruitId="recruit",ZealotId="zealot"; }
static class GwpTuning { public static class Firesworn { public const int MissionMinimumLeftBehind=5; } }
static class GwpStrength { public static float Party(MobileParty party)=>party.MemberRoster.TotalManCount; }
static class GwpTroopTransfer {
    public static TroopRosterElement Move(TroopRoster from,TroopRoster to,CharacterObject troop,int count,bool healthyOnly=false) {
        from.AddToCounts(troop,-count); to.AddToCounts(troop,count);
        return new TroopRosterElement {Character=troop,Number=count};
    }
}
class Regression {
    readonly Dictionary<string,ItemRoster> _armories=new Dictionary<string,ItemRoster>();
    readonly Dictionary<string,ArmoryPlan> _plans=new Dictionary<string,ArmoryPlan>();
    readonly TroopRoster guard=new TroopRoster();
    TroopRoster GuardOf(string id)=>guard;
    static bool IsGuardTroop(CharacterObject troop)=>troop.StringId!="militia";
    int Sworn(FaithVillage row)=>guard.TotalManCount;
    void PushGuard(FaithVillage row,Settlement home) { }
    void ReconcilePriestSlots(FaithVillage row) { }
    static int CountOf(TroopRoster roster,string id)=>roster.GetTroopRoster().Where(e=>e.Character.StringId==id).Sum(e=>e.Number);
    static ItemRoster ItemsOf(Dictionary<string,ItemRoster> ledger,string id) {
        if(!ledger.ContainsKey(id)) ledger[id]=new ItemRoster(); return ledger[id];
    }
    static List<TroopRosterElement> ArmoryTroops(TroopRoster roster)=>roster.GetTroopRoster().Where(e=>IsGuardTroop(e.Character)).ToList();
    static ArmoryPlan BuildPlan(ItemRoster stock,IReadOnlyList<TroopRosterElement> troops,int headroom) {
        int recruits=troops.Where(e=>e.Character.StringId==GwpIds.NewRecruitId).Sum(e=>e.Number);
        int zealots=troops.Where(e=>e.Character.StringId==GwpIds.ZealotId).Sum(e=>e.Number);
        // This boundary supplies one scored main weapon per eligible soldier.
        var items=stock.OrderByDescending(e=>e.EquipmentElement.Quality)
            .SelectMany(e=>Enumerable.Repeat(e.EquipmentElement,e.Amount)).ToList();
        var plan=new ArmoryPlan();
        foreach(var item in items.Take(recruits+zealots)) plan.Sets.Add(new ArmoryKit{Items=new[]{item}});
        return plan;
    }
    static int MoveAll(ItemRoster batch,ItemRoster from,ItemRoster to) {
        foreach(var item in batch) {from.AddToCounts(item.EquipmentElement,-item.Amount);to.AddToCounts(item.EquipmentElement,item.Amount);}
        return batch.Total;
    }
    __METHODS__
    static int checks;
    static void Check(bool condition,string message) {checks++;if(!condition)throw new Exception(message);}
    static CharacterObject Troop(string id)=>new CharacterObject {StringId=id};
    static void EquipmentScenario(int recruits,int zealots,int elite,int stock,int already) {
        var b=new Regression();var party=new MobileParty();
        party.MemberRoster.AddToCounts(Troop("recruit"),recruits);
        party.MemberRoster.AddToCounts(Troop("zealot"),zealots);
        party.MemberRoster.AddToCounts(Troop("elite"),elite);
        // Departure left only 5 villagers; these must not limit the departing 25.
        b.guard.AddToCounts(Troop("zealot"),5);
        ItemsOf(b._armories,"v").AddToCounts(new EquipmentElement{Quality=2},stock);
        ItemsOf(b._armories,"p").AddToCounts(new EquipmentElement{Quality=2},already);
        b._plans["v"]=new ArmoryPlan();b._plans["p"]=new ArmoryPlan();
        int taken=b.TakeBestSets("v",party);
        int expected=Math.Max(0,Math.Min(stock+already,recruits+zealots)-already);
        Check(taken==expected,"outgoing troop composition and missing equipment");
        Check(ItemsOf(b._armories,"v").Total+ItemsOf(b._armories,"p").Total==stock+already,"equipment conserved");
        Check(!b._plans.ContainsKey("v")&&!b._plans.ContainsKey("p"),"both plans invalidated");
        Check(b.TakeBestSets("v",party)==0,"repeat must not overdraw the same kit");
    }
    static void EscortScenario(int militia,int grey,int maximum,int danger,int expected) {
        var b=new Regression();var party=new MobileParty();var home=new Settlement();var row=new FaithVillage();
        home.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(Troop("militia"),militia);
        var sworn=Troop("zealot");b.guard.AddToCounts(sworn,grey);
        home.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(sworn,grey);
        b.ManToBeat(party,row,home,10,maximum,danger);
        Check(party.MemberRoster.TotalManCount==expected,"escort grows until it beats the road danger");
        Check(CountOf(party.MemberRoster,"militia")==0,"vanilla militia stays home");
        Check(party.MemberRoster.TotalManCount+home.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount==militia+grey,"defenders conserved");
    }
    static int Main() {
        foreach(bool inBattle in new[]{false,true}) {
            var join=new Regression();var home=new Settlement();var source=new TroopRoster();var grey=Troop("zealot");
            join.guard.AddToCounts(grey,5);home.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(grey,5);
            home.MilitiaPartyComponent.MobileParty.MapEvent=inBattle?new object():null;
            source.AddToCounts(grey,3);
            Check(join.JoinGuard(new FaithVillage(),home,grey,3,source)==3,"join returns actual moved count");
            Check(join.guard.TotalManCount==8,"guard record updated once");
            Check(home.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount==8,"real roster updated once");
            Check(source.TotalManCount==0,"returning source emptied");
        }
        EquipmentScenario(0,25,0,30,0); EquipmentScenario(0,0,25,30,0);
        EquipmentScenario(4,6,15,30,0); EquipmentScenario(0,25,0,3,0);
        EquipmentScenario(0,25,0,30,20); EquipmentScenario(0,10,0,30,20);
        EquipmentScenario(0,25,0,30,25);
        // Low quality already owned must not suppress a newly selected better weapon.
        var b=new Regression();var p=new MobileParty();p.MemberRoster.AddToCounts(Troop("zealot"),1);
        ItemsOf(b._armories,"v").AddToCounts(new EquipmentElement{Quality=2},1);
        ItemsOf(b._armories,"p").AddToCounts(new EquipmentElement{Quality=1},1);
        Check(b.TakeBestSets("v",p)==1,"quality identity retained");
        Check(b.TakeBestSets("v",p)==0,"better weapon only once");
        EscortScenario(30,40,25,11,12);
        EscortScenario(30,40,25,100,25);
        EscortScenario(30,40,25,9,10);
        // Five sworn always stay home.
        EscortScenario(30,17,25,100,12);
        EscortScenario(30,8,25,100,8);
        Console.WriteLine("OUTFITTING_ASSERTIONS="+checks+"; PASS");return 0;
    }
}
"""


class FireswornOutfittingTests(unittest.TestCase):
    def test_audit_counterexamples_and_stock_boundaries(self):
        self.assertIsNotNone(shutil.which("dotnet"))
        version = subprocess.run(["dotnet", "--version"], check=True, capture_output=True, text=True).stdout
        major = int(version.split(".")[0])
        with tempfile.TemporaryDirectory(prefix="gwp-outfitting-") as directory:
            path = Path(directory)
            (path / "Regression.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers>'
                '<Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(HARNESS.replace("__METHODS__", methods()), encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("OUTFITTING_ASSERTIONS=53; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
