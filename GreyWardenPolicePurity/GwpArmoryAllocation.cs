using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        // 每人一套完整原配 + 实际领出的库装。原配不计入库存，只有 Items 参与搬运和损耗。
        private sealed class ArmoryKit
        {
            public readonly CharacterObject Troop;
            public readonly Equipment Equipment;
            public readonly EquipmentElement[] Items = new EquipmentElement[KitSlots];
            public readonly int[] Slots = Enumerable.Repeat(-1, KitSlots).ToArray();
            public EquipmentElement this[int kind] => Items[kind];
            public ArmoryKit(CharacterObject troop, Equipment equipment) { Troop = troop; Equipment = equipment; }
        }

        private sealed class ArmoryPlan
        {
            public readonly List<ArmoryKit> Sets = new List<ArmoryKit>();
            public IReadOnlyList<TroopRosterElement> Troops = Array.Empty<TroopRosterElement>();
            public ItemRoster? Ledger;
            public int LedgerVersion, Reserve;
            public readonly ItemRoster Free = new ItemRoster();
            public readonly ItemRoster Surplus = new ItemRoster();
            // 自动结算每次模拟命中都要查，建方案时按兵种算好，不在命中时再遍历套装。
            public readonly Dictionary<CharacterObject, (float Weapon, float Armor)> TroopTiers =
                new Dictionary<CharacterObject, (float Weapon, float Armor)>();
        }

        private static bool IsArmoryKitArmed(ArmoryKit kit) =>
            Enumerable.Range(0, WeaponSlots).Any(slot => !kit.Equipment[slot].IsEmpty &&
                (KitKind(kit.Equipment[slot].Item) == KitMain || KitKind(kit.Equipment[slot].Item) == KitRanged));

        private static bool CanReplaceArmoryEquipment(CharacterObject troop) =>
            troop.StringId == GwpIds.NewRecruitId || troop.StringId == GwpIds.ZealotId;

        private static List<TroopRosterElement> ArmoryTroops(TroopRoster roster) =>
            roster.GetTroopRoster().Where(e => IsGuardTroop(e.Character) && e.Number > 0)
                .OrderByDescending(e => e.Character.Tier)
                .ThenBy(e => e.Character.StringId, StringComparer.Ordinal).ToList();

        // 人数按兵种逐项核对；升级后即使总人数不变也会失效。伤情/经验不改变配装，不需要进缓存键。
        private ArmoryPlan PlanForCounts(string holder, ItemRoster ledger, IReadOnlyList<TroopRosterElement> troops, int reserve)
        {
            if (_plans.TryGetValue(holder, out ArmoryPlan? plan) && ReferenceEquals(plan.Ledger, ledger) &&
                plan.LedgerVersion == ledger.VersionNo && plan.Reserve == reserve && plan.Troops.Count == troops.Count &&
                plan.Troops.Select((e, i) => e.Character == troops[i].Character && e.Number == troops[i].Number).All(equal => equal))
                return plan;
            plan = BuildPlan(ledger, troops, reserve);
            plan.Ledger = ledger;
            plan.LedgerVersion = ledger.VersionNo;
            plan.Reserve = reserve;
            _plans[holder] = plan;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("ARMORY_PLAN",
                "holder=" + holder + "; reserve=" + reserve + "; allocation=" + string.Join("|", plan.Sets.GroupBy(kit => kit.Troop)
                    .Select(group => group.Key.StringId + ":men=" + group.Count() + ",items=" +
                        group.Sum(kit => kit.Items.Count(item => !item.IsEmpty)) + ",first=" +
                        string.Join(",", group.First().Items.Where(item => !item.IsEmpty).Select(item => item.Item.StringId)))));
#endif
            return plan;
        }

        private static ArmoryPlan BuildPlan(ItemRoster ledger, IReadOnlyList<TroopRosterElement> troops, int reserve)
        {
            var ordered = troops.Where(e => IsGuardTroop(e.Character) && e.Number > 0)
                .OrderByDescending(e => e.Character.Tier).ThenBy(e => e.Character.StringId, StringComparer.Ordinal).ToList();
            ArmoryPlan plan = new ArmoryPlan { Troops = ordered };
            List<EquipmentElement>[] buckets = Enumerable.Range(0, KitSlots).Select(_ => new List<EquipmentElement>()).ToArray();
            // 空编制仍按低阶兵可用装备预留；现有高阶能用的装备也可留下，不能先按低阶技能丢掉。
            var users = ordered.Select(e => e.Character).Concat(new[] {
                CharacterObject.Find(GwpIds.NewRecruitId), CharacterObject.Find(GwpIds.ZealotId) }).Distinct().ToList();
            for (int i = 0; i < ledger.Count; i++)
            {
                ItemRosterElement entry = ledger.GetElementCopyAtIndex(i);
                int kind = KitKind(entry.EquipmentElement.Item);
                if (kind < 0 || !users.Any(troop => CanUse(troop, entry.EquipmentElement.Item)))
                {
                    plan.Surplus.AddToCounts(entry.EquipmentElement, entry.Amount);
                    continue;
                }
                for (int n = 0; n < entry.Amount; n++) buckets[kind].Add(entry.EquipmentElement);
            }
            foreach (List<EquipmentElement> bucket in buckets)
                bucket.Sort((a, b) => Strength(b).CompareTo(Strength(a)));

            foreach (TroopRosterElement batch in ordered)
            {
                // 一个人对应一套真实预设；按兵种内序号轮换，存读和库存重算时不会重新拼装原配。
                List<Equipment> presets = batch.Character.BattleEquipments.ToList();
                for (int i = 0; i < batch.Number; i++)
                {
                    Equipment original = presets.Count == 0 ? new Equipment(Equipment.EquipmentType.Battle) : presets[i % presets.Count];
                    plan.Sets.Add(AllocateArmoryKit(batch.Character, original, buckets));
                }
            }
            for (int kind = 0; kind < buckets.Length; kind++)
            {
                int keep = reserve + plan.Sets.Count - plan.Sets.Count(kit => !kit[kind].IsEmpty);
                for (int i = 0; i < buckets[kind].Count; i++)
                    (i < keep ? plan.Free : plan.Surplus).AddToCounts(buckets[kind][i], 1);
            }
            foreach (IGrouping<CharacterObject, ArmoryKit> group in plan.Sets.GroupBy(kit => kit.Troop))
            {
                Tiers(group, out float weapon, out float armor);
                plan.TroopTiers[group.Key] = (weapon, armor);
            }
            return plan;
        }

        private static ArmoryKit AllocateArmoryKit(CharacterObject troop, Equipment original, List<EquipmentElement>[] buckets)
        {
            ArmoryKit kit = new ArmoryKit(troop, original.Clone());
            Equipment equipment = kit.Equipment;
            bool replace = CanReplaceArmoryEquipment(troop);
            EquipmentIndex[] armor = { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves };
            for (int kind = KitHead; kind <= KitGloves; kind++)
            {
                EquipmentElement own = equipment[armor[kind]];
                if (!replace && !own.IsEmpty) continue;
                EquipmentElement item = Take(buckets[kind], e => CanUse(troop, e.Item) && StrongerArmoryItem(e, own));
                if (item.IsEmpty) continue;
                kit.Items[kind] = item;
                kit.Slots[kind] = (int)armor[kind];
                equipment[armor[kind]] = item;
            }

            // 主武器、投掷先选；高阶已有这一类就保留，不重新排槽，也不覆盖任何原配。
            foreach (int kind in new[] { KitMain, KitThrow })
            {
                int slot = ArmoryWeaponSlot(equipment, kind, replace);
                if (slot < 0) continue;
                EquipmentElement own = equipment[slot];
                EquipmentElement item = Take(buckets[kind], e => CanUse(troop, e.Item) && StrongerArmoryItem(e, own));
                if (item.IsEmpty) continue;
                kit.Items[kind] = item;
                kit.Slots[kind] = slot;
                equipment[slot] = item;
            }

            // 远程必须有匹配弹药和足够的实际空格。先检查整组，不能领出来后因第五格放不下而丢掉。
            int rangedSlot = ArmoryWeaponSlot(equipment, KitRanged, replace);
            if (rangedSlot >= 0)
            {
                EquipmentElement ranged = Take(buckets[KitRanged], e => CanUse(troop, e.Item) &&
                    StrongerArmoryItem(e, equipment[rangedSlot]) && ArmoryAmmoSlot(equipment, e.Item, rangedSlot, replace, buckets[KitAmmo], troop) >= 0);
                if (!ranged.IsEmpty)
                {
                    int ammoSlot = ArmoryAmmoSlot(equipment, ranged.Item, rangedSlot, replace, buckets[KitAmmo], troop);
                    EquipmentElement ammo = equipment[ammoSlot];
                    if (ammo.IsEmpty || ammo.Item.ItemType != AmmoFor(ranged.Item))
                    {
                        ammo = Take(buckets[KitAmmo], e => e.Item.ItemType == AmmoFor(ranged.Item) && CanUse(troop, e.Item));
                        kit.Items[KitAmmo] = ammo;
                        kit.Slots[KitAmmo] = ammoSlot;
                    }
                    kit.Items[KitRanged] = ranged;
                    kit.Slots[KitRanged] = rangedSlot;
                    equipment[rangedSlot] = ranged;
                    equipment[ammoSlot] = ammo;
                }
            }
            // 原配已有弓弩但缺弹药时，只补兼容弹药，不要求先从库里再领一把弓。
            int existingRanged = -1;
            for (int slot = 0; slot < WeaponSlots; slot++)
                if (!equipment[slot].IsEmpty && KitKind(equipment[slot].Item) == KitRanged) { existingRanged = slot; break; }
            if (existingRanged >= 0)
            {
                int ammoSlot = ArmoryAmmoSlot(equipment, equipment[existingRanged].Item, existingRanged, replace, buckets[KitAmmo], troop);
                if (ammoSlot >= 0 && (equipment[ammoSlot].IsEmpty || equipment[ammoSlot].Item.ItemType != AmmoFor(equipment[existingRanged].Item)))
                {
                    EquipmentElement ammo = Take(buckets[KitAmmo], e => e.Item.ItemType == AmmoFor(equipment[existingRanged].Item) && CanUse(troop, e.Item));
                    kit.Items[KitAmmo] = ammo;
                    kit.Slots[KitAmmo] = ammoSlot;
                    equipment[ammoSlot] = ammo;
                }
            }
            int shieldSlot = ArmoryWeaponSlot(equipment, KitShield, replace);
            if (shieldSlot >= 0 && Enumerable.Range(0, WeaponSlots).Any(slot =>
                !equipment[slot].IsEmpty && equipment[slot].Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon))
            {
                EquipmentElement item = Take(buckets[KitShield], e => CanUse(troop, e.Item) && StrongerArmoryItem(e, equipment[shieldSlot]));
                if (!item.IsEmpty) { kit.Items[KitShield] = item; kit.Slots[KitShield] = shieldSlot; equipment[shieldSlot] = item; }
            }
            if (replace)
            {
                equipment[EquipmentIndex.Cape] = CharacterObject.Find(GwpIds.NewRecruitId).FirstBattleEquipment[EquipmentIndex.Cape];
                equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
            }
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
            List<EquipmentElement> ammo, CharacterObject troop)
        {
            for (int i = 0; i < WeaponSlots; i++)
                if (i != rangedSlot && !equipment[i].IsEmpty && equipment[i].Item.ItemType == AmmoFor(ranged)) return i;
            if (!ammo.Any(e => e.Item.ItemType == AmmoFor(ranged) && CanUse(troop, e.Item))) return -1;
            for (int i = 0; i < WeaponSlots; i++)
                if (i != rangedSlot && (equipment[i].IsEmpty || replace && KitKind(equipment[i].Item) == KitAmmo)) return i;
            return -1;
        }
    }
}
