"""Execute production commission outcomes and payment with campaign/quest seams."""
import re
import unittest
from test_city_search import read
from test_duty_banner import method
from test_city_search_flow import run


STUBS = r'''
using System;using System.Linq;
namespace TaleWorlds.Library{class InformationMessage{public string Text;public InformationMessage(string t){Text=t;}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}}
namespace GreyWardenPolicePurity{
class Hero{public string StringId="target",Name="target";public int Gold=50000;public bool IsPrisoner;public bool IsAlive=true;}
class CrimeRecord{public bool HasOpenCase=true,CitySearchPersonalUsed=true,CitySearchSupportUsed=true;}
static class CrimePool{public static CrimeRecord Record=new();public static int NegativeStanding=100,UnredeemedLives=8;public static CrimeRecord GetRecord(Hero h)=>Record;}
static class PoliceEnforcementBehavior{public static void CloseCase(CrimeRecord c,string reason){c.HasOpenCase=false;}}
static class GwpFieldArrestBehavior{public static int Deterrence;public static void RegisterFieldEnforcement(Hero h,CrimeRecord c){Deterrence++;}}
class Quest{public string StringId="quest";public int Failed,Ready,Success;public void FailCitySearch(string s){Failed++;}public void MarkReadyForTurnIn(){Ready++;}public void WriteLog(string s){}}
class GwpFieldReportLedger{public bool HasPendingReports;public int TotalAssessed,Declarations;public int PendingReceivedFor(string s)=>0;public void DeclareAmount(int delivered,bool truthful,string offenderId){Declarations++;}}
static class GwpAiDiagnostics{public static void WriteFieldArrest(string a,string b){}}
static class GwpText{public static string Get(string k,params object[] args)=>k;}
static class GwpTextCatalog{KEYS}
static class MBTextManager{public static string Result="";public static void SetTextVariable(string key,string value){Result=value;}}
static class PoliceResourceManager{public static int Paid;public static int PayFromJudicialTreasury(int fee){Paid+=fee;return fee;}}
class PlayerBountyBehavior{
 public bool Tracking=true,Held=true;public bool IsTrackingBountyTarget=>Tracking;public bool HasBountyTask=>Held;
 public bool HasCompletedEnforcement=>_casePeacefullyResolved||_caseTargetDefeated;
 public string _activeBountyTargetHeroId="target",_activeBountyTargetId="commission",_pendingPrisonerHeroId="";
 public int _assignedCaseFine=100000,_pendingPrisonerAssessed,_bountyPlayerCasualties=2,_caseCashCollectedFromTarget;
 public bool _casePeacefullyResolved,_caseTargetDefeated,_caseClosedElsewhere,_supportRequested;
 public string _caseDeterrenceRegisteredId="";public double _activeBountyDeadlineHours=50;
 public Quest _activeQuest=new();public GwpFieldReportLedger Reports=new();public Hero? CaseHero=new();public string CaseReportIdentity=>"target";
 int CaseAmountDue=>Math.Max(_assignedCaseFine,Math.Max(_pendingPrisonerAssessed,Reports.TotalAssessed));
 bool HasFieldBusinessToSettle=>Reports.HasPendingReports||_pendingPrisonerAssessed>0;
 public int Releases;
 bool IsCommissionTarget(Hero h)=>Held&&h.StringId==_activeBountyTargetHeroId;
 void ReleaseWardensAfterEnforcement(){Releases++;}void EnterBountyCollectionState(){Tracking=false;}
 void EndBountyTaskState(bool tryRestorePeace){Tracking=Held=false;}
 int CalculateCaseFee(int cap)=>GwpCaseSettlementRules.Reward(cap,_bountyPlayerCasualties,100);
 void FinishCaseReport(int paid){_activeQuest.Success++;Tracking=Held=false;_casePeacefullyResolved=_caseClosedElsewhere=false;MBTextManager.Result=GwpTextCatalog.gwp_case_report_complete;}
 METHODS
 public string Save()=>SerializeCaseOutcome();public void Load(string s)=>DeserializeCaseOutcome(s);
 public int Fee=>ExpectedCaseFee;public void Report()=>CompleteCashReport(0,false);
}
class Program{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static PlayerBountyBehavior Setup(){CrimePool.Record=new();CrimePool.NegativeStanding=100;CrimePool.UnredeemedLives=8;GwpFieldArrestBehavior.Deterrence=0;PoliceResourceManager.Paid=0;return new();}
 static void Main(){
  var b=Setup();var target=b.CaseHero!;b.RecordCitySearchDuelAgreement(target);
  Check(!b.Tracking&&b.Held&&b._activeQuest.Ready==1&&b._activeQuest.Failed==0,"agreed loss enters normal report without failing player quest");
  Check(!CrimePool.Record.HasOpenCase&&!target.IsPrisoner&&target.Gold==50000&&CrimePool.NegativeStanding==100&&CrimePool.UnredeemedLives==8,"agreement closes pursuit without capture payment or redeeming unpaid record");
  Check(GwpFieldArrestBehavior.Deterrence==1&&b.Releases==1,"agreed encounter registers enforcement exactly once and releases duty");b.RecordCitySearchDuelAgreement(target);Check(GwpFieldArrestBehavior.Deterrence==1,"late duplicate outcome cannot register twice");
  var loaded=new PlayerBountyBehavior{Tracking=false,_assignedCaseFine=0};loaded.Load(b.Save());loaded.Reports.TotalAssessed=9000;loaded.Reports.HasPendingReports=true;int expected=GwpCaseSettlementRules.WithdrawnCaseCompensation(2);
  Check(loaded.Fee==expected,"effort compensation survives saved outcome without severity reward");loaded.Report();
  Check(PoliceResourceManager.Paid==expected&&loaded.Reports.Declarations==0&&!loaded.Held&&loaded._activeQuest.Success==1,"ordinary report pays only effort fee without declaring offender money");
  Check(MBTextManager.Result==GwpTextCatalog.gwp_case_report_complete,"agreement report uses normal completion text instead of withdrawn commission");
  b=Setup();b.FailCitySearchCommission(new(){StringId="unrelated"});Check(b.Held&&b._activeQuest.Failed==0,"different target cannot terminate current commission");b.FailCitySearchCommission(b.CaseHero!);
  Check(!b.Held&&!b.Tracking&&b._activeQuest.Failed==1&&CrimePool.Record.HasOpenCase,"genuine support failure releases player assignment while preserving case for wardens");
  Check(CrimePool.Record.CitySearchPersonalUsed&&CrimePool.Record.CitySearchSupportUsed&&PoliceResourceManager.Paid==0&&GwpFieldArrestBehavior.Deterrence==0,"failure resets neither player quota nor money and enforcement history");
  b=Setup();b._caseTargetDefeated=true;b.FailCitySearchCommission(b.CaseHero!);Check(b.Held&&b._activeQuest.Failed==0,"previously completed enforcement cannot be overwritten as failed search");
  b=Setup();CrimePool.Record.HasOpenCase=false;b.FailCitySearchCommission(b.CaseHero!);Check(b.Held&&b._activeQuest.Failed==0,"externally closed case keeps normal withdrawn compensation reconciliation");
  b=Setup();b.CaseHero!.IsPrisoner=true;b.FailCitySearchCommission(b.CaseHero!);Check(b.Held&&b._activeQuest.Failed==0,"third-party captivity keeps existing custody choices instead of player failure");
  b=Setup();b.CaseHero!.IsAlive=false;b.FailCitySearchCommission(b.CaseHero!);Check(b.Held&&b._activeQuest.Failed==0,"external target death keeps existing outcome reconciliation");
  Console.WriteLine("CITY_SEARCH_OUTCOME_ASSERTIONS="+n);
 }
}
}
'''


class CitySearchOutcomeTests(unittest.TestCase):
    def test_production_agreement_failure_payment_and_save(self):
        city = read('PlayerBountyBehavior.CitySearch.cs')
        settlement = read('PlayerBountyBehavior.CaseSettlement.cs')
        methods = '\n'.join(method(city, n) for n in ['RecordCitySearchDuelAgreement', 'FailCitySearchCommission'])
        methods += '\n' + '\n'.join(method(settlement, n) for n in [
            'NotifyCasePeacefullyResolved',
            'DeserializeCaseOutcome', 'CompleteWithdrawnCaseReport', 'CompleteCashReport'])
        methods += '\n' + method(settlement[settlement.index('        private int WithdrawnCaseCompensation()'):], 'WithdrawnCaseCompensation')
        start=settlement.index('        private string SerializeCaseOutcome()')
        methods += '\n' + settlement[start:settlement.index('        private void DeserializeCaseOutcome',start)]
        for name in ['HasNothingLeftToSettle', 'ExpectedCaseFee']:
            methods += '\n' + re.search(r'(?:private|internal) (?:bool|int) '+name+r' =>[\s\S]*?;', settlement).group(0)
        keys = set(re.findall(r'GwpTextCatalog\.(\w+)', methods)) | {'gwp_case_report_complete'}
        code = STUBS.replace('KEYS', ''.join(f'public const string {k}="{k}";' for k in keys)).replace('METHODS', methods)
        for name in ['GwpCaseSettlementRules.cs', 'GwpTuning.cs']:
            source = read(name)
            code += source[source.index('namespace GreyWardenPolicePurity'):]
        run(code)
        for old, failure in [
            (code.replace('_caseClosedElsewhere = true;', '_caseClosedElsewhere = false;'), 'effort compensation survives saved outcome'),
            (code.replace('EndBountyTaskState(tryRestorePeace: true);', ''), 'genuine support failure releases player assignment'),
        ]:
            with self.subTest(negative_control=failure), self.assertRaisesRegex(AssertionError, failure):
                run(old)


if __name__ == '__main__':
    unittest.main()
