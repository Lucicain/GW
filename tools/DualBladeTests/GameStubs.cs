// In-memory engine boundary for exercising the real behavior/input code.
// Wield calls are observable; no game process, native engine or sound is used.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TaleWorlds.Library
{
    public struct Vec2 { }
    public struct Vec3
    {
        public float X;
        public float DistanceSquared(Vec3 other) => (X - other.X) * (X - other.X);
    }
}
namespace TaleWorlds.Core
{
    public enum EquipmentIndex { None = -1, Weapon0, WeaponItemBeginSlot = 0, Weapon1, Weapon2, Weapon3 }
    [Flags] public enum WeaponFlags : ulong { None = 0, RangedWeapon = 2, NotUsableWithOneHand = 0x10, CanBlockRanged = 0x10000000 }
    public static class FlagExtensions
    {
        public static bool HasAnyFlag(this WeaponFlags flags, WeaponFlags value) => (flags & value) != 0;
    }
    public class Banner { }
    public class WeaponComponentData { public WeaponFlags WeaponFlags; public bool IsAmmo; public bool IsShield; }
    public class ItemObject { public string StringId = ""; public WeaponComponentData? PrimaryWeapon; }
    public class Equipment
    {
        public ItemSlot this[EquipmentIndex index] => new ItemSlot();
    }
    public class ItemSlot { public ItemObject? Item; }
}
namespace TaleWorlds.MountAndBlade
{
    using TaleWorlds.Core;
    using TaleWorlds.Library;
    public enum MissionBehaviorType { Other }
    public class MissionBehavior
    {
        public virtual MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        public virtual void OnAgentBuild(Agent agent, Banner banner) { }
        public virtual void OnMissionTick(float dt) { }
    }
    public class FiringOrder
    {
        public enum RangedWeaponUsageOrderEnum { HoldYourFire, FireAtWill }
    }
    public struct MissionWeapon
    {
        public ItemObject? Item;
        public int Amount;
        public bool IsEmpty => Item == null;
        public WeaponComponentData? CurrentUsageItem => Item?.PrimaryWeapon;
    }
    public class MissionEquipment
    {
        private readonly MissionWeapon[] _slots = new MissionWeapon[4];
        public MissionWeapon this[EquipmentIndex slot] { get => _slots[(int)slot]; set => _slots[(int)slot] = value; }
    }
    public class Mission
    {
        public float CurrentTime = 10;
        public Agent? Enemy;
        public Agent? GetClosestEnemyAgent(object team, Vec3 position, float distance) => Enemy;
    }
    public class AgentComponent
    {
        public Agent Agent;
        public AgentComponent(Agent agent) { Agent = agent; }
        public virtual void Initialize() { }
        public virtual void OnFormationSet() { }
        public virtual void OnAIInputSet(ref Agent.EventControlFlag flags, ref Agent.MovementControlFlag movement, ref Vec2 input) { }
    }
    public class Agent
    {
        [Flags] public enum EventControlFlag : uint
        {
            None = 0, Wield0 = 0x10, Wield1 = 0x20, Wield2 = 0x40, Wield3 = 0x80,
            Sheath0 = 0x100, Sheath1 = 0x200, ToggleAlternativeWeapon = 0x400, Run = 0x1000, Kick = 0x8000
        }
        public enum MovementControlFlag { None }
        public enum HandIndex { MainHand, OffHand }
        public enum WeaponWieldActionType { InstantAfterPickUp, Instant, WithAnimation }
        public enum ActionCodeType
        {
            Other = 0, DefendFist = 1, DefendShield = 2, DefendForward1h = 7, DefendUp1h = 8,
            DefendRight1h = 9, DefendLeft1h = 10, DefendAllBegin = 1, DefendAllEnd = 15,
            ReadyMelee = 19, ReleaseMelee = 20, ParriedMelee = 21, BlockedMelee = 22,
            Kick = 28, WeaponBash = 31, AlternativeAttackAllBegin = 28, AlternativeAttackAllEnd = 32,
            Idle = 35, Guard = 36, StrikeKnockBack = 51
        }
        public Mission Mission = new Mission();
        public MissionEquipment Equipment = new MissionEquipment();
        public Equipment? SpawnEquipment;
        public bool IsAIControlled = true, IsHuman = true, IsUsingGameObject;
        public Agent? MountAgent, ImmediateEnemy;
        public object? Team = new object();
        public Vec3 Position;
        public int Index, Firing, StatUpdates;
        public float LastMeleeHitTime = -100;
        public EventControlFlag EventControlFlags;
        public EquipmentIndex Main = EquipmentIndex.Weapon1, Off = EquipmentIndex.Weapon0;
        public ActionCodeType Action0 = ActionCodeType.Idle, Action1 = ActionCodeType.Idle;
        public List<string> Wields = new List<string>();
        private readonly List<AgentComponent> _components = new List<AgentComponent>();
        public bool IsActive() => true;
        public void UpdateAgentStats() => StatUpdates++;
        public bool IsEnemyOf(Agent other) => other != this;
        public Agent? GetTargetAgent() => ImmediateEnemy;
        public EquipmentIndex GetPrimaryWieldedItemIndex() => Main;
        public EquipmentIndex GetOffhandWieldedItemIndex() => Off;
        public ActionCodeType GetCurrentActionType(int channel) => channel == 0 ? Action0 : Action1;
        public int GetFiringOrder() => Firing;
        public MissionWeapon WieldedOffhandWeapon => Off == EquipmentIndex.None ? default : Equipment[Off];
        public T? GetComponent<T>() where T : AgentComponent => _components.OfType<T>().FirstOrDefault();
        public void AddComponent(AgentComponent component) => _components.Add(component);
        public void SetHasOnAiInputSetCallback(bool value) { }
        public bool GetHasOnAiInputSetCallback() => true;
        public void TryToWieldWeaponInSlot(EquipmentIndex slot, WeaponWieldActionType type, bool isWieldedOnSpawn)
        {
            Wields.Add("wield:" + (int)slot);
            if (slot == EquipmentIndex.Weapon0) Off = slot;
            else { Main = slot; if (slot == EquipmentIndex.Weapon2) Off = EquipmentIndex.None; }
        }
        public void TryToSheathWeaponInHand(HandIndex hand, WeaponWieldActionType type)
        { Wields.Add("sheathe"); Main = EquipmentIndex.None; }
    }
}
namespace GreyWardenPolicePurity
{
    using TaleWorlds.MountAndBlade;
    internal static class GwpIds { internal const string DualBladeMainhandItemId = "main"; }
    internal static class GwpDualBladeLoadout { internal static bool IsOffHandBladeId(string? id) => id == "off"; }
    internal static class GwpAlternativeAttackControlBehavior { internal static void BeginAction(Agent agent) { } }
    internal static class GwpFaultTrace
    {
        internal static int Conflicts;
        internal static void Write(string stage, Agent agent, string details) { Conflicts++; }
        internal static void WriteQuiet(Exception exception, string file = "", string member = "", int line = 0) { Conflicts++; }
    }
}
