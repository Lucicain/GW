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

        public static int UnitsPerPoint(FireswornTradeKind kind) => kind switch
        {
            FireswornTradeKind.Prisoners or FireswornTradeKind.Troops => WardenReputationRules.TroopUnitsPerPoint,
            FireswornTradeKind.Armory => 2000,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static (int ReputationUnits, int Remainder) Quote(FireswornTradeKind kind, int previousProgress,
            long offeredUnits, int remainingReputationUnits)
        {
            int rate = UnitsPerPoint(kind);
            long total = previousProgress + offeredUnits;
            return ((int)Math.Min(total / rate * WardenReputationRules.TroopUnitsPerPoint, remainingReputationUnits),
                (int)(total % rate));
        }
    }
}
