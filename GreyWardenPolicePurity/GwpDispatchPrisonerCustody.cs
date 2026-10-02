using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 送信队押着的俘虏不逃跑（用户裁定 2026-10-02）。原版
    /// <see cref="PrisonerReleaseCampaignBehavior"/> 有两处逃跑判定：英雄每天按关押队伍的健康人数掷一次
    /// （野外 2 人小队约 15%/天），以及俘虏超出容量时每小时逐个掷 10%。送信队通常只有几个人，
    /// 押本案目标走一两天就可能丢人，所以这两处对送信队跳过。议和放人、队伍覆灭、赎回等其他释放照原版。
    /// </summary>
    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "DailyHeroTick")]
    internal static class GwpDispatchHeroEscapePatch
    {
        [HarmonyPrefix]
        private static bool Before(Hero hero) =>
            !GwpWardenDispatchBehavior.IsDispatchParty(hero?.PartyBelongedToAsPrisoner?.MobileParty);
    }

    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "HourlyPartyTick")]
    internal static class GwpDispatchOverflowEscapePatch
    {
        [HarmonyPrefix]
        private static bool Before(MobileParty mobileParty) =>
            !GwpWardenDispatchBehavior.IsDispatchParty(mobileParty);
    }
}
