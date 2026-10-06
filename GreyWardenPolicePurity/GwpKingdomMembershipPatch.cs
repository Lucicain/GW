using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GreyWardenPolicePurity
{
    // 外交评分不约束直接调用动作的模组。先于债务、阵营关系和成员列表写入阻止入国。
    // 依据：1.4.8 ChangeKingdomAction / BellumCivile 1.4.0 DLL 反编译，2026-10-06。
    [HarmonyPatch]
    internal static class GwpKingdomMembershipPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // 叛国入口在 ApplyInternal 返回后还会发 OnClanDefected，必须拦住完整入口。
            yield return AccessTools.Method(typeof(ChangeKingdomAction), nameof(ChangeKingdomAction.ApplyByJoinToKingdom));
            yield return AccessTools.Method(typeof(ChangeKingdomAction), nameof(ChangeKingdomAction.ApplyByJoinToKingdomByDefection));
            yield return AccessTools.Method(typeof(ChangeKingdomAction), nameof(ChangeKingdomAction.ApplyByJoinFactionAsMercenary));
            yield return AccessTools.Method(typeof(ChangeKingdomAction), nameof(ChangeKingdomAction.ApplyByCreateKingdom));
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Clan clan, Kingdom newKingdom, MethodBase __originalMethod)
        {
            if (newKingdom == null || !PoliceAntiRecruitmentModel.IsGwClan(clan))
                return true;

#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("WARDEN_KINGDOM_JOIN_BLOCKED", details:
                "clan=" + clan.StringId + "; target=" + newKingdom.StringId
                + "; action=" + __originalMethod.Name + "; caller=" + Environment.StackTrace);
#endif
            return false;
        }
    }

    // 排除内战模组的流亡候选，避免先选收容国、排队询问玩家或发出虚假的收容通知。
    // 无此模组时不挂钩，不添加 DLL 依赖；入国动作的拦截始终有效。
    [HarmonyPatch]
    internal static class GwpBellumExileCandidatePatch
    {
        private static MethodBase? TargetMethod()
        {
            Type? behavior = AccessTools.TypeByName(
                "BellumCivile.Behaviors.ExiledClanRecoveryBehavior");
            return behavior == null ? null : AccessTools.Method(behavior,
                "IsRecoverableExileCandidate", new[] { typeof(Clan) });
        }

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Clan clan, ref bool __result)
        {
            if (!PoliceAntiRecruitmentModel.IsGwClan(clan))
                return true;

#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("WARDEN_BELLUM_EXILE_EXCLUDED", details:
                "clan=" + clan.StringId + "; caller=" + Environment.StackTrace);
#endif
            __result = false;
            return false;
        }
    }
}
