using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Helpers;
using SandBox.ViewModelCollection;
using SandBox.ViewModelCollection.Nameplate.NameplateNotifications;
using SandBox.ViewModelCollection.Nameplate.NameplateNotifications.SettlementNotificationTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 聚落名牌上冒出来的那一条「某某在这里做了什么」（用户 2026-10-07：原版领主在村里城里做了事，聚落上会显示他干了什么，
    /// 解决了一个请愿、招了多少兵）。原版由 SettlementNameplateNotificationsVM 听招兵、卖俘虏、卖东西、解决请愿等事件生成
    /// （C#反编译 v1.4.8 SandBox.ViewModelCollection）。灰袍在信众村里做的事原版没有对应事件；也不能借原版招兵事件——
    /// RBM 听到 AI 招兵会向领主收招兵钱（RBMCampaign SpoilsPool.OnTroopRecruited），而直接招狂信者不花钱（用户裁定）。
    /// 所以直接往原版这张列表里加一条同样样式的，只动界面，不触发任何原版逻辑。只在玩家查看过的聚落上显示，同原版。
    /// </summary>
    internal static class GwpSettlementNotices
    {
        private static readonly Dictionary<Settlement, SettlementNameplateNotificationsVM> Live =
            new Dictionary<Settlement, SettlementNameplateNotificationsVM>();
        private static readonly AccessTools.FieldRef<SettlementNameplateNotificationsVM, Settlement> SettlementOf =
            AccessTools.FieldRefAccess<SettlementNameplateNotificationsVM, Settlement>("_settlement");
        private static readonly AccessTools.FieldRef<SettlementNameplateNotificationsVM, int> TickOf =
            AccessTools.FieldRefAccess<SettlementNameplateNotificationsVM, int>("_tickSinceEnabled");
        private static readonly MethodInfo RemoveItem =
            AccessTools.Method(typeof(SettlementNameplateNotificationsVM), "RemoveItem");

        internal static void Register(SettlementNameplateNotificationsVM vm) => Live[SettlementOf(vm)] = vm;

        internal static void Unregister(SettlementNameplateNotificationsVM vm)
        {
            Settlement settlement = SettlementOf(vm);
            if (Live.TryGetValue(settlement, out SettlementNameplateNotificationsVM? current) && current == vm)
                Live.Remove(settlement);
        }

        /// <summary>一位英雄在这个聚落做了事。</summary>
        internal static void Post(Settlement? settlement, Hero? hero, string text) =>
            Post(settlement, hero?.CharacterObject, hero?.Name?.ToString() ?? string.Empty, hero?.Clan, text);

        /// <summary>一支无领主队在这个聚落做了事：头像用队伍的代表人物，名字用队名。</summary>
        internal static void Post(Settlement? settlement, MobileParty party, string text) =>
            Post(settlement, PartyBaseHelper.GetVisualPartyLeader(party.Party), party.Name.ToString(), party.ActualClan, text);

        private static void Post(Settlement? settlement, CharacterObject? visual, string name, IFaction? faction, string text)
        {
            if (settlement == null || !settlement.IsInspected ||
                !Live.TryGetValue(settlement, out SettlementNameplateNotificationsVM? vm))
                return;
            var remove = (Action<SettlementNotificationItemBaseVM>)Delegate.CreateDelegate(
                typeof(Action<SettlementNotificationItemBaseVM>), vm, RemoveItem);
            vm.Notifications.Add(new GwpNoticeItemVM(remove, TickOf(vm), visual, name, faction, text));
        }

        /// <summary>原版招兵那一条的文字（「招募了 N 名士兵」）。</summary>
        internal static string RecruitText(int amount) => SandBoxUIHelper.GetRecruitNotificationText(amount);
    }

    internal sealed class GwpNoticeItemVM : SettlementNotificationItemBaseVM
    {
        public GwpNoticeItemVM(Action<SettlementNotificationItemBaseVM> onRemove, int createdTick, CharacterObject? visual,
            string name, IFaction? faction, string text)
            : base(onRemove, createdTick)
        {
            Text = text;
            CharacterName = name;
            CharacterVisual = new CharacterImageIdentifierVM(visual == null ? null : SandBoxUIHelper.GetCharacterCode(visual));
            CreatedTick = createdTick;
            // 照原版：和玩家不交战的算友方，交战的算敌方。
            RelationType = faction == null ? 0 : faction.IsAtWarWith(Hero.MainHero.Clan) ? -1 : 1;
        }
    }

    [HarmonyPatch(typeof(SettlementNameplateNotificationsVM), nameof(SettlementNameplateNotificationsVM.RegisterEvents))]
    internal static class GwpSettlementNoticesRegisterPatch
    {
        private static void Postfix(SettlementNameplateNotificationsVM __instance) => GwpSettlementNotices.Register(__instance);
    }

    [HarmonyPatch(typeof(SettlementNameplateNotificationsVM), nameof(SettlementNameplateNotificationsVM.UnloadEvents))]
    internal static class GwpSettlementNoticesUnloadPatch
    {
        private static void Postfix(SettlementNameplateNotificationsVM __instance) => GwpSettlementNotices.Unregister(__instance);
    }
}
