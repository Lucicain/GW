using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        private readonly Dictionary<string, ArmoryBattleLoadout> _loadedBattleLoadouts = new();

        // 只保存分装引用与原版名册接续信息；实际物品仍只在_armories中，读档不增减资源。
        private void SyncArmoryBattles(IDataStore store)
        {
            List<string>? parties = null, holders = null, troops = null, items = null, modifiers = null;
            List<int>? seeds = null, outcomes = null, bindings = null, slots = null, kinds = null;
            if (store.IsSaving)
            {
                parties = new(); holders = new(); troops = new(); items = new(); modifiers = new();
                seeds = new(); outcomes = new(); bindings = new(); slots = new(); kinds = new();
                foreach (var row in _battleLoadouts)
                {
                    CaptureBattleOutcomes(row.Key);
                    foreach (var loan in row.Value.BySeed)
                    {
                        int binding = parties.Count;
                        parties.Add(row.Key.Party.Id);
                        holders.Add(row.Value.Holder);
                        troops.Add(loan.Value.Troop.StringId);
                        seeds.Add(loan.Key);
                        outcomes.Add(row.Value.Outcomes[loan.Key]);
                        for (int slot = 0; slot < (int)EquipmentIndex.NumEquipmentSetSlots; slot++)
                        {
                            EquipmentElement item = loan.Value.Equipment[slot];
                            if (item.IsEmpty) continue;
                            bindings.Add(binding); slots.Add(slot);
                            kinds.Add(System.Array.IndexOf(loan.Value.Slots, slot));
                            items.Add(item.Item.StringId);
                            modifiers.Add(item.ItemModifier?.StringId ?? string.Empty);
                        }
                    }
                }
            }
            const string key = "GWPP_FsBattle";
            store.SyncData(key + "Parties", ref parties);
            store.SyncData(key + "Holders", ref holders);
            store.SyncData(key + "Troops", ref troops);
            store.SyncData(key + "Seeds", ref seeds);
            store.SyncData(key + "Outcomes", ref outcomes);
            store.SyncData(key + "Bindings", ref bindings);
            store.SyncData(key + "Slots", ref slots);
            store.SyncData(key + "Kinds", ref kinds);
            store.SyncData(key + "Items", ref items);
            store.SyncData(key + "Modifiers", ref modifiers);
            if (!store.IsLoading) return;
            _loadedBattleLoadouts.Clear();
            // 旧档没有分装快照：保留原库存，由原版当前名册首次排兵时建立借用记录。
            if (parties == null) return;
            var kits = new List<ArmoryKit>();
            for (int i = 0; i < parties.Count; i++)
            {
                if (!_loadedBattleLoadouts.TryGetValue(parties[i], out ArmoryBattleLoadout? loadout))
                    _loadedBattleLoadouts.Add(parties[i], loadout = new ArmoryBattleLoadout(holders![i], new ArmoryPlan()));
                var kit = new ArmoryKit(CharacterObject.Find(troops![i]), new Equipment(Equipment.EquipmentType.Battle));
                kits.Add(kit);
                loadout.BySeed.Add(seeds![i], kit);
                loadout.Outcomes.Add(seeds[i], outcomes![i]);
            }
            for (int i = 0; i < items!.Count; i++)
            {
                ArmoryKit kit = kits[bindings![i]];
                var item = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>(items[i]),
                    modifiers![i].Length == 0 ? null : MBObjectManager.Instance.GetObject<ItemModifier>(modifiers[i]));
                kit.Equipment[slots![i]] = item;
                if (kinds![i] < 0) continue;
                kit.Items[kinds[i]] = item;
                kit.Slots[kinds[i]] = slots[i];
            }
        }

        private void RestoreArmoryBattleLoadouts()
        {
            foreach (var row in _loadedBattleLoadouts)
            {
                MobileParty? mobile = FindParty(row.Key);
                MapEventParty? party = mobile?.Party.MapEventSide?.Parties.FirstOrDefault(entry => entry.Party == mobile.Party);
                if (party == null) continue;
                _battleLoadouts.Add(party, row.Value);
                _battleOwners[party] = row.Value.Holder;
                RefreshBattlePlan(party, row.Value);
            }
            _loadedBattleLoadouts.Clear();
        }
    }
}
