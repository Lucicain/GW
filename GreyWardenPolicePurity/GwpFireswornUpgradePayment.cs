using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
#if GWP_DIAGNOSTICS
using TaleWorlds.CampaignSystem.Actions;
#endif

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 守村灰袍的升级费由灰袍公库承担，不由村庄所属家族付款（用户裁定 2026-10-09）。
    /// 只在原版一次升级调用、当前处理灰袍兵时替换付款人；不改实体所有者、报价或升级器。
    /// 原版和本机 RBM 都逐兵种调用 IsTroopUpgradeable，再从 Party.Owner 支付，故共用这个范围。
    /// </summary>
    internal static class GwpFireswornUpgradePayment
    {
        // 仅同步升级调用的栈内上下文，无跨小时待办，不需要存档；Finalizer 在正常与异常出口都还原。
        [ThreadStatic] internal static PaymentScope? Current;

        internal sealed class PaymentScope
        {
            internal readonly PartyBase Party;
            internal readonly Hero? Treasurer;
            internal CharacterObject? Troop;
            internal bool IsGuard => Troop != null && !Troop.IsHero && GwpCommon.IsGreyWardenTroop(Troop);

            internal PaymentScope(PartyBase party)
            {
                Party = party;
                Treasurer = PoliceStats.GetPoliceClan()?.Leader;
            }
        }

        internal static bool SelectTroop(PartyBase party, CharacterObject troop)
        {
            PaymentScope? scope = Current;
            if (scope?.Party != party)
                return true;
            scope.Troop = troop;
            return !scope.IsGuard || scope.Treasurer?.IsAlive == true;
        }

        internal static bool TryGetPayer(PartyBase party, out Hero? payer)
        {
            PaymentScope? scope = Current;
            payer = scope?.Treasurer;
            return scope?.Party == party && scope.IsGuard;
        }
    }

    [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "UpgradeReadyTroops")]
    internal static class GwpFireswornUpgradeScopePatch
    {
        // 先于替换原版升级体的第三方 Prefix 建立范围，原版被跳过也执行 Finalizer。
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Prefix(PartyBase party, out GwpFireswornUpgradePayment.PaymentScope? __state)
        {
            __state = GwpFireswornUpgradePayment.Current;
            GwpFireswornUpgradePayment.Current = party.MobileParty?.IsMilitia == true &&
                GwpFireswornBehavior.HasFireswornGuard(party.MobileParty.HomeSettlement)
                ? new GwpFireswornUpgradePayment.PaymentScope(party) : null;
        }

        [HarmonyFinalizer]
        private static void Finalizer(GwpFireswornUpgradePayment.PaymentScope? __state)
        {
            GwpFireswornUpgradePayment.Current = __state;
        }
    }

    [HarmonyPatch(typeof(PartyBase), nameof(PartyBase.Owner), MethodType.Getter)]
    internal static class GwpFireswornUpgradePayerPatch
    {
        private static void Postfix(PartyBase __instance, ref Hero __result)
        {
            if (GwpFireswornUpgradePayment.TryGetPayer(__instance, out Hero? payer))
                __result = payer!;
        }
    }

#if GWP_DIAGNOSTICS
    [HarmonyPatch(typeof(GiveGoldAction), nameof(GiveGoldAction.ApplyBetweenCharacters))]
    internal static class GwpFireswornUpgradePaymentTracePatch
    {
        private static void Prefix(Hero giverHero, Hero recipientHero, out int? __state)
        {
            var scope = GwpFireswornUpgradePayment.Current;
            __state = scope?.IsGuard == true && giverHero == scope.Treasurer && recipientHero == null
                ? giverHero.Gold : (int?)null;
        }

        private static void Postfix(Hero giverHero, int amount, int? __state)
        {
            if (__state == null)
                return;
            var scope = GwpFireswornUpgradePayment.Current!;
            GwpAiDiagnostics.WriteFaith("GUARD_UPGRADE_PAYMENT",
                "party=" + scope.Party.Id + "; troop=" + scope.Troop!.StringId +
                "; payer=" + giverHero.StringId + "; quoted=" + amount +
                "; paid=" + (__state.Value - giverHero.Gold) + "; treasury=" + giverHero.Gold);
        }
    }
#endif
}
