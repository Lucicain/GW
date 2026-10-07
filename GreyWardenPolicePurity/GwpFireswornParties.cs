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
    /// 信众村的四种无领主队（用户裁定 2026-10-07、2026-10-08）：
    /// <list type="bullet">
    /// <item>传教队：祭司已是本地最强的信众村，把狂信者跋山涉水送到别的村子，到了就并入那里的守村队，队伍没了。
    /// 先送已经有火誓者、比例比家里低得多的村子，再送还没有火誓者的。只送人，不运货。</item>
    /// <item>运械队：把家里的兵器护甲装进背包，运给武装率比家里低得多的村子，到了卸进那里的军械库、押车的人并入当地，队伍没了。
    /// 背包里的东西路上被打掉就归胜方。</item>
    /// <item>卖货队：照原版村民队常驻在村里，把货仓的东西运进城卖掉，钱带回来进灰袍公库，卖完回村。</item>
    /// <item>巡逻队：守村队超出上限的人分批出门，在村子周围巡逻抓匪，回村把俘虏交进地牢，人回到守村队。</item>
    /// </list>
    /// 都在出发前从守村队补人（先拉最高级的），再从家里的军械库拿最好的那几套装进自己的账。
    /// 路上和送信队一样专注自己的事：贴身的弱敌照打，不为远处的目标丢下差事（用户裁定 2026-10-07）。巡逻队例外，它的差事就是打。
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

                    case FaithPartyPhase.Outbound when record.Kind == FaithPartyKind.Patrol:
                        if (now - record.PhaseHour >= GwpTuning.Firesworn.PatrolHours ||
                            party.MemberRoster.TotalHealthyCount < record.Men * GwpTuning.Firesworn.PatrolReturnHealthyShare ||
                            party.PrisonRoster.TotalManCount >= party.Party.PrisonerSizeLimit || !IsVillageQuiet(home))
                        {
                            SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                            break;
                        }
                        Patrol(party, home);
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

        /// <summary>
        /// 巡逻：照原版巡逻队围着本村转（原版 PatrolPartiesCampaignBehavior 下注 PatrolAroundPoint，圆心是本城和属村），
        /// 见了匪就打。主动性拉满，打不过就躲（原版 SetInitiative 第二项）。
        /// </summary>
        private static void Patrol(MobileParty party, Settlement home)
        {
            party.Ai.SetInitiative(1f, 1f, GwpTuning.Firesworn.PatrolHours);
            GreyWardenPartyDesireBehavior.RequestBeat(party, home, GwpTuning.Firesworn.FaithPartyVisitScore);
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
                ? GwpText.Create("{=gwp_fs_mission_party}Disciples of the Eternal Fire")
                : kind == FaithPartyKind.Balance
                    ? GwpText.Create("{=gwp_fs_balance_party}Armsbearers of {VILLAGE}", "VILLAGE", home.Name)
                    : kind == FaithPartyKind.Patrol
                        ? GwpText.Create("{=gwp_fs_patrol_party}Fire Watch of {VILLAGE}", "VILLAGE", home.Name)
                        : GwpText.Create("{=gwp_fs_caravan_party}Zealots of {VILLAGE}", "VILLAGE", home.Name);
            MobileParty party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                home.GatePosition, 1f, home, name, police, police.DefaultPartyTemplate, police.Leader, "", "", 4f, false);
            party.StringId = GwpIds.FaithPartyPrefix + MBRandom.RandomInt(100000, 999999);
            party.ActualClan = police;
            party.MemberRoster.Clear();
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

        /// <summary>
        /// 从守村队补人，补到 <paramref name="size"/>，先拉最高级的（用户裁定 2026-10-08：出门的队伍精锐度要高）；
        /// 再从家里军械库拿最好的那几套。
        /// </summary>
        private int ManParty(MobileParty party, FaithVillage row, Settlement home, int size)
        {
            TroopRoster guard = GuardOf(row.VillageId);
            int need = Math.Max(0, size - party.MemberRoster.TotalManCount);
            foreach (TroopRosterElement element in guard.GetTroopRoster().OrderByDescending(entry => entry.Character.Tier).ToList())
            {
                if (need <= 0)
                    break;
                int take = Math.Min(need, element.Number);
                guard.AddToCounts(element.Character, -take);
                party.MemberRoster.AddToCounts(element.Character, take);
                need -= take;
            }
            PushGuard(row, home);
            ReconcilePriestSlots(row);
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
                if (FindLivingPriest(row) == null || !IsVillageQuiet(home) || HasRitual(home))
                    continue;
                TryDispatchMission(row, home, now);
                TryDispatchBalance(row, home, now);
                TryDispatchMarket(row, home, now);
                DispatchPatrols(row, home);
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
        /// 把它推过门槛再换下一座；都没有就去最近的还没有火誓者的村子。
        /// 原来挑比例最低的：灰袍领主路过就留下两户信众，这种 1% 的村子到处都是，每次换一座，哪座都扶不起来（实机诊断 2026-10-08）。
        /// </summary>
        private Settlement? MissionTarget(Settlement home)
        {
            List<Settlement> nearby = NearbyTargets(home, GwpTuning.Firesworn.MissionMaxDistance, FaithPartyKind.Mission);
            Settlement? growing = nearby
                .Where(settlement => _villages.TryGetValue(settlement.StringId, out FaithVillage? other) &&
                                     FindLivingPriest(other) == null &&
                                     SwornShare(other, settlement) < GwpTuning.Firesworn.PriestZealotShare)
                .OrderByDescending(settlement => SwornShare(_villages[settlement.StringId], settlement))
                .FirstOrDefault();
            return growing ?? nearby
                .Where(settlement => !_villages.ContainsKey(settlement.StringId))
                .OrderBy(settlement => home.GetPosition2D.Distance(settlement.GetPosition2D))
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
            int kits = ManParty(party, row, home, GwpTuning.Firesworn.MissionSize);
            row.LastMission = now;
            row.MissionsSent++;
            Dispatch(_faithParties[party.StringId], party, target);
            GwpAiDiagnostics.WriteFaith("MISSION_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; target=" + target.StringId +
                "; targetHasSworn=" + _villages.ContainsKey(target.StringId) + "; men=" + party.MemberRoster.TotalManCount +
                "; kitItems=" + kits + "; homeSworn=" + Sworn(row) + "; missionsSent=" + row.MissionsSent +
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

        // ---------------------------------------------------------------- 巡逻队

        /// <summary>
        /// 守村队超出上限的人分批出去巡逻，一批 10–25 人，够一批就派（用户裁定 2026-10-08：多批次派出，在村子周围抓匪回来烧）。
        /// </summary>
        private void DispatchPatrols(FaithVillage row, Settlement home)
        {
            int spare = Sworn(row) - GuardCap(row);
            while (spare >= GwpTuning.Firesworn.PatrolMinimumSize)
            {
                MobileParty party = CreateFaithParty(row, home, FaithPartyKind.Patrol);
                int kits = ManParty(party, row, home, Math.Min(spare, GwpTuning.Firesworn.PatrolMaximumSize));
                FaithParty record = _faithParties[party.StringId];
                record.Men = party.MemberRoster.TotalManCount;
                SetPhase(record, FaithPartyPhase.Outbound, home.StringId);
                Patrol(party, home);
                spare -= record.Men;
                GwpAiDiagnostics.WriteFaith("PATROL_DISPATCHED",
                    "party=" + party.StringId + "; home=" + home.StringId + "; men=" + record.Men + "; kitItems=" + kits +
                    "; guard=" + Sworn(row) + "; cap=" + GuardCap(row));
            }
        }

        // ---------------------------------------------------------------- 到了就并入

        /// <summary>
        /// 传教队、运械队到了，巡逻队回了村：自己那几套和背包里的东西卸给村里，人并入当地守村队，队伍没了
        /// （用户裁定 2026-10-07：进村相当于销毁，加入当地宗教）。当地还没有火誓者记录就建一条；有了祭司才算信众村。
        /// 人只进守村队、可以超过上限，不加信众户（用户裁定 2026-10-08）；传教队到了别的村就在那里开一场集会，
        /// 信众户靠这场集会涨，和祭司本地传教一样。巡逻队押回的普通俘虏进地牢，英雄俘虏放走。
        /// 走不到折回家时也是这样并入家里。
        /// </summary>
        private void JoinVillage(FaithParty record, MobileParty party, Settlement village)
        {
            FaithVillage row = EnsureVillage(village);
            int before = row.Households;
            int kits = ReturnPartyLedger(party, village.StringId) + UnloadKits(party, village.StringId);
            ItemRoster goods = ItemsOf(_goods, village.StringId);
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                goods.AddToCounts(element.EquipmentElement, element.Amount);
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            }
            int joined = 0;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
                if (IsGuardTroop(element.Character))
                    joined += JoinGuard(row, village, element.Character, element.Number);
            int prisoners = 0;
            TroopRoster dungeon = RosterOf(_dungeons, village.StringId);
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero)
                {
                    EndCaptivityAction.ApplyByReleasedAfterBattle(element.Character.HeroObject);
                    continue;
                }
                dungeon.AddToCounts(element.Character, element.Number);
                party.PrisonRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
                prisoners += element.Number;
            }
            bool backHome = string.Equals(village.StringId, record.HomeId, StringComparison.OrdinalIgnoreCase);
            bool preaching = record.Kind == FaithPartyKind.Mission && !backHome && IsVillageQuiet(village) && !HasRitual(village);
            if (preaching)
                StartCongregation(row, village, "mission:" + record.HomeId);
            GwpSettlementNotices.Post(village, party, record.Kind == FaithPartyKind.Mission
                ? GwpText.Get("{=gwp_notice_mission}Brought {COUNT} sworn to stay", "COUNT", joined)
                : record.Kind == FaithPartyKind.Patrol
                    ? GwpText.Get("{=gwp_notice_patrol}Back from the watch with {COUNT} prisoners", "COUNT", prisoners)
                    : GwpText.Get("{=gwp_notice_balance}Brought {COUNT} pieces of arms", "COUNT", kits));
            _faithParties.Remove(party.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            DestroyPartyAction.Apply(null, party);
            ReconcilePriestSlots(row);
            UpdatePriestPower(row);
            GwpAiDiagnostics.WriteFaith(record.Kind == FaithPartyKind.Mission ? "MISSION_JOINED"
                    : record.Kind == FaithPartyKind.Patrol ? "PATROL_RETURNED" : "BALANCE_DELIVERED",
                "party=" + party.StringId + "; home=" + record.HomeId + "; village=" + village.StringId +
                "; backHome=" + backHome + "; preaching=" + preaching + "; prisoners=" + prisoners +
                "; hours=" + (CampaignTime.Now.ToHours - record.PhaseHour).ToString("0") +
                "; joined=" + joined + "; households=" + before + "->" + row.Households + "; guard=" + Sworn(row) + "; kitItems=" + kits +
                "; share=" + SwornShare(row, village).ToString("0.00") + "; armed=" + ArmedShare(village.StringId).ToString("0.00") +
                "; priest=" + (FindLivingPriest(row) != null));
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
            int kits = ManParty(party, row, home, GwpTuning.Firesworn.CaravanSize);
            int value = goods.TotalValue;
            party.ItemRoster.Add(goods);
            goods.Clear();
            row.LastMarket = now;
            FaithParty cart = _faithParties[party.StringId];
            cart.Stops = 0;
            Dispatch(cart, party, town);
            GwpAiDiagnostics.WriteFaith("MARKET_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; town=" + town.StringId +
                "; cargoValue=" + value + "; men=" + party.MemberRoster.TotalManCount + "; kitItems=" + kits);
        }

        /// <summary>卖货队回村：钱进公库，自己那几套还给村里的军械库，没卖掉的放回货仓，停在村里。</summary>
        private void ArriveHome(FaithParty record, MobileParty party, Settlement home)
        {
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
                GwpSettlementNotices.Post(town, party, GwpText.Get("{=gwp_notice_market}Sold {COUNT} goods", "COUNT", sold));
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
