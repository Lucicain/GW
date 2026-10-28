"""Production native-villager integration, proportional births and cargo ownership."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_lord_combat import members, source
from test_training_transport import ROOT, hygiene
from test_direct_guard_recruitment import STUBS as RECRUIT_STUBS
from vanilla_source import vanilla


HARNESS = r'''
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
class CharacterObject {public string StringId;public int Tier;public bool IsHero,IsWarden;public static Dictionary<string,CharacterObject> All=new Dictionary<string,CharacterObject>();public static CharacterObject Find(string id)=>All[id];}
__ROSTER__
class ItemObject {public string StringId;public float Weight;public int Value;public object ItemCategory;public bool HasHorseComponent;}
class ItemModifier {public string StringId;public float WeightFactor=1;}
struct EquipmentElement {public ItemObject Item;public ItemModifier ItemModifier;public EquipmentElement(ItemObject i,ItemModifier m=null){Item=i;ItemModifier=m;}}
struct ItemRosterElement {public EquipmentElement EquipmentElement;public int Amount;}
class ItemRoster:IEnumerable<ItemRosterElement> {
 List<ItemRosterElement> rows=new List<ItemRosterElement>();public int Count=>rows.Count;public int TotalValue=>rows.Sum(e=>e.Amount*e.EquipmentElement.Item.Value);
 public ItemRosterElement this[int i]=>rows[i];public ItemRosterElement GetElementCopyAtIndex(int i)=>rows[i];public void Clear()=>rows.Clear();
 public void AddToCounts(EquipmentElement item,int count){int i=rows.FindIndex(e=>e.EquipmentElement.Equals(item));var e=i>=0?rows[i]:new ItemRosterElement{EquipmentElement=item};e.Amount+=count;if(e.Amount<0)throw new Exception("negative items");if(i>=0){if(e.Amount==0)rows.RemoveAt(i);else rows[i]=e;}else if(e.Amount>0)rows.Add(e);}
 public IEnumerator<ItemRosterElement> GetEnumerator()=>rows.ToList().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
class MBObjectManager {public static MBObjectManager Instance=new MBObjectManager();public Dictionary<string,object> Items=new Dictionary<string,object>();public T GetObject<T>(string id)=>(T)Items[id];}
static class DefaultItemCategories {public static readonly object PackAnimal=new object();}
enum AiBehavior {Hold,GoToSettlement}
class Campaign {public static Campaign Current=new Campaign();public Models Models=new Models();}
class Models {public InventoryCapacityModel InventoryCapacityModel=new InventoryCapacityModel();}
class InventoryCapacityModel {
 public float GetItemEffectiveWeight(EquipmentElement e,MobileParty p,bool sea,out object description){description=null;return e.Item.HasHorseComponent?(sea?30:0):e.Item.Weight*(e.ItemModifier?.WeightFactor??1);}
}
class MobileParty {
 public string StringId;public bool IsActive=true,IsVillager=true,IsMilitia,IsCaravan,IsCurrentlyAtSea,IsInRaftState;public object MapEvent;
 public Settlement HomeSettlement,CurrentSettlement,TargetSettlement;public AiBehavior DefaultBehavior;
 public TroopRoster MemberRoster=new TroopRoster(),PrisonRoster=new TroopRoster();public ItemRoster ItemRoster=new ItemRoster();
 public int PartyTradeGold,MapFaction=2;public float BaseCapacity=100;public float InventoryCapacity {get=>BaseCapacity+(IsCurrentlyAtSea?0:ItemRoster.Where(e=>e.EquipmentElement.Item.ItemCategory==DefaultItemCategories.PackAnimal).Sum(e=>e.Amount)*100);set=>BaseCapacity=value;}
 public float TotalWeightCarried=>ItemRoster.Sum(e=>e.Amount*Campaign.Current.Models.InventoryCapacityModel.GetItemEffectiveWeight(e.EquipmentElement,this,IsCurrentlyAtSea,out _));
 public PartyBase Party;public MobileParty(){Party=new PartyBase{MobileParty=this};}
}
class PartyBase {public MobileParty MobileParty;public ItemRoster ItemRoster=>MobileParty.ItemRoster;}
class MilitiaPartyComponent {public MobileParty MobileParty;}
class Settlement {
 public string StringId;public bool Preached=true,Quiet=true,Ritual,IsUnderSiege;public int MapFaction=2;
 public bool IsVillage=>Village!=null;
 public Village Village;public Town Town;public ItemRoster ItemRoster=new ItemRoster();public MilitiaPartyComponent MilitiaPartyComponent;
 public List<MobileParty> Parties=new List<MobileParty>();public PartyBase Party=new PartyBase();
}
class SettlementComponent {public Settlement Settlement;}
class Village: SettlementComponent {
 public Village(){Settlement=new Settlement{Village=this};}public MilitiaPartyComponent VillagerPartyComponent;public float Hearth=400;
 public Settlement TradeBound;__NATIVE_DEFENDERS__
}
static class MapEvent {public enum BattleTypes {Raid,IsForcingSupplies,IsForcingVolunteers,FieldBattle}}
class Town {public int Gold;public int GetItemPrice(EquipmentElement e,MobileParty p,bool isSelling)=>e.Item.Value;public void ChangeGold(int n){Gold+=n;}}
class IDataStore {public bool IsSaving;public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new Dictionary<string,object>();public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v;else v=(T)Data[k];}}
class CampaignTime {public static CampaignTime Now=new CampaignTime();public double ToHours;}
static class FactionManager {public static bool IsAtWarAgainstFaction(int a,int b)=>a!=b;}
static class MBRandom {public static int RoundRandomized(float n)=>(int)Math.Round(n);}
static class GwpIds {public const string ZealotId="gwzealot";}
static class GwpTuning {public static class Firesworn {public const int CaravanMinimumGoodsValue=2000,MissionMinimumLeftBehind=5;public const float GuardKeptFromPriestSlots=0.5f;}}
static class GwpSettlementNotices {public static void Post(Settlement s,MobileParty p,string text){}}
static class GwpText {public static string Get(string s,params object[] args)=>s;}
static class GwpTextCatalog {public const string gwp_notice_market="market";}
__TRANSFER__
class GwpFireswornBehavior {
 static GwpFireswornBehavior _instance;
 enum FaithPartyKind {Market}enum FaithPartyPhase {Home,Outbound,Returning}
 class FaithParty {public string PartyId,HomeId,TargetId;public FaithPartyKind Kind;public FaithPartyPhase Phase;public double PhaseHour;public int MarketGold;public bool DedicatedMarketTripPending;}
 class FaithVillage {public string VillageId;public int Households=200;}
 readonly Dictionary<string,FaithVillage> _villages=new Dictionary<string,FaithVillage>();
 readonly Dictionary<string,FaithParty> _faithParties=new Dictionary<string,FaithParty>();
 readonly Dictionary<string,ItemRoster> _goods=new Dictionary<string,ItemRoster>(),_marketCargo=new Dictionary<string,ItemRoster>();
 readonly Dictionary<string,TroopRoster> _guards=new Dictionary<string,TroopRoster>(),_dungeons=new Dictionary<string,TroopRoster>();
 readonly Dictionary<string,int> _plans=new Dictionary<string,int>();static int Treasury;public int Equipped;
 static bool IsPreachedVillage(Settlement s)=>s?.Preached==true;static bool IsVillageQuiet(Settlement s)=>s.Quiet;
 bool HasRitual(Settlement s)=>s.Ritual;static bool IsGuardTroop(CharacterObject c)=>c.IsWarden&&!c.IsHero;
 static float SwornShare(FaithVillage r,Settlement s)=>r.Households/s.Village.Hearth;
 TroopRoster GuardOf(string id)=>RosterOf(_guards,id);int Sworn(FaithVillage r)=>GuardOf(r.VillageId).TotalManCount;
 void ObserveGuard(FaithVillage r){}void ReconcilePriestSlots(FaithVillage r){}int ReturnPartyLedger(MobileParty p,string h)=>0;
 int TakeBestSets(string h,MobileParty p){Equipped++;return 0;}int MoveBestSets(string a,string b,CharacterObject t,int n)=>0;
 int JoinGuard(FaithVillage r,Settlement s,CharacterObject t,int n,TroopRoster from){int moved=GwpTroopTransfer.Move(from,s.MilitiaPartyComponent.MobileParty.MemberRoster,t,n).Number;GuardOf(r.VillageId).AddToCounts(t,moved);return moved;}
 static void ReleaseHeroPrisoners(MobileParty p){foreach(var e in p.PrisonRoster.GetTroopRoster().Where(e=>e.Character.IsHero))p.PrisonRoster.AddToCounts(e.Character,-e.Number,false,-e.WoundedNumber,-e.Xp);}
 static void CreditTreasury(int gold)=>Treasury+=gold;
__METHODS__
__NATIVE_LOAD__
 static bool Depart(Village v,MobileParty p){PrepareFaithVillagers(v,p);bool dedicated=IsDedicatedFaithMarket(v,p);if(LoadNativeMarketGoods(v,p))MoveItemsToVillagerParty(v,p);p.TargetSettlement=v.TradeBound;LoadFaithMarket(v,p,dedicated);return dedicated;}
 static int checks;static void Check(bool c,string why){checks++;if(!c)throw new Exception(why);}
 static int Amount(ItemRoster r,EquipmentElement e)=>r.Where(x=>x.EquipmentElement.Equals(e)).Sum(x=>x.Amount);
 static CharacterObject Troop(string id,int tier,bool warden=false){var t=new CharacterObject{StringId=id,Tier=tier,IsWarden=warden};CharacterObject.All[id]=t;return t;}
 static (GwpFireswornBehavior,Village,MobileParty) Start(){
  var b=new GwpFireswornBehavior();_instance=b;Treasury=0;var v=new Village();v.Settlement.StringId="home";v.TradeBound=new Settlement{StringId="town",Town=new Town{Gold=10000}};
  var p=new MobileParty{StringId="native",HomeSettlement=v.Settlement,CurrentSettlement=v.Settlement};v.VillagerPartyComponent=new MilitiaPartyComponent{MobileParty=p};v.Settlement.Parties.Add(p);
  b._villages["home"]=new FaithVillage{VillageId="home"};return(b,v,p);
 }
 static void Main(){
  var civilian=Troop("villager",1);var zealot=Troop(GwpIds.ZealotId,2,true);var elite=Troop("elite",6,true);
  var(b,v,p)=Start();p.MemberRoster.AddToCounts(civilian,20);ConvertRaisedVillagers(p,null);
  Check(p.MemberRoster.GetTroopCount(civilian)==10&&p.MemberRoster.GetTroopCount(zealot)==10,"native birth at half faith splits the original twenty people");
  Check(v.Hearth==400&&IsFaithVillager(p),"conversion adds no second household cost and registers population");
  var prior=p.MemberRoster.GetTroopRoster().ToDictionary(e=>e.Character,e=>e.Number);p.MemberRoster.AddToCounts(civilian,4);ConvertRaisedVillagers(p,prior);
  Check(p.MemberRoster.GetTroopCount(civilian)==12&&p.MemberRoster.GetTroopCount(zealot)==12,"only four newly raised villagers converted proportionally");
  p.MemberRoster.AddToCounts(zealot,-4);prior=p.MemberRoster.GetTroopRoster().ToDictionary(e=>e.Character,e=>e.Number);p.MemberRoster.AddToCounts(civilian,4);ConvertRaisedVillagers(p,prior);
  Check(p.MemberRoster.GetTroopCount(zealot)==12&&p.MemberRoster.GetTroopCount(civilian)==12,"faith casualties corrected only through new replacement slots");
  b._villages["home"].Households=400;prior=p.MemberRoster.GetTroopRoster().ToDictionary(e=>e.Character,e=>e.Number);p.MemberRoster.AddToCounts(civilian,6);ConvertRaisedVillagers(p,prior);
  Check(p.MemberRoster.GetTroopCount(civilian)==12&&p.MemberRoster.GetTroopCount(zealot)==18,"full faith newborns all zealots; old civilians not retroactively converted");
  b._villages["home"].Households=0;prior=p.MemberRoster.GetTroopRoster().ToDictionary(e=>e.Character,e=>e.Number);p.MemberRoster.AddToCounts(civilian,2);ConvertRaisedVillagers(p,prior);
  Check(p.MemberRoster.GetTroopCount(civilian)==14,"zero share leaves newborns native");
  (b,v,p)=Start();b._villages["home"].Households=400;v.Hearth=385;p.MemberRoster.AddToCounts(civilian,30);ConvertRaisedVillagers(p,null);
  Check(p.MemberRoster.GetTroopCount(zealot)==30&&p.MemberRoster.TotalManCount==30&&v.Hearth==385,"native household deduction cannot push birth share above one or charge twice");
  (b,v,p)=Start();v.Settlement.Preached=false;p.MemberRoster.AddToCounts(civilian,20);ConvertRaisedVillagers(p,null);
  Check(!IsFaithVillager(p)&&p.MemberRoster.GetTroopCount(civilian)==20,"unconverted village entirely native");
  (b,v,p)=Start();p.MemberRoster.AddToCounts(civilian,20);p.MemberRoster.AddToCounts(zealot,20);var home=v.Settlement;var m=new MobileParty{IsVillager=false,IsMilitia=true};home.MilitiaPartyComponent=new MilitiaPartyComponent{MobileParty=m};
  m.MemberRoster.AddToCounts(elite,40,false,10,407);b.GuardOf("home").AddToCounts(elite,40);
  PrepareFaithVillagers(v,p);
  Check(p.MemberRoster.TotalManCount==40&&p.MemberRoster.GetTroopCount(elite)==20&&p.MemberRoster.GetTroopCount(civilian)==20,"precisely twenty elite soldiers replace twenty weak members");
  Check(b.GuardOf("home").TotalManCount==40&&m.MemberRoster.GetTroopCount(civilian)==0&&m.MemberRoster.GetTroopCount(zealot)==20,"only gray members swapped, native villagers never leave their party");
  Check(m.MemberRoster.TotalManCount+p.MemberRoster.TotalManCount==80&&v.Hearth==400,"swapping conserves total people and hearths");
  var wounded=m.MemberRoster.GetElementCopyAtIndex(m.MemberRoster.FindIndexOfTroop(elite));var moved=p.MemberRoster.GetElementCopyAtIndex(p.MemberRoster.FindIndexOfTroop(elite));
  Check(wounded.WoundedNumber==10&&moved.WoundedNumber==0&&wounded.Xp+moved.Xp==407,"only healthy elites travel and shared XP remains conserved");
  Check(b.Equipped==1&&p.MapFaction==home.MapFaction,"actual replacement roster outfitted without changing native allegiance");
  Check(v.GetDefenderParties(MapEvent.BattleTypes.Raid).Contains(p.Party),"unmodified vanilla defender enumerator includes the real villager party at home");
  home.Parties.Remove(p);Check(!v.GetDefenderParties(MapEvent.BattleTypes.Raid).Contains(p.Party),"out-of-village party does not defend remotely");home.Parties.Add(p);
  var food=new EquipmentElement(new ItemObject{StringId="grain",Value=5,Weight=2});var weapon=new EquipmentElement(new ItemObject{StringId="sword",Value=100,Weight=2});
  MBObjectManager.Instance.Items["grain"]=food.Item;MBObjectManager.Instance.Items["sword"]=weapon.Item;
  p.ItemRoster.AddToCounts(food,10);ItemsOf(b._goods,"home").AddToCounts(food,4);ItemsOf(b._goods,"home").AddToCounts(weapon,2);p.TargetSettlement=v.TradeBound;p.DefaultBehavior=AiBehavior.GoToSettlement;
  LoadFaithMarket(v,p);Check(Amount(p.ItemRoster,food)==14&&Amount(p.ItemRoster,weapon)==2&&ItemsOf(b._goods,"home").Count==0,"native produce and real gray cargo share one inventory");
  var store=new IDataStore{IsSaving=true};SyncItemRosters(store,"Cargo",b._marketCargo);var restored=new Dictionary<string,ItemRoster>();store.IsSaving=false;SyncItemRosters(store,"Cargo",restored);
  Check(Amount(restored[p.StringId],food)==4&&Amount(restored[p.StringId],weapon)==2,"actual cargo serializer preserves ownership amounts");
  v.TradeBound.Town.Gold=100;Check(SellFaithMarket(v.TradeBound,p),"registered native sale intercepted");
  Check(p.PartyTradeGold==100&&b._faithParties[p.StringId].MarketGold==100&&Amount(p.ItemRoster,weapon)==1,"partial town budget pays only real sold gray goods");
  v.TradeBound.Town.Gold=120;SellFaithMarket(v.TradeBound,p);
  Check(p.PartyTradeGold==220&&b._faithParties[p.StringId].MarketGold==220&&Amount(p.ItemRoster,food)==10,"same-kind mixed stack sells gray portion first, keeps native goods");
  v.TradeBound.Town.Gold=25;SellFaithMarket(v.TradeBound,p);
  Check(p.PartyTradeGold==245&&b._faithParties[p.StringId].MarketGold==220&&Amount(p.ItemRoster,food)==5,"native goods income remains outside gray liability");
  SettleFaithMarket(p,home);Check(Treasury==220&&p.PartyTradeGold==25&&p.MemberRoster.TotalManCount==40,"gray revenue goes to treasury while exact native proceeds remain for vanilla tax callback");
  Check(Amount(p.ItemRoster,food)==5&&p.MemberRoster.GetTroopCount(civilian)==20,"native unsold goods and crew never absorbed as temporary troops");
  SettleFaithMarket(p,home);Check(Treasury==220&&p.PartyTradeGold==25,"return settlement is idempotent");
  var stop=new Village();b._faithParties[p.StringId].MarketGold=10;p.PartyTradeGold=15;SettleFaithMarket(p,stop.Settlement);
  Check(Treasury==230&&p.PartyTradeGold==5&&Amount(p.ItemRoster,food)==5,"foreign village tax callback cannot swallow gray proceeds or unload cargo");
  ItemsOf(b._marketCargo,p.StringId).AddToCounts(weapon,3);p.ItemRoster.AddToCounts(weapon,1);b._faithParties[p.StringId].MarketGold=100;p.PartyTradeGold=30;
  SettleFaithMarket(p,home);Check(Treasury==260&&Amount(ItemsOf(b._goods,"home"),weapon)==1&&p.PartyTradeGold==0,"lost cargo and gold never regenerated from ownership ledger");
  p.InventoryCapacity=p.TotalWeightCarried+2;ItemsOf(b._goods,"home").AddToCounts(weapon,3);LoadFaithMarket(v,p);
  Check(Amount(p.ItemRoster,weapon)==1&&Amount(ItemsOf(b._goods,"home"),weapon)==3,"native produce keeps its capacity; no gray overload");
  (b,v,p)=Start();p.MemberRoster.AddToCounts(civilian,2);PrepareFaithVillagers(v,p);Check(p.MemberRoster.GetTroopCount(civilian)==2,"no guard does not delete native crew or prevent native shipping");
  var valuable=new EquipmentElement(new ItemObject{StringId="loot",Value=2500,Weight=1});ItemsOf(b._goods,"home").AddToCounts(valuable,1);
  Check(FaithMarketWarehouseThreshold(50,v)==0,"gray-only warehouse participates in original threshold");v.Settlement.Ritual=true;Check(FaithMarketWarehouseThreshold(50,v)==50,"ritual leaves native warehouse threshold unchanged");v.Settlement.Ritual=false;v.Settlement.Preached=false;Check(FaithMarketWarehouseThreshold(50,v)==50,"non-faith village warehouse unchanged");
  (b,v,p)=Start();PrepareFaithVillagers(v,p);p.InventoryCapacity=610;
  var grain=new EquipmentElement(new ItemObject{StringId="heavy_grain",Value=5,Weight=10});
  var loot=new EquipmentElement(new ItemObject{StringId="gray_loot",Value=100,Weight=5});
  v.Settlement.ItemRoster.AddToCounts(grain,1000);ItemsOf(b._goods,"home").AddToCounts(loot,500);
  Check(!Depart(v,p)&&Amount(p.ItemRoster,grain)==61&&Amount(p.ItemRoster,loot)==0,"complete native loader fills ordinary trade with village produce");
  Check(b._faithParties[p.StringId].DedicatedMarketTripPending,"unsent gray stock schedules a separate trip");
  SettleFaithMarket(p,v.Settlement);Check(b._faithParties[p.StringId].DedicatedMarketTripPending,"home settlement preserves dedicated departure, including native unsold stock");
  Check(Depart(v,p)&&Amount(v.Settlement.ItemRoster,grain)==1000&&Amount(p.ItemRoster,grain)==0&&Amount(p.ItemRoster,loot)==122,"dedicated trip returns native unsold grain and loads real gray cargo");
  Check(!b._faithParties[p.StringId].DedicatedMarketTripPending&&p.InventoryCapacity==p.TotalWeightCarried,"dedicated trip is full without an artificial capacity boost and releases ordinary trade");
  SettleFaithMarket(p,v.Settlement);Check(!Depart(v,p)&&Amount(p.ItemRoster,grain)==61&&Amount(p.ItemRoster,loot)==0,"native trade resumes even while large gray backlog remains");
  p.CurrentSettlement=v.TradeBound;Check(!IsDedicatedFaithMarket(v,p),"pending dedicated trip cannot redirect villagers already in transit");p.CurrentSettlement=v.Settlement;
  p.IsInRaftState=true;Check(!IsDedicatedFaithMarket(v,p),"raft villagers cannot begin dedicated transport");p.IsInRaftState=false;
  p.MapEvent=new object();Check(!IsDedicatedFaithMarket(v,p),"battle prevents dedicated departure");p.MapEvent=null;
  v.Settlement.Ritual=true;Check(!IsDedicatedFaithMarket(v,p),"ritual prevents dedicated departure");v.Settlement.Ritual=false;
  v.Settlement.Quiet=false;Check(!IsDedicatedFaithMarket(v,p),"unsafe village cannot dedicate its villagers");v.Settlement.Quiet=true;
  v.Settlement.Preached=false;Check(LoadNativeMarketGoods(v,p),"nonfaith village retains native loading");v.Settlement.Preached=true;
  ItemsOf(b._goods,"home").Clear();Check(!IsDedicatedFaithMarket(v,p),"empty gray warehouse does not claim an empty dedicated trip");
  (b,v,p)=Start();PrepareFaithVillagers(v,p);p.InventoryCapacity=10;
  var pack=new EquipmentElement(new ItemObject{StringId="pack",Value=100,Weight=430,HasHorseComponent=true,ItemCategory=DefaultItemCategories.PackAnimal});
  ItemsOf(b._goods,"home").AddToCounts(loot,20);ItemsOf(b._goods,"home").AddToCounts(pack,1);
  LoadFaithMarket(v,p);Check(Amount(p.ItemRoster,pack)==1&&Amount(p.ItemRoster,loot)==20&&p.InventoryCapacity==110&&p.TotalWeightCarried==100,"land pack animal uses model weight and increases capacity before other goods load");
  var nativePack=new EquipmentElement(new ItemObject{StringId="native_pack",Value=20,Weight=430,HasHorseComponent=true,ItemCategory=DefaultItemCategories.PackAnimal});
  p.MemberRoster.AddToCounts(civilian,2);p.ItemRoster.AddToCounts(nativePack,1);v.Settlement.ItemRoster.AddToCounts(grain,4);b._faithParties[p.StringId].DedicatedMarketTripPending=true;ItemsOf(b._goods,"home").AddToCounts(loot,100);
  Check(!LoadNativeMarketGoods(v,p)&&Amount(p.ItemRoster,loot)==20&&Amount(p.ItemRoster,nativePack)==1&&Amount(v.Settlement.ItemRoster,loot)==0,"native unloading preserves gray ownership and native transport animals");
  p.ItemRoster.AddToCounts(nativePack,5);Check(!LoadNativeMarketGoods(v,p)&&Amount(p.ItemRoster,nativePack)==1&&Amount(v.Settlement.ItemRoster,nativePack)==5,"unsold excess native animals return home rather than occupying dedicated cargo space");
  (b,v,p)=Start();PrepareFaithVillagers(v,p);p.IsCurrentlyAtSea=true;p.InventoryCapacity=65;ItemsOf(b._goods,"home").AddToCounts(pack,11);
  LoadFaithMarket(v,p);Check(Amount(p.ItemRoster,pack)==2&&p.TotalWeightCarried==60&&Amount(ItemsOf(b._goods,"home"),pack)==9,"sea load follows active model's 30 weight, never raw 430 or land zero");
  (b,v,p)=Start();PrepareFaithVillagers(v,p);p.InventoryCapacity=10;
  var modified=new EquipmentElement(loot.Item,new ItemModifier{StringId="heavy",WeightFactor=2});ItemsOf(b._goods,"home").AddToCounts(modified,3);
  LoadFaithMarket(v,p);Check(Amount(p.ItemRoster,modified)==1&&p.TotalWeightCarried==10,"modified equipment uses actual effective weight");
  (b,v,p)=Start();p.InventoryCapacity=150;v.Settlement.ItemRoster.AddToCounts(grain,10);ItemsOf(b._goods,"home").AddToCounts(loot,10);
  Check(!Depart(v,p)&&Amount(p.ItemRoster,grain)>0&&Amount(p.ItemRoster,loot)==10,"ordinary native trade still piggybacks gray goods into spare capacity");
  Console.WriteLine("FAITH_VILLAGER_ASSERTIONS="+checks+"; PASS");
 }
}
'''


class FaithVillagerTests(unittest.TestCase):
    def test_native_birth_swap_sale_and_persistence(self):
        production = members(source("GwpFaithVillagers.cs"), "IsFaithVillager", "RegisterFaithVillager", "ConvertRaisedVillagers",
            "FaithMarketWarehouseThreshold", "PrepareFaithVillagers", "RefitVillageEscort", "IsDedicatedFaithMarket", "LoadNativeMarketGoods", "LoadFaithMarket", "RecordFaithMarketSale",
            "MarketPackAnimal", "SellFaithMarket", "SettleFaithMarket")
        production += members(source("GwpFireswornBehavior.cs"), "RosterOf", "ItemsOf", "SpareGuard", "SyncItemRosters")
        production += members(source("GwpFireswornParties.cs"), "SetPhase")
        roster = RECRUIT_STUBS[RECRUIT_STUBS.index("struct TroopRosterElement"):RECRUIT_STUBS.index("class Hero")]
        roster = roster.replace(" public int TotalManCount", " public static TroopRoster CreateDummyTroopRoster()=>new TroopRoster();public int TotalRegulars=>rows.Where(e=>!e.Character.IsHero).Sum(e=>e.Number);\n public int TotalManCount")
        transfer = source("GwpTroopTransfer.cs").split("namespace GreyWardenPolicePurity", 1)[1].strip()[1:-1]
        native = vanilla("TaleWorlds.CampaignSystem", "TaleWorlds.CampaignSystem.Settlements/Village.cs")
        start = native.index("public IEnumerable<PartyBase> GetDefenderParties(")
        brace = native.index("{", start)
        native = native[start:hygiene.match_brace(native, hygiene.code_mask(native), brace)+1]
        loader = vanilla("TaleWorlds.CampaignSystem", "TaleWorlds.CampaignSystem.CampaignBehaviors/VillagerCampaignBehavior.cs")
        start = loader.index("private static void MoveItemsToVillagerParty(")
        brace = loader.index("{", start)
        loader = loader[start:hygiene.match_brace(loader, hygiene.code_mask(loader), brace)+1]
        loader = loader.replace("MathF.Ceiling(", "(int)Math.Ceiling(")
        program = HARNESS.replace("__METHODS__", production).replace("__ROSTER__", roster).replace("__TRANSFER__", transfer).replace("__NATIVE_DEFENDERS__", native).replace("__NATIVE_LOAD__", loader)
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-villagers-") as directory:
            path = Path(directory)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn>'
                '</PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
