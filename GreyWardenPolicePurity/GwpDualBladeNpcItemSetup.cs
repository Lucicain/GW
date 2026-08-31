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
                // Every blade needs a collision body, not just the NPC copy.
                // Crafted items get none - BladeData has no collision-body
                // attribute - and a weapon dropped into water is handled as a
                // world object with physics. Agents drowning in a naval battle
                // crashed inside TaleWorlds.Native with an access violation,
                // the last log line each time being a render request for a
                // dual blade, which is a null physics body all over. ROT's
                // plain blades declare a body explicitly for the same reason.
                foreach (string bladeId in new[]
                {
                    GwpIds.DualBladeOffhandItemId,
                    GwpIds.DualBladeMainhandItemId,
                    GwpIds.DualBladeOffhandAiItemId
                })
                {
                    ItemObject? anyBlade = game.ObjectManager
                        .GetObject<ItemObject>(bladeId);
                    if (anyBlade == null
                        || !string.IsNullOrEmpty(anyBlade.CollisionBodyName))
                    {
                        continue;
                    }

                    AccessTools.Property(typeof(ItemObject), "CollisionBodyName")
                        ?.SetValue(anyBlade, anyBlade.BodyName);

                }

                ItemObject? blade = game.ObjectManager
                    .GetObject<ItemObject>(GwpIds.DualBladeOffhandAiItemId);
                if (blade == null)
                {
                    GwpFaultTrace.Write(
                        "NPC_ITEM_SETUP_MISSING",
                        details: GwpIds.DualBladeOffhandAiItemId);
                    return;
                }

                // Every usage, not just the primary one. A crafted item can
                // carry more than one WeaponComponentData, and native reads
                // whichever usage is current - qualifying only the first would
                // leave the one actually in play untouched.
                var usages = blade.Weapons;
                if (usages == null || usages.Count == 0)
                {
                    GwpFaultTrace.Write(
                        "NPC_ITEM_SETUP_NO_WEAPON",
                        details: blade.StringId);
                    return;
                }

                foreach (WeaponComponentData usage in usages)
                {
                    // WeaponComponentData.WeaponFlags is a public field, not a
                    // property; the build before last reached for it with
                    // AccessTools.Property, got null, and the null-conditional
                    // call swallowed the miss. Assign it directly. MeleeWeapon
                    // and the sword usage are preserved by OR - clearing the
                    // weapon mask caused its own native crash historically.
                    usage.WeaponFlags |= Qualification;

                    if (usage.MaxDataValue < OffHandHitPoints)
                    {
                        AccessTools.Property(
                                typeof(WeaponComponentData), "MaxDataValue")
                            ?.SetValue(usage, OffHandHitPoints);
                    }
                }

                WeaponComponentData? readback = blade.PrimaryWeapon;
                int qualifiedUsages = 0;
                foreach (WeaponComponentData usage in usages)
                {
                    if ((usage.WeaponFlags & Qualification) == Qualification)
                        qualifiedUsages++;
                }
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write(
                    "NPC_ITEM_SETUP_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
