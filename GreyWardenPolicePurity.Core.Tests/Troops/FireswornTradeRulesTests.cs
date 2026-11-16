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
            int firstPrisoners = 2 * WardenReputationRules.TroopRewardWeight(6) +
                WardenReputationRules.TroopRewardWeight(4) + WardenReputationRules.TroopRewardWeight(2);
            var first = FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, 0, firstPrisoners, 100);
            Assert.Equal((0, 18), first);
            Assert.Equal((5, 1), FireswornTradeRules.Quote(FireswornTradeKind.Prisoners, first.Remainder,
                WardenReputationRules.TroopRewardWeight(3), 100));
        }

        [Fact]
        public void HigherTierSoldiersAndRemainderCombineInOneDelivery()
        {
            int weight = 2 * WardenReputationRules.TroopRewardWeight(6) +
                WardenReputationRules.TroopRewardWeight(5) + WardenReputationRules.TroopRewardWeight(1);
            Assert.Equal((5, 0), FireswornTradeRules.Quote(FireswornTradeKind.Troops, 2, weight, 70));
        }

        [Theory]
        [InlineData(150, 149, 70, 0, 299)]
        [InlineData(150, 150, 70, 5, 0)]
        [InlineData(250, 101, 0, 0, 51)]
        [InlineData(1450, 50, 70, 25, 0)]
        [InlineData(155, 3200000800L, 1, 1, 255)]
        public void ArmorySalesCarryRemainderAndRespectReputationLimit(int previous, long value,
            int room, int gained, int remainder)
        {
            Assert.Equal((gained, remainder), FireswornTradeRules.Quote(FireswornTradeKind.Armory, previous, value, room));
        }
    }
}
