using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
#if GWP_DIAGNOSTICS
using System.Runtime.CompilerServices;
#endif

namespace GreyWardenPolicePurity
{
    // Per-agent protection: mixed formations keep their ordinary troops' morale
    // and orders. No formation reassignment inside native spawn/retreat callbacks.
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

#if GWP_DIAGNOSTICS
        private sealed class Counts { internal int Panic, Retreat; }
        private static readonly ConditionalWeakTable<Mission, Counts> Traces = new();
        internal static void Trace(Agent agent, bool panic)
        {
            Counts counts = Traces.GetValue(agent.Mission, _ => new Counts());
            if (panic ? counts.Panic++ >= 8 : counts.Retreat++ >= 8) return;
            GwpFaultTrace.Write("WARDEN_RESOLVE_BLOCK", agent,
                $"path={(panic ? "panic" : "retreat")} side={agent.Team.Side} morale={agent.GetMorale():F2} "
                + $"order={agent.Formation?.GetReadonlyMovementOrderReference().OrderEnum.ToString() ?? "none"} "
                + $"support={agent.Origin is GwpBattleSupportOrigin}");
        }
#endif
    }

    [HarmonyPatch(typeof(CommonAIComponent), nameof(CommonAIComponent.Morale), MethodType.Setter)]
    internal static class GwpWardenMoralePatch
    {
        [HarmonyPrefix]
        internal static void Before(Agent ___Agent, ref float value)
        {
            // Covers InitializeMorale and all later native morale shocks alike.
            if (GwpWardenResolve.Applies(___Agent)) value = 100f;
        }
    }

    [HarmonyPatch(typeof(CommonAIComponent), nameof(CommonAIComponent.Panic))]
    internal static class GwpWardenPanicPatch
    {
        [HarmonyPrefix]
        internal static bool Before(Agent ___Agent)
        {
            if (!GwpWardenResolve.Applies(___Agent)) return true;
#if GWP_DIAGNOSTICS
            GwpWardenResolve.Trace(___Agent, true);
#endif
            return false;
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
#if GWP_DIAGNOSTICS
            GwpWardenResolve.Trace(___Agent, false);
#endif
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
