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

            string escortPartyName = "未指定护送部队";
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                MobileParty? escortParty = MobileParty.All.FirstOrDefault(p =>
                    p != null &&
                    p.IsActive &&
                    string.Equals(p.StringId, _escortPolicePartyId, StringComparison.OrdinalIgnoreCase));
                if (escortParty != null)
                    escortPartyName = escortParty.Name?.ToString() ?? escortPartyName;
            }

            string targetName = string.IsNullOrWhiteSpace(_activeBountyTargetName) ? "未知目标" : _activeBountyTargetName;
            string stage = IsTrackingBountyTarget
                ? "目标仍在逃逸，灰袍部队正在护送玩家追捕"
                : IsWaitingForBountyCollection
                    ? "目标已经被击败，等待玩家领取赏金并由灰袍善后"
                    : "悬赏流程仍在进行";

            return $"玩家悬赏协同：当前目标是 {targetName}，敌对势力为 {targetFaction.Name}。护送部队：{escortPartyName}；当前阶段：{stage}。";
        }
    }
}
