using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    // IDs, not live object references: missing mod items must not destroy an old account.
    internal sealed class GwpCaseReceipt
    {
        internal sealed class Item
        {
            internal string Id = "", Modifier = "";
            internal int Amount, Price;
            internal bool Matches(EquipmentElement e) => e.Item.StringId == Id && (e.ItemModifier?.StringId ?? "") == Modifier;
        }
        internal int Gold;
        internal readonly List<Item> Items = new List<Item>();
        internal int PriceFor(EquipmentElement equipment) => Math.Max(1,
            Items.FirstOrDefault(i => i.Amount > 0 && i.Matches(equipment))?.Price ?? equipment.ItemValue);
        internal string Encode() => Gold.ToString(CultureInfo.InvariantCulture) + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            string.Join("\n", Items.Select(e => string.Join("\t", e.Id, e.Modifier, e.Amount.ToString(CultureInfo.InvariantCulture), e.Price.ToString(CultureInfo.InvariantCulture))))));
        internal static GwpCaseReceipt? Decode(string? state)
        {
            if (string.IsNullOrEmpty(state)) return null; // Unknown legacy receipt, not an empty receipt.
            string[] parts = state!.Split(':');
            if (parts.Length != 2) throw new FormatException("Invalid case receipt");
            var receipt = new GwpCaseReceipt { Gold = int.Parse(parts[0], CultureInfo.InvariantCulture) };
            foreach (string line in Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])).Split('\n'))
            {
                if (line.Length == 0) continue;
                string[] fields = line.Split('\t');
                if (fields.Length != 4) throw new FormatException("Invalid case receipt item");
                receipt.Items.Add(new Item { Id = fields[0], Modifier = fields[1], Amount = int.Parse(fields[2], CultureInfo.InvariantCulture), Price = int.Parse(fields[3], CultureInfo.InvariantCulture) });
            }
            return receipt;
        }
        internal void Add(GwpCaseReceipt other)
        {
            Gold = checked(Gold + other.Gold);
            foreach (var item in other.Items)
                Items.Add(new Item { Id = item.Id, Modifier = item.Modifier, Amount = item.Amount, Price = item.Price });
        }
    }
}
