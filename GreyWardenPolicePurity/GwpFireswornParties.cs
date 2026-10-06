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

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 每座信众村一支信众队（用户裁定 2026-10-07：传教队和狂信者队合成一支，在村庄之间传递兵器和信众人数）：
    /// <list type="bullet">
    /// <item>向外传教：祭司已是本地最强的信众村，把狂信者连同兵器送到别的村子，到了就并入那里的守村队和军械库，
    /// 这支队就没了（用户裁定 2026-10-07：进村相当于销毁，加入当地宗教）。先送已经有火誓者的村子——
    /// 谁的火誓者比例、武装率比家里低得多就送谁——再送还没有火誓者的。</item>
    /// <item>卖货：把村里货仓的东西运进城卖掉，钱带回来进灰袍公库（用户裁定：收益最后都进公库），卖完回村。</item>
    /// </list>
    /// 照原版村民队（C#反编译 v1.4.8 VillagerCampaignBehavior）：在家待在村子里、出发前从守村队补人。
    /// 路上和送信队一样专注自己的事：贴身的弱敌照打，不为远处的目标丢下差事（用户裁定 2026-10-07）。
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
                                "party=" + party.StringId + "; home=" + record.HomeId + "; target=" + record.TargetId +
                                "; hours=" + (now - record.PhaseHour).ToString("0"));
                            SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                            break;
                        }
                        if (!HasArrived(party, target))
                        {
                            Travel(party, target);
                            break;
                        }
                        if (target.IsVillage)
                            JoinVillage(record, party, target);
                        else
                            SellCargo(record, party, target);
                        break;

                    case FaithPartyPhase.Returning:
                        if (!HasArrived(party, home))
                        {
                            Travel(party, home);
                            break;
                        }
                        ArriveHome(record, party, home);
                        break;
                }
            }
        }

        private static void Travel(MobileParty party, Settlement destination)
        {
            GwpWardenDispatchBehavior.KeepCourierDisposition(party);
            GreyWardenPartyDesireBehavior.RequestVisit(party, destination, GwpTuning.Firesworn.FaithPartyVisitScore);
        }

        private static bool IsTripTargetValid(Settlement target) =>
            !FactionManager.IsAtWarAgainstFaction(PoliceStats.GetPoliceClan(), target.MapFaction) &&
            (target.IsVillage ? IsVillageQuiet(target) : !target.IsUnderSiege);

        private void ArriveHome(FaithParty record, MobileParty party, Settlement home)
        {
            int proceeds = party.PartyTradeGold;
            party.PartyTradeGold = 0;
            CreditTreasury(proceeds);
            // 没送出去的兵器护甲回本村军械库，没卖掉的别的东西回货仓。
            int unsold = UnloadKits(party, home.StringId);
            ItemRoster goods = ItemsOf(_goods, home.StringId);
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
                "; proceedsToTreasury=" + proceeds + "; unsoldBack=" + unsold);
        }

        /// <summary>
        /// 让信众队待在村子里面、看不见、不动，照原版村民队在家的样子。
        /// 不冻住的话，原版会把这种无领主的自定义队拉出村，停在村口不动（实机诊断 2026-10-06：
        /// 传教队回村 5 小时后出村，此后 56 小时一直在村口 Hold，「去本村」的竞价赢了也不落地）。
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

        /// <summary>这座村子的信众队：在且在家就用它，没有就新建。不在家（出门或回程中）返回 null。</summary>
        private MobileParty? ReadyFaithParty(FaithVillage row, Settlement home)
        {
            FaithParty? record = _faithParties.Values.FirstOrDefault(entry =>
                string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase));
            if (record != null)
                return record.Phase == FaithPartyPhase.Home && FindParty(record.PartyId)!.CurrentSettlement == home
                    ? FindParty(record.PartyId)
                    : null;

            Clan police = PoliceStats.GetPoliceClan();
            MobileParty party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                home.GatePosition, 1f, home, GwpText.Create("{=gwp_fs_caravan_party}Zealots of {VILLAGE}", "VILLAGE", home.Name),
                police, police.DefaultPartyTemplate, police.Leader, "", "", 4f, false);
            party.StringId = GwpIds.FaithPartyPrefix + MBRandom.RandomInt(100000, 999999);
            party.ActualClan = police;
            party.MemberRoster.Clear();
            // 不借船、不配口粮：信众队只走陆路，不吃饭（GwpCommon.IsLeaderlessDutyParty）。
            // 以前照临时办差队向最近的灰袍领主借真船，常驻队从不解散，船就再也还不回去
            // （用户反馈 2026-10-07：灰袍船异常少、狂信者队有船）。
            Park(party, home);
            _faithParties[party.StringId] = new FaithParty
            {
                PartyId = party.StringId,
                HomeId = row.VillageId,
                Phase = FaithPartyPhase.Home,
                TargetId = row.VillageId,
                PhaseHour = CampaignTime.Now.ToHours
            };
            GwpAiDiagnostics.WriteFaith("FAITH_PARTY_FORMED", "party=" + party.StringId + "; home=" + home.StringId);
            return party;
        }

        /// <summary>出发前从守村队补人，补到 <paramref name="size"/>；先补最低级的。</summary>
        private void TopUp(MobileParty party, FaithVillage row, Settlement home, int size)
        {
            TroopRoster guard = GuardOf(row.VillageId);
            int need = Math.Max(0, size - party.MemberRoster.TotalManCount);
            foreach (TroopRosterElement element in guard.GetTroopRoster().OrderBy(entry => entry.Character.Tier).ToList())
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
        }

        /// <summary>每天一次：祭司已是本地最强的信众村先看要不要向外传教，不传就看要不要卖货。</summary>
        private void DispatchFaithParties()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement home = Settlement.Find(row.VillageId);
                if (FindLivingPriest(row) == null || !IsVillageQuiet(home) || HasRitual(home))
                    continue;
                if (!TryDispatchMission(row, home, now))
                    TryDispatchMarket(row, home, now);
            }
        }

        // ---------------------------------------------------------------- 向外传教

        /// <summary>这座村的火誓者武装率：守村队、信众队里狂信者和新兵有几成拿到了主兵器。</summary>
        private float ArmedShare(string villageId)
        {
            ArmoryPlan plan = PlanOf(villageId);
            return plan.Sets.Count == 0 ? 0f : plan.Sets.Count(kit => !kit[KitMain].IsEmpty) / (float)plan.Sets.Count;
        }

        /// <summary>
        /// 向外传教要去哪儿：先看附近已经有火誓者的村子，比家里缺得多（火誓者比例、武装率两项的差加起来够大）
        /// 就去最缺的；都不缺就去最近的还没有火誓者的村子（用户裁定 2026-10-07：先传有狂信者的，再传其他）。
        /// </summary>
        private Settlement? MissionTarget(FaithVillage row, Settlement home)
        {
            Clan police = PoliceStats.GetPoliceClan();
            float homeShare = SwornShare(row, home);
            float homeArmed = ArmedShare(row.VillageId);
            List<Settlement> nearby = Village.All.Select(village => village.Settlement)
                .Where(settlement => settlement != home && IsVillageQuiet(settlement) && !HasRitual(settlement) &&
                    !FactionManager.IsAtWarAgainstFaction(police, settlement.MapFaction) &&
                    home.GetPosition2D.Distance(settlement.GetPosition2D) <= GwpTuning.Firesworn.MissionMaxDistance &&
                    !_faithParties.Values.Any(entry => entry.Phase == FaithPartyPhase.Outbound &&
                        string.Equals(entry.TargetId, settlement.StringId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Settlement? needy = nearby
                .Where(settlement => _villages.TryGetValue(settlement.StringId, out FaithVillage? other) &&
                                     Sworn(other) < ZealotCap(settlement))
                .Select(settlement => (settlement, need: homeShare - SwornShare(_villages[settlement.StringId], settlement) +
                                                         homeArmed - ArmedShare(settlement.StringId)))
                .Where(entry => entry.need >= GwpTuning.Firesworn.MissionMinimumNeed)
                .OrderByDescending(entry => entry.need)
                .Select(entry => entry.settlement)
                .FirstOrDefault();
            return needy ?? nearby
                .Where(settlement => !_villages.ContainsKey(settlement.StringId))
                .OrderBy(settlement => home.GetPosition2D.Distance(settlement.GetPosition2D))
                .FirstOrDefault();
        }

        private bool TryDispatchMission(FaithVillage row, Settlement home, double now)
        {
            if (!IsPriestStrongest(row, home) ||
                now - row.LastMission < GwpTuning.Firesworn.MissionIntervalDays * 24d ||
                Sworn(row) < GwpTuning.Firesworn.MissionSize + GwpTuning.Firesworn.MissionMinimumLeftBehind ||
                _faithParties.Values.Count(entry => entry.Phase == FaithPartyPhase.Outbound &&
                    Settlement.Find(entry.TargetId).IsVillage) >= GwpTuning.Firesworn.MaxActiveMissions)
                return false;
            Settlement? target = MissionTarget(row, home);
            if (target == null)
                return false;
            MobileParty? party = ReadyFaithParty(row, home);
            if (party == null)
                return false;
            TopUp(party, row, home, GwpTuning.Firesworn.MissionSize);
            row.LastMission = now;
            SetPhase(_faithParties[party.StringId], FaithPartyPhase.Outbound, target.StringId);
            Travel(party, target);
            GwpAiDiagnostics.WriteFaith("MISSION_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; target=" + target.StringId +
                "; targetHasSworn=" + _villages.ContainsKey(target.StringId) + "; men=" + party.MemberRoster.TotalManCount +
                "; homeSworn=" + Sworn(row) + "; homeArmed=" + ArmedShare(row.VillageId).ToString("0.00") +
                "; distance=" + home.GetPosition2D.Distance(target.GetPosition2D).ToString("0"));
            return true;
        }

        /// <summary>
        /// 到了：家里的兵器护甲匀一部分过来（<see cref="TransferMissionSets"/>），人并入当地守村队，
        /// 这支队没了（用户裁定 2026-10-07）。当地还没有火誓者记录就建一条；有了祭司才算信众村。
        /// </summary>
        private void JoinVillage(FaithParty record, MobileParty party, Settlement village)
        {
            FaithVillage row = EnsureVillage(village);
            int before = Sworn(row);
            int travellers = party.MemberRoster.GetTroopRoster()
                .Where(element => IsArmedTroop(element.Character)).Sum(element => element.Number);
            int kits = TransferMissionSets(record.HomeId, village.StringId, travellers);
            int joined = 0;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
                if (IsGuardTroop(element.Character))
                    joined += AddToGuard(row, village, element.Character, element.Number);
            _faithParties.Remove(party.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            DestroyPartyAction.Apply(null, party);
            ReconcilePriestSlots(row);
            UpdatePriestPower(row);
            GwpAiDiagnostics.WriteFaith("MISSION_JOINED",
                "party=" + party.StringId + "; home=" + record.HomeId + "; village=" + village.StringId +
                "; joined=" + joined + "; sworn=" + before + "->" + Sworn(row) + "; kitItems=" + kits +
                "; share=" + SwornShare(row, village).ToString("0.00") + "; priest=" + (FindLivingPriest(row) != null));
        }

        // ---------------------------------------------------------------- 卖货

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

            MobileParty? party = ReadyFaithParty(row, home);
            if (party == null)
                return;
            TopUp(party, row, home, GwpTuning.Firesworn.CaravanSize);
            int value = goods.TotalValue;
            for (int i = goods.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = goods.GetElementCopyAtIndex(i);
                party.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
            }
            goods.Clear();
            row.LastMarket = now;
            FaithParty cart = _faithParties[party.StringId];
            cart.Stops = 0;
            SetPhase(cart, FaithPartyPhase.Outbound, town.StringId);
            Travel(party, town);
            GwpAiDiagnostics.WriteFaith("MARKET_DISPATCHED",
                "party=" + party.StringId + "; home=" + home.StringId + "; town=" + town.StringId +
                "; cargoValue=" + value + "; men=" + party.MemberRoster.TotalManCount);
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
