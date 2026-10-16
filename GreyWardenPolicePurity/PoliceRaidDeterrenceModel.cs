using System;
#if GWP_DIAGNOSTICS
using System.Collections.Generic;
#endif
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
#if GWP_DIAGNOSTICS
        // 仅限本次烧村抑制调查的采样；不参与玩法，读档后重新采样即可，不进存档。
        private readonly Dictionary<string, double> _lastRaidSample = new Dictionary<string, double>();
#endif
        public override float GetTargetScoreForFaction(
            Settlement targetSettlement,
            Army.ArmyTypes missionType,
            MobileParty mobileParty,
            float ourStrength)
        {
            float baseScore = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);
            if (missionType != Army.ArmyTypes.Raider || baseScore <= 0f || mobileParty?.LeaderHero == null)
                return baseScore;

            float multiplier = GwpAiDeterrenceState.GetVillagerAttackScoreMultiplier(mobileParty);
            float result = baseScore * multiplier;
#if GWP_DIAGNOSTICS
            string offender = mobileParty.LeaderHero.StringId;
            double now = CampaignTime.Now.ToHours;
            if (!_lastRaidSample.TryGetValue(offender, out double previous) || now < previous || now - previous >= 24d)
            {
                _lastRaidSample[offender] = now;
                GwpAiDiagnostics.WriteFieldArrest("RAID_SCORE_SAMPLE",
                    "offender=" + offender + "; village=" + targetSettlement.StringId +
                    "; baseScore=" + baseScore.ToString("0.0000") + "; multiplier=" + multiplier.ToString("0.0000") +
                    "; result=" + result.ToString("0.0000") +
                    "; activeModel=" + Campaign.Current.Models.TargetScoreCalculatingModel.GetType().FullName);
            }
#endif
            return result;
        }
    }
}
