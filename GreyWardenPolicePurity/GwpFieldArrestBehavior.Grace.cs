using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFieldArrestBehavior
    {
        private string _graceHeroId = string.Empty;
        private double _graceDueHours;
        private bool _graceNoticeShown;
        private bool HasGrace => _offender != null && _offender.StringId == _graceHeroId
            && IsAssignedCase(_offender);
        private bool CanOfferGrace => !HasGrace
            && GwpPlayerCaseAssignment.CanOfferGrace(_offender);

        internal void ClearFieldGrace()
        {
            _graceHeroId = string.Empty;
            _graceDueHours = 0;
            _graceNoticeShown = false;
        }

        private void GrantGrace()
        {
            if (!CanOfferGrace || _offender == null) return;
            _graceHeroId = _offender.StringId;
            _graceDueHours = CampaignTime.Now.ToHours + GwpTuning.FieldArrest.GraceHours;
            _graceNoticeShown = false;
            string message = GwpText.Get(GwpTextCatalog.gwp_grace_granted,
                "VAR_1", _offender.Name.ToString());
            GwpPlayerCaseAssignment.RecordFieldGrace(_offender, message);
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
            GwpAiDiagnostics.WriteFieldArrest("GRACE_GRANTED", "hero=" + _graceHeroId + "; due=" + _graceDueHours);
            EndPersuasionConsequence();
            LeaveEncounterPeacefully();
            ClearState();
        }

        private void CheckGraceDeadline()
        {
            if (string.IsNullOrEmpty(_graceHeroId) || _graceNoticeShown || CampaignTime.Now.ToHours < _graceDueHours) return;
            Hero? target = Hero.FindFirst(h => h.StringId == _graceHeroId);
            if (target == null || !GwpPlayerCaseAssignment.IsAssignedCase(target)) { ClearFieldGrace(); return; }
            _graceNoticeShown = true;
            string message = GwpText.Get(GwpTextCatalog.gwp_grace_due, "VAR_1", target.Name.ToString());
            GwpPlayerCaseAssignment.RecordFieldGrace(target, message);
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
        }

        /// <summary>说好的日子到了没有。没到就别开口要钱。</summary>
        private bool GraceExpired => HasGrace && CampaignTime.Now.ToHours >= _graceDueHours;

        /// <summary>
        /// 宽限还没到期时，玩家手里只该有两条路：现在就强办，或者等日子到了再说。
        /// "现在先收一点"这种中间选项不给——那是我们自己答应过的期限。
        /// </summary>
        internal bool GraceStillRunning => HasGrace && !GraceExpired;

        private bool GraceCollectionAvailable() => GraceExpired;
        private void PrepareGraceCollection()
        {
            _enforcementAccepted = true;
            _paymentAccepted = true;
            _desire = GwpOffenderDesire.PayInFull;
        }
    }
}
