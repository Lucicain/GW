using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Makes unresolved native town and village issues part of the unlimited Grey Warden
    /// task pool. A party must reach the issuer and remain for six hours before invoking
    /// Bannerlord's own AI-lord completion path with a guaranteed success chance.
    /// </summary>
    public sealed class GreyWardenIssueResolutionBehavior : CampaignBehaviorBase
    {
        private const string IssueIdsKey = "GWPP_IssueDutyIssueIds";
        private const string OwnerIdsKey = "GWPP_IssueDutyOwnerIds";
        private const string PartyIdsKey = "GWPP_IssueDutyPartyIds";
        private const string WorkEndHoursKey = "GWPP_IssueDutyWorkEndHours";

        private static GreyWardenIssueResolutionBehavior? _instance;
        private readonly List<IssueAssignment> _assignments = new List<IssueAssignment>();

        internal enum IssueDutyStage
        {
            WaitingForAssignment,
            TravelingToIssuer,
            ReviewingPetition
        }

        internal sealed class IssueTaskSnapshot
        {
            public string IssueId { get; set; } = string.Empty;
            public string IssueTitle { get; set; } = string.Empty;
            public string OwnerName { get; set; } = string.Empty;
            public string SettlementName { get; set; } = string.Empty;
            public string PolicePartyId { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public IssueDutyStage Stage { get; set; }
            public double RemainingHours { get; set; }
            public bool IsAssigned => !string.IsNullOrWhiteSpace(PolicePartyId);
        }

        private sealed class IssueAssignment
        {
            public string IssueId { get; set; } = string.Empty;
            public string OwnerHeroId { get; set; } = string.Empty;
            public string PolicePartyId { get; set; } = string.Empty;
            public double WorkEndHours { get; set; }
        }

        public GreyWardenIssueResolutionBehavior() => _instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? issueIds = null;
            List<string>? ownerIds = null;
            List<string>? partyIds = null;
            List<double>? workEnds = null;
            if (dataStore.IsSaving)
            {
                issueIds = _assignments.Select(entry => entry.IssueId).ToList();
                ownerIds = _assignments.Select(entry => entry.OwnerHeroId).ToList();
                partyIds = _assignments.Select(entry => entry.PolicePartyId).ToList();
                workEnds = _assignments.Select(entry => entry.WorkEndHours).ToList();
            }

            dataStore.SyncData(IssueIdsKey, ref issueIds);
            dataStore.SyncData(OwnerIdsKey, ref ownerIds);
            dataStore.SyncData(PartyIdsKey, ref partyIds);
            dataStore.SyncData(WorkEndHoursKey, ref workEnds);
            if (!dataStore.IsLoading) return;

            _assignments.Clear();
            int count = new[] { issueIds?.Count ?? 0, ownerIds?.Count ?? 0,
                partyIds?.Count ?? 0, workEnds?.Count ?? 0 }.Min();
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(issueIds![i]) ||
                    string.IsNullOrWhiteSpace(ownerIds![i])) continue;
                _assignments.Add(new IssueAssignment
                {
                    IssueId = issueIds[i],
                    OwnerHeroId = ownerIds[i],
                    PolicePartyId = partyIds![i] ?? string.Empty,
                    WorkEndHours = workEnds![i]
                });
            }
        }

        internal static bool IsIssueDutyParty(MobileParty? party) =>
            party != null && _instance != null && _instance._assignments.Any(entry =>
                string.Equals(entry.PolicePartyId, party.StringId, StringComparison.OrdinalIgnoreCase));

        internal static bool HasAvailableIssue() => _instance != null &&
            GreyWardenFamilyBehavior.HasLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.IssueResolution) &&
            _instance.GetEligibleIssues().Any(issue => !_instance.IsAssigned(issue));

        internal static bool ShouldReserveFromOrdinaryCases(MobileParty? party)
        {
            if (party == null || _instance == null) return false;
            if (IsIssueDutyParty(party)) return true;
            return GreyWardenFamilyBehavior.IsIssueResolutionParty(party) && HasAvailableIssue();
        }

        internal static void ReleasePartyForForcedDuty(MobileParty? party)
        {
            if (party == null || _instance == null) return;
            IssueAssignment? assignment = _instance._assignments.FirstOrDefault(entry =>
                string.Equals(entry.PolicePartyId, party.StringId, StringComparison.OrdinalIgnoreCase));
            if (assignment == null) return;
            _instance.Unassign(assignment, party, "forced_duty");
        }

        internal static IReadOnlyList<IssueTaskSnapshot> GetTaskSnapshots()
        {
            var result = new List<IssueTaskSnapshot>();
            if (_instance == null || !GreyWardenFamilyBehavior.HasLivingDutyHolder(
                    GreyWardenFamilyBehavior.DutyKind.IssueResolution)) return result;

            foreach (IssueBase issue in _instance.GetEligibleIssues())
            {
                IssueAssignment? assignment = _instance.FindAssignment(issue);
                Settlement? settlement = ResolveSettlement(issue);
                result.Add(new IssueTaskSnapshot
                {
                    IssueId = issue.StringId ?? string.Empty,
                    IssueTitle = issue.Title?.ToString() ?? string.Empty,
                    OwnerName = issue.IssueOwner?.Name?.ToString() ?? string.Empty,
                    SettlementName = settlement?.Name?.ToString() ?? string.Empty,
                    PolicePartyId = assignment?.PolicePartyId ?? string.Empty,
                    QueuedTime = issue.IssueCreationTime,
                    Stage = assignment == null || string.IsNullOrWhiteSpace(assignment.PolicePartyId)
                        ? IssueDutyStage.WaitingForAssignment
                        : assignment.WorkEndHours > 0d
                            ? IssueDutyStage.ReviewingPetition
                            : IssueDutyStage.TravelingToIssuer,
                    RemainingHours = assignment?.WorkEndHours > 0d
                        ? Math.Max(0d, assignment.WorkEndHours - CampaignTime.Now.ToHours)
                        : 0d
                });
            }
            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter) => _assignments.Clear();
        private void OnGameLoaded(CampaignGameStarter starter) => Normalize();
        private void OnSessionLaunched(CampaignGameStarter starter) => Normalize();

        private void OnHourlyTick()
        {
            if (!GreyWardenFamilyBehavior.HasLivingDutyHolder(
                    GreyWardenFamilyBehavior.DutyKind.IssueResolution))
            {
                ClearExtinctDuty();
                return;
            }

            Normalize();
            UpdateAssignments();
            AssignIssues();
        }

        private IEnumerable<IssueBase> GetEligibleIssues()
        {
            IssueManager? manager = Campaign.Current?.IssueManager;
            if (manager == null) return Enumerable.Empty<IssueBase>();
            return manager.Issues.Values.Where(IsEligibleIssue).ToList();
        }

        private static bool IsEligibleIssue(IssueBase? issue)
        {
            if (issue?.IssueOwner == null || !issue.IsOngoingWithoutQuest ||
                !issue.CanBeCompletedByAI()) return false;
            Settlement? settlement = ResolveSettlement(issue);
            return settlement != null && (settlement.IsTown || settlement.IsVillage);
        }

        private static Settlement? ResolveSettlement(IssueBase issue) =>
            issue.IssueSettlement ?? issue.IssueOwner?.CurrentSettlement;

        private void Normalize()
        {
            var valid = new HashSet<string>(GetEligibleIssues()
                .Select(IssueKey), StringComparer.OrdinalIgnoreCase);
            foreach (IssueAssignment assignment in _assignments.ToList())
            {
                if (valid.Contains(AssignmentKey(assignment))) continue;
                MobileParty? party = FindParty(assignment.PolicePartyId);
                FinishWithoutCompletion(assignment, party, "issue_no_longer_open");
            }
        }

        private void UpdateAssignments()
        {
            foreach (IssueAssignment assignment in _assignments
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.PolicePartyId)).ToList())
            {
                IssueBase? issue = FindIssue(assignment);
                MobileParty? party = FindParty(assignment.PolicePartyId);
                Settlement? settlement = issue == null ? null : ResolveSettlement(issue);
                if (!IsEligibleIssue(issue) || settlement == null)
                {
                    FinishWithoutCompletion(assignment, party, "issue_invalid");
                    continue;
                }
                if (party?.IsActive != true || party.LeaderHero?.IsActive != true)
                {
                    Unassign(assignment, party, "assignee_invalid");
                    continue;
                }
                if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(party) ||
                    party.Army != null)
                {
                    Unassign(assignment, party, "higher_priority_duty");
                    continue;
                }
                if (party.MapEvent != null && !party.MapEvent.IsFinalized) continue;

                if (assignment.WorkEndHours <= 0d)
                {
                    if (HasArrived(party, settlement))
                    {
                        assignment.WorkEndHours = CampaignTime.Now.ToHours +
                                                  GwpTuning.IssueResolution.WorkHours;
                        HoldAtSettlement(party, settlement);
                    }
                    else
                        GreyWardenPartyDesireBehavior.RequestVisit(party, settlement, 8f);
                    continue;
                }

                HoldAtSettlement(party, settlement);
                if (CampaignTime.Now.ToHours < assignment.WorkEndHours) continue;

                string title = issue!.Title?.ToString() ?? string.Empty;
                issue.CompleteIssueWithAiLord(party.LeaderHero);
                ApplyLocalDevelopmentGain(settlement);
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
                GwpPlayerRequestDeferral.NotifyDutyCompleted(party,
                    "issue_resolution");
                _assignments.Remove(assignment);
                GreyWardenPartyDesireBehavior.ClearIntent(party);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
                GwpAiDiagnostics.WriteAction(party, "ISSUE_DUTY_COMPLETED",
                    "issue=" + assignment.IssueId + "; title=" + title +
                    "; settlement=" + settlement.StringId +
                    "; development=" + GwpTuning.IssueResolution.LocalDevelopmentGain +
                    "; grant=" + PoliceResourceManager.SuccessfulCaseReward);
            }
        }

        private static void ApplyLocalDevelopmentGain(Settlement settlement)
        {
            if (settlement.IsVillage && settlement.Village != null)
            {
                settlement.Village.Hearth += GwpTuning.IssueResolution.LocalDevelopmentGain;
                return;
            }

            if (settlement.IsTown && settlement.Town != null)
                settlement.Town.Prosperity += GwpTuning.IssueResolution.LocalDevelopmentGain;
        }

        private void AssignIssues()
        {
            List<IssueBase> waiting = GetEligibleIssues().Where(issue => !IsAssigned(issue)).ToList();
            if (waiting.Count == 0) return;

            List<MobileParty> candidates = PoliceStats.GetAllPoliceParties()
                .Where(IsCandidate)
                .OrderByDescending(GreyWardenFamilyBehavior.IsIssueResolutionParty)
                .ThenBy(party => party.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (MobileParty party in candidates)
            {
                IssueBase? nearest = waiting
                    .Select(issue => new { Issue = issue, Settlement = ResolveSettlement(issue) })
                    .Where(row => row.Settlement != null)
                    .OrderBy(row => party.GetPosition2D.Distance(row.Settlement!.GetPosition2D))
                    .Select(row => row.Issue).FirstOrDefault();
                if (nearest == null) break;
                if (!PoliceEnforcementBehavior.TryReservePolicePartyForVillageRelief(party)) continue;

                _assignments.Add(new IssueAssignment
                {
                    IssueId = nearest.StringId ?? string.Empty,
                    OwnerHeroId = nearest.IssueOwner.StringId,
                    PolicePartyId = party.StringId
                });
                waiting.Remove(nearest);
                Settlement? settlement = ResolveSettlement(nearest);
                if (settlement != null) GreyWardenPartyDesireBehavior.RequestVisit(party, settlement, 8f);
            }
        }

        private bool IsCandidate(MobileParty? party)
        {
            if (party?.IsActive != true || party.LeaderHero?.IsActive != true ||
                party.Army != null || party.MapEvent is { IsFinalized: false } ||
                IsIssueDutyParty(party) ||
                GreyWardenTrainingBehavior.ShouldReserveFromNewDuties(party) ||
                GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest(party) ||
                GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder(party) ||
                GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(party) ||
                GreyWardenVillageReconstructionBehavior.IsReconstructionParty(party) ||
                GwpCommon.IsPatrolParty(party) || GwpCommon.IsEnforcementDelayPatrolParty(party))
                return false;

            if (!GreyWardenFamilyBehavior.IsIssueResolutionParty(party) &&
                CrimePool.HasTask(party.StringId))
                return false;

            // The petition office gets first claim. Other offices may help only after
            // their own currently available work and every pursuable criminal case
            // have been exhausted. Criminal cases carry an actual occurrence time;
            // native petitions are deliberately the lower-priority fallback pool.
            return GreyWardenFamilyBehavior.IsIssueResolutionParty(party) ||
                   (!CrimePool.IsDispatchReady &&
                    !GreyWardenVillageReconstructionBehavior.HasAvailableReconstruction() &&
                    !GreyWardenDutyScheduler.HasPreferredWork(party));
        }

        private bool IsAssigned(IssueBase issue) => FindAssignment(issue) != null;
        private IssueAssignment? FindAssignment(IssueBase issue) => _assignments.FirstOrDefault(entry =>
            string.Equals(entry.IssueId, issue.StringId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.OwnerHeroId, issue.IssueOwner?.StringId, StringComparison.OrdinalIgnoreCase));

        private IssueBase? FindIssue(IssueAssignment assignment) => GetEligibleIssues().FirstOrDefault(issue =>
            string.Equals(issue.StringId, assignment.IssueId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.IssueOwner?.StringId, assignment.OwnerHeroId, StringComparison.OrdinalIgnoreCase));

        private static string IssueKey(IssueBase issue) =>
            (issue.StringId ?? string.Empty) + "|" + (issue.IssueOwner?.StringId ?? string.Empty);
        private static string AssignmentKey(IssueAssignment assignment) =>
            assignment.IssueId + "|" + assignment.OwnerHeroId;

        private void Unassign(IssueAssignment assignment, MobileParty? party, string reason)
        {
            _assignments.Remove(assignment);
            ReleaseParty(party, "ISSUE_DUTY_RELEASED", reason);
        }

        private void FinishWithoutCompletion(IssueAssignment assignment, MobileParty? party, string reason)
        {
            _assignments.Remove(assignment);
            ReleaseParty(party, "ISSUE_DUTY_CANCELLED", reason);
        }

        private static void ReleaseParty(MobileParty? party, string action, string reason)
        {
            if (party?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
            GwpAiDiagnostics.WriteAction(party, action, "reason=" + reason);
        }

        private void ClearExtinctDuty()
        {
            foreach (IssueAssignment assignment in _assignments.ToList())
                ReleaseParty(FindParty(assignment.PolicePartyId), "ISSUE_DUTY_EXTINCT", "office_extinct");
            _assignments.Clear();
        }

        private static bool HasArrived(MobileParty party, Settlement settlement) =>
            party.CurrentSettlement == settlement ||
            party.GetPosition2D.Distance(settlement.GetPosition2D) <=
            GwpTuning.IssueResolution.ArrivalDistance;

        private static void HoldAtSettlement(MobileParty party, Settlement settlement) =>
            GreyWardenPartyDesireBehavior.RequestVisit(party, settlement, 10f);

        private static MobileParty? FindParty(string partyId) => string.IsNullOrWhiteSpace(partyId)
            ? null
            : MobileParty.All.FirstOrDefault(party => string.Equals(
                party.StringId, partyId, StringComparison.OrdinalIgnoreCase));
    }
}
