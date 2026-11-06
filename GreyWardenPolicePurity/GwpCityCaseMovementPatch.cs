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

        // 只替换原版读取的家族名单，保留候选顺序、评分、抽签和无人可选时的处理。
        internal static IEnumerable<CodeInstruction> FilterCandidates(
            IEnumerable<CodeInstruction> instructions, string clanList, string filter, int expected)
        {
            MethodInfo source = AccessTools.PropertyGetter(typeof(Clan), clanList);
            MethodInfo filtered = AccessTools.Method(typeof(GwpCityCaseLock), filter);
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(source))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = filtered;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != expected)
                throw new InvalidOperationException($"城内案件候选入口有变：{clanList} 预期 {expected} 处，实际 {replaced} 处。");
        }

        internal static MBReadOnlyList<Hero> EligibleHeroes(Clan clan) => Unlocked(clan.Heroes);

        internal static MBReadOnlyList<Hero> EligibleLords(Clan clan) => Unlocked(clan.AliveLords);

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
            GwpCityCaseLock.FilterCandidates(instructions, nameof(Clan.Heroes), nameof(GwpCityCaseLock.EligibleHeroes), 2);
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
            GwpCityCaseLock.FilterCandidates(instructions, nameof(Clan.Heroes), nameof(GwpCityCaseLock.EligibleHeroes), 1);
    }

    // AI家族每日重排总督会把无队领主延迟传送到别的封地；已接案目标不进候选。
    [HarmonyPatch(typeof(ClanVariablesCampaignBehavior), "UpdateGovernorsOfClan")]
    internal static class GwpCityCaseGovernorPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GwpCityCaseLock.FilterCandidates(instructions, nameof(Clan.AliveLords), nameof(GwpCityCaseLock.EligibleLords), 1);
    }

    [HarmonyPatch(typeof(TeleportationCampaignBehavior), "OnHeroComesOfAge")]
    internal static class GwpCityCaseAdultCommanderPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero hero) => GwpCityCaseLock.IsUnlocked(hero);
    }
}
