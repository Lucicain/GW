using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 鼠标悬停在灰袍协力军团上时，原版会在日志里打
    /// <c>Failed to display tooltip of type: TaleWorlds.CampaignSystem.Army</c>，
    /// 提示框什么也不显示。实机 09-16 与 09-17 反复出现。
    ///
    /// 成因：<c>Army.GetLongTermBehaviorTextForAILeadedParty</c> 的若干分支直接解引用
    /// <c>AiBehaviorObject</c>——例如 <c>PatrolAroundPoint</c> 那支写的是
    /// <c>((Settlement)AiBehaviorObject).EncyclopediaLinkWithName</c>，没有判空。
    /// 原版的王国军团经 <c>Army.Gather</c> / <c>GatherArmyAction</c> 建立，这个字段总是有值；
    /// 而协力军团是 <c>new Army(null, leader, ArmyTypes.Patrolling)</c> 直接造出来的，
    /// 从不走集结流程，<c>AiBehaviorObject</c> 始终为 null，于是一解引用就抛。
    ///
    /// 这里只在**我们自己的军团且该字段确实为 null** 时接管，返回空文本收场；
    /// 其余一切（王国军团、字段有值的情况）原样交回原版。
    /// </summary>
    [HarmonyPatch]
    internal static class GwpAssistanceArmyTooltipPatch
    {
        private static MethodBase? _target;

        private static bool Prepare()
        {
            _target ??= AccessTools.Method(typeof(Army),
                "GetLongTermBehaviorTextForAILeadedParty", new[] { typeof(bool) });
            return _target != null;
        }

        private static MethodBase TargetMethod() => _target!;

        [HarmonyPrefix]
        private static bool Prefix(Army __instance, ref TextObject __result)
        {
            if (!NeedsSafeText(__instance)) return true;

            // 返回空文本，与原版自己在"无话可说"时的收尾一致
            // （方法末尾就是 return TextObject.GetEmpty()）。协力编成属于监控里的
            // 沉默内容，玩家不需要、也不应该在提示框里看到它。这里只负责
            // 不再抛异常，不往界面上写任何东西。
            __result = TextObject.GetEmpty();
            return false;
        }

        /// <summary>
        /// 只接管"我们自己造的、且 <c>AiBehaviorObject</c> 为 null"这一种。字段有值时
        /// 原版能正常取名字，没有理由插手。
        /// </summary>
        private static bool NeedsSafeText(Army? army)
        {
            try
            {
                if (army?.LeaderParty?.IsActive != true) return false;
                if (army.Kingdom != null) return false;
                if (army.AiBehaviorObject != null) return false;

                return string.Equals(army.LeaderParty.ActualClan?.StringId,
                    GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
