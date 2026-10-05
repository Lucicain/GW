using System;
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
    /// 每座信众村的两支常驻队（用户裁定：传教队和狂信者队分开）：
    /// <list type="bullet">
    /// <item>传教队去别的村子主持集会传教，传完回村。灰袍领主不主持传教（用户裁定）。</item>
    /// <item>狂信者队把村里货仓的东西运进贸易城镇卖掉，钱带回来进灰袍公库（用户裁定：收益最后都进公库）。</item>
    /// </list>
    /// 两支都照原版村民队（C#反编译 v1.4.8 VillagerCampaignBehavior）：常驻、卖完或办完就回村待着，
    /// 下次出发前从村里的狂信者补人；只有人死光（被原版销毁）才没了，下次需要时再建。
    /// 队里的人不算在村子的狂信者数里。
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
                        // 在家也一直续「去本村」：人已经在村里时原版就待着不动，不续的话意图过期后会被原版拉去别处。
                        GreyWardenPartyDesireBehavior.RequestVisit(party, home, GwpTuning.Firesworn.FaithPartyVisitScore);
                        break;

                    case FaithPartyPhase.Outbound:
                        Settlement target = Settlement.Find(record.TargetId);
                        if (now - record.PhaseHour > GwpTuning.Firesworn.FaithPartyTravelTimeoutHours ||
                            !IsTripTargetValid(record, target))
                        {
                            GwpAiDiagnostics.WriteFaith(record.Kind == FaithPartyKind.Disciples ? "DISCIPLES_TURNED_BACK" : "CARAVAN_TURNED_BACK",
                                "party=" + party.StringId + "; home=" + record.HomeId + "; target=" + record.TargetId +
                                "; hours=" + (now - record.PhaseHour).ToString("0"));
                            SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                            break;
                        }
                        if (!HasArrived(party, target))
                        {
                            GreyWardenPartyDesireBehavior.RequestVisit(party, target, GwpTuning.Firesworn.FaithPartyVisitScore);
                            break;
                        }
                        if (record.Kind == FaithPartyKind.Disciples)
                            StartCongregation(record, party, target);
                        else
                            SellCargo(record, party, target);
                        break;

                    case FaithPartyPhase.Returning:
                        if (!HasArrived(party, home))
                        {
                            GreyWardenPartyDesireBehavior.RequestVisit(party, home, GwpTuning.Firesworn.FaithPartyVisitScore);
                            break;
                        }
                        ArriveHome(record, party, home);
                        break;
                }
            }
        }

        private bool IsTripTargetValid(FaithParty record, Settlement target)
        {
            Clan police = PoliceStats.GetPoliceClan();
            if (FactionManager.IsAtWarAgainstFaction(police, target.MapFaction))
                return false;
            return record.Kind == FaithPartyKind.Disciples
                ? IsCongregationTarget(target, police)
                : !target.IsUnderSiege;
        }

        private void ArriveHome(FaithParty record, MobileParty party, Settlement home)
        {
            int proceeds = party.PartyTradeGold;
            party.PartyTradeGold = 0;
            CreditTreasury(proceeds);
            int unsold = 0;
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
            if (party.CurrentSettlement != home)
                EnterSettlementAction.ApplyForParty(party, home);
            GwpAiDiagnostics.WriteFaith(record.Kind == FaithPartyKind.Disciples ? "DISCIPLES_HOME" : "CARAVAN_HOME",
                "party=" + party.StringId + "; home=" + home.StringId + "; men=" + party.MemberRoster.TotalManCount +
                "; proceedsToTreasury=" + proceeds + "; unsoldBack=" + unsold);
        }

        private static void SetPhase(FaithParty record, FaithPartyPhase phase, string targetId)
        {
            record.Phase = phase;
            record.TargetId = targetId;
            record.PhaseHour = CampaignTime.Now.ToHours;
        }

        /// <summary>这座村子这一种常驻队：在且在家就用它，没有就新建。不在家（出门或回程中）返回 null。</summary>
        private MobileParty? ReadyFaithParty(FaithVillage row, Settlement home, FaithPartyKind kind)
        {
            FaithParty? record = _faithParties.Values.FirstOrDefault(entry =>
                entry.Kind == kind && string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase));
            if (record != null)
                return record.Phase == FaithPartyPhase.Home && FindParty(record.PartyId)!.CurrentSettlement == home
                    ? FindParty(record.PartyId)
                    : null;

            Clan police = PoliceStats.GetPoliceClan();
            TextObject name = kind == FaithPartyKind.Disciples
                ? GwpText.Create("{=gwp_fs_disciple_party}Disciples of the Eternal Fire")
                : GwpText.Create("{=gwp_fs_caravan_party}Zealots of {VILLAGE}", "VILLAGE", home.Name);
            MobileParty party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                home.GatePosition, 1f, home, name, police, police.DefaultPartyTemplate, police.Leader, "", "", 4f, false);
            party.StringId = (kind == FaithPartyKind.Disciples ? GwpIds.DisciplePartyPrefix : GwpIds.ZealotCaravanPrefix) +
                             MBRandom.RandomInt(100000, 999999);
            party.ActualClan = police;
            party.MemberRoster.Clear();
            PoliceResourceManager.OutfitTemporaryDutyParty(party);
            EnterSettlementAction.ApplyForParty(party, home);
            _faithParties[party.StringId] = new FaithParty
            {
                PartyId = party.StringId,
                HomeId = row.VillageId,
                Kind = kind,
                Phase = FaithPartyPhase.Home,
                TargetId = row.VillageId,
                PhaseHour = CampaignTime.Now.ToHours
            };
            GwpAiDiagnostics.WriteFaith(kind == FaithPartyKind.Disciples ? "DISCIPLES_FORMED" : "CARAVAN_FORMED",
                "party=" + party.StringId + "; home=" + home.StringId);
            return party;
        }

        /// <summary>出发前从村里的狂信者补人，补到这一种队伍的人数。</summary>
        private static void TopUp(MobileParty party, FaithVillage row, Settlement home, int size)
        {
            int add = Math.Min(row.Zealots, Math.Max(0, size - party.MemberRoster.TotalManCount));
            if (add <= 0)
                return;
            party.MemberRoster.AddToCounts(CharacterObject.Find(GwpIds.ZealotId), add);
            row.Zealots -= add;
            PushMilitia(row, home);
        }

        // ---------------------------------------------------------------- 传教队

        /// <summary>
        /// 每天一次。有祭司、狂信者占户数到了门槛、离上次派队够久的信众村，派传教队去最近的还能传的村子：
        /// 先选还不是信众村的，其次是还没到上限、离上次传教够 15 天的信众村。
        /// </summary>
        private void DispatchDisciples()
        {
            Clan police = PoliceStats.GetPoliceClan();
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values
                         .OrderByDescending(entry => entry.Zealots)
                         .ThenBy(entry => entry.VillageId, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                int away = _faithParties.Values.Count(entry =>
                    entry.Kind == FaithPartyKind.Disciples && entry.Phase != FaithPartyPhase.Home);
                if (away >= GwpTuning.Firesworn.MaxActiveDisciples)
                    return;

                Settlement home = Settlement.Find(row.VillageId);
                if (FindLivingPriest(row) == null || !IsVillageQuiet(home) || HasRitual(home) ||
                    now - row.LastDisciple < GwpTuning.Firesworn.DiscipleIntervalDays * 24d ||
                    row.Zealots < GwpTuning.Firesworn.DiscipleSize + GwpTuning.Firesworn.DiscipleMinimumLeftBehind ||
                    row.Zealots / Math.Max(1f, home.Village.Hearth) < GwpTuning.Firesworn.DiscipleZealotShare)
                    continue;

                Settlement? target = Village.All
                    .Select(entry => entry.Settlement)
                    .Where(settlement => settlement != home && IsCongregationTarget(settlement, police) &&
                        home.GetPosition2D.Distance(settlement.GetPosition2D) <= GwpTuning.Firesworn.DiscipleTargetMaxDistance &&
                        !_faithParties.Values.Any(entry => entry.Kind == FaithPartyKind.Disciples &&
                            string.Equals(entry.TargetId, settlement.StringId, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(settlement => IsPreachedVillage(settlement) ? 1 : 0)
                    .ThenBy(settlement => home.GetPosition2D.Distance(settlement.GetPosition2D))
                    .FirstOrDefault();
                if (target == null)
                    continue;

                MobileParty? party = ReadyFaithParty(row, home, FaithPartyKind.Disciples);
                if (party == null)
                    continue;
                TopUp(party, row, home, GwpTuning.Firesworn.DiscipleSize);
                row.LastDisciple = now;
                FaithParty record = _faithParties[party.StringId];
                SetPhase(record, FaithPartyPhase.Outbound, target.StringId);
                GreyWardenPartyDesireBehavior.RequestVisit(party, target, GwpTuning.Firesworn.FaithPartyVisitScore);
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_fs_disciple_left}Disciples of the Eternal Fire have left {HOME} to preach in {TARGET}.",
                    "HOME", home.Name, "TARGET", target.Name), Colors.Gray));
                GwpAiDiagnostics.WriteFaith("DISCIPLES_DISPATCHED",
                    "party=" + party.StringId + "; home=" + home.StringId + "; target=" + target.StringId +
                    "; targetPreached=" + IsPreachedVillage(target) + "; men=" + party.MemberRoster.TotalManCount +
                    "; homeZealots=" + row.Zealots + "; distance=" +
                    home.GetPosition2D.Distance(target.GetPosition2D).ToString("0"));
            }
        }

        // ---------------------------------------------------------------- 狂信者队

        /// <summary>
        /// 每天一次。货仓里东西值够了、离上次出车够久，就派狂信者队把货运到本村的贸易城镇。
        /// </summary>
        private void DispatchCaravans()
        {
            Clan police = PoliceStats.GetPoliceClan();
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                if (!_goods.TryGetValue(row.VillageId, out ItemRoster? goods) ||
                    goods.TotalValue < GwpTuning.Firesworn.CaravanMinimumGoodsValue ||
                    now - row.LastCaravan < GwpTuning.Firesworn.CaravanIntervalDays * 24d ||
                    row.Zealots < GwpTuning.Firesworn.CaravanSize + GwpTuning.Firesworn.DiscipleMinimumLeftBehind)
                    continue;
                Settlement home = Settlement.Find(row.VillageId);
                Settlement? town = home.Village.TradeBound;
                if (town == null || town.IsUnderSiege || !IsVillageQuiet(home) ||
                    FactionManager.IsAtWarAgainstFaction(police, town.MapFaction))
                    continue;

                MobileParty? party = ReadyFaithParty(row, home, FaithPartyKind.Caravan);
                if (party == null)
                    continue;
                TopUp(party, row, home, GwpTuning.Firesworn.CaravanSize);
                int value = goods.TotalValue;
                for (int i = goods.Count - 1; i >= 0; i--)
                {
                    ItemRosterElement element = goods.GetElementCopyAtIndex(i);
                    party.ItemRoster.AddToCounts(element.EquipmentElement, element.Amount);
                }
                goods.Clear();
                row.LastCaravan = now;
                SetPhase(_faithParties[party.StringId], FaithPartyPhase.Outbound, town.StringId);
                GreyWardenPartyDesireBehavior.RequestVisit(party, town, GwpTuning.Firesworn.FaithPartyVisitScore);
                GwpAiDiagnostics.WriteFaith("CARAVAN_DISPATCHED",
                    "party=" + party.StringId + "; home=" + home.StringId + "; town=" + town.StringId +
                    "; cargoValue=" + value + "; men=" + party.MemberRoster.TotalManCount);
            }
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
            SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
            GwpAiDiagnostics.WriteFaith("CARAVAN_SOLD",
                "party=" + party.StringId + "; town=" + town.StringId + "; sold=" + sold + "; proceeds=" + proceeds +
                "; townGoldLeft=" + town.Town.Gold);
        }
    }
}
