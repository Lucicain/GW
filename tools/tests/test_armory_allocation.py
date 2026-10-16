"""Execute production all-tier allocation, inventory transfers and per-troop battle losses."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_training_transport import ROOT, extract, hygiene

HARNESS = r"""
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
[Flags] enum ItemFlags {None=0,NotUsableByFemale=1,NotUsableByMale=2}
static class FlagExtensions {public static bool HasAnyFlag(this ItemFlags a,ItemFlags b)=>(a&b)!=0;}
enum EquipmentIndex {Weapon0=0,Weapon1=1,Weapon2=2,Weapon3=3,NumPrimaryWeaponSlots=4,ExtraWeaponSlot=4,Head=5,Body=6,Leg=7,Gloves=8,Cape=9,Horse=10,HorseHarness=11,NumEquipmentSetSlots=12}
class ItemModifier {}
class ModifierGroup {public ItemModifier GetRandomItemModifierLootScoreBased()=>null;}
class ItemComponent {public ModifierGroup ItemModifierGroup;}
class Mission {public static Mission Current=new Mission();}
class ItemObject {public bool NoMount;public ItemComponent ItemComponent;public string StringId;public ItemFlags ItemFlags;public string RelevantSkill;public int Difficulty,Tier=3;public float Power=1;public ItemTypeEnum ItemType;public enum ItemTypeEnum {OneHandedWeapon,TwoHandedWeapon,Polearm,Thrown,Bow,Crossbow,Arrows,Bolts,Shield,HeadArmor,BodyArmor,LegArmor,HandArmor,Horse,HorseHarness,Cape}}
struct EquipmentElement {public ItemObject Item;public int Quality;public bool IsEmpty=>Item==null;public void SetModifier(ItemModifier modifier){Quality++;}public static EquipmentElement Invalid=>default;public EquipmentElement(ItemObject item){Item=item;Quality=0;} }
class Equipment {public enum EquipmentType {Battle} readonly EquipmentElement[] slots=new EquipmentElement[12];public Equipment(EquipmentType type=EquipmentType.Battle){}public EquipmentElement this[int i]{get=>slots[i];set=>slots[i]=value;}public EquipmentElement this[EquipmentIndex i]{get=>slots[(int)i];set=>slots[(int)i]=value;}public Equipment Clone(){var e=new Equipment();Array.Copy(slots,e.slots,12);return e;} }
class CharacterRegistry:IEnumerable<CharacterObject> {readonly Dictionary<string,CharacterObject> d=new Dictionary<string,CharacterObject>();public CharacterObject this[string id]{get=>d[id];set=>d[id]=value;}public IEnumerator<CharacterObject> GetEnumerator()=>d.Values.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
class CharacterObject {public static readonly CharacterRegistry All=new CharacterRegistry();public string StringId;public int Tier,Skill=100;public bool IsHero;public List<Equipment> BattleEquipments=new List<Equipment>();public Equipment FirstBattleEquipment=>BattleEquipments.FirstOrDefault()??new Equipment();public int GetSkillValue(string s)=>Skill;public static CharacterObject Find(string id)=>All[id];}
struct TroopRosterElement {public CharacterObject Character;public int Number,WoundedNumber;public TroopRosterElement(CharacterObject c){Character=c;Number=WoundedNumber=0;} }
class TroopRoster {public List<TroopRosterElement> Rows=new List<TroopRosterElement>();public int GetTroopCount(CharacterObject troop)=>Rows.Where(e=>e.Character==troop).Sum(e=>e.Number);public int TotalManCount=>Rows.Sum(e=>e.Number);public IEnumerable<TroopRosterElement> GetTroopRoster()=>Rows.ToList();public void AddToCounts(CharacterObject c,int n){int i=Rows.FindIndex(e=>e.Character==c);var e=i<0?new TroopRosterElement(c):Rows[i];e.Number+=n;if(e.Number<0)throw new Exception("negative troop stock");if(i<0)Rows.Add(e);else Rows[i]=e;}public int Count=>Rows.Count;public CharacterObject GetCharacterAtIndex(int i)=>Rows[i].Character;public int GetElementNumber(int i)=>Rows[i].Number;}
class ItemRosterElement {public EquipmentElement EquipmentElement;public int Amount;}
class ItemRoster:IEnumerable<ItemRosterElement> {readonly List<ItemRosterElement> rows=new List<ItemRosterElement>();public int VersionNo;public int Count=>rows.Count;public int Total=>rows.Sum(e=>e.Amount);public int TotalValue=>Total;public void AddToCounts(EquipmentElement item,int n){var e=rows.FirstOrDefault(e=>e.EquipmentElement.Equals(item));if(e==null){e=new ItemRosterElement{EquipmentElement=item};rows.Add(e);}e.Amount+=n;if(e.Amount<0)throw new Exception("negative equipment stock");if(e.Amount==0)rows.Remove(e);VersionNo++;}public ItemRosterElement GetElementCopyAtIndex(int i)=>rows[i];public void Add(ItemRoster other){foreach(var e in other)AddToCounts(e.EquipmentElement,e.Amount);}public IEnumerator<ItemRosterElement> GetEnumerator()=>rows.ToList().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
class PartyBase {public static PartyBase MainParty=new PartyBase();public string Id;public string Name=>Id;public ItemRoster ItemRoster=new ItemRoster();}
class MobileParty {public string StringId;public bool IsLordParty=true,IsVillager;public TroopRoster MemberRoster=new TroopRoster();public ItemRoster ItemRoster=new ItemRoster();}
class Hero {public MobileParty PartyBelongedTo;public bool IsPriest;}
class Settlement {public string StringId;}
class FaithVillage {public string VillageId;}
static class GwpWardenRecruitment {public static bool IsWardenParty(MobileParty party)=>true;}
class MapEventParty {public PartyBase Party;public TroopRoster DiedInBattle=new TroopRoster();}
class MapEventSide {public List<MapEventParty> Parties=new List<MapEventParty>();public PartyBase LeaderParty;}
enum BattleState {None,AttackerVictory,DefenderVictory}
class MapEvent {public BattleState BattleState=BattleState.AttackerVictory;public int DefeatedSide=0,WinningSide=1;public string EventType="battle";public MapEventSide Loser=new MapEventSide(),Winner=new MapEventSide();public MapEventSide GetMapEventSide(int side)=>side==0?Loser:Winner;}
static class MBRandom {public static float RandomFloat=>0.2f;}
static class GwpIds {public const string NewRecruitId="gwnewrecruit",ZealotId="gwzealot";}
static class GwpCommon {public static bool IsGreyWardenTroop(CharacterObject t)=>t.StringId.StartsWith("gw");}
static class GwpTuning {public static class Firesworn {public const float ArmoryLossLootShare=.33f;}}
static class GwpAiDiagnostics {public static void WriteFaith(string a,string b){} }
partial class Regression {
static bool IsFaithVillager(MobileParty p)=>p?.IsVillager==true;
readonly Dictionary<string,FaithVillage> _villages=new Dictionary<string,FaithVillage>();
TroopRoster GuardOf(string id)=>rosters[id];static bool IsFireswornPriest(Hero hero)=>hero?.IsPriest==true;void PushGuard(FaithVillage row,Settlement village){}void ReconcilePriestSlots(FaithVillage row){}static void RecordVisit(MobileParty p,Settlement v,int recruits){}
static Regression _instance;readonly Dictionary<string,int> _issuedThisMission=new Dictionary<string,int>();Mission _issuedMission;
const int KitHead=0,KitBody=1,KitLeg=2,KitGloves=3,KitMain=4,KitShield=5,KitThrow=6,KitRanged=7,KitAmmo=8,KitSlots=9,WeaponSlots=4;
readonly Dictionary<string,ArmoryPlan> _plans=new Dictionary<string,ArmoryPlan>();readonly Dictionary<string,ItemRoster> _armories=new Dictionary<string,ItemRoster>();readonly Dictionary<string,TroopRoster> rosters=new Dictionary<string,TroopRoster>();
static ItemRoster ItemsOf(Dictionary<string,ItemRoster> d,string key){if(!d.ContainsKey(key))d[key]=new ItemRoster();return d[key];}
static float Strength(EquipmentElement e)=>e.Item.Power+e.Quality;
string HolderOf(PartyBase p)=>p.Id;
static bool UsableMounted(ItemObject i)=>!i.NoMount;
readonly Dictionary<string,ItemRoster> _goods=new Dictionary<string,ItemRoster>();readonly Dictionary<string,MobileParty> parties=new Dictionary<string,MobileParty>();MobileParty FindParty(string id)=>parties.TryGetValue(id,out var p)?p:null;
readonly HashSet<string> battleHolders=new HashSet<string>();bool HolderInBattle(string id)=>battleHolders.Contains(id);
ArmoryPlan BuildPlanFor(string id)=>PlanForCounts(id,ItemsOf(_armories,id),ArmoryTroops(rosters[id],IsFaithVillager(FindParty(id))),0,IsFaithVillager(FindParty(id)));
__PRODUCTION__
static int checks;
static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
static CharacterObject Troop(string id,int tier,params Equipment[] equipment){var t=new CharacterObject{StringId=id,Tier=tier};t.BattleEquipments.AddRange(equipment);CharacterObject.All[id]=t;return t;}
static EquipmentElement Item(string id,ItemObject.ItemTypeEnum type,float power=1,int quality=0,int difficulty=0){return new EquipmentElement{Item=new ItemObject{StringId=id,ItemType=type,Power=power,Difficulty=difficulty,RelevantSkill="skill"},Quality=quality};}
static Equipment Gear(params (int,EquipmentElement)[] slots){var e=new Equipment();foreach(var s in slots)e[s.Item1]=s.Item2;return e;}
static List<TroopRosterElement> Troops(params (CharacterObject,int)[] rows)=>rows.Select(e=>new TroopRosterElement(e.Item1){Number=e.Item2}).ToList();
static ItemRoster Stock(params (EquipmentElement,int)[] rows){var s=new ItemRoster();foreach(var row in rows)s.AddToCounts(row.Item1,row.Item2);return s;}
static ArmoryKit For(ArmoryPlan p,CharacterObject t,int index=0)=>p.Sets.Where(k=>k.Troop==t).ElementAt(index);
static int Assigned(ArmoryPlan p)=>p.Sets.Sum(k=>k.Items.Count(e=>!e.IsEmpty));
static void Conserved(ArmoryPlan p,ItemRoster stock){foreach(var e in stock){int given=p.Sets.Sum(k=>k.Items.Count(i=>i.Equals(e.EquipmentElement)));int free=p.Free.Where(i=>i.EquipmentElement.Equals(e.EquipmentElement)).Sum(i=>i.Amount);int surplus=p.Surplus.Where(i=>i.EquipmentElement.Equals(e.EquipmentElement)).Sum(i=>i.Amount);Check(given+free+surplus==e.Amount,"per-quality inventory conservation "+e.EquipmentElement.Item.StringId);}}
public static int Main(){
 var zealot=Troop(GwpIds.ZealotId,0,new Equipment());var recruit=Troop(GwpIds.NewRecruitId,1,new Equipment());
 var sword=Item("sword",ItemObject.ItemTypeEnum.OneHandedWeapon,5);var dual=Item("dual",ItemObject.ItemTypeEnum.OneHandedWeapon,8);var body=Item("native_body",ItemObject.ItemTypeEnum.BodyArmor,10);var cape=Item("cape",ItemObject.ItemTypeEnum.Cape);var horse=Item("horse",ItemObject.ItemTypeEnum.Horse);var harness=Item("harness",ItemObject.ItemTypeEnum.HorseHarness);
 var top=Troop("gwtop",6,Gear((0,sword),(6,body),(9,cape),(10,horse),(11,harness)));var middle=Troop("gwmiddle",3,Gear((0,sword)));
 var best=Item("best",ItemObject.ItemTypeEnum.HeadArmor,9);var second=Item("second",ItemObject.ItemTypeEnum.HeadArmor,8);var third=Item("third",ItemObject.ItemTypeEnum.HeadArmor,7);var fourth=Item("fourth",ItemObject.ItemTypeEnum.HeadArmor,6);
 var stock=Stock((best,1),(second,1),(third,1),(fourth,1));var plan=BuildPlan(stock,Troops((zealot,1),(recruit,1),(middle,1),(top,1)),0);
 Check(For(plan,top)[KitHead].Equals(best)&&For(plan,middle)[KitHead].Equals(second),"highest tier selects best before middle tier regardless roster order");Check(For(plan,recruit)[KitHead].Equals(third)&&For(plan,zealot)[KitHead].Equals(fourth),"lower tiers choose leftovers");
 Check(For(plan,top).Equipment[0].Equals(sword)&&For(plan,top).Equipment[6].Equals(body)&&For(plan,top).Equipment[9].Equals(cape)&&For(plan,top).Equipment[10].Equals(horse)&&For(plan,top).Equipment[11].Equals(harness),"high-tier original weapon armour cape horse harness preserved");Conserved(plan,stock);
 var strongerBody=Item("better_body",ItemObject.ItemTypeEnum.BodyArmor,99);stock=Stock((strongerBody,1));plan=BuildPlan(stock,Troops((top,1),(recruit,1)),0);Check(For(plan,top)[KitBody].IsEmpty&&For(plan,recruit)[KitBody].Equals(strongerBody),"high tier skips occupied slot even for stronger armour; low tier can take it");Conserved(plan,stock);
 recruit.BattleEquipments[0]=Gear((0,dual),(5,best));stock=Stock((second,1),(sword,1));plan=BuildPlan(stock,Troops((recruit,1)),0);Check(Assigned(plan)==0&&For(plan,recruit).Equipment[0].Equals(dual)&&For(plan,recruit).Equipment[5].Equals(best),"low tier never replaces with weaker equipment");recruit.BattleEquipments[0]=new Equipment();
 var variants=Troop("gwvariants",4,Gear((0,dual),(5,best),(6,body)),Gear((1,sword),(6,body)));stock=Stock((second,1));plan=BuildPlan(stock,Troops((variants,2)),0);Check(For(plan,variants,0)[KitHead].IsEmpty&&For(plan,variants,1)[KitHead].Equals(second),"actual selected whole presets determine emptiness, not merged OwnBest");Check(For(plan,variants,0).Equipment[0].Equals(dual)&&For(plan,variants,1).Equipment[1].Equals(sword),"preset weapons are never mixed or reordered");Check(variants.BattleEquipments[1][5].IsEmpty,"allocation does not mutate shared troop preset");
 var bow=Item("bow",ItemObject.ItemTypeEnum.Bow,9);var arrows=Item("arrows",ItemObject.ItemTypeEnum.Arrows,3);var bolts=Item("bolts",ItemObject.ItemTypeEnum.Bolts,3);var thrown=Item("javelins",ItemObject.ItemTypeEnum.Thrown,6);var shield=Item("shield",ItemObject.ItemTypeEnum.Shield,6);
 var bowman=Troop("gwbowman",5,Gear((0,dual),(1,dual),(2,bow),(3,arrows),(9,cape),(10,horse),(11,harness)));stock=Stock((sword,5),(thrown,5),(bow,5),(arrows,5),(shield,5));plan=BuildPlan(stock,Troops((bowman,1)),0);Check(Assigned(plan)==0&&Enumerable.Range(0,12).All(i=>For(plan,bowman).Equipment[i].Equals(bowman.FirstBattleEquipment[i])),"full four-slot dual blade archer keeps every original slot and takes no phantom weapon");Check(IsArmoryKitArmed(For(plan,bowman)),"native-equipped high tier is counted armed without library weapon");Conserved(plan,stock);
 var missingAmmo=Troop("gwmissingammo",4,Gear((0,sword),(1,bow)));stock=Stock((arrows,1));plan=BuildPlan(stock,Troops((missingAmmo,1)),0);Check(For(plan,missingAmmo)[KitAmmo].Equals(arrows)&&For(plan,missingAmmo).Equipment[1].Equals(bow),"existing bow gets missing arrows without replacement bow");Tiers(plan.Sets,out float w,out float a);Check(w==0&&a==0,"native equipment does not become extra simulation bonus");
 var twoSlots=Troop("gwtwoslots",4,Gear((0,sword),(1,thrown)));stock=Stock((bow,1),(arrows,1),(shield,1));plan=BuildPlan(stock,Troops((twoSlots,1)),0);Check(For(plan,twoSlots)[KitRanged].Equals(bow)&&For(plan,twoSlots)[KitAmmo].Equals(arrows)&&For(plan,twoSlots)[KitShield].IsEmpty,"two free slots get bow plus arrows ahead of shield");Check(For(plan,twoSlots).Equipment[4].IsEmpty,"banner slot never receives fifth weapon");Conserved(plan,stock);Tiers(plan.Sets,out w,out a);Check(w==4&&a==0,"added ranged equipment contributes simulation tier");
 var oneSlot=Troop("gwoneslot",4,Gear((0,sword),(1,thrown),(2,shield)));stock=Stock((bow,1),(arrows,1));plan=BuildPlan(stock,Troops((oneSlot,1)),0);Check(Assigned(plan)==0&&plan.Free.Total==2,"one free slot cannot consume bow-ammo pair");Conserved(plan,stock);
 var ownAmmo=Troop("gwownammo",4,Gear((0,sword),(1,thrown),(2,arrows)));stock=Stock((bow,1));plan=BuildPlan(stock,Troops((ownAmmo,1)),0);Check(For(plan,ownAmmo)[KitRanged].Equals(bow)&&For(plan,ownAmmo)[KitAmmo].IsEmpty,"existing matching arrows allow bow in last slot without withdrawing more arrows");
 stock=Stock((bow,1),(bolts,1));plan=BuildPlan(stock,Troops((twoSlots,1)),0);Check(Assigned(plan)==0,"incompatible ammo never enables a bow");
 var highSkill=Item("elite_helmet",ItemObject.ItemTypeEnum.HeadArmor,100,difficulty:150);top.Skill=200;recruit.Skill=20;stock=Stock((highSkill,1),(best,1));plan=BuildPlan(stock,Troops((recruit,1),(top,1)),0);Check(For(plan,top)[KitHead].Equals(highSkill)&&For(plan,recruit)[KitHead].Equals(best),"high-tier usable stock is not discarded due to recruit skill gate");Conserved(plan,stock);
 var onlyMale=Item("male_only",ItemObject.ItemTypeEnum.HeadArmor,100);onlyMale.Item.ItemFlags=ItemFlags.NotUsableByFemale;stock=Stock((onlyMale,1),(horse,1),(harness,1),(cape,1));plan=BuildPlan(stock,Troops((top,1)),0);Check(Assigned(plan)==0&&plan.Surplus.Total==4,"gender-restricted horse harness and cape remain ineligible library stock");
 var outsider=Troop("outsider",6);var hero=Troop("gwhero",6);hero.IsHero=true;stock=Stock((best,10));plan=BuildPlan(stock,Troops((hero,1),(outsider,1),(top,1)),0);Check(plan.Sets.Count==1&&plan.Sets[0].Troop==top,"hero and non-grey troops excluded");
 var villager=Troop("villager",1,Gear((0,Item("farm_tool",ItemObject.ItemTypeEnum.OneHandedWeapon,0.1f))));stock=Stock((sword,1),(best,1));plan=BuildPlan(stock,Troops((villager,1)),0,true);
 Check(For(plan,villager).Equipment[0].Equals(sword)&&For(plan,villager)[KitHead].Equals(best),"authorized native villager can replace weak equipment with actual stock");Conserved(plan,stock);
 stock=Stock((best,1));plan=BuildPlan(stock,Troops((villager,1)),0);Check(plan.Sets.Count==0&&plan.Surplus.Total==1,"ordinary village and unrelated party remain excluded");
 var b=new Regression();stock=Stock((best,10),(second,10));var troopRows=Troops((recruit,2),(zealot,3));plan=b.PlanForCounts("cache",stock,troopRows,15);Check(plan.Sets.Count==5&&plan.Free.Total==15,"full twenty positions retained");Conserved(plan,stock);var ordered=plan.Troops;Check(ReferenceEquals(plan,b.PlanForCounts("cache",stock,ordered,15)),"unchanged inputs reuse cached plan");stock.AddToCounts(best,1);var next=b.PlanForCounts("cache",stock,ordered,15);Check(!ReferenceEquals(plan,next),"stock version invalidates plan");Check(!ReferenceEquals(next,b.PlanForCounts("cache",stock,ordered,16)),"reserve change invalidates plan");var upgraded=Troops((top,2),(zealot,3));Check(!ReferenceEquals(next,b.PlanForCounts("cache",stock,upgraded,15)),"same headcount upgraded to higher type invalidates plan");plan=BuildPlan(stock,Troops(),20);Check(plan.Free.Total==20&&plan.Surplus.Total==1,"empty roster still reserves entire capacity");
 b=new Regression();_instance=b;var nativeParty=new MobileParty{StringId="native",IsVillager=true,IsLordParty=false};nativeParty.MemberRoster.AddToCounts(villager,1);b.parties["native"]=nativeParty;b.rosters["native"]=nativeParty.MemberRoster;
 ItemsOf(b._armories,"home").AddToCounts(sword,1);ItemsOf(b._armories,"home").AddToCounts(best,1);
 Check(b.TakeBestSets("home",nativeParty)==2&&ItemsOf(b._armories,"home").Total==0,"native villager actually withdraws gear from home stock");
 Check(TakeKitForAgent(new PartyBase{Id="native"},villager)[0].Equals(sword),"native villager receives its stock weapon in live spawn resolver");
 Check(TryGetArmoryTiers(new PartyBase{Id="native"},villager,out w,out a)&&w>0&&a>0,"native villager equipment reaches actual simulation resolver");
 // Actual troop-specific transfers carry only library additions, not native items.
 b=new Regression();b.rosters["v"]=new TroopRoster();b.rosters["v"].AddToCounts(top,2);b.rosters["p"]=new TroopRoster();ItemsOf(b._armories,"v").AddToCounts(best,2);Check(b.MoveBestSets("v","p",top,1)==1,"high tier carries allocated equipment on recruitment");Check(ItemsOf(b._armories,"v").Total==1&&ItemsOf(b._armories,"p").Total==1,"recruit equipment transfer conserves both ledgers");Check(!b._plans.ContainsKey("v")&&!b._plans.ContainsKey("p"),"recruit transfer invalidates both plans");
 b=new Regression();var escort=new MobileParty{StringId="p"};escort.MemberRoster.AddToCounts(top,1);escort.MemberRoster.AddToCounts(recruit,1);ItemsOf(b._armories,"v").AddToCounts(best,1);ItemsOf(b._armories,"v").AddToCounts(second,1);Check(b.TakeBestSets("v",escort)==2&&b.TakeBestSets("v",escort)==0,"mixed high-low outgoing party carries chosen kits without duplicate draws");
 b=new Regression();b.rosters["dead"]=new TroopRoster();b.rosters["dead"].AddToCounts(top,1);b.rosters["dead"].AddToCounts(recruit,1);ItemsOf(b._armories,"dead").AddToCounts(best,1);ItemsOf(b._armories,"dead").AddToCounts(second,1);b.BuildPlanFor("dead");var battle=new MapEvent();var defeated=new MapEventParty{Party=new PartyBase{Id="dead"}};defeated.DiedInBattle.AddToCounts(top,1);battle.Loser.Parties.Add(defeated);battle.Winner.LeaderParty=new PartyBase{Id="winner"};b.SettleBattleLosses(battle);Check(ItemsOf(b._armories,"dead").Total==1&&ItemsOf(b._armories,"dead").Single().EquipmentElement.Equals(second),"high-tier death removes its own allocated kit and preserves recruit kit");Check(battle.Winner.LeaderParty.ItemRoster.Single().EquipmentElement.Equals(best),"winner receives actual lost item including quality");Check(!b._plans.ContainsKey("dead"),"defeat invalidates plan");
 b=new Regression();_instance=b;b.rosters["spawn"]=new TroopRoster();b.rosters["spawn"].AddToCounts(top,2);ItemsOf(b._armories,"spawn").AddToCounts(best,1);ItemsOf(b._armories,"spawn").AddToCounts(second,1);var pb=new PartyBase{Id="spawn"};
 var first=TakeKitForAgent(pb,top);var another=TakeKitForAgent(pb,top);Check(first[5].Equals(best)&&another[5].Equals(second),"actual spawn API issues troop-specific kits in allocation order");Check(TakeKitForAgent(pb,top)==null,"actual spawn API cannot issue more kits than allocated soldiers");
 first[0]=default;Mission.Current=new Mission();Check(TakeKitForAgent(pb,top)[0].Equals(sword),"new mission restarts issuance and caller cannot mutate cached equipment");
 Check(TryGetArmoryTiers(pb,top,out w,out a)&&w==0&&a==1,"simulation API uses only this high troop added armour tier");Check(!TryGetArmoryTiers(pb,hero,out w,out a),"hero never receives library simulation bonus");
 b=new Regression();b.rosters["v"]=new TroopRoster();b.rosters["v"].AddToCounts(top,1);b._villages["v"]=new FaithVillage{VillageId="v"};ItemsOf(b._armories,"v").AddToCounts(best,1);var recruiter=new Hero{PartyBelongedTo=new MobileParty{StringId="p"}};b.OnTroopRecruited(recruiter,new Settlement{StringId="v"},new Hero{IsPriest=true},top,1);Check(b.rosters["v"].TotalManCount==0&&ItemsOf(b._armories,"p").Total==1,"last priest recruit takes allocated kit before shadow roster decrement");Check(ItemsOf(b._armories,"v").Total==0,"last priest recruit cannot strand its kit in village ledger");
 b=new Regression();b.rosters["live"]=new TroopRoster();b.rosters["live"].AddToCounts(recruit,1);ItemsOf(b._armories,"live").AddToCounts(best,1);var before=b.PlanOf("live");
 Check(ReferenceEquals(b.PlanOf("live"),before),"unchanged roster and ledger reuse the cached plan");
 b.rosters["live"].AddToCounts(recruit,-1);b.rosters["live"].AddToCounts(top,1);var after=b.PlanOf("live");Check(!ReferenceEquals(after,before)&&For(after,top)[KitHead].Equals(best),"outside battle a vanilla upgrade re-plans without waiting for the hourly refresh");
 Tiers(after.Sets.Where(k=>k.Troop==top),out float tw,out float ta);Check(after.TroopTiers[top]==(tw,ta),"cached simulation tiers equal per-troop recomputation");
 b.battleHolders.Add("live");b.rosters["live"].AddToCounts(top,-1);Check(ReferenceEquals(b.PlanOf("live"),after),"in battle the issued plan stays frozen despite casualties");
 b.battleHolders.Clear();Check(b.PlanOf("live").Sets.Count==0,"plan re-checks once the battle ends");
 // 入库分流：只有灰袍兵能用的兵器护甲进军械库，其余进持有者自己的背包（村子是货仓）。
 b=new Regression();var lordParty=new MobileParty{StringId="lord"};b.parties["lord"]=lordParty;b.rosters["lord"]=new TroopRoster();b.rosters["lord"].AddToCounts(recruit,1);
 var mount=Item("warhorse_loot",ItemObject.ItemTypeEnum.Horse,50);var harnessLoot=Item("harness_loot",ItemObject.ItemTypeEnum.HorseHarness,5);var capeLoot=Item("cape_loot",ItemObject.ItemTypeEnum.Cape,5);
 var tooHard=Item("masterwork",ItemObject.ItemTypeEnum.OneHandedWeapon,99,difficulty:100000);var femaleOnly=Item("gown",ItemObject.ItemTypeEnum.BodyArmor,5);femaleOnly.Item.ItemFlags=ItemFlags.NotUsableByMale;var blade=Item("loot_blade",ItemObject.ItemTypeEnum.OneHandedWeapon,7);
 foreach(var junk in new[]{mount,harnessLoot,capeLoot,tooHard,femaleOnly})Check(!b.Store("lord",junk,1),"unusable loot never enters a lord armory: "+junk.Item.StringId);
 Check(ItemsOf(b._armories,"lord").Total==0&&lordParty.ItemRoster.Total==5,"unusable lord loot goes to the party backpack");
 b.PlanOf("lord");Check(b.Store("lord",blade,2)&&ItemsOf(b._armories,"lord").Total==2&&!b._plans.ContainsKey("lord"),"usable loot enters the armory and invalidates the plan");
 b._villages["vil"]=new FaithVillage{VillageId="vil"};Check(!b.Store("vil",mount,3)&&ItemsOf(b._goods,"vil").Total==3&&ItemsOf(b._armories,"vil").Total==0,"village unusable loot goes to the warehouse");
 Check(b.Store("vil",blade,1)&&ItemsOf(b._armories,"vil").Total==1,"village usable loot enters its armory");
 // 旧档领主库里留下的马随捐赠进村货仓，能用的余量进村军械库。
 ItemsOf(b._armories,"lord").AddToCounts(mount,4);b.rosters["lord"]=new TroopRoster();int donated=b.DonateArmorySurplus("lord","vil");
 Check(donated==2&&ItemsOf(b._armories,"lord").Total==0,"lord surplus fully leaves on donation and only usable items count as donated kits");
 Check(ItemsOf(b._armories,"vil").Total==3&&ItemsOf(b._goods,"vil").Total==7,"legacy horses go to the village warehouse, usable surplus to the village armory");
 // 每日：留够编制以后多出来的，领主进部队背包、村子进货仓；交战中和无领主队不动（用户裁定 2026-10-16）。
 b=new Regression();var lordP=new MobileParty{StringId="lordp"};var cart=new MobileParty{StringId="cart",IsLordParty=false};var fighting=new MobileParty{StringId="fight"};
 b.parties["lordp"]=lordP;b.parties["cart"]=cart;b.parties["fight"]=fighting;b._villages["vv"]=new FaithVillage{VillageId="vv"};
 foreach(var id in new[]{"lordp","cart","fight","vv"}){b.rosters[id]=new TroopRoster();b.rosters[id].AddToCounts(recruit,1);ItemsOf(b._armories,id).AddToCounts(best,1);ItemsOf(b._armories,id).AddToCounts(second,2);}
 b.battleHolders.Add("fight");b.RebalanceArmories();
 Check(ItemsOf(b._armories,"lordp").Total==1&&lordP.ItemRoster.Total==2,"lord keeps the reserved best piece and sends weaker surplus to the backpack");
 Check(ItemsOf(b._armories,"vv").Total==1&&ItemsOf(b._goods,"vv").Total==2,"village surplus goes to its warehouse");
 Check(ItemsOf(b._armories,"cart").Total==3&&cart.ItemRoster.Total==0,"leaderless parties keep their ledger");
 Check(ItemsOf(b._armories,"fight").Total==3&&fighting.ItemRoster.Total==0,"a party in battle keeps its frozen ledger");
 // 骑马的只领马上用得了的武器；步兵不受限。
 var longbow=Item("longbow",ItemObject.ItemTypeEnum.Bow,20);longbow.Item.NoMount=true;var shortbow=Item("shortbow",ItemObject.ItemTypeEnum.Bow,5);
 var rider=Troop("gwrider",6,Gear((0,sword),(6,body),(10,horse)));var walker=Troop("gwwalker",6,Gear((0,sword),(6,body)));
 stock=Stock((longbow,1),(shortbow,1),(arrows,2));plan=BuildPlan(stock,Troops((rider,1),(walker,1)),0);
 Check(For(plan,rider)[KitRanged].Equals(shortbow)&&For(plan,walker)[KitRanged].Equals(longbow),"mounted kit skips a no-mount bow even though it is stronger; the foot soldier takes it");
 Console.WriteLine("ARMORY_ALLOCATION_ASSERTIONS="+checks+"; PASS");return 0;
}
}
"""

class ArmoryAllocationTests(unittest.TestCase):
    def test_production_priority_slots_transfers_and_losses(self):
        source = (ROOT / "GreyWardenPolicePurity/GwpArmoryAllocation.cs").read_text(encoding="utf-8-sig")
        start = source.index("public sealed partial class")
        brace = source.index("{", start)
        end = hygiene.match_brace(source, hygiene.code_mask(source), brace)
        production = source[brace + 1:end]
        production += extract("GwpFireswornBehavior.cs", ("OnTroopRecruited",))
        production += extract("GwpFireswornArmory.cs", ("Take", "KitKind", "Tiers", "MoveBestSets", "TakeBestSets", "MoveAll", "SettleBattleLosses", "PlanOf", "Store", "DonateArmorySurplus", "RebalanceArmories", "RefreshArmoryPlans"))
        armory = (ROOT / "GreyWardenPolicePurity/GwpFireswornArmory.cs").read_text(encoding="utf-8-sig")
        for name in ("TakeKitForAgent", "TryGetArmoryTiers"):
            match = re.search(r"internal static [^\n]+\b" + name + r"\([^)]*\)\s*\{", armory)
            end = hygiene.match_brace(armory, hygiene.code_mask(armory), match.end()-1)+1
            production += armory[match.start():end]
        production += re.search(r"private List<CharacterObject>\? _armoryUsers;", armory).group(0)
        production += re.search(r"private bool IsArmoryItem\([^;]+;", armory).group(0)
        for name in ("CanUse", "AmmoFor", "TierOf"):
            production += re.search(r"(?:internal|private) static [^\n]+\b" + name + r"\([^;]+;", armory).group(0)
        behavior = (ROOT / "GreyWardenPolicePurity/GwpFireswornBehavior.cs").read_text(encoding="utf-8-sig")
        production += re.search(r"internal static bool IsGuardTroop\([^;]+;", behavior).group(0)
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-armory-") as directory:
            path = Path(directory)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>' + f'<TargetFramework>net{major}.0</TargetFramework>' + '<Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(HARNESS.replace("__PRODUCTION__", production), encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"], capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("; PASS", result.stdout)
            print(result.stdout.strip())

if __name__ == "__main__":
    unittest.main()
