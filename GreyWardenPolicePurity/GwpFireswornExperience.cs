using System;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        /// <summary>
        /// 巡逻与紧急出动的战斗经验供本村留守灰袍兵，参战队不吃这份经验（用户裁定 2026-10-09）。
        /// 在原版 CommitXpGain 入口替换收款方；仍按原版筛选未战死者、逐兵计算并取整，再清除已结算的增量。
        /// 不留内存待办：经验当场写入村庄实体民兵名册，由原版保存、累积与升级。
        /// </summary>
        internal static bool CommitWatchExperience(MapEventParty battleParty)
        {
            if (_instance == null || !_instance._faithParties.TryGetValue(battleParty.Party.Id, out FaithParty? record) ||
                !IsWatch(record.Kind))
                return false;

            int experience = battleParty.Troops.Where(troop => !troop.IsKilled && troop.XpGained > 0)
                .Sum(troop => TaleWorlds.Library.MathF.Round(Campaign.Current.Models.PartyTrainingModel
                    .CalculateXpGainFromBattles(troop, battleParty.Party).ResultNumber));
            battleParty.Troops.ResetTroopXP();
            if (_instance._villages.TryGetValue(record.HomeId, out FaithVillage? row))
                _instance.GrantGuardExperience(row, experience, "watch:" + record.PartyId);
            return true;
        }

        /// <summary>
        /// 只给村内灰袍兵分经验，满级兵、杂兵和外出队不占份额。
        /// 照原版 MobilePartyHelper.PartyAddSharedXp 按各兵种剩余升级经验容量分配；
        /// 容量由原版 CanTroopGainXp 查询，不改升级门槛、费用或分支。多出的经验不存成另一个池。
        /// </summary>
        private int GrantGuardExperience(FaithVillage row, int experience, string source)
        {
            if (experience <= 0)
                return 0;
            ObserveGuard(row);
            var village = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(row.VillageId);
            var militia = village?.MilitiaPartyComponent?.MobileParty;
            if (militia?.IsActive != true)
                return 0;

            var recipients = militia.MemberRoster.GetTroopRoster()
                .Where(troop => IsGuardTroop(troop.Character))
                .Select(troop =>
                {
                    MobilePartyHelper.CanTroopGainXp(militia.Party, troop.Character, out int capacity);
                    return (troop.Character, Capacity: capacity);
                }).Where(troop => troop.Capacity > 0).ToList();
            int capacityLeft = recipients.Sum(troop => troop.Capacity);
            int left = experience;
            foreach (var troop in recipients)
            {
                if (left <= 0)
                    break;
                int take = Math.Min(troop.Capacity, Math.Min(left,
                    Math.Max(1, (int)((long)left * troop.Capacity / capacityLeft))));
                militia.MemberRoster.AddXpToTroop(troop.Character, take);
                left -= take;
                capacityLeft -= troop.Capacity;
            }
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("GUARD_EXPERIENCE",
                "village=" + row.VillageId + "; source=" + source + "; earned=" + experience +
                "; granted=" + (experience - left) + "; unused=" + left);
#endif
            return experience - left;
        }
    }

    [HarmonyPatch(typeof(MapEventParty), "CommitXpGain")]
    internal static class GwpWatchExperiencePatch
    {
        private static bool Prefix(MapEventParty __instance) =>
            !GwpFireswornBehavior.CommitWatchExperience(__instance);
    }
}
