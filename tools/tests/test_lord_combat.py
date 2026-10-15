"""Compile actual combat resolvers and shield lifecycle with engine-boundary stubs."""
import re
import subprocess
import tempfile
import unittest
from pathlib import Path

from test_training_transport import ROOT, hygiene


def source(name):
    return (ROOT / "GreyWardenPolicePurity" / name).read_text(encoding="utf-8-sig")


def block(text, name):
    start = re.search(r"(?m)^    (?:internal|public).*\b(?:class|struct) " + name + r"\b", text).start()
    brace = text.index("{", start)
    return text[start:hygiene.match_brace(text, hygiene.code_mask(text), brace) + 1]


def members(text, *names):
    result = []
    for name in names:
        match = re.search(r"(?m)^        (?:internal|private|public).*\b" + name + r"\(", text)
        assert match, name
        body = re.search(r"\{|=>", text[match.end():])
        pos = match.end() + body.start()
        end = (hygiene.match_brace(text, hygiene.code_mask(text), pos) + 1
               if text[pos] == "{" else text.index(";", pos) + 1)
        result.append(text[match.start():end].replace("public override", "public"))
    return "\n".join(result)


STUBS = r'''
using System; using System.Collections.Generic; using System.Linq;
class BasicCharacterObject {public string StringId="ordinary";}
class CharacterObject:BasicCharacterObject {public Hero HeroObject;}
enum Occupation {Lord, Wanderer}
class Clan {public string StringId;}
class Hero {public string StringId="hero";public Clan Clan;public Occupation Occupation;}
class ItemObject {
 public string StringId,CollisionBodyName;public ItemTypeEnum ItemType;
 public enum ItemTypeEnum {OneHandedWeapon,TwoHandedWeapon,Polearm,Bow,Crossbow,Arrows,Bolts,Thrown,Shield,BodyArmor}
}
enum EquipmentIndex {None=-1,WeaponItemBeginSlot=0,Weapon0=0,Weapon1=1,Weapon2=2,Weapon3=3,NumAllWeaponSlots=5}
class WeaponComponentData {public bool IsShield;}
struct MissionWeapon {public ItemObject Item;public WeaponComponentData CurrentUsageItem;public int HitPoints;public bool IsEmpty=>Item==null;public bool IsShield()=>CurrentUsageItem?.IsShield==true;}
class MissionEquipment {readonly MissionWeapon[] slots=new MissionWeapon[5];public MissionWeapon this[EquipmentIndex i]{get=>slots[(int)i];set=>slots[(int)i]=value;}}
struct WeakGameEntity {public bool IsValid;public static WeakGameEntity Invalid=>default;}
class Monster {public string StringId;}
struct Vec3 {}
enum DamageTypes {Cut}
enum CombatCollisionResult {StrikeAgent,Blocked}
struct AttackCollisionData {public int AffectorWeaponSlotOrMissileIndex,InflictedDamage,DamageType;public bool IsMissile,IsColliderAgent,IsAlternativeAttack,IsHorseCharge,IsFallDamage,AttackBlockedWithShield,CollidedWithShieldOnBack,MissileBlockedWithWeapon,IsShieldBroken;public CombatCollisionResult CollisionResult;public Vec3 WeaponBlowDir;}
struct Blow {public int OwnerId;}
struct AttackInformation {public Agent AttackerAgent,VictimAgent;public MissionWeapon AttackerWeapon,VictimShield;}
class Agent {
 public BasicCharacterObject Character;public bool IsMount,IsHuman=true,Active=true;public int Team,Index;public float Health=100;
 public Monster Monster;public Agent MountAgent,RiderAgent;public bool IsDoingPassiveAttack;
 public Mission Mission;public MissionEquipment Equipment=new MissionEquipment();public EquipmentIndex Offhand=EquipmentIndex.None;
 public MissionWeapon WieldedWeapon,WieldedOffhandWeapon;public bool IsActive()=>Active;public bool IsEnemyOf(Agent other)=>Team!=other.Team;
 public EquipmentIndex GetOffhandWieldedItemIndex()=>Offhand;public WeakGameEntity ShieldEntity=new WeakGameEntity{IsValid=true};
 public WeakGameEntity GetWeaponEntityFromEquipmentSlot(EquipmentIndex i)=>ShieldEntity;
 public void ChangeWeaponHitPoints(EquipmentIndex i,short hp){var w=Equipment[i];w.HitPoints=hp;Equipment[i]=w;}
 public void RemoveEquippedWeapon(EquipmentIndex i){Equipment[i]=default;}
}
class Mission {public static Mission Current;public List<Missile> MissilesList=new List<Missile>();public Dictionary<int,Agent> Agents=new Dictionary<int,Agent>();public Agent FindAgentWithIndex(int i)=>Agents[i];public class Missile {public int Index;public MissionWeapon Weapon;}}
static class MBRandom {public static float RandomFloat;}
static class MathF {public static int Max(int a,int b)=>Math.Max(a,b);public static int Round(float f)=>(int)Math.Round(f);}
static class GwpIds {public const string CustomBattleCommanderId="gwp_custom_commander",PoliceClanId="police",DualBladeMainhandItemId="gwdualblademainhand",DualBladeOffhandItemId="gwdualbladeoffhand",LargeShieldItemId="wlarge_shield",BlackLargeShieldItemId="wlarge_shield_black";__SHIELD_IDS__}
class NativeDamageModel {public float CalculateShieldDamage(in AttackInformation info,float amount)=>amount;}
class GwpAgentApplyDamageModel {readonly NativeDamageModel NativeModel=new NativeDamageModel();__DAMAGE__}
static class GwpCommon {__IDENTITY__}
class GwpWardenMarriageBehavior {__MARRIAGE__}
__TRAITS__
static class GwpTroopCombat {__COMBAT_FIELDS__ __COMBAT__}
static class GwpPassiveHeldShieldCollision {
 private readonly struct PendingShieldBreak {internal readonly Agent Agent;internal readonly EquipmentIndex Slot;internal PendingShieldBreak(Agent a,EquipmentIndex s){Agent=a;Slot=s;}}
 private static readonly List<PendingShieldBreak> PendingShieldBreaks=new List<PendingShieldBreak>();
 private static void PlayShieldBreakFeedback(Agent a,EquipmentIndex s,in AttackCollisionData c){}
 __SHIELDS__
 public static string Shape(Agent a)=>TryGetHeldGiantShield(a,out _,out string shape)?shape:null;
}
class Regression {
 static int checks;static void Check(bool c,string why){checks++;if(!c)throw new Exception(why);}
 static Agent Person(string clan="police",Occupation occupation=Occupation.Lord,string id="joined_later")=>new Agent{Character=new CharacterObject{StringId=id,HeroObject=new Hero{Clan=new Clan{StringId=clan},Occupation=occupation}}};
 static ItemObject Item(ItemObject.ItemTypeEnum type,string id="native")=>new ItemObject{StringId=id,ItemType=type,CollisionBodyName="bo_"+id};
 static MissionWeapon Weapon(ItemObject item,int hp=100)=>new MissionWeapon{Item=item,HitPoints=hp,CurrentUsageItem=new WeaponComponentData{IsShield=item.ItemType==ItemObject.ItemTypeEnum.Shield}};
 static Agent ShieldUser(Agent a,ItemObject shield,bool held=true,int hp=100){a.Equipment[EquipmentIndex.Weapon0]=Weapon(shield,hp);a.Offhand=held?EquipmentIndex.Weapon0:EquipmentIndex.None;a.WieldedOffhandWeapon=held?a.Equipment[EquipmentIndex.Weapon0]:default;return a;}
 public static void Main(){
  var lord=Person();var foreign=Person("foreign");var soldier=new Agent{Character=new BasicCharacterObject{StringId="gwheavyinfantry"}};
  Check(GwpCommon.IsGreyWardenLordAgent(lord),"new member need not use a founder ID");
  foreach(var a in new[]{foreign,soldier,Person("police",Occupation.Wanderer),new Agent(),null})Check(!GwpCommon.IsGreyWardenLordAgent(a),"only a real current clan lord qualifies");
  ((CharacterObject)lord.Character).HeroObject.Clan=foreign.Character is CharacterObject fc?fc.HeroObject.Clan:null;
  Check(!GwpCommon.IsGreyWardenLordAgent(lord),"departed lord loses effects");lord=Person();
  Check(GwpCommon.IsGreyWardenLordAgent(new Agent{Character=new BasicCharacterObject{StringId="gwp_custom_commander"}}),"custom battle commander has no hero but is the Grey Warden lord");
  Check(!GwpCommon.IsGreyWardenLordAgent(new Agent{Character=new BasicCharacterObject{StringId="commander_2"}}),"other custom battle commanders stay native");
  Check(!GwpCommon.IsGreyWardenLordAgent(new Agent{Character=new CharacterObject{StringId="gwp_custom_commander"}}),"campaign characters are judged by their hero, never by the custom ID");
  var marriage=new GwpWardenMarriageBehavior();var bride=Person(id:"bride");var groom=Person("player_clan");var bystander=Person();var outsiderBride=Person("foreign",id:"foreign_bride");
  ((CharacterObject)bride.Character).HeroObject.StringId="bride";((CharacterObject)groom.Character).HeroObject.StringId="player";((CharacterObject)bystander.Character).HeroObject.StringId="bystander";((CharacterObject)outsiderBride.Character).HeroObject.StringId="foreign_bride";
  marriage.OnBeforeHeroesMarried(((CharacterObject)groom.Character).HeroObject,((CharacterObject)bride.Character).HeroObject,true);
  marriage.OnBeforeHeroesMarried(((CharacterObject)groom.Character).HeroObject,((CharacterObject)outsiderBride.Character).HeroObject,true);
  ((CharacterObject)bride.Character).HeroObject.Clan=new Clan{StringId="player_clan"};((CharacterObject)bystander.Character).HeroObject.Clan=new Clan{StringId="player_clan"};
  Check(GwpCommon.IsGreyWardenLordAgent(bride),"a Grey Warden lord who married out keeps her identity effects");
  Check(!GwpCommon.IsGreyWardenLordAgent(groom)&&!GwpCommon.IsGreyWardenLordAgent(outsiderBride),"the spouse and non-Warden brides gain nothing from the marriage");
  Check(!GwpCommon.IsGreyWardenLordAgent(bystander),"leaving the clan without marrying as a Warden lord still removes the effects");
  foreach(var type in new[]{ItemObject.ItemTypeEnum.OneHandedWeapon,ItemObject.ItemTypeEnum.TwoHandedWeapon,ItemObject.ItemTypeEnum.Polearm}){
   var item=Item(type);var t=GwpWeaponTraits.OfAttack(lord,item);
   Check(t.Knockdown==.125f&&t.Dismount==.125f&&t.ParryBreak==.25f,"ordinary melee inherits existing values");
   Check(t.PassiveKnockdown==.5f&&t.PassiveDismount==.25f&&t.PassiveAlwaysBreaksParry,"actual passive usage inherits lance values");
   foreach(var a in new[]{foreign,soldier})Check(GwpWeaponTraits.OfAttack(a,item)==null,"no item-independent soldier/foreigner melee bonus");
  }
  foreach(var type in new[]{ItemObject.ItemTypeEnum.Bow,ItemObject.ItemTypeEnum.Crossbow,ItemObject.ItemTypeEnum.Arrows,ItemObject.ItemTypeEnum.Bolts,ItemObject.ItemTypeEnum.Thrown}){
   var item=Item(type);var t=GwpWeaponTraits.OfAttack(lord,item);
   Check(t.Knockdown==.25f&&t.Dismount==0&&t.ShieldDamageMultiplier==2,"ordinary missile or launcher inherits arrow values");
   Check(GwpWeaponTraits.OfAttack(soldier,item)==null,"ordinary soldiers need special ammo");
  }
  Check(GwpWeaponTraits.OfAttack(lord,null)==null,"no phantom unarmed weapon bonus");
  Check(GwpWeaponTraits.OfAttack(lord,Item(ItemObject.ItemTypeEnum.BodyArmor))==null,"armor is not an attack");
  foreach(var id in new[]{"gwarrows",GwpIds.DualBladeMainhandItemId,GwpIds.DualBladeOffhandItemId}){
   var item=Item(id=="gwarrows"?ItemObject.ItemTypeEnum.Arrows:ItemObject.ItemTypeEnum.OneHandedWeapon,id);
   Check(ReferenceEquals(GwpWeaponTraits.OfAttack(lord,item),GwpWeaponTraits.Of(item)),"special effects resolved once, pair keeps its value");
   Check(GwpWeaponTraits.OfAttack(foreign,item)!=null,"existing weapon effects still work for outsiders");
  }
  var mission=new Mission();Mission.Current=mission;lord.Mission=mission;lord.Index=7;mission.Agents[7]=lord;
  lord.Equipment[EquipmentIndex.Weapon1]=Weapon(Item(ItemObject.ItemTypeEnum.OneHandedWeapon));
  var hit=new AttackCollisionData{AffectorWeaponSlotOrMissileIndex=1,InflictedDamage=10,IsColliderAgent=true};
  Check(GwpWeaponTraits.OfHit(lord,in hit).ParryBreak==.25f,"actual melee slot resolves identity");
  mission.MissilesList.Add(new Mission.Missile{Index=123,Weapon=Weapon(Item(ItemObject.ItemTypeEnum.Arrows))});hit.IsMissile=true;hit.AffectorWeaponSlotOrMissileIndex=123;
  Check(GwpWeaponTraits.OfHit(lord,in hit).Knockdown==.25f,"actual missile index resolves ordinary ammunition");
  hit.AffectorWeaponSlotOrMissileIndex=124;Check(GwpWeaponTraits.OfHit(lord,in hit)==null,"missing missile is not fabricated");hit.AffectorWeaponSlotOrMissileIndex=123;
  var victim=Person("foreign");victim.Team=1;MBRandom.RandomFloat=.249f;
  Check(GwpTroopCombat.RollBodyHit(victim,in hit,new Blow{OwnerId=7})&&GwpTroopCombat.TakeKnockdown(victim),"ordinary arrows trigger knockdown through actual body-hit pipeline");
  Check(!GwpTroopCombat.TakeKnockdown(victim),"reaction consumed exactly once");
  MBRandom.RandomFloat=.25f;Check(!GwpTroopCombat.RollBodyHit(victim,in hit,new Blow{OwnerId=7}),"arrow chance boundary");
  victim.MountAgent=new Agent();victim.MountAgent.RiderAgent=victim;MBRandom.RandomFloat=0;
  Check(!GwpTroopCombat.RollBodyHit(victim,in hit,new Blow{OwnerId=7}),"arrows do not inherit melee dismount");victim.MountAgent=null;
  hit.IsMissile=false;hit.AffectorWeaponSlotOrMissileIndex=1;MBRandom.RandomFloat=.124f;
  Check(GwpTroopCombat.RollBodyHit(victim,in hit,new Blow{OwnerId=7}),"ordinary melee triggers body control");
  hit.IsAlternativeAttack=true;Check(!GwpTroopCombat.RollBodyHit(victim,in hit,new Blow{OwnerId=7}),"kick stays on its own control path");hit.IsAlternativeAttack=false;
  lord.WieldedWeapon=lord.Equipment[EquipmentIndex.Weapon1];MBRandom.RandomFloat=.249f;
  Check(GwpTroopCombat.WeaponCrushesBlock(lord,victim,new WeaponComponentData(),false),"ordinary lord sword breaks weapon parry");
  Check(!GwpTroopCombat.WeaponCrushesBlock(lord,victim,new WeaponComponentData{IsShield=true},false),"ordinary sword never inherits shield bash crush");
  Check(GwpTroopCombat.WeaponCrushesBlock(lord,victim,new WeaponComponentData(),true),"passive attack weapon parry break");
  var shield=Item(ItemObject.ItemTypeEnum.Shield,"native_round");var grayShield=Item(ItemObject.ItemTypeEnum.Shield,GwpIds.LargeShieldItemId);
  Check(GwpWeaponTraits.HasShieldEffects(lord,shield),"ordinary lord shield eligible");
  Check(!GwpWeaponTraits.HasShieldEffects(soldier,shield)&&!GwpWeaponTraits.HasShieldEffects(foreign,shield),"ordinary shields of others untouched");
  Check(GwpWeaponTraits.HasShieldEffects(foreign,grayShield),"special shield still benefits outsiders");
  var model=new GwpAgentApplyDamageModel();var info=new AttackInformation{VictimAgent=lord,VictimShield=Weapon(shield),AttackerAgent=foreign,AttackerWeapon=Weapon(Item(ItemObject.ItemTypeEnum.Arrows))};
  Check(model.CalculateShieldDamage(in info,100)==50,"ordinary lord shield half damage");info.AttackerAgent=lord;
  Check(model.CalculateShieldDamage(in info,100)==100,"ordinary lord arrows times lord shield cancels once");
  info.AttackerWeapon=Weapon(Item(ItemObject.ItemTypeEnum.Arrows,"gwarrows"));info.VictimShield=Weapon(grayShield);
  Check(model.CalculateShieldDamage(in info,100)==100,"named equipment never quadruples or quarters");info.VictimAgent=foreign;info.VictimShield=Weapon(shield);
  Check(model.CalculateShieldDamage(in info,100)==200,"attacking lord doubles ordinary outsider shield damage");
  ShieldUser(lord,shield);Check(GwpPassiveHeldShieldCollision.Shape(lord)==shield.CollisionBodyName,"ordinary shield retains its actual shape");
  ShieldUser(foreign,shield);Check(GwpPassiveHeldShieldCollision.Shape(foreign)==null,"foreign ordinary held shield excluded");
  var durabilityHit=new AttackCollisionData{InflictedDamage=20};
  GwpPassiveHeldShieldCollision.ApplyPassiveShieldDurabilityDamage(lord,ref durabilityHit,true);
  Check(lord.Equipment[EquipmentIndex.Weapon0].HitPoints==80,"held shield damage already reduced by model, not halved again");
  ShieldUser(lord,shield,false);Check(GwpPassiveHeldShieldCollision.HasGiantShieldCarriedOnBack(lord),"ordinary lord back shield protected");
  GwpPassiveHeldShieldCollision.ApplyPassiveShieldDurabilityDamage(lord,ref durabilityHit,false);
  Check(lord.Equipment[EquipmentIndex.Weapon0].HitPoints==90,"back shield damage halved in durability path");
  ShieldUser(lord,shield,true,5);Check(GwpPassiveHeldShieldCollision.ApplyPassiveShieldDurabilityDamage(lord,ref durabilityHit,true),"ordinary shield break queued");
  Check(lord.Equipment[EquipmentIndex.Weapon0].HitPoints==1,"native hit callback keeps shield alive");
  GwpPassiveHeldShieldCollision.ProcessQueuedShieldBreaks();Check(lord.Equipment[EquipmentIndex.Weapon0].IsEmpty,"ordinary shield removed next tick");
  ShieldUser(lord,grayShield,true,5);GwpPassiveHeldShieldCollision.ApplyPassiveShieldDurabilityDamage(lord,ref durabilityHit,true);GwpPassiveHeldShieldCollision.ProcessQueuedShieldBreaks();
  Check(lord.Equipment[EquipmentIndex.Weapon0].IsEmpty,"named shield break unchanged");
  var horse=new Agent{IsMount=true,IsHuman=false,Monster=new Monster{StringId="horse"},RiderAgent=lord};lord.MountAgent=horse;
  Check(GwpTroopCombat.HasWarhorseEffects(horse)&&GwpTroopCombat.IsWarhorseOrRider(lord),"ordinary current lord mount qualifies");
  var charge=new AttackCollisionData{IsHorseCharge=true};Check(GwpTroopCombat.IsWarhorseCharge(horse,victim,in charge),"ordinary lord horse charge benefits");
  horse.RiderAgent=foreign;Check(!GwpTroopCombat.HasWarhorseEffects(horse),"dismount or rider replacement removes horse identity bonus");horse.RiderAgent=lord;
  info=new AttackInformation{VictimAgent=lord,AttackerAgent=foreign};hit.CollisionResult=CombatCollisionResult.StrikeAgent;
  Check(GwpTroopCombat.ApplyWarhorseDamageRules(in info,in hit,40)==20,"ordinary lord mount shares half body damage");
  hit.AttackBlockedWithShield=true;Check(GwpTroopCombat.ApplyWarhorseDamageRules(in info,in hit,40)==40,"blocked hit never transferred");hit.AttackBlockedWithShield=false;
  horse.Health=0;Check(GwpTroopCombat.ApplyWarhorseDamageRules(in info,in hit,40)==40,"dead mount cannot absorb damage");horse.Health=100;
  MBRandom.RandomFloat=.75f;Check(!GwpTroopCombat.WarhorseRears(horse),"ordinary lord mount suppresses quarter of native rears");
  horse.RiderAgent=foreign;Check(GwpTroopCombat.WarhorseRears(horse),"ordinary foreign mount retains native rear");
  horse.Monster.StringId="gw_warhorse";Check(GwpTroopCombat.HasWarhorseEffects(horse),"authored warhorse benefits every rider as before");
  lord.MountAgent=null;Check(!GwpTroopCombat.IsWarhorseOrRider(lord),"unmounted lord receives no horse damage sharing");
  Console.WriteLine("LORD_COMBAT_ASSERTIONS="+checks+"; PASS");
 }
}
'''


class LordCombatTests(unittest.TestCase):
    def test_identity_hits_shield_damage_break_and_mount_lifecycle(self):
        combat = source("GwpTroopCombat.cs")
        fields = combat[combat.index("{", combat.index("internal static class GwpTroopCombat")) + 1:
                        combat.index("        internal static void Reset()")]
        replacements = {
            "__SHIELD_IDS__": members(source("GwpIds.cs"), "IsGreyWardenLargeShieldItemId"),
            "__IDENTITY__": members(source("GwpCommon.cs"), "IsGreyWardenClan", "IsGreyWardenClanMember", "IsGreyWardenLord", "IsGreyWardenLordAgent"),
            "__MARRIAGE__": "\n".join(re.findall(r"(?m)^        private (?:static GwpWardenMarriageBehavior\? _instance|List<string> _marriedWardenIds)[^;]*;", source("GwpWardenMarriageBehavior.cs")))
                + members(source("GwpWardenMarriageBehavior.cs"), "GwpWardenMarriageBehavior", "IsMarriedWarden", "OnBeforeHeroesMarried").replace("private void", "public void"),
            "__TRAITS__": block(combat, "GwpWeaponTrait") + block(combat, "GwpWeaponTraits"),
            "__COMBAT_FIELDS__": fields,
            "__COMBAT__": members(combat, "Reset", "RollBodyHit", "TakeKnockdown", "PendingKnockdown", "HasWarhorseEffects", "IsWarhorseOrRider", "ApplyWarhorseDamageRules", "IsWarhorseCharge", "WarhorseRears", "WeaponCrushesBlock"),
            "__DAMAGE__": members(source("GwpAgentApplyDamageModel.cs"), "CalculateShieldDamage"),
            "__SHIELDS__": members(source("GwpShieldBashGuardPatch.cs"), "TryGetHeldGiantShield", "HasGiantShieldCarriedOnBack", "TryGetPassiveShieldSlot", "ApplyPassiveShieldDurabilityDamage", "ProcessQueuedShieldBreaks", "QueueShieldBreak"),
        }
        program = STUBS
        for key, value in replacements.items():
            program = program.replace(key, value)
        major = subprocess.check_output(["dotnet", "--version"], text=True).split(".")[0]
        with tempfile.TemporaryDirectory(prefix="gwp-lord-combat-") as tmp:
            folder = Path(tmp)
            (folder / "test.csproj").write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>disable</Nullable><NoWarn>8632;0649;0414;0169;1573</NoWarn></PropertyGroup></Project>', encoding="utf-8")
            (folder / "Program.cs").write_text(program, encoding="utf-8")
            result = subprocess.run(["dotnet", "run", "--project", str(folder), "-c", "Release"], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertRegex(result.stdout, r"LORD_COMBAT_ASSERTIONS=\d+; PASS")
            print(result.stdout.strip())


if __name__ == "__main__":
    unittest.main()
