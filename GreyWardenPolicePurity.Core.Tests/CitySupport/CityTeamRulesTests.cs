using GreyWardenPolicePurity.Core.CitySupport;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.CitySupport
{
    public class CityTeamRulesTests
    {
        /// <summary>一支正常在路上、目标住城可接触、独立办理的队。</summary>
        private static CityTeamView Travelling() => new CityTeamView
        {
            PartyExists = true, CaseOpen = true, TargetAvailable = true,
            HealthyMembers = 5, ShelterKnown = true,
        };

        private static CityTeamDecision Decide(CityTeamView view) => CityTeamRules.Decide(view);

        [Fact]
        public void SceneKeepsItsTeamAndEndsWhenTheTeamIsGone()
        {
            var view = Travelling();
            view.UsedByActiveSearchScene = true;
            view.CaseOpen = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Wait), Decide(view));
            view.PartyExists = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.FinishSearchScene), Decide(view));
        }

        [Fact]
        public void ClosedCaseOrUnavailableTargetReturns()
        {
            var closed = Travelling();
            closed.CaseOpen = false;
            Assert.Equal(CityTeamAction.Return, Decide(closed).Action);
            var captured = Travelling();
            captured.TargetAvailable = false;
            Assert.Equal(CityTeamAction.Return, Decide(captured).Action);
        }

        [Fact]
        public void BattleWaitsEvenWhenNoOneIsHealthy()
        {
            // 2026-10-28 的 bug：路上被打光后，战斗结束前不能抢先补派或返程。
            var view = Travelling();
            view.InBattle = true;
            view.HealthyMembers = 0;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Wait), Decide(view));
        }

        [Fact]
        public void LostOrWoundedTeamIsReplacedInsteadOfReturning()
        {
            // 2026-10-28 的 bug：失队曾被当成任务完成，之后再也没人补派。
            var gone = Travelling();
            gone.PartyExists = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Replace), Decide(gone));
            var wounded = Travelling();
            wounded.HealthyMembers = 0;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Replace), Decide(wounded));
        }

        [Fact]
        public void PlayerTeamFollowsWhileThePlayerHoldsTheCase()
        {
            var view = Travelling();
            view.ForPlayer = true;
            view.PlayerHoldsCase = true;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.EscortPlayer, rethink: true), Decide(view));
        }

        [Fact]
        public void PlayerTeamBecomesIndependentOnceThePlayerLetsGo()
        {
            var view = Travelling();
            view.ForPlayer = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.GoToShelter, dropPlayerLink: true), Decide(view));
        }

        [Fact]
        public void MissingShelterReturnsWithoutSuccessOrRedispatch()
        {
            var view = Travelling();
            view.ShelterKnown = false;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Return), Decide(view));
        }

        [Fact]
        public void WarWithTheShelterReturns()
        {
            var view = Travelling();
            view.AtWarWithShelter = true;
            view.ArrivedAtShelter = true;
            Assert.Equal(CityTeamAction.Return, Decide(view).Action);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IndependentTeamReleasesHostileShelterEvenWithNoHealthyMembers(bool partyExists)
        {
            var view = Travelling();
            view.PartyExists = partyExists;
            view.HealthyMembers = 0;
            view.AtWarWithShelter = true;
            Assert.Equal(CityTeamAction.Return, Decide(view).Action);
        }

        [Fact]
        public void TravelsUntilArrivalThenSettles()
        {
            var view = Travelling();
            Assert.Equal(new CityTeamDecision(CityTeamAction.GoToShelter), Decide(view));
            view.ArrivedAtShelter = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Settle), Decide(view));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void InvalidCaseReturnsWithoutReplacingOrChangingPlayerAssignment(bool open, bool available)
        {
            var view = Travelling();
            view.CaseOpen = open;
            view.TargetAvailable = available;
            view.PartyExists = false;
            view.ForPlayer = true;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Return), Decide(view));
        }

        [Fact]
        public void LostPlayerTeamKeepsItsAssignmentUntilReplacement()
        {
            var view = Travelling();
            view.PartyExists = false;
            view.ForPlayer = true;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Replace), Decide(view));
        }

        [Fact]
        public void PlayerEscortTakesPriorityOverIndependentSettlement()
        {
            var view = Travelling();
            view.ForPlayer = true;
            view.PlayerHoldsCase = true;
            view.AtWarWithShelter = true;
            view.ArrivedAtShelter = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.EscortPlayer), Decide(view));
        }

        [Fact]
        public void PlayerEscortKeepsFollowingWhileTheTargetHasNoShelter()
        {
            // 审查发现：留城改动曾让玩家持案时的随行队因目标暂离城而返程。
            var view = Travelling();
            view.ForPlayer = true;
            view.PlayerHoldsCase = true;
            view.ShelterKnown = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.EscortPlayer), Decide(view));
            view.PartyExists = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Replace), Decide(view));
        }

        [Fact]
        public void ReleasedPlayerLinkReturnsWhenTheTargetHasNoShelter()
        {
            var view = Travelling();
            view.ForPlayer = true;
            view.ShelterKnown = false;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Return), Decide(view));
        }

        [Fact]
        public void LostTeamIsNotReplacedWhenTheTargetHasNoShelter()
        {
            var view = Travelling();
            view.PartyExists = false;
            view.ShelterKnown = false;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Return), Decide(view));
        }
    }
}
