using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        internal static IEnumerable<string> AffairsVillageParties(Settlement village)
        {
            if (_instance == null) yield break;
            foreach (var record in _instance._faithParties.Values.Where(r => r.HomeId == village.StringId && r.Phase != FaithPartyPhase.Home))
            {
                var party = GwpAffairsData.FindParty(record.PartyId);
                if (party == null) continue;
                yield return GwpAffairsText.Pair(party.Name.ToString(),
                    GwpAffairsText.Pair(GwpText.Get(record.Phase == FaithPartyPhase.Returning
                        ? GwpTextCatalog.gwp_affairs_returning : GwpTextCatalog.gwp_affairs_outbound),
                        GwpAffairsData.FindParty(record.TargetId)?.Name?.ToString()
                        ?? Settlement.Find(record.TargetId)?.Name?.ToString()
                        ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
            }
        }
    }
}
