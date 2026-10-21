"""Execute production per-agent growth and effective-skill reads at engine boundaries."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from test_lord_combat import block, members, source

SKILL_STUBS = r'''
class SkillObject {}
static class DefaultSkills {
 public static readonly SkillObject OneHanded=new(),TwoHanded=new(),Polearm=new(),Bow=new(),Crossbow=new(),Throwing=new(),Athletics=new(),Riding=new();
}
enum WeaponClass {Undefined,OneHandedSword,TwoHandedSword,OneHandedPolearm,Arrow,Bolt,Javelin,ThrowingAxe}
enum AgentAttackType {Standard,Kick,Bash}
class WeaponComponentData {
 public SkillObject RelevantSkill;
 // Native GetRelevantSkillFromWeaponClass is the controlled engine boundary.
 public static SkillObject GetRelevantSkillFromWeaponClass(WeaponClass w)=>w switch {
 WeaponClass.OneHandedSword=>DefaultSkills.OneHanded,WeaponClass.TwoHandedSword=>DefaultSkills.TwoHanded,
 WeaponClass.OneHandedPolearm=>DefaultSkills.Polearm,WeaponClass.Arrow=>DefaultSkills.Bow,WeaponClass.Bolt=>DefaultSkills.Crossbow,
 WeaponClass.Javelin or WeaponClass.ThrowingAxe=>DefaultSkills.Throwing,_=>null};
}
static class GwpKickBehavior {public static bool IsEligibleGreyWarden(Agent a)=>a!=null&&(a.Warden||a.Lord);}
'''

STUBS = r'''
using System;using System.Collections.Generic;using System.Linq;
namespace TaleWorlds.Library {struct Vec3 {}struct Mat3 {}}
enum AgentState {Active,Routed,Killed,Unconscious}
enum MissionBehaviorType {Other}
enum EquipmentIndex {Weapon0,Weapon1,Weapon2}
struct MissionWeapon {public WeaponComponentData CurrentUsageItem;}
class MissionEquipment {readonly MissionWeapon[] items=new MissionWeapon[3];public MissionWeapon this[EquipmentIndex i]{get=>items[(int)i];set=>items[(int)i]=value;}public MissionWeapon this[int i]{get=>items[i];set=>items[i]=value;}}
struct Blow {public AgentAttackType AttackType;}
struct KillingBlow {public bool IsMissile;public int WeaponClass;public AgentAttackType AttackType;}
struct AttackCollisionData {public bool IsAlternativeAttack;}
class Agent {public bool Warden=true,Lord,IsHuman=true,IsMount;public int Team,Index,Updates;public Agent RiderAgent;public Mission Mission;public MissionEquipment Equipment=new();public bool IsEnemyOf(Agent a)=>a.Team!=Team;public void UpdateAgentStats(){Updates++;}}
class Mission {public GwpAlternativeAttackControlBehavior Growth;public T GetMissionBehavior<T>() where T:class=>Growth as T;}
class MissionBehavior {public virtual void OnAgentHit(Agent a,Agent b,in MissionWeapon w,in Blow blow,in AttackCollisionData c){}public virtual void OnAgentShootMissile(Agent a,EquipmentIndex i,TaleWorlds.Library.Vec3 p,TaleWorlds.Library.Vec3 v,TaleWorlds.Library.Mat3 o,bool r,int m){}public virtual void OnAgentRemoved(Agent a,Agent b,AgentState s,KillingBlow k){}public virtual void OnAgentDeleted(Agent a){}public virtual void OnRemoveBehavior(){}}
static class GwpCommon {public static bool IsGreyWardenLordAgent(Agent a)=>a.Lord;}
static class GwpTuning {public static class LastStand {public const float SkillMultiplier=1.5f;}}
static class GwpLastStandBehavior {public static Agent Standing;public static bool IsStanding(Agent a)=>a==Standing;}
__SKILLS__
class GwpAlternativeAttackControlBehavior:MissionBehavior {
 readonly Dictionary<int,Dictionary<SkillObject,int>> _masteryByAgentIndex=new();
 class PendingAlternativeAttack {public HashSet<int> NativeHitTargetIndices=new();}
 readonly Dictionary<int,PendingAlternativeAttack> _pendingActions=new();
 __GROWTH__
}
class GwpAgentStatCalculateModel {__APPLY__}
class Regression {
 static int n;static void Check(bool c,string why){n++;if(!c)throw new Exception(why);}
 static int Bonus(Agent a,SkillObject s)=>GwpAlternativeAttackControlBehavior.GetMasteryBonus(a,s);
 public static void Main(){
 var mission=new Mission();var g=mission.Growth=new GwpAlternativeAttackControlBehavior();int idx=0;
 Agent Make(int team=0,bool lord=false)=>new(){Index=++idx,Team=team,Lord=lord,Mission=mission};
 var soldier=Make();var other=Make();var lord=Make(lord:true);
 void Shoot(Agent a,SkillObject s){a.Equipment[0]=new MissionWeapon{CurrentUsageItem=new WeaponComponentData{RelevantSkill=s}};g.OnAgentShootMissile(a,EquipmentIndex.Weapon0,default,default,default,false,0);}
 void Kill(Agent a,WeaponClass w,AgentState state=AgentState.Killed,int team=1)=>g.OnAgentRemoved(Make(team),a,state,new KillingBlow{WeaponClass=(int)w});
 foreach(var s in new[]{DefaultSkills.Bow,DefaultSkills.Crossbow}){Shoot(soldier,s);Check(Bonus(soldier,s)==10,"each bow/crossbow shot grows its own skill");}
 Shoot(soldier,DefaultSkills.Throwing);Check(Bonus(soldier,DefaultSkills.Throwing)==30,"throw grows 30 on release without a hit");
 Shoot(soldier,DefaultSkills.OneHanded);Check(Bonus(soldier,DefaultSkills.OneHanded)==0,"unrelated skill is not ranged growth");
 other.Warden=false;Shoot(other,DefaultSkills.Bow);Kill(other,WeaponClass.Arrow);Check(Bonus(other,DefaultSkills.Bow)==0,"ordinary outsider never grows");
 foreach(var pair in new[]{(WeaponClass.OneHandedSword,DefaultSkills.OneHanded),(WeaponClass.TwoHandedSword,DefaultSkills.TwoHanded),(WeaponClass.OneHandedPolearm,DefaultSkills.Polearm),(WeaponClass.Arrow,DefaultSkills.Bow),(WeaponClass.Bolt,DefaultSkills.Crossbow),(WeaponClass.Javelin,DefaultSkills.Throwing)}){
 int before=Bonus(soldier,pair.Item2);Kill(soldier,pair.Item1);Check(Bonus(soldier,pair.Item2)==before+25,"soldier kill maps to actual weapon skill");Kill(lord,pair.Item1,AgentState.Unconscious);Check(Bonus(lord,pair.Item2)==50,"lord knockout maps to same skill with higher growth");}
 Check(Bonus(soldier,DefaultSkills.Athletics)==0,"weapon kills no longer silently grow athletics");
 int bow=Bonus(soldier,DefaultSkills.Bow);Kill(soldier,WeaponClass.Arrow,team:0);Kill(soldier,WeaponClass.Arrow,AgentState.Routed);Check(Bonus(soldier,DefaultSkills.Bow)==bow,"friendly kills and routs give no growth");
 var collision=new AttackCollisionData{IsAlternativeAttack=true};var kick=new Blow{AttackType=AgentAttackType.Kick};var mw=default(MissionWeapon);
 g.OnAgentHit(Make(1),soldier,in mw,in kick,in collision);Check(Bonus(soldier,DefaultSkills.OneHanded)==75&&Bonus(soldier,DefaultSkills.Athletics)==50,"kick hit keeps 50 one-handed and athletics");
 g.OnAgentHit(Make(0),soldier,in mw,in kick,in collision);Check(Bonus(soldier,DefaultSkills.Athletics)==50,"friendly kick is excluded");
 GwpLastStandBehavior.Standing=lord;Shoot(lord,DefaultSkills.Throwing);Kill(lord,WeaponClass.Bolt);Check(Bonus(lord,DefaultSkills.Throwing)==80&&Bonus(lord,DefaultSkills.Crossbow)==100,"last stand does not inflate earned throws and kills");Check(GwpAgentStatCalculateModel.ApplyBattleMastery(lord,DefaultSkills.Throwing,220)==450,"battle frenzy multiplies existing base plus earned skill as a whole");
 Check(GwpAgentStatCalculateModel.ApplyBattleMastery(lord,DefaultSkills.Athletics,170)==255,"skill without any growth still receives frenzy multiplier");
 Check(GwpAgentStatCalculateModel.ApplyBattleMastery(soldier,DefaultSkills.Throwing,150)==150+Bonus(soldier,DefaultSkills.Throwing),"other soldier's effective skills stay independent");
 GwpLastStandBehavior.Standing=null;Check(GwpAgentStatCalculateModel.ApplyBattleMastery(lord,DefaultSkills.Throwing,220)==300,"frenzy expiry restores base plus earned skill without permanently inflated growth");
 for(int i=0;i<120;i++)Shoot(soldier,DefaultSkills.Bow);Check(Bonus(soldier,DefaultSkills.Bow)==bow+1200,"old cumulative cap is gone");
 Check(GwpAgentStatCalculateModel.ApplyBattleMastery(soldier,DefaultSkills.Bow,220)==220+bow+1200,"effective skill can exceed former 1000 cap and reaches native formulas");
 Check(Bonus(other,DefaultSkills.Bow)==0,"per-agent growth never leaks");
 g.OnAgentDeleted(soldier);Check(Bonus(soldier,DefaultSkills.Bow)==0,"agent deletion clears earned skills");g.OnRemoveBehavior();Check(Bonus(lord,DefaultSkills.Crossbow)==0,"mission end clears all growth");
 Console.WriteLine("BATTLE_GROWTH_ASSERTIONS="+n+"; PASS");
 }
}
'''


def growth_members(*names):
    attack = source("GwpAlternativeAttackControlBehavior.cs")
    constants = block(re.sub(r"(?m)^    ", "", source("GwpTuning.cs")), "BattleGrowth")
    result = constants + members(attack, *names).replace("GwpTuning.BattleGrowth.", "BattleGrowth.")
    # The member extractor intentionally removes override on extracted instance methods.
    return re.sub(r"public void (On\w+)\(", r"public override void \1(", result)


class BattleGrowthTests(unittest.TestCase):
    def test_projectiles_kills_caps_and_isolation(self):
        growth = growth_members("GetMasteryBonus", "OnAgentHit", "OnAgentShootMissile", "OnAgentRemoved",
                                "OnAgentDeleted", "OnRemoveBehavior", "AddMeleeMastery", "AddMasteryBonus", "IsAlternativeAttack")
        apply = members(source("GwpAgentStatCalculateModel.cs"), "ApplyBattleMastery")
        program = STUBS.replace("__SKILLS__", SKILL_STUBS).replace("__GROWTH__", growth).replace("__APPLY__", apply)
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-battle-growth-") as tmp:
            p = Path(tmp)
            (p / "test.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable><NoWarn>CS0649</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (p / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(p), "-c", "Release", "-v", "quiet"], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            print(result.stdout.strip())
