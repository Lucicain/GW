using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    internal static class GwpDutyBanner
    {
        internal static Banner? GetDutyEmblem() =>
            GwpCommon.IsPlayerGreyWardenOnDuty() && PoliceStats.GetPoliceClan()?.Banner is Banner banner
                ? new Banner(banner)
                : null;

        internal static void OnInventoryExchange(List<(ItemRosterElement, int)> bought,
            List<(ItemRosterElement, int)> sold, bool isTrading) => RefreshPlayerClanVisuals();

        internal static void RefreshPlayerClanVisuals()
        {
            // 原版地图缓存只在标脏后重取Banner；存档仍保存玩家原徽，无额外进行中状态。
            if (Clan.PlayerClan != null)
                foreach (var warParty in Clan.PlayerClan.WarPartyComponents)
                    warParty.Party.SetVisualAsDirty();
        }
    }

    // 原版旗帜奖励池不检查 NotMerchandise；必须在池的归属模型排除执勤凭证。
    // 依据：v1.4.8 DefaultBannerItemModel、BannerCampaignBehavior、DefaultBattleRewardModel。
    public sealed class GwpDutyBannerItemModel : DefaultBannerItemModel
    {
        public override IEnumerable<ItemObject> GetPossibleRewardBannerItems() =>
            base.GetPossibleRewardBannerItems().Where(item => item.StringId != GwpIds.DutyBannerItemId);

        public override bool CanBannerBeUpdated(ItemObject item) =>
            item.StringId != GwpIds.DutyBannerItemId && base.CanBannerBeUpdated(item);
    }

    // 返回可独立染色的副本，不改玩家保存的原徽，也不让原版染色写回灰袍家徽。
    [HarmonyPatch(typeof(Clan), nameof(Clan.Banner), MethodType.Getter)]
    internal static class GwpDutyBannerClanEmblemPatch
    {
        private static void Postfix(Clan __instance, ref Banner __result)
        {
            // 原版编辑器直接修改传入Banner；编辑的是玩家原徽，执勤展示保持独立。
            if (__instance == Clan.PlayerClan && Game.Current?.GameStateManager?.ActiveState is not BannerEditorState
                && GwpDutyBanner.GetDutyEmblem() is Banner banner)
                __result = banner;
        }
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.Banner), MethodType.Getter)]
    internal static class GwpDutyBannerMapEmblemPatch
    {
        private static void Postfix(MobileParty __instance, ref Banner __result)
        {
            if (__instance == MobileParty.MainParty && GwpDutyBanner.GetDutyEmblem() is Banner banner)
                __result = banner;
        }
    }

    // 原版场景还允许显式SetBanner覆盖家徽，主队旗手与tableau统一从同一执勤入口取。
    [HarmonyPatch(typeof(PartyAgentOrigin), nameof(PartyAgentOrigin.Banner), MethodType.Getter)]
    internal static class GwpDutyBannerPartyEmblemPatch
    {
        private static void Postfix(PartyAgentOrigin __instance, ref Banner __result)
        {
            if (__instance.Party == PartyBase.MainParty && GwpDutyBanner.GetDutyEmblem() is Banner banner)
                __result = banner;
        }
    }
}
