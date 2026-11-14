using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace GreyWardenPolicePurity
{
    // 原版 Stash 双栏用于挑选货物，个人装备和背包都不是这笔交易的来源。
    internal static class GwpArmoryTransferScreen
    {
        private static readonly ConditionalWeakTable<InventoryLogic, Func<bool>> Deliveries = new();

        internal static void Register(InventoryLogic logic, Func<bool> confirm) => Deliveries.Add(logic, confirm);

        [HarmonyPatch(typeof(InventoryLogic), "ProcessTransferCommand")]
        private static class TransferPatch
        {
            private static bool Prefix(InventoryLogic __instance, TransferCommand command) =>
                !Deliveries.TryGetValue(__instance, out _) ||
                !InventoryLogic.IsEquipmentSide(command.FromSide) && !InventoryLogic.IsEquipmentSide(command.ToSide);
        }

        [HarmonyPatch(typeof(InventoryLogic), nameof(InventoryLogic.DoneLogic))]
        private static class DonePatch
        {
            private static bool Prefix(InventoryLogic __instance, ref bool __result)
            {
                if (!Deliveries.TryGetValue(__instance, out Func<bool> confirm)) return true;
                __result = !__instance.IsPreviewingItem && confirm();
                return false;
            }
        }

        [HarmonyPatch(typeof(SPInventoryVM))]
        private static class LabelPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => new[]
            {
                AccessTools.Method(typeof(SPInventoryVM), nameof(SPInventoryVM.RefreshValues)),
                AccessTools.Method(typeof(SPInventoryVM), "AfterReset")
            };

            private static void Postfix(SPInventoryVM __instance, InventoryLogic ____inventoryLogic)
            {
                if (____inventoryLogic == null || !Deliveries.TryGetValue(____inventoryLogic, out _)) return;
                __instance.RightInventoryOwnerName = GwpText.Get(GwpTextCatalog.gwp_player_armory);
            }
        }
    }
}
