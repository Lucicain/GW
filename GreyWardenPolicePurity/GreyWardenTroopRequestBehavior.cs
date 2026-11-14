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
            dataStore.SyncData("GWPP_PlayerTroopOrderCohortPartyId",
                ref _cohortPartyId);
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
            // 练兵队里的成品加上练兵官手上已练成、还没拨过去的，仍不够数的那一项。
            return _instance.TrainingTarget(_instance.ResolveCohortParty(), party).Troop;
        }

        internal static bool IsTrainerReservedForPlayerOrder(MobileParty? party)
        {
            if (party?.LeaderHero == null || _instance == null)
                return false;

            if ((PlayerTroopOrderStage)_instance._orderStage == PlayerTroopOrderStage.None)
                return false;

            return GreyWardenFamilyBehavior.IsTrainingHero(party.LeaderHero);
        }

        /// <summary>
        /// 练兵队只装备好的成品，整队不升级。原版和 RBM 的升级器都先问升级模型的
        /// IsTroopUpgradeable（C#反编译 v1.5.7 PartyUpgraderCampaignBehavior；本机 RBM 4.5.4
        /// SpoilsUpgradePatches.OverrideUpgradeReadyTroops），整队锁住不依赖哪个升级器怎么数人。
        /// 练兵官手上还没拨进练兵队的成品（15 人留队或还没建队时会等一阵）同样整叠锁住，
        /// 拨够了就解锁，这叠剩下的兵照常当原料练。（依据：用户裁定 2026-11-15；独立审查 2026-11-15）
        /// </summary>
        internal static bool IsOrderedTroopUpgradeLocked(PartyBase? party,
            CharacterObject? troop)
        {
            if (_instance == null || troop == null || party?.MobileParty?.IsActive != true ||
                _instance._orderStage == (int)PlayerTroopOrderStage.None)
                return false;
            if (!string.IsNullOrEmpty(_instance._cohortPartyId) &&
                string.Equals(party.MobileParty.StringId, _instance._cohortPartyId, StringComparison.OrdinalIgnoreCase))
                return true;
            // 原版每天对全图每支队伍每个兵种都问；先查领队本职（字典查找），不是练兵官就不去遍历家族找人。
            return _instance.RequestedCount(troop) > 0 &&
                GreyWardenFamilyBehavior.IsTrainingHero(party.MobileParty.LeaderHero) &&
                party.MobileParty == _instance.ResolveTrainerParty() &&
                _instance.StillNeededInCohort(_instance.ResolveCohortParty(), troop) > 0;
        }

        /// <summary>
        /// 练兵官这条升级分支是否通往当前在练的订单兵种。只管能练成该兵种的兵，
        /// 原版和 RBM 都按升级模型的分支权重选分支；返回 null 交还原有规则（兵种配比）。
        /// </summary>
        internal static bool? TrainerBranchServesOrder(PartyBase? party,
            CharacterObject troop, CharacterObject branch)
        {
            if (_instance == null || party?.MobileParty == null ||
                _instance._orderStage == (int)PlayerTroopOrderStage.None ||
                !GreyWardenFamilyBehavior.IsTrainingHero(party.MobileParty.LeaderHero) ||
                party.MobileParty != _instance.ResolveTrainerParty())
                return null;
            CharacterObject? target = _instance.TrainingTarget(_instance.ResolveCohortParty(), party.MobileParty).Troop;
            if (target == null || troop == target || !CanReachTarget(troop, target,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                return null;
            return CanReachTarget(branch, target,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        internal static IReadOnlyList<PlayerTroopOrderSnapshot> GetTaskSnapshots()
        {
            var result = new List<PlayerTroopOrderSnapshot>();
            if (_instance == null ||
                (PlayerTroopOrderStage)_instance._orderStage ==
                PlayerTroopOrderStage.None)
                return result;
            MobileParty? trainer = _instance.ResolveTrainerParty();
            MobileParty? cohort = _instance.ResolveCohortParty();
            result.Add(new PlayerTroopOrderSnapshot
            {
                TrainerPartyId = trainer?.StringId ?? string.Empty,
                TroopName = _instance.DescribeComposition(),
                Count = _instance._orderedCount,
                ReadyCount = _instance.CountReady(cohort),
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
            // 成品只认练兵队；练兵官手上有成品可拨时才建队。没有练兵队时照样在练兵官手上练，
            // 只是交不了货：人少时原版会照常去招兵，在海上借不到船时等上岸再建。
            MobileParty? cohort = AdvanceCohort(trainer);
            if (cohort == null || TrainingTarget(cohort).Troop != null)
            {
                _orderStage = (int)PlayerTroopOrderStage.Training;
                CharacterObject? target = TrainingTarget(cohort, trainer).Troop;
                if (target != null) TrainForOrderIfDue(trainer, cohort, target);
                return;
            }

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
        /// 这一项还要练几个：练兵队里的成品和练兵官手上已练成、下一拍会拨过去的都算进去。
        /// </summary>
        private int StillToTrain(MobileParty trainer, MobileParty? cohort, CharacterObject target) =>
            Math.Max(0, RequestedCount(target) - CountHealthy(cohort, target) - CountHealthy(trainer, target));

        /// <summary>这个兵种作为订单项，练兵队里还缺几个；练兵官手上这叠兵要留着拨过去，锁升级、不加经验、不拆。</summary>
        private int StillNeededInCohort(MobileParty? cohort, CharacterObject troop) =>
            Math.Max(0, RequestedCount(troop) - CountHealthy(cohort, troop));

        /// <summary>
        /// 练兵官手上能练成目标的原料兵：灰袍兵、能沿升级树升到目标。别的订单项还要拨走成品的那叠
        /// 整叠锁着升级，先不算原料，拨够了再算。离目标越近越靠前，少走几级就能交货。
        /// </summary>
        private List<(CharacterObject Troop, int Healthy)> TrainingStock(MobileParty trainer, MobileParty? cohort,
            CharacterObject target) =>
            trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null && !element.Character.IsHero &&
                    element.Character != target && GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0 &&
                    StillNeededInCohort(cohort, element.Character) == 0 &&
                    CanReachTarget(element.Character, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .Select(element => (element.Character, Healthy: element.Number - element.WoundedNumber))
                .Where(entry => entry.Healthy > 0)
                .OrderByDescending(entry => entry.Character.Tier)
                .ThenBy(entry => entry.Character.StringId, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// 每 6 小时把练兵官手上原料的经验补到「还差人数 × 朝目标升一级的经验」，离目标近的兵种先补。
        /// 是补足不是累加：升级器一天只跑一次，累加会让一天里升出好几倍的人。
        /// 升级仍由原版（或 RBM）的升级器做，分支由 <see cref="TrainerBranchServesOrder"/> 引向目标。
        /// 原料不够时拆编重训。（依据：用户裁定 2026-11-15；独立审查 2026-11-15）
        /// </summary>
        private void TrainForOrderIfDue(MobileParty trainer, MobileParty? cohort,
            CharacterObject target)
        {
            double now = CampaignTime.Now.ToHours;
            if (_lastOrderXpHour >= 0d &&
                now - _lastOrderXpHour <
                GwpTuning.TroopRequest.PlayerOrderXpIntervalHours)
                return;
            _lastOrderXpHour = now;

            int needed = StillToTrain(trainer, cohort, target);
            List<(CharacterObject Troop, int Healthy)> stock = TrainingStock(trainer, cohort, target);
            int supply = stock.Sum(entry => entry.Healthy);
            int remaining = needed;
            int totalXp = 0;
            var model = Campaign.Current.Models.PartyTroopUpgradeModel;
            foreach (var entry in stock)
            {
                int trained = Math.Min(entry.Healthy, remaining);
                if (trained <= 0) break;
                remaining -= trained;
                CharacterObject? branch = entry.Troop.UpgradeTargets.FirstOrDefault(next => next != null &&
                    CanReachTarget(next, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
                if (branch == null) continue;
                int xp = Math.Max(0, trained * model.GetXpCostForUpgrade(trainer.Party, entry.Troop, branch) -
                    trainer.MemberRoster.GetElementXp(entry.Troop));
                if (xp <= 0) continue;
                trainer.MemberRoster.AddXpToTroop(entry.Troop, xp);
                totalXp += xp;
            }

            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_XP_GRANTED",
                "target=" + target.StringId +
                "; requested=" + RequestedCount(target) +
                "; inCohort=" + CountHealthy(cohort, target) +
                "; onTrainer=" + CountHealthy(trainer, target) +
                "; needed=" + needed +
                "; supply=" + supply +
                "; xp=" + totalXp);

            // 锁着等拨走的那叠里超出预留、能练成目标的人也是原料，只是暂时不练；不算进去会误判缺原料，
            // 一直拆别的兵（比如重训点正是被锁的灰袍兵时，偏路精锐会被一路拆光）。（依据：独立审查 2026-11-15）
            int lockedSurplus = trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null && !element.Character.IsHero && element.Character != target &&
                    StillNeededInCohort(cohort, element.Character) > 0 &&
                    CanReachTarget(element.Character, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .Sum(element => Math.Max(0, element.Number - element.WoundedNumber - StillNeededInCohort(cohort, element.Character)));
            DowngradeForOrderIfDue(trainer, cohort, target, needed - supply - lockedSurplus);
        }

        /// <summary>
        /// 拆编重训：练兵官手上的原料不够时，把别的灰袍兵降回能练成目标的那一级。
        /// 目标的下游（目标能升成它）直接降成目标；别的分支（比如要弓手、手上只有骑士）
        /// 降到两边共同的上游，再由经验练过去。先降离目标最近、阶级最低的兵，尽量少糟蹋；
        /// 别的订单项还要拨走的成品不动。每 6 小时最多降几个，不是凭空变人。
        /// 成品拨走后练兵官队伍变小，原版会照常去招兵补回来。（依据：用户裁定 2026-11-15）
        /// </summary>
        private void DowngradeForOrderIfDue(MobileParty trainer, MobileParty? cohort,
            CharacterObject target, int shortfall)
        {
            if (shortfall <= 0) return;
            // 目标自己和它的各级上游，按阶从高到低：降到其中最高、还能升回这个兵的那一级。
            List<CharacterObject> rebuildPoints = OrderTroopIds.Select(CharacterObject.Find)
                .Where(troop => troop != null && CanReachTarget(troop, target,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .OrderByDescending(troop => troop.Tier).ThenBy(troop => troop.StringId, StringComparer.Ordinal)
                .ToList();

            var candidates = trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null && !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    !CanReachTarget(element.Character, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .Select(element => (Troop: element.Character,
                    Spare: element.Number - element.WoundedNumber - StillNeededInCohort(cohort, element.Character),
                    Rebuild: rebuildPoints.FirstOrDefault(point => CanReachTarget(point, element.Character,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)))))
                .Where(entry => entry.Spare > 0 && entry.Rebuild != null)
                .OrderByDescending(entry => entry.Rebuild!.Tier)
                .ThenBy(entry => entry.Troop.Tier)
                .ThenBy(entry => entry.Troop.StringId, StringComparer.Ordinal)
                .ToList();

            int budget = Math.Min(shortfall, GwpTuning.TroopRequest.PlayerOrderDowngradePerInterval);
            int moved = 0;
            foreach (var entry in candidates)
            {
                int take = Math.Min(entry.Spare, budget - moved);
                if (take <= 0) break;
                trainer.MemberRoster.AddToCounts(entry.Troop, -take, false, 0);
                trainer.MemberRoster.AddToCounts(entry.Rebuild!, take, false, 0);
                moved += take;
                GwpAiDiagnostics.WriteAction(trainer, "PLAYER_TROOP_ORDER_DOWNGRADED",
                    "target=" + target.StringId +
                    "; from=" + entry.Troop.StringId +
                    "; to=" + entry.Rebuild!.StringId +
                    "; count=" + take +
                    "; shortfall=" + shortfall);
            }
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

        private bool HasRoomForTroopDelivery() => MobileParty.MainParty is { IsActive: true } player &&
            _orderedCount <= GwpPlayerWardenTroops.RemainingSlots(player.MemberRoster);

        private bool PrepareDeliveryConversation()
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                !GreyWardenFamilyBehavior.IsTrainingHero(
                    Hero.OneToOneConversationHero))
                return false;
            MobileParty? pool = ResolveCohortParty();
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
            MobileParty? pool = ResolveCohortParty();
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
