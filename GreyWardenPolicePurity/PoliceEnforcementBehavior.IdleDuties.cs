using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        private const int IdlePoliceResupplyFoodDaysThreshold = 6;
        private const float ReadyPoliceMinPartySizeRatioForPatrol = 0.85f;
        private const float ReadyPoliceMaxWoundedRatioForPatrol = 0.2f;
        private const float ReadyPolicePatrolScoreMultiplier = 1.12f;
        private const float ReadyPoliceCurrentSettlementVisitMultiplier = 0.55f;
        private const float ReadyPolicePatrolAdvantageRatio = 1.15f;

        private void UpdateIdlePoliceDuties()
        {
            foreach (MobileParty police in PoliceStats.GetAllPoliceParties())
            {
                if (!ShouldManageIdlePoliceResupply(police))
                    continue;

                PoliceResourceManager.StartResupply(police);
                if (police.CurrentSettlement == null)
                    PoliceResourceManager.ForceImmediateMoveToResupply(police);
            }
        }

        private void OnAiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
        {
            if (mobileParty == null || p == null)
                return;

            float illegalActionMultiplier = GetIllegalActionMultiplier(mobileParty);
            if (illegalActionMultiplier < 0.999f)
                AdjustIllegalEngagementScores(p, illegalActionMultiplier);

            if (!ShouldRebalanceIdlePolicePatrolScores(mobileParty))
                return;

            RebalanceIdlePolicePatrolScores(mobileParty, p);
        }

        private bool ShouldManageIdlePoliceResupply(MobileParty? police)
        {
            if (police == null || !police.IsActive) return false;
            if (GwpCommon.IsPatrolParty(police) || GwpCommon.IsEnforcementDelayPatrolParty(police)) return false;
            if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(police)) return false;
            if (police.LeaderHero == null || !police.LeaderHero.IsActive) return false;
            if (police.MapEvent != null && !police.MapEvent.IsFinalized) return false;
            if (police.Army != null) return false;
            if (PoliceResourceManager.IsResupplying(police)) return false;

            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
                return false;

            return police.GetNumDaysForFoodToLast() <= IdlePoliceResupplyFoodDaysThreshold;
        }

        private static float GetIllegalActionMultiplier(MobileParty? party)
        {
            if (party == null || !party.IsActive)
                return 1f;

            if (party.IsMainParty || party.LeaderHero == null || !party.LeaderHero.IsActive)
                return 1f;

            return GwpAiDeterrenceState.GetCrimeDesireMultiplier(party);
        }

        private bool ShouldRebalanceIdlePolicePatrolScores(MobileParty? police)
        {
            if (police == null || !police.IsActive) return false;
            if (!IsFormalPoliceParty(police)) return false;
            if (GwpCommon.IsPatrolParty(police) || GwpCommon.IsEnforcementDelayPatrolParty(police)) return false;
            if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(police)) return false;
            if (police.LeaderHero == null || !police.LeaderHero.IsActive) return false;
            if (police.MapEvent != null && !police.MapEvent.IsFinalized) return false;
            if (police.CurrentSettlement?.SiegeEvent != null) return false;
            if (police.Army != null) return false;
            if (PoliceResourceManager.IsResupplying(police)) return false;
            if (CrimeState.GetTask(police.StringId) != null) return false;

            return IsPoliceReadyForPatrolDeployment(police);
        }

        private void AdjustIllegalEngagementScores(PartyThinkParams p, float multiplier)
        {
            List<(AIBehaviorData behavior, float score)> scoreUpdates = new List<(AIBehaviorData behavior, float score)>();

            foreach ((AIBehaviorData behavior, float score) in p.AIBehaviorScores)
            {
                if (behavior.AiBehavior != AiBehavior.GoAroundParty || behavior.Party is not MobileParty target)
                    continue;

                if (score > 0f && ShouldSuppressPoliceIllegalTarget(target))
                    scoreUpdates.Add((behavior, score * multiplier));
            }

            foreach ((AIBehaviorData behavior, float score) in scoreUpdates)
                p.SetBehaviorScore(in behavior, score);
        }

        private void RebalanceIdlePolicePatrolScores(MobileParty police, PartyThinkParams p)
        {
            Settlement? currentSettlement = police.CurrentSettlement;
            List<(AIBehaviorData behavior, float score)> patrolUpdates = new List<(AIBehaviorData behavior, float score)>();
            List<(AIBehaviorData behavior, float score)> visitUpdates = new List<(AIBehaviorData behavior, float score)>();

            AIBehaviorData strongestPatrolBehavior = default;
            float strongestPatrolScore = 0f;
            bool hasPatrol = false;

            AIBehaviorData currentSettlementVisitBehavior = default;
            float currentSettlementVisitScore = 0f;
            bool hasCurrentSettlementVisit = false;

            foreach ((AIBehaviorData behavior, float score) in p.AIBehaviorScores)
            {
                if (score <= 0f)
                    continue;

                if (behavior.AiBehavior == AiBehavior.PatrolAroundPoint)
                {
                    float boostedScore = score * ReadyPolicePatrolScoreMultiplier;
                    patrolUpdates.Add((behavior, boostedScore));

                    if (!hasPatrol || boostedScore > strongestPatrolScore)
                    {
                        strongestPatrolBehavior = behavior;
                        strongestPatrolScore = boostedScore;
                        hasPatrol = true;
                    }

                    continue;
                }

                if (currentSettlement != null &&
                    behavior.AiBehavior == AiBehavior.GoToSettlement &&
                    behavior.Party is Settlement settlement &&
                    settlement == currentSettlement)
                {
                    float dampedScore = score * ReadyPoliceCurrentSettlementVisitMultiplier;
                    visitUpdates.Add((behavior, dampedScore));

                    if (!hasCurrentSettlementVisit || dampedScore > currentSettlementVisitScore)
                    {
                        currentSettlementVisitBehavior = behavior;
                        currentSettlementVisitScore = dampedScore;
                        hasCurrentSettlementVisit = true;
                    }
                }
            }

            foreach ((AIBehaviorData behavior, float score) in patrolUpdates)
                p.SetBehaviorScore(in behavior, score);

            foreach ((AIBehaviorData behavior, float score) in visitUpdates)
                p.SetBehaviorScore(in behavior, score);

            if (hasPatrol && hasCurrentSettlementVisit && strongestPatrolScore <= currentSettlementVisitScore)
            {
                p.SetBehaviorScore(
                    in strongestPatrolBehavior,
                    currentSettlementVisitScore * ReadyPolicePatrolAdvantageRatio);
            }
        }

        private static bool ShouldSuppressPoliceIllegalTarget(MobileParty? target)
        {
            return target != null
                   && target.IsActive
                   && !target.IsMainParty
                   && (target.IsVillager || target.IsCaravan);
        }

        private static bool IsFormalPoliceParty(MobileParty? police)
        {
            return police != null &&
                   string.Equals(
                       police.ActualClan?.StringId,
                       PoliceStats.PoliceClanId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPoliceReadyForPatrolDeployment(MobileParty police)
        {
            if (police.GetNumDaysForFoodToLast() <= IdlePoliceResupplyFoodDaysThreshold)
                return false;

            int totalMembers = Math.Max(1, police.Party.NumberOfAllMembers);
            float woundedRatio = (float)police.MemberRoster.TotalWounded / totalMembers;
            if (woundedRatio > ReadyPoliceMaxWoundedRatioForPatrol)
                return false;

            return police.PartySizeRatio >= ReadyPoliceMinPartySizeRatioForPatrol;
        }
    }
}
