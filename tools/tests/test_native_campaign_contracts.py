"""Run methods freshly decompiled from the installed game against boundary inputs.

Navigation success/distances, faction relations and object state are fixtures.
The tested decision bodies are native-game managed C#, not reimplemented rules.
No native engine, game launch or real map pathfinding is exercised.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

GAME_DLL = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace Audit {
enum AiBehavior {None,GoToPoint,EngageParty,EscortParty}
enum BattleSideEnum {Attacker,Defender}
class CampaignVec2 {public float X;public bool IsOnLand=true;public float DistanceSquared(CampaignVec2 p)=>(X-p.X)*(X-p.X);}
class Faction {public HashSet<Faction> Enemies=new();public bool IsAtWarWith(Faction f)=>Enemies.Contains(f);}
class PartyBase {public bool IsActive=true;public Faction MapFaction=new();}
class MapEventParty {public PartyBase Party=new();}
class Side {public List<MapEventParty> Parties=new();}
class NativeMapEvent {public Side Attack=new(),Defend=new();private Side GetMapEventSide(BattleSideEnum s)=>s==BattleSideEnum.Attacker?Attack:Defend;
 private BattleSideEnum GetOtherSide(BattleSideEnum s)=>s==BattleSideEnum.Attacker?BattleSideEnum.Defender:BattleSideEnum.Attacker;
 JOIN_METHOD
}
class SiegeEvent {public bool IsBlockadeActive;}
class Settlement {public bool HasPort,IsFortification;public SiegeEvent? SiegeEvent;public bool IsUnderSiege=>SiegeEvent!=null;
 public float LandDistance=120,PortDistance=60;public bool LandReachable=true,PortReachable=true;}
class Army {public MobileParty LeaderParty=null!;}
class MobileParty {public enum NavigationType{None,Default,Naval,All}
 public CampaignVec2 Position=new(),AiBehaviorTarget=new();public float _lastCalculatedSpeed=4,PartySizeRatio=1,FoodDays=20,PathDistance=30;
 public bool HasNavalNavigationCapability,IsMainParty,Reachable=true;public Army? Army;public MobileParty? AttachedTo;public Settlement? CurrentSettlement;
 public NavigationType NavigationCapability=NavigationType.Default;public int GetNumDaysForFoodToLast()=>(int)FoodDays;}
class ArmyModel {public float PlayerMobilePartySizeRatioToCallToArmy=.4f,AIMobilePartySizeRatioToCallToArmy=.4f,MinimumNeededFoodInDaysToCallToArmy=2;}
class Encounter {public float GetEncounterJoiningRadius=3;}
class Models {public Encounter EncounterModel=new();public ArmyModel ArmyManagementCalculationModel=new();}
class Campaign {public static Campaign Current=new();public Models Models=new();public float EstimatedAverageLordPartySpeed=5;
 public float GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType n)=>20;}
static class CampaignTime {public const int HoursInDay=24;}
static class MBRandom {public static float RandomFloat=>1;}
static class MathF {public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);}
static class AiHelper {
 public static void GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(MobileParty p,Settlement s,bool isTargetingPort,
  out MobileParty.NavigationType n,out float d,out bool from){n=(isTargetingPort?s.PortReachable:s.LandReachable)?(isTargetingPort?MobileParty.NavigationType.Naval:MobileParty.NavigationType.Default):MobileParty.NavigationType.None;d=n==MobileParty.NavigationType.None?float.MaxValue:isTargetingPort?s.PortDistance:s.LandDistance;from=isTargetingPort;}
 public static void GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(MobileParty p,MobileParty t,out MobileParty.NavigationType n,out float d){n=p.Reachable?MobileParty.NavigationType.Default:MobileParty.NavigationType.None;d=p.PathDistance;}
}
struct AIBehaviorData {public object Target;public AiBehavior Behavior;public AIBehaviorData(object t,AiBehavior b,MobileParty.NavigationType n,bool willGatherArmy,bool isFromPort,bool isTargetingPort){Target=t;Behavior=b;}}
class PartyThinkParams {public List<(AIBehaviorData,float)> Scores=new();public void AddBehaviorScore((AIBehaviorData,float) score)=>Scores.Add(score);}
class NativePartyAi {private MobileParty _mobileParty;public bool DefendingAvailable=true;public CampaignVec2 DefendingPoint=new(){X=77};
 public NativePartyAi(MobileParty p){_mobileParty=p;}private bool GetDefendingPosition(CampaignVec2 p,MobileParty.NavigationType n,float r,out CampaignVec2 point){point=DefendingPoint;return DefendingAvailable;}
 AROUND_METHOD
 public (AiBehavior,float) Run(MobileParty t){GetGoAroundPartyBehavior(t,out var b,out var p);return(b,p.X);}
}
class NativeVisit {
 VISIT_METHOD
 public static (MobileParty.NavigationType,float,bool) Run(MobileParty p,Settlement s){GetBestNavigationDataForVisitingSettlement(p,s,out var n,out var days,out _,out var port);return(n,days,port);}
}
class NativeArmyAi {
 private float FollowingArmyLeaderMaxScore=>20f;private float FollowingArmyLeaderMinScore=>FollowingArmyLeaderMaxScore*.5f;private float ArmyLeaderIsUnreachableScore=>.02475f;
 ARMY_METHOD
}
static class Replay {static int Checks;static void Check(bool x,string name){if(!x)throw new Exception(name);Checks++;}
 static void Main(){
  var p=new MobileParty();var target=new MobileParty{_lastCalculatedSpeed=8};var ai=new NativePartyAi(p);
  Check(ai.Run(target)==(AiBehavior.GoToPoint,77f),"faster target with defend point uses ring point, not direct engage");
  ai.DefendingAvailable=false;Check(ai.Run(target).Item1==AiBehavior.EngageParty,"no defend point falls back to engage");
  ai.DefendingAvailable=true;target._lastCalculatedSpeed=2;p.AiBehaviorTarget.X=77;Check(ai.Run(target).Item1==AiBehavior.GoToPoint,"slower target still uses available ring point");
  var town=new Settlement();var nav=NativeVisit.Run(p,town);Check(nav.Item1==MobileParty.NavigationType.Default&&Math.Abs(nav.Item2-1)<.0001,"120 path units at average5 yields one day");
  p._lastCalculatedSpeed=1;Check(NativeVisit.Run(p,town).Item2==nav.Item2,"vanilla visit normalization uses average lord speed, not this party speed");
  town.LandDistance=240;Check(NativeVisit.Run(p,town).Item2==2,"navigation path distance changes visit travel days");
  p.HasNavalNavigationCapability=true;town.HasPort=true;town.IsFortification=true;
  nav=NativeVisit.Run(p,town);Check(nav.Item1==MobileParty.NavigationType.Naval&&nav.Item2==.5f&&nav.Item3,"shorter port route selected");
  town.PortDistance=360;nav=NativeVisit.Run(p,town);Check(nav.Item1==MobileParty.NavigationType.Default&&nav.Item2==2&&!nav.Item3,"longer port route does not replace land route");
  town.SiegeEvent=new SiegeEvent();nav=NativeVisit.Run(p,town);Check(nav.Item1==MobileParty.NavigationType.Naval&&nav.Item2==3,"unblockaded siege removes land option for naval capable party");
  town.SiegeEvent=null;town.LandReachable=false;town.PortReachable=false;Check(NativeVisit.Run(p,town).Item1==MobileParty.NavigationType.None,"unreachable destination remains invalid");
  var battle=new NativeMapEvent();var attackers=new Faction();var defenders=new Faction();attackers.Enemies.Add(defenders);defenders.Enemies.Add(attackers);
  battle.Attack.Parties.Add(new(){Party=new(){MapFaction=attackers}});battle.Defend.Parties.Add(new(){Party=new(){MapFaction=defenders}});
  Check(battle.CanPartyJoinBattle(new(){MapFaction=defenders},BattleSideEnum.Defender),"same faction joins defender against its enemy");
  var neutral=new Faction();Check(!battle.CanPartyJoinBattle(new(){MapFaction=neutral},BattleSideEnum.Defender),"neutral to opposite side cannot join just because friendly");
  attackers.Enemies.Add(neutral);neutral.Enemies.Add(attackers);
  Check(battle.CanPartyJoinBattle(new(){MapFaction=neutral},BattleSideEnum.Defender),"different faction may join if at war with all opposite parties");
  battle.Attack.Parties.Add(new(){Party=new(){MapFaction=new Faction()}});
  Check(!battle.CanPartyJoinBattle(new(){MapFaction=neutral},BattleSideEnum.Defender),"one neutral party on opposite side prevents join");
  var chief=new MobileParty();p=new MobileParty{Army=new Army{LeaderParty=chief}};var armyAi=new NativeArmyAi();var think=new PartyThinkParams();armyAi.AiHourlyTick(p,think);
  Check(think.Scores.Count==1&&think.Scores[0].Item2==20,"healthy in-transit army member receives native score20");
  p.FoodDays=1;think=new();armyAi.AiHourlyTick(p,think);Check(think.Scores[0].Item2==10,"far hungry member receives native score10");
  p.Reachable=false;think=new();armyAi.AiHourlyTick(p,think);Check(think.Scores[0].Item2==.02475f,"unreachable chief receives fallback score");
  p.Army=null;think=new();armyAi.AiHourlyTick(p,think);Check(think.Scores.Count==0,"non-army party receives no native army-follow candidate");
  Console.WriteLine("NATIVE_CAMPAIGN_CONTRACT_ASSERTIONS="+Checks+"; PASS; managed decision methods with controlled engine boundaries");
 }
}
}
'''


class NativeCampaignContracts(unittest.TestCase):
    def test_installed_native_managed_decisions(self):
        names = {
            'AROUND_METHOD': ('TaleWorlds.CampaignSystem.Party.MobilePartyAi', 'GetGoAroundPartyBehavior'),
            'VISIT_METHOD': ('TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior', 'GetBestNavigationDataForVisitingSettlement'),
            'JOIN_METHOD': ('TaleWorlds.CampaignSystem.MapEvents.MapEvent', 'CanPartyJoinBattle'),
            'ARMY_METHOD': ('TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiArmyMemberBehavior', 'AiHourlyTick'),
        }
        code = HARNESS
        for marker, (type_name, method) in names.items():
            source = subprocess.check_output(['ilspycmd', '-t', type_name, str(GAME_DLL)], text=True, encoding='utf-8-sig')
            code = code.replace(marker, extract(source, method))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-native-contracts-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('NATIVE_CAMPAIGN_CONTRACT_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
