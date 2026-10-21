using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>One authority/reservation query for both bounties and atonement warrants.</summary>
    internal static class GwpPlayerCaseAssignment
    {
        private static PlayerBountyBehavior? Bounty => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
        private static PoliceEnforcementBehavior? Enforcement => Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();

        internal static bool HasAuthority(Hero? hero) =>
            Bounty?.HasCommissionAuthority(hero) == true || Enforcement?.IsActiveAtonementTarget(hero) == true;

        internal static bool IsAssignedCase(Hero? hero) =>
            Bounty?.IsActiveBountyTarget(hero) == true || Enforcement?.IsActiveAtonementTarget(hero) == true;

        internal static bool IsCaseHeldByPlayer(string? heroId) =>
            PlayerBountyBehavior.IsBountyCaseHeldByPlayer(heroId) || Enforcement?.HoldsAtonementCase(heroId) == true;

        internal static bool IsReservedTarget(MobileParty? party) =>
            PlayerBountyBehavior.IsBountyReservedTarget(party) || Enforcement?.ReservesAtonementTarget(party) == true;

        internal static bool CanTakeCase(CrimeRecord crime) => crime.HasOpenCase &&
            crime.IsOffenderPursuable() && !PoliceEnforcementBehavior.HasEnvoyAgreement(crime);

        /// <summary>玩家能接的：野外可追捕的，加上无部队住城、可进城搜捕的。</summary>
        internal static bool CanPlayerTakeCase(CrimeRecord crime) => crime.HasOpenCase &&
            (crime.IsOffenderPursuable() || crime.IsOffenderSearchableInSettlement())
            && !PoliceEnforcementBehavior.HasEnvoyAgreement(crime);

        internal static bool CanOfferGrace(Hero? hero) => Enforcement?.IsActiveAtonementTarget(hero) == true
            ? Enforcement.CanOfferAtonementGrace : Bounty?.CanOfferFieldGrace == true;

        internal static void RecordFieldGrace(Hero? hero, string message)
        {
            if (Enforcement?.IsActiveAtonementTarget(hero) == true) Enforcement.RecordAtonementGrace(message);
            else Bounty?.RecordFieldGrace(message);
        }

        internal static void NotifySettledInField(Hero? hero, int collected, int assessed, int severity)
        {
            Bounty?.NotifyCaseSettledInField(hero, collected, assessed, severity);
            Enforcement?.NotifyAtonementResolved(hero, "field_settlement");
        }

        internal static void NotifyCaptured(Hero? hero, int assessed, int severity)
        {
            Bounty?.NotifyCaseClosedByCapture(hero, assessed, severity);
            Enforcement?.NotifyAtonementResolved(hero, "field_capture");
        }
    }
}
