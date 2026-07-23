using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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
    public sealed class GreyWardenTroopRequestBehavior : CampaignBehaviorBase
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
        private int _deferredTasksRemaining;

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
            public int DeferredTasksRemaining { get; set; }
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
            dataStore.SyncData("GWPP_PlayerTroopOrderDeferredTasksRemaining",
                ref _deferredTasksRemaining);
        }

        internal static bool IsTrainerReservedForPlayerOrder(MobileParty? party)
        {
            return party?.LeaderHero != null && _instance != null &&
                   (PlayerTroopOrderStage)_instance._orderStage !=
                   PlayerTroopOrderStage.None &&
                   !((PlayerTroopOrderStage)_instance._orderStage ==
                         PlayerTroopOrderStage.Delivering &&
                     _instance._deferredTasksRemaining > 0) &&
                   GreyWardenFamilyBehavior.IsTrainingHero(party.LeaderHero);
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
                Stage = (PlayerTroopOrderStage)_instance._orderStage,
                DeferredTasksRemaining =
                    _instance._deferredTasksRemaining
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
                GwpText.Get("{=gwp_player_troop_order_recorded}I will send the order. No payment is due now; the Training Warden will train real troops and bring them to you when they are ready."),
                null, null, 215);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_insufficient_start",
                "start",
                "close_window",
                GwpText.Get("{=gwp_player_troop_delivery_insufficient}You cannot pay the agreed price, so I have cancelled the order. No payment has been taken, and the soldiers remain in Grey Warden service."),
                IsInsufficientFundsDeliveryConversation,
                CancelTroopOrderForInsufficientFunds,
                1200);

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
                GwpText.Get("{=gwp_player_troop_delivery_accept}Pay the agreed sum into the public treasury and place the soldiers under my command."),
                CanPayForDelivery,
                CompleteTroopDelivery,
                310);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_accepted",
                "gwp_player_troop_delivery_accepted",
                "close_window",
                GwpText.Get("{=gwp_player_troop_delivery_accepted}The payment is entered in the Grey Warden public treasury. These soldiers now answer to you."),
                null, null, 310);

            starter.AddPlayerLine(
                "gwp_player_troop_delivery_defer",
                "gwp_player_troop_delivery_choice",
                "gwp_player_troop_delivery_deferred",
                GwpText.Get("{=gwp_player_troop_delivery_defer}Keep them with you for now."),
                null,
                DeferDelivery,
                300);

            starter.AddDialogLine(
                "gwp_player_troop_delivery_deferred",
                "gwp_player_troop_delivery_deferred",
                "close_window",
                GwpText.Get("{=gwp_player_troop_delivery_deferred}They will remain in my company. I will return when you are ready."),
                null, null, 300);

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

        private void ShowTroopOrderInquiry()
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
                        if (choice != null) FileTroopOrder(choice);
                    },
                    _ => { }),
                true);
        }

        private void FileTroopOrder(TroopOrderChoice choice)
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                PlayerTroopOrderStage.None)
                return;
            _orderedTroopId = choice.Kind.TroopId;
            _orderedCount = choice.Count;
            _orderPrice = choice.Price;
            _orderStage = (int)PlayerTroopOrderStage.Training;
            _filedHour = CampaignTime.Now.ToHours;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _deferredTasksRemaining = 0;
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_player_troop_order_filed}The Training Warden has received your order. No payment has been taken. She will train the requested troops and bring them to you."),
                Colors.Cyan));
        }

        private void OnHourlyTick()
        {
            if ((PlayerTroopOrderStage)_orderStage ==
                PlayerTroopOrderStage.None)
                return;

            if ((PlayerTroopOrderStage)_orderStage ==
                    PlayerTroopOrderStage.Delivering &&
                _deferredTasksRemaining > 0)
                return;

            MobileParty? trainer = ResolveTrainerParty();
            CharacterObject? target = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive != true || target == null ||
                !PoliceEnforcementBehavior.TryReservePartyForPlayerRequest(
                    trainer))
                return;

            int ready = CountHealthy(trainer, target);
            if (ready < _orderedCount)
            {
                _orderStage = (int)PlayerTroopOrderStage.Training;
                TrainForOrderIfDue(trainer, target);
                return;
            }

            _orderStage = (int)PlayerTroopOrderStage.Delivering;
            if (_nextContactHour < 0d)
                _nextContactHour = CampaignTime.Now.ToHours;
            if (CampaignTime.Now.ToHours >= _nextContactHour)
                MoveTrainerToPlayer(trainer);
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

            int exchanged = RefreshTrainerStock(trainer, target);
            List<TroopRosterElement> cohorts = trainer.MemberRoster
                .GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0 &&
                    CanReachTarget(element.Character, target,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
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
                "; cohorts=" + cohorts.Count +
                "; realTroopsExchanged=" + exchanged +
                "; xp=" + totalXp +
                "; nativeUpgradePending=true");
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

        private int RefreshTrainerStock(MobileParty trainer,
            CharacterObject target)
        {
            int missing = Math.Max(0, _orderedCount -
                CountHealthy(trainer, target));
            if (missing <= 0) return 0;

            var outgoing = trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character != target &&
                    !CanReachTarget(element.Character, target,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                .OrderBy(element => element.Character.Tier)
                .ThenBy(element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
            int outgoingHealthy = outgoing.Sum(element =>
                Math.Max(0, element.Number - element.WoundedNumber));
            int exchangeBudget = Math.Min(missing, outgoingHealthy);
            if (exchangeBudget <= 0) return 0;

            int exchanged = 0;
            foreach (MobileParty source in PoliceStats.GetAllPoliceParties()
                         .Where(party => party != trainer &&
                             party.IsActive && party.MapEvent == null)
                         .OrderBy(party => party.GetPosition2D.Distance(
                             trainer.GetPosition2D))
                         .ThenBy(party => party.StringId,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (exchanged >= exchangeBudget) break;
                var incoming = source.MemberRoster.GetTroopRoster()
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
                int available = incoming.Sum(element =>
                    Math.Max(0, element.Number - element.WoundedNumber));
                int swap = Math.Min(exchangeBudget - exchanged, available);
                if (swap <= 0) continue;

                int movedIn = TransferHealthyBatches(source.MemberRoster,
                    trainer.MemberRoster, incoming, swap);
                if (movedIn <= 0) continue;
                int movedOut = TransferHealthyBatches(trainer.MemberRoster,
                    source.MemberRoster, outgoing, movedIn);
                if (movedOut < movedIn)
                {
                    // 防止任何异常名单变化造成凭空扩军：未能完成的一换一部分
                    // 立即从练兵官退回原来源部队。
                    TransferHealthyBatches(trainer.MemberRoster,
                        source.MemberRoster, incoming, movedIn - movedOut);
                }
                exchanged += Math.Min(movedIn, movedOut);
            }

            return exchanged;
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
            MobileParty? trainer = ResolveTrainerParty();
            if (troop == null ||
                CountHealthy(trainer, troop) < _orderedCount)
                return false;

            MBTextManager.SetTextVariable("GWP_PLAYER_TROOP_DELIVERY_OFFER",
                GwpText.Get("{=gwp_player_troop_delivery_offer}Your order is ready: {VAR_1} {VAR_2}. The agreed price is {VAR_3} denars, payable directly into the Grey Warden public treasury.",
                    "VAR_1", _orderedCount, "VAR_2", troop.Name,
                    "VAR_3", _orderPrice));
            return true;
        }

        private bool CanPayForDelivery()
        {
            return PrepareDeliveryConversation() &&
                   PoliceResourceManager.CanCollectPlayerRequestPayment(
                       _orderPrice);
        }

        private bool IsInsufficientFundsDeliveryConversation()
        {
            return IsReadyDeliveryConversation() &&
                   !PoliceResourceManager.CanCollectPlayerRequestPayment(
                       _orderPrice);
        }

        private bool IsReadyDeliveryConversation()
        {
            if ((PlayerTroopOrderStage)_orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                _deferredTasksRemaining > 0 ||
                !GreyWardenFamilyBehavior.IsTrainingHero(
                    Hero.OneToOneConversationHero))
                return false;
            CharacterObject? troop = CharacterObject.Find(_orderedTroopId);
            return troop != null &&
                   CountHealthy(ResolveTrainerParty(), troop) >= _orderedCount;
        }

        private void CompleteTroopDelivery()
        {
            MobileParty? trainer = ResolveTrainerParty();
            CharacterObject? troop = CharacterObject.Find(_orderedTroopId);
            if (trainer?.IsActive != true || troop == null ||
                CountHealthy(trainer, troop) < _orderedCount ||
                MobileParty.MainParty?.IsActive != true ||
                !PoliceResourceManager.TryCollectPlayerRequestPayment(_orderPrice))
                return;

            trainer.MemberRoster.AddToCounts(troop, -_orderedCount,
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

        private void DeferDelivery()
        {
            _deferredTasksRemaining =
                GwpTuning.PlayerRequests.DeferredOrdinaryTasks;
            _nextContactHour = -1d;
            QueueFinishDeliveryEncounter();
            MobileParty? trainer = ResolveTrainerParty();
            StopPlayerContact(trainer);
            ReleaseTrainer(trainer);
            if (trainer?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_DEFERRED",
                    "troop=" + _orderedTroopId +
                    "; count=" + _orderedCount +
                    "; dutiesRemaining=" + _deferredTasksRemaining);
            }
        }

        private void CancelTroopOrder()
        {
            MobileParty? trainer = ResolveTrainerParty();
            QueueFinishDeliveryEncounter();
            StopPlayerContact(trainer);
            ReleaseTrainer(trainer);
            ClearOrder();
        }

        private void CancelTroopOrderForInsufficientFunds()
        {
            MobileParty? trainer = ResolveTrainerParty();
            if (trainer?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_CANCELLED_INSUFFICIENT_FUNDS",
                    "troop=" + _orderedTroopId +
                    "; count=" + _orderedCount +
                    "; price=" + _orderPrice);
            }
            CancelTroopOrder();
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

            float distance = trainer.GetPosition2D.Distance(player.GetPosition2D);
            if (distance <= GwpTuning.TroopRequest.ContactDistance)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(trainer);
                trainer.Ai.SetDoNotMakeNewDecisions(false);
                trainer.SetMoveEngageParty(player,
                    trainer.NavigationCapability);
            }
            else
            {
                GreyWardenPartyDesireBehavior.RequestApproach(trainer, player,
                    GreyWardenPartyDesireBehavior.PlayerRequestScore,
                    validHours: GwpTuning.Training.MovementIntentHours);
            }
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
                    _instance._deferredTasksRemaining <= 0 &&
                    GreyWardenFamilyBehavior.IsTrainingHero(hero);
        }

        internal static void NotifyOrdinaryDutyCompleted(MobileParty? party,
            string duty)
        {
            if (_instance == null || party?.IsActive != true ||
                party.LeaderHero == null ||
                !GreyWardenFamilyBehavior.IsTrainingHero(party.LeaderHero) ||
                (PlayerTroopOrderStage)_instance._orderStage !=
                    PlayerTroopOrderStage.Delivering ||
                _instance._deferredTasksRemaining <= 0)
                return;

            _instance._deferredTasksRemaining--;
            GwpAiDiagnostics.WriteAction(party,
                "PLAYER_TROOP_ORDER_DEFERRED_DUTY_COMPLETED",
                "troop=" + _instance._orderedTroopId +
                "; duty=" + duty +
                "; dutiesRemaining=" +
                _instance._deferredTasksRemaining);
            if (_instance._deferredTasksRemaining > 0) return;

            _instance._nextContactHour = CampaignTime.Now.ToHours;
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(
                    "{=gwp_player_troop_defer_complete}The Training Warden will return with your prepared troops."),
                Colors.Cyan));
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
            _orderedTroopId = string.Empty;
            _orderedCount = 0;
            _orderPrice = 0;
            _orderStage = (int)PlayerTroopOrderStage.None;
            _filedHour = -1d;
            _lastOrderXpHour = -1d;
            _nextContactHour = -1d;
            _deferredTasksRemaining = 0;
        }

        private static int CountHealthy(MobileParty? party,
            CharacterObject? troop)
        {
            if (party?.IsActive != true || troop == null) return 0;
            TroopRosterElement element = party.MemberRoster.GetTroopRoster()
                .FirstOrDefault(candidate => candidate.Character == troop);
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
