"""Regress premature peace during player battle result application.

Compile the production GwpPeace owner and the enforcement retry against stubs,
and run the installed game's DoApplyMapEventResults. The native diplomacy check
that tears down an unfinished player encounter is a stub; no game is launched.
"""
import importlib.util
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
GAME_DLL = Path("D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll")
# Last commit whose neutral entry ran inside the capture callback (crash of 2026-10-14).
PRE_FIX = "0614808"


def extract(source, name):
    spec = importlib.util.spec_from_file_location("hygiene", ROOT / "tools/Check-CodeHygiene.py")
    hygiene = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(hygiene)
    mask = hygiene.code_mask(source)
    match = re.search(r"(?m)^[ \t]+(?:private|internal|public)[^\n]+\b" + name + r"\([^)]*\)\s*\{", source)
    if not match:
        raise AssertionError("Missing production method: " + name)
    end = hygiene.match_brace(source, mask, match.end() - 1) + 1
    return source[match.start():end]


HARNESS = r"""
using System;
using System.Collections.Generic;
using System.Linq;
namespace GreyWardenPolicePurity {
interface IFaction { string StringId {get;} string Name {get;} }
class Clan : IFaction { public string StringId {get;set;}=""; public string Name=>StringId; public IFaction MapFaction=>this;
    public static Clan PlayerClan=new Clan{StringId="player"}; }
class Hero { public static Hero MainHero=new Hero(); public IFaction MapFaction=>Clan.PlayerClan; }
static class PoliceStats { public static Clan Police=new Clan{StringId="gw"}; public static Clan GetPoliceClan()=>Police; }
class CampaignTime { public double ToHours; public static CampaignTime Now=new CampaignTime(); }
class CampaignEventDispatcher { public static CampaignEventDispatcher Instance=new CampaignEventDispatcher(); public void OnPlayerBattleEnd(MapEvent? m){} }
class PartyBase { public static PartyBase MainParty=new PartyBase(); public int Side=1; }
enum PlayerEncounterState { PlayerVictory,PlayerTotalDefeat,End }
class MapEvent {
    public static MapEvent? PlayerMapEvent;
    public bool IsFinalized;
    public int WinningSide=1,DefeatedSide=0,AwardCount;
    public Action? Capture;
    public void CalculateAndCommitMapEventResults() { Capture?.Invoke(); AwardCount++; }
}
class PlayerEncounter {
    public static PlayerEncounter? Current;
    public static MapEvent? Battle=>Current?._mapEvent;
    MapEvent? _mapEvent;
    bool IsNavalEncounterFinishedWithDisengage=false;
    public PlayerEncounterState EncounterState;
    public PlayerEncounter(MapEvent m) { _mapEvent=m;Current=this; }
    public static void Finish() { if(Current!=null)Current._mapEvent=null;Current=null; }
    public void Apply() { DoApplyMapEventResults(); }
    __NATIVE_RESULTS__
}
static class FactionManager {
    public static bool War=true; public static int NeutralCalls;
    public static bool IsAtWarAgainstFaction(IFaction left,IFaction right)=>War;
    public static void SetNeutral(IFaction left,IFaction right) { NeutralCalls++;Diplomacy(); }
    // Native CheckMapEvents is the actor boundary: a stance change while the
    // player's battle is unfinished calls Finish and clears the encounter.
    public static void Diplomacy() { War=false;if((PlayerEncounter.Battle??MapEvent.PlayerMapEvent)?.IsFinalized==false)PlayerEncounter.Finish(); }
}
static class MakePeaceAction { public static int Calls; public static void Apply(IFaction a,IFaction b) { Calls++;FactionManager.Diplomacy(); } }
static class GwpAiDiagnostics { public static int Deferred; public static void WriteFieldArrest(string stage,string details) {
    if(stage=="PEACE_DEFERRED_UNTIL_PLAYER_BATTLE_END")Deferred++;
} }
static class GwpPoliceWarReasonService { public static bool Reason; public static bool HasLegitimateWarReason(IFaction target)=>Reason; }
static class PlayerBehaviorPool { public static List<IFaction> VictimFactions=new List<IFaction>(); public static void ClearVictimFactions()=>VictimFactions.Clear(); }
static class ChangeCrimeRatingAction { public static void Apply(IFaction f,float v,bool b){} }
static class GwpText { public static string Get(string key,string name,string value)=>key; }
class InformationMessage { public InformationMessage(string text,int color){} }
static class InformationManager { public static void DisplayMessage(InformationMessage m){} }
static class Colors { public const int Green=0; }
static class OldCommon {
    __OLD_NEUTRAL__
}
class PoliceEnforcementBehavior {
    static readonly Dictionary<IFaction,double> _peaceRetryTargets=new Dictionary<IFaction,double>();
    public static int Pending=>_peaceRetryTargets.Count;
    public static void Queue(IFaction f,double deadline)=>_peaceRetryTargets[f]=deadline;
    public static void Clear()=>_peaceRetryTargets.Clear();
    public static void Retry()=>RetryPeaceAfterPlayerEnforcement();
    __RETRY__
}
class Regression {
    static int checks;
    static readonly Clan Enemy=new Clan{StringId="enemy"};
    static void Check(bool condition,string message) {checks++;if(!condition)throw new Exception(message);}
    static void Reset() {
        PoliceEnforcementBehavior.Clear();CampaignTime.Now.ToHours=100;FactionManager.War=true;
        FactionManager.NeutralCalls=0;MakePeaceAction.Calls=0;GwpAiDiagnostics.Deferred=0;GwpPoliceWarReasonService.Reason=false;
        PlayerEncounter.Current=null;MapEvent.PlayerMapEvent=null;
    }
    static MapEvent Start() {var m=new MapEvent();MapEvent.PlayerMapEvent=m;new PlayerEncounter(m);return m;}
    static void Finalize(MapEvent m) {m.IsFinalized=true;GwpPeace.OnMapEventEnded();}
    static void Main() {
        Reset();var old=Start();var previous=PlayerEncounter.Current!;
        old.Capture=()=>OldCommon.TrySetNeutral(PoliceStats.Police,Enemy);
        bool crashed=false;try{previous.Apply();}catch(NullReferenceException){crashed=true;}
        Check(crashed,"pre-fix neutral inside the capture callback reproduces the native null reference");

        // Victory/defeat/draw, with and without the main party detached from the event.
        foreach(int winningSide in new[]{0,1,2})
        foreach(bool detached in new[]{false,true}) {
            Reset();var battle=Start();battle.WinningSide=winningSide;battle.DefeatedSide=winningSide==0?1:0;var encounter=PlayerEncounter.Current!;
            if(detached)MapEvent.PlayerMapEvent=null;
            battle.Capture=()=>{Check(GwpPeace.EndPoliceWar(Enemy),"deferred peace counts as settled");GwpPeace.EndPoliceWar(Enemy);};
            encounter.Apply();
            Check(PlayerEncounter.Current==encounter,"encounter survives capture callback");
            Check(FactionManager.NeutralCalls==0 && FactionManager.War,"unfinished results keep diplomacy unchanged");
            Check(battle.AwardCount==1,"results commit once");
            var expected=winningSide==1?PlayerEncounterState.PlayerVictory:winningSide==0?PlayerEncounterState.PlayerTotalDefeat:PlayerEncounterState.End;
            Check(encounter.EncounterState==expected,"native outcome is preserved");
            GwpPeace.OnMapEventEnded();
            Check(FactionManager.NeutralCalls==0,"another event ending while the player battle is unfinished applies nothing");
            Finalize(battle);
            Check(FactionManager.NeutralCalls==1 && !FactionManager.War,"finalized player battle applies the peace");
            GwpPeace.OnMapEventEnded();
            Check(FactionManager.NeutralCalls==1,"a later event end does not repeat diplomacy");
        }

        Reset();var r=Start();GwpPeace.EndPoliceWar(Enemy);GwpPeace.Make(Enemy,PoliceStats.Police);Finalize(r);
        Check(FactionManager.NeutralCalls==1,"reverse pair shares one pending entry");
        Reset();var b=Start();GwpPeace.EndPoliceWar(Enemy);GwpPeace.Make(Enemy,PoliceStats.Police);
        GwpPoliceWarReasonService.Reason=true;Finalize(b);
        Check(FactionManager.War && FactionManager.NeutralCalls==0,"a reason that appears before the battle ends keeps the war");

        Reset();GwpPoliceWarReasonService.Reason=true;
        Check(!GwpPeace.EndPoliceWar(Enemy) && FactionManager.War,"legitimate reason keeps the war");
        Check(GwpPeace.Make(PoliceStats.Police,Enemy) && !FactionManager.War,"settled peace ignores police war reasons");

        Reset();var p=Start();GwpPeace.Make(Clan.PlayerClan,Enemy);
        Check(MakePeaceAction.Calls==0 && PlayerEncounter.Current!=null,"player peace waits for the battle too");
        Finalize(p);
        Check(MakePeaceAction.Calls==1 && FactionManager.NeutralCalls==0,"player peace uses the native peace action");

        Reset();Check(GwpPeace.Make(null,Enemy) && GwpPeace.EndPoliceWar(null),"null pair is a no-op");
        FactionManager.War=false;var n=Start();Check(GwpPeace.EndPoliceWar(Enemy),"already neutral completes");
        Finalize(n);Check(FactionManager.NeutralCalls==0,"already neutral queues nothing");
        Reset();Check(GwpPeace.EndPoliceWar(Enemy) && FactionManager.NeutralCalls==1,"no player battle applies immediately");

        Reset();PoliceEnforcementBehavior.Queue(Enemy,830);GwpPoliceWarReasonService.Reason=true;PoliceEnforcementBehavior.Retry();
        Check(PoliceEnforcementBehavior.Pending==1 && FactionManager.War,"retry keeps waiting while a reason remains");
        CampaignTime.Now.ToHours=831;PoliceEnforcementBehavior.Retry();
        Check(PoliceEnforcementBehavior.Pending==0 && FactionManager.War,"expired retry with a reason is dropped");
        Reset();PoliceEnforcementBehavior.Queue(Enemy,830);PoliceEnforcementBehavior.Retry();
        Check(PoliceEnforcementBehavior.Pending==0 && !FactionManager.War,"retry makes peace once reasons are gone");
        Console.WriteLine("BATTLE_PEACE_ASSERTIONS="+checks+"; PASS");
    }
}
}
"""


class BattlePeaceTests(unittest.TestCase):
    def test_peace_waits_for_player_battle_and_keeps_police_reasons(self):
        self.assertTrue(GAME_DLL.is_file(), "Installed Bannerlord assembly required")
        self.assertIsNotNone(shutil.which("ilspycmd"), "ilspycmd required for current native result method")
        native = subprocess.run(["ilspycmd", "-t", "TaleWorlds.CampaignSystem.Encounters.PlayerEncounter", str(GAME_DLL)],
                                capture_output=True, text=True, encoding="utf-8", timeout=60, check=True).stdout
        peace = (ROOT / "GreyWardenPolicePurity/GwpPeace.cs").read_text(encoding="utf-8-sig")
        peace = re.sub(r"(?m)^using [^\n]+\n", "", peace)
        enforcement = (ROOT / "GreyWardenPolicePurity/PoliceEnforcementBehavior.cs").read_text(encoding="utf-8-sig")
        old = subprocess.run(["git", "show", PRE_FIX + ":GreyWardenPolicePurity/GwpCommon.cs"],
                             cwd=ROOT, capture_output=True, text=True, encoding="utf-8", check=True).stdout
        program = HARNESS
        for key, body in (("__NATIVE_RESULTS__", extract(native, "DoApplyMapEventResults")),
                          ("__OLD_NEUTRAL__", extract(old, "TrySetNeutral")),
                          ("__RETRY__", extract(enforcement, "RetryPeaceAfterPlayerEnforcement"))):
            program = program.replace(key, body)
        program = "using System.Collections.Generic;\nusing System.Linq;\n" + program.replace(
            "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n", "using System;\n") + "\n" + peace
        major = int(subprocess.run(["dotnet", "--version"], check=True, capture_output=True, text=True).stdout.split(".")[0])
        with tempfile.TemporaryDirectory(prefix="gwp-battle-peace-") as directory:
            path = Path(directory)
            (path / "Regression.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable>'
                '<DefineConstants>GWP_DIAGNOSTICS</DefineConstants><NoWarn>CS8602</NoWarn>'
                '</PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(program, encoding="utf-8")
            shutil.copyfile(ROOT / "GreyWardenPolicePurity/GwpTextCatalog.g.cs", path / "GwpTextCatalog.cs")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=120)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
