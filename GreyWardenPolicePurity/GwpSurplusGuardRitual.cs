using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        private int BurnSurplusGuards(FaithVillage row, Settlement village, int budget,
            Dictionary<CharacterObject, int> promoted, ref int experience)
        {
            int excess = Math.Max(0, Sworn(row) - GuardCap(row) * GwpTuning.Firesworn.SurplusGuardCapMultiplier);
            int remaining = Math.Min(budget, excess);
            var militia = village.MilitiaPartyComponent?.MobileParty;
            if (remaining < 2 || militia?.IsActive != true || militia.MapEvent != null || !IsPriestStrongest(row, village))
                return 0;
            int burned = 0;
            foreach (TroopRosterElement entry in militia.MemberRoster.GetTroopRoster()
                .Where(e => IsGuardTroop(e.Character) && e.Character.UpgradeTargets.Any(t => t.Tier > e.Character.Tier))
                .OrderBy(e => e.Character.Tier).ThenBy(e => e.Character.StringId, StringComparer.Ordinal).ToList())
            {
                int take = Math.Min(remaining, entry.Number) / 2 * 2;
                if (take == 0) continue;
                CharacterObject[] targets = entry.Character.UpgradeTargets.Where(t => t.Tier > entry.Character.Tier).ToArray();
                for (int pair = 0; pair < take / 2; pair++)
                {
                    CharacterObject target = targets[MBRandom.RandomInt(targets.Length)];
                    promoted.TryGetValue(target, out int count);
                    promoted[target] = count + 1;
                }
                int wounded = Math.Max(0, take - (entry.Number - entry.WoundedNumber));
                militia.MemberRoster.AddToCounts(entry.Character, -take, false, -wounded);
                GuardOf(row.VillageId).AddToCounts(entry.Character, -take);
                experience += Campaign.Current.Models.PartyTrainingModel.GetXpReward(entry.Character) * (take / 2);
                burned += take;
                remaining -= take;
                if (remaining < 2) break;
            }
            // 原配不生成库存；被烧者的实际库装仍在村账，随后供留守者重新分配。
            _plans.Remove(row.VillageId);
            return burned;
        }
    }
}
