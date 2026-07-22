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
}
