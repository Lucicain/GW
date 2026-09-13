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
            /// <summary>应缴里属于"做下的事"的那一段；余下的才抵负声望。</summary>
            public int BaseCharge;
            /// <summary>Collection-time evidence for legitimate inability to pay.</summary>
            public bool OffenderWasBroke;
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
        public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, AuditDueReports);


        public override void SyncData(IDataStore dataStore)
        {
            List<string>? ids = null;
            List<int>? assessed = null;
            List<int>? collected = null;
            List<int>? received = null;
            List<double>? auditHours = null;
            List<int>? baseCharges = null;
            List<int>? broke = null;
            List<string>? povertyIds = null;
            List<int>? povertyGaps = null;
            List<string>? povertyTrue = null;

            if (dataStore.IsSaving)
            {
                ids = _pending.Select(entry => entry.OffenderId).ToList();
                assessed = _pending.Select(entry => entry.Assessed).ToList();
                collected = _pending.Select(entry => entry.Collected).ToList();
                received = _pending.Select(entry => entry.CashReceived).ToList();
                baseCharges = _pending.Select(entry => entry.BaseCharge).ToList();
                broke = _pending.Select(entry => entry.OffenderWasBroke ? 1 : 0).ToList();
                povertyIds = _pendingAuditGaps.Keys.ToList();
                povertyGaps = _pendingAuditGaps.Values.ToList();
                auditHours = povertyIds.Select(id => _auditDueHours.TryGetValue(id, out double due) ? due : CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays).ToList();
                povertyTrue = _legacyTruthfulClaims.ToList();
            }

            dataStore.SyncData("gwp_report_ids", ref ids);
            dataStore.SyncData("gwp_report_assessed", ref assessed);
            dataStore.SyncData("gwp_report_collected", ref collected);
            dataStore.SyncData("gwp_report_received", ref received);
            dataStore.SyncData("gwp_report_audit_due", ref auditHours);
            dataStore.SyncData("gwp_report_basecharge", ref baseCharges);
            dataStore.SyncData("gwp_report_broke", ref broke);
            dataStore.SyncData("gwp_report_poverty_ids", ref povertyIds);
            dataStore.SyncData("gwp_report_poverty_gaps", ref povertyGaps);
            dataStore.SyncData("gwp_report_poverty_true", ref povertyTrue);

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
            Hero? offender, int assessed, int collected, int baseCharge, bool offenderWasBroke, int? receivedCash = null)
        {
            if (offender == null || assessed <= 0) return;

            _pending.Add(new PendingReport
            {
                OffenderId = offender.StringId ?? string.Empty,
                Assessed = assessed,
                Collected = collected,
                CashReceived = Math.Max(0, receivedCash ?? collected),
                BaseCharge = baseCharge,
                OffenderWasBroke = offenderWasBroke
            });

            GwpAiDiagnostics.WriteFieldArrest(
                "REPORT_PENDING",
                "offender=" + (offender.StringId ?? "-") +
                "; assessed=" + assessed + "; collected=" + collected + "; broke=" + offenderWasBroke);
        }

        internal void ResolveByPrisoner(string offenderId, int deliveredCash = 0)
        {
            int received = _pending.Where(r => r.OffenderId == offenderId).Sum(r => r.CashReceived);
            QueueAudit(offenderId, Math.Max(0, received - deliveredCash));
            _pending.RemoveAll(report => report.OffenderId == offenderId);
            AuditAtHandIn();
        }

        internal bool HasPendingReports => _pending.Count > 0;

        internal int PendingAssessedFor(string id) => _pending.Where(e => e.OffenderId == id).Sum(e => e.Assessed);
        internal int PendingCollectedFor(string id) => _pending.Where(e => e.OffenderId == id).Sum(e => e.Collected);
        internal int TotalAssessed => _pending.Sum(entry => entry.Assessed);
        internal int TotalCollected => _pending.Sum(entry => entry.Collected);
        internal int TotalReceived => _pending.Sum(entry => entry.CashReceived);
        internal int LegitimatePovertyShortfall => _pending.Where(r => r.OffenderWasBroke).Sum(r => Math.Max(0, r.Assessed - r.CashReceived));
        internal int PendingAuditGap => _pendingAuditGaps.Values.Sum();

        #endregion

        #region 交代

        /// <summary>
        /// 玩家报了一个数目，灰袍照单全收，并按差额扣声望。差额每 300 扣一点，
        /// 向下取整——和灰袍给罪犯定价用的是同一把尺子。
        /// </summary>
        internal int DeclareAmount(int declared, bool truthful = false)
        {
            int assessed = TotalAssessed;
            int shortfall = Math.Max(0, assessed - Math.Max(0, declared));
            int legitimateGap = truthful ? LegitimatePovertyShortfall : 0;
            int penalty = GwpCaseSettlementRules.ShortfallPenalty(Math.Max(0, assessed - legitimateGap), declared);

            if (penalty > 0)
            {
                PlayerState.ChangeReputation(-penalty);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_report_short}The ledger says {VAR_1}; you handed in {VAR_2}. Grey Warden standing {VAR_3}.",
                        "VAR_1", assessed.ToString(), "VAR_2", declared.ToString(), "VAR_3", (-penalty).ToString()),
                    Colors.Red));
            }

            GwpAiDiagnostics.WriteFieldArrest(
                "REPORT_DECLARED",
                "assessed=" + assessed + "; declared=" + declared +
                "; shortfall=" + shortfall + "; legitimateGap=" + legitimateGap + "; standingPenalty=" + penalty);

            int remaining = Math.Max(0, declared);
            foreach (var report in _pending)
            {
                int credited = Math.Min(report.Assessed, remaining);
                remaining -= credited;
                report.Collected = Math.Max(report.Collected, credited);
            }
            ClearOffenderRecords();
            _pending.Clear();
            AuditAtHandIn();
            return penalty;
        }

        // A false explanation is audited independently of later NPC arrests.
        internal void DeclareFalseAmount(int declared)
        {
            int remaining = Math.Max(0, declared);
            foreach (var report in _pending)
            {
                int credited = Math.Min(report.Assessed, remaining);
                remaining -= credited;
                int legitimateGap = report.OffenderWasBroke ? Math.Max(0, report.Assessed - report.CashReceived) : 0;
                QueueAudit(report.OffenderId, Math.Max(0, report.Assessed - credited - legitimateGap));
                report.Collected = Math.Max(report.Collected, credited);
            }
            ClearOffenderRecords();
            _pending.Clear();
            AuditAtHandIn();
        }

        private void QueueAudit(string id, int gap)
        {
            if (gap <= 0) return;
            _pendingAuditGaps[id] = (_pendingAuditGaps.TryGetValue(id, out int old) ? old : 0) + gap;
            _legacyTruthfulClaims.Remove(id);
            if (!_auditDueHours.ContainsKey(id))
                _auditDueHours[id] = CampaignTime.Now.ToHours + 24 * GwpTuning.FieldArrest.ReportAuditDelayDays;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_AUDIT_QUEUED", "offender=" + id + "; gap=" + gap + "; due=" + _auditDueHours[id]);
        }

        private void AuditAtHandIn()
        {
            if (!GwpTuning.FieldArrest.ImmediateAuditTesting) return;
            foreach (string id in _pendingAuditGaps.Keys.ToList()) ResolveAudit(id, 0f);
        }

        private void AuditDueReports()
        {
            foreach (string id in _auditDueHours.Where(p => p.Value <= CampaignTime.Now.ToHours).Select(p => p.Key).ToList())
                ResolveAudit(id, MBRandom.RandomFloat);
        }

        internal void ResolveAudit(string id, float roll)
        {
            if (!_pendingAuditGaps.TryGetValue(id, out int gap)) return;
            _pendingAuditGaps.Remove(id);
            _auditDueHours.Remove(id);
            _legacyTruthfulClaims.Remove(id);
            bool discovered = roll < GwpTuning.FieldArrest.ReportAuditChance;
            int penalty = discovered ? GwpCaseSettlementRules.ShortfallPenalty(gap, 0) : 0;
            GwpAiDiagnostics.WriteFieldArrest("REPORT_AUDIT_RESOLVED", "offender=" + id + "; gap=" + gap
                + "; discovered=" + discovered + "; standingPenalty=" + penalty);
            if (!discovered) return;
            PlayerState.ChangeReputation(-penalty);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_report_audit_found}A review found {VAR_1} denars missing from your report. Grey Warden standing {VAR_2}.",
                "VAR_1", gap, "VAR_2", -penalty), Colors.Red));
        }

        /// <summary>
        /// 上交之后才轮到清犯人的账，而且**严格按他自己交了多少**算——玩家私吞
        /// 也好、少报也好，那是玩家和灰袍之间的账，不该让犯人替他背，也不该让
        /// 犯人白白得利。缴款先抵"做下的事"那一段，余下的每 300 抵一点负声望。
        /// </summary>
        private void ClearOffenderRecords()
        {
            foreach (PendingReport report in _pending)
            {
                Hero? offender = Hero.FindFirst(hero =>
                    string.Equals(hero.StringId, report.OffenderId, StringComparison.OrdinalIgnoreCase));
                if (offender == null) continue;

                HeroCrimeStats history = CrimePool.GetOrCreateHistory(offender);
                int before = Math.Max(0, history.NegativeStanding);
                if (before <= 0) continue;

                int towardStanding = Math.Max(0, report.Collected - report.BaseCharge);
                int cleared = Math.Min(before, towardStanding / GwpTuning.Enforcement.FinePerPoint);
                history.NegativeStanding = before - cleared;

                GwpAiDiagnostics.WriteFieldArrest(
                    "RECORD_CLEARED",
                    "offender=" + report.OffenderId +
                    "; paid=" + report.Collected + "; baseCharge=" + report.BaseCharge +
                    "; standingBefore=" + before + "; standingCleared=" + cleared);
            }
        }

        #endregion

    }
}
