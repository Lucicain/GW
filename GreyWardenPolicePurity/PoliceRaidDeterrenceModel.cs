using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 在原版劫掠评分之上追加“灰袍执法后的收敛期”修正。
    /// 只影响 Raider 目标，不干扰普通 VisitSettlement（招兵、补粮、入城出售）路径。
    /// </summary>
    public sealed class PoliceRaidDeterrenceModel : DefaultTargetScoreCalculatingModel
    {
        public override float GetTargetScoreForFaction(
            Settlement targetSettlement,
            Army.ArmyTypes missionType,
            MobileParty mobileParty,
            float ourStrength)
        {
            float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);
            if (missionType != Army.ArmyTypes.Raider || baseScore <= 0f || mobileParty?.LeaderHero == null)
                return baseScore;

            float multiplier = GwpAiDeterrenceState.GetRaidScoreMultiplier(mobileParty);
            return baseScore * multiplier;
        }
    }
}
