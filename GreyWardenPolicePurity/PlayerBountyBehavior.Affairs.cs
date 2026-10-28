using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        internal GwpAffairsEntry? GetAffairsSupport()
        {
            if (!_supportRequested || !HasBountyTask) return null;
            Hero? hero = CaseHero;
            PoliceEnforcementBehavior? enforcement = Campaign.Current.GetCampaignBehavior<PoliceEnforcementBehavior>();
            // 城内求援不记随行领主编号（GrantCitySearchSupport），支援是派队那边登记的办案队：
            // 在路上或失队待补派时同样是正在赶来；搜捕结束、登记转为返程后显示余部返回。
            if (string.IsNullOrEmpty(_escortPolicePartyId))
            {
                MobileParty? team = null;
                bool assigned = hero != null && enforcement?.TryGetPlayerCitySupport(hero, out team) == true;
                var city = new GwpAffairsEntry("support:city", GwpText.Get(GwpTextCatalog.gwp_affairs_support),
                    GwpText.Get(GwpTextCatalog.gwp_cs_support_party_name))
                { IsPersonal = true, Summary = GwpText.Get(!assigned ? GwpTextCatalog.gwp_cs_support_spent
                    : enforcement!.CitySearchSupport(hero!) != null ? GwpTextCatalog.gwp_cs_support_ready
                    : GwpTextCatalog.gwp_cs_support_travelling) };
                if (team?.IsActive == true)
                {
                    city.Fields.Add(GwpAffairsText.TargetLocation(team, GwpTextCatalog.gwp_affairs_current_location));
                    city.Fields.Add(team.GetBehaviorText().ToString());
                }
                return city;
            }
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
                GwpAffairsText.Money(ExpectedCaseFee)));
            if (_activeBountyDeadlineHours > 0 && IsTrackingBountyTarget)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_deadline,
                    GwpCaseArchiveItemVM.FormatCampaignDate(CampaignTime.Hours((float)_activeBountyDeadlineHours))));
            MobileParty? target = crime?.Offender ?? hero?.PartyBelongedTo;
            PartyBase? holder = hero?.IsPrisoner == true ? hero.PartyBelongedToAsPrisoner : null;
            if (holder != null && IsTrackingBountyTarget)
            {
                entry.Summary = GwpText.Get(GwpTextCatalog.gwp_case_custody_held, "HOLDER", holder.Name);
                entry.Fields.Add(GwpText.Get(GwpTextCatalog.gwp_case_custody_help));
                target = holder.MobileParty;
                if (holder.Settlement != null)
                {
                    entry.Place = holder.Settlement;
                    entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_current_location, holder.Settlement.Name.ToString()));
                }
            }
            Settlement? shelter = holder == null && IsTrackingBountyTarget ? CrimeRecord.ShelterOf(hero) : null;
            if (shelter != null)
            {
                entry.Summary = GwpText.Get(GwpTextCatalog.gwp_affairs_sheltered, "PLACE", shelter.Name);
                entry.Place = shelter;
                entry.Fields.Add(GwpText.Get(GwpTextCatalog.gwp_affairs_sheltered_help));
            }
            if (holder == null && IsTrackingBountyTarget && IsCaseTargetMissing)
            {
                entry.Summary = GwpText.Get(GwpTextCatalog.gwp_case_target_missing);
                entry.Fields.Add(GwpText.Get(GwpTextCatalog.gwp_case_target_missing_help));
                target = null;
            }
            if (target?.IsActive == true)
            {
                entry.Fields.Add(GwpAffairsText.TargetLocation(target));
                entry.Place = GwpCommon.NormalizePlayerFacingSettlement(target.CurrentSettlement, target.GetPosition2D);
            }
            if (holder == null && crime != null && target?.IsActive == true)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_strength,
                    GwpText.Format(GwpStrength.CaseTarget(crime), "0")));
            if (HasEscortPoliceParty)
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_escort,
                    GwpAffairsData.FindParty(_escortPolicePartyId)?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
            return entry;
        }
    }
}
