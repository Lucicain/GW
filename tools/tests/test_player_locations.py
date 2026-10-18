"""Execute the shared settlement query and affairs formatter against mixed settlement types."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from test_duty_banner import method

MODULE = Path(__file__).resolve().parents[2] / 'GreyWardenPolicePurity'


class PlayerLocationTests(unittest.TestCase):
    def test_nearer_castle_village_town_and_special_locations(self):
        common = (MODULE / 'GwpCommon.cs').read_text(encoding='utf-8-sig')
        affairs = (MODULE / 'GwpAffairsData.cs').read_text(encoding='utf-8-sig')
        code = r'''
using System;using System.Collections.Generic;
struct Vec2{public float X,Y;public Vec2(float x,float y){X=x;Y=y;}public float Distance(Vec2 b)=>MathF.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y));}
class Settlement{public static List<Settlement> All=new();public string Name="";public bool IsTown,IsCastle,IsVillage;public Vec2 GetPosition2D;}
class MobileParty{public Settlement? CurrentSettlement;public Vec2 GetPosition2D;}
static class GwpCommon{COMMON}
static class GwpTextCatalog{public const string gwp_affairs_unknown="unknown",gwp_affairs_location="location",gwp_affairs_near="near";}
static class GwpText{public static string Get(string k,params object[] p)=>k=="near"?"near "+p[1]:k;}
static class GwpAffairsText{public static string Line(string k,object v)=>k+":"+v;FORMAT}
class Program{static int checks;static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}static void Main(){
var town=new Settlement{Name="town",IsTown=true,GetPosition2D=new(20,0)};
var castle=new Settlement{Name="castle",IsCastle=true,GetPosition2D=new(2,0)};
var village=new Settlement{Name="village",IsVillage=true,GetPosition2D=new(8,0)};
var hideout=new Settlement{Name="hideout",GetPosition2D=new(0,0)};
Settlement.All.AddRange(new[]{town,castle,village,hideout});
Check(!castle.IsTown&&GwpCommon.IsOrdinarySettlement(castle),"castle is a separate native settlement type and must participate");
Check(GwpCommon.FindNearestSettlement(new(0,0))==castle,"nearer castle wins over village and town; hideout excluded");
Check(GwpCommon.FindNearestSettlement(new(7,0))==village,"nearer village wins");
Check(GwpCommon.FindNearestSettlement(new(19,0))==town,"nearer town wins");
Check(!GwpCommon.IsOrdinarySettlement(hideout)&&!GwpCommon.IsOrdinarySettlement(null),"special and null locations excluded");
Check(GwpCommon.NormalizePlayerFacingSettlement(castle,new(20,0))==castle,"party inside a castle uses that castle");
Check(GwpCommon.NormalizePlayerFacingSettlement(hideout,new(20,0))==castle,"special settlement uses its own coordinate");
Check(GwpCommon.NormalizePlayerFacingSettlement(null,new(20,0))==town,"moving party uses its current coordinate");
var party=new MobileParty{GetPosition2D=new(0,0)};
Check(GwpAffairsText.TargetLocation(party)=="location:near castle","affairs uses the shared accurate query");
party.CurrentSettlement=castle;Check(GwpAffairsText.TargetLocation(party,"member")=="member:castle","inside settlement is not described as nearby");
party.CurrentSettlement=hideout;Check(GwpAffairsText.TargetLocation(party)=="location:near castle","special site is not exposed as a normal reference");
party.CurrentSettlement=null;Settlement.All.Clear();Check(GwpAffairsText.TargetLocation(party)=="location:unknown","missing location has no misleading nearby suffix");
Console.WriteLine("PLAYER_LOCATION_ASSERTIONS="+checks);
}}
'''
        code = code.replace('COMMON', '\n'.join(method(common, n) for n in ['IsOrdinarySettlement', 'FindNearestSettlement', 'NormalizePlayerFacingSettlement']))
        code = code.replace('FORMAT', method(affairs, 'TargetLocation'))
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-player-location-') as temp:
            p = Path(temp)
            (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(s for s in result.stdout.splitlines() if s.startswith('PLAYER_LOCATION_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
