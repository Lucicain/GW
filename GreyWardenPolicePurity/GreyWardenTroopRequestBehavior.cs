using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Any Grey Warden lord may record a player troop order.  No money and no
    /// soldiers move at filing time.  The Training Warden grants experience to
    /// real Grey Warden troops, leaves branch selection to Bannerlord's native
    /// upgrader, then personally seeks the player and completes an all-or-nothing
    /// paid handover into the public treasury.
    /// </summary>
    public sealed partial class GreyWardenTroopRequestBehavior : CampaignBehaviorBase
    {
        private static readonly TroopKind[] TroopKinds =
        {
            new TroopKind(GwpIds.NewRecruitId,
                GwpTuning.TroopRequest.MinimumReputation,
                GwpTuning.TroopRequest.RecruitBasePrice),
            new TroopKind(GwpIds.HeavyInfantryId,
                GwpTuning.TroopRequest.VeteranReputation,
                GwpTuning.TroopRequest.HeavyInfantryBasePrice),
            new TroopKind(GwpIds.ArcherId,
                GwpTuning.TroopRequest.VeteranReputation,
                GwpTuning.TroopRequest.ArcherBasePrice),
            new TroopKind(GwpIds.KnightId,
                GwpTuning.TroopRequest.KnightReputation,
                GwpTuning.TroopRequest.KnightBasePrice)
        };

        private static GreyWardenTroopRequestBehavior? _instance;
        private string _orderedTroopId = string.Empty;
        private int _orderedCount;
        private int _orderPrice;
        private int _orderStage;
        private double _filedHour = -1d;
        private double _lastOrderXpHour = -1d;
        private double _nextContactHour = -1d;
        private bool _isOrderedTroopUpgradeLocked;
        private string _stockSourcePartyId = string.Empty;
        private string _stockRendezvousSettlementId = string.Empty;
        private double _stockStayStartHour = -1d;
        private string _lastStockSourcePartyId = string.Empty;
        private string _cohortPartyId = string.Empty;

        internal enum PlayerTroopOrderStage
        {
            None = 0,
            Training = 1,
            Delivering = 2
        }

        internal sealed class PlayerTroopOrderSnapshot
        {
            public string TrainerPartyId { get; set; } = string.Empty;
            public string TroopName { get; set; } = string.Empty;
            public int Count { get; set; }
            public int ReadyCount { get; set; }
            public int Price { get; set; }
            public CampaignTime FiledTime { get; set; }
            public PlayerTroopOrderStage Stage { get; set; }
            }

        private sealed class TroopKind
        {
            public TroopKind(string troopId, int minimumReputation,
                int pricePerTroop)
            {
                TroopId = troopId;
                MinimumReputation = minimumReputation;
                PricePerTroop = pricePerTroop;
            }

            public string TroopId { get; }
            public int MinimumReputation { get; }
            public int PricePerTroop { get; }
        }

        private sealed class TroopOrderChoice
        {
            public TroopKind Kind { get; set; } = null!;
            public int Count { get; set; }
            public int Price { get; set; }
        }

        public GreyWardenTroopRequestBehavior() => _instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this,
                OnNewGameCreated);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,
                OnHourlyTick);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this,
                OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("GWPP_PlayerTroopOrderTroopId",
                ref _orderedTroopId);
            dataStore.SyncData("GWPP_PlayerTroopOrderCount",
                ref _orderedCount);
            dataStore.SyncData("GWPP_PlayerTroopOrderPrice",
                ref _orderPrice);
            dataStore.SyncData("GWPP_PlayerTroopOrderStage",
                ref _orderStage);
            dataStore.SyncData("GWPP_PlayerTroopOrderFiledHour",
                ref _filedHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderLastXpHour",
                ref _lastOrderXpHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderNextContactHour",
                ref _nextContactHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderUpgradeLocked",
                ref _isOrderedTroopUpgradeLocked);
            dataStore.SyncData("GWPP_PlayerTroopOrderStockSourcePartyId",
                ref _stockSourcePartyId);
            dataStore.SyncData("GWPP_PlayerTroopOrderStockSettlementId",
                ref _stockRendezvousSettlementId);
            dataStore.SyncData("GWPP_PlayerTroopOrderStockStayStartHour",
                ref _stockStayStartHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderCohortPartyId",
                ref _cohortPartyId);
            dataStore.SyncData("GWPP_PlayerTroopOrderLastStockSourcePartyId",
                ref _lastStockSourcePartyId);
        }

        /// <summary>
        /// 这支队伍正在替玩家赶制的兵种。兵种配比取向必须让位给它——否则配比会把
        /// 新兵引向别的分支，玩家的订单永远凑不齐数。没有在办订单时返回 null。
        /// </summary>
        internal static CharacterObject? GetOrderedTroopForTrainer(MobileParty? party)
        {
            if (_instance == null || party?.IsActive != true ||
                (PlayerTroopOrderStage)_instance._orderStage ==
                    PlayerTroopOrderStage.None ||
                _instance.ResolveTrainerParty() != party)
                return null;

            return CharacterObject.Find(_instance._orderedTroopId);
        }

        internal static bool IsTrainerReservedForPlayerOrder(MobileParty? party)
        {
            if (party?.LeaderHero == null || _instance == null)
                return false;

            PlayerTroopOrderStage stage =
                (PlayerTroopOrderStage)_instance._orderStage;
            if (stage == PlayerTroopOrderStage.None)
                return false;

            bool trainerReserved =
                GreyWardenFamilyBehavior.IsTrainingHero(party.LeaderHero);
            bool stockSourceReserved =
                stage == PlayerTroopOrderStage.Training &&
                string.Equals(party.StringId,
                    _instance._stockSourcePartyId,
                    StringComparison.OrdinalIgnoreCase);
            return trainerReserved || stockSourceReserved;
        }

        internal static bool IsOrderedTroopUpgradeLocked(PartyBase? party,
            CharacterObject? troop)
        {
            if (_instance == null || party?.MobileParty?.IsActive != true ||
                troop == null || !_instance._isOrderedTroopUpgradeLocked ||
                (PlayerTroopOrderStage)_instance._orderStage ==
                    PlayerTroopOrderStage.None ||
                !string.Equals(troop.StringId, _instance._orderedTroopId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return party.MobileParty ==
                   _instance.ResolveOrderPool(_instance.ResolveTrainerParty());
        }

        internal static IReadOnlyList<PlayerTroopOrderSnapshot> GetTaskSnapshots()
        {
            var result = new List<PlayerTroopOrderSnapshot>();
            if (_instance == null ||
                (PlayerTroopOrderStage)_instance._orderStage ==
                PlayerTroopOrderStage.None)
                return result;
            MobileParty? trainer = _instance.ResolveTrainerParty();
            CharacterObject? troop = CharacterObject.Find(
                _instance._orderedTroopId);
            result.Add(new PlayerTroopOrderSnapshot
            {
                TrainerPartyId = trainer?.StringId ?? string.Empty,
                TroopName = troop?.Name?.ToString() ??
                            _instance._orderedTroopId,
                Count = _instance._orderedCount,
                ReadyCount = CountHealthy(trainer, troop),
                Price = _instance._orderPrice,
                FiledTime = CampaignTime.Hours((float)Math.Max(0d,
                    _instance._filedHour)),
                Stage = (PlayerTroopOrderStage)_instance._orderStage
            });
            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _ = starter;
            ClearOrder();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            MobileParty? trainer = ResolveTrainerParty();
            CharacterObject? target = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive == true && target != null)
                LockOrderedTroopIfReady(trainer, target);

            starter.AddPlayerLine(
                "gwp_warden_mediation_ask",
                "lord_talk_speak_diplomacy_2",
                "gwp_warden_mediation_reply",
                GwpText.Get("{=gwp_warden_mediation_ask}The fighting I did on your account has left me at war. Speak for me."),
                () => IsOrdinaryGreyWardenLordConversation() && IsPlayerGreyWardenMember() &&
                      PoliceAntiWarDeclaration.HasMediationRequests(),
                ApplyWardenMediationFromConversation,
                216);

            starter.AddDialogLine(
                "gwp_warden_mediation_reply",
                "gwp_warden_mediation_reply",
                "lord_pretalk",
                "{GWP_WARDEN_MEDIATION_RESULT}",
                null, null, 216);

            starter.AddPlayerLine(
                "gwp_player_troop_order_file",
                "lord_talk_speak_diplomacy_2",
                "gwp_player_troop_order_file_response",
                GwpText.Get("{=gwp_player_troop_order_file}I want the Training Warden to prepare soldiers for my command."),
                CanFileTroopOrder,
                ShowTroopOrderInquiry,
                215);

            starter.AddDialogLine(
                "gwp_player_troop_order_file_response",
                "gwp_player_troop_order_file_response",
                "lord_pretalk",
                GwpText.Get("{=gwp_player_troop_order_recorded}Settle the price with me now, and I will send the order. The Training Warden will train real troops and bring them to you when they are ready."),
                null, null, 215);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_offer_start",
                "start",
                "gwp_player_troop_delivery_choice",
                "{" + "GWP_PLAYER_TROOP_DELIVERY_OFFER" + "}",
                PrepareDeliveryConversation,
                null,
                1100);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_offer",
                "lord_talk_speak_diplomacy_2",
                "gwp_player_troop_delivery_choice",
                "{" + "GWP_PLAYER_TROOP_DELIVERY_OFFER" + "}",
                PrepareDeliveryConversation,
                null,
                310);

            starter.AddPlayerLine(
                "gwp_player_troop_delivery_accept",
                "gwp_player_troop_delivery_choice",
                "gwp_player_troop_delivery_accepted",
                GwpText.Get("{=gwp_player_troop_delivery_accept}Then place them under my command."),
                PrepareDeliveryConversation,
                CompleteTroopDelivery,
                310);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_accepted",
                "gwp_player_troop_delivery_accepted",
                "close_window",
                GwpText.Get("{=gwp_player_troop_delivery_accepted}They are yours. These soldiers now answer to you."),
                null, null, 310);


            starter.AddPlayerLine(
                "gwp_player_troop_delivery_cancel",
                "gwp_player_troop_delivery_choice",
                "gwp_player_troop_delivery_cancelled",
                GwpText.Get("{=gwp_player_troop_delivery_cancel}Cancel the order. Keep the soldiers."),
                null,
                CancelTroopOrder,
                290);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_cancelled",
                "gwp_player_troop_delivery_cancelled",
                "close_window",
                GwpText.Get("{=gwp_player_troop_delivery_cancelled}Then no payment is due. They return to ordinary Grey Warden service."),
                null, null, 290);
        }

        /// <summary>
        /// 灰袍替玩家出面，把因为帮他们办事结下的仇一并了结。回话按结果分两种：
        /// 真的了结了几家，或者赶到时已经没有需要了结的了。
        /// </summary>
        private static void ApplyWardenMediationFromConversation()
        {
            int settled = PoliceAntiWarDeclaration.ApplyWardenMediation();
            MBTextManager.SetTextVariable("GWP_WARDEN_MEDIATION_RESULT", settled > 0
                ? GwpText.Create("{=gwp_warden_mediation_done}Consider it carried. Word goes out today, and the quarrels you took up for us are closed.")
                : GwpText.Create("{=gwp_warden_mediation_moot}There is nothing left for us to carry. Whatever you took up on our account is already settled."));
        }

        private bool CanFileTroopOrder()
        {
            return IsOrdinaryGreyWardenLordConversation() &&
                   IsPlayerGreyWardenMember() &&
                   GwpRuntimeState.Player.Reputation >=
                       GwpTuning.TroopRequest.MinimumReputation &&
                   (PlayerTroopOrderStage)_orderStage ==
                       PlayerTroopOrderStage.None &&
                   ResolveTrainerParty()?.IsActive == true;
        }

        /// <summary>
        /// 下单选项界面。<paramref name="onChosen"/> 为空时沿用当面下单；使者送单时
        /// 由它把选择带走，等使者抵达灰袍手里再真正立案。
        /// </summary>
        /// <summary>对话里当面下单的入口。保持无参签名，供原版对话委托直接绑定。</summary>
        private void ShowTroopOrderInquiry() => ShowTroopOrderInquiry(null);

        private void ShowTroopOrderInquiry(Action<TroopOrderChoice>? onChosen)
        {
            int reputation = GwpRuntimeState.Player.Reputation;
            int orderLimit = GetOrderLimit(reputation);
            var choices = new List<TroopOrderChoice>();
            foreach (TroopKind kind in TroopKinds.Where(kind =>
                         reputation >= kind.MinimumReputation))
            {
                CharacterObject? troop = CharacterObject.Find(kind.TroopId);
                if (troop == null) continue;
                int kindLimit = string.Equals(kind.TroopId, GwpIds.KnightId,
                    StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(10, orderLimit / 3)
                    : orderLimit;
                foreach (int count in new[] { 10, 20, 40, 60, 80 }
                             .Where(count => count <= kindLimit))
                {
                    choices.Add(new TroopOrderChoice
                    {
                        Kind = kind,
                        Count = count,
                        Price = GetPrice(kind, count, reputation)
                    });
                }
            }

            var elements = choices.Select(choice =>
            {
                CharacterObject? troop = CharacterObject.Find(
                    choice.Kind.TroopId);
                string label = GwpText.Get(
                    "{=gwp_player_troop_choice}{VAR_1} × {VAR_2} — {VAR_3} denars",
                    "VAR_1", choice.Count,
                    "VAR_2", troop?.Name?.ToString() ?? choice.Kind.TroopId,
                    "VAR_3", choice.Price);
                return new InquiryElement(choice, label, null, true,
                    string.Empty);
            }).ToList();
            if (elements.Count == 0) return;

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    GwpText.Get("{=gwp_player_troop_order_title}Place a troop order"),
                    GwpText.Get("{=gwp_player_troop_order_description}Your Grey Warden standing sets the maximum order and available branches."),
                    elements, true, 1, 1,
                    GwpText.Get("{=gwp_player_troop_order_confirm}Send order"),
                    GwpText.Get("{=gwp_cancel}Cancel"),
                    selected =>
                    {
                        TroopOrderChoice? choice = selected.FirstOrDefault()
                            ?.Identifier as TroopOrderChoice;
                        if (choice == null) return;
                        if (onChosen != null) { onChosen(choice); return; }
                        OpenOrderPayment(choice);
                    },
                    _ => { }),
                true);
        }

        /// <summary>使者能不能替玩家送这一单：手上没有在办订单，且练兵长还在。</summary>
        internal static bool CanFileCourierOrder() =>
            _instance != null &&
            (PlayerTroopOrderStage)_instance._orderStage == PlayerTroopOrderStage.None &&
            _instance.ResolveTrainerParty() != null;

        /// <summary>让玩家先挑好兵种数量，选择交给使者带走，此刻还没立案。</summary>
        internal static void ShowCourierOrderInquiry(Action<string, int, int> onChosen)
        {
            if (_instance == null) return;
            _instance.ShowTroopOrderInquiry(choice =>
                onChosen(choice.Kind.TroopId, choice.Count, choice.Price));
        }

        /// <summary>
        /// 使者把单据送到了。这里才真正立案，并且记下订金已经在出发时预付过——
        /// 交付时不能再向玩家收第二次。
        /// </summary>
        internal static bool FileCourierOrder(string troopId, int count, int price)
        {
            if (_instance == null ||
                (PlayerTroopOrderStage)_instance._orderStage != PlayerTroopOrderStage.None)
                return false;
            TroopKind? kind = TroopKinds.FirstOrDefault(candidate =>
                string.Equals(candidate.TroopId, troopId, StringComparison.OrdinalIgnoreCase));
            if (kind == null || count <= 0 || CharacterObject.Find(troopId) == null) return false;

            _instance.FileTroopOrder(new TroopOrderChoice
            {
                Kind = kind,
                Count = count,
                Price = Math.Max(0, price)
            }, alreadyCollected: true);
            if ((PlayerTroopOrderStage)_instance._orderStage == PlayerTroopOrderStage.None)
                return false;
            return true;
        }

        /// <summary>当面下单：订金到交付时才收。保持无参委托签名。</summary>
        /// <summary>
        /// 当面下单的付款：开原版交易界面，跟野外罚金走同一套。玩家可以用金币也可以
        /// 用货物折价，界面里的一键平衡照常可用。谈成了才立案；关掉或谈不拢就当没下单。
        /// 使者送单那一路不走这里——订金在出发时随队扣走。
        /// </summary>
        private void OpenOrderPayment(TroopOrderChoice choice)
        {
            Hero? lord = Hero.OneToOneConversationHero;
            MobileParty? lordParty = lord?.PartyBelongedTo;
            if (lord == null || lordParty?.IsActive != true ||
                (PlayerTroopOrderStage)_orderStage != PlayerTroopOrderStage.None)
                return;

            // selectionOnly：界面只让玩家挑用什么付，不做即时转移。挑完由本类实扣，
            // 折价总额记进公共金库——买兵的钱归公库，不进经手领主的口袋。
            var payment = new GwpAssetPayment(Hero.MainHero, lord,
                MobileParty.MainParty.Party, lordParty.Party,
                choice.Price, choice.Price, selectionOnly: true);
            BarterManager manager = Campaign.Current.BarterManager;
            BarterManager.BarterBeginEventDelegate original = manager.BarterBegin;
            BarterManager.BarterCloseEventDelegate? closed = null;
            closed = () =>
            {
                manager.Closed -= closed;
                SettleOrderPayment(payment, choice);
            };
            manager.Closed += closed;
            try
            {
                manager.BarterBegin = data =>
                {
                    payment.PrepareCatalogue(data);
                    original?.Invoke(data);
                };
                manager.StartBarterOffer(Hero.MainHero, lord,
                    MobileParty.MainParty.Party, lordParty.Party, null,
                    (item, data, obj) => false, 0, false, payment.Entries);
            }
            catch (Exception ex)
            {
                manager.Closed -= closed;
                GwpFaultTrace.Write("TROOP_ORDER_PAYMENT_OPEN_FAILED", details: ex.ToString());
            }
            finally { manager.BarterBegin = original; }
        }

        /// <summary>
        /// 买兵的钱可多不可少：凑不齐订价就不立案。凑齐了才实扣金币与货物，
        /// 折价总额计入公共金库，然后立案。
        /// </summary>
        private void SettleOrderPayment(GwpAssetPayment payment, TroopOrderChoice choice)
        {
            if (!payment.Applied || !payment.Valid) return;
            if (payment.Paid < choice.Price)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_player_troop_order_short}That does not cover the price, so no order is placed."),
                    Colors.Yellow));
                return;
            }
            if ((PlayerTroopOrderStage)_orderStage != PlayerTroopOrderStage.None) return;

            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;

            int gold = Math.Max(0, payment.SelectedGold);
            List<ItemRosterElement> goods = payment.SelectedGoods;
            // 先核对库存再动手，避免扣到一半停在中间。
            foreach (ItemRosterElement element in goods)
                if (player.ItemRoster.Where(x => x.EquipmentElement.Equals(element.EquipmentElement))
                        .Sum(x => x.Amount) < element.Amount)
                    return;
            if (Hero.MainHero.Gold < gold) return;

            if (gold > 0)
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, gold, true);
            foreach (ItemRosterElement element in goods)
                player.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            PoliceResourceManager.CreditJudicialTreasuryFromCourier(payment.Paid);

            FileTroopOrder(choice, alreadyCollected: true);
        }

        /// <summary>
        /// 订金一律在下单这一刻结清：当面下单当场收，使者送单则出发时已随队扣走。
        /// 交付环节因此不再收钱，也就不再需要"付不起就取消"和"先欠着"两条旧路径。
        /// </summary>
        private void FileTroopOrder(TroopOrderChoice choice, bool alreadyCollected)
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                PlayerTroopOrderStage.None)
                return;
            if (!alreadyCollected &&
                !PoliceResourceManager.TryCollectPlayerRequestPayment(choice.Price))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_player_troop_order_unaffordable}You cannot put up the price, so no order is placed."),
                    Colors.Yellow));
                return;
            }
            _orderedTroopId = choice.Kind.TroopId;
            _orderedCount = choice.Count;
            _orderPrice = choice.Price;
            _orderStage = (int)PlayerTroopOrderStage.Training;
            _filedHour = CampaignTime.Now.ToHours;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _isOrderedTroopUpgradeLocked = false;
            _stockSourcePartyId = string.Empty;
            _stockRendezvousSettlementId = string.Empty;
            _stockStayStartHour = -1d;
            _lastStockSourcePartyId = string.Empty;
            MobileParty? trainer = ResolveTrainerParty();
            CharacterObject? target = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_FILED",
                    "target=" + _orderedTroopId +
                    "; requested=" + _orderedCount +
                    "; ready=" + CountHealthy(trainer, target) +
                    "; price=" + _orderPrice);
            }
            InformationManager.DisplayMessage(new InformationMessage(alreadyCollected
                ? GwpText.Get("{=gwp_player_troop_order_filed_prepaid}Your riders handed the order and the coin to the Wardens. The Training Warden will train the requested troops and bring them to you herself.")
                : GwpText.Get("{=gwp_player_troop_order_filed}The Training Warden has your order and the price is paid into the public treasury. She will train the requested troops and bring them to you."),
                Colors.Cyan));
        }

        private void OnHourlyTick()
        {
            if ((PlayerTroopOrderStage)_orderStage ==
                PlayerTroopOrderStage.None)
                return;

            MobileParty? trainer = ResolveTrainerParty();
            CharacterObject? target = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive != true || target == null ||
                !PoliceEnforcementBehavior.TryReservePartyForPlayerRequest(
                    trainer))
                return;

            // 订单的人装在随行练兵队里：练兵官只管跑腿、调货和最后送货，
            // 名额和超编惩罚都落在练兵队自己头上。拉不起来时落回练兵官，
            // 行为与改动前一致。
            MobileParty pool = AdvanceCohort(trainer, target) ?? trainer;
            int ready = CountHealthy(pool, target);
            LockOrderedTroopIfReady(pool, target);
            if (ready < _orderedCount)
            {
                _orderStage = (int)PlayerTroopOrderStage.Training;
                AdvanceStockCollection(trainer, target);
                // 调货刚卸在练兵官手上，立刻转进练兵队再清点。
                if (pool != trainer) TopUpCohort(trainer, pool, target);
                ready = CountHealthy(pool, target);
                LockOrderedTroopIfReady(pool, target);
                if (ready >= _orderedCount)
                {
                    ReleaseStockRendezvous("order_stock_ready");
                }
                else
                {
                    TrainForOrderIfDue(pool, target);
                    return;
                }
            }

            ReleaseStockRendezvous("order_stock_ready");
            _orderStage = (int)PlayerTroopOrderStage.Delivering;
            if (_nextContactHour < 0d)
                _nextContactHour = CampaignTime.Now.ToHours;
            if (CampaignTime.Now.ToHours >= _nextContactHour)
                MoveTrainerToPlayer(trainer);
        }

        /// <summary>
        /// 拆编重训：把下游的老兵降回订单要的兵种。升级树是单向的——玩家订最低级兵时
        /// 没有任何兵能升成它，喂再多经验也凑不出来，只能从下游降回去。
        /// 训练永远优先：只有能升上来的人填不满这张订单时，才动已经练出来的兵，
        /// 而且先降**最接近**目标的那一级，尽量少糟蹋本事。订最高级兵时下游为空，
        /// 这里自然什么都不做。
        /// </summary>
        private void DowngradeForOrderIfDue(MobileParty trainer,
            CharacterObject target, int trainableSupply)
        {
            int needed = _orderedCount - CountHealthy(trainer, target);
            if (needed <= 0 || trainableSupply >= needed) return;
            int shortfall = needed - trainableSupply;

            var descendants = trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    element.Character != target &&
                    element.Number - element.WoundedNumber > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    // 目标能升到它 ⇒ 它可以降回目标。
                    CanReachTarget(target, element.Character,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .OrderBy(element => element.Character.Tier)
                .ThenBy(element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (descendants.Count == 0) return;

            int budget = Math.Min(shortfall,
                GwpTuning.TroopRequest.PlayerOrderDowngradePerInterval);
            int moved = 0;
            foreach (TroopRosterElement element in descendants)
            {
                if (moved >= budget) break;
                int healthy = Math.Max(0, element.Number - element.WoundedNumber);
                int take = Math.Min(healthy, budget - moved);
                if (take <= 0) continue;
                trainer.MemberRoster.AddToCounts(element.Character, -take, false, 0);
                trainer.MemberRoster.AddToCounts(target, take, false, 0);
                moved += take;
            }
            if (moved <= 0) return;

            GwpAiDiagnostics.WriteAction(trainer, "PLAYER_TROOP_ORDER_DOWNGRADED",
                "target=" + target.StringId +
                "; downgraded=" + moved +
                "; trainable=" + trainableSupply +
                "; stillNeeded=" + Math.Max(0, needed - moved) +
                "; ready=" + CountHealthy(trainer, target));
        }

        private void TrainForOrderIfDue(MobileParty trainer,
            CharacterObject target)
        {
            double now = CampaignTime.Now.ToHours;
            if (_lastOrderXpHour >= 0d &&
                now - _lastOrderXpHour <
                GwpTuning.TroopRequest.PlayerOrderXpIntervalHours)
                return;
            _lastOrderXpHour = now;

            List<TroopRosterElement> cohorts = trainer.MemberRoster
                .GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    element.Character != target &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0 &&
                    CanReachTarget(element.Character, target,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
            int trainableSupply = cohorts.Sum(cohort =>
                Math.Max(0, cohort.Number - cohort.WoundedNumber));
            int totalXp = 0;
            foreach (TroopRosterElement cohort in cohorts)
            {
                int xp = cohort.Number *
                         GwpTuning.TroopRequest.PlayerOrderXpPerTroop;
                trainer.MemberRoster.AddXpToTroop(cohort.Character, xp);
                totalXp += xp;
            }

            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_XP_GRANTED",
                "target=" + target.StringId +
                "; requested=" + _orderedCount +
                "; ready=" + CountHealthy(trainer, target) +
                "; targetUpgradeLocked=" +
                _isOrderedTroopUpgradeLocked +
                "; cohorts=" + cohorts.Count +
                "; trainable=" + trainableSupply +
                "; xp=" + totalXp +
                "; nativeUpgradePending=true");

            DowngradeForOrderIfDue(trainer, target, trainableSupply);
        }

        private void LockOrderedTroopIfReady(MobileParty pool,
            CharacterObject target)
        {
            if (_isOrderedTroopUpgradeLocked ||
                (PlayerTroopOrderStage)_orderStage ==
                    PlayerTroopOrderStage.None ||
                (_orderStage != (int)PlayerTroopOrderStage.Delivering &&
                 CountHealthy(pool, target) < _orderedCount))
                return;

            _isOrderedTroopUpgradeLocked = true;
            GwpAiDiagnostics.WriteAction(pool,
                "PLAYER_TROOP_ORDER_TARGET_LOCKED",
                "troop=" + target.StringId +
                "; requested=" + _orderedCount +
                "; ready=" + CountHealthy(pool, target) +
                "; stage=" + (PlayerTroopOrderStage)_orderStage);
        }

        /// <summary>
        /// 队里有没有人能升成这个兵种。没有的话（比如订单要的是最低级兵），
        /// 把升级偏好钉在它身上毫无用处——反而会把新兵往那条线上赶，
        /// 正好吃掉订单要的人。
        /// </summary>
        internal static bool HasTrainableCohort(MobileParty? party,
            CharacterObject? target)
        {
            if (party?.MemberRoster == null || target == null) return false;
            return party.MemberRoster.GetTroopRoster().Any(element =>
                element.Character != null && !element.Character.IsHero &&
                element.Number - element.WoundedNumber > 0 &&
                element.Character != target &&
                GwpCommon.IsGreyWardenTroop(element.Character) &&
                element.Character.UpgradeTargets.Length > 0 &&
                CanReachTarget(element.Character, target,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }

        private static bool CanReachTarget(CharacterObject current,
            CharacterObject target, HashSet<string> visited)
        {
            if (current == target) return true;
            if (!visited.Add(current.StringId)) return false;
            foreach (CharacterObject? next in current.UpgradeTargets)
            {
                if (next != null && CanReachTarget(next, target,
                        new HashSet<string>(visited,
                            StringComparer.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private void AdvanceStockCollection(MobileParty trainer,
            CharacterObject target)
        {
            if (CountHealthy(trainer, target) >= _orderedCount)
                return;
            if (CountHealthy(GetOutgoingBatches(trainer, target)) <= 0)
            {
                ReleaseStockRendezvous("trainer_has_no_exchange_stock");
                return;
            }

            MobileParty? source = ResolveStockSourceParty();
            Settlement? rendezvous = ResolveStockRendezvousSettlement();
            bool hasStoredRendezvous =
                !string.IsNullOrWhiteSpace(_stockSourcePartyId) ||
                !string.IsNullOrWhiteSpace(
                    _stockRendezvousSettlementId);
            if (hasStoredRendezvous &&
                (source == null || rendezvous == null ||
                 !IsAssignedStockSourceValid(source, target)))
            {
                ReleaseStockRendezvous("source_roster_or_role_changed");
                source = null;
                rendezvous = null;
            }

            if (source == null || rendezvous == null)
            {
                source = FindNextStockSource(trainer, target);
                if (source == null) return;

                rendezvous =
                    GreyWardenTrainingBehavior.FindRendezvousSettlement(
                        trainer, source);
                if (rendezvous == null ||
                    !PoliceEnforcementBehavior
                        .TryReservePartyForPlayerRequest(source))
                    return;

                _stockSourcePartyId = source.StringId;
                _stockRendezvousSettlementId = rendezvous.StringId;
                _stockStayStartHour = -1d;
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_STOCK_RENDEZVOUS_ASSIGNED",
                    "source=" + source.StringId +
                    "; settlement=" + rendezvous.StringId +
                    "; target=" + target.StringId +
                    "; requested=" + _orderedCount +
                    "; ready=" + CountHealthy(trainer, target) +
                    "; sourceAvailable=" +
                    CountHealthy(GetIncomingBatches(source, target)));
            }

            if (trainer.MapEvent is { IsFinalized: false } ||
                source.MapEvent is { IsFinalized: false })
            {
                ResetStockStayIfNeeded(trainer, source, rendezvous,
                    "party_in_map_event");
                return;
            }

            bool bothInside = trainer.CurrentSettlement == rendezvous &&
                              source.CurrentSettlement == rendezvous;
            if (!bothInside)
            {
                ResetStockStayIfNeeded(trainer, source, rendezvous,
                    "party_left_rendezvous");
                GreyWardenPartyDesireBehavior.RequestVisit(trainer,
                    rendezvous,
                    validHours: GwpTuning.Training.MovementIntentHours);
                GreyWardenPartyDesireBehavior.RequestVisit(source,
                    rendezvous,
                    validHours: GwpTuning.Training.MovementIntentHours);
                return;
            }

            if (_stockStayStartHour < 0d)
            {
                _stockStayStartHour = CampaignTime.Now.ToHours;
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_STOCK_STAY_STARTED",
                    "source=" + source.StringId +
                    "; settlement=" + rendezvous.StringId +
                    "; hours=" + GwpTuning.Training.ExchangeStayHours);
            }

            GreyWardenPartyDesireBehavior.RequestVisit(trainer, rendezvous,
                validHours: GwpTuning.Training.MovementIntentHours);
            GreyWardenPartyDesireBehavior.RequestVisit(source, rendezvous,
                validHours: GwpTuning.Training.MovementIntentHours);
            if (CampaignTime.Now.ToHours < _stockStayStartHour +
                GwpTuning.Training.ExchangeStayHours)
                return;

            ExchangeStockAtRendezvous(trainer, source, rendezvous, target);
            _lastStockSourcePartyId = source.StringId;
            ReleaseStockRendezvous("rendezvous_exchange_completed");
        }

        private MobileParty? FindNextStockSource(MobileParty trainer,
            CharacterObject target)
        {
            return PoliceStats.GetAllPoliceParties()
                .Where(party => party != trainer &&
                    party.AttachedTo == null &&
                    GreyWardenTrainingBehavior
                        .IsFreeForTrainingExchange(party) &&
                    CountHealthy(GetIncomingBatches(party, target)) > 0)
                .OrderBy(party => string.Equals(party.StringId,
                    _lastStockSourcePartyId,
                    StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(party => party.GetPosition2D.Distance(
                    trainer.GetPosition2D))
                .ThenBy(party => party.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool IsAssignedStockSourceValid(MobileParty source,
            CharacterObject target)
        {
            return source.IsActive && source.IsLordParty &&
                   !source.IsDisbanding &&
                   source.LeaderHero?.IsActive == true &&
                   source.Army == null && source.AttachedTo == null &&
                   CountHealthy(GetIncomingBatches(source, target)) > 0;
        }

        private int ExchangeStockAtRendezvous(MobileParty trainer,
            MobileParty source, Settlement rendezvous,
            CharacterObject target)
        {
            int missing = Math.Max(0, _orderedCount -
                CountHealthy(trainer, target));
            List<TroopRosterElement> outgoing =
                GetOutgoingBatches(trainer, target);
            List<TroopRosterElement> incoming =
                GetIncomingBatches(source, target);
            int requestedSwap = Math.Min(missing,
                Math.Min(CountHealthy(outgoing), CountHealthy(incoming)));
            if (requestedSwap <= 0) return 0;

            // Both real parties have already reached the same settlement.
            // Stage both sides outside the live party rosters before inserting
            // either side, so this remains a physical one-for-one exchange and
            // neither nearly-full party is ever transiently over capacity.
            TroopRoster outgoingBuffer =
                TroopRoster.CreateDummyTroopRoster();
            TroopRoster incomingBuffer =
                TroopRoster.CreateDummyTroopRoster();
            int stagedOut = TransferHealthyBatches(trainer.MemberRoster,
                outgoingBuffer, outgoing, requestedSwap);
            int stagedIn = TransferHealthyBatches(source.MemberRoster,
                incomingBuffer, incoming, requestedSwap);
            int exchangeCount = Math.Min(stagedOut, stagedIn);

            int movedIn = TransferHealthyBatches(incomingBuffer,
                trainer.MemberRoster,
                incomingBuffer.GetTroopRoster().ToList(), exchangeCount);
            int movedOut = TransferHealthyBatches(outgoingBuffer,
                source.MemberRoster,
                outgoingBuffer.GetTroopRoster().ToList(), exchangeCount);

            if (incomingBuffer.TotalManCount > 0)
                TransferHealthyBatches(incomingBuffer, source.MemberRoster,
                    incomingBuffer.GetTroopRoster().ToList(),
                    incomingBuffer.TotalManCount);
            if (outgoingBuffer.TotalManCount > 0)
                TransferHealthyBatches(outgoingBuffer, trainer.MemberRoster,
                    outgoingBuffer.GetTroopRoster().ToList(),
                    outgoingBuffer.TotalManCount);

            int completed = Math.Min(movedIn, movedOut);
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_STOCK_EXCHANGED",
                "source=" + source.StringId +
                "; settlement=" + rendezvous.StringId +
                "; target=" + target.StringId +
                "; requestedSwap=" + requestedSwap +
                "; stagedOut=" + stagedOut +
                "; stagedIn=" + stagedIn +
                "; movedIn=" + movedIn +
                "; movedOut=" + movedOut +
                "; completed=" + completed +
                "; trainerReady=" + CountHealthy(trainer, target));
            return completed;
        }

        private static List<TroopRosterElement> GetOutgoingBatches(
            MobileParty trainer, CharacterObject target)
        {
            return trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character != target &&
                    !CanReachTarget(element.Character, target,
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase)))
                .OrderBy(element => element.Character.Tier)
                .ThenBy(element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<TroopRosterElement> GetIncomingBatches(
            MobileParty source, CharacterObject target)
        {
            return source.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    CanReachTarget(element.Character, target,
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase)))
                .OrderBy(element => element.Character == target ? 0 : 1)
                .ThenBy(element => element.Character.Tier)
                .ThenBy(element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int CountHealthy(
            IEnumerable<TroopRosterElement> batches)
        {
            return batches.Sum(element =>
                Math.Max(0, element.Number - element.WoundedNumber));
        }

        private void ResetStockStayIfNeeded(MobileParty trainer,
            MobileParty source, Settlement rendezvous, string reason)
        {
            if (_stockStayStartHour < 0d) return;
            _stockStayStartHour = -1d;
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_STOCK_STAY_RESET",
                "source=" + source.StringId +
                "; settlement=" + rendezvous.StringId +
                "; reason=" + reason);
        }

        private MobileParty? ResolveStockSourceParty()
        {
            if (string.IsNullOrWhiteSpace(_stockSourcePartyId))
                return null;
            return PoliceStats.GetAllPoliceParties().FirstOrDefault(party =>
                string.Equals(party.StringId, _stockSourcePartyId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private Settlement? ResolveStockRendezvousSettlement()
        {
            return string.IsNullOrWhiteSpace(
                _stockRendezvousSettlementId)
                ? null
                : Settlement.Find(_stockRendezvousSettlementId);
        }

        private void ReleaseStockRendezvous(string reason)
        {
            if (string.IsNullOrWhiteSpace(_stockSourcePartyId) &&
                string.IsNullOrWhiteSpace(
                    _stockRendezvousSettlementId))
                return;

            MobileParty? source = ResolveStockSourceParty();
            MobileParty? trainer = ResolveTrainerParty();
            string sourceId = _stockSourcePartyId;
            string settlementId = _stockRendezvousSettlementId;
            _stockSourcePartyId = string.Empty;
            _stockRendezvousSettlementId = string.Empty;
            _stockStayStartHour = -1d;

            if (source?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(source);
                GreyWardenPartyDesireBehavior
                    .RequestImmediateRethink(source);
            }
            if (trainer?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_STOCK_RENDEZVOUS_RELEASED",
                    "source=" + sourceId +
                    "; settlement=" + settlementId +
                    "; reason=" + reason);
            }
        }

        private static int TransferHealthyBatches(TroopRoster source,
            TroopRoster destination, IEnumerable<TroopRosterElement> batches,
            int requested)
        {
            int moved = 0;
            foreach (TroopRosterElement batch in batches)
            {
                if (moved >= requested) break;
                TroopRosterElement current = source.GetTroopRoster()
                    .FirstOrDefault(element =>
                        element.Character == batch.Character);
                if (current.Character == null) continue;
                int healthy = Math.Max(0,
                    current.Number - current.WoundedNumber);
                int take = Math.Min(healthy, requested - moved);
                if (take <= 0) continue;
                source.AddToCounts(batch.Character, -take, false, 0);
                destination.AddToCounts(batch.Character, take, false, 0);
                moved += take;
            }
            return moved;
        }

        private bool PrepareDeliveryConversation()
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                !GreyWardenFamilyBehavior.IsTrainingHero(
                    Hero.OneToOneConversationHero))
                return false;
            CharacterObject? troop = CharacterObject.Find(_orderedTroopId);
            MobileParty? pool = ResolveOrderPool(ResolveTrainerParty());
            if (troop == null ||
                CountHealthy(pool, troop) < _orderedCount)
                return false;

            MBTextManager.SetTextVariable("GWP_PLAYER_TROOP_DELIVERY_OFFER",
                GwpText.Get("{=gwp_player_troop_delivery_offer}Your order is ready: {VAR_1} {VAR_2}. The {VAR_3} denars were settled when you placed it; nothing further is owed.",
                    "VAR_1", _orderedCount, "VAR_2", troop.Name,
                    "VAR_3", _orderPrice));
            return true;
        }

        private bool IsReadyDeliveryConversation()
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                !GreyWardenFamilyBehavior.IsTrainingHero(
                    Hero.OneToOneConversationHero))
                return false;
            CharacterObject? troop = CharacterObject.Find(_orderedTroopId);
            return troop != null &&
                   CountHealthy(ResolveOrderPool(ResolveTrainerParty()), troop)
                       >= _orderedCount;
        }

        private void CompleteTroopDelivery()
        {
            MobileParty? trainer = ResolveTrainerParty();
            MobileParty? pool = ResolveOrderPool(trainer);
            CharacterObject? troop = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive != true || pool?.IsActive != true ||
                troop == null ||
                CountHealthy(pool, troop) < _orderedCount ||
                // 订金在下单时就已结清，交付不再收款。
                MobileParty.MainParty?.IsActive != true)
                return;

            pool.MemberRoster.AddToCounts(troop, -_orderedCount,
                insertAtFront: false, woundedCount: 0);
            MobileParty.MainParty.MemberRoster.AddToCounts(troop, _orderedCount,
                insertAtFront: false, woundedCount: 0);
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_DELIVERED",
                "troop=" + troop.StringId +
                "; count=" + _orderedCount +
                "; price=" + _orderPrice +
                "; treasury=" +
                PoliceResourceManager.GetJudicialTreasuryBalance());
            QueueFinishDeliveryEncounter();
            StopPlayerContact(trainer);
            ReleaseTrainer(trainer);
            ClearOrder();
        }


        private void CancelTroopOrder()
        {
            MobileParty? trainer = ResolveTrainerParty();
            QueueFinishDeliveryEncounter();
            StopPlayerContact(trainer);
            ReleaseTrainer(trainer);
            ClearOrder();
        }


        private void QueueFinishDeliveryEncounter()
        {
            if (!PlayerEncounter.IsActive) return;
            PlayerEncounter.LeaveEncounter = true;
            if (Campaign.Current?.ConversationManager == null)
            {
                FinishDeliveryEncounter();
                return;
            }

            Campaign.Current.ConversationManager.ConversationEndOneShot -=
                FinishDeliveryEncounter;
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                FinishDeliveryEncounter;
        }

        private void FinishDeliveryEncounter()
        {
            MobileParty? trainer = ResolveTrainerParty();
            GwpCommon.TryFinishPlayerEncounter();
            StopPlayerContact(trainer);
            if ((PlayerTroopOrderStage)_orderStage ==
                PlayerTroopOrderStage.None)
                ReleaseTrainer(trainer);
            if (trainer?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_ENCOUNTER_FINISHED",
                    "stage=" + (PlayerTroopOrderStage)_orderStage +
                    "; encounterActive=" + PlayerEncounter.IsActive);
            }
        }

        private void MoveTrainerToPlayer(MobileParty trainer)
        {
            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;
            if (player.CurrentSettlement != null)
            {
                GreyWardenPartyDesireBehavior.RequestVisit(trainer,
                    player.CurrentSettlement,
                    GreyWardenPartyDesireBehavior.PlayerRequestScore,
                    validHours: GwpTuning.Training.MovementIntentHours);
                return;
            }

            // 这里以前分两支：远了下 Approach 欲望，近了就 ClearIntent +
            // SetDoNotMakeNewDecisions(false) + SetMoveEngageParty 交还原版。
            // 交还的那一下正是毛病——原版当小时就把 EngageParty 改回
            // PatrolAroundPoint，下一小时我们又下一次 Approach，于是练兵官在
            // "奔玩家"和"绕城巡逻"之间每小时翻一次，永远送不到。
            //
            // 而且 Approach 下的是目标**当时位置**的快照点，玩家一动就追空。
            // 改为全程 Rush：它留在欲望竞价里（原版盖不掉），落地时由
            // GwpPlayerEnforcementEngageActionPatch 翻成原版 EngageParty，持续跟着玩家走。
            GreyWardenPartyDesireBehavior.RequestRush(trainer, player,
                GreyWardenPartyDesireBehavior.PlayerRequestScore,
                validHours: GwpTuning.Training.MovementIntentHours);
        }

        private static void StopPlayerContact(MobileParty? trainer)
        {
            if (trainer?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            try
            {
                trainer.Ai.SetDoNotMakeNewDecisions(false);
                trainer.SetMoveModeHold();
                trainer.Ai.RethinkAtNextHourlyTick = true;
            }
            catch { }
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty,
            PartyBase defenderParty)
        {
            _ = attackerParty;
            _ = defenderParty;
            if ((PlayerTroopOrderStage)_orderStage !=
                PlayerTroopOrderStage.Delivering)
                return;

            MobileParty? trainer = ResolveTrainerParty();
            if (trainer == null ||
                !mapEvent.InvolvedParties.Any(p => p.MobileParty == trainer) ||
                !mapEvent.InvolvedParties.Any(p =>
                    p.MobileParty?.IsMainParty == true))
                return;
            if (PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                _nextContactHour = CampaignTime.Now.ToHours +
                                   GwpTuning.PlayerRequests.DeferredContactHours;
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_CONTACT_STARTED",
                    "troop=" + _orderedTroopId +
                    "; count=" + _orderedCount +
                    "; price=" + _orderPrice +
                    "; retryAfterHour=" + _nextContactHour);
                try { PlayerEncounter.DoMeeting(); }
                catch { }
            }
        }

        internal static bool IsPendingAutomaticConversation(Hero? hero)
        {
            return _instance != null &&
                    (PlayerTroopOrderStage)_instance._orderStage ==
                        PlayerTroopOrderStage.Delivering &&
                    GreyWardenFamilyBehavior.IsTrainingHero(hero);
        }


        private MobileParty? ResolveTrainerParty()
        {
            Hero? holder = GreyWardenFamilyBehavior.GetLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.Training);
            return holder?.PartyBelongedTo?.IsActive == true
                ? holder.PartyBelongedTo
                : null;
        }

        private static void ReleaseTrainer(MobileParty? trainer)
        {
            if (trainer?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
        }

        private void ClearOrder()
        {
            // 交付、取消、清空都从这里过：练兵队一定要收，剩下的人还给练兵官，
            // 不许把一支无领主队丢在地图上。
            DisbandCohort("order_cleared");
            ReleaseStockRendezvous("order_cleared");
            _orderedTroopId = string.Empty;
            _orderedCount = 0;
            _orderPrice = 0;
            _orderStage = (int)PlayerTroopOrderStage.None;
            _filedHour = -1d;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _isOrderedTroopUpgradeLocked = false;
            _lastStockSourcePartyId = string.Empty;
            _cohortPartyId = string.Empty;
        }

        private static int CountHealthy(MobileParty? party,
            CharacterObject? troop)
        {
            if (party?.IsActive != true || troop == null) return 0;
            TroopRosterElement element = party.MemberRoster.GetTroopRoster()
                .FirstOrDefault(candidate => candidate.Character == troop);
            if (element.Character == null) return 0;
            return Math.Max(0, element.Number - element.WoundedNumber);
        }

        private static int GetOrderLimit(int reputation)
        {
            if (reputation >= GwpTuning.TroopRequest.EliteDiscountReputation)
                return GwpTuning.TroopRequest.EliteOrderLimit;
            if (reputation >= GwpTuning.TroopRequest.KnightReputation)
                return GwpTuning.TroopRequest.KnightOrderLimit;
            if (reputation >= GwpTuning.TroopRequest.VeteranReputation)
                return GwpTuning.TroopRequest.VeteranOrderLimit;
            return GwpTuning.TroopRequest.LowStandingOrderLimit;
        }

        private static int GetPrice(TroopKind kind, int count, int reputation)
        {
            int discount = reputation >= 80 ? 30 :
                reputation >= 60 ? 20 :
                reputation >= 40 ? 10 : 0;
            return Math.Max(1,
                kind.PricePerTroop * count * (100 - discount) / 100);
        }

        private static bool IsPlayerGreyWardenMember()
        {
            PlayerBountyBehavior? behavior = Campaign.Current
                ?.GetCampaignBehavior<PlayerBountyBehavior>();
            return behavior?.IsRecruitedByGreyWardens == true;
        }

        private static bool IsOrdinaryGreyWardenLordConversation()
        {
            Hero? hero = Hero.OneToOneConversationHero;
            if (!GwpCommon.IsGreyWardenLord(hero)) return false;
            MobileParty? party = MobileParty.ConversationParty;
            if (party == null) return true;
            return !GwpCommon.IsPatrolParty(party) &&
                   !GwpCommon.IsEnforcementDelayPatrolParty(party);
        }
    }
}
