using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Registry of AI agents that dual wield. Qualification is decided once,
    /// when the agent is built, and read back later by a simple lookup - the
    /// formation patch must not go poking at an agent's equipment while the
    /// game is arranging troops.
    ///
    /// Membership is by equipment, not by character id: a Twinblade Guard and
    /// an AI-controlled Grey Warden commander both carry the pair and both
    /// need the same handling.
    /// </summary>
    internal static class GwpDualBladeAgents
    {
        private static readonly ConditionalWeakTable<Agent, object> Registered =
            new ConditionalWeakTable<Agent, object>();

        internal static bool IsRegistered(Agent? agent) =>
            agent != null && Registered.TryGetValue(agent, out _);

        internal static bool TryRegister(Agent? agent)
        {
            if (agent == null
                || !agent.IsAIControlled
                || Registered.TryGetValue(agent, out _)
                || !CarriesPair(agent))
            {
                return false;
            }

            Registered.Add(agent, new object());
            return true;
        }

        private static bool CarriesPair(Agent agent)
        {
            try
            {
                Equipment? spawnEquipment = agent.SpawnEquipment;
                if (spawnEquipment != null
                    && GwpDualBladeLoadout.IsOffHandBladeId(
                        spawnEquipment[EquipmentIndex.WeaponItemBeginSlot].Item?.StringId)
                    && IsMainBlade(spawnEquipment[EquipmentIndex.Weapon1].Item?.StringId))
                {
                    return true;
                }

                MissionEquipment? equipment = agent.Equipment;
                return equipment != null
                    && GwpDualBladeLoadout.IsOffHandBladeId(
                        equipment[EquipmentIndex.WeaponItemBeginSlot].Item?.StringId)
                    && IsMainBlade(equipment[EquipmentIndex.Weapon1].Item?.StringId);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMainBlade(string? itemId) =>
            string.Equals(itemId, GwpIds.DualBladeMainhandItemId,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Keeps an AI dual wielder's pair in hand once the battle starts.
    ///
    /// Deployment settled how this works: native's own spawn wield already
    /// pairs Weapon0 and Weapon1 correctly, and the pair survives for as long
    /// as no AI weapon selection runs. Establishment therefore needs no code;
    /// only retention does, and it has two disruptors - formation shield
    /// tidying, handled by GwpDualBladeShieldDirectionPatch, and the native AI's
    /// own weapon selection, handled here.
    ///
    /// That selection's one managed surface is AgentComponent.OnAIInputSet,
    /// which is a component rather than a Harmony patch and so stays clear of
    /// the Agent and MissionWeapon types whose per-call patches break character
    /// previews.
    /// </summary>
    internal sealed class GwpDualBladeGuardBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (GwpDualBladeAgents.TryRegister(agent)
                && agent.GetComponent<GwpDualBladeGuardInputComponent>() == null)
            {
                agent.AddComponent(new GwpDualBladeGuardInputComponent(agent));
            }
        }
    }

    internal sealed class GwpDualBladeGuardInputComponent : AgentComponent
    {
        private const Agent.EventControlFlag WeaponChange =
            Agent.EventControlFlag.Wield0
            | Agent.EventControlFlag.Wield1
            | Agent.EventControlFlag.Wield2
            | Agent.EventControlFlag.Wield3
            | Agent.EventControlFlag.Sheath0
            | Agent.EventControlFlag.Sheath1
            | Agent.EventControlFlag.ToggleAlternativeWeapon;

        private bool _logged;

        internal GwpDualBladeGuardInputComponent(Agent agent)
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

            if ((eventFlag & WeaponChange) == Agent.EventControlFlag.None)
                return;

            // These characters carry nothing but the pair, so there is no
            // weapon choice worth making. Movement, attacks, blocks and every
            // other decision stay entirely native.
            Agent.EventControlFlag before = eventFlag;
            eventFlag &= ~WeaponChange;

            if (!_logged)
            {
                _logged = true;
                GwpDualBladeTrace.Write(
                    "GUARD_WEAPON_SELECTION_SUPPRESSED",
                    Agent,
                    "before=" + before
                    + "; main=" + Agent.GetPrimaryWieldedItemIndex()
                    + "; offhand=" + Agent.GetOffhandWieldedItemIndex());
            }
        }
    }
}
