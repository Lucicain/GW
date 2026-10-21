"""Characterize r13 save writers against current readers without starting Bannerlord.

This is an investigation tool, not an end-to-end .sav compatibility certificate.
It compiles unmodified production serialization methods with controlled entities.
Missing-key behavior comes from the installed game's decompiled IDataStore.
"""
from pathlib import Path
import importlib.util
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / "GreyWardenPolicePurity"
BASE = "v1.4-r13"
sys.path.insert(0, str(ROOT / "tools/tests"))
from test_assistance import member

spec = importlib.util.spec_from_file_location("hygiene", ROOT / "tools/Check-CodeHygiene.py")
hygiene = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hygiene)


def old_source(filename):
    return subprocess.check_output(
        ["git", "show", f"{BASE}:GreyWardenPolicePurity/{filename}"],
        cwd=ROOT, encoding="utf-8-sig")


def nested(source, name):
    match = re.search(r"private sealed class " + name + r"\s*\{", source)
    end = hygiene.match_brace(source, hygiene.code_mask(source), match.end() - 1) + 1
    return source[match.start():end]


def wrapper(source, name, kind):
    # Only serialization and its direct structural helpers are included.
    constants = "\n".join(re.findall(r"private const [^;]+;", source))
    sync_name = "SyncAssistanceData" if kind == "Assist" else "SyncData"
    rewritten = source.replace("public override void SyncData", "internal void SyncData")
    methods = member(rewritten, sync_name).replace("internal void SyncData", "public void SyncData")
    if kind == "Issue":
        types = nested(source, "IssueAssignment")
        fields = "readonly List<IssueAssignment> _assignments=new();"
        seed = '_assignments.Add(new(){IssueId="issue",OwnerHeroId="owner",PolicePartyId="warden",WorkEndHours=150});'
        count = "_assignments.Count"
    elif kind == "Rebuild":
        types = nested(source, "ReconstructionTask")
        fields = "readonly List<ReconstructionTask> _tasks=new();"
        seed = '_tasks.Add(new(){VillageSettlementId="village",VillageName="Village",PolicePartyId="warden",QueuedTimeHours=70,WorkStarted=true,WorkEndHours=150});'
        count = "_tasks.Count"
    elif kind == "Adopt":
        types = nested(source, "PendingVillageRelief") + nested(source, "ActiveVillageRelief")
        fields = '''readonly List<ActiveVillageRelief> _activeReliefs=new();
readonly List<PendingVillageRelief> _pendingReliefs=new();
readonly Dictionary<string,string> _adoptionOrigins=new();
double _lastAdoptionTimeHours=-1000000;
static HashSet<string> _activeReliefPartyIds=new(StringComparer.OrdinalIgnoreCase);'''
        seed = '_activeReliefs.Add(new(){PolicePartyId="warden",VillageSettlementId="village",VillageName="Village",QueuedTimeHours=70,ReliefStarted=true,'
        seed += "ReliefEndHours=150});" if "public double ReliefEndHours" in types else "RemainingHours=50,LastWorkHours=100});"
        count = "_activeReliefs.Count"
        methods += member(source, "MinCount") + member(source, "RebuildActiveReliefPartyIndex")
        # Old notification cleanup is a UI side effect, outside this data fixture.
        if "SanitizeLegacyAdoptionMapNotices();" in methods:
            methods += "private static void SanitizeLegacyAdoptionMapNotices(){}"
    else:
        types = nested(source, "LordAssistanceGroup")
        fields = '''readonly Dictionary<string,LordAssistanceGroup> _assistanceGroups=new();
readonly Dictionary<string,double> _assistanceAssignedHours=new();
readonly HashSet<string> _playerBountyEscortGroups=new();'''
        seed = '_assistanceGroups["warden"]=new(){LeaderPartyId="warden",CrimeId="crime",TargetPartyId="offender",MemberPartyIds=new(){"helper"}};_assistanceAssignedHours["helper"]=90;'
        count = "_assistanceGroups.Count"
        methods += "public void SyncData(IDataStore d)=>SyncAssistanceData(d);"
    return f"class {name}{{{constants}{types}{fields}{methods} public int Count=>{count};public void Seed(){{{seed}}}}}"


def main():
    native = ROOT / ".codex_tmp/campaignsystem-full-decompile/TaleWorlds.CampaignSystem/CampaignBehaviorDataStore.cs"
    native_source = native.read_text(encoding="utf-8-sig")
    sync_match = re.search(r"public bool SyncData<T>\(string key, ref T data\)\s*\{", native_source)
    end = hygiene.match_brace(native_source, hygiene.code_mask(native_source), sync_match.end() - 1) + 1
    store_method = native_source[sync_match.start():end]
    code = '''using System;using System.Linq;using System.Collections.Generic;using System.Globalization;
interface IDataStore{bool IsSaving{get;}bool IsLoading{get;}bool SyncData<T>(string key,ref T data);}
class Store:IDataStore{readonly bool _isSaving;readonly Dictionary<string,object> _records;
public bool IsSaving=>_isSaving;public bool IsLoading=>!IsSaving;
public Store(bool saving,Dictionary<string,object>? records=null){_isSaving=saving;_records=records??new();}
public Store Reader()=>new(false,_records);
STORE_METHOD
}
class CampaignTime{public double ToHours=100;public static CampaignTime Now=new();}
// Case-pool capacity adjustment is outside the adoption persistence fixture.
static class CrimePool{public const int MaxTaskPoolEntries=100;public static void TrimOpenCasesToCapacity(int n){}}
'''.replace("STORE_METHOD", store_method)
    cases = {
        "Issue": "GreyWardenIssueResolutionBehavior.cs",
        "Rebuild": "GreyWardenVillageReconstructionBehavior.cs",
        "Adopt": "GreyWardenVillageAdoptionBehavior.cs",
        "Assist": "PoliceEnforcementBehavior.Assistance.cs",
    }
    checks = []
    for kind, filename in cases.items():
        code += wrapper(old_source(filename), "R13" + kind, kind)
        code += wrapper((MODULE / filename).read_text(encoding="utf-8-sig"), "Current" + kind, kind)
        checks.append(f'''{{var before=new R13{kind}();before.Seed();var store=new Store(true);before.SyncData(store);
var control=new R13{kind}();control.SyncData(store.Reader());
if(control.Count!=1)throw new Exception("r13 writer/control failed: {kind}");
try{{var after=new Current{kind}();after.SyncData(store.Reader());Console.WriteLine("{kind}: r13="+control.Count+" current="+after.Count);}}
catch(Exception e){{Console.WriteLine("{kind}: r13="+control.Count+" current="+e.GetType().Name);}}
var same=new Current{kind}();same.Seed();var currentStore=new Store(true);same.SyncData(currentStore);
var currentCopy=new Current{kind}();currentCopy.SyncData(currentStore.Reader());
if(currentCopy.Count!=1)throw new Exception("same-build control failed: {kind}");
}}''')
    code += "class Program{static void Main(){" + "\n".join(checks) + 'Console.WriteLine("CONTROLS=8 PASS; engine entities stubbed; no .sav loaded");}}'
    major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
    print("BASE=" + subprocess.check_output(["git", "rev-parse", BASE + "^{}"], cwd=ROOT, text=True).strip(), flush=True)
    with tempfile.TemporaryDirectory(prefix="gwp-r13-audit-") as directory:
        path = Path(directory)
        (path / "Audit.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding="utf-8")
        (path / "Program.cs").write_text(code, encoding="utf-8")
        result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"], text=True, capture_output=True)
        if result.returncode:
            print(result.stdout + result.stderr)
            return result.returncode
        for line in result.stdout.splitlines():
            if not re.search(r"warning [A-Z]+\d+:", line):
                print(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
