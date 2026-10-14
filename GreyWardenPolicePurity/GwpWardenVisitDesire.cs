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

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍领主进聚落的意愿。灰袍对原版进城意愿（C#反编译 v1.4.8 AiVisitSettlementBehavior.AiHourlyTick）的全部改动都在这里，
    /// 每一处改动有名字，监控按名字记下来（用户裁定 2026-10-07：要知道领主为什么产生这个欲望，深层机制统一）。
    /// 只对灰袍领主改，原版领主和别的部队不受影响。改动：
    /// <list type="number">
    /// <item>候选：原版只给王国领主看本国所有聚落，独立家族只看身边约 0.8 天路程以内的（FillSettlementsToVisitWithDistancesAsDays：
    /// 直线 0.5 天 × 1.6 以内）。灰袍是独立家族，信众村和城镇一样，路过身边才进候选（用户裁定 2026-10-08：路过才去）。
    /// 以前信众村全图都进候选，俘虏半满就压过差事，跟人途中跑 60–126 格、16–86 小时回信众村交俘虏（实机诊断 2026-10-08）。</item>
    /// <item>招兵数：原版数要人格子里的兵；信众村再加上能直接招的狂信者（正在这里或刚走到门口时不加）；平均工资至少报 1（<see cref="VolunteersFor"/>）。</item>
    /// <item>打分（<see cref="Rescore"/>）：原版只给城堡城镇乘的俘虏、伤员、卖东西三项（合起来叫「需要」），信众村也乘，
    /// 「灰袍优先做信众村的事」的倍数放大的是需要高出 1 的那部分：1 + (需要 − 1) × 倍数。城堡城镇照原版不动：
    /// 买卖、招兵、招降俘虏、卖俘虏都照原版，灰袍只是更偏好去信众村办（用户裁定 2026-10-08：不是大改，别破坏原版体验）。</item>
    /// <item>养伤：灰袍领主在信众村里按在城里算（<see cref="GwpFaithHealingModel"/>）。</item>
    /// </list>
    /// 原版底分不只是 1.6 × 0.16：导航方式对得上还要 × 1.5（num38），所以一位什么都不缺的灰袍领主看身边的信众村约 0.38；
    /// 卖东西项只要背包里有一点非口粮的东西就大于 1（几百第纳尔的装备约 1.18，C#反编译 CalculateSellItemScore）。
    /// 以前倍数直接乘在整个分上，0.38 × 3 = 1.15 压过普通案件的 0.99，领主没事也往信众村跑、在村口原地待命
    /// （实机诊断 2026-10-07）。现在闲着、背包里只有一点东西约 0.59，压不过差事；俘虏半满约 1.96、伤员三成约 1.27，压得过。
    /// </summary>
    internal static partial class GwpWardenVisitDesire
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
        private static readonly MethodInfo NeutralRadiusDays =
            AccessTools.PropertyGetter(typeof(AiVisitSettlementBehavior), "SearchForNeutralSettlementRadiusAsDays");

        // 每位领主上一次打分时分最高的信众村，只给监控用；每小时重算，不存档。
        private static readonly Dictionary<string, string> LastBest = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static bool AppliesTo(MobileParty? party) =>
            party?.IsLordParty == true && party.LeaderHero != null && GwpWardenRecruitment.IsWardenParty(party);

        internal static string Describe(MobileParty party) =>
            LastBest.TryGetValue(party.StringId, out string best) ? best : "-";

        // ---------------------------------------------------------------- 候选

        internal static void AddFaithVillages(MobileParty party, IList candidates)
        {
            HashSet<Settlement> present = new HashSet<Settlement>(
                candidates.Cast<object>().Select(entry => (Settlement)NavigationSettlement.GetValue(entry)));
            // 原版独立家族的搜索半径：0.5 天 × 领主平均速度 × 24 × 1.6，直线距离。
            float radius = (float)NeutralRadiusDays.Invoke(null, null) * Campaign.Current.EstimatedAverageLordPartySpeed *
                           CampaignTime.HoursInDay * 1.6f;
            foreach (Settlement village in GwpFireswornBehavior.FaithVillages())
            {
                if (present.Contains(village) || party.Position.ToVec2().Distance(village.GetPosition2D) > radius ||
                    !(bool)IsSuitable.Invoke(null, new object[] { party, village }))
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

        // ---------------------------------------------------------------- 招兵数

        /// <summary>
        /// 原版估算「这里能招几个兵」时不看招不招得到，只数每位要人的前几格（同势力 4 格、否则 2 格），平均工资是整数除法
        /// （C#反编译 v1.4.8 GetApproximateVolunteersCanBeRecruitedDataFromSettlement）。灰袍照原版去各处要人那里招兵（用户裁定 2026-10-07）；
        /// 信众村再加上能直接招的狂信者（只招守村队里现有的、留够守村的）。
        /// 正在这座村或刚走到村口时不加：招兵只在进门那一刻（RecruitmentCampaignBehavior.OnBeforeSettlementEntered），
        /// 站在村口的招兵分只会让原版每小时选「去这里」，而「已经在这里」就原地待命（AiPartyThinkBehavior.PartyHourlyAiTick）。
        /// 原版拿平均工资去除工资预算再 MathF.Floor 成人数上限；格子里全是不领饷的灰袍兵时平均是 0，得到 -2147483648，
        /// 招兵意愿整个变成负数（运行时反射 2026-10-07），所以至少报 1。
        /// 灰袍偏好去信众村招兵（用户裁定 2026-10-08：买卖、招兵、卖俘虏照原版，只是偏好村子），所以信众村报的人数再乘
        /// 「灰袍优先做信众村的事」的倍数。原版招兵分按能招到的人数做乘方，城里要人多（城里七八位，村里三位），不乘的话缺兵时总是城里赢
        /// （C#反编译 v1.4.8 AiVisitSettlementBehavior.AiHourlyTick 的 num26–num31）。各要人出兵快慢和势力无关，只看格子和关系
        /// （DefaultVolunteerModel）。
        /// </summary>
        internal static (int, float) VolunteersFor(Hero hero, Settlement settlement, (int Count, float Wage) vanilla)
        {
            MobileParty? party = hero.PartyBelongedTo;
            bool here = party != null && MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(party) == settlement;
            int direct = here || party == null ? 0 : GwpFireswornBehavior.DirectZealotsFor(settlement, party);
            int count = vanilla.Count + direct;
            if (!here && GwpFireswornBehavior.IsPreachedVillage(settlement))
                count = (int)(count * GwpTuning.Firesworn.FaithVillageVisitPriority);
            return (count, Math.Max(1f, vanilla.Wage));
        }

        // ---------------------------------------------------------------- 打分

        /// <summary>
        /// 信众村的「卖东西」项：照原版 CalculateSellItemScore 的非坐骑那一半，1 + min(3, (价值 / ((人数 + 5) × 100))^0.33)，
        /// 军团再开方；但只算到了村里会交出去的东西（<see cref="GwpFireswornBehavior.KeptOnHandover"/>）。
        /// 原版这一项还有坐骑那一半（坐骑总值超过部队金币一成就加分），而交东西时留下了普通坐骑，用原版的分就永远交不干净、
        /// 永远想回来（实机诊断 2026-10-08：gw_leader_2 跟人途中每六小时回一次家乡信众村，9 次，信众村 1.24 压过差事 0.99）。
        /// </summary>
        private static float HandoverTerm(MobileParty party)
        {
            float value = 0f;
            for (int i = 0; i < party.ItemRoster.Count; i++)
            {
                ItemRosterElement element = party.ItemRoster[i];
                if (!GwpFireswornBehavior.KeptOnHandover(element.EquipmentElement))
                    value += element.Amount * element.EquipmentElement.Item.Value;
            }
            float term = 1f + Math.Min(3f, (float)Math.Pow(value / ((party.MemberRoster.TotalManCount + 5f) * 100f), 0.33f));
            return party.Army != null ? (float)Math.Sqrt(term) : term;
        }

        /// <summary>「灰袍优先做信众村的事」放大需要高出 1 的那部分。</summary>
        private static float Amplified(float need) => 1f + (need - 1f) * GwpTuning.Firesworn.FaithVillageVisitPriority;

        internal static void Rescore(MobileParty party, PartyThinkParams think)
        {
            // 军团长照原版把挂靠部队一起算。
            List<MobileParty> members = new List<MobileParty> { party };
            if (party.Army != null)
                members.AddRange(party.AttachedParties);
            int prisoners = members.Sum(member => member.PrisonRoster.TotalRegulars);
            int prisonLimit = Math.Max(1, members.Sum(member => member.Party.PrisonerSizeLimit));
            int men = Math.Max(1, members.Sum(member => member.MemberRoster.TotalManCount));
            float wounded = members.Sum(member => member.MemberRoster.TotalWounded) / (float)men;
            float sell = HandoverTerm(party);
            // 原版把「刚离开、还站在门口（距离 1 以内）」的聚落也当成「正在这里」；赢的目标就是「正在这里」时原版什么也不做，
            // 部队原地待命（MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation、AiPartyThinkBehavior.PartyHourlyAiTick）。
            Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(party);
            // 原版城堡城镇的伤员项：伤员超过两成，按比例乘 1–5。
            float woundTerm = wounded > 0.2f ? MBMath.Map(wounded - 0.2f, 0f, 0.8f, 1f, 5f) : 1f;
            // 原版城堡城镇的俘虏项是整数除法、满了才乘 3；信众村连续算，地牢满了（软上限）就不算。
            float prisonerTerm = 1f + 2f * Math.Min(1f, prisoners / (float)prisonLimit);

            string best = "-";
            float bestScore = 0f;
            foreach ((AIBehaviorData data, float score) in think.AIBehaviorScores.ToList())
            {
                // 只改信众村；城堡、城镇和别的聚落照原版。
                if (data.AiBehavior != AiBehavior.GoToSettlement || data.Party is not Settlement settlement || score <= 0f ||
                    !GwpFireswornBehavior.IsPreachedVillage(settlement))
                    continue;
                float factor;
                if (settlement == here)
                {
                    // 已经在这座村：只有人还在村里、伤员多时留下养伤；刚走到村口的不再加分。
                    bool healing = party.CurrentSettlement == settlement && woundTerm > 1f;
                    factor = healing ? Amplified(woundTerm) : 1f;
                }
                else
                {
                    // 倍数放大的是需要，不是底分；没事就是原版的闲逛分（用户裁定 2026-10-05：灰袍优先做信众村的事）。
                    float need = (GwpFireswornBehavior.PrisonerRoom(settlement) > 0 ? prisonerTerm : 1f) * woundTerm * sell;
                    factor = Amplified(need);
                }
                float rescored = score * factor;
                if (factor != 1f)
                    think.SetBehaviorScore(in data, rescored);
                if (rescored > bestScore)
                {
                    bestScore = rescored;
                    best = settlement.StringId + ":" + rescored.ToString("0.00");
                }
            }
            LastBest[party.StringId] = best;
        }

    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "FillSettlementsToVisitWithDistancesAsDays")]
    internal static class GwpWardenVisitCandidatesPatch
    {
        private static void Postfix(MobileParty mobileParty, object listToFill)
        {
            if (!GwpWardenVisitDesire.AppliesTo(mobileParty))
                return;
            GwpWardenVisitDesire.AddFaithVillages(mobileParty, (IList)listToFill);
        }
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "AiHourlyTick")]
    internal static class GwpWardenVisitScorePatch
    {
        private static bool Applies(MobileParty mobileParty) =>
            GwpWardenVisitDesire.AppliesTo(mobileParty) &&
            !(mobileParty.Army != null && mobileParty.Army.LeaderParty != mobileParty);

        private static void Postfix(MobileParty mobileParty, PartyThinkParams p)
        {
            if (!Applies(mobileParty))
                return;
            GwpWardenVisitDesire.Rescore(mobileParty, p);
        }
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "GetApproximateVolunteersCanBeRecruitedDataFromSettlement")]
    internal static class GwpWardenVisitVolunteersPatch
    {
        private static void Postfix(Hero hero, Settlement settlement, ref (int, float) __result)
        {
            if (!GwpWardenRecruitment.IsWardenBuyer(hero))
                return;
            __result = GwpWardenVisitDesire.VolunteersFor(hero, settlement, __result);
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
            return GwpWardenVisitDesire.AppliesTo(mobile) && GwpFireswornBehavior.IsPreachedVillage(settlement) &&
                   !settlement!.IsRaided && !settlement.IsUnderRaid;
        }

        public override ExplainedNumber GetDailyHealingForRegulars(PartyBase partyBase, bool isPrisoner,
            bool includeDescriptions = false)
        {
            ExplainedNumber healing = Inner.GetDailyHealingForRegulars(partyBase, isPrisoner, includeDescriptions);
            if (!isPrisoner && InFaithVillage(partyBase))
                healing.Add(10f, GwpText.Create(GwpTextCatalog.gwp_fs_healing_hearth));
            return healing;
        }

        public override ExplainedNumber GetDailyHealingHpForHeroes(PartyBase partyBase, bool isPrisoners,
            bool includeDescriptions = false)
        {
            ExplainedNumber healing = Inner.GetDailyHealingHpForHeroes(partyBase, isPrisoners, includeDescriptions);
            if (!isPrisoners && InFaithVillage(partyBase))
                healing.Add(8f, GwpText.Create(GwpTextCatalog.gwp_fs_healing_hearth));
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
