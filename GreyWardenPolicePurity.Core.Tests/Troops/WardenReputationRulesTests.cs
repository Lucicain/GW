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
