using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍巡区：在编的灰袍领主各守一座不同文化的城，作为巡逻圆心。领主闲时去那里巡逻
    /// （<see cref="GreyWardenPartyDesireBehavior"/>），信众村也尽量长在这些巡区一带（用户裁定 2026-10-15）。
    ///
    /// 原版防御巡逻分与离家距离成反比，而灰袍的家乡是同一个，闲下来都挤在出生点附近。这里不改"家"
    /// （灰袍会新增、会老死，开档时钉死的驻地很快就不对），而是每次按当前在编名单现算：领主按 StringId
    /// 定序，每种文化取 StringId 最小的那座非城堡城，按序号取模分配。人员一变，分配自己就重排。
    /// </summary>
    internal static class GwpPatrolBeats
    {
        /// <summary>这位灰袍领主的巡区圆心；不在编（没带队、不是领主队）时为 null。</summary>
        internal static Settlement? BeatOf(MobileParty party)
        {
            List<MobileParty> roster = Roster();
            int index = roster.FindIndex(candidate =>
                string.Equals(candidate.StringId, party.StringId, StringComparison.OrdinalIgnoreCase));
            List<Settlement> centers = Centers();
            return index < 0 || centers.Count == 0 ? null : centers[index % centers.Count];
        }

        /// <summary>每座在用的巡区圆心，以及守它的领主人数。</summary>
        internal static Dictionary<Settlement, int> Assignments()
        {
            var result = new Dictionary<Settlement, int>();
            List<Settlement> centers = Centers();
            if (centers.Count == 0) return result;
            List<MobileParty> roster = Roster();
            for (int i = 0; i < roster.Count; i++)
            {
                Settlement center = centers[i % centers.Count];
                result[center] = result.TryGetValue(center, out int count) ? count + 1 : 1;
            }
            return result;
        }

        private static List<MobileParty> Roster() =>
            PoliceStats.GetAllPoliceParties()
                .Where(candidate => candidate?.IsActive == true &&
                    candidate.IsLordParty && candidate.LeaderHero?.IsActive == true)
                .OrderBy(candidate => candidate.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static List<Settlement> Centers() =>
            Town.AllTowns
                .Where(town => town?.Settlement != null && !town.IsCastle)
                .GroupBy(town => town.Settlement.Culture?.StringId ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(town => town.Settlement.StringId, StringComparer.OrdinalIgnoreCase)
                    .First().Settlement)
                .ToList();
    }
}
