using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 协力编队的唯一判断：谁算在编、编队空了怎么收、要不要先会合。军团维护、原版解散保护、
    /// 会合意图都调这里，不各写一份空组判断。只放编队，不结案——案件怎么结束仍归 CaseLifecycle。
    /// </summary>
    public partial class PoliceEnforcementBehavior
    {
        /// <summary>本案还在参与的普通临时支援队。返程的、使者、独立追截的极速截击队不算，不靠它们保留军团。</summary>
        private IEnumerable<MobileParty> FormationSupports(LordAssistanceGroup group) =>
            _delayPatrolStates.Values.Where(state => !state.Returning && !state.IsEnvoy &&
                    !state.IsImmediateInterceptor && state.SourceTaskPolicePartyId == group.LeaderPartyId &&
                    state.TargetPartyId == group.TargetPartyId)
                .Select(state => FindActiveParty(state.PatrolPartyId))
                .Where(party => party != null).Cast<MobileParty>();

        /// <summary>有效的协办领主加上本案临时支援。</summary>
        private IEnumerable<MobileParty> FormationParticipants(LordAssistanceGroup group) =>
            group.MemberPartyIds.Select(FindActiveParty).Where(CanContinueLeadingPoliceTask)
                .Cast<MobileParty>().Concat(FormationSupports(group)).Distinct();

        private bool HasFormationParticipants(LordAssistanceGroup group) =>
            FormationParticipants(group).Any();

        /// <summary>组长或军团里任何一支队伍正在打仗。</summary>
        private static bool FormationInBattle(MobileParty leader) =>
            leader.MapEvent != null || leader.Army?.Parties.Any(party => party.MapEvent != null) == true ||
            _instance != null && _instance._assistanceGroups.TryGetValue(leader.StringId, out LordAssistanceGroup? group) &&
            _instance.FormationParticipants(group).Any(member => member.MapEvent != null);

        /// <summary>
        /// 核对编队：剔掉失效、被别的军团拉走的协办人；一个有效协力都不剩就放掉协力组和军团壳子，案子照留。
        /// 返回编队是否还在。
        /// </summary>
        private bool ReconcileAssistanceFormation(MobileParty leader, LordAssistanceGroup group)
        {
            if (group.PendingReleaseReason.Length != 0)
            {
                ReleaseAssistanceGroup(group.LeaderPartyId, group.PendingReleaseReason);
                return false;
            }
            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!CanContinueLeadingPoliceTask(member) ||
                    member!.Army != null && !IsArmyOwnedByGroup(member.Army, group))
                    RemoveAssistanceMember(group, memberId);
            }
            if (HasFormationParticipants(group)) return true;
            // 正在打仗时不从战斗里拆人，仗打完再核对；空组状态本身就在已存档的协力组字段里，读档后照样接上。
            if (FormationInBattle(leader)) return false;
            ReleaseAssistanceGroup(leader.StringId, "formation_empty");
            return false;
        }

        private void ReconcileAssistanceFormations()
        {
            foreach (LordAssistanceGroup group in _assistanceGroups.Values.ToList())
            {
                MobileParty? leader = FindActiveParty(group.LeaderPartyId);
                if (leader != null) ReconcileAssistanceFormation(leader, group);
            }
        }

        /// <summary>灰袍领主带着一个没有协力组认领的巡逻军团（读档、旧流程留下的壳子）：用原版解散入口放掉，案子照留。</summary>
        private void ReleaseUnownedAssistanceArmy(MobileParty leader)
        {
            Army? army = leader.Army;
            if (army == null || army.LeaderParty != leader || army.Kingdom != null ||
                army.ArmyType != Army.ArmyTypes.Patrolling ||
                _assistanceGroups.ContainsKey(leader.StringId) || FormationInBattle(leader)) return;
            GwpAssistanceArmyDisbandGuardPatch.ApplyAuthorizedObjectiveFinished(army);
            GreyWardenPartyDesireBehavior.ClearIntent(leader);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ORPHAN_ARMY_RELEASED", "casePreserved=True");
#endif
        }

    }
}
