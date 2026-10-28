using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        internal void RecordCitySearchLosses(Hero target, int killed)
        {
            if (IsCommissionTarget(target)) _bountyPlayerCasualties += System.Math.Max(0, killed);
        }

        internal void RecordCitySearchDefeat(Hero target)
        {
            if (!IsCommissionTarget(target)) return;
            NotifyCaseBattleOutcome(target, true, true);
            RegisterCaseDeterrence(null, target, false);
            EnterBountyCollectionState();
            _activeBountyDeadlineHours = -1d;
            _activeQuest?.MarkReadyForTurnIn();
        }

        /// <summary>支援约斗输了也兑现约定：结束本次执行，未缴案底不抵销，只结辛苦费。</summary>
        internal void RecordCitySearchDuelAgreement(Hero target)
        {
            if (!IsCommissionTarget(target) || !IsTrackingBountyTarget) return;
            CrimeRecord? crime = CrimePool.GetRecord(target);
            if (crime?.HasOpenCase != true) return;
            GwpFieldArrestBehavior.RegisterFieldEnforcement(target, crime);
            NotifyCasePeacefullyResolved(target, 0);
            PoliceEnforcementBehavior.CloseCase(crime, "city_support_duel_agreement");
            // 与使者约斗免罚相同，CloseCase只结束执行；不收钱、不抵负声望与未赎人命。
            _caseClosedElsewhere = true;
            _assignedCaseFine = 0;
            _pendingPrisonerAssessed = 0;
            _pendingPrisonerHeroId = string.Empty;
            EnterBountyCollectionState();
            _activeBountyDeadlineHours = -1d;
            _activeQuest?.MarkReadyForTurnIn();
            string message = GwpText.Get(GwpTextCatalog.gwp_cs_support_duel_agreement, "TARGET", target.Name);
            _activeQuest?.WriteLog(message);
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(message));
        }

        internal void FailCitySearchCommission(Hero target)
        {
            if (!IsCommissionTarget(target) || !IsTrackingBountyTarget || HasCompletedEnforcement
                || !target.IsAlive || target.IsPrisoner || CrimePool.GetRecord(target)?.HasOpenCase != true) return;
            string message = GwpText.Get(GwpTextCatalog.gwp_cs_support_commission_failed, "TARGET", target.Name);
            _activeQuest?.FailCitySearch(message);
            EndBountyTaskState(tryRestorePeace: true);
            TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(message));
        }

        /// <summary>玩家当前委托的目标正没带兵住在这座城里时返回他，进城搜捕由此开放。</summary>
        internal Hero? CitySearchTargetIn(Settlement? settlement) =>
            settlement != null && IsTrackingBountyTarget && CaseHero is { } hero
            && CrimeRecord.ShelterOf(hero) == settlement ? hero : null;
    }
}
