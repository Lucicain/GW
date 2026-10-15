"""Replay production atonement assignment, settlement and envoy handover against engine boundaries."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / "GreyWardenPolicePurity"

STUBS = r'''
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace GreyWardenPolicePurity {
enum AtonementFlowState { Inactive, Active, WaitingForTurnIn }
enum GwpCrimeCategory { Unknown }
class Hero {
 public string StringId="target"; public bool IsPrisoner,IsDead; public int Gold;
 public MobileParty? PartyBelongedTo; public PartyBase? PartyBelongedToAsPrisoner; public Settlement? CurrentSettlement;
 public static Hero MainHero=new Hero{StringId="player"}; public static Hero? Target;
 public static Hero? Find(string id)=>Target?.StringId==id?Target:null;
 public static Hero? FindFirst(Func<Hero,bool> p)=>Target!=null&&p(Target)?Target:null;
 public static Hero? OneToOneConversationHero;
}
class PartyBase { public MobileParty? MobileParty; public bool IsMobile=>MobileParty!=null; }
class Settlement { public PartyBase Party=new PartyBase(); }
class Army { public MobileParty? LeaderParty; }
class MobileParty {
 public string StringId="target_party"; public bool IsActive=true,IsMainParty;
 public PartyBase Party=new PartyBase(); public Hero? LeaderHero; public Army? Army;
 public object? CurrentSettlement; public static MobileParty MainParty=new MobileParty{StringId="player_party",IsMainParty=true};
 public static MobileParty? ConversationParty;
}
class CampaignTime {
 public double ToHours; public double ToDays=>ToHours/24; public static CampaignTime Now=new CampaignTime{ToHours=100};
 public static CampaignTime Zero=new CampaignTime(); public static CampaignTime Never=new CampaignTime{ToHours=double.MaxValue};
 public static CampaignTime operator -(CampaignTime a,CampaignTime b)=>new CampaignTime{ToHours=a.ToHours-b.ToHours};
}
class Campaign {
 public static Campaign Current=new Campaign(); public PoliceEnforcementBehavior Enforcement=null!;
 public PlayerBountyBehavior Bounty=new PlayerBountyBehavior(); public BarterManager BarterManager=new BarterManager();
 public PoliceAIDeterrenceBehavior Deterrence=new PoliceAIDeterrenceBehavior();
 public T? GetCampaignBehavior<T>() where T:class {
  if(typeof(T)==typeof(PoliceEnforcementBehavior))return Enforcement as T;
  if(typeof(T)==typeof(PlayerBountyBehavior))return Bounty as T;
  if(typeof(T)==typeof(PoliceAIDeterrenceBehavior))return Deterrence as T;return null; }
}
class PlayerBountyBehavior {
 public Hero? Assigned; public bool Joined; public bool CanOfferFieldGrace=>Assigned!=null;
 public bool HasCommissionAuthority(Hero? h)=>Joined&&h!=null&&h==Assigned;
 public bool IsActiveBountyTarget(Hero? h)=>HasCommissionAuthority(h);
 public static bool IsBountyCaseHeldByPlayer(string? id)=>id!=null&&Campaign.Current.Bounty.Assigned?.StringId==id;
 public static bool IsBountyReservedTarget(MobileParty? p)=>p!=null&&p==Campaign.Current.Bounty.Assigned?.PartyBelongedTo;
 public void RecordFieldGrace(string m){} public void NotifyCaseSettledInField(Hero? h,int c,int a,int s){}
 public void NotifyCaseClosedByCapture(Hero? h,int a,int s){}
}
class CrimeRecord { public string OffenderHeroId="target",CrimeId="target"; public bool HasOpenCase=true;
 public Hero? OffenderHero=>Hero.Target; public GwpCrimeCategory CrimeCategory; public bool IsOffenderPursuable()=>true; }
static class CrimePool { public static CrimeRecord? Record=new CrimeRecord();
 public static CrimeRecord? GetRecordByKey(string id)=>Record?.CrimeId==id?Record:null; }
static class CrimeState { public static CrimeRecord? Other;
 public static CrimeRecord? GetByOffenderId(string id)=>Other??CrimePool.Record; public static void RefreshAccepting(){} }
class PlayerStateStub { public int Reputation=-18; public bool HasAtonement;
 public void SetAtonementTaskActive(bool v){HasAtonement=v;} public void ResetReputation(int v){Reputation=v;}
 public void ChangeReputation(int v){Reputation+=v;} }
class TextObject { public TextObject(string t){} }
class InformationMessage { public InformationMessage(string m,object c){} }
static class InformationManager { public static void DisplayMessage(InformationMessage m){} }
static class Colors { public static object Green=new object(),Yellow=new object(); }
static class GwpText { public static string Get(string k,params object[] p)=>k; }
static class GwpTuning { public static class FieldArrest { public const int GraceHours=72; }
 public static class Enforcement { public const int AtonementIntelReportIntervalDays=2; } }
class GwpFieldReportLedger { public static GwpFieldReportLedger Instance=new GwpFieldReportLedger();
 public int Pending,Received,Declared=-1; public int PendingAssessedFor(string id)=>Pending;
 public int PendingReceivedFor(string id)=>Received; public void DeclareAmount(int n,string offenderId){Declared=n;Pending=0;}
 public object? ReceiptFor(string id)=>null; }
class GwpAssetPayment { public static GwpAssetPayment? Last; public bool Applied; public int Paid;
 public object Entries=new object(); public GwpAssetPayment(Hero p,Hero r,PartyBase f,PartyBase t,int l,int a,
   bool reportMode,object? autoReceipt,Hero? prisoner){Last=this;} public void PrepareCatalogue(object data){} }
class Barterable {} class BarterData {}
class BarterManager { public delegate void BarterBeginEventDelegate(object data); public BarterBeginEventDelegate? BarterBegin;
 public void StartBarterOffer(Hero p,Hero r,PartyBase f,PartyBase t,object? a,Func<Barterable,BarterData,object,bool> c,
   int n,bool b,object e){BarterBegin?.Invoke(new object());} }
static class PoliceStats { public static Holder GetPoliceClan()=>new Holder();
 public class Holder { public Hero Leader=new Hero(); } }
class MapEventSide { public List<MapEventParty> Parties=new List<MapEventParty>(); }
class MapEventParty { public PartyBase Party=null!; public MobileParty? MobileParty=>Party.MobileParty; }
class MapEvent { public List<MapEventParty> InvolvedParties=new List<MapEventParty>();
 public bool HasWinner=true; public MapEventSide Winner=new MapEventSide(),AttackerSide=new MapEventSide(); }
class PoliceAIDeterrenceBehavior { public bool Arrest;
 public void RegisterEnforcementOutcome(MapEvent m,Hero? h,GwpCrimeCategory c,bool countAsArrest){Arrest=countAsArrest;} }
partial class PoliceEnforcementBehavior {
 public static PoliceEnforcementBehavior? _instance;
 public bool _atonementActive,_atonementWaitingForTurnIn,_awaitingAtonementQuestReconnect,_enforcementAtonementAssigned;
 public string _atonementTargetPartyId="target_party",_atonementTargetHeroId="target",_atonementTargetName="Target",_atonementTargetFactionId="faction";
 public int _atonementTargetCrimeCategory,_atonementTargetSizeSnapshot,_atonementReputationReward=5;
 public float _atonementDeadlineHours=200;
 public CampaignTime _lastAtonementIntelReportTime=CampaignTime.Zero;
 public AtonementQuest? _atonementQuest=new AtonementQuest(); public GwpAssetPayment? _atonementPayment;
 public bool _atonementTurnInOpened; public static PlayerStateStub PlayerState=new PlayerStateStub();
 public List<MobileParty> Parties=new List<MobileParty>();
 public void SetState(AtonementFlowState s)=>SetAtonementFlowState(s);
 public void Update()=>UpdateAtonementTask(); public void OpenReport()=>BeginAtonementTurnIn();
 public void TurnIn()=>OnEnforcementAtonementTurnInConsequence(); public bool Accepted()=>AtonementPaymentAccepted();
 public void Battle(MapEvent e)=>HandleAtonementMapEventEnded(e);
 private MobileParty? FindActiveParty(string id)=>Parties.FirstOrDefault(p=>p.StringId==id&&p.IsActive);
 private void TryReconnectAtonementQuestOnHourlyTick(){} private void AppendAtonementIntelLog(MobileParty t){}
 private void MakePeaceWithAtonementTargetFaction(){}
 private void FailAtonementTask(string r){PlayerState.ChangeReputation(-5);ClearAtonementTaskState();}
 private void CloseCase(CrimeRecord? c,string r){if(c!=null)c.HasOpenCase=false;}
 public class AtonementQuest { public bool Cancelled,Succeeded; public bool IsOngoing=true; public CampaignTime Due=CampaignTime.Now;
 public void WriteLog(string m){} public void SucceedQuestWithReputation(int g,int r){Succeeded=true;}
 public void ChangeQuestDueTime(CampaignTime d){Due=d;} public void CompleteQuestWithCancel(TextObject t){Cancelled=true;}
 QUEST_METHOD }
 METHODS
 // Envoy engine boundaries; the cancellation and hourly ownership checks below are production methods.
 enum EnvoyStage { Travelling,Failed,Grace,Duel,DuelDecided,Aftermath }
 class EnvoyCase { public string CrimeId="target",EnvoyPartyId="envoy"; public EnvoyStage Stage; public double DueHours; public bool CollectOnly; }
 readonly Dictionary<string,EnvoyCase> _envoyCases=new Dictionary<string,EnvoyCase>();
 readonly Dictionary<string,bool> _delayPatrolStates=new Dictionary<string,bool>();
 public int Returned,ReleasedDuelists,EnvoyActions;
 private void MarkDelayPatrolReturning(string id){Returned++;}
 private void ReleaseDuelists(EnvoyCase e){ReleasedDuelists++;}
 private void ForgetEnvoyCase(EnvoyCase e,string r){_envoyCases.Remove(e.CrimeId);}
 private void TendAftermath(EnvoyCase e){EnvoyActions++;}
 private void TendTravellingEnvoy(EnvoyCase e,CrimeRecord c){EnvoyActions++;}
 private void SendGraceCollector(EnvoyCase e,CrimeRecord c){EnvoyActions++;}
 private void TendDuel(EnvoyCase e,CrimeRecord c){EnvoyActions++;}
 private void FinishDuel(EnvoyCase e,CrimeRecord c){EnvoyActions++;}
 public void AddEnvoy(int stage){_envoyCases["target"]=new EnvoyCase{Stage=(EnvoyStage)stage};_delayPatrolStates["envoy"]=true;}
 public void Handover()=>ReleaseEnvoysToPlayer("target"); public void Tend()=>TendEnvoyCases();
 public int Envoys=>_envoyCases.Count;
 ENVOY_METHODS
}
static class Program {
 static int checks; static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static PoliceEnforcementBehavior Setup(){
  Campaign.Current=new Campaign();var e=new PoliceEnforcementBehavior();Campaign.Current.Enforcement=e;PoliceEnforcementBehavior._instance=e;
  Hero.Target=new Hero(); var party=new MobileParty{LeaderHero=Hero.Target};party.Party.MobileParty=party;
  Hero.Target.PartyBelongedTo=party;e.Parties.Add(party);
  PoliceEnforcementBehavior.PlayerState=new PlayerStateStub();e.SetState(AtonementFlowState.Active);
  MobileParty.MainParty.Party.MobileParty=MobileParty.MainParty;
  MobileParty.ConversationParty=MobileParty.MainParty;CrimePool.Record=new CrimeRecord();CrimeState.Other=null;
  GwpFieldReportLedger.Instance=new GwpFieldReportLedger();return e;
 }
 static void Main(){
  var e=Setup();Check(!Campaign.Current.Bounty.HasCommissionAuthority(Hero.Target),"pre-fix bounty gate rejects non-member");
  Check(GwpPlayerCaseAssignment.HasAuthority(Hero.Target),"atonement grants authority without recruitment");
  Check(GwpPlayerCaseAssignment.IsAssignedCase(Hero.Target),"charge options recognize atonement");
  Check(!GwpPlayerCaseAssignment.HasAuthority(new Hero{StringId="other"}),"no general arrest power");
  Check(GwpPlayerCaseAssignment.IsCaseHeldByPlayer("target"),"crime picker reserves atonement");
  Check(GwpPlayerCaseAssignment.IsReservedTarget(Hero.Target!.PartyBelongedTo),"pursuit and envoy gate reserve target");
  var army=new MobileParty();Hero.Target.PartyBelongedTo!.Army=new Army{LeaderParty=army};
  Check(GwpPlayerCaseAssignment.IsReservedTarget(army),"army leader protected too");
  var saved=e;var restored=new PoliceEnforcementBehavior();
  foreach(string field in new[]{PERSISTED})typeof(PoliceEnforcementBehavior).GetField(field)!.SetValue(restored,typeof(PoliceEnforcementBehavior).GetField(field)!.GetValue(saved));
  Campaign.Current.Enforcement=restored;Check(GwpPlayerCaseAssignment.HasAuthority(Hero.Target),"production saved fields restore authority");
  Check(GwpPlayerCaseAssignment.IsReservedTarget(army),"reservation survives state restoration");
  foreach(int stage in new[]{0,2,3,4}){e=Setup();e.AddEnvoy(stage);e.Handover();Check(e.Envoys==0&&e.Returned==1,"in-flight envoy recalled at handover");}
  e=Setup();e.AddEnvoy(1);e.Handover();Check(e.Envoys==1&&e.Returned==0,"failed negotiation history survives player handover");
  foreach(int stage in new[]{2,3,4}){e=Setup();e.AddEnvoy(stage);e.SetState(AtonementFlowState.Inactive);Check(!GwpPlayerCaseAssignment.CanTakeCase(CrimePool.Record!),"existing grace and duel agreements are not offered as new warrants");}
  e=Setup();e.AddEnvoy(0);e.Tend();Check(e.EnvoyActions==0&&e.Returned==1,"hourly envoy cannot settle a player case");
  e=Setup();e.AddEnvoy(5);e.Tend();Check(e.EnvoyActions==1,"already settled aftermath remains its owner's risk");
  e=Setup();var quest=e._atonementQuest!;GwpPlayerCaseAssignment.NotifySettledInField(Hero.Target,300,500,3);
  Check(!GwpPlayerCaseAssignment.IsAssignedCase(Hero.Target),"peace moves pursuit to turn-in");
  Check(GwpPlayerCaseAssignment.IsCaseHeldByPlayer("target"),"case retained until report");
  Check(quest.Due==CampaignTime.Never,"native quest timer disabled at completion");
  Check(e._atonementDeadlineHours==0,"behavior pursuit timer disabled");
  e.OpenReport();Check(e.Accepted(),"no-money outcome can report immediately");e.TurnIn();
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-13&&quest.Succeeded,"award granted once");e.TurnIn();
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-13,"repeated callback cannot duplicate award");
  e=Setup();GwpPlayerCaseAssignment.NotifySettledInField(Hero.Target,300,500,3);
  GwpFieldReportLedger.Instance.Pending=500;GwpFieldReportLedger.Instance.Received=300;e.OpenReport();
  Check(!e.Accepted(),"money report waits for actual barter acceptance");e.TurnIn();
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-18,"cancelled/unapplied barter awards nothing");
  e.OpenReport();e._atonementPayment!.Applied=true;e._atonementPayment.Paid=250;e.TurnIn();
  Check(GwpFieldReportLedger.Instance.Declared==250,"actual delivery passed to audit owner");
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-13,"report completes redemption");
  e=Setup();Hero.Target!.IsPrisoner=true;Hero.Target.PartyBelongedToAsPrisoner=MobileParty.MainParty.Party;e.Update();
  Check(e._atonementWaitingForTurnIn&&!CrimePool.Record!.HasOpenCase,"capture in custody is a completed case");
  e=Setup();quest=e._atonementQuest!;CrimePool.Record!.HasOpenCase=false;e.Update();
  Check(quest.Cancelled&&!e._atonementActive,"case resolved elsewhere is withdrawn");
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-18,"no penalty for another party's resolution");
  e=Setup();e._atonementDeadlineHours=50;CrimePool.Record!.HasOpenCase=false;e.Update();
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-18,"external closure precedes timeout penalty");
  e=Setup();e._atonementDeadlineHours=50;e.Update();
  Check(PoliceEnforcementBehavior.PlayerState.Reputation==-23&&!e._atonementActive,"unfulfilled active deadline still fails");
  e=Setup();var rebuilt=new MobileParty{StringId="rebuilt",LeaderHero=Hero.Target};Hero.Target!.PartyBelongedTo=rebuilt;e.Update();
  Check(e._atonementTargetPartyId=="rebuilt","tracking follows hero instead of stale party id");
  e=Setup();GwpPlayerCaseAssignment.NotifySettledInField(Hero.Target,0,0,0);PoliceEnforcementBehavior.PlayerState.Reputation=4;
  e.OpenReport();e.TurnIn();Check(PoliceEnforcementBehavior.PlayerState.Reputation==4,"redemption never lowers existing positive standing");
  e=Setup();var us=new MapEventParty{Party=MobileParty.MainParty.Party};var them=new MapEventParty{Party=Hero.Target!.PartyBelongedTo!.Party};
  var battle=new MapEvent();battle.InvolvedParties.AddRange(new[]{us,them});battle.AttackerSide.Parties.Add(us);battle.Winner.Parties.Add(us);
  CrimeState.Other=new CrimeRecord{CrimeId="other",OffenderHeroId="other"};
  e.Battle(battle);Check(e._atonementWaitingForTurnIn&&!CrimePool.Record!.HasOpenCase,"defeating the target completes and closes case");
  Check(CrimeState.Other.HasOpenCase,"same-party case lookup cannot close another suspect's case");
  Check(!Campaign.Current.Deterrence.Arrest,"defeated escapee is not counted as captured");
  e=Setup();us=new MapEventParty{Party=MobileParty.MainParty.Party};them=new MapEventParty{Party=Hero.Target!.PartyBelongedTo!.Party};
  battle=new MapEvent();battle.InvolvedParties.AddRange(new[]{us,them});battle.AttackerSide.Parties.AddRange(new[]{us,them});battle.Winner.Parties.AddRange(new[]{us,them});
  e.Battle(battle);Check(e._atonementActive,"helping the target win is not enforcement");
  e=Setup();e.SetState(AtonementFlowState.Inactive);Campaign.Current.Bounty.Assigned=Hero.Target;Campaign.Current.Bounty.Joined=true;
  Check(GwpPlayerCaseAssignment.HasAuthority(Hero.Target)&&GwpPlayerCaseAssignment.IsReservedTarget(Hero.Target!.PartyBelongedTo),"ordinary bounty authority and reservation preserved");
  Console.WriteLine("ATONEMENT_ASSERTIONS="+checks+"; PASS");
 }
}
}
'''


class AtonementTests(unittest.TestCase):
    def test_non_member_authority_envoy_handover_completion_and_reports(self):
        atonement = (MODULE / 'PoliceEnforcementBehavior.AtonementQuest.cs').read_text(encoding='utf-8-sig')
        enforcement = (MODULE / 'PoliceEnforcementBehavior.cs').read_text(encoding='utf-8-sig')
        envoy = (MODULE / 'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig')
        names = ['HoldsAtonementCase', 'IsActiveAtonementTarget', 'ReservesAtonementTarget',
                 'CanOfferAtonementGrace', 'RecordAtonementGrace', 'AtonementTargetHero', 'AtonementTargetParty',
                 'AtonementPaymentAccepted', 'AtonementPaymentContext']
        members = []
        for name in names:
            match = re.search(r'(?m)^        (?:private|internal)[^\n]+\b' + name + r'\b[^\n]*(?:=>|\()', atonement)
            self.assertIsNotNone(match, name)
            if '=>' in match[0]:
                members.append(atonement[match.start():atonement.index(';', match.end()) + 1])
            else:
                members.append(extract(atonement, name))
        for name in ['NotifyAtonementResolved', 'MarkAtonementReady', 'WithdrawAtonementTask',
                     'BeginAtonementTurnIn', 'OnEnforcementAtonementTurnInConsequence']:
            members.append(extract(atonement, name))
        for name in ['SetAtonementFlowState', 'UpdateAtonementTask', 'ClearAtonementTaskState', 'HandleAtonementMapEventEnded']:
            members.append(extract(enforcement, name))
        for name in ['CurrentAtonementState', 'HasAtonementTask', 'IsAtonementActiveState', 'IsAtonementWaitingForTurnInState']:
            match = re.search(r'(?m)^        private [^\n]+\b' + name + r'\b\s*=>', enforcement)
            members.append(enforcement[match.start():enforcement.index(';', match.end()) + 1])
        persisted = re.findall(r'dataStore.SyncData\("gwp_enf_atone[^"\n]+", ref (\w+)\)', enforcement)
        self.assertGreaterEqual(len(persisted), 9)
        harness = STUBS.replace('QUEST_METHOD', extract(atonement, 'MarkReadyForTurnIn'))
        harness = harness.replace('ENVOY_METHODS', '\n'.join(extract(envoy, name) for name in
            ['ReleaseEnvoysToPlayer', 'CancelEnvoyForPlayer', 'TendEnvoyCases']))
        for name in ['HasEnvoyAgreement', 'IsAwaitingEnvoyCollection']:
            match = re.search(r'(?m)^        (?:private|internal)[^\n]+\b' + name + r'\b[^\n]*=>', envoy)
            members.append(envoy[match.start():envoy.index(';', match.end()) + 1])
        harness = harness.replace('METHODS', '\n'.join(members)).replace('PERSISTED', ','.join('"'+p+'"' for p in persisted))
        owner = (MODULE / 'GwpPlayerCaseAssignment.cs').read_text(encoding='utf-8-sig')
        owner = re.sub(r'^using TaleWorlds[^\n]+\n', '', owner, flags=re.M)
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            (folder / 'Replay.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers>'
                '</PropertyGroup></Project>', encoding='utf-8')
            (folder / 'Program.cs').write_text(harness, encoding='utf-8')
            (folder / 'Assignment.cs').write_text(owner, encoding='utf-8')
            (folder / 'GwpTextCatalog.g.cs').write_bytes((MODULE / 'GwpTextCatalog.g.cs').read_bytes())
            result = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'],
                                    capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn('ATONEMENT_ASSERTIONS=', result.stdout)
            print(next(line for line in result.stdout.splitlines() if 'ATONEMENT_ASSERTIONS=' in line))


if __name__ == '__main__':
    unittest.main()
