using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        private bool _fieldCaseContract;
        private int _assignedCaseFine;
        private int _assignedCaseStanding;
        private string _pendingPrisonerHeroId = string.Empty;
        private GwpFieldFineBarterable? _casePayment;
        private bool _casePaymentOpen;
        private bool _casePaymentWithPrisoner;
        private int CaseAmountDue => Math.Max(_assignedCaseFine, Math.Max(_pendingPrisonerAssessed, Reports?.TotalAssessed ?? 0));
        private float _caseCheckElapsed;
        private string _cachedCaseHeroId = string.Empty;
        private Hero? _cachedCaseHero;

        private GwpFieldReportLedger? Reports => GwpFieldReportLedger.Instance;
        private bool HasFieldBusinessToSettle => Reports?.HasPendingReports == true || _pendingPrisonerAssessed > 0;
        private bool IsCaseClerk => IsOrdinaryGreyWardenLordConversation()
            || IsBountyCollectionCourier(MobileParty.ConversationParty);
        internal bool IsCommissionTarget(Hero? hero) => HasBountyTask && hero != null
            && hero.StringId == _activeBountyTargetHeroId;
        internal bool HasCommissionAuthority(Hero? hero) => _recruitmentAccepted && IsCommissionTarget(hero);
        internal void NotifyCaseCombatStarting(Hero? offender)
        {
            if (!IsCommissionTarget(offender)) return;
            IFaction? ours = Hero.MainHero?.MapFaction;
            IFaction? theirs = offender?.MapFaction;
            // A war already under way before this encounter is not caused by this arrest.
            if (ours != null && theirs != null && FactionManager.IsAtWarAgainstFaction(ours, theirs)
                && !_bountyTargetEncounterStarted)
                _playerFactionWasAtWarWhenBountyAccepted = true;
            _bountyTargetEncounterStarted = true;
        }

        private Hero? CaseHero
        {
            get
            {
                if (_cachedCaseHeroId != _activeBountyTargetHeroId)
                {
                    _cachedCaseHeroId = _activeBountyTargetHeroId ?? string.Empty;
                    _cachedCaseHero = string.IsNullOrEmpty(_cachedCaseHeroId) ? null :
                        Hero.FindFirst(h => h.StringId == _cachedCaseHeroId);
                }
                return _cachedCaseHero;
            }
        }

        private void ReconcileCaseOnTick(float dt)
        {
            _caseCheckElapsed += dt;
            if (_caseCheckElapsed < 1f) return;
            _caseCheckElapsed = 0f;
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true
                || MobileParty.MainParty?.MapEvent != null) return;
            ReconcileAssignedCase();
        }

        private void ReconcileAssignedCase()
        {
            if (!HasBountyTask || _pendingPrisonerAssessed > 0) return;
            Hero? hero = CaseHero;
            if (hero == null) return;
            CrimeRecord? crime = CrimePool.LedgerRecords.FirstOrDefault(r => r.HasOpenCase && r.OffenderHeroId == hero.StringId);
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner == MobileParty.MainParty?.Party)
            {
                int fine = crime == null ? _assignedCaseFine : GwpFieldArrestPricing.AssessFine(crime);
                int standing = Math.Max(0, CrimePool.GetHistory(hero)?.NegativeStanding ?? _assignedCaseStanding);
                NotifyCaseClosedByCapture(hero, fine, standing);
                if (IsWaitingForBountyCollection)
                {
                    HeroCrimeStats history = CrimePool.GetOrCreateHistory(hero);
                    history.NegativeStanding = 0;
                    history.CrimeKillProgress = 0;
                    CrimePool.CloseCaseSettledInField(crime);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_case_captive_ready}The assigned offender is in your custody. Deliver him to a Grey Warden lord or settlement party to receive your expenses."), Colors.Green));
                }
                return;
            }
            if (!IsTrackingBountyTarget) return;
            if (hero.IsDead || crime == null)
            {
                // Other Wardens may settle this case while the player is travelling.
                // Such a closure does not invent a payment or a prisoner for the player.
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_case_closed_elsewhere}The assigned case is no longer open. The commission is withdrawn without a payment or standing penalty."), Colors.Yellow));
                _activeQuest?.CancelCommission();
                EndBountyTaskState(tryRestorePeace: true);
                return;
            }
            if (hero.PartyBelongedTo?.IsActive == true)
                _activeBountyTargetId = hero.PartyBelongedTo.StringId;
        }

        private void MigrateCaseSettlement()
        {
            // In the previous implementation the old reward action cleared the
            // commission but left its report behind. Such orphan rows must never
            // charge an already-completed commission again.
            if (!HasBountyTask && Reports?.HasPendingReports == true)
            {
                GwpAiDiagnostics.WriteFieldArrest("REPORT_MIGRATED", "retired orphan report from completed legacy commission");
                Reports.DeclareAmount(Reports.TotalAssessed);
            }
            // Existing ledger rows are authoritative. Never import the same collected
            // money a second time from the old aggregate counters.
            if (Reports?.HasPendingReports != true && _pendingFieldFineAssessed > 0 && CaseHero != null)
                Reports?.RecordSettlement(CaseHero, _pendingFieldFineAssessed, _pendingFieldFine,
                    Math.Max(0, _pendingFieldFineAssessed - _pendingFieldFineSeverity * GwpTuning.Enforcement.FinePerPoint), false);
            _pendingFieldFine = 0;
            _pendingFieldFineAssessed = 0;
            if (_pendingPrisonerAssessed > 0 && string.IsNullOrEmpty(_pendingPrisonerHeroId))
                _pendingPrisonerHeroId = _activeBountyTargetHeroId ?? string.Empty;
            if (HasFieldBusinessToSettle)
            {
                _fieldCaseContract = true;
                if (!IsWaitingForBountyCollection) EnterBountyCollectionState();
                _activeBountyDeadlineHours = -1d;
                StopBountyEscortAfterTargetDefeat();
            }
            else if (IsTrackingBountyTarget)
            {
                _fieldCaseContract = true;
                CrimeRecord? crime = CrimeState.GetByOffenderId(_activeBountyTargetId);
                if (crime != null)
                {
                    _assignedCaseFine = GwpFieldArrestPricing.AssessFine(crime);
                    _assignedCaseStanding = Math.Max(0, CrimePool.GetHistory(crime.OffenderHero)?.NegativeStanding ?? 0);
                }
            }
            // Completed stable-version contracts without a field ledger keep their
            // already-promised fixed payment; newly accepted commissions do not.
        }

        private void RegisterCaseTurnInDialogues(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("gwp_case_report_lord", "lord_talk_speak_diplomacy_2", "gwp_case_report_ask",
                GwpText.Get("{=gwp_case_report_open}I have come to report on the commission."),
                () => IsCaseClerk && HasBountyTask, null, 102);
            starter.AddPlayerLine("gwp_case_report_courier", "gwp_bounty_courier_player", "gwp_case_report_ask",
                GwpText.Get("{=gwp_case_report_open}I have come to report on the commission."), null, null);
            starter.AddDialogLine("gwp_case_report_ask", "gwp_case_report_ask", "gwp_case_report_options",
                "{GWP_CASE_REPORT_SUMMARY}", PrepareCaseReportSummary, null);
            starter.AddPlayerLine("gwp_case_pay_full", "gwp_case_report_options", "gwp_case_report_receipt",
                "{GWP_CASE_FULL_PAYMENT}", PrepareFullPaymentOption, PayCaseInFull);
            starter.AddPlayerLine("gwp_case_pay_truth", "gwp_case_report_options", "gwp_case_report_receipt",
                "{GWP_CASE_TRUE_PAYMENT}", PrepareTruthfulPaymentOption, PayTruthfulShortfall);
            starter.AddPlayerLine("gwp_case_pay_false", "gwp_case_report_options", "gwp_case_barter_open",
                GwpText.Get("{=gwp_case_false_payment}[Lie] This is all I collected. Let me show you the account."),
                () => HasBountyTask && CaseAmountDue > 0, () => _casePaymentWithPrisoner = false);
            starter.AddPlayerLine("gwp_case_false_zero", "gwp_case_report_options", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_false_zero}[Lie] I collected nothing. Enter that in the account."),
                () => HasBountyTask && CaseAmountDue > 0, () => CompleteCashReport(0, true));
            starter.AddPlayerLine("gwp_case_deliver_prisoner", "gwp_case_report_options", "gwp_case_report_receipt",
                "{GWP_CASE_PRISONER_PAYMENT}", PreparePrisonerPaymentOption, () => DeliverCasePrisoner(false));
            starter.AddPlayerLine("gwp_case_prisoner_hide", "gwp_case_report_options", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_prisoner_hide}[Lie] Here is the prisoner. I took no money from him."),
                () => CanDeliverCasePrisoner() && Reports?.TotalReceived > 0, () => DeliverCasePrisoner(true));
            starter.AddPlayerLine("gwp_case_prisoner_partial", "gwp_case_report_options", "gwp_case_barter_open",
                GwpText.Get("{=gwp_case_prisoner_partial}[Lie] Take the prisoner and the money I have listed here."),
                () => CanDeliverCasePrisoner() && Reports?.TotalReceived > 0, () => _casePaymentWithPrisoner = true);
            starter.AddPlayerLine("gwp_case_legacy_reward", "gwp_case_report_options", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_legacy_reward}Settle the payment promised under the old warrant."),
                () => IsWaitingForBountyCollection && !_fieldCaseContract && !HasFieldBusinessToSettle,
                () => FinishCaseReport(_activeBountyReward));
            starter.AddPlayerLine("gwp_case_report_later", "gwp_case_report_options", "close_window",
                GwpText.Get("{=gwp_case_report_later}I am not ready to hand it over yet."), null, LeaveCaseClerk);
            starter.AddDialogLine("gwp_case_barter_open", "gwp_case_barter_open", "gwp_case_barter_result",
                GwpText.Get("{=gwp_case_barter_count}Put the money here. Let us count it."), null, OpenCasePayment);
            starter.AddDialogLine("gwp_case_barter_accepted", "gwp_case_barter_result", "gwp_case_receipt_ack",
                GwpText.Get("{=gwp_case_barter_accepted}We have counted the money. Your report will be entered."),
                () => _casePaymentOpen && _casePayment?.Applied == true && Campaign.Current.BarterManager.LastBarterIsAccepted,
                CommitCasePayment);
            starter.AddDialogLine("gwp_case_barter_cancelled", "gwp_case_barter_result", "gwp_case_report_options",
                GwpText.Get("{=gwp_case_barter_cancelled}Nothing has been entered. We can count it again when you are ready."),
                null, () => {
                    GwpAiDiagnostics.WriteFieldArrest("REPORT_PAYMENT_CANCELLED", "open=" + _casePaymentOpen + "; applied=" + (_casePayment?.Applied == true));
                    _casePaymentOpen = false; _casePayment = null;
                });
            starter.AddPlayerLine("gwp_case_report_acknowledge", "gwp_case_receipt_ack", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_report_acknowledge}Let me have the account."), null, null);
            starter.AddDialogLine("gwp_case_report_receipt", "gwp_case_report_receipt", "close_window",
                "{GWP_CASE_REPORT_RESULT}", null, LeaveCaseClerk);
        }

        private bool PrepareFullPaymentOption()
        {
            MBTextManager.SetTextVariable("GWP_CASE_FULL_PAYMENT", GwpText.Get(
                "{=gwp_case_full_payment}Here is the full {VAR_1} denars. Settle the account.", "VAR_1", CaseAmountDue));
            return HasBountyTask && CaseAmountDue > 0 && Hero.MainHero.Gold >= CaseAmountDue;
        }

        private bool PrepareTruthfulPaymentOption()
        {
            int received = Reports?.TotalReceived ?? 0;
            MBTextManager.SetTextVariable("GWP_CASE_TRUE_PAYMENT", GwpText.Get(
                "{=gwp_case_true_payment}I collected {VAR_1} denars, and I am handing over all of it. I could not obtain the rest.", "VAR_1", received));
            return Reports?.HasPendingReports == true && received < CaseAmountDue && !CanDeliverCasePrisoner()
                && Hero.MainHero.Gold >= received;
        }

        private bool PreparePrisonerPaymentOption()
        {
            int cash = Reports?.TotalReceived ?? 0;
            MBTextManager.SetTextVariable("GWP_CASE_PRISONER_PAYMENT", cash > 0
                ? GwpText.Get("{=gwp_case_prisoner_cash}Here is the prisoner, and the {VAR_1} denars I already took from him.", "VAR_1", cash)
                : GwpText.Get("{=gwp_case_deliver_prisoner}The offender is here. Take him into your custody."));
            return CanDeliverCasePrisoner() && Hero.MainHero.Gold >= cash;
        }

        private void PayCaseInFull()
        {
            int due = CaseAmountDue;
            if (!HasBountyTask || due <= 0 || Hero.MainHero.Gold < due) return;
            if (PoliceResourceManager.DepositFieldFine(due) == due) CompleteCashReport(due, false);
        }

        private void PayTruthfulShortfall()
        {
            int cash = Reports?.TotalReceived ?? 0;
            if (Reports?.HasPendingReports != true || Hero.MainHero.Gold < cash) return;
            if (PoliceResourceManager.DepositFieldFine(cash) == cash) CompleteCashReport(cash, false);
        }

        private bool PrepareCaseReportSummary()
        {
            int due = CaseAmountDue;
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_SUMMARY", GwpText.Get(
                "{=gwp_case_report_summary}The account lists {VAR_1} denars. Have you brought the fines, or the prisoner? If any money is missing, tell me why.", "VAR_1", due > 0 ? due : _pendingPrisonerAssessed));
            return true;
        }

        private void OpenCasePayment()
        {
            _casePaymentOpen = false;
            _casePayment = null;
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            PartyBase? clerk = MobileParty.ConversationParty?.Party
                ?? Hero.OneToOneConversationHero?.PartyBelongedTo?.Party
                ?? Hero.OneToOneConversationHero?.CurrentSettlement?.Party
                ?? treasurer?.PartyBelongedTo?.Party;
            if (treasurer == null || treasurer.IsDead || clerk == null || !HasBountyTask || CaseAmountDue <= 0) return;
            try
            {
                _casePayment = new GwpFieldFineBarterable(treasurer, _casePaymentWithPrisoner ? Reports?.TotalReceived ?? 0 : CaseAmountDue);
                _casePaymentOpen = true;
                GwpAiDiagnostics.WriteFieldArrest("REPORT_PAYMENT_OPEN", "assessed=" + CaseAmountDue + "; received=" + Reports?.TotalReceived + "; prisoner=" + _casePaymentWithPrisoner);
                BarterManager manager = Campaign.Current.BarterManager;
                // The native context initializer selects entries; it does NOT filter
                // the normal trading catalogue. Remove those entries before the VM
                // is created so this table contains only this commission's payment.
                BarterManager.BarterBeginEventDelegate original = manager.BarterBegin;
                try
                {
                    manager.BarterBegin = data =>
                    {
                        data.GetBarterables().RemoveAll(entry => !ReferenceEquals(entry, _casePayment));
                        original?.Invoke(data);
                    };
                    manager.StartBarterOffer(Hero.MainHero, treasurer,
                        MobileParty.MainParty.Party, clerk, null, CasePaymentContext, 0, false, new[] { _casePayment });
                }
                finally { manager.BarterBegin = original; }
            }
            catch (Exception ex)
            {
                _casePaymentOpen = false;
                GwpAiDiagnostics.WriteFieldArrest("REPORT_PAYMENT_FAILED", ex.ToString());
            }
        }

        private bool CasePaymentContext(Barterable barterable, BarterData data, object obj) =>
            ReferenceEquals(barterable, _casePayment);

        private void CommitCasePayment()
        {
            if (!_casePaymentOpen || _casePayment?.Applied != true || !Campaign.Current.BarterManager.LastBarterIsAccepted) return;
            int paid = _casePayment?.Applied == true ? _casePayment.Paid : 0;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_PAYMENT_ACCEPTED", "applied=" + (_casePayment?.Applied == true) + "; paid=" + paid);
            _casePaymentOpen = false;
            _casePayment = null;
            if (_casePaymentWithPrisoner) DeliverCasePrisoner(true, paid);
            else CompleteCashReport(paid, true);
        }

        private void CompleteCashReport(int delivered, bool falseReport)
        {
            if (!HasBountyTask || CaseHero == null || Reports == null) return;
            GwpFieldReportLedger ledger = Reports;
            int due = CaseAmountDue;
            if (!ledger.HasPendingReports)
                ledger.RecordSettlement(CaseHero, due, 0, Math.Max(0, due - _assignedCaseStanding * GwpTuning.Enforcement.FinePerPoint), false);
            int legitimateGap = ledger.LegitimatePovertyShortfall;
            int fee = CalculateCaseFee(Math.Min(Math.Max(0, delivered), due));
            if (falseReport) ledger.DeclareFalseAmount(delivered);
            else ledger.DeclareAmount(delivered, truthful: true);
            GwpAiDiagnostics.WriteFieldArrest("REPORT_CASH", "due=" + due + "; delivered=" + delivered + "; falseReport=" + falseReport);
            int paidFee = PoliceResourceManager.PayFromJudicialTreasury(fee);
            FinishCaseReport(paidFee);
            if (!falseReport && delivered < due)
                MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                    legitimateGap >= due - delivered
                        ? "{=gwp_case_truthful_poor_result}The {VAR_1} denars are entered. His unpaid debt remains against him; you are not penalized. Your expenses are {VAR_2} denars. The commission is concluded."
                        : "{=gwp_case_truthful_gap_result}The {VAR_1} denars are entered. The unjustified shortfall is recorded against your commission. Your expenses are {VAR_2} denars. The commission is concluded.",
                    "VAR_1", delivered, "VAR_2", paidFee));
        }

        private int CalculateCaseFee(int cap) => GwpCaseSettlementRules.Reward(cap,
            _bountyPlayerCasualties, Math.Max(_pendingFieldFineSeverity, _assignedCaseStanding));

        private bool CanDeliverCasePrisoner()
        {
            Hero? prisoner = CaseHero;
            return _pendingPrisonerAssessed > 0 && prisoner != null
                && prisoner.StringId == _pendingPrisonerHeroId && prisoner.IsPrisoner
                && prisoner.PartyBelongedToAsPrisoner == MobileParty.MainParty?.Party;
        }

        private void DeliverCasePrisoner(bool conceal, int prepaid = 0)
        {
            if (!CanDeliverCasePrisoner()) { RefundPrisonerPrepayment(prepaid); return; }
            Hero prisoner = CaseHero!;
            int cash = conceal ? 0 : Reports?.TotalReceived ?? 0;
            if (Hero.MainHero.Gold < cash)
            {
                MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                    "{=gwp_case_cash_with_prisoner}You also collected {VAR_1} denars from this offender. Bring that money with the prisoner so both can be handed over together.", "VAR_1", cash));
                return;
            }
            PartyBase? receiver = MobileParty.ConversationParty?.Party
                ?? Hero.OneToOneConversationHero?.PartyBelongedTo?.Party
                ?? Hero.OneToOneConversationHero?.CurrentSettlement?.Party;
            if (receiver == null || receiver == MobileParty.MainParty.Party)
            {
                RefundPrisonerPrepayment(prepaid);
                MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get("{=gwp_case_transfer_failed}We cannot take custody here. Keep the prisoner and report to another Warden party."));
                return;
            }
            try
            {
                TransferPrisonerAction.Apply(prisoner.CharacterObject, MobileParty.MainParty.Party, receiver);
                if (prisoner.PartyBelongedToAsPrisoner != receiver) { RefundPrisonerPrepayment(prepaid); return; }
                PoliceResourceManager.DepositFieldFine(cash);
                int fee = CalculateCaseFee(_pendingPrisonerAssessed);
                // A paid-then-captured target may have both a cash report and a prisoner.
                // Delivering the man replaces that report; it cannot pay twice.
                Reports?.ResolveByPrisoner(prisoner.StringId, cash + prepaid);
                FinishCaseReport(PoliceResourceManager.PayFromJudicialTreasury(fee));
            }
            catch (Exception ex)
            {
                if (prisoner.PartyBelongedToAsPrisoner == MobileParty.MainParty.Party)
                    RefundPrisonerPrepayment(prepaid);
                GwpAiDiagnostics.WriteFieldArrest("REPORT_PRISONER_FAILED", ex.ToString());
                MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get("{=gwp_case_transfer_failed}We cannot take custody here. Keep the prisoner and report to another Warden party."));
            }
        }

        private void RefundPrisonerPrepayment(int amount)
        {
            if (amount > 0)
            {
                int refunded = PoliceResourceManager.PayFromJudicialTreasury(amount);
                GwpAiDiagnostics.WriteFieldArrest("REPORT_PAYMENT_REFUNDED", "paid=" + amount + "; refunded=" + refunded);
            }
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                "{=gwp_case_transfer_retry}The delivery could not be completed; the money has been returned. Keep the prisoner and funds and report to another Warden lord."));
        }

        private void FinishCaseReport(int reward)
        {
            if (!HasBountyTask) return;
            var openCrime = CrimePool.LedgerRecords.FirstOrDefault(r => r.HasOpenCase && r.OffenderHeroId == _activeBountyTargetHeroId);
            if (openCrime != null) CrimePool.CloseCaseSettledInField(openCrime);
            // Fixed legacy rewards have not been transferred; all new fees already
            // came from the judicial treasury. Keep that distinction explicit.
            if (!_fieldCaseContract && reward > 0) Hero.MainHero.ChangeHeroGold(reward);
            _activeQuest?.SucceedQuest();
            MakePeaceWithCriminalFaction();
            MobileParty? courier = IsBountyCollectionCourier(MobileParty.ConversationParty) ? MobileParty.ConversationParty : null;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_COMPLETED", "reward=" + reward + "; offender=" + _activeBountyTargetHeroId);
            ClearBountyTaskState(courier);
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                "{=gwp_case_report_complete}Your report is entered. The treasury has paid {VAR_1} denars in expenses. This commission is concluded.", "VAR_1", reward));
        }

        private void LeaveCaseClerk()
        {
            MobileParty? courier = MobileParty.ConversationParty;
            if (!IsWaitingForBountyCollection && IsBountyCollectionCourier(courier))
                CloseBountyCollectionCourierEncounterAndReturn(courier);
            else if (PlayerEncounter.Current != null) PlayerEncounter.LeaveEncounter = true;
        }
    }
}
