using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版 <c>CalculateMobilePartyMemberSizeLimit</c> 的领主/管家加成整段写在
    /// <c>LeaderHero != null &amp;&amp; LeaderHero.Clan != null</c> 里，无领主的自定义队
    /// 因此只拿得到 20 的裸底数。玩家练兵订单最多 80 人，而超编速度惩罚是
    /// <c>limit / men - 1</c>——80 人塞进 20 的上限就是 -75% 速度，随行练兵队
    /// 根本跟不住练兵官。
    ///
    /// 这里只为当前那一支练兵队按订单量抬高上限，别的队伍一律交回原版。
    /// </summary>
    public sealed class GwpPartySizeLimitModel : DefaultPartySizeLimitModel
    {
        public override ExplainedNumber GetPartyMemberSizeLimit(
            PartyBase party, bool includeDescriptions = false)
        {
            int cohortLimit = GreyWardenTroopRequestBehavior
                .GetCohortSizeLimit(party?.MobileParty);
            if (cohortLimit > 0)
                return new ExplainedNumber(cohortLimit, includeDescriptions);

            return base.GetPartyMemberSizeLimit(party!, includeDescriptions);
        }
    }
}
