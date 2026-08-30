using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Routes only a completed ground-item interaction for the two paired
    /// blades. No Agent or MissionEquipment method is patched, so character
    /// tableaux and Custom Battle previews remain outside this code path.
    /// </summary>
    internal static class GwpDualBladeGroundPickup
    {
        internal static bool TryGetFixedSlot(
            in MissionWeapon weapon,
            out EquipmentIndex slot)
        {
            string? itemId = weapon.Item?.StringId;
            if (itemId == GwpIds.DualBladeOffhandItemId)
            {
                slot = EquipmentIndex.WeaponItemBeginSlot;
                return true;
            }

            if (itemId == GwpIds.DualBladeMainhandItemId)
            {
                slot = EquipmentIndex.Weapon1;
                return true;
            }

            slot = EquipmentIndex.None;
            return false;
        }

        internal static void RoutePickup(
            Agent userAgent,
            SpawnedItemEntity spawnedItemEntity,
            EquipmentIndex requestedSlot,
            out bool removeWeapon)
        {
            MissionWeapon incomingWeapon = spawnedItemEntity.WeaponCopy;
            if (!TryGetFixedSlot(
                    in incomingWeapon,
                out EquipmentIndex fixedSlot)
                || !GwpDualBladeLoadout.IsEligibleDualBladeUser(userAgent)
                || GameNetwork.IsClientOrReplay)
            {
                userAgent.OnItemPickup(
                    spawnedItemEntity,
                    requestedSlot,
                    out removeWeapon);
                return;
            }

            removeWeapon = true;
            if (!MissionEquipment.DoesWeaponFitToSlot(
                    fixedSlot,
                    incomingWeapon))
            {
                removeWeapon = false;
                return;
            }

            if (!userAgent.Equipment[fixedSlot].IsEmpty)
            {
                userAgent.DropItem(
                    fixedSlot,
                    incomingWeapon.Item.PrimaryWeapon.WeaponClass);
            }

            userAgent.EquipWeaponFromSpawnedItemEntity(
                fixedSlot,
                spawnedItemEntity,
                removeWeapon: true);

            // Routing the blade into its fixed slot is all this needs to do.
            // Forcing an action set and re-wielding both hands from here was
            // part of the removed AI dual-blade work; native decides what the
            // character wields, exactly as it does in ROT.

            foreach (AgentComponent component in userAgent.Components)
                component.OnItemPickup(spawnedItemEntity);

            if (userAgent.Controller == AgentControllerType.AI
                && userAgent.HumanAIComponent != null)
            {
                AiItemPickupDoneMethod?.Invoke(
                    userAgent.HumanAIComponent,
                    new object[] { spawnedItemEntity });
            }

            userAgent.Mission.TriggerOnItemPickUpEvent(
                userAgent,
                spawnedItemEntity);
        }

        private static readonly MethodInfo? AiItemPickupDoneMethod =
            AccessTools.Method(
                typeof(HumanAIComponent),
                "ItemPickupDone",
                new[] { typeof(SpawnedItemEntity) });
    }

    /// <summary>
    /// Replaces only the single Agent.OnItemPickup call made after a real
    /// SpawnedItemEntity interaction succeeds. The rest of native
    /// OnUseStopped, including object-use cleanup and ground-entity deletion,
    /// remains untouched.
    /// </summary>
    [HarmonyPatch(
        typeof(SpawnedItemEntity),
        nameof(SpawnedItemEntity.OnUseStopped),
        new[] { typeof(Agent), typeof(bool), typeof(int) })]
    internal static class GwpDualBladeGroundPickupPatch
    {
        private static readonly MethodInfo NativePickupMethod =
            AccessTools.Method(
                typeof(Agent),
                nameof(Agent.OnItemPickup),
                new[]
                {
                    typeof(SpawnedItemEntity),
                    typeof(EquipmentIndex),
                    typeof(bool).MakeByRefType()
                })
            ?? throw new MissingMethodException(
                typeof(Agent).FullName,
                nameof(Agent.OnItemPickup));

        private static readonly MethodInfo RoutedPickupMethod =
            AccessTools.Method(
                typeof(GwpDualBladeGroundPickup),
                nameof(GwpDualBladeGroundPickup.RoutePickup))
            ?? throw new MissingMethodException(
                typeof(GwpDualBladeGroundPickup).FullName,
                nameof(GwpDualBladeGroundPickup.RoutePickup));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (!replaced && instruction.Calls(NativePickupMethod))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = RoutedPickupMethod;
                    replaced = true;
                }

                yield return instruction;
            }

            if (!replaced)
            {
                throw new InvalidOperationException(
                    "GreyWarden could not locate the native ground-pickup "
                    + "call in SpawnedItemEntity.OnUseStopped.");
            }
        }
    }
}
