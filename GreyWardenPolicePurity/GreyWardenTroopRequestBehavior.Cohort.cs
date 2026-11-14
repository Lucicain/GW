using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家练兵订单的随行练兵队。
    ///
    /// 订单以前直接堆在练兵官自己的名册里：他的名额被占住，超编还要按
    /// <c>limit / men - 1</c> 吃速度惩罚，而这批人本来就不归他指挥。现在订单的人
    /// 全部装在一支独立的无领主队里，跟着练兵官走，练成了再由他照常送给玩家。
    ///
    /// 这支队伍不需要自己的脑子——欲望只有一条：跟住练兵官。攻击倾向压到和使者
    /// 同一档，路上不主动找事。
    /// </summary>
    public sealed partial class GreyWardenTroopRequestBehavior
    {
        /// <summary>练兵官自己至少要留下的人数。订单再急也不能把他抽空。</summary>
        private const int CohortTrainerFloor = 15;

        /// <summary>和使者同一档的低侵略性：会躲、必要时会还手，不主动找事。</summary>
        private const float CohortAttackInitiative = 0.2f;
        private const float CohortInitiativeHours = 2f;

        /// <summary>
        /// 练兵队必须比练兵官快，不然它带着一队步兵永远吊在后面。建队时按练兵官
        /// 当时的基速取一次，之后不再改。下限取和高速追截队一样的 5.0。
        /// </summary>
        private const float CohortSpeedMargin = 1.15f;
        private const float CohortSpeedFloor = 5f;

        /// <summary>
        /// 无领主自定义队在原版 <c>DefaultPartySizeLimitModel</c> 里只有 20 的裸底数
        /// （领主/管家加成整段被 <c>LeaderHero != null</c> 挡住）。用订单总人数扩大上限，避免超编拖慢练兵队。
        /// </summary>
        internal static int GetCohortSizeLimit(MobileParty? party)
        {
            if (_instance == null || !GwpCommon.IsTrainingCohortParty(party))
                return 0;
            if (!string.Equals(party!.StringId, _instance._cohortPartyId,
                    StringComparison.OrdinalIgnoreCase))
                return 0;
            return Math.Max(20, _instance._orderedCount);
        }

        private MobileParty? ResolveCohortParty()
        {
            if (string.IsNullOrEmpty(_cohortPartyId)) return null;
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate?.StringId, _cohortPartyId,
                    StringComparison.OrdinalIgnoreCase));
            return party?.IsActive == true ? party : null;
        }

        /// <summary>
        /// 订单人数点在哪里。练兵队立起来之前（或者刚被打散）落回练兵官，
        /// 清点和交付都指着这个名册；升级预留另外把练兵官手上补足缺额所需的成品也留住。
        /// </summary>
        private MobileParty? ResolveOrderPool(MobileParty? trainer)
            => ResolveCohortParty() ?? trainer;

        private void KeepCohortDisposition(MobileParty? cohort)
        {
            if (cohort?.IsActive != true) return;
            cohort.Ai.SetInitiative(CohortAttackInitiative, 1f,
                CohortInitiativeHours);
        }

        /// <summary>
        /// 练兵官可以被订单占用时才跑：没有练兵队就拉起来，然后照看一次。
        /// </summary>
        private MobileParty? AdvanceCohort(MobileParty trainer)
        {
            MobileParty? cohort = ResolveCohortParty();
            if (cohort == null)
            {
                // 队没了但记录还在：这批人在路上折了。重召是对的，但不能不吭声——
                // 订单已经付过费用，告诉玩家要重新备兵。
                if (!string.IsNullOrEmpty(_cohortPartyId))
                {
                    GwpAiDiagnostics.WriteAction(trainer,
                        "PLAYER_TROOP_ORDER_COHORT_LOST",
                        "cohort=" + _cohortPartyId +
                        "; composition=" + DescribeComposition() +
                        "; requested=" + _orderedCount);
                    _cohortPartyId = string.Empty;
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get(GwpTextCatalog.gwp_troop_order_cohort_lost),
                        Colors.Yellow));
                }
                // 空队会被原版当无人队清掉，下一拍我们就会把它误报成"折在路上"。
                // 所以练兵官手里确实有人可拨之前，先不要把队伍立起来。
                if (trainer.MemberRoster.TotalManCount <= CohortTrainerFloor)
                    return null;
                cohort = CreateCohort(trainer);
                if (cohort == null) return null;
                // 已有的练兵队由 TendCohort 在门禁之前照看；新立的这一拍补一次。
                TendCohort(trainer, cohort);
            }
            return cohort;
        }

        /// <summary>
        /// 练兵队自己的照看：续跟随、压侵略性、和练兵官双向换兵。每小时都跑，
        /// **不经过** <c>TryReservePartyForPlayerRequest</c>——练兵官在打仗、在军团里时
        /// 那道门是关的，以前练兵队因此一连几天没人管，跟随和侵略性过期、战后收进来的
        /// 外来兵也退不回去。任一方在战斗中时不动名册，等打完再换。
        /// </summary>
        private void TendCohort(MobileParty trainer, MobileParty cohort)
        {
            KeepCohortDisposition(cohort);
            GreyWardenPartyDesireBehavior.RequestEscort(cohort, trainer);
            if (trainer.MapEvent != null || cohort.MapEvent != null) return;
            TopUpCohort(trainer, cohort);
        }

        private MobileParty? CreateCohort(MobileParty trainer)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null || trainer?.IsActive != true) return null;

            Settlement? home = trainer.HomeSettlement ??
                               PoliceStats.GetPoliceClan()?.HomeSettlement;
            if (home == null) return null;
            // 练兵队在练兵官身边出生；练兵官在海上又借不出船，就先不建。
            if (PoliceResourceManager.IsStrandedAtSea(trainer)) return null;

            string cohortId = GwpPartyIdentity.Next(GwpCommon.TrainingCohortIdPrefix);

            // 固定速度在这里一次定死，之后不再校。原版 CalculateFinalSpeed 对
            // 自定义队是"非零就整个替换最终速度"，替换发生在叠地形之前，所以拿它
            // 比练兵官的 LastCalculatedBaseSpeed（同样是叠地形之前那个数）是同一把尺子。
            float cohortSpeed = Math.Max(CohortSpeedFloor,
                trainer.LastCalculatedBaseSpeed * CohortSpeedMargin);

            MobileParty? cohort = null;
            cohort = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                trainer.Position,
                1f,
                home,
                new TextObject(GwpText.Get(
                    GwpTextCatalog.gwp_troop_order_cohort_name)),
                policeClan,
                policeClan.DefaultPartyTemplate,
                // owner 必须留空：原版 ApplyEffects 会朝 party.Owner 收升级费，
                // 填人进去就是让他自掏腰包替订单买单。
                null,
                string.Empty,
                string.Empty,
                cohortSpeed,
                false);
            if (cohort == null) return null;

            cohort.StringId = cohortId;
            cohort.ActualClan = policeClan;
            cohort.MemberRoster.Clear();
            cohort.ItemRoster.Clear();
            // 船向练兵官借，按订单人数借：练兵队出生时是空的，之后才补到订单量。
            PoliceResourceManager.OutfitTemporaryDutyParty(cohort, trainer, _orderedCount);
            KeepCohortDisposition(cohort);
            GreyWardenPartyDesireBehavior.RequestEscort(cohort, trainer);

            _cohortPartyId = cohortId;
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_COHORT_RAISED",
                "cohort=" + cohortId +
                "; composition=" + DescribeComposition() +
                "; requested=" + _orderedCount +
                "; sizeLimit=" + Math.Max(20, _orderedCount) +
                "; speed=" + cohortSpeed.ToString("0.00") +
                "; trainerBaseSpeed=" +
                trainer.LastCalculatedBaseSpeed.ToString("0.00"));
            return cohort;
        }

        private void TopUpCohort(MobileParty trainer, MobileParty cohort)
        {
            TransferExactOrderTroops(trainer, cohort);
            CharacterObject? target = TrainingTarget(cohort).Troop;
            if (target == null)
            {
                foreach (TroopRosterElement entry in cohort.MemberRoster.GetTroopRoster().Where(entry =>
                    !entry.Character.IsHero && entry.Number > RequestedCount(entry.Character)).ToList())
                    ReturnCohortSurplus(cohort, trainer, entry.Character, entry.Number - RequestedCount(entry.Character));
                return;
            }

            bool CanTrainForTarget(CharacterObject troop) =>
                CanReachTarget(troop, target, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            bool CanRetrainForTarget(CharacterObject troop) =>
                CanReachTarget(target, troop, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            bool IsTrainingStock(CharacterObject troop) => GwpCommon.IsGreyWardenTroop(troop) &&
                (CanTrainForTarget(troop) || CanRetrainForTarget(troop));

            int needed = RequestedCount(target) - CountHealthy(cohort, target);
            int retained = 0;
            var returns = new List<TroopRosterElement>();
            foreach (TroopRosterElement entry in cohort.MemberRoster.GetTroopRoster()
                .Where(entry => entry.Number > 0 && !entry.Character.IsHero)
                .OrderBy(entry => CanTrainForTarget(entry.Character) ? 0 : 1).ThenBy(entry => entry.Character.Tier))
            {
                int reserved = Math.Min(entry.Number, RequestedCount(entry.Character));
                int healthySurplus = Math.Max(0, entry.Number - entry.WoundedNumber - reserved);
                int keep = entry.Character != target && IsTrainingStock(entry.Character) ? Math.Min(healthySurplus, needed - retained) : 0;
                retained += keep;
                int excess = entry.Number - reserved - keep;
                if (excess > 0) returns.Add(new TroopRosterElement(entry.Character) { Number = excess });
            }

            int spare = trainer.MemberRoster.TotalManCount - CohortTrainerFloor;
            foreach (TroopRosterElement entry in trainer.MemberRoster.GetTroopRoster()
                .Where(entry => entry.Character != target && !entry.Character.IsHero && IsTrainingStock(entry.Character))
                .OrderBy(entry => CanTrainForTarget(entry.Character) ? 0 : 1).ThenBy(entry => entry.Character.Tier).ToList())
            {
                int budget = Math.Min(needed - retained, spare);
                if (budget <= 0) break;
                // 同一兵种可能是后续订单的成品，只取超出该项预留的部分。
                int reserved = Math.Max(0, RequestedCount(entry.Character) - CountHealthy(cohort, entry.Character));
                int take = Math.Min(budget, entry.Number - entry.WoundedNumber - reserved);
                int moved = GwpTroopTransfer.Move(trainer.MemberRoster, cohort.MemberRoster,
                    entry.Character, take, healthyOnly: true).Number;
                retained += moved;
                spare -= moved;
            }
            if (cohort.MemberRoster.TotalManCount > returns.Sum(entry => entry.Number))
                foreach (TroopRosterElement entry in returns)
                    ReturnCohortSurplus(cohort, trainer, entry.Character, entry.Number);
        }

        private static void ReturnCohortSurplus(MobileParty cohort, MobileParty trainer, CharacterObject troop, int count)
        {
            int index = cohort.MemberRoster.FindIndexOfTroop(troop);
            TroopRosterElement current = cohort.MemberRoster.GetElementCopyAtIndex(index);
            var selected = new TroopRosterElement(troop) { Number = count, WoundedNumber = Math.Min(count, current.WoundedNumber) };
            GwpTroopTransfer.MoveSelectedBatch(cohort.Party, cohort.MemberRoster, trainer.MemberRoster, selected);
        }

        private void TransferExactOrderTroops(MobileParty trainer, MobileParty cohort)
        {
            int spare = trainer.MemberRoster.TotalManCount - CohortTrainerFloor;
            foreach (var line in _orderLines)
            {
                if (spare <= 0) break;
                int missing = line.Count - CountHealthy(cohort, line.Troop);
                spare -= GwpTroopTransfer.Move(trainer.MemberRoster, cohort.MemberRoster, line.Troop,
                    Math.Min(spare, missing), healthyOnly: true).Number;
            }
        }



        /// <summary>
        /// 解散练兵队：剩下的人原样还给练兵官，队伍销毁。订单交付、取消、
        /// 清空都走这里，任何一条路都不许把人丢在地图上。
        /// </summary>
        private void DisbandCohort(string reason)
        {
            MobileParty? cohort = ResolveCohortParty();
            _cohortPartyId = string.Empty;
            if (cohort == null) return;

            // 练兵官缺席（被俘、队伍被打散）时交给最近的灰袍领主，
            // 不把人连同名册一起清掉。
            MobileParty? trainer = ResolveTrainerParty() ??
                GwpCommon.FindNearestGreyWardenLordParty(cohort);
            int returned = 0;
            if (trainer?.IsActive == true)
            {
                foreach (TroopRosterElement element in
                         cohort.MemberRoster.GetTroopRoster().ToList())
                {
                    if (element.Character == null || element.Number <= 0) continue;
                    returned += GwpTroopTransfer.Move(cohort.MemberRoster, trainer.MemberRoster,
                        element.Character, element.Number).Number;
                }
            }
            cohort.MemberRoster.Clear();

            GwpAiDiagnostics.WriteAction(cohort,
                "PLAYER_TROOP_ORDER_COHORT_DISBANDED",
                "reason=" + reason +
                "; returned=" + returned +
                "; trainer=" + (trainer?.StringId ?? "none"));
            GwpCommon.TryDestroyParty(cohort);
        }
    }
}
