using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    internal static class GreyWardenDutyScheduler
    {
        internal static bool HasPreferredWork(MobileParty? party)
        {
            if (!GreyWardenFamilyBehavior.TryGetDuty(party?.LeaderHero,
                    out GreyWardenFamilyBehavior.DutyKind duty)) return false;

            return duty switch
            {
                GreyWardenFamilyBehavior.DutyKind.RoadsAndVillages => HasWaitingDutyCase(),
                GreyWardenFamilyBehavior.DutyKind.IssueResolution =>
                    GreyWardenIssueResolutionBehavior.HasAvailableIssue(),
                GreyWardenFamilyBehavior.DutyKind.Reconstruction =>
                    GreyWardenVillageReconstructionBehavior.HasAvailableReconstruction(),
                _ => false
            };
        }

        /// <summary>乡路巡法本职负责的案件：袭击村民、袭击商队与烧村（用户裁定 2026-11-16）。</summary>
        internal static bool IsRoadsAndVillagesCase(GwpCrimeCategory category) =>
            category == GwpCrimeCategory.CaravanAttack || category == GwpCrimeCategory.VillageViolence;

        private static bool HasWaitingDutyCase() =>
            CrimePool.LedgerRecords.Any(record => record.HasOpenCase &&
                IsRoadsAndVillagesCase(record.CrimeCategory) &&
                !CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == record.CrimeId));
    }
}
