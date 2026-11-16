using System;

namespace GreyWardenPolicePurity.Core.Troops
{
    public enum FireswornTradeKind
    {
        Prisoners,
        Troops,
        Armory
    }

    public static class FireswornTradeRules
    {
        public const int AdvancedTradeThreshold = 20;

        public static bool IsAvailable(FireswornTradeKind kind, decimal reputation) =>
            kind == FireswornTradeKind.Prisoners || reputation > AdvancedTradeThreshold;

        private static int ProgressPerPoint(FireswornTradeKind kind) => kind switch
        {
            FireswornTradeKind.Prisoners or FireswornTradeKind.Troops => WardenReputationRules.TroopWeightPerPoint,
            FireswornTradeKind.Armory => 2000,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static (int ReputationUnits, int Remainder) Quote(FireswornTradeKind kind, int previousProgress,
            long offeredProgress, int remainingReputationUnits) =>
            WardenReputationRules.QuoteRewardProgress(
                previousProgress, offeredProgress, ProgressPerPoint(kind), remainingReputationUnits);
    }
}
