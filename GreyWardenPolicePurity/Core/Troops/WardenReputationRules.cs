using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class WardenReputationRules
    {
        public const int UpgradesPerPoint = 5;
        private const int TroopsPerReputation = 2;

        public static int Capacity(int reputation) => Math.Max(0, reputation) * TroopsPerReputation;

        public static int RemainingSlots(int troops, int reputation) => Math.Max(0, Capacity(reputation) - troops);

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
