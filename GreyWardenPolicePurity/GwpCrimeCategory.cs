using System;

namespace GreyWardenPolicePurity
{
    public enum GwpCrimeCategory
    {
        Unknown = 0,
        CaravanAttack = 1,
        VillageViolence = 2,
        PlayerCase = 3
    }

    internal static class GwpCrimeCategoryClassifier
    {
        internal static GwpCrimeCategory FromCrimeType(string? crimeType, string? crimeId = null)
        {
            if (string.Equals(crimeId, CrimePool.PlayerCrimeId, StringComparison.OrdinalIgnoreCase))
                return GwpCrimeCategory.PlayerCase;

            string value = (crimeType ?? string.Empty).Trim();
            if (value.IndexOf("caravan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.Contains("商队"))
                return GwpCrimeCategory.CaravanAttack;

            if (value.IndexOf("villager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("village", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.Contains("村民") || value.Contains("村庄") || value.Contains("劫掠") ||
                value.Contains("烧村"))
                return GwpCrimeCategory.VillageViolence;

            return GwpCrimeCategory.Unknown;
        }
    }

    /// <summary>
    /// One base charge per deed. The intake ledger adds it the moment a crime is reported,
    /// so an offender who burns two villages before anyone reaches him owes two charges.
    /// </summary>
    internal static class GwpFieldArrestPricing
    {
        internal static int StandingFine(int standing, int rate) =>
            (int)Math.Min(GwpTuning.FieldArrest.MaximumCaseFine, Math.Abs((long)standing) * Math.Max(0, rate));
        internal static int BaseChargeFor(CrimeRecord crime) =>
            (int)Math.Min(int.MaxValue, (long)Math.Max(1, crime.IncidentCount) * GwpTuning.FieldArrest.BaseChargeVillageViolence);
        internal static int BaseChargeFor(GwpCrimeCategory category) =>
            category == GwpCrimeCategory.CaravanAttack
                ? GwpTuning.FieldArrest.BaseChargeCaravanAttack
                : GwpTuning.FieldArrest.BaseChargeVillageViolence;

        /// <summary>
        /// The whole bill for one open case: every deed it already covers, plus the
        /// offender's own standing at the Wardens' standard rate.
        /// </summary>
        internal static int AssessFine(CrimeRecord crime)
        {
            int baseCharge = BaseChargeFor(crime);

            TaleWorlds.CampaignSystem.Hero? offender = crime.OffenderHero;
            int standing = offender == null
                ? 0
                : System.Math.Max(0, CrimePool.GetHistory(offender)?.NegativeStanding ?? 0);

            return (int)Math.Min(GwpTuning.FieldArrest.MaximumCaseFine,
                (long)baseCharge + (long)standing * GwpTuning.Enforcement.FinePerPoint);
        }
    }
}
