using System;
using System.Collections.Generic;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        private const string PriestTradeReturn = "gwp_fs_trade_return";

        private void RegisterPlayerTrade(CampaignGameStarter starter)
        {
            AddPriestTrade(starter, FireswornTradeKind.Prisoners, "gwp_fs_trade_prisoners",
                GwpTextCatalog.gwp_fs_trade_prisoners_ask, GwpTextCatalog.gwp_fs_trade_prisoners_reply);
            AddPriestTrade(starter, FireswornTradeKind.Troops, "gwp_fs_trade_troops",
                GwpTextCatalog.gwp_fs_trade_troops_ask, GwpTextCatalog.gwp_fs_trade_troops_reply);
            AddPriestTrade(starter, FireswornTradeKind.Armory, "gwp_fs_trade_armory",
                GwpTextCatalog.gwp_fs_trade_armory_ask, GwpTextCatalog.gwp_fs_trade_armory_reply);
            starter.AddPlayerLine("gwp_fs_trade_armory_prepare", "hero_main_options", "gwp_fs_trade_armory_prepare",
                GwpText.Get(GwpTextCatalog.gwp_fs_trade_armory_prepare_ask),
                () => !CanManagePlayerArmory && CurrentPriestTrade(FireswornTradeKind.Armory).HasValue, null);
            starter.AddDialogLine("gwp_fs_trade_armory_prepare_reply", "gwp_fs_trade_armory_prepare", PriestTradeReturn,
                GwpText.Get(GwpTextCatalog.gwp_fs_trade_armory_prepare_reply), null, () =>
                {
                    if (CurrentPriestTrade(FireswornTradeKind.Armory) is { } trade) OpenPlayerArmoryLedger(trade.Party);
                });
            // 原版赎金商在关界面后再走一条 NPC 答复，此时才按最新名册和声望生成选项。
            starter.AddDialogLine(PriestTradeReturn, PriestTradeReturn, "hero_main_options",
                GwpText.Get(GwpTextCatalog.gwp_fs_trade_return), null, null);
        }

        private void AddPriestTrade(CampaignGameStarter starter, FireswornTradeKind kind,
            string id, string ask, string reply)
        {
            starter.AddPlayerLine(id, "hero_main_options", id, GwpText.Get(ask), () => CanOfferPriestTrade(kind), null);
            starter.AddDialogLine(id + "_reply", id, PriestTradeReturn, GwpText.Get(reply), null, () => OpenPriestTrade(kind));
        }

        private bool CanOfferPriestTrade(FireswornTradeKind kind)
        {
            if (CurrentPriestTrade(kind) is not { } trade) return false;

            return kind == FireswornTradeKind.Armory
                ? _armories.TryGetValue(trade.Party.StringId, out ItemRoster? ledger) && ledger.Any(IsTradeEquipment)
                : TradeRoster(trade.Party, kind).GetTroopRoster().Any(entry => entry.Number > 0 && CanHandOver(entry.Character, kind));
        }

        private (MobileParty Party, Settlement Village, Hero Priest)? CurrentPriestTrade(FireswornTradeKind kind)
        {
            MobileParty party = MobileParty.MainParty;
            if (party?.CurrentSettlement is not { } village || Hero.OneToOneConversationHero is not { } priest ||
                !CanCompletePriestTrade(party, village, priest, kind)) return null;
            return (party, village, priest);
        }

        private bool CanCompletePriestTrade(MobileParty party, Settlement village, Hero priest, FireswornTradeKind kind) =>
            party == MobileParty.MainParty && party.IsActive && party.MapEvent == null && party.CurrentSettlement == village &&
            !Hero.MainHero.IsPrisoner && !priest.IsPrisoner && village.IsVillage && IsVillageQuiet(village) &&
            _villages.TryGetValue(village.StringId, out FaithVillage? row) && FindLivingPriest(row) == priest &&
            FireswornTradeRules.IsAvailable(kind, PlayerBehaviorPool.PreciseReputation);

        private void OpenPriestTrade(FireswornTradeKind kind)
        {
            if (CurrentPriestTrade(kind) is not { } trade) return;
            var (party, village, priest) = trade;

            if (kind == FireswornTradeKind.Armory)
            {
                OpenArmoryTrade(party, village, priest);
                return;
            }
            OpenPersonnelTrade(party, village, priest, kind);
        }

        private static TroopRoster TradeRoster(MobileParty party, FireswornTradeKind kind) =>
            kind == FireswornTradeKind.Prisoners ? party.PrisonRoster : party.MemberRoster;

        private static bool CanHandOver(CharacterObject troop, FireswornTradeKind kind) =>
            !troop.IsHero && !troop.IsNotTransferableInPartyScreen &&
            (kind == FireswornTradeKind.Prisoners || !GwpCommon.IsGreyWardenTroop(troop));

        private void OpenPersonnelTrade(MobileParty party, Settlement village, Hero priest, FireswornTradeKind kind)
        {
            bool prisoners = kind == FireswornTradeKind.Prisoners;
            TroopRoster source = TradeRoster(party, kind);
            var leftMembers = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            var rightMembers = TroopRoster.CreateDummyTroopRoster();
            var rightPrisoners = TroopRoster.CreateDummyTroopRoster();
            rightMembers.AddToCounts(CharacterObject.PlayerCharacter, 1);
            TroopRoster rightOffers = prisoners ? rightPrisoners : rightMembers;
            int eligible = 0;
            foreach (TroopRosterElement batch in source.GetTroopRoster())
            {
                if (batch.Number <= 0 || !CanHandOver(batch.Character, kind)) continue;
                rightOffers.AddToCounts(batch.Character, batch.Number, woundedCount: batch.WoundedNumber);
                eligible += batch.Number;
            }
            if (eligible == 0) return;

            // 原版赎金界面也先把人移进左侧副本，由完成委托统一结算。
            var state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.PartyScreenMode = prisoners ? PartyScreenHelper.PartyScreenMode.Ransom : PartyScreenHelper.PartyScreenMode.TroopsManage;
            var transferable = PartyScreenLogic.TransferState.Transferable;
            var blocked = PartyScreenLogic.TransferState.NotTransferable;
            bool committed = false;
            var data = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(leftMembers, leftPrisoners,
                prisoners ? blocked : transferable, prisoners ? transferable : blocked, blocked,
                (troop, type, _, _) => type == (prisoners ? PartyScreenLogic.TroopType.Prisoner : PartyScreenLogic.TroopType.Member) &&
                    CanHandOver(troop, kind), state.PartyScreenMode,
                leftPartyName: priest.Name,
                header: GwpText.Create(prisoners ? GwpTextCatalog.gwp_fs_trade_prisoners_title : GwpTextCatalog.gwp_fs_trade_troops_title),
                leftPartyMembersSizeLimit: prisoners ? 0 : eligible, leftPartyPrisonersSizeLimit: prisoners ? eligible : 0,
                partyPresentationDoneButtonDelegate: (members, captives, _, _, _, _, _, _, _) =>
                {
                    if (committed) return true;
                    committed = CompletePersonnelTrade(party, village, priest, kind, prisoners ? captives : members);
                    return committed;
                },
                isTroopUpgradesDisabled: true);
            data.RightMemberRoster = rightMembers;
            data.RightPrisonerRoster = rightPrisoners;
            data.RightPartyMembersSizeLimit = Math.Max(data.RightPartyMembersSizeLimit, rightMembers.TotalManCount);
            data.RightPartyPrisonersSizeLimit = Math.Max(data.RightPartyPrisonersSizeLimit, rightPrisoners.TotalManCount);
            data.DoNotApplyGoldTransactions = true;
            state.PartyScreenLogic = new PartyScreenLogic();
            state.PartyScreenLogic.Initialize(data);
            // 原版选择页仍绑定背包；重置、取消也只应碰本次选择用的副本。
            state.PartyScreenLogic.CurrentData.RightItemRoster = new ItemRoster(party.ItemRoster);
            // 像原版赎金界面在底部居中写「你将获得多少金币」一样，挑人时就写能换多少声望。
            GwpPartyScreenPreview.RegisterSelection(state.PartyScreenLogic, logic =>
                PersonnelTradePreview(kind, prisoners ? logic.CurrentData.LeftPrisonerRoster : logic.CurrentData.LeftMemberRoster));
            Game.Current.GameStateManager.PushState(state);
        }

        private static string? PersonnelTradePreview(FireswornTradeKind kind, TroopRoster selection) =>
            TradePreview(kind, PersonnelTradeUnits(selection));

        private static string? TradePreview(FireswornTradeKind kind, long units)
        {
            int reputation = PlayerBehaviorPool.QuoteFireswornTrade(kind, units).ReputationUnits;
            return reputation > 0 ? GwpPartyScreenPreview.Gain(reputation) : null;
        }

        private static long PersonnelTradeUnits(TroopRoster selection) => selection.GetTroopRoster()
            .Where(batch => batch.Number > 0)
            .Sum(batch => (long)batch.Number * WardenReputationRules.TroopUnits(batch.Character.Tier));

        private bool CompletePersonnelTrade(MobileParty party, Settlement village, Hero priest,
            FireswornTradeKind kind, TroopRoster selection)
        {
            List<TroopRosterElement> batches = selection.GetTroopRoster().Where(batch => batch.Number > 0).ToList();
            if (batches.Count == 0) return true;

            TroopRoster source = TradeRoster(party, kind);
            if (!CanCompletePriestTrade(party, village, priest, kind) ||
                batches.Any(batch => !CanHandOver(batch.Character, kind) || !GwpTroopTransfer.ContainsBatch(source, batch)))
                return NotifyPriestTradeUnavailable();

            TroopRoster destination = kind == FireswornTradeKind.Prisoners
                ? RosterOf(_dungeons, village.StringId) : EnsureVillageMilitia(village).MemberRoster;
            foreach (TroopRosterElement batch in batches)
                GwpTroopTransfer.MoveSelectedBatch(party.Party, source, destination, batch);

            NotifyPriestTradeCompleted(village, kind, selection.TotalManCount, PersonnelTradeUnits(selection));
            return true;
        }

        private static bool IsTradeEquipment(ItemRosterElement entry) =>
            entry.Amount > 0 && !entry.EquipmentElement.IsEmpty && !entry.EquipmentElement.IsQuestItem &&
            entry.EquipmentElement.Item.IsTransferable && IsArmoryItem(entry.EquipmentElement.Item);

        private void OpenArmoryTrade(MobileParty party, Settlement village, Hero priest)
        {
            if (!_armories.TryGetValue(party.StringId, out ItemRoster? ledger)) return;
            ItemRoster available = new ItemRoster();
            foreach (ItemRosterElement entry in ledger.Where(IsTradeEquipment)) available.Add(entry);
            if (available.Count == 0) return;

            var state = Game.Current.GameStateManager.CreateState<InventoryState>();
            state.InventoryMode = InventoryScreenHelper.InventoryMode.Stash;
            var selection = new ItemRoster();
            var logic = new InventoryLogic(null);
            logic.Initialize(selection, available, party.MemberRoster, isTrading: false, isSpecialActionsPermitted: false,
                CharacterObject.PlayerCharacter, InventoryScreenHelper.InventoryCategoryType.None, village.Village.MarketData,
                useBasePrices: false, state.InventoryMode, GwpText.Create(GwpTextCatalog.gwp_fs_trade_armory_title));
            GwpArmoryTransferScreen.Register(logic, () => CompleteArmoryTrade(party, village, priest, selection),
                () => TradePreview(FireswornTradeKind.Armory, ArmoryTradeUnits(party, village, selection)));
            state.InventoryLogic = logic;
            Game.Current.GameStateManager.PushState(state);
        }

        private bool CompleteArmoryTrade(MobileParty party, Settlement village, Hero priest, ItemRoster selection)
        {
            const FireswornTradeKind kind = FireswornTradeKind.Armory;
            if (selection.Count == 0) return true;
            if (!CanCompletePriestTrade(party, village, priest, kind) ||
                !_armories.TryGetValue(party.StringId, out ItemRoster? ledger)) return NotifyPriestTradeUnavailable();
            foreach (ItemRosterElement entry in selection)
            {
                int index = ledger.FindIndexOfElement(entry.EquipmentElement);
                if (!IsTradeEquipment(entry) || index < 0 || ledger.GetElementNumber(index) < entry.Amount)
                    return NotifyPriestTradeUnavailable();
            }

            long units = ArmoryTradeUnits(party, village, selection);
            int count = MoveAll(selection, ledger, ItemsOf(_armories, village.StringId));
            _plans.Remove(party.StringId);
            _plans.Remove(village.StringId);
            NotifyPriestTradeCompleted(village, kind, count, units);
            selection.Clear();
            return true;
        }

        private static long ArmoryTradeUnits(MobileParty party, Settlement village, ItemRoster selection) =>
            selection.Sum(entry => (long)village.Village.MarketData.GetPrice(entry.EquipmentElement, party, true, village.Party) *
                entry.Amount);

        private static bool NotifyPriestTradeUnavailable()
        {
            MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_fs_trade_unavailable));
            return false;
        }

        private static void NotifyPriestTradeCompleted(Settlement village, FireswornTradeKind kind, int count, long units)
        {
            var receipt = PlayerBehaviorPool.CompleteFireswornTrade(kind, units);
            // 原版得金币后在左下角消息栏记一笔；声望同样只记得了多少，不讲换算和余数。
            if (receipt.ReputationUnits > 0)
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_reputation_gained_log,
                    "AMOUNT", PlayerBehaviorPool.FormatReputationUnits(receipt.ReputationUnits))));
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("PLAYER_PRIEST_TRADE", "village=" + village.StringId + "; kind=" + kind +
                "; transferred=" + count + "; units=" + units + "; reputation=" +
                PlayerBehaviorPool.FormatReputationUnits(receipt.ReputationUnits) + "; remainder=" + receipt.Remainder);
#endif
        }
    }
}
