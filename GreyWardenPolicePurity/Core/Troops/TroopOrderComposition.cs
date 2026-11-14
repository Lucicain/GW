using System.Collections.Generic;
using System.Globalization;

namespace GreyWardenPolicePurity.Core.Troops
{
    public static class TroopOrderComposition
    {
        public static string Encode(IEnumerable<(string TroopId, int Count)> lines)
        {
            var encoded = new List<string>();
            foreach (var line in lines)
                encoded.Add(line.TroopId + ":" + line.Count.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", encoded);
        }

        public static IReadOnlyList<(string TroopId, int Count)> Decode(string composition)
        {
            var lines = new List<(string, int)>();
            if (string.IsNullOrEmpty(composition)) return lines;
            foreach (string entry in composition.Split(','))
            {
                int delimiter = entry.LastIndexOf(':');
                if (delimiter <= 0 || !int.TryParse(entry.Substring(delimiter + 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int count) || count <= 0)
                    return System.Array.Empty<(string, int)>();
                lines.Add((entry.Substring(0, delimiter), count));
            }
            return lines;
        }
    }
}
