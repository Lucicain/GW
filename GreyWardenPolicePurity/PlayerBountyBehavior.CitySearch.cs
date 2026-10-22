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

        /// <summary>玩家当前委托的目标正没带兵住在这座城里时返回他，进城搜捕由此开放。</summary>
        internal Hero? CitySearchTargetIn(Settlement? settlement) =>
            settlement != null && IsTrackingBountyTarget && CaseHero is { } hero
            && CrimeRecord.ShelterOf(hero) == settlement ? hero : null;
    }
}
