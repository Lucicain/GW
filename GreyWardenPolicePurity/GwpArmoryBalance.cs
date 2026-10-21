using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        // 军械账和待售仓都是真实库存；可用的待售装备也能先调剂，不能只认当天新溢出的物品。
        private ItemRoster VillageArmoryStock(string village)
        {
            ItemRoster stock = new ItemRoster();
            foreach (ItemRoster ledger in new[] { ItemsOf(_armories, village), ItemsOf(_goods, village) })
                foreach (ItemRosterElement entry in ledger)
                    if (IsArmoryItem(entry.EquipmentElement.Item))
                        stock.AddToCounts(entry.EquipmentElement, entry.Amount);
            return stock;
        }

        private ItemRoster ArmoryCargoFor(FaithVillage home, FaithVillage target) =>
            BalancedArmoryCargo(VillageArmoryStock(home.VillageId), VillageArmoryStock(target.VillageId),
                Math.Max(1, GuardPopulation(home)), Math.Max(1, GuardPopulation(target)),
                Math.Max(GuardCap(home), Sworn(home)), Math.Max(GuardCap(target), Sworn(target)));

        // 九类分别按人数比较有效强度总量。每步模拟接收方择优保留后的收益，
        // 只有缩小人均质量差才运输；缺甲、缺数量和品质差都能触发，不用ARMED代替质量。
        private static ItemRoster BalancedArmoryCargo(ItemRoster home, ItemRoster target,
            int homeMen, int targetMen, int homeCapacity, int targetCapacity)
        {
            ItemRoster cargo = new ItemRoster();
            for (int kind = 0; kind < KitSlots; kind++)
            {
                List<EquipmentElement> source = home.Where(e => KitKind(e.EquipmentElement.Item) == kind)
                    .SelectMany(e => Enumerable.Repeat(e.EquipmentElement, e.Amount))
                    .OrderByDescending(Strength).ToList();
                List<EquipmentElement> receiver = target.Where(e => KitKind(e.EquipmentElement.Item) == kind)
                    .SelectMany(e => Enumerable.Repeat(e.EquipmentElement, e.Amount))
                    .OrderByDescending(Strength).Take(targetCapacity).ToList();
                double homeQuality = source.Take(homeCapacity).Sum(e => (double)Strength(e));
                double targetQuality = receiver.Sum(e => (double)Strength(e));
                for (int index = 0; index < source.Count;)
                {
                    double gap = homeQuality / Math.Max(1, homeMen) - targetQuality / Math.Max(1, targetMen);
                    if (gap <= 0d) break;
                    EquipmentElement item = source[index];
                    float strength = Strength(item);
                    float displaced = receiver.Count < targetCapacity ? 0f : receiver.Count == 0 ? strength : Strength(receiver[receiver.Count - 1]);
                    double gain = Math.Max(0d, strength - displaced);
                    double replacement = source.Count > homeCapacity && index < homeCapacity
                        ? Strength(source[homeCapacity]) : 0d;
                    double loss = index < homeCapacity ? strength - replacement : 0d;
                    double nextGap = (homeQuality - loss) / Math.Max(1, homeMen) -
                        (targetQuality + gain) / Math.Max(1, targetMen);
                    if (gain <= 0d || Math.Abs(nextGap) >= gap)
                    {
                        index++;
                        continue;
                    }
                    cargo.AddToCounts(item, 1);
                    source.RemoveAt(index);
                    homeQuality -= loss;
                    targetQuality += gain;
                    receiver.Add(item);
                    receiver = receiver.OrderByDescending(Strength).Take(targetCapacity).ToList();
                }
            }
            return cargo;
        }

        private static float ArmoryCargoWeight(ItemRoster cargo, MobileParty party) => cargo.Sum(e =>
            e.Amount * Campaign.Current.Models.InventoryCapacityModel.GetItemEffectiveWeight(
                e.EquipmentElement, party, party.IsCurrentlyAtSea, out _));

        private int LoadBalanceCargo(string homeId, MobileParty party, ItemRoster requested)
        {
            int loaded = 0;
            // 强的先运；每种品质用实际模型重算剩余运力，余货仍在源村，后续行程重新计算需求。
            foreach (ItemRosterElement entry in requested.OrderByDescending(e => Strength(e.EquipmentElement)))
            {
                float space = Math.Max(0f, party.InventoryCapacity - party.TotalWeightCarried);
                float weight = Campaign.Current.Models.InventoryCapacityModel.GetItemEffectiveWeight(
                    entry.EquipmentElement, party, party.IsCurrentlyAtSea, out _);
                int wanted = weight > 0f ? Math.Min(entry.Amount, (int)(space / weight)) : entry.Amount;
                foreach (ItemRoster stock in new[] { ItemsOf(_goods, homeId), ItemsOf(_armories, homeId) })
                {
                    int present = stock.Where(e => e.EquipmentElement.Equals(entry.EquipmentElement)).Sum(e => e.Amount);
                    int count = Math.Min(wanted, present);
                    if (count <= 0) continue;
                    stock.AddToCounts(entry.EquipmentElement, -count);
                    party.ItemRoster.AddToCounts(entry.EquipmentElement, count);
                    wanted -= count;
                    loaded += count;
                }
            }
            _plans.Remove(homeId);
            return loaded;
        }
    }
}
