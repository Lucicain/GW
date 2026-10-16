using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者祭司不因势力低被原版删掉（用户裁定 2026-10-16，取代 10-07 的「祭司照原版退休」）。
    /// 原版每天对每位要人查一次：没有产业、能死、势力低于 100，就按 (100 − 势力)/100 × 2% 的概率
    /// <c>KillCharacterAction.ApplyByRemove</c>（C#反编译 v1.4.8 NotablesCampaignBehavior.CheckAndMakeNotableDisappear）。
    /// 祭司势力等于火誓者比例那一份，信众少的村子常年在 100 以下，开局家乡村的祭司就这样被删掉过（实机诊断 2026-10-16）。
    /// 其他要人照原版；祭司战死、老死照原版。
    /// </summary>
    [HarmonyPatch(typeof(NotablesCampaignBehavior), "CheckAndMakeNotableDisappear")]
    internal static class GwpPriestStaysPatch
    {
        [HarmonyPrefix]
        private static bool Before(Hero notable)
        {
            if (!GwpFireswornBehavior.IsFireswornPriest(notable))
                return true;
#if GWP_DIAGNOSTICS
            if (notable.Power < Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit)
                GwpAiDiagnostics.WriteFaith("PRIEST_DISAPPEAR_BLOCKED", "priest=" + notable.StringId +
                    "; village=" + notable.CurrentSettlement?.StringId + "; power=" + notable.Power.ToString("0"));
#endif
            return false;
        }
    }
}
