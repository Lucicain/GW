using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// High-priority requests filed by the player.  Any Grey Warden lord may
    /// record a petition, but only the Noble Affairs Liaison may collect the fee
    /// and carry the fief appeal to the target settlement.
    /// </summary>
    public sealed class GreyWardenPlayerRequestBehavior : CampaignBehaviorBase
    {
        private const string CaptureSettlementIdsKey = "GWPP_PlayerRequestCaptureSettlementIds";
        private const string CaptureKingdomIdsKey = "GWPP_PlayerRequestCaptureKingdomIds";
        private const string CaptureHoursKey = "GWPP_PlayerRequestCaptureHours";
        private const string CaptureFlagsKey = "GWPP_PlayerRequestCaptureFlags";
        private const string CaptureAwardedClanIdsKey = "GWPP_PlayerRequestCaptureAwardedClanIds";

        private static GreyWardenPlayerRequestBehavior? _instance;

        private readonly List<CaptureRecord> _captures = new List<CaptureRecord>();
        private string _activeSettlementId = string.Empty;
        private int _activeStage;
        private double _activeFiledHour = -1d;
        private double _stayStartHour = -1d;
        private double _nextContactHour = -1d;
        private int _deferredTasksRemaining;
        private bool _feePaid;
        private int _publicSupportPercent;

        [Flags]
        private enum CaptureFlags
        {
            None = 0,
            PlayerParticipated = 1,
            DecisionResolved = 2,
            AutoRolled = 4,
            AutoTriggered = 8,
            RequestFiled = 16,
            AppealUsed = 32,
            AutoOfferConsumed = 64
        }

        internal enum FiefRequestStage
        {
            None = 0,
            SeekingPlayerForPayment = 1,
            TravelingToSettlement = 2,
            LobbyingAtSettlement = 3,
            AwaitingVote = 4
        }

        private sealed class CaptureRecord
        {
            public string SettlementId { get; set; } = string.Empty;
            public string KingdomId { get; set; } = string.Empty;
            public double CaptureHour { get; set; }
            public CaptureFlags Flags { get; set; }
            public string AwardedClanId { get; set; } = string.Empty;
        }

        internal sealed class PlayerRequestTaskSnapshot
        {
            public string SettlementName { get; set; } = string.Empty;
            public string AssigneePartyId { get; set; } = string.Empty;
            public CampaignTime FiledTime { get; set; }
            public FiefRequestStage Stage { get; set; }
            public bool FeePaid { get; set; }
            public int Fee { get; set; }
            public int PublicSupportPercent { get; set; }
            public double RemainingHours { get; set; }
            public int DeferredTasksRemaining { get; set; }
        }

        public GreyWardenPlayerRequestBehavior() => _instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this,
                OnNewGameCreated);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this,
                OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this,
                OnHourlyTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
                this, OnSettlementOwnerChanged);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this,
                OnMapEventEnded);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this,
                OnMapEventStarted);
            CampaignEvents.KingdomDecisionConcluded.AddNonSerializedListener(this,
                OnKingdomDecisionConcluded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? settlementIds = null;
            List<string>? kingdomIds = null;
            List<double>? captureHours = null;
            List<int>? flags = null;
            List<string>? awardedClanIds = null;

            if (dataStore.IsSaving)
            {
                settlementIds = _captures.Select(record => record.SettlementId).ToList();
                kingdomIds = _captures.Select(record => record.KingdomId).ToList();
                captureHours = _captures.Select(record => record.CaptureHour).ToList();
                flags = _captures.Select(record => (int)record.Flags).ToList();
                awardedClanIds = _captures.Select(record => record.AwardedClanId).ToList();
            }

            dataStore.SyncData(CaptureSettlementIdsKey, ref settlementIds);
            dataStore.SyncData(CaptureKingdomIdsKey, ref kingdomIds);
            dataStore.SyncData(CaptureHoursKey, ref captureHours);
            dataStore.SyncData(CaptureFlagsKey, ref flags);
            dataStore.SyncData(CaptureAwardedClanIdsKey, ref awardedClanIds);
            dataStore.SyncData("GWPP_PlayerRequestActiveSettlement", ref _activeSettlementId);
            dataStore.SyncData("GWPP_PlayerRequestActiveStage", ref _activeStage);
            dataStore.SyncData("GWPP_PlayerRequestFiledHour", ref _activeFiledHour);
            dataStore.SyncData("GWPP_PlayerRequestStayStartHour", ref _stayStartHour);
            dataStore.SyncData("GWPP_PlayerRequestNextContactHour", ref _nextContactHour);
            dataStore.SyncData("GWPP_PlayerRequestDeferredTasksRemaining",
                ref _deferredTasksRemaining);
            dataStore.SyncData("GWPP_PlayerRequestFeePaid", ref _feePaid);
            dataStore.SyncData("GWPP_PlayerRequestPublicSupport", ref _publicSupportPercent);

            if (!dataStore.IsLoading) return;

            _captures.Clear();
            int count = new[]
            {
                settlementIds?.Count ?? 0,
                kingdomIds?.Count ?? 0,
                captureHours?.Count ?? 0,
                flags?.Count ?? 0,
                awardedClanIds?.Count ?? 0
            }.Min();
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(settlementIds![i])) continue;
                _captures.Add(new CaptureRecord
                {
                    SettlementId = settlementIds[i],
                    KingdomId = kingdomIds![i] ?? string.Empty,
                    CaptureHour = captureHours![i],
                    Flags = (CaptureFlags)flags![i],
                    AwardedClanId = awardedClanIds![i] ?? string.Empty
                });
            }

            // Older saves could have an already active automatic offer without a
            // separate "consumed" bit.  Mark it now so withdrawing that offer
            // cannot make the hourly scanner reopen it forever.
            if ((FiefRequestStage)_activeStage != FiefRequestStage.None)
            {
                CaptureRecord? active = FindActiveRecord();
                if (active != null &&
                    active.Flags.HasFlag(CaptureFlags.AutoTriggered))
                    active.Flags |= CaptureFlags.AutoOfferConsumed;
            }
        }

        internal static bool IsPartyReservedForPlayerRequest(MobileParty? party)
        {
            if (party?.LeaderHero == null || _instance == null) return false;
            FiefRequestStage stage = (FiefRequestStage)_instance._activeStage;
            return stage != FiefRequestStage.None &&
                   !(stage == FiefRequestStage.SeekingPlayerForPayment &&
                     _instance._deferredTasksRemaining > 0) &&
                   GreyWardenFamilyBehavior.IsPlayerRequestsHero(party.LeaderHero);
        }

        internal static IReadOnlyList<PlayerRequestTaskSnapshot> GetTaskSnapshots()
        {
            var result = new List<PlayerRequestTaskSnapshot>();
            if (_instance == null ||
                (FiefRequestStage)_instance._activeStage == FiefRequestStage.None)
                return result;

            Settlement? settlement = ResolveSettlement(_instance._activeSettlementId);
            MobileParty? assignee = _instance.ResolveCoordinatorParty();
            result.Add(new PlayerRequestTaskSnapshot
            {
                SettlementName = settlement?.Name?.ToString() ??
                                 _instance._activeSettlementId,
                AssigneePartyId = assignee?.StringId ?? string.Empty,
                FiledTime = CampaignTime.Hours((float)Math.Max(0d,
                    _instance._activeFiledHour)),
                Stage = (FiefRequestStage)_instance._activeStage,
                FeePaid = _instance._feePaid,
                Fee = GwpTuning.PlayerRequests.FiefAppealPrice,
                PublicSupportPercent = _instance._publicSupportPercent,
                RemainingHours = _instance._stayStartHour >= 0d
                    ? Math.Max(0d, _instance._stayStartHour +
                        GwpTuning.PlayerRequests.LobbyingHours -
                        CampaignTime.Now.ToHours)
                    : 0d,
                DeferredTasksRemaining =
                    _instance._deferredTasksRemaining
            });
            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _ = starter;
            _captures.Clear();
            ClearActiveRequest();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "gwp_fief_appeal_file",
                "lord_talk_speak_diplomacy_2",
                "gwp_fief_appeal_file_response",
                GwpText.Get("{=gwp_fief_appeal_file}I wish to file a petition for reconsideration of a fief."),
                CanFileFiefAppeal,
                ShowFiefSelectionInquiry,
                220);

            starter.AddDialogLine(
                "gwp_fief_appeal_file_response",
                "gwp_fief_appeal_file_response",
                "lord_pretalk",
                GwpText.Get("{=gwp_fief_appeal_recorded}I will enter the petition. The Noble Affairs Liaison will come to you personally before any fee is collected."),
                null, null, 220);

            starter.AddDialogLine(
                "gwp_fief_payment_insufficient_start",
                "start",
                "gwp_fief_payment_choice",
                GwpText.Get("{=gwp_fief_payment_insufficient}You do not have the required fifty thousand denars. You may set the petition aside or withdraw it without payment."),
                IsInsufficientFundsPaymentConversation,
                null,
                1200);

            starter.AddDialogLine(
                "gwp_fief_payment_offer_start",
                "start",
                "gwp_fief_payment_choice",
                GwpText.Get("{=gwp_fief_payment_offer}Your petition is in my hands. For fifty thousand denars paid into the Grey Warden public treasury, I will carry it to the people and reopen the fief decision."),
                IsCoordinatorPaymentConversation,
                null,
                1100);

            starter.AddDialogLine(
                "gwp_fief_payment_offer",
                "lord_talk_speak_diplomacy_2",
                "gwp_fief_payment_choice",
                GwpText.Get("{=gwp_fief_payment_offer}Your petition is in my hands. For fifty thousand denars paid into the Grey Warden public treasury, I will carry it to the people and reopen the fief decision."),
                IsCoordinatorPaymentConversation,
                null,
                300);

            starter.AddPlayerLine(
                "gwp_fief_payment_accept",
                "gwp_fief_payment_choice",
                "gwp_fief_payment_accepted",
                GwpText.Get("{=gwp_fief_payment_accept}Take the fifty thousand denars and proceed."),
                CanPayFiefAppeal,
                AcceptFiefAppealPayment,
                300);

            starter.AddDialogLine(
                "gwp_fief_payment_accepted",
                "gwp_fief_payment_accepted",
                "close_window",
                GwpText.Get("{=gwp_fief_payment_accepted}The sum is entered in the public treasury. I leave for the disputed fief at once."),
                null, null, 300);

            starter.AddPlayerLine(
                "gwp_fief_payment_defer",
                "gwp_fief_payment_choice",
                "gwp_fief_payment_deferred",
                GwpText.Get("{=gwp_fief_payment_defer}Set the petition aside for now."),
                null,
                DeferFiefAppealPayment,
                290);

            starter.AddDialogLine(
                "gwp_fief_payment_deferred",
                "gwp_fief_payment_deferred",
                "close_window",
                GwpText.Get("{=gwp_fief_payment_deferred}Very well. The petition will remain open. I will seek you out again when the time is right."),
                null, null, 290);

            starter.AddPlayerLine(
                "gwp_fief_payment_withdraw",
                "gwp_fief_payment_choice",
                "gwp_fief_payment_withdrawn",
                GwpText.Get("{=gwp_fief_payment_withdraw}Withdraw the petition."),
                null,
                WithdrawUnpaidFiefAppeal,
                280);

            starter.AddDialogLine(
                "gwp_fief_payment_withdrawn",
                "gwp_fief_payment_withdrawn",
                "close_window",
                GwpText.Get("{=gwp_fief_payment_withdrawn}Then no payment is due, and the petition is closed."),
                null, null, 280);
        }

        private bool CanFileFiefAppeal()
        {
            return IsOrdinaryGreyWardenLordConversation() &&
                   IsPlayerGreyWardenMember() &&
                   (FiefRequestStage)_activeStage == FiefRequestStage.None &&
                   GetEligibleCaptures().Count > 0;
        }

        private void ShowFiefSelectionInquiry()
        {
            List<CaptureRecord> eligible = GetEligibleCaptures();
            var elements = eligible.Select(record =>
            {
                Settlement? settlement = ResolveSettlement(record.SettlementId);
                string label = settlement?.Name?.ToString() ?? record.SettlementId;
                return new InquiryElement(record, label, null, true,
                    GwpText.Get("{=gwp_fief_select_hint}Captured with your participation and awarded to another clan."));
            }).ToList();
            if (elements.Count == 0) return;

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    GwpText.Get("{=gwp_fief_select_title}Select a fief petition"),
                    GwpText.Get("{=gwp_fief_select_description}Choose the recently captured fief whose ownership should be reconsidered."),
                    elements, true, 1, 1,
                    GwpText.Get("{=gwp_file_petition}File petition"),
                    GwpText.Get("{=gwp_cancel}Cancel"),
                    selected =>
                    {
                        CaptureRecord? record = selected.FirstOrDefault()?.Identifier
                            as CaptureRecord;
                        if (record != null)
                            ActivateRequest(record, false);
                    },
                    _ => { }),
                true);
        }

        private void ActivateRequest(CaptureRecord record, bool automatic)
        {
            if ((FiefRequestStage)_activeStage != FiefRequestStage.None ||
                !IsEligible(record))
                return;

            record.Flags |= CaptureFlags.RequestFiled;
            if (automatic) record.Flags |= CaptureFlags.AutoTriggered;
            if (record.Flags.HasFlag(CaptureFlags.AutoTriggered))
                record.Flags |= CaptureFlags.AutoOfferConsumed;
            _activeSettlementId = record.SettlementId;
            _activeStage = (int)FiefRequestStage.SeekingPlayerForPayment;
            _activeFiledHour = CampaignTime.Now.ToHours;
            _stayStartHour = -1d;
            _nextContactHour = CampaignTime.Now.ToHours;
            _deferredTasksRemaining = 0;
            _feePaid = false;
            _publicSupportPercent = 0;
            InformationManager.DisplayMessage(new InformationMessage(
                automatic
                    ? GwpText.Get("{=gwp_fief_auto_triggered}The Noble Affairs Liaison has taken up your denied fief claim and is coming to ask whether you want Grey Warden assistance.")
                    : GwpText.Get("{=gwp_fief_request_filed}Your fief petition has been recorded. No money has been taken; the Noble Affairs Liaison is coming to meet you."),
                Colors.Cyan));
        }

        private bool IsCoordinatorPaymentConversation()
        {
            return (FiefRequestStage)_activeStage ==
                   FiefRequestStage.SeekingPlayerForPayment &&
                   _deferredTasksRemaining <= 0 &&
                   GreyWardenFamilyBehavior.IsPlayerRequestsHero(
                       Hero.OneToOneConversationHero);
        }

        private static bool CanPayFiefAppeal() =>
            PoliceResourceManager.CanCollectPlayerRequestPayment(
                GwpTuning.PlayerRequests.FiefAppealPrice);

        private bool IsInsufficientFundsPaymentConversation()
        {
            return IsCoordinatorPaymentConversation() &&
                   !CanPayFiefAppeal();
        }

        private void AcceptFiefAppealPayment()
        {
            if (!IsCoordinatorPaymentConversation() ||
                !PoliceResourceManager.TryCollectPlayerRequestPayment(
                    GwpTuning.PlayerRequests.FiefAppealPrice))
                return;

            _feePaid = true;
            _publicSupportPercent = GetPublicSupportPercent(
                GwpRuntimeState.Player.Reputation);
            _activeStage = (int)FiefRequestStage.TravelingToSettlement;
            _nextContactHour = -1d;
            _deferredTasksRemaining = 0;
            QueueFinishPaymentEncounter();
            MobileParty? coordinator = ResolveCoordinatorParty();
            if (coordinator?.IsActive == true)
            {
                StopPlayerContact(coordinator);
                Settlement? target = ResolveSettlement(_activeSettlementId);
                if (target != null)
                    GreyWardenPartyDesireBehavior.RequestVisit(coordinator, target,
                        GreyWardenPartyDesireBehavior.PlayerRequestScore,
                        GwpTuning.PlayerRequests.MovementIntentHours);
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_PAID",
                    "settlement=" + _activeSettlementId +
                    "; fee=" + GwpTuning.PlayerRequests.FiefAppealPrice +
                    "; publicSupport=" + _publicSupportPercent);
            }
        }

        private void DeferFiefAppealPayment()
        {
            _deferredTasksRemaining =
                GwpTuning.PlayerRequests.DeferredOrdinaryTasks;
            _nextContactHour = -1d;
            QueueFinishPaymentEncounter();
            MobileParty? coordinator = ResolveCoordinatorParty();
            StopPlayerContact(coordinator);
            ReleaseCoordinator();
            if (coordinator?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_DEFERRED",
                    "settlement=" + _activeSettlementId +
                    "; dutiesRemaining=" + _deferredTasksRemaining);
            }
        }

        private void WithdrawUnpaidFiefAppeal()
        {
            QueueFinishPaymentEncounter();
            CaptureRecord? record = FindActiveRecord();
            if (record != null)
                record.Flags &= ~CaptureFlags.RequestFiled;
            StopPlayerContact(ResolveCoordinatorParty());
            ReleaseCoordinator();
            ClearActiveRequest();
        }

        private void QueueFinishPaymentEncounter()
        {
            if (!PlayerEncounter.IsActive) return;
            PlayerEncounter.LeaveEncounter = true;
            if (Campaign.Current?.ConversationManager == null)
            {
                FinishPaymentEncounter();
                return;
            }

            Campaign.Current.ConversationManager.ConversationEndOneShot -=
                FinishPaymentEncounter;
            Campaign.Current.ConversationManager.ConversationEndOneShot +=
                FinishPaymentEncounter;
        }

        private void FinishPaymentEncounter()
        {
            MobileParty? coordinator = ResolveCoordinatorParty();
            GwpCommon.TryFinishPlayerEncounter();
            StopPlayerContact(coordinator);

            FiefRequestStage stage = (FiefRequestStage)_activeStage;
            if (stage == FiefRequestStage.TravelingToSettlement ||
                stage == FiefRequestStage.LobbyingAtSettlement)
            {
                Settlement? target = ResolveSettlement(_activeSettlementId);
                if (coordinator?.IsActive == true && target != null)
                    GreyWardenPartyDesireBehavior.RequestVisit(coordinator,
                        target,
                        GreyWardenPartyDesireBehavior.PlayerRequestScore,
                        GwpTuning.PlayerRequests.MovementIntentHours);
            }
            else if (stage == FiefRequestStage.None)
            {
                ReleaseCoordinator();
            }

            if (coordinator?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_ENCOUNTER_FINISHED",
                    "stage=" + stage +
                    "; encounterActive=" + PlayerEncounter.IsActive);
            }
        }

        private void OnHourlyTick()
        {
            CleanupExpiredCaptures();
            if ((FiefRequestStage)_activeStage == FiefRequestStage.None)
            {
                CaptureRecord? automatic = GetEligibleCaptures()
                    .FirstOrDefault(record =>
                        record.Flags.HasFlag(CaptureFlags.AutoTriggered) &&
                        !record.Flags.HasFlag(CaptureFlags.AutoOfferConsumed));
                if (automatic != null) ActivateRequest(automatic, true);
                return;
            }

            if (!IsActiveRequestValid())
            {
                CancelActiveRequest("eligibility_lost", refundPaidFee: true);
                return;
            }

            MobileParty? coordinator = ResolveCoordinatorParty();
            if ((FiefRequestStage)_activeStage ==
                    FiefRequestStage.SeekingPlayerForPayment &&
                _deferredTasksRemaining > 0)
                return;
            if (coordinator?.IsActive != true ||
                !PoliceEnforcementBehavior.TryReservePartyForPlayerRequest(
                    coordinator))
                return;

            switch ((FiefRequestStage)_activeStage)
            {
                case FiefRequestStage.SeekingPlayerForPayment:
                    if (CampaignTime.Now.ToHours >= _nextContactHour)
                        MoveSpecialistToPlayer(coordinator,
                            GwpTuning.PlayerRequests.ContactDistance);
                    break;
                case FiefRequestStage.TravelingToSettlement:
                case FiefRequestStage.LobbyingAtSettlement:
                    UpdateLobbying(coordinator);
                    break;
                case FiefRequestStage.AwaitingVote:
                    GreyWardenPartyDesireBehavior.ClearIntent(coordinator);
                    break;
            }
        }

        private void UpdateLobbying(MobileParty coordinator)
        {
            Settlement? settlement = ResolveSettlement(_activeSettlementId);
            if (settlement == null)
            {
                CancelActiveRequest("settlement_missing", refundPaidFee: true);
                return;
            }

            if (coordinator.CurrentSettlement != settlement)
            {
                if ((FiefRequestStage)_activeStage ==
                    FiefRequestStage.LobbyingAtSettlement)
                {
                    _activeStage = (int)FiefRequestStage.TravelingToSettlement;
                    _stayStartHour = -1d;
                }
                GreyWardenPartyDesireBehavior.RequestVisit(coordinator, settlement,
                    GreyWardenPartyDesireBehavior.PlayerRequestScore,
                    validHours: GwpTuning.PlayerRequests.MovementIntentHours);
                return;
            }

            if ((FiefRequestStage)_activeStage ==
                FiefRequestStage.TravelingToSettlement)
            {
                _activeStage = (int)FiefRequestStage.LobbyingAtSettlement;
                _stayStartHour = CampaignTime.Now.ToHours;
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_LOBBYING_STARTED",
                    "settlement=" + settlement.StringId +
                    "; hours=" + GwpTuning.PlayerRequests.LobbyingHours);
            }

            GreyWardenPartyDesireBehavior.RequestVisit(coordinator, settlement,
                GreyWardenPartyDesireBehavior.PlayerRequestScore,
                validHours: GwpTuning.PlayerRequests.MovementIntentHours);
            if (CampaignTime.Now.ToHours <
                _stayStartHour + GwpTuning.PlayerRequests.LobbyingHours)
                return;

            Kingdom? kingdom = settlement.MapFaction as Kingdom;
            if (kingdom == null) return;
            bool duplicate = kingdom.UnresolvedDecisions.Any(decision =>
                decision is SettlementClaimantDecision claimant &&
                claimant.Settlement == settlement);
            if (duplicate) return;

            var decision = new GwpSettlementReconsiderationDecision(
                kingdom.RulingClan, settlement, _publicSupportPercent);
            kingdom.AddDecision(decision, ignoreInfluenceCost: true);
            _activeStage = (int)FiefRequestStage.AwaitingVote;
            GreyWardenPartyDesireBehavior.ClearIntent(coordinator);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_fief_vote_reopened}The Grey Warden petition at {VAR_1} is complete. A new native fief decision has been opened, and your clan is guaranteed a nomination.",
                    "VAR_1", settlement.Name),
                Colors.Green));
            GwpAiDiagnostics.WriteAction(coordinator,
                "PLAYER_FIEF_APPEAL_DECISION_OPENED",
                "settlement=" + settlement.StringId +
                "; support=" + _publicSupportPercent);
        }

        private void OnSettlementOwnerChanged(Settlement settlement,
            bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            _ = oldOwner;
            _ = detail;
            Kingdom? playerKingdom = Clan.PlayerClan?.Kingdom;
            if (!openToClaim || settlement?.Town == null ||
                !settlement.IsFortification || playerKingdom == null ||
                newOwner?.MapFaction != playerKingdom)
                return;

            CaptureRecord? recent = _captures
                .Where(record => string.Equals(record.SettlementId,
                    settlement.StringId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.CaptureHour)
                .FirstOrDefault(record =>
                    CampaignTime.Now.ToHours - record.CaptureHour < 24d);
            if (recent == null)
            {
                recent = new CaptureRecord
                {
                    SettlementId = settlement.StringId,
                    KingdomId = playerKingdom.StringId,
                    CaptureHour = CampaignTime.Now.ToHours
                };
                _captures.Add(recent);
            }

            if (capturerHero == Hero.MainHero ||
                MapEventContainsPlayerAsAttacker(settlement.Party?.MapEvent))
                recent.Flags |= CaptureFlags.PlayerParticipated;
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent?.IsSiegeAssault != true ||
                mapEvent.MapEventSettlement?.IsFortification != true ||
                mapEvent.Winner != mapEvent.AttackerSide ||
                !MapEventContainsPlayerAsAttacker(mapEvent))
                return;

            Settlement settlement = mapEvent.MapEventSettlement;
            Kingdom? playerKingdom = Clan.PlayerClan?.Kingdom;
            if (playerKingdom == null) return;

            // Siege-assault completion can be dispatched before the settlement
            // owner/faction changes. Record participation now; the owner-change
            // listener and later eligibility check validate the final kingdom.
            CaptureRecord? record = _captures
                .Where(candidate => string.Equals(candidate.SettlementId,
                    settlement.StringId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.CaptureHour)
                .FirstOrDefault(candidate =>
                    CampaignTime.Now.ToHours - candidate.CaptureHour < 24d);
            if (record == null)
            {
                record = new CaptureRecord
                {
                    SettlementId = settlement.StringId,
                    KingdomId = playerKingdom.StringId,
                    CaptureHour = CampaignTime.Now.ToHours
                };
                _captures.Add(record);
            }
            record.Flags |= CaptureFlags.PlayerParticipated;
        }

        private void OnKingdomDecisionConcluded(KingdomDecision decision,
            DecisionOutcome chosenOutcome, bool isPlayerInvolved)
        {
            _ = isPlayerInvolved;
            if (decision is GwpSettlementReconsiderationDecision reconsideration)
            {
                if (string.Equals(_activeSettlementId,
                    reconsideration.Settlement.StringId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    MobileParty? coordinator = ResolveCoordinatorParty();
                    if (coordinator?.IsActive == true)
                    {
                        string winner = chosenOutcome is
                            SettlementClaimantDecision.ClanAsDecisionOutcome clanOutcome
                                ? clanOutcome.Clan.StringId
                                : chosenOutcome.GetType().Name;
                        GwpAiDiagnostics.WriteAction(coordinator,
                            "PLAYER_FIEF_APPEAL_DECISION_CONCLUDED",
                            "settlement=" +
                            reconsideration.Settlement.StringId +
                            "; winner=" + winner +
                            "; playerInvolved=" + isPlayerInvolved);
                    }
                    CaptureRecord? active = FindActiveRecord();
                    if (active != null)
                    {
                        active.Flags |= CaptureFlags.AppealUsed;
                        active.Flags &= ~CaptureFlags.RequestFiled;
                    }
                    ReleaseCoordinator();
                    ClearActiveRequest();
                }
                return;
            }

            if (decision is not SettlementClaimantDecision claimant ||
                chosenOutcome is not SettlementClaimantDecision.ClanAsDecisionOutcome
                    outcome)
                return;

            CaptureRecord? record = _captures
                .Where(candidate => string.Equals(candidate.SettlementId,
                    claimant.Settlement.StringId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.CaptureHour)
                .FirstOrDefault(candidate =>
                    !candidate.Flags.HasFlag(CaptureFlags.DecisionResolved));
            if (record == null) return;

            record.Flags |= CaptureFlags.DecisionResolved;
            record.AwardedClanId = outcome.Clan.StringId;
            if (outcome.Clan == Clan.PlayerClan ||
                !record.Flags.HasFlag(CaptureFlags.PlayerParticipated) ||
                !IsPlayerGreyWardenMember())
                return;

            record.Flags |= CaptureFlags.AutoRolled;
            int reputation = Math.Max(0, Math.Min(100,
                GwpRuntimeState.Player.Reputation));
            if (MBRandom.RandomInt(100) < reputation)
                record.Flags |= CaptureFlags.AutoTriggered;
        }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty,
            PartyBase defenderParty)
        {
            _ = attackerParty;
            _ = defenderParty;
            if ((FiefRequestStage)_activeStage !=
                FiefRequestStage.SeekingPlayerForPayment)
                return;

            MobileParty? coordinator = ResolveCoordinatorParty();
            if (coordinator == null ||
                !mapEvent.InvolvedParties.Any(p => p.MobileParty == coordinator) ||
                !mapEvent.InvolvedParties.Any(p => p.MobileParty?.IsMainParty == true))
                return;

            if (PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                _nextContactHour = CampaignTime.Now.ToHours +
                                   GwpTuning.PlayerRequests.DeferredContactHours;
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_CONTACT_STARTED",
                    "settlement=" + _activeSettlementId +
                    "; fee=" + GwpTuning.PlayerRequests.FiefAppealPrice +
                    "; retryAfterHour=" + _nextContactHour);
                try { PlayerEncounter.DoMeeting(); }
                catch { }
            }
        }

        internal static bool IsPendingAutomaticConversation(Hero? hero)
        {
            return _instance != null &&
                    (FiefRequestStage)_instance._activeStage ==
                        FiefRequestStage.SeekingPlayerForPayment &&
                    _instance._deferredTasksRemaining <= 0 &&
                    GreyWardenFamilyBehavior.IsPlayerRequestsHero(hero);
        }

        internal static void NotifyOrdinaryDutyCompleted(MobileParty? party,
            string duty)
        {
            if (_instance == null || party?.IsActive != true ||
                party.LeaderHero == null ||
                !GreyWardenFamilyBehavior.IsPlayerRequestsHero(
                    party.LeaderHero) ||
                (FiefRequestStage)_instance._activeStage !=
                    FiefRequestStage.SeekingPlayerForPayment ||
                _instance._deferredTasksRemaining <= 0)
                return;

            _instance._deferredTasksRemaining--;
            GwpAiDiagnostics.WriteAction(party,
                "PLAYER_FIEF_APPEAL_DEFERRED_DUTY_COMPLETED",
                "settlement=" + _instance._activeSettlementId +
                "; duty=" + duty +
                "; dutiesRemaining=" +
                _instance._deferredTasksRemaining);
            if (_instance._deferredTasksRemaining > 0) return;

            _instance._nextContactHour = CampaignTime.Now.ToHours;
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(
                    "{=gwp_fief_defer_complete}The Noble Affairs Liaison will return to continue your fief petition."),
                Colors.Cyan));
        }

        private void MoveSpecialistToPlayer(MobileParty specialist,
            float contactDistance)
        {
            MobileParty? player = MobileParty.MainParty;
            if (player?.IsActive != true) return;

            if (player.CurrentSettlement != null)
            {
                GreyWardenPartyDesireBehavior.RequestVisit(specialist,
                    player.CurrentSettlement,
                    GreyWardenPartyDesireBehavior.PlayerRequestScore,
                    validHours: GwpTuning.PlayerRequests.MovementIntentHours);
                return;
            }

            float distance = specialist.GetPosition2D.Distance(player.GetPosition2D);
            if (distance <= contactDistance)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(specialist);
                specialist.Ai.SetDoNotMakeNewDecisions(false);
                specialist.SetMoveEngageParty(player,
                    specialist.NavigationCapability);
            }
            else
            {
                GreyWardenPartyDesireBehavior.RequestApproach(specialist, player,
                    GreyWardenPartyDesireBehavior.PlayerRequestScore,
                    validHours: GwpTuning.PlayerRequests.MovementIntentHours);
            }
        }

        private static void StopPlayerContact(MobileParty? specialist)
        {
            if (specialist?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(specialist);
            try
            {
                specialist.Ai.SetDoNotMakeNewDecisions(false);
                specialist.SetMoveModeHold();
                specialist.Ai.RethinkAtNextHourlyTick = true;
            }
            catch { }
        }

        private bool IsActiveRequestValid()
        {
            CaptureRecord? record = FindActiveRecord();
            Settlement? settlement = ResolveSettlement(_activeSettlementId);
            Kingdom? playerKingdom = Clan.PlayerClan?.Kingdom;
            return record != null && settlement?.IsFortification == true &&
                   playerKingdom != null && settlement.MapFaction == playerKingdom &&
                   string.Equals(record.KingdomId, playerKingdom.StringId,
                       StringComparison.OrdinalIgnoreCase) &&
                   IsPlayerGreyWardenMember() &&
                   settlement.OwnerClan != Clan.PlayerClan;
        }

        private List<CaptureRecord> GetEligibleCaptures() =>
            _captures.Where(IsEligible)
                .GroupBy(record => record.SettlementId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(record =>
                    record.CaptureHour).First())
                .OrderByDescending(record => record.CaptureHour)
                .ToList();

        private bool IsEligible(CaptureRecord record)
        {
            Settlement? settlement = ResolveSettlement(record.SettlementId);
            Kingdom? playerKingdom = Clan.PlayerClan?.Kingdom;
            double age = CampaignTime.Now.ToHours - record.CaptureHour;
            return record.Flags.HasFlag(CaptureFlags.PlayerParticipated) &&
                   record.Flags.HasFlag(CaptureFlags.DecisionResolved) &&
                   !record.Flags.HasFlag(CaptureFlags.AppealUsed) &&
                   (!record.Flags.HasFlag(CaptureFlags.RequestFiled) ||
                    string.Equals(record.SettlementId, _activeSettlementId,
                        StringComparison.OrdinalIgnoreCase)) &&
                   !string.Equals(record.AwardedClanId,
                       Clan.PlayerClan?.StringId,
                       StringComparison.OrdinalIgnoreCase) &&
                   age >= 0d &&
                   age <= GwpTuning.PlayerRequests.FiefAppealWindowDays * 24d &&
                   settlement?.IsFortification == true &&
                   playerKingdom != null &&
                   settlement.MapFaction == playerKingdom &&
                   settlement.OwnerClan != Clan.PlayerClan &&
                   string.Equals(record.KingdomId, playerKingdom.StringId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void CleanupExpiredCaptures()
        {
            double cutoff = CampaignTime.Now.ToHours -
                            GwpTuning.PlayerRequests.FiefAppealWindowDays * 24d;
            _captures.RemoveAll(record =>
                record.CaptureHour < cutoff &&
                !string.Equals(record.SettlementId, _activeSettlementId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void CancelActiveRequest(string reason, bool refundPaidFee)
        {
            CaptureRecord? record = FindActiveRecord();
            if (record != null)
                record.Flags &= ~CaptureFlags.RequestFiled;
            if (refundPaidFee && _feePaid &&
                (FiefRequestStage)_activeStage != FiefRequestStage.AwaitingVote)
            {
                PoliceResourceManager.RefundPlayerRequestPayment(
                    GwpTuning.PlayerRequests.FiefAppealPrice);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_fief_appeal_refunded}The fief petition became invalid before the new vote opened. The full fifty thousand denars have been returned."),
                    Colors.Yellow));
            }
            MobileParty? coordinator = ResolveCoordinatorParty();
            if (coordinator != null)
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_APPEAL_CANCELLED",
                    "settlement=" + _activeSettlementId + "; reason=" + reason +
                    "; refunded=" + (refundPaidFee && _feePaid));
            ReleaseCoordinator();
            ClearActiveRequest();
        }

        private void ReleaseCoordinator()
        {
            MobileParty? coordinator = ResolveCoordinatorParty();
            if (coordinator?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(coordinator);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(coordinator);
        }

        private void ClearActiveRequest()
        {
            _activeSettlementId = string.Empty;
            _activeStage = (int)FiefRequestStage.None;
            _activeFiledHour = -1d;
            _stayStartHour = -1d;
            _nextContactHour = -1d;
            _deferredTasksRemaining = 0;
            _feePaid = false;
            _publicSupportPercent = 0;
        }

        private CaptureRecord? FindActiveRecord() =>
            _captures.Where(record => string.Equals(record.SettlementId,
                    _activeSettlementId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.CaptureHour)
                .FirstOrDefault();

        private MobileParty? ResolveCoordinatorParty()
        {
            Hero? holder = GreyWardenFamilyBehavior.GetLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.PlayerRequests);
            return holder?.PartyBelongedTo?.IsActive == true
                ? holder.PartyBelongedTo
                : null;
        }

        private static Settlement? ResolveSettlement(string settlementId)
        {
            if (string.IsNullOrWhiteSpace(settlementId)) return null;
            return Settlement.All.FirstOrDefault(settlement =>
                string.Equals(settlement.StringId, settlementId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static bool MapEventContainsPlayerAsAttacker(MapEvent? mapEvent)
        {
            return mapEvent != null && mapEvent.AttackerSide.Parties.Any(party =>
                party.Party?.MobileParty?.IsMainParty == true);
        }

        private static int GetPublicSupportPercent(int reputation)
        {
            return Math.Max(0, Math.Min(100, reputation)) / 2;
        }

        private static bool IsPlayerGreyWardenMember()
        {
            PlayerBountyBehavior? behavior = Campaign.Current
                ?.GetCampaignBehavior<PlayerBountyBehavior>();
            return behavior?.IsRecruitedByGreyWardens == true &&
                   Clan.PlayerClan?.Kingdom != null &&
                   Clan.PlayerClan.IsUnderMercenaryService == false;
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
