using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 练兵领主照常办案，顺带给自己队里的灰袍兵加经验，升不升、往哪条分支升交给原版升级器。
    /// 队里全练到头后排一个练兵差事：办完手上的事，去最近的、守村队里有可升级灰袍兵的信众村待两小时，
    /// 把满级老兵换给守村队，换回守村队里最弱的可升级兵接着练。不再和别的领主换兵，
    /// 也不占用别的领主（用户裁定 2026-10-14：练兵领主总干扰办案的领主；守村队能被招募，练它更好）。
    /// </summary>
    public sealed class GreyWardenTrainingBehavior : CampaignBehaviorBase
    {
        private const string TrainerIdsKey = "GWPP_TrainingTrainerIds";
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
            public string SettlementName { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public TrainingTaskStage Stage { get; set; }
            public double RemainingHours { get; set; }
            public bool IsAssigned { get; set; }
        }

        private sealed class TrainingAssignment
        {
            public string TrainerHeroId { get; set; } = string.Empty;
            // 去换兵的信众村；空着表示还没选。
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
            List<string>? settlementIds = null;
            List<double>? queuedHours = null;
            List<double>? stayStartHours = null;
            List<string>? upgradeHeroIds = null;
            List<double>? upgradeHours = null;

            if (dataStore.IsSaving)
            {
                trainerIds = _assignments.Select(static x => x.TrainerHeroId).ToList();
                settlementIds = _assignments.Select(static x => x.SettlementId).ToList();
                queuedHours = _assignments.Select(static x => x.QueuedTimeHours).ToList();
                stayStartHours = _assignments.Select(static x => x.StayStartHours).ToList();
                upgradeHeroIds = _lastUpgradeHourByHeroId.Keys.ToList();
                upgradeHours = upgradeHeroIds.Select(id => _lastUpgradeHourByHeroId[id]).ToList();
            }

            dataStore.SyncData(TrainerIdsKey, ref trainerIds);
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
                       string.Equals(task.TrainerHeroId, heroId, StringComparison.OrdinalIgnoreCase));
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
                string.Equals(task.TrainerHeroId, heroId, StringComparison.OrdinalIgnoreCase));
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
                Settlement? settlement = ResolveSettlement(task.SettlementId);
                result.Add(new TrainingTaskSnapshot
                {
                    TrainerPartyId = trainer?.StringId ?? string.Empty,
                    SettlementName = settlement?.Name?.ToString() ??
                                     (string.IsNullOrWhiteSpace(task.SettlementId)
                                         ? GwpText.Get("{=gwp_training_ledger_pending_place}To be selected")
                                         : task.SettlementId),
                    QueuedTime = CampaignTime.Hours((float)task.QueuedTimeHours),
                    Stage = GetTaskStage(task, trainer),
                    RemainingHours = task.StayStartHours > 0d
                        ? Math.Max(0d, task.StayStartHours +
                            GwpTuning.Training.ExchangeStayHours - CampaignTime.Now.ToHours)
                        : 0d,
                    IsAssigned = settlement != null
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

        /// <summary>
        /// 把每支灰袍部队的升级取向指向配比缺口最大的兵种。这里不改原版升级公式，
        /// 只写原版自己的 <c>Hero.PreferredUpgradeFormation</c>——原版
        /// <c>DefaultPartyTroopUpgradeModel.GetUpgradeChanceForTroopUpgrade</c> 会把
        /// 子树里含该兵种的那条分支抬到 9999 权重，`gwrecruit` 的三条分支于是几乎
        /// 全部走过去。不设它的话，原版按领主 RandomValue 的哈希把每个领主**永久**
        /// 锁死在一条分支上，骑兵被现场追截队抽走之后就再也补不回来。
        /// </summary>
        private static void SteerUpgradePreference(MobileParty party)
        {
            Hero? leader = party.LeaderHero;
            if (leader?.IsActive != true) return;

            // 玩家订单优先于配比。否则配比会把新兵引向别的分支，订单永远凑不齐数。
            // 但订单要的是队里没人能升上去的兵（最低级兵就是这样）时，钉偏好只会
            // 把新兵抽走，那还不如照常按配比走，缺的人由拆编重训补。
            CharacterObject? ordered =
                GreyWardenTroopRequestBehavior.GetOrderedTroopForTrainer(party);
            if (ordered != null &&
                GreyWardenTroopRequestBehavior.HasTrainableCohort(party, ordered))
            {
                if (leader.PreferredUpgradeFormation == ordered.DefaultFormationClass) return;
                FormationClass previousForOrder = leader.PreferredUpgradeFormation;
                leader.PreferredUpgradeFormation = ordered.DefaultFormationClass;
                GwpAiDiagnostics.WriteAction(party, "UPGRADE_PREFERENCE_STEERED",
                    "previous=" + previousForOrder +
                    "; desired=" + ordered.DefaultFormationClass +
                    "; reason=player_troop_order; orderedTroop=" + ordered.StringId);
                return;
            }

            int cavalry = 0, infantry = 0, archers = 0;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero ||
                    element.Number <= 0 || !GwpCommon.IsGreyWardenTroop(element.Character))
                    continue;
                switch (element.Character.DefaultFormationClass)
                {
                    case FormationClass.Cavalry: cavalry += element.Number; break;
                    case FormationClass.Ranged: archers += element.Number; break;
                    case FormationClass.Infantry: infantry += element.Number; break;
                }
            }

            // 按份额折算成"每一份"有多少人，取最少的那一类去补。总数为 0 时先补骑兵，
            // 它是唯一会被追截队持续抽走的兵种。
            float cavalryPerShare = (float)cavalry / GwpTuning.Training.CavalryShare;
            float infantryPerShare = (float)infantry / GwpTuning.Training.InfantryShare;
            float archerPerShare = (float)archers / GwpTuning.Training.ArcherShare;

            FormationClass desired = FormationClass.Cavalry;
            float lowest = cavalryPerShare;
            if (infantryPerShare < lowest) { desired = FormationClass.Infantry; lowest = infantryPerShare; }
            if (archerPerShare < lowest) { desired = FormationClass.Ranged; }

            if (leader.PreferredUpgradeFormation == desired) return;
            FormationClass previous = leader.PreferredUpgradeFormation;
            leader.PreferredUpgradeFormation = desired;
            GwpAiDiagnostics.WriteAction(party, "UPGRADE_PREFERENCE_STEERED",
                "previous=" + previous + "; desired=" + desired +
                "; cavalry=" + cavalry + "; infantry=" + infantry + "; archers=" + archers +
                "; shares=" + GwpTuning.Training.CavalryShare + ":" +
                GwpTuning.Training.InfantryShare + ":" + GwpTuning.Training.ArcherShare);
        }

        private void OnHourlyTick()
        {
            NormalizeAssignments();

            // 配比取向对每一支灰袍部队都要维持，不只是当值的练兵长。
            foreach (MobileParty party in PoliceStats.GetAllPoliceParties()
                         .Where(static candidate => candidate.LeaderHero?.IsActive == true)
                         .ToList())
                SteerUpgradePreference(party);

            foreach (MobileParty trainer in PoliceStats.GetAllPoliceParties()
                         .Where(GreyWardenFamilyBehavior.IsTrainingParty)
                         .Where(static party => party.LeaderHero?.IsActive == true)
                         .OrderBy(static party => party.StringId,
                             StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (GwpWardenDuties.HoldsAny(trainer, WardenDuty.PlayerTroopOrder, WardenDuty.PlayerRequest))
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
                bool settlementInvalid = !string.IsNullOrWhiteSpace(task.SettlementId) &&
                                         ResolveSettlement(task.SettlementId) == null;
                if (duplicate || ResolveParty(task.TrainerHeroId)?.IsActive != true || settlementInvalid)
                    CancelAssignment(task, duplicate ? "duplicate" : "invalid_after_load");
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

                bool trainerReady = IsFreeForTrainingWork(trainer, ignoreTrainingAssignment: true);
                if (string.IsNullOrWhiteSpace(task.SettlementId))
                {
                    if (trainerReady)
                        TryAssignVillage(task, trainer);
                    continue;
                }

                Settlement? village = ResolveSettlement(task.SettlementId);
                if (village == null || GwpFireswornBehavior.GuardTraineeCount(village) <= 0)
                {
                    RequeueVillage(task, trainer, "village_has_no_trainees");
                    continue;
                }

                if (!trainerReady || !GwpFireswornBehavior.HasArrived(trainer, village))
                {
                    if (task.StayStartHours > 0d)
                    {
                        task.StayStartHours = 0d;
                        GwpAiDiagnostics.WriteAction(trainer, "TRAINING_STAY_RESET",
                            "settlement=" + village.StringId);
                    }

                    if (trainerReady)
                        GreyWardenPartyDesireBehavior.RequestVisit(trainer, village,
                            validHours: GwpTuning.Training.MovementIntentHours);
                    continue;
                }

                if (task.StayStartHours <= 0d)
                {
                    task.StayStartHours = CampaignTime.Now.ToHours;
                    GwpAiDiagnostics.WriteAction(trainer, "TRAINING_STAY_STARTED",
                        "settlement=" + village.StringId + "; hours=" + GwpTuning.Training.ExchangeStayHours);
                }

                GreyWardenPartyDesireBehavior.RequestVisit(trainer, village,
                    validHours: GwpTuning.Training.MovementIntentHours);

                if (CampaignTime.Now.ToHours < task.StayStartHours +
                    GwpTuning.Training.ExchangeStayHours)
                    continue;

                int exchanged = ExchangeTroops(trainer, village);
                FinishAssignment(task, trainer, village, exchanged);
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

        private void TryAssignVillage(TrainingAssignment assignment, MobileParty trainer)
        {
            Settlement? village = GwpFireswornBehavior.NearestGuardToTrain(trainer);
            if (village == null) return;

            assignment.SettlementId = village.StringId;
            assignment.StayStartHours = 0d;
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_ASSIGNED",
                "settlement=" + village.StringId + "; elite=" + CountEliteTroops(trainer) +
                "; guardTrainees=" + GwpFireswornBehavior.GuardTraineeCount(village));
        }

        private void RequeueVillage(TrainingAssignment task, MobileParty trainer, string reason)
        {
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_VILLAGE_REQUEUED",
                "settlement=" + task.SettlementId + "; reason=" + reason);
            task.SettlementId = string.Empty;
            task.StayStartHours = 0d;
        }

        /// <summary>
        /// 一换一：守村队里最弱的可升级灰袍兵先跟她走（守村队那边的规矩在 GwpFireswornBehavior），
        /// 再按各兵种比例把同样多的满级老兵交给守村队。守村队人数不变。
        /// </summary>
        private static int ExchangeTroops(MobileParty trainer, Settlement village)
        {
            int requested = CountEliteTroops(trainer);
            if (requested <= 0) return 0;

            string trainerBefore = FormatGreyWardenRoster(trainer);
            var elites = trainer.MemberRoster.GetTroopRoster()
                .Where(static element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    GwpCommon.IsGreyWardenTroop(element.Character) &&
                    element.Character.UpgradeTargets.Length == 0)
                .OrderBy(static element => element.Character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
            int movedTrainees = GwpFireswornBehavior.ReleaseGuardTrainees(village, trainer, requested);
            int movedElites = TransferEliteTroopsProportionally(elites, movedTrainees,
                (troop, count) => GwpFireswornBehavior.AdmitToGuard(village, trainer.MemberRoster, troop, count),
                out string movedComposition);
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_EXCHANGE_ROSTER",
                "settlement=" + village.StringId + "; requested=" + requested +
                "; movedTrainee=" + movedTrainees + "; movedElite=" + movedElites +
                "; eliteComposition=" + movedComposition +
                "; trainerBefore=" + trainerBefore +
                "; trainerAfter=" + FormatGreyWardenRoster(trainer));
            return Math.Min(movedElites, movedTrainees);
        }

        private static int TransferEliteTroopsProportionally(IReadOnlyList<TroopRosterElement> batches,
            int requested, Func<CharacterObject, int, int> move, out string composition)
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

                int take = move(batches[index].Character, Math.Min(batches[index].Number, planned));
                if (take <= 0) continue;
                moved += take;
                movedParts.Add(batches[index].Character.StringId + ":" + take);
            }

            composition = movedParts.Count > 0 ? string.Join(",", movedParts) : "none";
            return moved;
        }



        private void FinishAssignment(TrainingAssignment task, MobileParty trainer,
            Settlement village, int exchanged)
        {
            _assignments.Remove(task);
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
            GwpPlayerRequestDeferral.NotifyDutyCompleted(trainer, "training_exchange");
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_COMPLETED",
                "settlement=" + village.StringId + "; exchanged=" + exchanged +
                "; trainerTrainable=" + CountTrainableTroops(trainer) +
                "; guardTrainees=" + GwpFireswornBehavior.GuardTraineeCount(village));
        }

        private void CancelAssignment(TrainingAssignment task, string reason)
        {
            MobileParty? trainer = ResolveParty(task.TrainerHeroId);
            _assignments.Remove(task);
            if (trainer?.IsActive != true) return;
            GreyWardenPartyDesireBehavior.ClearIntent(trainer);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(trainer);
            GwpAiDiagnostics.WriteAction(trainer, "TRAINING_TASK_CANCELLED",
                "settlement=" + task.SettlementId + "; reason=" + reason);
        }

        private static bool IsFreeForTrainingWork(MobileParty? party,
            bool ignoreTrainingAssignment = false)
        {
            if (party?.IsActive != true || !party.IsLordParty || party.IsDisbanding ||
                party.LeaderHero?.IsActive != true || party.Army != null ||
                party.MapEvent is { IsFinalized: false })
                return false;

            return ignoreTrainingAssignment
                ? GwpWardenDuties.IsFreeFor(party, WardenDuty.Training, WardenDuty.Training)
                : GwpWardenDuties.IsFreeFor(party, WardenDuty.Training);
        }

        private static TrainingTaskStage GetTaskStage(TrainingAssignment task, MobileParty? trainer)
        {
            if (string.IsNullOrWhiteSpace(task.SettlementId))
                return TrainingTaskStage.QueuedForTrainer;
            if (task.StayStartHours > 0d)
                return TrainingTaskStage.ExchangingAtSettlement;
            if (!IsFreeForTrainingWork(trainer, ignoreTrainingAssignment: true))
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
                // 依据：实机诊断 2026-09-27、2026-10-02（读档时 Hero.FindFirst 因英雄全局表未建好抛 ArgumentNullException）
                // Hero.FindFirst throws until Hero's global collection exists (during load).
                return null;
            }
        }

        private static Settlement? ResolveSettlement(string settlementId) =>
            string.IsNullOrWhiteSpace(settlementId) ? null : Settlement.Find(settlementId);
    }
}
