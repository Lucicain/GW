using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    [HarmonyPatch(typeof(BarterVM), nameof(BarterVM.ExecuteOffer))]
    internal static class GwpAssetOfferValidationPatch
    {
        private static bool Prefix(BarterVM __instance, BarterData ____barterData)
        {
            if (!GwpAssetPayment.Sessions.TryGetValue(____barterData, out var payment)) return true;
            foreach (var item in __instance.LeftOfferList.Concat(__instance.RightOfferList))
                if (payment.Entries.Contains(item.Barterable) && item.Barterable.IsOffered)
                    item.Barterable.CurrentAmount = item.CurrentOfferedAmount;
            if (payment.Valid)
            {
                // This VM prefix is reached in the live failing path; the
                // patched manager wrapper is not. Complete our own sessions
                // here so native ApplyBarterOffer cannot publish a trade event.
                if (payment.SelectionOnly || payment.ReportMode)
                    return GwpDispatchSelectionPatch.Prefix(
                        TaleWorlds.CampaignSystem.Campaign.Current.BarterManager, ____barterData);
                return true;
            }
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_offer_changed}This offer exceeds our agreement or includes unavailable property. Please adjust it.")));
            return false;
        }
    }

    [HarmonyPatch(typeof(BarterManager), nameof(BarterManager.IsOfferAcceptable))]
    internal static class GwpAssetAcceptancePatch
    {
        private static void Postfix(BarterData args, ref bool __result)
        {
            if (GwpAssetPayment.Sessions.TryGetValue(args, out var payment))
                __result = payment.SelectionOnly || payment.ReportMode ? payment.Valid : __result && payment.Valid;
        }
    }

    // Planning a shipment must not grant barter relations, trading XP or cooldowns.
    [HarmonyPatch(typeof(BarterManager), nameof(BarterManager.ApplyAndFinalizePlayerBarter))]
    internal static class GwpDispatchSelectionPatch
    {
        internal static bool Prefix(BarterManager __instance, BarterData barterData)
        {
            if (!GwpAssetPayment.Sessions.TryGetValue(barterData, out var payment) || (!payment.SelectionOnly && !payment.ReportMode)) return true;
            if (!payment.Valid) return false;
            payment.ConfirmSelection();
            __instance.Close();
            if (!payment.SelectionOnly && TaleWorlds.CampaignSystem.Campaign.Current.ConversationManager.IsConversationInProgress)
                TaleWorlds.CampaignSystem.Campaign.Current.ConversationManager.ContinueConversation();
            return false;
        }
    }

    [HarmonyPatch(typeof(BarterManager), nameof(BarterManager.CancelAndFinalizePlayerBarter))]
    internal static class GwpDispatchSelectionCancelPatch
    {
        internal static bool Prefix(BarterManager __instance, BarterData barterData)
        {
            if (!GwpAssetPayment.Sessions.TryGetValue(barterData, out var payment) || !payment.SelectionOnly) return true;
            __instance.Close();
            return false;
        }
    }

    [HarmonyPatch(typeof(BarterVM), nameof(BarterVM.ExecuteCancel))]
    internal static class GwpDispatchVmCancelPatch
    {
        private static bool Prefix(BarterData ____barterData)
        {
            return GwpDispatchSelectionCancelPatch.Prefix(
                TaleWorlds.CampaignSystem.Campaign.Current.BarterManager, ____barterData);
        }
    }

    [HarmonyPatch(typeof(BarterVM), "SendOffer")]
    internal static class GwpDispatchOfferDisplayPatch
    {
        private static void Postfix(BarterVM __instance, BarterData ____barterData)
        {
            if (!GwpAssetPayment.Sessions.TryGetValue(____barterData, out var payment) || (!payment.SelectionOnly && !payment.ReportMode)) return;
            __instance.IsOfferDisabled = !payment.Valid;
            UpdateLabel(__instance, payment);
        }

        internal static void UpdateLabel(BarterVM vm, GwpAssetPayment payment) => vm.OfferLbl = GwpText.Get(
            "{=gwp_dispatch_manifest_offer}Load {VAR_1} / {VAR_2}", "VAR_1", payment.OfferedValue, "VAR_2", payment.SuggestedTarget);
    }

    [HarmonyPatch(typeof(BarterVM), "RefreshOfferLabel")]
    internal static class GwpDispatchOfferLabelPatch
    {
        private static void Postfix(BarterVM __instance, BarterData ____barterData)
        {
            if (GwpAssetPayment.Sessions.TryGetValue(____barterData, out var payment) && (payment.SelectionOnly || payment.ReportMode))
                GwpDispatchOfferDisplayPatch.UpdateLabel(__instance, payment);
        }
    }
}
