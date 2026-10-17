"""Characterize real town expulsion, pursuit kind, and target authorization.

Leave/engage engine calls, group membership and diplomatic state are fixtures.
No game is launched. Assertions cover town following, fresh combat gates, and suspended own targets.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace GreyWardenPolicePurity {
enum AiBehavior{None,EscortParty,GoAroundParty} enum IntentKind{Approach,Escort,Pursue,Visit}
struct Vec2{public float X;public Vec2(float x){X=x;}public float Distance(Vec2 p)=>Math.Abs(X-p.X);}
class Position{public float X;public Vec2 ToVec2()=>new(X);}
class Settlement{}
class MobileParty{public string StringId="";public bool IsActive=true,IsMainParty,IsBandit,Managed=true;public float Strength=10;public Settlement? CurrentSettlement;
 public object? MapEvent;public Position Position=new();public Vec2 GetPosition2D=>Position.ToVec2();}
class CrimeRecord{public string CrimeId="crime";public bool HasOpenCase=true;public MobileParty? Offender;}
class PoliceTask{public bool WarDeclared,IsPlayerBountyEscort;public CrimeRecord? TargetCrime;}
static class CrimePool{public static Dictionary<string,PoliceTask> Tasks=new();public static PoliceTask? GetTask(string id)=>Tasks.GetValueOrDefault(id);}
static class CrimeState{public static PoliceTask? GetTask(string id)=>CrimePool.GetTask(id);}
static class GwpStrength{public class LocalBalance{public MobileParty Actor=null!;public float Distance;public bool StrengthReady;}
 public static MobileParty CombatGroupOf(MobileParty p)=>p;
 public static LocalBalance Compare(MobileParty a,MobileParty b,bool includePlayer=false)=>new(){Actor=a,Distance=a.GetPosition2D.Distance(b.GetPosition2D),StrengthReady=a.Strength>b.Strength};}
static class CampaignTime{public static NowValue Now=new();public class NowValue{public double ToHours=10;}}
enum WardenDuty {Assistance}
static class GwpWardenDuties {public static bool HoldsAny(MobileParty p, WardenDuty d)=>PoliceEnforcementBehavior.IsPartyOccupiedByAssistance(p);}
static class GwpWardenDispatchBehavior{public static bool IsDispatchParty(MobileParty? p)=>false;}
class PoliceEnforcementBehavior{
 enum EnvoyStage{Travelling,Failed}class EnvoyCase{public EnvoyStage Stage;}
 class LordAssistanceGroup{public string LeaderPartyId="chief",PendingReleaseReason="";public bool DispersedForSpeed;public List<string> MemberPartyIds=new();}
 static PoliceEnforcementBehavior? _instance; Dictionary<string,LordAssistanceGroup> _assistanceGroups=new();Dictionary<string,EnvoyCase> _envoyCases=new();
 static Dictionary<string,MobileParty> Parties=new();
 private static MobileParty? FindActiveParty(string id)=>Parties.GetValueOrDefault(id);
 private static bool IsActiveGreyWardenLordParty(MobileParty? p)=>p?.IsActive==true;
 private static MobileParty? GetActiveAssistanceCaseTarget(LordAssistanceGroup g,PoliceTask? t)=>t?.TargetCrime?.Offender;
 private IEnumerable<MobileParty> FormationSupports(LordAssistanceGroup g)=>Array.Empty<MobileParty>();
 public static bool IsPartyOccupiedByAssistance(MobileParty? p)=>p!=null&&_instance!=null&&_instance._assistanceGroups.Values.Any(g=>g.LeaderPartyId==p.StringId||g.MemberPartyIds.Contains(p.StringId));
 private bool IsAwaitingEnvoyResult(MobileParty p,PoliceTask t,MobileParty o)=>!t.WarDeclared&&_envoyCases.GetValueOrDefault(t.TargetCrime!.CrimeId)?.Stage!=EnvoyStage.Failed;
 private bool TrySendEnvoy(MobileParty police,CrimeRecord crime,MobileParty criminal,bool collectOnly)=>false;
 PRODUCTION
 public bool HoldEnvoy(MobileParty c,PoliceTask task,MobileParty target,bool failed){_envoyCases[task.TargetCrime!.CrimeId]=new(){Stage=failed?EnvoyStage.Failed:EnvoyStage.Travelling};return HoldWarForEnvoy(c,task,target);}
 public bool Declaration(MobileParty c,MobileParty t)=>TryGetNativeDeclarationCandidate(c,t,2,out _,out _);
 public void AddHelper(MobileParty h)=>_assistanceGroups["chief"]=new(){MemberPartyIds=new(){h.StringId}};
 public void EndHelp()=>_assistanceGroups.Clear();
 public static PoliceEnforcementBehavior Setup(MobileParty c,MobileParty t,PoliceTask task){Parties=new(){{c.StringId,c},{t.StringId,t}};CrimePool.Tasks.Clear();CrimePool.Tasks[c.StringId]=task;var e=new PoliceEnforcementBehavior();_instance=e;return e;}
}
class GreyWardenPartyDesireBehavior{
 class Intent{public IntentKind Kind;public double ExpiresAt;public MobileParty? Party;}
 static Dictionary<string,Intent> Intents=new();private static bool IsManagedParty(MobileParty? p)=>p?.Managed==true;
 DESIRE_METHODS
 public static string Kind(PoliceTask t,MobileParty p)=>ResolveUndeclaredPursuitKind(t,p).ToString();
}
static class Replay{
 static int checks;static void Check(bool x,string s){if(!x)throw new Exception(s);checks++;}
 static void Main(){
  var c=new MobileParty{StringId="chief",Strength=200};var t=new MobileParty{StringId="offender",Strength=100,CurrentSettlement=new()};
  var task=new PoliceTask{TargetCrime=new(){Offender=t}};var e=PoliceEnforcementBehavior.Setup(c,t,task);
  Check(GreyWardenPartyDesireBehavior.Kind(task,t)=="Escort","sheltered target uses live escort");
  t.CurrentSettlement=null;Check(GreyWardenPartyDesireBehavior.Kind(task,t)=="Escort","field target uses same live escort");
  task.WarDeclared=true;Check(GreyWardenPartyDesireBehavior.Kind(task,t)=="Pursue","declared task retains native pursuit");task.WarDeclared=false;
  c.CurrentSettlement=t.CurrentSettlement=new();Check(e.HoldEnvoy(c,task,t,false),"travelling envoy holds declaration");
  Check(!e.HoldEnvoy(c,task,t,true)&&e.Declaration(c,t),"failed talks allow declaration inside town under native departure contract");
  Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(c,t),"actual field attack cannot start while actor is in town");
  c.CurrentSettlement=t.CurrentSettlement=null;Check(PoliceEnforcementBehavior.CanInitiateCaseAttack(c,t),"outside town current local advantage permits attack");
  t.Strength=300;Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(c,t),"enemy growth blocks even previously ready attack");
  task.WarDeclared=true;Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(c,t),"already declared war does not bypass fresh strength check");
  t.Strength=100;e.HoldEnvoy(c,task,t,false);task.WarDeclared=false;Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(c,t),"pending envoy blocks actual attack");
  e.HoldEnvoy(c,task,t,true);
  var h=new MobileParty{StringId="helper",Strength=200};var old=new MobileParty{StringId="old",Strength=1000};CrimePool.Tasks[h.StringId]=new(){WarDeclared=true,TargetCrime=new(){Offender=old}};e.AddHelper(h);
  Check(GreyWardenPartyDesireBehavior.IsAuthorizedAttackTarget(h,t),"helper authorized for main target");
  Check(!GreyWardenPartyDesireBehavior.IsAuthorizedAttackTarget(h,old),"paused own target cannot be attacked during assistance");
  Check(PoliceEnforcementBehavior.CanInitiateCaseAttack(h,t),"helper uses chief's failed envoy and current strength");
  Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(h,old),"helper cannot reuse old war against old target");
  e.EndHelp();Check(GreyWardenPartyDesireBehavior.IsAuthorizedAttackTarget(h,old),"own target resumes after release");
  Check(!PoliceEnforcementBehavior.CanInitiateCaseAttack(h,old),"resumed old case must pass its own fresh strength check");
  Console.WriteLine("SHELTERED_ENFORCEMENT_ASSERTIONS="+checks+"; PASS");
 }
}}
'''


class ShelteredEnforcementTests(unittest.TestCase):
    def test_town_expulsion_and_post_assistance_authorization(self):
        desire = (MODULE / 'GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        assistance = (MODULE / 'PoliceEnforcementBehavior.Assistance.cs').read_text(encoding='utf-8-sig')
        envoy = (MODULE / 'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig')
        code = HARNESS.replace('PRODUCTION', '\n'.join(extract(assistance, n) for n in
            ['TryGetNativeDeclarationCandidate','TryGetAssistanceCase','IsAuthorizedAssistanceTarget','CanInitiateCaseAttack']) + '\n' + extract(envoy,'HoldWarForEnvoy'))
        code = code.replace('DESIRE_METHODS', '\n'.join(extract(desire, n) for n in ['ResolveUndeclaredPursuitKind', 'IsAuthorizedAttackTarget']))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-sheltered-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            diagnostics = '\n'.join(line for line in result.stdout.splitlines() if ': warning ' not in line)
            self.assertEqual(0, result.returncode, diagnostics + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('SHELTERED_')))


if __name__ == '__main__':
    unittest.main()
