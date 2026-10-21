"""Run actual surplus promotion and bounded cargo loading with controlled engine storage/models."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from test_training_transport import ROOT, extract

HARNESS = r'''
using System;using System.Linq;using System.Collections;using System.Collections.Generic;
class CharacterObject{public string StringId;public int Tier;public bool IsHero;public CharacterObject[] UpgradeTargets=Array.Empty<CharacterObject>();}
struct TroopRosterElement{public CharacterObject Character;public int Number,WoundedNumber;}
class TroopRoster{public List<TroopRosterElement> Rows=new List<TroopRosterElement>();public int TotalManCount=>Rows.Sum(e=>e.Number);public IEnumerable<TroopRosterElement> GetTroopRoster()=>Rows.ToList();public void AddToCounts(CharacterObject c,int n,bool ignored=false,int wounded=0){int i=Rows.FindIndex(e=>e.Character==c);var e=i<0?new TroopRosterElement{Character=c}:Rows[i];e.Number+=n;e.WoundedNumber+=wounded;if(e.Number<0||e.WoundedNumber<0||e.WoundedNumber>e.Number)throw new Exception("invalid roster");if(i<0)Rows.Add(e);else Rows[i]=e;}}
class ItemObject{public string StringId;public float Weight;}
struct EquipmentElement{public ItemObject Item;public int Quality;}
class ItemRosterElement{public EquipmentElement EquipmentElement;public int Amount;}
class ItemRoster:IEnumerable<ItemRosterElement>{public List<ItemRosterElement> Rows=new List<ItemRosterElement>();public int Total=>Rows.Sum(e=>e.Amount);public void AddToCounts(EquipmentElement e,int n){var r=Rows.FirstOrDefault(x=>x.EquipmentElement.Equals(e));if(r==null){r=new ItemRosterElement{EquipmentElement=e};Rows.Add(r);}r.Amount+=n;if(r.Amount<0)throw new Exception("negative cargo");if(r.Amount==0)Rows.Remove(r);}public IEnumerator<ItemRosterElement> GetEnumerator()=>Rows.ToList().GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();}
class MobileParty{public bool IsActive=true,IsCurrentlyAtSea;public object MapEvent;public int Men=5;public float InventoryCapacity=>10+Men*20;public ItemRoster ItemRoster=new ItemRoster();public TroopRoster MemberRoster=new TroopRoster();public float TotalWeightCarried=>ItemRoster.Sum(e=>e.Amount*Campaign.Current.Models.InventoryCapacityModel.GetItemEffectiveWeight(e.EquipmentElement,this,IsCurrentlyAtSea,out _));}
class MilitiaPartyComponent{public MobileParty MobileParty=new MobileParty();}class Settlement{public MilitiaPartyComponent MilitiaPartyComponent=new MilitiaPartyComponent();}
class InventoryModel{public int Calls;public float GetItemEffectiveWeight(EquipmentElement e,MobileParty p,bool sea,out object description){Calls++;description=null;return e.Item.Weight*(sea?3:1)+(e.Quality>0?1:0);}}
class TrainingModel{public int GetXpReward(CharacterObject c)=>10;}
class Models{public InventoryModel InventoryCapacityModel=new InventoryModel();public TrainingModel PartyTrainingModel=new TrainingModel();}class Campaign{public static Campaign Current=new Campaign();public Models Models=new Models();}
static class MBRandom{public static int RandomInt(int n)=>0;}static class GwpTuning{public static class Firesworn{public const int SurplusGuardCapMultiplier=2;}}
class Regression{
 class FaithVillage{public string VillageId="home";public int Cap=100;public bool Strong=true;}
 readonly Dictionary<string,object> _plans=new Dictionary<string,object>();readonly Dictionary<string,ItemRoster> _goods=new Dictionary<string,ItemRoster>(),_armories=new Dictionary<string,ItemRoster>();readonly TroopRoster guard=new TroopRoster();
 static int GuardCap(FaithVillage r)=>r.Cap;int Sworn(FaithVillage r)=>guard.TotalManCount;TroopRoster GuardOf(string id)=>guard;static bool IsGuardTroop(CharacterObject c)=>!c.IsHero&&c.StringId.StartsWith("gw");static bool IsPriestStrongest(FaithVillage r,Settlement s)=>r.Strong;
 static ItemRoster ItemsOf(Dictionary<string,ItemRoster> d,string id){if(!d.ContainsKey(id))d[id]=new ItemRoster();return d[id];}static float Strength(EquipmentElement e)=>e.Quality;
 __METHODS__
 static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 static (Regression r,FaithVillage row,Settlement village,CharacterObject low,CharacterObject high) Start(int n,int wounded=0){var r=new Regression();var row=new FaithVillage();var v=new Settlement();var hi=new CharacterObject{StringId="gwhigh",Tier=3};var lo=new CharacterObject{StringId="gwlow",Tier=2,UpgradeTargets=new[]{hi}};r.guard.AddToCounts(lo,n);v.MilitiaPartyComponent.MobileParty.MemberRoster.AddToCounts(lo,n,false,wounded);return(r,row,v,lo,hi);}
 public static int Main(){
  var (r,row,v,lo,hi)=Start(220);int xp=0;var promoted=new Dictionary<CharacterObject,int>();r._plans["home"]=new object();
  Check(r.BurnSurplusGuards(row,v,220,promoted,ref xp)==20,"only twenty surplus people selected above twice current cap");
  Check(promoted[hi]==10&&r.guard.TotalManCount==200&&v.MilitiaPartyComponent.MobileParty.MemberRoster.TotalManCount==200,"two low become one actual next-tier soldier, shadow and entity agree before admission");
  Check(xp==100&&!r._plans.ContainsKey("home"),"only dead half grants XP; gear plan invalidated");
  foreach(int n in new[]{199,200,201}){(r,row,v,lo,hi)=Start(n);xp=0;promoted.Clear();Check(r.BurnSurplusGuards(row,v,999,promoted,ref xp)==0,"below threshold or odd single not burned");}
  (r,row,v,lo,hi)=Start(220,215);xp=0;promoted.Clear();r.BurnSurplusGuards(row,v,220,promoted,ref xp);Check(v.MilitiaPartyComponent.MobileParty.MemberRoster.Rows[0].WoundedNumber==200,"healthy first, only required wounded removed");
  (r,row,v,lo,hi)=Start(220);row.Strong=false;xp=0;promoted.Clear();Check(r.BurnSurplusGuards(row,v,220,promoted,ref xp)==0,"existing priest authority condition retained");
  (r,row,v,lo,hi)=Start(220);lo.UpgradeTargets=Array.Empty<CharacterObject>();xp=0;Check(r.BurnSurplusGuards(row,v,220,promoted,ref xp)==0,"finished elite troops are not fuel");
  (r,row,v,lo,hi)=Start(220);xp=0;promoted.Clear();Check(r.BurnSurplusGuards(row,v,7,promoted,ref xp)==6,"shared ritual budget and pairs bound processing");
  var item=new EquipmentElement{Item=new ItemObject{StringId="armour",Weight=5},Quality=1};var request=new ItemRoster();request.AddToCounts(item,100);r=new Regression();ItemsOf(r._goods,"home").AddToCounts(item,10);ItemsOf(r._armories,"home").AddToCounts(item,90);var cart=new MobileParty{Men=5};
  Check(r.LoadBalanceCargo("home",cart,request)==18,"effective quality weight six limits 110 capacity to eighteen pieces");
  Check(cart.TotalWeightCarried==108&&ItemsOf(r._goods,"home").Total==0&&ItemsOf(r._armories,"home").Total==82,"warehouse then armory transfer conserves actual copies and leaves rest home");
  var sea=new MobileParty{Men=5,IsCurrentlyAtSea=true};Check(r.LoadBalanceCargo("home",sea,request)==6&&sea.TotalWeightCarried==96,"sea flag uses actual model, not raw weight");
  Check(ItemsOf(r._armories,"home").Total+cart.ItemRoster.Total+sea.ItemRoster.Total==100,"multiple shipments do not duplicate stock");
  Check(Campaign.Current.Models.InventoryCapacityModel.Calls>0,"production loader calls active inventory model");
  Console.WriteLine("FAITH_REFINEMENT_ASSERTIONS="+checks+"; PASS");return 0;
 }
}
'''

class FaithRefinementTests(unittest.TestCase):
    def test_surplus_and_weighted_shipments(self):
        methods = extract("GwpSurplusGuardRitual.cs", ("BurnSurplusGuards",))
        methods += extract("GwpArmoryBalance.cs", ("LoadBalanceCargo",))
        major = subprocess.check_output(["dotnet", "--version"], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix="gwp-refinement-") as tmp:
            p = Path(tmp)
            (p/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>')
            (p/'Program.cs').write_text(HARNESS.replace('__METHODS__', methods), encoding='utf-8')
            result = subprocess.run(['dotnet','run','--project',tmp,'-c','Release','-v','quiet'], capture_output=True,text=True,timeout=60)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            print(result.stdout.strip())

    def test_troop_skill_floors_and_riding_preservation(self):
        expected={11:[60,40,60,60,40,40,40],16:[100,90,100,100,70,70,80],21:[110,110,150,130,100,100,130],31:[220,220,260,220,130,150,170]}
        keys=['OneHanded','TwoHanded','Polearm','Bow','Crossbow','Throwing','Athletics']
        nodes=ET.parse(ROOT/'GreyWardenPolicePurity/_Module/ModuleData/spnpccharacters.xml').getroot()
        ids={'gwzealot','gwnewrecruit','gwrecruit','gwheavyinfantry','gwarcher','gwknight'}
        selected=[n for n in nodes if n.get('id') in ids]
        self.assertEqual(6,len(selected))
        for n in selected:
            stats={s.get('id'):int(s.get('value')) for s in n.findall('skills/skill')}
            for key,floor in zip(keys,expected[int(n.get('level'))]):
                self.assertGreaterEqual(stats[key],floor,(n.get('id'),key))
            if n.get('id')=='gwknight':self.assertEqual(200,stats['Riding'])

if __name__=='__main__':unittest.main()
