using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public sealed class PoliceMobilePartyAIModel : DefaultMobilePartyAIModel
    {
        public override bool ShouldConsiderAttacking(MobileParty party, MobileParty targetParty)
        {
            if (!base.ShouldConsiderAttacking(party, targetParty))
                return false;

            if (!IsSuppressibleCivilianTarget(targetParty))
                return true;

            float multiplier = GwpAiDeterrenceState.GetCrimeDesireMultiplier(party);
            if (multiplier <= 0f)
                return false;

            if (multiplier >= 0.999f)
                return true;

            return RollCivilianAttackAllowance(party, targetParty, multiplier);
        }

        private static bool IsSuppressibleCivilianTarget(MobileParty? targetParty)
        {
            return targetParty != null
                   && targetParty.IsActive
                   && !targetParty.IsMainParty
                   && (targetParty.IsVillager || targetParty.IsCaravan);
        }

        private static bool RollCivilianAttackAllowance(MobileParty party, MobileParty targetParty, float multiplier)
        {
            if (Campaign.Current == null)
                return true;

            long timeBucket = (long)Math.Floor(
                CampaignTime.Now.ToHours / Campaign.Current.Models.MobilePartyAIModel.AiCheckInterval);

            string attackerId = party.StringId
                                ?? party.LeaderHero?.StringId
                                ?? "unknown_attacker";
            string targetId = targetParty.StringId
                              ?? targetParty.LeaderHero?.StringId
                              ?? "unknown_target";

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(attackerId);
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(targetId);
                hash = hash * 31 + timeBucket.GetHashCode();

                float normalized = ((uint)hash & 0x00FFFFFF) / 16777215f;
                return normalized < multiplier;
            }
        }
    }
}
