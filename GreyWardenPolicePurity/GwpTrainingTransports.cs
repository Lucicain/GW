using System;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
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
                        : string.IsNullOrEmpty(record.TrainerId)
                            ? GreyWardenTrainingBehavior.TrainingTaskStage.SettlingInVillage
                            : GreyWardenTrainingBehavior.TrainingTaskStage.ReturningToTrainer
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
                int available = Math.Min(elites.Sum(e => e.Number - e.WoundedNumber),
                    Math.Max(0, trainer.MemberRoster.TotalManCount - GwpTuning.Firesworn.MissionMinimumLeftBehind));
                if (available < GwpTuning.Firesworn.BalanceEscortSize) continue;
                double last = _trainingDepartures.TryGetValue(trainer.LeaderHero.StringId, out double sent) ? sent : Never;
                FaithVillage? row = _villages.Values
                    .Where(v => IsPreachedVillage(Settlement.Find(v.VillageId)) &&
                        IsTripTargetValid(Settlement.Find(v.VillageId)) && !HasRitual(Settlement.Find(v.VillageId)) &&
                        GuardTraineeCount(Settlement.Find(v.VillageId)) >= GwpTuning.Firesworn.BalanceMinimumMen)
                    .OrderBy(v => Settlement.Find(v.VillageId).GetPosition2D.DistanceSquared(trainer.GetPosition2D))
                    .FirstOrDefault(v => IsFestivalDay(v, last, now));
                if (row == null) continue;
                Settlement village = Settlement.Find(row.VillageId);
                int requested = Math.Min(available, GuardTraineeCount(village));
                MobileParty transport = CreateFaithParty(row, village, FaithPartyKind.Training, trainer);
                var healthy = elites.Select(e => new TroopRosterElement(e.Character) { Number = e.Number - e.WoundedNumber }).ToList();
                int moved = TransferEliteTroopsProportionally(healthy, requested,
                    (troop, count) => GwpTroopTransfer.Move(trainer.MemberRoster, transport.MemberRoster, troop, count, healthyOnly: true).Number,
                    out string composition);
                FaithParty record = _faithParties[transport.StringId];
                record.TrainerId = trainer.LeaderHero.StringId;
                // 高阶老兵自带装备；多余军械装在背包里，路上能被抢，不能远程交给村庄。
                int cargo = MoveAll(PlanOf(trainer.StringId).Surplus, ItemsOf(_armories, trainer.StringId), transport.ItemRoster);
                _plans.Remove(trainer.StringId);
                _trainingDepartures[record.TrainerId] = now;
                Dispatch(record, transport, village);
                GwpAiDiagnostics.WriteFaith("TRAINING_TRANSPORT_DISPATCHED",
                    "party=" + transport.StringId + "; trainer=" + trainer.StringId + "; village=" + row.VillageId +
                    "; veterans=" + moved + "; composition=" + composition + "; cargo=" + cargo);
            }
        }

        private void TendTrainingTransport(FaithParty record, MobileParty party)
        {
            Settlement village = Settlement.Find(record.HomeId);
            MobileParty? trainer = ResolveTrainer(record.TrainerId);
            bool timedOut = CampaignTime.Now.ToHours - record.PhaseHour > GwpTuning.Firesworn.FaithPartyTravelTimeoutHours;
            if (trainer?.LeaderHero?.IsActive != true || timedOut)
            {
                SettleTrainingTransport(record, party);
                return;
            }
            if (record.Phase == FaithPartyPhase.Outbound)
            {
                if (!IsTripTargetValid(village) || !IsPreachedVillage(village))
                {
                    SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                    return;
                }
                if (!HasArrived(party, village))
                {
                    Travel(party, village);
                    return;
                }
                // 先取出弱兵，避免刚送入的兵被再次取走。按实际到达的老兵人数交换，减员不补发。
                int veterans = party.MemberRoster.GetTroopRoster().Where(e => IsGuardTroop(e.Character) &&
                    e.Character.UpgradeTargets.Length == 0).Sum(e => e.Number);
                int trainees = ReleaseGuardTrainees(village, party, veterans);
                int delivered = 0;
                foreach (TroopRosterElement batch in party.MemberRoster.GetTroopRoster()
                    .Where(e => IsGuardTroop(e.Character) && e.Character.UpgradeTargets.Length == 0).ToList())
                    delivered += AdmitToGuard(village, party.MemberRoster, batch.Character, batch.Number);
                UnloadKits(party, village.StringId);
                // 取回的弱兵自带的军械仍在本队账本里，归队时一同交还练兵官。
                SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
                GwpAiDiagnostics.WriteFaith("TRAINING_TRANSPORT_EXCHANGED",
                    "party=" + party.StringId + "; village=" + village.StringId + "; veterans=" + delivered + "; trainees=" + trainees);
                if (party.MemberRoster.TotalManCount == 0)
                    JoinVillage(record, party, village);
                return;
            }

            // 与送信队相同的陆路可达检查；海上的练兵官等下一小时，超时则实际走回信众村。
            if (trainer.MapEvent != null || trainer.IsCurrentlyAtSea ||
                !NavigationHelper.IsPositionValidForNavigationType(trainer.Position, party.NavigationCapability))
                return;
            if (party.GetPosition2D.Distance(trainer.GetPosition2D) > GwpTuning.Firesworn.ArrivalDistance)
            {
                GwpWardenDispatchBehavior.KeepCourierDisposition(party);
                GreyWardenPartyDesireBehavior.RequestEscort(party, trainer);
                return;
            }
            int returned = 0;
            foreach (TroopRosterElement batch in party.MemberRoster.GetTroopRoster().Where(e => IsGuardTroop(e.Character)).ToList())
            {
                int count = Math.Min(batch.Number, FreeSlots(trainer));
                MoveBestSets(party.StringId, trainer.StringId, batch.Character, count);
                returned += GwpTroopTransfer.Move(party.MemberRoster, trainer.MemberRoster, batch.Character, count).Number;
            }
            GwpAiDiagnostics.WriteFaith("TRAINING_TRANSPORT_RETURNED",
                "party=" + party.StringId + "; trainer=" + trainer.StringId + "; troops=" + returned +
                "; overflow=" + party.MemberRoster.TotalManCount);
            if (party.MemberRoster.TotalManCount > 0)
            {
                // 原队补满后不超编塞兵；剩下的人带着自己的军械走回村里。
                record.TrainerId = string.Empty;
                SettleTrainingTransport(record, party);
                return;
            }
            ReturnPartyLedger(party, trainer.StringId);
            GwpTroopTransfer.MoveBatches(party.PrisonRoster, trainer.PrisonRoster,
                party.PrisonRoster.GetTroopRoster(), party.PrisonRoster.TotalManCount);
            UnloadKits(party, trainer.StringId);
            trainer.ItemRoster.Add(party.ItemRoster);
            party.ItemRoster.Clear();
            CreditTreasury(party.PartyTradeGold);
            party.PartyTradeGold = 0;
            _faithParties.Remove(party.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            DestroyPartyAction.Apply(null, party);
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
