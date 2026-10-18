"""Run the production duel venue rule: both sides must be able to enter and leave the town."""
from pathlib import Path
import subprocess
import tempfile
import unittest
from test_assistance import member

MODULE = Path(__file__).resolve().parents[2] / 'GreyWardenPolicePurity'

HARNESS = r'''
using System;using System.Linq;using System.Collections.Generic;
interface IFaction{string Id{get;}}
class Clan:IFaction{public string Id{get;set;}="gw";}
class Kingdom:IFaction{public string Id{get;set;}="";}
static class FactionManager{
 public static HashSet<string> Wars=new();
 public static bool IsAtWarAgainstFaction(IFaction a,IFaction b)=>Wars.Contains(a.Id+"|"+b.Id)||Wars.Contains(b.Id+"|"+a.Id);}
struct Vec2{public float X,Y;public Vec2(float x,float y){X=x;Y=y;}
 public static Vec2 operator+(Vec2 a,Vec2 b)=>new Vec2(a.X+b.X,a.Y+b.Y);
 public static Vec2 operator*(Vec2 a,float f)=>new Vec2(a.X*f,a.Y*f);
 public float DistanceSquared(Vec2 b)=>(X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y);}
class Settlement{public string StringId="";public Vec2 GetPosition2D;}
class Town{public static List<Town> AllTowns=new();public bool IsTown=true,IsUnderSiege,InRebelliousState;public IFaction MapFaction=new Kingdom();public Settlement Settlement=new();}
class MobileParty{public Vec2 GetPosition2D;public IFaction MapFaction=new Kingdom{Id="offender"};}
static class PoliceStats{public static Clan? Clan=new();public static Clan? GetPoliceClan()=>Clan;}
static class GwpTuning{public static class Enforcement{public const int TournamentDuelNearbyTowns=3;}}
class TournamentGame{}
class TournamentManager{public HashSet<Town> Running=new();public TournamentGame? GetTournamentGame(Town t)=>Running.Contains(t)?new TournamentGame():null;}
class Campaign{public static Campaign Current=new();public TournamentManager TournamentManager=new();}
static class Probe{
METHODS
 static int Checks;static void Check(bool c,string why){Checks++;if(!c)throw new Exception(why);}
 static Town Add(string id,float x,string owner){var t=new Town{MapFaction=new Kingdom{Id=owner},Settlement=new Settlement{StringId=id,GetPosition2D=new Vec2(x,0)}};Town.AllTowns.Add(t);return t;}
 static void Main(){
  var police=new MobileParty{GetPosition2D=new Vec2(0,0)};var target=new MobileParty{GetPosition2D=new Vec2(10,0)};
  var a=Add("a",5,"k1");var b=Add("b",8,"k2");var c=Add("c",12,"k3");var far=Add("far",40,"k4");
  Check(PickDuelVenue(police,target)==a,"nearest open town to the midpoint");
  Campaign.Current.TournamentManager.Running.Add(c);
  Check(PickDuelVenue(police,target)==c,"a running tournament among the nearest three wins");
  Campaign.Current.TournamentManager.Running.Clear();
  FactionManager.Wars.Add("k1|gw");
  Check(!IsDuelVenueOpen(a,target)&&PickDuelVenue(police,target)==b,"town at war with the Wardens is closed and replaced");
  FactionManager.Wars.Add("k2|offender");
  Check(!IsDuelVenueOpen(b,target)&&PickDuelVenue(police,target)==c,"town at war with the offender's faction is closed");
  c.IsUnderSiege=true;Check(!IsDuelVenueOpen(c,target)&&PickDuelVenue(police,target)==far,"besieged town is closed");
  far.InRebelliousState=true;Check(PickDuelVenue(police,target)==null,"no open town means no venue");
  c.IsUnderSiege=false;far.InRebelliousState=false;FactionManager.Wars.Clear();
  target.MapFaction=new Kingdom{Id="k1"};Check(IsDuelVenueOpen(a,target),"offender's own faction town is open while at peace with the Wardens");
  PoliceStats.Clan=null;Check(!IsDuelVenueOpen(a,target)&&PickDuelVenue(police,target)==null,"without the Warden clan no town is open");
  Console.WriteLine("DUEL_VENUE_ASSERTIONS="+Checks+"; PASS");
 }
}
'''


class DuelVenueTests(unittest.TestCase):
    def test_venue_open_for_both_sides_and_replacement(self):
        source = (MODULE / 'PoliceEnforcementBehavior.Envoy.cs').read_text(encoding='utf-8-sig')
        methods = '\n'.join(member(source, name) for name in ['IsDuelVenueOpen', 'PickDuelVenue'])
        major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
        with tempfile.TemporaryDirectory(prefix='gwp-duel-venue-') as temp:
            path = Path(temp)
            (path / 'Replay.csproj').write_text(
                f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable>'
                f'<EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
            (path / 'Program.cs').write_text(HARNESS.replace('METHODS', methods), encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'],
                                    capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(next(line for line in result.stdout.splitlines() if line.startswith('DUEL_VENUE_ASSERTIONS=')))


if __name__ == '__main__':
    unittest.main()
