"""Native ViewModel command -> production dispatch gate/queue/pump/selection/cancel.

World, party-screen presentation and dispatch delivery are controlled boundaries.
Uses the installed TaleWorlds.Library.dll command dispatcher, not a fake VM call.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'
GAME_BIN = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client')


class AffairsDispatchTests(unittest.TestCase):
    def test_native_command_to_original_selection_and_cancel(self):
        source = (MODULE / 'GwpWardenDispatchDialogue.cs').read_text(encoding='utf-8-sig')
        vm = (MODULE / 'GwpAffairsVM.cs').read_text(encoding='utf-8-sig')
        row = vm[vm.index('    public sealed class GwpAffairsDispatchVM'):vm.index('    public sealed class GwpAffairsVM')]
        methods = '\n'.join(extract(source, n) for n in (
            'AffairsDispatchBlockReason', 'TryStartFromAffairs', 'Pump', 'OpenTroopSelection',
            'OnSelectionClosed', 'ReturnSelection', 'Disarm', 'ReportAvailable',
            'SupportAvailable', 'TroopOrderAvailable', 'PeaceRequestAvailable', 'SendFiefAppeal', 'TryTakeSelectedEscort'))
        methods += re.search(r'private static bool FiefAppealAvailable\([^;]+;', source).group()
        methods += re.search(r'private static bool PurposeAvailable\([^;]+;', source).group()
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)', methods)))
        root = ET.parse(MODULE / '_Module/GUI/Prefabs/GwpCaseArchive.xml').getroot()
        panel = root.find('.//ListPanel[@DataSource="{DispatchOptions}"]')
        self.assertIsNotNone(panel)
        button = panel.find('.//ButtonWidget')
        command = button.get('Command.Click')
        self.assertEqual('ExecuteSend', command)
        self.assertEqual('@IsEnabled', button.get('IsEnabled'))
        self.assertEqual('Fixed', button.get('WidthSizePolicy'))
        self.assertEqual('240', button.get('SuggestedWidth'))
        self.assertEqual('40', button.get('SuggestedHeight'))
        self.assertTrue(any(n.get('Text') == '@Reason' for n in panel.iter('TextWidget')))
        self.assertFalse(any(n.get('Command.Click') == 'ExecuteActions' for n in root.iter()))
        code = r'''
using System;using System.Collections.Generic;using System.Linq;using TaleWorlds.Library;
namespace GreyWardenPolicePurity{
 enum GwpDispatchPurpose{Report,Support,TroopOrder,PeaceRequest,FiefAppeal}
 class CharacterObject{public string Id="grey";public bool IsHero,Grey=true;public static CharacterObject PlayerCharacter=new();}
 class TroopRosterElement{public CharacterObject Character=new();public int Number=4,WoundedNumber=1,Xp=80;}
 class TroopRoster{public List<TroopRosterElement> Items=new();public int TotalManCount=>Items.Sum(e=>e.Number);public static TroopRoster CreateDummyTroopRoster()=>new();public IEnumerable<TroopRosterElement> GetTroopRoster()=>Items;public void AddToCounts(CharacterObject c,int n,bool front=false,int wounded=0,int xp=0){var e=Items.FirstOrDefault(x=>x.Character==c);if(e==null){e=new(){Character=c,Number=0,WoundedNumber=0,Xp=0};Items.Add(e);}e.Number+=n;e.WoundedNumber+=wounded;e.Xp+=xp;}public void Clear()=>Items.Clear();}
 class MobileParty{public static MobileParty MainParty=new();public bool IsActive=true;public object? MapEvent,CurrentSettlement;public TroopRoster MemberRoster=new();public TroopRoster PrisonRoster=new();}
 class PartyBase{public static object MainParty=new();}class PlayerEncounter{public static object? Current;}
 class ConversationManager{public bool IsConversationInProgress;}class ConversationCharacterData{public ConversationCharacterData(CharacterObject c,object p,params bool[] flags){}}static class CampaignMapConversation{public static int Opens;public static void OpenConversation(ConversationCharacterData a,ConversationCharacterData b){Opens++;}}
 class Campaign{public static Campaign Current=new();public object? CurrentMenuContext;public ConversationManager ConversationManager=new();public Dictionary<Type,object> Behaviors=new();public T? GetCampaignBehavior<T>() where T:class=>Behaviors.TryGetValue(typeof(T),out var x)?(T)x:null;}
 class MapState{}class Game{public static Game Current=new();public Manager GameStateManager=new();}class Manager{public object ActiveState=new object();}
 static class GwpCaseArchiveScreen{public static bool CanOpen=true;}static class GwpCommon{public static bool IsGreyWardenTroop(CharacterObject c)=>c.Grey;}
 class PlayerBountyBehavior{public bool IsRecruitedByGreyWardens=true,CanDispatchCaseReport=true,CanRequestCaseSupport=true;}
 static class GreyWardenPlayerRequestBehavior{public static bool Allowed=true;public static Action<string,double>? Selected;public static bool CanFileCourierAppeal()=>Allowed;public static bool CanFileCourierAppeal(string id,double hour)=>Allowed;public static void ShowCourierAppealInquiry(Action<string,double> selected){Selected=selected;}}
class GwpWardenDispatchBehavior{public string Fief="";public double Capture;public int Dispatches;public MobileParty? Dispatch(TroopRoster m,TroopRoster p,GwpDispatchPurpose purpose,int gold,bool lie,string hero,object? cargo=null,string orderTroopId="",int orderCount=0,int orderPrice=0,string fiefSettlementId="",double fiefCaptureHours=0){Fief=fiefSettlementId;Capture=fiefCaptureHours;Dispatches++;return new();}public HashSet<GwpDispatchPurpose> Active=new();public bool HasActiveDispatch(GwpDispatchPurpose p)=>Active.Contains(p);}
 static class GreyWardenTroopRequestBehavior{public static bool Allowed=true;public static bool CanFileCourierOrder()=>Allowed;public static void ShowCourierOrderInquiry(Action<string,int,int> done){}}
 static class PoliceAntiWarDeclaration{public static bool Allowed=true;public static bool HasMediationRequests()=>Allowed;}
 static class GwpDispatchBarterScreen{public static void Tick(){}}
 class TextObject{public TextObject(string text){}}static class GwpText{public static string Get(string k)=>k;public static TextObject Create(string k)=>new(k);}static class GwpTextCatalog{KEYS}
 class PartyScreenLogic{public enum TroopType{Member,Prisoner}}
 static class PartyScreenHelper{
 public delegate Tuple<bool,TextObject> Validate(TroopRoster lm,TroopRoster lp,TroopRoster rm,TroopRoster rp,int ll,int rl);
 public delegate void Closed(object lo,TroopRoster lm,TroopRoster lp,object ro,TroopRoster rm,TroopRoster rp,bool cancel);
 public delegate bool Filter(CharacterObject c,PartyScreenLogic.TroopType t,object s,object p);
 public static int Opens;public static TroopRoster? Selection,Prisoners;public static Closed? Callback;
 public static void OpenScreenWithDummyRosterWithMainParty(TroopRoster m,TroopRoster p,TextObject title,int limit,Validate validate,Closed closed,Filter filter,Func<bool> done){Opens++;Selection=m;Prisoners=p;Callback=closed;}
 public static void Cancel(){Callback!(new(),Selection!,Prisoners!,new(),new(),new(),true);}
 }
 static class GwpWardenDispatchDialogue{
 static CharacterObject? _requestedTroop,_activeTroop;static int _requestedAtTicks,_openedAtTicks;const int RequestExpiryMilliseconds=20000,OpenGraceMilliseconds=15000;static GwpDispatchPurpose _pendingPurpose;static bool _selectionQueued;static Action? _afterPayment;static TroopRoster? _reportSelection;
 static bool CanTalkToTroop(CharacterObject c)=>false;static TextObject BuildScreenName(GwpDispatchPurpose p)=>new("selection");static bool IsSendable(GwpDispatchPurpose p,CharacterObject c,PartyScreenLogic.TroopType t)=>true;
 static void Send(GwpDispatchPurpose p,TroopRoster m,TroopRoster pr,int a,bool lie,string h){Sent++;}static int Sent;static void SendTroopOrder(TroopRoster roster,string id,int count,int price){}static void AskWhatToHandOver(TroopRoster m,TroopRoster p){}
 METHODS
 public static void Reset(){_selectionQueued=false;_requestedTroop=_activeTroop=null;_afterPayment=null;_reportSelection=null;Sent=0;}public static bool Queued=>_selectionQueued;public static GwpDispatchPurpose Pending=>_pendingPurpose;
 }
 ROW
 class Program{
 static int checks,closes;static CharacterObject soldier=new();static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static void Setup(){Campaign.Current=new();Campaign.Current.Behaviors[typeof(PlayerBountyBehavior)]=new PlayerBountyBehavior();Campaign.Current.Behaviors[typeof(GwpWardenDispatchBehavior)]=new GwpWardenDispatchBehavior();MobileParty.MainParty=new();MobileParty.MainParty.MemberRoster.AddToCounts(soldier,10,false,2,200);Game.Current=new();GwpCaseArchiveScreen.CanOpen=true;PlayerEncounter.Current=null;GreyWardenTroopRequestBehavior.Allowed=PoliceAntiWarDeclaration.Allowed=GreyWardenPlayerRequestBehavior.Allowed=true;GwpWardenDispatchDialogue.Reset();PartyScreenHelper.Opens=0;closes=0;}
 static void Click(GwpAffairsDispatchVM row)=>((ViewModel)row).ExecuteCommand("COMMAND",new object[0]);
 static void Main(){
 foreach(GwpDispatchPurpose purpose in Enum.GetValues(typeof(GwpDispatchPurpose))){Setup();var row=new GwpAffairsDispatchVM(purpose,"send",()=>{closes++;});Check(row.IsEnabled,"valid action enabled");Click(row);Check(closes==1&&GwpWardenDispatchDialogue.Queued&&GwpWardenDispatchDialogue.Pending==purpose,"native ViewModel command queues correct purpose");GwpWardenDispatchDialogue.Pump();Check(PartyScreenHelper.Opens==0,"does not open party screen over affairs");Game.Current.GameStateManager.ActiveState=new MapState();GwpWardenDispatchDialogue.Pump();GwpWardenDispatchDialogue.Pump();Check(PartyScreenHelper.Opens==1&&!GwpWardenDispatchDialogue.Queued,"map opens real production selection exactly once");Check(MobileParty.MainParty.MemberRoster.TotalManCount==10,"click does not spend soldiers before selection");MobileParty.MainParty.MemberRoster.AddToCounts(soldier,-3,false,-1,-60);PartyScreenHelper.Selection!.AddToCounts(soldier,3,false,1,60);PartyScreenHelper.Cancel();var returned=MobileParty.MainParty.MemberRoster.GetTroopRoster().Single();Check(returned.Number==10&&returned.WoundedNumber==2&&returned.Xp==200&&PartyScreenHelper.Selection.TotalManCount==0,"production cancellation returns soldiers wounds and XP");}
 foreach(var block in new Action[]{()=>GwpCaseArchiveScreen.CanOpen=false,()=>MobileParty.MainParty.IsActive=false,()=>MobileParty.MainParty.MapEvent=new(),()=>PlayerEncounter.Current=new(),()=>MobileParty.MainParty.CurrentSettlement=new(),()=>Campaign.Current.CurrentMenuContext=new(),()=>MobileParty.MainParty.MemberRoster.Clear(),()=>soldier.Grey=false,()=>soldier.IsHero=true}){Setup();soldier.Grey=true;soldier.IsHero=false;block();var row=new GwpAffairsDispatchVM(GwpDispatchPurpose.Support,"send",()=>closes++);Check(!row.IsEnabled&&row.Reason.Length>0,"blocked row visibly explains qualification");Click(row);Check(closes==0&&!GwpWardenDispatchDialogue.Queued,"stale/native command cannot bypass disabled gate");}
 soldier.Grey=true;soldier.IsHero=false;
 foreach(GwpDispatchPurpose purpose in Enum.GetValues(typeof(GwpDispatchPurpose))){Setup();Campaign.Current.GetCampaignBehavior<GwpWardenDispatchBehavior>()!.Active.Add(purpose);Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(purpose)==GwpTextCatalog.gwp_affairs_dispatch_in_transit,"same-purpose trip has specific reason");}
 Setup();var bounty=Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()!;bounty.CanDispatchCaseReport=false;Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(GwpDispatchPurpose.Report)==GwpTextCatalog.gwp_affairs_dispatch_no_report,"report authority");bounty.CanRequestCaseSupport=false;Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(GwpDispatchPurpose.Support)==GwpTextCatalog.gwp_affairs_dispatch_no_support,"support authority");GreyWardenTroopRequestBehavior.Allowed=false;Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(GwpDispatchPurpose.TroopOrder)==GwpTextCatalog.gwp_affairs_dispatch_no_order,"order authority");PoliceAntiWarDeclaration.Allowed=false;Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(GwpDispatchPurpose.PeaceRequest)==GwpTextCatalog.gwp_affairs_dispatch_no_peace,"mediation authority");
 Setup();var stale=new GwpAffairsDispatchVM(GwpDispatchPurpose.Support,"send",()=>closes++);Check(stale.IsEnabled,"initial qualification");Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()!.CanRequestCaseSupport=false;Click(stale);Check(closes==0&&!GwpWardenDispatchDialogue.Queued&&stale.Reason.Length>0,"qualification rechecked at actual native command");
 Setup();GwpWardenDispatchDialogue.TryStartFromAffairs(GwpDispatchPurpose.Support);Check(GwpWardenDispatchDialogue.AffairsDispatchBlockReason(GwpDispatchPurpose.PeaceRequest)==GwpTextCatalog.gwp_affairs_dispatch_busy,"cannot overwrite queued dispatch");Setup();GwpWardenDispatchDialogue.TryStartFromAffairs(GwpDispatchPurpose.FiefAppeal);Game.Current.GameStateManager.ActiveState=new MapState();GwpWardenDispatchDialogue.Pump();MobileParty.MainParty.MemberRoster.AddToCounts(soldier,-3,false,-1,-60);PartyScreenHelper.Selection!.AddToCounts(soldier,3,false,1,60);PartyScreenHelper.Callback!(new(),PartyScreenHelper.Selection,PartyScreenHelper.Prisoners!,new(),new(),new(),false);Check(MobileParty.MainParty.MemberRoster.TotalManCount==10,"fief target inquiry keeps soldiers in main party");GwpWardenDispatchDialogue.Pump();GreyWardenPlayerRequestBehavior.Selected!("town",12.25);var sent=Campaign.Current.GetCampaignBehavior<GwpWardenDispatchBehavior>()!;Check(sent.Dispatches==1&&sent.Fief=="town"&&sent.Capture==12.25&&MobileParty.MainParty.MemberRoster.TotalManCount==7,"final fief choice deducts real escort and sends selected capture");GreyWardenPlayerRequestBehavior.Selected!("town",12.25);Check(sent.Dispatches==1&&MobileParty.MainParty.MemberRoster.TotalManCount==7,"duplicate fief confirmation cannot duplicate or deduct twice");Console.WriteLine("AFFAIRS_NATIVE_DISPATCH_ASSERTIONS="+checks);
 }}
}
'''.replace('METHODS', methods).replace('ROW', row).replace('KEYS', ''.join(f'public const string {k}="{k}";' for k in keys)).replace('COMMAND', command)
        with tempfile.TemporaryDirectory(prefix='gwp-native-affairs-dispatch-') as temp:
            p = Path(temp)
            refs = ''.join(f'<Reference Include="{n}"><HintPath>{escape(str(GAME_BIN / (n + ".dll")))}</HintPath></Reference>' for n in ['TaleWorlds.Library'])
            (p / 'Replay.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>{refs}</ItemGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            build = subprocess.run(['dotnet', 'build', str(p / 'Replay.csproj'), '-c', 'Release', '-v:q'], text=True, capture_output=True)
            self.assertEqual(0, build.returncode, build.stdout + build.stderr)
            run = subprocess.run([str(p / 'bin/Release/net472/Replay.exe')], text=True, capture_output=True)
            self.assertEqual(0, run.returncode, run.stdout + run.stderr)
            print(next(line for line in run.stdout.splitlines() if line.startswith('AFFAIRS_NATIVE_DISPATCH_ASSERTIONS=')))
