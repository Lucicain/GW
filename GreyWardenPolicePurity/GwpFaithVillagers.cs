using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        // 身上的货仍只有一份；这里只标记其中属灰袍的物品/品质/数量，随在途任务保存。
        private readonly Dictionary<string, ItemRoster> _marketCargo =
            new Dictionary<string, ItemRoster>(StringComparer.OrdinalIgnoreCase);

        internal static bool IsFaithVillager(MobileParty? party) =>
            party?.IsVillager == true && _instance != null &&
            _instance._faithParties.TryGetValue(party.StringId, out FaithParty? record) && record.Kind == FaithPartyKind.Market;

        private FaithParty RegisterFaithVillager(MobileParty party, Settlement home)
        {
            if (!_faithParties.TryGetValue(party.StringId, out FaithParty? record))
                _faithParties[party.StringId] = record = new FaithParty
                {
                    PartyId = party.StringId, HomeId = home.StringId, Kind = FaithPartyKind.Market,
                    Phase = FaithPartyPhase.Home, TargetId = home.StringId, PhaseHour = CampaignTime.Now.ToHours
                };
            return record;
        }

        internal static void ConvertRaisedVillagers(MobileParty party, IReadOnlyDictionary<CharacterObject, int>? before)
        {
            Settlement home = party.HomeSettlement;
            if (!IsPreachedVillage(home)) return;
            GwpFireswornBehavior behavior = _instance!;
            behavior.RegisterFaithVillager(party, home);
            // 原版刚扣完出队户数，每小时户数回读尚未发生；全信村比例也最多为1。
            float share = Math.Min(1f, SwornShare(behavior._villages[home.StringId], home));
            int present = party.MemberRoster.GetTroopRoster().Where(e => IsGuardTroop(e.Character)).Sum(e => e.Number);
            int needed = Math.Max(0, MBRandom.RoundRandomized(party.MemberRoster.TotalRegulars * share) - present);
#if GWP_DIAGNOSTICS
            int raisedTotal = 0, swornTotal = 0;
#endif
            foreach (TroopRosterElement entry in party.MemberRoster.GetTroopRoster()
                .Where(e => !e.Character.IsHero && !IsGuardTroop(e.Character)).ToList())
            {
                int prior = before != null && before.TryGetValue(entry.Character, out int count) ? count : 0;
                int raised = Math.Max(0, entry.Number - prior);
                int sworn = Math.Min(raised, needed);
#if GWP_DIAGNOSTICS
                raisedTotal += raised;
                swornTotal += sworn;
#endif
                if (sworn <= 0) continue;
                // 只用本次健康、零经验的新村民补比例缺口，既有普通村民不转换，也不重复扣户数。
                party.MemberRoster.AddToCounts(entry.Character, -sworn);
                party.MemberRoster.AddToCounts(CharacterObject.Find(GwpIds.ZealotId), sworn);
                needed -= sworn;
            }
            behavior._plans.Remove(party.StringId);
#if GWP_DIAGNOSTICS
            if (raisedTotal > 0)
                GwpAiDiagnostics.WriteFaith("VILLAGERS_RAISED",
                    "party=" + party.StringId + "; home=" + home.StringId + "; stage=" + (before == null ? "birth" : "refill") +
                    "; raised=" + raisedTotal + "; converted=" + swornTotal + "; share=" + share.ToString("0.000") +
                    "; faithfulBefore=" + present + "; faithfulAfter=" + (present + swornTotal) +
                    "; members=" + party.MemberRoster.TotalRegulars + "; hearth=" + home.Village.Hearth.ToString("0.0"));
#endif
        }

        internal static int FaithMarketWarehouseThreshold(int vanilla, Village village) =>
            IsPreachedVillage(village.Settlement) && !_instance!.HasRitual(village.Settlement) &&
            _instance._goods.TryGetValue(village.Settlement.StringId, out ItemRoster? goods) &&
            goods.TotalValue >= GwpTuning.Firesworn.CaravanMinimumGoodsValue ? 0 : vanilla;

        internal static void PrepareFaithVillagers(Village village, MobileParty party)
        {
            Settlement home = village.Settlement;
            if (!IsPreachedVillage(home) || !IsVillageQuiet(home) || _instance!.HasRitual(home) ||
                party.MapEvent != null || party.CurrentSettlement != home)
                return;
            GwpFireswornBehavior behavior = _instance!;
            behavior.RegisterFaithVillager(party, home);
            FaithVillage row = behavior._villages[home.StringId];
            behavior.ObserveGuard(row);
            // 先还现有库装，再按替换后的实际名单重新领装，不能让原村民的装备随换人消失。
            behavior.ReturnPartyLedger(party, home.StringId);
            int replaced = behavior.RefitVillageEscort(row, home, party);
            int kits = behavior.TakeBestSets(home.StringId, party);
            behavior.ReconcilePriestSlots(row);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("VILLAGERS_EQUIPPED",
                "party=" + party.StringId + "; home=" + home.StringId + "; replaced=" + replaced +
                "; men=" + party.MemberRoster.TotalManCount + "; kitItems=" + kits +
                "; guardLeft=" + behavior.GuardOf(home.StringId).TotalManCount);
#endif
        }

        private int RefitVillageEscort(FaithVillage row, Settlement home, MobileParty party)
        {
            MobileParty? militia = home.MilitiaPartyComponent?.MobileParty;
            if (militia?.IsActive != true || militia.MapEvent != null)
                return 0;
            TroopRoster guard = GuardOf(home.StringId);
            int budget = SpareGuard(row);
            int replaced = 0;
            foreach (TroopRosterElement elite in militia.MemberRoster.GetTroopRoster()
                .Where(e => IsGuardTroop(e.Character)).OrderByDescending(e => e.Character.Tier).ToList())
            {
                int remaining = elite.Number - elite.WoundedNumber;
                foreach (TroopRosterElement weak in party.MemberRoster.GetTroopRoster()
                    .Where(e => IsGuardTroop(e.Character) && e.Character.Tier < elite.Character.Tier)
                    .OrderBy(e => e.Character.Tier).ToList())
                {
                    int count = Math.Min(Math.Min(remaining, weak.Number), budget);
                    if (count <= 0) break;
                    MoveBestSets(home.StringId, party.StringId, elite.Character, count);
                    int moved = GwpTroopTransfer.Move(militia.MemberRoster, party.MemberRoster, elite.Character,
                        count, healthyOnly: true).Number;
                    guard.AddToCounts(elite.Character, -moved);
                    JoinGuard(row, home, weak.Character, moved, party.MemberRoster);
                    remaining -= moved;
                    budget -= moved;
                    replaced += moved;
                }
            }
            _plans.Remove(home.StringId);
            _plans.Remove(party.StringId);
            return replaced;
        }

        internal static void LoadFaithMarket(Village village, MobileParty party)
        {
            if (!IsFaithVillager(party)) return;
            GwpFireswornBehavior behavior = _instance!;
            ItemRoster goods = ItemsOf(behavior._goods, village.Settlement.StringId);
            ItemRoster cargo = ItemsOf(behavior._marketCargo, party.StringId);
            float space = Math.Max(0f, party.InventoryCapacity - party.TotalWeightCarried);
            int loaded = 0;
            foreach (ItemRosterElement entry in goods.ToList())
            {
                float weight = entry.EquipmentElement.Item.Weight;
                int count = weight > 0f ? Math.Min(entry.Amount, (int)(space / weight)) : entry.Amount;
                if (count <= 0) continue;
                goods.AddToCounts(entry.EquipmentElement, -count);
                party.ItemRoster.AddToCounts(entry.EquipmentElement, count);
                cargo.AddToCounts(entry.EquipmentElement, count);
                space = Math.Max(0f, space - count * weight);
                loaded += count;
            }
            SetPhase(behavior._faithParties[party.StringId], FaithPartyPhase.Outbound,
                (party.TargetSettlement ?? village.TradeBound ?? village.Settlement).StringId);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("MARKET_DISPATCHED",
                "party=" + party.StringId + "; home=" + village.Settlement.StringId + "; nativeVillager=True; cargoItems=" + loaded +
                "; cargoValue=" + cargo.TotalValue + "; men=" + party.MemberRoster.TotalManCount);
#endif
        }

        // 同物品/品质混装时先卖灰袍份额；金币仍真正在队伍身上，遇袭不会从账本补发。
        internal static void RecordFaithMarketSale(MobileParty party, EquipmentElement item, int count, int unitPrice)
        {
            if (!IsFaithVillager(party)) return;
            ItemRoster cargo = ItemsOf(_instance!._marketCargo, party.StringId);
            int owned = cargo.Where(e => e.EquipmentElement.Equals(item)).Sum(e => e.Amount);
            int sold = Math.Min(owned, count);
            if (sold == 0) return;
            cargo.AddToCounts(item, -sold);
            _instance._faithParties[party.StringId].MarketGold += sold * unitPrice;
        }

        internal static bool SellFaithMarket(Settlement settlement, MobileParty party)
        {
            if (!IsFaithVillager(party) || settlement.Town == null) return false;
            ItemObject? pack = party.ItemRoster.Select(e => e.EquipmentElement.Item)
                .Where(item => item.ItemCategory == DefaultItemCategories.PackAnimal)
                .OrderBy(item => item.Value).FirstOrDefault();
            int sold = 0;
            for (int i = party.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement entry = party.ItemRoster.GetElementCopyAtIndex(i);
                int offered = entry.Amount - (entry.EquipmentElement.Item == pack ? (int)(party.MemberRoster.TotalManCount * 0.5f) : 0);
                int price = settlement.Town.GetItemPrice(entry.EquipmentElement, party, isSelling: true);
                if (offered <= 0 || price <= 0) continue;
                int count = Math.Min(offered, settlement.Town.Gold / price);
                if (count <= 0) continue;
                RecordFaithMarketSale(party, entry.EquipmentElement, count, price);
                settlement.Town.ChangeGold(-count * price);
                settlement.ItemRoster.AddToCounts(entry.EquipmentElement, count);
                party.ItemRoster.AddToCounts(entry.EquipmentElement, -count);
                party.PartyTradeGold += count * price;
                sold += count;
            }
            if (sold > 0) GwpSettlementNotices.Post(settlement, party, GwpText.Get(GwpTextCatalog.gwp_notice_market, "COUNT", sold));
            return true;
        }

        internal static void SettleFaithMarket(MobileParty party, Settlement settlement)
        {
            if (!IsFaithVillager(party) || !settlement.IsVillage) return;
            GwpFireswornBehavior behavior = _instance!;
            FaithParty record = behavior._faithParties[party.StringId];
            int gold = Math.Min(record.MarketGold, party.PartyTradeGold);
            party.PartyTradeGold -= gold;
            record.MarketGold = 0;
            CreditTreasury(gold);
            // 原版经过任何村庄都会清交易金币并记回家乡村税；先分出灰袍部分，
            // 但灰袍余货、俘虏和本队成员只有到家才处理。
            if (party.HomeSettlement != settlement) return;
            ItemRoster cargo = ItemsOf(behavior._marketCargo, party.StringId);
            int returned = 0;
            foreach (ItemRosterElement entry in cargo.ToList())
            {
                int present = party.ItemRoster.Where(e => e.EquipmentElement.Equals(entry.EquipmentElement)).Sum(e => e.Amount);
                int count = Math.Min(entry.Amount, present);
                party.ItemRoster.AddToCounts(entry.EquipmentElement, -count);
                ItemsOf(behavior._goods, settlement.StringId).AddToCounts(entry.EquipmentElement, count);
                returned += count;
            }
            cargo.Clear();
            ReleaseHeroPrisoners(party);
            TroopRoster dungeon = RosterOf(behavior._dungeons, settlement.StringId);
            int prisoners = GwpTroopTransfer.MoveBatches(party.PrisonRoster, dungeon,
                party.PrisonRoster.GetTroopRoster().Where(e => !e.Character.IsHero), party.PrisonRoster.TotalRegulars);
            // 原版村民队的成员、驮兽和普通生产货物保留，不能按旧临时队落村逻辑整队吸收。
            SetPhase(record, FaithPartyPhase.Home, settlement.StringId);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("MARKET_HOME",
                "party=" + party.StringId + "; home=" + settlement.StringId + "; goldToTreasury=" + gold +
                "; nativeTradeGold=" + party.PartyTradeGold + "; unsoldReturned=" + returned + "; prisoners=" + prisoners);
#endif
        }

        private static void ObserveFaithMarket(FaithParty record, MobileParty party, Settlement home)
        {
            Settlement target = party.TargetSettlement ?? home;
            FaithPartyPhase phase = party.CurrentSettlement == home && target == home ? FaithPartyPhase.Home :
                target == home ? FaithPartyPhase.Returning : FaithPartyPhase.Outbound;
            if (record.Phase != phase || record.TargetId != target.StringId)
                SetPhase(record, phase, target.StringId);
        }
    }

    [HarmonyPatch(typeof(VillagerCampaignBehavior), "CreateVillagerParty")]
    internal static class GwpFaithVillagerBirthPatch
    {
        private static void Postfix(Village village) =>
            GwpFireswornBehavior.ConvertRaisedVillagers(village.VillagerPartyComponent.MobileParty, null);
    }

    [HarmonyPatch(typeof(VillagerCampaignBehavior), "AddVillagersToParty")]
    internal static class GwpFaithVillagerReplenishPatch
    {
        private static void Prefix(MobileParty villagerParty, out Dictionary<CharacterObject, int>? __state) =>
            __state = GwpFireswornBehavior.IsPreachedVillage(villagerParty.HomeSettlement)
                ? villagerParty.MemberRoster.GetTroopRoster().ToDictionary(e => e.Character, e => e.Number) : null;

        private static void Postfix(MobileParty villagerParty, Dictionary<CharacterObject, int>? __state)
        {
            if (__state != null) GwpFireswornBehavior.ConvertRaisedVillagers(villagerParty, __state);
        }
    }

    [HarmonyPatch(typeof(VillagerCampaignBehavior), "LoadAndSendVillagerParty")]
    internal static class GwpFaithVillagerDeparturePatch
    {
        private static void Prefix(Village village, MobileParty villagerParty) => GwpFireswornBehavior.PrepareFaithVillagers(village, villagerParty);
        private static void Postfix(Village village, MobileParty villagerParty) => GwpFireswornBehavior.LoadFaithMarket(village, villagerParty);
    }

    [HarmonyPatch(typeof(VillagerCampaignBehavior), "ThinkAboutSendingItemToTown")]
    internal static class GwpFaithVillagerWarehousePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var warehouse = AccessTools.Method(typeof(Village), nameof(Village.GetWarehouseCapacity));
            var threshold = AccessTools.Method(typeof(GwpFireswornBehavior), nameof(GwpFireswornBehavior.FaithMarketWarehouseThreshold));
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!instruction.Calls(warehouse)) continue;
                // 只扩展本次出车检查的货源；原版15%小时概率、是否在家、建队/补员、扣户数照走。
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, threshold);
            }
        }
    }

    [HarmonyPatch(typeof(SellGoodsForTradeAction), nameof(SellGoodsForTradeAction.ApplyByVillagerTrade))]
    internal static class GwpFaithVillagerSalePatch
    {
        private static bool Prefix(Settlement settlement, MobileParty villagerParty) => !GwpFireswornBehavior.SellFaithMarket(settlement, villagerParty);
    }

    [HarmonyPatch(typeof(VillagerCampaignBehavior), "OnSettlementEntered")]
    internal static class GwpFaithVillagerIncomePatch
    {
        private static void Prefix(MobileParty mobileParty, Settlement settlement)
        {
            if (mobileParty != null) GwpFireswornBehavior.SettleFaithMarket(mobileParty, settlement);
        }
    }
}
