using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// The fourth of the four scripts the v1.4.8 build used. The formation code
    /// makes its role explicit: ArrangementOrder.OnApply runs
    ///
    ///     if (agent.IsAIControlled)
    ///         agent.EnforceShieldUsage(GetShieldDirectionOfUnit(...));
    ///
    /// and GetShieldDirectionOfUnit returns UsageDirection.None for every
    /// arrangement except ShieldWall, Circle and Square. None means "put the
    /// off-hand item away", so an ordinary line formation sheathes the second
    /// blade. That is why the archers were seen holding both blades at spawn
    /// and losing them once orders were applied, and why the guard - which is
    /// arranged immediately - never appears to draw it at all.
    ///
    /// The previous attempt at this bypass qualified agents by "AI carrying the
    /// complete pair", which is also true of the Custom Battle Grey Warden
    /// commander, so it fired on the hero as well - and the hero preview model
    /// was what broke. This matches the Twinblade Guard's character id alone:
    /// an immutable reference read, no Equipment or SpawnEquipment access, no
    /// Mission lookup, no logging, and no hero or preview character can match
    /// it.
    ///
    /// Skipping it costs these guards nothing - they carry no shield, so there
    /// is no shield usage to enforce.
    /// </summary>
    [HarmonyPatch(typeof(Agent), nameof(Agent.EnforceShieldUsage))]
    internal static class GwpDualBladeShieldEnforcementPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Agent __instance) =>
            __instance?.Character?.StringId != GwpIds.TwinbladeTroopId;
    }
}
