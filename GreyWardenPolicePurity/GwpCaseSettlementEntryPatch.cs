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

            // 清掉本次已经选中的进城动作；下一次原版思考仍可选择逃离、
            // 接战或其他野外行为，但在案件有效期间不能再次钻入聚落。
            try { mobileParty.SetMoveModeHold(); } catch { }
            return false;
        }
    }
}
