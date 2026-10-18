using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    // A read-only presentation snapshot. Business state remains with its campaign behavior.
    internal sealed class GwpAffairsEntry
    {
        internal GwpAffairsEntry(string id, string group, string title) { Id = id; Group = group; Title = title; }
        internal string Id { get; }
        internal string Group { get; }
        internal string Title { get; }
        internal string Summary { get; set; } = string.Empty;
        internal string WorkSummary { get; set; } = string.Empty;
        internal string NotesTitle { get; set; } = string.Empty;
        internal GreyWardenFamilyBehavior.DutyKind Duty { get; set; }
        internal bool IsPersonal { get; set; }
        internal bool IsOffer { get; set; }
        internal int DisplayedFine { get; set; }
        internal int DisplayedReward { get; set; }
        internal Hero? Hero { get; set; }
        internal Hero? Assignee { get; set; }
        internal Settlement? Place { get; set; }
        internal List<string> Fields { get; } = new();
        internal List<string> Notes { get; } = new();
        internal List<string> Work { get; } = new();
        internal List<string> Reports { get; } = new();
    }

    internal static class GwpAffairsText
    {
        internal static string Line(string labelKey, object value) => GwpText.Get(GwpTextCatalog.gwp_affairs_line,
            "LABEL", GwpText.Get(labelKey), "VALUE", value);
        internal static string Money(int value) => GwpText.Get(GwpTextCatalog.gwp_affairs_amount, "VALUE", value);
        internal static string Pair(string a, string b) => GwpText.Get(GwpTextCatalog.gwp_affairs_row, "A", a, "B", b);
        internal static string TargetLocation(MobileParty target, string? labelKey = null) => Line(labelKey ?? GwpTextCatalog.gwp_affairs_location,
            target.CurrentSettlement?.Name?.ToString() ??
            GwpText.Get(GwpTextCatalog.gwp_affairs_near, "PLACE",
                GwpCommon.FindNearestTown(target.GetPosition2D)?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
        internal static string Duty(GreyWardenFamilyBehavior.DutyKind duty) => GwpText.Get(duty switch
        {
            GreyWardenFamilyBehavior.DutyKind.Training => GwpTextCatalog.gwp_duty_training,
            GreyWardenFamilyBehavior.DutyKind.CaravanProtection => GwpTextCatalog.gwp_duty_caravan,
            GreyWardenFamilyBehavior.DutyKind.VillageProtection => GwpTextCatalog.gwp_duty_village,
            GreyWardenFamilyBehavior.DutyKind.IssueResolution => GwpTextCatalog.gwp_duty_issues,
            GreyWardenFamilyBehavior.DutyKind.Reconstruction => GwpTextCatalog.gwp_greywardenfamilybehavior_053,
            GreyWardenFamilyBehavior.DutyKind.PlayerRequests => GwpTextCatalog.gwp_duty_player,
            _ => GwpTextCatalog.gwp_affairs_children
        });
    }

    internal static class GwpAffairsData
    {
        internal static readonly GreyWardenFamilyBehavior.DutyKind[] Duties =
        {
            GreyWardenFamilyBehavior.DutyKind.Training, GreyWardenFamilyBehavior.DutyKind.CaravanProtection,
            GreyWardenFamilyBehavior.DutyKind.VillageProtection, GreyWardenFamilyBehavior.DutyKind.IssueResolution,
            GreyWardenFamilyBehavior.DutyKind.Reconstruction, GreyWardenFamilyBehavior.DutyKind.PlayerRequests
        };
        internal static MobileParty? FindParty(string id) => MobileParty.All.FirstOrDefault(p => p.StringId == id);

        private static GwpAffairsEntry Formatted(string id, GreyWardenFamilyBehavior.DutyKind duty,
            GwpCaseArchiveItemVM item, string assignee, bool personal = false)
        {
            var entry = new GwpAffairsEntry(id, GwpAffairsText.Duty(duty), item.Header)
            { Duty = duty, IsPersonal = personal, Summary = item.AssignmentText, WorkSummary = item.AssignmentText,
                NotesTitle = GwpText.Get(GwpTextCatalog.gwp_affairs_processing), Assignee = FindParty(assignee)?.LeaderHero };
            entry.Fields.Add(item.DetailsText);
            entry.Notes.Add(item.TimeText);
            return entry;
        }

        internal static List<GwpAffairsEntry> Read()
        {
            var result = new List<GwpAffairsEntry>();
            PlayerBountyBehavior? bounty = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            var available = new HashSet<string>(bounty?.GetAffairsOffers().Select(c => c.CrimeId) ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var assigned = CrimePool.ActiveTasks.Values.Where(t => t.TargetCrime != null)
                .GroupBy(t => t.TargetCrimeId).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var reports = PoliceEnforcementBehavior.GetEnvoyReports();
            foreach (CrimeRecord crime in CrimePool.LedgerRecords.Where(c => c.HasOpenCase &&
                         (c.CrimeCategory == GwpCrimeCategory.CaravanAttack || c.CrimeCategory == GwpCrimeCategory.VillageViolence)))
            {
                assigned.TryGetValue(crime.CrimeId, out PoliceTask? task);
                var duty = crime.CrimeCategory == GwpCrimeCategory.CaravanAttack
                    ? GreyWardenFamilyBehavior.DutyKind.CaravanProtection : GreyWardenFamilyBehavior.DutyKind.VillageProtection;
                var item = new GwpCaseArchiveItemVM(crime, task);
                GwpAffairsEntry entry = Formatted(crime.CrimeId, duty, item, task?.PolicePartyId ?? string.Empty);
                entry.NotesTitle = GwpText.Get(GwpTextCatalog.gwp_affairs_incident);
                entry.Fields.Insert(0, item.AssignmentText);
                entry.Hero = crime.OffenderHero;
                entry.IsOffer = available.Contains(crime.CrimeId);
                entry.DisplayedFine = GwpFieldArrestPricing.AssessFine(crime);
                int reward = PlayerBountyBehavior.EstimateAffairsReward(crime);
                entry.DisplayedReward = reward;
                entry.Summary = GwpAffairsText.Pair(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_reward, GwpAffairsText.Money(reward)),
                    PlayerBountyBehavior.AffairsDifficulty(crime));
                entry.Notes.Add(GwpText.Get(GwpTextCatalog.gwp_affairs_reward_note));
                entry.Fields.Insert(0, GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_fine, GwpAffairsText.Money(entry.DisplayedFine)));
                MobileParty? target = crime.Offender;
                if (target?.IsActive == true)
                {
                    entry.Place = target.CurrentSettlement;
                    entry.Fields.Insert(0, GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_formation,
                        GwpText.Get(target.AttachedTo != null || target.Army?.LeaderParty == target
                            ? GwpTextCatalog.gwp_affairs_army : GwpTextCatalog.gwp_affairs_party)));
                    entry.Fields.Insert(0, GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_men, GwpText.Get(GwpTextCatalog.gwp_affairs_numbers,
                        "TOTAL", target.MemberRoster.TotalManCount, "HEALTHY", target.Party.NumberOfHealthyMembers)));
                    entry.Fields.Insert(0, GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_strength, GwpText.Format(GwpStrength.CaseTarget(crime), "0")));
                    entry.Fields.Insert(0, GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_own_strength,
                        GwpText.Format(MobileParty.MainParty == null ? 0 : GwpStrength.CombatGroup(MobileParty.MainParty), "0")));
                    entry.Fields.Insert(0, GwpAffairsText.TargetLocation(target));
                }
                else entry.Fields.Insert(0, GwpText.Get(GwpTextCatalog.gwp_affairs_unavailable));
                // Reward and difficulty are already the selected row's detail summary.
                // Incident details are secondary. The current target position above is the actionable location.
                entry.Fields.Remove(item.DetailsText);
                entry.Notes.Add(item.DetailsText);
                foreach (var report in reports.Where(r => r.OffenderHeroId == crime.OffenderHeroId))
                {
                    var reportVM = new GwpCaseArchiveItemVM(report);
                    entry.Reports.Add(GwpAffairsText.Pair(reportVM.TimeText, reportVM.AssignmentText));
                    entry.Reports.Add(reportVM.DetailsText);
                }
                result.Add(entry);
            }
            foreach (var assist in PoliceEnforcementBehavior.GetAssistanceTaskSnapshots())
            {
                GwpAffairsEntry? entry = result.FirstOrDefault(e => e.Id == assist.CrimeId);
                if (entry != null) entry.Notes.Add(new GwpCaseArchiveItemVM(assist).AssignmentText);
            }
            // Completed negotiations are reports on a member/case, never additional work or cases.
            foreach (var item in GreyWardenVillageAdoptionBehavior.GetTaskSnapshots())
            {
                var entry = Formatted("relief:" + item.VillageSettlementId, GreyWardenFamilyBehavior.DutyKind.VillageProtection,
                    new GwpCaseArchiveItemVM(item), item.PolicePartyId);
                entry.Place = Settlement.Find(item.VillageSettlementId);
                result.Add(entry);
            }
            foreach (var item in GreyWardenVillageReconstructionBehavior.GetTaskSnapshots())
            {
                var entry = Formatted("rebuild:" + item.VillageSettlementId, GreyWardenFamilyBehavior.DutyKind.Reconstruction,
                    new GwpCaseArchiveItemVM(item), item.PolicePartyId);
                entry.Place = Settlement.Find(item.VillageSettlementId);
                result.Add(entry);
            }
            foreach (var item in GreyWardenIssueResolutionBehavior.GetTaskSnapshots())
                result.Add(Formatted("issue:" + item.IssueId, GreyWardenFamilyBehavior.DutyKind.IssueResolution,
                    new GwpCaseArchiveItemVM(item), item.PolicePartyId));
            foreach (var item in GreyWardenTrainingBehavior.GetTaskSnapshots())
                result.Add(Formatted("training:" + item.TrainerPartyId, GreyWardenFamilyBehavior.DutyKind.Training,
                    new GwpCaseArchiveItemVM(item), item.TrainerPartyId));
            foreach (var item in GreyWardenTroopRequestBehavior.GetTaskSnapshots())
                result.Add(Formatted("order:" + item.TrainerPartyId, GreyWardenFamilyBehavior.DutyKind.Training,
                    new GwpCaseArchiveItemVM(item), item.TrainerPartyId, true));
            foreach (var item in GreyWardenPlayerRequestBehavior.GetTaskSnapshots())
                result.Add(Formatted("request:" + item.SettlementName, GreyWardenFamilyBehavior.DutyKind.PlayerRequests,
                    new GwpCaseArchiveItemVM(item), item.AssigneePartyId, true));
            GwpAffairsEntry? commission = bounty?.GetAffairsCommission();
            if (commission != null) result.Insert(0, commission);
            GwpAffairsEntry? support = bounty?.GetAffairsSupport();
            if (support != null) result.Add(support);
            result.AddRange(GwpWardenDispatchBehavior.Instance?.GetAffairsDispatches() ?? Enumerable.Empty<GwpAffairsEntry>());
            result.AddRange(PoliceAntiWarDeclaration.GetAffairsMediation());
            return result;
        }

        internal static List<GwpAffairsEntry> ReadFaith()
        {
            var entries = new List<GwpAffairsEntry>();
            foreach (Settlement village in GwpFireswornBehavior.FaithVillages().OrderBy(v => v.StringId))
            {
                if (!GwpFireswornBehavior.TryGetVillageFaith(village, out int homes, out int guard, out Hero? priest)) continue;
                var entry = new GwpAffairsEntry(village.StringId, GwpText.Get(GwpTextCatalog.gwp_affairs_faith), village.Name.ToString())
                { Place = village, Hero = priest, Summary = GwpText.Get(GwpTextCatalog.gwp_affairs_faith_none),
                    NotesTitle = GwpText.Get(GwpTextCatalog.gwp_affairs_stores) };
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_priest, priest?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_affairs_unknown)));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_homes,
                    GwpAffairsText.Pair(homes.ToString(), GwpFireswornBehavior.HouseholdCap(village).ToString())));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_share,
                    GwpText.Format(GwpFireswornBehavior.GetPreachedVillageShare(village) * 100f, "0") + "%"));
                entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_guard, guard));
                if (GwpFireswornBehavior.TryGetActiveRitual(village, out var kind, out double remaining, out string host))
                {
                    entry.Summary = GwpText.Get(kind == GwpFireswornBehavior.RitualKind.Congregation
                        ? GwpTextCatalog.gwp_affairs_congregation : GwpTextCatalog.gwp_affairs_damnation);
                    entry.Fields.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_ritual, entry.Summary));
                    entry.Fields.Add(GwpText.Get(GwpTextCatalog.gwp_affairs_faith_end, "HOURS", Math.Ceiling(remaining)));
                }
                GwpFireswornBehavior.GetVillageStores(village, out int prisoners, out int items, out int value);
                entry.Notes.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_dungeon, prisoners));
                entry.Notes.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_armory, items));
                entry.Notes.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_faith_goods, GwpAffairsText.Money(value)));
                entry.Notes.AddRange(GwpFireswornBehavior.AffairsVillageParties(village));
                entries.Add(entry);
            }
            return entries;
        }
    }
}
