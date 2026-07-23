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
            GreyWardenPlayerRequestBehavior.NotifyOrdinaryDutyCompleted(
                party, duty);
            GreyWardenTroopRequestBehavior.NotifyOrdinaryDutyCompleted(
                party, duty);
        }
    }
}
