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
                stage = "玩家已被纠察队控制，正在押送或等待处罚";
            else if (activePatrolCount > 0)
                stage = $"地图上仍有 {activePatrolCount} 支纠察队在执行强制缉拿";
            else
                stage = "已进入纠察队战争状态，正在等待收尾";

            return $"纠察队执法：玩家在负声望执法阶段拒绝配合后，灰袍守卫会对玩家所属势力宣战以强制缉拿。当前阶段：{stage}；当前模组声望：{PlayerState.Reputation}。";
        }
    }
}
