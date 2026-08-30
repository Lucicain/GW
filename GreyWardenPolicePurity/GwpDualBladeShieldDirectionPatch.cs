using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Last remaining way to reach the formation logic that sheathes the
    /// second blade, without patching Agent.
    ///
    /// Both call sites go through this one static helper:
    ///   ArrangementOrder.OnApply -> agent.EnforceShieldUsage(GetShieldDirectionOfUnit(...))
    ///   Agent.UpdateFormationOrders -> EnforceShieldUsage(GetShieldDirectionOfUnit(...))
    /// and it returns UsageDirection.None (-1) for every arrangement except
    /// ShieldWall, Circle and Square. None is what puts the off-hand item away.
    /// ShieldWall's middle ranks instead get AttackEnd (4), which is the value
    /// for "carried, not raised in a direction" - the state we want.
    ///
    /// Patching Agent.EnforceShieldUsage itself was tried twice and broke the
    /// hero preview both times, the second time with a predicate narrowed to a
    /// single immutable character-id read, which rules out the predicate and
    /// establishes that any per-call patch on Agent reaches the previews. This
    /// targets ArrangementOrder instead - a formation class with no part in
    /// character rendering, and one patch covers both call sites.
    ///
    /// Scoped to the Twinblade Guard's character id alone: an immutable
    /// reference read, no Equipment or Mission access, and no hero or preview
    /// character can match it.
    /// </summary>
    [HarmonyPatch(
        typeof(ArrangementOrder),
        nameof(ArrangementOrder.GetShieldDirectionOfUnit))]
    internal static class GwpDualBladeShieldDirectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Agent unit, ref Agent.UsageDirection __result)
        {
            if (__result == Agent.UsageDirection.None
                && unit?.Character?.StringId == GwpIds.TwinbladeTroopId)
            {
                __result = Agent.UsageDirection.AttackEnd;
            }
        }
    }
}
