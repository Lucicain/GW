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
    def test_production_viewmodel_selection_refresh_and_acceptance_flow(self):
        vm = (MODULE / 'GwpAffairsVM.cs').read_text(encoding='utf-8-sig')
        data = (MODULE / 'GwpAffairsData.cs').read_text(encoding='utf-8-sig')
        entry = data[data.index('    internal sealed class GwpAffairsEntry'):data.index('    internal static class GwpAffairsText')]
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)', vm)))
        harness = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace TaleWorlds.Library{public class DataSourceProperty:Attribute{}public class ViewModel{public void OnPropertyChanged(string n){}public void OnPropertyChangedWithValue<T>(T v,string n){}public virtual void OnFinalize(){}}public class MBBindingList<T>:List<T>{}}
namespace TaleWorlds.Core.ViewModelCollection.ImageIdentifiers{public class CharacterImageIdentifierVM{public static int Finalized;public CharacterImageIdentifierVM(object c){}public void OnFinalize(){Finalized++;}}}
namespace TaleWorlds.CampaignSystem{
 public class Party{public string StringId="";public string Name="";public Text GetBehaviorText()=>new();}public class Text{public override string ToString()=>"moving";}
 public class Settlement{public string EncyclopediaLink="village";}public class Hero{public bool IsAlive=true;public int Age=30;public string StringId="hero",Name="hero",EncyclopediaLink="hero";public object CharacterObject=new();public Party? PartyBelongedTo;public Settlement? CurrentSettlement;}
 public class Clan{public List<Hero> Heroes=new();}
 public class Campaign{public static Campaign Current=new();public Models Models=new();public Encyclopedia EncyclopediaManager=new();public T? GetCampaignBehavior<T>() where T:class=>GreyWardenPolicePurity.PlayerBountyBehavior.Current as T;}
 public class Models{public AgeModel AgeModel=new();}public class AgeModel{public int HeroComesOfAge=18;}public class Encyclopedia{public string Last="";public void GoToLink(string s){Last=s;}}
}
namespace TaleWorlds.CampaignSystem.ViewModelCollection{public static class CampaignUIHelper{public static object GetCharacterCode(object c)=>c;}}
namespace GreyWardenPolicePurity{
using TaleWorlds.CampaignSystem;
ENTRY
static class GwpTextCatalog{KEYS}
static class GwpText{public static string Get(string k,params object[] a)=>k;}
static class GwpAffairsText{public static string Pair(string a,string b)=>a+"|"+b;public static string Duty(GreyWardenFamilyBehavior.DutyKind d)=>d.ToString();public static string Line(string k,object v)=>k+":"+v;public static string TargetLocation(Party p)=>p.StringId;}
static class GwpRuntimeState{public static class Player{public static int Reputation=22;}}
static class GreyWardenFamilyBehavior{public enum DutyKind{LegacyOrdinary,Training,CaravanProtection,VillageProtection,IssueResolution,Reconstruction,PlayerRequests}public static bool TryGetDuty(Hero h,out DutyKind d){d=DutyKind.Training;return true;}public static string GetDutyTitle(Hero h)=>"Training";}
class CrimeRecord{public int Reward;public float Strength;}
static class CrimePool{public static Dictionary<string,CrimeRecord> Cases=new();public static CrimeRecord? GetRecordByKey(string id)=>Cases.TryGetValue(id,out var c)?c:null;}
static class GwpStrength{public static float CaseTarget(CrimeRecord c)=>c.Strength;}
static class GwpAffairsData{
 public static readonly GreyWardenFamilyBehavior.DutyKind[] Duties={GreyWardenFamilyBehavior.DutyKind.Training,GreyWardenFamilyBehavior.DutyKind.CaravanProtection,GreyWardenFamilyBehavior.DutyKind.VillageProtection,GreyWardenFamilyBehavior.DutyKind.IssueResolution,GreyWardenFamilyBehavior.DutyKind.Reconstruction,GreyWardenFamilyBehavior.DutyKind.PlayerRequests};
 public static List<GwpAffairsEntry> Items=new(),Faith=new();public static List<GwpAffairsEntry> Read()=>Items.ToList();public static List<GwpAffairsEntry> ReadFaith()=>Faith.ToList();}
class PlayerBountyBehavior{public static PlayerBountyBehavior Current=new();public bool Ready=true,Accept=true;public string? Accepted;
 public string GetBountyOfferStatusText()=>"ready";public bool CanInspectBountyOffers()=>Ready;
 public static int EstimateAffairsReward(CrimeRecord c)=>c.Reward;
 public bool TryAcceptAffairsCase(string id,int fine,int reward){if(!Ready||!Accept)return false;Accepted=id;GwpAffairsData.Items.Insert(0,new("commission","current","mine"));return true;}}
static class PoliceStats{public static Clan Clan=new();public static Clan GetPoliceClan()=>Clan;}
static class PoliceResourceManager{public static int GetJudicialTreasuryBalance()=>100;}
static class GwpPoliceWarReasonService{public static IEnumerable<int> GetCurrentWarReasonEntries()=>Array.Empty<int>();}
class GwpCaseArchiveItemVM{public GwpCaseArchiveItemVM(int w){}public string AssignmentText="war";}
static class GreyWardenVillageAdoptionBehavior{public class Status{public int LivingMembers=6,MaxMembers=15;}public static bool TryGetAdoptionOrigin(string id,out string n){n="village";return false;}public static bool TryGetAdoptionStatus(out Status s){s=new();return true;}}
class Replay{
static int count;static void Check(bool b,string why){if(!b)throw new Exception(why);count++;}
static void Main(){
 var trade=GreyWardenFamilyBehavior.DutyKind.CaravanProtection;
 var target=new Hero{Name="offender",StringId="offender"};
 GwpAffairsData.Items.Add(new("dispatch:1","couriers","courier"){IsPersonal=true});
 for(int i=0;i<8;i++){string id="case"+i;GwpAffairsData.Items.Add(new(id,"trade",id){Duty=trade,Hero=target,IsOffer=true,DisplayedFine=2000,DisplayedReward=400+i});CrimePool.Cases.Add(id,new(){Reward=400+i,Strength=100-i});}
 var vm=new GwpAffairsVM(()=>{});
 Check(vm.Rows.Single().Id=="dispatch:1","opening without a commission defaults to the active player courier");
 Check(vm.Tabs.Count==5,"existing personal, member, duty, faith and armory sections");
 vm.BrowseCases();Check(vm.Categories.Count==6&&vm.Rows.Count==8,"case browser has six offices and all available cases");
 Check(!vm.Categories.Any(c=>c.Id=="all"),"no crowded all-affairs category");Check(vm.Rows[0].Id=="case7","highest personal fee first");
 vm.Rows.First(r=>r.Id=="case3").ExecuteSelect();vm.ExecuteRefresh();Check(vm.Rows.First(r=>r.Id=="case3").IsSelected,"refresh retains selected stable case identity");
 GwpAffairsData.Items.RemoveAll(e=>e.Id=="case3");vm.ExecuteRefresh();Check(vm.Rows[0].IsSelected,"withdrawn selected case picks a valid remaining row");
 PlayerBountyBehavior.Current.Ready=false;Check(!vm.CanAccept,"button eligibility is live after flag removal");vm.ExecuteAccept();Check(PlayerBountyBehavior.Current.Accepted==null&&vm.Status.Length>0,"stale acceptance refreshes in place without closing");
 PlayerBountyBehavior.Current.Ready=true;vm.ExecuteAccept();Check(PlayerBountyBehavior.Current.Accepted=="case7","selected full-list case reaches authority");Check(vm.Rows.Single().Id=="commission","successful acceptance opens the ongoing personal contract");
 vm.Tabs[1].ExecuteSelect();Check(vm.Categories.Count==7&&!vm.Categories.Any(c=>c.Id=="other"),"adult offices plus children, no unassigned adult group");
 vm.Tabs[4].ExecuteSelect();Check(vm.IsEmpty&&!vm.ShowAccept,"unimplemented player armory cannot manufacture items or commissions");
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
        assignment = method((MODULE / 'GwpPlayerCaseAssignment.cs').read_text(encoding='utf-8-sig'), 'CanTakeCase')
        pool = method((MODULE / 'GwpData.cs').read_text(encoding='utf-8-sig'), 'GetAvailablePlayerBounties')
        reward = method((MODULE / 'GwpCaseSettlementRules.cs').read_text(encoding='utf-8-sig'), 'Reward')
        tuning = (MODULE / 'GwpTuning.cs').read_text(encoding='utf-8-sig')
        keys = sorted(set(re.findall(r'GwpTuning\.FieldArrest\.(\w+)', reward)))
        tuning_members = '\n'.join(re.search(r'public const int ' + k + r'\s*=.*?;', tuning).group() for k in keys)
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
namespace GreyWardenPolicePurity {
public enum GwpCrimeCategory { Unknown,CaravanAttack,VillageViolence,PlayerCase }
public class CrimeRecord {public string CrimeId="";public GwpCrimeCategory CrimeCategory;public bool HasOpenCase=true,Pursuable=true,Agreement;public int Fine=2000,Severity;public object? OffenderHero;public bool IsOffenderPursuable()=>Pursuable;}
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


if __name__ == '__main__':
    unittest.main()
