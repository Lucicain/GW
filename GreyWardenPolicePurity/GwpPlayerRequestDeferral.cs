using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Counts only successfully completed ordinary Grey Warden duties toward a
    /// player-request postponement.  Cancelled, displaced, or invalid work does
    /// not shorten the postponement.
    /// </summary>
    internal static class GwpPlayerRequestDeferral
    {
        internal static void NotifyDutyCompleted(MobileParty? party,
            string duty)
        {
            // 练兵订单已改为下单即结清，不再有顺延交付，因此不再参与这条通知。
            GreyWardenPlayerRequestBehavior.NotifyOrdinaryDutyCompleted(
                party, duty);
        }
    }
}
