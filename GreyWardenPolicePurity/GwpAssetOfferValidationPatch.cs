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
            if (payment.Valid) return true;
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
            if (GwpAssetPayment.Sessions.TryGetValue(args, out var payment)) __result &= payment.Valid;
        }
    }
}
