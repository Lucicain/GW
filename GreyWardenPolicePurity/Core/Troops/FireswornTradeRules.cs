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

        public static bool IsAvailable(FireswornTradeKind kind, int reputation) =>
            kind == FireswornTradeKind.Prisoners || reputation > AdvancedTradeThreshold;

        public static int UnitsPerPoint(FireswornTradeKind kind) => kind switch
        {
            FireswornTradeKind.Prisoners => 5,
            FireswornTradeKind.Troops => 5,
            FireswornTradeKind.Armory => 2000,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static int TroopUnits(int tier) => tier switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 6,
            _ => 8
        };

        public static (int Points, int Remainder) Quote(FireswornTradeKind kind, int previousProgress,
            long offeredUnits, int remainingReputation)
        {
            int rate = UnitsPerPoint(kind);
            long total = previousProgress + offeredUnits;
            return ((int)Math.Min(total / rate, remainingReputation), (int)(total % rate));
        }
    }
}
