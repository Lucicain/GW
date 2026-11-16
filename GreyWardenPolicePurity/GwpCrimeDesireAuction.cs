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

                if (target.PartyComponent is not CaravanPartyComponent &&
                    target.PartyComponent is not VillagerPartyComponent)
                    continue;

                float multiplier = GwpAiDeterrenceState.GetCrimeDesireMultiplier(offender);

                if (multiplier >= 0.9999f) continue;
                AIBehaviorData candidate = behavior;
                think.SetBehaviorScore(in candidate, score * multiplier);
            }
        }
    }
}
