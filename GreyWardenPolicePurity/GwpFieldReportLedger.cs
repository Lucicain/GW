using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>Field receipts, honest hand-ins, and delayed audits of concealed money.
    /// Genuine inability to pay leaves the offender's debt, not a hunter penalty.
    /// Legacy save keys are retained so in-flight receipts and audits survive updates.</summary>
    public sealed class GwpFieldReportLedger : CampaignBehaviorBase
    {
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;

        internal sealed class PendingReport
        {
            public string OffenderId = string.Empty;
            /// <summary>卷宗上的应缴数目。灰袍只认这个。</summary>
            public int Assessed;
            /// <summary>玩家实际收到手的数目。犯人的案底按这个数抵，不按玩家报的数。</summary>
            public int Collected;
            public int CashReceived;
            internal GwpCaseReceipt? Receipt;
            /// <summary>应缴里属于"做下的事"的那一段；余下的才抵负声望。</summary>
            public int BaseCharge;
            /// <summary>Collection-time evidence for legitimate inability to pay.</summary>
            public bool OffenderWasBroke;
        }

        private readonly Dictionary<string, double> _betrayalHours = new Dictionary<string, double>();
        internal void RecordBetrayal(string id)
        {
            if (PendingReceivedFor(id) > 0 && !_betrayalHours.ContainsKey(id)) _betrayalHours[id] = CampaignTime.Now.ToHours;
        }

        private readonly List<PendingReport> _pending = new List<PendingReport>();

        /// <summary>Outstanding audit gaps keyed by offender; legacy save identifiers are retained.</summary>
        private readonly Dictionary<string, int> _pendingAuditGaps =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _legacyTruthfulClaims =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static GwpFieldReportLedger? Instance =>
            Campaign.Current?.GetCampaignBehavior<GwpFieldReportLedger>();

        private readonly Dictionary<string, double> _auditDueHours = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>已经缴过罚金或兑现过处置，之后又被玩家打垮的人：每人记下当时已缴的数目。</summary>
        private readonly Dictionary<string, int> _excessEnforcement =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _excessDueHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>最近若干次委托的汇报是否属实，新的加在末尾。用来决定下次被查的概率。</summary>
        private readonly List<int> _recentReportHonesty = new List<int>();

        public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, AuditDueReports);


        public override void SyncData(IDataStore dataStore) =>
            GwpRuntimeFaultWatch.Guard("FIELD_REPORT_SYNC", () => SyncLedgerData(dataStore));

        private void SyncLedgerData(IDataStore dataStore)
        {
            List<string>? betrayalIds = dataStore.IsSaving ? _betrayalHours.Keys.ToList() : null;
            List<double>? betrayalTimes = dataStore.IsSaving ? _betrayalHours.Values.ToList() : null;
            dataStore.SyncData("gwp_betrayal_ids", ref betrayalIds);
            dataStore.SyncData("gwp_betrayal_hours", ref betrayalTimes);
            if (dataStore.IsLoading)
            {
                _betrayalHours.Clear();
                if (betrayalIds != null && betrayalTimes != null)
                    for (int i=0; i<Math.Min(betrayalIds.Count,betrayalTimes.Count); i++)
                        _betrayalHours[betrayalIds[i]] = betrayalTimes[i];
            }
            List<string>? ids = null;
            List<int>? assessed = null;
            List<int>? collected = null;
            List<int>? received = null;
            List<string>? receipts = null;
            List<double>? auditHours = null;
            List<int>? baseCharges = null;
            List<int>? broke = null;
            List<string>? povertyIds = null;
            List<int>? povertyGaps = null;
            List<string>? povertyTrue = null;
            List<string>? excessIds = null;
            List<int>? excessAmounts = null;
            List<double>? excessDue = null;

            if (dataStore.IsSaving)
            {
                ids = _pending.Select(entry => entry.OffenderId).ToList();
                assessed = _pending.Select(entry => entry.Assessed).ToList();
                collected = _pending.Select(entry => entry.Collected).ToList();
                received = _pending.Select(entry => entry.CashReceived).ToList();
                receipts = _pending.Select(entry => entry.Receipt?.Encode() ?? "").ToList();
                baseCharges = _pending.Select(entry => entry.BaseCharge).ToList();
                broke = _pending.Select(entry => entry.OffenderWasBroke ? 1 : 0).ToList();
                povertyIds = _pendingAuditGaps.Keys.ToList();
                povertyGaps = _pendingAuditGaps.Values.ToList();
                auditHours = povertyIds.Select(id => _auditDueHours.TryGetValue(id, out double due) ? due : CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays).ToList();
                povertyTrue = _legacyTruthfulClaims.ToList();
                excessIds = _excessEnforcement.Keys.ToList();
                excessAmounts = excessIds.Select(id => _excessEnforcement[id]).ToList();
                excessDue = excessIds.Select(id => _excessDueHours.TryGetValue(id, out double due)
                    ? due
                    : CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays).ToList();
            }

            dataStore.SyncData("gwp_report_ids", ref ids);
            dataStore.SyncData("gwp_report_assessed", ref assessed);
            dataStore.SyncData("gwp_report_collected", ref collected);
            dataStore.SyncData("gwp_report_received", ref received);
            dataStore.SyncData("gwp_report_asset_receipts", ref receipts);
            dataStore.SyncData("gwp_report_audit_due", ref auditHours);
            dataStore.SyncData("gwp_report_basecharge", ref baseCharges);
            dataStore.SyncData("gwp_report_broke", ref broke);
            dataStore.SyncData("gwp_report_poverty_ids", ref povertyIds);
            dataStore.SyncData("gwp_report_poverty_gaps", ref povertyGaps);
            dataStore.SyncData("gwp_report_poverty_true", ref povertyTrue);
            dataStore.SyncData("gwp_report_excess_ids", ref excessIds);
            dataStore.SyncData("gwp_report_excess_amounts", ref excessAmounts);
            dataStore.SyncData("gwp_report_excess_due", ref excessDue);
            List<int>? honesty = dataStore.IsSaving ? _recentReportHonesty.ToList() : null;
            dataStore.SyncData("gwp_report_recent_honesty", ref honesty);
            if (dataStore.IsLoading)
            {
                _recentReportHonesty.Clear();
                if (honesty != null) _recentReportHonesty.AddRange(honesty);
            }

            if (!dataStore.IsLoading) return;

            _pending.Clear();
            if (ids != null && assessed != null && collected != null && broke != null)
            {
                int count = new[] { ids.Count, assessed.Count, collected.Count, broke.Count }.Min();
                for (int i = 0; i < count; i++)
                    _pending.Add(new PendingReport
                    {
                        OffenderId = ids[i] ?? string.Empty,
                        Assessed = assessed[i],
                        Collected = collected[i],
                        CashReceived = received != null && i < received.Count ? received[i] : collected[i],
                        Receipt = receipts != null && i < receipts.Count ? GwpCaseReceipt.Decode(receipts[i]) : null,
                        BaseCharge = baseCharges != null && i < baseCharges.Count ? baseCharges[i] : 0,
                        OffenderWasBroke = broke[i] != 0
                    });
            }

            _pendingAuditGaps.Clear();
            _auditDueHours.Clear();
            if (povertyIds != null && povertyGaps != null)
                for (int i = 0; i < Math.Min(povertyIds.Count, povertyGaps.Count); i++)
                    if (!string.IsNullOrWhiteSpace(povertyIds[i]))
                    {
                        _pendingAuditGaps[povertyIds[i]] = povertyGaps[i];
                        _auditDueHours[povertyIds[i]] = auditHours != null && i < auditHours.Count
                            ? auditHours[i] : CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays;
                    }

            _excessEnforcement.Clear();
            _excessDueHours.Clear();
            if (excessIds != null && excessAmounts != null)
                for (int i = 0; i < Math.Min(excessIds.Count, excessAmounts.Count); i++)
                    if (!string.IsNullOrWhiteSpace(excessIds[i]) && excessAmounts[i] > 0)
                    {
                        _excessEnforcement[excessIds[i]] = excessAmounts[i];
                        _excessDueHours[excessIds[i]] = excessDue != null && i < excessDue.Count
                            ? excessDue[i]
                            : CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays;
                    }

            _legacyTruthfulClaims.Clear();
            if (povertyTrue != null)
                foreach (string id in povertyTrue.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    _pendingAuditGaps.Remove(id);
                    _auditDueHours.Remove(id);
                }
        }

        #region 记账

        internal void RecordSettlement(
            Hero? offender, int assessed, int collected, int baseCharge, bool offenderWasBroke, int? receivedCash = null, GwpCaseReceipt? receipt = null)
        {
            if (offender == null || assessed <= 0) return;

            _pending.Add(new PendingReport
            {
                OffenderId = offender.StringId ?? string.Empty,
                Assessed = assessed,
                Collected = collected,
                CashReceived = Math.Max(0, receivedCash ?? collected),
                Receipt = receipt,
                BaseCharge = baseCharge,
                OffenderWasBroke = offenderWasBroke
            });

            // 犯人自己的账在这一刻就清，按他**实际交了多少**算。玩家之后交不交差、
            // 交多少，是玩家与灰袍之间的事，不该再回头影响犯人已经受过的惩戒。
            ClearOffenderRecord(offender, collected, baseCharge, assessed);

            GwpAiDiagnostics.WriteFieldArrest(
                "REPORT_PENDING",
                "offender=" + (offender.StringId ?? "-") +
                "; assessed=" + assessed + "; collected=" + collected + "; broke=" + offenderWasBroke);
        }

        internal void ResolveByPrisoner(string offenderId, int deliveredCash = 0, bool truthful = true)
        {
            RememberReportHonesty(truthful);
            int received = _pending.Where(r => r.OffenderId == offenderId).Sum(r => r.CashReceived);
            QueueAudit(offenderId, Math.Max(0, received - deliveredCash));
            _pending.RemoveAll(report => report.OffenderId == offenderId);
            _betrayalHours.Remove(offenderId);
        }

        internal bool HasPendingReports => _pending.Count > 0;
        internal GwpCaseReceipt? ReceiptFor(string id)
        {
            var reports = _pending.Where(e => e.OffenderId == id).ToList();
            if (reports.Any(e => e.Receipt == null && e.CashReceived > 0)) return null;
            var result = new GwpCaseReceipt();
            foreach (var report in reports) if (report.Receipt != null) result.Add(report.Receipt);
            return result;
        }
        internal bool NeedsExplanation(string id, int delivered, bool prisoner) =>
            delivered < PendingReceivedFor(id) || (!prisoner && delivered < PendingAssessedFor(id)) || _excessEnforcement.ContainsKey(id);

        internal int PendingAssessedFor(string id) => _pending.Where(e => e.OffenderId == id).Sum(e => e.Assessed);
        internal int PendingReceivedFor(string id) => _pending.Where(e => e.OffenderId == id).Sum(e => e.CashReceived);
        internal int PendingCollectedFor(string id) => _pending.Where(e => e.OffenderId == id).Sum(e => e.Collected);
        internal int TotalAssessed => _pending.Sum(entry => entry.Assessed);
        internal int TotalCollected => _pending.Sum(entry => entry.Collected);
        internal int TotalReceived => _pending.Sum(entry => entry.CashReceived);
        internal int LegitimatePovertyShortfall => _pending.Where(r => r.OffenderWasBroke).Sum(r => Math.Max(0, r.Assessed - r.CashReceived));
        internal int PendingAuditGap => _pendingAuditGaps.Values.Sum();

        #endregion

        #region 交代

        /// <summary>
        /// 玩家来交差。他代表的就是灰袍，所以第二层谈成什么条件、对方最后只交得出多少，
        /// 都不构成他的过失——**只要他把从犯人手里拿到的钱如实上缴，就算办妥**。
        /// 索贿同理：钱只要进了公库，就不追究。
        ///
        /// 唯一会出事的是私留：手里收了多少、交上来多少，差额就是他昧下的钱。这笔账不当场
        /// 结算，而是排进查账；查不查得出来，看他近来谎报的次数和自己的灰袍声望。
        /// </summary>
        /// <param name="declared">实际交到灰袍手上的数目。</param>
        /// <param name="truthful">他嘴上说的是不是实话。只影响今后被查的概率，不改本次判定。</param>
        internal int DeclareAmount(int declared, bool truthful = true, string? offenderId = null)
        {
            var reports = _pending.Where(r => offenderId == null || r.OffenderId == offenderId).ToList();
            int received = reports.Sum(r => r.CashReceived);
            int handedOver = Math.Max(0, declared);
            int concealed = Math.Max(0, received - handedOver);

            RememberReportHonesty(truthful);
            if (concealed > 0 && reports.Count > 0)
                QueueAudit(reports[0].OffenderId, concealed);

            GwpAiDiagnostics.WriteFieldArrest(
                "REPORT_DECLARED",
                "assessed=" + TotalAssessed + "; receivedFromOffender=" + received +
                "; handedOver=" + handedOver + "; concealed=" + concealed +
                "; truthful=" + truthful + "; recentLies=" + RecentLieCount);

            foreach (var report in reports) { _betrayalHours.Remove(report.OffenderId); _pending.Remove(report); }
            return 0;
        }

        /// <summary>
        /// 保留给旧调用点：谎报就是"交上来的比收到的少，而且嘴上不认"。判定与
        /// <see cref="DeclareAmount"/> 同一条，只是记一次谎。
        /// </summary>
        internal void DeclareFalseAmount(int declared) =>
            DeclareAmount(declared, truthful: false);

        /// <summary>近十次委托里谎报过几次。</summary>
        internal int RecentLieCount => _recentReportHonesty.Count(entry => entry == 0);

        private void RememberReportHonesty(bool honest)
        {
            _recentReportHonesty.Add(honest ? 1 : 0);
            while (_recentReportHonesty.Count > GwpTuning.FieldArrest.RecentReportMemory)
                _recentReportHonesty.RemoveAt(0);
        }

        /// <summary>
        /// 被查出来的概率：底数之上，近来谎报得越多越容易被查，灰袍声望越高越不容易被查。
        /// 声望为负时反而更容易被盯上。
        /// </summary>
        internal float CurrentAuditChance()
        {
            int repeats = Math.Max(0, RecentLieCount - 1);
            float chance = Math.Min(GwpTuning.FieldArrest.AuditChanceCeiling,
                GwpTuning.FieldArrest.ReportAuditChance
                + GwpTuning.FieldArrest.AuditChancePerRepeatSquared * repeats * repeats);
            int standing = PlayerState.Reputation;
            float trust = Math.Min(GwpTuning.FieldArrest.AuditMaximumTrustReduction,
                Math.Max(0f, (float)standing - GwpTuning.FieldArrest.AuditTrustStanding)
                * GwpTuning.FieldArrest.AuditTrustReductionPerPoint);
            chance = chance * (1f - trust)
                + Math.Max(0f, -(float)standing) * GwpTuning.FieldArrest.AuditChancePerNegativeStandingPoint;
            return Math.Max(GwpTuning.FieldArrest.AuditChanceFloor,
                Math.Min(GwpTuning.FieldArrest.AuditChanceCeiling, chance));
        }

        private void QueueAudit(string id, int gap)
        {
            if (gap <= 0) return;
            _pendingAuditGaps[id] = (_pendingAuditGaps.TryGetValue(id, out int old) ? old : 0) + gap;
            _legacyTruthfulClaims.Remove(id);
            if (!_auditDueHours.ContainsKey(id))
                _auditDueHours[id] = (_betrayalHours.TryGetValue(id, out double occurred) ? occurred : CampaignTime.Now.ToHours) + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_AUDIT_QUEUED", "offender=" + id + "; gap=" + gap + "; due=" + _auditDueHours[id]);
        }

        private void AuditDueReports()
        {
            foreach (string id in _auditDueHours.Where(p => p.Value <= CampaignTime.Now.ToHours).Select(p => p.Key).ToList())
                ResolveAudit(id, MBRandom.RandomFloat);
            foreach (string id in _excessDueHours.Where(p => p.Value <= CampaignTime.Now.ToHours).Select(p => p.Key).ToList())
                ResolveExcessAudit(id, MBRandom.RandomFloat);
        }

        /// <summary>
        /// 过度执法：这个人已经缴过罚金或兑现过谈成的处置，玩家之后还是把他的部队打垮了。
        /// 赏照领、案照结，但这件事本身要独立查一次账；查不查得出来是另一回事。
        /// </summary>
        internal void RecordExcessEnforcement(string? offenderId, int alreadySettled)
        {
            if (string.IsNullOrWhiteSpace(offenderId) || alreadySettled <= 0) return;
            int previous = _excessEnforcement.TryGetValue(offenderId!, out int old) ? old : 0;
            if (alreadySettled <= previous) return;
            _excessEnforcement[offenderId!] = alreadySettled;
            if (!_excessDueHours.ContainsKey(offenderId!))
                _excessDueHours[offenderId!] = CampaignTime.Now.ToHours
                    + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays;
            GwpAiDiagnostics.WriteFieldArrest("EXCESS_ENFORCEMENT_QUEUED",
                "offender=" + offenderId + "; alreadySettled=" + alreadySettled
                + "; due=" + _excessDueHours[offenderId!]);
        }

        internal bool HasPendingExcessEnforcement(string? offenderId) =>
            !string.IsNullOrWhiteSpace(offenderId) && _excessEnforcement.ContainsKey(offenderId!);

        internal void ResolveExcessAudit(string id, float roll)
        {
            if (!_excessEnforcement.TryGetValue(id, out int settled)) return;
            _excessEnforcement.Remove(id);
            _excessDueHours.Remove(id);
            float chance = CurrentAuditChance();
            bool discovered = roll < chance;
            int penalty = discovered
                ? Math.Max(1, GwpCaseSettlementRules.ShortfallPenalty(settled, 0))
                : 0;
            GwpAiDiagnostics.WriteFieldArrest("EXCESS_ENFORCEMENT_RESOLVED",
                "offender=" + id + "; alreadySettled=" + settled
                + "; chance=" + chance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "; discovered=" + discovered + "; standingPenalty=" + penalty);
            if (!discovered) return;
            PlayerState.ChangeReputation(-penalty);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_report_excess_found}A review found that you broke {VAR_1} after he had already answered for his crime. Grey Warden standing {VAR_2}.",
                "VAR_1", GetOffenderName(id), "VAR_2", -penalty), Colors.Red));
        }

        private static string GetOffenderName(string id)
        {
            Hero? offender = Hero.FindFirst(hero =>
                string.Equals(hero.StringId, id, StringComparison.OrdinalIgnoreCase));
            return offender?.Name?.ToString() ?? id;
        }

        internal void ResolveAudit(string id, float roll)
        {
            if (!_pendingAuditGaps.TryGetValue(id, out int gap)) return;
            _pendingAuditGaps.Remove(id);
            _auditDueHours.Remove(id);
            _legacyTruthfulClaims.Remove(id);
            float chance = CurrentAuditChance();
            bool discovered = roll < chance;
            int penalty = discovered ? GwpCaseSettlementRules.ShortfallPenalty(gap, 0) : 0;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_AUDIT_RESOLVED", "offender=" + id + "; gap=" + gap
                + "; chance=" + chance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "; roll=" + roll.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "; discovered=" + discovered + "; standingPenalty=" + penalty);
            if (!discovered) return;
            PlayerState.ChangeReputation(-penalty);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_report_audit_found}A review found {VAR_1} denars missing from your report. Grey Warden standing {VAR_2}.",
                "VAR_1", gap, "VAR_2", -penalty), Colors.Red));
        }

        /// <summary>
        /// 犯人自己的账：实缴先抵基础罚款，余款按 Enforcement.FinePerPoint 抵负声望。
        /// 结算当场就清，与玩家事后交多少无关——犯人已经当众交代过了。
        /// </summary>
        private static void ClearOffenderRecord(Hero offender, int collected, int baseCharge, int assessed)
        {
            HeroCrimeStats history = CrimePool.GetOrCreateHistory(offender);
            int before = Math.Max(0, history.NegativeStanding);
            if (before <= 0) return;

            if (assessed >= GwpTuning.FieldArrest.MaximumCaseFine && collected >= assessed)
            {
                history.NegativeStanding = 0;
                return;
            }
            int towardStanding = Math.Max(0, collected - Math.Max(0, baseCharge));
            int cleared = Math.Min(before, towardStanding / GwpTuning.Enforcement.FinePerPoint);
            if (cleared <= 0) return;
            history.NegativeStanding = before - cleared;

            GwpAiDiagnostics.WriteFieldArrest(
                "RECORD_CLEARED",
                "offender=" + (offender.StringId ?? "-") +
                "; paid=" + collected + "; baseCharge=" + baseCharge +
                "; standingBefore=" + before + "; standingCleared=" + cleared);
        }

        #endregion

    }
}
