using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GreyWardenPolicePurity
{
    internal static class GwpCrimeDesireAuction
    {
        internal static void Apply(MobileParty? offender, PartyThinkParams? think)
        {
            if (offender?.IsActive != true || offender.LeaderHero == null || think == null)
                return;

            foreach ((AIBehaviorData behavior, float score) in think.AIBehaviorScores.ToList())
            {
                if (score <= 0f || behavior.Party is not MobileParty target)
                    continue;

                float multiplier;
                if (target.PartyComponent is CaravanPartyComponent)
                    multiplier = GwpAiDeterrenceState.GetCaravanAttackScoreMultiplier(offender);
                else if (target.PartyComponent is VillagerPartyComponent)
                    multiplier = GwpAiDeterrenceState.GetVillagerAttackScoreMultiplier(offender);
                else
                    continue;

                if (multiplier >= 0.9999f) continue;
                AIBehaviorData candidate = behavior;
                think.SetBehaviorScore(in candidate, score * multiplier);
            }
        }
    }
}
