using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Rebuilds the mechanism this project had working on v1.4.8 for the
    /// Twinblade Guard, recovered from the maintenance record because it was
    /// never committed.
    ///
    /// The record's own A/B ladder is unambiguous about what native needs
    /// before its AI will keep a blade in the off hand:
    ///   - MeleeWeapon + HasHitPoints alone: the guard drew the off-hand blade
    ///     and native cleared it again about 2.3s later, across 200 agents.
    ///   - adding CanBlockRanged: "NPC 已成功拔出左手剑并保持双持动作" - the
    ///     blade stayed. This is the flag native's off-hand qualification
    ///     actually tests, which is consistent with every native HeldInOffHand
    ///     item being a shield.
    ///   - with CanBlockRanged but no shield collision object, the first real
    ///     block crashed at TaleWorlds.Native.dll+0x73ddf8, a null source in an
    ///     object copy. That remains an open risk here; see below.
    ///
    /// So the off-hand blade's *native copy* is given HasHitPoints,
    /// CanBlockRanged and durability, while MeleeWeapon is preserved by OR
    /// rather than replaced - clearing the weapon mask produced its own native
    /// crash and must not come back.
    ///
    /// MissionWeapon.GetWeaponData is deliberately NOT patched. It returns a
    /// large by-value WeaponData carrying MetaMesh, TableauMaterial and
    /// PhysicsShape handles, and the last round proved that merely having it
    /// patched corrupts what every caller receives: the patch could not even
    /// fire, because __instance was declared 'in' on a struct method and never
    /// bound, yet the character previews broke exactly as they have every other
    /// time this method was patched. AgentVisuals builds weapon meshes through
    /// it, which is the whole story of the recurring model damage. The
    /// historical collision-shape fix went through that method and therefore
    /// cannot be reused; if blocking crashes, the collision body has to be
    /// supplied some other way.
    ///
    /// GetWeaponStatsData is safe by comparison - it returns a managed array
    /// reference - and carries the flags and durability that decide the
    /// qualification. Everything is confined to one thread-local scope around a
    /// single Agent.EquipItemsFromSpawnEquipment call for an AI agent with the
    /// complete pair; the managed MissionWeapon is never modified, the player
    /// never enters the scope, and no preview-side patch is added.
    /// </summary>
    internal static class GwpDualBladeAiNativeSync
    {
        // Durability for the off-hand blade's native record. A shield-qualified
        // item with no hit points would register as already broken.
        private const short OffHandHitPoints = 500;

        // Preserved by OR: MeleeWeapon and the sword usage stay exactly as they
        // are, so the blade remains a blade for damage and animation.
        private const ulong OffHandQualification =
            (ulong)(WeaponFlags.HasHitPoints | WeaponFlags.CanBlockRanged);

        [ThreadStatic]
        private static Agent? _scope;

        internal static bool InScope => _scope != null;

        internal static void OpenScope(Agent? agent)
        {
            _scope = GwpDualBladeLoadout.IsDualBladeNpc(agent) ? agent : null;

            if (_scope != null)
            {
                GwpDualBladeTrace.Write(
                    "AI_NATIVE_SYNC_BEGIN",
                    _scope,
                    "offhandQualification=HasHitPoints|CanBlockRanged");
            }
        }

        internal static void CloseScope() => _scope = null;

        internal static bool IsOffHandBlade(in MissionWeapon weapon) =>
            !weapon.IsEmpty
            && string.Equals(
                weapon.Item?.StringId,
                GwpIds.DualBladeOffhandItemId,
                StringComparison.OrdinalIgnoreCase);

        private static bool _appliedOnceLogged;

        internal static void ApplyQualification(ref WeaponStatsData stats)
        {
            stats.WeaponFlags |= OffHandQualification;
            if (stats.MaxDataValue < OffHandHitPoints)
                stats.MaxDataValue = OffHandHitPoints;

            if (!_appliedOnceLogged)
            {
                _appliedOnceLogged = true;
                GwpDualBladeTrace.Write(
                    "AI_NATIVE_SYNC_APPLIED",
                    _scope,
                    "flags=" + (WeaponFlags)stats.WeaponFlags
                    + "; maxDataValue=" + stats.MaxDataValue);
            }
        }
    }

    /// <summary>
    /// Opens the scope for exactly one native equipment synchronisation of one
    /// qualifying AI agent. A finalizer closes it even if native throws.
    /// </summary>
    [HarmonyPatch(
        typeof(Agent),
        nameof(Agent.EquipItemsFromSpawnEquipment),
        new[] { typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
    internal static class GwpDualBladeAiEquipScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Agent __instance) =>
            GwpDualBladeAiNativeSync.OpenScope(__instance);

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            GwpDualBladeAiNativeSync.CloseScope();
            return __exception;
        }
    }

    /// <summary>
    /// The qualification itself. MeleeWeapon and the sword usage are preserved:
    /// the record shows that clearing the weapon mask produced its own native
    /// crash, and ROT's blades keep OneHandedSword + MeleeWeapon throughout.
    /// </summary>
    [HarmonyPatch(
        typeof(MissionWeapon),
        nameof(MissionWeapon.GetWeaponStatsData))]
    internal static class GwpDualBladeAiWeaponStatsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            ref MissionWeapon __instance,
            ref WeaponStatsData[] __result)
        {
            if (__result == null
                || !GwpDualBladeAiNativeSync.InScope
                || !GwpDualBladeAiNativeSync.IsOffHandBlade(in __instance))
            {
                return;
            }

            for (int i = 0; i < __result.Length; i++)
                GwpDualBladeAiNativeSync.ApplyQualification(ref __result[i]);
        }
    }
}
