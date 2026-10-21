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
        // 只接续旧存档里的在途老兵队；挂名练兵官不再派出新队。

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

    }
}
