using System;
using System.Collections.Generic;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
using TaleWorlds.CampaignSystem;
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
    /// 订单正式登记时结清声望，练兵官训好后亲自交付真实兵员。
    /// </summary>
    public sealed partial class GreyWardenTroopRequestBehavior : CampaignBehaviorBase
    {
        private static readonly string[] OrderTroopIds =
        {
            GwpIds.ZealotId, GwpIds.NewRecruitId, GwpIds.PoliceRecruitId,
            GwpIds.HeavyInfantryId, GwpIds.ArcherId, GwpIds.KnightId
        };

        private static GreyWardenTroopRequestBehavior? _instance;
        private string _orderedTroopId = string.Empty;
        private int _orderedCount;
        private string _orderComposition = string.Empty;
        private IReadOnlyList<(CharacterObject Troop, int Count)> _orderLines = Array.Empty<(CharacterObject, int)>();
        private int _orderPrice;
        private int _orderPriceUnits;
        private bool _orderPaidWithReputation;
        private int _orderStage;
        private double _filedHour = -1d;
        private double _lastOrderXpHour = -1d;
        private double _nextContactHour = -1d;
        private string _stockSourcePartyId = string.Empty;
        private string _stockTroopId = string.Empty;
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
            public string PaymentText { get; set; } = string.Empty;
            public CampaignTime FiledTime { get; set; }
            public PlayerTroopOrderStage Stage { get; set; }
            public WardenDuty? PausedBy { get; set; }
            public bool WaitingForCapacity { get; set; }
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
            dataStore.SyncData("GWPP_PlayerTroopOrderComposition", ref _orderComposition);
            dataStore.SyncData("GWPP_PlayerTroopOrderPrice",
                ref _orderPrice);
            dataStore.SyncData("GWPP_PlayerTroopOrderPriceUnits", ref _orderPriceUnits);
            dataStore.SyncData("GWPP_PlayerTroopOrderPaidWithReputation", ref _orderPaidWithReputation);
            dataStore.SyncData("GWPP_PlayerTroopOrderStage",
                ref _orderStage);
            dataStore.SyncData("GWPP_PlayerTroopOrderFiledHour",
                ref _filedHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderLastXpHour",
                ref _lastOrderXpHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderNextContactHour",
                ref _nextContactHour);
            dataStore.SyncData("GWPP_PlayerTroopOrderStockSourcePartyId",
                ref _stockSourcePartyId);
            dataStore.SyncData("GWPP_PlayerTroopOrderStockTroopId", ref _stockTroopId);
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
                    PlayerTroopOrderStage.None)
                return null;
            if (_instance.ResolveTrainerParty() != party) return null;
            MobileParty? cohort = _instance.ResolveCohortParty();
            return _instance._orderLines.Count == 1 ? _instance.TrainingTarget(cohort ?? party).Troop :
                _instance.TrainingTarget(cohort ?? party, cohort == null ? null : party).Troop;
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
                troop == null || _instance._orderStage == (int)PlayerTroopOrderStage.None)
                return false;

            // 单兵种与混合订单同一套逐兵预留：已备好的订单兵不升级。0 权重拦不住原版升级器
            // （只有一个升级目标或全部为 0 时仍取第一个，C#反编译 v1.4.8 PartyUpgraderCampaignBehavior.SelectPossibleUpgrade）。
            // （依据：独立审查 2026-11-15）
            return _instance.RequestedCount(troop) > 0 &&
                _instance.UnreservedHealthyTroops(party.MobileParty, troop, CountHealthy(party.MobileParty, troop)) == 0;
        }

        /// <summary>
        /// 随行练兵队这条升级分支是否通往订单兵种。练兵队没有领主，升级取向
        /// 落不到它身上，原版会把新兵平均练向各条分支——练偏的人再多也凑不齐订单。
        /// 返回 null：不是练兵队，或这个兵本来就练不成订单兵种，交还原有规则。
        /// </summary>
        internal static bool? CohortBranchServesOrder(PartyBase? party,
            CharacterObject troop, CharacterObject branch)
        {
            if (_instance == null || party?.MobileParty == null ||
                party.MobileParty != _instance.ResolveOrderPool(_instance.ResolveTrainerParty()) ||
                _instance._orderLines.Count == 1 && party.MobileParty != _instance.ResolveCohortParty())
                return null;
            CharacterObject? target = _instance.TrainingTarget(party.MobileParty).Troop;
            if (target == null || !CanReachTarget(troop, target,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                return null;
            return CanReachTarget(branch, target,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        internal static int AvailableForOrderUpgrade(PartyBase party, CharacterObject troop, int healthy)
        {
            if (_instance == null) return healthy;
            return _instance.UnreservedHealthyTroops(party.MobileParty, troop, healthy);
        }

        private int UnreservedHealthyTroops(MobileParty? party, CharacterObject troop, int healthy)
        {
            int reserved = RequestedCount(troop);
            if (reserved == 0 || healthy <= 0 || party == null) return healthy;
            // 原版每天对每支队伍每个兵种问一次，先比 id，只有练兵官才去全图找练兵队。
            if (!string.Equals(party.StringId, _cohortPartyId, StringComparison.OrdinalIgnoreCase))
            {
                if (party != ResolveTrainerParty()) return healthy;
                reserved = Math.Max(0, reserved - CountHealthy(ResolveCohortParty(), troop));
            }
            return Math.Max(0, healthy - reserved);
        }

        internal static IReadOnlyList<PlayerTroopOrderSnapshot> GetTaskSnapshots()
        {
            var result = new List<PlayerTroopOrderSnapshot>();
            if (_instance == null ||
                (PlayerTroopOrderStage)_instance._orderStage ==
                PlayerTroopOrderStage.None)
                return result;
            MobileParty? trainer = _instance.ResolveTrainerParty();
            MobileParty? pool = _instance.ResolveOrderPool(trainer);
            result.Add(new PlayerTroopOrderSnapshot
            {
                TrainerPartyId = trainer?.StringId ?? string.Empty,
                TroopName = _instance.DescribeComposition(),
                Count = _instance._orderedCount,
                ReadyCount = _instance.CountReady(pool),
                PaymentText = _instance.DescribeOrderPayment(),
                FiledTime = CampaignTime.Hours((float)Math.Max(0d,
                    _instance._filedHour)),
                Stage = (PlayerTroopOrderStage)_instance._orderStage,
                WaitingForCapacity = (PlayerTroopOrderStage)_instance._orderStage == PlayerTroopOrderStage.Delivering &&
                                     !_instance.HasRoomForTroopDelivery(),
                PausedBy = trainer == null ? null : GwpWardenDuties.BlockingDuty(trainer, WardenDuty.PlayerRequest)
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
            if (_orderStage != (int)PlayerTroopOrderStage.None)
            {
                _orderLines = ResolveOrderLines(_orderComposition, _orderedTroopId, _orderedCount);
                if (string.IsNullOrEmpty(_stockTroopId) && _orderLines.Count == 1)
                    _stockTroopId = _orderLines[0].Troop.StringId;
            }

            starter.AddPlayerLine(
                "gwp_warden_mediation_ask",
                "lord_talk_speak_diplomacy_2",
                "gwp_warden_mediation_reply",
                GwpText.Get(GwpTextCatalog.gwp_warden_mediation_ask),
                // 不要求玩家已经入会。`HasMediationRequests()` 本身已经把范围锁死在
                // "替灰袍打出来的战争"上——登记入口只有三种：制止正在发生的案件、
                // 看见灰袍在打过去帮忙、玩家自己承办的案子。这三种都是本模组自己
                // 把玩家拉进去的，善后就不该再要求他先够 20 声望入会。
                () => GwpCommon.IsOrdinaryGreyWardenLordConversation() &&
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
                GwpText.Get(GwpTextCatalog.gwp_player_troop_order_file),
                CanFileTroopOrder, null, 215);

            starter.AddDialogLine(
                "gwp_player_troop_order_file_response",
                "gwp_player_troop_order_file_response",
                "gwp_player_troop_order_return",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_order_choose),
                null, ShowTroopOrderInquiry, 215);
            starter.AddDialogLine("gwp_player_troop_order_return", "gwp_player_troop_order_return", "hero_main_options",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_order_return), null, null);

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
                GwpText.Get(GwpTextCatalog.gwp_player_troop_delivery_accept),
                PrepareDeliveryConversation,
                CompleteTroopDelivery,
                310,
                CanAcceptTroopDelivery);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_accepted",
                "gwp_player_troop_delivery_accepted",
                "close_window",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_delivery_accepted),
                null, null, 310);


            starter.AddPlayerLine(
                "gwp_player_troop_delivery_cancel",
                "gwp_player_troop_delivery_choice",
                "gwp_player_troop_delivery_cancelled",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_delivery_cancel),
                null,
                CancelTroopOrder,
                290);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_cancelled",
                "gwp_player_troop_delivery_cancelled",
                "close_window",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_delivery_cancelled),
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
                ? GwpText.Create(GwpTextCatalog.gwp_warden_mediation_done)
                : GwpText.Create(GwpTextCatalog.gwp_warden_mediation_moot));
        }

        private bool CanFileTroopOrder() =>
            GwpCommon.IsOrdinaryGreyWardenLordConversation() && CanChooseNewOrder();

        private void ShowTroopOrderInquiry()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            ShowTroopOrderScreen(0, composition =>
            {
                if (lord == Hero.OneToOneConversationHero && GwpCommon.IsOrdinaryGreyWardenLordConversation())
                    FileReputationOrder(composition);
            }, null);
        }

        internal static bool CanChooseNewOrder() =>
            _instance != null && _instance.CanStartOrder() && GwpCommon.IsPlayerGreyWardenMember();

        private bool CanStartOrder() =>
            _orderStage == (int)PlayerTroopOrderStage.None && ResolveTrainerParty()?.IsActive == true;

        private static void ShowTroopOrderScreen(int escortCount, Action<string> confirm, Action? cancel)
        {
            if (!CanChooseNewOrder())
            {
                cancel?.Invoke();
                return;
            }
            var troops = OrderTroopIds.Select(CharacterObject.Find).Where(GwpCommon.IsGreyWardenTroop).ToList();
            GwpTroopOrderState.Open(troops, escortCount, confirm, cancel);
        }

        internal static void ShowCourierOrderInquiry(int escortCount, Action<string> onChosen, Action onCancelled) =>
            ShowTroopOrderScreen(escortCount, onChosen, onCancelled);

        internal static bool CanSendCourierOrder(string composition, int escortCount) =>
            _instance != null && _instance.TryQuoteReputationOrder(
                ResolveOrderLines(composition), escortCount, out _);

        internal static bool FileCourierOrder(string troopId, int count, int legacyGold, string composition)
        {
            if (_instance == null) return false;
            var lines = ResolveOrderLines(composition, troopId, count);
            if (legacyGold <= 0) return _instance.FileReputationOrder(lines);
            if (_instance._orderStage != (int)PlayerTroopOrderStage.None || lines.Count == 0) return false;
            _instance.FileTroopOrder(lines, legacyGold, paidWithReputation: false);
            return true;
        }

        private bool TryQuoteReputationOrder(IReadOnlyList<(CharacterObject Troop, int Count)> lines,
            int escortCount, out int cost)
        {
            cost = 0;
            int maxTroops = GwpPlayerWardenTroops.MaximumCapacity;
            if (lines.Count == 0 || lines.Sum(line => (long)line.Count) > maxTroops || !CanStartOrder() ||
                !GwpCommon.IsPlayerGreyWardenMember() || MobileParty.MainParty?.IsActive != true)
            {
                MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_player_troop_order_unavailable));
                return false;
            }
            cost = lines.Sum(line => line.Count * GetUnitCost(line.Troop));
            int count = lines.Sum(line => line.Count);
            var quote = QuoteReputationOrder(cost, count, escortCount);
            TextObject? restriction = GetOrderRestriction(quote);
            if (restriction != null)
            {
                MBInformationManager.AddQuickInformation(restriction);
                return false;
            }
            return true;
        }

        internal static int GetUnitCost(CharacterObject troop) => WardenReputationRules.TroopUnits(troop.Tier);

        internal static string EncodeOrder(IEnumerable<(string TroopId, int Count)> lines) => TroopOrderComposition.Encode(lines);

        internal static (int Balance, bool HasRoom) QuoteReputationOrder(int cost, int count, int escortCount) =>
            WardenReputationRules.QuoteOrder(cost, count, PlayerBehaviorPool.ReputationUnits,
                Math.Max(0, GwpPlayerWardenTroops.Count(MobileParty.MainParty.MemberRoster) - escortCount));

        internal static TextObject? GetOrderRestriction((int Balance, bool HasRoom) quote)
        {
            if (quote.Balance < 0) return GwpText.Create(GwpTextCatalog.gwp_player_troop_order_unaffordable);
            return quote.HasRoom ? null : GwpText.Create(GwpTextCatalog.gwp_player_troop_order_capacity,
                "BUDGET", PlayerBehaviorPool.FormatReputationUnits(quote.Balance), "CAPACITY", GwpPlayerWardenTroops.Capacity(quote.Balance));
        }

        private static IReadOnlyList<(CharacterObject Troop, int Count)> ResolveOrderLines(
            string composition, string legacyTroop = "", int legacyCount = 0)
        {
            IReadOnlyList<(string TroopId, int Count)> decoded = string.IsNullOrEmpty(composition) && legacyCount > 0
                ? new[] { (legacyTroop, legacyCount) } : TroopOrderComposition.Decode(composition);
            var lines = new List<(CharacterObject Troop, int Count)>();
            foreach (var line in decoded)
            {
                if (!OrderTroopIds.Contains(line.TroopId, StringComparer.OrdinalIgnoreCase) ||
                    CharacterObject.Find(line.TroopId) is not { } troop || !GwpCommon.IsGreyWardenTroop(troop) ||
                    lines.Any(current => current.Troop == troop))
                    return Array.Empty<(CharacterObject, int)>();
                lines.Add((troop, line.Count));
            }
            return lines.OrderByDescending(line => line.Troop.Tier)
                .ThenBy(line => line.Troop.StringId, StringComparer.Ordinal).ToList();
        }

        private bool FileReputationOrder(string composition) => FileReputationOrder(ResolveOrderLines(composition));

        private bool FileReputationOrder(IReadOnlyList<(CharacterObject Troop, int Count)> lines)
        {
            if (!TryQuoteReputationOrder(lines, 0, out int cost) || !PlayerBehaviorPool.TrySpendReputationUnits(cost)) return false;
            FileTroopOrder(lines, cost, paidWithReputation: true);
            return true;
        }

        private void FileTroopOrder(IReadOnlyList<(CharacterObject Troop, int Count)> lines, int price, bool paidWithReputation)
        {
            _orderLines = lines;
            _orderedTroopId = lines[0].Troop.StringId;
            _orderedCount = lines.Sum(line => line.Count);
            _orderComposition = EncodeOrder(lines.Select(line => (line.Troop.StringId, line.Count)));
            _orderPrice = paidWithReputation ? 0 : price;
            _orderPriceUnits = paidWithReputation ? price : 0;
            _orderPaidWithReputation = paidWithReputation;
            _orderStage = (int)PlayerTroopOrderStage.Training;
            _filedHour = CampaignTime.Now.ToHours;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _stockSourcePartyId = string.Empty;
            _stockTroopId = string.Empty;
            _stockRendezvousSettlementId = string.Empty;
            _stockStayStartHour = -1d;
            _lastStockSourcePartyId = string.Empty;
#if GWP_DIAGNOSTICS
            MobileParty? trainer = ResolveTrainerParty();
            if (trainer?.IsActive == true)
                GwpAiDiagnostics.WriteAction(trainer, "PLAYER_TROOP_ORDER_FILED",
                    "composition=" + _orderComposition + "; requested=" + _orderedCount +
                    "; ready=" + CountReady(trainer) + "; price=" + FormattedPrice() +
                    "; paidWithReputation=" + _orderPaidWithReputation);
#endif
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_player_troop_order_filed, "PAYMENT", DescribeOrderPayment()), Colors.Cyan));
        }

        private string DescribeOrderPayment() => GwpText.Get(_orderPaidWithReputation
            ? GwpTextCatalog.gwp_player_troop_cost_reputation : GwpTextCatalog.gwp_player_troop_cost_denars,
            "AMOUNT", FormattedPrice());

        private string FormattedPrice() => _orderPaidWithReputation
            ? PlayerBehaviorPool.FormatReputationUnits(_orderPriceUnits > 0 ? _orderPriceUnits :
                _orderPrice * WardenReputationRules.TroopUnitsPerPoint) : _orderPrice.ToString();

        private string DescribeComposition() => string.Join(", ", _orderLines.Select(line =>
            GwpText.Get(GwpTextCatalog.gwp_player_troop_order_line, "COUNT", line.Count, "NAME", line.Troop.Name)));

        private (CharacterObject? Troop, int Count) TrainingTarget(MobileParty? pool, MobileParty? extra = null)
        {
            foreach (var line in _orderLines)
                if (CountHealthy(pool, line.Troop) + CountHealthy(extra, line.Troop) < line.Count) return line;
            return (null, 0);
        }

        private int RequestedCount(CharacterObject troop) => _orderLines.FirstOrDefault(line => line.Troop == troop).Count;

        private int CountReady(MobileParty? pool) => _orderLines.Sum(line => Math.Min(line.Count, CountHealthy(pool, line.Troop)));

        private void OnHourlyTick()
        {
            if (_orderStage == (int)PlayerTroopOrderStage.None || _orderLines.Count == 0) return;
            MobileParty? trainer = ResolveTrainerParty();
            if (trainer?.IsActive != true) return;

            MobileParty? existingCohort = ResolveCohortParty();
            if (existingCohort != null) TendCohort(trainer, existingCohort);

            if (!PoliceEnforcementBehavior.TryReservePartyForPlayerRequest(trainer)) return;
            MobileParty pool = AdvanceCohort(trainer) ?? trainer;
            var missing = TrainingTarget(pool);
            if (missing.Troop != null)
            {
                _orderStage = (int)PlayerTroopOrderStage.Training;
                if (missing.Troop.StringId != _stockTroopId)
                {
                    ReleaseStockRendezvous("order_line_changed");
                    _lastStockSourcePartyId = string.Empty;
                    _stockTroopId = missing.Troop.StringId;
                }
                AdvanceStockCollection(trainer, missing.Troop);
                if (pool != trainer) TopUpCohort(trainer, pool);
                missing = TrainingTarget(pool);
                if (missing.Troop != null)
                {
                    TrainForOrderIfDue(pool, missing.Troop);
                    return;
                }
            }

            ReleaseStockRendezvous("order_stock_ready");
            _orderStage = (int)PlayerTroopOrderStage.Delivering;
            if (!HasRoomForTroopDelivery())
            {
                if (_nextContactHour >= 0d)
                {
                    StopPlayerContact(trainer);
                    _nextContactHour = -1d;
                }
                return;
            }
            if (_nextContactHour < 0d) _nextContactHour = CampaignTime.Now.ToHours;
            if (CampaignTime.Now.ToHours >= _nextContactHour) MoveTrainerToPlayer(trainer);
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
            int needed = RequestedCount(target) - CountHealthy(trainer, target);
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
                int healthy = AvailableForOrderUpgrade(trainer.Party, element.Character, element.Number - element.WoundedNumber);
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
            int trainableSupply = cohorts.Sum(cohort => AvailableForOrderUpgrade(trainer.Party, cohort.Character,
                cohort.Number - cohort.WoundedNumber));
            int totalXp = 0;
            foreach (TroopRosterElement cohort in cohorts)
            {
                int available = AvailableForOrderUpgrade(trainer.Party, cohort.Character,
                    cohort.Number - cohort.WoundedNumber);
                int xp = available *
                         GwpTuning.TroopRequest.PlayerOrderXpPerTroop;
                trainer.MemberRoster.AddXpToTroop(cohort.Character, xp);
                totalXp += xp;
            }

            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_XP_GRANTED",
                "target=" + target.StringId +
                "; requested=" + RequestedCount(target) +
                "; ready=" + CountHealthy(trainer, target) +
                "; cohorts=" + cohorts.Count +
                "; trainable=" + trainableSupply +
                "; xp=" + totalXp +
                "; nativeUpgradePending=true");

            DowngradeForOrderIfDue(trainer, target, trainableSupply);
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

        /// <summary>
        /// 订单兵种已经到手的人数：练兵官身上的，加上已经拨进练兵队的。
        /// 调货只补这之外的差额。
        /// </summary>
        private int CountOrderedOnHand(MobileParty trainer, CharacterObject target)
        {
            MobileParty? cohort = ResolveCohortParty();
            return CountHealthy(trainer, target) +
                   (cohort != null && cohort != trainer ? CountHealthy(cohort, target) : 0);
        }

        private void AdvanceStockCollection(MobileParty trainer,
            CharacterObject target)
        {
            if (CountOrderedOnHand(trainer, target) >= RequestedCount(target))
            {
                ReleaseStockRendezvous("order_line_stock_ready");
                return;
            }
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
                    "; requested=" + RequestedCount(target) +
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
                    party.IsActive && party.IsLordParty && !party.IsDisbanding &&
                    party.LeaderHero?.IsActive == true && party.AttachedTo == null && party.Army == null &&
                    party.MapEvent is not { IsFinalized: false } &&
                    GwpWardenDuties.IsFreeFor(party, WardenDuty.StockExchange) &&
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
            int missing = Math.Max(0, RequestedCount(target) -
                CountOrderedOnHand(trainer, target));
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

        private List<TroopRosterElement> GetOutgoingBatches(
            MobileParty trainer, CharacterObject target)
        {
            MobileParty? cohort = ResolveCohortParty();
            return trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character != target &&
                    !CanReachTarget(element.Character, target,
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase)))
                // 先算人数再构造：原版 TroopRosterElement.Number 赋负数直接抛 MBUnderFlowException（C#反编译 v1.4.8），
                // 伤兵或预留缺额大于练兵官手里的健康人数时差值为负。（依据：独立审查 2026-11-15）
                .Select(element => (element.Character, Count: element.Number - element.WoundedNumber -
                    Math.Max(0, RequestedCount(element.Character) - CountHealthy(cohort, element.Character))))
                .Where(entry => entry.Count > 0)
                .Select(entry => new TroopRosterElement(entry.Character) { Number = entry.Count })
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
            int requested) =>
            GwpTroopTransfer.MoveBatches(source, destination, batches, requested, healthyOnly: true);

        private bool HasRoomForTroopDelivery() => MobileParty.MainParty is { IsActive: true } player &&
            _orderedCount <= GwpPlayerWardenTroops.RemainingSlots(player.MemberRoster);

        private bool PrepareDeliveryConversation()
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                !GreyWardenFamilyBehavior.IsTrainingHero(
                    Hero.OneToOneConversationHero))
                return false;
            MobileParty? pool = ResolveOrderPool(ResolveTrainerParty());
            if (_orderLines.Count == 0 || CountReady(pool) < _orderedCount)
                return false;

            MBTextManager.SetTextVariable("GWP_PLAYER_TROOP_DELIVERY_OFFER",
                GwpText.Get(GwpTextCatalog.gwp_player_troop_delivery_offer,
                    "VAR_1", _orderedCount, "VAR_2", DescribeComposition(),
                    "VAR_3", DescribeOrderPayment()));
            return true;
        }

        private bool CanAcceptTroopDelivery(out TextObject reason)
        {
            bool hasRoom = HasRoomForTroopDelivery();
            reason = hasRoom ? TextObject.GetEmpty() : GwpPlayerWardenTroops.CapacityMessage(PlayerBehaviorPool.ReputationUnits);
            return hasRoom;
        }

        private void CompleteTroopDelivery()
        {
            MobileParty? trainer = ResolveTrainerParty();
            MobileParty? pool = ResolveOrderPool(trainer);
            if (trainer?.IsActive != true || pool?.IsActive != true ||
                _orderLines.Count == 0 || CountReady(pool) < _orderedCount ||
                !HasRoomForTroopDelivery())
                return;

            foreach (var line in _orderLines)
                GwpTroopTransfer.Move(pool.MemberRoster, MobileParty.MainParty.MemberRoster,
                    line.Troop, line.Count, healthyOnly: true);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_DELIVERED",
                "composition=" + _orderComposition +
                "; count=" + _orderedCount +
                "; price=" + FormattedPrice() +
                "; paidWithReputation=" + _orderPaidWithReputation);
#endif
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
            trainer.Ai.SetDoNotMakeNewDecisions(false);
            trainer.SetMoveModeHold();
            trainer.Ai.RethinkAtNextHourlyTick = true;
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty,
            PartyBase defenderParty)
        {
            _ = attackerParty;
            _ = defenderParty;
            if ((PlayerTroopOrderStage)_orderStage !=
                PlayerTroopOrderStage.Delivering || !HasRoomForTroopDelivery())
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
                    "; price=" + FormattedPrice() +
                    "; retryAfterHour=" + _nextContactHour);
                PlayerEncounter.DoMeeting();
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
            _orderLines = Array.Empty<(CharacterObject, int)>();
            _orderComposition = string.Empty;
            _orderPrice = 0;
            _orderPriceUnits = 0;
            _orderPaidWithReputation = false;
            _orderStage = (int)PlayerTroopOrderStage.None;
            _filedHour = -1d;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _lastStockSourcePartyId = string.Empty;
            _stockTroopId = string.Empty;
            _cohortPartyId = string.Empty;
        }

        private static int CountHealthy(MobileParty? party,
            CharacterObject? troop)
        {
            if (party?.IsActive != true || troop == null) return 0;
            int index = party.MemberRoster.FindIndexOfTroop(troop);
            if (index < 0) return 0;
            TroopRosterElement element = party.MemberRoster.GetElementCopyAtIndex(index);
            return Math.Max(0, element.Number - element.WoundedNumber);
        }

    }
}
