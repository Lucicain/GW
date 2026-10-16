"""Run production village handover, militia creation, power and convoy methods."""
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_lord_combat import members, source
from test_direct_guard_recruitment import STUBS as RECRUIT_STUBS


HARNESS = r'''
using System; using System.Collections.Generic; using System.Linq;
class CharacterObject {
 public string StringId;public int Tier;public bool IsHero,IsWarden=true;
 public static Dictionary<string,CharacterObject> All=new Dictionary<string,CharacterObject>();
 public static CharacterObject Find(string id)=>All[id];
}
__ROSTER__
class Hero {public float Power;public bool IsAlive=true;public void AddPower(float n){Power+=n;}}
class MobileParty {
 public bool IsActive=true;public object MapEvent;public string StringId="party";public Hero LeaderHero;
 public TroopRoster MemberRoster=new TroopRoster(),PrisonRoster=new TroopRoster();
 public ItemRoster ItemRoster=new ItemRoster();public PartyBase Party=new PartyBase();
 public Settlement CurrentSettlement,Here;
}
class PartyBase {public int PrisonerSizeLimit=10;}
class MilitiaPartyComponent {
 public MobileParty MobileParty;public static int Created;
 public static MobileParty CreateMilitiaParty(string id,Settlement village){
  Created++;var p=new MobileParty();p.MemberRoster.AddToCounts(new CharacterObject{StringId="template",IsWarden=false},2);
  village.MilitiaPartyComponent=new MilitiaPartyComponent{MobileParty=p};return p;
 }
}
class Settlement {
 public string StringId;public bool IsVillage=true;public MilitiaPartyComponent MilitiaPartyComponent;
 public List<Hero> Notables=new List<Hero>();public static Dictionary<string,Settlement> All=new Dictionary<string,Settlement>();
 public static Settlement Find(string id)=>All[id];
}
enum AiBehavior {GoToSettlement,Other}
class AIBehaviorData {public AiBehavior AiBehavior;public object Party;}
class PartyThinkParams {
 public List<(AIBehaviorData,float)> AIBehaviorScores=new List<(AIBehaviorData,float)>();
 public void SetBehaviorScore(in AIBehaviorData data,float score){var key=data;int i=AIBehaviorScores.FindIndex(x=>x.Item1==key);AIBehaviorScores[i]=(data,score);}
}
static class MobilePartyHelper {public static Settlement GetCurrentSettlementOfMobilePartyForAICalculation(MobileParty p)=>p.Here;}
static class MBMath {public static float Map(float v,float a,float b,float c,float d)=>c+(v-a)/(b-a)*(d-c);}
static class GwpFireswornBehavior {public static bool IsPreachedVillage(Settlement s)=>true;}
static class GwpWardenVisitDesire {
 static readonly Dictionary<string,string> LastBest=new Dictionary<string,string>();
 static List<MobileParty> LocalMembers(MobileParty p)=>new List<MobileParty>{p};
 static float HandoverTerm(MobileParty p)=>1;
__VISIT__
}
class IDataStore {
 public bool IsSaving;public bool IsLoading=>!IsSaving;public Dictionary<string,object> Data=new Dictionary<string,object>();
 public void SyncData<T>(string k,ref T v){if(IsSaving)Data[k]=v;else v=(T)Data[k];}
}
struct EquipmentElement {public int ItemValue;}
struct ItemRosterElement {public EquipmentElement EquipmentElement;public int Amount;}
class ItemRoster {public int Count=>0;public ItemRosterElement GetElementCopyAtIndex(int i)=>default;public void AddToCounts(EquipmentElement e,int n){}}
static class GwpTuning {public static class Firesworn {public const float PriestMaxPowerShare=0.5f,FaithVillageVisitPriority=3;}}
static class GwpAiDiagnostics {public static void WriteFaith(string a,string b){}}
static class GwpSettlementNotices {public static void Post(Settlement s,Hero h,string text){}}
static class GwpText {public static string Get(string s,params object[] args)=>s;}
static class GwpTextCatalog {public const string gwp_notice_prisoners="prisoners",gwp_notice_donated="donated";}
__TRANSFER__
class Regression {
 class FaithVillage {public string VillageId;public int Households=100;public float Share=1;public Hero Priest;}
 readonly Dictionary<string,TroopRoster> _guards=new Dictionary<string,TroopRoster>(),_dungeons=new Dictionary<string,TroopRoster>();
 readonly Dictionary<string,int> _plans=new Dictionary<string,int>();
 TroopRoster GuardOf(string id)=>RosterOf(_guards,id);
 static bool IsGuardTroop(CharacterObject t)=>t!=null&&!t.IsHero&&t.IsWarden;
 static int HouseholdCap(Settlement s)=>1000;
 static Hero FindLivingPriest(FaithVillage r)=>r.Priest;
 static IEnumerable<Hero> OtherNotables(Settlement s,Hero p)=>s.Notables.Where(h=>h!=p&&h.IsAlive);
 static float SwornShare(FaithVillage r,Settlement s)=>r.Share;
 static bool KeptOnHandover(EquipmentElement e)=>false;
 static bool Store(string id,EquipmentElement e,int n)=>false;
 static int DonateArmorySurplus(string a,string b)=>0;
 static void RecordVisit(MobileParty p,Settlement s,int prisoners,int donated){}
 static void ReleaseHeroPrisoners(MobileParty p){foreach(var e in p.PrisonRoster.GetTroopRoster().Where(e=>e.Character.IsHero).ToList())p.PrisonRoster.AddToCounts(e.Character,-e.Number,false,-e.WoundedNumber,-e.Xp);}
__METHODS__
 static int checks;static void Check(bool c,string why){checks++;if(!c)throw new Exception(why);}
 static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<0.001f,why+": "+a+" != "+b);
 static CharacterObject Troop(string id,bool warden=true,bool hero=false){var t=new CharacterObject{StringId=id,IsWarden=warden,IsHero=hero};CharacterObject.All[id]=t;return t;}
 static (Regression,Settlement,FaithVillage) Start(){var r=new Regression();var s=new Settlement{StringId="v"};Settlement.All[s.StringId]=s;return(r,s,new FaithVillage{VillageId=s.StringId});}
 public static void Main(){
  var weak=Troop("weak");var other=Troop("other",false);var hero=Troop("hero",false,true);
  var(r,s,row)=Start();r.GuardOf(s.StringId).AddToCounts(weak,1);r.PushGuard(row,s);
  Check(s.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount==1,"first single guard physically exists, template soldiers removed");
  r.ObserveGuard(row);Check(r.GuardOf(s.StringId).TotalManCount==1,"hourly observation does not erase first guard");
  for(int i=0;i<4;i++){r.JoinGuard(row,s,weak,1);r.ObserveGuard(row);}
  Check(r.GuardOf(s.StringId).TotalManCount==5,"daily singleton growth accumulates");
  var physical=s.MilitiaPartyComponent.MobileParty.MemberRoster;physical.AddToCounts(other,3);int created=MilitiaPartyComponent.Created;r.PushGuard(row,s);
  Check(physical.GetTroopCount(other)==3&&created==MilitiaPartyComponent.Created,"existing real militia is never cleared or rebuilt");
  s.MilitiaPartyComponent.MobileParty.IsActive=false;r.ObserveGuard(row);
  Check(r.GuardOf(s.StringId).TotalManCount==0&&MilitiaPartyComponent.Created==created,"destroyed guards stay dead");
  (r,s,row)=Start();var arrivals=new TroopRoster();arrivals.AddToCounts(weak,1,false,1,137);
  r.JoinGuard(row,s,weak,1,arrivals);physical=s.MilitiaPartyComponent.MobileParty.MemberRoster;var e=physical.GetElementCopyAtIndex(0);
  Check(e.Number==1&&e.WoundedNumber==1&&e.Xp==137&&arrivals.TotalManCount==0,"first arriving wounded soldier keeps XP and wounds");
  r.ObserveGuard(row);e=physical.GetElementCopyAtIndex(0);Check(e.WoundedNumber==1&&e.Xp==137,"observing shadow ledger preserves physical XP and wounds");
  physical.Clear();r.ObserveGuard(row);Check(r.GuardOf(s.StringId).TotalManCount==0,"real casualties are not respawned");
  row.Priest=new Hero{Power=100};var rival=new Hero{Power=100};s.Notables.AddRange(new[]{row.Priest,rival});
  for(int i=0;i<500;i++){r.UpdatePriestPower(row);Near(row.Priest.Power+rival.Power,200,"power conserved across repeated updates");Check(row.Priest.Power>rival.Power,"priest strictly leads");}
  Near(row.Priest.Power,100.5f,"two notables share only the necessary half-point");Near(rival.Power,99.5f,"rival loses only the amount transferred");
  row.Share=0.2f;r.UpdatePriestPower(row);Near(row.Priest.Power,40,"below cap keeps real faith share");Near(rival.Power,160,"ordinary shares conserve total");
  var second=new Hero{Power=100};s.Notables.Add(second);row.Share=1;r.UpdatePriestPower(row);Near(row.Priest.Power,150,"multiple rivals do not require excess half share");Near(rival.Power/second.Power,1.6f,"rivals retain relative proportions");
  s.Notables.Clear();float before=row.Priest.Power;r.UpdatePriestPower(row);Near(before,row.Priest.Power,"sole priest unchanged");
  Check(PrisonersToBalance(200,50,60,50)==70,"both villages over soft target still rebalance");
  Check(PrisonersToBalance(50,50,0,100)==0,"no export below or at soft target");
  Check(PrisonersToBalance(200,50,0,100)==133,"different village sizes weighted by faith households");
  Check(PrisonersToBalance(100,50,300,50)==0&&PrisonersToBalance(100,50,0,0)==0,"no wrong-way or zero-household delivery");
  var p=new MobileParty();p.PrisonRoster.AddToCounts(other,40,false,31,407);p.PrisonRoster.AddToCounts(hero,1);
  var ai=new AIBehaviorData{AiBehavior=AiBehavior.GoToSettlement,Party=s};var think=new PartyThinkParams();think.AIBehaviorScores.Add((ai,0.384f));
  GwpWardenVisitDesire.Rescore(p,think);Near(think.AIBehaviorScores[0].Item2,2.688f,"full prisoner load gives real delivery need even at full village");
  var dungeon=RosterOf(r._dungeons,s.StringId);dungeon.AddToCounts(other,150);r.HandOver(p,s);
  Check(dungeon.TotalManCount==190&&p.PrisonRoster.GetTroopCount(other)==0&&p.PrisonRoster.GetTroopCount(hero)==1,"full dungeon accepts every ordinary captive; captive lord stays with lord");
  e=dungeon.GetElementCopyAtIndex(dungeon.FindIndexOfTroop(other));Check(e.WoundedNumber==31&&e.Xp==407,"handover preserves prisoner wounds and XP");
  think=new PartyThinkParams();think.AIBehaviorScores.Add((ai,0.384f));GwpWardenVisitDesire.Rescore(p,think);
  Near(think.AIBehaviorScores[0].Item2,0.384f,"delivery clears the visit bonus, captive hero never keeps it alive");
  p=new MobileParty();p.Party.PrisonerSizeLimit=12;p.PrisonRoster.AddToCounts(other,2);int moved=r.LoadBalancePrisoners(row,p,70);
  Check(moved==10&&p.PrisonRoster.TotalManCount==12&&dungeon.TotalManCount==180,"convoy respects actual moving prisoner capacity");
  Check(r.LoadBalancePrisoners(row,p,70)==0,"full moving convoy never overloads");
  var store=new IDataStore{IsSaving=true};SyncTroopRosters(store,"Dungeon",r._dungeons);var loaded=new Dictionary<string,TroopRoster>();store.IsSaving=false;SyncTroopRosters(store,"Dungeon",loaded);
  var saved=dungeon.GetElementCopyAtIndex(0);e=loaded[s.StringId].GetElementCopyAtIndex(0);
  Check(e.Number==saved.Number&&e.WoundedNumber==saved.WoundedNumber&&e.Xp==saved.Xp,"actual dungeon persistence restores counts wounds XP");
  var target=new Settlement{StringId="target"};Settlement.All[target.StringId]=target;p.MemberRoster.AddToCounts(other,1,false,1,73);p.PrisonRoster.AddToCounts(hero,1);
  int delivered=r.LodgePrisonersAndStrangers(p,target,out int joined);var local=RosterOf(r._dungeons,target.StringId);
  Check(delivered==12&&local.TotalManCount==12&&p.PrisonRoster.TotalManCount==0,"village convoy arrival transfers full ordinary load and releases heroes");
  e=target.MilitiaPartyComponent.MobileParty.MemberRoster.GetElementCopyAtIndex(0);
  Check(joined==1&&e.Character==other&&e.WoundedNumber==1&&e.Xp==73,"non-warden rescued soldier survives first arrival without militia");
  Near(r._dungeons[s.StringId].TotalManCount+local.TotalManCount,192,"global ordinary captives conserved across journey");
  Console.WriteLine("FAITH_LOGISTICS_ASSERTIONS="+checks+"; PASS");
 }
}
'''


class FaithLogisticsTests(unittest.TestCase):
    def test_production_village_logistics_and_conservation(self):
        methods = members(source("GwpFireswornBehavior.cs"), "RosterOf", "SyncTroopRosters",
                          "UpdatePriestPower", "PushGuard", "ObserveGuard", "EnsureVillageMilitia", "JoinGuard")
        methods += members(source("GwpFireswornDungeon.cs"), "HandOver")
        methods += members(source("GwpFireswornParties.cs"), "PrisonersToBalance", "LoadBalancePrisoners",
                           "LodgePrisonersAndStrangers")
        roster = RECRUIT_STUBS[RECRUIT_STUBS.index("struct TroopRosterElement"):RECRUIT_STUBS.index("class Hero")]
        roster = roster.replace(" public int TotalManCount", " public static TroopRoster CreateDummyTroopRoster()=>new TroopRoster();public void Clear()=>rows.Clear();\n public int TotalManCount")
        roster = roster.replace(" public List<TroopRosterElement>", " public int TotalWounded=>rows.Sum(e=>e.WoundedNumber);public int TotalRegulars=>rows.Where(e=>!e.Character.IsHero).Sum(e=>e.Number);\n public List<TroopRosterElement>")
        roster = roster.replace(" public int GetTroopCount", " public int GetElementNumber(int i)=>rows[i].Number;public int GetElementWoundedNumber(int i)=>rows[i].WoundedNumber;\n public int GetTroopCount")
        transfer = source("GwpTroopTransfer.cs").split("namespace GreyWardenPolicePurity", 1)[1].strip()[1:-1]
        program = HARNESS.replace("__METHODS__", methods).replace("__ROSTER__", roster).replace("__TRANSFER__", transfer)
        program = program.replace("__VISIT__", members(source("GwpWardenVisitDesire.cs"), "Amplified", "Rescore"))
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-faith-") as directory:
            path = Path(directory)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn>'
                '</PropertyGroup></Project>', encoding="utf-8")
            (path / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("; PASS", result.stdout)
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
