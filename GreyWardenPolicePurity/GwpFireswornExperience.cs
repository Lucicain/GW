using System;
using System.Linq;
using Helpers;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
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
}
