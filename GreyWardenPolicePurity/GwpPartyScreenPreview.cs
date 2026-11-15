using System;
using System.Runtime.CompilerServices;
using GreyWardenPolicePurity.Core.Troops;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版部队界面底部居中那几行「你将获得/失去……」是 PartyVM 的 GoldChangeText、MoraleChangeText、
    /// HorseChangeText、InfluenceChangeText，空字符串自动隐藏（C#反编译 v1.4.8/v1.5.7 PartyVM、PartyScreen.xml）。
    /// 灰袍声望的预期接在影响力那一行，不另做界面：影响力文字只由私有的 OnPartyInfluenceChanged 写，
    /// 非取消重置的末尾也会再调它，所以拼接挂在它后面；每次命令和打开时原版都走 RefreshTopInformation，
    /// 在那里补调一次，让声望预期随每次操作刷新。
    ///
    /// 借部队页挑人的副本界面（向祭司交人、调兵订单）在这里登记：主队灰袍兵额度、升阶扣声望和
    /// 灰袍兵交谈按钮只管真正的主队名册，不管这些副本。
    /// </summary>
    internal static class GwpPartyScreenPreview
    {
        private static readonly ConditionalWeakTable<PartyScreenLogic, Func<PartyScreenLogic, string?>> Selections = new();

        internal static void RegisterSelection(PartyScreenLogic logic, Func<PartyScreenLogic, string?> preview) =>
            Selections.Add(logic, preview);

        internal static bool IsSelection(PartyScreenLogic logic) => Selections.TryGetValue(logic, out _);

        internal static string Gain(int reputationUnits) => GwpText.Get(GwpTextCatalog.gwp_party_reputation_gain,
            "AMOUNT", PlayerBehaviorPool.FormatReputationUnits(reputationUnits));

        internal static string Cost(int reputationUnits) => GwpText.Get(GwpTextCatalog.gwp_party_reputation_cost,
            "AMOUNT", PlayerBehaviorPool.FormatReputationUnits(reputationUnits));

        private static string? PreviewFor(PartyScreenLogic logic)
        {
            if (Selections.TryGetValue(logic, out Func<PartyScreenLogic, string?> preview)) return preview(logic);
            if (logic.RightOwnerParty?.MobileParty?.IsMainParty != true) return null;
            int cost = PlayerBehaviorPool.WardenUpgradeCost(GwpPlayerWardenTroops.PendingUpgrades(logic));
            return cost > 0 ? Cost(cost * WardenReputationRules.TroopUnitsPerPoint) : null;
        }

        [HarmonyPatch(typeof(PartyVM), "OnPartyInfluenceChanged")]
        private static class InfluenceLinePatch
        {
            private static void Postfix(PartyVM __instance)
            {
                if (__instance.PartyScreenLogic is not { } logic || PreviewFor(logic) is not { } preview) return;
                string influence = __instance.InfluenceChangeText;
                __instance.InfluenceChangeText = string.IsNullOrEmpty(influence) ? preview : influence + "\n" + preview;
            }
        }

        [HarmonyPatch(typeof(PartyVM), "RefreshTopInformation")]
        private static class RefreshPatch
        {
            private static readonly Action<PartyVM> RefreshInfluence = AccessTools.MethodDelegate<Action<PartyVM>>(
                AccessTools.Method(typeof(PartyVM), "OnPartyInfluenceChanged"));

            private static void Postfix(PartyVM __instance)
            {
                if (__instance.PartyScreenLogic != null) RefreshInfluence(__instance);
            }
        }
    }
}
