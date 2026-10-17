"""Check the actual raid model's gates, single native call and bounded trace."""
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_lord_combat import source, members
import re


STUBS = r'''
using System;using System.Collections.Generic;using TaleWorlds.CampaignSystem;using TaleWorlds.CampaignSystem.Settlements;
namespace TaleWorlds.CampaignSystem {
 public class Hero {public string StringId="offender";}
 public class Army {public enum ArmyTypes {Raider,Defender}}
 public class CampaignTime {public static CampaignTime Now=new CampaignTime();public double ToHours;}
 public class Campaign {public static Campaign Current=new Campaign();public Models Models=new Models();}
 public class Models {public object TargetScoreCalculatingModel;}
}
namespace TaleWorlds.CampaignSystem.Party {public class MobileParty {public TaleWorlds.CampaignSystem.Hero LeaderHero=new TaleWorlds.CampaignSystem.Hero();}}
namespace TaleWorlds.CampaignSystem.Settlements {public class Village {public float Hearth=100;} public class Settlement {public string StringId="village";public bool IsVillage=true;public Village Village=new Village();}}
namespace TaleWorlds.CampaignSystem.GameComponents {
 public class DefaultTargetScoreCalculatingModel {
  public float NativeScore=2;public int Calls;
  public virtual float GetTargetScoreForFaction(TaleWorlds.CampaignSystem.Settlements.Settlement s,TaleWorlds.CampaignSystem.Army.ArmyTypes t,TaleWorlds.CampaignSystem.Party.MobileParty p,float strength){Calls++;return NativeScore;}
 }
}
namespace GreyWardenPolicePurity {
 class GwpFireswornBehavior {
  private class FaithVillage {public int Households;public Hero Priest;}
  private static GwpFireswornBehavior _instance;
  private Dictionary<string,FaithVillage> _villages=new Dictionary<string,FaithVillage>();
  private static Hero FindLivingPriest(FaithVillage r)=>r.Priest;
  public static void Setup(Settlement s,int households,bool priest=true){_instance=new GwpFireswornBehavior();_instance._villages[s.StringId]=new FaithVillage{Households=households,Priest=priest?new Hero():null};}
  public static void Clear()=>_instance=null;
  FAITH_METHODS
 }
 static class GwpTuning {public static class Firesworn {public const float MaximumFaithRaidScoreReduction=MAX_REDUCTION;}}
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
   GwpFireswornBehavior.Setup(s,0);Check(model.GetTargetScoreForFaction(s,raid,p,100)==2,"zero faithful retains native score");
   for(int share=0;share<=100;share+=10){
    GwpFireswornBehavior.Setup(s,share);float got=model.GetTargetScoreForFaction(s,raid,p,100);
    Check(System.Math.Abs(got-2f*(1f-.5f*share/100f))<.00001f,"faith increases suppression monotonically: "+share);
   }
   GwpFireswornBehavior.Setup(s,80);GwpAiDeterrenceState.Multiplier=.65f;
   Check(System.Math.Abs(model.GetTargetScoreForFaction(s,raid,p,100)-.78f)<.00001f,"80 percent faithful stacks with existing deterrence to 39 percent score");
   GwpFireswornBehavior.Setup(s,100);Check(System.Math.Abs(model.GetTargetScoreForFaction(s,raid,p,100)-.65f)<.00001f,"full faith adds at most half reduction");
   GwpFireswornBehavior.Setup(s,1000);Check(GwpFireswornBehavior.GetPreachedVillageShare(s)==1,"hearth drop cannot over-suppress beyond full share");
   GwpFireswornBehavior.Setup(s,-10);Check(GwpFireswornBehavior.GetPreachedVillageShare(s)==0,"negative stored count cannot raise raid score");
   GwpFireswornBehavior.Setup(s,100,false);Check(model.GetTargetScoreForFaction(s,raid,p,100)==1.3f,"without living priest only personal deterrence applies");
   GwpFireswornBehavior.Setup(s,100);s.IsVillage=false;Check(GwpFireswornBehavior.GetPreachedVillageShare(s)==0,"non-village gets no faith multiplier");s.IsVillage=true;
   s.StringId="unregistered";Check(GwpFireswornBehavior.GetPreachedVillageShare(s)==0,"unregistered village gets no faith multiplier");s.StringId="village";
   s.Village.Hearth=0;Check(GwpFireswornBehavior.GetPreachedVillageShare(s)==1,"zero hearth uses existing finite share denominator");s.Village.Hearth=100;
   Check(model.GetTargetScoreForFaction(s,defend,p,100)==2,"defend mission in full-faith village stays unchanged");
   model.NativeScore=0;Check(model.GetTargetScoreForFaction(s,raid,p,100)==0,"faith never turns native zero into a target");model.NativeScore=2;
#if GWP_DIAGNOSTICS
   Check(GwpAiDiagnostics.Samples==3,"faith and ordinary categories bounded separately per day");
   Check(GwpAiDiagnostics.Last.Contains("faithShare=")&&GwpAiDiagnostics.Last.Contains("faithMultiplier=")&&GwpAiDiagnostics.Last.Contains("deterrenceMultiplier="),"one actual-score sample includes both factors");
#endif
   GwpFireswornBehavior.Clear();
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
                f'<TargetFramework>net{major}.0</TargetFramework><NoWarn>CS0649;CS8632</NoWarn>'
                '</PropertyGroup></Project>', encoding="utf-8")
            (path / "Model.cs").write_text(source("PoliceRaidDeterrenceModel.cs"), encoding="utf-8")
            faith = members(source("GwpFireswornBehavior.cs"), "IsPreachedVillage", "GetPreachedVillageShare", "SwornShare")
            reduction = re.search(r"MaximumFaithRaidScoreReduction\s*=\s*([^;]+);", source("GwpTuning.cs")).group(1)
            (path / "Program.cs").write_text(STUBS.replace("FAITH_METHODS", faith).replace("MAX_REDUCTION", reduction), encoding="utf-8")
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
