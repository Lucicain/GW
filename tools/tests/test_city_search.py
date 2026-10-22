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

    def test_spawned_troops_join_the_player_team(self):
        """Negative control: the installed GetAgentTeam puts SimpleAgentOrigin soldiers in the ally team
        whenever an ally team exists (the town mission has one); a main-party PartyAgentOrigin goes to the player team."""
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')

        def native(type_name, dll):
            return subprocess.check_output(['ilspycmd', '-t', type_name, str(game / dll)], text=True, encoding='utf-8-sig')

        def prop(source, name):
            start = re.search(r'(?m)^\s*public [^\n]*\b' + name + r'\s*$', source)
            if not start:
                raise AssertionError('Missing native property: ' + name)
            depth, i = 0, source.index('{', start.end())
            for j in range(i, len(source)):
                depth += {'{': 1, '}': -1}.get(source[j], 0)
                if depth == 0:
                    return source[start.start():j + 1]
            raise AssertionError(name)

        mission = native('TaleWorlds.MountAndBlade.Mission', 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')
        simple = native('TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin', 'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')
        party = native('TaleWorlds.CampaignSystem.AgentOrigins.PartyAgentOrigin', 'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')
        production = read('GwpCitySearchMission.cs')
        self.assertIn('new PartyAgentOrigin(PartyBase.MainParty, character)', production)
        self.assertNotIn('new SimpleAgentOrigin(', production)
        code = r'''
using System;
namespace TaleWorlds.Library{static class Debug{public static void FailedAssert(string a,string b,string c,int d){}}}
namespace Probe{
class Hero{public static Hero MainHero=new();public MobileParty? PartyBelongedTo;}
class Faction{public Hero? Leader;}
class PartyBase{public static PartyBase MainParty=new();public Hero? Owner;public Faction MapFaction=new();}
class MobileParty{public PartyBase Party=new();}
class CharacterObject{public bool IsHero;public Hero? HeroObject;}
class Team{public string Name;public Team(string n){Name=n;}}
interface IAgentOriginBase{bool IsUnderPlayersCommand{get;}bool IsInSameArmyAsPlayer{get;}}
class Simple:IAgentOriginBase{CharacterObject _troop;public Simple(CharacterObject c){_troop=c;}public bool IsInSameArmyAsPlayer=>false; SIMPLE_PARTY SIMPLE_COMMAND}
class FromParty:IAgentOriginBase{public PartyBase Party;public FromParty(PartyBase p){this.Party=p;}public bool IsInSameArmyAsPlayer=>false; PARTY_COMMAND}
class Mission{public static Mission? Current;public Team PlayerTeam=new("player"),PlayerEnemyTeam=new("enemy");public Team? PlayerAllyTeam=new("ally");
 GET_AGENT_TEAM}
static class Program{static int Main(){
 Mission.Current=new Mission();var soldier=new CharacterObject();
 if(Mission.GetAgentTeam(new Simple(soldier),true).Name!="ally")throw new Exception("old origin no longer reproduces the ally-team split");
 if(Mission.GetAgentTeam(new FromParty(PartyBase.MainParty),true).Name!="player")throw new Exception("main-party origin must join the player team");
 Mission.Current.PlayerAllyTeam=null;
 if(Mission.GetAgentTeam(new Simple(soldier),true).Name!="player")throw new Exception("without an ally team every player-side soldier joins the player team");
 Console.WriteLine("TEAM_ASSIGNMENT_ASSERTIONS=3; old SimpleAgentOrigin -> ally team, main-party PartyAgentOrigin -> player team");return 0;}}
}
'''
        code = (code.replace('SIMPLE_PARTY', prop(simple, 'Party'))
                .replace('SIMPLE_COMMAND', prop(simple, 'IsUnderPlayersCommand'))
                .replace('PARTY_COMMAND', prop(party, 'IsUnderPlayersCommand'))
                .replace('GET_AGENT_TEAM', method(mission, 'GetAgentTeam')))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-city-team-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><NoWarn>CS8618;CS0649;CS8602;CS8603</NoWarn><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            r = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, r.returncode, r.stdout + r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('TEAM_ASSIGNMENT_ASSERTIONS=')))

    def test_defying_the_watch_starts_the_fight(self):
        """Negative control: the installed hit rule cancels every blow on a neutral human, so the old
        OnAgentHit trigger could never fire before the fight; the collision-callback note does.
        After the guard refuses, the player either fights or withdraws; walking back in after promising to
        leave earns one warning, after choosing to withdraw none. Guard odds: skill and trait matter,
        critical failure has a floor, the four outcomes add up to one."""
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')
        mission = subprocess.check_output(['ilspycmd', '-t', 'TaleWorlds.MountAndBlade.Mission',
                                           str(game / 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')],
                                          text=True, encoding='utf-8-sig')
        # Harmony binds prefix arguments by name.
        self.assertRegex(mission, r'MeleeHitCallback\([^)]*\bAgent attacker, Agent victim\b')
        production = read('GwpCitySearchMission.cs')
        behavior = read('GwpCitySearchBehavior.cs')
        tuning = read('GwpTuning.cs')
        self.assertNotIn('override void OnAgentHit', production)
        self.assertIn('[HarmonyPatch(typeof(Mission), "MeleeHitCallback")]', production)
        # troop-combat.md: patching MissileHitCallback lost battle shouts (2026-09-03).
        self.assertNotIn('"MissileHitCallback"', production)
        city_tuning = block(tuning, r'internal static class CitySearch\b')
        code = r'''
using System;using System.Collections.Generic;
namespace Probe{
enum MissionMode{StartUp,Battle,Duel}
enum AgentControllerType{None,AI,Player}
static class GameNetwork{public static bool IsSessionActive;}
static class MultiplayerOptions{public enum OptionType{FriendlyFireDamageMeleeFriendPercent,FriendlyFireDamageMeleeSelfPercent}}
static class OptionValues{public static int GetIntValue(this MultiplayerOptions.OptionType t)=>0;}
struct Vec3{public float X;public Vec3(float x){X=x;}public float Distance(Vec3 o)=>Math.Abs(X-o.X);public static Vec3 Zero=>new(0);}
class Agent{public static Agent? Main;public string Team;public Vec3 Position;public bool IsHuman=true;public AgentControllerType Controller=AgentControllerType.Player;public Agent(string t){Team=t;}
 public bool IsFriendOf(Agent o)=>Team==o.Team;
 public bool IsEnemyOf(Agent o)=>Team=="player"&&o.Team=="enemy"||Team=="enemy"&&o.Team=="player";}
class Mission{public bool ForceNoFriendlyFire;public MissionMode Mode=MissionMode.StartUp;public bool Cancels(Agent a,Agent v)=>CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase(a,v);
 CANCEL}
class ConversationFlow{public bool IsConversationFlowActive;}
class Campaign{public static Campaign Current=new();public ConversationFlow ConversationManager=new();}
static class GwpAiDiagnostics{public static void WriteCitySearch(string stage,string details){}}
static class GwpTextCatalog{public const string gwp_cs_leave_notice="leave",gwp_cs_withdraw_notice="withdraw",gwp_cs_leave_warning="warning";}
static class GwpText{public static string Get(string k)=>k;public static string Create(string k)=>k;}
class InformationMessage{public InformationMessage(string s){}}
static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
static class MBInformationManager{public static int Warnings;public static void AddQuickInformation(string s){Warnings++;}}
static class GwpTuning{CITY_TUNING}
enum Stage{Entering,Questioned,Allowed,Leaving,Refused,Withdrawing,Fighting,FightOver}
enum GwpCitySearchGate{Unchecked,Allowed,Leaving,Refused,Withdrawing,Cleared}
class SearchState{public GwpCitySearchGate GateState;}
class Controller{public SearchState Search=new();public Stage _stage;public string? _pendingFight;public Vec3 _gate;public float _inwardLimit;public bool _warned;
 public HashSet<Agent> Ours=new(),Defenders=new();bool IsOurs(Agent a)=>Ours.Contains(a);bool IsDefender(Agent a)=>Defenders.Contains(a);
 NOTE_STRIKE PROMISED CHOSE_FIGHT CHOSE_WITHDRAW BEGIN_LEAVING HEADING_IN
 public void Tick(){CheckHeadingIn();}}
enum PersuasionArgumentStrength{ExtremelyHard=-3,VeryHard,Hard,Normal,Easy,VeryEasy,ExtremelyEasy}
class GwpFieldArrestBehavior{internal enum WardenStandingTier{UnderCharge,Ordinary,Respected}}
static class Guard{GUARD_STRENGTH GUARD_ODDS
 // Vanilla DefaultPersuasionModel.GetChances multipliers with the default chance at its usual ~1 (C# decompile v1.4.8).
 static (float S,float C) Native(PersuasionArgumentStrength s)=>s switch{PersuasionArgumentStrength.Easy=>(0.7f,0f),PersuasionArgumentStrength.Normal=>(0.55f,0.15f),PersuasionArgumentStrength.Hard=>(0.35f,0.15f),_=>throw new Exception()};
 // Each concern has four arguments, the third (and fourth in the second concern) staking a critical success; the player tries them in order.
 static readonly bool[][] Crit={new[]{false,false,true,false},new[]{false,false,false,true}};
 public static double Pass(PersuasionArgumentStrength strength,int skill,int trait=0,float situation=0f)=>Walk(strength,skill,trait,situation,0,0,0);
 static double Walk(PersuasionArgumentStrength st,int skill,int trait,float sit,int c,int i,int progress){
  if(progress>=2)return 1;if(c==2)return 0;if(i==4)return Walk(st,skill,trait,sit,c+1,0,progress);
  var n=Native(st);GuardOdds(n.S,n.C,Crit[c][i],skill,trait,sit,out float s,out float cs,out float cf,out float f);
  return s*Walk(st,skill,trait,sit,c+1,0,progress+1)+cs*Walk(st,skill,trait,sit,c+1,0,progress+2)+f*Walk(st,skill,trait,sit,c,i+1,progress);}}
static class Program{static int n;static void Check(bool c,string m){n++;if(!c)throw new Exception(m);}
 static Controller Scene(Stage stage,Agent main,Agent guard){var c=new Controller{_stage=stage};c.Ours.Add(main);c.Defenders.Add(guard);return c;}
 static int Main(){
  var mission=new Mission();var main=new Agent("player");var soldier=new Agent("player"){Controller=AgentControllerType.AI};
  var guard=new Agent("town"){Controller=AgentControllerType.AI};var civilian=new Agent("town"){Controller=AgentControllerType.AI};
  Agent.Main=main;
  Check(mission.Cancels(main,guard),"old: a neutral guard's blow is cancelled, so OnAgentHit never fires");
  Check(mission.Cancels(main,civilian),"civilians stay untouchable");
  foreach(var before in new[]{Stage.Entering,Stage.Questioned,Stage.Allowed,Stage.Fighting}){var p=Scene(before,main,guard);p.NoteStrike(main,guard);Check(p._pendingFight==null,"a strike is not war in stage "+before);}
  foreach(var after in new[]{Stage.Refused,Stage.Leaving,Stage.Withdrawing}){var p=Scene(after,main,guard);p.NoteStrike(main,guard);Check(p._pendingFight=="struck","new: striking the watch after it refused is war: "+after);}
  var c=Scene(Stage.Withdrawing,main,guard);c.Ours.Add(soldier);
  c.NoteStrike(main,civilian);c.NoteStrike(guard,main);c.NoteStrike(main,null);Check(c._pendingFight==null,"civilians, being struck and misses are not our attack");
  // Refused: the player picks.
  var fight=Scene(Stage.Refused,main,guard);fight.OnChoseFight();Check(fight._pendingFight=="chose_fight","choosing to fight starts it");
  var tol=GwpTuning.CitySearch.InwardTolerance;
  main.Position=new Vec3(20);var back=Scene(Stage.Refused,main,guard);back.OnChoseWithdraw();Check(back._stage==Stage.Withdrawing&&back._pendingFight==null,"choosing to withdraw is no fight yet");
  main.Position=new Vec3(5);back.Tick();Check(back._pendingFight==null,"walking out is fine");
  main.Position=new Vec3(20+tol-1);back.Tick();Check(back._pendingFight==null,"milling about where you stood is fine");
  main.Position=new Vec3(20+tol+1);MBInformationManager.Warnings=0;back.Tick();Check(back._pendingFight=="broke_withdrawal"&&MBInformationManager.Warnings==0,"withdrew then went in: war without warning");
  // Promised to leave at once: one warning, then war.
  main.Position=new Vec3(20);var promised=Scene(Stage.Questioned,main,guard);promised.OnPromisedToLeave();Check(promised._stage==Stage.Leaving,"promise to leave");
  main.Position=new Vec3(20+tol+1);promised.Tick();Check(promised._pendingFight==null&&promised._warned&&MBInformationManager.Warnings==1,"first time heading in: a warning");
  main.Position=new Vec3(20+tol+1+tol-1);promised.Tick();Check(promised._pendingFight==null,"the warning restarts the measure");
  main.Position=new Vec3(20+2*tol+2);promised.Tick();Check(promised._pendingFight=="ignored_warning","ignored the warning: war");
  Campaign.Current.ConversationManager.IsConversationFlowActive=true;var talking=Scene(Stage.Questioned,main,guard);main.Position=new Vec3(0);talking.OnPromisedToLeave();main.Position=new Vec3(100);talking.Tick();talking.Tick();Check(talking._pendingFight==null,"no judgement during a conversation");
  Campaign.Current.ConversationManager.IsConversationFlowActive=false;
  var wrongStage=Scene(Stage.Allowed,main,guard);wrongStage.OnChoseFight();wrongStage.OnChoseWithdraw();wrongStage.OnPromisedToLeave();Check(wrongStage._stage==Stage.Allowed&&wrongStage._pendingFight==null,"choices only count at their own step");
  guard.Team="enemy";Check(!mission.Cancels(main,guard),"after the fight starts the guard takes damage");
  // Odds.
  var R=GwpFieldArrestBehavior.WardenStandingTier.Respected;var O=GwpFieldArrestBehavior.WardenStandingTier.Ordinary;var U=GwpFieldArrestBehavior.WardenStandingTier.UnderCharge;
  Check(Guard.GuardStrength(R)>Guard.GuardStrength(O)&&Guard.GuardStrength(O)>Guard.GuardStrength(U),"standing orders the argument strength");
  Check(Guard.GuardStrength(O)==PersuasionArgumentStrength.Normal,"ordinary standing is one notch harder than the first version's Easy");
  foreach(float ns in new[]{0f,0.35f,0.55f,0.7f})foreach(float nc in new[]{0f,0.15f,0.2f})foreach(bool crit in new[]{false,true})foreach(int skill in new[]{0,100,300})foreach(int trait in new[]{-2,0,2})foreach(float sit in new[]{-0.5f,0f,0.2f}){
   Guard.GuardOdds(ns,nc,crit,skill,trait,sit,out float s,out float cs,out float cf,out float f);
   Check(Math.Abs(s+cs+cf+f-1f)<1e-5f&&s>=0&&cs>=0&&cf>=0&&f>=-1e-6f,"outcomes add up to one");
   Check(cf>=GwpTuning.CitySearch.CriticalFailureFloor*(crit?2:1)-1e-6f&&cf>=nc-1e-6f,"critical failure keeps its floor and never drops below vanilla");
   Check(crit?s==0:cs==0,"critical-success arguments win only critically");}
  Guard.GuardOdds(0.7f,0f,false,100,0,0f,out float s1,out _,out float c1,out _);Check(Math.Abs(s1-0.7f*(GwpTuning.CitySearch.SkillFactorAtZero+100f/GwpTuning.CitySearch.SkillPointsPerFactor))<1e-5f&&c1==GwpTuning.CitySearch.CriticalFailureFloor,"vanilla's zero critical failure on easy arguments gets the floor");
  Guard.GuardOdds(0.55f,0.15f,false,0,0,0f,out float low,out _,out _,out _);Guard.GuardOdds(0.55f,0.15f,false,200,0,0f,out float high,out _,out _,out _);Check(high>low+0.2f,"skill counts point by point");
  Guard.GuardOdds(0.55f,0.15f,false,100,2,0f,out float good,out _,out _,out _);Guard.GuardOdds(0.55f,0.15f,false,100,-2,0f,out float bad,out _,out _,out _);Check(good>bad,"the argument's trait counts");
  Guard.GuardOdds(0.55f,0.15f,false,100,0,-1f,out float hated,out _,out _,out _);Check(hated==GwpTuning.CitySearch.SuccessFloor,"bad relations and crime cannot make it impossible");
  Check(Guard.Pass(Guard.GuardStrength(R),100)>Guard.Pass(Guard.GuardStrength(O),100)&&Guard.Pass(Guard.GuardStrength(O),100)>Guard.Pass(Guard.GuardStrength(U),100),"pass rate follows standing");
  Check(Guard.Pass(Guard.GuardStrength(R),300)<1,"even the best player can fail");
  foreach(var tier in new[]{R,O,U})Console.WriteLine("PASS_RATE "+tier+": skill0="+Guard.Pass(Guard.GuardStrength(tier),0).ToString("0.00")+" skill100="+Guard.Pass(Guard.GuardStrength(tier),100).ToString("0.00")+" skill200="+Guard.Pass(Guard.GuardStrength(tier),200).ToString("0.00"));
  Console.WriteLine("STRIKE_ASSERTIONS="+n);return 0;}}
}
'''
        code = (code.replace('CANCEL', method(mission, 'CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase'))
                .replace('CITY_TUNING', city_tuning)
                .replace('NOTE_STRIKE', method(production, 'NoteStrike'))
                .replace('PROMISED', method(production, 'OnPromisedToLeave').replace('internal ', 'public '))
                .replace('CHOSE_FIGHT', method(production, 'OnChoseFight').replace('internal ', 'public '))
                .replace('CHOSE_WITHDRAW', method(production, 'OnChoseWithdraw').replace('internal ', 'public '))
                .replace('BEGIN_LEAVING', method(production, 'BeginLeaving'))
                .replace('HEADING_IN', method(production, 'CheckHeadingIn'))
                .replace('GUARD_STRENGTH', method(behavior, 'GuardStrength'))
                .replace('GUARD_ODDS', method(behavior, 'GuardOdds')))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-city-strike-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><NoWarn>CS8618;CS0649;CS8602;CS8603</NoWarn><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            r = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, r.returncode, r.stdout + r.stderr)
            for line in r.stdout.splitlines():
                if line.startswith(('PASS_RATE', 'STRIKE_ASSERTIONS=')):
                    print(line)


    def test_order_menu_has_orders_in_town(self):
        """Negative control: the installed default order list is empty in friendly missions (a town mission
        never stops being one), so the order menu offered nothing; our list answers for the search scene."""
        game = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')

        def native(type_name, dll):
            return subprocess.check_output(['ilspycmd', '-t', type_name, str(game / dll)], text=True, encoding='utf-8-sig')

        provider = native('TaleWorlds.MountAndBlade.View.VisualOrders.DefaultVisualOrderProvider',
                          'Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll')
        factory = native('TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual.VisualOrderFactory',
                         'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.ViewModelCollection.dll')
        mission = native('TaleWorlds.MountAndBlade.Mission', 'bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll')
        self.assertRegex(block(provider, r'public override bool IsAvailable\(\)'), r'return !Mission\.Current\.IsFriendlyMission;')
        self.assertIn('public bool IsFriendlyMission = true;', mission)
        # Providers are asked last-registered first; the first available one supplies the whole list.
        self.assertRegex(factory, r'for \(int num = _providers\.Count - 1; num >= 0; num--\)')
        views = read('GwpCitySearchMissionViews.cs')
        submodule = read('SubModule.cs')
        search = read('GwpCitySearchMission.cs')
        self.assertRegex(views, r'class GwpCitySearchVisualOrderProvider : DefaultVisualOrderProvider')
        self.assertIn('HasMissionBehavior<GwpCitySearchMissionController>()', views)
        self.assertIn('RegisterProvider(\n                new GwpCitySearchVisualOrderProvider())', submodule.replace('\r\n', '\n'))
        # Holding Tab to leave also needs the friendly flag, so the search scene keeps it.
        self.assertRegex(mission, r'IsFriendlyMission && !IsInPhotoMode')
        self.assertNotIn('IsFriendlyMission', search)


def block(source, header):
    start = re.search(r'(?m)^\s*' + header, source)
    if not start:
        raise AssertionError('Missing block: ' + header)
    depth, i = 0, source.index('{', start.end())
    for j in range(i, len(source)):
        depth += {'{': 1, '}': -1}.get(source[j], 0)
        if depth == 0:
            return source[start.start():j + 1]
    raise AssertionError(header)


if __name__ == '__main__':
    unittest.main()
