using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 案件生命周期的唯一入口。一宗案子的承办任务怎么结束、案卷怎么销，都只走这里。
    ///
    /// 以前每个结束承办的地方各自按顺序做一串善后（恢复 AI、清意图、清宣战记录、结束任务、
    /// 恢复和平、协力组、记功），每处的组合都略有不同；外部结案（玩家当面了结、被抓）
    /// 直接删任务，善后全靠别的扫除兜底。改一处忘一处，「反复接手」「被别人了结」这类问题
    /// 都出在这里（见 2026-10-07 journal）。现在三种结局各有一套固定善后，调用方只说结局和原因。
    /// </summary>
    public partial class PoliceEnforcementBehavior
    {
        internal enum CaseTaskEnd
        {
            /// <summary>灰袍办成了：结案、记办案经费、通知差事完成，协力组按办成收尾。</summary>
            Solved,
            /// <summary>结案但不算灰袍办成：承办人战败或失效、案子被别人了结。不记功，协力组解散。</summary>
            Closed,
            /// <summary>撤下承办人，案卷留在台账上等别人接；只有犯人已死才销卷。</summary>
            Released,
        }

        /// <summary>结束一支灰袍的承办任务，按结局做全套善后。顺序有讲究，见各步注释。</summary>
        private void EndCaseTask(string policePartyId, CaseTaskEnd end, string reason)
        {
            PoliceTask? task = CrimeState.GetTask(policePartyId);
            if (task == null) return;
            MobileParty? police = FindActiveParty(policePartyId);

            // 协力军团要在组还认得每个协办人的时候解散，所以排在结束任务之前。
            if (end == CaseTaskEnd.Solved)
                CompleteAssistanceTasks(policePartyId);
            else
                ReleaseAssistanceGroup(policePartyId, reason);

            if (police != null)
            {
                GwpCommon.TryResetAi(police);
                GreyWardenPartyDesireBehavior.ClearIntent(police);
            }
            ClearTaskWarTracking(policePartyId, true);

            if (end == CaseTaskEnd.Released)
                CrimeState.ReleaseTask(policePartyId, reason);
            else
                CrimeState.EndTask(policePartyId, reason);

            if (end == CaseTaskEnd.Solved)
            {
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
                GwpPlayerRequestDeferral.NotifyDutyCompleted(police, "criminal_case");
            }

            // 必须在台账变动之后：HasLegitimateWarReason 要先看到这宗案子已经不在了，
            // 才不会把它当成继续打仗的理由。
            RestorePeaceAfterCaseEnd(task);
            CrimeState.RefreshAccepting();
            if (police != null)
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
        }

        /// <summary>
        /// 销掉一宗案卷：谁了结的都走这里——玩家当面了结、玩家抓了人、委托交差、被捕清账。
        /// 正在承办它的灰袍先按「结案」收尾，再销卷。
        /// </summary>
        internal static void CloseCase(CrimeRecord? crime, string reason)
        {
            if (crime == null) return;
            if (_instance != null)
            {
                foreach (string ownerId in CrimeState.ActiveTasks
                             .Where(kv => string.Equals(kv.Value.TargetCrimeId, crime.CrimeId,
                                 StringComparison.OrdinalIgnoreCase))
                             .Select(kv => kv.Key).ToList())
                    _instance.EndCaseTask(ownerId, CaseTaskEnd.Closed, reason);
            }
            CrimeState.CloseCase(crime, reason);
        }

        /// <summary>
        /// 玩家接手了这宗案子（他自己去办、或已经办完还没交差）。灰袍撤下承办，案卷不销——
        /// 玩家可能还要向犯人追缴罚金、还要回来汇报。
        /// </summary>
        internal static void ReleaseWardensFromCase(string? offenderHeroId)
        {
            if (_instance == null || string.IsNullOrWhiteSpace(offenderHeroId)) return;

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values
                         .Where(t => string.Equals(t.TargetCrime?.OffenderHeroId, offenderHeroId,
                             StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                string policePartyId = task.PolicePartyId ?? string.Empty;
                IFaction? warTarget = task.WarTarget;
                _instance.EndCaseTask(policePartyId, CaseTaskEnd.Released, "player_enforcement_completed");
                // 卷宗可能要等玩家汇报才销，这一刻的"仍有合法理由"未必是最终结论。
                if (warTarget != null)
                    _peaceRetryTargets[warTarget] = CampaignTime.Now.ToHours + 24 * 30;
                GwpAiDiagnostics.WriteFieldArrest("CASE_RELEASED_TO_PLAYER",
                    "offender=" + offenderHeroId + "; police=" + policePartyId +
                    "; warTarget=" + (warTarget?.StringId ?? "-"));
            }
        }

        /// <summary>承办部队已经消失、或案卷目标无法恢复的任务按「结案」收尾；死人的案卷移出台账。</summary>
        private void CleanCaseLedger()
        {
            foreach (string ownerId in CrimeState.StaleTaskOwnerIds())
                EndCaseTask(ownerId, CaseTaskEnd.Closed, "owner_party_or_crime_record_missing");
            CrimeState.RemoveDeadOffenderRecords();
        }
    }
}
