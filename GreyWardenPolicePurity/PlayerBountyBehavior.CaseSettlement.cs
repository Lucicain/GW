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
        private GwpAssetPayment? _casePayment;
        private bool _casePaymentOpen;
        private int _caseSubmitted = -1;
        private bool _caseSubmittedPrisoner;
        private int CaseAmountDue => Math.Max(_assignedCaseFine, Math.Max(_pendingPrisonerAssessed, Reports?.TotalAssessed ?? 0));
        private float _caseCheckElapsed;
        private TaleWorlds.CampaignSystem.MapEvents.MapEvent? _caseBattleToReconcile;
        private string _caseBattleHeroId = string.Empty;
        private string _cachedCaseHeroId = string.Empty;
        private Hero? _cachedCaseHero;

        // ---- 本案办案结果。抓到人只是其中一种，不是唯一一种 ----
        /// <summary>玩家参战、本案目标在败方：执法达成，哪怕人跑了。</summary>
        private bool _caseTargetDefeated;
        /// <summary>本案已经和平了结过——缴清罚金，或兑现了谈成的处置。</summary>
        private bool _casePeacefullyResolved;
        /// <summary>本案目标实际交到玩家手里的钱，用来判断之后再动手算不算过度执法。</summary>
        private int _caseCashCollectedFromTarget;
        /// <summary>同一宗案子的执法成功只登记一次震慑。</summary>
        private string _caseDeterrenceRegisteredId = string.Empty;
        /// <summary>案子在玩家手上被别人了结了：没有罚金可交、没有人可押，只剩一笔辛苦费。</summary>
        private bool _caseClosedElsewhere;
        internal bool IsCaseClosedElsewhere => _caseClosedElsewhere;
        /// <summary>击败之后不再让期限把这宗案子作废，玩家仍可回去交差。</summary>
        internal bool HasCompletedEnforcement => _caseTargetDefeated || _casePeacefullyResolved;

        /// <summary>
        /// 玩家接了这宗案子，他就是这宗案子的主理人。在他交差之前，灰袍不再自行派人
        /// 承办这个罪犯——别人该去干别的事。只有玩家求援送达之后，才由
        /// <see cref="AssignSupportResponder"/> 明确指派一名灰袍领主接手支援。
        /// </summary>
        internal static bool IsCaseHeldByPlayer(string? offenderHeroId)
        {
            if (string.IsNullOrWhiteSpace(offenderHeroId)) return false;
            var current = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            return current != null && current.HasBountyTask
                && string.Equals(current._activeBountyTargetHeroId, offenderHeroId,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>本案办成之后，把灰袍从这宗案子上撤下来，并解除护送关系。</summary>
        private void ReleaseWardensAfterEnforcement()
        {
            PoliceEnforcementBehavior.ReleaseWardensFromCase(_activeBountyTargetHeroId);
            StopBountyEscortAfterTargetDefeat();
            // 执法已经完成，这场因案件而起的战争也到此为止；玩家若还想追缴罚金，
            // 和平反而是他能开口的前提。接案前就有的战争不在此列。
            MakePeaceWithCriminalFaction();
        }

        private GwpFieldReportLedger? Reports => GwpFieldReportLedger.Instance;
        private bool HasFieldBusinessToSettle => Reports?.HasPendingReports == true || _pendingPrisonerAssessed > 0;
        private bool IsCaseClerk => IsOrdinaryGreyWardenLordConversation();
        internal bool IsCommissionTarget(Hero? hero) => HasBountyTask && hero != null
            && hero.StringId == _activeBountyTargetHeroId;
        internal int ExpectedCaseFee => CalculateCaseFee(CaseAmountDue);
        internal bool CanOfferFieldGrace => IsTrackingBountyTarget && _activeBountyDeadlineHours - CampaignTime.Now.ToHours >= 72d;
        internal void RecordFieldGrace(string message) => _activeQuest?.WriteLog(message);
        internal bool HasCommissionAuthority(Hero? hero) => _recruitmentAccepted && IsCommissionTarget(hero);
        internal void NotifyCaseCombatStarting(Hero? offender)
        {
            if (!IsCommissionTarget(offender)) return;
            if (offender != null) Reports?.RecordBetrayal(offender.StringId);
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
                || MobileParty.MainParty?.MapEvent != null || PlayerEncounter.Current != null) return;
            ReconcileAssignedCase();
            if (_caseBattleToReconcile != null)
            {
                var battle = _caseBattleToReconcile;
                _caseBattleToReconcile = null;
                Hero? target = CaseHero;
                if (target == null || target.StringId != _caseBattleHeroId || !HasBountyTask) return;
                if (target.IsPrisoner && target.PartyBelongedToAsPrisoner == MobileParty.MainParty?.Party)
                {
                    RegisterCaseDeterrence(battle, target, countAsArrest: true);
                }
                else if (_caseTargetDefeated)
                {
                    // 击溃他的部队本身就是惩戒的目的。人跑掉是结果的一种，不是失败。
                    RegisterCaseDeterrence(battle, target, countAsArrest: false);
                    string message = GwpText.Get("{=gwp_case_defeat_recorded}You broke {VAR_1} in the field. Go and make your report.",
                        "VAR_1", target.Name.ToString());
                    _activeQuest?.WriteLog(message);
                    try { _activeQuest?.MarkReadyForTurnIn(); } catch { }
                    InformationManager.DisplayMessage(new InformationMessage(message, Colors.Green));
                }
                else if (!target.IsDead)
                {
                    string message = GwpText.Get("{=gwp_case_capture_missed}The offender is not in your custody. The commission remains open: pursue him again or settle the fine with the Wardens. Any money already collected must still be reported.");
                    _activeQuest?.WriteLog(message);
                    InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
                }
            }
        }

        /// <summary>
        /// 记录一次本案的执法结果：战场上打垮、押走，或和平了结。同一宗案子只登记一次，
        /// 之后亲自交差、派人送信都不会重复加分。押走才算一次被捕，其余不动拘捕履历。
        /// </summary>
        private void RegisterCaseDeterrence(
            TaleWorlds.CampaignSystem.MapEvents.MapEvent? battle, Hero? target, bool countAsArrest)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.StringId)) return;
            string key = target.StringId + "|" + (_activeQuest?.StringId ?? _activeBountyTargetId ?? string.Empty);
            if (string.Equals(_caseDeterrenceRegisteredId, key, StringComparison.Ordinal)) return;
            _caseDeterrenceRegisteredId = key;

            var deterrence = Campaign.Current?.GetCampaignBehavior<PoliceAIDeterrenceBehavior>();
            if (deterrence == null) return;
            if (countAsArrest) deterrence.RegisterPlayerCompletedCase(
                battle, target, (GwpCrimeCategory)_activeBountyCrimeCategory);
            else deterrence.RegisterPlayerEnforcementSuccess(
                battle, target, (GwpCrimeCategory)_activeBountyCrimeCategory);
            GwpAiDiagnostics.WriteFieldArrest("CASE_DETERRENCE_REGISTERED",
                "offender=" + target.StringId + "; countAsArrest=" + countAsArrest
                + "; battle=" + (battle != null));
        }

        /// <summary>
        /// 战斗结束后登记本案结果。打赢了就是打赢了，人有没有落到手里是另一件事。
        /// 已经缴过罚金或兑现过处置的人再被打垮，另外记一笔过度执法待查。
        /// </summary>
        internal void NotifyCaseBattleOutcome(Hero? target, bool playerWon, bool targetOnLosingSide)
        {
            if (target == null || !IsCommissionTarget(target)) return;
            if (!playerWon || !targetOnLosingSide)
            {
                GwpAiDiagnostics.WriteFieldArrest("CASE_BATTLE_OUTCOME",
                    "offender=" + target.StringId + "; playerWon=" + playerWon
                    + "; targetOnLosingSide=" + targetOnLosingSide + "; defeated=False");
                return;
            }

            bool first = !_caseTargetDefeated;
            _caseTargetDefeated = true;
            if (first) ReleaseWardensAfterEnforcement();
            int alreadySettled = Math.Max(_caseCashCollectedFromTarget,
                Reports?.PendingReceivedFor(target.StringId) ?? 0);
            if (_casePeacefullyResolved || alreadySettled > 0)
                Reports?.RecordExcessEnforcement(target.StringId,
                    Math.Max(alreadySettled, GwpTuning.Enforcement.FinePerPoint));
            GwpAiDiagnostics.WriteFieldArrest("CASE_BATTLE_OUTCOME",
                "offender=" + target.StringId + "; defeated=True; first=" + first
                + "; alreadySettled=" + alreadySettled
                + "; peacefullyResolved=" + _casePeacefullyResolved);
        }

        /// <summary>和平了结：缴清罚金，或兑现谈成的处置。执法成功，但没有人被押走。</summary>
        internal void NotifyCasePeacefullyResolved(Hero? target, int collectedFine)
        {
            if (target == null || !IsCommissionTarget(target)) return;
            bool firstResolution = !_casePeacefullyResolved;
            _casePeacefullyResolved = true;
            _caseCashCollectedFromTarget += Math.Max(0, collectedFine);
            // 震慑由野外执法那一侧统一登记（GwpFieldArrestBehavior.RegisterFieldEnforcement），
            // 那条路同时覆盖有委托和没委托的情形；这里再记一次就是重复。
            _caseDeterrenceRegisteredId = target.StringId + "|" +
                (_activeQuest?.StringId ?? _activeBountyTargetId ?? string.Empty);
            if (firstResolution) ReleaseWardensAfterEnforcement();
        }

        private void SyncCaseOutcomeData(IDataStore dataStore) =>
            GwpRuntimeFaultWatch.Guard("BOUNTY_CASE_OUTCOME_SYNC", () => SyncCaseOutcomeFields(dataStore));

        /// <summary>
        /// 本案结果整体存成一个字符串。原来的五个独立键里有一个在读档时抛
        /// InvalidCastException（见 GreyWarden-Faults.log 的 BOUNTY_CASE_OUTCOME_SYNC），
        /// 而它后面的 `_supportRequested` 因此永远没有被读回来——支援状态每次读档都丢，
        /// 表现为"求援过了，灰袍却又不认"。一个键、一种类型，就不会再有这种半途失败。
        /// </summary>
        private string _caseOutcomeState = string.Empty;

        private void SyncCaseOutcomeFields(IDataStore dataStore)
        {
            if (dataStore.IsSaving) _caseOutcomeState = SerializeCaseOutcome();
            dataStore.SyncData("gwp_case_outcome_state", ref _caseOutcomeState);
            if (!dataStore.IsLoading) return;

            if (!string.IsNullOrEmpty(_caseOutcomeState))
            {
                DeserializeCaseOutcome(_caseOutcomeState!);
                return;
            }
            ReadLegacyCaseOutcome(dataStore);
        }

        private string SerializeCaseOutcome() => string.Join(";",
            _caseTargetDefeated ? "1" : "0",
            _casePeacefullyResolved ? "1" : "0",
            _supportRequested ? "1" : "0",
            _caseCashCollectedFromTarget.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _caseClosedElsewhere ? "1" : "0",
            _caseDeterrenceRegisteredId ?? string.Empty);

        private void DeserializeCaseOutcome(string state)
        {
            string[] parts = state.Split(new[] { ';' }, 6);
            _caseTargetDefeated = parts.Length > 0 && parts[0] == "1";
            _casePeacefullyResolved = parts.Length > 1 && parts[1] == "1";
            _supportRequested = parts.Length > 2 && parts[2] == "1";
            _caseCashCollectedFromTarget = parts.Length > 3 &&
                int.TryParse(parts[3], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int cash)
                ? Math.Max(0, cash) : 0;
            _caseClosedElsewhere = parts.Length > 4 && parts[4] == "1";
            _caseDeterrenceRegisteredId = parts.Length > 5 ? parts[5] ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 旧存档里的五个分键。逐个读、逐个兜住：其中一个坏掉不能再把后面的一起带走，
        /// 并且把出问题的键名写进故障日志，好确认到底是哪一个。
        /// </summary>
        private void ReadLegacyCaseOutcome(IDataStore dataStore)
        {
            _caseTargetDefeated = ReadLegacyFlag(dataStore, "gwp_case_target_defeated");
            _casePeacefullyResolved = ReadLegacyFlag(dataStore, "gwp_case_peacefully_resolved");
            _supportRequested = ReadLegacyFlag(dataStore, "gwp_case_support_requested");
            GwpRuntimeFaultWatch.Guard("LEGACY_gwp_case_cash_from_target", () =>
            {
                int cash = 0;
                dataStore.SyncData("gwp_case_cash_from_target", ref cash);
                _caseCashCollectedFromTarget = Math.Max(0, cash);
            });
            GwpRuntimeFaultWatch.Guard("LEGACY_gwp_case_deterrence_key", () =>
            {
                string key = string.Empty;
                dataStore.SyncData("gwp_case_deterrence_key", ref key);
                _caseDeterrenceRegisteredId = key ?? string.Empty;
            });
        }

        private static bool ReadLegacyFlag(IDataStore dataStore, string key)
        {
            // The support key was a bool; the other flags were integers.
            // Accept both historical representations in player builds too.
            try
            {
                return GwpLegacySave.ReadFlag(dataStore, key, key == "gwp_case_support_requested");
            }
            catch (InvalidCastException exception)
            {
                GwpFaultTrace.Write("INVALID_LEGACY_FLAG", details: key + " | " + exception);
                return false;
            }
        }

        /// <summary>
        /// 案子在玩家手上被别人了结了。对 NPC 来说案子就此消失，他去接下一宗；玩家忙了
        /// 半天不能只收到一句"委托取消"。委托转入交差状态，他可以亲自去、也可以派人去
        /// 向灰袍复命，领一笔按实际出力算的辛苦费。不记震慑，不扣声望，不编造缴款。
        /// </summary>
        private void WithdrawCommissionClosedElsewhere(CrimeRecord? crime, Hero? hero)
        {
            _caseClosedElsewhere = true;
            _pendingPrisonerAssessed = 0;
            _pendingPrisonerHeroId = string.Empty;
            // 已经从他手里收到过钱的话，那笔钱照样要上缴——人没了不等于账没了。
            // 只有确实一分没收时，才把应缴清零，走"只领辛苦费"那条路。
            if (Reports?.HasPendingReports != true) _assignedCaseFine = 0;
            if (crime != null) CrimePool.CloseCaseSettledInField(crime);
            PoliceEnforcementBehavior.ReleaseWardensFromCase(_activeBountyTargetHeroId);
            StopBountyEscortAfterTargetDefeat();
            MakePeaceWithCriminalFaction();

            EnterBountyCollectionState();
            _activeBountyDeadlineHours = -1d;
            try { _activeQuest?.MarkReadyForTurnIn(); } catch { }

            string message = GwpText.Get(
                "{=gwp_case_closed_elsewhere}{VAR_1} is beyond your reach now. The commission is void. Report to any Grey Warden lord, or send a man.",
                "VAR_1", hero?.Name?.ToString() ?? _activeBountyTargetName ?? string.Empty);
            _activeQuest?.WriteLog(GwpText.Create(message));
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
            GwpAiDiagnostics.WriteFieldArrest("CASE_WITHDRAWN_CLOSED_ELSEWHERE",
                "offender=" + (_activeBountyTargetHeroId ?? "-") +
                "; dead=" + (hero?.IsDead == true) + "; prisoner=" + (hero?.IsPrisoner == true) +
                "; crimeGone=" + (crime == null) + "; casualties=" + _bountyPlayerCasualties);
        }

        /// <summary>
        /// 案子被别人了结，而且玩家手上一分钱、一个人都没有。这时才只剩一笔辛苦费；
        /// 收过钱就仍然要正常上缴，只是差额全免。
        /// </summary>
        private bool HasNothingLeftToSettle =>
            _caseClosedElsewhere && CaseAmountDue <= 0 && !HasFieldBusinessToSettle;

        /// <summary>白跑一趟的辛苦费：按底薪加实际阵亡算，不含按罪责严重度给的那一段。</summary>
        private int WithdrawnCaseCompensation() =>
            GwpCaseSettlementRules.WithdrawnCaseCompensation(_bountyPlayerCasualties);

        #region 派人复命

        /// <summary>还有帐要交、或者还有人要押走，才值得派一趟。</summary>
        internal bool CanDispatchCaseReport => HasBountyTask && CaseHero != null
            && (HasNothingLeftToSettle || CaseAmountDue > 0 || CanDeliverCasePrisoner());
        internal int CaseReportAmountDue => CaseAmountDue;
        internal string CaseReportIdentity => CaseHero?.StringId ?? string.Empty;
        internal int CaseReportSuggestedPayment => Reports?.PendingReceivedFor(CaseReportIdentity) ?? 0;
        internal GwpCaseReceipt? CaseReportReceipt => Reports?.ReceiptFor(CaseReportIdentity);
        internal bool CaseReportNeedsExplanation(int delivered, bool prisoner) =>
            (!prisoner && delivered < CaseAmountDue) || Reports?.NeedsExplanation(CaseReportIdentity, delivered, prisoner) == true;

        /// <summary>
        /// 这名俘虏就是本案要押走的人，可以交给派出去的队伍带走。
        ///
        /// 判的是 <see cref="_pendingPrisonerHeroId"/>——**被押下的那一刻记下来的人**，
        /// 而不是 <c>CaseHero</c>。对方选择投降之后，委托会转入待交付状态，追捕目标那几个
        /// 字段随之清掉；再拿 <c>CaseHero</c> 去比，人就对不上了，于是分兵界面里那名俘虏
        /// 根本选不动，玩家只能自己押着他跑一趟。
        /// </summary>
        internal bool IsDeliverableCasePrisoner(Hero? hero) =>
            hero != null && CanDeliverCasePrisoner() &&
            string.Equals(hero.StringId, _pendingPrisonerHeroId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 派出去的队伍替玩家交了差。判定口径和玩家亲自去完全一样：足额或交人算完美，
        /// 真穷和已经打垮都不罚玩家，撒谎照样单独查账。办案费交到使者手上带回来。
        /// </summary>
        /// <returns>
        /// 交到使者手上的办案费，由他带回来；委托已经不在（玩家自己交过、或案子被撤）
        /// 时返回 -1。这笔钱不直接进玩家口袋——使者在路上出事就跟着没了。
        /// </returns>
        internal int CompleteDispatchedCaseReport(int deliveredCash, bool falseReport,
            bool prisonerDelivered, PartyBase? receiver)
        {
            if (!HasBountyTask || CaseHero == null || Reports == null) return -1;
            GwpFieldReportLedger ledger = Reports;
            int due = CaseAmountDue;

            if (prisonerDelivered && _pendingPrisonerAssessed > 0)
            {
                int prisonerFee = CalculateCaseFee(_pendingPrisonerAssessed);
                ledger.ResolveByPrisoner(_pendingPrisonerHeroId, Math.Max(0, deliveredCash), !falseReport);
                int paidPrisonerFee = PoliceResourceManager.WithdrawFromJudicialTreasury(prisonerFee);
                FinishDispatchedReport(paidPrisonerFee);
                return paidPrisonerFee;
            }

            if (HasNothingLeftToSettle)
            {
                int compensation = PoliceResourceManager.WithdrawFromJudicialTreasury(
                    WithdrawnCaseCompensation());
                GwpAiDiagnostics.WriteFieldArrest("REPORT_DISPATCHED_WITHDRAWN_CASE",
                    "offender=" + (_activeBountyTargetHeroId ?? "-") + "; compensation=" + compensation);
                FinishDispatchedReport(compensation);
                return compensation;
            }
            // 玩家代表灰袍。对方最后交得出多少不是他的过失，办案费按整案算，不按实收封顶。
            int fee = CalculateCaseFee(due);
            ledger.DeclareAmount(deliveredCash, truthful: !falseReport, offenderId: CaseReportIdentity);
            int paidFee = PoliceResourceManager.WithdrawFromJudicialTreasury(fee);
            FinishDispatchedReport(paidFee);
            return paidFee;
        }

        /// <summary>
        /// 和当面交差走同一条收尾：关案、结束任务、恢复和平。差别只在酬劳不是
        /// 当场进玩家口袋，而是由使者带回来。
        /// </summary>
        private void FinishDispatchedReport(int fee)
        {
            var openCrime = CrimePool.LedgerRecords.FirstOrDefault(r =>
                r.HasOpenCase && r.OffenderHeroId == _activeBountyTargetHeroId);
            if (openCrime != null) CrimePool.CloseCaseSettledInField(openCrime);
            _activeQuest?.SucceedQuest();
            MakePeaceWithCriminalFaction();
            GwpAiDiagnostics.WriteFieldArrest("REPORT_DISPATCHED_COMPLETED",
                "fee=" + fee + "; offender=" + _activeBountyTargetHeroId);
            ClearBountyTaskState();
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_dispatch_report_done}Your men have made the report. The commission is closed; they are bringing {VAR_1} denars home to you.",
                "VAR_1", fee), Colors.Green));
        }

        #endregion

        private void ClearCaseOutcomeState()
        {
            _caseTargetDefeated = false;
            _casePeacefullyResolved = false;
            _caseClosedElsewhere = false;
            _caseOutcomeState = string.Empty;
            _caseCashCollectedFromTarget = 0;
            _caseDeterrenceRegisteredId = string.Empty;
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
                        GwpText.Get("{=gwp_case_captive_ready}The assigned offender is in your custody. Deliver him to any Grey Warden lord, in person or by your own men, to have your expenses settled."), Colors.Green));
                }
                return;
            }
            if (!IsTrackingBountyTarget) return;

            // 普通案件在承办队那边靠 IsTargetValid / IsOffenderPursuable 自动结案：罪犯
            // 死了、被别人拿下了、部队没了，案子就消失，那名灰袍去接下一宗。玩家自己
            // 承办，没有承办队替他做这件事，所以这里按同一套判据替他判。
            bool offenderGone = hero.IsDead || hero.IsPrisoner || crime == null ||
                crime.Offender?.IsActive != true;
            if (offenderGone)
            {
                WithdrawCommissionClosedElsewhere(crime, hero);
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
            starter.AddDialogLine("gwp_case_report_ask", "gwp_case_report_ask", "gwp_case_report_options",
                "{GWP_CASE_REPORT_SUMMARY}", PrepareCaseReportSummary, null);
            starter.AddPlayerLine("gwp_case_pay_any", "gwp_case_report_options", "gwp_case_barter_open",
                GwpText.Get("{=gwp_case_pay_any}I am ready to submit my report. Let us settle the account."),
                () => HasBountyTask && _caseSubmitted < 0, null);
            starter.AddPlayerLine("gwp_case_resume_report", "gwp_case_report_options", "gwp_case_short_question",
                GwpText.Get("{=gwp_case_resume_report}About the shortfall in what I handed over..."), () => _caseSubmitted >= 0, null);
            starter.AddPlayerLine("gwp_case_report_later", "gwp_case_report_options", "close_window",
                GwpText.Get("{=gwp_case_report_later}I am not ready to hand it over yet."), null, LeaveCaseClerk);
            // Financially empty / legacy contracts use the same report button;
            // keep saved entitlements without exposing obsolete menu entries.
            starter.AddDialogLine("gwp_case_settle_withdrawn", "gwp_case_barter_open", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_settle_expenses}The commission is closed. We will settle what you are owed."),
                () => HasBountyTask && HasNothingLeftToSettle, CompleteWithdrawnCaseReport, 130);
            starter.AddDialogLine("gwp_case_settle_legacy", "gwp_case_barter_open", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_settle_expenses}The commission is closed. We will settle what you are owed."),
                () => IsWaitingForBountyCollection && !_fieldCaseContract && !HasFieldBusinessToSettle,
                () => FinishCaseReport(_activeBountyReward), 120);
            starter.AddDialogLine("gwp_case_barter_open", "gwp_case_barter_open", "gwp_case_barter_result",
                GwpText.Get("{=gwp_case_barter_count}Let us check what you have brought."), null, OpenCasePayment);
            starter.AddDialogLine("gwp_case_barter_short", "gwp_case_barter_result", "gwp_case_short_options",
                GwpText.Get("{=gwp_case_short_question}This is less than the fine. Tell me why."),
                // 新口径下要对的账是"从犯人手里拿到多少 vs 交上来多少"，不是整案应缴。
                // 犯人穷、只交得出一部分，玩家如实全交，文书就没有什么好问的。
                () => CasePaymentAccepted() && CaseReportNeedsExplanation(_casePayment!.Paid, _casePayment.SelectedPrisoner != null),
                CommitCasePayment, 110);
            starter.AddDialogLine("gwp_case_barter_accepted", "gwp_case_barter_result", "gwp_case_receipt_ack",
                GwpText.Get("{=gwp_case_barter_accepted}We have counted the payment. Your report will be entered."),
                CasePaymentAccepted, () => { CommitCasePayment(); FinishSubmittedCase(false); });
            // 同理：交空手也好，回头补交也好，只要没昧下从犯人那里拿到的钱，就不该被盘问。
            // 不盘问就不会有"认不认"这道选择，也就不会有人因为随手点了那句而被记上一次谎。
            starter.AddDialogLine("gwp_case_nothing_concealed", "gwp_case_short_question", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_nothing_concealed}That is what he put in your hands, and all of it is here. Your report will be entered."),
                () => !CaseReportNeedsExplanation(Math.Max(0, _caseSubmitted), _caseSubmittedPrisoner),
                () => FinishSubmittedCase(false), 110);
            starter.AddDialogLine("gwp_case_short_question", "gwp_case_short_question", "gwp_case_short_options",
                GwpText.Get("{=gwp_case_short_question}This is less than what he paid you. Tell me why."), null, null);
            starter.AddPlayerLine("gwp_case_short_truth", "gwp_case_short_options", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_short_truth}Tell the truth"), null, () => FinishSubmittedCase(false));
            starter.AddPlayerLine("gwp_case_short_lie", "gwp_case_short_options", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_short_lie}Lie"), null, () => FinishSubmittedCase(true));
            starter.AddDialogLine("gwp_case_barter_cancelled", "gwp_case_barter_result", "gwp_case_report_options",
                GwpText.Get("{=gwp_case_barter_cancelled}Nothing has been entered. We can count it again when you are ready."),
                null, () => {
                    _casePaymentOpen = false; _casePayment = null;
                });
            starter.AddPlayerLine("gwp_case_report_acknowledge", "gwp_case_receipt_ack", "gwp_case_report_receipt",
                GwpText.Get("{=gwp_case_report_acknowledge}Let me have the account."), null, null);
            starter.AddDialogLine("gwp_case_report_receipt", "gwp_case_report_receipt", "close_window",
                "{GWP_CASE_REPORT_RESULT}", null, LeaveCaseClerk);
        }

        private bool PrepareCaseReportSummary()
        {
            if (HasNothingLeftToSettle)
            {
                MBTextManager.SetTextVariable("GWP_CASE_REPORT_SUMMARY", GwpText.Get(
                    "{=gwp_case_report_withdrawn_summary}We have word that the man is beyond reach. Say the word and I will settle your expenses."));
                return true;
            }
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
            if (treasurer == null || treasurer.IsDead || clerk == null || !HasBountyTask) return;
            try
            {
                _casePayment = new GwpAssetPayment(Hero.MainHero, treasurer, MobileParty.MainParty.Party, clerk, int.MaxValue, CaseReportSuggestedPayment,
                    reportMode: true, autoReceipt: CaseReportReceipt, prisoner: PendingCasePrisonerForDispatch);
                ShowUnknownCaseReceipt();
                _casePaymentOpen = true;
                BarterManager manager = Campaign.Current.BarterManager;
                // The native context initializer selects entries; it does NOT filter
                // the normal trading catalogue. Remove those entries before the VM
                // is created so this table contains only this commission's payment.
                BarterManager.BarterBeginEventDelegate original = manager.BarterBegin;
                try
                {
                    manager.BarterBegin = data =>
                    {
                        _casePayment.PrepareCatalogue(data);
                        original?.Invoke(data);
                    };
                    manager.StartBarterOffer(Hero.MainHero, treasurer,
                        MobileParty.MainParty.Party, clerk, null, CasePaymentContext, 0, false, _casePayment.Entries);
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
            false;

        private void CommitCasePayment()
        {
            if (!_casePaymentOpen || _casePayment?.Applied != true) return;
            int paid = _casePayment?.Applied == true ? _casePayment.Paid : 0;
            _casePaymentOpen = false;
            _caseSubmittedPrisoner = _casePayment!.SelectedPrisoner != null;
            _casePayment = null;
            _caseSubmitted = Math.Max(0, _caseSubmitted) + paid;
        }

        private bool CasePaymentAccepted() => _casePaymentOpen && _casePayment?.Applied == true;
        private void FinishSubmittedCase(bool lie)
        {
            if (_caseSubmitted < 0) return;
            int delivered = _caseSubmitted;
            if (_caseSubmittedPrisoner)
            {
                Reports?.ResolveByPrisoner(_pendingPrisonerHeroId, delivered, !lie);
                FinishCaseReport(PoliceResourceManager.PayFromJudicialTreasury(CalculateCaseFee(_pendingPrisonerAssessed)));
                return;
            }
            CompleteCashReport(delivered, lie);
            _caseSubmitted = -1;
        }

        private void CompleteCashReport(int delivered, bool falseReport)
        {
            if (!HasBountyTask || CaseHero == null || Reports == null) return;
            GwpFieldReportLedger ledger = Reports;
            if (HasNothingLeftToSettle)
            {
                CompleteWithdrawnCaseReport();
                return;
            }
            int due = CaseAmountDue;
            int received = ledger.PendingReceivedFor(CaseReportIdentity);
            // 玩家代表灰袍办这宗案子。第二层谈成什么条件、对方最后只交得出多少，都不算他的
            // 过失——办案费按整案算，不按实收封顶，也不再有"差额扣声望"这回事。
            int fee = CalculateCaseFee(due);
            ledger.DeclareAmount(delivered, truthful: !falseReport, offenderId: CaseReportIdentity);
            int paidFee = PoliceResourceManager.PayFromJudicialTreasury(fee);
            FinishCaseReport(paidFee);
            if (delivered >= received) return;
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                "{=gwp_case_report_kept_result}The {VAR_1} denars are entered. Your expenses are {VAR_2} denars. The commission is concluded.",
                "VAR_1", delivered, "VAR_2", paidFee));
        }

        /// <summary>白跑一趟的结案：只付辛苦费，不查账、不扣分、不动震慑。</summary>
        private void CompleteWithdrawnCaseReport()
        {
            int paid = PoliceResourceManager.PayFromJudicialTreasury(WithdrawnCaseCompensation());
            GwpAiDiagnostics.WriteFieldArrest("REPORT_WITHDRAWN_CASE",
                "offender=" + (_activeBountyTargetHeroId ?? "-") +
                "; casualties=" + _bountyPlayerCasualties + "; compensation=" + paid);
            FinishCaseReport(paid);
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                "{=gwp_case_withdrawn_result}The man was already beyond reach. The treasury covers {VAR_1} denars for your trouble. The commission is closed.",
                "VAR_1", paid));
        }

        private int CalculateCaseFee(int cap) => GwpCaseSettlementRules.Reward(cap,
            _bountyPlayerCasualties, Math.Max(_pendingFieldFineSeverity, _assignedCaseStanding));

        internal void ShowUnknownCaseReceipt()
        {
            if (CaseReportReceipt == null && CaseReportSuggestedPayment > 0)
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_receipt_unknown}This old account has no itemized receipt. Select the property to hand in manually.")));
        }

        /// <summary>
        /// 要押去交差的那个人。认的是 <see cref="_pendingPrisonerHeroId"/>——他被押下那一刻
        /// 记下来的名字。**不要再走 <c>CaseHero</c>**：那是"正在追捕的目标"，对方一投降，
        /// 委托就转入待交付阶段，追捕目标随之作废，人就查不出来了。
        /// </summary>
        private Hero? PendingCasePrisoner =>
            string.IsNullOrEmpty(_pendingPrisonerHeroId)
                ? null
                : Hero.FindFirst(h => h.StringId == _pendingPrisonerHeroId);

        /// <summary>
        /// 现在就能交给派出去的队伍带走的那个人；不能交时为 <c>null</c>。
        /// 押人不走分兵界面，由派遣流程直接转交，所以这里给出的是本人而不是判据。
        /// </summary>
        internal Hero? PendingCasePrisonerForDispatch =>
            CanDeliverCasePrisoner() ? PendingCasePrisoner : null;

        private bool CanDeliverCasePrisoner()
        {
            Hero? prisoner = PendingCasePrisoner;
            return _pendingPrisonerAssessed > 0 && prisoner != null && prisoner.IsPrisoner
                && prisoner.PartyBelongedToAsPrisoner == MobileParty.MainParty?.Party;
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
            ClearBountyTaskState();
            MBTextManager.SetTextVariable("GWP_CASE_REPORT_RESULT", GwpText.Get(
                "{=gwp_case_report_complete}Your report is entered. The treasury has paid {VAR_1} denars in expenses. This commission is concluded.", "VAR_1", reward));
        }

        private void LeaveCaseClerk()
        {
            if (PlayerEncounter.Current != null) PlayerEncounter.LeaveEncounter = true;
        }
    }
}
