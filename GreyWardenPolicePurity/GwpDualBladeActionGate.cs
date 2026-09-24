using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // Native's WeaponBash collision uses the current weapon slot and off hand.
    // A kick request may outlive the tick that supplied it, so action-type
    // checks alone cannot protect an instant sheath/wield sequence.
    internal static class GwpDualBladeActionGate
    {
        internal const Agent.EventControlFlag WeaponChanges =
            Agent.EventControlFlag.Wield0 | Agent.EventControlFlag.Wield1
            | Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Wield3
            | Agent.EventControlFlag.Sheath0 | Agent.EventControlFlag.Sheath1
            | Agent.EventControlFlag.ToggleAlternativeWeapon;

        internal static bool CanChangeWeapons(Agent agent) =>
            !GwpKickInputComponent.IsPerformingAlternativeAttack(agent)
            && (agent.EventControlFlags & Agent.EventControlFlag.Kick) == 0
            && !(agent.GetComponent<GwpKickInputComponent>()?.HasPendingKick(
                agent.Mission.CurrentTime) ?? false);

        internal static bool FilterArcherInput(Agent agent, ref Agent.EventControlFlag flags)
        {
            GwpDualBladeAgentState? state = GwpDualBladeAgents.Find(agent);
            if (state == null || !state.HasRangedAlternative)
                return true;

            bool alternative = GwpKickInputComponent.IsPerformingAlternativeAttack(agent);
            bool weaponChange = (flags & WeaponChanges) != 0;
            bool paired = agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.Weapon1
                && agent.GetOffhandWieldedItemIndex() == EquipmentIndex.Weapon0;
            bool allowKick = !alternative && !weaponChange && paired
                && state.OpeningWieldDone && !state.SequenceRunning
                && state.StableHandTicks > 0;

            // An action already in progress keeps its weapons through contact.
            // A new simultaneous request yields to the weapon change instead.
            if (alternative)
                flags &= ~WeaponChanges;
            if (!allowKick)
                flags &= ~Agent.EventControlFlag.Kick;

#if GWP_DIAGNOSTICS
            if (alternative && !paired && !state.ReportedInvalidAction)
            {
                state.ReportedInvalidAction = true;
                GwpFaultTrace.Write("DUAL_BLADE_INVALID_ACTION", agent,
                    "alternative=True paired=" + paired + " weaponChange=" + weaponChange
                    + " step=" + state.CurrentStep
                    + " main=" + agent.GetPrimaryWieldedItemIndex()
                    + " off=" + agent.GetOffhandWieldedItemIndex()
                    + " action0=" + agent.GetCurrentActionType(0)
                    + " action1=" + agent.GetCurrentActionType(1));
            }
#endif
            return allowKick;
        }
    }
}
