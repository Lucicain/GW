using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class WardenReputationRules
    {
        public const int UpgradesPerPoint = 5;
        public const int TroopUnitsPerPoint = 5;
        public const int TroopsPerReputation = 2;

        public static int CapacityFromUnits(int units) => Math.Max(0, units) * TroopsPerReputation / TroopUnitsPerPoint;

        public static int WholePointsFromUnits(int units) => (int)Math.Floor(units / (decimal)TroopUnitsPerPoint);

        public static int RemainingSlots(int troops, int units) => Math.Max(0, CapacityFromUnits(units) - troops);

        public static int TroopUnits(int tier) => tier switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 6,
            _ => 8
        };

        public static (int Balance, bool HasRoom) QuoteOrder(int cost, int count, int reputationUnits, int troops)
        {
            int balance = reputationUnits - cost;
            return (balance, count <= RemainingSlots(troops, balance));
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
