using System;
using GreyWardenPolicePurity.Core.Negotiation;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.Negotiation
{
    public class NegotiationPolicyTests
    {
        [Fact]
        public void NeutralSolventOffenderKeepsExistingProposalWeights()
        {
            Assert.Equal(new[] { 5, 3, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0 },
                GwpNegotiationPolicy.Weights(0, 0, 0, 0, 0, 1000, 1000, true, true, false, false));
        }

        [Fact]
        public void GenerosityFeedsItsOwnProposalInsteadOfPlainFullPayment()
        {
            // 用户裁定 2026-11-06：慷慨高单独成一种打算（按罚金 120% 交），不再并进照数全缴。
            int[] weights = GwpNegotiationPolicy.Weights(0, 0, 0, 0, 2, 1200, 1000, true, true, false, false);
            Assert.Equal(5, weights[0]);
            Assert.Equal(8, weights[8]);
        }

        [Theory]
        [InlineData(1199, 0)]
        [InlineData(1200, 8)]
        public void GenerousNeedsTheExtraFifthOnHand(int gold, int expected)
        {
            Assert.Equal(expected, GwpNegotiationPolicy.Weights(0, 0, 0, 0, 2, gold, 1000, true, true, false, false)[8]);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 8)]
        public void RashNeedsSomethingToHandOver(int gold, int expected)
        {
            Assert.Equal(expected, GwpNegotiationPolicy.Weights(0, 0, 0, -2, 0, gold, 1000, true, true, false, false)[9]);
        }

        [Theory]
        [InlineData(true, 1000, 10, 5)]   // 有村可修、交得起：仁慈整份移到修村。
        [InlineData(false, 1000, 0, 15)]  // 没有可修的村：仁慈仍落在照数全缴。
        [InlineData(true, 999, 0, 15)]    // 交不起罚金不答应修村（罚金照交是前提）。
        public void MercyRepairsTheVillageOnlyWhenItCanAlsoPayTheFine(bool canRepair, int gold, int repair, int full)
        {
            // 用户裁定 2026-11-06、2026-11-07：仁慈高罚金照交，再带人去修本国的废村。
            int[] weights = GwpNegotiationPolicy.Weights(0, 2, 0, 0, 0, gold, 1000, true, true, canRepair, false);
            Assert.Equal(repair, weights[10]);
            Assert.Equal(full, weights[0]);
        }

        [Theory]
        [InlineData(true, true, 6, 5)]    // 有城可躲、能借请宽限脱身：胆怯整份移到躲城。
        [InlineData(false, true, 0, 11)]  // 本国没城可躲：胆怯仍落在照数全缴。
        [InlineData(true, false, 0, 11)]  // 当下不能请宽限，就借不了这个口实。
        public void CowardHidesInTownOnlyWhenHeCanPretendToAskForGrace(bool canHide, bool canGrace, int hide, int full)
        {
            // 用户裁定 2026-11-06：勇武低带着部队躲进本国的城，玩家当面时嘴上走请宽限。
            int[] weights = GwpNegotiationPolicy.Weights(0, 0, -2, 0, 0, 1000, 1000, true, canGrace, false, canHide);
            Assert.Equal(hide, weights[11]);
            Assert.Equal(full, weights[0]);
        }

        [Theory]
        [InlineData(0, 50)]
        [InlineData(50, 100)]
        [InlineData(100, 150)]
        public void RashShareCoversFiftyToOneHundredFiftyPercentInclusive(int roll, int expected)
        {
            Assert.Equal(101, GwpNegotiationPolicy.RashShareRollSpan);
            Assert.Equal(expected, GwpNegotiationPolicy.RashSharePercent(roll));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void RashShareRollOutsideTheSpanIsRejected(int roll)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GwpNegotiationPolicy.RashSharePercent(roll));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 2)]
        [InlineData(4, 2)]
        [InlineData(5, 4)]
        public void ZeroWeightProposalsAreSkippedAtBothIntervalEdges(int roll, int expected)
        {
            Assert.Equal(expected, GwpNegotiationPolicy.SelectDesireIndex(new[] { 2, 0, 3, 0, 1 }, roll));
        }

        [Fact]
        public void EveryLegalRollMatchesTheActualWeightWithoutMutatingInputs()
        {
            // 遍历正常性格范围。以整个抽签空间的次数守恒核对区间，避免只测选中的几个例子。
            for (int honor = -2; honor <= 2; honor++)
            for (int mercy = -2; mercy <= 2; mercy++)
            for (int valor = -2; valor <= 2; valor++)
            for (int calculating = -2; calculating <= 2; calculating++)
            for (int generosity = -2; generosity <= 2; generosity++)
            foreach (bool canRepair in new[] { false, true })
            {
                int[] weights = GwpNegotiationPolicy.Weights(honor, mercy, valor, calculating,
                    generosity, 1000, 1000, true, true, canRepair, canRepair);
                int[] before = (int[])weights.Clone();
                int total = 0;
                foreach (int weight in weights)
                {
                    Assert.True(weight >= 0);
                    total += weight;
                }
                Assert.True(total > 0);
                var selected = new int[weights.Length];
                for (int roll = 0; roll < total; roll++)
                    selected[GwpNegotiationPolicy.SelectDesireIndex(weights, roll)]++;
                Assert.Equal(before, weights);
                Assert.Equal(weights, selected);
            }
        }

        [Theory]
        [InlineData(0, 1000)]
        [InlineData(999, 1000)]
        [InlineData(1000, 1000)]
        public void UnavailableProposalsNeverEnterThePool(int gold, int fine)
        {
            int[] weights = GwpNegotiationPolicy.Weights(1, -2, 2, 2, -2, gold, fine, false, false, false, false);
            Assert.Equal(0, weights[2]); // 有荣誉的人不骗一道。
            Assert.Equal(0, weights[4]); // 没有兵不能拿兵出气。
            Assert.Equal(0, weights[5]); // 有荣誉的人不行贿。
            Assert.Equal(0, weights[7]); // 当前条件不能求宽限。
            if (gold == 0) Assert.Equal(0, weights[1]);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        public void InvalidRollIsReportedInsteadOfInventingFullPayment(int roll)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                GwpNegotiationPolicy.SelectDesireIndex(new[] { 2, 0, 3, 0, 1 }, roll));
        }
    }
}
