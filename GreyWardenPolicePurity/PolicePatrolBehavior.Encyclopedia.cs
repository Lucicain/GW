using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public partial class PolicePatrolBehavior
    {
        internal bool HasActivePatrolWarForFaction(IFaction? targetFaction)
        {
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (targetFaction == null || playerFaction == null) return false;
            if (!string.Equals(targetFaction.StringId, playerFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return false;

            int activePatrolCount = MobileParty.All.Count(p =>
                p != null &&
                p.IsActive &&
                IsPatrol(p) &&
                p.CurrentSettlement == null);

            return _warDeclared || activePatrolCount > 0 || _playerCapturedByPatrol;
        }

        internal string? BuildPatrolWarReasonDetails(IFaction? targetFaction)
        {
            if (!HasActivePatrolWarForFaction(targetFaction))
                return null;

            int activePatrolCount = MobileParty.All.Count(p =>
                p != null &&
                p.IsActive &&
                IsPatrol(p) &&
                p.CurrentSettlement == null);

            string stage;
            if (_playerCapturedByPatrol)
                stage = GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_encyclopedia_001);
            else if (activePatrolCount > 0)
                stage = GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_encyclopedia_002, "VAR_1", activePatrolCount);
            else
                stage = GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_encyclopedia_003);

            return GwpText.Get(GwpTextCatalog.gwp_policepatrolbehavior_encyclopedia_004, "VAR_1", stage, "VAR_2", PlayerState.Reputation);
        }
    }
}
