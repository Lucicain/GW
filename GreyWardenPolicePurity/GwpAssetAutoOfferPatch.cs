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
#if !GWP_GAME_157_OR_LATER
        // v1.4.8 按报价正负把按钮文字在「报价/赠送」间切换；v1.5.7 删了这个方法，按钮在打开时固定为「确认」。
        // （依据：v1.4.8/v1.5.7 C#反编译 BarterVM；实机故障 2026-11-16 复命配平空引用）
        private static readonly System.Reflection.MethodInfo Label = AccessTools.Method(typeof(BarterVM), "RefreshOfferLabel");
#endif

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
#if !GWP_GAME_157_OR_LATER
            Label.Invoke(__instance, null);
#endif
            return false;
        }
    }
}
