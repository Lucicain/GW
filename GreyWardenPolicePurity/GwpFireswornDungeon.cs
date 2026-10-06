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
    /// 信众村对灰袍等于城（用户裁定）：灰袍领主在那里交俘虏进地牢、捐东西、招兵。
    /// 俘虏不扒装备：装备照原版尸体缴获进军械库（用户裁定 2026-10-06，见 <see cref="LootCasualtyIntoArmory"/>）。
    /// 灰袍领主有俘虏要交、缺兵、背包里有东西时，去最近的信众村办差；灰袍优先做和信众村相关的事（用户裁定），
    /// 俘虏快装满或者兵快打光时，普通案件也让路。
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
            GwpAiDiagnostics.WriteFaith("ZEALOTS_ENLISTED",
                "party=" + party.StringId + "; village=" + settlement.StringId + "; enlisted=" + enlisted +
                "; kitItems=" + kits + "; hearth=" + hearthBefore.ToString("0.0") + "->" +
                settlement.Village.Hearth.ToString("0.0") +
                "; members=" + party.Party.NumberOfAllMembers + "/" + party.Party.PartySizeLimit);
        }

        // ---------------------------------------------------------------- 灰袍领主的信众村差事

        private void TendErrands()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (KeyValuePair<string, Errand> pair in _errands.ToList())
            {
                MobileParty? party = FindParty(pair.Key);
                Settlement village = Settlement.Find(pair.Value.VillageId);
                string? reason = party?.IsActive != true || party.LeaderHero?.IsActive != true ? "party_gone"
                    : ClaimedByOtherDuty(party!) is string duty ? "claimed_by_" + duty
                    : !IsErrandDestination(village) ? "village_unusable"
                    : now - pair.Value.StartHour > GwpTuning.Firesworn.ErrandTimeoutHours ? "timeout"
                    : null;
                if (reason != null)
                {
                    _errands.Remove(pair.Key);
                    if (party?.IsActive == true)
                        GreyWardenPartyDesireBehavior.ClearIntent(party);
                    GwpAiDiagnostics.WriteFaith("ERRAND_CANCELLED",
                        "party=" + pair.Key + "; village=" + pair.Value.VillageId + "; reason=" + reason +
                        "; hours=" + (now - pair.Value.StartHour).ToString("0"));
                    continue;
                }
                if (party!.MapEvent == null)
                    GreyWardenPartyDesireBehavior.RequestVisit(party, village, GwpTuning.Firesworn.ErrandScore);
            }

            foreach (MobileParty party in PoliceStats.GetAllPoliceParties().ToList())
            {
                if (_errands.ContainsKey(party.StringId) || !party.IsActive || !party.IsLordParty ||
                    party.LeaderHero?.IsActive != true || party.MapEvent != null || party.IsCurrentlyAtSea ||
                    party.IsDisbanding || ClaimedByOtherDuty(party) != null)
                    continue;
                // 缺兵只在有信众村招得到兵时才算：不然领主进了村招不到人，出来又要回去。
                Settlement? recruitVillage = NearestErrandVillage(party, needRecruits: true);
                string? need = ErrandNeed(party, recruitVillage != null, out bool urgent);
                if (need == null)
                    continue;
                Settlement? village = need.StartsWith("troops", StringComparison.Ordinal)
                    ? recruitVillage
                    : NearestErrandVillage(party, needRecruits: false);
                if (village == null)
                    continue;
                if (CrimePool.HasTask(party.StringId) &&
                    !(urgent && PoliceEnforcementBehavior.TryReleaseOrdinaryCaseForFaithErrand(party)))
                    continue;
                _errands[party.StringId] = new Errand { VillageId = village.StringId, StartHour = now, Reason = need };
                GreyWardenPartyDesireBehavior.RequestVisit(party, village, GwpTuning.Firesworn.ErrandScore);
                GwpAiDiagnostics.WriteFaith("ERRAND_STARTED",
                    "party=" + party.StringId + "; village=" + village.StringId + "; reason=" + need +
                    "; urgent=" + urgent + "; prisoners=" + CountRegularPrisoners(party) + "/" + party.Party.PrisonerSizeLimit +
                    "; members=" + party.Party.NumberOfAllMembers + "/" + party.Party.PartySizeLimit +
                    "; distance=" + party.GetPosition2D.Distance(village.GetPosition2D).ToString("0"));
            }
        }

        /// <summary>
        /// 要不要去信众村，为什么。紧急（俘虏快装满、兵快打光）时连普通案件都让路；不紧急的只在手上没案子时去。
        /// </summary>
        private static string? ErrandNeed(MobileParty party, bool canRecruit, out bool urgent)
        {
            int prisoners = CountRegularPrisoners(party);
            int prisonLimit = Math.Max(1, party.Party.PrisonerSizeLimit);
            float strength = party.Party.NumberOfAllMembers / (float)Math.Max(1, party.Party.PartySizeLimit);
            bool prisonersFull = prisoners >= prisonLimit * GwpTuning.Firesworn.ErrandUrgentPrisonerShare;
            bool troopsLow = canRecruit && strength < GwpTuning.Firesworn.ErrandUrgentStrength;
            urgent = prisonersFull || troopsLow;
            if (prisonersFull)
                return "prisoners_full";
            if (troopsLow)
                return "troops_low";
            if (prisoners >= GwpTuning.Firesworn.ErrandPrisoners)
                return "prisoners";
            if (canRecruit && strength < GwpTuning.Firesworn.ErrandStrength)
                return "troops";
            return NonFoodValue(party.ItemRoster) >= GwpTuning.Firesworn.ErrandGoodsValue ? "goods" : null;
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

        private Settlement? NearestErrandVillage(MobileParty party, bool needRecruits) =>
            _villages.Keys.Select(Settlement.Find)
                .Where(village => IsErrandDestination(village) &&
                    (!needRecruits || RecruitsAvailableToWardens(village, party.LeaderHero) > 0))
                .OrderBy(village => party.GetPosition2D.Distance(village.GetPosition2D))
                .FirstOrDefault();

        private bool IsErrandDestination(Settlement village) =>
            IsPreachedVillage(village) && IsVillageQuiet(village) &&
            !FactionManager.IsAtWarAgainstFaction(PoliceStats.GetPoliceClan(), village.MapFaction);

        /// <summary>
        /// 比信众村差事更要紧的活：玩家相关、强制善后、重建、请愿、练兵、协力、军团。普通案件不在此列——
        /// 灰袍优先做和信众村相关的事（用户裁定）。
        /// </summary>
        private static string? ClaimedByOtherDuty(MobileParty party) =>
            party.Army != null ? "army"
            : GwpWardenDuties.BlockingDuty(party, WardenDuty.FaithErrand)?.ToString();

        private void CompleteErrand(MobileParty party, Settlement village)
        {
            if (!_errands.TryGetValue(party.StringId, out Errand? errand))
                return;
            _errands.Remove(party.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
            GwpAiDiagnostics.WriteFaith("ERRAND_DONE",
                "party=" + party.StringId + "; village=" + village.StringId + "; planned=" + errand.VillageId +
                "; reason=" + errand.Reason + "; hours=" + (CampaignTime.Now.ToHours - errand.StartHour).ToString("0"));
        }
    }
}
