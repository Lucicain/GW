"""Execute production dispatch: dungeon size must neither trigger nor choose a shipment."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from test_training_transport import extract

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
class Point{public float Distance(Point other)=>0;}
class Village{public float Hearth=100;}
class Settlement{public string StringId;public Village Village=new();public Point GetPosition2D=new();}
class Roster{public int TotalManCount;}
class ItemRoster{public int Total;public int TotalValue=>Total;}
class MobileParty{public string StringId="courier";public Roster MemberRoster=new(),PrisonRoster=new();public ItemRoster ItemRoster=new();public float InventoryCapacity=100,TotalWeightCarried;}
class FaithVillage{public string VillageId;public int Men=20,Dungeon;public double LastBalance;}
class FaithParty{}
enum FaithPartyKind{Balance}
static class GwpTuning{public static class Firesworn{public const int BalanceEscortSize=5,BalanceMinimumMen=5,BalanceMinimumSets=5,EscortMaximumSize=25;public const float BalanceMaxDistance=300;}}
class Regression{
 readonly Dictionary<string,FaithVillage> _villages=new();readonly Dictionary<string,FaithParty> _faithParties=new();
 readonly List<Settlement> targets=new();readonly Dictionary<string,int> cargo=new();
 MobileParty sent;Settlement destination;int built,checks;
 void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static int Sworn(FaithVillage row)=>row.Men;static int CountItems(ItemRoster items)=>items.Total;
 int FaithEscortAvailable(FaithVillage row,Settlement home)=>row.Men-5;
 static bool IsFestivalDay(FaithVillage row,double previous,double now)=>true;
 IEnumerable<Settlement> NearbyTargets(Settlement home,float range,FaithPartyKind kind)=>targets;
 static bool IsPreachedVillage(Settlement village)=>true;
 ItemRoster ArmoryCargoFor(FaithVillage source,FaithVillage target)=>new(){Total=cargo.GetValueOrDefault(target.VillageId)};
 MobileParty CreateFaithParty(FaithVillage row,Settlement home,FaithPartyKind kind){built++;sent=new();_faithParties[sent.StringId]=new();return sent;}
 int ManEscort(MobileParty party,FaithVillage row,Settlement home,Settlement target,int n,out float danger){danger=0;party.MemberRoster.TotalManCount=n;return n;}
 static float ArmoryCargoWeight(ItemRoster plan,MobileParty party)=>plan.Total;
 void DrawMen(MobileParty party,FaithVillage row,Settlement home,int n)=>party.MemberRoster.TotalManCount=n;
 static int TakeBestSets(string holder,MobileParty party)=>0;
 static int LoadBalanceCargo(string home,MobileParty party,ItemRoster plan){party.ItemRoster.Total=plan.Total;return plan.Total;}
 void Dispatch(FaithParty record,MobileParty party,Settlement target)=>destination=target;
 __PRODUCTION__
 static void Main(){var b=new Regression();var home=new Settlement{StringId="home"};var rich=new Settlement{StringId="rich"};var poor=new Settlement{StringId="poor"};
  var origin=new FaithVillage{VillageId="home",Dungeon=10000};b._villages["home"]=origin;b._villages["rich"]=new(){VillageId="rich",Dungeon=0};b._villages["poor"]=new(){VillageId="poor",Dungeon=50000};b.targets.Add(rich);b.targets.Add(poor);
  b.TryDispatchBalance(origin,home,100);b.Check(b.built==0,"prisoner imbalance alone never creates a transport");b.Check(origin.Dungeon==10000,"source prisoners stay for local burning");
  b.cargo["poor"]=5;b.cargo["rich"]=6;b.TryDispatchBalance(origin,home,101);b.Check(b.built==1&&b.destination==rich,"target choice uses real equipment need and ignores dungeon imbalance");
  b.Check(b.sent.PrisonRoster.TotalManCount==0&&origin.Dungeon==10000,"new shipment never loads dungeon prisoners");b.Check(b.sent.ItemRoster.Total==6,"equipment shipment keeps backpack cargo path");
  b.cargo.Clear();b._villages["rich"].Men=8;b.TryDispatchBalance(origin,home,102);b.Check(b.built==2&&b.destination==rich&&b.sent.MemberRoster.TotalManCount>=5,"real troop imbalance still dispatches guards for one way delivery");
  Console.WriteLine("BALANCE_DISPATCH_ASSERTIONS="+b.checks+"; PASS; production demand/dispatch, controlled world/capacity/escort boundaries");
 }
}
'''


class BalanceDispatchTests(unittest.TestCase):
    def test_prisoners_never_drive_dispatch(self):
        production = extract('GwpFireswornParties.cs', ('BalanceNeed', 'TryDispatchBalance'))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-balance-dispatch-') as directory:
            path = Path(directory)
            (path / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(HARNESS.replace('__PRODUCTION__', production), encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', directory, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('BALANCE_DISPATCH_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
