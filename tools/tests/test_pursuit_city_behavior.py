"""Installed short-term movement and contact across an offender's city visit.

Execute complete decompiled methods. Initiative results and navigation validity
are boundary inputs; this does not run scene pathfinding or simulate battle time.
Real initiative arithmetic with production hooks is tested by regional_threat.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
GAME_BIN = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')

HARNESS = r'''
using System;
namespace TaleWorlds.Library {static class MathF {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}}
namespace Audit {
enum AiBehavior {None,Hold,GoToPoint,GoAroundParty,EngageParty,EscortParty,GoToSettlement,RaidSettlement,AssaultSettlement,BesiegeSettlement,DefendSettlement,PatrolAroundPoint,FleeToPoint,FleeToGate,FleeToParty,JoinParty,MoveToNearestLandOrPort,DoOperation}
enum BattleSideEnum {Attacker,Defender}
struct Vec2 {
 public float X,Y;public Vec2(float x,float y){X=x;Y=y;}public float Length=>(float)Math.Sqrt(X*X+Y*Y);
 public void Normalize(){var n=Length;if(n>0){X/=n;Y/=n;}}
 public void RotateCCW(float angle){float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);(X,Y)=(X*c-Y*s,X*s+Y*c);}
 public static Vec2 operator -(Vec2 a,Vec2 b)=>new(a.X-b.X,a.Y-b.Y);
 public static Vec2 operator *(Vec2 a,float b)=>new(a.X*b,a.Y*b);
}
struct CampaignVec2 {
 public float X,Y;public bool IsOnLand=>true;public int Face=>0;public bool IsValid()=>true;
 public CampaignVec2(float x,float y=0){X=x;Y=y;}public Vec2 ToVec2()=>new(X,Y);
 public float DistanceSquared(CampaignVec2 p)=>(X-p.X)*(X-p.X)+(Y-p.Y)*(Y-p.Y);
 public static CampaignVec2 operator +(CampaignVec2 a,Vec2 b)=>new(a.X+b.X,a.Y+b.Y);
 public static bool operator ==(CampaignVec2 a,CampaignVec2 b)=>a.X==b.X&&a.Y==b.Y;
 public static bool operator !=(CampaignVec2 a,CampaignVec2 b)=>!(a==b);
 public override bool Equals(object? o)=>o is CampaignVec2 v&&this==v;public override int GetHashCode()=>HashCode.Combine(X,Y);
}
interface IInteractablePoint {bool CanPartyInteract(MobileParty p,float dt);void OnPartyInteraction(MobileParty p);}
class PartyBase:IInteractablePoint {
 public static PartyBase MainParty=new();public bool IsMobile=true,IsSettlement,InReach=true;
 public MapEvent? MapEvent;public MobileParty? MobileParty;public object MapFaction=new();public int Interactions;
 public bool CanPartyInteract(MobileParty p,float dt)=>InReach;public void OnPartyInteraction(MobileParty p){Interactions++;}
}
class MapEvent {public Settlement? MapEventSettlement;public PartyBase Attacker=new(),Defender=new();public bool ContainsFriendly;
 public PartyBase GetLeaderParty(BattleSideEnum s)=>s==BattleSideEnum.Attacker?Attacker:Defender;}
class SiegeEvent {public Settlement BesiegedSettlement=new();}
class Settlement {public bool HasPort;public MobileParty? LastAttackerParty;public CampaignVec2 PortPosition,GatePosition;public PartyBase Party=new(){IsMobile=false,IsSettlement=true};}
class MobileParty {
 public enum NavigationType {Default,Naval,All}
 public static MobileParty MainParty=new();public bool IsMainParty=>this==MainParty;
 public bool IsActive=true,IsGarrison,IsCurrentlyAtSea,IsTransitionInProgress,StartTransitionNextFrameToExitFromPort,IsTargetingPort;
 public MobileParty? TargetParty,AttachedTo;public MobileParty? ShortTermTargetParty=>(Ai.AiBehaviorInteractable as PartyBase)?.MobileParty;public Settlement? CurrentSettlement,TargetSettlement,ShortTermTargetSettlement,BesiegedSettlement;
 public object? MapEventSide;public MapEvent? MapEvent;public SiegeEvent? SiegeEvent;
 public CampaignVec2 Position,TargetPosition,AiBehaviorTarget,MoveTargetPoint;public float _lastCalculatedSpeed=3;
 public AiBehavior DefaultBehavior,ShortTermBehavior;public PartyBase Party=new();public NativePartyAi Ai;
 public object MapFaction=>Party.MapFaction;public bool IsCurrentlyEngagingParty=>ShortTermBehavior==AiBehavior.EngageParty;
 public bool IsCurrentlyEngagingSettlement=>ShortTermBehavior==AiBehavior.AssaultSettlement;
 public MobileParty(){Ai=new(this);Party.MobileParty=this;}public int RandomInt(int a,int b)=>0;
 public static bool IsFleeBehavior(AiBehavior b)=>b==AiBehavior.FleeToPoint||b==AiBehavior.FleeToGate||b==AiBehavior.FleeToParty;
 public void SetShortTermBehavior(AiBehavior b,IInteractablePoint? target){ShortTermBehavior=b;Ai.AiBehaviorInteractable=target;}
 public void SetMoveEngageParty(MobileParty target,NavigationType n){DefaultBehavior=AiBehavior.EngageParty;TargetParty=target;}
}
class InitiativeModel {
 public AiBehavior Result;public MobileParty? Target;public float Score;public float AiCheckInterval=>.1f;
 public bool ShouldPartyCheckInitiativeBehavior(MobileParty p)=>true;
 public void GetBestInitiativeBehavior(MobileParty p,out AiBehavior b,out MobileParty t,out float score,out Vec2 vec){b=Result;t=Target!;score=Score;vec=new();}
}
class MapDistance {public bool PathExistBetweenPoints(CampaignVec2 a,in CampaignVec2 b,MobileParty.NavigationType n)=>NavigationHelper.PathValid;}
static class NavigationHelper {public static bool PathValid=true;public static bool IsPositionValidForNavigationType(CampaignVec2 p,MobileParty.NavigationType n)=>PathValid;}
class Models {public InitiativeModel MobilePartyAIModel=new();public MapDistance MapDistanceModel=new();public EncounterModel EncounterModel=new();}
class EncounterModel {public float GetEncounterJoiningRadius=>3;}
class Campaign {public static Campaign Current=new();public bool GameStarted=true;public Models Models=new();}
static class MBRandom {public static float RandomFloat=>0;}
static class Debug {public static void FailedAssert(string a,string b,string c,int n)=>throw new Exception(a);}
class FleeData {public void Clear(){}}
class NativePartyAi {
 private MobileParty _mobileParty;private FleeData _fleeingData=new();public NativePartyAi(MobileParty p){_mobileParty=p;}
 public bool DoNotMakeNewDecisions,IsAlerted;public IInteractablePoint? AiBehaviorInteractable;public float AttackInitiative,AvoidInitiative;
 public void SetDoNotMakeNewDecisions(bool v){DoNotMakeNewDecisions=v;}
 public void SetInitiative(float attack,float avoid,float hours){AttackInitiative=attack;AvoidInitiative=avoid;}
 private void GetFleeBehavior(out AiBehavior b,out CampaignVec2 p,ref Settlement s,MobileParty t,Vec2 v){b=AiBehavior.FleeToPoint;p=new(-20);}
 private void GetFollowBehavior(ref AiBehavior b,ref Settlement s,MobileParty t,out CampaignVec2 p)=>throw new Exception("unexpected follow path");
 private void GetBesiegeBehavior(out AiBehavior b,out CampaignVec2 p,out Settlement s)=>throw new Exception("unexpected siege path");
 private void GetInSettlementBehavior(ref AiBehavior b,ref MobileParty t)=>throw new Exception("unexpected resident actor");
 private void GetDefendSettlementBehavior(Settlement s,out AiBehavior b,out CampaignVec2 p,out MobileParty t)=>throw new Exception("unexpected defend path");
 private void GetBestMoveToNearestLandBehavior(out AiBehavior b,out CampaignVec2 p,out Settlement s)=>throw new Exception("unexpected navigation transition");
 private void GetLandPatrolBehavior(out AiBehavior b,out CampaignVec2 p,CampaignVec2 c,bool f)=>throw new Exception("unexpected patrol");
 private void GetNavalPatrolBehavior(out AiBehavior b,out CampaignVec2 p,CampaignVec2 c,bool f)=>throw new Exception("unexpected naval patrol");
 NATIVE_MOVEMENT_METHODS
 public (AiBehavior,IInteractablePoint,CampaignVec2) Step(){GetBehaviors(out var b,out var target,out var point);_mobileParty.SetShortTermBehavior(b,target);_mobileParty.AiBehaviorTarget=point;return(b,target,point);}
}
class PlayerEncounter {public static MobileParty? EncounteredMobileParty;public static PlayerEncounter? Current;public BattleSideEnum PlayerSide;}
static class NativeEncounter { ENCOUNTER_METHOD }
static class ProductionCommand {
 private static void ResolveNavigation(MobileParty a,MobileParty b,out MobileParty.NavigationType n,out bool from){n=MobileParty.NavigationType.Default;from=false;}
 DIRECT_ATTACK_METHOD
 public static void Run(MobileParty a,MobileParty b)=>StartDirectAttack(a,b);
}
static class Replay {
 static int Checks;static void Check(bool b,string why){Checks++;if(!b)throw new Exception(why);}
 static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0001f,why+": "+a+" != "+b);
 static void Main(){
  var chief=new MobileParty{Position=new(-2),DefaultBehavior=AiBehavior.GoAroundParty};
  var target=new MobileParty{Position=new(0),CurrentSettlement=new(),_lastCalculatedSpeed=6};chief.TargetParty=target;
  var first=chief.Ai.Step();Check(first.Item1==AiBehavior.GoToPoint,"native go-around remains point movement, not a Hold/wait state");
  Near(first.Item3.X,-5.95125f,"normal navigable fixture chooses native outer point rather than town gate");
  Check(first.Item2==target.Party&&chief.TargetSettlement==null,"town residence does not turn wartime go-around into settlement visit");
  target.CurrentSettlement=null;
  chief.ShortTermBehavior=AiBehavior.EngageParty;
  Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.FleeToPoint;
  Campaign.Current.Models.MobilePartyAIModel.Target=target;
  Campaign.Current.Models.MobilePartyAIModel.Score=.0472f;
  Check(chief.Ai.Step().Item1==AiBehavior.GoToPoint,"recorded 0.0472 flee candidate does not initiate flight from previous engage");
  Check(!chief.Ai.IsAlerted,"sub-threshold flee candidate leaves actor unalerted");
  chief.ShortTermBehavior=AiBehavior.FleeToPoint;chief.AiBehaviorTarget=new(-20);
  Check(chief.Ai.Step().Item1==AiBehavior.FleeToPoint,"sub-threshold flee can continue an existing unfinished flight, so score alone is insufficient");
  chief.ShortTermBehavior=AiBehavior.GoToPoint;
  target.CurrentSettlement=new();
  Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.None;
  Campaign.Current.Models.MobilePartyAIModel.Score=0;
  chief.Position=first.Item3;
  for(int tick=0;tick<48;tick++){var step=chief.Ai.Step();Check(step.Item1==AiBehavior.GoToPoint&&step.Item3==first.Item3,"stable surroundings retain outer destination");}
  var patrol=new MobileParty{Position=new(-.2f)};ProductionCommand.Run(patrol,target);
  var order=patrol.Ai.Step();Check(order.Item1==AiBehavior.EngageParty&&patrol.Ai.DoNotMakeNewDecisions,"production interceptor stays direct-engage locked");
  Near(order.Item3.X,0,"interceptor aims at resident party position rather than lord's outer point");
  NativeEncounter.HandleEncounterForMobileParty(patrol,1);Check(target.Party.Interactions==0,"native contact cannot attack ordinary resident");
  Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.FleeToPoint;Campaign.Current.Models.MobilePartyAIModel.Target=chief;Campaign.Current.Models.MobilePartyAIModel.Score=2;
  order=patrol.Ai.Step();Check(order.Item1==AiBehavior.EngageParty,"interceptor lock retains direct attack through initiative boundary input");
  Campaign.Current.Models.MobilePartyAIModel.Target=patrol;
  Check(chief.Ai.Step().Item1==AiBehavior.FleeToPoint,"unlocked chief may leave outer destination for a new native threat");
  Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.None;Campaign.Current.Models.MobilePartyAIModel.Score=0;
  chief.ShortTermBehavior=AiBehavior.None;chief.DefaultBehavior=AiBehavior.GoToSettlement;chief.TargetSettlement=new();chief.TargetPosition=new(50);
  Check(chief.Ai.Step().Item1==AiBehavior.GoToSettlement,"if later hourly visit wins, resident target has no overriding attack and chief can depart");
  target.CurrentSettlement=null;target.Position=new(1);order=patrol.Ai.Step();
  Near(order.Item3.X,1,"existing interceptor tracks offender's new field position without new pursuit command");
  target.Party.InReach=false;NativeEncounter.HandleEncounterForMobileParty(patrol,1);Check(target.Party.Interactions==0,"departure still requires actual contact");
  target.Party.InReach=true;NativeEncounter.HandleEncounterForMobileParty(patrol,1);Check(target.Party.Interactions==1,"existing interceptor can engage after departure and contact");
  target.MapEvent=new();Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.EngageParty;Campaign.Current.Models.MobilePartyAIModel.Target=target;Campaign.Current.Models.MobilePartyAIModel.Score=1.6536f;
  Check(chief.Ai.Step().Item1==AiBehavior.EngageParty,"independently verified battle initiative above one overrides long-term visit");
  chief.DefaultBehavior=AiBehavior.GoAroundParty;chief.TargetParty=target;chief.ShortTermBehavior=AiBehavior.None;
  target.MapEvent=null;target.CurrentSettlement=new();Campaign.Current.Models.MobilePartyAIModel.Result=AiBehavior.None;Campaign.Current.Models.MobilePartyAIModel.Score=0;
  NavigationHelper.PathValid=false;Check(chief.Ai.Step().Item1==AiBehavior.EngageParty,"native go-around falls back to Engage when no defending position is valid");
  NativeEncounter.HandleEncounterForMobileParty(chief,1);Check(target.Party.Interactions==1,"fallback Engage still cannot pull town resident out");
  Console.WriteLine("PURSUIT_CITY_BEHAVIOR: normal outer point=-5.95125; existing interceptor=EngageParty with city contact blocked; departure resumes tracking/contact; chief has no dedicated wait lock");
  Console.WriteLine("PURSUIT_CITY_BEHAVIOR_ASSERTIONS="+Checks+"; PASS; complete native managed movement/contact, initiative and scene navigation are boundary inputs");
 }
}
}
'''


class PursuitCityBehaviorTests(unittest.TestCase):
    def test_native_movement_across_city_entry_and_departure(self):
        def decompile(name):
            return subprocess.check_output(['ilspycmd', '-r', str(GAME_BIN), '-t', name,
                str(GAME_BIN / 'TaleWorlds.CampaignSystem.dll')], text=True, encoding='utf-8')
        ai = decompile('TaleWorlds.CampaignSystem.Party.MobilePartyAi')
        encounter = decompile('TaleWorlds.CampaignSystem.EncounterManager')
        production = (ROOT / 'GreyWardenPolicePurity/GreyWardenPartyDesireBehavior.cs').read_text(encoding='utf-8-sig')
        code = HARNESS.replace('NATIVE_MOVEMENT_METHODS', '\n'.join(extract(ai, name) for name in
            ['GetBehaviors', 'GetGoAroundPartyBehavior', 'GetDefendingPosition']))
        code = code.replace('ENCOUNTER_METHOD', extract(encounter, 'HandleEncounterForMobileParty'))
        code = code.replace('DIRECT_ATTACK_METHOD', extract(production, 'StartDirectAttack'))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-pursuit-city-') as directory:
            path = Path(directory)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            diagnostics = '\n'.join(line for line in result.stdout.splitlines() if ': warning ' not in line)
            self.assertEqual(0, result.returncode, diagnostics + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('PURSUIT_CITY_')))


if __name__ == '__main__':
    unittest.main()
