"""Compile the production shared last-stand behavior against engine-boundary stubs."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_lord_combat import block, members, source
from test_battle_growth import SKILL_STUBS, growth_members

STUBS = r'''
using System; using System.Collections.Generic; using System.Linq;
enum MissionBehaviorType {Other}
enum AgentState {Active, Routed, Unconscious, Killed}
struct Blow {}
__SKILLS__
struct KillingBlow {public bool IsMissile;public int WeaponClass;public AgentAttackType AttackType;}

struct AttackCollisionData {public bool AttackBlockedWithShield, CollidedWithShieldOnBack, MissileBlockedWithWeapon;}
struct AttackInformation {public Agent VictimAgent;}
class BasicCharacterObject {public string StringId;public bool IsHero;}
static class GwpIds {__IDS__}
class Agent {
 public enum MortalityState {Mortal, Invulnerable, Immortal}
 public MortalityState CurrentMortalityState;public bool Warden=true,Lord, IsMount, IsHuman=true;public int Team, Index;
 public BasicCharacterObject Character=new(){StringId="gwrecruit"};public float Health=100, HealthLimit=100;public Agent RiderAgent;public Mission Mission;
 public bool IsEnemyOf(Agent other)=>Team!=other.Team;public int Updates,CurrentMelee;public void UpdateAgentStats(){Updates++;CurrentMelee=GwpAgentStatCalculateModel.ApplyBattleMastery(this,DefaultSkills.OneHanded,200);}
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
static class GwpTuning {internal static class LastStand {__TUNING__}}
__BEHAVIOR__
class GwpAgentStatCalculateModel{__APPLY__}
class GwpAlternativeAttackControlBehavior : MissionBehavior {
 public override MissionBehaviorType BehaviorType=>MissionBehaviorType.Other;
 readonly Dictionary<int,Dictionary<SkillObject,int>> _masteryByAgentIndex=new();
 __GROWTH__
}
class Regression {
 static int checks;static void Check(bool c,string why){checks++;if(!c)throw new Exception(why);}
 static Mission mission;static GwpLastStandBehavior stand;static GwpAlternativeAttackControlBehavior growth;const float S=GwpTuning.LastStand.LordSeconds;
 static float Hit(Agent victim,float damage,bool blocked=false){
  var info=new AttackInformation{VictimAgent=victim};var c=new AttackCollisionData{AttackBlockedWithShield=blocked};
  float allowed=GwpLastStandBehavior.LimitDamage(in info,in c,damage);
  if(!blocked)victim.Health=Math.Max(0,victim.Health-(float)Math.Round(allowed));return allowed;}
 static void Score(Agent victim,Agent striker,float hp,bool blocked=false){stand.OnScoreHit(victim,striker,null,blocked,false,default,default,hp,0,0);}
 // 原版按注册顺序回调：成长在前，锁血在后（SubModule 里由 Python 断言核对）。
 static void Remove(Agent victim,Agent striker,AgentState state=AgentState.Killed,KillingBlow blow=default){if(blow.WeaponClass==(int)WeaponClass.Undefined)blow.WeaponClass=(int)WeaponClass.OneHandedSword;growth.OnAgentRemoved(victim,striker,state,blow);stand.OnAgentRemoved(victim,striker,state,blow);}
 static int Melee(Agent a)=>GwpAlternativeAttackControlBehavior.GetMasteryBonus(a,DefaultSkills.OneHanded);static int Bow(Agent a)=>GwpAlternativeAttackControlBehavior.GetMasteryBonus(a,DefaultSkills.Bow);
 public static void Main(){
  mission=new Mission();growth=new GwpAlternativeAttackControlBehavior{Mission=mission};stand=new GwpLastStandBehavior{Mission=mission};mission.Behaviors.Add(growth);mission.Behaviors.Add(stand);
  int next=0;Agent Make(int team,bool lord=false,string id="ordinary")=>new Agent{Team=team,Lord=lord,Mission=mission,Index=++next,Character=new(){StringId=id,IsHero=lord}};
  var lord=Make(0,true);var ally=Make(0);var enemy=Make(1);var other=Make(1);
  Check(Hit(lord,30)==30&&lord.Health==70,"non-lethal hit is native");
  Check(Hit(lord,500,blocked:true)==500&&lord.Health==70,"shield damage is not a body hit and does not trigger");
  Check(Hit(enemy,500)==500,"ordinary agents die normally");
  var trooper=Make(0);var foe=Make(1);Remove(foe,trooper);Check(Melee(trooper)==25,"warden troops get smaller kill growth");
  var archer=Make(0,true);Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Arrow});Check(Bow(archer)==50&&Melee(archer)==0&&archer.Updates==1,"arrow kill grows bow by 50");
  Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Bolt});Remove(Make(1),archer,blow:new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Javelin});Check(Melee(archer)==0&&Bow(archer)==50&&GwpAlternativeAttackControlBehavior.GetMasteryBonus(archer,DefaultSkills.Crossbow)==50&&GwpAlternativeAttackControlBehavior.GetMasteryBonus(archer,DefaultSkills.Throwing)==50,"bolt and javelin kills grow their own skill");
  Remove(Make(0),archer);Remove(Make(1),archer,AgentState.Routed);Check(Melee(archer)==0,"friendly kills and routs grow nothing");
  var mount=new Agent{IsMount=true,IsHuman=false,RiderAgent=archer,Team=0,Mission=mission,Index=++next};Remove(Make(1),mount,AgentState.Unconscious);Check(GwpAlternativeAttackControlBehavior.GetMasteryBonus(archer,DefaultSkills.Riding)==50,"mount charge knockout grows rider riding");
  for(int i=0;i<20;i++)Remove(Make(1),archer);Check(Melee(archer)==1000,"kill growth has no mod cumulative cap");
  lord.Health=30.6f;Check(!GwpLastStandBehavior.IsStanding(lord),"not standing before a lethal blow");Check(Hit(lord,80)==29&&lord.Health>=1&&lord.Health<2,"lethal blow leaves one hit point after engine rounding");Check(GwpLastStandBehavior.IsStanding(lord),"standing after a lethal blow");
  mission.CurrentTime=2;Check(Hit(lord,40)==0&&lord.Health>=1,"locked lord cannot drop below one");
  Score(enemy,lord,20);Score(enemy,lord,15);Score(enemy,lord,90,blocked:true);Score(ally,lord,50);Score(enemy,other,40);
  var horse=new Agent{IsMount=true,IsHuman=false,RiderAgent=lord,Team=0};Score(enemy,horse,10);
  Remove(other,enemy);Check(lord.Health<2,"someone else's kill does not heal");
  Remove(ally,lord);Check(lord.Health<2,"friendly kill does not heal");
  mission.CurrentTime=S-.1f;Remove(enemy,lord);float healed=lord.Health;Check(Melee(lord)==50&&!GwpLastStandBehavior.IsStanding(lord),"the kill ending a stand keeps normal earned growth, then the stand ends");
  Remove(Make(1),lord);Check(Melee(lord)==100,"growth is back to normal after the stand");
  Check(Math.Abs(healed-(1.6f+45))<.01f,"kill heals exactly the damage dealt to enemies in the window, including mount charge");
  Check(Hit(lord,20)==20,"lock ends at the kill");
  lord.Health=10;mission.CurrentTime=10;Check(Hit(lord,50)==9,"a successful stand re-arms the next lethal blow");
  var victim=Make(1);Score(victim,lord,300);Remove(victim,lord,AgentState.Unconscious);
  Check(lord.Health==lord.HealthLimit,"knockout counts and heal is capped by the health limit");
  lord.Health=5;mission.CurrentTime=20;Check(Hit(lord,50)==4,"third stand still available after a success");
  mission.CurrentTime=20+S-.01f;stand.OnMissionTick(0);Check(Hit(lord,50)==0,"still locked before the time limit");
  Remove(Make(1),lord,AgentState.Killed,new KillingBlow{IsMissile=true,WeaponClass=(int)WeaponClass.Arrow});Check(Bow(lord)==50,"arrow kill during a stand grows bow by its normal 50");lord.Health=1;
  lord.Health=5;mission.CurrentTime=20.5f;Hit(lord,50);mission.CurrentTime=20.5f+S;var late=Make(1);Score(late,lord,30);Remove(late,lord);Check(lord.Health==1,"kill at the boundary is too late");
  Check(Hit(lord,50)==50&&lord.Health==0,"expired stand without a kill does not re-arm");
  var second=Make(0,true);second.Health=10;mission.CurrentTime=30;Hit(second,50);
  mission.CurrentTime=30+S;stand.OnMissionTick(0);Check(!GwpLastStandBehavior.IsStanding(second),"released on the tick at the time limit");Check(Hit(second,1)==1&&second.Health==0,"tick release leaves one hit point and later damage kills");
  var immortal=Make(0,true);immortal.CurrentMortalityState=Agent.MortalityState.Immortal;immortal.Health=10;
  Check(Hit(immortal,50)==50,"undying missions do not spend the stand");
  immortal.CurrentMortalityState=Agent.MortalityState.Mortal;immortal.Health=10;Check(Hit(immortal,50)==9,"stand still available when mortality returns");
  Remove(immortal,enemy);mission.CurrentTime=100;stand.OnMissionTick(0);
  var reborn=Make(0,true);reborn.Health=10;Check(Hit(reborn,50)==9,"each agent has its own stand");
  var noBehavior=new Agent{Lord=true,Mission=new Mission(),Health=10};Check(Hit(noBehavior,50)==50,"missions without the behavior stay native");
  Check(S==10f&&GwpTuning.LastStand.EliteTroopSeconds==4f,"lord remains ten seconds and elite soldiers get exactly four");
  foreach(var tier in new[]{("gwnewrecruit",1f),("gwrecruit",2f),("gwheavyinfantry",4f),("gwarcher",4f)}){
   string id=tier.Item1;float seconds=tier.Item2;mission.CurrentTime=200;var elite=Make(0,id:id);elite.Health=8;
   var shieldOnly=new AttackInformation{VictimAgent=elite};var back=new AttackCollisionData{CollidedWithShieldOnBack=true};
   Check(GwpLastStandBehavior.LimitDamage(in shieldOnly,in back,999)==999&&!GwpLastStandBehavior.IsStanding(elite),id+" back shield hit does not spend stand");
   Check(Hit(elite,1000)==7&&elite.Health==1&&elite.Updates==1&&elite.CurrentMelee==300&&GwpLastStandBehavior.IsStanding(elite),id+" lethal body damage begins individual stand");
   mission.CurrentTime=200+seconds-.001f;Check(Hit(elite,1000)==0&&GwpLastStandBehavior.IsStanding(elite),id+" remains protected just before own tier window");
   Score(Make(1),elite,60);Remove(Make(0),elite);var animal=Make(1);animal.IsHuman=false;Remove(animal,elite);
   Remove(Make(1),elite,AgentState.Routed);Check(elite.Health==1&&GwpLastStandBehavior.IsStanding(elite),id+" allies animals and routs cannot end stand");
   Remove(Make(1),elite,AgentState.Unconscious,new KillingBlow{WeaponClass=(int)WeaponClass.Arrow});
   Check(elite.Health==61&&!GwpLastStandBehavior.IsStanding(elite)&&Bow(elite)==25&&elite.Updates==3&&elite.CurrentMelee==200,id+" enemy knockout heals actual window damage and keeps normal earned soldier growth");
   Check(Hit(elite,1000)==60&&elite.Health==1,id+" successful recovery re-arms next stand");
   var independent=Make(0,id:id);independent.Health=3;Check(Hit(independent,1000)==2,id+" another soldier has independent state");
   mission.CurrentTime=210;stand.OnMissionTick(0);Check(!GwpLastStandBehavior.IsStanding(elite)&&!GwpLastStandBehavior.IsStanding(independent),id+" tier-specific expiry releases both");
   elite.Health=20;Check(Hit(elite,1000)==1000&&elite.Health==0,id+" expired soldier cannot re-arm even if healed externally");
   mission.CurrentTime=300;var lateElite=Make(0,id:id);lateElite.Health=9;Hit(lateElite,1000);Score(Make(1),lateElite,90);mission.CurrentTime=300+seconds;
   Remove(Make(1),lateElite);Check(lateElite.Health==1&&Melee(lateElite)==25,id+" kill exactly at expiry cannot heal or get multiplier");
  }
  foreach(string id in new[]{"gwknight","gwzealot","imperial_legionary","other_tier6_archer"}){
   var excluded=Make(0,id:id);excluded.Health=7;Check(Hit(excluded,1000)==1000&&!GwpLastStandBehavior.IsStanding(excluded),id+" does not inherit elite stand (including dismounted knight)");
  }
  var pretender=Make(0,id:"gwheavyinfantry");pretender.Character.IsHero=true;Check(GwpLastStandBehavior.DurationFor(pretender)==0,"non-Warden hero cannot borrow troop ID eligibility");
  var nonHuman=Make(0,id:"gwarcher");nonHuman.IsHuman=false;Check(GwpLastStandBehavior.DurationFor(nonHuman)==0,"a mount cannot qualify via troop ID");
  Console.WriteLine("LAST_STAND_ASSERTIONS="+checks+"; PASS");
 }
}
'''


class LastStandTests(unittest.TestCase):
    def test_lethal_lock_heal_and_release(self):
        damage_model = source("GwpAgentApplyDamageModel.cs")
        hook = re.search(r"public override float ApplyGeneralDamageModifiers\([^)]*\)\s*=>\s*(\w+)\.(\w+)\(", damage_model)
        self.assertEqual(("GwpLastStandBehavior", "LimitDamage"), hook.groups(),
                         "the cap must be the last step of the damage model")
        self.assertIn("new GwpLastStandBehavior()", source("SubModule.cs"))
        tuning = block(re.sub(r"(?m)^    ", "", source("GwpTuning.cs")), "LastStand")
        tuning = tuning[tuning.index("{")+1:tuning.rfind("}")]
        ids = "\n".join(re.findall(r"public const string (?:HeavyInfantryId|ArcherId|PoliceRecruitId|NewRecruitId) = [^;]+;", source("GwpIds.cs")))
        sub = source("SubModule.cs")
        self.assertLess(sub.index("new GwpAlternativeAttackControlBehavior()"), sub.index("new GwpLastStandBehavior()"),
                        "growth must see the kill before the stand ends")
        growth = growth_members("GetMasteryBonus", "OnAgentRemoved", "AddMasteryBonus")
        behavior = block(source("GwpLastStand.cs"), "GwpLastStandBehavior")
        behavior = re.sub(r"#if GWP_DIAGNOSTICS.*?#endif", "", behavior, flags=re.S)
        program = STUBS.replace("__SKILLS__", SKILL_STUBS).replace("__TUNING__", tuning).replace("__IDS__", ids).replace("__BEHAVIOR__", behavior).replace("__APPLY__", members(source("GwpAgentStatCalculateModel.cs"), "ApplyBattleMastery")).replace("__GROWTH__", growth)
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
            self.assertRegex(result.stdout, r"LAST_STAND_ASSERTIONS=\d+; PASS")
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
