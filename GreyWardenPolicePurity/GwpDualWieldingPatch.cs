using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Dual-wield attacks authored on the left-hand skeleton use bone 20 while
    /// Bannerlord still reports the ordinary weapon attachment bone. Native
    /// combat rejects that mismatch. ROT's implementation treats bone 20 as
    /// the intentional dual-wield collision and lets the normal weapon data
    /// determine the damage type and magnitude.
    /// </summary>
    [HarmonyPatch(
        typeof(MissionCombatMechanicsHelper),
        nameof(MissionCombatMechanicsHelper.IsCollisionBoneDifferentThanWeaponAttachBone))]
    internal static class GwpDualWieldCollisionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            ref bool __result,
            in AttackCollisionData collisionData,
            int weaponAttachBoneIndex)
        {
            // Bone 20 is the authored off-hand attack bone.  This is the same
            // narrow exception used by ROT: the animation authoring, rather
            // than the reported weapon slot, identifies the dual-wield hit.
            // Keeping the condition to this one bone avoids changing ordinary
            // sword collisions while allowing the left blade to retain its
            // declared cut damage.
            if (__result && collisionData.AttackBoneIndex == 20)
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Native collision processing changes a mismatched-bone hit to Blunt
    /// before it calls the damage routine. ROT's bone-20 exception prevents
    /// the mismatch, but the current engine still passes the selected type
    /// as a separate argument. Correct that argument at the final calculation
    /// boundary so the off-hand blade receives the declared Cut damage.
    /// </summary>
    [HarmonyPatch(typeof(MissionCombatMechanicsHelper), "ComputeBlowDamage")]
    internal static class GwpDualWieldDamageTypePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            in AttackInformation attackInformation,
            in AttackCollisionData attackCollisionData,
            ref DamageTypes damageType)
        {
            if (attackCollisionData.StrikeType != (int)StrikeType.Swing
                || attackCollisionData.AttackBoneIndex != 20)
            {
                return;
            }

            MissionWeapon attackerWeapon = attackInformation.AttackerWeapon;
            if (attackerWeapon.IsEmpty
                || attackerWeapon.Item?.StringId
                    != GwpIds.DualBladeOffhandItemId)
            {
                return;
            }

            Agent? attacker = attackInformation.AttackerAgent;
            if (attacker == null
                || !GwpDualBladeLoadout.TryGetCombatPair(
                    attacker,
                    out EquipmentIndex offhandSlot,
                    out EquipmentIndex mainhandSlot))
                return;

            try
            {
                MissionEquipment equipment = attacker.Equipment;
                if (IsItem(
                        equipment[offhandSlot],
                        GwpIds.DualBladeOffhandItemId)
                    && IsItem(
                        equipment[mainhandSlot],
                        GwpIds.DualBladeMainhandItemId))
                {
                    damageType = DamageTypes.Cut;
                }
            }
            catch
            {
                // Keep native damage if equipment is being rebuilt.
            }
        }

        private static bool IsItem(
            in MissionWeapon weapon,
            string itemId) =>
            !weapon.IsEmpty
            && weapon.Item?.StringId == itemId;
    }

}
