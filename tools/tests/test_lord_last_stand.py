"""Compile the production lord last-stand behavior against engine-boundary stubs."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_lord_combat import block, members, source

STUBS = r'''
using System; using System.Collections.Generic; using System.Linq;
enum MissionBehaviorType {Other}
enum AgentState {Active, Routed, Unconscious, Killed}
struct Blow {}
enum WeaponClass {Undefined, OneHandedSword, Arrow, Bolt, Javelin}
struct KillingBlow {public bool IsMissile;public int WeaponClass;}
class WeaponComponentData {}
struct AttackCollisionData {public bool AttackBlockedWithShield, CollidedWithShieldOnBack, MissileBlockedWithWeapon;}
struct AttackInformation {public Agent VictimAgent;}
class Agent {
 public enum MortalityState {Mortal, Invulnerable, Immortal}
 public MortalityState CurrentMortalityState;public bool Lord, IsMount, IsHuman=true;public int Team, Index;
 public float Health=100, HealthLimit=100;public Agent RiderAgent;public Mission Mission;
 public bool IsEnemyOf(Agent other)=>Team!=other.Team;public int Updates;public void UpdateAgentStats(){Updates++;}
}
class Mission {
 public float CurrentTime;public List<MissionBehavior> Behaviors=new List<MissionBehavior>();
 public T GetMissionBehavior<T>() where T:MissionBehavior=>Behaviors.OfType<T>().FirstOrDefault();
}
abstract class MissionBehavior {
 public Mission Mission;public abstract MissionBehaviorType BehaviorType {get;}
 public virtual void OnMissionTick(float dt){}
 public virtual void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon, bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData, float damagedHp, float hitDistance, float shotDifficulty){}
 public virtual void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow){}
}
static class GwpCommon {public static bool IsGreyWardenLordAgent(Agent a)=>a!=null&&a.Lord;}
static class GwpTuning {internal static class LordCombat {__TUNING__}}
__BEHAVIOR__
class GwpAlternativeAttackControlBehavior : MissionBehavior {
 public override MissionBehaviorType BehaviorType=>MissionBehaviorType.Other;
 readonly Dictionary<int,int> _meleeMasteryBonusByAgentIndex=new Dictionary<int,int>();readonly Dictionary<int,int> _bowMasteryBonusByAgentIndex=new Dictionary<int,int>();
 __GROWTH__
}
class Regression {
 static int checks;static void Check(bool c,string why){checks++;if(!c)throw new Exception(why);}
 static Mission mission;static GwpLordLastStandBehavior stand;static GwpAlternativeAttackControlBehavior growth;const float S=GwpTuning.LordCombat.LastStandSeconds;
 static float Hit(Agent victim,float damage,bool blocked=false){
  var info=new AttackInformation{VictimAgent=victim};var c=new AttackCollisionData{AttackBlockedWithShield=blocked};
  float allowed=GwpLordLastStandBehavior.LimitDamage(in info,in c,damage);
  if(!blocked)victim.Health=Math.Max(0,victim.Health-(float)Math.Round(allowed));return allowed;}
 static void Score(Agent victim,Agent striker,float hp,bool blocked=false){stand.OnScoreHit(victim,striker,null,blocked,false,default,default,hp,0,0);}
 // 原版按注册顺序回调：成长在前，锁血在后（SubModule 里由 Python 断言核对）。
 static void Remove(Agent victim,Agent striker,AgentState state=AgentState.Killed,KillingBlow blow=default){growth.OnAgentRemoved(victim,striker,state,blow);stand.OnAgentRemoved(victim,striker,state,blow);}
 static int Melee(Agent a)=>GwpAlternativeAttackControlBehavior.GetMeleeMasteryBonus(a);static int Bow(Agent a)=>GwpAlternativeAttackControlBehavior.GetBowMasteryBonus(a);
 public static void Main(){
  mission=new Mission();growth=new GwpAlternativeAttackControlBehavior{Mission=mission};stand=new GwpLordLastStandBehavior{Mission=mission};mission.Behaviors.Add(growth);mission.Behaviors.Add(stand);
  int next=0;Agent Make(int team,bool lord=false)=>new Agent{Team=team,Lord=lord,Mission=mission,Index=++next};
  var lord=Make(0,true);var ally=Make(0);var enemy=Make(1);var other=Make(1);
  Check(Hit(lord,30)==30&&lord.Health==70,"non-lethal hit is native");
  Check(Hit(lord,500,blocked:true)==500&&lord.Health==70,"shield damage is not a body hit and does not trigger");
  Check(Hit(enemy,500)==500,"ordinary agents die normally");
  var trooper=Make(0);var foe=Make(1);Remove(foe,trooper);Check(Melee(trooper)==0,"ordinary troops get no kill growth");
  var archer=Make(0,true);Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Arrow});Check(Bow(archer)==50&&Melee(archer)==0&&archer.Updates==1,"arrow kill grows bow by 50");
  Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Bolt});Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Javelin});Check(Melee(archer)==100&&Bow(archer)==50,"bolt and javelin kills grow the melee track");
  Remove(Make(0),archer);Remove(Make(1),archer,AgentState.Routed);Check(Melee(archer)==100,"friendly kills and routs grow nothing");
  var mount=new Agent{IsMount=true,IsHuman=false,RiderAgent=archer,Team=0,Mission=mission,Index=++next};Remove(Make(1),mount,AgentState.Unconscious);Check(Melee(archer)==150,"mount charge knockout credits the rider");
  for(int i=0;i<20;i++)Remove(Make(1),archer);Check(Melee(archer)==500,"kill growth keeps the 500 cap");
  lord.Health=30.6f;Check(!GwpLordLastStandBehavior.IsStanding(lord),"not standing before a lethal blow");Check(Hit(lord,80)==29&&lord.Health>=1&&lord.Health<2,"lethal blow leaves one hit point after engine rounding");Check(GwpLordLastStandBehavior.IsStanding(lord),"standing after a lethal blow");
  mission.CurrentTime=2;Check(Hit(lord,40)==0&&lord.Health>=1,"locked lord cannot drop below one");
  Score(enemy,lord,20);Score(enemy,lord,15);Score(enemy,lord,90,blocked:true);Score(ally,lord,50);Score(enemy,other,40);
  var horse=new Agent{IsMount=true,IsHuman=false,RiderAgent=lord,Team=0};Score(enemy,horse,10);
  Remove(other,enemy);Check(lord.Health<2,"someone else's kill does not heal");
  Remove(ally,lord);Check(lord.Health<2,"friendly kill does not heal");
  mission.CurrentTime=S-.1f;Remove(enemy,lord);float healed=lord.Health;Check(Melee(lord)==75&&!GwpLordLastStandBehavior.IsStanding(lord),"the kill ending a stand still grows at 1.5x, then the stand ends");
  Remove(Make(1),lord);Check(Melee(lord)==125,"growth is back to normal after the stand");
  Check(Math.Abs(healed-(1.6f+45))<.01f,"kill heals exactly the damage dealt to enemies in the window, including mount charge");
  Check(Hit(lord,20)==20,"lock ends at the kill");
  lord.Health=10;mission.CurrentTime=10;Check(Hit(lord,50)==9,"a successful stand re-arms the next lethal blow");
  var victim=Make(1);Score(victim,lord,300);Remove(victim,lord,AgentState.Unconscious);
  Check(lord.Health==lord.HealthLimit,"knockout counts and heal is capped by the health limit");
  lord.Health=5;mission.CurrentTime=20;Check(Hit(lord,50)==4,"third stand still available after a success");
  mission.CurrentTime=20+S-.01f;stand.OnMissionTick(0);Check(Hit(lord,50)==0,"still locked before the time limit");
  Remove(Make(1),lord,AgentState.Killed,new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Arrow});Check(Bow(lord)==75,"arrow kill during a stand grows bow by 75");lord.Health=1;
  lord.Health=5;mission.CurrentTime=20.5f;Hit(lord,50);mission.CurrentTime=20.5f+S;var late=Make(1);Score(late,lord,30);Remove(late,lord);Check(lord.Health==1,"kill at the boundary is too late");
  Check(Hit(lord,50)==50&&lord.Health==0,"expired stand without a kill does not re-arm");
  var second=Make(0,true);second.Health=10;mission.CurrentTime=30;Hit(second,50);
  mission.CurrentTime=30+S;stand.OnMissionTick(0);Check(!GwpLordLastStandBehavior.IsStanding(second),"released on the tick at the time limit");Check(Hit(second,1)==1&&second.Health==0,"tick release leaves one hit point and later damage kills");
  var immortal=Make(0,true);immortal.CurrentMortalityState=Agent.MortalityState.Immortal;immortal.Health=10;
  Check(Hit(immortal,50)==50,"undying missions do not spend the stand");
  immortal.CurrentMortalityState=Agent.MortalityState.Mortal;immortal.Health=10;Check(Hit(immortal,50)==9,"stand still available when mortality returns");
  Remove(immortal,enemy);mission.CurrentTime=100;stand.OnMissionTick(0);
  var reborn=Make(0,true);reborn.Health=10;Check(Hit(reborn,50)==9,"each agent has its own stand");
  var noBehavior=new Agent{Lord=true,Mission=new Mission(),Health=10};Check(Hit(noBehavior,50)==50,"missions without the behavior stay native");
  Console.WriteLine("LORD_LAST_STAND_ASSERTIONS="+checks+"; PASS");
 }
}
'''


class LordLastStandTests(unittest.TestCase):
    def test_lethal_lock_heal_and_release(self):
        damage_model = source("GwpAgentApplyDamageModel.cs")
        hook = re.search(r"public override float ApplyGeneralDamageModifiers\([^)]*\)\s*=>\s*(\w+)\.(\w+)\(", damage_model)
        self.assertEqual(("GwpLordLastStandBehavior", "LimitDamage"), hook.groups(),
                         "the cap must be the last step of the damage model")
        self.assertIn("new GwpLordLastStandBehavior()", source("SubModule.cs"))
        tuning = "\n".join(re.findall(r"public const float LastStand\w+ = [^;]+;", source("GwpTuning.cs")))
        self.assertEqual(2, len(tuning.splitlines()))
        sub = source("SubModule.cs")
        self.assertLess(sub.index("new GwpAlternativeAttackControlBehavior()"), sub.index("new GwpLordLastStandBehavior()"),
                        "growth must see the kill before the stand ends")
        attack = source("GwpAlternativeAttackControlBehavior.cs")
        growth = "\n".join(re.findall(r"(?m)^        private const int \w+ = \d+;", attack))
        growth += members(attack, "GetMeleeMasteryBonus", "GetBowMasteryBonus", "OnAgentRemoved", "AddMasteryBonus").replace("public void OnAgentRemoved", "public override void OnAgentRemoved")
        behavior = block(source("GwpLordLastStand.cs"), "GwpLordLastStandBehavior")
        behavior = re.sub(r"#if GWP_DIAGNOSTICS.*?#endif", "", behavior, flags=re.S)
        program = STUBS.replace("__TUNING__", tuning).replace("__BEHAVIOR__", behavior).replace("__GROWTH__", growth)
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-last-stand-") as tmp:
            folder = Path(tmp)
            (folder / "test.csproj").write_text(
                f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                f'<TargetFramework>net{major}.0</TargetFramework><Nullable>annotations</Nullable>'
                '<NoWarn>CS0649;CS1573</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (folder / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(folder), "-c", "Release"],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertRegex(result.stdout, r"LORD_LAST_STAND_ASSERTIONS=\d+; PASS")
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
