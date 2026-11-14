using GreyWardenPolicePurity.Core.Troops;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.Troops
{
    public class WardenReputationRulesTests
    {
        [Theory]
        [InlineData(-7, 0)]
        [InlineData(0, 0)]
        [InlineData(24, 48)]
        public void CapacityUsesCurrentStanding(int reputation, int expected)
        {
            Assert.Equal(expected, WardenReputationRules.Capacity(reputation));
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
        public void TroopValueUsesTier(int tier, int units)
        {
            Assert.Equal(units, WardenReputationRules.TroopUnits(tier));
        }

        [Theory]
        [InlineData(1, 20, 20, 0, 4, 16, true)]
        [InlineData(3, 10, 20, 18, 6, 14, true)]
        [InlineData(3, 10, 20, 19, 6, 14, false)]
        [InlineData(6, 10, 60, 0, 16, 44, true)]
        [InlineData(6, 40, 40, 0, 64, -24, false)]
        [InlineData(6, 40, 64, 0, 64, 0, false)]
        public void OrderQuotesUseRemainingStandingForCapacity(int tier, int count, int reputation, int troops,
            int cost, int balance, bool hasRoom)
        {
            Assert.Equal((cost, balance, hasRoom), WardenReputationRules.QuoteOrder(tier, count, reputation, troops));
        }

        [Theory]
        [InlineData(12, 5, 0)]
        [InlineData(10, 5, 0)]
        [InlineData(7, 5, 3)]
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
