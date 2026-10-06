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
    /// 领主什么时候进信众村，由原版的进城意愿决定，信众村对灰袍按城算（GwpFaithVisitDesire.cs）。
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
        private void HandOver(MobileParty party, Settlement village)
        {
            TroopRoster dungeon = RosterOf(_dungeons, village.StringId);
            int prisoners = 0;
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster().ToList())
            {
                if (element.Character.IsHero)
                    continue;
                dungeon.AddToCounts(element.Character, element.Number);
                party.PrisonRoster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
                prisoners += element.Number;
            }

            ItemRoster goods = ItemsOf(_goods, village.StringId);
            ItemRoster villageArmory = ItemsOf(_armories, village.StringId);
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            int goodsCount = 0, goodsValue = 0, armoryItems = 0;
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = party.ItemRoster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item.IsFood)
                    continue;
                if (KitKind(element.EquipmentElement.Item, zealot) >= 0)
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
            GwpAiDiagnostics.WriteFaith("HANDOVER",
                "party=" + party.StringId + "; village=" + village.StringId + "; prisoners=" + prisoners +
                "; dungeon=" + dungeon.TotalManCount + "; goods=" + goodsCount + "; goodsValue=" + goodsValue +
                "; armoryDonated=" + armoryItems);
        }

        /// <summary>
        /// 灰袍领主用部队空位直接招最低级狂信者，不花钱，扣户数；每人从村军械库带走一套（用户裁定：招兵连装备带走）。
        /// 祭司格子的兵已经在进门前招完。
        /// </summary>
        private void EnlistZealots(MobileParty party, Settlement settlement)
        {
            int enlisted = Math.Min(FreeSlots(party), DirectZealotsAvailable(settlement));
            if (enlisted <= 0)
                return;
            party.MemberRoster.AddToCounts(CharacterObject.Find(GwpIds.ZealotId), enlisted);
            float hearthBefore = settlement.Village.Hearth;
            settlement.Village.Hearth -= (enlisted + 1) / 2;
            int kits = MoveKits(settlement.StringId, party.StringId, enlisted, "direct_zealots");
            RecordVisit(party, settlement, recruits: enlisted);
            GwpAiDiagnostics.WriteFaith("ZEALOTS_ENLISTED",
                "party=" + party.StringId + "; village=" + settlement.StringId + "; enlisted=" + enlisted +
                "; kitItems=" + kits + "; hearth=" + hearthBefore.ToString("0.0") + "->" +
                settlement.Village.Hearth.ToString("0.0") +
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
