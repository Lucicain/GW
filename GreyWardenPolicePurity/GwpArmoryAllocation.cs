using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        private sealed class ArmoryKit
        {
            public readonly CharacterObject Troop;
            public readonly Equipment Equipment;
            public readonly EquipmentElement[] Items = new EquipmentElement[(int)EquipmentIndex.NumEquipmentSetSlots];

            public ArmoryKit(CharacterObject troop, Equipment equipment) { Troop = troop; Equipment = equipment; }

            public void Equip(EquipmentIndex slot, EquipmentElement item)
            {
                if (item.IsEmpty) return;
                Equipment[slot] = item;
                Items[(int)slot] = item;
            }
        }

        private sealed class ArmoryPlan
        {
            public readonly List<ArmoryKit> Sets = new List<ArmoryKit>();
            public IReadOnlyList<TroopRosterElement> Troops = Array.Empty<TroopRosterElement>();
            public ItemRoster? Ledger;
            public int LedgerVersion, Capacity;
            public readonly ItemRoster Free = new ItemRoster();
            public readonly ItemRoster Surplus = new ItemRoster();
            public readonly Dictionary<CharacterObject, (float Weapon, float Armor)> TroopTiers =
                new Dictionary<CharacterObject, (float Weapon, float Armor)>();
        }

        /// <summary>
        /// 分发额度按装备种类计（<see cref="QuotaKind"/>），每种的上限相同；可预先计入已借出的装备。
        /// </summary>
        private sealed class ArmoryStock
        {
            private readonly List<EquipmentElement>[] _buckets;
            private readonly int _limit;
            private readonly Dictionary<int, int> _issued = new Dictionary<int, int>();

            internal ArmoryStock(List<EquipmentElement>[] buckets, int limit, IEnumerable<EquipmentElement> alreadyIssued)
            {
                _buckets = buckets;
                _limit = limit;
                foreach (EquipmentElement item in alreadyIssued)
                    CountQuota(_issued, QuotaKind(item.Item));
            }

            private bool HasQuota(EquipmentElement item) => QuotaCount(_issued, QuotaKind(item.Item)) < _limit;

            internal IEnumerable<EquipmentElement> Available(int kind) => _buckets[kind].Where(HasQuota);

            internal EquipmentElement Take(int kind, Func<EquipmentElement, bool> fits)
            {
                List<EquipmentElement> bucket = _buckets[kind];
                int index = bucket.FindIndex(item => HasQuota(item) && fits(item));
                if (index < 0) return EquipmentElement.Invalid;
                EquipmentElement item = bucket[index];
                bucket.RemoveAt(index);
                CountQuota(_issued, QuotaKind(item.Item));
                return item;
            }
        }

        /// <summary>
        /// 军械库额度的种类：防具每个部位一种；武器按物品类别加武器类别细分，单手剑、单手斧、长杆、弓、弩、投矛、箭、矢等各算一种。
        /// 每种都按编制人数收集和分发，不让同属主武器的单手、双手、长杆互相挤占。（依据：用户裁定 2026-11-15）
        /// </summary>
        private static int QuotaKind(ItemObject item) =>
            (int)item.ItemType * 1000 + (IsArmoryWeapon(item) ? (int)(item.WeaponComponent?.PrimaryWeapon?.WeaponClass ?? 0) : 0);

        private static int QuotaCount(Dictionary<int, int> counts, int key) =>
            counts.TryGetValue(key, out int count) ? count : 0;

        private static void CountQuota(Dictionary<int, int> counts, int key) => counts[key] = QuotaCount(counts, key) + 1;

        private static bool IsArmoryWeapon(ItemObject item) => KitKind(item) is KitMain or KitRanged or KitThrow;

        private static bool IsArmoryKitArmed(ArmoryKit kit) =>
            Enumerable.Range(0, WeaponSlots).Any(slot => !kit.Equipment[slot].IsEmpty &&
                KitKind(kit.Equipment[slot].Item) is KitMain or KitRanged);

        private static List<TroopRosterElement> ArmoryTroops(TroopRoster roster) =>
            roster.GetTroopRoster().Where(entry => entry.Number > 0 && !entry.Character.IsHero)
                .OrderByDescending(entry => IsGuardTroop(entry.Character))
                .ThenByDescending(entry => entry.Character.Tier)
                .ThenBy(entry => entry.Character.StringId, StringComparer.Ordinal).ToList();

        private ArmoryPlan PlanForCounts(string holder, ItemRoster ledger, IReadOnlyList<TroopRosterElement> troops,
            int capacity, bool isPlayer = false)
        {
            if (_plans.TryGetValue(holder, out ArmoryPlan? plan) && ReferenceEquals(plan.Ledger, ledger) &&
                plan.LedgerVersion == ledger.VersionNo && plan.Capacity == capacity &&
                plan.Troops.Count == troops.Count &&
                plan.Troops.Select((e, i) => e.Character == troops[i].Character && e.Number == troops[i].Number).All(equal => equal))
                return plan;
            plan = BuildPlan(ledger, troops, capacity, capacity, retainPlayerStock: isPlayer);
            plan.Ledger = ledger;
            plan.LedgerVersion = ledger.VersionNo;
            plan.Capacity = capacity;
            _plans[holder] = plan;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("ARMORY_PLAN",
                "holder=" + holder + "; capacity=" + capacity + "; allocation=" + string.Join("|", plan.Sets.GroupBy(kit => kit.Troop)
                    .Select(group => group.Key.StringId + ":men=" + group.Count() + ",items=" +
                        group.Sum(kit => kit.Items.Count(item => !item.IsEmpty)) + ",first=" +
                        string.Join(",", group.First().Items.Where(item => !item.IsEmpty).Select(item => item.Item.StringId)))));
#endif
            return plan;
        }

        private static ArmoryPlan BuildPlan(ItemRoster ledger, IReadOnlyList<TroopRosterElement> troops,
            int capacity, int issuanceLimit, IReadOnlyCollection<EquipmentElement>? alreadyIssued = null, bool retainPlayerStock = false)
        {
            var plan = new ArmoryPlan { Troops = troops };
            var buckets = Enumerable.Range(0, KitSlots).Select(_ => new List<EquipmentElement>()).ToArray();
            foreach (ItemRosterElement entry in ledger)
            {
                int kind = KitKind(entry.EquipmentElement.Item);
                if (kind < 0)
                {
                    plan.Surplus.Add(entry);
                    continue;
                }
                for (int n = 0; n < entry.Amount; n++) buckets[kind].Add(entry.EquipmentElement);
            }
            foreach (List<EquipmentElement> bucket in buckets)
                bucket.Sort((a, b) => Strength(b).CompareTo(Strength(a)));
            alreadyIssued ??= Array.Empty<EquipmentElement>();
            var stock = new ArmoryStock(buckets, issuanceLimit, alreadyIssued);

            foreach (TroopRosterElement batch in troops)
            {
                List<Equipment> presets = batch.Character.BattleEquipments.ToList();
                for (int i = 0; i < batch.Number; i++)
                {
                    Equipment original = presets.Count == 0 ? new Equipment(Equipment.EquipmentType.Battle) : presets[i % presets.Count];
                    plan.Sets.Add(IsGuardTroop(batch.Character)
                        ? AllocateArmoryKit(batch.Character, original, stock)
                        : AllocateOrdinaryArmoryKit(batch.Character, original, stock));
                }
            }

            List<CharacterObject> futureWardens = troops.Where(entry => IsGuardTroop(entry.Character)).Select(entry => entry.Character)
                .Concat(new[] { CharacterObject.Find(GwpIds.NewRecruitId), CharacterObject.Find(GwpIds.ZealotId) })
                .Distinct().ToList();
            var ordinaryTroops = troops.Where(entry => !IsGuardTroop(entry.Character)).Select(entry => entry.Character).ToList();
            // 留库连已分发的在内最多编制人数件，强的先留。玩家主队按装备种类计，与分发同一口径（用户裁定 2026-11-15）；
            // NPC 领主、村庄和无领主队仍按配装大类计，灰袍每类只领一件，不为他们多囤各种武器、少卖背包货。
            int RetainKey(ItemObject item) => retainPlayerStock ? QuotaKind(item) : -1 - KitKind(item);
            var kept = new Dictionary<int, int>();
            // 每套每个留库键只计一次：NPC 按大类时，普通兵剑加矛同属主武器也只占一人份编制，与灰袍每类一件同口径。
            foreach (int key in plan.Sets.SelectMany(kit => kit.Items.Where(item => !item.IsEmpty).Select(item => RetainKey(item.Item)).Distinct())
                .Concat(alreadyIssued.Select(item => RetainKey(item.Item))))
                CountQuota(kept, key);
            for (int kind = 0; kind < buckets.Length; kind++)
            {
                foreach (EquipmentElement item in buckets[kind])
                {
                    if (QuotaCount(kept, RetainKey(item.Item)) < capacity &&
                        (retainPlayerStock || kind < KitCape &&
                        futureWardens.Any(troop => CanUseWardenArmoryItem(troop, item.Item)) ||
                        ordinaryTroops.Any(troop => CanUseOrdinaryArmoryItem(troop, item.Item))))
                    {
                        plan.Free.AddToCounts(item, 1);
                        CountQuota(kept, RetainKey(item.Item));
                    }
                    else plan.Surplus.AddToCounts(item, 1);
                }
            }
            foreach (IGrouping<CharacterObject, ArmoryKit> group in plan.Sets.GroupBy(kit => kit.Troop))
            {
                Tiers(group, out float weapon, out float armor);
                plan.TroopTiers[group.Key] = (weapon, armor);
            }
            return plan;
        }

        private static ArmoryKit AllocateOrdinaryArmoryKit(CharacterObject troop, Equipment original, ArmoryStock stock)
        {
            var kit = new ArmoryKit(troop, original.Clone());
            bool mounted = !original[EquipmentIndex.Horse].IsEmpty;
            // 每个兵每种装备最多领一件，同种第二格（如两份箭）保留原配，额度才够每人一份。（用户裁定 2026-11-15）
            var taken = new HashSet<int>();
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                EquipmentElement own = original[slot];
                if (own.IsEmpty) continue;
                int kind = KitKind(own.Item);
                if (kind < 0 || slot == EquipmentIndex.ExtraWeaponSlot || taken.Contains(QuotaKind(own.Item))) continue;
                EquipmentElement item = stock.Take(kind, candidate =>
                    MatchesOrdinaryEquipment(candidate.Item, own.Item) && !candidate.IsEqualTo(own) &&
                    CanUseOrdinaryArmoryItem(troop, candidate.Item) && (!mounted || UsableMounted(candidate.Item)) &&
                    Strength(candidate) >= Strength(own));
                if (!item.IsEmpty) taken.Add(QuotaKind(item.Item));
                kit.Equip(slot, item);
            }
            return kit;
        }

        private static bool MatchesOrdinaryEquipment(ItemObject candidate, ItemObject own) =>
            candidate.ItemType == own.ItemType && (!IsArmoryWeapon(own) ||
                candidate.WeaponComponent?.PrimaryWeapon?.WeaponClass == own.WeaponComponent?.PrimaryWeapon?.WeaponClass);

        private static ArmoryKit AllocateArmoryKit(CharacterObject troop, Equipment original, ArmoryStock stock)
        {
            ArmoryKit kit = new ArmoryKit(troop, original.Clone());
            Equipment equipment = kit.Equipment;
            bool replace = troop.StringId == GwpIds.NewRecruitId || troop.StringId == GwpIds.ZealotId;
            // 这一套骑马的（骑士），只发骑在马上用得了的武器，否则长弓、长枪、装不了箭的弩只会白占格子。
            bool mounted = !equipment[EquipmentIndex.Horse].IsEmpty;
            bool Fits(EquipmentElement e) => CanUseWardenArmoryItem(troop, e.Item) && (!mounted || UsableMounted(e.Item));
            EquipmentIndex[] armor = { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves };
            for (int kind = KitHead; kind <= KitGloves; kind++)
            {
                EquipmentElement own = equipment[armor[kind]];
                if (!replace && !own.IsEmpty) continue;
                kit.Equip(armor[kind], stock.Take(kind, e => CanUseWardenArmoryItem(troop, e.Item) && StrongerArmoryItem(e, own)));
            }

            // 主武器、投掷先选；高阶已有这一类就保留，不重新排槽，也不覆盖任何原配。
            foreach (int kind in new[] { KitMain, KitThrow })
            {
                int slot = ArmoryWeaponSlot(equipment, kind, replace);
                if (slot < 0) continue;
                EquipmentElement own = equipment[slot];
                kit.Equip((EquipmentIndex)slot, stock.Take(kind, e => Fits(e) && StrongerArmoryItem(e, own)));
            }

            // 远程必须有匹配弹药和足够的实际空格。先检查整组，不能领出来后因第五格放不下而丢掉。
            int rangedSlot = ArmoryWeaponSlot(equipment, KitRanged, replace);
            if (rangedSlot >= 0)
            {
                EquipmentElement ranged = stock.Take(KitRanged, e => Fits(e) &&
                    StrongerArmoryItem(e, equipment[rangedSlot]) && ArmoryAmmoSlot(equipment, e.Item, rangedSlot, replace, stock.Available(KitAmmo), troop) >= 0);
                if (!ranged.IsEmpty)
                {
                    int ammoSlot = ArmoryAmmoSlot(equipment, ranged.Item, rangedSlot, replace, stock.Available(KitAmmo), troop);
                    EquipmentElement ammo = equipment[ammoSlot];
                    if (ammo.IsEmpty || ammo.Item.ItemType != AmmoFor(ranged.Item))
                    {
                        ammo = stock.Take(KitAmmo, e => e.Item.ItemType == AmmoFor(ranged.Item) && CanUseWardenArmoryItem(troop, e.Item));
                        kit.Equip((EquipmentIndex)ammoSlot, ammo);
                    }
                    kit.Equip((EquipmentIndex)rangedSlot, ranged);
                }
            }
            // 原配已有弓弩但缺弹药时，只补兼容弹药，不要求先从库里再领一把弓。
            int existingRanged = -1;
            for (int slot = 0; slot < WeaponSlots; slot++)
                if (!equipment[slot].IsEmpty && KitKind(equipment[slot].Item) == KitRanged) { existingRanged = slot; break; }
            if (existingRanged >= 0)
            {
                int ammoSlot = ArmoryAmmoSlot(equipment, equipment[existingRanged].Item, existingRanged, replace, stock.Available(KitAmmo), troop);
                if (ammoSlot >= 0 && (equipment[ammoSlot].IsEmpty || equipment[ammoSlot].Item.ItemType != AmmoFor(equipment[existingRanged].Item)))
                {
                    kit.Equip((EquipmentIndex)ammoSlot, stock.Take(KitAmmo, e =>
                        e.Item.ItemType == AmmoFor(equipment[existingRanged].Item) && CanUseWardenArmoryItem(troop, e.Item)));
                }
            }
            int shieldSlot = ArmoryWeaponSlot(equipment, KitShield, replace);
            if (shieldSlot >= 0 && Enumerable.Range(0, WeaponSlots).Any(slot =>
                !equipment[slot].IsEmpty && equipment[slot].Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon))
            {
                kit.Equip((EquipmentIndex)shieldSlot, stock.Take(KitShield, e => Fits(e) && StrongerArmoryItem(e, equipment[shieldSlot])));
            }
            // 肩部印章写在兵种原配里（狂信者也有），军械库不发肩部也不发马，这里不用再改原配。
            return kit;
        }

        private static bool StrongerArmoryItem(EquipmentElement candidate, EquipmentElement own) =>
            own.IsEmpty || Strength(candidate) > Strength(own);

        private static int ArmoryWeaponSlot(Equipment equipment, int kind, bool replace)
        {
            for (int i = 0; i < WeaponSlots; i++)
                if (!equipment[i].IsEmpty && KitKind(equipment[i].Item) == kind)
                    return replace ? i : -1;
            for (int i = 0; i < WeaponSlots; i++)
                if (equipment[i].IsEmpty) return i;
            return -1;
        }

        private static int ArmoryAmmoSlot(Equipment equipment, ItemObject ranged, int rangedSlot, bool replace,
            IEnumerable<EquipmentElement> ammo, CharacterObject troop)
        {
            for (int i = 0; i < WeaponSlots; i++)
                if (i != rangedSlot && !equipment[i].IsEmpty && equipment[i].Item.ItemType == AmmoFor(ranged)) return i;
            if (!ammo.Any(e => e.Item.ItemType == AmmoFor(ranged) && CanUseWardenArmoryItem(troop, e.Item))) return -1;
            for (int i = 0; i < WeaponSlots; i++)
                if (i != rangedSlot && (equipment[i].IsEmpty || replace && KitKind(equipment[i].Item) == KitAmmo)) return i;
            return -1;
        }
    }
}
