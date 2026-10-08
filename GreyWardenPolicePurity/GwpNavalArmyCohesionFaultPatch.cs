using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// NavalDLC 的军团凝聚力模型每小时拿军团长查「诺德军团凝聚力」文化特长：<c>PartyBaseHelper.HasFeat</c> 在领主英雄不为空时
    /// 直接读 <c>LeaderHero.Culture.HasFeat</c>，文化为空、或文化对象没加载完（特长列表为空）就抛空引用，游戏直接崩
    /// （C#反编译 NavalDLC v1.2.8 NavalDLCArmyManagementCalculationModel.CalculateDailyCohesionChange、v1.4.8 PartyBaseHelper.HasFeat、CultureObject.HasFeat）。
    /// 还没查到是哪支军团、谁的文化有问题：这里接住，把军团长、领主英雄和文化记进故障日志，
    /// 凝聚力退回 NavalDLC 包着的原版算法（只少了诺德那一项加成），游戏继续。
    /// </summary>
    [HarmonyPatch]
    internal static class GwpNavalArmyCohesionFaultPatch
    {
        private static MethodBase? Target() =>
            AccessTools.Method("NavalDLC.GameComponents.NavalDLCArmyManagementCalculationModel:CalculateDailyCohesionChange");

        // 没装 NavalDLC 时没有这个模型，不打补丁。
        private static bool Prepare() => Target() != null;

        private static MethodBase TargetMethod() => Target()!;

        private static Exception? Finalizer(Exception? __exception, ArmyManagementCalculationModel __instance, Army army,
            bool includeDescriptions, ref ExplainedNumber __result)
        {
            if (__exception is not NullReferenceException)
                return __exception;
            // 依据：实机诊断 2026-10-08（NavalDLCArmyManagementCalculationModel.CalculateDailyCohesionChange → PartyBaseHelper.HasFeat
            // 抛 NullReferenceException，Army.HourlyTick 里没人接，游戏崩溃退出）。
            Hero? leader = army.LeaderParty?.LeaderHero;
            GwpFaultTrace.Write("ARMY_COHESION_FEAT_FAULT", details:
                "army=" + (army.LeaderParty?.StringId ?? "null") + "; armyType=" + army.ArmyType +
                "; kingdom=" + (army.Kingdom?.StringId ?? "null") + "; leaderHero=" + (leader?.StringId ?? "null") +
                "; heroState=" + (leader?.HeroState.ToString() ?? "-") + "; heroClan=" + (leader?.Clan?.StringId ?? "null") +
                "; heroCulture=" + (leader == null ? "-" : leader.Culture?.StringId ?? "null") +
                "; heroCultureFeats=" + (leader?.Culture == null ? "-" : leader.Culture.CultureFeats == null ? "null" : "ok") +
                "; partyCulture=" + (army.LeaderParty?.Party.Culture?.StringId ?? "null") +
                "; members=" + army.Parties.Count + "; exception=" + __exception.ToString().Replace(Environment.NewLine, " | "));
            var baseModel = (ArmyManagementCalculationModel)AccessTools.Property(__instance.GetType(), "BaseModel").GetValue(__instance);
            __result = baseModel.CalculateDailyCohesionChange(army, includeDescriptions);
            return null;
        }
    }
}
