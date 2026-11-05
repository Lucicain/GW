using System.IO;
using System.Linq;
using GreyWardenPolicePurity.Core.CitySupport;
using Xunit;

namespace GreyWardenPolicePurity.Core.Tests.CitySupport
{
    public class CityTeamRulesTests
    {
        /// <summary>一支正常在路上、目标住城可接触、独立办理的队。</summary>
        private static CityTeamView Travelling() => new CityTeamView
        {
            TeamId = "team", PartyExists = true, CaseOpen = true, TargetAvailable = true,
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
        public void UnreachableTargetKeepsTheTeamWithoutSuccessOrRedispatch()
        {
            var view = Travelling();
            view.ShelterKnown = false;
            view.Holding = true;
            Assert.Equal(new CityTeamDecision(CityTeamAction.Wait, rethink: true), Decide(view));
        }

        [Fact]
        public void WarWithTheShelterReturns()
        {
            var view = Travelling();
            view.AtWarWithShelter = true;
            view.ArrivedAtShelter = true;
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

        [Fact]
        public void RecordRoundTripsEveryField()
        {
            var view = new CityTeamView
            {
                TeamId = "gw_city_7", UsedByActiveSearchScene = true, PartyExists = true, CaseOpen = true,
                TargetAvailable = true, InBattle = true, HealthyMembers = 3, Holding = true, ForPlayer = true,
                PlayerHoldsCase = true, ShelterKnown = true, AtWarWithShelter = true, ArrivedAtShelter = true,
            };
            Assert.Equal(view.ToRecord(), CityTeamView.FromRecord(view.ToRecord()).ToRecord());
            var decision = new CityTeamDecision(CityTeamAction.GoToShelter, rethink: true, dropPlayerLink: true);
            Assert.Equal(decision, CityTeamDecision.FromRecord(decision.ToRecord()));
        }

        /// <summary>
        /// 回放真实对局：每行是「局面 | 旧代码当时的决定」，新逻辑必须给出同样的决定。
        /// 录像由诊断版的影子运行写出（CITY_TEAM_SHADOW），见 docs/state/code-health.md「分层架构」。
        /// </summary>
        [Fact]
        public void ReplayRecordedGamesMatchTheOldDecisions()
        {
            string dir = Path.Combine(System.AppContext.BaseDirectory, "Replay", "CitySupport");
            if (!Directory.Exists(dir)) return;
            var cases = Directory.GetFiles(dir, "*.log").SelectMany(File.ReadAllLines)
                .Where(line => line.Contains('|')).ToList();
            Assert.NotEmpty(cases);
            foreach (string line in cases)
            {
                string[] parts = line.Split('|');
                var expected = CityTeamDecision.FromRecord(parts[1].Trim());
                Assert.True(expected.Equals(CityTeamRules.Decide(CityTeamView.FromRecord(parts[0].Trim()))), line);
            }
        }
    }
}
