using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 在 AI 评分阶段彻底堵死招募 gw 家族的所有路径。
    ///
    /// AI 在决策是否招募/加入时会调用这 4 个方法计算意愿分，
    /// 对 gw 家族返回 float.MinValue，AI 永远不会考虑招募。
    ///
    /// 相比事后踢出（ApplyByLeaveKingdom），此方法在评分阶段拦截，
    /// 不触发任何引擎状态变化，不会影响俘虏状态。
    /// PoliceAntiRecruitment Behavior 保留为最后兜底（加了俘虏守卫）。
    /// </summary>
    public class PoliceAntiRecruitmentModel : DefaultDiplomacyModel
    {
        private static bool IsGwClan(Clan clan) =>
            string.Equals(clan?.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        // 家族主动投靠某王国
        public override float GetScoreOfClanToJoinKingdom(Clan clan, Kingdom kingdom) =>
            IsGwClan(clan) ? float.MinValue : base.GetScoreOfClanToJoinKingdom(clan, kingdom);

        // 王国主动招募某家族
        public override float GetScoreOfKingdomToGetClan(Kingdom kingdom, Clan clan) =>
            IsGwClan(clan) ? float.MinValue : base.GetScoreOfKingdomToGetClan(kingdom, clan);

        // 家族主动以佣兵身份加入
        public override float GetScoreOfMercenaryToJoinKingdom(Clan clan, Kingdom kingdom) =>
            IsGwClan(clan) ? float.MinValue : base.GetScoreOfMercenaryToJoinKingdom(clan, kingdom);

        // 王国主动雇佣某佣兵家族
        public override float GetScoreOfKingdomToHireMercenary(Kingdom kingdom, Clan mercenaryClan) =>
            IsGwClan(mercenaryClan) ? float.MinValue : base.GetScoreOfKingdomToHireMercenary(kingdom, mercenaryClan);
    }
}
