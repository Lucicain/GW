using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchBehavior
    {
        private string _searchSourcePartyId = string.Empty;
        private bool _searchAttemptStarted;
        private static PoliceEnforcementBehavior? Enforcement => Campaign.Current?.GetCampaignBehavior<PoliceEnforcementBehavior>();
        internal bool Supported => !string.IsNullOrEmpty(_searchSourcePartyId);
        internal bool UsesSupportParty(string partyId) => Supported && _searchSourcePartyId == partyId;
        internal PartyBase? SupportSource => Supported
            ? MobileParty.All.FirstOrDefault(party => party.IsActive && party.StringId == _searchSourcePartyId)?.Party
            : null;

        private bool CanBeginSearch(Hero target) => GwpCitySearchCasePolicy.CanBegin(CrimePool.GetRecord(target),
            Enforcement?.CitySearchSupport(target) != null);

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

        private string? SearchSupportStatus(Hero target)
        {
            CrimeRecord? crime = CrimePool.GetRecord(target);
            if (GwpCitySearchCasePolicy.SupportSpent(crime)) return GwpTextCatalog.gwp_cs_support_spent;
            if (Enforcement?.CitySearchSupport(target) != null) return GwpTextCatalog.gwp_cs_support_ready;
            if (Enforcement?.CitySearchSupport(target, false) != null) return GwpTextCatalog.gwp_cs_support_travelling;
            if (!GwpCitySearchCasePolicy.CanBegin(crime, false)) return GwpTextCatalog.gwp_cs_support_personal_spent;
            return null;
        }
    }
}
