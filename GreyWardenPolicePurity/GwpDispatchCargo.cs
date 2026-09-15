using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    internal static class GwpDispatchCargo
    {
        internal sealed class Entry
        {
            internal EquipmentElement Item;
            internal int Amount;
            internal int Price;
        }

        // Base64 keeps the outer dispatch save's pipe/semicolon delimiters unambiguous.
        internal static string Encode(IEnumerable<Entry> entries) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Join("\n", entries.Select(e => string.Join("\t", e.Item.Item.StringId,
                e.Item.ItemModifier?.StringId ?? "", e.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                e.Price.ToString(System.Globalization.CultureInfo.InvariantCulture))))));

        internal static List<Entry> Decode(string state)
        {
            var result = new List<Entry>();
            if (string.IsNullOrEmpty(state)) return result;
            foreach (string line in Encoding.UTF8.GetString(Convert.FromBase64String(state)).Split('\n'))
            {
                if (line.Length == 0) continue;
                string[] fields = line.Split('\t');
                if (fields.Length != 4 || !int.TryParse(fields[2], out int amount) || amount <= 0 ||
                    !int.TryParse(fields[3], out int price) || price <= 0) throw new FormatException("Invalid dispatch cargo manifest");
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(fields[0])
                    ?? throw new InvalidOperationException("Missing dispatch item: " + fields[0]);
                ItemModifier? modifier = fields[1].Length == 0 ? null : MBObjectManager.Instance.GetObject<ItemModifier>(fields[1])
                    ?? throw new InvalidOperationException("Missing dispatch modifier: " + fields[1]);
                result.Add(new Entry { Item = new EquipmentElement(item, modifier), Amount = amount, Price = price });
            }
            return result;
        }

        internal static int Available(MobileParty party, Entry entry) => Math.Max(0, Math.Min(entry.Amount,
            party.ItemRoster.Where(e => e.EquipmentElement.Equals(entry.Item)).Sum(e => e.Amount)));

        internal static int Food(MobileParty party, string state) => Math.Max(0, party.ItemRoster.TotalFood -
            Decode(state).Where(e => e.Item.Item.IsFood).Sum(e => Available(party, e)));

        internal static int Value(MobileParty party, string state) => (int)Math.Min(int.MaxValue,
            Decode(state).Sum(e => (long)Available(party, e) * e.Price));
    }

    // Cargo remains real inventory (weight and battle loot included). Only the native
    // daily feeding/breeding operation sees the usable stores, restored even on failure.
    [HarmonyPatch(typeof(FoodConsumptionBehavior), nameof(FoodConsumptionBehavior.DailyTickParty))]
    internal static class GwpDispatchCargoFoodPatch
    {
        private static void Prefix(MobileParty party, out List<ItemRosterElement>? __state)
        {
            __state = null;
            string? cargo = GwpWardenDispatchBehavior.Instance?.CargoFor(party);
            if (string.IsNullOrEmpty(cargo)) return;
            var entries = GwpDispatchCargo.Decode(cargo!);
            __state = new List<ItemRosterElement>();
            foreach (var entry in entries)
            {
                int count = GwpDispatchCargo.Available(party, entry);
                if (count <= 0) continue;
                party.ItemRoster.AddToCounts(entry.Item, -count);
                __state.Add(new ItemRosterElement(entry.Item, count));
            }
        }

        private static Exception? Finalizer(MobileParty party, List<ItemRosterElement>? __state, Exception? __exception)
        {
            if (__state != null)
                foreach (var item in __state) party.ItemRoster.AddToCounts(item.EquipmentElement, item.Amount);
            return __exception;
        }
    }
}
