using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Low-priority public works docket for villages left in the native Looted state.
    /// A living reconstruction office keeps the feature available. Its holders get first
    /// claim, while other idle offices may help after their own preferred work runs out.
    /// If the reconstruction line dies out, the feature and its docket disappear with it.
    /// </summary>
    public sealed class GreyWardenVillageReconstructionBehavior : CampaignBehaviorBase
    {
        private const string VillageIdsKey = "GWPP_RebuildVillageIds";
        private const string VillageNamesKey = "GWPP_RebuildVillageNames";
        private const string PartyIdsKey = "GWPP_RebuildPartyIds";
        private const string QueuedHoursKey = "GWPP_RebuildQueuedHours";
        private const string WorkStartedFlagsKey = "GWPP_RebuildWorkStartedFlags";
        private const string WorkEndHoursKey = "GWPP_RebuildWorkEndHours";

        private static GreyWardenVillageReconstructionBehavior? _instance;
        private readonly List<ReconstructionTask> _tasks = new List<ReconstructionTask>();

        internal enum ReconstructionStage
        {
            WaitingForAssignment,
            TravelingToVillage,
            Rebuilding
        }

        internal sealed class ReconstructionTaskSnapshot
        {
            public string VillageSettlementId { get; set; } = string.Empty;
            public string VillageName { get; set; } = string.Empty;
            public string PolicePartyId { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public ReconstructionStage Stage { get; set; }
            public double RemainingHours { get; set; }
            public int EstimatedCost { get; set; }
            public int TreasuryReserve { get; set; }
            public bool IsAssigned => !string.IsNullOrWhiteSpace(PolicePartyId);
        }

        private sealed class ReconstructionTask
        {
            public string VillageSettlementId { get; set; } = string.Empty;
            public string VillageName { get; set; } = string.Empty;
            public string PolicePartyId { get; set; } = string.Empty;
            public double QueuedTimeHours { get; set; }
            public bool WorkStarted { get; set; }
            public double WorkEndHours { get; set; }
        }

        public GreyWardenVillageReconstructionBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? villageIds = null;
            List<string>? villageNames = null;
            List<string>? partyIds = null;
            List<double>? queuedHours = null;
            List<int>? workStartedFlags = null;
            List<double>? workEndHours = null;

            if (dataStore.IsSaving)
            {
                villageIds = _tasks.Select(task => task.VillageSettlementId).ToList();
                villageNames = _tasks.Select(task => task.VillageName).ToList();
                partyIds = _tasks.Select(task => task.PolicePartyId).ToList();
                queuedHours = _tasks.Select(task => task.QueuedTimeHours).ToList();
                workStartedFlags = _tasks.Select(task => task.WorkStarted ? 1 : 0).ToList();
                workEndHours = _tasks.Select(task => task.WorkEndHours).ToList();
            }

            dataStore.SyncData(VillageIdsKey, ref villageIds);
            dataStore.SyncData(VillageNamesKey, ref villageNames);
            dataStore.SyncData(PartyIdsKey, ref partyIds);
            dataStore.SyncData(QueuedHoursKey, ref queuedHours);
            dataStore.SyncData(WorkStartedFlagsKey, ref workStartedFlags);
            dataStore.SyncData(WorkEndHoursKey, ref workEndHours);

            if (!dataStore.IsLoading)
                return;

            _tasks.Clear();
            int count = new[]
            {
                villageIds?.Count ?? 0,
                villageNames?.Count ?? 0,
                partyIds?.Count ?? 0,
                queuedHours?.Count ?? 0,
                workStartedFlags?.Count ?? 0,
                workEndHours?.Count ?? 0
            }.Min();
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(villageIds![i]))
                    continue;
                _tasks.Add(new ReconstructionTask
                {
                    VillageSettlementId = villageIds[i],
                    VillageName = villageNames![i],
                    PolicePartyId = partyIds![i] ?? string.Empty,
                    QueuedTimeHours = queuedHours![i],
                    WorkStarted = workStartedFlags![i] != 0,
                    WorkEndHours = workEndHours![i]
                });
            }
        }

        internal static bool IsReconstructionParty(MobileParty? party)
        {
            return party != null && _instance != null && _instance._tasks.Any(task =>
                string.Equals(task.PolicePartyId, party.StringId, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool ShouldReserveFromOrdinaryCases(MobileParty? party)
        {
            if (party == null || _instance == null)
                return false;

            // Cross-office helpers already assigned to a rebuilding task must remain
            // reserved just like the dedicated reconstruction holder.
            if (IsReconstructionParty(party))
                return true;

            if (!GreyWardenFamilyBehavior.IsReconstructionParty(party))
                return false;

            return PoliceResourceManager.CanFundVillageReconstruction(out _, out _) &&
                   _instance._tasks.Any(task =>
                       string.IsNullOrWhiteSpace(task.PolicePartyId) &&
                       IsLootedVillage(FindVillage(task.VillageSettlementId)));
        }

        internal static bool HasAvailableReconstruction()
        {
            if (_instance == null || !GreyWardenFamilyBehavior.HasLivingReconstructionHolder() ||
                !PoliceResourceManager.CanFundVillageReconstruction(out _, out _))
                return false;

            return _instance._tasks.Any(task =>
                string.IsNullOrWhiteSpace(task.PolicePartyId) &&
                IsLootedVillage(FindVillage(task.VillageSettlementId)));
        }

        internal static void ReleasePartyForForcedDuty(MobileParty? party)
        {
            if (party == null || _instance == null)
                return;

            ReconstructionTask? task = _instance._tasks.FirstOrDefault(candidate =>
                string.Equals(candidate.PolicePartyId, party.StringId,
                    StringComparison.OrdinalIgnoreCase));
            if (task == null)
                return;

            _instance.UnassignTask(task, party, "forced_duty");
        }

        internal static IReadOnlyList<ReconstructionTaskSnapshot> GetTaskSnapshots()
        {
            var result = new List<ReconstructionTaskSnapshot>();
            if (_instance == null)
                return result;

            PoliceResourceManager.CanFundVillageReconstruction(out int estimatedCost,
                out int reserve);
            foreach (ReconstructionTask task in _instance._tasks)
            {
                result.Add(new ReconstructionTaskSnapshot
                {
                    VillageSettlementId = task.VillageSettlementId,
                    VillageName = ResolveVillageName(task),
                    PolicePartyId = task.PolicePartyId,
                    QueuedTime = CampaignTime.Hours((float)task.QueuedTimeHours),
                    Stage = string.IsNullOrWhiteSpace(task.PolicePartyId)
                        ? ReconstructionStage.WaitingForAssignment
                        : task.WorkStarted
                            ? ReconstructionStage.Rebuilding
                            : ReconstructionStage.TravelingToVillage,
                    RemainingHours = task.WorkStarted
                        ? Math.Max(0d, task.WorkEndHours - CampaignTime.Now.ToHours)
                        : 0d,
                    EstimatedCost = estimatedCost,
                    TreasuryReserve = reserve
                });
            }

            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _tasks.Clear();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            NormalizeAndScan();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            NormalizeAndScan();
        }

        private void OnVillageLooted(Village village)
        {
            if (village?.Settlement != null)
                QueueVillageIfEligible(village.Settlement);
        }

        private void OnHourlyTick()
        {
            if (!GreyWardenFamilyBehavior.HasLivingReconstructionHolder())
            {
                ClearExtinctDutyDocket();
                return;
            }

            RemoveInvalidOrRecoveredTasks();
            UpdateAssignedTasks();
            AssignWaitingTasks();
        }

        private void NormalizeAndScan()
        {
            if (!GreyWardenFamilyBehavior.HasLivingReconstructionHolder())
            {
                ClearExtinctDutyDocket();
                return;
            }

            RemoveInvalidOrRecoveredTasks();
            foreach (Settlement settlement in Settlement.All.Where(settlement => settlement.IsVillage))
                QueueVillageIfEligible(settlement);
        }

        private void QueueVillageIfEligible(Settlement village)
        {
            if (!GreyWardenFamilyBehavior.HasLivingReconstructionHolder() ||
                !IsLootedVillage(village) ||
                _tasks.Any(task => string.Equals(task.VillageSettlementId, village.StringId,
                    StringComparison.OrdinalIgnoreCase)))
                return;

            _tasks.Add(new ReconstructionTask
            {
                VillageSettlementId = village.StringId,
                VillageName = village.Name?.ToString() ??
                              GwpText.Get("{=gwp_reconstruction_001}nameless village"),
                QueuedTimeHours = CampaignTime.Now.ToHours
            });
        }

        private void RemoveInvalidOrRecoveredTasks()
        {
            foreach (ReconstructionTask task in _tasks.ToList())
            {
                Settlement? village = FindVillage(task.VillageSettlementId);
                if (IsLootedVillage(village))
                    continue;

                MobileParty? party = FindParty(task.PolicePartyId);
                FinishWithoutCompletion(task, party, "village_recovered_or_invalid");
            }
        }

        private void UpdateAssignedTasks()
        {
            foreach (ReconstructionTask task in _tasks
                         .Where(task => !string.IsNullOrWhiteSpace(task.PolicePartyId))
                         .ToList())
            {
                MobileParty? police = FindParty(task.PolicePartyId);
                Settlement? village = FindVillage(task.VillageSettlementId);
                if (!IsLootedVillage(village))
                {
                    FinishWithoutCompletion(task, police, "assignee_or_village_invalid");
                    continue;
                }

                if (police?.IsActive != true || police.LeaderHero?.IsActive != true)
                {
                    UnassignTask(task, police, "assignee_invalid");
                    continue;
                }

                if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(police) ||
                    police.Army != null)
                {
                    ReleasePartyForForcedDuty(police);
                    continue;
                }

                if (police.MapEvent != null && !police.MapEvent.IsFinalized)
                    continue;

                if (!task.WorkStarted)
                {
                    if (HasArrived(police, village!))
                    {
                        task.WorkStarted = true;
                        task.WorkEndHours = CampaignTime.Now.ToHours +
                                            GwpTuning.Reconstruction.WorkHours;
                        HoldAtVillage(police, village!);
                        GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_STARTED",
                            "village=" + village!.StringId + "; end=" + task.WorkEndHours);
                    }
                    else
                    {
                        GreyWardenPartyDesireBehavior.RequestVisit(police, village!, 8f);
                    }
                    continue;
                }

                HoldAtVillage(police, village!);
                if (CampaignTime.Now.ToHours < task.WorkEndHours)
                    continue;

                if (!PoliceResourceManager.TrySpendVillageReconstructionFunds(
                        out int cost, out int reserve))
                {
                    GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_UNFUNDED",
                        "village=" + village!.StringId + "; reserve=" + reserve);
                    ReleasePartyForForcedDuty(police);
                    continue;
                }

                float missingHealth = MathF.Max(0f, 1f - village!.SettlementHitPoints);
                IncreaseSettlementHealthAction.Apply(village, missingHealth);
                if (!IsLootedVillage(village))
                {
                    PoliceResourceManager.CreditSuccessfulCaseCompletion();
                    GwpPlayerRequestDeferral.NotifyDutyCompleted(police,
                        "village_reconstruction");
                    _tasks.Remove(task);
                    GreyWardenPartyDesireBehavior.ClearIntent(police);
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_reconstruction_002}{VAR_1} completed the reconstruction of {VAR_2}.",
                            "VAR_1", police.LeaderHero?.Name?.ToString() ?? police.Name?.ToString() ?? string.Empty,
                            "VAR_2", village.Name?.ToString() ?? task.VillageName), Colors.Green));
                    GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_COMPLETED",
                        "village=" + village.StringId + "; cost=" + cost +
                        "; grant=" + PoliceResourceManager.SuccessfulCaseReward +
                        "; reserve=" + reserve);
                }
                else
                {
                    // The native transition unexpectedly failed. Return the construction
                    // allocation so a save cannot lose money without a restored village.
                    PoliceResourceManager.RefundJudicialTreasury(cost);
                    ReleasePartyForForcedDuty(police);
                }
            }
        }

        private void AssignWaitingTasks()
        {
            if (!PoliceResourceManager.CanFundVillageReconstruction(out _, out _))
                return;

            foreach (MobileParty police in PoliceStats.GetAllPoliceParties()
                         .Where(IsCandidate)
                         .OrderByDescending(GreyWardenFamilyBehavior.IsReconstructionParty)
                         .ThenBy(party => party.StringId, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                ReconstructionTask? nearest = _tasks
                    .Where(task => string.IsNullOrWhiteSpace(task.PolicePartyId))
                    .Select(task => new { Task = task, Village = FindVillage(task.VillageSettlementId) })
                    .Where(row => IsLootedVillage(row.Village))
                    .OrderBy(row => police.GetPosition2D.Distance(row.Village!.GetPosition2D))
                    .Select(row => row.Task)
                    .FirstOrDefault();
                if (nearest == null)
                    break;

                if (!PoliceEnforcementBehavior.TryReservePolicePartyForVillageRelief(police))
                    continue;

                nearest.PolicePartyId = police.StringId;
                nearest.WorkStarted = false;
                nearest.WorkEndHours = 0d;
                Settlement? village = FindVillage(nearest.VillageSettlementId);
                if (village != null)
                    GreyWardenPartyDesireBehavior.RequestVisit(police, village, 8f);
                GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_ASSIGNED",
                    "village=" + nearest.VillageSettlementId);
            }
        }

        private static bool IsCandidate(MobileParty? police)
        {
            if (police?.IsActive != true || police.LeaderHero?.IsActive != true ||
                GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(police) ||
                IsReconstructionParty(police) || police.Army != null ||
                GreyWardenTrainingBehavior.ShouldReserveFromNewDuties(police) ||
                GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest(police) ||
                GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder(police) ||
                GreyWardenIssueResolutionBehavior.IsIssueDutyParty(police) ||
                GwpCommon.IsPatrolParty(police) ||
                GwpCommon.IsEnforcementDelayPatrolParty(police))
                return false;

            if (police.MapEvent != null && !police.MapEvent.IsFinalized)
                return false;

            if (!GreyWardenFamilyBehavior.IsReconstructionParty(police) &&
                CrimePool.HasTask(police.StringId))
                return false;

            // Reconstruction keeps its dedicated office, but cross-office helpers
            // first clear every pursuable criminal case. Burned villages remain in
            // this docket without expiring, whereas criminal cases are time-sensitive.
            return GreyWardenFamilyBehavior.IsReconstructionParty(police) ||
                   (!CrimePool.IsDispatchReady &&
                    !GreyWardenDutyScheduler.HasPreferredWork(police));
        }

        private void FinishWithoutCompletion(ReconstructionTask task, MobileParty? police,
            string reason)
        {
            _tasks.Remove(task);
            if (police?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(police);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
                GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_CANCELLED",
                    "village=" + task.VillageSettlementId + "; reason=" + reason);
            }
        }

        private void UnassignTask(ReconstructionTask task, MobileParty? police, string reason)
        {
            task.PolicePartyId = string.Empty;
            task.WorkStarted = false;
            task.WorkEndHours = 0d;
            if (police?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(police);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
                GwpAiDiagnostics.WriteAction(police, "RECONSTRUCTION_RELEASED",
                    "village=" + task.VillageSettlementId + "; reason=" + reason);
            }
        }

        private void ClearExtinctDutyDocket()
        {
            foreach (ReconstructionTask task in _tasks.ToList())
            {
                MobileParty? party = FindParty(task.PolicePartyId);
                if (party?.IsActive == true)
                    GreyWardenPartyDesireBehavior.ClearIntent(party);
            }
            _tasks.Clear();
        }

        private static bool HasArrived(MobileParty police, Settlement village)
        {
            return police.CurrentSettlement == village ||
                   police.GetPosition2D.Distance(village.GetPosition2D) <=
                   GwpTuning.Reconstruction.ArrivalDistance;
        }

        private static void HoldAtVillage(MobileParty police, Settlement village)
        {
            GreyWardenPartyDesireBehavior.RequestVisit(police, village, 10f);
        }

        private static bool IsLootedVillage(Settlement? settlement)
        {
            return settlement?.IsVillage == true &&
                   (settlement.SettlementComponent as Village)?.VillageState ==
                   Village.VillageStates.Looted;
        }

        private static Settlement? FindVillage(string settlementId)
        {
            return string.IsNullOrWhiteSpace(settlementId) ? null : Settlement.Find(settlementId);
        }

        private static MobileParty? FindParty(string partyId)
        {
            return string.IsNullOrWhiteSpace(partyId)
                ? null
                : MobileParty.All.FirstOrDefault(party =>
                    string.Equals(party.StringId, partyId, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveVillageName(ReconstructionTask task)
        {
            return FindVillage(task.VillageSettlementId)?.Name?.ToString() ?? task.VillageName;
        }
    }
}
