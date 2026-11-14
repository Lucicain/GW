using GreyWardenPolicePurity.Core.Troops;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.Troops
{
    public class TroopTransferRulesTests
    {
        [Theory]
        [InlineData(38, 3, 2, 100, 24)]
        [InlineData(299, 3, 2, 100, 199)]
        [InlineData(299, 3, 3, 100, 299)]
        public void SelectedBatchTakesExperienceAboveRemainingCapacity(int total, int count, int moving,
            int capacityPerTroop, int expected)
        {
            Assert.Equal(expected, TroopTransferRules.SelectedExperience(total, count, moving, capacityPerTroop));
        }
    }
}
