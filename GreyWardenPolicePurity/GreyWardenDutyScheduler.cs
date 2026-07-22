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
                GreyWardenFamilyBehavior.DutyKind.CaravanProtection =>
                    HasWaitingCrime(GwpCrimeCategory.CaravanAttack),
                GreyWardenFamilyBehavior.DutyKind.VillageProtection =>
                    HasWaitingCrime(GwpCrimeCategory.VillageViolence),
                GreyWardenFamilyBehavior.DutyKind.IssueResolution =>
                    GreyWardenIssueResolutionBehavior.HasAvailableIssue(),
                GreyWardenFamilyBehavior.DutyKind.Reconstruction =>
                    GreyWardenVillageReconstructionBehavior.HasAvailableReconstruction(),
                _ => false
            };
        }

        private static bool HasWaitingCrime(GwpCrimeCategory category) =>
            CrimePool.LedgerRecords.Any(record => record.HasOpenCase &&
                record.CrimeCategory == category &&
                !CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == record.CrimeId));
    }
}
