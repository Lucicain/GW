using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// The fourth of the four scripts the v1.4.8 build used, and the only one
    /// that has never yet coexisted with a qualification that actually applied.
    ///
    /// The record kept it for exactly this reason: "保留范围精确到完整双刀装备的
    /// Agent.EnforceShieldUsage() 旁路，避免原版盾墙整理再次把非标准副手直接剔除" -
    /// native's shield-wall tidying strips a non-standard off-hand item. The
    /// other three supplied the off-hand's native qualification, which is now
    /// written once onto the NPC item instead of through per-call MissionWeapon
    /// patches; this is what stops the formation logic taking it away again.
    ///
    /// It is deliberately the narrowest of the four to reinstate. It is scoped
    /// to an AI mission agent carrying the complete pair, and it patches an
    /// Agent instance method, so no tableau or preview can reach it - tableaus
    /// build through AgentVisuals and have no Agent at all. That matters
    /// because the isolation run proved the recurring model damage came from
    /// per-call patches on the MissionWeapon/Agent equipment chain, and this
    /// touches neither weapon data nor equipment.
    ///
    /// Skipping it costs these agents nothing: they carry no shield, so there
    /// is no shield usage to enforce.
    /// </summary>
    [HarmonyPatch(typeof(Agent), nameof(Agent.EnforceShieldUsage))]
    internal static class GwpDualBladeShieldEnforcementPatch
    {
        private static bool _loggedOnce;

        [HarmonyPrefix]
        private static bool Prefix(Agent __instance)
        {
            if (!GwpDualBladeLoadout.IsDualBladeNpc(__instance))
                return true;

            if (!_loggedOnce)
            {
                _loggedOnce = true;
                GwpDualBladeTrace.Write(
                    "NPC_SHIELD_ENFORCEMENT_BYPASSED",
                    __instance,
                    "main=" + __instance.GetPrimaryWieldedItemIndex()
                    + "; offhand=" + __instance.GetOffhandWieldedItemIndex());
            }

            return false;
        }
    }
}
