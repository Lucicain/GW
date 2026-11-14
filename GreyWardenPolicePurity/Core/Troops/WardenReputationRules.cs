using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class WardenReputationRules
    {
        public const int UpgradesPerPoint = 5;
        public const int TroopUnitsPerPoint = 5;
        private const int TroopsPerReputation = 2;

        public static int Capacity(int reputation) => Math.Max(0, reputation) * TroopsPerReputation;

        public static int RemainingSlots(int troops, int reputation) => Math.Max(0, Capacity(reputation) - troops);

        public static int TroopUnits(int tier) => tier switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 6,
            _ => 8
        };

        public static (int Cost, int Balance, bool HasRoom) QuoteOrder(int tier, int count, int reputation, int troops)
        {
            // All order sizes are multiples of five, so the unit prices settle in whole points.
            int cost = count * TroopUnits(tier) / TroopUnitsPerPoint;
            int balance = reputation - cost;
            return (cost, balance, count <= RemainingSlots(troops, balance));
        }

        public static int UpgradeCost(int previousProgress, int upgrades) =>
            (previousProgress + upgrades) / UpgradesPerPoint;

        public static int UpgradeProgress(int previousProgress, int upgrades) =>
            (previousProgress + upgrades) % UpgradesPerPoint;

        // The final point pays for the end of its group. A batch cannot advance
        // into the next group when the remaining balance reaches zero.
        public static int AffordableUpgrades(int reputation, int previousProgress) =>
            Math.Max(0, reputation * UpgradesPerPoint - previousProgress);
    }
}
