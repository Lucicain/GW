"""Production duty gating, banner ownership and explicit issue with native slot/pool bodies.

Campaign objects, UI display and inventories are controlled boundaries. No game
or renderer is started; native managed methods are freshly decompiled locally.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from test_battle_peace import extract

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'
GAME = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord')


def method(source, name):
    match = re.search(r'(?m)^\s*(?:private|internal|public)[^\n]*\b' + name + r'\s*\(', source)
    if not match:
        raise AssertionError(name)
    brace, arrow = source.find('{', match.end()), source.find('=>', match.end())
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[match.start():source.index(';', arrow) + 1]
    return extract(source, name)


HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;using System.Reflection;
namespace GreyWardenPolicePurity {
public class Banner{public string Code="own";public Banner(){}public Banner(Banner b){Code=b.Code;}} public class Clan{public static Clan PlayerClan=new();public Banner Banner=new();public List<WarParty> WarPartyComponents=new();}public class WarParty{public PartyBase Party=new();}
public class ItemObject{public string StringId="";public bool IsBannerItem,NotMerchandise;}
public enum EquipmentIndex{ExtraWeaponSlot=4,Head=5,Body=6}
public struct EquipmentElement{public ItemObject? Item;public EquipmentElement(ItemObject? i){Item=i;}}
public class Equipment{EquipmentElement[] slots=new EquipmentElement[12];public EquipmentElement this[EquipmentIndex i]{get=>slots[(int)i];set=>slots[(int)i]=value;}}
public class Hero{public static Hero? MainHero=new();public Equipment BattleEquipment=new(),CivilianEquipment=new(); HERO_BANNER }
public struct ItemRosterElement{public EquipmentElement EquipmentElement;public int Amount;}
public class Roster:List<ItemRosterElement>{public void AddToCounts(EquipmentElement e,int n){if(n>=0){for(int i=0;i<n;i++)Add(new(){EquipmentElement=e,Amount=1});}else{for(int i=0;i>n;i--){int index=FindIndex(v=>v.EquipmentElement.Item==e.Item);if(index>=0)RemoveAt(index);}}}}
public class MobileParty{public static MobileParty? MainParty=new();public Banner Banner=new();public Roster ItemRoster=new();}
public class PartyBase{public static PartyBase MainParty=new();public int Dirties;public void SetVisualAsDirty(){Dirties++;}}
public class PartyAgentOrigin{public PartyBase? Party;public Banner Banner=new();}
public class BannerEditorState{}public class GameStateManager{public object? ActiveState;}public class Game{public static Game? Current=new();public GameStateManager GameStateManager=new();}
public class Campaign{public static Campaign? Current=new();public PlayerBountyBehavior? Behavior;public T? GetCampaignBehavior<T>() where T:class=>Behavior as T;}
static class PoliceStats{public static Clan? Police=new();public static Clan? GetPoliceClan()=>Police;}
static class Items{public static List<ItemObject> All=new();}
static class Quick{public static IEnumerable<T> WhereQ<T>(this IEnumerable<T> s,Func<T,bool> f)=>s.Where(f);}
public abstract class BannerItemModel{public abstract IEnumerable<ItemObject> GetPossibleRewardBannerItems();public abstract bool CanBannerBeUpdated(ItemObject item);}
public class DefaultBannerItemModel:BannerItemModel{ NATIVE_POOL }
enum MethodType{Getter}class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,MethodType m){}}
static class GwpCommon{public static bool LordConversation=true;public static bool IsOrdinaryGreyWardenLordConversation()=>LordConversation; COMMON_METHODS }
static class GwpTuning{public static class Bounty{public const int RecruitmentReputationThreshold=20,MaximumVoluntaryExits=3;}}
static class PlayerState{public static int Reputation=20;}
static class CrimeState{public static List<int> Available=new(){1};public static List<int> GetAvailablePlayerBounties()=>Available;}
static class GwpText{public static string Get(string k,params object[] args)=>k;}
static class GwpTextCatalog{ TEXT_KEYS }
class MBObjectManager{public static MBObjectManager Instance=new();public ItemObject? Item;public T GetObject<T>(string id) where T:class=>(Item?.StringId==id?Item as T:null)!;}
public class PlayerBountyBehavior{
 public bool _recruitmentAccepted=true,HasBountyTask,_recruitmentOffered,HasFieldBusinessToSettle;public int _voluntaryExitCount,GearGrants,RecruitGrants,QuestEnds;public bool IsRecruitedByGreyWardens=>_recruitmentAccepted;
 public List<int> GetAffairsOffers()=>CrimeState.Available;
void DestroyRecruitmentPatrol(){}void GiveCommanderEquipment(){GearGrants++;}void GiveStarterRecruits(){RecruitGrants++;}void EndBountyTaskState(bool tryRestorePeace){QuestEnds++;HasBountyTask=false;}Quest? _activeQuest;class Quest{public void FailQuestMembershipEnded(){}}
 public void Join()=>OnRejoinThroughLord();public void Leave()=>OnLeaveThroughLord();
 BOUNTY_METHODS
 public bool Ready()=>CanInspectBountyOffers();public void Issue()=>IssueDutyBanner();public bool CanIssue()=>CanRequestDutyBanner();
}
 IDS
 BANNER_CLASSES
class Replay{static int Checks;static void Check(bool b,string why){if(!b)throw new Exception(why);Checks++;}
 static Banner Emblem(PartyBase? party,Banner original){var args=new object?[]{new PartyAgentOrigin{Party=party},original};typeof(GwpDutyBannerPartyEmblemPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);return (Banner)args[1]!;}
 static void Main(){
  var b=new PlayerBountyBehavior();Campaign.Current!.Behavior=b;var hero=Hero.MainHero!;var duty=new ItemObject{StringId=GwpIds.DutyBannerItemId,IsBannerItem=true,NotMerchandise=true};MBObjectManager.Instance.Item=duty;
  Check(!GwpIds.MembershipGrantItemIds.Contains(duty.StringId),"equipment grant excludes separately handed duty credential");
  Check(!b.Ready(),"no flag equipped blocks new warrants");MobileParty.MainParty!.ItemRoster.AddToCounts(new(duty),1);
  Check(!b.Ready()&&!GwpCommon.IsPlayerGreyWardenOnDuty(),"inventory possession alone keeps player off duty");
  hero.BattleEquipment[EquipmentIndex.Head]=new(duty);Check(!b.Ready(),"wrong slot cannot opt in");hero.BattleEquipment[EquipmentIndex.Head]=default;
  hero.BannerItem=new(duty);Check(hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item==duty&&b.Ready(),"native Hero.BannerItem uses actual banner slot and unlocks offers without commander armor");
  var own=new Banner();Check(Emblem(PartyBase.MainParty,own).Code==PoliceStats.Police!.Banner.Code,"equipped member main-party troops use Grey Warden emblem");
  Check(Emblem(new PartyBase(),own)==own&&Emblem(null,own)==own,"allies enemies and no party preserve their banners");
  b._recruitmentAccepted=false;Check(!b.Ready()&&Emblem(PartyBase.MainParty,own)==own,"nonmember cannot use flag to gain warrants or emblem");b._recruitmentAccepted=true;
  PlayerState.Reputation=19;Check(!b.Ready(),"standing threshold still applies");PlayerState.Reputation=20;
  b.HasBountyTask=true;hero.BannerItem=default;Check(b.HasBountyTask&&!b.Ready(),"unequipping does not cancel accepted warrant and blocks new ones");Check(Emblem(PartyBase.MainParty,own)==own,"unequipping restores original emblem without rewriting clan");
  b.HasBountyTask=false;hero.BannerItem=new(new(){StringId="ordinary_banner"});Check(!b.Ready(),"ordinary banner cannot opt in");
  hero.BannerItem=new(duty);CrimeState.Available.Clear();Check(!b.Ready(),"empty pool cannot open an offer");CrimeState.Available.Add(1);

  hero.BannerItem=default;Check(!b.CanIssue(),"inventory copy suppresses replacement");MobileParty.MainParty.ItemRoster.Clear();Check(b.CanIssue(),"member may explicitly replace lost flag");b.Issue();b.Issue();Check(MobileParty.MainParty.ItemRoster.Count==1,"repeated issue calls cannot duplicate flag in current party inventory");
  Check(hero.BannerItem.Item==null,"issuing flag does not silently activate duty");MobileParty.MainParty.ItemRoster.Clear();b._recruitmentAccepted=false;b.Issue();Check(MobileParty.MainParty.ItemRoster.Count==0,"nonmember cannot issue flag");b._recruitmentAccepted=true;
  GwpCommon.LordConversation=false;b.Issue();Check(MobileParty.MainParty.ItemRoster.Count==0,"cannot issue from unrelated or enforcement conversation");GwpCommon.LordConversation=true;
  hero.BannerItem=new(duty);Check(!b.CanIssue(),"equipped copy suppresses replacement");
  MBObjectManager.Instance.Item=null;hero.BannerItem=default;Check(!b.CanIssue(),"missing registered item cannot promise issuance");
  Items.All=new(){duty,new(){StringId="ordinary_banner",IsBannerItem=true,NotMerchandise=true},new(){StringId="campaign_banner_small",IsBannerItem=true},new(){StringId="sword"}};
  Check(new DefaultBannerItemModel().GetPossibleRewardBannerItems().Contains(duty),"native reward pool includes even nonmerchandise custom banners");
  var model=new GwpDutyBannerItemModel();var rewards=model.GetPossibleRewardBannerItems().ToList();Check(rewards.Count==1&&rewards[0].StringId=="ordinary_banner","production model excludes duty credential while preserving other original exclusions");
  Check(!model.CanBannerBeUpdated(duty)&&model.CanBannerBeUpdated(rewards[0]),"native upgrades cannot turn duty credential into ordinary flag");
  PoliceStats.Police!.Banner.Code="warden-black-gold";
  hero.BannerItem=new(duty);var personal=Clan.PlayerClan.Banner;personal.Code="personal-original";
  Banner ClanEmblem(Clan c){Banner value=c.Banner;object[] args={c,value};typeof(GwpDutyBannerClanEmblemPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);return (Banner)args[1];}
  Banner MapEmblem(MobileParty party){object[] args={party,personal};typeof(GwpDutyBannerMapEmblemPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);return (Banner)args[1];}
  Check(ClanEmblem(Clan.PlayerClan).Code=="warden-black-gold"&&Clan.PlayerClan.Banner.Code=="personal-original","duty changes displayed clan emblem while saved personal banner remains untouched");
  Check(MapEmblem(MobileParty.MainParty).Code=="warden-black-gold","main map party uses duty emblem even when another native banner would win");
  Check(ClanEmblem(new Clan()).Code=="own"&&MapEmblem(new MobileParty())==personal,"other clan and other parties retain native emblems");
  Banner copy=ClanEmblem(Clan.PlayerClan);copy.Code="editor-or-native-recolor";Check(PoliceStats.Police.Banner.Code=="warden-black-gold"&&ClanEmblem(Clan.PlayerClan).Code=="warden-black-gold","returned banner cannot mutate shared Grey Warden emblem or later display");
  Game.Current!.GameStateManager.ActiveState=new BannerEditorState();Check(ClanEmblem(Clan.PlayerClan)==personal,"native banner editor edits personal original rather than shared duty emblem");Game.Current.GameStateManager.ActiveState=null;
  var war=new WarParty();Clan.PlayerClan.WarPartyComponents.Add(war);GwpDutyBanner.OnInventoryExchange(new(),new(),false);Check(war.Party.Dirties==1,"even equipment-only inventory close refreshes cached map banner");
  hero.BannerItem=default;Check(ClanEmblem(Clan.PlayerClan)==personal&&MapEmblem(MobileParty.MainParty)==personal,"unequip restores personal clan and map emblem");
  MBObjectManager.Instance.Item=duty;b._voluntaryExitCount=0;b._recruitmentAccepted=false;MobileParty.MainParty.ItemRoster.Clear();b.Join();Check(b.GearGrants==1&&MobileParty.MainParty.ItemRoster.Count==1,"first join through lord receives first kit and one duty flag");
  hero.BannerItem=new(duty);hero.CivilianEquipment[EquipmentIndex.ExtraWeaponSlot]=new(duty);MobileParty.MainParty.ItemRoster.AddToCounts(new(duty),2);var sword=new ItemObject{StringId="kept-sword"};MobileParty.MainParty.ItemRoster.AddToCounts(new(sword),1);
  b.Leave();Check(!b._recruitmentAccepted&&hero.BannerItem.Item==null&&hero.CivilianEquipment[EquipmentIndex.ExtraWeaponSlot].Item==null,"leaving membership clears equipped duty flags");
  Check(MobileParty.MainParty.ItemRoster.Count==1&&MobileParty.MainParty.ItemRoster[0].EquipmentElement.Item==sword,"leaving recovers all baggage duty flags and preserves other possessions");
  b.Join();Check(b.GearGrants==1&&MobileParty.MainParty.ItemRoster.Count==2&&b._recruitmentAccepted,"rejoining receives flag but never another full equipment set");
  b.Join();Check(b.GearGrants==1&&MobileParty.MainParty.ItemRoster.Count==2,"repeated handoff cannot duplicate held duty flag");
  hero.BannerItem=new(new ItemObject{StringId="ordinary-banner"});b.Leave();Check(hero.BannerItem.Item?.StringId=="ordinary-banner","leaving preserves unrelated equipped banner");
  b._recruitmentAccepted=true;
  PoliceStats.Police=null;hero.BannerItem=new(duty);Check(Emblem(PartyBase.MainParty,own)==own,"no Warden clan preserves original emblem");
  Hero.MainHero=null;Check(!GwpCommon.IsPlayerGreyWardenOnDuty(),"missing main hero is off duty");Campaign.Current=null;Check(!GwpCommon.IsPlayerGreyWardenOnDuty(),"outside campaign remains off duty");
  Console.WriteLine("DUTY_BANNER_ASSERTIONS="+Checks+"; PASS; production bodies, controlled UI and engine boundaries");
 }
}
}
'''


class DutyBannerTests(unittest.TestCase):
    def test_duty_lifecycle_and_native_slot_reward_contract(self):
        def read(name):
            return (MODULE / name).read_text(encoding='utf-8-sig')
        def native(type_name):
            return subprocess.check_output(['ilspycmd','-t',type_name,str(GAME / 'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')],text=True,encoding='utf-8-sig')
        def body(source):
            return source[source.index('{', source.index('namespace ')) + 1:source.rindex('}')]
        hero = native('TaleWorlds.CampaignSystem.Hero')
        hero_banner = hero[hero.index('\tpublic EquipmentElement BannerItem'):hero.index('\n\tpublic float ProbabilityOfDeath')]
        base = native('TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel')
        common = read('GwpCommon.cs')
        bounty, dialogue = read('PlayerBountyBehavior.cs'), read('PlayerBountyBehavior.DialogueAndNotification.cs')
        methods = method(bounty,'MeetsBountyDutyAndStanding') + '\n' + '\n'.join(method(dialogue,n) for n in ['GetBountyOfferBlocker','CanInspectBountyOffers','GetBountyOfferStatusText','CanLeaveThroughLord','CanRequestDutyBanner','IssueDutyBanner','GiveDutyBanner','RecoverDutyBanners','OnRejoinThroughLord','OnLeaveThroughLord'])
        keys = sorted(set(re.findall(r'GwpTextCatalog\.(\w+)',methods)))
        code = HARNESS.replace('HERO_BANNER',hero_banner).replace('NATIVE_POOL',method(base,'GetPossibleRewardBannerItems') + method(base,'CanBannerBeUpdated'))
        code = code.replace('COMMON_METHODS',method(common,'IsPlayerGreyWardenMember') + method(common,'IsPlayerGreyWardenOnDuty'))
        code = code.replace('BOUNTY_METHODS',methods)
        code = code.replace('TEXT_KEYS','\n'.join(f'public const string {k}="{k}";' for k in keys))
        code = code.replace('IDS',body(read('GwpIds.cs'))).replace('BANNER_CLASSES',body(read('GwpDutyBanner.cs')))
        major = subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-duty-banner-') as temp:
            p=Path(temp)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            r=subprocess.run(['dotnet','run','--project',temp,'-c','Release','-v','quiet'],capture_output=True,text=True)
            self.assertEqual(0,r.returncode,r.stdout+r.stderr)
            print(next(line for line in r.stdout.splitlines() if line.startswith('DUTY_BANNER_ASSERTIONS=')))

    def test_civilian_template_and_registered_banner_assets(self):
        sets=ET.parse(MODULE/'_Module/ModuleData/gw_equipment_sets.xml').getroot().find('EquipmentRoster')
        civilian=sets.find("EquipmentSet[@equipmentType='Civilian']")
        slots={e.get('slot'):e.get('id') for e in civilian.findall('Equipment')}
        self.assertEqual({'Body':'Item.monk_robe','Cape':'Item.warchshoulder','Leg':'Item.leather_shoes'},slots)
        battle=sets.find('EquipmentSet')
        self.assertEqual('Item.wcomhelmethv',battle.find("Equipment[@slot='Head']").get('id'))
        item=ET.parse(MODULE/'_Module/ModuleData/items.xml').getroot().find("Item[@id='gwp_duty_banner']")
        self.assertEqual(('Banner','false','true'),tuple(item.get(k) for k in ['Type','is_merchandise','using_tableau']))
        native_items=ET.parse(GAME/'Modules/SandBoxCore/ModuleData/items/banners.xml').getroot()
        self.assertTrue(any(i.get('mesh')==item.get('mesh') and i.get('prefab')==item.get('prefab') for i in native_items.findall('Item')))
        self.assertIn(item.find('ItemComponent/Banner').get('effect'),{'DecreasedMoraleShock'})
        empire=[i for i in native_items.findall('Item') if i.get('culture')=='Culture.empire' and i.find('ItemComponent/Banner') is not None]
        top=max(empire,key=lambda i:int(i.find('ItemComponent/Banner').get('banner_level')))
        self.assertEqual(top.get('mesh'),item.get('mesh'))
        self.assertEqual('1',item.find('ItemComponent/Banner').get('banner_level'))


if __name__ == '__main__':
    unittest.main()
