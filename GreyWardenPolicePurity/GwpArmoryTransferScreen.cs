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
        private sealed class Delivery
        {
            internal Func<bool> Confirm = () => false;
            internal Func<string?> Preview = () => null;
        }

        private static readonly ConditionalWeakTable<InventoryLogic, Delivery> Deliveries = new();

        internal static void Register(InventoryLogic logic, Func<bool> confirm, Func<string?> preview) =>
            Deliveries.Add(logic, new Delivery { Confirm = confirm, Preview = preview });

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
                if (!Deliveries.TryGetValue(__instance, out Delivery delivery)) return true;
                __result = !__instance.IsPreviewingItem && delivery.Confirm();
                return false;
            }
        }

        /// <summary>
        /// 原版背包界面底部居中的 TradeLbl 写「你将付出/获得多少金币」（v1.4.8/v1.5.7 SPInventoryVM.OnTotalAmountChange、
        /// Inventory.xml TradeLabel，空字符串自动隐藏）。交给祭司时这笔不是金币交易，改写成能换多少声望。
        /// 不交易时原版不会再写它；打开、转移（AfterTransfer）、重置（AfterReset）三处按当前挑选重算。
        /// </summary>
        [HarmonyPatch(typeof(SPInventoryVM))]
        private static class LabelPatch
        {
            private static IEnumerable<MethodBase> TargetMethods() => new[]
            {
                AccessTools.Method(typeof(SPInventoryVM), nameof(SPInventoryVM.RefreshValues)),
                AccessTools.Method(typeof(SPInventoryVM), "AfterReset"),
                AccessTools.Method(typeof(SPInventoryVM), "AfterTransfer")
            };

            private static void Postfix(SPInventoryVM __instance, InventoryLogic ____inventoryLogic)
            {
                if (____inventoryLogic == null || !Deliveries.TryGetValue(____inventoryLogic, out Delivery delivery)) return;
                __instance.RightInventoryOwnerName = GwpText.Get(GwpTextCatalog.gwp_player_armory);
                __instance.TradeLbl = delivery.Preview() ?? string.Empty;
            }
        }
    }
}
