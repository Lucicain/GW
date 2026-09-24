using System;
using HarmonyLib;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Gives both dual blades - one pair, shared by the player and every NPC -
    /// the collision body a crafted item cannot carry from XML, on the
    /// ItemObjects of the game being started.
    ///
    /// It runs for every Game, not once per process: native builds a fresh
    /// MBObjectManager for each game (campaign load, every custom battle) and
    /// reloads every item into it, so a one-time write only ever reached the
    /// first game of a session (2026-09-24).
    ///
    /// The blades carry no shield flags. Native's AI keeps an off-hand item only
    /// when it is HeldInOffHand and CanBlockRanged (TaleWorlds.Native 1.4.8,
    /// 0x1806b0118), and CanBlockRanged is the same flag that makes a held item
    /// stop arrows. The off-hand blade is a weapon and must not block missiles,
    /// so native is left not recognising it and the pair is kept together by
    /// GwpDualBladeFightGripComponent instead (user decision, 2026-09-24).
    /// </summary>
    internal static class GwpDualBladeItemSetup
    {
        internal static void Apply(Game? game)
        {
            if (game?.ObjectManager == null)
                return;

            try
            {
                // Crafted items get no collision body - BladeData has no
                // collision-body attribute - and a weapon dropped into water is
                // handled as a world object with physics. Agents drowning in a
                // naval battle crashed inside TaleWorlds.Native with an access
                // violation, the last log line each time being a render request
                // for a dual blade, which is a null physics body all over. ROT's
                // plain blades declare a body explicitly for the same reason.
                foreach (string bladeId in new[]
                {
                    GwpIds.DualBladeOffhandItemId,
                    GwpIds.DualBladeMainhandItemId
                })
                {
                    ItemObject? blade = game.ObjectManager
                        .GetObject<ItemObject>(bladeId);
                    if (blade == null)
                    {
                        GwpFaultTrace.Write(
                            "DUAL_BLADE_ITEM_SETUP_MISSING",
                            details: bladeId);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(blade.CollisionBodyName))
                        continue;

                    AccessTools.Property(typeof(ItemObject), "CollisionBodyName")
                        ?.SetValue(blade, blade.BodyName);
                }
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write(
                    "DUAL_BLADE_ITEM_SETUP_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
