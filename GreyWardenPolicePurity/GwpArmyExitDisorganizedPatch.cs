using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版 <c>DisorganizedStateCampaignBehavior.OnPartyRemovedFromArmy</c> 对每一支
    /// 脱离军团的队伍无条件 <c>SetDisorganized(true)</c>，代价是 <c>-40%</c> 速度、
    /// 持续 <c>6</c> 小时（`DefaultPartySpeedCalculatingModel` 的 `AddFactor(-0.4f)`
    /// 与 `DefaultPartyImpairmentModel.GetDisorganizedStateDuration` 的 `6f`）。
    ///
    /// 那条规则是为王国军团写的：集结一次、打一仗、散伙，一年也没几回。灰袍的协力
    /// 军团完全不是这个节奏——按目标现场战力随时拉人、随时放人，还会因为目标太快而
    /// 整组速度分散。每一次进出都吃一记 40%，办案的人就永远在减速里爬，而且玩家在
    /// 地图上看不出缘由。
    ///
    /// 因此灰袍的队伍脱离军团时跳过这一次混乱。**只跳过"脱离军团"这一个入口**：
    /// 同一个行为类里战斗结束、突围、菜单进攻那几条照常生效，打完仗该乱还是乱。
    ///
    /// 灰袍是无封地独立家族，不会加入王国军团，所以"本家族队伍脱离的军团"实际就是
    /// 我们自己的协力军团。事件签名只给 <c>MobileParty</c>、不给它离开的那个 Army，
    /// 无法再细分，这一点写在这里备查。
    /// </summary>
    [HarmonyPatch]
    internal static class GwpArmyExitDisorganizedPatch
    {
        private const string BehaviorTypeName =
            "TaleWorlds.CampaignSystem.CampaignBehaviors.DisorganizedStateCampaignBehavior";

        private static MethodBase? _target;

        private static bool Prepare()
        {
            _target ??= ResolveTarget();
            return _target != null;
        }

        private static MethodBase? ResolveTarget()
        {
            try
            {
                Type? behavior = AccessTools.TypeByName(BehaviorTypeName);
                return behavior == null
                    ? null
                    : AccessTools.Method(behavior, "OnPartyRemovedFromArmy",
                        new[] { typeof(MobileParty) });
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("ARMY_EXIT_DISORGANIZED_TARGET_FAILED",
                    details: exception.GetType().FullName + ":" + exception.Message);
                return null;
            }
        }

        private static MethodBase TargetMethod() => _target!;

        [HarmonyPrefix]
        private static bool Prefix(MobileParty mobileParty)
        {
            if (!IsGreyWardenParty(mobileParty)) return true;

            GwpAiDiagnostics.WriteAction(mobileParty, "ARMY_EXIT_DISORGANIZED_SKIPPED",
                "army=" + (mobileParty.Army?.LeaderParty?.StringId ?? "-") +
                "; men=" + (mobileParty.MemberRoster?.TotalManCount ?? 0));
            return false;
        }

        private static bool IsGreyWardenParty(MobileParty? party)
        {
            if (party?.IsActive != true || party.IsMainParty) return false;
            if (GwpCommon.IsEnforcementDelayPatrolParty(party) ||
                GwpCommon.IsTrainingCohortParty(party) ||
                GwpWardenDispatchBehavior.IsDispatchParty(party))
                return true;

            return string.Equals(party.ActualClan?.StringId, GwpIds.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
