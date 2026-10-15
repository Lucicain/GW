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
    /// <summary>全队兵种配比与练兵运兵台账；自动换兵由实体运兵队执行，练兵官不离开案件、不额外发经验。</summary>
    public sealed class GreyWardenTrainingBehavior : CampaignBehaviorBase
    {
        internal enum TrainingTaskStage { TravelingToRendezvous, ReturningToTrainer, SettlingInVillage }
        internal sealed class TrainingTaskSnapshot
        {
            public string TrainerPartyId { get; set; } = string.Empty;
            public string SettlementName { get; set; } = string.Empty;
            public CampaignTime QueuedTime { get; set; }
            public TrainingTaskStage Stage { get; set; }
            public bool IsAssigned { get; set; }
        }
        public override void RegisterEvents() =>
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        // 本类只维护每小时可重算的升级偏好；进行中的运兵和冷却由 GwpFireswornBehavior 存档。
        public override void SyncData(IDataStore dataStore) { }
        internal static IReadOnlyList<TrainingTaskSnapshot> GetTaskSnapshots() =>
            GwpFireswornBehavior.TrainingTransports();
        private void OnHourlyTick()
        {
            foreach (MobileParty party in PoliceStats.GetAllPoliceParties())
                if (party.LeaderHero?.IsActive == true)
                    SteerUpgradePreference(party);
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

    }
}
