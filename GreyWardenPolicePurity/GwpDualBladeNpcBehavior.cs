using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// NPC dual-blade qualification. The slot map is ROT's and is shared with
    /// the player: off-hand blade in Weapon0, main-hand blade in Weapon1, with
    /// any ranged weapon behind them. Keeping one layout for both is what lets
    /// the previews, the damage rules and the wield path stay identical.
    /// </summary>
    internal static class GwpDualBladeNpcLoadout
    {
        internal const EquipmentIndex OffhandSlot =
            EquipmentIndex.WeaponItemBeginSlot;
        internal const EquipmentIndex MainhandSlot = EquipmentIndex.Weapon1;

        internal static bool IsNpcArcher(Agent? agent) =>
            agent != null
            && agent.IsAIControlled
            && agent.Mission != null;

        internal static bool IsNpcDualBladeAgent(Agent? agent) =>
            IsNpcArcher(agent) && HasPairEquipment(agent);

        internal static bool HasPairEquipment(Agent? agent)
        {
            if (agent == null)
                return false;

            try
            {
                Equipment? spawnEquipment = agent.SpawnEquipment;
                if (spawnEquipment != null
                    && IsItem(spawnEquipment[OffhandSlot].Item,
                        GwpIds.DualBladeOffhandItemId)
                    && IsItem(spawnEquipment[MainhandSlot].Item,
                        GwpIds.DualBladeMainhandItemId))
                {
                    return true;
                }

                MissionEquipment? equipment = agent.Equipment;
                return equipment != null
                    && IsItem(equipment[OffhandSlot], GwpIds.DualBladeOffhandItemId)
                    && IsItem(equipment[MainhandSlot], GwpIds.DualBladeMainhandItemId);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsItem(ItemObject? item, string itemId) =>
            item != null
            && string.Equals(item.StringId, itemId,
                StringComparison.OrdinalIgnoreCase);

        private static bool IsItem(in MissionWeapon weapon, string itemId) =>
            !weapon.IsEmpty && IsItem(weapon.Item, itemId);
    }

    /// <summary>
    /// A dual-blade carrier that also has a ranged weapon should open with the
    /// bow, not the blades. GetInitialWeaponIndicesToEquip keeps the first
    /// ranged weapon for the main hand under RangedForMainHand while still
    /// resolving Weapon0 as the off hand, so the slot layout does not have to
    /// be rearranged to get this - rearranging it is what left archers holding
    /// a bow together with an off-hand blade, a combination the character
    /// previews have no pose for.
    /// </summary>
    [HarmonyPatch(typeof(Agent), nameof(Agent.WieldInitialWeapons))]
    internal static class GwpDualBladeNpcInitialWeaponPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            Agent __instance,
            ref Equipment.InitialWeaponEquipPreference initialWeaponEquipPreference)
        {
            if (initialWeaponEquipPreference
                    != Equipment.InitialWeaponEquipPreference.Any
                || !GwpDualBladeNpcLoadout.IsNpcDualBladeAgent(__instance))
            {
                return;
            }

            initialWeaponEquipPreference =
                Equipment.InitialWeaponEquipPreference.RangedForMainHand;
        }
    }

    internal sealed class GwpDualBladeNpcBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        // OnAgentBuild rather than OnAgentCreated: native calls it after
        // BuildAgent, so the equipment the qualification reads is already in
        // place.
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (GwpDualBladeNpcLoadout.IsNpcDualBladeAgent(agent)
                && agent.GetComponent<GwpDualBladeNpcInputComponent>() == null)
            {
                agent.AddComponent(new GwpDualBladeNpcInputComponent(agent));
            }
        }
    }

    /// <summary>
    /// Runs at the engine's final mutable AI-input boundary, where the native
    /// AI expresses a weapon change as Wield/Sheath flags. This is the entry
    /// point the earlier tick-driven attempts were missing: rather than undoing
    /// the AI's decision after the fact, the decision itself is amended.
    ///
    /// The one thing that must not be done here is asking for both hands in the
    /// same input frame. Measured twice, from opposite directions: native
    /// WieldInitialWeapons wields off hand then main hand within one frame and
    /// ends with no off hand, and the previous version of this component
    /// injected Wield0 and the main-hand flag together 596 times without the
    /// pair ever being held. The same three calls spread across frames reach
    /// paired=True. So the off hand is requested on one frame and the main hand
    /// on the next.
    /// </summary>
    internal sealed class GwpDualBladeNpcInputComponent : AgentComponent
    {
        private const Agent.EventControlFlag OffHandWield =
            Agent.EventControlFlag.Wield0;

        private const Agent.EventControlFlag MainHandWield =
            Agent.EventControlFlag.Wield1;

        private const Agent.EventControlFlag MeleeWield =
            OffHandWield | MainHandWield;

        private const Agent.EventControlFlag RangedWield =
            Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Wield3;

        private const Agent.EventControlFlag SheathFlags =
            Agent.EventControlFlag.Sheath0 | Agent.EventControlFlag.Sheath1;

        private enum Step
        {
            Idle,
            OffHandRequested,
            MainHandRequested
        }

        // A wield takes a frame or two to land. These bound the retry so a
        // refusal can never become a per-frame loop.
        private const int MaxOffHandAttempts = 6;
        private const int CooldownFrames = 60;

        private Step _step;
        private int _attempts;
        private int _cooldown;
        private int _logged;

        internal GwpDualBladeNpcInputComponent(Agent agent)
            : base(agent)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnFormationSet()
        {
            base.OnFormationSet();
            if (!Agent.GetHasOnAiInputSetCallback())
                Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            _ = movementFlag;
            _ = inputVector;

            if (!GwpDualBladeNpcLoadout.IsNpcDualBladeAgent(Agent))
                return;

            // Anything to do with the bow stays completely native, so the AI
            // keeps full control of when it shoots and when it closes.
            if ((eventFlag & RangedWield) != Agent.EventControlFlag.None)
            {
                _step = Step.Idle;
                return;
            }

            EquipmentIndex main = Agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex off = Agent.GetOffhandWieldedItemIndex();
            bool pairHeld = main == GwpDualBladeNpcLoadout.MainhandSlot
                && off == GwpDualBladeNpcLoadout.OffhandSlot;
            Agent.EventControlFlag before = eventFlag;
            bool wantsMelee =
                (eventFlag & MeleeWield) != Agent.EventControlFlag.None;

            if (pairHeld)
            {
                _step = Step.Idle;
                _attempts = 0;

                // Only a repeated melee selection is suppressed: that is the
                // one that re-wields the main hand and takes the off hand with
                // it, which cleared the pair roughly every 1.3s. A sheath on
                // its own is the AI genuinely putting the blades away — often
                // the first half of switching back to the bow — so it has to
                // pass through, or the archer can never go ranged again.
                if (wantsMelee)
                {
                    eventFlag &= ~(MeleeWield | SheathFlags);
                    Log("NPC_DUAL_INPUT_KEEP_PAIR", before, eventFlag, main, off);
                }

                return;
            }

            switch (_step)
            {
                case Step.Idle:
                    // Enter either on the AI's own melee request, or on the
                    // recovery case where it already holds the main blade with
                    // an empty off hand.
                    if (!wantsMelee && main != GwpDualBladeNpcLoadout.MainhandSlot)
                        return;

                    if (_cooldown > 0)
                    {
                        _cooldown--;
                        return;
                    }

                    eventFlag &= ~SheathFlags;
                    eventFlag = (eventFlag & ~MeleeWield) | OffHandWield;
                    _step = Step.OffHandRequested;
                    _attempts = 0;
                    Log("NPC_DUAL_INPUT_OFFHAND", before, eventFlag, main, off);
                    return;

                case Step.OffHandRequested:
                    if (off != GwpDualBladeNpcLoadout.OffhandSlot)
                    {
                        // The off hand has not taken yet. Spending this frame
                        // on the main hand would just re-create the state we
                        // are trying to leave, so ask again.
                        if (++_attempts > MaxOffHandAttempts)
                        {
                            _step = Step.Idle;
                            _cooldown = CooldownFrames;
                            Log("NPC_DUAL_INPUT_GAVE_UP", before, eventFlag, main, off);
                            return;
                        }

                        eventFlag &= ~SheathFlags;
                        eventFlag = (eventFlag & ~MeleeWield) | OffHandWield;
                        return;
                    }

                    eventFlag &= ~SheathFlags;
                    eventFlag = (eventFlag & ~MeleeWield) | MainHandWield;
                    _step = Step.MainHandRequested;
                    Log("NPC_DUAL_INPUT_MAINHAND", before, eventFlag, main, off);
                    return;

                default:
                    // Let native settle; the result is judged on the next call
                    // through the pairHeld branch above.
                    _step = Step.Idle;
                    return;
            }
        }

        private void Log(
            string stage,
            Agent.EventControlFlag before,
            Agent.EventControlFlag after,
            EquipmentIndex main,
            EquipmentIndex off)
        {
            if (_logged >= 4)
                return;

            _logged++;
            GwpDualBladeTrace.Write(
                stage,
                Agent,
                "before=" + before + "; after=" + after
                + "; main=" + main + "; offhand=" + off);
        }
    }
}
