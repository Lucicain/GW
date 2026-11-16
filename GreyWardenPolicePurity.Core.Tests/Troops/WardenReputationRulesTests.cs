using GreyWardenPolicePurity.Core.Troops;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.Troops
{
    public class WardenReputationRulesTests
    {
        [Theory]
        [InlineData(-7, 0)]
        [InlineData(0, 0)]
        [InlineData(120, 48)]
        [InlineData(123, 49)]
        public void CapacityUsesCurrentStanding(int units, int expected)
        {
            Assert.Equal(expected, WardenReputationRules.CapacityFromUnits(units));
        }

        [Theory]
        [InlineData(-54, -11)]
        [InlineData(-1, -1)]
        [InlineData(0, 0)]
        [InlineData(99, 19)]
        [InlineData(100, 20)]
        public void FractionalBalancePreservesWholePointThresholds(int units, int wholePoints)
        {
            Assert.Equal(wholePoints, WardenReputationRules.WholePointsFromUnits(units));
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
        public void OrderCostUsesLogisticalTierRates(int tier, int units)
        {
            Assert.Equal(units, WardenReputationRules.TroopOrderCostUnits(tier));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(5, 5)]
        [InlineData(6, 6)]
        [InlineData(7, 6)]
        public void PersonnelRewardsUseOneThroughSix(int tier, int weight)
        {
            Assert.Equal(weight, WardenReputationRules.TroopRewardWeight(tier));
        }

        [Fact]
        public void PersonalDefeatsCarryWeightAcrossVictories()
        {
            const int rate = WardenReputationRules.BattleWeightUnitsPerPoint;
            int personal = 2 * WardenReputationRules.TroopRewardWeight(6) + WardenReputationRules.TroopRewardWeight(5);
            var first = WardenReputationRules.QuoteRewardProgress(0,
                WardenReputationRules.BattleRewardWeightUnits(personal, 0), rate, 50);
            Assert.Equal((0, 68), first);
            var second = WardenReputationRules.QuoteRewardProgress(first.Remainder,
                WardenReputationRules.BattleRewardWeightUnits(WardenReputationRules.TroopRewardWeight(4), 0), rate, 50);
            Assert.Equal((5, 4), second);
        }

        [Fact]
        public void MainPartyDefeatsCarryExactQuarterWeightAcrossVictories()
        {
            const int rate = WardenReputationRules.BattleWeightUnitsPerPoint;
            var first = WardenReputationRules.QuoteRewardProgress(0,
                WardenReputationRules.BattleRewardWeightUnits(17, 11), rate, 50);
            Assert.Equal((0, 79), first);
            var second = WardenReputationRules.QuoteRewardProgress(first.Remainder,
                WardenReputationRules.BattleRewardWeightUnits(0, 2), rate, 50);
            Assert.Equal((5, 1), second);
        }

        [Theory]
        [InlineData(1, 20, 100, 0, 80, true)]
        [InlineData(3, 10, 100, 18, 70, true)]
        [InlineData(3, 10, 100, 19, 70, false)]
        [InlineData(6, 10, 300, 0, 220, true)]
        [InlineData(6, 40, 200, 0, -120, false)]
        [InlineData(6, 40, 320, 0, 0, false)]
        public void OrderQuotesUseRemainingStandingForCapacity(int tier, int count, int reputation, int troops,
            int balance, bool hasRoom)
        {
            Assert.Equal((balance, hasRoom), WardenReputationRules.QuoteOrder(
                WardenReputationRules.TroopOrderCostUnits(tier) * count, count, reputation, troops));
        }

        [Fact]
        public void MixedOrderUsesExactCostAndFractionalCapacity()
        {
            int units = 3 * WardenReputationRules.TroopOrderCostUnits(1) + 2 * WardenReputationRules.TroopOrderCostUnits(3);
            Assert.Equal((18, true), WardenReputationRules.QuoteOrder(units, 5, 27, 2));
            Assert.Equal((18, false), WardenReputationRules.QuoteOrder(units, 5, 27, 3));
        }

        [Fact]
        public void CompositionRoundTripsWithoutUsingDispatchFieldSeparators()
        {
            var lines = new[] { ("gwnewrecruit", 3), ("gwknight", 2) };
            string encoded = TroopOrderComposition.Encode(lines);
            Assert.DoesNotContain('|', encoded);
            Assert.DoesNotContain(';', encoded);
            Assert.Equal(lines, TroopOrderComposition.Decode(encoded));
        }

        [Theory]
        [InlineData(12, 25, 0)]
        [InlineData(10, 25, 0)]
        [InlineData(7, 25, 3)]
        public void RemainingSlotsAreZeroAtOrOverTheSoftCap(int troops, int reputation, int expected)
        {
            Assert.Equal(expected, WardenReputationRules.RemainingSlots(troops, reputation));
        }

        [Fact]
        public void SeparateUpgradesCarryRemainder()
        {
            Assert.Equal(0, WardenReputationRules.UpgradeCost(0, 3));
            int remainder = WardenReputationRules.UpgradeProgress(0, 3);
            Assert.Equal(1, WardenReputationRules.UpgradeCost(remainder, 2));
            Assert.Equal(0, WardenReputationRules.UpgradeProgress(remainder, 2));
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, 3, 0)]
        [InlineData(1, 0, 5)]
        [InlineData(1, 4, 1)]
        [InlineData(2, 3, 7)]
        public void LimitReservesEveryPointRequiredByTheWholeBatch(int reputation, int progress, int expected)
        {
            int maximum = WardenReputationRules.AffordableUpgrades(reputation, progress);
            Assert.Equal(expected, maximum);
            if (reputation <= 0) return;
            Assert.Equal(reputation, WardenReputationRules.UpgradeCost(progress, maximum));
            Assert.Equal(0, WardenReputationRules.UpgradeProgress(progress, maximum));
            Assert.Equal(0, WardenReputationRules.AffordableUpgrades(0, 0));
        }
    }
}
