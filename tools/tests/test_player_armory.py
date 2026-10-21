"""Player storage/withdrawal/cancel, real allocation and save format.

Production holder, capacity, plan, inventory entry and trim run with freshly
decompiled native stash opening/closing/reset. Engine UI, inventory transfers,
character objects and transaction acceptance are controlled boundaries.
"""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

from test_armory_allocation import HARNESS
from test_training_transport import ROOT, extract, hygiene
from test_duty_banner import method

GAME_DLL = Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll')


def class_body(source):
    brace = source.index('{', source.index('partial class'))
    return source[brace + 1:hygiene.match_brace(source, hygiene.code_mask(source), brace)]


EXTRAS = r'''
class PlayerBountyBehavior{public bool IsRecruitedByGreyWardens;}
class Campaign{public static Campaign Current=new();public PlayerBountyBehavior Bounty=new();public T GetCampaignBehavior<T>() where T:class=>Bounty as T;}
static class GwpRuntimeState{public static class Player{public static int Reputation=20;}}
interface IDataStore{bool IsSaving{get;}bool IsLoading{get;}void SyncData<T>(string key,ref T value);}
class Store:IDataStore{public bool IsSaving{get;set;}public bool IsLoading=>!IsSaving;public Dictionary<string,string> Data=new();public void SyncData<T>(string k,ref T value){if(IsSaving)Data[k]=System.Text.Json.JsonSerializer.Serialize(value);else if(Data.TryGetValue(k,out var s))value=System.Text.Json.JsonSerializer.Deserialize<T>(s);}}
class MBObjectManager{public static MBObjectManager Instance=new();public Dictionary<string,object> Objects=new();public T GetObject<T>(string id) where T:class=>Objects[id] as T;}
class Game{public static Game Current=new();public StateManager GameStateManager=new();}
class StateManager{public object ActiveState;public int Pushes,Pops;public T CreateState<T>() where T:new()=>new();public void PushState(object s){ActiveState=s;Pushes++;}public void PopState(){ActiveState=null;Pops++;}}
class InventoryState{public InventoryScreenHelper.InventoryMode InventoryMode;public InventoryLogic InventoryLogic;public Action DoneLogicExtrasDelegate;}
class TransferCommandResult{}
static class Debug{public static void Print(string s){}}
class EquipmentSnapshot{public void ResetEquipment(){}}
class InventoryLogic{
 public event Action<InventoryLogic,bool> AfterReset;
 ItemRoster[] _rosters=new ItemRoster[2],_rostersBackup=new ItemRoster[2];
 public bool Accept=true;int TransactionDebt;List<int> _transactionHistory=new();EquipmentSnapshot _partyInitialEquipment=new(),_otherPartyInitialEquipment;
 public InventoryLogic(object listener){}
 public void Initialize(ItemRoster left,MobileParty party,bool isTrading,bool isSpecialActionsPermitted,CharacterObject initial,InventoryScreenHelper.InventoryCategoryType category,object market,bool useBasePrices,InventoryScreenHelper.InventoryMode mode,object name){
  if(isTrading||isSpecialActionsPermitted||mode!=InventoryScreenHelper.InventoryMode.Stash)throw new Exception("stash flags changed");
  _rosters[0]=left;_rosters[1]=party.ItemRoster;for(int i=0;i<2;i++){_rostersBackup[i]=new();_rostersBackup[i].Add(_rosters[i]);}}
 public void Reset(bool fromCancel)=>ResetLogic(fromCancel);public bool DoneLogic()=>Accept;
 void InitializeXpGainFromDonations(){}void OnAfterTransfer(List<TransferCommandResult> list){}
 NATIVE_RESET
}
class TextObject{public TextObject(string text){}}
static class InventoryScreenHelper{
 public enum InventoryMode{Stash}public enum InventoryCategoryType{None}
 static object GetCurrentMarketDataForPlayer()=>new();static InventoryState GetActiveInventoryState()=>Game.Current.GameStateManager.ActiveState as InventoryState;
 NATIVE_STASH
 NATIVE_CLOSE
 public static void Close(bool cancel)=>CloseInventoryPresentation(cancel);
}
partial class Regression{
 static bool HasFireswornGuard(Settlement s)=>false;static int GuardCap(FaithVillage v)=>0;static int Sworn(FaithVillage v)=>0;
 PLAYER_PRODUCTION
 static void Transfer(ItemRoster from,ItemRoster to,EquipmentElement item,int n){from.AddToCounts(item,-n);to.AddToCounts(item,n);}
 static int Count(ItemRoster r,EquipmentElement item)=>r.Where(e=>e.EquipmentElement.Equals(item)).Sum(e=>e.Amount);
 static void Main(){
  _instance=new();var b=_instance;var party=new MobileParty{StringId="main_party",IsMainParty=true};party.Party.MobileParty=party;party.Party.Id=party.StringId;MobileParty.MainParty=party;PartyBase.MainParty=party.Party;b.parties[party.StringId]=party;
  var recruit=Troop(GwpIds.NewRecruitId,1,Gear());Troop(GwpIds.ZealotId,1,Gear());party.MemberRoster.AddToCounts(recruit,2);party.Party.PartySizeLimit=3;
  var blade=Item("blade",ItemObject.ItemTypeEnum.OneHandedWeapon,10);var fineModifier=new ItemModifier{StringId="fine",Quality=3};var fine=new EquipmentElement(blade.Item,fineModifier);var weak=Item("weak",ItemObject.ItemTypeEnum.OneHandedWeapon,1);var horse=Item("horse",ItemObject.ItemTypeEnum.Horse);
  party.ItemRoster.AddToCounts(fine,5);party.ItemRoster.AddToCounts(weak,4);party.ItemRoster.AddToCounts(horse,2);
  Check(PlayerArmoryItems.Total==0&&!b._armories.ContainsKey(party.StringId),"browsing does not manufacture stock");
  Check(!OpenPlayerArmory()&&Game.Current.GameStateManager.Pushes==0,"unjoined Easter egg cannot manage stock");Campaign.Current.Bounty.IsRecruitedByGreyWardens=true;
  GwpRuntimeState.Player.Reputation=19;Check(!OpenPlayerArmory(),"standing gate applies to actual management");GwpRuntimeState.Player.Reputation=20;
  party.MapEvent=new();Check(!OpenPlayerArmory(),"combat cannot edit issued equipment");party.MapEvent=null;
  Check(b.HolderOf(party.Party)==party.StringId,"player troop equipment resolves to its own saved ledger");
  Check(OpenPlayerArmory(),"qualified player opens native stash");var ledger=PlayerArmoryItems;
  Transfer(party.ItemRoster,ledger,weak,4);Transfer(party.ItemRoster,ledger,fine,5);Transfer(party.ItemRoster,ledger,horse,2);
  InventoryScreenHelper.Close(false);
  Check(Count(ledger,fine)==3&&Count(ledger,weak)==0&&Count(ledger,horse)==0,"same plan keeps best gear to capacity and rejects mounts");
  Check(Count(party.ItemRoster,fine)==2&&Count(party.ItemRoster,weak)==4&&Count(party.ItemRoster,horse)==2,"overflow returns to backpack without deletion");
  Check(ledger.Total+party.ItemRoster.Total==11,"all manual transfer quantities conserved");
  var plan=b.PlanOf(party.StringId);Check(plan.Sets.Count==2&&For(plan,recruit)[KitMain].Equals(fine),"actual player troops receive the best stored equipment");
  Check(TakeKitForAgent(party.Party,recruit)[0].Equals(fine),"production spawn equipment uses player ledger");
  Check(OpenPlayerArmory(),"reopen after commit");Transfer(ledger,party.ItemRoster,fine,1);InventoryScreenHelper.Close(false);
  Check(Count(ledger,fine)==2&&Count(party.ItemRoster,fine)==3,"withdrawal removes actual armory equipment");
  Check(OpenPlayerArmory(),"reopen before cancellation");Transfer(ledger,party.ItemRoster,fine,2);Transfer(party.ItemRoster,ledger,weak,4);InventoryScreenHelper.Close(true);
  Check(Count(ledger,fine)==2&&Count(ledger,weak)==0&&Count(party.ItemRoster,fine)==3&&Count(party.ItemRoster,weak)==4,"native reset and cancel-aware callback restore both inventories");
  Check(OpenPlayerArmory(),"reopen before transaction rejection");var state=(InventoryState)Game.Current.GameStateManager.ActiveState;state.InventoryLogic.Accept=false;int pops=Game.Current.GameStateManager.Pops;InventoryScreenHelper.Close(false);
  Check(Game.Current.GameStateManager.Pops==pops&&state.DoneLogicExtrasDelegate!=null,"failed native transaction does not normalize or close");state.InventoryLogic.Accept=true;InventoryScreenHelper.Close(true);
  MBObjectManager.Instance.Objects[blade.Item.StringId]=blade.Item;MBObjectManager.Instance.Objects[fineModifier.StringId]=fineModifier;
  var store=new Store{IsSaving=true};SyncItemRosters(store,"GWPP_FsArmory",b._armories);var restored=new Dictionary<string,ItemRoster>();store.IsSaving=false;SyncItemRosters(store,"GWPP_FsArmory",restored);
  Check(Count(restored[party.StringId],fine)==2,"production existing save format retains main party stock and modifier");
  Check(store.Data.Count==4,"player uses four existing armory keys without a new save schema");
  party.MemberRoster.AddToCounts(recruit,-1);party.Party.PartySizeLimit=1;b.RebalanceArmories();
  Check(Count(ledger,fine)==1&&Count(party.ItemRoster,fine)==4,"daily capacity shrink returns excess to player backpack");
  party.MemberRoster.AddToCounts(recruit,2);Check(ArmoryCapacity(party)==3,"over-limit actual soldiers keep the same capacity rule as NPCs");
  // Full loan snapshots retain the first round dead and surviving quality through native Update and save/load.
  b=new Regression();_instance=b;party.MapEvent=new MapEvent();party.Party.MapEventSide=party.MapEvent.Loser;
  var combatParty=new MapEventParty{Party=party.Party};party.MapEvent.Loser.Parties.Add(combatParty);party.MapEvent.Winner.LeaderParty=new PartyBase{Id="enemy"};
  var firstMan=new FlattenedTroopRosterElement{Troop=recruit,Descriptor=new UniqueTroopDescriptor{UniqueSeed=10}};
  var secondMan=new FlattenedTroopRosterElement{Troop=recruit,Descriptor=new UniqueTroopDescriptor{UniqueSeed=20}};
  combatParty.Troops.Add(firstMan);combatParty.Troops.Add(secondMan);ledger=ItemsOf(b._armories,party.StringId);
  ledger.AddToCounts(fine,1);ledger.AddToCounts(blade,1);ledger.AddToCounts(weak,1);
  b.CaptureArmoryBattleOwners(party.MapEvent);
  Check(TakeKitForAgent(party.Party,recruit,10)[0].Equals(fine)&&TakeKitForAgent(party.Party,recruit,20)[0].Equals(blade),"actual battle loans distinguish same troop qualities");
  firstMan.IsKilled=true;BeforeArmoryRosterUpdate(combatParty);combatParty.Troops.Clear();
  combatParty.Troops.Add(new FlattenedTroopRosterElement{Troop=recruit,Descriptor=new UniqueTroopDescriptor{UniqueSeed=30}});AfterArmoryRosterUpdate(combatParty);
  Check(TakeKitForAgent(party.Party,recruit,30)[0].Equals(blade)&&b._battleLoadouts[combatParty].BySeed[10].Items[KitMain].Equals(fine),"native roster rebuild preserves dead kit and rebinds survivor without reissuing it");
  MBObjectManager.Instance.Objects[weak.Item.StringId]=weak.Item;
  var battleSave=new Store{IsSaving=true};b.SyncArmoryBattles(battleSave);b._battleLoadouts.Clear();b._battleOwners.Clear();battleSave.IsSaving=false;b.SyncArmoryBattles(battleSave);b.RestoreArmoryBattleLoadouts();
  Check(TakeKitForAgent(party.Party,recruit,30)[0].Equals(blade)&&b._battleLoadouts[combatParty].BySeed[10].Items[KitMain].Equals(fine),"saved primitive loan metadata retains exact items and modifier across battle stages");
  Check(ledger.Total==3,"snapshot load never creates or removes real inventory");
  b.SettleBattleLosses(party.MapEvent);
  Check(Count(ledger,weak)==1&&Count(ledger,fine)==0&&Count(ledger,blade)==0,"full defeat deducts archived dead and surviving captured kit, preserving unissued stock");
  Check(party.MapEvent.Winner.LeaderParty.ItemRoster.Total==2,"33 percent loot rolls award actual battlefield loan items once");
  battleSave.Data.Clear();b.SyncArmoryBattles(battleSave);b.RestoreArmoryBattleLoadouts();
  Check(ledger.Total==1&&b._battleLoadouts.Count==0,"absent metadata in old saves preserves existing resources");
  Hero.MainHero.IsPrisoner=true;party.Party.PartySizeLimit=0;party.MemberRoster=new TroopRoster();b.RebalanceArmories();
  Check(ledger.Total==1,"captured player daily trim preserves unissued reserve");Hero.MainHero.IsPrisoner=false;
  // Original capture code removes the party leader BEFORE MapEventEnded.
  b=new Regression();_instance=b;var lord=new Hero{StringId="tested_lord"};Hero.All[lord.StringId]=lord;
  var army=new MobileParty{StringId="tested_army",LeaderHero=lord};lord.PartyBelongedTo=army;army.MapEvent=new MapEvent();army.Party.MapEventSide=army.MapEvent.Loser;
  var lordBattle=new MapEventParty{Party=army.Party};lordBattle.Troops.Add(new FlattenedTroopRosterElement{Troop=recruit,Descriptor=new UniqueTroopDescriptor{UniqueSeed=99},IsKilled=true});army.MapEvent.Loser.Parties.Add(lordBattle);
  ItemsOf(b._armories,army.StringId).AddToCounts(fine,2);b.CaptureArmoryBattleOwners(army.MapEvent);army.LeaderHero=null;lord.PartyBelongedTo=null;
  b.SettleBattleLosses(army.MapEvent);
  Check(Count(ItemsOf(b._armories,LordArmoryId(lord)),fine)==1&&!b._armories.ContainsKey(army.StringId),"pre-capture owner record deducts actual kit after native leader removal and retains personal reserve");
  Console.WriteLine("PLAYER_ARMORY_ASSERTIONS="+checks+"; PASS; production allocation, transfer entry/trim/save + installed stash open/close/reset; UI/transfer commands and transaction acceptance controlled");
 }
}
'''


class PlayerArmoryTests(unittest.TestCase):
    def test_storage_allocation_cancel_capacity_and_save(self):
        allocation=(ROOT/'GreyWardenPolicePurity/GwpArmoryAllocation.cs').read_text(encoding='utf-8-sig')
        source=(ROOT/'GreyWardenPolicePurity/GwpFireswornArmory.cs').read_text(encoding='utf-8-sig')
        player=(ROOT/'GreyWardenPolicePurity/GwpPlayerArmory.cs').read_text(encoding='utf-8-sig')
        production=class_body(allocation)+extract('GwpFireswornArmory.cs',('Take','KitKind','Tiers','MoveAll','BuildPlanFor','PlanOf','RebalanceArmories','RefreshArmoryPlans','HolderOf'))
        production+=class_body((ROOT/'GreyWardenPolicePurity/GwpArmoryBattle.cs').read_text(encoding='utf-8-sig'))
        production+=class_body((ROOT/'GreyWardenPolicePurity/GwpArmoryOwnership.cs').read_text(encoding='utf-8-sig'))
        production+=class_body((ROOT/'GreyWardenPolicePurity/GwpArmoryBattleSave.cs').read_text(encoding='utf-8-sig'))
        production+=method(source,'TakeKitForAgent')
        for name in ('CanUse','AmmoFor','TierOf','ArmoryCapacity'):
            production+=method(source,name)
        production+=extract('GwpFireswornBehavior.cs',('SyncItemRosters',))
        behavior=(ROOT/'GreyWardenPolicePurity/GwpFireswornBehavior.cs').read_text(encoding='utf-8-sig')
        production+=method(behavior,'IsGuardTroop')
        base=HARNESS
        match=re.search(r'(?:public\s+)?static int Main\(\)\s*\{',base)
        end=hygiene.match_brace(base,hygiene.code_mask(base),match.end()-1)+1
        base=base[:match.start()]+base[end:]
        base=re.sub(r'ArmoryPlan BuildPlanFor\(string id\)=>[^;]+;','',base)
        base=base.replace('string HolderOf(PartyBase p)=>p.Id;','')
        base=base.replace('class ItemModifier {}','class ItemModifier {public string StringId;public int Quality;}')
        base=base.replace('public int Quality;public bool IsEmpty','public int Quality;public ItemModifier ItemModifier;public bool IsEmpty')
        base=base.replace('Item=item;Quality=0;','Item=item;Quality=0;ItemModifier=null;')
        base=base.replace('public EquipmentElement(ItemObject item){','public EquipmentElement(ItemObject item,ItemModifier modifier){Item=item;ItemModifier=modifier;Quality=modifier?.Quality??0;}public EquipmentElement(ItemObject item){')
        base=base.replace('class ItemRoster:IEnumerable<ItemRosterElement> {','class ItemRoster:IEnumerable<ItemRosterElement> {public void Clear(){rows.Clear();VersionNo++;}')
        base=base.replace('class MobileParty {','class MobileParty {public static MobileParty MainParty;public bool IsMilitia;public Settlement HomeSettlement;')
        base=base.replace('class PartyBase {','class PartyBase {public int PartySizeLimit;')
        base=base.replace('class CharacterObject {','class CharacterObject {public static CharacterObject PlayerCharacter=new();')
        base=base.replace('class Settlement {','class Settlement {public Militia MilitiaPartyComponent;}class Militia{public MobileParty MobileParty;}class UnusedSettlement {')
        base=base.replace('static class GwpTuning {','static class GwpTuning {public static class Bounty{public const int RecruitmentReputationThreshold=20;}')
        # The standalone field stubs use a class with StringId for both settlement types.
        base=base.replace('class Settlement {public Militia','class Settlement {public string StringId;public Militia')
        for type_name in ('Helpers.InventoryScreenHelper','TaleWorlds.CampaignSystem.Inventory.InventoryLogic'):
            native=subprocess.check_output(['ilspycmd','-t',type_name,str(GAME_DLL)],text=True,encoding='utf-8-sig')
            if type_name.startswith('Helpers'):
                extras=EXTRAS.replace('NATIVE_STASH',method(native,'OpenScreenAsStash')).replace('NATIVE_CLOSE',method(native,'CloseInventoryPresentation'))
            else:
                extras=extras.replace('NATIVE_RESET',method(native,'ResetLogic'))
        code=base.replace('__PRODUCTION__',production)+extras.replace('PLAYER_PRODUCTION',class_body(player))
        major=subprocess.check_output(['dotnet','--version'],text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-player-armory-') as directory:
            p=Path(directory)
            (p/'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers><NoWarn>CS0649</NoWarn></PropertyGroup></Project>',encoding='utf-8')
            (p/'Program.cs').write_text(code,encoding='utf-8')
            result=subprocess.run(['dotnet','run','--project',directory,'-c','Release','-v','quiet'],capture_output=True,text=True,timeout=60)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('PLAYER_ARMORY_ASSERTIONS=')))


if __name__=='__main__':
    unittest.main()
