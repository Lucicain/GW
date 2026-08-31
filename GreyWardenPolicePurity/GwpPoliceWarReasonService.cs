using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal static class GwpPoliceWarReasonService
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;

        private sealed class FactionReasonBucket
        {
            public FactionReasonBucket(IFaction faction)
            {
                Faction = faction;
            }

            public IFaction Faction { get; }
            public List<string> Details { get; } = new List<string>();
        }

        public static bool SupportsClan(Clan? clan)
        {
            return clan != null &&
                   string.Equals(clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// One faction the Grey Wardens are at war with, and one ground for
        /// that war.  A faction with several grounds yields several entries.
        /// </summary>
        internal readonly struct WarReasonEntry
        {
            internal WarReasonEntry(IFaction faction, string detail, int ordinal, int total)
            {
                Faction = faction;
                Detail = detail;
                Ordinal = ordinal;
                Total = total;
            }

            internal IFaction Faction { get; }

            internal string Detail { get; }

            /// <summary>1-based position among this faction's grounds.</summary>
            internal int Ordinal { get; }

            /// <summary>How many grounds this faction has in total.</summary>
            internal int Total { get; }
        }

        /// <summary>
        /// The standing wars and why each one is being prosecuted, flattened
        /// for the case ledger.  This used to be rendered into a separate
        /// inquiry popup behind its own clan-page button; that popup repeated
        /// the ledger's own case rows almost field for field, so the ledger now
        /// owns the presentation and this only supplies the data.
        /// </summary>
        internal static IReadOnlyList<WarReasonEntry> GetCurrentWarReasonEntries()
        {
            var entries = new List<WarReasonEntry>();

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return entries;

            foreach (FactionReasonBucket bucket in
                     CollectCurrentWarReasons(policeClan).Values
                         .OrderBy(static b => b.Faction.Name?.ToString() ?? string.Empty,
                             StringComparer.Ordinal))
            {
                List<string> details = bucket.Details
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                for (int i = 0; i < details.Count; i++)
                    entries.Add(new WarReasonEntry(bucket.Faction, details[i], i + 1, details.Count));
            }

            return entries;
        }

        public static bool HasLegitimateWarReason(IFaction? targetFaction)
        {
            if (targetFaction == null) return false;

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;
            if (!FactionManager.IsAtWarAgainstFaction(policeClan, targetFaction)) return false;

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (TaskMaintainsFactionWar(task) && TaskMatchesFaction(task, targetFaction))
                    return true;
            }

            PlayerBountyBehavior? bountyBehavior = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bountyBehavior?.HasActiveBountyWarForFaction(targetFaction) == true)
                return true;

            PolicePatrolBehavior? patrolBehavior = Campaign.Current?.GetCampaignBehavior<PolicePatrolBehavior>();
            if (patrolBehavior?.HasActivePatrolWarForFaction(targetFaction) == true)
                return true;

            return false;
        }

        public static IEnumerable<IFaction> GetCurrentPoliceWarFactions(Clan policeClan)
        {
            return GetCurrentWarFactions(policeClan);
        }

        private static Dictionary<string, FactionReasonBucket> CollectCurrentWarReasons(Clan policeClan)
        {
            var buckets = new Dictionary<string, FactionReasonBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (!TaskMaintainsFactionWar(task)) continue;

                MobileParty? offender = task.TargetCrime?.Offender;
                IFaction? targetFaction = task.WarTarget ?? offender?.ActualClan?.MapFaction;
                if (targetFaction == null) continue;
                if (!FactionManager.IsAtWarAgainstFaction(policeClan, targetFaction)) continue;

                AddFactionReason(buckets, targetFaction, BuildTaskReasonDetail(task));
            }

            PlayerBountyBehavior? bountyBehavior = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bountyBehavior != null)
            {
                foreach (IFaction faction in GetCurrentWarFactions(policeClan))
                {
                    string? detail = bountyBehavior.BuildActiveBountyWarReasonDetails(faction);
                    if (!string.IsNullOrWhiteSpace(detail))
                        AddFactionReason(buckets, faction, detail);
                }
            }

            PolicePatrolBehavior? patrolBehavior = Campaign.Current?.GetCampaignBehavior<PolicePatrolBehavior>();
            if (patrolBehavior != null)
            {
                IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction != null && FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
                {
                    string? detail = patrolBehavior.BuildPatrolWarReasonDetails(playerFaction);
                    if (!string.IsNullOrWhiteSpace(detail))
                        AddFactionReason(buckets, playerFaction, detail);
                }
            }

            foreach (IFaction faction in GetCurrentWarFactions(policeClan))
            {
                if (!buckets.ContainsKey(faction.StringId))
                {
                    AddFactionReason(
                        buckets,
                        faction,
                        GwpText.Get("{=gwp_gwppolicewarreasonservice_007}A war is currently in progress, but there is no active wartime-pursuit case or other valid enforcement reason. The next two-day review will restore peace."));
                }
            }

            return buckets;
        }

        private static bool TaskMatchesFaction(PoliceTask task, IFaction targetFaction)
        {
            if (task == null || targetFaction == null) return false;

            if (task.WarTarget != null &&
                string.Equals(task.WarTarget.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return true;

            MobileParty? offender = task.TargetCrime?.Offender;
            IFaction? offenderFaction = offender?.ActualClan?.MapFaction;
            if (offenderFaction != null &&
                string.Equals(offenderFaction.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (offender?.IsMainParty == true)
            {
                IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction != null &&
                    string.Equals(playerFaction.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// An ordinary case is allowed to preserve a faction war only after its
        /// enforcement task has actually entered wartime pursuit. Merely being
        /// present in the case pool, assigned, preparing, or tracking a target
        /// is not a legitimate reason to keep two factions at war.
        /// </summary>
        internal static bool TaskMaintainsFactionWar(PoliceTask? task)
        {
            return task != null && task.FlowState == PoliceTaskFlowState.WarPursuit;
        }

        private static IEnumerable<IFaction> GetCurrentWarFactions(Clan policeClan)
        {
            var results = new Dictionary<string, IFaction>(StringComparer.OrdinalIgnoreCase);

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                TryAddWarFaction(results, policeClan, kingdom);
            }

            foreach (Clan clan in Clan.All)
            {
                if (clan == null || clan == policeClan || clan.IsEliminated) continue;
                if (clan.Kingdom != null) continue;
                TryAddWarFaction(results, policeClan, clan);
            }

            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction != null)
                TryAddWarFaction(results, policeClan, playerFaction);

            return results.Values;
        }

        private static void TryAddWarFaction(
            IDictionary<string, IFaction> results,
            Clan policeClan,
            IFaction candidate)
        {
            if (candidate == null) return;
            if (candidate == policeClan || candidate == policeClan.MapFaction) return;
            if (candidate is Clan clanCandidate && clanCandidate.IsOutlaw && clanCandidate.IsBanditFaction) return;
            if (candidate.IsBanditFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(policeClan, candidate)) return;
            if (string.IsNullOrEmpty(candidate.StringId)) return;

            results[candidate.StringId] = candidate;
        }

        private static void AddFactionReason(
            IDictionary<string, FactionReasonBucket> buckets,
            IFaction faction,
            string detail)
        {
            if (faction == null || string.IsNullOrWhiteSpace(detail) || string.IsNullOrEmpty(faction.StringId))
                return;
            if (faction is Clan clanFaction && clanFaction.IsOutlaw && clanFaction.IsBanditFaction)
                return;
            if (faction.IsBanditFaction)
                return;

            if (!buckets.TryGetValue(faction.StringId, out FactionReasonBucket? bucket))
            {
                bucket = new FactionReasonBucket(faction);
                buckets[faction.StringId] = bucket;
            }

            bucket.Details.Add(detail.Trim());
        }

        private static string BuildTaskReasonDetail(PoliceTask task)
        {
            CrimeRecord? crime = task.TargetCrime;
            MobileParty? offender = crime?.Offender;

            string policePartyName = ResolvePartyName(task.PolicePartyId, GwpText.Get("{=gwp_gwppolicewarreasonservice_023}Unrecorded enforcement party"));
            string offenderName = offender?.Name?.ToString() ?? GwpText.Get("{=gwp_gwppolicewarreasonservice_024}Unknown Target");
            string actionType = GetActionType(task, offender);
            string crimeType = string.IsNullOrWhiteSpace(crime?.CrimeType) ? GwpText.Get("{=gwp_gwppolicewarreasonservice_025}Undocumented") : GwpText.CrimeType(crime.CrimeType);
            string stage = DescribeTaskStage(task);

            // Names the case rather than restating it.  The long form used to
            // repeat the ledger's own row field for field - offender, offence,
            // victim, location, filing time, party and stage - which is why the
            // two clan-page buttons felt like the same screen twice.  Victim,
            // location and filing time are one row away in the same list.
            return GwpText.Get(
                "{=gwp_gwppolicewarreasonservice_029}{VAR_1}: {VAR_2} is pursuing {VAR_3} over {VAR_4} — {VAR_5}",
                "VAR_1", actionType,
                "VAR_2", policePartyName,
                "VAR_3", offenderName,
                "VAR_4", crimeType,
                "VAR_5", stage);
        }

        private static string GetActionType(PoliceTask task, MobileParty? offender)
        {
            if (task.IsPlayerBountyEscort)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_030}Player bounty collaboration");

            if (offender?.IsMainParty == true)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_031}Player case enforcement");

            if (task.WarDeclared)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_032}Cross-faction pursuit");

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_033}Law enforcement contract");
        }

        private static string DescribeTaskStage(PoliceTask task)
        {
            if (task.IsPlayerBountyEscort)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_034}A Grey Warden party is escorting the player in pursuit of the quarry");

            if (task.IsEscortingPlayer)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_035}The target has been defeated and is escorting the player");

            if (task.WarDeclared)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_036}Formal war declared; pursuit continues");

            if (task.TargetCrime != null)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_037}A case has been filed for tracking, but it has not been upgraded to a formal war.");

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_038}Not recorded");
        }

        private static string ResolvePartyName(string? partyId, string fallback)
        {
            if (!string.IsNullOrEmpty(partyId))
            {
                MobileParty? party = MobileParty.All.FirstOrDefault(p =>
                    p != null &&
                    string.Equals(p.StringId, partyId, StringComparison.OrdinalIgnoreCase));
                if (party != null)
                    return party.Name?.ToString() ?? fallback;
            }

            return fallback;
        }

        private static string FormatElapsedSince(CampaignTime occurredTime)
        {
            float days = (float)(CampaignTime.Now - occurredTime).ToDays;
            if (days < (1f / CampaignTime.HoursInDay))
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_039}Just now");

            if (days < 1f)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_040}{VAR_1} hours ago", "VAR_1", GwpText.Format(days * CampaignTime.HoursInDay, "0.#"));

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_041}{VAR_1} days ago", "VAR_1", GwpText.Format(days, "0.##"));
        }

        private static string FormatLocation(Vec2 position)
        {
            Settlement? nearestTown = GwpCommon.FindNearestTown(position);
            if (nearestTown != null)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_042}Near {VAR_1} ({VAR_2}, {VAR_3})", "VAR_1", nearestTown.Name, "VAR_2", GwpText.Format(position.x, "0.0"), "VAR_3", GwpText.Format(position.y, "0.0"));

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_043}wild ({VAR_1}, {VAR_2})", "VAR_1", GwpText.Format(position.x, "0.0"), "VAR_2", GwpText.Format(position.y, "0.0"));
        }

        private static string FormatRemainingDuration(double hours)
        {
            double clampedHours = Math.Max(0d, hours);
            int days = (int)(clampedHours / CampaignTime.HoursInDay);
            double hoursRemainder = clampedHours - days * CampaignTime.HoursInDay;

            if (days <= 0)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_044}{VAR_1} hours", "VAR_1", GwpText.Format(hoursRemainder, "0.#"));

            if (hoursRemainder < 0.05d)
                return GwpText.Get("{=gwp_gwppolicewarreasonservice_045}{VAR_1} days", "VAR_1", days);

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_046}{VAR_1} days {VAR_2} hours", "VAR_1", days, "VAR_2", GwpText.Format(hoursRemainder, "0.#"));
        }

        private static string FormatCampaignDate(double hours)
        {
            CampaignTime time = CampaignTime.Hours((float)hours);
            string season = time.GetSeasonOfYear switch
            {
                CampaignTime.Seasons.Spring => GwpText.Get("{=gwp_gwppolicewarreasonservice_047}spring"),
                CampaignTime.Seasons.Summer => GwpText.Get("{=gwp_gwppolicewarreasonservice_048}Summer"),
                CampaignTime.Seasons.Autumn => GwpText.Get("{=gwp_gwppolicewarreasonservice_049}Autumn"),
                CampaignTime.Seasons.Winter => GwpText.Get("{=gwp_gwppolicewarreasonservice_050}Winter"),
                _ => GwpText.Get("{=gwp_gwppolicewarreasonservice_051}Unknown season")
            };

            return GwpText.Get("{=gwp_gwppolicewarreasonservice_052}Year {VAR_1}, {VAR_2}, day {VAR_3}, {VAR_4}:00", "VAR_1", time.GetYear, "VAR_2", season, "VAR_3", time.GetDayOfSeason + 1, "VAR_4", time.GetHourOfDay);
        }
    }
}
