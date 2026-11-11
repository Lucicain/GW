using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>已接住城案的目标留城：原版换城、领军、接任与总督入口只排除被锁者，资格归 AcceptedCityCaseSettlement。</summary>
    internal static class GwpCityCaseLock
    {
        internal static bool IsUnlocked(Hero hero) => PoliceEnforcementBehavior.AcceptedCityCaseSettlement(hero) == null;

        // 四个选人方法中，这类返回值只用来提供候选名单。保留名单来源，让其他补丁也能改写它。
        internal static IEnumerable<CodeInstruction> FilterCandidates(IEnumerable<CodeInstruction> instructions, int expected)
        {
            MethodInfo filter = AccessTools.Method(typeof(GwpCityCaseLock), nameof(Unlocked));
            int matched = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                bool loadsCandidates = (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                    && instruction.operand is MethodInfo source && source.ReturnType == typeof(MBReadOnlyList<Hero>);
                yield return instruction;
                if (!loadsCandidates) continue;
                yield return new CodeInstruction(OpCodes.Call, filter);
                matched++;
            }
            if (matched != expected)
                throw new InvalidOperationException($"城内案件候选入口有变：预期 {expected} 处，实际 {matched} 处。");
        }

        private static MBReadOnlyList<Hero> Unlocked(MBReadOnlyList<Hero> heroes)
        {
            MBList<Hero>? eligible = null;
            foreach (Hero hero in heroes)
                if (!IsUnlocked(hero))
                {
                    eligible ??= new MBList<Hero>(heroes);
                    eligible.Remove(hero);
                }
            return eligible ?? heroes;
        }
    }

    /// <summary>
    /// 原版每日对无队英雄的移动：在役者走日常换城，逃亡/获释者按概率另找落脚处（后者不经 CanMoveToSettlement）。
    /// 被锁目标当天整段跳过；解除后照原版继续。
    /// </summary>
    [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "OnHeroDailyTick")]
    internal static class GwpCityCaseDailyMovePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero hero)
        {
            if (PoliceEnforcementBehavior.AcceptedCityCaseSettlement(hero) is not { } town) return true;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("CITY_CASE_LOCKED", "target=" + hero.StringId
                + "; town=" + town.StringId + "; state=" + hero.HeroState);
#endif
            return false;
        }
    }

    [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "GetBestAvailableCommander")]
    internal static class GwpCityCaseNewCommanderPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GwpCityCaseLock.FilterCandidates(instructions, 2);
    }

    // 非战斗领袖换人与待解散部队找接任者，各从家族英雄里取一次候选。
    [HarmonyPatch]
    internal static class GwpCityCaseReplacementCommanderPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(TeleportationCampaignBehavior), "DailyTickParty"),
            AccessTools.Method(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbandStarted"),
        };

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GwpCityCaseLock.FilterCandidates(instructions, 1);
    }

    // AI家族每日重排总督会把无队领主延迟传送到别的封地；已接案目标不进候选。
    [HarmonyPatch(typeof(ClanVariablesCampaignBehavior), "UpdateGovernorsOfClan")]
    internal static class GwpCityCaseGovernorPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GwpCityCaseLock.FilterCandidates(instructions, 1);
    }

    [HarmonyPatch(typeof(TeleportationCampaignBehavior), "OnHeroComesOfAge")]
    internal static class GwpCityCaseAdultCommanderPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero hero) => GwpCityCaseLock.IsUnlocked(hero);
    }
}
