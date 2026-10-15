using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        // 冷却是进行中的工作状态，随同 FaithParty 的队伍/目标/阶段/出发时间一起存档。
        private readonly Dictionary<string, double> _trainingDepartures =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyList<GreyWardenTrainingBehavior.TrainingTaskSnapshot> TrainingTransports()
        {
            var tasks = new List<GreyWardenTrainingBehavior.TrainingTaskSnapshot>();
            if (_instance == null) return tasks;
            foreach (FaithParty record in _instance._faithParties.Values.Where(p => p.Kind == FaithPartyKind.Training))
                tasks.Add(new GreyWardenTrainingBehavior.TrainingTaskSnapshot
                {
                    TrainerPartyId = ResolveTrainer(record.TrainerId)?.StringId ?? string.Empty,
                    SettlementName = Settlement.Find(record.HomeId).Name.ToString(),
                    QueuedTime = CampaignTime.Hours((float)record.PhaseHour),
                    IsAssigned = true,
                    Stage = record.Phase == FaithPartyPhase.Outbound
                        ? GreyWardenTrainingBehavior.TrainingTaskStage.TravelingToRendezvous
                        : GreyWardenTrainingBehavior.TrainingTaskStage.SettlingInVillage
                });
            return tasks;
        }

        private static MobileParty? ResolveTrainer(string heroId) =>
            PoliceStats.GetAllPoliceParties().FirstOrDefault(p => p.IsActive && p.LeaderHero?.StringId == heroId);

        private void DispatchTrainingTransports()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (MobileParty trainer in PoliceStats.GetAllPoliceParties().Where(GreyWardenFamilyBehavior.IsTrainingParty).ToList())
            {
                if (!trainer.IsActive || trainer.LeaderHero?.IsActive != true || trainer.MapEvent != null ||
                    trainer.IsCurrentlyAtSea || trainer.IsDisbanding ||
                    GwpWardenDuties.HoldsAny(trainer, WardenDuty.PlayerTroopOrder, WardenDuty.PlayerRequest) ||
                    _faithParties.Values.Any(p => p.Kind == FaithPartyKind.Training && p.TrainerId == trainer.LeaderHero.StringId))
                    continue;
                List<TroopRosterElement> elites = trainer.MemberRoster.GetTroopRoster()
                    .Where(e => IsGuardTroop(e.Character) && e.Character.UpgradeTargets.Length == 0 && e.Number > e.WoundedNumber)
                    .OrderByDescending(e => e.Character.Tier).ThenBy(e => e.Character.StringId).ToList();
                int healthyVeterans = elites.Sum(e => e.Number - e.WoundedNumber);
                int requested = TrainingVeteransToDispatch(trainer.MemberRoster.TotalManCount, healthyVeterans);
                if (requested < GwpTuning.Firesworn.BalanceEscortSize) continue;
                float chanceMultiplier = healthyVeterans > trainer.MemberRoster.TotalManCount * GwpTuning.Training.VeteranDispatchBoostShare
                    ? GwpTuning.Training.VeteranDispatchChanceMultiplier : 1f;
                double last = _trainingDepartures.TryGetValue(trainer.LeaderHero.StringId, out double sent) ? sent : Never;
                FaithVillage? row = _villages.Values
                    .Where(v => IsPreachedVillage(Settlement.Find(v.VillageId)) &&
                        IsTripTargetValid(Settlement.Find(v.VillageId)) && !HasRitual(Settlement.Find(v.VillageId)))
                    .OrderBy(v => Settlement.Find(v.VillageId).GetPosition2D.DistanceSquared(trainer.GetPosition2D))
                    .FirstOrDefault(v => IsFestivalDay(v, last, now, chanceMultiplier));
                if (row == null) continue;
                Settlement village = Settlement.Find(row.VillageId);
                MobileParty transport = CreateFaithParty(row, village, FaithPartyKind.Training, trainer);
                var healthy = elites.Select(e => new TroopRosterElement(e.Character) { Number = e.Number - e.WoundedNumber }).ToList();
                int moved = TransferEliteTroopsProportionally(healthy, requested,
                    (troop, count) =>
                    {
                        MoveBestSets(trainer.StringId, transport.StringId, troop, count);
                        return GwpTroopTransfer.Move(trainer.MemberRoster, transport.MemberRoster, troop, count, healthyOnly: true).Number;
                    },
                    out string composition);
                FaithParty record = _faithParties[transport.StringId];
                record.TrainerId = trainer.LeaderHero.StringId;
                // 高阶老兵自带装备；多余军械装在背包里，路上能被抢，不能远程交给村庄。
                int cargo = MoveAll(PlanOf(trainer.StringId).Surplus, ItemsOf(_armories, trainer.StringId), transport.ItemRoster);
                _plans.Remove(trainer.StringId);
                _trainingDepartures[record.TrainerId] = now;
                Dispatch(record, transport, village);
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteFaith("TRAINING_TRANSPORT_DISPATCHED",
                    "party=" + transport.StringId + "; trainer=" + trainer.StringId + "; village=" + row.VillageId +
                    "; veterans=" + moved + "; composition=" + composition + "; cargo=" + cargo);
#endif
            }
        }

        private static int TrainingVeteransToDispatch(int total, int healthyVeterans)
        {
            if (healthyVeterans <= total * GwpTuning.Training.VeteranDispatchTriggerShare)
                return 0;
            // 派出后分母也变小：(老兵 - 派出) / (全队 - 派出) <= 目标占比。
            int desired = (int)Math.Ceiling((healthyVeterans - total * GwpTuning.Training.VeteranDispatchTargetShare) /
                (1d - GwpTuning.Training.VeteranDispatchTargetShare));
            return Math.Min(desired, Math.Min(healthyVeterans,
                Math.Max(0, total - GwpTuning.Firesworn.MissionMinimumLeftBehind)));
        }

        private void TendTrainingTransport(FaithParty record, MobileParty party)
        {
            Settlement village = Settlement.Find(record.HomeId);
            MobileParty? trainer = ResolveTrainer(record.TrainerId);
            bool timedOut = CampaignTime.Now.ToHours - record.PhaseHour > GwpTuning.Firesworn.FaithPartyTravelTimeoutHours;
            if (record.Phase != FaithPartyPhase.Outbound || trainer?.LeaderHero?.IsActive != true || timedOut ||
                !IsTripTargetValid(village) || !IsPreachedVillage(village))
            {
                SettleTrainingTransport(record, party);
                return;
            }
            if (!HasArrived(party, village))
            {
                Travel(party, village);
                return;
            }
            // 单程交付：老兵、人货和军械全部落村，新兵继续留村供正常招募。
            JoinVillage(record, party, village);
        }

        private void SettleTrainingTransport(FaithParty record, MobileParty party)
        {
            Settlement? home = NearestFaithVillage(party);
            if (home == null) return;
            // 这是最终落村阶段，不再追原队；阶段与目标已进入存档。
            if (!string.IsNullOrEmpty(record.TrainerId) || record.Phase != FaithPartyPhase.Returning)
                SetPhase(record, FaithPartyPhase.Returning, home.StringId);
            record.TrainerId = string.Empty;
            record.HomeId = home.StringId;
            record.TargetId = home.StringId;
            if (!HasArrived(party, home))
            {
                Travel(party, home);
                return;
            }
            JoinVillage(record, party, home);
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
    }
}
