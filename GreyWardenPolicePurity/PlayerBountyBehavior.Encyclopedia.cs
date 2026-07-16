using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    public partial class PlayerBountyBehavior
    {
        internal bool HasActiveBountyWarForFaction(IFaction? targetFaction)
        {
            if (targetFaction == null) return false;
            if (!HasBountyTask) return false;
            if (string.IsNullOrEmpty(_activeBountyTargetFactionId)) return false;

            return string.Equals(
                _activeBountyTargetFactionId,
                targetFaction.StringId,
                StringComparison.OrdinalIgnoreCase);
        }

        internal string? BuildActiveBountyWarReasonDetails(IFaction? targetFaction)
        {
            if (!HasActiveBountyWarForFaction(targetFaction))
                return null;

            string escortPartyName = GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_001}Unspecified escort unit");
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                MobileParty? escortParty = MobileParty.All.FirstOrDefault(p =>
                    p != null &&
                    p.IsActive &&
                    string.Equals(p.StringId, _escortPolicePartyId, StringComparison.OrdinalIgnoreCase));
                if (escortParty != null)
                    escortPartyName = escortParty.Name?.ToString() ?? escortPartyName;
            }

            string targetName = string.IsNullOrWhiteSpace(_activeBountyTargetName) ? GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_002}Unknown target") : _activeBountyTargetName;
            string stage = IsTrackingBountyTarget
                ? GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_003}The target is still escaping, and the Grey Wardens troops are escorting the player to pursue")
                : IsWaitingForBountyCollection
                    ? GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_004}The target has been defeated, waiting for the player to receive the bounty and the Grey Wardens will deal with the aftermath")
                    : GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_005}The bounty process is still in progress");

            return GwpText.Get("{=gwp_playerbountybehavior_encyclopedia_006}Player bounty collaboration: The current target is {VAR_1}, and the hostile forces are {VAR_2}. Escort force: {VAR_3}; current stage: {VAR_4}.", "VAR_1", targetName, "VAR_2", targetFaction.Name, "VAR_3", escortPartyName, "VAR_4", stage);
        }
    }
}
