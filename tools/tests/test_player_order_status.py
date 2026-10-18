"""Run production order snapshots and text formatting for blocked and resumed orders."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from test_duty_banner import method
from test_battle_peace import extract

MODULE = Path(__file__).resolve().parents[2] / 'GreyWardenPolicePurity'


class PlayerOrderStatusTests(unittest.TestCase):
    def test_blocked_order_snapshot_and_displayed_ready_count(self):
        order = (MODULE / 'GreyWardenTroopRequestBehavior.cs').read_text(encoding='utf-8-sig')
        vm = (MODULE / 'GwpCaseArchiveItemVM.cs').read_text(encoding='utf-8-sig')
        types = order[order.index('        internal enum PlayerTroopOrderStage'):order.index('        private sealed class TroopKind')]
        start = vm.index('        internal GwpCaseArchiveItemVM(\n            GreyWardenTroopRequestBehavior.PlayerTroopOrderSnapshot')
        constructor = extract(vm[start:], 'GwpCaseArchiveItemVM')
        formatter = constructor + method(vm, 'DescribePlayerTroopOrderStage')
        keys = set(re.findall(r'GwpTextCatalog\.(\w+)', formatter))
        code = r'''
using System;using System.Linq;using System.Collections.Generic;
class Hero{public string Name="trainer";}class MobileParty{public static List<MobileParty> All=new();public string StringId="trainer",Name="trainer";public Hero? LeaderHero=new();}
class CharacterObject{public string Name="recruit";public static CharacterObject? Find(string id)=>new();}
struct CampaignTime{public float Value;public static CampaignTime Hours(float h)=>new(){Value=h};}
enum WardenDuty{PlayerRequest,PlayerEscort,PlayerCase,Duel}
static class GwpWardenDuties{public static WardenDuty? Blocker;public static WardenDuty? BlockingDuty(MobileParty p,WardenDuty wanted)=>Blocker;}
class GreyWardenTroopRequestBehavior{
TYPES
internal static GreyWardenTroopRequestBehavior? _instance;
internal int _orderStage=1,_orderedCount=10,_orderPrice=800;internal string _orderedTroopId="recruit";internal double _filedHour=123;
MobileParty? ResolveTrainerParty()=>MobileParty.All.FirstOrDefault();object ResolveOrderPool(MobileParty? p)=>new();static int CountHealthy(object pool,CharacterObject? troop)=>31;
SNAPSHOT
}
static class GwpTextCatalog{KEYS}
static class GwpText{public static string Get(string k,params object[] p)=>k+string.Concat(Enumerable.Range(0,p.Length/2).Select(i=>"|"+p[2*i]+"="+p[2*i+1]));}
class GwpCaseArchiveItemVM{public string Header="",TimeText="",AssignmentText="",DetailsText="";static string FormatCampaignDate(CampaignTime t)=>t.Value.ToString();FORMATTER}
class Program{static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}static void Main(){
Check(GreyWardenTroopRequestBehavior.GetTaskSnapshots().Count==0,"no order has no snapshot");
GreyWardenTroopRequestBehavior._instance=new();MobileParty.All.Add(new());
foreach(var duty in new[]{WardenDuty.PlayerEscort,WardenDuty.PlayerCase,WardenDuty.Duel}){
 GwpWardenDuties.Blocker=duty;var s=GreyWardenTroopRequestBehavior.GetTaskSnapshots().Single();var text=new GwpCaseArchiveItemVM(s);
 Check(s.PausedBy==duty&&s.ReadyCount==31&&s.Count==10,"snapshot reports shared duty blockage without modifying business counts");
 var key=duty==WardenDuty.PlayerEscort?GwpTextCatalog.gwp_player_order_wait_support:duty==WardenDuty.PlayerCase?GwpTextCatalog.gwp_player_order_wait_case:GwpTextCatalog.gwp_player_order_wait_duel;
 Check(text.AssignmentText.Contains(key)&&!text.AssignmentText.Contains(GwpTextCatalog.gwp_player_troop_stage_training),"blocked order explains waiting instead of concurrent training");
 Check(text.DetailsText.Contains("VAR_1=10|VAR_2=10")&&!text.DetailsText.Contains("VAR_1=31"),"display caps prepared allocation to order size");
}
GwpWardenDuties.Blocker=null;var resumed=new GwpCaseArchiveItemVM(GreyWardenTroopRequestBehavior.GetTaskSnapshots().Single());
Check(resumed.AssignmentText.Contains(GwpTextCatalog.gwp_player_troop_stage_training),"order resumes training display when owning duty permits it");
GreyWardenTroopRequestBehavior._instance._orderStage=2;var delivery=new GwpCaseArchiveItemVM(GreyWardenTroopRequestBehavior.GetTaskSnapshots().Single());
Check(delivery.AssignmentText.Contains(GwpTextCatalog.gwp_player_troop_stage_delivery),"delivery retains its own stage");
Console.WriteLine("PLAYER_ORDER_STATUS_ASSERTIONS="+checks);
}}
'''
        code = code.replace('TYPES', types).replace('SNAPSHOT', method(order, 'GetTaskSnapshots'))
        code = code.replace('FORMATTER', formatter).replace('KEYS', '\n'.join(f'public const string {k}="{k}";' for k in keys))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-order-status-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('PLAYER_ORDER_STATUS_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
