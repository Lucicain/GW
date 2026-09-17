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
        /// （领主/管家加成整段被 <c>LeaderHero != null</c> 挡住），而订单最多 80 人。
        /// 超编速度惩罚是 <c>limit / men - 1</c>，80 人塞进 20 的上限就是 -75%，
        /// 练兵队根本跟不住练兵官。这里按订单量给它一个够用的上限。
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
        /// 这样清点、锁升级、交付三处永远指着同一个名册。
        /// </summary>
        private MobileParty? ResolveOrderPool(MobileParty? trainer)
            => ResolveCohortParty() ?? trainer;

        private void KeepCohortDisposition(MobileParty? cohort)
        {
            if (cohort?.IsActive != true) return;
            try
            {
                cohort.Ai.SetInitiative(CohortAttackInitiative, 1f,
                    CohortInitiativeHours);
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        /// <summary>
        /// 每个订单心跳都跑一次：没有就拉起来，有就续上跟随欲望、补口粮，
        /// 然后把练兵官手里该归订单的人挪进来。
        /// </summary>
        private MobileParty? AdvanceCohort(MobileParty trainer,
            CharacterObject target)
        {
            MobileParty? cohort = ResolveCohortParty();
            if (cohort == null)
            {
                // 队没了但记录还在：这批人在路上折了。重召是对的，但不能不吭声——
                // 玩家付过钱，得知道进度回到了起点。
                if (!string.IsNullOrEmpty(_cohortPartyId))
                {
                    GwpAiDiagnostics.WriteAction(trainer,
                        "PLAYER_TROOP_ORDER_COHORT_LOST",
                        "cohort=" + _cohortPartyId +
                        "; troop=" + _orderedTroopId +
                        "; requested=" + _orderedCount);
                    _cohortPartyId = string.Empty;
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_troop_order_cohort_lost}Word comes from the Grey Wardens: the men set aside for you were lost on the road. They are calling up others."),
                        Colors.Yellow));
                }
                // 空队会被原版当无人队清掉，下一拍我们就会把它误报成"折在路上"。
                // 所以练兵官手里确实有人可拨之前，先不要把队伍立起来。
                if (trainer.MemberRoster.TotalManCount <= CohortTrainerFloor)
                    return null;
                cohort = CreateCohort(trainer);
                if (cohort == null) return null;
            }

            KeepCohortDisposition(cohort);
            GreyWardenPartyDesireBehavior.RequestEscort(cohort, trainer);
            if (cohort.Food <= 0f)
                PoliceResourceManager.ProvisionTemporaryDutyParty(cohort);

            TopUpCohort(trainer, cohort, target);
            return cohort;
        }

        private MobileParty? CreateCohort(MobileParty trainer)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null || trainer?.IsActive != true) return null;

            Settlement? home = trainer.HomeSettlement ??
                               PoliceStats.GetPoliceClan()?.HomeSettlement;
            if (home == null) return null;

            string cohortId;
            do
            {
                cohortId = GwpCommon.TrainingCohortIdPrefix +
                           MBRandom.RandomInt(10000, 99999);
            }
            while (MobileParty.All.Any(party => string.Equals(party.StringId,
                cohortId, StringComparison.OrdinalIgnoreCase)));

            // 固定速度在这里一次定死，之后不再校。原版 CalculateFinalSpeed 对
            // 自定义队是"非零就整个替换最终速度"，替换发生在叠地形之前，所以拿它
            // 比练兵官的 LastCalculatedBaseSpeed（同样是叠地形之前那个数）是同一把尺子。
            float cohortSpeed = Math.Max(CohortSpeedFloor,
                trainer.LastCalculatedBaseSpeed * CohortSpeedMargin);

            MobileParty? cohort = null;
            try
            {
                cohort = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    trainer.Position,
                    1f,
                    home,
                    new TextObject(GwpText.Get(
                        "{=gwp_troop_order_cohort_name}Grey Warden training cohort")),
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
                PoliceResourceManager.ProvisionTemporaryDutyParty(cohort);
                KeepCohortDisposition(cohort);
                GreyWardenPartyDesireBehavior.RequestEscort(cohort, trainer);
            }
            catch (Exception error)
            {
                if (cohort != null)
                    GwpCommon.TryDestroyParty(cohort);
                GwpAiDiagnostics.WriteAction(trainer,
                    "PLAYER_TROOP_ORDER_COHORT_FAILED", error.ToString());
                return null;
            }

            _cohortPartyId = cohortId;
            GwpAiDiagnostics.WriteAction(trainer,
                "PLAYER_TROOP_ORDER_COHORT_RAISED",
                "cohort=" + cohortId +
                "; troop=" + _orderedTroopId +
                "; requested=" + _orderedCount +
                "; sizeLimit=" + Math.Max(20, _orderedCount) +
                "; speed=" + cohortSpeed.ToString("0.00") +
                "; trainerBaseSpeed=" +
                trainer.LastCalculatedBaseSpeed.ToString("0.00"));
            return cohort;
        }

        /// <summary>
        /// 从练兵官名册里把订单该用的人挪进练兵队，上限就是订单人数——练兵队
        /// 只装这张订单，不当第二个军营。取用顺序：已经是成品的先走，其次是能
        /// 练上去的（低级在前，最便宜），最后才是只能拆编重训的下游老兵。
        /// 练兵官手上始终留够 <see cref="CohortTrainerFloor"/> 人。
        /// </summary>
        private void TopUpCohort(MobileParty trainer, MobileParty cohort,
            CharacterObject target)
        {
            int room = _orderedCount - cohort.MemberRoster.TotalManCount;
            if (room <= 0) return;
            int spare = trainer.MemberRoster.TotalManCount - CohortTrainerFloor;
            if (spare <= 0) return;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<TroopRosterElement> pool = trainer.MemberRoster.GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero &&
                    element.Number - element.WoundedNumber > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    (element.Character == target ||
                     CanReachTarget(element.Character, target,
                         new HashSet<string>(visited)) ||
                     CanReachTarget(target, element.Character,
                         new HashSet<string>(visited))))
                .OrderBy(element => element.Character == target ? 0
                    : CanReachTarget(element.Character, target,
                        new HashSet<string>(visited)) ? 1 : 2)
                .ThenBy(element => element.Character.Tier)
                .ThenBy(element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            int moved = 0;
            foreach (TroopRosterElement element in pool)
            {
                int budget = Math.Min(room - moved, spare - moved);
                if (budget <= 0) break;
                int take = Math.Min(
                    Math.Max(0, element.Number - element.WoundedNumber), budget);
                if (take <= 0) continue;
                trainer.MemberRoster.AddToCounts(element.Character, -take,
                    insertAtFront: false, woundedCount: 0);
                cohort.MemberRoster.AddToCounts(element.Character, take,
                    insertAtFront: false, woundedCount: 0);
                moved += take;
            }
            if (moved <= 0) return;

            GwpAiDiagnostics.WriteAction(trainer, "PLAYER_TROOP_ORDER_COHORT_FED",
                "cohort=" + cohort.StringId +
                "; target=" + target.StringId +
                "; moved=" + moved +
                "; cohortMen=" + cohort.MemberRoster.TotalManCount +
                "; ready=" + CountHealthy(cohort, target) +
                "; trainerMen=" + trainer.MemberRoster.TotalManCount);
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

            MobileParty? trainer = ResolveTrainerParty();
            int returned = 0;
            if (trainer?.IsActive == true)
            {
                foreach (TroopRosterElement element in
                         cohort.MemberRoster.GetTroopRoster().ToList())
                {
                    if (element.Character == null || element.Number <= 0) continue;
                    trainer.MemberRoster.AddToCounts(element.Character,
                        element.Number, insertAtFront: false,
                        woundedCount: element.WoundedNumber);
                    returned += element.Number;
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
