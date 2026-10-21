"""City search, stage 1: sheltered offenders, the player's offer pool, bounty state and guard persuasion exits.

Production members are extracted from source and run against minimal campaign stubs.
No game is launched; the town scene, guards and the fight are verified by in-game diagnostics.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_duty_banner import method

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'


def read(name):
    return (MODULE / name).read_text(encoding='utf-8-sig')


def member(source, pattern):
    match = re.search(r'(?ms)^\s*(?:private|internal|public)[^\n]*\b' + pattern + r'\b[^\n]*=>.*?;\s*$', source)
    if not match:
        raise AssertionError('Missing production member: ' + pattern)
    return match.group(0)


HARNESS = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace GreyWardenPolicePurity {
class Settlement{public string Id;public bool IsFortification=true;public List<Hero> HeroesWithoutParty=new();public Settlement(string id){Id=id;}}
class MobileParty{public bool IsActive=true;}
class Hero{public string StringId="lord";public bool IsAlive=true,IsPrisoner,IsDisabled,IsFugitive,IsDead;public MobileParty? PartyBelongedTo;public Settlement? CurrentSettlement;}
static class CrimePool{public const string PlayerCrimeId="player";}
static class PoliceEnforcementBehavior{public static bool Agreement;public static bool HasEnvoyAgreement(CrimeRecord c)=>Agreement;}
static class GwpPlayerCaseAssignment{public static bool Held;public static bool IsCaseHeldByPlayer(string? id)=>Held;
 CAN_TAKE CAN_PLAYER_TAKE}
partial class CrimeRecord{public bool HasOpenCase=true;public string CrimeId="crime";public Hero? OffenderHero;public string OffenderHeroId="lord";public bool Pursuable;public bool IsOffenderPursuable()=>Pursuable;
 SHELTER_OF SHELTER_SETTLEMENT SEARCHABLE}
enum PlayerBountyFlowState{Idle,HuntingTarget,WaitingForCollection}
class Bounty{public string _activeBountyTargetId=null!,_activeBountyTargetHeroId=null!,_activeBountyTargetName=null!,_activeBountyTargetFactionId=null!;public bool _waitingForCollection;public int _activeBountyCrimeCategory;
 public PlayerBountyFlowState Old=>!string.IsNullOrEmpty(_activeBountyTargetId)?PlayerBountyFlowState.HuntingTarget:(_waitingForCollection?PlayerBountyFlowState.WaitingForCollection:PlayerBountyFlowState.Idle);
 public Hero? CaseHero;
 CURRENT_STATE
 public bool IsTrackingBountyTarget=>CurrentBountyState==PlayerBountyFlowState.HuntingTarget;
 ENTER_COLLECTION CLEAR_TARGET MISSING CITY_TARGET}
static class GwpCrimeCategory{public const int Unknown=0;}
enum PersuasionOptionResult{CriticalFailure,Failure,Success,CriticalSuccess,Miss}
class TextObject{public TextObject(string s){}}static class MBTextManager{public static void SetTextVariable(string k,object v){}}
class PersuasionOptionArgs{public bool IsBlocked,CanMoveToTheNextReservation=true;public TextObject Line=new("line");}
class PersuasionTask{public TextObject SpokenLine=new("spoken");public List<PersuasionOptionArgs> Options=new();public void BlockAllOptions(){foreach(var o in Options)o.IsBlocked=true;}}
static class ConversationManager{public static bool Active,Failure,Satisfied;public static bool GetPersuasionIsActive()=>Active;public static bool GetPersuasionIsFailure()=>Failure;public static bool GetPersuasionProgressSatisfied()=>Satisfied;}
class Round{const int Arguments=4;string _variablePrefix="GWP_CS_GUARD";public List<PersuasionTask> _tasks=new();public PersuasionTask? _lastTask;public PersuasionOptionArgs? _pending;public PersuasionOptionResult _lastResult;
 IS_SATISFIED IS_FAILED CURRENT ON_COMMITTED PREPARE_RESERVATION
 public void Pick(int i){_lastTask=Current;_pending=Current!.Options[i];}
 public string Exit()=>IsSatisfied?"pass":PrepareReservation()?"reservation":IsFailed?"refuse":"stuck";}
static class Program{static int n;static void Check(bool c,string m){n++;if(!c)throw new Exception(m);}
 static int Main(){
  var town=new Settlement("town");var village=new Settlement("village"){IsFortification=false};
  Hero Lord(Settlement? s,bool listed=true){var h=new Hero{CurrentSettlement=s};if(listed&&s!=null)s.HeroesWithoutParty.Add(h);return h;}
  Check(CrimeRecord.ShelterOf(Lord(town))==town,"partyless lord in town is sheltered");
  Check(CrimeRecord.ShelterOf(Lord(new Settlement("castle")))!=null,"castles count as fortifications");
  var party=Lord(town);party.PartyBelongedTo=new MobileParty();Check(CrimeRecord.ShelterOf(party)==null,"party leader is pursued in the field");
  foreach(var setup in new Action<Hero>[]{h=>h.IsPrisoner=true,h=>h.IsFugitive=true,h=>h.IsDisabled=true,h=>h.IsAlive=false}){var h=Lord(town);setup(h);Check(CrimeRecord.ShelterOf(h)==null,"prisoner/fugitive/disabled/dead are not sheltered");}
  Check(CrimeRecord.ShelterOf(Lord(village))==null,"villages are not searched");
  Check(CrimeRecord.ShelterOf(Lord(town,listed:false))==null,"only heroes without party in that settlement");
  Check(CrimeRecord.ShelterOf(null)==null,"no hero");
  var crime=new CrimeRecord{OffenderHero=Lord(town)};
  Check(crime.IsOffenderSearchableInSettlement(),"sheltered open case is searchable");
  Check(GwpPlayerCaseAssignment.CanPlayerTakeCase(crime),"player may take a sheltered case");
  Check(!GwpPlayerCaseAssignment.CanTakeCase(crime),"NPC pool does not take sheltered cases yet");
  GwpPlayerCaseAssignment.Held=true;Check(!crime.IsOffenderSearchableInSettlement(),"case held by the player is not offered");GwpPlayerCaseAssignment.Held=false;
  PoliceEnforcementBehavior.Agreement=true;Check(!GwpPlayerCaseAssignment.CanPlayerTakeCase(crime),"envoy agreement blocks the offer");PoliceEnforcementBehavior.Agreement=false;
  Check(!new CrimeRecord{OffenderHero=Lord(town),CrimeId=CrimePool.PlayerCrimeId}.IsOffenderSearchableInSettlement(),"player's own case is not searched");
  Check(!new CrimeRecord{OffenderHero=Lord(town),HasOpenCase=false}.IsOffenderSearchableInSettlement(),"closed case");
  var field=new CrimeRecord{OffenderHero=party,Pursuable=true};Check(GwpPlayerCaseAssignment.CanPlayerTakeCase(field)&&GwpPlayerCaseAssignment.CanTakeCase(field),"field cases unchanged");
  // Every bounty state the old flows can produce: party id and waiting never both set.
  foreach(string? partyId in new[]{null,"party"})foreach(string? heroId in new[]{null,"lord"})foreach(bool waiting in new[]{false,true}){
   if(partyId!=null&&waiting)continue; if(partyId==null&&heroId!=null&&!waiting)continue;
   var b=new Bounty{_activeBountyTargetId=partyId!,_activeBountyTargetHeroId=heroId!,_waitingForCollection=waiting};
   Check(b.CurrentBountyState==b.Old,"state unchanged for "+partyId+"/"+heroId+"/"+waiting);}
  var s=new Bounty{_activeBountyTargetHeroId="lord"};Check(s.CurrentBountyState==PlayerBountyFlowState.HuntingTarget,"sheltered target is being hunted without a party id");
  s.EnterBountyCollectionState();Check(s.CurrentBountyState==PlayerBountyFlowState.WaitingForCollection,"capture moves to collection");
  s._waitingForCollection=false;s.ClearActiveBountyTarget();Check(s.CurrentBountyState==PlayerBountyFlowState.Idle,"cleared task is idle");
  var m=new Bounty{_activeBountyTargetHeroId="lord",CaseHero=Lord(town)};
  Check(!m.IsCaseTargetMissing,"sheltered target is not missing");
  Check(m.CitySearchTargetIn(town)==m.CaseHero&&m.CitySearchTargetIn(village)==null&&m.CitySearchTargetIn(null)==null,"search only in the target's settlement");
  var gone=new Bounty{_activeBountyTargetHeroId="lord",CaseHero=new Hero()};Check(gone.IsCaseTargetMissing&&gone.CitySearchTargetIn(town)==null,"partyless hero outside a settlement is missing");
  var held=new Bounty{_activeBountyTargetHeroId="lord",_waitingForCollection=true,CaseHero=Lord(town)};Check(held.CitySearchTargetIn(town)==null,"no search after the case is done");
  // Guard persuasion: the dialogue always has an exit.
  Round R(int options){var r=new Round();var t=new PersuasionTask();for(int i=0;i<options;i++)t.Options.Add(new PersuasionOptionArgs());r._tasks.Add(t);ConversationManager.Active=true;ConversationManager.Failure=false;ConversationManager.Satisfied=false;return r;}
  var ok=R(4);Check(ok.Exit()=="reservation","first the guard's reservation");ok.Pick(0);ok.OnCommitted(Tuple.Create(ok._pending!,PersuasionOptionResult.Success));ConversationManager.Satisfied=true;Check(ok.Exit()=="pass","success lets the player through");
  var retry=R(4);retry.Pick(1);var used=retry._pending!;retry.OnCommitted(Tuple.Create(used,PersuasionOptionResult.Failure));used.IsBlocked=true;Check(retry.Exit()=="reservation","a failed argument leaves the others");
  foreach(var o in retry._tasks[0].Options)o.IsBlocked=true;Check(retry.Exit()=="refuse","all arguments spent is a refusal");
  var crit=R(4);crit.Pick(2);crit.OnCommitted(Tuple.Create(crit._pending!,PersuasionOptionResult.CriticalFailure));Check(crit.Exit()=="refuse","critical failure ends it");
  var stale=R(4);stale.Pick(0);stale.OnCommitted(Tuple.Create(new PersuasionOptionArgs(),PersuasionOptionResult.CriticalFailure));Check(stale.Exit()=="reservation","another round's commit is ignored");
  var idle=R(4);ConversationManager.Active=false;Check(idle.Exit()=="refuse","inactive persuasion never hangs");
  var failed=R(4);ConversationManager.Failure=true;Check(failed.Exit()=="refuse","native failure is a refusal even with arguments left");
  Console.WriteLine("CITY_SEARCH_ASSERTIONS="+n);return 0;}}
}
'''


class CitySearchTests(unittest.TestCase):
    def test_sheltered_offers_bounty_state_and_guard_exits(self):
        data = read('GwpData.cs')
        assignment = read('GwpPlayerCaseAssignment.cs')
        bounty = read('PlayerBountyBehavior.cs')
        custody = read('PlayerBountyBehavior.Custody.cs')
        search = read('PlayerBountyBehavior.CitySearch.cs')
        persuasion = read('GwpPersuasionRound.cs')
        state = re.search(r'(?ms)^\s*private PlayerBountyFlowState CurrentBountyState =>.*?;\s*$', bounty).group(0)
        code = (HARNESS
                .replace('CAN_TAKE', method(assignment, 'CanTakeCase'))
                .replace('CAN_PLAYER_TAKE', method(assignment, 'CanPlayerTakeCase'))
                .replace('SHELTER_OF', method(data, 'ShelterOf'))
                .replace('SHELTER_SETTLEMENT', member(data, 'ShelterSettlement'))
                .replace('SEARCHABLE', method(data, 'IsOffenderSearchableInSettlement'))
                .replace('CURRENT_STATE', state.replace('private ', 'public '))
                .replace('ENTER_COLLECTION', method(bounty, 'EnterBountyCollectionState').replace('private ', 'public '))
                .replace('CLEAR_TARGET', method(bounty, 'ClearActiveBountyTarget').replace('private ', 'public '))
                .replace('MISSING', member(custody, 'IsCaseTargetMissing').replace('private ', 'public '))
                .replace('CITY_TARGET', method(search, 'CitySearchTargetIn'))
                .replace('IS_SATISFIED', member(persuasion, 'IsSatisfied'))
                .replace('IS_FAILED', member(persuasion, 'IsFailed'))
                .replace('CURRENT', member(persuasion, 'Current').replace('private ', 'public '))
                .replace('ON_COMMITTED', method(persuasion, 'OnCommitted'))
                .replace('PREPARE_RESERVATION', method(persuasion, 'PrepareReservation')))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-city-search-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><NoWarn>CS8618;CS0649;CS0414</NoWarn><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            r = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, r.returncode, r.stdout + r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('CITY_SEARCH_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
