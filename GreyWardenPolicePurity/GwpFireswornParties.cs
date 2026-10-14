using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
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
    /// 信众村的三种无领主队（用户裁定 2026-10-07、2026-10-08）：
    /// <list type="bullet">
    /// <item>传教队：祭司已是本地最强的信众村，把狂信者跋山涉水送到别的村子，到了就并入那里的守村队，队伍没了。
    /// 先送已经有火誓者、比例比家里低得多的村子，再送还没有火誓者的。只送人，不运货。</item>
    /// <item>运械队：把家里的兵器护甲装进背包，运给武装率比家里低得多的村子，到了卸进那里的军械库、押车的人并入当地，队伍没了。
    /// 背包里的东西路上被打掉就归胜方。</item>
    /// <item>卖货队：照原版村民队常驻在村里，把货仓的东西运进城卖掉，钱带回来进灰袍公库，卖完回村。</item>
    /// </list>
    /// 都在出发前从守村队补人（先拉最高级的），再从家里的军械库拿最好的那几套装进自己的账。
    /// 路上和送信队一样专注自己的事：贴身的弱敌照打，不为远处的目标丢下差事（用户裁定 2026-10-07）。
    /// 传教队、运械队走不到（超时、目标出事）就回家，并入家里。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        private void TendFaithParties()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (FaithParty record in _faithParties.Values.ToList())
            {
                MobileParty party = FindParty(record.PartyId)!;
                Settlement home = Settlement.Find(record.HomeId);
                if (party.MapEvent != null)
                    continue;

                switch (record.Phase)
                {
                    case FaithPartyPhase.Home:
                        if (party.CurrentSettlement == home && party.Ai.DoNotMakeNewDecisions)
                            break;
                        if (HasArrived(party, home))
                            Park(party, home);
                        else
                            Travel(party, home);
                        break;

                    case FaithPartyPhase.Outbound:
                        Settlement target = Settlement.Find(record.TargetId);
                        if (now - record.PhaseHour > GwpTuning.Firesworn.FaithPartyTravelTimeoutHours ||
                            !IsTripTargetValid(target))
                        {
                            GwpAiDiagnostics.WriteFaith("FAITH_PARTY_TURNED_BACK",
                                "party=" + party.StringId + "; kind=" + record.Kind + "; home=" + record.HomeId +
                                "; target=" + record.TargetId + "; hours=" + (now - record.PhaseHour).ToString("0"));
                            SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                            break;
                        }
                        if (!HasArrived(party, target))
                        {
                            Travel(party, target);
                            break;
                        }
                        if (record.Kind == FaithPartyKind.Market)
                            SellCargo(record, party, target);
                        else
                            JoinVillage(record, party, target);
                        break;

                    case FaithPartyPhase.Returning:
                        // 村里正在打仗也直接并入，人加进正在打的民兵队（JoinGuard、LodgePrisonersAndStrangers）。
                        if (!HasArrived(party, home))
                        {
                            Travel(party, home);
                            break;
                        }
                        if (record.Kind == FaithPartyKind.Market)
                            ArriveHome(record, party, home);
                        else
                            JoinVillage(record, party, home);
                        break;
                }
            }
        }

        private static void Travel(MobileParty party, Settlement destination)
        {
            GwpWardenDispatchBehavior.KeepCourierDisposition(party);
            GreyWardenPartyDesireBehavior.RequestVisit(party, destination, GwpTuning.Firesworn.FaithPartyVisitScore);
        }

        /// <summary>原版领主追击敌军看的范围：遭遇距离 × 45（C#反编译 v1.4.8 AiEngagePartyBehavior）。</summary>
        private static float EngageRadius =>
            Campaign.Current.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty * 45f;

        /// <summary>
        /// 身边和灰袍交战、能当敌人打的队：匪、逃兵、交战势力的领主和巡逻队；村民、商队、民兵、驻军不算，
        /// 在聚落里、正在打仗、在海上的不算；挂在军团或攻城营里的只算带头那支。
        /// </summary>
        private static List<MobileParty> HostilesAround(Vec2 position, float radius)
        {
            Clan police = PoliceStats.GetPoliceClan();
            List<MobileParty> found = new List<MobileParty>();
            LocatableSearchData<MobileParty> search = MobileParty.StartFindingLocatablesAroundPosition(position, radius);
            for (MobileParty other = MobileParty.FindNextLocatable(ref search); other != null; other = MobileParty.FindNextLocatable(ref search))
            {
                if (!other.IsActive || other.CurrentSettlement != null || other.MapEvent != null || other.IsCurrentlyAtSea ||
                    other.IsVillager || other.IsCaravan || other.IsMilitia || other.IsGarrison ||
                    GwpStrength.CombatGroupOf(other) != other ||
                    !FactionManager.IsAtWarAgainstFaction(police, other.MapFaction) ||
                    position.Distance(other.Position.ToVec2()) > radius)
                    continue;
                found.Add(other);
            }
            return found;
        }

        /// <summary>
        /// 路上最强的敌人有多强：出发村和目的地附近（原版追击范围内）的敌人里战斗组最强的那个。
        /// 卖货队、传教队按它补人（<see cref="ManEscort"/>）。
        /// </summary>
        private static float RoadDanger(Settlement home, Settlement target) =>
            HostilesAround(home.GetPosition2D, EngageRadius).Concat(HostilesAround(target.GetPosition2D, EngageRadius))
                .Select(GwpStrength.CombatGroup)
                .DefaultIfEmpty(0f)
                .Max();

        /// <summary>
        /// 火誓者无领主队的编制上限：不低于队里现有的人数（<see cref="GwpPartySizeLimitModel"/> 用）。不是火誓者无领主队返回 0。
        /// 原版无领主自定义队只有 20 的裸底数，超编按「上限 / 人数 − 1」减速（C#反编译 v1.4.8 DefaultPartySizeLimitModel、
        /// DefaultPartySpeedCalculatingModel.GetOverPartySizeEffect），25 人的队慢两成
        /// （实机诊断 2026-10-08：76 人的队地图速度只剩 1.0，什么都追不上）。
        /// </summary>
        internal static int FaithPartySizeLimit(MobileParty? party) =>
            party != null && _instance != null && _instance._faithParties.ContainsKey(party.StringId)
                ? party.MemberRoster.TotalManCount
                : 0;

        /// <summary>
        /// 无领主队打赢抓到的英雄当场放走，只押普通俘虏回村（用户裁定 2026-10-08）。英雄从来不进地牢、不会被烧。
        /// </summary>
        private static void ReleaseHeroPrisoners(MobileParty party)
        {
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster().Where(element => element.Character.IsHero).ToList())
            {
                GwpAiDiagnostics.WriteFaith("PATROL_RELEASED_HERO", "party=" + party.StringId + "; hero=" + element.Character.StringId);
                EndCaptivityAction.ApplyByReleasedAfterBattle(element.Character.HeroObject);
            }
        }

        private static bool IsTripTargetValid(Settlement target) =>
            !FactionManager.IsAtWarAgainstFaction(PoliceStats.GetPoliceClan(), target.MapFaction) &&
            (target.IsVillage ? IsVillageQuiet(target) : !target.IsUnderSiege);

        /// <summary>
        /// 让卖货队待在村子里面、看不见、不动，照原版村民队在家的样子。
        /// 不冻住的话，原版会把这种无领主的自定义队拉出村，停在村口不动（实机诊断 2026-10-06：
        /// 回村 5 小时后出村，此后 56 小时一直在村口 Hold，「去本村」的竞价赢了也不落地）。
        /// 下次出发时下达新意图，<see cref="GreyWardenPartyDesireBehavior.RequestImmediateRethink"/> 会解冻。
        /// </summary>
        private static void Park(MobileParty party, Settlement settlement)
        {
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            if (party.CurrentSettlement != settlement)
            {
                if (party.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(party);
                EnterSettlementAction.ApplyForParty(party, settlement);
            }
            party.Ai.SetDoNotMakeNewDecisions(true);
        }

        private static void SetPhase(FaithParty record, FaithPartyPhase phase, string targetId)
        {
            record.Phase = phase;
            record.TargetId = targetId;
            record.PhaseHour = CampaignTime.Now.ToHours;
        }

        /// <summary>
        /// 建一支无领主队。不借船、不配口粮：只走陆路，不吃饭（GwpCommon.IsLeaderlessDutyParty）。
        /// 以前照临时办差队向最近的灰袍领主借真船，常驻队从不解散，船就再也还不回去
        /// （用户反馈 2026-10-07：灰袍船异常少、狂信者队有船）。
        /// </summary>
        private MobileParty CreateFaithParty(FaithVillage row, Settlement home, FaithPartyKind kind)
        {
            Clan police = PoliceStats.GetPoliceClan();
            TextObject name = kind == FaithPartyKind.Mission
                ? GwpText.Create(GwpTextCatalog.gwp_fs_mission_party)
                : kind == FaithPartyKind.Balance
                    ? GwpText.Create(GwpTextCatalog.gwp_fs_balance_party, "VILLAGE", home.Name)
                    : GwpText.Create(GwpTextCatalog.gwp_fs_caravan_party, "VILLAGE", home.Name);
            // 所有者留空，和纠察队、追截队、练兵队一样（PolicePatrolBehavior 当年因为填了族长出过事）：
            // 填族长的话，这支队一打输，被俘豁免就把族长当败将强制逃亡，原版逃亡先销毁她正带着的部队（实机诊断 2026-10-08）。
            MobileParty party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                home.GatePosition, 1f, home, name, police, police.DefaultPartyTemplate, null, "", "", 4f, false);
            party.StringId = GwpPartyIdentity.Next(GwpIds.FaithPartyPrefix);
            party.ActualClan = police;
            party.MemberRoster.Clear();
            // 打赢抢到的钱：原版只给有领主英雄、或开了交易账户的队（C#反编译 v1.4.8 MapEventParty.CommitGoldChanges），
            // 没有账户就丢了。开个账户，落村时交给村子、进公库（用户裁定 2026-10-08：无人队上交缴获给村子，村子再给灰袍）。
            // 原版拿这个账户花钱（买粮、雇人）都要求有领主英雄或是商队，开了不会乱花。
            party.InitializePartyTrade(0);
            _faithParties[party.StringId] = new FaithParty
            {
                PartyId = party.StringId,
                HomeId = row.VillageId,
                Kind = kind,
                Phase = FaithPartyPhase.Home,
                TargetId = row.VillageId,
                PhaseHour = CampaignTime.Now.ToHours
            };
            GwpAiDiagnostics.WriteFaith("FAITH_PARTY_FORMED", "party=" + party.StringId + "; kind=" + kind + "; home=" + home.StringId);
            return party;
        }

        /// <summary>补到 <paramref name="size"/> 人（<see cref="DrawMen"/>），再从家里军械库拿最好的那几套。返回拿了几件。</summary>
        private int ManParty(MobileParty party, FaithVillage row, Settlement home, int size)
        {
            DrawMen(party, row, home, size);
            _plans.Remove(row.VillageId);
            return TakeBestSets(row.VillageId, party);
        }

        /// <summary>
        /// 补人，补到 <paramref name="size"/>：拉守村队最高级的（用户裁定 2026-10-08：出门的队伍精锐度要高）。
        /// </summary>
        private void DrawMen(MobileParty party, FaithVillage row, Settlement home, int size)
        {
            int need = Math.Max(0, size - party.MemberRoster.TotalManCount);
            MobileParty? militia = home.MilitiaPartyComponent?.MobileParty;
            TroopRoster guard = GuardOf(row.VillageId);
            foreach (TroopRosterElement element in guard.GetTroopRoster().OrderByDescending(entry => entry.Character.Tier).ToList())
            {
                if (need <= 0)
                    break;
                int take = Math.Min(need, element.Number);
                // 自有记录只记人数，实际伤兵与经验仍由村内实体民兵持有。
                if (militia?.IsActive == true && militia.MapEvent == null)
                    GwpTroopTransfer.Move(militia.MemberRoster, party.MemberRoster, element.Character, take);
                else
                    party.MemberRoster.AddToCounts(element.Character, take);
                guard.AddToCounts(element.Character, -take);
                need -= take;
            }
            PushGuard(row, home);
            ReconcilePriestSlots(row);
        }

        /// <summary>
        /// 卖货队、传教队补人：先补到原来的人数，路上最强的敌人（<see cref="RoadDanger"/>）比我们强就一个个加人，
        /// 加到打得过为止；最多 25 人，家里守村队至少留 5 人（用户裁定 2026-10-08：这两种队人数固定，路上老挨打）。
        /// 以前卖货队固定 8 人、传教队 10 人（实机诊断 2026-10-08：卖货队一趟被山贼打剩 3 人，下一趟被逃兵全灭）。
        /// </summary>
        private int ManEscort(MobileParty party, FaithVillage row, Settlement home, Settlement target, int baseSize, out float danger)
        {
            danger = RoadDanger(home, target);
            return ManToBeat(party, row, home, baseSize, GwpTuning.Firesworn.EscortMaximumSize, danger);
        }

        /// <summary>
        /// 先补到 <paramref name="minimum"/> 人，比 <paramref name="danger"/> 弱就一个个加人，加到打得过或到 <paramref name="maximum"/> 为止，
        /// 家里守村队至少留 5 人；最后从家里军械库拿最好的那几套（装备只拿一次）。返回拿了几件。
        /// </summary>
        private int ManToBeat(MobileParty party, FaithVillage row, Settlement home, int minimum, int maximum, float danger)
        {
            int size = minimum;
            DrawMen(party, row, home, size);
            while (GwpStrength.Party(party) <= danger && size < maximum &&
                   Sworn(row) > GwpTuning.Firesworn.MissionMinimumLeftBehind)
                DrawMen(party, row, home, ++size);
            _plans.Remove(row.VillageId);
            return TakeBestSets(row.VillageId, party);
        }

        private void Dispatch(FaithParty record, MobileParty party, Settlement target)
        {
            SetPhase(record, FaithPartyPhase.Outbound, target.StringId);
            Travel(party, target);
        }

        /// <summary>
        /// 每天一次，只在信众村：祭司已是最强就看要不要派传教队；再看要不要派运械队、卖货队。
        /// 传教队、运械队照比武大会派（<see cref="IsFestivalDay"/>），卖货队隔几天看货值。
        /// </summary>
        private void DispatchFaithParties()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement home = Settlement.Find(row.VillageId);
                if (!IsVillageQuiet(home))
                    continue;
                if (FindLivingPriest(row) != null && !HasRitual(home))
                {
                    TryDispatchMission(row, home, now);
                    TryDispatchBalance(row, home, now);
                    TryDispatchMarket(row, home, now);
                }
            }
        }

        private List<Settlement> NearbyTargets(Settlement home, float maxDistance, FaithPartyKind kind)
        {
            Clan police = PoliceStats.GetPoliceClan();
            return Village.All.Select(village => village.Settlement)
                .Where(settlement => settlement != home && IsVillageQuiet(settlement) && !HasRitual(settlement) &&
                    !FactionManager.IsAtWarAgainstFaction(police, settlement.MapFaction) &&
                    home.GetPosition2D.Distance(settlement.GetPosition2D) <= maxDistance &&
                    !_faithParties.Values.Any(entry => entry.Kind == kind && entry.Phase == FaithPartyPhase.Outbound &&
                        string.Equals(entry.TargetId, settlement.StringId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // ---------------------------------------------------------------- 传教队

        /// <summary>
        /// 传教去哪儿（用户裁定 2026-10-08）：附近有火誓者、还没有祭司、比例还不到来祭司门槛的村子里，挑比例最高的，
        /// 把它推过门槛再换下一座；都没有就去还没有火誓者的村子。
        /// 原来挑比例最低的：灰袍领主路过就留下两户信众，这种 1% 的村子到处都是，每次换一座，哪座都扶不起来（实机诊断 2026-10-08）。
        /// 只去信众村还没满的巡区，先去最空的那片；新开的村子挑离巡区圆心最近的（用户裁定 2026-10-15：
        /// 信众村和灰袍领主一样分散，尽量长在灰袍的巡逻点一带）。
        /// </summary>
        private Settlement? MissionTarget(Settlement home)
        {
            Dictionary<Settlement, int> beats = GwpPatrolBeats.Assignments();
            List<Settlement> nearby = NearbyTargets(home, GwpTuning.Firesworn.MissionMaxDistance, FaithPartyKind.Mission)
                .Where(settlement => FaithRegionHasRoom(settlement, beats))
                .ToList();
            Settlement? growing = nearby
                .Where(settlement => _villages.TryGetValue(settlement.StringId, out FaithVillage? other) &&
                                     FindLivingPriest(other) == null &&
                                     SwornShare(other, settlement) < GwpTuning.Firesworn.PriestZealotShare)
                .OrderBy(settlement => FaithRegionLoad(settlement, beats))
                .ThenByDescending(settlement => SwornShare(_villages[settlement.StringId], settlement))
                .FirstOrDefault();
            return growing ?? nearby
                .Where(settlement => !_villages.ContainsKey(settlement.StringId))
                .OrderBy(settlement => FaithRegionLoad(settlement, beats))
                .ThenBy(settlement => FaithRegionOf(settlement, beats)!.GetPosition2D.Distance(settlement.GetPosition2D))
                .FirstOrDefault();
        }

        /// <summary>
        /// 派传教队。祭司已是本地最强的信众村之间，派过的次数要拉平：派得比别人多的先不派，等别人追上来
        /// （用户裁定 2026-10-07：成为最强后不能自顾自不断往外送人）。新成为最强的村子从 0 算起，
        /// 名单是所有祭司最强的村子，暂时派不出队的也算——缺人靠运械队押车和守村队补兵补上。
        /// 照比武大会派（用户裁定 2026-10-07）。
        /// </summary>
        private void TryDispatchMission(FaithVillage row, Settlement home, double now)
        {
            if (!IsPriestStrongest(row, home) ||
                row.MissionsSent > _villages.Values
                    .Where(other => IsPriestStrongest(other, Settlement.Find(other.VillageId)))
                    .Min(other => other.MissionsSent) ||
                Sworn(row) < GwpTuning.Firesworn.MissionSize + GwpTuning.Firesworn.MissionMinimumLeftBehind ||
                _faithParties.Values.Count(entry => entry.Kind == FaithPartyKind.Mission) >= GwpTuning.Firesworn.MaxActiveMissions ||
                !IsFestivalDay(row, row.LastMission, now))
                return;
            Settlement? target = MissionTarget(home);
            if (target == null)
                return;
            MobileParty party = CreateFaithParty(row, home, FaithPartyKind.Mission);
            int kits = ManEscort(party, row, home, target, GwpTuning.Firesworn.MissionSize, out float danger);
            row.LastMission = now;
            row.MissionsSent++;
            Dispatch(_faithParties[party.StringId], party, target);
            GwpAiDiagnostics.WriteFaith("MISSION_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; target=" + target.StringId +
                "; targetHasSworn=" + _villages.ContainsKey(target.StringId) + "; men=" + party.MemberRoster.TotalManCount +
                "; kitItems=" + kits + "; danger=" + danger.ToString("0") + "; strength=" + GwpStrength.Party(party).ToString("0") +
                "; homeSworn=" + Sworn(row) + "; missionsSent=" + row.MissionsSent +
                "; distance=" + home.GetPosition2D.Distance(target.GetPosition2D).ToString("0"));
        }

        // ---------------------------------------------------------------- 运械队

        /// <summary>
        /// 运械队要补给目标村的人和兵器（用户裁定 2026-10-07：运械队算周围信众村之间的人数差和武器差来平衡）。
        /// 两边都按大小拉平：人按户数比分两边守村队的总人数，兵器按两边要配的人数分拿到主兵器的总套数。
        /// 兵器只在目标的武装率比家里低得够多时才算。
        /// </summary>
        private (int men, int sets) BalanceNeed(FaithVillage row, Settlement home, Settlement target)
        {
            FaithVillage other = _villages[target.StringId];
            float hearths = home.Village.Hearth + target.Village.Hearth;
            int men = hearths <= 0f ? 0
                : (int)Math.Round((Sworn(row) + Sworn(other)) * target.Village.Hearth / hearths) - Sworn(other);
            ArmoryPlan homePlan = PlanOf(home.StringId);
            ArmoryPlan targetPlan = PlanOf(target.StringId);
            int sets = 0;
            if (targetPlan.Sets.Count > 0 &&
                ArmedShare(home.StringId) - ArmedShare(target.StringId) >= GwpTuning.Firesworn.BalanceMinimumArmedGap)
            {
                int homeArmed = homePlan.Sets.Count(kit => !kit[KitMain].IsEmpty);
                int targetArmed = targetPlan.Sets.Count(kit => !kit[KitMain].IsEmpty);
                sets = (int)Math.Round((homeArmed + targetArmed) * targetPlan.Sets.Count /
                                       (float)Math.Max(1, homePlan.Sets.Count + targetPlan.Sets.Count)) - targetArmed;
            }
            return (Math.Max(0, men), Math.Max(0, sets));
        }

        /// <summary>
        /// 家里比附近有火誓者的村子人多或兵器好得够多，就派运械队：押车的人至少 5 个、人数差多少带多少（到了并入当地守村队），
        /// 背包里装上让两边武装率拉平所差的那几套（<see cref="LoadBalanceCargo"/>）。去差得最多的那座。照比武大会派（用户裁定 2026-10-07）。
        /// 只送有祭司的信众村：运械是祭司来了才解锁的服务（用户裁定 2026-10-07）。
        /// </summary>
        private void TryDispatchBalance(FaithVillage row, Settlement home, double now)
        {
            if (Sworn(row) < GwpTuning.Firesworn.BalanceEscortSize + GwpTuning.Firesworn.MissionMinimumLeftBehind ||
                !IsFestivalDay(row, row.LastBalance, now))
                return;
            (Settlement target, int men, int sets)? best = NearbyTargets(home, GwpTuning.Firesworn.BalanceMaxDistance, FaithPartyKind.Balance)
                .Where(IsPreachedVillage)
                .Select(settlement =>
                {
                    (int men, int sets) need = BalanceNeed(row, home, settlement);
                    return ((Settlement target, int men, int sets)?)(settlement, need.men, need.sets);
                })
                .Where(entry => entry!.Value.men >= GwpTuning.Firesworn.BalanceMinimumMen ||
                                entry!.Value.sets >= GwpTuning.Firesworn.BalanceMinimumSets)
                .OrderByDescending(entry => entry!.Value.men + entry!.Value.sets)
                .FirstOrDefault();
            if (best == null)
                return;
            Settlement target = best.Value.target;
            int escort = Math.Min(Math.Max(GwpTuning.Firesworn.BalanceEscortSize, best.Value.men),
                Sworn(row) - GwpTuning.Firesworn.MissionMinimumLeftBehind);
            MobileParty party = CreateFaithParty(row, home, FaithPartyKind.Balance);
            int kits = ManParty(party, row, home, escort);
            int cargo = LoadBalanceCargo(home.StringId, target.StringId, party);
            row.LastBalance = now;
            Dispatch(_faithParties[party.StringId], party, target);
            GwpAiDiagnostics.WriteFaith("BALANCE_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; target=" + target.StringId +
                "; menNeed=" + best.Value.men + "; setNeed=" + best.Value.sets +
                "; men=" + party.MemberRoster.TotalManCount + "; escortKitItems=" + kits + "; cargoSets=" + cargo +
                "; cargoValue=" + party.ItemRoster.TotalValue + "; homeGuard=" + Sworn(row) +
                "; targetGuard=" + Sworn(_villages[target.StringId]) +
                "; homeArmed=" + ArmedShare(home.StringId).ToString("0.00") +
                "; targetArmed=" + ArmedShare(target.StringId).ToString("0.00"));
        }

        // ---------------------------------------------------------------- 到了就并入

        /// <summary>
        /// 传教队、运械队到了：自己那几套和背包里的东西卸给村里，人并入当地守村队，队伍没了
        /// （用户裁定 2026-10-07：进村相当于销毁，加入当地宗教）。当地还没有火誓者记录就建一条；有了祭司才算信众村。
        /// 人只进守村队、可以超过上限，不加信众户（用户裁定 2026-10-08）；传教队到了别的村就在那里开一场集会，
        /// 信众户靠这场集会涨，和祭司本地传教一样。押回的普通俘虏进地牢，英雄俘虏放走。
        /// 走不到折回家时也是这样并入家里。
        /// </summary>
        private void JoinVillage(FaithParty record, MobileParty party, Settlement village)
        {
            FaithVillage row = EnsureVillage(village);
            int before = row.Households;
            int kits = ReturnPartyLedger(party, village.StringId) + UnloadKits(party, village.StringId);
            int joined = Absorb(row, party, village, out int prisoners, out int militiaJoined, out int gold);
            bool backHome = string.Equals(village.StringId, record.HomeId, StringComparison.OrdinalIgnoreCase);
            bool preaching = record.Kind == FaithPartyKind.Mission && !backHome && IsVillageQuiet(village) && !HasRitual(village);
            if (preaching)
                StartCongregation(row, village, "mission:" + record.HomeId);
            GwpSettlementNotices.Post(village, party, record.Kind == FaithPartyKind.Mission
                ? GwpText.Get(GwpTextCatalog.gwp_notice_mission, "COUNT", joined)
                : GwpText.Get(GwpTextCatalog.gwp_notice_balance, "COUNT", kits));
            _faithParties.Remove(party.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            DestroyPartyAction.Apply(null, party);
            ReconcilePriestSlots(row);
            UpdatePriestPower(row);
            GwpAiDiagnostics.WriteFaith(record.Kind == FaithPartyKind.Mission ? "MISSION_JOINED" : "BALANCE_DELIVERED",
                "party=" + party.StringId + "; home=" + record.HomeId + "; village=" + village.StringId +
                "; backHome=" + backHome + "; preaching=" + preaching + "; prisoners=" + prisoners + "; gold=" + gold +
                "; hours=" + (CampaignTime.Now.ToHours - record.PhaseHour).ToString("0") +
                "; joined=" + joined + "; militiaJoined=" + militiaJoined + "; households=" + before + "->" + row.Households +
                "; guard=" + Sworn(row) + "; kitItems=" + kits +
                "; share=" + SwornShare(row, village).ToString("0.00") + "; armed=" + ArmedShare(village.StringId).ToString("0.00") +
                "; priest=" + (FindLivingPriest(row) != null));
        }

        /// <summary>
        /// 一支队伍在村里落脚：背包进货仓，俘虏进地牢、非灰袍兵进原版民兵（<see cref="LodgePrisonersAndStrangers"/>），
        /// 钱进公库，灰袍兵进守村队。返回进守村队的人数。队伍本身由调用方处理。
        /// </summary>
        private int Absorb(FaithVillage row, MobileParty party, Settlement village,
            out int prisoners, out int militiaJoined, out int gold)
        {
            ItemRoster goods = ItemsOf(_goods, village.StringId);
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                goods.AddToCounts(element.EquipmentElement, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            }
            prisoners = LodgePrisonersAndStrangers(party, village, out militiaJoined);
            gold = party.PartyTradeGold;
            party.PartyTradeGold = 0;
            CreditTreasury(gold);
            int joined = 0;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
                if (IsGuardTroop(element.Character))
                    joined += JoinGuard(row, village, element.Character, element.Number, party.MemberRoster);
            return joined;
        }

        /// <summary>离这支队伍最近的、能去落脚的信众村（有祭司、安静、不与灰袍交战）；没有就是 null。</summary>
        internal static Settlement? NearestFaithVillage(MobileParty party) =>
            _instance?._villages.Keys.Select(Settlement.Find)
                .Where(village => village != null && IsPreachedVillage(village) && IsTripTargetValid(village))
                .OrderBy(village => village.GetPosition2D.DistanceSquared(party.GetPosition2D))
                .FirstOrDefault();

        /// <summary>
        /// 灰袍别的临时队在信众村落脚、解散（用户裁定 2026-10-14：追截队主队装不下的人去最近的信众村加入守村队，不进城解散）。
        /// 不是信众村就返回 false，由调用方照旧处理。
        /// </summary>
        internal static bool SettleInFaithVillage(MobileParty party, Settlement village)
        {
            if (_instance == null || !IsPreachedVillage(village))
                return false;
            FaithVillage row = _instance.EnsureVillage(village);
            int joined = _instance.Absorb(row, party, village, out int prisoners, out int militiaJoined, out int gold);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            DestroyPartyAction.Apply(null, party);
            _instance.ReconcilePriestSlots(row);
            _instance.UpdatePriestPower(row);
            GwpAiDiagnostics.WriteFaith("DETACHMENT_SETTLED",
                "party=" + party.StringId + "; village=" + village.StringId + "; joined=" + joined +
                "; militiaJoined=" + militiaJoined + "; prisoners=" + prisoners + "; gold=" + gold);
            return true;
        }

        // ---------------------------------------------------------------- 卖货队

        /// <summary>这座村子的卖货队：在且在家就用它，没有就新建、停在村里。不在家（出门或回程中）返回 null。</summary>
        private MobileParty? ReadyMarketParty(FaithVillage row, Settlement home)
        {
            FaithParty? record = _faithParties.Values.FirstOrDefault(entry => entry.Kind == FaithPartyKind.Market &&
                string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase));
            if (record != null)
                return record.Phase == FaithPartyPhase.Home && FindParty(record.PartyId)!.CurrentSettlement == home
                    ? FindParty(record.PartyId)
                    : null;
            MobileParty party = CreateFaithParty(row, home, FaithPartyKind.Market);
            Park(party, home);
            return party;
        }

        /// <summary>货仓里东西值够了、离上次出车够久，就把货运到本村的贸易城镇。</summary>
        private void TryDispatchMarket(FaithVillage row, Settlement home, double now)
        {
            Clan police = PoliceStats.GetPoliceClan();
            if (!_goods.TryGetValue(row.VillageId, out ItemRoster? goods) ||
                goods.TotalValue < GwpTuning.Firesworn.CaravanMinimumGoodsValue ||
                now - row.LastMarket < GwpTuning.Firesworn.CaravanIntervalDays * 24d ||
                Sworn(row) < GwpTuning.Firesworn.CaravanSize + GwpTuning.Firesworn.MissionMinimumLeftBehind)
                return;
            Settlement? town = home.Village.TradeBound;
            if (town == null || town.IsUnderSiege || FactionManager.IsAtWarAgainstFaction(police, town.MapFaction))
                return;

            MobileParty? party = ReadyMarketParty(row, home);
            if (party == null)
                return;
            int kits = ManEscort(party, row, home, town, GwpTuning.Firesworn.CaravanSize, out float danger);
            int value = goods.TotalValue;
            party.ItemRoster.Add(goods);
            goods.Clear();
            row.LastMarket = now;
            FaithParty cart = _faithParties[party.StringId];
            cart.Stops = 0;
            Dispatch(cart, party, town);
            GwpAiDiagnostics.WriteFaith("MARKET_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; town=" + town.StringId +
                "; cargoValue=" + value + "; men=" + party.MemberRoster.TotalManCount + "; kitItems=" + kits +
                "; danger=" + danger.ToString("0") + "; strength=" + GwpStrength.Party(party).ToString("0"));
        }

        /// <summary>卖货队回村：钱进公库，自己那几套还给村里的军械库，没卖掉的放回货仓，停在村里。</summary>
        /// <summary>
        /// 无领主队进村落脚时交人（传教队、运械队到了就并入，卖货队回家）：押着的普通俘虏进地牢，英雄当场放走；
        /// 队里不是灰袍兵种的人并进这座村的原版民兵，和村里别的原版民兵一样，祭司最强以后会被烧。灰袍兵由调用方并进守村队。
        /// 出门打赢了，原版把败方押着的普通俘虏直接编进 AI 胜方的队伍（C#反编译 v1.4.8 MapEvent.LootDefeatedPartyPrisoners、
        /// MapEventParty.RosterToReceiveLootMembers），这些就是队里的非灰袍兵。以前回村只并灰袍兵，这些人随队伍一起销毁，
        /// 卖货队常驻就一直带着（实机诊断 2026-10-08：打赢劫匪后 25→28 人，回村只并入 25）。
        /// 原版民兵人数就是民兵队名单上的人数（Settlement.Militia），直接加进名单不会被原版改回；民兵正在打仗也加，下一轮模拟就上场
        /// （见 <see cref="JoinGuard"/>）。没有民兵队时并不进去，人随队伍走。
        /// （用户裁定 2026-10-08：出门的队回来全部并入守村总数，非灰袍的本来就可能被烧；抓的俘虏进地牢）
        /// </summary>
        private int LodgePrisonersAndStrangers(MobileParty party, Settlement village, out int militiaJoined)
        {
            ReleaseHeroPrisoners(party);
            TroopRoster dungeon = RosterOf(_dungeons, village.StringId);
            int prisoners = 0;
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster().ToList())
            {
                dungeon.AddToCounts(element.Character, element.Number);
                party.PrisonRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
                prisoners += element.Number;
            }
            militiaJoined = 0;
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.IsActive != true)
                return prisoners;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster()
                         .Where(entry => !entry.Character.IsHero && !IsGuardTroop(entry.Character)).ToList())
            {
                militiaJoined += GwpTroopTransfer.Move(party.MemberRoster, militia.MemberRoster,
                    element.Character, element.Number).Number;
            }
            return prisoners;
        }

        private void ArriveHome(FaithParty record, MobileParty party, Settlement home)
        {
            int prisoners = LodgePrisonersAndStrangers(party, home, out int militiaJoined);
            int proceeds = party.PartyTradeGold;
            party.PartyTradeGold = 0;
            CreditTreasury(proceeds);
            int kits = ReturnPartyLedger(party, home.StringId) + UnloadKits(party, home.StringId);
            ItemRoster goods = ItemsOf(_goods, home.StringId);
            int unsold = 0;
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.IsFood)
                    continue;
                goods.AddToCounts(element.EquipmentElement, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
                unsold += element.Amount;
            }
            SetPhase(record, FaithPartyPhase.Home, record.HomeId);
            Park(party, home);
            GwpAiDiagnostics.WriteFaith("FAITH_PARTY_HOME",
                "party=" + party.StringId + "; home=" + home.StringId + "; men=" + party.MemberRoster.TotalManCount +
                "; prisoners=" + prisoners + "; militiaJoined=" + militiaJoined +
                "; proceedsToTreasury=" + proceeds + "; kitItemsBack=" + kits + "; unsoldBack=" + unsold);
        }

        /// <summary>
        /// 在城里卖货：照原版村民队卖货（C#反编译 v1.4.8 SellGoodsForTradeAction.ApplyInternal），
        /// 按城镇收购价、城镇金库买得起多少卖多少；卖不掉的带回村。钱先放在队伍身上，回村才进公库。
        /// </summary>
        private void SellCargo(FaithParty record, MobileParty party, Settlement town)
        {
            int proceeds = 0, sold = 0;
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.IsFood)
                    continue;
                int price = town.Town.GetItemPrice(element.EquipmentElement, party, isSelling: true);
                int amount = price <= 0 ? 0 : Math.Min(element.Amount, town.Town.Gold / price);
                if (amount <= 0)
                    continue;
                town.Town.ChangeGold(-amount * price);
                town.ItemRoster.AddToCounts(element.EquipmentElement, amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -amount);
                proceeds += amount * price;
                sold += amount;
            }
            party.PartyTradeGold += proceeds;
            record.Stops++;
            if (sold > 0)
                GwpSettlementNotices.Post(town, party, GwpText.Get(GwpTextCatalog.gwp_notice_market, "COUNT", sold));
            int unsoldValue = NonFoodValue(party.ItemRoster);
            // 一座城的金库常常只有两三万，一车俘虏装备能值几十万（实机诊断 2026-10-06：装车估值 32.7 万只卖出 9%，
            // 把城镇金库卖空）。卖不完就去附近还有钱的城接着卖，最多 3 座。
            Settlement? next = unsoldValue >= GwpTuning.Firesworn.CaravanMinimumGoodsValue &&
                               record.Stops < GwpTuning.Firesworn.CaravanMaxTowns
                ? Town.AllTowns.Select(entry => entry.Settlement)
                    .Where(settlement => settlement != town && !settlement.IsUnderSiege &&
                        settlement.Town.Gold >= GwpTuning.Firesworn.CaravanMinimumTownGold &&
                        !FactionManager.IsAtWarAgainstFaction(PoliceStats.GetPoliceClan(), settlement.MapFaction))
                    .OrderBy(settlement => town.GetPosition2D.Distance(settlement.GetPosition2D))
                    .FirstOrDefault()
                : null;
            if (next != null)
                SetPhase(record, FaithPartyPhase.Outbound, next.StringId);
            else
                SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
            GwpAiDiagnostics.WriteFaith("MARKET_SOLD",
                "party=" + party.StringId + "; town=" + town.StringId + "; sold=" + sold + "; proceeds=" + proceeds +
                "; townGoldLeft=" + town.Town.Gold + "; unsoldValue=" + unsoldValue + "; stop=" + record.Stops +
                "; next=" + (next?.StringId ?? "home"));
        }
    }
}
