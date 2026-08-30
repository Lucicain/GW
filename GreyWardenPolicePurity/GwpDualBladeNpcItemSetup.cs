using System;
using HarmonyLib;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Gives the NPC off-hand blade the two things a crafted item cannot carry
    /// from XML, once, on the loaded ItemObject.
    ///
    /// Why this shape. The record's A/B ladder shows native only keeps a blade
    /// in an AI's off hand when it carries HasHitPoints and CanBlockRanged, and
    /// a HasHitPoints item needs both a hit-point value and a collision body or
    /// it registers as already broken. Crafted items cannot supply either:
    /// Crafting.SetWeaponData leaves maxDataValue at 0 for anything that is not
    /// a throwing class, and BladeData has no collision-body attribute.
    ///
    /// The previous attempts supplied them by patching MissionWeapon per call,
    /// and every build that did so came back with broken character previews.
    /// The isolation run settled that it is the code and not the troop data, so
    /// this writes the values once to a data object instead - no detour on any
    /// method a preview or tableau calls, and nothing that runs per frame.
    ///
    /// It applies only to gwdualbladeoffhandai, the NPC copy. The player's
    /// gwdualbladeoffhand is left exactly as it is.
    /// </summary>
    internal static class GwpDualBladeNpcItemSetup
    {
        private const short OffHandHitPoints = 500;

        private const WeaponFlags Qualification =
            WeaponFlags.HasHitPoints | WeaponFlags.CanBlockRanged;

        private static bool _applied;

        internal static void Apply(Game? game)
        {
            if (_applied || game?.ObjectManager == null)
                return;

            _applied = true;

            try
            {
                ItemObject? blade = game.ObjectManager
                    .GetObject<ItemObject>(GwpIds.DualBladeOffhandAiItemId);
                if (blade == null)
                {
                    GwpDualBladeTrace.Write(
                        "NPC_ITEM_SETUP_MISSING",
                        details: GwpIds.DualBladeOffhandAiItemId);
                    return;
                }

                // A collision object has to exist before the qualification is
                // added; without one the weapon does not register at all.
                if (string.IsNullOrEmpty(blade.CollisionBodyName))
                {
                    AccessTools.Property(typeof(ItemObject), "CollisionBodyName")
                        ?.SetValue(blade, blade.BodyName);
                }

                WeaponComponentData? weapon = blade.PrimaryWeapon;
                if (weapon == null)
                {
                    GwpDualBladeTrace.Write(
                        "NPC_ITEM_SETUP_NO_WEAPON",
                        details: blade.StringId);
                    return;
                }

                // WeaponComponentData.WeaponFlags is a public field, not a
                // property. The previous build reached for it with
                // AccessTools.Property, which returned null and was swallowed
                // by the null-conditional call, so the flags were never written
                // and the qualification was never actually exercised - the
                // trace read back "flags=MeleeWeapon". Assign it directly.
                // MeleeWeapon and the sword usage are preserved by OR; clearing
                // the weapon mask caused its own native crash historically.
                weapon.WeaponFlags |= Qualification;

                if (weapon.MaxDataValue < OffHandHitPoints)
                {
                    AccessTools.Property(typeof(WeaponComponentData), "MaxDataValue")
                        ?.SetValue(weapon, OffHandHitPoints);
                }

                WeaponComponentData? readback = blade.PrimaryWeapon;
                GwpDualBladeTrace.Write(
                    "NPC_ITEM_SETUP",
                    details: blade.StringId
                        + "; collision=" + blade.CollisionBodyName
                        + "; flags=" + readback?.WeaponFlags
                        + "; maxDataValue=" + readback?.MaxDataValue
                        + "; qualified="
                        + (readback != null
                            && (readback.WeaponFlags & Qualification) == Qualification));
            }
            catch (Exception exception)
            {
                GwpDualBladeTrace.Write(
                    "NPC_ITEM_SETUP_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
