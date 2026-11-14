using GreyWardenPolicePurity.Core.Troops;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.Troops
{
    public class FireswornTradeRulesTests
    {
        [Theory]
        [InlineData(FireswornTradeKind.Prisoners, -30, true)]
        [InlineData(FireswornTradeKind.Prisoners, 21, true)]
        [InlineData(FireswornTradeKind.Troops, 20, false)]
        [InlineData(FireswornTradeKind.Troops, 21, true)]
        [InlineData(FireswornTradeKind.Armory, 20, false)]
        [InlineData(FireswornTradeKind.Armory, 21, true)]
        public void OnlyAdvancedTradesRequireMoreThanTwentyReputation(FireswornTradeKind kind, int reputation, bool available)
        {
            Assert.Equal(available, FireswornTradeRules.IsAvailable(kind, reputation));
        }

        [Fact]
        public void FractionalStandingCanPassTheTradeThreshold()
        {
            Assert.True(FireswornTradeRules.IsAvailable(FireswornTradeKind.Armory, 20.2m));
        }

        [Fact]
        public void MixedTierPrisonersEarnStandingAcrossDeliveries()
        {
            int firstPrisoners = WardenReputationRules.TroopUnits(1) + WardenReputationRules.TroopUnits(2);
            var first = FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, 0, firstPrisoners, 100);
            Assert.Equal((0, 3), first);
            Assert.Equal((5, 1), FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, first.Remainder,
                WardenReputationRules.TroopUnits(3), 100));
        }

        [Fact]
        public void HigherTierSoldiersAndRemainderCombineInOneDelivery()
        {
            int units = WardenReputationRules.TroopUnits(1) + WardenReputationRules.TroopUnits(6);
            Assert.Equal((10, 1), FireswornTradeRules.Quote(FireswornTradeKind.Troops, 2, units, 70));
        }

        [Theory]
        [InlineData(1450, 799, 70, 5, 249)]
        [InlineData(1450, 799, 0, 0, 249)]
        [InlineData(500, 3200000800L, 1, 1, 1300)]
        public void ArmorySalesCarryRemainderAndRespectReputationLimit(int previous, long value,
            int room, int gained, int remainder)
        {
            Assert.Equal((gained, remainder), FireswornTradeRules.Quote(FireswornTradeKind.Armory, previous, value, room));
        }
    }
}
