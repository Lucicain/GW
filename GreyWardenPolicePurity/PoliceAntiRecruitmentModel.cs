using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 在 AI 评分阶段堵死 gw 家族的原版招募与原版外交宣战路径。
    /// </summary>
    public class PoliceAntiRecruitmentModel : DefaultDiplomacyModel
    {
        private static bool IsGwClan(Clan clan) =>
            string.Equals(clan?.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        private static bool IsGwFaction(IFaction faction)
        {
            if (faction == null) return false;
            if (string.Equals(faction.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase))
                return true;

            Clan policeClan = PoliceStats.GetPoliceClan();
            return policeClan != null && faction == policeClan;
        }

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

        // 灰袍守卫不会参与原版独立家族 / 王国外交 AI 的主动宣战。
        public override float GetScoreOfDeclaringWar(
            IFaction factionDeclaresWar,
            IFaction factionDeclaredWar,
            Clan evaluatingClan,
            out TextObject reason,
            bool includeReason = false)
        {
            if (IsGwFaction(factionDeclaresWar) || IsGwClan(evaluatingClan))
            {
                reason = includeReason
                    ? new TextObject("灰袍守卫不会参与原版外交 AI 的主动宣战。")
                    : TextObject.GetEmpty();
                return float.MinValue;
            }

            return base.GetScoreOfDeclaringWar(
                factionDeclaresWar,
                factionDeclaredWar,
                evaluatingClan,
                out reason,
                includeReason);
        }
    }
}
