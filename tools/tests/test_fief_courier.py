"""Execute production fief eligibility, delivery and dispatch serialization.

World, inquiry rendering and movement/return effects are controlled boundaries.
No game or player save is opened.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_battle_peace import extract

MODULE = Path(__file__).resolve().parents[2] / 'GreyWardenPolicePurity'


class FiefCourierTests(unittest.TestCase):
    def test_selected_capture_delivery_expiry_and_save_continuity(self):
        owner = (MODULE / 'GreyWardenPlayerRequestBehavior.cs').read_text(encoding='utf-8-sig')
        delivery = extract((MODULE / 'GwpWardenDispatchBehavior.cs').read_text(encoding='utf-8-sig'), 'DeliverTo')
        record = (MODULE / 'GwpWardenDispatch.cs').read_text(encoding='utf-8-sig')
        record = re.sub(r'^using TaleWorlds\.[^\n]+\n', '', record, flags=re.M)
        methods = '\n'.join(extract(owner, name) for name in ('IsEligible', 'FileCourierAppeal', 'ActivateRequest', 'IsPlayerGreyWardenVassal'))
        methods += re.search(r'internal static bool CanFileCourierAppeal\(\)[^;]+;', owner).group()
        methods += re.search(r'internal static bool CanFileCourierAppeal\(string[^;]+;', owner).group()
        methods += re.search(r'private List<CaptureRecord> GetEligibleCaptures\(\)[^;]+;', owner).group()
        capture_flags = owner[owner.index('        [Flags]'):owner.index('        internal sealed class PlayerRequestTaskSnapshot')]
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)', methods + delivery)))
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity{
 static class GwpTextCatalog{KEYS}static class GwpText{public static string Get(string k,params object[] args)=>k;}
 static class Colors{public static object Green=new(),Yellow=new(),Cyan=new();}class InformationMessage{public InformationMessage(string text,object color){}}static class InformationManager{public static void DisplayMessage(InformationMessage m){}}
 class CampaignTime{public static CampaignTime Now=new();public double ToHours=1000;}
 class Kingdom{public string StringId="kingdom";}class Clan{public static Clan PlayerClan=new(){Kingdom=new()};public Kingdom? Kingdom;public bool IsUnderMercenaryService;public string StringId="player";}
 class Settlement{public bool IsFortification=true;public Kingdom? MapFaction=Clan.PlayerClan.Kingdom;public Clan OwnerClan=new(){StringId="other"};public static Dictionary<string,Settlement> All=new();}
 static class GwpCommon{public static bool Member=true,Handover=true;public static bool IsPlayerGreyWardenMember()=>Member;public static bool CanCompletePartyHandover(MobileParty a,MobileParty b)=>Handover;}
 static class GwpTuning{public static class PlayerRequests{public const int FiefAppealWindowDays=30;}}
 class MobileParty{public string StringId="courier";public object Party=new();public int PartyTradeGold=500;public ItemRoster ItemRoster=new();}
 class ItemRoster{public void AddToCounts(object item,int count){}}
 class Hero{public string StringId="";public bool IsPrisoner;public object? PartyBelongedToAsPrisoner;public object CharacterObject=new();public static Hero? FindFirst(Func<Hero,bool> f)=>null;}
 static class TransferPrisonerAction{public static void Apply(object c,object a,object b){}}
 class Campaign{public static Campaign Current=new();public T? GetCampaignBehavior<T>() where T:class=>null;}
 class PlayerBountyBehavior{public bool CanDispatchCaseReport;public string CaseReportIdentity="";public bool GrantCaseSupport(string reason,MobileParty p)=>false;public int CompleteDispatchedCaseReport(int v,bool lie,bool prisoner,object receiver)=>-1;}
 static class PoliceAntiWarDeclaration{public static int ApplyWardenMediation()=>0;}static class GreyWardenTroopRequestBehavior{public static bool FileCourierOrder(string id,int count,int price)=>false;}
 static class PoliceResourceManager{public static int Credited;public static void CreditJudicialTreasuryFromCourier(int amount){Credited+=amount;}}
 static class GwpAiDiagnostics{public static void WriteAction(MobileParty p,string action,string details){}}
 static class GwpDispatchCargo{public class CargoItem{public object Item=new();}public static int Value(MobileParty p,string s)=>0;public static List<CargoItem> Decode(string s)=>new();public static int Available(MobileParty p,CargoItem i)=>0;}
 static class GwpRuntimeState{public static class Player{public static int Reputation=40;}}
 class GreyWardenPlayerRequestBehavior{
 static GreyWardenPlayerRequestBehavior? _instance;List<CaptureRecord> _captures=new();string _activeSettlementId="";int _activeStage,_deferredTasksRemaining,_publicSupportPercent;double _activeFiledHour,_stayStartHour,_nextContactHour;bool _feePaid;
 FLAGS
 static Settlement? ResolveSettlement(string id)=>Settlement.All.TryGetValue(id,out var s)?s:null;
 METHODS
 public static void Setup(){_instance=new();GwpCommon.Member=true;Clan.PlayerClan=new(){Kingdom=new()};Settlement.All.Clear();Settlement.All["town"]=new();_instance._captures.Add(new(){SettlementId="town",KingdomId="kingdom",CaptureHour=900,AwardedClanId="other",Flags=CaptureFlags.PlayerParticipated|CaptureFlags.DecisionResolved});CampaignTime.Now.ToHours=1000;}
 public static bool Active=>_instance!._activeStage==(int)FiefRequestStage.SeekingPlayerForPayment;public static bool Paid=>_instance!._feePaid;
 public static void Expire()=>CampaignTime.Now.ToHours=2000;public static void Recapture()=>_instance!._captures[0].CaptureHour=950;public static void LoseKingdom()=>Settlement.All["town"].MapFaction=new();public static void OwnFief()=>Settlement.All["town"].OwnerClan=Clan.PlayerClan;public static void Mercenary()=>Clan.PlayerClan.IsUnderMercenaryService=true;
 }
 class CourierBehavior{public int Returns;void BeginReturn(GwpDispatchRecord r,MobileParty p){Returns++;r.Phase=GwpDispatchPhase.Returning;}
 DELIVERY
 public void Deliver(GwpDispatchRecord r,MobileParty p)=>DeliverTo(r,p,new());}
 class Program{static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}static GwpDispatchRecord Request()=>new(){PartyId="courier",ReceiverPartyId="warden",Purpose=GwpDispatchPurpose.FiefAppeal,FiefSettlementId="town",FiefCaptureHours=900,DispatchedHours=1000};
 static void Main(){
 System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("de-DE");var old=new GwpDispatchRecord{PartyId="old",Purpose=GwpDispatchPurpose.TroopOrder,OrderTroopId="heavy",OrderCount=12,OrderPrice=900,CaseGoldFloor=900,CargoState="cargo",CaseHeroId="case",NextTownBusinessHours=1200.5};string legacy=string.Join("|",old.Serialize().Split('|').Take(15));var restored=GwpDispatchRecord.Deserialize(legacy)!;Check(restored.Purpose==GwpDispatchPurpose.TroopOrder&&restored.OrderCount==12&&restored.OrderPrice==900&&restored.CaseGoldFloor==900&&restored.CargoState=="cargo"&&restored.CaseHeroId=="case"&&restored.NextTownBusinessHours==1200.5,"legacy fifteen fields keep existing purpose money cargo order and cooldown");Check(restored.FiefSettlementId==""&&restored.FiefCaptureHours==0,"legacy records default to no fief payload");
 var request=Request();request.FiefCaptureHours=900.125;var loaded=GwpDispatchRecord.Deserialize(request.Serialize())!;Check(loaded.Purpose==GwpDispatchPurpose.FiefAppeal&&loaded.FiefSettlementId=="town"&&loaded.FiefCaptureHours==900.125,"fief selected capture roundtrips invariantly");Check((int)GwpDispatchPurpose.Report==0&&(int)GwpDispatchPurpose.Support==1&&(int)GwpDispatchPurpose.TroopOrder==2&&(int)GwpDispatchPurpose.PeaceRequest==3,"existing purpose ids unchanged");
 GreyWardenPlayerRequestBehavior.Setup();Check(GreyWardenPlayerRequestBehavior.CanFileCourierAppeal("town",900)&&!GreyWardenPlayerRequestBehavior.Active,"choosing/sending request does not register early");var courier=new MobileParty();var behavior=new CourierBehavior();GwpCommon.Handover=false;behavior.Deliver(Request(),courier);Check(!GreyWardenPlayerRequestBehavior.Active&&behavior.Returns==0,"handover must actually be possible");GwpCommon.Handover=true;var live=GwpDispatchRecord.Deserialize(Request().Serialize())!;behavior.Deliver(live,courier);Check(GreyWardenPlayerRequestBehavior.Active&&!GreyWardenPlayerRequestBehavior.Paid&&courier.PartyTradeGold==500&&PoliceResourceManager.Credited==0,"delivered request enters original payment stage without collecting the appeal fee");Check(live.Phase==GwpDispatchPhase.Returning&&behavior.Returns==1,"delivered courier returns normally");Check(!GreyWardenPlayerRequestBehavior.FileCourierAppeal("town",900),"active request rejects duplicate delivery");
 foreach(var invalidate in new Action[]{GreyWardenPlayerRequestBehavior.Expire,GreyWardenPlayerRequestBehavior.Recapture,GreyWardenPlayerRequestBehavior.LoseKingdom,GreyWardenPlayerRequestBehavior.OwnFief,GreyWardenPlayerRequestBehavior.Mercenary,()=>GwpCommon.Member=false}){GreyWardenPlayerRequestBehavior.Setup();invalidate();var failed=Request();var route=new CourierBehavior();route.Deliver(failed,courier);Check(!GreyWardenPlayerRequestBehavior.Active&&failed.Phase==GwpDispatchPhase.Returning&&courier.PartyTradeGold==500,"invalid or changed captured fief returns intact without filing a different request");}
 Console.WriteLine("FIEF_COURIER_ASSERTIONS="+checks);
 }}
}
'''.replace('FLAGS', capture_flags).replace('METHODS', methods).replace('DELIVERY', delivery).replace('KEYS', ''.join(f'public const string {k}="{k}";' for k in keys))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-fief-courier-') as temp:
            p = Path(temp)
            (p / 'Tests.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            (p / 'Record.cs').write_text(record, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v:q'], text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('FIEF_COURIER_ASSERTIONS=')))
