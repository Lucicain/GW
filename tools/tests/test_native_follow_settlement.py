"""Installed vanilla follow and war-at-settlement decision bodies.

Enter/leave/prisoner engine actions and campaign objects are controlled. This
tests managed decisions, not the full engine or player settlement menus.
"""
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_battle_peace import extract

GAME_BIN = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')

HARNESS = r'''
using System;using System.Collections.Generic;
namespace Audit {
enum AiBehavior {None,EscortParty,GoToSettlement,GoToPoint,Hold}
class CampaignVec2 {public float X;}
interface IFaction {List<Settlement> Settlements{get;}bool IsAtWarWith(IFaction f);}
class Faction:IFaction {public List<Settlement> Settlements{get;}=new();public HashSet<IFaction> Enemies=new();public bool IsAtWarWith(IFaction f)=>Enemies.Contains(f);}
class Hero {public enum CharacterStates{Active,Prisoner,Dead}public static Hero MainHero=new();public bool IsWanderer,IsNotable;
 public CharacterStates HeroState;public IFaction? MapFaction;public Settlement? CurrentSettlement;}
class Army {public MobileParty LeaderParty=null!;public List<MobileParty> Parties=new();}
class MobileParty {public enum NavigationType{Default,All}public static MobileParty MainParty=new();public bool IsActive=true,IsTargetingPort,IsCurrentlyAtSea,HasNavalNavigationCapability,IsTransitionInProgress,HasLandNavigationCapability=true,IsLordParty=true;
 public CampaignVec2 Position=new(),MoveTargetPoint=new(),EndPositionForNavigationTransition=new();public Settlement? CurrentSettlement;public Hero LeaderHero=new();
 public Army? Army;public object? MapEvent;public NavigationType NavigationCapability=NavigationType.Default;}
class Settlement {public bool IsFortification=true;public CampaignVec2 GatePosition=new(){X=10},PortPosition=new(){X=20};
 public IFaction MapFaction=null!;public object Party=new();public List<Hero> HeroesWithoutParty=new();public List<MobileParty> Parties=new();}
static class Campaign {public const float MapDiagonal=1000;}
static class DistanceHelper {public static float FindClosestDistanceFromMobilePartyToPoint(MobileParty p,CampaignVec2 v,MobileParty.NavigationType n)=>0;}
static class DeclareWarAction {public enum DeclareWarDetail{Default}}
static class LeaveSettlementAction {public static List<MobileParty> Left=new();public static void ApplyForParty(MobileParty p){Left.Add(p);p.CurrentSettlement?.Parties.Remove(p);p.CurrentSettlement=null;p.LeaderHero.CurrentSettlement=null;}}
static class TakePrisonerAction {public static List<Hero> Taken=new();public static void Apply(object holder,Hero h){Taken.Add(h);h.HeroState=Hero.CharacterStates.Prisoner;}}
class NativeFollow {private MobileParty _mobileParty;public NativeFollow(MobileParty p){_mobileParty=p;}
 FOLLOW_METHOD
 public (AiBehavior,Settlement?,float) Run(MobileParty t){AiBehavior b=AiBehavior.None;Settlement s=null!;GetFollowBehavior(ref b,ref s,t,out var v);return(b,s,v.X);}
}
class NativeWar {
 WAR_METHODS
 public void Run(IFaction a,IFaction b)=>OnWarDeclared(a,b,DeclareWarAction.DeclareWarDetail.Default);
}
static class Replay {
 static int Checks;static void Check(bool b,string s){if(!b)throw new Exception(s);Checks++;}
 static MobileParty InTown(Settlement town,IFaction faction){var p=new MobileParty{CurrentSettlement=town};p.LeaderHero.MapFaction=faction;p.LeaderHero.CurrentSettlement=town;town.Parties.Add(p);return p;}
 static (Faction,Faction,Settlement,MobileParty) Setup(){
  LeaveSettlementAction.Left.Clear();TakePrisonerAction.Taken.Clear();var a=new Faction();var b=new Faction();a.Enemies.Add(b);b.Enemies.Add(a);
  var town=new Settlement{MapFaction=b};b.Settlements.Add(town);return(a,b,town,InTown(town,a));
 }
 static void Main(){
  var follower=new MobileParty();var target=new MobileParty();target.Position.X=44;var n=new NativeFollow(follower);
  Check(n.Run(target)==(AiBehavior.GoToPoint,null,44f),"field follow updates live target point");
  var town=new Settlement();target.CurrentSettlement=town;
  Check(n.Run(target)==(AiBehavior.GoToSettlement,town,10f),"AI follow of town resident becomes GoToSettlement at gate");
  follower.IsTargetingPort=true;Check(n.Run(target)==(AiBehavior.GoToSettlement,town,20f),"port following uses town port");
  target.CurrentSettlement=null;target.Position.X=99;Check(n.Run(target)==(AiBehavior.GoToPoint,null,99f),"follow resumes live target after departure");
  target.IsActive=false;Check(n.Run(target).Item1==AiBehavior.Hold,"invalid followed party holds");
  var(a,b,fort,warden)=Setup();var war=new NativeWar();war.Run(a,b);
  Check(LeaveSettlementAction.Left.Contains(warden)&&warden.CurrentSettlement==null,"armed enemy lord in belligerent town is expelled on declaration");
  Check(TakePrisonerAction.Taken.Count==0,"lord with party not directly captured by this war callback");
  (a,b,fort,warden)=Setup();var loose=new Hero{MapFaction=a,CurrentSettlement=fort};fort.HeroesWithoutParty.Add(loose);war.Run(a,b);
  Check(TakePrisonerAction.Taken.Contains(loose),"eligible hostile hero without party is captured separately");
  Check(!TakePrisonerAction.Taken.Contains(warden.LeaderHero)&&LeaveSettlementAction.Left.Contains(warden),"same town handles loose hero and armed lord differently");
  (a,b,fort,warden)=Setup();a.Enemies.Clear();b.Enemies.Clear();war.Run(a,b);
  Check(LeaveSettlementAction.Left.Count==0,"callback does not expel non-hostile resident");
  (a,b,fort,warden)=Setup();var third=new Faction();fort.MapFaction=third;b.Settlements.Clear();third.Settlements.Add(fort);war.Run(a,b);
  Check(LeaveSettlementAction.Left.Count==0,"neutral third faction town not traversed by this declaration callback");
  (a,b,fort,warden)=Setup();fort.IsFortification=false;war.Run(a,b);Check(LeaveSettlementAction.Left.Count==0,"village not treated as fortification captivity callback");
  Console.WriteLine("NATIVE_FOLLOW_SETTLEMENT_ASSERTIONS="+Checks+"; PASS; real managed methods, controlled campaign actions");
 }
}
}
'''


class NativeFollowSettlementTests(unittest.TestCase):
    def test_follow_and_war_settlement_lifecycle(self):
        def decompile(name):
            return subprocess.check_output(['ilspycmd', '-r', str(GAME_BIN), '-t', name,
                str(GAME_BIN / 'TaleWorlds.CampaignSystem.dll')], text=True, encoding='utf-8')
        follow = decompile('TaleWorlds.CampaignSystem.Party.MobilePartyAi')
        war = decompile('TaleWorlds.CampaignSystem.CampaignBehaviors.PrisonerCaptureCampaignBehavior')
        code = HARNESS.replace('FOLLOW_METHOD', extract(follow, 'GetFollowBehavior'))
        code = code.replace('WAR_METHODS', '\n'.join(extract(war, n) for n in
            ['OnWarDeclared', 'HandleSettlementHeroes', 'SettlementHeroCaptureCommonCondition']))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-native-follow-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            diagnostics = '\n'.join(line for line in result.stdout.splitlines() if ': warning ' not in line)
            self.assertEqual(0, result.returncode, diagnostics + result.stderr)
            print('\n'.join(line for line in result.stdout.splitlines() if line.startswith('NATIVE_FOLLOW_')))


if __name__ == '__main__':
    unittest.main()
