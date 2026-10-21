"""Execute production case selection/acceptance against controlled campaign boundaries.

Also validate the shipped prefab's binding scopes and scroll targets without
starting Bannerlord. Native rendering and menu input remain in-game acceptance.
"""
import json
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from test_duty_banner import method

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'


class AffairsTests(unittest.TestCase):
    def test_clan_easter_egg_bypasses_only_screen_entry(self):
        screen = (MODULE / 'GwpCaseArchiveScreen.cs').read_text(encoding='utf-8-sig')
        clan = (MODULE / 'GwpEncyclopediaClanPageVM.cs').read_text(encoding='utf-8-sig')
        gate = re.search(r'internal static bool CanOpen\s*=>.*?;', screen, re.S).group(0)
        methods = '\n'.join(method(screen, n) for n in ['Show', 'ShowFromClanPage', 'Open'])
        click = method(clan, 'ExecuteOpenCaseArchive')
        code = r'''
using System;
class PlayerBountyBehavior{public bool IsRecruitedByGreyWardens;}
enum CampaignTimeControlMode{Stop,UnstoppablePlay}
class Campaign{public static Campaign? Current=new();public PlayerBountyBehavior Member=new();public CampaignTimeControlMode TimeControlMode=CampaignTimeControlMode.UnstoppablePlay;public T? GetCampaignBehavior<T>() where T:class=>Member as T;}
static class GwpRuntimeState{public static PlayerData Player=new();public class PlayerData{public int Reputation;}}
static class GwpTuning{public static class Bounty{public const int RecruitmentReputationThreshold=20;}}
class GwpAffairsState{}
class StateManager{public object? ActiveState;public int Pushes;public T CreateState<T>() where T:new()=>new();public void PushState(object o){Pushes++;ActiveState=o;}}
class Game{public static Game? Current=new();public StateManager GameStateManager=new();}
static class GwpCaseArchiveScreen{static bool IsOpen=>Game.Current?.GameStateManager.ActiveState is GwpAffairsState;
GATE
METHODS
}
class ClanExtension{public bool IsVisible=true;CLICK public void Click()=>ExecuteOpenCaseArchive();}
class Program{static int checks;static void Check(bool c,string message){checks++;if(!c)throw new Exception(message);}static void Main(){
 foreach(bool joined in new[]{false,true})foreach(int rep in new[]{-10,0,19,20,40}){
  Campaign.Current=new();Campaign.Current.Member.IsRecruitedByGreyWardens=joined;GwpRuntimeState.Player.Reputation=rep;Game.Current=new();
  bool allowed=joined&&rep>=20;Check(GwpCaseArchiveScreen.CanOpen==allowed,"map navigation gate preserved");GwpCaseArchiveScreen.Show();Check(Game.Current.GameStateManager.Pushes==(allowed?1:0),"ordinary opening respects membership and reputation");
  Game.Current=new();Campaign.Current.TimeControlMode=CampaignTimeControlMode.UnstoppablePlay;new ClanExtension().Click();Check(Game.Current.GameStateManager.Pushes==1&&Campaign.Current.TimeControlMode==CampaignTimeControlMode.Stop,"Grey Warden clan Easter egg opens for every membership/reputation combination and pauses time");
  new ClanExtension().Click();Check(Game.Current.GameStateManager.Pushes==1,"Easter egg does not duplicate open state");
  Check(Campaign.Current.Member.IsRecruitedByGreyWardens==joined&&GwpRuntimeState.Player.Reputation==rep,"viewing does not enlist player or change reputation");
 }
 Game.Current=new();new ClanExtension{IsVisible=false}.Click();Check(Game.Current.GameStateManager.Pushes==0,"unrelated clans cannot open hidden entry");
 Campaign.Current=null;new ClanExtension().Click();Check(Game.Current.GameStateManager.Pushes==0,"no campaign does not open screen");Game.Current=null;new ClanExtension().Click();Check(true,"no game is safe");
 Console.WriteLine("AFFAIRS_CLAN_ENTRY_ASSERTIONS="+checks);
}}
'''.replace('GATE', gate).replace('METHODS', methods).replace('CLICK', click)
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-affairs-clan-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('AFFAIRS_CLAN_ENTRY_ASSERTIONS=')))

    def test_encyclopedia_overlay_owns_exit_and_tab_input(self):
        screen = (MODULE / 'GwpCaseArchiveScreen.cs').read_text(encoding='utf-8-sig')
        # The shared extractor recognizes public/internal methods; retain the original modifier in the harness.
        tick = method(screen.replace('protected override void OnFrameTick', 'public override void OnFrameTick'), 'OnFrameTick')
        tick = tick.replace('public override void OnFrameTick', 'protected override void OnFrameTick')
        code = r'''
using System;
static class ScreenManager{public static object? FocusedLayer;}
class Input{public string Released="";public bool IsHotKeyReleased(string key)=>key==Released;}
class Layer{public Input Input=new();}
class VM{public int Delta;public void SwitchTab(int d){Delta+=d;}}
class ScreenBase{protected virtual void OnFrameTick(float dt){}}
class Screen:ScreenBase{internal Layer? _layer=new();internal VM? _vm=new();internal int Closed;void Close(){Closed++;}public void Tick()=>OnFrameTick(0);
TICK
}
class Program{static int checks;static void Check(bool c){checks++;if(!c)throw new Exception("overlay input "+checks);}static void Main(){
var s=new Screen();s._layer!.Input.Released="Exit";ScreenManager.FocusedLayer=new object();s.Tick();Check(s.Closed==0);
s._layer.Input.Released="SwitchToNextTab";s.Tick();Check(s._vm!.Delta==0);
ScreenManager.FocusedLayer=s._layer;s.Tick();Check(s._vm.Delta==1);
s._layer.Input.Released="Confirm";s.Tick();Check(s.Closed==1);
s._layer.Input.Released="Exit";s.Tick();Check(s.Closed==2);
Console.WriteLine("AFFAIRS_OVERLAY_INPUT_ASSERTIONS="+checks);
}}
'''.replace('TICK', tick)
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-affairs-overlay-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('AFFAIRS_OVERLAY_INPUT_ASSERTIONS=')))

    def test_actual_game_input_context_supports_map_bar_panel_switching(self):
        screen = (MODULE / 'GwpCaseArchiveScreen.cs').read_text(encoding='utf-8-sig')
        categories = re.findall(r'\.Input\.RegisterHotKeyCategory\(HotKeyManager.GetCategory\("([^"]+)"\)\)', screen)
        # Execute the installed game's actual InputContext and key categories. No engine or game is started.
        script = r'''
param([string]$Registrations)
$ErrorActionPreference = 'Stop'
$gameBin = 'D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client'
Get-ChildItem $gameBin -Filter 'TaleWorlds.*.dll' | Where-Object Name -ne 'TaleWorlds.Native.dll' | ForEach-Object { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
$panel = [TaleWorlds.MountAndBlade.GenericPanelGameKeyCategory]::new('GenericPanelGameKeyCategory')
$campaign = [TaleWorlds.MountAndBlade.GenericCampaignPanelsGameKeyCategory]::new('GenericCampaignPanelsGameKeyCategory')
$known = @{ GenericPanelGameKeyCategory = $panel; GenericCampaignPanelsGameKeyCategory = $campaign }
$old = [TaleWorlds.InputSystem.InputContext]::new()
$old.RegisterHotKeyCategory($panel)
$reproduced = $false
try { [void]$old.IsGameKeyReleased(37) }
catch { if ($_.Exception.ToString().Contains('NullReferenceException')) { $reproduced = $true } else { throw } }
if (-not $reproduced) { throw 'The old screen must reproduce the observed map-bar null-reference failure.' }
$current = [TaleWorlds.InputSystem.InputContext]::new()
foreach ($name in (Get-Content -LiteralPath $Registrations -Raw | ConvertFrom-Json)) {
    if (-not $known.ContainsKey($name)) { throw "Unmapped production input category: $name" }
    $current.RegisterHotKeyCategory($known[$name])
}
$count = 1
foreach ($id in @(37, 43, 42, 38, 41, 40, 45)) {
    if ($current.IsGameKeyReleased($id)) { throw "Idle navigation key $id was unexpectedly released." }
    $count++
}
foreach ($name in @('Exit', 'SwitchToNextTab', 'SwitchToPreviousTab')) {
    if ($null -eq $panel.GetHotKey($name)) { throw "Missing own panel hotkey: $name" }
    if ($current.IsHotKeyReleased($name)) { throw "Idle hotkey $name was unexpectedly released." }
    $count++
}
Write-Output "AFFAIRS_NATIVE_INPUT_ASSERTIONS=$count"
'''
        with tempfile.TemporaryDirectory(prefix='gwp-affairs-input-') as temp:
            p = Path(temp)
            (p / 'keys.json').write_text(json.dumps(categories), encoding='utf-8')
            (p / 'test.ps1').write_text(script, encoding='utf-8-sig')
            result = subprocess.run(['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(p / 'test.ps1'), '-Registrations', str(p / 'keys.json')], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('AFFAIRS_NATIVE_INPUT_ASSERTIONS=')))

    def test_production_viewmodel_selection_refresh_and_acceptance_flow(self):
        vm = (MODULE / 'GwpAffairsVM.cs').read_text(encoding='utf-8-sig')
        data = (MODULE / 'GwpAffairsData.cs').read_text(encoding='utf-8-sig')
        entry = data[data.index('    internal sealed class GwpAffairsEntry'):data.index('    internal static class GwpAffairsText')]
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)', vm)))
        harness = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace TaleWorlds.Library{public class DataSourceProperty:Attribute{}public class ViewModel{public void OnPropertyChanged(string n){}public void OnPropertyChangedWithValue<T>(T v,string n){}public virtual void OnFinalize(){}}public class MBBindingList<T>:List<T>{}public class InquiryData{public Action Accept;public InquiryData(string title,string text,bool ok,bool cancel,string yes,string no,Action accept,Action reject){Accept=accept;}}public static class InformationManager{public static InquiryData Last;public static void ShowInquiry(InquiryData data){Last=data;}}}
namespace TaleWorlds.Core.ViewModelCollection.ImageIdentifiers{public class CharacterImageIdentifierVM{public static int Finalized;public object? Code;public CharacterImageIdentifierVM(object? c){Code=c;}public void OnFinalize(){Finalized++;}}}
namespace TaleWorlds.InputSystem{public class HotKey{}}
namespace TaleWorlds.CampaignSystem.ViewModelCollection.Input{public class InputKeyItemVM{public static InputKeyItemVM CreateFromHotKey(TaleWorlds.InputSystem.HotKey k,bool c)=>new();public void OnFinalize(){}}}
namespace TaleWorlds.CampaignSystem{
 public class TestParty{public string StringId="";public string Name="";public object GetPosition2D=new();public Text GetBehaviorText()=>new();}public class Text{public override string ToString()=>"moving";}
 public class Settlement{public string EncyclopediaLink="village";}public class Hero{public bool IsAlive=true;public int Age=30;public string StringId="hero",Name="hero",EncyclopediaLink="hero";public object CharacterObject=new();public TestParty? PartyBelongedTo;public Settlement? CurrentSettlement;}
 public class Clan{public List<Hero> Heroes=new();}
 public class Campaign{public static Campaign Current=new();public Models Models=new();public Encyclopedia EncyclopediaManager=new();public T? GetCampaignBehavior<T>() where T:class=>GreyWardenPolicePurity.PlayerBountyBehavior.Current as T;}
 public class Models{public AgeModel AgeModel=new();}public class AgeModel{public int HeroComesOfAge=18;}public class Encyclopedia{public string Last="";public void GoToLink(string s){Last=s;}}
}
namespace TaleWorlds.CampaignSystem.ViewModelCollection{public static class CampaignUIHelper{public static object? GetCharacterCode(object? c)=>c;}}
namespace TaleWorlds.CampaignSystem.Party{public class MobileParty{public static MobileParty MainParty=new();}}
namespace GreyWardenPolicePurity{
using TaleWorlds.CampaignSystem;
ENTRY
internal enum GwpDispatchPurpose{Report,Support,TroopOrder,PeaceRequest,FiefAppeal}
static class GwpWardenDispatchDialogue{public static string Block="";public static bool Accept=true;public static List<GwpDispatchPurpose> Sent=new();public static string AffairsDispatchBlockReason(GwpDispatchPurpose p)=>Block;public static bool TryStartFromAffairs(GwpDispatchPurpose p){if(Block.Length>0||!Accept)return false;Sent.Add(p);return true;}}
static class GwpTextCatalog{KEYS}
static class GwpText{public static string Get(string k,params object[] a)=>k;}
static class GwpCommon{public static Settlement? NormalizePlayerFacingSettlement(Settlement? s,object position)=>s;}
static class GwpAffairsText{public static string Pair(string a,string b)=>a+"|"+b;public static string Duty(GreyWardenFamilyBehavior.DutyKind d)=>d.ToString();public static string Line(string k,object v)=>k+":"+v;public static string TargetLocation(TestParty p,string? k=null)=>p.StringId;}
static class GwpTuning{public static class Bounty{public const int RecruitmentReputationThreshold=20;}}
static class GwpFireswornBehavior{public static bool CanManagePlayerArmory=true;public static int Opens;public static List<ArmoryItem> PlayerArmoryItems=new();public static bool OpenPlayerArmory(){if(!CanManagePlayerArmory)return false;Opens++;return true;}public static int ArmoryCapacity(TaleWorlds.CampaignSystem.Party.MobileParty p)=>100;public class ArmoryItem{public int Amount;}}
static class GwpRuntimeState{public static class Player{public static int Reputation=22;}}
static class GreyWardenFamilyBehavior{public enum DutyKind{LegacyOrdinary,Training,CaravanProtection,VillageProtection,IssueResolution,Reconstruction,PlayerRequests}public static bool TryGetDuty(Hero h,out DutyKind d){d=DutyKind.Training;return true;}public static string GetDutyTitle(Hero h)=>"Training";}
class CrimeRecord{public int Reward;public float Strength;}
static class CrimePool{public static Dictionary<string,CrimeRecord> Cases=new();public static CrimeRecord? GetRecordByKey(string id)=>Cases.TryGetValue(id,out var c)?c:null;}
static class GwpStrength{public static float CaseTarget(CrimeRecord c)=>c.Strength;}
static class GwpAffairsData{
 public static readonly GreyWardenFamilyBehavior.DutyKind[] Duties={GreyWardenFamilyBehavior.DutyKind.Training,GreyWardenFamilyBehavior.DutyKind.CaravanProtection,GreyWardenFamilyBehavior.DutyKind.VillageProtection,GreyWardenFamilyBehavior.DutyKind.IssueResolution,GreyWardenFamilyBehavior.DutyKind.Reconstruction,GreyWardenFamilyBehavior.DutyKind.PlayerRequests};
 public static List<GwpAffairsEntry> Items=new(),Faith=new(),Armory=new();public static List<GwpAffairsEntry> Read()=>Items.ToList();public static List<GwpAffairsEntry> ReadFaith()=>Faith.ToList();public static List<GwpAffairsEntry> ReadPlayerArmory()=>Armory.ToList();}
class PlayerBountyBehavior{public static PlayerBountyBehavior Current=new();public bool Ready=true,Accept=true;public string? Accepted;
 public bool CanWithdrawUnavailableCommission;public int Withdrawals;public bool WithdrawUnavailableCommission(){if(!CanWithdrawUnavailableCommission)return false;Withdrawals++;CanWithdrawUnavailableCommission=false;GwpAffairsData.Items.RemoveAll(e=>e.Id=="commission");return true;}
 public string GetBountyOfferStatusText()=>"ready";public bool CanInspectBountyOffers()=>Ready;
 public static int EstimateAffairsReward(CrimeRecord c)=>c.Reward;
 public bool TryAcceptAffairsCase(string id,int fine,int reward){if(!Ready||!Accept)return false;Accepted=id;GwpAffairsData.Items.Insert(0,new("commission","current","mine"));return true;}}
static class PoliceStats{public static Clan Clan=new();public static Clan GetPoliceClan()=>Clan;}
static class PoliceResourceManager{public static int GetJudicialTreasuryBalance()=>100;}
static class GwpPoliceWarReasonService{public static IEnumerable<int> GetCurrentWarReasonEntries()=>Array.Empty<int>();}
static class PoliceEnforcementBehavior{public class EnvoyReport{public string WardenHeroId="warden";}public static List<EnvoyReport> Reports=new();public static IReadOnlyList<EnvoyReport> GetEnvoyReports()=>Reports;}
class GwpCaseArchiveItemVM{public GwpCaseArchiveItemVM(int w){}public GwpCaseArchiveItemVM(PoliceEnforcementBehavior.EnvoyReport r){}public string Header="negotiation target",TimeText="report date",DetailsText="paid outcome 123",AssignmentText="war";}
static class GreyWardenVillageAdoptionBehavior{public class Status{public int LivingMembers=6,MaxMembers=15;}public static bool TryGetAdoptionOrigin(string id,out string n){n="village";return false;}public static bool TryGetAdoptionStatus(out Status s){s=new();return true;}}
class Replay{
static int count;static void Check(bool b,string why){if(!b)throw new Exception(why);count++;}
static void Main(){
 var trade=GreyWardenFamilyBehavior.DutyKind.CaravanProtection;
 var target=new Hero{Name="offender",StringId="offender"};
 GwpAffairsData.Items.Add(new("dispatch:1","couriers","courier"){IsPersonal=true});
 var pursuer=new Hero{Name="pursuer",StringId="pursuer"};
 for(int i=0;i<8;i++){string id="case"+i;GwpAffairsData.Items.Add(new(id,"trade",id){Duty=trade,Hero=target,IsCase=true,DistanceToPlayer=i*10,IsOffer=true,DisplayedFine=2000,DisplayedReward=400+i,Summary="pursuer: pursuing",DetailSummary="PLAYER FEE",Assignee=pursuer});CrimePool.Cases.Add(id,new(){Reward=400+i,Strength=100-i});}
 GwpAffairsData.Items.Single(e=>e.Id=="dispatch:1").DispatchPurpose=GwpDispatchPurpose.Report;
 int closes=0;var vm=new GwpAffairsVM(()=>closes++);
 Check(vm.Rows.Single().Id=="dispatch:1","opening without a commission defaults to the active player courier");
 Check(vm.Tabs.Count==5,"existing personal, member, duty, faith and armory sections");
 Check(vm.ShowBrowse&&vm.DispatchOptions.Count==0,"returning case courier remains visible with the commission entry available and no new report action");
 Check(!vm.Categories.Any(c=>c.Id=="world"||c.Id=="benefits"),"unneeded world and gift categories removed");
 Check(typeof(GwpAffairsVM).GetMethod("ExecuteActions")==null,"no generic affairs action command");
 Check(!vm.Categories.Any(c=>c.Id=="couriers"),"combined courier category removed");
 Check(vm.Categories.Select(c=>c.Id).SequenceEqual(new[]{"current","orders","mediation","requests"}),"personal categories group cases, troop orders, peace and fiefs by business");
 foreach(var pair in new[]{("orders",GwpDispatchPurpose.TroopOrder),("mediation",GwpDispatchPurpose.PeaceRequest),("requests",GwpDispatchPurpose.FiefAppeal)}){vm.Categories.First(c=>c.Id==pair.Item1).ExecuteSelect();Check(vm.DispatchOptions.Count==1,"each independent service page has exactly one purpose");vm.DispatchOptions.Single().ExecuteSend();Check(GwpWardenDispatchDialogue.Sent.Last()==pair.Item2,"service starts only its own purpose");}
 GwpWardenDispatchDialogue.Sent.Clear();closes=0;vm.Categories.First(c=>c.Id=="mediation").ExecuteSelect();
 vm.DispatchOptions[0].ExecuteSend();Check(closes==1&&GwpWardenDispatchDialogue.Sent.Single()==GwpDispatchPurpose.PeaceRequest,"peace button directly queues correct existing purpose and closes");closes=0;
 GwpWardenDispatchDialogue.Block="no eligible case";Check(!vm.DispatchOptions[0].IsEnabled&&vm.DispatchOptions[0].Reason=="no eligible case","disabled action explains exact reason");vm.DispatchOptions[0].ExecuteSend();Check(closes==0&&GwpWardenDispatchDialogue.Sent.Count==1,"changed qualification leaves screen open with reason");GwpWardenDispatchDialogue.Block="";
 GwpAffairsData.Items.RemoveAll(e=>e.Id.StartsWith("dispatch:"));vm.ExecuteRefresh();Check(vm.IsEmpty&&vm.DispatchOptions.Count==1,"empty service page still allows its own dispatch");
 GwpAffairsData.Items.Add(new("dispatch:peace","peace","peace courier"){DispatchPurpose=GwpDispatchPurpose.PeaceRequest});GwpAffairsData.Items.Add(new("dispatch:order","order","order courier"){DispatchPurpose=GwpDispatchPurpose.TroopOrder});vm.ExecuteRefresh();Check(vm.Rows.Single().Id=="dispatch:peace","peace page excludes other courier purposes");vm.Categories.First(c=>c.Id=="orders").ExecuteSelect();Check(vm.Rows.Single().Id=="dispatch:order","order page contains only its own courier");GwpAffairsData.Items.RemoveAll(e=>e.DispatchPurpose.HasValue);
 vm.Categories.First(c=>c.Id=="current").ExecuteSelect();Check(vm.ShowBrowse&&vm.DispatchOptions.Count==0,"empty current commission offers only the commission entry");
 vm.BrowseCases();Check(vm.Categories.Count==6&&vm.Rows.Count==8,"case browser has six offices and all available cases");
 Check(!vm.ShowBrowse&&vm.ShowRefresh&&vm.DispatchOptions.Count==0,"office case browser only offers relevant actions");
 Check(!vm.Categories.Any(c=>c.Id=="all"),"no crowded all-affairs category");Check(vm.Rows[0].Id=="case0","current offender distance takes precedence over reward and strength");
 Check(vm.Categories[0].Id=="CaravanProtection","offices with actual work precede empty offices");
 Check(!vm.Rows.Any(r=>r.Summary.Contains("PLAYER FEE"))&&vm.DetailSummary=="PLAYER FEE","personal fee is confined to the selected detail, while rows retain assignment progress");
 Check(vm.Rows[0].Portrait?.Code==target.CharacterObject&&vm.Rows[0].AssigneePortrait?.Code==pursuer.CharacterObject,"offender and pursuer have distinct portraits");
 GwpAffairsData.Items.Single(e=>e.Id=="case0").Hero=null;GwpAffairsData.Items.Single(e=>e.Id=="case0").IsOffer=false;vm.ExecuteRefresh();
 Check(vm.Rows[0].Id=="case0"&&vm.Rows[0].HasPortrait&&vm.Rows[0].Portrait!.Code==null&&vm.Rows[0].HasAssigneePortrait,"missing offender uses the native empty portrait without replacing it with the pursuer or sorting it below offers");
 GwpAffairsData.Items.Single(e=>e.Id=="case0").Hero=target;GwpAffairsData.Items.Single(e=>e.Id=="case0").IsOffer=true;
 vm.Rows.First(r=>r.Id=="case3").ExecuteSelect();vm.ExecuteRefresh();Check(vm.Rows.First(r=>r.Id=="case3").IsSelected,"refresh retains selected stable case identity");
 vm.ExecuteHero();Check(closes==0&&Campaign.Current.EncyclopediaManager.Last=="hero"&&vm.Rows.Single(r=>r.IsSelected).Id=="case3","hero encyclopedia overlays the current selection without closing to the map");
 GwpAffairsData.Items.Single(e=>e.Id=="case3").Place=new();vm.ExecuteRefresh();vm.ExecutePlace();Check(closes==0&&Campaign.Current.EncyclopediaManager.Last=="village"&&vm.Rows.Single(r=>r.IsSelected).Id=="case3","settlement encyclopedia keeps the current screen and row too");
 GwpAffairsData.Items.RemoveAll(e=>e.Id=="case3");vm.ExecuteRefresh();Check(vm.Rows[0].IsSelected,"withdrawn selected case picks a valid remaining row");
 PlayerBountyBehavior.Current.Ready=false;Check(!vm.CanAccept,"button eligibility is live after flag removal");vm.ExecuteAccept();Check(PlayerBountyBehavior.Current.Accepted==null&&vm.Status.Length>0,"stale acceptance refreshes in place without closing");
 PlayerBountyBehavior.Current.Ready=true;vm.ExecuteAccept();Check(PlayerBountyBehavior.Current.Accepted=="case0","selected full-list case reaches authority");Check(vm.Rows.Single().Id=="commission","successful acceptance opens the ongoing personal contract");
 Check(vm.DispatchOptions.Count==2,"commission details contain report and support dispatch actions");
 GwpWardenDispatchDialogue.Sent.Clear();foreach(var option in vm.DispatchOptions)option.ExecuteSend();Check(GwpWardenDispatchDialogue.Sent.SequenceEqual(new[]{GwpDispatchPurpose.Report,GwpDispatchPurpose.Support}),"commission actions dispatch the actual report and support purposes");closes=0;
 GwpAffairsData.Items.Add(new("support:active","support","assigned escort"){IsPersonal=true});GwpAffairsData.Items.Add(new("dispatch:report","report","report courier"){DispatchPurpose=GwpDispatchPurpose.Report});GwpAffairsData.Items.Add(new("dispatch:support","support","support courier"){DispatchPurpose=GwpDispatchPurpose.Support});GwpAffairsData.Items.Add(new("dispatch:fief","fief","fief courier"){DispatchPurpose=GwpDispatchPurpose.FiefAppeal});vm.ExecuteRefresh();
 Check(vm.Rows.Select(r=>r.Id).SequenceEqual(new[]{"commission","support:active","dispatch:report","dispatch:support"}),"commission, actual escort and both case couriers share the case category without fief courier or duplicate commission");
 vm.Rows.First(r=>r.Id=="dispatch:report").ExecuteSelect();Check(vm.DispatchOptions.Count==0&&!vm.ShowWithdrawCommission,"courier detail cannot dispatch another report or withdraw the commission");vm.ExecuteRefresh();Check(vm.Rows.Single(r=>r.IsSelected).Id=="dispatch:report","refresh retains selected case courier");vm.Rows.First(r=>r.Id=="commission").ExecuteSelect();
 GwpAffairsData.Items.RemoveAll(e=>e.Id=="support:active"||e.DispatchPurpose.HasValue);vm.ExecuteRefresh();
 Check(!vm.ShowBrowse&&vm.Standing==GwpTextCatalog.gwp_affairs_standing,"accepted commission has no browser shortcut or banner-mechanics explanation");
 Check(!vm.ShowWithdrawCommission,"free target commission has no exceptional withdrawal");
 PlayerBountyBehavior.Current.CanWithdrawUnavailableCommission=true;Check(vm.ShowWithdrawCommission,"externally captured commission exposes withdrawal only on current commission");
 vm.ExecuteWithdrawCommission();Check(PlayerBountyBehavior.Current.Withdrawals==0,"withdrawal waits for explicit confirmation");
 TaleWorlds.Library.InformationManager.Last.Accept();Check(PlayerBountyBehavior.Current.Withdrawals==1&&vm.ShowBrowse,"confirmed withdrawal releases commission and refreshes current page");
 Check(vm.Categories.Count==4&&!vm.Categories.Any(c=>c.Id=="report"||c.Id=="support"),"report and support do not duplicate the commission as separate categories");
 vm.Categories.First(c=>c.Id=="requests").ExecuteSelect();Check(vm.IsEmpty&&vm.EmptyText==GwpTextCatalog.gwp_affairs_fief_empty,"empty fief page explains the existing application entry");
 GwpAffairsData.Items.Add(new("support:1","support","case escort"){IsPersonal=true});GwpAffairsData.Items.Add(new("mediation:1","peace","faction"){IsPersonal=true});vm.ExecuteRefresh();
 vm.Categories.First(c=>c.Id=="current").ExecuteSelect();Check(vm.Rows.Single().Id=="support:1"&&vm.DispatchOptions.Count==0,"actual case escort belongs to commission and cannot start an unrelated case dispatch");
 vm.Categories.First(c=>c.Id=="mediation").ExecuteSelect();Check(vm.Rows.Single().Id=="mediation:1","post-battle peace excludes escort work");
 vm.Tabs[1].ExecuteSelect();Check(vm.Categories.Count==7&&!vm.Categories.Any(c=>c.Id=="other"),"adult offices plus children, no unassigned adult group");
 var warden=new Hero{Name="warden",StringId="warden"};PoliceStats.Clan.Heroes.Add(warden);
 var task=new GwpAffairsEntry("work-case","trade","real current case"){Assignee=warden,WorkSummary="envoy travelling",Summary="PLAYER FEE"};task.Fields.Add("PLAYER FEE AND TARGET STRENGTH");GwpAffairsData.Items.Add(task);
 PoliceEnforcementBehavior.Reports.Add(new());vm.ExecuteRefresh();
 Check(vm.Rows.Single().Title=="warden","only the member occupies the member roster");
 Check(vm.Sections.Single(s=>s.Title==GwpTextCatalog.gwp_affairs_work).Lines.Select(l=>l.Text).SequenceEqual(new[]{"real current case","envoy travelling"}),"current work uses assignment progress, not personal reward fields or report entries");
 Check(vm.Sections.Single(s=>s.Title==GwpTextCatalog.gwp_affairs_negotiations).Lines.Any(l=>l.Text=="paid outcome 123"),"historical negotiation shows its actual outcome in a separate named section");
 Check(!vm.Fields.Any(l=>l.Text.Contains("PLAYER FEE"))&&!vm.Sections.SelectMany(s=>s.Lines).Any(l=>l.Text.Contains("PLAYER FEE")),"member details do not copy a player's potential bounty into NPC work");
 task.IsPaused=true;task.WorkSummary="waiting for support";vm.ExecuteRefresh();
 Check(!vm.Sections.Any(s=>s.Title==GwpTextCatalog.gwp_affairs_work),"paused order is not presented as concurrent active work");
 Check(vm.Sections.Single(s=>s.Title==GwpTextCatalog.gwp_affairs_queued_work).Lines.Any(l=>l.Text=="waiting for support"),"paused order has an explicit pending section");
 Check(!vm.ShowBrowse,"member page has no commission shortcut");
 vm.Tabs[4].ExecuteSelect();Check(vm.IsEmpty&&!vm.ShowAccept&&vm.ShowManageArmory,"empty armory offers inventory management without manufacturing stock or commissions");
 Check(!vm.ShowRefresh&&!vm.ShowBrowse,"armory has no case actions");
 GwpFireswornBehavior.CanManagePlayerArmory=false;Check(!vm.CanManageArmory,"browsing an Easter egg does not grant armory management");vm.ExecuteManageArmory();Check(GwpFireswornBehavior.Opens==0,"locked armory cannot open native storage");
 GwpFireswornBehavior.CanManagePlayerArmory=true;vm.ExecuteManageArmory();Check(GwpFireswornBehavior.Opens==1,"armory opens its inventory authority");
 GwpAffairsData.Armory.Add(new("player-armory:0","armory","Fine sword"){Summary="In stock: 3"});vm.ExecuteRefresh();Check(vm.Rows.Single().Title=="Fine sword"&&!vm.HasHero&&!vm.HasPlace,"armory shows real stock without stale person or settlement actions");
 var resumed=new GwpAffairsVM(()=>{},4);Check(resumed.CurrentPage==4&&resumed.Rows.Single().Title=="Fine sword"&&resumed.ShowManageArmory,"returning from inventory reopens the armory page with new stock");
 vm.Tabs[3].ExecuteSelect();Check(vm.IsEmpty&&!vm.HasHero&&!vm.HasPlace,"empty congregation has no stale navigation target");
 Console.WriteLine("AFFAIRS_VM_ASSERTIONS="+count);
}}
}
'''
        harness = harness.replace('ENTRY', entry).replace('KEYS', '\n'.join(f'public const string {k}="{k}";' for k in keys))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-affairs-vm-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(harness, encoding='utf-8')
            (p / 'VM.cs').write_text(vm, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('AFFAIRS_VM_ASSERTIONS=')))

    def test_live_offer_authority_and_stale_contract(self):
        source = (MODULE / 'PlayerBountyBehavior.DialogueAndNotification.cs').read_text(encoding='utf-8-sig')
        methods = '\n'.join(method(source, n) for n in ['GetAffairsOffers', 'EstimateAffairsReward', 'TryAcceptAffairsCase'])
        assignment = method((MODULE / 'GwpPlayerCaseAssignment.cs').read_text(encoding='utf-8-sig'), 'CanTakeCase') + method((MODULE / 'GwpPlayerCaseAssignment.cs').read_text(encoding='utf-8-sig'), 'CanPlayerTakeCase')
        pool = method((MODULE / 'GwpData.cs').read_text(encoding='utf-8-sig'), 'GetAvailablePlayerBounties')
        reward = method((MODULE / 'GwpCaseSettlementRules.cs').read_text(encoding='utf-8-sig'), 'Reward')
        tuning = (MODULE / 'GwpTuning.cs').read_text(encoding='utf-8-sig')
        keys = sorted(set(re.findall(r'GwpTuning\.FieldArrest\.(\w+)', reward)))
        tuning_members = '\n'.join(re.search(r'public const int ' + k + r'\s*=.*?;', tuning).group() for k in keys)
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
public enum GwpCrimeCategory { Unknown,CaravanAttack,VillageViolence,PlayerCase }
public class CrimeRecord {public string CrimeId="";public GwpCrimeCategory CrimeCategory;public bool HasOpenCase=true,Pursuable=true,Agreement;public int Fine=2000,Severity;public object? OffenderHero;public bool IsOffenderPursuable()=>Pursuable;public bool IsOffenderSearchableInSettlement()=>false;}
public class History{public int NegativeStanding;}
public static class CrimePool {public const string PlayerCrimeId="player";internal static List<CrimeRecord> _ledgerList=new();internal static Dictionary<string,CrimeRecord> _ledger=>_ledgerList.ToDictionary(c=>c.CrimeId);public static History? GetHistory(object? o)=>o as History; POOL }
static class CrimeState{public static List<CrimeRecord> GetAvailablePlayerBounties()=>CrimePool.GetAvailablePlayerBounties();}
static class PoliceEnforcementBehavior{public static bool HasEnvoyAgreement(CrimeRecord c)=>c.Agreement;}
static class GwpPlayerCaseAssignment { ASSIGNMENT }
static class GwpFieldArrestPricing{public static int AssessFine(CrimeRecord c)=>c.Fine;}
static class GwpTuning{internal static class FieldArrest { TUNING }}
static class GwpCaseSettlementRules{ REWARD }
public class PlayerBountyBehavior {
public bool Eligible=true,Busy;public CrimeRecord? Accepted;public int FineAccepted;
internal bool CanInspectBountyOffers()=>Eligible&&!Busy&&GetAffairsOffers().Count>0;
void AcceptBounty(CrimeRecord c,int fine){Accepted=c;FineAccepted=fine;Busy=true;}
METHODS
}
class Program{
static int count;static void Check(bool b,string msg){if(!b)throw new Exception(msg);count++;}
static CrimeRecord Add(string id,GwpCrimeCategory kind,int severity){var h=new History{NegativeStanding=severity};var c=new CrimeRecord{CrimeId=id,CrimeCategory=kind,Severity=severity,OffenderHero=h};CrimePool._ledgerList.Add(c);return c;}
static void Main(){
 var b=new PlayerBountyBehavior();
 for(int i=0;i<8;i++)Add("case"+i,i%2==0?GwpCrimeCategory.CaravanAttack:GwpCrimeCategory.VillageViolence,i+1);
 Add("unknown",GwpCrimeCategory.Unknown,99);Add("player",GwpCrimeCategory.PlayerCase,99);
 var cases=b.GetAffairsOffers();Check(cases.Count==8,"all eight eligible cases, not a three-choice sample");
 Check(cases[0].CrimeId=="case7","rank by personal fee rather than fine cap");
 var c=cases[0];int fee=PlayerBountyBehavior.EstimateAffairsReward(c);
 Check(fee==1360&&fee<c.Fine,"personal reward follows the real settlement formula and differs from fine");
 b.Eligible=false;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee)&&b.Accepted==null,"membership/flag/standing invalidated after selection");b.Eligible=true;
 b.Busy=true;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"a concurrent accepted commission blocks another");b.Busy=false;
 c.Agreement=true;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"new envoy agreement cannot be stolen by a stale selected row");c.Agreement=false;
 c.HasOpenCase=false;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"closed case rejected");c.HasOpenCase=true;
 c.Pursuable=false;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"temporarily missing offender rejected");c.Pursuable=true;
 c.Fine++;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine-1,fee),"stale fine rejected");c.Fine--;
 ((History)c.OffenderHero!).NegativeStanding++;Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"changed fee at the same fine cap rejected");((History)c.OffenderHero!).NegativeStanding--;
 Check(!b.TryAcceptAffairsCase("missing",2000,fee),"removed identity rejected");
 Check(!b.TryAcceptAffairsCase("unknown",2000,2000),"non-case category is not made available through this UI");
 Check(b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee)&&ReferenceEquals(b.Accepted,c)&&b.FineAccepted==c.Fine,"same authoritative case reaches the existing acceptance transaction");
 Check(!b.TryAcceptAffairsCase(c.CrimeId,c.Fine,fee),"double-click cannot take a second contract");
 Check(GwpCaseSettlementRules.Reward(100,0,99)==100,"actual fee cap retained");
 Console.WriteLine("AFFAIRS_AUTHORITY_ASSERTIONS="+count);
}}
}
'''
        code = code.replace('METHODS', methods).replace('ASSIGNMENT', assignment).replace('POOL', pool).replace('REWARD', reward).replace('TUNING', tuning_members)
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-affairs-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('AFFAIRS_AUTHORITY_ASSERTIONS=')))

    def test_shipped_prefab_bindings_and_independent_scroll_panes(self):
        root = ET.parse(MODULE / '_Module/GUI/Prefabs/GwpCaseArchive.xml').getroot()
        vm = (MODULE / 'GwpAffairsVM.cs').read_text(encoding='utf-8-sig')
        vm_names = set(re.findall(r'public\s+(?:[\w<>?]+)\s+(\w+)\s*(?:\{|=>|\()', vm))
        image_names = {'AdditionalArgs', 'Id', 'TextureProviderName'}
        for element in root.iter():
            for attr, value in element.attrib.items():
                if value.startswith('@'):
                    self.assertIn(value[1:], vm_names | image_names, (element.tag, attr, value))
                if attr.startswith('Command.'):
                    self.assertIn(value, vm_names, (element.tag, attr, value))
        regions = [n for n in root.iter('Widget') if n.get('Id', '').endswith('Region')]
        self.assertEqual(3, len(regions))
        for panel in root.iter('ScrollablePanel'):
            ids = {e.get('Id') for e in panel.iter()}
            self.assertIn(panel.get('ClipRect'), ids)
            self.assertIn(panel.get('InnerPanel').split('\\')[-1], ids)
        texts = json.loads((MODULE / 'PlayerText/strings.json').read_text(encoding='utf-8-sig'))['strings']
        for key in re.findall(r'GwpTextCatalog\.(gwp_affairs_\w+)', vm):
            self.assertTrue(texts[key]['en'] and texts[key]['zh'], key)
        # Native header/footer and page-scoped actions address the reported overlap and styling.
        footer = root.find('.//Standard.DialogCloseButtons')
        self.assertIsNotNone(footer)
        self.assertEqual('ExecuteClose', footer.get('Parameter.DoneButtonAction'))
        self.assertEqual('false', footer.get('Parameter.ShowCancel'))
        self.assertEqual('@Title', root.find('.//Standard.TopPanel').get('Parameter.Title'))
        self.assertFalse(any(e.get('Id') == 'ListActions' for e in root.iter()))
        browse = next(e for e in root.iter('ButtonWidget') if e.get('Command.Click') == 'ExecuteTakeBounty')
        self.assertEqual('@ShowBrowse', browse.get('IsVisible'))
        refresh = next(e for e in root.iter('ButtonWidget') if e.get('Command.Click') == 'ExecuteRefresh')
        self.assertEqual('@ShowRefresh', refresh.get('IsVisible'))
        self.assertEqual('24', refresh.get('MarginTop'))
        tabs = [e for e in root.iter('ButtonWidget') if e.get('Brush', '').startswith('Header.Tab.')]
        self.assertEqual(3, len(tabs))
        for button in tabs:
            self.assertEqual('!'+button.get('Brush')+'.Width.Scaled', button.get('SuggestedWidth'))
            self.assertEqual('!'+button.get('Brush')+'.Height.Scaled', button.get('SuggestedHeight'))
        self.assertFalse(any(e.get('Command.Click') == 'ExecuteAppendix' for e in root.iter()))
        self.assertFalse(any(e.get('Brush') == 'ButtonBrush3' for e in root.iter()))
        for text in root.iter('TextWidget'):
            if text.get('HeightSizePolicy') == 'CoverChildren':
                self.assertEqual('true', text.get('CanBreakWords'))
        portraits = list(root.iter('ImageIdentifierWidget'))
        parents = {child: parent for parent in root.iter() for child in parent}
        self.assertEqual({'{Portrait}', '{AssigneePortrait}'}, {p.get('DataSource') for p in portraits})
        for portrait in portraits:
            frame = parents[parents[portrait]]
            self.assertEqual('Clan\\portrait_slot_right_panel', frame.get('Sprite'))
            self.assertAlmostEqual(113 / 84, float(frame.get('SuggestedWidth')) / float(frame.get('SuggestedHeight')))
            self.assertTrue(all(float(portrait.get('Margin' + side)) > 0 for side in ('Left', 'Right', 'Top', 'Bottom')))
        row = root.find('.//ListPanel[@Id="AffairList"]//ButtonWidget')
        self.assertEqual('CoverChildren', row.get('HeightSizePolicy'))
        row_summary = next(e for e in row.iter('TextWidget') if e.get('Text') == '@Summary')
        self.assertEqual('CoverChildren', row_summary.get('HeightSizePolicy'))
        self.assertEqual('@SummaryRightInset', row_summary.get('MarginRight'))


if __name__ == '__main__':
    unittest.main()
