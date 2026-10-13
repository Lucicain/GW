using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GreyWardenPolicePurity
{
    /// <summary>搬一批兵的实际人数、伤兵与共享经验；选兵、编制和差事由调用方决定。</summary>
    internal static class GwpTroopTransfer
    {
        internal static int MoveBatches(TroopRoster source, TroopRoster destination,
            IEnumerable<TroopRosterElement> batches, int requested, bool healthyOnly = false)
        {
            int moved = 0;
            foreach (TroopRosterElement batch in batches.ToList())
            {
                if (moved >= requested)
                    break;
                moved += Move(source, destination, batch.Character, Math.Min(batch.Number, requested - moved), healthyOnly).Number;
            }
            return moved;
        }

        internal static TroopRosterElement Move(TroopRoster source, TroopRoster destination,
            CharacterObject troop, int requested, bool healthyOnly = false, int? exactXp = null)
        {
            int index = source.FindIndexOfTroop(troop);
            if (source == destination || requested <= 0 || index < 0)
                return default;
            TroopRosterElement current = source.GetElementCopyAtIndex(index);
            int healthy = current.Number - current.WoundedNumber;
            int count = Math.Min(requested, healthyOnly ? healthy : current.Number);
            if (count <= 0)
                return default;
            int wounded = Math.Max(0, count - healthy);
            // 原版 PartyScreenLogic.TransferTroopToLeaderSlot 按整除后的人均经验搬运；
            // 整批搬空则带走整个池，避免原版移除空行时丢掉整除余数。回滚可交回原先搬出的精确经验。
            int xp = Math.Min(current.Xp, exactXp ??
                (count == current.Number ? current.Xp : count * (current.Xp / current.Number)));
            source.AddToCounts(troop, -count, false, -wounded, -xp);
            destination.AddToCounts(troop, count, false, wounded, xp);
            return new TroopRosterElement(troop) { Number = count, WoundedNumber = wounded, Xp = xp };
        }
    }
}
