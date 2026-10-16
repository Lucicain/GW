"""Check the actual raid model's gates, single native call and bounded trace."""
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_lord_combat import source


STUBS = r'''
namespace TaleWorlds.CampaignSystem {
 public class Hero {public string StringId="offender";}
 public class Army {public enum ArmyTypes {Raider,Defender}}
 public class CampaignTime {public static CampaignTime Now=new CampaignTime();public double ToHours;}
 public class Campaign {public static Campaign Current=new Campaign();public Models Models=new Models();}
 public class Models {public object TargetScoreCalculatingModel;}
}
namespace TaleWorlds.CampaignSystem.Party {public class MobileParty {public TaleWorlds.CampaignSystem.Hero LeaderHero=new TaleWorlds.CampaignSystem.Hero();}}
namespace TaleWorlds.CampaignSystem.Settlements {public class Settlement {public string StringId="village";}}
namespace TaleWorlds.CampaignSystem.GameComponents {
 public class DefaultTargetScoreCalculatingModel {
  public float NativeScore=2;public int Calls;
  public virtual float GetTargetScoreForFaction(TaleWorlds.CampaignSystem.Settlements.Settlement s,TaleWorlds.CampaignSystem.Army.ArmyTypes t,TaleWorlds.CampaignSystem.Party.MobileParty p,float strength){Calls++;return NativeScore;}
 }
}
namespace GreyWardenPolicePurity {
 static class GwpAiDeterrenceState {public static float Multiplier=0.65f;public static int Calls;public static float GetVillagerAttackScoreMultiplier(TaleWorlds.CampaignSystem.Party.MobileParty p){Calls++;return Multiplier;}}
 static class GwpAiDiagnostics {public static int Samples;public static string Last;public static void WriteFieldArrest(string stage,string text){Samples++;Last=text;}}
 class Regression {
  static int checks;static void Check(bool c,string why){checks++;if(!c)throw new System.Exception(why);}
  static void Main(){
   var model=new PoliceRaidDeterrenceModel();TaleWorlds.CampaignSystem.Campaign.Current.Models.TargetScoreCalculatingModel=model;
   var p=new TaleWorlds.CampaignSystem.Party.MobileParty();var s=new TaleWorlds.CampaignSystem.Settlements.Settlement();
   var raid=TaleWorlds.CampaignSystem.Army.ArmyTypes.Raider;var defend=TaleWorlds.CampaignSystem.Army.ArmyTypes.Defender;
   Check(model.GetTargetScoreForFaction(s,raid,p,100)==1.3f,"actual raid score multiplied");
   Check(model.Calls==1&&GwpAiDeterrenceState.Calls==1,"native score evaluated exactly once");
   Check(model.GetTargetScoreForFaction(s,defend,p,100)==2,"other missions unchanged");
   model.NativeScore=0;Check(model.GetTargetScoreForFaction(s,raid,p,100)==0,"zero native score stays zero");
   model.NativeScore=-1;Check(model.GetTargetScoreForFaction(s,raid,p,100)==-1,"negative native score unchanged");
   model.NativeScore=2;p.LeaderHero=null;Check(model.GetTargetScoreForFaction(s,raid,p,100)==2,"leaderless party unchanged");
   Check(GwpAiDeterrenceState.Calls==1&&model.Calls==5,"excluded calls do not query deterrence");
   p.LeaderHero=new TaleWorlds.CampaignSystem.Hero();model.GetTargetScoreForFaction(s,raid,p,100);
#if GWP_DIAGNOSTICS
   Check(GwpAiDiagnostics.Samples==1,"same day sample is bounded");
   TaleWorlds.CampaignSystem.CampaignTime.Now.ToHours=24;model.GetTargetScoreForFaction(s,raid,p,100);
   Check(GwpAiDiagnostics.Samples==2&&GwpAiDiagnostics.Last.Contains("activeModel=GreyWardenPolicePurity.PoliceRaidDeterrenceModel"),"next day logs actual active model and score");
#else
   Check(GwpAiDiagnostics.Samples==0,"player build never calls diagnostics");
#endif
   GwpAiDeterrenceState.Multiplier=0;Check(model.GetTargetScoreForFaction(s,raid,p,100)==0,"warden zero multiplier respected");
   GwpAiDeterrenceState.Multiplier=1;Check(model.GetTargetScoreForFaction(s,raid,p,100)==2,"unpunished lord retains native score");
   System.Console.WriteLine("RAID_SCORE_ASSERTIONS="+checks+"; PASS");
  }
 }
}
'''


class RaidScoreTests(unittest.TestCase):
    def test_model_gates_and_diagnostic_builds(self):
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-raid-") as directory:
            path = Path(directory)
            (path / "Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><NoWarn>CS0649</NoWarn>'
                '</PropertyGroup></Project>', encoding="utf-8")
            (path / "Model.cs").write_text(source("PoliceRaidDeterrenceModel.cs"), encoding="utf-8")
            (path / "Program.cs").write_text(STUBS, encoding="utf-8")
            for diagnostics in (False, True):
                command = ["dotnet", "run", "--project", directory, "-c", "Release", "-v", "quiet"]
                if diagnostics:
                    command += ["-p:DefineConstants=GWP_DIAGNOSTICS"]
                result = subprocess.run(command, capture_output=True, text=True, timeout=60)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("; PASS", result.stdout)
                print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
