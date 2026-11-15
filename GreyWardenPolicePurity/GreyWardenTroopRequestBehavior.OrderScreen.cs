using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 调兵订单用原版部队界面挑兵：左边是练兵官可练的灰袍兵种，人数是只订这一种时最多能订的数；
    /// 右边是这张订单。照原版挪兵（点一下一个，Shift 五个，Ctrl 全部），底部居中写要花多少声望，
    /// 余额或名额不够时完成按钮变灰、悬停说明原因。界面只挑兵种和人数，登记、扣声望仍走原有订单入口。
    /// 非普通模式的部队页原版锁住导航条，离开只有完成和取消两条路。
    /// （依据：用户裁定 2026-11-15「找原版的界面，不要自创界面」）
    /// </summary>
    public sealed partial class GreyWardenTroopRequestBehavior
    {
        private static void ShowTroopOrderScreen(int escortCount, Action<string> confirm, Action? cancel)
        {
            if (!CanChooseNewOrder())
            {
                cancel?.Invoke();
                return;
            }

            var catalog = TroopRoster.CreateDummyTroopRoster();
            foreach (CharacterObject troop in OrderTroopIds.Select(CharacterObject.Find).Where(GwpCommon.IsGreyWardenTroop))
            {
                int most = MostOrderable(troop, escortCount);
                if (most > 0) catalog.AddToCounts(troop, most);
            }
            if (catalog.TotalManCount == 0)
            {
                MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_player_troop_order_unaffordable));
                cancel?.Invoke();
                return;
            }

            bool settled = false;
            void Cancel()
            {
                if (settled) return;
                settled = true;
                cancel?.Invoke();
            }

            var order = TroopRoster.CreateDummyTroopRoster();
            order.AddToCounts(CharacterObject.PlayerCharacter, 1);
            var state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.TroopsManage;
            var data = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(catalog,
                TroopRoster.CreateDummyTroopRoster(), PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable, PartyScreenLogic.TransferState.NotTransferable,
                (troop, type, _, _) => type == PartyScreenLogic.TroopType.Member && !troop.IsHero, state.PartyScreenMode,
                leftPartyName: _instance?.ResolveTrainerParty()?.LeaderHero?.Name,
                header: GwpText.Create(GwpTextCatalog.gwp_player_troop_order_title),
                leftPartyMembersSizeLimit: catalog.TotalManCount,
                partyPresentationDoneButtonDelegate: (_, _, members, _, _, _, _, _, _) =>
                {
                    if (settled) return true;
                    var lines = OrderedLines(members);
                    if (OrderRestriction(lines, escortCount) != null) return false;
                    settled = true;
                    confirm(EncodeOrder(lines.Select(line => (line.Troop.StringId, line.Count))));
                    return true;
                },
                partyPresentationDoneButtonConditionDelegate: (_, _, members, _, _, _) =>
                {
                    TextObject? reason = OrderRestriction(OrderedLines(members), escortCount);
                    return Tuple.Create(reason == null, reason ?? TextObject.GetEmpty());
                },
                partyPresentationCancelButtonDelegate: Cancel,
                isTroopUpgradesDisabled: true);
            data.RightMemberRoster = order;
            data.RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
            // 右侧上限写成这张单子最多能订的总人数：最便宜的那一种订满就是上限。
            data.RightPartyMembersSizeLimit = 1 + catalog.GetTroopRoster().Max(entry => entry.Number);
            data.RightPartyPrisonersSizeLimit = 0;
            data.DoNotApplyGoldTransactions = true;
            state.PartyScreenLogic = new PartyScreenLogic();
            state.PartyScreenLogic.Initialize(data);
            // 原版选择页仍绑定背包；重置、取消也只应碰本次选择用的副本。
            state.PartyScreenLogic.CurrentData.RightItemRoster = new ItemRoster(MobileParty.MainParty.ItemRoster);
            GwpPartyScreenPreview.RegisterSelection(state.PartyScreenLogic, logic =>
            {
                int cost = OrderCost(OrderedLines(logic.CurrentData.RightMemberRoster));
                return cost > 0 ? GwpPartyScreenPreview.Cost(cost) : null;
            });
            Game.Current.GameStateManager.PushState(state);
        }

        /// <summary>只订这一种兵时，按当前声望和主队额度最多能订几个。</summary>
        private static int MostOrderable(CharacterObject troop, int escortCount)
        {
            int cost = GetUnitCost(troop);
            for (int count = GwpPlayerWardenTroops.MaximumCapacity; count > 0; count--)
                if (GetOrderRestriction(QuoteReputationOrder(count * cost, count, escortCount)) == null)
                    return count;
            return 0;
        }

        private static IReadOnlyList<(CharacterObject Troop, int Count)> OrderedLines(TroopRoster roster) =>
            roster.GetTroopRoster()
                .Where(entry => entry.Number > 0 && !entry.Character.IsHero &&
                    OrderTroopIds.Contains(entry.Character.StringId, StringComparer.OrdinalIgnoreCase))
                .Select(entry => (entry.Character, entry.Number))
                .OrderByDescending(line => line.Character.Tier)
                .ThenBy(line => line.Character.StringId, StringComparer.Ordinal)
                .ToList();

        private static int OrderCost(IReadOnlyList<(CharacterObject Troop, int Count)> lines) =>
            lines.Sum(line => line.Count * GetUnitCost(line.Troop));

        private static TextObject? OrderRestriction(IReadOnlyList<(CharacterObject Troop, int Count)> lines, int escortCount)
        {
            if (lines.Count == 0) return GwpText.Create(GwpTextCatalog.gwp_player_troop_order_empty);
            return GetOrderRestriction(QuoteReputationOrder(OrderCost(lines), lines.Sum(line => line.Count), escortCount));
        }
    }
}
