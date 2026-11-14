using System;
using System.Collections.Generic;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

        /// <summary>
        /// 在同一份名册里把一整批兵换成另一兵种（纯化用）：人数、伤兵照原样，原兵种攒的经验不带过去——兵种树不同。
        /// 返回换了几个。
        /// </summary>
        internal static int Convert(TroopRoster roster, CharacterObject from, CharacterObject to)
        {
            int index = roster.FindIndexOfTroop(from);
            if (index < 0 || from == to)
                return 0;
            TroopRosterElement current = roster.GetElementCopyAtIndex(index);
            if (current.Number <= 0)
                return 0;
            roster.AddToCounts(from, -current.Number, woundedCount: -current.WoundedNumber);
            roster.AddToCounts(to, current.Number, woundedCount: current.WoundedNumber);
            return current.Number;
        }

        internal static bool ContainsBatch(TroopRoster source, TroopRosterElement batch)
        {
            int index = source.FindIndexOfTroop(batch.Character);
            if (index < 0) return false;
            TroopRosterElement current = source.GetElementCopyAtIndex(index);
            return current.WoundedNumber >= batch.WoundedNumber &&
                   current.Number - current.WoundedNumber >= batch.Number - batch.WoundedNumber;
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
            MoveBatch(source, destination, troop, count, wounded, xp);
            return new TroopRosterElement(troop) { Number = count, WoundedNumber = wounded, Xp = xp };
        }

        internal static void MoveSelectedBatch(PartyBase owner, TroopRoster source, TroopRoster destination,
            TroopRosterElement batch)
        {
            CharacterObject troop = batch.Character;
            TroopRosterElement current = source.GetElementCopyAtIndex(source.FindIndexOfTroop(troop));
            int xpCapacity = source == owner.PrisonRoster ? troop.ConformityNeededToRecruitPrisoner :
                Enumerable.Range(0, troop.UpgradeTargets.Length).Select(index => troop.GetUpgradeXpCost(owner, index))
                    .DefaultIfEmpty().Max();
            int xp = TroopTransferRules.SelectedExperience(current.Xp, current.Number, batch.Number, xpCapacity);
            MoveBatch(source, destination, troop, batch.Number, batch.WoundedNumber, xp);
        }

        private static void MoveBatch(TroopRoster source, TroopRoster destination, CharacterObject troop,
            int count, int wounded, int xp)
        {
            source.AddToCounts(troop, -count, false, -wounded, -xp);
            destination.AddToCounts(troop, count, false, wounded, xp);
        }
    }
}
