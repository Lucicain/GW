"""Execute installed vanilla initiative bodies, with controlled engine objects.

This characterizes the native-game managed policy, not a new GWP policy.
Spatial search uses real fixture coordinates. Path reachability, faction war,
strength, and model authorization are boundary inputs; no game is launched.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

GAME_BIN = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace TaleWorlds.Library {static class MathF {
 public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
 public static float Sqrt(float a)=>(float)Math.Sqrt(a);public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
 public static float Sin(float a)=>(float)Math.Sin(a);public static float Cos(float a)=>(float)Math.Cos(a);
}}
namespace Audit {
enum AiBehavior{None,PatrolAroundPoint,DefendSettlement,EngageParty,GoAroundParty,FleeToPoint,FleeToGate}
enum BattleSideEnum{Attacker,Defender}
struct Vec2 {public float X,Y;public Vec2(float x,float y=0){X=x;Y=y;}public static Vec2 Zero=>new();
 public float Length=>(float)Math.Sqrt(X*X+Y*Y);public Vec2 Normalized()=>Length==0?new():new(X/Length,Y/Length);
 public float Distance(Vec2 p)=>(this-p).Length;public float DistanceSquared(Vec2 p){var d=this-p;return d.X*d.X+d.Y*d.Y;}
 public static Vec2 operator -(Vec2 a,Vec2 b)=>new(a.X-b.X,a.Y-b.Y);public static Vec2 operator -(Vec2 a)=>new(-a.X,-a.Y);}
class CampaignVec2 {public float X;public bool IsOnLand=true;public Vec2 ToVec2()=>new(X);
 public float Distance(CampaignVec2 p)=>Math.Abs(X-p.X);public float Distance(Vec2 p)=>ToVec2().Distance(p);
 public float DistanceSquared(CampaignVec2 p)=>(X-p.X)*(X-p.X);}
class Faction {public string Id="";public HashSet<Faction> Enemies=new();public bool IsAtWarWith(Faction f)=>Enemies.Contains(f);}
static class FactionManager {public static bool IsAtWarAgainstFaction(Faction a,Faction b)=>a.IsAtWarWith(b);}
static class DiplomacyHelper {public static bool IsSameFactionAndNotEliminated(Faction a,Faction b)=>a==b;}
class Hero {public bool IsLord=true,IsMinorFactionHero;}
class PartyBase {public float EstimatedStrength;public Faction MapFaction=null!;public bool IsMobile=true;
 public MobileParty MobileParty=null!;public MapEvent? MapEvent=>MobileParty.MapEvent;public SiegeEvent? SiegeEvent=>MobileParty.SiegeEvent;
 public float GetCustomStrength(BattleSideEnum s,MapEvent.PowerCalculationContext c)=>EstimatedStrength;}
class Settlement {public bool HasPort,IsUnderSiege,IsHideout,IsVillage;public SiegeEvent? SiegeEvent;
 public CampaignVec2 Position=new(),PortPosition=new();public Faction MapFaction=new();public PartyBase Party=new();public MobileParty? LastAttackerParty;}
class MapEventParty {public PartyBase Party=null!;}
class Side {public PartyBase LeaderParty=null!;public Side OtherSide=null!;public List<MapEventParty> Parties=new();}
class MapEvent {public enum PowerCalculationContext{SeaBattle,LandBattle}public enum BattleTypes{BlockadeBattle,Siege}
 public Settlement? MapEventSettlement;public bool IsBlockade,IsSiegeAssault,IsSallyOut;public Side AttackerSide=new(),DefenderSide=new();
 public PartyBase GetLeaderParty(BattleSideEnum s)=>s==BattleSideEnum.Attacker?AttackerSide.LeaderParty:DefenderSide.LeaderParty;}
class Camp {public MobileParty LeaderParty=null!;public List<PartyBase> Parties=new();
 public IEnumerable<PartyBase> GetInvolvedPartiesForEventType(MapEvent.BattleTypes b=MapEvent.BattleTypes.Siege)=>Parties;}
class SiegeEvent {public Settlement BesiegedSettlement=null!;public Camp BesiegerCamp=new();}
class Army {public MobileParty LeaderParty=null!;public float EstimatedStrength;public List<MobileParty> Parties=new();public bool Waiting;
 public bool DoesLeaderPartyAndAttachedPartiesContain(MobileParty p)=>LeaderParty==p||LeaderParty.AttachedParties.Contains(p);
 public float GetCustomStrength(BattleSideEnum s,MapEvent.PowerCalculationContext c)=>EstimatedStrength;public bool IsWaitingForArmyMembers()=>Waiting;}
class MobilePartyAi {public PartyBase? AiBehaviorPartyBase;public float AttackInitiative=1,AvoidInitiative=1;
 public static List<(float,Vec2)> DangerousPartiesAndTheirVecs=new();}
class LocatableSearchData<T> {public List<MobileParty> Parties=new();public int Index;}
class MobileParty {public string Id="";public bool IsGarrison,IsMilitia,IsCurrentlyAtSea,IsBandit,IsCaravan,IsVillager,IsPatrolParty;
 public bool IsLordParty=true,IsActive=true,ShouldBeIgnored,IsTransitionInProgress,IsTargetingPort,IsInRaftState,IsMoving,Reachable=true;
 public bool HasNavalNavigationCapability,HasLandNavigationCapability=true;public bool IsMainParty=>this==MainParty;
 public float Aggressiveness=1,_lastCalculatedSpeed=4,LastCalculatedBaseSpeed=4;public CampaignVec2 Position=new(),TargetPosition=new(),AiBehaviorTarget=new();
 public Vec2 VisualPosition2DWithoutError=>Position.ToVec2();public Settlement? CurrentSettlement,TargetSettlement,BesiegedSettlement,LastVisitedSettlement;
 public MobileParty? AttachedTo,TargetParty,ShortTermTargetParty;public Army? Army;public MapEvent? MapEvent;public Camp? BesiegerCamp;public SiegeEvent? SiegeEvent;
 public Side MapEventSide=new();public AiBehavior DefaultBehavior=AiBehavior.GoAroundParty,ShortTermBehavior;public MobilePartyAi Ai=new();
 public Hero LeaderHero=new();public PartyBase Party=new();public Faction MapFaction=>Party.MapFaction;public List<MobileParty> AttachedParties=new();
 public enum NavigationType{Default,All}public NavigationType NavigationCapability=NavigationType.Default;public bool IsFleeing()=>ShortTermBehavior==AiBehavior.FleeToPoint;
 public void RecalculateShortTermBehavior(){}public static MobileParty MainParty=new();public static List<MobileParty> All=new();
 public static LocatableSearchData<MobileParty> StartFindingLocatablesAroundPosition(Vec2 p,float r)=>new(){Parties=All.Where(x=>x.Position.ToVec2().Distance(p)<=r).ToList()};
 public static MobileParty? FindNextLocatable(ref LocatableSearchData<MobileParty> d)=>d.Index<d.Parties.Count?d.Parties[d.Index++]:null;}
static class DistanceHelper {public static bool FindClosestDistanceFromMobilePartyToMobileParty(MobileParty a,MobileParty b,MobileParty.NavigationType n,float r,out float d,out bool v){d=a.Position.Distance(b.Position);v=false;return b.Reachable;}}
static class Extensions {public static bool ContainsQ<T>(this IEnumerable<T> x,Func<T,bool> p)=>x.Any(p);}
static class MBMath {public static float ClampFloat(float x,float a,float b)=>Math.Clamp(x,a,b);}
static class CampaignTime {public const int HoursInDay=24;}
class Encounter {public float GetEncounterJoiningRadius=3,NeededMaximumNavalDistanceForEncounteringMobileParty=.5f,NeededMaximumLandDistanceForEncounteringMobileParty=.5f;}
class MilitaryPower {public MapEvent.PowerCalculationContext GetContextForPosition(CampaignVec2 p)=>MapEvent.PowerCalculationContext.LandBattle;}
class Simulation {public float GetSettlementAdvantage(Settlement s)=>1;}
class Models {public Encounter EncounterModel=new();public NativeAi MobilePartyAIModel=null!;public MilitaryPower MilitaryPowerModel=new();public Simulation CombatSimulationModel=new();}
class Campaign {public static Campaign Current=new();public Models Models=new();public float GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType n)=>20;}
class NativeAi {
 public float SettlementDefendingNearbyPartyCheckRadius=>9;public bool AttackAllowed=true,AvoidAllowed=true;
 public Dictionary<MobileParty,(float Advantage,float Attack,float Avoid)> Observed=new();
 public bool ShouldConsiderAttacking(MobileParty a,MobileParty b)=>AttackAllowed;public bool ShouldConsiderAvoiding(MobileParty a,MobileParty b)=>AvoidAllowed;
 private void CalculateInitiativeScoresForEnemy(MobileParty a,MobileParty b,out float avoid,out float attack,float ratio,float aggression){
  CalculateInitiativeScoresForEnemyImpl(a,b,out avoid,out attack,ratio,aggression);Observed[b]=(ratio,attack,avoid);
 }
 NATIVE_METHODS
}
static class Replay {
 static int Checks;static void Check(bool b,string s){if(!b)throw new Exception(s);Checks++;}
 static void Near(float x,float y,string s)=>Check(Math.Abs(x-y)<.0002f,s+": "+x+" != "+y);
 static MobileParty New(string id,float x,float strength,Faction f){var p=new MobileParty{Id=id};p.Position.X=x;p.Party=new(){MobileParty=p,EstimatedStrength=strength,MapFaction=f};MobileParty.All.Add(p);return p;}
 static (NativeAi,MobileParty,MobileParty,MobileParty) Setup(float d=1.43f,float supportX=4.5f){
  MobileParty.All.Clear();var own=new Faction{Id="warden"};var enemy=new Faction{Id="enemy"};own.Enemies.Add(enemy);enemy.Enemies.Add(own);
  var a=New("chief",-d,300,own);var t=New("offender",0,200,enemy);var r=New("enemy_support",supportX,600,enemy);
  a.TargetParty=t;var n=new NativeAi();Campaign.Current=new();Campaign.Current.Models.MobilePartyAIModel=n;return(n,a,t,r);
 }
 static void Main(){
  var(n,a,t,r)=Setup();n.GetBestInitiativeBehavior(a,out var behavior,out var target,out var score,out _);
  Near(n.Observed[t].Advantage,300.01f/200.01f,"close observer excludes outer reinforcement for offender estimate");
  Check(n.Observed.ContainsKey(r),"600-strength reinforcement separately considered as hostile target");
  Console.WriteLine($"NATIVE_NEAR: behavior={behavior}, target={target?.Id}, offender_ratio={n.Observed[t].Advantage:F4}, attack={n.Observed[t].Attack:F4}, reinforcement_avoid={n.Observed[r].Avoid:F4}");
  Check(behavior==AiBehavior.EngageParty&&target==t,"close attack wins despite stronger nearby enemy");
  Check(n.Observed[t].Attack>100&&n.Observed[r].Avoid>1,"close attack multiplier outweighs separate flee score");
  n.AttackAllowed=false;n.Observed.Clear();n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Check(behavior==AiBehavior.FleeToPoint&&n.Observed[t].Attack==0,"model authorization can veto close attack before its 100-fold multiplier");
  (n,a,t,r)=Setup(5);n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/200.01f,"reinforcement 9.5 from observer excluded by observer-centered scan radius 9");
  Check(!n.Observed.ContainsKey(r),"near offender reinforcement can lie outside observer scan entirely");
  Console.WriteLine($"NATIVE_SCAN_EDGE: behavior={behavior}, target={target?.Id}, offender_ratio={n.Observed[t].Advantage:F4}");
  a.Position.X=5;n.Observed.Clear();n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/(200.01f+600*.5f*.9f),"farther observer weights reinforcement half then enemy 0.9");
  Console.WriteLine($"NATIVE_FAR: behavior={behavior}, target={target?.Id}, offender_ratio={n.Observed[t].Advantage:F4}");
  Check(behavior==AiBehavior.FleeToPoint,"same forces farther from offender select flee");
  (n,a,t,r)=Setup();r.Ai.AiBehaviorPartyBase=t.Party;n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/(200.01f+600*.9f),"explicit common combat target gives full contribution beyond shrinking near ring");
  Check(behavior==AiBehavior.FleeToPoint,"coordinating enemy reinforcement prevents optimistic close strike here");
  (n,a,t,r)=Setup();a.MapFaction.Enemies.Clear();t.MapFaction.Enemies.Clear();n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Check(n.Observed.Count==0&&behavior==AiBehavior.None,"neutral before declaration is not evidence of safe regional advantage");
  (n,a,t,r)=Setup();r.Position.X=2;n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/(200.01f+600*.9f),"inner reinforcement full before native enemy 0.9 factor");
  Check(behavior==AiBehavior.FleeToPoint,"strong reinforcement in inner ring leads to flee");
  (n,a,t,r)=Setup();r.Aggressiveness=0;n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/200.01f,"zero-aggression reinforcement omitted");
  (n,a,t,r)=Setup();r.IsGarrison=true;n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/200.01f,"garrison deliberately excluded from field support");
  (n,a,t,r)=Setup();r.Position.X=2;var third=new Faction{Id="another_enemy"};a.MapFaction.Enemies.Add(third);third.Enemies.Add(a.MapFaction);r.Party.MapFaction=third;
  n.GetBestInitiativeBehavior(a,out behavior,out target,out score,out _);
  Near(n.Observed[t].Advantage,300.01f/200.01f,"different hostile faction is not summed into offender faction");
  Check(n.Observed.ContainsKey(r),"different hostile faction instead gets separate initiative score");
  Console.WriteLine("NATIVE_REGIONAL_ASSERTIONS="+Checks+"; CHARACTERIZED; original managed decisions, controlled engine boundaries");
 }
}
}
'''


class NativeRegionalStrengthTests(unittest.TestCase):
    def test_installed_native_regional_strength_and_choice(self):
        source = subprocess.check_output(['ilspycmd', '-r', str(GAME_BIN), '-t',
            'TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyAIModel',
            str(GAME_BIN / 'TaleWorlds.CampaignSystem.dll')], text=True, encoding='utf-8')
        names = ['GetBestInitiativeBehavior', 'IsEnemy', 'CalculateInitiativeScoresForEnemy',
                 'GetInitiativeDistanceForAttack', 'CalculateStanceScore']
        methods = []
        for name in names:
            method = extract(source, name).replace('public override void', 'public void')
            if name == 'CalculateInitiativeScoresForEnemy':
                method = method.replace('void CalculateInitiativeScoresForEnemy(', 'void CalculateInitiativeScoresForEnemyImpl(', 1)
            methods.append(method)
        code = HARNESS.replace('NATIVE_METHODS', '\n'.join(methods))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-native-region-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            diagnostics = '\n'.join(line for line in result.stdout.splitlines() if ': warning ' not in line)
            self.assertEqual(0, result.returncode, diagnostics + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('NATIVE_')))


if __name__ == '__main__':
    unittest.main()
