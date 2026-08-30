using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
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
    ///     object copy. Supplying a real collision shape is what addressed it.
    ///
    /// So the off-hand blade's *native copy* is given HasHitPoints,
    /// CanBlockRanged, durability and a shield collision body, while
    /// MeleeWeapon is preserved by OR rather than replaced - clearing the
    /// weapon mask is what produced a different crash and must not come back.
    ///
    /// Everything is confined to one thread-local scope around a single
    /// Agent.EquipItemsFromSpawnEquipment call for an AI agent that carries the
    /// complete pair. The managed MissionWeapon is never modified, the player
    /// never enters the scope, and no preview path can either: tableaus build
    /// through AgentVisuals and never call Agent.EquipItemsFromSpawnEquipment,
    /// so with the scope closed these postfixes return the stock data
    /// unchanged. No preview-side patch is added - adding one is what damaged
    /// character models in earlier rounds.
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

        private const string ShieldCollisionBody = "bo_wlarge_shield";

        [ThreadStatic]
        private static Agent? _scope;

        private static PhysicsShape? _shieldCollision;
        private static bool _shieldCollisionResolved;

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

        /// <summary>
        /// Resolved once and cached. A miss leaves the collision shape alone
        /// rather than substituting anything.
        /// </summary>
        internal static PhysicsShape? ShieldCollision
        {
            get
            {
                if (_shieldCollisionResolved)
                    return _shieldCollision;

                _shieldCollisionResolved = true;
                try
                {
                    _shieldCollision =
                        PhysicsShape.GetFromResource(ShieldCollisionBody);
                }
                catch (Exception exception)
                {
                    GwpDualBladeTrace.Write(
                        "AI_NATIVE_SYNC_NO_COLLISION",
                        details: ShieldCollisionBody
                            + " -> " + exception.GetType().Name);
                    _shieldCollision = null;
                }

                return _shieldCollision;
            }
        }

        internal static void ApplyQualification(ref WeaponStatsData stats)
        {
            stats.WeaponFlags |= OffHandQualification;
            if (stats.MaxDataValue < OffHandHitPoints)
                stats.MaxDataValue = OffHandHitPoints;
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
    /// Supplies the off-hand blade's native durability and a real collision
    /// body. Without a collision object the shield-qualified path dereferences
    /// null on the first block.
    /// </summary>
    [HarmonyPatch(
        typeof(MissionWeapon),
        nameof(MissionWeapon.GetWeaponData),
        new[] { typeof(bool) })]
    internal static class GwpDualBladeAiWeaponDataPatch
    {
        [HarmonyPostfix]
        private static void Postfix(in MissionWeapon __instance, ref WeaponData __result)
        {
            if (!GwpDualBladeAiNativeSync.InScope
                || !GwpDualBladeAiNativeSync.IsOffHandBlade(in __instance))
            {
                return;
            }

            PhysicsShape? collision = GwpDualBladeAiNativeSync.ShieldCollision;
            if (collision != null)
                __result.CollisionShape = collision;
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
            in MissionWeapon __instance,
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
