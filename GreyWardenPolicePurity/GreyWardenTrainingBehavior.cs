using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Training Wardens remain ordinary case-capable lords and periodically grant
    /// experience to their Grey Warden regulars alongside that work.
    /// Bannerlord's native party upgrader remains solely responsible for deciding
    /// when, whether, and along which branch those troops advance. Once every regular
    /// has reached the end of her troop branch, a training task is queued as the
    /// Warden's next duty. When she finishes her current duty, the nearest non-training
    /// Warden lord with trainees is reserved as the recipient; each party travels after
    /// finishing its current duty, then they exchange troops after a two-hour meeting.
    /// </summary>
    public sealed class GreyWardenTrainingBehavior : CampaignBehaviorBase
    {
        private const string TrainerIdsKey = "GWPP_TrainingTrainerIds";
        private const string TargetIdsKey = "GWPP_TrainingTargetIds";
        private const string SettlementIdsKey = "GWPP_TrainingSettlementIds";
        private const string QueuedHoursKey = "GWPP_TrainingQueuedHours";
        private const string StayStartHoursKey = "GWPP_TrainingStayStartHours";
        private const string UpgradeHeroIdsKey = "GWPP_TrainingUpgradeHeroIds";
        private const string UpgradeHoursKey = "GWPP_TrainingUpgradeHours";

        private static GreyWardenTrainingBehavior? _instance;
        private readonly List<TrainingAssignment> _assignments =
            new List<TrainingAssignment>();
        private readonly Dictionary<string, double> _lastUpgradeHourByHeroId =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        internal enum TrainingTaskStage
        {
            QueuedForTrainer,
            WaitingForCurrentDuties,
            TravelingToRendezvous,
            ExchangingAtSettlement
        }

        internal sealed class TrainingTaskSnapshot
        {
            public string TrainerPartyId { get; set; } = string.Empty;
            public string TargetPartyId { get; set; } = string.Empty;
            public string SettlementName { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public TrainingTaskStage Stage { get; set; }
            public double RemainingHours { get; set; }
            public bool IsAssigned { get; set; }
        }

        private sealed class TrainingAssignment
        {
            public string TrainerHeroId { get; set; } = string.Empty;
            public string TargetHeroId { get; set; } = string.Empty;
            public string SettlementId { get; set; } = string.Empty;
            public double QueuedTimeHours { get; set; }
            public double StayStartHours { get; set; }
        }

        public GreyWardenTrainingBehavior() => _instance = this;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? trainerIds = null;
            List<string>? targetIds = null;
            List<string>? settlementIds = null;
            List<double>? queuedHours = null;
            List<double>? stayStartHours = null;
            List<string>? upgradeHeroIds = null;
            List<double>? upgradeHours = null;

            if (dataStore.IsSaving)
            {
                trainerIds = _assignments.Select(static x => x.TrainerHeroId).ToList();
                targetIds = _assignments.Select(static x => x.TargetHeroId).ToList();
                settlementIds = _assignments.Select(static x => x.SettlementId).ToList();
                queuedHours = _assignments.Select(static x => x.QueuedTimeHours).ToList();
                stayStartHours = _assignments.Select(static x => x.StayStartHours).ToList();
                upgradeHeroIds = _lastUpgradeHourByHeroId.Keys.ToList();
                upgradeHours = upgradeHeroIds.Select(id => _lastUpgradeHourByHeroId[id]).ToList();
            }

            dataStore.SyncData(TrainerIdsKey, ref trainerIds);
            dataStore.SyncData(TargetIdsKey, ref targetIds);
            dataStore.SyncData(SettlementIdsKey, ref settlementIds);
            dataStore.SyncData(QueuedHoursKey, ref queuedHours);
            dataStore.SyncData(StayStartHoursKey, ref stayStartHours);
            dataStore.SyncData(UpgradeHeroIdsKey, ref upgradeHeroIds);
            dataStore.SyncData(UpgradeHoursKey, ref upgradeHours);

            if (!dataStore.IsLoading) return;

            _assignments.Clear();
            int assignmentCount = new[]
            {
                trainerIds?.Count ?? 0,
                targetIds?.Count ?? 0,
                settlementIds?.Count ?? 0,
                queuedHours?.Count ?? 0,
                stayStartHours?.Count ?? 0
            }.Min();
            for (int i = 0; i < assignmentCount; i++)
            {
                if (string.IsNullOrWhiteSpace(trainerIds![i]))
                    continue;

                _assignments.Add(new TrainingAssignment
                {
                    TrainerHeroId = trainerIds[i],
                    TargetHeroId = targetIds![i],
                    SettlementId = settlementIds![i],
                    QueuedTimeHours = queuedHours![i],
                    StayStartHours = stayStartHours![i]
                });
            }

            _lastUpgradeHourByHeroId.Clear();
            int upgradeCount = Math.Min(upgradeHeroIds?.Count ?? 0, upgradeHours?.Count ?? 0);
            for (int i = 0; i < upgradeCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(upgradeHeroIds![i]))
                    _lastUpgradeHourByHeroId[upgradeHeroIds[i]] = upgradeHours![i];
            }
        }

        internal static bool IsTrainingOccupied(MobileParty? party)
        {
            string? heroId = party?.LeaderHero?.StringId;
            return !string.IsNullOrWhiteSpace(heroId) && _instance != null &&
                   _instance._assignments.Any(task =>
                       string.Equals(task.TrainerHeroId, heroId,
                           StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(task.TargetHeroId, heroId,
                           StringComparison.OrdinalIgnoreCase));
        }

        internal static bool ShouldReserveFromNewDuties(MobileParty? party)
        {
            if (party == null) return false;
            return IsTrainingOccupied(party) ||
                   (GreyWardenFamilyBehavior.IsTrainingParty(party) &&
                    IsFullyTrained(party));
        }

        internal static bool IsFreeForTrainingExchange(MobileParty? party) =>
            IsFreeForTrainingWork(party);

        internal static void ReleasePartyForForcedDuty(MobileParty? party)
        {
            string? heroId = party?.LeaderHero?.StringId;
            if (string.IsNullOrWhiteSpace(heroId) || _instance == null) return;

            TrainingAssignment? assignment = _instance._assignments.FirstOrDefault(task =>
                string.Equals(task.TrainerHeroId, heroId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.TargetHeroId, heroId, StringComparison.OrdinalIgnoreCase));
            if (assignment != null)
                _instance.CancelAssignment(assignment, "forced_duty");
        }

        internal static IReadOnlyList<TrainingTaskSnapshot> GetTaskSnapshots()
        {
            var result = new List<TrainingTaskSnapshot>();
            if (_instance == null) return result;

            foreach (TrainingAssignment task in _instance._assignments)
            {
                MobileParty? trainer = ResolveParty(task.TrainerHeroId);
                MobileParty? target = ResolveParty(task.TargetHeroId);
                Settlement? settlement = ResolveSettlement(task.SettlementId);
                result.Add(new TrainingTaskSnapshot
                {
                    TrainerPartyId = trainer?.StringId ?? string.Empty,
                    TargetPartyId = target?.StringId ?? string.Empty,
                    SettlementName = settlement?.Name?.ToString() ??
                                     (string.IsNullOrWhiteSpace(task.SettlementId)
                                         ? GwpText.Get("{=gwp_training_ledger_pending_place}To be selected")
                                         : task.SettlementId),
                    QueuedTime = CampaignTime.Hours((float)task.QueuedTimeHours),
                    Stage = GetTaskStage(task, trainer, target),
                    RemainingHours = task.StayStartHours > 0d
                        ? Math.Max(0d, task.StayStartHours +
                            GwpTuning.Training.ExchangeStayHours - CampaignTime.Now.ToHours)
                        : 0d,
                    IsAssigned = target != null && settlement != null
                });
            }

            return result;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _ = starter;
            _assignments.Clear();
            _lastUpgradeHourByHeroId.Clear();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _ = starter;
            NormalizeAssignments();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _ = starter;
            NormalizeAssignments();
        }

        private void OnHourlyTick()
        {
            NormalizeAssignments();

            foreach (MobileParty trainer in PoliceStats.GetAllPoliceParties()
                         .Where(GreyWardenFamilyBehavior.IsTrainingParty)
                         .Where(static party => party.LeaderHero?.IsActive == true)
                         .OrderBy(static party => party.StringId,
                             StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (GreyWardenTroopRequestBehavior
                    .IsTrainerReservedForPlayerOrder(trainer) ||
                    GreyWardenPlayerRequestBehavior
                    .IsPartyReservedForPlayerRequest(trainer))
                    continue;
                TrainPartyIfDue(trainer);
                if (IsFullyTrained(trainer))
                    EnsureQueuedAssignment(trainer);
            }

            UpdateAssignments();
        }

        private void NormalizeAssignments()
        {
            var seenHeroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TrainingAssignment task in _assignments.ToList())
            {
                bool duplicate = !seenHeroes.Add(task.TrainerHeroId);
                if (!string.IsNullOrWhiteSpace(task.TargetHeroId))
                    duplicate |= !seenHeroes.Add(task.TargetHeroId);

                bool targetInvalid = !string.IsNullOrWhiteSpace(task.TargetHeroId) &&
                                     ResolveParty(task.TargetHeroId)?.IsActive != true;
                bool settlementInvalid = !string.IsNullOrWhiteSpace(task.SettlementId) &&
                                         ResolveSettlement(task.SettlementId) == null;
                if (duplicate || ResolveParty(task.TrainerHeroId)?.IsActive != true ||
                    targetInvalid || settlementInvalid)
                {
                    CancelAssignment(task, duplicate ? "duplicate" : "invalid_after_load");
                }
            }
        }

        private void UpdateAssignments()
        {
            foreach (TrainingAssignment task in _assignments.ToList())
            {
                MobileParty? trainer = ResolveParty(task.TrainerHeroId);
                if (trainer?.IsActive != true)
                {
                    CancelAssignment(task, "trainer_missing");
                    continue;
                }

                if (!GreyWardenFamilyBehavior.IsTrainingParty(trainer))
                {
                    CancelAssignment(task, "trainer_role_changed");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(task.TargetHeroId))
                {
                    if (!IsFreeForTrainingWork(trainer, ignoreTrainingAssignment: true))
                        continue;

                    TryAssignRecipient(task, trainer);
                    continue;
                }

                MobileParty? target = ResolveParty(task.TargetHeroId);
                Settlement? settlement = ResolveSettlement(task.SettlementId);
                if (target?.IsActive != true || settlement == null ||
                    GreyWardenFamilyBehavior.IsTrainingParty(target) ||
                    CountTrainableTroops(target) <= 0)
                {
                    RequeueRecipient(task, trainer,
                        target?.IsActive == true
                            ? "recipient_roster_or_role_changed"
                            : "recipient_or_settlement_missing");
                    continue;
                }

                bool trainerReady = IsFreeForTrainingWork(trainer,
                    ignoreTrainingAssignment: true);
                bool targetReady = IsFreeForTrainingWork(target,
                    ignoreTrainingAssignment: true);
                bool bothInside = trainerReady && targetReady &&
                                  trainer.CurrentSettlement == settlement &&
                                  target.CurrentSettlement == settlement;
                if (!bothInside)
                {
                    if (task.StayStartHours > 0d)
                    {
                        task.StayStartHours = 0d;
                        GwpAiDiagnostics.WriteAction(trainer, "TRAINING_STAY_RESET",
                            "target=" + target.StringId + "; settlement=" + settlement.StringId);
                    }

                    if (trainerReady)
                        GreyWardenPartyDesireBehavior.RequestVisit(trainer, settlement,
                            validHours: GwpTuning.Training.MovementIntentHours);
                    if (targetReady)
                        GreyWardenPartyDesireBehavior.RequestVisit(target, settlement,
                            validHours: GwpTuning.Training.MovementIntentHours);
                    continue;
                }

                if (task.StayStartHours <= 0d)
                {
                    task.StayStartHours = CampaignTime.Now.ToHours;
                    GwpAiDiagnostics.WriteAction(trainer, "TRAINING_STAY_STARTED",
                        "target=" + target.StringId + "; settlement=" + settlement.StringId +
                        "; hours=" + GwpTuning.Training.ExchangeStayHours);
                }

                GreyWardenPartyDesireBehavior.RequestVisit(trainer, settlement,
                    validHours: GwpTuning.Training.MovementIntentHours);
                GreyWardenPartyDesireBehavior.RequestVisit(target, settlement,
                    validHours: GwpTuning.Training.MovementIntentHours);

                if (CampaignTime.Now.ToHours < task.StayStartHours +
                    GwpTuning.Training.ExchangeStayHours)
                    continue;

                int exchanged = ExchangeTroops(trainer, target);
                FinishAssignment(task, trainer, target, settlement, exchanged);
            }
        }

        private void TrainPartyIfDue(MobileParty trainer)
        {
            Hero? hero = trainer.LeaderHero;
            if (hero == null) return;

            double now = CampaignTime.Now.ToHours;
            if (_lastUpgradeHourByHeroId.TryGetValue(hero.StringId, out double last) &&
                now - last < GwpTuning.Training.ExperienceIntervalHours)
                return;

            _lastUpgradeHourByHeroId[hero.StringId] = now;
            List<TroopRosterElement> trainees = trainer.MemberRoster.GetTroopRoster()
                .Where(static element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0)
                .ToList();
            if (trainees.Count == 0) return;

            int trainedTroops = 0;
            int totalExperience = 0;
            foreach (TroopRosterElement trainee in trainees)
            {
                int experience = trainee.Number *
                    GwpTuning.Training.ExperiencePerTroopPerInterval;
                trainer.MemberRoster.AddXpToTroop(trainee.Character, experience);
                trainedTroops += trainee.Number;
                totalExperience += experience;
            }

            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TROOP_XP_GRANTED",
                "troops=" + trainedTroops + "; cohorts=" + trainees.Count +
                "; xpPerTroop=" +
                GwpTuning.Training.ExperiencePerTroopPerInterval +
                "; totalXp=" + totalExperience +
                "; nativeUpgradePending=true");
        }

        private void EnsureQueuedAssignment(MobileParty trainer)
        {
            if (trainer.LeaderHero == null || _assignments.Any(task =>
                    string.Equals(task.TrainerHeroId, trainer.LeaderHero.StringId,
                        StringComparison.OrdinalIgnoreCase)))
                return;

            _assignments.Add(new TrainingAssignment
            {
                TrainerHeroId = trainer.LeaderHero.StringId,
                QueuedTimeHours = CampaignTime.Now.ToHours
            });
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_QUEUED",
                "elite=" + CountEliteTroops(trainer) +
                "; waitsForCurrentDuty=true");
        }

        private void TryAssignRecipient(TrainingAssignment assignment,
            MobileParty trainer)
        {
            MobileParty? target = PoliceStats.GetAllPoliceParties()
                .Where(candidate => candidate != trainer)
                .Where(candidate => !GreyWardenFamilyBehavior.IsTrainingParty(candidate))
                .Where(candidate => !IsTrainingOccupied(candidate))
                .Where(IsStructurallyValidTrainingParty)
                .Where(candidate => !GreyWardenTroopRequestBehavior
                    .IsTrainerReservedForPlayerOrder(candidate))
                .Where(candidate => !GreyWardenPlayerRequestBehavior
                    .IsPartyReservedForPlayerRequest(candidate))
                .Where(candidate => CountTrainableTroops(candidate) > 0)
                .OrderBy(candidate => candidate.GetPosition2D.Distance(trainer.GetPosition2D))
                .ThenBy(candidate => candidate.StringId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (target?.LeaderHero == null || trainer.LeaderHero == null) return;

            Settlement? rendezvous = FindRendezvousSettlement(trainer, target);
            if (rendezvous == null) return;

            assignment.TargetHeroId = target.LeaderHero.StringId;
            assignment.SettlementId = rendezvous.StringId;
            assignment.StayStartHours = 0d;
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_ASSIGNED",
                "target=" + target.StringId + "; settlement=" + rendezvous.StringId +
                "; elite=" + CountEliteTroops(trainer) +
                "; targetTrainable=" + CountTrainableTroops(target) +
                "; trainerReady=true; targetReady=" +
                IsFreeForTrainingWork(target, ignoreTrainingAssignment: true));
        }

        private void RequeueRecipient(TrainingAssignment task, MobileParty trainer,
            string reason)
        {
            MobileParty? target = ResolveParty(task.TargetHeroId);
            if (target?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(target);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(target);
            }
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_RECIPIENT_REQUEUED",
                "target=" + (target?.StringId ?? task.TargetHeroId) +
                "; reason=" + reason);
            task.TargetHeroId = string.Empty;
            task.SettlementId = string.Empty;
            task.StayStartHours = 0d;
        }

        private static int ExchangeTroops(MobileParty trainer, MobileParty target)
        {
            int exchangeCount = Math.Min(CountEliteTroops(trainer),
                CountTrainableTroops(target));
            if (exchangeCount <= 0) return 0;

            string trainerBefore = FormatGreyWardenRoster(trainer);
            string targetBefore = FormatGreyWardenRoster(target);

            var elites = trainer.MemberRoster.GetTroopRoster()
                .Where(static element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length == 0)
                .OrderBy(static element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
            var trainees = target.MemberRoster.GetTroopRoster()
                .Where(static element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0)
                .OrderBy(static element => element.Character.Tier)
                .ThenBy(static element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            int movedElites = TransferEliteTroopsProportionally(trainer.MemberRoster,
                target.MemberRoster, elites, exchangeCount, out string movedComposition);
            int movedTrainees = TransferFromBatches(target.MemberRoster,
                trainer.MemberRoster, trainees, exchangeCount);
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_EXCHANGE_ROSTER",
                "target=" + target.StringId + "; requested=" + exchangeCount +
                "; movedElite=" + movedElites + "; movedTrainee=" + movedTrainees +
                "; eliteComposition=" + movedComposition +
                "; trainerBefore=" + trainerBefore +
                "; trainerAfter=" + FormatGreyWardenRoster(trainer) +
                "; targetBefore=" + targetBefore +
                "; targetAfter=" + FormatGreyWardenRoster(target));
            return Math.Min(movedElites, movedTrainees);
        }

        private static int TransferEliteTroopsProportionally(TroopRoster source,
            TroopRoster destination, IReadOnlyList<TroopRosterElement> batches,
            int requested, out string composition)
        {
            int total = batches.Sum(static batch => Math.Max(0, batch.Number));
            if (requested <= 0 || total <= 0)
            {
                composition = "none";
                return 0;
            }

            int[] allocations = new int[batches.Count];
            long[] remainders = new long[batches.Count];
            int allocated = 0;
            for (int index = 0; index < batches.Count; index++)
            {
                long scaled = (long)requested * Math.Max(0, batches[index].Number);
                allocations[index] = (int)(scaled / total);
                remainders[index] = scaled % total;
                allocated += allocations[index];
            }

            foreach (int index in Enumerable.Range(0, batches.Count)
                .OrderByDescending(index => remainders[index])
                .ThenBy(index => batches[index].Character.StringId,
                    StringComparer.OrdinalIgnoreCase))
            {
                if (allocated >= requested) break;
                if (allocations[index] >= batches[index].Number) continue;
                allocations[index]++;
                allocated++;
            }

            int moved = 0;
            var movedParts = new List<string>();
            for (int index = 0; index < batches.Count; index++)
            {
                int planned = allocations[index];
                if (planned <= 0) continue;

                TroopRosterElement current = source.GetTroopRoster().FirstOrDefault(element =>
                    element.Character == batches[index].Character);
                int take = Math.Min(current.Number, planned);
                if (take <= 0) continue;

                int healthy = Math.Max(0, current.Number - current.WoundedNumber);
                int wounded = Math.Max(0, take - healthy);
                source.AddToCounts(batches[index].Character, -take, false, -wounded);
                destination.AddToCounts(batches[index].Character, take, false, wounded);
                moved += take;
                movedParts.Add(batches[index].Character.StringId + ":" + take);
            }

            composition = movedParts.Count > 0 ? string.Join(",", movedParts) : "none";
            return moved;
        }

        private static int TransferFromBatches(TroopRoster source, TroopRoster destination,
            IEnumerable<TroopRosterElement> batches, int requested)
        {
            int moved = 0;
            foreach (TroopRosterElement batch in batches)
            {
                if (moved >= requested) break;
                TroopRosterElement current = source.GetTroopRoster().FirstOrDefault(element =>
                    element.Character == batch.Character);
                int take = Math.Min(current.Number, requested - moved);
                if (take <= 0) continue;

                int healthy = Math.Max(0, current.Number - current.WoundedNumber);
                int wounded = Math.Max(0, take - healthy);
                source.AddToCounts(batch.Character, -take, false, -wounded);
                destination.AddToCounts(batch.Character, take, false, wounded);
                moved += take;
            }
            return moved;
        }

        private void FinishAssignment(TrainingAssignment task, MobileParty trainer,
            MobileParty target, Settlement settlement, int exchanged)
        {
            _assignments.Remove(task);
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.ClearIntent(target);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(target);
            GwpPlayerRequestDeferral.NotifyDutyCompleted(trainer,
                "training_exchange");
            GwpPlayerRequestDeferral.NotifyDutyCompleted(target,
                "training_exchange");
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_COMPLETED",
                "target=" + target.StringId + "; settlement=" + settlement.StringId +
                "; exchanged=" + exchanged + "; trainerTrainable=" +
                CountTrainableTroops(trainer) + "; targetTrainable=" +
                CountTrainableTroops(target));
        }

        private void CancelAssignment(TrainingAssignment task, string reason)
        {
            MobileParty? trainer = ResolveParty(task.TrainerHeroId);
            MobileParty? target = ResolveParty(task.TargetHeroId);
            _assignments.Remove(task);
            if (trainer?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(trainer);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
                GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_CANCELLED",
                    "target=" + (target?.StringId ?? task.TargetHeroId) +
                    "; reason=" + reason);
            }
            if (target?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(target);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(target);
            }
        }

        private static bool IsFreeForTrainingWork(MobileParty? party,
            bool ignoreTrainingAssignment = false)
        {
            if (party?.IsActive != true || !party.IsLordParty || party.IsDisbanding ||
                party.LeaderHero?.IsActive != true || party.Army != null ||
                party.MapEvent is { IsFinalized: false })
                return false;

            if (!ignoreTrainingAssignment && IsTrainingOccupied(party)) return false;
            if (GreyWardenTroopRequestBehavior
                    .IsTrainerReservedForPlayerOrder(party) ||
                GreyWardenPlayerRequestBehavior
                    .IsPartyReservedForPlayerRequest(party))
                return false;
            if (CrimePool.HasTask(party.StringId) ||
                PoliceEnforcementBehavior.IsPartyOccupiedByAssistance(party) ||
                GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(party) ||
                GreyWardenVillageReconstructionBehavior.IsReconstructionParty(party) ||
                GreyWardenIssueResolutionBehavior.IsIssueDutyParty(party))
                return false;

            return true;
        }

        private static bool IsStructurallyValidTrainingParty(MobileParty? party) =>
            party?.IsActive == true && party.IsLordParty && !party.IsDisbanding &&
            party.LeaderHero?.IsActive == true;

        private static TrainingTaskStage GetTaskStage(TrainingAssignment task,
            MobileParty? trainer, MobileParty? target)
        {
            if (string.IsNullOrWhiteSpace(task.TargetHeroId))
                return TrainingTaskStage.QueuedForTrainer;
            if (task.StayStartHours > 0d)
                return TrainingTaskStage.ExchangingAtSettlement;
            if (!IsFreeForTrainingWork(trainer, ignoreTrainingAssignment: true) ||
                !IsFreeForTrainingWork(target, ignoreTrainingAssignment: true))
                return TrainingTaskStage.WaitingForCurrentDuties;
            return TrainingTaskStage.TravelingToRendezvous;
        }

        private static bool IsFullyTrained(MobileParty party) =>
            CountGreyWardenRegulars(party) > 0 && CountTrainableTroops(party) == 0;

        private static int CountGreyWardenRegulars(MobileParty party) =>
            party.MemberRoster.GetTroopRoster().Where(static element =>
                    element.Character != null && !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character))
                .Sum(static element => Math.Max(0, element.Number));

        private static int CountTrainableTroops(MobileParty party) =>
            party.MemberRoster.GetTroopRoster().Where(static element =>
                    element.Character != null && !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length > 0)
                .Sum(static element => Math.Max(0, element.Number));

        private static int CountEliteTroops(MobileParty party) =>
            party.MemberRoster.GetTroopRoster().Where(static element =>
                    element.Character != null && !element.Character.IsHero &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length == 0)
                .Sum(static element => Math.Max(0, element.Number));

        private static string FormatGreyWardenRoster(MobileParty party)
        {
            string[] parts = party.MemberRoster.GetTroopRoster()
                .Where(static element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character))
                .OrderBy(static element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .Select(static element => element.Character.StringId + ":" + element.Number)
                .ToArray();
            return parts.Length > 0 ? string.Join(",", parts) : "none";
        }

        internal static Settlement? FindRendezvousSettlement(MobileParty trainer,
            MobileParty target)
        {
            Vec2 left = trainer.GetPosition2D;
            Vec2 right = target.GetPosition2D;
            var midpoint = new Vec2((left.x + right.x) * 0.5f,
                (left.y + right.y) * 0.5f);
            Clan? policeClan = PoliceStats.GetPoliceClan();

            return Settlement.All
                .Where(static settlement => settlement.IsTown || settlement.IsCastle)
                .Where(static settlement => !settlement.IsUnderSiege)
                .Where(settlement => policeClan == null || settlement.MapFaction == null ||
                    !FactionManager.IsAtWarAgainstFaction(policeClan,
                        settlement.MapFaction))
                .OrderBy(settlement => settlement.GetPosition2D.Distance(midpoint))
                .ThenBy(settlement => settlement.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static MobileParty? ResolveParty(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId)) return null;
            try
            {
                return Hero.FindFirst(hero => string.Equals(hero.StringId, heroId,
                    StringComparison.OrdinalIgnoreCase))?.PartyBelongedTo;
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

        private static Settlement? ResolveSettlement(string settlementId) =>
            string.IsNullOrWhiteSpace(settlementId) ? null : Settlement.Find(settlementId);
    }
}
