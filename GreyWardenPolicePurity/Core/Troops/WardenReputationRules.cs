using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class WardenReputationRules
    {
        public const int UpgradesPerPoint = 5;
        public const int ReputationUnitsPerPoint = 5;
        public const int TroopWeightPerPoint = 20;
        public const int TroopBattleRewardDivisor = 4;
        public const int BattleWeightUnitsPerPoint = TroopWeightPerPoint * TroopBattleRewardDivisor;
        public const int TroopsPerReputation = 2;

        public static int CapacityFromUnits(int units) => Math.Max(0, units) * TroopsPerReputation / ReputationUnitsPerPoint;

        public static int WholePointsFromUnits(int units) => (int)Math.Floor(units / (decimal)ReputationUnitsPerPoint);

        public static int RemainingSlots(int troops, int units) => Math.Max(0, CapacityFromUnits(units) - troops);

        public static int TroopRewardWeight(int tier) => Math.Max(1, Math.Min(6, tier));

        public static int BattleRewardWeightUnits(int personalWeight, int mainPartyWeight) =>
            personalWeight * TroopBattleRewardDivisor + mainPartyWeight;

        public static (int ReputationUnits, int Remainder) QuoteRewardProgress(int previousProgress,
            long earnedProgress, int progressPerPoint, int remainingReputationUnits)
        {
            long total = previousProgress + earnedProgress;
            return ((int)Math.Min(total / progressPerPoint * ReputationUnitsPerPoint, remainingReputationUnits),
                (int)(total % progressPerPoint));
        }

        public static int TroopOrderCostUnits(int tier) => tier switch
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
