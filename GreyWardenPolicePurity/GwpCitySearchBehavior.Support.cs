using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchBehavior
    {
        private string _searchSourcePartyId = string.Empty;
        private bool _searchAttemptStarted;
        private static PoliceEnforcementBehavior? Enforcement => Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();
        internal bool Supported => !string.IsNullOrEmpty(_searchSourcePartyId);
        internal PartyBase? TroopSource => Supported
            ? MobileParty.All.FirstOrDefault(party => party.IsActive && party.StringId == _searchSourcePartyId)?.Party
            : PartyBase.MainParty;

        private bool CanBeginSearch(Hero target)
        {
            if (Enforcement?.CitySearchDelegated(target) == true) return false;
            return GwpCitySearchCasePolicy.CanBegin(CrimePool.GetRecord(target),
                Enforcement?.CitySearchSupport(target) != null);
        }

        private bool PrepareSearchSource(Hero target)
        {
            if (!CanBeginSearch(target)) return false;
            MobileParty? support = Enforcement?.CitySearchSupport(target);
            _searchSourcePartyId = support?.StringId ?? string.Empty;
            _searchAttemptStarted = false;
            return true;
        }

        private bool StartSearchAttempt()
        {
            if (_searchAttemptStarted) return true;
            if (Supported && Enforcement?.CitySearchSupport(SearchTarget)?.StringId != _searchSourcePartyId) return false;
            if (!GwpCitySearchCasePolicy.TryBegin(CrimePool.GetRecord(SearchTarget), Supported)) return false;
            _searchAttemptStarted = true;
            return true;
        }

        private void RegisterSupportMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(SearchMenu, "support", GwpTextCatalog.gwp_cs_support_request,
                args => SupportMenuCondition(args), _ => RequestCitySupport(true));
            starter.AddGameMenuOption(SearchMenu, "delegate", GwpTextCatalog.gwp_cs_support_delegate,
                args => SupportMenuCondition(args), _ => RequestCitySupport(false));
        }

        private bool SupportMenuCondition(MenuCallbackArgs args)
        {
            Hero? target = Bounty?.CitySearchTargetIn(TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement);
            if (SearchActive || target == null || GwpCitySearchCasePolicy.SupportSpent(CrimePool.GetRecord(target))) return false;
            if (!GwpCitySearchCasePolicy.CanDispatch(CrimePool.GetRecord(target))
                && Enforcement?.CitySearchSupport(target, false) == null) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        private void RequestCitySupport(bool followPlayer)
        {
            Hero? target = Bounty?.CitySearchTargetIn(TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement);
            bool sent = target != null && Enforcement?.RequestCitySearchParty(target, followPlayer) == true;
            string key = sent ? followPlayer ? GwpTextCatalog.gwp_cs_support_sent : GwpTextCatalog.gwp_cs_support_delegated
                : GwpTextCatalog.gwp_cs_support_unavailable;
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(key)));
            GameMenu.SwitchToMenu(SearchMenu);
        }

        private string? SearchSupportStatus(Hero target)
        {
            if (Enforcement?.CitySearchDelegated(target) == true) return GwpTextCatalog.gwp_cs_support_delegated;
            CrimeRecord? crime = CrimePool.GetRecord(target);
            if (GwpCitySearchCasePolicy.SupportSpent(crime)) return GwpTextCatalog.gwp_cs_support_spent;
            if (Enforcement?.CitySearchSupport(target) != null) return GwpTextCatalog.gwp_cs_support_ready;
            if (Enforcement?.CitySearchSupport(target, false) != null) return GwpTextCatalog.gwp_cs_support_travelling;
            if (!GwpCitySearchCasePolicy.CanBegin(crime, false)) return GwpTextCatalog.gwp_cs_support_personal_spent;
            return null;
        }
    }
}
