using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
#if GWP_DIAGNOSTICS
    /// <summary>
    /// 灰袍领主每小时为什么想去哪儿（用户 2026-10-07：要知道领主当时在想什么、为什么产生这个欲望）。
    /// 原版进城分是二十来个因子连乘（C#反编译 v1.4.8 AiVisitSettlementBehavior.AiHourlyTick），这里逐个因子照原版重算，
    /// 和原版实际加进候选的分数对账（对不上就在行里写出差多少），再写上灰袍改了哪几处、最后竞价谁赢、赢了以后原版会怎么做。
    /// 原版的辅助方法（部队参数、卖东西分、要人格子、做领主的分）不另外调用——有的会改状态（.claude/rules/csharp-gotchas.md）——
    /// 只在原版自己调用时用后置补丁旁观返回值。
    /// </summary>
    internal static partial class GwpWardenVisitDesire
    {
        private sealed class TickTrace
        {
            public MobileParty Party = null!;
            public AiVisitSettlementBehavior Behavior = null!;
            public (float SizeRatio, float NormalLimit, int Wounded, int Men) Params;
            public float Sell = 1f;
            public readonly Dictionary<Settlement, (float Distance, MobileParty.NavigationType Navigation)> Candidates =
                new Dictionary<Settlement, (float, MobileParty.NavigationType)>();
            public readonly Dictionary<Settlement, (int Count, float Wage)> Volunteers = new Dictionary<Settlement, (int, float)>();
            public readonly Dictionary<Settlement, (float, float, float, float)> Owner = new Dictionary<Settlement, (float, float, float, float)>();
            public readonly Dictionary<Settlement, float> Vanilla = new Dictionary<Settlement, float>();
            public readonly Dictionary<Settlement, (float Factor, string Why)> Gwp = new Dictionary<Settlement, (float, string)>();
        }

        private static readonly FieldInfo NavigationDistance = AccessTools.Field(NavigationDataType, "Distance");
        private static readonly FieldInfo NavigationType = AccessTools.Field(NavigationDataType, "BestNavigationType");
        private static readonly AccessTools.FieldRef<AiVisitSettlementBehavior, Dictionary<Settlement, int>> AlliedTargeting =
            AccessTools.FieldRefAccess<AiVisitSettlementBehavior, Dictionary<Settlement, int>>("_numberOfAlliedMobilePartiesTargetingSettlement");

        // 正在算的那一位（原版一次只算一支部队，不会重入）；算完的放进 Finished，等竞价写行时取走。只给监控用，不存档。
        private static TickTrace? _current;
        private static readonly Dictionary<string, TickTrace> Finished = new Dictionary<string, TickTrace>(StringComparer.Ordinal);

        internal static void BeginTrace(AiVisitSettlementBehavior behavior, MobileParty party) =>
            _current = new TickTrace { Party = party, Behavior = behavior };

        internal static void EndTrace(MobileParty party)
        {
            if (_current?.Party == party)
                Finished[party.StringId] = _current;
            _current = null;
        }

        internal static void TraceCandidates(MobileParty party, IList candidates)
        {
            if (_current?.Party != party)
                return;
            foreach (object entry in candidates)
                _current.Candidates[(Settlement)NavigationSettlement.GetValue(entry)] =
                    ((float)NavigationDistance.GetValue(entry), (MobileParty.NavigationType)NavigationType.GetValue(entry));
        }

        internal static void TraceVolunteers(Settlement settlement, (int, float) result)
        {
            if (_current != null)
                _current.Volunteers[settlement] = result;
        }

        internal static void RecordAdjustment(MobileParty party, Settlement settlement, float factor, string why)
        {
            if (_current?.Party == party)
                _current.Gwp[settlement] = (factor, why);
        }

        internal static void TraceParameters(MobileParty party, (float, float, int, int) result)
        {
            if (_current?.Party == party)
                _current.Params = result;
        }

        internal static void TraceSell(MobileParty party, float result)
        {
            if (_current?.Party == party)
                _current.Sell = result;
        }

        internal static void TraceOwner(MobileParty party, Settlement settlement, (float, float, float, float) result)
        {
            if (_current?.Party == party)
                _current.Owner[settlement] = result;
        }

        internal static void TraceVanilla(Settlement settlement, float score)
        {
            if (_current == null)
                return;
            _current.Vanilla.TryGetValue(settlement, out float sum);
            _current.Vanilla[settlement] = sum + score;
        }

        /// <summary>照原版公式逐个因子重算这个聚落的进城分，返回分数和因子明细。</summary>
        private static (float Score, string Factors) Mirror(TickTrace t, Settlement settlement)
        {
            MobileParty party = t.Party;
            if (!t.Candidates.TryGetValue(settlement, out var nav))
                return (0f, "notCandidate");
            float maxMeaningful = (float)GetMaximumDistance.Invoke(null, new object[] { nav.Navigation }) * 0.7f;
            if (nav.Distance >= maxMeaningful)
                return (0.025f, "far:" + nav.Distance.ToString("0.00") + ">=" + maxMeaningful.ToString("0.00"));

            float item = t.Params.SizeRatio;
            float num = t.Params.NormalLimit / Math.Min(1f, Math.Max(0.1f, item));
            float num2 = num >= 1f ? 0.33f : (Math.Max(1f, Math.Min(2f, num)) - 0.5f) / 1.5f;
            float food = party.Food;
            float foodUse = -party.FoodChange;
            int gold = party.PartyTradeGold;
            int prisonLimit = party.Party.PrisonerSizeLimit;
            if (party.Army != null && party == party.Army.LeaderParty)
                foreach (MobileParty attached in party.Army.LeaderParty.AttachedParties)
                {
                    food += attached.Food;
                    foodUse += -attached.FoodChange;
                    gold += attached.PartyTradeGold;
                }
            if (party.Army != null)
                foreach (MobileParty attached in party.Army.LeaderParty.AttachedParties)
                    prisonLimit += attached.Party.PrisonerSizeLimit;
            Hero leader = party.LeaderHero;
            float num8 = PartyBaseHelper.FindPartySizeNormalLimit(party);
            float num9 = HeroHelper.StartRecruitingMoneyLimitForClanLeader(leader);
            float num10 = HeroHelper.StartRecruitingMoneyLimit(leader);
            const float num11 = 0.2f, num12 = 1f;
            Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(party);

            float distance = nav.Distance;
            float num14 = Math.Max(num11, distance);
            float num15 = distance > num11 ? num12 / (num12 - num11 + distance) : 1f;
            float num16 = item < 0.6f ? (float)Math.Pow(num15, Math.Pow(0.6f / Math.Max(0.15f, item), 0.3f)) : num15;
            float num18 = t.Params.Men == 0 ? 0f : t.Params.Wounded / (float)t.Params.Men;
            bool flag = party.Army != null && party.AttachedTo == null && party.Army.LeaderParty != party;
            float num17 = 1f;
            if (settlement.IsFortification && num18 > 0.2f)
            {
                num17 = MBMath.Map(num18 - 0.2f, 0f, 0.8f, 1f, 5f);
                if (flag || party.MapEvent != null || party.SiegeEvent != null)
                    num17 *= 0.6f;
            }
            float num19 = 1f;
            if (party.DefaultBehavior == AiBehavior.GoToSettlement &&
                ((settlement == here && here.IsFortification) || (here == null && settlement == party.TargetSettlement)))
                num19 = 1.2f;
            else if (here == null && settlement == party.LastVisitedSettlement)
                num19 = 0.8f;

            float num20 = num18 > 0.2f ? 1f : 0.16f;
            float num21 = Math.Max(0f, food) / foodUse;
            if (foodUse > 0f && (party.BesiegedSettlement == null || num21 <= 1f) && gold > 100 &&
                (settlement.IsTown || (settlement.IsVillage && party.Army == null)))
            {
                float threshold = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege;
                if (num21 < threshold)
                {
                    int num22 = (int)(foodUse * ((num21 < 1f && settlement.IsVillage)
                        ? Campaign.Current.Models.PartyFoodBuyingModel.MinimumDaysFoodToLastWhileBuyingFoodFromVillage
                        : Campaign.Current.Models.PartyFoodBuyingModel.MinimumDaysFoodToLastWhileBuyingFoodFromTown)) + 1;
                    float num23 = threshold * 0.5f;
                    float num24 = num23 - Math.Min(num23, Math.Max(0f, num21 - 1f));
                    float num25 = num22 + 20f * (settlement.IsTown ? 2 : 1) * (num14 > num12 ? 1f : num14 / num12);
                    int val = (int)((gold - 100) / Campaign.Current.Models.PartyFoodBuyingModel.LowCostFoodPriceAverage);
                    num20 += num24 * num24 * 0.093f * (num21 < num23 ? 15f + 0.5f * (num23 - num21) : 1f) *
                             Math.Min(num25, Math.Min(val, settlement.ItemRoster.TotalFood)) / num25;
                }
            }

            float num26 = 0f;
            float num29 = 1f;
            int num27 = 0, num28 = 0;
            if (!settlement.IsCastle && item < 1f && party.GetAvailableWageBudget() > 0)
            {
                AlliedTargeting(t.Behavior).TryGetValue(settlement, out num28);
                if (party.TargetSettlement == settlement || (party.Army != null && party.Army.LeaderParty.TargetSettlement == settlement))
                    num28 = Math.Max(0, num28 - (party.Army?.LeaderPartyAndAttachedPartiesCount ?? 1));
                if (party.Army != null)
                    num28 += party.Army.LeaderPartyAndAttachedPartiesCount;
                if (!party.Party.IsStarving && party.PartyTradeGold > num10 &&
                    (leader.Clan.Leader == leader || leader.Clan.Gold > num9) && num8 > party.PartySizeRatio &&
                    t.Volunteers.TryGetValue(settlement, out var volunteers))
                {
                    num26 = volunteers.Count;
                    if (num26 > 0f)
                        num26 = Math.Min(num26, (float)Math.Floor(party.GetAvailableWageBudget() / volunteers.Wage));
                }
                float num30 = num26 * num15 / (float)Math.Sqrt(1 + num27 + num28);
                float num31 = num30 < 1f ? num30 : (float)Math.Pow(num30, num2);
                num29 = Math.Max(Math.Min(1f, num20), Math.Max(party.MapFaction == settlement.MapFaction ? 0.25f : 0.16f,
                    num * Math.Max(1f, Math.Min(2f, num)) * num31 * (1f - 0.9f * num18) * (1f - 0.9f * num18)));
            }
            float score = 1.6f * num29 * num17 * num20 * num16;
            string early = string.Empty;
            float num32 = 1f;
            if (num26 > 0f && !flag)
                num32 = 1f + ((party.DefaultBehavior == AiBehavior.GoToSettlement && settlement != here && num14 < num11)
                    ? 0.1f * Math.Min(5f, num26) - 0.1f * Math.Min(5f, num26) * (num14 / num11) * (num14 / num11)
                    : 0f);
            float num33 = settlement.IsCastle && !flag && num20 < 1f ? 1.4f : 1f;
            float sell = settlement.IsTown ? t.Sell : 1f;
            if (score >= 8f)
                early = "earlyBreak@need";
            else
                score *= sell * num32 * num33;

            int num34 = party.PrisonRoster.TotalRegulars + 6 * party.PrisonRoster.GetTroopRoster().Count(entry =>
                entry.Character.IsHero && entry.Character.HeroObject.Clan.IsAtWarWith(settlement.MapFaction));
            float num35 = 1f, num36 = 1f;
            if (party.Army != null && party.Army.LeaderParty.AttachedParties.Contains(party))
            {
                if (party.Army.LeaderParty != party)
                    num35 = (party.Army.CohesionThresholdForDispersion - party.Army.Cohesion) / (float)party.Army.CohesionThresholdForDispersion;
                num36 = MobileParty.MainParty != null && party.Army == MobileParty.MainParty.Army ? 0.6f : 0.8f;
                foreach (MobileParty attached in party.Army.LeaderParty.AttachedParties)
                    num34 += attached.PrisonRoster.TotalRegulars + 6 * attached.PrisonRoster.GetTroopRoster().Count(entry =>
                        entry.Character.IsHero && entry.Character.HeroObject.Clan.IsAtWarWith(settlement.MapFaction));
            }
            float num37 = settlement.IsFortification ? 1f + 2f * (num34 / Math.Max(1, prisonLimit)) : 1f;
            float num38 = party.DesiredAiNavigationType == nav.Navigation ? 1.5f : 1f;
            (float o39, float o40, float o41, float o42) = num20 <= 0.5f && t.Owner.TryGetValue(settlement, out var owner) ? owner : (1f, 1f, 1f, 1f);
            float num44 = 1f;
            if (settlement.HasPort && party.Ships.Any())
            {
                float hp = party.Ships.Average(ship => ship.HitPoints / ship.MaxHitPoints);
                if (hp < 0.8f)
                    num44 = hp > 0.6f ? 1.5f : hp > 0.4f ? 1.75f : 3f;
            }
            if (early.Length == 0 && score >= 8f)
                early = "earlyBreak@sell";
            else if (early.Length == 0)
                score *= num19 * num35 * num37 * num36 * o39 * o41 * o40 * o42 * num38 * num44;

            string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
            string factors = "base1.6" + " dist" + Num(num16) + "(d" + Num(distance) + ")" +
                             " food" + Num(num20) + " recruit" + Num(num29) + "(vol" + Num(num26) + ",allied" + num28 + ")" +
                             " wound" + Num(num17) + " visited" + Num(num19) + " sell" + Num(sell) + " recruitNear" + Num(num32) +
                             " castle" + Num(num33) + " prisoners" + Num(num37) + "(" + num34 + "/" + prisonLimit + ")" +
                             " nav" + Num(num38) + " own" + Num(o39 * o40 * o41 * o42) + " army" + Num(num35 * num36) +
                             " ship" + Num(num44) + (early.Length > 0 ? " " + early : string.Empty);
            return (score, factors);
        }

        /// <summary>
        /// 竞价定了以后写一行 DESIRE：这一小时分最高的几个进城候选（原版分、照原版重算的分和因子、灰袍改了什么、最后的分），
        /// 差事分，谁赢了，赢了以后原版会怎么做——赢的就是「正在这里」的聚落时，原版什么也不做、原地待命。
        /// </summary>
        internal static void WriteDesireExplain(MobileParty party, IReadOnlyCollection<(AIBehaviorData, float)> finalScores,
            string intent, float dutyScore)
        {
            if (!Finished.TryGetValue(party.StringId, out TickTrace? t))
                return;
            Finished.Remove(party.StringId);
            Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(party);
            (AIBehaviorData Data, float Score) winner = finalScores.OrderByDescending(entry => entry.Item2).FirstOrDefault();
            string outcome = winner.Data.AiBehavior == AiBehavior.GoToSettlement && winner.Data.Party is Settlement target && target == here
                ? "NO_OP_HOLD_AT_CURRENT"
                : winner.Data.AiBehavior == AiBehavior.GoToSettlement ? "VISIT" : "DUTY_OR_OTHER";
            IEnumerable<string> top = finalScores
                .Where(entry => entry.Item1.AiBehavior == AiBehavior.GoToSettlement && entry.Item1.Party is Settlement)
                .OrderByDescending(entry => entry.Item2)
                .Take(4)
                .Select(entry =>
                {
                    Settlement settlement = (Settlement)entry.Item1.Party;
                    t.Vanilla.TryGetValue(settlement, out float vanilla);
                    (float mirror, string factors) = Mirror(t, settlement);
                    string check = Math.Abs(mirror - vanilla) <= Math.Max(0.002f, vanilla * 0.01f)
                        ? "ok"
                        : "MISMATCH(mirror=" + mirror.ToString("0.0000", CultureInfo.InvariantCulture) + ")";
                    string gwp = t.Gwp.TryGetValue(settlement, out var adjust)
                        ? adjust.Why + "=x" + adjust.Factor.ToString("0.###", CultureInfo.InvariantCulture)
                        : "none";
                    return settlement.StringId + (settlement == here ? "(here)" : string.Empty) +
                           "{vanilla=" + vanilla.ToString("0.0000", CultureInfo.InvariantCulture) + " " + check +
                           " [" + factors + "] gwp=" + gwp + " final=" + entry.Item2.ToString("0.0000", CultureInfo.InvariantCulture) + "}";
                });
            GwpAiDiagnostics.WriteDesire(party,
                "outcome=" + outcome +
                " | winner=" + winner.Data.AiBehavior + "@" + (winner.Data.Party switch
                {
                    Settlement settlement => settlement.StringId,
                    MobileParty mobile => mobile.StringId,
                    _ => "point"
                }) + "=" + winner.Score.ToString("0.0000", CultureInfo.InvariantCulture) +
                " | intent=" + intent + " duty=" + dutyScore.ToString("0.00", CultureInfo.InvariantCulture) +
                " | here=" + (here?.StringId ?? "-") + (party.CurrentSettlement != null ? "(inside)" : here != null ? "(atGate)" : string.Empty) +
                " | party=size" + t.Params.SizeRatio.ToString("0.00", CultureInfo.InvariantCulture) +
                ",wounded" + t.Params.Wounded + "/" + t.Params.Men + ",food" + party.Food.ToString("0.0", CultureInfo.InvariantCulture) +
                ",foodUse" + (-party.FoodChange).ToString("0.00", CultureInfo.InvariantCulture) +
                ",gold" + party.PartyTradeGold + ",prisoners" + party.PrisonRoster.TotalRegulars + "/" + party.Party.PrisonerSizeLimit +
                ",sell" + t.Sell.ToString("0.00", CultureInfo.InvariantCulture) +
                " | visits=" + string.Join(" ", top));
        }
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "CalculatePartyParameters")]
    internal static class GwpWardenVisitTraceParametersPatch
    {
        private static void Postfix(MobileParty mobileParty, (float, float, int, int) __result) =>
            GwpWardenVisitDesire.TraceParameters(mobileParty, __result);
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "CalculateSellItemScore")]
    internal static class GwpWardenVisitTraceSellPatch
    {
        private static void Postfix(MobileParty mobileParty, float __result) =>
            GwpWardenVisitDesire.TraceSell(mobileParty, __result);
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "CalculateBeingSettlementOwnerScores")]
    internal static class GwpWardenVisitTraceOwnerPatch
    {
        private static void Postfix(MobileParty mobileParty, Settlement settlement, (float, float, float, float) __result) =>
            GwpWardenVisitDesire.TraceOwner(mobileParty, settlement, __result);
    }

    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "AddBehaviorTupleWithScore")]
    internal static class GwpWardenVisitTraceScorePatch
    {
        private static void Prefix(Settlement settlement, float visitingNearbySettlementScore) =>
            GwpWardenVisitDesire.TraceVanilla(settlement, visitingNearbySettlementScore);
    }
#else
    internal static partial class GwpWardenVisitDesire
    {
        internal static void BeginTrace(AiVisitSettlementBehavior behavior, MobileParty party) { }
        internal static void EndTrace(MobileParty party) { }
        internal static void TraceCandidates(MobileParty party, IList candidates) { }
        internal static void TraceVolunteers(Settlement settlement, (int, float) result) { }
        internal static void RecordAdjustment(MobileParty party, Settlement settlement, float factor, string why) { }
        internal static void WriteDesireExplain(MobileParty party, IReadOnlyCollection<(AIBehaviorData, float)> finalScores,
            string intent, float dutyScore) { }
    }
#endif
}
