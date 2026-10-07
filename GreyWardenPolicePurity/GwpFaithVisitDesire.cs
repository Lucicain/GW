using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 信众村对灰袍等于城（用户裁定 2026-10-05；2026-10-07 定为改原版进城意愿，取代另搞一套「信众村差事」）。
    /// 灰袍领主什么时候进信众村交俘虏、捐东西、招兵、买粮、养伤，全由原版的进城意愿决定
    /// （C#反编译 v1.4.8 AiVisitSettlementBehavior.AiHourlyTick），只对灰袍领主改三处，原版领主和别的部队不受影响：
    /// <list type="number">
    /// <item>候选：原版只给王国领主看本国所有聚落，独立家族只看身边约 0.8 天路程以内的（FillSettlementsToVisitWithDistancesAsDays）。
    /// 灰袍的信众村像王国领主的本国聚落一样，只要在原版最远距离内都进候选。</item>
    /// <item>打分：俘虏、伤员、背包里的东西三项原版只给城堡、城镇加分，信众村按城加，再乘「灰袍优先做信众村的事」的倍数。
    /// 原版俘虏项是整数除法，俘虏不满等于没有，信众村按俘虏多少连续加。城镇、城堡对灰袍去掉俘虏项和卖东西项——
    /// 灰袍在城里不卖普通俘虏，也不卖背包里的东西。</item>
    /// <item>疗伤：灰袍领主在信众村里按在城里算（<see cref="GwpFaithHealingModel"/>）。</item>
    /// </list>
    /// 买粮、招兵两项原版本来就给村子算；招兵数见 <see cref="GwpWardenRecruitDesirePatch"/>。
    /// 进了信众村以后交接、招狂信者由 <see cref="GwpFireswornBehavior"/> 做。
    /// </summary>
    internal static class GwpFaithVisitDesire
    {
        private static readonly Type NavigationDataType =
            AccessTools.Inner(typeof(AiVisitSettlementBehavior), "SettlementNavigationData");
        private static readonly FieldInfo NavigationSettlement = AccessTools.Field(NavigationDataType, "Settlement");
        private static readonly MethodInfo IsSuitable =
            AccessTools.Method(typeof(AiVisitSettlementBehavior), "IsSettlementSuitableForVisitingCondition");
        private static readonly MethodInfo GetNavigation =
            AccessTools.Method(typeof(AiVisitSettlementBehavior), "GetBestNavigationDataForVisitingSettlement");
        private static readonly MethodInfo GetMaximumDistance =
            AccessTools.Method(typeof(AiVisitSettlementBehavior), "GetMaximumDistanceAsDays");
        private static readonly MethodInfo SellItemScore =
            AccessTools.Method(typeof(AiVisitSettlementBehavior), "CalculateSellItemScore");

        // 每位领主上一次打分时分最高的信众村，只给监控用；每小时重算，不存档。
        private static readonly Dictionary<string, string> LastBest = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static bool AppliesTo(MobileParty? party) =>
            party?.IsLordParty == true && party.LeaderHero != null && GwpWardenRecruitment.IsWardenParty(party);

        internal static string Describe(MobileParty party) =>
            LastBest.TryGetValue(party.StringId, out string best) ? best : "-";

        internal static void AddFaithVillages(MobileParty party, IList candidates)
        {
            HashSet<Settlement> present = new HashSet<Settlement>(
                candidates.Cast<object>().Select(entry => (Settlement)NavigationSettlement.GetValue(entry)));
            foreach (Settlement village in GwpFireswornBehavior.FaithVillages())
            {
                if (present.Contains(village) || !(bool)IsSuitable.Invoke(null, new object[] { party, village }))
                    continue;
                object?[] args = { party, village, null, null, null, null };
                GetNavigation.Invoke(null, args);
                var navigation = (MobileParty.NavigationType)args[2]!;
                float days = (float)args[3]!;
                if (navigation == MobileParty.NavigationType.None ||
                    days >= (float)GetMaximumDistance.Invoke(null, new object[] { navigation }))
                    continue;
                candidates.Add(Activator.CreateInstance(NavigationDataType,
                    days, village.GetHashCode(), village, navigation, args[4], args[5]));
            }
        }

        internal static void Rescore(AiVisitSettlementBehavior behavior, MobileParty party, PartyThinkParams think)
        {
            // 军团长照原版把挂靠部队一起算。
            List<MobileParty> members = new List<MobileParty> { party };
            if (party.Army != null)
                members.AddRange(party.AttachedParties);
            int prisoners = members.Sum(member => member.PrisonRoster.TotalRegulars);
            int prisonLimit = Math.Max(1, members.Sum(member => member.Party.PrisonerSizeLimit));
            int men = Math.Max(1, members.Sum(member => member.MemberRoster.TotalManCount));
            float wounded = members.Sum(member => member.MemberRoster.TotalWounded) / (float)men;
            float sell = (float)SellItemScore.Invoke(behavior, new object[] { party });

            // 原版把「刚离开、还站在门口（距离 1 以内）」的聚落也当成「正在这里」（MobilePartyHelper
            // .GetCurrentSettlementOfMobilePartyForAICalculation）；赢的目标就是「正在这里」的聚落时，原版什么也不做，部队原地待命
            // （C#反编译 v1.4.8 AiPartyThinkBehavior.PartyHourlyAiTick）。所以刚办完交接走出村口的领主不能再给这座村加分，
            // 否则它每小时都赢、每小时都不动（实机诊断 2026-10-07：开局四位领主在家乡村门口待命约 100 小时）。
            // 还在村里面、伤员多时照样加，让她留下养伤。
            Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(party);
            string best = "-";
            float bestScore = 0f;
            foreach ((AIBehaviorData data, float score) in think.AIBehaviorScores.ToList())
            {
                if (data.AiBehavior != AiBehavior.GoToSettlement || data.Party is not Settlement settlement || score <= 0f)
                    continue;
                float rescored;
                // 原版城堡城镇的伤员项：伤员超过两成，按比例乘 1–5。
                float woundTerm = wounded > 0.2f ? MBMath.Map(wounded - 0.2f, 0f, 0.8f, 1f, 5f) : 1f;
                if (settlement == here && GwpFireswornBehavior.IsPreachedVillage(settlement))
                {
                    if (party.CurrentSettlement != settlement || woundTerm <= 1f)
                        continue;
                    rescored = score * woundTerm * GwpTuning.Firesworn.FaithVillageVisitPriority;
                }
                else if (GwpFireswornBehavior.IsPreachedVillage(settlement))
                {
                    float prisonerTerm = 1f + 2f * Math.Min(1f, prisoners / (float)prisonLimit);
                    rescored = score * prisonerTerm * woundTerm * sell * GwpTuning.Firesworn.FaithVillageVisitPriority;
                    if (rescored > bestScore)
                    {
                        bestScore = rescored;
                        best = settlement.StringId + ":" + rescored.ToString("0.00");
                    }
                }
                else if (settlement.IsFortification)
                {
                    // 去掉原版给城堡城镇的俘虏项（整数除法，满了才乘 3）和城镇的卖东西项。
                    float vanillaPrisonerTerm = 1f + 2f * (prisoners / prisonLimit);
                    rescored = score / (vanillaPrisonerTerm * (settlement.IsTown ? sell : 1f));
                }
                else
                    continue;
                think.SetBehaviorScore(in data, rescored);
            }
            LastBest[party.StringId] = best;
        }
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "FillSettlementsToVisitWithDistancesAsDays")]
    internal static class GwpFaithVisitCandidatesPatch
    {
        private static void Postfix(MobileParty mobileParty, object listToFill)
        {
            if (GwpFaithVisitDesire.AppliesTo(mobileParty))
                GwpFaithVisitDesire.AddFaithVillages(mobileParty, (IList)listToFill);
        }
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "AiHourlyTick")]
    internal static class GwpFaithVisitScorePatch
    {
        private static void Postfix(AiVisitSettlementBehavior __instance, MobileParty mobileParty, PartyThinkParams p)
        {
            if (GwpFaithVisitDesire.AppliesTo(mobileParty) &&
                !(mobileParty.Army != null && mobileParty.Army.LeaderParty != mobileParty))
                GwpFaithVisitDesire.Rescore(__instance, mobileParty, p);
        }
    }

    /// <summary>
    /// 灰袍领主在信众村里养伤按在城里算：原版在城堡、城镇里普通兵每天多好 10 个、英雄多回 8 点
    /// （C#反编译 v1.4.8 DefaultPartyHealingModel）。别的部队、别的村子照原版。
    /// </summary>
    public sealed class GwpFaithHealingModel : PartyHealingModel
    {
        private PartyHealingModel Inner => BaseModel;

        private static bool InFaithVillage(PartyBase? party)
        {
            MobileParty? mobile = party?.MobileParty;
            Settlement? settlement = mobile?.CurrentSettlement;
            return GwpFaithVisitDesire.AppliesTo(mobile) && GwpFireswornBehavior.IsPreachedVillage(settlement) &&
                   !settlement!.IsRaided && !settlement.IsUnderRaid;
        }

        public override ExplainedNumber GetDailyHealingForRegulars(PartyBase partyBase, bool isPrisoner,
            bool includeDescriptions = false)
        {
            ExplainedNumber healing = Inner.GetDailyHealingForRegulars(partyBase, isPrisoner, includeDescriptions);
            if (!isPrisoner && InFaithVillage(partyBase))
                healing.Add(10f, GwpText.Create("{=gwp_fs_healing_hearth}Hearth of the Eternal Fire"));
            return healing;
        }

        public override ExplainedNumber GetDailyHealingHpForHeroes(PartyBase partyBase, bool isPrisoners,
            bool includeDescriptions = false)
        {
            ExplainedNumber healing = Inner.GetDailyHealingHpForHeroes(partyBase, isPrisoners, includeDescriptions);
            if (!isPrisoners && InFaithVillage(partyBase))
                healing.Add(8f, GwpText.Create("{=gwp_fs_healing_hearth}Hearth of the Eternal Fire"));
            return healing;
        }

        public override float GetSurgeryChance(PartyBase party) => Inner.GetSurgeryChance(party);

        public override float GetSurvivalChance(PartyBase party, CharacterObject agentCharacter, DamageTypes damageType,
            bool canDamageKillEvenIfBlunt, PartyBase? enemyParty = null) =>
            Inner.GetSurvivalChance(party, agentCharacter, damageType, canDamageKillEvenIfBlunt, enemyParty!);

        public override int GetSkillXpFromHealingTroop(PartyBase party) => Inner.GetSkillXpFromHealingTroop(party);

        public override int GetHeroesEffectedHealingAmount(Hero hero, float healingRate) =>
            Inner.GetHeroesEffectedHealingAmount(hero, healingRate);

        public override float GetSiegeBombardmentHitSurgeryChance(PartyBase party) =>
            Inner.GetSiegeBombardmentHitSurgeryChance(party);

        public override ExplainedNumber GetBattleEndHealingAmount(PartyBase partyBase, Hero hero) =>
            Inner.GetBattleEndHealingAmount(partyBase, hero);
    }
}
