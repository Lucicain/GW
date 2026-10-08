using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 信众村对灰袍等于城（用户裁定）：灰袍领主在那里交俘虏进地牢、捐东西、招兵；做了什么显示在地图状态文字上。
    /// 俘虏不扒装备：装备照原版尸体缴获进军械库（用户裁定 2026-10-06，见 <see cref="LootCasualtyIntoArmory"/>）。
    /// 领主什么时候进信众村，由原版的进城意愿决定，信众村对灰袍按城算（GwpWardenVisitDesire.cs）。
    /// 信众村自己守赢也有收益（用户裁定）：俘获的敌兵进地牢，背包里的东西进货仓，金币进公库。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        // ---------------------------------------------------------------- 守村收益

        private void SweepPrisonersAndSpoils()
        {
            foreach (FaithVillage row in _villages.Values)
            {
                Settlement village = Settlement.Find(row.VillageId);
                MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
                if (militia?.IsActive == true && militia.MapEvent == null)
                    CollectDefenceSpoils(militia, village);
            }
        }

        private static int CountRegularPrisoners(MobileParty party) =>
            party.PrisonRoster.GetTroopRoster().Where(element => !element.Character.IsHero).Sum(element => element.Number);

        /// <summary>
        /// 信众村民兵守赢后，原版按我们补的分赃规则（<see cref="GwpFaithBattleRewardModel"/>）把俘虏、
        /// 东西、金币给了民兵队。民兵队平时不带这些，所以这里看到的就是守村的收益。
        /// </summary>
        private void CollectDefenceSpoils(MobileParty militia, Settlement village)
        {
            int men = 0, released = 0, goodsCount = 0;
            TroopRoster dungeon = RosterOf(_dungeons, village.StringId);
            foreach (TroopRosterElement element in militia.PrisonRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero)
                {
                    // 领主不准交给村子（用户裁定），放了。
                    EndCaptivityAction.ApplyByReleasedAfterBattle(element.Character.HeroObject);
                    released++;
                    continue;
                }
                dungeon.AddToCounts(element.Character, element.Number);
                militia.PrisonRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
                men += element.Number;
            }
            ItemRoster goods = ItemsOf(_goods, village.StringId);
            for (int i = militia.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = militia.ItemRoster.GetElementCopyAtIndex(i);
                goods.AddToCounts(element.EquipmentElement, element.Amount);
                militia.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
                goodsCount += element.Amount;
            }
            int gold = militia.PartyTradeGold;
            militia.PartyTradeGold = 0;
            CreditTreasury(gold);
            if (men + released + goodsCount + gold > 0)
                GwpAiDiagnostics.WriteFaith("DEFENCE_SPOILS",
                    "village=" + village.StringId + "; prisoners=" + men +
                    "; lordsReleased=" + released + "; goods=" + goodsCount + "; goldToTreasury=" + gold +
                    "; dungeon=" + dungeon.TotalManCount);
        }

        // ---------------------------------------------------------------- 进村交接

        /// <summary>
        /// 普通俘虏交进地牢；背包里除了口粮都捐给村子：狂信者能用的兵器、护甲进村军械库，其余进货仓；
        /// 部队军械库余量以外的捐进村军械库。部队每天把多出来的装备挪进背包（<see cref="RebalanceArmories"/>），
        /// 所以背包里的兵器护甲不进军械库就会被当货卖掉（实机诊断 2026-10-06：村军械库一周都是空的，
        /// 缴获的装备全被狂信者队拉去城里卖了）。
        /// </summary>
        /// <summary>
        /// 灰袍领主进信众村交东西时自己留下的：口粮和普通坐骑（无前缀、不是驮兽），照原版领主进城卖东西的规矩，坐骑多了行军快
        /// （C#反编译 v1.4.8 PartiesSellLootCampaignBehavior.OnSettlementEntered）。进信众村的意愿只算会交出去的东西
        /// （GwpWardenVisitDesire.Rescore），两处用这一个判断。
        /// </summary>
        internal static bool KeptOnHandover(EquipmentElement element)
        {
            ItemObject item = element.Item;
            return item.IsFood || item.ItemType == ItemObject.ItemTypeEnum.Horse && item.HorseComponent.IsRideable &&
                   element.ItemModifier == null && !item.HorseComponent.IsPackAnimal;
        }

        /// <summary>
        /// 地牢还收得下几个：软上限是本村信众户数（用户裁定 2026-10-08）。满了领主就不交、也不再想来交，
        /// 改去附近别的信众村或照原版进城卖（<see cref="GwpWardenVisitDesire.Rescore"/>）；巡逻队押回来的照收，超出的留着慢慢烧。
        /// </summary>
        internal static int PrisonerRoom(Settlement village) =>
            _instance != null && _instance._villages.TryGetValue(village.StringId, out FaithVillage? row)
                ? Math.Max(0, row.Households - RosterOf(_instance._dungeons, village.StringId).TotalManCount)
                : 0;

        private void HandOver(MobileParty party, Settlement village)
        {
            TroopRoster dungeon = RosterOf(_dungeons, village.StringId);
            int room = PrisonerRoom(village);
            int prisoners = 0;
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster()
                         .Where(entry => !entry.Character.IsHero).OrderByDescending(entry => entry.Character.Tier).ToList())
            {
                int take = Math.Min(element.Number, room - prisoners);
                if (take <= 0)
                    break;
                dungeon.AddToCounts(element.Character, take);
                party.PrisonRoster.AddToCounts(element.Character, -take, false, -Math.Min(take, element.WoundedNumber));
                prisoners += take;
            }

            ItemRoster goods = ItemsOf(_goods, village.StringId);
            ItemRoster villageArmory = ItemsOf(_armories, village.StringId);
            int goodsCount = 0, goodsValue = 0, armoryItems = 0;
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                if (KeptOnHandover(element.EquipmentElement))
                    continue;
                if (KitKind(element.EquipmentElement.Item) >= 0)
                {
                    villageArmory.AddToCounts(element.EquipmentElement, element.Amount);
                    armoryItems += element.Amount;
                }
                else
                {
                    goods.AddToCounts(element.EquipmentElement, element.Amount);
                    goodsCount += element.Amount;
                    goodsValue += element.Amount * element.EquipmentElement.ItemValue;
                }
                party.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
            }
            _plans.Remove(village.StringId);

            armoryItems += DonateArmorySurplus(party.StringId, village.StringId);
            RecordVisit(party, village, prisoners: prisoners, donated: armoryItems + goodsCount);
            if (prisoners > 0)
                GwpSettlementNotices.Post(village, party.LeaderHero,
                    GwpText.Get("{=gwp_notice_prisoners}Handed over {COUNT} prisoners", "COUNT", prisoners));
            if (armoryItems + goodsCount > 0)
                GwpSettlementNotices.Post(village, party.LeaderHero,
                    GwpText.Get("{=gwp_notice_donated}Donated {COUNT} items", "COUNT", armoryItems + goodsCount));
            GwpAiDiagnostics.WriteFaith("HANDOVER",
                "party=" + party.StringId + "; village=" + village.StringId + "; prisoners=" + prisoners +
                "; dungeon=" + dungeon.TotalManCount + "; goods=" + goodsCount + "; goodsValue=" + goodsValue +
                "; armoryDonated=" + armoryItems);
        }

        /// <summary>
        /// 灰袍领主用部队空位直接招守村队里现有的狂信者（用户裁定 2026-10-07：不凭空变，不能招完），不花钱。
        /// 户数和信众户都不扣：信教的人家还在，只是当兵的子女走了（用户裁定 2026-10-07），守村队慢慢补回。
        /// 每人带走他那一套装备（用户裁定：招兵连装备带走）。祭司格子的兵已经在进门前招完。
        /// </summary>
        private void EnlistZealots(MobileParty party, Settlement settlement)
        {
            int enlisted = DirectZealotsFor(settlement, party);
            if (enlisted <= 0)
                return;
            FaithVillage row = _villages[settlement.StringId];
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            int kits = MoveBestSets(settlement.StringId, party.StringId, zealot, enlisted);
            GuardOf(row.VillageId).AddToCounts(zealot, -enlisted);
            party.MemberRoster.AddToCounts(zealot, enlisted);
            PushGuard(row, settlement);
            ReconcilePriestSlots(row);
            UpdatePriestPower(row);
            RecordVisit(party, settlement, recruits: enlisted);
            GwpSettlementNotices.Post(settlement, party.LeaderHero, GwpSettlementNotices.RecruitText(enlisted));
            GwpAiDiagnostics.WriteFaith("ZEALOTS_ENLISTED",
                "party=" + party.StringId + "; village=" + settlement.StringId + "; enlisted=" + enlisted +
                "; kitItems=" + kits + "; guardLeft=" + Sworn(row) + "; households=" + row.Households +
                "; members=" + party.Party.NumberOfAllMembers + "/" + party.Party.PartySizeLimit);
        }

        private static int NonFoodValue(ItemRoster roster)
        {
            int value = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                if (!element.EquipmentElement.Item.IsFood)
                    value += element.Amount * element.EquipmentElement.ItemValue;
            }
            return value;
        }
    }
}
