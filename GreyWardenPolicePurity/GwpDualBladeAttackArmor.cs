using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Reaction immunity while attacking: the archers' dual-blade pair readied or striking,
    /// and (2026-09-24) Grey Warden knights striking or couching with the lance or greatsword.
    /// </summary>
    internal static class GwpDualBladeAttackArmor
    {
        // Existing mod-created control contacts bypass the model callbacks.
        // Preserve their damage while applying the same reaction policy.
        internal static void ApplyToControlContact(Agent victim, ref Blow blow)
        {
            if (!IsActive(victim))
                return;
            blow.BlowFlag &= ~(BlowFlags.KnockBack | BlowFlags.KnockDown | BlowFlags.CanDismount);
            blow.BlowFlag |= BlowFlags.ShrugOff;
            blow.DefenderStunPeriod = 0f;
        }

        internal static bool IsActive(Agent? agent)
        {
            if (agent == null || agent.Mission == null || !agent.IsActive() || !agent.IsHuman)
                return false;
            if (GwpTroopCombat.IsKnightAttacking(agent))
                return true;
            if (!GwpDualBladeLoadout.IsDualBladeCombatant(agent))
                return false;

            if (agent.WieldedWeapon.Item?.StringId != GwpIds.DualBladeMainhandItemId
                || !GwpDualBladeLoadout.IsOffHandBladeId(agent.WieldedOffhandWeapon.Item?.StringId))
                return false;

            return IsAttack(agent.GetCurrentActionType(0))
                || IsAttack(agent.GetCurrentActionType(1));
        }

        private static bool IsAttack(Agent.ActionCodeType action) =>
            action == Agent.ActionCodeType.ReadyMelee
            || action == Agent.ActionCodeType.ReleaseMelee;
    }
}
