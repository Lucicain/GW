using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 当前已宣战案件的目标不能靠原版安全欲望立刻钻入聚落躲避执法。
    /// 限制完全由案件生命周期驱动；案件结束或战争状态回退后自动消失。
    /// </summary>
    [HarmonyPatch(typeof(EnterSettlementAction),
        nameof(EnterSettlementAction.ApplyForParty))]
    internal static class GwpCaseSettlementEntryPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty mobileParty, Settlement settlement)
        {
            if (!PoliceEnforcementBehavior.IsSettlementEntryBlockedByActiveCase(
                    mobileParty, settlement))
                return true;

            // 拒绝本次进城动作，并重新命令被逐出的案件目标（若属于军团则
            // 命令军团领队）进攻承办该案的灰袍领主，避免在城门口停住。
            PoliceEnforcementBehavior.RedirectShelteredCasePartyToAssignee(
                mobileParty);
            return false;
        }
    }
}
