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
                stage = GwpText.Get("{=gwp_policepatrolbehavior_encyclopedia_001}The player is in provost custody, under escort or awaiting judgment.");
            else if (activePatrolCount > 0)
                stage = GwpText.Get("{=gwp_policepatrolbehavior_encyclopedia_002}{VAR_1} provost patrols remain upon the map under orders of arrest.", "VAR_1", activePatrolCount);
            else
                stage = GwpText.Get("{=gwp_policepatrolbehavior_encyclopedia_003}has entered the picket war state and is waiting for the end.");

            return GwpText.Get("{=gwp_policepatrolbehavior_encyclopedia_004}Provost enforcement: after the player refuses a lawful order while under warrant, the Grey Wardens declare war upon the player’s faction to compel arrest. Present stage: {VAR_1}; present standing: {VAR_2}.", "VAR_1", stage, "VAR_2", PlayerState.Reputation);
        }
    }
}
