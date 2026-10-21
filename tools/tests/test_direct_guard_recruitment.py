"""Exercise production direct recruitment and transfer with bounded engine stubs."""

import re
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_training_transport import ROOT, extract


STUBS = r'''
using System; using System.Collections.Generic; using System.Linq;
class CharacterObject {public string StringId; public int Tier; public bool IsHero, IsWarden=true;}
struct TroopRosterElement {
 public CharacterObject Character; public int Number,WoundedNumber,Xp;
 public TroopRosterElement(CharacterObject t){Character=t;Number=WoundedNumber=Xp=0;}
}
class TroopRoster {
 readonly List<TroopRosterElement> rows=new List<TroopRosterElement>();
 public int TotalManCount=>rows.Sum(e=>e.Number);
 public List<TroopRosterElement> GetTroopRoster()=>rows.ToList();
 public int FindIndexOfTroop(CharacterObject t)=>rows.FindIndex(e=>e.Character==t);
 public TroopRosterElement GetElementCopyAtIndex(int i)=>rows[i];
 public int GetTroopCount(CharacterObject t)=>rows.Where(e=>e.Character==t).Sum(e=>e.Number);
 public void AddToCounts(CharacterObject t,int n,bool insertAtFront=false,int woundedCount=0,int xp=0){
  int i=FindIndexOfTroop(t);var e=i<0?new TroopRosterElement(t):rows[i];
  e.Number+=n;e.WoundedNumber+=woundedCount;e.Xp+=xp;
  if(e.Number<0||e.WoundedNumber<0||e.WoundedNumber>e.Number||e.Xp<0)throw new Exception("invalid roster");
  if(i>=0)rows.RemoveAt(i);if(e.Number>0)rows.Add(e);
 }
}
class Hero {public CharacterObject[] VolunteerTypes=new CharacterObject[6];}
class MobileParty {
 public object Party=>this;
 public bool IsLordParty=true,IsWarden=true,IsActive=true;public object MapEvent;
 public string StringId="lord";public int Capacity=100;public Hero LeaderHero=new Hero();
 public TroopRoster MemberRoster=new TroopRoster();
}
class MilitiaComponent {public MobileParty MobileParty;}
class Settlement {public string StringId="village";public bool Preached=true,Quiet=true;public MilitiaComponent MilitiaPartyComponent;}
static class GwpWardenRecruitment {public static bool IsWardenParty(MobileParty p)=>p.IsWarden;}
static class GwpSettlementNotices {public static string RecruitText(int n)=>n.ToString();public static void Post(Settlement s,Hero h,string t){}}
class Regression {
 class FaithVillage {public string VillageId="village";public Hero Priest=new Hero();}
 static Regression _instance;readonly Dictionary<string,FaithVillage> _villages=new Dictionary<string,FaithVillage>();
 readonly TroopRoster guard=new TroopRoster();public int Kits;readonly Dictionary<string,int> carried=new Dictionary<string,int>();
 TroopRoster GuardOf(string id)=>guard;Hero FindLivingPriest(FaithVillage r)=>r.Priest;
 int SpareGuard(FaithVillage row)=>Math.Max(0,guard.TotalManCount-Math.Max(5,(int)Math.Ceiling(guard.TotalManCount*0.5)));
 static bool IsPreachedVillage(Settlement s)=>s.Preached;static bool IsVillageQuiet(Settlement s)=>s.Quiet;
 static int FreeSlots(MobileParty p)=>Math.Max(0,p.Capacity-p.MemberRoster.TotalManCount);
 static bool IsGuardTroop(CharacterObject t)=>t!=null&&!t.IsHero&&t.IsWarden;
 string HolderOf(object party)=>"lord";
 int MoveBestSets(string from,string to,CharacterObject t,int n){Kits+=n;if(!carried.ContainsKey(t.StringId))carried[t.StringId]=0;carried[t.StringId]+=n;return n;}
 void PushGuard(FaithVillage r,Settlement s){foreach(var e in guard.GetTroopRoster())if(s.MilitiaPartyComponent.MobileParty.MemberRoster.GetTroopCount(e.Character)!=e.Number)throw new Exception("shadow differs from physical guard");}
 void ReconcilePriestSlots(FaithVillage r){}void UpdatePriestPower(FaithVillage r){}void RecordVisit(MobileParty p,Settlement s,int recruits){}
__METHODS__
 static int checks;static void Check(bool c,string m){checks++;if(!c)throw new Exception(m);}
 static CharacterObject Low=new CharacterObject{StringId="zealot",Tier=2},New=new CharacterObject{StringId="recruit",Tier=3},
  Mid=new CharacterObject{StringId="light",Tier=4},High=new CharacterObject{StringId="heavy",Tier=6};
 static (Regression,Settlement,MobileParty) Start(int low=8,int recruit=8,int mid=8,int high=16,int slots=2){
  var r=new Regression();_instance=r;var s=new Settlement{MilitiaPartyComponent=new MilitiaComponent{MobileParty=new MobileParty()}};
  var p=new MobileParty();var row=new FaithVillage();r._villages[s.StringId]=row;
  for(int i=0;i<slots;i++)row.Priest.VolunteerTypes[i]=High;
  foreach(var x in new[]{(Low,low),(New,recruit),(Mid,mid),(High,high)}){
   r.guard.AddToCounts(x.Item1,x.Item2);s.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(x.Item1,x.Item2);
  }
  return (r,s,p);
 }
 public static int Main(){
  var (r,s,p)=Start();p.Capacity=10;p.MemberRoster.AddToCounts(High,1);
  int expected=DirectGuardRecruitsFor(s,p);r.EnlistGuardRecruits(p,s);
  Check(expected==9&&p.MemberRoster.TotalManCount==10,"counts agree with recruitment desire and capacity");
  Check(p.MemberRoster.GetTroopCount(Low)==8&&p.MemberRoster.GetTroopCount(New)==1,"lowest tiers first");
  Check(r.guard.GetTroopCount(Mid)==8&&r.guard.GetTroopCount(High)==16,"higher tiers untouched while weak troops remain");
  Check(r.guard.TotalManCount==31&&r.Kits==9,"nine people and nine kits actually leave village");
  (r,s,p)=Start(0,8,6,16);r.EnlistGuardRecruits(p,s);
  Check(p.MemberRoster.GetTroopCount(New)==8&&p.MemberRoster.GetTroopCount(Mid)==5,"recruit without zealots and advance tier only when needed");
  Check(p.MemberRoster.GetTroopCount(High)==0&&r.guard.TotalManCount==17,"half guard plus two priest slots protected");
  Check(r.carried[New.StringId]==8&&r.carried[Mid.StringId]==5,"kits follow actual troop types");
  (r,s,p)=Start(0,0,0,20);r.EnlistGuardRecruits(p,s);
  Check(p.MemberRoster.GetTroopCount(High)==8&&r.guard.TotalManCount==12,"can recruit higher tiers when no lower tier exists");
  (r,s,p)=Start(0,10,0,0,0);p.Capacity=5;
  var physical=s.MilitiaPartyComponent.MobileParty.MemberRoster;physical.AddToCounts(New,0,false,7,107);
  r.EnlistGuardRecruits(p,s);
  var moved=p.MemberRoster.GetElementCopyAtIndex(p.MemberRoster.FindIndexOfTroop(New));
  var left=physical.GetElementCopyAtIndex(physical.FindIndexOfTroop(New));
  Check(moved.Number==5&&moved.WoundedNumber==2&&left.Number==5&&left.WoundedNumber==5,"wounds conserved through production transfer");
  Check(moved.Xp==50&&left.Xp==57,"shared experience and remainder conserved");
  (r,s,p)=Start(0,0,0,10,5);r.EnlistGuardRecruits(p,s);
  Check(p.MemberRoster.TotalManCount==0&&r.Kits==0,"priest slots consume the whole spare budget");
  (r,s,p)=Start(0,4,0,0,0);r.EnlistGuardRecruits(p,s);
  Check(p.MemberRoster.TotalManCount==0,"five-person village minimum");
  foreach(var reason in new[]{"outsider","notlord","full","raided","unpreached","battle","inactive","nomilitia"}){
   (r,s,p)=Start();switch(reason){
    case "outsider":p.IsWarden=false;break;case "notlord":p.IsLordParty=false;break;case "full":p.Capacity=0;break;
    case "raided":s.Quiet=false;break;case "unpreached":s.Preached=false;break;
    case "battle":s.MilitiaPartyComponent.MobileParty.MapEvent=new object();break;
    case "inactive":s.MilitiaPartyComponent.MobileParty.IsActive=false;break;case "nomilitia":s.MilitiaPartyComponent=null;break;
   }
   r.EnlistGuardRecruits(p,s);Check(p.MemberRoster.TotalManCount==0&&r.guard.TotalManCount==40,"gate: "+reason);
  }
  Console.WriteLine("DIRECT_GUARD_RECRUITMENT_ASSERTIONS="+checks+"; PASS");return 0;
 }
}
'''


class DirectGuardRecruitmentTests(unittest.TestCase):
    def test_order_limits_rosters_equipment_and_permissions(self):
        behavior = (ROOT / "GreyWardenPolicePurity/GwpFireswornBehavior.cs").read_text(encoding="utf-8-sig")
        eligibility = re.search(r"internal static int DirectGuardRecruitsFor[\s\S]*?;", behavior).group(0)
        methods = eligibility + extract("GwpFireswornBehavior.cs", ("DirectGuardRecruitsAvailable",))
        methods += extract("GwpFireswornDungeon.cs", ("EnlistGuardRecruits",))
        transfer = (ROOT / "GreyWardenPolicePurity/GwpTroopTransfer.cs").read_text(encoding="utf-8-sig")
        transfer = transfer[transfer.index("    internal static class GwpTroopTransfer"):].rstrip()[:-1]
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-guard-recruitment-") as tmp:
            folder = Path(tmp)
            (folder / "test.csproj").write_text(
                f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>disable</Nullable></PropertyGroup></Project>', encoding="utf-8")
            (folder / "Program.cs").write_text(STUBS.replace("__METHODS__", methods) + transfer, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(folder), "-c", "Release"], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn("DIRECT_GUARD_RECRUITMENT_ASSERTIONS=20; PASS", result.stdout)


if __name__ == "__main__":
    unittest.main()
