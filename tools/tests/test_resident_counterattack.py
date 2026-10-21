"""Couple both parties' native initiative, movement and exit admission.

Unlike a supplied initiative result, resident decisions are calculated by the
installed native policy. World objects, navigation validity and final action
dispatch remain fixture boundaries; this is not a headless Bannerlord campaign.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract
from test_native_regional_strength import HARNESS, GAME_BIN

AI_ADAPTER = r'''
interface IInteractablePoint {bool CanPartyInteract(MobileParty p,float dt);void OnPartyInteraction(MobileParty p);}
class PastTime {public bool IsPast=true;}
class FleeData {public bool CcwFleeDirectionIsBlocked,CwFleeDirectionIsBlocked;public void Clear(){}}
enum MoveModeType {Hold,Point,Party}
static class MBRandom {public static float RandomFloat=>0;}
static class Debug {public static void FailedAssert(string a,string b,string c,int n)=>throw new Exception(a);}
static class NavigationHelper {public static bool IsPositionValidForNavigationType(CampaignVec2 p,MobileParty.NavigationType n)=>true;}
static class MobilePartyHelper {public static bool CanPartyAttackWithCurrentMorale(MobileParty p)=>p.MoraleAllowsAttack;}
class SettlementLocator {
 public List<Settlement> All=new();public LocatableSearchData<Settlement> StartFindingLocatablesAroundPosition(Vec2 p,float r)=>new(){Settlements=All.Where(s=>s.Position.Distance(p)<r).ToList()};
 public Settlement? FindNextLocatable(ref LocatableSearchData<Settlement> d)=>d.Index<d.Settlements.Count?d.Settlements[d.Index++]:null;
}
class NativePartyAiBody {
 private readonly MobileParty _mobileParty;private readonly FleeData _fleeingData=new();public NativePartyAiBody(MobileParty p){_mobileParty=p;}
 public bool DoNotMakeNewDecisions,IsAlerted,IsDisabled;public PastTime DoNotAttackMainPartyUntil=new();
 public PartyBase? AiBehaviorPartyBase;public IInteractablePoint? AiBehaviorInteractable=>AiBehaviorPartyBase;
 public float AttackInitiative=1,AvoidInitiative=1;public CampaignVec2 BehaviorTarget{get=>_mobileParty.AiBehaviorTarget;set=>_mobileParty.AiBehaviorTarget=value;}
 private void CalculateFleePosition(out CampaignVec2 p,MobileParty t,Vec2 v)=>throw new Exception("unexpected open-field escape path");
 private void GetFollowBehavior(ref AiBehavior b,ref Settlement s,MobileParty t,out CampaignVec2 p)=>throw new Exception("unexpected follow branch");
 private void GetBesiegeBehavior(out AiBehavior b,out CampaignVec2 p,out Settlement s)=>throw new Exception("unexpected siege");
 private void GetDefendSettlementBehavior(Settlement s,out AiBehavior b,out CampaignVec2 p,out MobileParty t)=>throw new Exception("unexpected defend");
 private void GetBestMoveToNearestLandBehavior(out AiBehavior b,out CampaignVec2 p,out Settlement s)=>throw new Exception("unexpected naval transition");
 private void GetLandPatrolBehavior(out AiBehavior b,out CampaignVec2 p,CampaignVec2 c,bool forceUpdate)=>throw new Exception("unexpected patrol");
 private void GetNavalPatrolBehavior(out AiBehavior b,out CampaignVec2 p,CampaignVec2 c,bool forceUpdate)=>throw new Exception("unexpected naval patrol");
 AI_METHODS
 public (AiBehavior,CampaignVec2) Step(){GetBehaviors(out var b,out var target,out var point);SetAiBehavior(b,target,point);return(b,point);}
}
'''

SCENARIOS = r'''
static class ResidentScenario {
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static MobileParty New(string id,float x,float strength,Faction f){var p=new MobileParty{Id=id};p.Position.X=x;p.Party=new(){MobileParty=p,EstimatedStrength=strength,MapFaction=f};MobileParty.All.Add(p);return p;}
 static void Main(){
  foreach(float chiefDistance in new[]{.5f,2f,3f,5.95125f,8f}){
   MobileParty.All.Clear();var own=new Faction{Id="warden"};var enemy=new Faction{Id="enemy",IsKingdomFaction=true};own.Enemies.Add(enemy);enemy.Enemies.Add(own);
   var chief=New("chief",-chiefDistance,300,own);var resident=New("resident",0,110,enemy);var interceptor=New("interceptor",-.2f,16,own);
   var town=new Settlement{IsFortification=true,Position=new(){X=0},GatePosition=new(){X=0},MapFaction=enemy};town.Party.Settlement=town;
   resident.CurrentSettlement=resident.TargetSettlement=town;resident.DefaultBehavior=AiBehavior.GoToSettlement;
   chief.DefaultBehavior=AiBehavior.GoAroundParty;chief.TargetParty=resident;chief.Ai.AiBehaviorPartyBase=resident.Party;
   interceptor.IsLordParty=false;interceptor.LeaderHero=null!;interceptor.Ai.AvoidInitiative=0;
   interceptor.DefaultBehavior=interceptor.ShortTermBehavior=AiBehavior.EngageParty;interceptor.TargetParty=interceptor.ShortTermTargetParty=resident;interceptor.Ai.AiBehaviorPartyBase=resident.Party;interceptor.Ai.DoNotMakeNewDecisions=true;
   Campaign.Current=new();Campaign.Current.SettlementLocator.All.Add(town);var model=new NativeAi();Campaign.Current.Models.MobilePartyAIModel=model;
   var chiefMove=chief.Ai.Step();
   Check(chiefMove.Item1==AiBehavior.GoToPoint,"strong chief's real native movement selects an outer point around resident");
   Check(Math.Abs(chiefMove.Item2.Distance(town.Position)-5.95125f)<.0001f,"outer destination is computed by complete native defending-position method");
   var decision=resident.Ai.Step();int count=-1,grid=-1;var exits=new MobileParty[3];var changed=new MobileParty[3];
   resident.CheckExitingSettlementParallel(ref count,ref exits,ref grid,ref changed);
   if(chiefDistance<=3){
    Check(decision.Item1==AiBehavior.GoToSettlement,"close chief makes resident remain in settlement");
    Check(count==-1,"native exit gate keeps fleeing resident in its current refuge");
   } else {
    Check(decision.Item1==AiBehavior.EngageParty&&resident.MoveTargetParty==interceptor,"resident itself chooses small interceptor despite stronger chief nearby");
    Check(count==0&&exits[0]==resident,"native full exit predicate queues resident to leave without forced expulsion");
    var attack=model.Observed[interceptor];var flee=model.Observed[chief];
    Check(attack.Attack>flee.Avoid,"attack and avoidance are jointly resolved by real policy");
   }
   Console.WriteLine($"RESIDENT_COUPLED: chief={chiefDistance:F5}, behavior={decision.Item1}, movementTarget={resident.MoveTargetParty?.Id}, exitQueued={count==0}, attackSmall={model.Observed[interceptor].Attack:F4}, avoidChief={model.Observed[chief].Avoid:F4}");
   if(chiefDistance==2){
    // Apply the computed destination as a navigation boundary, not as another AI decision.
    chief.Position=chiefMove.Item2;
    var afterMove=resident.Ai.Step();count=-1;
    resident.CheckExitingSettlementParallel(ref count,ref exits,ref grid,ref changed);
    Check(afterMove.Item1==AiBehavior.EngageParty&&resident.MoveTargetParty==interceptor&&count==0,"native chief outer move changes resident from staying to requesting exit to attack small squad");
    Console.WriteLine("RESIDENT_LINKED_SEQUENCE: close chief -> native outer destination applied at navigation boundary -> resident independently selects interceptor -> native exit queued");
   }
  }
  Console.WriteLine("RESIDENT_COUPLED_ASSERTIONS="+checks+"; PASS; real native initiative -> complete movement -> native exit predicate; engine commit/pathfinding excluded");
 }
}
}
'''


def build_code(ai_source, party_source, model_source):
    methods = []
    for name in ['GetBestInitiativeBehavior', 'IsEnemy', 'CalculateInitiativeScoresForEnemy',
                 'GetInitiativeDistanceForAttack', 'CalculateStanceScore', 'ShouldConsiderAttacking',
                 'ShouldConsiderAvoiding', 'ShouldPartyCheckInitiativeBehavior']:
        body = extract(model_source, name).replace('public override ', 'public ')
        if name == 'CalculateInitiativeScoresForEnemy':
            body = body.replace('void CalculateInitiativeScoresForEnemy(', 'void CalculateInitiativeScoresForEnemyImpl(', 1)
        methods.append(body)
    code = HARNESS[:HARNESS.index('static class Replay {')].replace('NATIVE_METHODS', '\n'.join(methods))
    code = code.replace('using System;', 'using System;using System.Threading;', 1)
    code = code.replace('static class MathF {', 'static class MathF {public static float Abs(float x)=>Math.Abs(x);')
    code = code.replace('enum AiBehavior{', 'enum AiBehavior{Hold,GoToPoint,EscortParty,RaidSettlement,AssaultSettlement,BesiegeSettlement,JoinParty,MoveToNearestLandOrPort,DoOperation,FleeToParty,')
    code = code.replace('public float Length=>', 'public void Normalize(){var n=Length;if(n>0){X/=n;Y/=n;}}public void RotateCCW(float a){var c=(float)Math.Cos(a);var s=(float)Math.Sin(a);(X,Y)=(X*c-Y*s,X*s+Y*c);}public float AngleBetween(Vec2 b)=>0;public static Vec2 operator *(Vec2 a,float b)=>new(a.X*b,a.Y*b);public float Length=>')
    code = code.replace('class CampaignVec2 {', 'class CampaignVec2 {public int Face=>0;public bool IsValid()=>true;public void Normalize(){X=Math.Sign(X);}public static CampaignVec2 operator +(CampaignVec2 a,Vec2 b)=>new(){X=a.X+b.X};public static CampaignVec2 operator -(CampaignVec2 a,CampaignVec2 b)=>new(){X=a.X-b.X};public float DistanceSquared(Vec2 p)=>ToVec2().DistanceSquared(p);')
    code = code.replace('public HashSet<Faction> Enemies=', 'public bool IsKingdomFaction;public HashSet<Faction> Enemies=')
    code = code.replace('class PartyBase {', 'class PartyBase:IInteractablePoint {public Settlement? Settlement;public bool IsValid=>true;public bool IsSettlement=>Settlement!=null;public bool CanPartyInteract(MobileParty p,float dt)=>true;public void OnPartyInteraction(MobileParty p){}')
    code = code.replace('public bool HasPort,IsUnderSiege', 'public bool IsFortification;public CampaignVec2 GatePosition=new();public bool HasPort,IsUnderSiege')
    code = code.replace('public Settlement BesiegedSettlement=null!;', 'public bool IsBlockadeActive;public Settlement BesiegedSettlement=null!;')
    begin = code.index('class MobilePartyAi {')
    end = code.index('class LocatableSearchData<T>', begin)
    code = code[:begin] + 'class MobilePartyAi:NativePartyAiBody {public MobilePartyAi(MobileParty p):base(p){}public static List<(float,Vec2)> DangerousPartiesAndTheirVecs=new();}\n' + code[end:]
    code = code.replace('public List<MobileParty> Parties=new();public int Index;', 'public List<MobileParty> Parties=new();public List<Settlement> Settlements=new();public int Index;')
    code = code.replace('class MobileParty {', '''class MobileParty {
 public bool MoraleAllowsAttack=true,StartTransitionNextFrameToExitFromPort;public Settlement? ShortTermTargetSettlement,HomeSettlement;
 public MoveModeType PartyMoveMode;public MobileParty? MoveTargetParty;public CampaignVec2 MoveTargetPoint=new();public bool _aiPathNotFound;
 public MobileParty(){Ai=new(this);}public int RandomInt(int a,int b)=>0;
 public static bool IsFleeBehavior(AiBehavior b)=>b==AiBehavior.FleeToPoint||b==AiBehavior.FleeToGate||b==AiBehavior.FleeToParty;
 public void SetShortTermBehavior(AiBehavior b,IInteractablePoint target){ShortTermBehavior=b;Ai.AiBehaviorPartyBase=target as PartyBase;ShortTermTargetParty=Ai.AiBehaviorPartyBase?.MobileParty;ShortTermTargetSettlement=Ai.AiBehaviorPartyBase?.Settlement;}
 public void SetNavigationModeHold(){PartyMoveMode=MoveModeType.Hold;}
 private void UpdatePathModeWithPosition(CampaignVec2 p){MoveTargetPoint=p;}
 private void InitializeNavigationTransitionParallel(CampaignVec2 a,CampaignVec2 b,ref int n,ref MobileParty[] rows)=>throw new Exception("unexpected naval branch");
 PARTY_METHODS
''')
    code = code.replace('public MobilePartyAi Ai=new();', 'public MobilePartyAi Ai;')
    code = code.replace('enum NavigationType{Default,All}', 'enum NavigationType{Default,Naval,All}')
    code = code.replace('static class DistanceHelper {', '''static class DistanceHelper {
 public static float FindClosestDistanceFromMobilePartyToMobileParty(MobileParty a,MobileParty b,MobileParty.NavigationType n)=>a.Position.Distance(b.Position);
 public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty a,Settlement b,MobileParty.NavigationType n)=>a.Position.Distance(b.Position);
''')
    code = code.replace('public float SettlementDefendingNearbyPartyCheckRadius=>9;', 'public float AiCheckInterval=>.1f;public float FleeToNearbyPartyRadius=>12;public float FleeToNearbySettlementRadius=>12;public float SettlementDefendingNearbyPartyCheckRadius=>9;')
    code = code.replace('public bool ShouldConsiderAttacking(MobileParty a,MobileParty b)=>AttackAllowed;public bool ShouldConsiderAvoiding(MobileParty a,MobileParty b)=>AvoidAllowed;', '')
    code = code.replace('class Models {', 'class PathModel {public bool PathExistBetweenPoints(CampaignVec2 a,in CampaignVec2 b,MobileParty.NavigationType n)=>true;}\nclass Models {public PathModel MapDistanceModel=new();')
    code = code.replace('public float GetEncounterJoiningRadius=3,', 'public float NeededMaximumDistanceForEncounteringTown=1;public float GetEncounterJoiningRadius=3,')
    code = code.replace('class Campaign {', 'class Campaign {public const float MapDiagonal=1000;public bool GameStarted=true;public SettlementLocator SettlementLocator=new();')
    body = AI_ADAPTER.replace('AI_METHODS', '\n'.join(extract(ai_source, name) for name in [
        'GetBehaviors', 'GetGoAroundPartyBehavior', 'GetDefendingPosition', 'GetFleeBehavior',
        'GetInSettlementBehavior', 'GetNearbyPartyToFlee', 'GetBehaviorForNearbySettlementToFlee',
        'SetAiBehavior', 'UpdateBehavior']))
    return code.replace('PARTY_METHODS', '\n'.join(extract(party_source, name) for name in [
        'SetNavigationModeParty', 'SetNavigationModePoint', 'CheckExitingSettlementParallel'])) + body + SCENARIOS


class ResidentCounterattackTests(unittest.TestCase):
    def test_resident_decides_and_requests_exit_without_manual_departure(self):
        def decompile(name):
            return subprocess.check_output(['ilspycmd', '-r', str(GAME_BIN), '-t', name,
                str(GAME_BIN / 'TaleWorlds.CampaignSystem.dll')], text=True, encoding='utf-8')
        code = build_code(decompile('TaleWorlds.CampaignSystem.Party.MobilePartyAi'),
                          decompile('TaleWorlds.CampaignSystem.Party.MobileParty'),
                          decompile('TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyAIModel'))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-resident-coupled-') as directory:
            path = Path(directory)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            diagnostics = '\n'.join(line for line in result.stdout.splitlines() if ': warning ' not in line)
            self.assertEqual(0, result.returncode, diagnostics + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('RESIDENT_')))


if __name__ == '__main__':
    unittest.main()
