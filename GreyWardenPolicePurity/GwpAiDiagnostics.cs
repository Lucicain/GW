using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
#if GWP_DIAGNOSTICS
    /// <summary>
    /// 开发期灰袍 AI、任务、军团与经济观测。正式发行构建会编译为下方空实现。
    /// </summary>
    internal static class GwpAiDiagnostics
    {
        private static readonly object Sync = new object();
        private static bool _sessionStarted;

        internal static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "GreyWarden-AI-Diagnostics.log");

        internal static void StartSession()
        {
            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    File.WriteAllText(LogPath,
                        $"# GreyWarden AI diagnostics | session={DateTime.Now:O} | " +
                        $"assembly={typeof(GwpAiDiagnostics).Assembly.GetName().Version} | " +
                        "scope=all_grey_warden_lord_parties_and_all_leaderless_grey_warden_parties\r\n",
                        Encoding.UTF8);
                    _sessionStarted = true;
                }
                catch
                {
                    _sessionStarted = false;
                }
            }
        }

        internal static void WriteState(MobileParty party, string stage)
        {
            if (!ShouldTraceParty(party)) return;
            Append(BuildPrefix(party, stage) + " | " + BuildPartyState(party));
        }

        internal static void WriteAuction(MobileParty party,
            IReadOnlyCollection<(AIBehaviorData, float)> rawScores,
            IReadOnlyCollection<(AIBehaviorData, float)> finalScores,
            string intent,
            float originalPatrolCeiling,
            float suppressedPatrolCeiling,
            float dutyScore,
            int suppressedPatrolCount,
            float minimumPositiveNonPatrolScore,
            int nonPatrolAtOrBelowDutyCount,
            string dutyAdded)
        {
            if (!ShouldTraceParty(party)) return;
            string line = BuildPrefix(party, "AUCTION") +
                " | intent=" + Safe(intent) +
                " | nativeNonPatrolScoresPreserved=True" +
                " | originalPatrolCeiling=" + originalPatrolCeiling.ToString("R", CultureInfo.InvariantCulture) +
                " | suppressedPatrolCeiling=" + suppressedPatrolCeiling.ToString("R", CultureInfo.InvariantCulture) +
                " | suppressedPatrolCount=" + suppressedPatrolCount +
                " | dutyScore=" + dutyScore.ToString("R", CultureInfo.InvariantCulture) +
                " | minimumPositiveNonPatrolScore=" + minimumPositiveNonPatrolScore.ToString("R", CultureInfo.InvariantCulture) +
                " | nonPatrolAtOrBelowDutyCount=" + nonPatrolAtOrBelowDutyCount +
                " | dutyAdded=" + Safe(dutyAdded) +
                " | " + BuildPartyState(party) +
                " | rawScores=" + FormatScores(rawScores) +
                " | finalScores=" + FormatScores(finalScores);
            Append(line);
        }

        internal static void WriteResolved(MobileParty party) =>
            WriteState(party, "RESOLVED");

        internal static void WriteAction(MobileParty party, string action, string details)
        {
            if (!ShouldTraceParty(party)) return;
            Append(BuildPrefix(party, "ACTION") + " | action=" + Safe(action) +
                " | " + Safe(details) + " | " + BuildPartyState(party));
        }

        internal static void WriteMapEvent(MapEvent? mapEvent, string stage)
        {
            if (mapEvent == null) return;
            string involved = string.Join(",", mapEvent.InvolvedParties
                .Select(entry => entry?.MobileParty?.StringId ?? entry?.Settlement?.StringId)
                .Where(id => !string.IsNullOrWhiteSpace(id)));
            string details = "eventStage=" + stage +
                "; eventType=" + mapEvent.EventType +
                "; eventFinalized=" + mapEvent.IsFinalized +
                "; battleState=" + mapEvent.BattleState +
                "; settlement=" + Safe(mapEvent.MapEventSettlement?.StringId) +
                "; attackerLeader=" + Safe(mapEvent.AttackerSide?.LeaderParty?.MobileParty?.StringId) +
                "; defenderLeader=" + Safe(mapEvent.DefenderSide?.LeaderParty?.MobileParty?.StringId) +
                "; involved=" + Safe(involved);

            var parties = new HashSet<MobileParty>();
            foreach (PartyBase? entry in mapEvent.InvolvedParties)
                if (ShouldTraceParty(entry?.MobileParty))
                    parties.Add(entry!.MobileParty);
            foreach (PoliceTask task in CrimePool.ActiveTasks.Values)
            {
                MobileParty? police = MobileParty.All.FirstOrDefault(candidate =>
                    string.Equals(candidate.StringId, task.PolicePartyId,
                        StringComparison.OrdinalIgnoreCase));
                MobileParty? offender = task.TargetCrime?.Offender;
                if (ShouldTraceParty(police) && offender != null &&
                    mapEvent.InvolvedParties.Any(entry => entry?.MobileParty == offender))
                    parties.Add(police!);
            }

            foreach (MobileParty party in parties)
                WriteAction(party, "MAP_EVENT_" + stage, details);
        }

        internal static bool ShouldTraceParty(MobileParty? party)
        {
            if (party?.IsActive != true) return false;
            if (IsGreyWardenLordParty(party)) return true;
            if (party.LeaderHero != null) return false;
            return GwpCommon.IsPatrolParty(party) ||
                   GwpCommon.IsEnforcementDelayPatrolParty(party) ||
                   string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(party.MapFaction?.StringId, PoliceStats.PoliceClanId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPrefix(MobileParty party, string stage)
        {
            double campaignHours = 0d;
            try { campaignHours = CampaignTime.Now.ToHours; } catch { }
            return DateTime.Now.ToString("O", CultureInfo.InvariantCulture) +
                " | campaignHour=" + campaignHours.ToString("0.00", CultureInfo.InvariantCulture) +
                " | " + stage +
                " | partyKind=" + GetPartyKind(party) +
                " | party=" + Safe(party.StringId) +
                " | name=" + Safe(party.Name?.ToString()) +
                " | leader=" + Safe(party.LeaderHero?.Name?.ToString());
        }

        private static string BuildPartyState(MobileParty party)
        {
            float consumption = Math.Max(0f, -party.FoodChange);
            string foodDays = consumption > 0f
                ? (Math.Max(0f, party.Food) / consumption).ToString("0.00", CultureInfo.InvariantCulture)
                : "inf";
            PoliceTask? task = CrimePool.GetTask(party.StringId);
            MobileParty? offender = task?.TargetCrime?.Offender ??
                GreyWardenPartyDesireBehavior.GetDiagnosticTargetParty(party);
            string distance = offender?.IsActive == true
                ? party.GetPosition2D.Distance(offender.GetPosition2D)
                    .ToString("0.00", CultureInfo.InvariantCulture)
                : "n/a";

            return "aiDisabled=" + party.Ai.IsDisabled +
                "; doNotDecide=" + party.Ai.DoNotMakeNewDecisions +
                "; rethink=" + party.Ai.RethinkAtNextHourlyTick +
                "; dutyIntent=" + Safe(GreyWardenPartyDesireBehavior.GetDiagnosticIntent(party)) +
                "; directAttackLock=" + GreyWardenPartyDesireBehavior.HasDirectAttackLock(party) +
                "; assistance=" + Safe(PoliceEnforcementBehavior.GetAssistanceDiagnostic(party)) +
                "; ordinaryCaseEligible=" + PoliceStats.CanHandleOrdinaryCase(party) +
                "; armyLeader=" + Safe(party.Army?.LeaderParty?.StringId) +
                "; armyMemberCount=" + (party.Army?.Parties.Count ?? 0) +
                "; attachedTo=" + Safe(party.AttachedTo?.StringId) +
                "; armyKingdom=" + Safe(party.Army?.Kingdom?.StringId) +
                "; mapEvent=" + DescribeMapEvent(party) +
                "; siege=" + DescribeSiege(party) +
                "; isLordParty=" + party.IsLordParty +
                "; isPatrolParty=" + party.IsPatrolParty +
                "; mapFaction=" + Safe(party.MapFaction?.StringId) +
                "; factionMinor=" + (party.MapFaction?.IsMinorFaction ?? false) +
                "; factionKingdom=" + (party.MapFaction?.IsKingdomFaction ?? false) +
                "; default=" + party.DefaultBehavior +
                "; short=" + party.ShortTermBehavior +
                "; currentSettlement=" + Safe(party.CurrentSettlement?.StringId) +
                "; targetSettlement=" + Safe(party.TargetSettlement?.StringId) +
                "; targetParty=" + Safe(party.TargetParty?.StringId) +
                "; shortTarget=" + Safe(party.ShortTermTargetParty?.StringId) +
                "; targetPoint=" + party.TargetPosition.ToVec2() +
                "; position=" + party.GetPosition2D +
                "; food=" + party.Food.ToString("0.00", CultureInfo.InvariantCulture) +
                "; foodChange=" + party.FoodChange.ToString("0.00", CultureInfo.InvariantCulture) +
                "; foodDays=" + foodDays +
                "; gold=" + party.PartyTradeGold +
                "; leaderGold=" + (party.LeaderHero?.Gold ?? 0) +
                "; clanGold=" + (party.ActualClan?.Gold ?? 0) +
                "; dailyWage=" + party.TotalWage +
                "; wageLimit=" + party.PaymentLimit +
                "; unpaidWages=" + party.HasUnpaidWages.ToString("0.000", CultureInfo.InvariantCulture) +
                "; men=" + party.MemberRoster.TotalManCount +
                "; wounded=" + party.MemberRoster.TotalWounded +
                "; sizeRatio=" + party.PartySizeRatio.ToString("0.000", CultureInfo.InvariantCulture) +
                "; prisoners=" + party.PrisonRoster.TotalManCount +
                "; task=" + Safe(task?.TargetCrimeId) +
                "; taskFlow=" + (task?.FlowState.ToString() ?? "-") +
                "; war=" + (task?.WarDeclared ?? false) +
                "; offender=" + Safe(offender?.StringId) +
                "; offenderState=" + DescribeOffender(party, offender) +
                "; offenderDistance=" + distance;
        }

        private static string DescribeMapEvent(MobileParty? party)
        {
            MapEvent? mapEvent = party?.MapEvent;
            if (mapEvent == null) return "-";
            string side = party!.MapEventSide == mapEvent.AttackerSide ? "attacker" :
                party.MapEventSide == mapEvent.DefenderSide ? "defender" : "unknown";
            return mapEvent.EventType + ":" + side +
                   ",finalized=" + mapEvent.IsFinalized +
                   ",state=" + mapEvent.BattleState +
                   ",settlement=" + (mapEvent.MapEventSettlement?.StringId ?? "-") +
                   ",attacker=" + (mapEvent.AttackerSide?.LeaderParty?.MobileParty?.StringId ?? "-") +
                   ",defender=" + (mapEvent.DefenderSide?.LeaderParty?.MobileParty?.StringId ?? "-");
        }

        private static string DescribeSiege(MobileParty? party)
        {
            if (party?.SiegeEvent == null) return "-";
            return "settlement=" + (party.BesiegedSettlement?.StringId ?? "-") +
                   ",campLeader=" + (party.BesiegerCamp?.LeaderParty?.StringId ?? "-") +
                   ",isCampLeader=" + (party.BesiegerCamp?.LeaderParty == party) +
                   ",blockade=" + party.SiegeEvent.IsBlockadeActive;
        }

        private static string DescribeOffender(MobileParty observer, MobileParty? offender)
        {
            if (offender == null) return "missing";
            MobileParty? combatLeader = offender.BesiegerCamp?.LeaderParty ??
                offender.Army?.LeaderParty ?? offender.AttachedTo ?? offender;
            Clan? policeClan = PoliceStats.GetPoliceClan();
            bool atWar = policeClan != null && offender.MapFaction != null &&
                FactionManager.IsAtWarAgainstFaction(policeClan, offender.MapFaction);
            return "active=" + offender.IsActive +
                   ",healthy=" + (offender.Party?.NumberOfHealthyMembers ?? -1) +
                   ",faction=" + (offender.MapFaction?.StringId ?? "-") +
                   ",atWar=" + atWar +
                   ",armyLeader=" + (offender.Army?.LeaderParty?.StringId ?? "-") +
                   ",attachedTo=" + (offender.AttachedTo?.StringId ?? "-") +
                   ",combatLeader=" + (combatLeader?.StringId ?? "-") +
                   ",mapEvent=" + DescribeMapEvent(offender) +
                   ",siege=" + DescribeSiege(offender) +
                   ",observerMapEventSame=" +
                       (observer.MapEvent != null && observer.MapEvent == offender.MapEvent);
        }

        private static string GetPartyKind(MobileParty party)
        {
            if (IsGreyWardenLordParty(party)) return "grey_warden_lord";
            if (GwpCommon.IsEnforcementDelayPatrolParty(party)) return "leaderless_delay_support";
            if (GwpCommon.IsPatrolParty(party)) return "leaderless_picket";
            if (party.LeaderHero == null) return "leaderless_grey_warden";
            return "other";
        }

        private static bool IsGreyWardenLordParty(MobileParty party) =>
            party.LeaderHero != null && party.IsLordParty &&
            string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);

        private static string FormatScores(IEnumerable<(AIBehaviorData, float)> scores) =>
            "[" + string.Join(", ", scores
                .OrderByDescending(static entry => entry.Item2)
                .Select(entry => DescribeBehavior(entry.Item1) + "=" +
                    entry.Item2.ToString("0.0000", CultureInfo.InvariantCulture))) + "]";

        private static string DescribeBehavior(AIBehaviorData data)
        {
            string target = data.Party switch
            {
                MobileParty mobile => mobile.StringId,
                Settlement settlement => settlement.StringId,
                _ => data.Position.ToVec2().ToString()
            };
            return data.AiBehavior + "@" + Safe(target) + "/" + data.NavigationType;
        }

        private static string Safe(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

        private static void Append(string line)
        {
            lock (Sync)
            {
                try
                {
                    if (!_sessionStarted) StartSession();
                    File.AppendAllText(LogPath, line + "\r\n", Encoding.UTF8);
                }
                catch { }
            }
        }
    }
#else
    /// <summary>
    /// 玩家发行构建的空实现：保留调用接口，但不创建或写入任何诊断日志。
    /// </summary>
    internal static class GwpAiDiagnostics
    {
        internal static string LogPath => string.Empty;
        internal static void StartSession() { }
        internal static void WriteState(MobileParty party, string stage) { }
        internal static void WriteAuction(MobileParty party,
            IReadOnlyCollection<(AIBehaviorData, float)> rawScores,
            IReadOnlyCollection<(AIBehaviorData, float)> finalScores,
            string intent,
            float originalPatrolCeiling,
            float suppressedPatrolCeiling,
            float dutyScore,
            int suppressedPatrolCount,
            float minimumPositiveNonPatrolScore,
            int nonPatrolAtOrBelowDutyCount,
            string dutyAdded) { }
        internal static void WriteResolved(MobileParty party) { }
        internal static void WriteAction(MobileParty party, string action, string details) { }
        internal static void WriteMapEvent(MapEvent? mapEvent, string stage) { }
        internal static bool ShouldTraceParty(MobileParty? party) => false;
    }
#endif
}
