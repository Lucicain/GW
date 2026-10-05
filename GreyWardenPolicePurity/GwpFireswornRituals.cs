using System;
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
    /// 烈火责罚与集会传教（docs/state/firesworn.md）。两件事的频率参考比武大会（用户裁定）：
    /// 比武大会每座城每季只有固定的一周能开、两次至少隔 15 天（C#反编译 v1.4.8 TournamentCampaignBehavior）。
    /// 这里烧不信教者也是每座信众村每季一个固定的周、至少隔 15 天；地牢里有俘虏时另算，隔 5 天就能烧。
    /// 传教由各村的传教队主持（GwpFireswornParties.cs），同一座村两次至少隔 15 天。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        private void UpdateRituals()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (Ritual ritual in _rituals.ToList())
            {
                Settlement village = Settlement.Find(ritual.VillageId);
                if (ritual.Kind == RitualKind.Damnation)
                {
                    if (now >= ritual.EndHour)
                        _rituals.Remove(ritual);
                    continue;
                }

                MobileParty? host = FindParty(ritual.HostPartyId);
                if (host?.IsActive != true)
                {
                    CancelCongregation(ritual, "host_lost");
                    continue;
                }
                if (!IsVillageQuiet(village))
                {
                    CancelCongregation(ritual, "village_unrest");
                    continue;
                }
                GreyWardenPartyDesireBehavior.RequestVisit(host, village, GwpTuning.Firesworn.FaithPartyVisitScore);
                if (now >= ritual.EndHour)
                    CompleteCongregation(ritual, host, village);
            }
        }

        private void StartCongregation(FaithParty record, MobileParty host, Settlement village)
        {
            double now = CampaignTime.Now.ToHours;
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Congregation,
                HostPartyId = host.StringId,
                Started = true,
                QueuedHour = now,
                EndHour = now + GwpTuning.Firesworn.CongregationHours
            });
            SetPhase(record, FaithPartyPhase.Working, village.StringId);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_congregation_started}{HOST} has opened a Congregation of the Eternal Fire in {VILLAGE}.",
                "HOST", host.Name, "VILLAGE", village.Name), Colors.Gray));
            GwpAiDiagnostics.WriteFaith("CONGREGATION_STARTED",
                "party=" + host.StringId + "; home=" + record.HomeId + "; village=" + village.StringId +
                "; preached=" + IsPreachedVillage(village));
        }

        private void CompleteCongregation(Ritual ritual, MobileParty host, Settlement village)
        {
            _rituals.Remove(ritual);
            FaithVillage row = EnsureVillage(village);
            int gain = Math.Max(GwpTuning.Firesworn.CongregationMinimumGain,
                (int)Math.Round(village.Village.Hearth * GwpTuning.Firesworn.CongregationHearthShare));
            int before = row.Zealots;
            row.Zealots = Math.Min(ZealotCap(village), row.Zealots + gain);
            row.LastCongregation = CampaignTime.Now.ToHours;
            PushMilitia(row, village);
            if (_faithParties.TryGetValue(host.StringId, out FaithParty? record))
                SetPhase(record, FaithPartyPhase.Returning, record.HomeId);

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_congregation_done}The Congregation in {VILLAGE} has ended. The village now counts {ZEALOTS} zealots.",
                "VILLAGE", village.Name, "ZEALOTS", row.Zealots), Colors.Gray));
            GwpAiDiagnostics.WriteFaith("CONGREGATION_COMPLETED",
                "party=" + host.StringId + "; village=" + village.StringId + "; before=" + before +
                "; gain=" + gain + "; after=" + row.Zealots + "; hearth=" + village.Village.Hearth.ToString("0"));
        }

        private void CancelCongregation(Ritual ritual, string reason)
        {
            _rituals.Remove(ritual);
            if (_faithParties.TryGetValue(ritual.HostPartyId, out FaithParty? record))
                SetPhase(record, FaithPartyPhase.Returning, record.HomeId);
            if (ritual.Started)
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_fs_congregation_broken}The Congregation in {VILLAGE} broke up before it was finished.",
                    "VILLAGE", Settlement.Find(ritual.VillageId).Name), Colors.Gray));
            GwpAiDiagnostics.WriteFaith("CONGREGATION_CANCELLED",
                "party=" + ritual.HostPartyId + "; village=" + ritual.VillageId + "; reason=" + reason);
        }

        private bool IsCongregationTarget(Settlement settlement, Clan police)
        {
            if (!settlement.IsVillage || !IsVillageQuiet(settlement) || HasRitual(settlement) ||
                FactionManager.IsAtWarAgainstFaction(police, settlement.MapFaction))
                return false;
            if (!_villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                return true;
            return row.Zealots < ZealotCap(settlement) &&
                   CampaignTime.Now.ToHours - row.LastCongregation >=
                   GwpTuning.Firesworn.CongregationVillageIntervalDays * 24d;
        }

        private bool HasRitual(Settlement settlement) => _rituals.Any(ritual =>
            string.Equals(ritual.VillageId, settlement.StringId, StringComparison.OrdinalIgnoreCase));

        // ---------------------------------------------------------------- 烈火责罚

        /// <summary>
        /// 每天一次。地牢里俘虏够数、离上次烧够 5 天就烧俘虏；否则只在本村每季固定的那一周里，
        /// 离上次烧够 15 天，按概率烧村里不信教的人。
        /// </summary>
        private void ScheduleDamnations()
        {
            double now = CampaignTime.Now.ToHours;
            int weekOfSeason = CampaignTime.Now.GetDayOfSeason / CampaignTime.DaysInWeek;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement village = Settlement.Find(row.VillageId);
                if (HasRitual(village) || !IsVillageQuiet(village))
                    continue;
                double sinceLast = now - row.LastDamnation;
                int prisoners = _dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon)
                    ? dungeon.TotalManCount
                    : 0;
                if (prisoners >= GwpTuning.Firesworn.DamnationMinimumPrisoners &&
                    sinceLast >= GwpTuning.Firesworn.DamnationPrisonerIntervalDays * 24d)
                {
                    BurnPrisoners(row, village, dungeon!);
                    continue;
                }
                if (weekOfSeason == StableDay(row.VillageId, CampaignTime.WeeksInSeason) &&
                    sinceLast >= GwpTuning.Firesworn.DamnationFestivalIntervalDays * 24d &&
                    village.Village.Hearth > GwpTuning.Firesworn.DirectZealotHearthFloor &&
                    MBRandom.RandomFloat < GwpTuning.Firesworn.DamnationFestivalDailyChance)
                    BurnUnbelievers(row, village);
            }
        }

        /// <summary>
        /// 烧俘虏：俘虏烧死，同时有一部分被火说服，成了狂信者、在村里落户（用户裁定：烧俘虏加狂信者、加人口）。
        /// 每烧 1 人从公库付钱（用户裁定：数据上只扣钱），公库不够就少烧。
        /// </summary>
        private void BurnPrisoners(FaithVillage row, Settlement village, TroopRoster dungeon)
        {
            int batch = Math.Min(dungeon.TotalManCount, GwpTuning.Firesworn.DamnationMaximumPrisoners);
            batch = Math.Min(batch, PoliceResourceManager.GetJudicialTreasuryBalance() /
                                    GwpTuning.Firesworn.DamnationCostPerHead);
            if (batch <= 0)
                return;
            int left = batch;
            foreach (TroopRosterElement element in dungeon.GetTroopRoster().ToList())
            {
                if (left <= 0)
                    break;
                int take = Math.Min(left, element.Number);
                dungeon.AddToCounts(element.Character, -take);
                left -= take;
            }

            int converts = (int)Math.Round(batch * GwpTuning.Firesworn.DamnationPrisonerConvertShare);
            float hearthBefore = village.Village.Hearth;
            village.Village.Hearth += converts * GwpTuning.Firesworn.HearthPerPerson;
            int before = row.Zealots;
            row.Zealots = Math.Min(ZealotCap(village), row.Zealots + converts);
            FinishDamnation(row, village, batch, "prisoners", before, hearthBefore);
        }

        /// <summary>
        /// 没有俘虏时烧村里不信教的人：户数减少，看着的人信了，狂信者增加（用户裁定）。
        /// </summary>
        private void BurnUnbelievers(FaithVillage row, Settlement village)
        {
            int burned = GwpTuning.Firesworn.DamnationUnbelieverBase +
                         (int)(village.Village.Hearth / GwpTuning.Firesworn.DamnationHearthPerUnbeliever);
            burned = Math.Min(burned, PoliceResourceManager.GetJudicialTreasuryBalance() /
                                      GwpTuning.Firesworn.DamnationCostPerHead);
            if (burned <= 0)
                return;
            float hearthBefore = village.Village.Hearth;
            village.Village.Hearth -= burned * GwpTuning.Firesworn.HearthPerPerson;
            int before = row.Zealots;
            row.Zealots = Math.Min(ZealotCap(village),
                row.Zealots + burned * GwpTuning.Firesworn.DamnationWitnessConverts);
            FinishDamnation(row, village, burned, "unbelievers", before, hearthBefore);
        }

        private void FinishDamnation(FaithVillage row, Settlement village, int burned, string mode,
            int zealotsBefore, float hearthBefore)
        {
            int cost = burned * GwpTuning.Firesworn.DamnationCostPerHead;
            PoliceResourceManager.SpendJudicialTreasury(cost);
            double now = CampaignTime.Now.ToHours;
            row.LastDamnation = now;
            PushMilitia(row, village);
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Damnation,
                Started = true,
                QueuedHour = now,
                EndHour = now + GwpTuning.Firesworn.DamnationLockHours
            });
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_damnation_done}{HOST} held a Damnation in {VILLAGE}: {BURNED} burned, and the village now counts {ZEALOTS} zealots.",
                "HOST", RitualHostName(_rituals[_rituals.Count - 1], village), "VILLAGE", village.Name,
                "BURNED", burned, "ZEALOTS", row.Zealots), Colors.Gray));
            GwpAiDiagnostics.WriteFaith("DAMNATION",
                "village=" + village.StringId + "; mode=" + mode + "; burned=" + burned + "; cost=" + cost +
                "; zealots=" + zealotsBefore + "->" + row.Zealots +
                "; hearth=" + hearthBefore.ToString("0.0") + "->" + village.Village.Hearth.ToString("0.0") +
                "; dungeonLeft=" + (_dungeons.TryGetValue(village.StringId, out TroopRoster? dungeon) ? dungeon.TotalManCount : 0));
        }

        /// <summary>仪式画面里主持人的名字：烧人由祭司主持（没有祭司就写村名），传教写传教队。</summary>
        private string RitualHostName(Ritual ritual, Settlement village)
        {
            if (ritual.Kind == RitualKind.Congregation)
                return FindParty(ritual.HostPartyId)?.Name?.ToString() ?? village.Name.ToString();
            Hero? priest = _villages.TryGetValue(village.StringId, out FaithVillage? row) ? FindLivingPriest(row) : null;
            return priest?.Name?.ToString() ?? village.Name.ToString();
        }
    }
}
