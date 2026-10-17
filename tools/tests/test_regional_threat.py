"""Verify fixed production regional threat inclusion.

Extract EnemyAround and its eligibility/strength helpers verbatim. Only campaign
objects, spatial lookup and per-party engine strength/speed are controlled inputs.
No game is started and no new gameplay rule is asserted as already implemented.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "GreyWardenPolicePurity/GwpStrength.cs"

HARNESS = r"""
using System; using System.Linq; using System.Collections.Generic; using System.Globalization;
namespace GreyWardenPolicePurity {
enum BattleSideEnum { None, Attacker, Defender }
class Vec2 { public float X; public Vec2(float x=0){X=x;} public float Distance(Vec2 p)=>Math.Abs(X-p.X); }
class Position { public float X; public bool IsOnLand=true; public Vec2 ToVec2()=>new Vec2(X); }
class PartyBase { public bool IsActive=true; public float EstimatedStrength; public int NumberOfHealthyMembers=20;
 public MobileParty? MobileParty; public string Id="party"; public BattleSideEnum Side=BattleSideEnum.None; }
class MapEventParty { public PartyBase Party=null!; }
class MapEvent { public bool IsFinalized; public Position Position=new(); public bool AllowJoin=true;
 public List<MapEventParty> Participants=new(); public IEnumerable<MapEventParty> PartiesOnSide(BattleSideEnum s)=>Participants;
 public bool CanPartyJoinBattle(PartyBase p,BattleSideEnum s)=>AllowJoin&&p.MobileParty?.StringId!="warden"; }
class Settlement { public object? SiegeEvent; }
class Camp { public MobileParty? LeaderParty; public List<PartyBase> Parties=new();
 public IEnumerable<PartyBase> GetInvolvedPartiesForEventType()=>Parties; }
class Army { public MobileParty LeaderParty=null!; public float EstimatedStrength; }
class Search { public List<MobileParty> Parties=new(); public int Index; }
class LocatableSearchData<T> { public Search State=new(); }
interface IFaction { bool IsAtWarWith(IFaction other); }
class Faction:IFaction { public bool Hostile; public bool IsAtWarWith(IFaction other)=>Hostile; }
class MobileParty { public bool IsActive=true,IsLordParty=true,IsMainParty,IsGarrison,IsMilitia; public string StringId="";
 public PartyBase Party=new(); public float Aggressiveness=1,Speed=4; public Position Position=new();
 public MobileParty? AttachedTo; public Army? Army; public Camp? BesiegerCamp; public MapEvent? MapEvent;
 public Settlement? CurrentSettlement; public IFaction? MapFaction;
 public static List<MobileParty> All=new();
 public static LocatableSearchData<MobileParty> StartFindingLocatablesAroundPosition(Vec2 p,float r)=>
  new(){State=new Search{Parties=All.Where(x=>x.Position.ToVec2().Distance(p)<=r).ToList()}};
 public static MobileParty? FindNextLocatable(ref LocatableSearchData<MobileParty> d)=>
  d.State.Index<d.State.Parties.Count?d.State.Parties[d.State.Index++]:null; }
class Encounter { public float GetEncounterJoiningRadius=3; }
class Models { public Encounter EncounterModel=new(); }
class Campaign { public static Campaign Current=new(); public Models Models=new(); }
class CrimeRecord {public MobileParty? Offender;}
static class GwpTuning {public static class Enforcement { public const float RegionalThreatRadius=6; }}
static class GwpStrength {
 internal class Threat { public float Strength,MaximumSpeed,JoiningRadius,ThreatRadius;
  public string FastestCombatGroupId=""; public List<string> CombatGroups=new(); }
 internal static float Party(MobileParty p)=>p.Party.EstimatedStrength;
 internal static float TheoreticalBaseSpeed(MobileParty p)=>p.Speed;
 internal class LocalBalance {public MobileParty Actor=null!,MovementTarget=null!;public float FriendlyLocalStrength,EnemyLocalStrength,Distance;public string FriendlyCombatGroups="",Reason="";public bool StrengthReady;}
 PRODUCTION
 static int checks;
 static void Check(bool b,string label){if(!b)throw new Exception(label);checks++;}
 static MobileParty New(string id,float x,float strength,IFaction f){var p=new MobileParty{StringId=id,MapFaction=f};
  p.Position.X=x;p.Party.MobileParty=p;p.Party.EstimatedStrength=strength;MobileParty.All.Add(p);return p;}
 static (MobileParty actor,MobileParty target,MobileParty reinforcement) Setup(float observerDistance,float reinforceDistance){
  MobileParty.All.Clear(); var enemies=new Faction();
  var a=New("warden",-observerDistance,300,new Faction());var t=New("offender",0,200,enemies);
  var r=New("reinforcement",reinforceDistance,600,enemies);return(a,t,r); }
 static void Main(){
  var(a,t,r)=Setup(1.43f,4.5f);var x=EnemyAround(a,t);
  Check(x.Strength==800,"same-faction party inside fixed radius counts fully");
  Check(x.ThreatRadius==6&&x.CombatGroups.Count==2,"fixed radius includes both combat groups");
  Console.WriteLine("near_observer: friendly=300, reported_enemy="+x.Strength+", enemy_in_radius=800, nominal_radius="+x.ThreatRadius);
  a.Position.X=-5;x=EnemyAround(a,t);
  Check(x.Strength==800,"observer distance does not shrink enemy threat");
  Console.WriteLine("farther_observer: same enemies, reported_enemy="+x.Strength);
  (a,t,r)=Setup(3,4);x=EnemyAround(a,t);
  Check(x.Strength==800,"outer-ring threat counts fully");
  r.Position.X=2;x=EnemyAround(a,t);Check(x.Strength==800,"inner-ring allied enemy counted fully");
  r.MapFaction=new Faction();x=EnemyAround(a,t);Check(x.Strength==200,"different-faction threat omitted before battle");
  var battle=new MapEvent();battle.Participants.Add(new MapEventParty{Party=t.Party});
  t.MapEvent=battle;t.Party.Side=BattleSideEnum.Defender;x=EnemyAround(a,t);
  Check(x.Strength==800,"battle join eligibility can admit the different faction after battle starts");
  battle.Position.X=10;r.Position.X=12;Check(EnemyAround(a,t).Strength==800,"ongoing battle uses actual battle position as fixed regional center");
  battle.AllowJoin=false;x=EnemyAround(a,t);Check(x.Strength==200,"native battle join refusal honored");
  (a,t,r)=Setup(1,2);r.IsGarrison=true;x=EnemyAround(a,t);Check(x.Strength==200,"intentional garrison exclusion retained");
  r.IsGarrison=false;r.MapEvent=new MapEvent();x=EnemyAround(a,t);Check(x.Strength==200,"party in another battle omitted");
  (a,t,r)=Setup(1,2);r.CurrentSettlement=new Settlement();x=EnemyAround(a,t);
  Check(x.Strength==800,"ordinary lord sheltered in town still counts, unlike garrison roster");
  r.Aggressiveness=0;x=EnemyAround(a,t);Check(x.Strength==800,"town lord counts even at zero aggression");
  r.IsLordParty=false;x=EnemyAround(a,t);Check(x.Strength==200,"nonlord zero-aggression party excluded");
  r.IsLordParty=true;r.Position.X=6;Check(EnemyAround(a,t).Strength==800,"fixed boundary included");r.Position.X=6.01f;Check(EnemyAround(a,t).Strength==200,"outside fixed boundary excluded");r.Position.X=2;
  r.Aggressiveness=1;r.CurrentSettlement.SiegeEvent=new object();x=EnemyAround(a,t);
  Check(x.Strength==200,"lord inside settlement under siege omitted by current eligibility");

  (a,t,r)=Setup(1,4.5f);var record=new CrimeRecord{Offender=t};
  Check(CaseTarget(record)==200&&EnemyAround(a,t).Strength==800,"intake stable strength excludes passing reinforcements");
  Check(!Compare(a,t).StrengthReady,"real comparison blocks local 300 against regional 800");
  a.Position.X=-5;Check(!Compare(a,t).StrengthReady,"moving observer does not recover a false regional advantage");
  r.Position.X=7;Check(Compare(a,t).StrengthReady,"passing threat leaving region permits fresh combat check");
  var army=new Army{LeaderParty=r,EstimatedStrength=900};r.Army=army;t.Army=army;t.AttachedTo=r;
  Check(CaseTarget(record)==900,"attached offender uses current army strength for intake and composition");
  t.AttachedTo=null;t.Army=null;Check(CaseTarget(record)==200,"army dissolution returns stable target to own strength");
  t.Party.EstimatedStrength=600;Check(CaseTarget(record)==600,"own recruitment changes stable target strength");
  (a,t,r)=Setup(1,2);r.MapFaction=new Faction{Hostile=true};Check(EnemyAround(a,t).Strength==800,"other already hostile faction counts as regional danger");
  (a,t,r)=Setup(1,2);r.MapFaction=a.MapFaction;
  Check(FriendlyAround(a,t,out _)==900,"actual nearby same-faction friend counts");
  r.Position.X=4;Check(FriendlyAround(a,t,out _)==300,"remote promised friend outside native actor joining radius does not count");
  r.Position.X=2;r.AttachedTo=a;a.Army=r.Army=new Army{LeaderParty=a,EstimatedStrength=900};
  Check(FriendlyAround(a,t,out _)==900,"attached friend counted once through actual army strength");
  Console.WriteLine("REGIONAL_THREAT_CHARACTERIZATION="+checks+"; fixed-radius regressions passed");
 }
}
}
"""


class RegionalThreatTests(unittest.TestCase):
    def test_production_regional_threat_envelope(self):
        source = SOURCE.read_text(encoding="utf-8-sig")
        source = re.sub(r"(?m)^(\s*(?:internal|private) static \w+)\s*\n\s*(\w+\s*\()", r"\1 \2", source)
        methods = [extract(source, name) for name in ["EnemyAround", "CanNearbyCombatGroupJoinTarget",
                   "IncludeMaximumCombatGroupSpeed", "CombatGroupOf", "CombatGroup", "CaseTarget", "Compare", "FriendlyAround", "CanNearbyFriendlyGroupJoinActor"]]
        # This production expression-bodied helper is independent of method extraction.
        methods.append("internal static string CombatGroupKey(MobileParty p)=>CombatGroupOf(p).StringId;")
        code = HARNESS.replace("PRODUCTION", "\n".join(methods))
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-regional-threat-") as temp:
            path = Path(temp)
            (path / "Replay.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(code, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", temp, "-c", "Release", "-v", "quiet"], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print("\n".join(line for line in result.stdout.splitlines() if ": warning " not in line))


if __name__ == "__main__":
    unittest.main()
