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
        public void FivePrisonersEarnOnePointAcrossDeliveries()
        {
            var first = FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, 0, 3, 100);
            Assert.Equal((0, 3), first);
            Assert.Equal((1, 0), FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, first.Remainder, 2, 100));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(5, 6)]
        [InlineData(6, 8)]
        [InlineData(7, 8)]
        public void SoldierValueUsesTier(int tier, int units)
        {
            Assert.Equal(units, FireswornTradeRules.TroopUnits(tier));
        }

        [Fact]
        public void HigherTierSoldiersAndRemainderCombineInOneDelivery()
        {
            int units = FireswornTradeRules.TroopUnits(1) + FireswornTradeRules.TroopUnits(6);
            Assert.Equal((2, 1), FireswornTradeRules.Quote(FireswornTradeKind.Troops, 2, units, 70));
        }

        [Theory]
        [InlineData(1450, 799, 70, 1, 249)]
        [InlineData(1450, 799, 0, 0, 249)]
        [InlineData(500, 3200000800L, 1, 1, 1300)]
        public void ArmorySalesCarryRemainderAndRespectReputationLimit(int previous, long value,
            int room, int gained, int remainder)
        {
            Assert.Equal((gained, remainder), FireswornTradeRules.Quote(FireswornTradeKind.Armory, previous, value, room));
        }
    }
}
