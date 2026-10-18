using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        internal GwpAffairsEntry? GetAffairsSupport()
        {
            if (!_supportRequested || !HasBountyTask) return null;
            MobileParty? party = GwpAffairsData.FindParty(_escortPolicePartyId);
            var entry = new GwpAffairsEntry("support:" + _escortPolicePartyId,
                GwpText.Get(GwpTextCatalog.gwp_affairs_support), GwpText.Get(GwpTextCatalog.gwp_affairs_escort))
            { IsPersonal = true, Hero = party?.LeaderHero, Assignee = party?.LeaderHero,
                Summary = party?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown) };
            if (party?.IsActive == true)
            {
                entry.Fields.Add(GwpAffairsText.TargetLocation(party, GwpTextCatalog.gwp_affairs_current_location));
                entry.Fields.Add(party.GetBehaviorText().ToString());
            }
            return entry;
        }
        internal GwpAffairsEntry? GetAffairsCommission()
        {
            if (!HasBountyTask) return null;
            Hero? hero = CaseHero;
            CrimeRecord? crime = hero == null ? null : CrimePool.GetRecord(hero);
            var entry = new GwpAffairsEntry("commission", GwpText.Get(GwpTextCatalog.gwp_affairs_current),
                _activeBountyTargetName ?? hero?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown));
            entry.Hero = hero;
            entry.IsPersonal = true;
            entry.Summary = GwpText.Get(IsWaitingForBountyCollection ? GwpTextCatalog.gwp_affairs_collect : GwpTextCatalog.gwp_affairs_pursuing);
            entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_reward,
                GwpAffairsText.Money(CalculateCaseFee(CaseAmountDue))));
            entry.Fields.Add(GwpText.Get(GwpTextCatalog.gwp_affairs_reward_note));
            if (_activeBountyDeadlineHours > 0 && IsTrackingBountyTarget)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_deadline,
                    GwpCaseArchiveItemVM.FormatCampaignDate(CampaignTime.Hours((float)_activeBountyDeadlineHours))));
            MobileParty? target = crime?.Offender ?? hero?.PartyBelongedTo;
            if (target?.IsActive == true) entry.Fields.Add(GwpAffairsText.TargetLocation(target));
            if (crime != null && target?.IsActive == true)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_strength,
                    GwpText.Format(GwpStrength.CaseTarget(crime), "0")));
            if (HasEscortPoliceParty)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_escort,
                    GwpAffairsData.FindParty(_escortPolicePartyId)?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
            return entry;
        }
    }
}
