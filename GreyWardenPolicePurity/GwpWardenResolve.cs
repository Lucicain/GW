using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // Per-agent protection: a Grey Warden never panics or flees, while his morale
    // rises and falls as native's does (2026-09-25: no longer held at 100). Mixed
    // formations keep their ordinary troops' morale and orders. No formation
    // reassignment inside native spawn/retreat callbacks.
    internal static class GwpWardenResolve
    {
        internal static bool Applies(Agent? agent)
        {
            Mission? mission = agent?.Mission;
            return agent != null && agent.IsHuman && agent.IsAIControlled
                && agent.Team != null && agent.Team.Side != BattleSideEnum.None
                && mission != null && mission.Mode == MissionMode.Battle
                && !mission.MissionEnded && mission.MissionResult == null
                && !GameNetwork.IsMultiplayer
                && GwpCommon.IsGreyWardenAffiliatedCharacter(agent.Character);
        }
    }

    [HarmonyPatch(typeof(CommonAIComponent), nameof(CommonAIComponent.Panic))]
    internal static class GwpWardenPanicPatch
    {
        [HarmonyPrefix]
        internal static bool Before(Agent ___Agent)
        {
            // Low morale leads here; blocking it keeps the Warden fighting whatever his morale.
            return !GwpWardenResolve.Applies(___Agent);
        }
    }

    [HarmonyPatch(typeof(CommonAIComponent), nameof(CommonAIComponent.Retreat))]
    internal static class GwpWardenRetreatPatch
    {
        [HarmonyPrefix]
        internal static bool Before(Agent ___Agent)
        {
            if (!GwpWardenResolve.Applies(___Agent)) return true;
            // OnUnitJoinOrLeave immediately calls Retreat for new arrivals in a
            // withdrawing formation, without a morale check. Block it before
            // IsRetreating or the native escape destination can be set.
            ___Agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
            return false;
        }
    }

    [HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.RefreshBehaviorValues))]
    internal static class GwpWardenRetreatBehaviorPatch
    {
        [HarmonyPrefix]
        internal static void Before(Agent ___Agent, ref MovementOrder.MovementOrderEnum movementOrder)
        {
            // Native OnApply refreshes behavior after RetreatAux. Preserve the
            // Warden's combat behavior without changing any other formation unit.
            if (movementOrder == MovementOrder.MovementOrderEnum.Retreat && GwpWardenResolve.Applies(___Agent))
                movementOrder = MovementOrder.MovementOrderEnum.Charge;
        }
    }
}
