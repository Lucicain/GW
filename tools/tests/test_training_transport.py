"""Compile production logistics, persistence and armory allocation against engine-boundary stubs."""
import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("hygiene", ROOT / "tools/Check-CodeHygiene.py")
hygiene = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hygiene)


def extract(file, names):
    source = (ROOT / "GreyWardenPolicePurity" / file).read_text(encoding="utf-8-sig")
    mask = hygiene.code_mask(source)
    result = []
    for name in names:
        match = re.search(r"(?m)^        (?:private|public) [^\n]+\b" + name + r"\([^)]*\)\s*\{", source)
        assert match, name
        end = hygiene.match_brace(source, mask, match.end() - 1) + 1
        result.append(source[match.start():end].replace("public override void", "public void"))
    return "\n".join(result)


HARNESS = r'''
using System; using System.Collections; using System.Collections.Generic; using System.Linq;
class Vec { public float X; public float Distance(Vec p)=>Math.Abs(X-p.X); public float DistanceSquared(Vec p)=>Distance(p)*Distance(p); }
class CharacterObject { public string StringId; public int Tier=6; public bool IsHero; public CharacterObject[] UpgradeTargets=Array.Empty<CharacterObject>(); public static CharacterObject Find(string id)=>new CharacterObject{StringId=id}; }
struct TroopRosterElement { public CharacterObject Character; public int Number,WoundedNumber; public TroopRosterElement(CharacterObject c){Character=c;Number=WoundedNumber=0;} }
class TroopRoster {
 public List<TroopRosterElement> Rows=new List<TroopRosterElement>(); public int TotalManCount=>Rows.Sum(e=>e.Number);
 public IEnumerable<TroopRosterElement> GetTroopRoster()=>Rows.ToList();
 public void AddToCounts(CharacterObject t,int n){int i=Rows.FindIndex(e=>e.Character==t);var e=i<0?new TroopRosterElement(t):Rows[i];e.Number+=n;if(e.Number<0)throw new Exception("negative soldiers");if(i<0)Rows.Add(e);else Rows[i]=e;}
}
class Hero {public string StringId="trainer"; public bool IsActive=true;}
class PartyBase {public int PartySizeLimit=20;}
class MobileParty {
 public static List<MobileParty> All=new List<MobileParty>(); public string StringId; public bool IsActive=true,IsCurrentlyAtSea,IsDisbanding,Reachable=true;
 public Hero LeaderHero; public object MapEvent; public Vec Position=new Vec(); public Vec GetPosition2D=>Position;public int NavigationCapability;
 public TroopRoster MemberRoster=new TroopRoster(),PrisonRoster=new TroopRoster();public ItemRoster ItemRoster=new ItemRoster(); public int PartyTradeGold;public PartyBase Party=new PartyBase();
 public MobileParty(string id){StringId=id;All.Add(this);}
}
class Settlement {public static Dictionary<string,Settlement> All=new Dictionary<string,Settlement>();public string StringId;public string Name=>StringId;public bool Quiet=true;public Vec GetPosition2D=new Vec();public TroopRoster Guard=new TroopRoster(); public static Settlement Find(string id)=>All[id];}
class CampaignTime {public static CampaignTime Now=new CampaignTime();public double ToHours;public int GetDayOfSeason;public const int DaysInWeek=7,WeeksInSeason=3;}
static class MBRandom { public static float RandomFloat; }
class ItemObject {public int Kind;public ItemTypeEnum ItemType;public enum ItemTypeEnum{OneHandedWeapon,Bow,Arrows} }
struct EquipmentElement {public ItemObject Item;public int Quality;public bool IsEmpty=>Item==null;}
class ItemRosterElement {public EquipmentElement EquipmentElement;public int Amount;}
class ItemRoster:IEnumerable<ItemRosterElement> {
 readonly List<ItemRosterElement> rows=new List<ItemRosterElement>();public int VersionNo;public int Count=>rows.Count;public int Total=>rows.Sum(e=>e.Amount);
 public void AddToCounts(EquipmentElement e,int n){var row=rows.FirstOrDefault(r=>r.EquipmentElement.Quality==e.Quality&&r.EquipmentElement.Item==e.Item);if(row==null){row=new ItemRosterElement{EquipmentElement=e};rows.Add(row);}row.Amount+=n;if(row.Amount<0)throw new Exception("negative kit stock");VersionNo++;if(row.Amount==0)rows.Remove(row);}
 public ItemRosterElement GetElementCopyAtIndex(int i)=>rows[i];public IEnumerator<ItemRosterElement> GetEnumerator()=>rows.ToList().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();public void Clear(){rows.Clear();VersionNo++;}public void Add(ItemRoster r){foreach(var e in r)AddToCounts(e.EquipmentElement,e.Amount);}
}
class IDataStore {public bool IsSaving;public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new Dictionary<string,object>();public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v;else v=(T)Data[k];}}
static class GwpIds {public const string NewRecruitId="recruit",ZealotId="zealot";}
static class GwpTuning {public static class Training {__TRAINING_TUNING__} public static class Firesworn {public const int MissionMinimumLeftBehind=5,BalanceEscortSize=5;__FESTIVAL_TUNING__ public const double FaithPartyTravelTimeoutHours=100; public const float ArrivalDistance=3;} }
enum WardenDuty {PlayerTroopOrder,PlayerRequest}
static class GwpWardenDuties {public static bool HoldsAny(MobileParty p,params WardenDuty[] d)=>false;}
static class PoliceStats {public static IEnumerable<MobileParty> GetAllPoliceParties()=>MobileParty.All.Where(p=>p.LeaderHero!=null);}
static class GreyWardenFamilyBehavior {public static bool IsTrainingParty(MobileParty p)=>p.LeaderHero!=null;}
static class GwpAiDiagnostics {public static void WriteFaith(string a,string b){} }
static class GwpWardenDispatchBehavior {public static void KeepCourierDisposition(MobileParty p){} }
static class GreyWardenPartyDesireBehavior {public static void RequestEscort(MobileParty p,MobileParty t){}public static void ClearIntent(MobileParty p){} }
static class NavigationHelper {public static bool IsPositionValidForNavigationType(Vec v,int n)=>true;}
static class DestroyPartyAction {public static void Apply(object a,MobileParty p){p.IsActive=false;} }
static class GwpTroopTransfer {
 public static TroopRosterElement Move(TroopRoster a,TroopRoster b,CharacterObject t,int n,bool healthyOnly=false){n=Math.Min(n,a.GetTroopRoster().Where(e=>e.Character==t).Sum(e=>e.Number-(healthyOnly?e.WoundedNumber:0)));a.AddToCounts(t,-n);b.AddToCounts(t,n);return new TroopRosterElement(t){Number=n};}
 public static int MoveBatches(TroopRoster a,TroopRoster b,IEnumerable<TroopRosterElement> rows,int n,bool healthyOnly=false){int moved=0;foreach(var e in rows.ToList()){moved+=Move(a,b,e.Character,Math.Min(e.Number,n-moved),healthyOnly).Number;if(moved==n)break;}return moved;}
}
class Regression {
 const double Never=-10000;static int checks;static void Check(bool c,string m){checks++;if(!c)throw new Exception(m);}
 enum FaithPartyKind{Mission,Market,Balance,Training} enum FaithPartyPhase{Home,Outbound,Returning} enum RitualKind{Congregation}
 class FaithVillage{public string VillageId,PriestId="p";public double LastCongregation,LastDamnation,LastMission,LastMarket,LastBalance;public int Households,MissionsSent;}
 class FaithParty{public string PartyId,HomeId,TargetId,TrainerId;public FaithPartyKind Kind;public FaithPartyPhase Phase;public double PhaseHour;public int Stops;}
 class Ritual{public string VillageId;public RitualKind Kind;public double EndHour;}
 readonly Dictionary<string,FaithVillage> _villages=new Dictionary<string,FaithVillage>();readonly List<Ritual> _rituals=new List<Ritual>();
 readonly Dictionary<string,FaithParty> _faithParties=new Dictionary<string,FaithParty>();readonly Dictionary<string,double> _trainingDepartures=new Dictionary<string,double>();
 readonly Dictionary<string,TroopRoster> _guards=new Dictionary<string,TroopRoster>(),_dungeons=new Dictionary<string,TroopRoster>();
 readonly Dictionary<string,ItemRoster> _armories=new Dictionary<string,ItemRoster>(),_goods=new Dictionary<string,ItemRoster>();
 class ArmoryPlan {public int Recruits,Zealots,Reserve,LedgerVersion;public ItemRoster Ledger;public List<EquipmentElement[]> Sets=new List<EquipmentElement[]>();public ItemRoster Free=new ItemRoster(),Surplus=new ItemRoster();public float RecruitWeaponTier,RecruitArmorTier,ZealotWeaponTier,ZealotArmorTier;}
 readonly Dictionary<string,ArmoryPlan> _plans=new Dictionary<string,ArmoryPlan>();
 const int KitSlots=9,WeaponSlots=4,KitHead=0,KitBody=1,KitLeg=2,KitGloves=3,KitMain=4,KitShield=5,KitThrow=6,KitRanged=7,KitAmmo=8;
 static int KitKind(ItemObject i)=>i.Kind;static bool CanUse(CharacterObject t,ItemObject i)=>true;static float Strength(EquipmentElement e)=>e.Quality;static bool Stronger(EquipmentElement e,EquipmentElement own)=>own.IsEmpty||e.Quality>own.Quality;
 static bool StrongOwn;
 static EquipmentElement[] OwnBest(CharacterObject t){var own=new EquipmentElement[9];if(StrongOwn)own[KitMain]=new EquipmentElement{Item=new ItemObject{Kind=KitMain},Quality=100};return own;}static ItemObject.ItemTypeEnum AmmoFor(ItemObject i)=>ItemObject.ItemTypeEnum.Arrows;
 static EquipmentElement Take(List<EquipmentElement> b,Func<EquipmentElement,bool> f){int i=b.FindIndex(x=>f(x));if(i<0)return default;var e=b[i];b.RemoveAt(i);return e;}
 static void Tiers(IEnumerable<EquipmentElement[]> s,out float w,out float a){w=a=0;}
 static ItemRoster ItemsOf(Dictionary<string,ItemRoster> d,string id){if(!d.ContainsKey(id))d[id]=new ItemRoster();return d[id];}
 ArmoryPlan PlanOf(string id)=>new ArmoryPlan();
 static int MoveAll(ItemRoster what,ItemRoster a,ItemRoster b){int n=0;foreach(var e in what.ToList()){a.AddToCounts(e.EquipmentElement,-e.Amount);b.AddToCounts(e.EquipmentElement,e.Amount);n+=e.Amount;}return n;}
 static bool IsGuardTroop(CharacterObject t)=>!t.IsHero;
 static MobileParty ResolveTrainer(string id)=>MobileParty.All.FirstOrDefault(p=>p.IsActive&&p.LeaderHero?.StringId==id);
 static bool IsTripTargetValid(Settlement s)=>s.Quiet;static bool IsPreachedVillage(Settlement s)=>true;static bool HasRitual(Settlement s)=>false;
 static int StableDay(string id,int period)=>0;
 __FESTIVAL_METHOD__
 static bool HasArrived(MobileParty p,Settlement s)=>p.Position.Distance(s.GetPosition2D)<=3;
 static void Travel(MobileParty p,Settlement s){}static int FreeSlots(MobileParty p)=>Math.Max(0,p.Party.PartySizeLimit-p.MemberRoster.TotalManCount);
 int transportedKits;void MoveBestSets(string a,string b,CharacterObject t,int n){transportedKits+=n;}void UnloadKits(MobileParty p,string id){MoveAll(p.ItemRoster,p.ItemRoster,ItemsOf(_armories,id));}
 void ReturnPartyLedger(MobileParty p,string id){MoveAll(ItemsOf(_armories,p.StringId),ItemsOf(_armories,p.StringId),ItemsOf(_armories,id));}
 static void CreditTreasury(int n){}static Settlement NearestFaithVillage(MobileParty p)=>Settlement.All.Values.FirstOrDefault(v=>v.Quiet);
 void JoinVillage(FaithParty r,MobileParty p,Settlement v){GwpTroopTransfer.MoveBatches(p.MemberRoster,v.Guard,p.MemberRoster.GetTroopRoster(),p.MemberRoster.TotalManCount);ReturnPartyLedger(p,v.StringId);UnloadKits(p,v.StringId);_faithParties.Remove(p.StringId);p.IsActive=false;}
 static void SetPhase(FaithParty r,FaithPartyPhase phase,string target){r.Phase=phase;r.TargetId=target;r.PhaseHour=CampaignTime.Now.ToHours;}
 MobileParty CreateFaithParty(FaithVillage row,Settlement home,FaithPartyKind kind,MobileParty origin){var p=new MobileParty("transport"+MobileParty.All.Count);p.Position.X=origin.Position.X;_faithParties[p.StringId]=new FaithParty{PartyId=p.StringId,HomeId=home.StringId,Kind=kind};return p;}
 static void Dispatch(FaithParty r,MobileParty p,Settlement v)=>SetPhase(r,FaithPartyPhase.Outbound,v.StringId);
 static void SyncTroopRosters(IDataStore d,string k,Dictionary<string,TroopRoster> r){}static void SyncItemRosters(IDataStore d,string k,Dictionary<string,ItemRoster> r){}void RebuildPriestIndex(){}
 __METHODS__
 static CharacterObject Veteran=new CharacterObject{StringId="veteran"};static CharacterObject Weak=new CharacterObject{StringId="weak",UpgradeTargets=new[]{Veteran}};
 static (Regression r,MobileParty trainer,Settlement village) Start(){MobileParty.All.Clear();Settlement.All.Clear();CampaignTime.Now.ToHours=1;CampaignTime.Now.GetDayOfSeason=0;MBRandom.RandomFloat=0;var r=new Regression();var t=new MobileParty("lord"){LeaderHero=new Hero()};t.MemberRoster.AddToCounts(Veteran,15);t.Position.X=50;var v=new Settlement{StringId="v"};v.Guard.AddToCounts(Weak,10);Settlement.All[v.StringId]=v;r._villages[v.StringId]=new FaithVillage{VillageId=v.StringId};return(r,t,v);}
 static (FaithParty record,MobileParty party) DispatchOne(Regression r){r.DispatchTrainingTransports();var record=r._faithParties.Values.Single();return(record,MobileParty.All.Single(p=>p.StringId==record.PartyId));}
 public static int Main(){
  var (r,t,v)=Start();var (record,p)=DispatchOne(r);
  Check(t.MemberRoster.TotalManCount==5&&p.MemberRoster.TotalManCount==10,"real veterans leave trainer; five stay");
  Check(r.transportedKits==10,"veteran dispatch invokes troop-specific kit transfer");
  r.DispatchTrainingTransports();Check(r._faithParties.Count==1,"one in-flight shipment per trainer");
  // Save and load actual production fields while physical engine parties remain registered.
  var store=new IDataStore{IsSaving=true};r.SyncData(store);var loaded=new Regression();store.IsSaving=false;loaded.SyncData(store);r=loaded;record=r._faithParties.Values.Single();
  Check(record.TrainerId=="trainer"&&record.HomeId=="v"&&record.Phase==FaithPartyPhase.Outbound,"outbound save/load owner and destination");Check(r._trainingDepartures["trainer"]==1,"dispatch cooldown survives save/load");
  p.MemberRoster.AddToCounts(Veteran,-3);p.Position.X=0;r.TendTrainingTransport(record,p);
  Check(v.Guard.TotalManCount==17&&v.Guard.GetTroopRoster().Single(e=>e.Character==Veteran).Number==7,"only seven surviving veterans delivered; ten trainees stay");
  Check(p.MemberRoster.TotalManCount==0&&!p.IsActive&&r._faithParties.Count==0,"one-way delivery closes transport without slow return leg");
  Check(t.MemberRoster.TotalManCount==5,"trainer receives no remote replacements");
  store.IsSaving=true;r.SyncData(store);loaded=new Regression();store.IsSaving=false;loaded.SyncData(store);r=loaded;Check(r._faithParties.Count==0,"completed delivery stays completed after save/load");
  t.MemberRoster.AddToCounts(Veteran,10);
  r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"cooldown prevents immediate repeat");
  CampaignTime.Now.ToHours=361;(record,p)=DispatchOne(r);Check(p.MemberRoster.TotalManCount==10,"new shipment possible after cooldown and previous delivery");
  (r,t,v)=Start();(record,p)=DispatchOne(r);t.MemberRoster.AddToCounts(Veteran,15);t.MapEvent=new object();t.IsCurrentlyAtSea=true;p.Position.X=0;r.TendTrainingTransport(record,p);
  Check(!p.IsActive&&v.Guard.TotalManCount==20&&t.MemberRoster.TotalManCount==20,"delivery independent of trainer capacity, battle and sea movement");
  (r,t,v)=Start();(record,p)=DispatchOne(r);t.IsActive=false;r.TendTrainingTransport(record,p);Check(p.IsActive&&record.TrainerId=="","lost trainer triggers physical return");p.Position.X=0;r.TendTrainingTransport(record,p);Check(!p.IsActive&&v.Guard.TotalManCount==20,"orphan transport settles rather than disappears");
  (r,t,v)=Start();(record,p)=DispatchOne(r);v.Quiet=false;r.TendTrainingTransport(record,p);Check(p.IsActive&&v.Guard.TotalManCount==10,"no safe village preserves actual transport without teleporting");
  var safe=new Settlement{StringId="safe"};safe.GetPosition2D.X=20;Settlement.All[safe.StringId]=safe;r.TendTrainingTransport(record,p);Check(record.Phase==FaithPartyPhase.Returning&&record.HomeId=="safe"&&record.TrainerId=="","unsafe target reroutes to safe village");
  store.IsSaving=true;r.SyncData(store);loaded=new Regression();store.IsSaving=false;loaded.SyncData(store);r=loaded;record=r._faithParties.Values.Single();Check(record.HomeId=="safe"&&record.Phase==FaithPartyPhase.Returning,"fallback stage and destination survive save/load");
  p.Position.X=20;r.TendTrainingTransport(record,p);Check(!p.IsActive&&safe.Guard.TotalManCount==10&&t.MemberRoster.TotalManCount==5,"rerouted veterans settle physically without returning to trainer");
  (r,t,v)=Start();(record,p)=DispatchOne(r);CampaignTime.Now.ToHours=102;r.TendTrainingTransport(record,p);Check(record.TrainerId=="","timeout stops chasing trainer");p.Position.X=0;r.TendTrainingTransport(record,p);Check(!p.IsActive,"timed out carrier settles");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,-6);r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"never send fewer than five or empty the trainer");
  (r,t,v)=Start();v.Guard.AddToCounts(Weak,-10);(record,p)=DispatchOne(r);p.Position.X=0;r.TendTrainingTransport(record,p);Check(!p.IsActive&&v.Guard.TotalManCount==10,"no trainees required for one-way veteran delivery");
  (r,t,v)=Start();t.MapEvent=new object();r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"battle prevents dispatch");
  Check(TrainingVeteransToDispatch(100,50)==0,"exactly one half does not trigger");
  Check(TrainingVeteransToDispatch(100,49)==0,"below one half does not trigger");
  Check(TrainingVeteransToDispatch(100,76)==52,"strictly above three quarters leaves twenty-four veterans among forty-eight");
  Check(TrainingVeteransToDispatch(100,80)==60,"eighty of one hundred sends sixty, leaving twenty of forty");
  Check(TrainingVeteransToDispatch(101,80)==59,"odd total also reaches exactly one half");
  Check(TrainingVeteransToDispatch(10,8)==5,"existing five-person retention floor takes priority");
  Check(TrainingVeteransToDispatch(5,5)==0,"never empty a small trainer party");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,65);t.MemberRoster.AddToCounts(Weak,20);(record,p)=DispatchOne(r);
  Check(p.MemberRoster.TotalManCount==60&&t.MemberRoster.TotalManCount==40&&t.MemberRoster.GetTroopRoster().Single(e=>e.Character==Veteran).Number==20,"dispatch uses remaining roster denominator in production");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,65);t.MemberRoster.AddToCounts(Weak,20);int vi=t.MemberRoster.Rows.FindIndex(e=>e.Character==Veteran);var wound=t.MemberRoster.Rows[vi];wound.WoundedNumber=30;t.MemberRoster.Rows[vi]=wound;r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"wounded veterans do not count toward trigger");
  wound.WoundedNumber=27;t.MemberRoster.Rows[vi]=wound;(record,p)=DispatchOne(r);Check(p.MemberRoster.TotalManCount==6&&t.MemberRoster.Rows[vi].WoundedNumber==27,"healthy-only dispatch leaves wounded at home");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,35);t.MemberRoster.AddToCounts(Weak,49);t.MemberRoster.AddToCounts(new CharacterObject{StringId="hero",IsHero=true},1);r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"hero belongs to full roster denominator but is never a veteran candidate");
  Check(TrainingVeteransToDispatch(100,60)==20,"sixty percent sends twenty, leaving forty of eighty");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,45);t.MemberRoster.AddToCounts(Weak,40);MBRandom.RandomFloat=0.35f;r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"base probability excludes boundary thirty-five percent");
  MBRandom.RandomFloat=0.349f;(record,p)=DispatchOne(r);Check(p.MemberRoster.TotalManCount==20,"over half can dispatch at base probability");
  (r,t,v)=Start();t.MemberRoster.AddToCounts(Veteran,60);t.MemberRoster.AddToCounts(Weak,25);MBRandom.RandomFloat=0.4f;r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"exactly seventy-five percent does not double chance");
  t.MemberRoster.AddToCounts(Veteran,1);t.MemberRoster.AddToCounts(Weak,-1);MBRandom.RandomFloat=0.7f;r.DispatchTrainingTransports();Check(r._faithParties.Count==0,"boosted probability excludes boundary seventy percent");
  MBRandom.RandomFloat=0.699f;(record,p)=DispatchOne(r);Check(p.MemberRoster.TotalManCount==52,"over seventy-five percent uses doubled probability");
  var festival=new FaithVillage{VillageId="v"};CampaignTime.Now.GetDayOfSeason=7;MBRandom.RandomFloat=0;Check(!IsFestivalDay(festival,Never,CampaignTime.Now.ToHours,2f),"boost never bypasses village week");
  CampaignTime.Now.GetDayOfSeason=0;Check(!IsFestivalDay(festival,0,359,2f)&&IsFestivalDay(festival,0,360,2f),"boost preserves actual fifteen-day cooldown boundary");
  MBRandom.RandomFloat=0.4f;Check(!IsFestivalDay(festival,Never,360),"mission and balance default probability unchanged");
  Console.WriteLine("TRAINING_LOGISTICS_ASSERTIONS="+checks+"; PASS");return 0;
 }
}
'''


class TrainingTransportTests(unittest.TestCase):
    def test_one_way_delivery_ratio_persistence_and_armory(self):
        methods = extract("GwpTrainingTransports.cs", (
            "DispatchTrainingTransports", "TendTrainingTransport", "SettleTrainingTransport",
            "TransferEliteTroopsProportionally", "TrainingVeteransToDispatch"))
        methods += extract("GwpFireswornBehavior.cs", ("SyncData",))
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-training-") as directory:
            path = Path(directory)
            (path / "Test.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable>'
                '<NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            tuning = (ROOT / "GreyWardenPolicePurity/GwpTuning.cs").read_text(encoding="utf-8-sig")
            constants = "\n".join(re.findall(r"public const (?:double|float) VeteranDispatch\w+ = [^;]+;", tuning))
            self.assertEqual(4, len(constants.splitlines()))
            festival_constants = "\n".join(re.findall(r"public const float Festival(?:DailyChance|IntervalDays) = [^;]+;", tuning))
            behavior = (ROOT / "GreyWardenPolicePurity/GwpFireswornBehavior.cs").read_text(encoding="utf-8-sig")
            festival_method = re.search(r"private static bool IsFestivalDay\([^;]+;", behavior).group(0)
            (path / "Program.cs").write_text(HARNESS.replace("__METHODS__", methods).replace(
                "__TRAINING_TUNING__", constants).replace("__FESTIVAL_TUNING__", festival_constants).replace(
                "__FESTIVAL_METHOD__", festival_method), encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
