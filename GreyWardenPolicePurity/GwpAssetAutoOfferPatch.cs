using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;

namespace GreyWardenPolicePurity
{
    [HarmonyPatch(typeof(BarterVM), nameof(BarterVM.ExecuteAutoBalance))]
    internal static class GwpAssetAutoOfferPatch
    {
        private static readonly System.Reflection.MethodInfo Change = AccessTools.Method(typeof(BarterVM), "ChangeBarterableIsOffered");
        private static readonly System.Reflection.MethodInfo Refresh = AccessTools.Method(typeof(BarterVM), "SendOffer");
        private static readonly System.Reflection.MethodInfo Label = AccessTools.Method(typeof(BarterVM), "RefreshOfferLabel");
        private static bool Prefix(BarterVM __instance, BarterData ____barterData)
        {
            if (!GwpAssetPayment.Sessions.TryGetValue(____barterData, out var payment)) return true;
            var proposal = payment.SuggestedOffer();
            foreach (var entry in payment.Entries.Where(e => !payment.IsAgreement(e)))
            {
                bool selected = proposal.TryGetValue(entry, out int count);
                Change.Invoke(__instance, new object[] { entry, selected });
                var vm = __instance.LeftOfferList.Concat(__instance.RightOfferList).FirstOrDefault(x => x.Barterable == entry);
                if (selected && vm != null)
                {
                    entry.CurrentAmount = count;
                    vm.CurrentOfferedAmount = count;
                }
                // Unoffered items retain their VM quantity. IsOffered alone excludes them from the receipt.
            }
            Refresh.Invoke(__instance, null);
            Label.Invoke(__instance, null);
            return false;
        }
    }
}
