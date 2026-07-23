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
using TaleWorlds.Core;
using TaleWorlds.Library;

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
        private static readonly Dictionary<string, InitiativeSnapshot> InitiativeByPartyId =
            new Dictionary<string, InitiativeSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ObservedPartyIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ObservedCasesByPartyId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LastPartyByHeroId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _observedPartyCacheInitialized;

        private sealed class InitiativeSnapshot
        {
            internal AiBehavior Behavior { get; set; }
            internal string TargetPartyId { get; set; } = string.Empty;
            internal float Score { get; set; }
            internal Vec2 AverageEnemyVector { get; set; }
            internal double CampaignHour { get; set; }
        }

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
                        "scope=all_grey_warden_lord_parties_and_all_leaderless_grey_warden_parties_and_active_case_targets\r\n",
                        Encoding.UTF8);
                    InitiativeByPartyId.Clear();
                    ObservedPartyIds.Clear();
                    ObservedCasesByPartyId.Clear();
                    LastPartyByHeroId.Clear();
                    _observedPartyCacheInitialized = false;
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

        internal static void WriteObservedAuction(MobileParty party,
            IReadOnlyCollection<(AIBehaviorData, float)> scores)
        {
            if (!ShouldTraceObservedParty(party)) return;
            Append(BuildPrefix(party, "OBSERVED_AUCTION") +
                " | observedForCases=" + DescribeObservedCases(party) +
                " | " + BuildPartyState(party) +
                " | rawScores=" + FormatScores(scores));
        }

        internal static void WriteObservedResolved(MobileParty party)
        {
            if (!ShouldTraceObservedParty(party)) return;
            Append(BuildPrefix(party, "OBSERVED_RESOLVED") +
                " | observedForCases=" + DescribeObservedCases(party) +
                " | " + BuildPartyState(party));
        }

        internal static void CaptureInitiative(MobileParty party,
            AiBehavior behavior, MobileParty? target, float score,
            Vec2 averageEnemyVector)
        {
            if (!ShouldTraceParty(party) && !ShouldTraceObservedParty(party)) return;
            InitiativeByPartyId[party.StringId] = new InitiativeSnapshot
            {
                Behavior = behavior,
                TargetPartyId = target?.StringId ?? string.Empty,
                Score = score,
                AverageEnemyVector = averageEnemyVector,
                CampaignHour = CampaignTime.Now.ToHours
            };
        }

        internal static void WriteAction(MobileParty party, string action, string details)
        {
            if (!ShouldTraceParty(party)) return;
            Append(BuildPrefix(party, "ACTION") + " | action=" + Safe(action) +
                " | " + Safe(details) + " | " + BuildPartyState(party));
        }

        internal static void WritePartyLifecycle(MobileParty? party, string action, string details)
        {
            if (!IsPoliceRelatedParty(party)) return;
            Append(BuildPrefix(party!, "LIFECYCLE") + " | action=" + Safe(action) +
                " | " + Safe(details) + " | " + BuildPartyState(party!));
        }

        internal static void WriteHeroLifecycle(Hero? hero, string action, string details)
        {
            if (!IsPoliceHero(hero)) return;
            string lastParty = LastPartyByHeroId.TryGetValue(hero!.StringId,
                out string? partyId) ? partyId : "-";
            Append(DateTime.Now.ToString("O", CultureInfo.InvariantCulture) +
                " | campaignHour=" + CampaignTime.Now.ToHours.ToString("0.00", CultureInfo.InvariantCulture) +
                " | HERO_LIFECYCLE" +
                " | hero=" + Safe(hero.StringId) +
                " | name=" + Safe(hero.Name?.ToString()) +
                " | action=" + Safe(action) +
                " | " + Safe(details) +
                " | currentParty=" + Safe(hero.PartyBelongedTo?.StringId) +
                " | lastObservedParty=" + Safe(lastParty) +
                " | state=" + hero.HeroState +
                "; alive=" + hero.IsAlive +
                "; age=" + hero.Age.ToString("0.00", CultureInfo.InvariantCulture) +
                "; noncombatant=" + hero.IsNoncombatant +
                "; commander=" + hero.IsCommander +
                "; traveling=" + hero.IsTraveling +
                "; fugitive=" + hero.IsFugitive +
                "; prisoner=" + hero.IsPrisoner +
                "; prisonerParty=" + Safe(DescribePartyBaseId(hero.PartyBelongedToAsPrisoner)) +
                "; currentSettlement=" + Safe(hero.CurrentSettlement?.StringId));
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

        internal static bool ShouldTraceObservedParty(MobileParty? party)
        {
            if (party?.IsActive != true || ShouldTraceParty(party)) return false;
            EnsureObservedPartyCache();
            return ObservedPartyIds.Contains(party.StringId);
        }

        internal static void RefreshObservedPartyCache()
        {
            _observedPartyCacheInitialized = false;
            EnsureObservedPartyCache();
        }

        private static void EnsureObservedPartyCache()
        {
            if (_observedPartyCacheInitialized) return;

            ObservedPartyIds.Clear();
            ObservedCasesByPartyId.Clear();
            var descriptions = new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (PoliceTask task in CrimePool.ActiveTasks.Values)
            {
                MobileParty? offender = task.TargetCrime?.Offender;
                if (offender?.IsActive != true) continue;
                MobileParty combatLeader = offender.BesiegerCamp?.LeaderParty ??
                    offender.Army?.LeaderParty ?? offender.AttachedTo ?? offender;

                AddObservedParty(offender, task, "offender", descriptions);
                if (combatLeader != offender)
                    AddObservedParty(combatLeader, task, "combatLeader", descriptions);
            }

            foreach (KeyValuePair<string, List<string>> entry in descriptions)
                ObservedCasesByPartyId[entry.Key] = Safe(string.Join(",", entry.Value));
            _observedPartyCacheInitialized = true;
        }

        private static void AddObservedParty(MobileParty party, PoliceTask task,
            string role, Dictionary<string, List<string>> descriptions)
        {
            if (party.IsActive != true || string.IsNullOrWhiteSpace(party.StringId)) return;
            ObservedPartyIds.Add(party.StringId);
            if (!descriptions.TryGetValue(party.StringId, out List<string>? cases))
            {
                cases = new List<string>();
                descriptions[party.StringId] = cases;
            }
            cases.Add(task.PolicePartyId + ":" + task.TargetCrimeId + ":" + role);
        }

        private static string BuildPrefix(MobileParty party, string stage)
        {
            double campaignHours = 0d;
            try { campaignHours = CampaignTime.Now.ToHours; } catch { }
            Hero? leader = party.LeaderHero;
            if (leader != null && !string.IsNullOrWhiteSpace(leader.StringId) &&
                !string.IsNullOrWhiteSpace(party.StringId))
                LastPartyByHeroId[leader.StringId] = party.StringId;
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
            float nonFoodCargoValue = 0f;
            float mountCargoValue = 0f;
            for (int i = 0; i < party.ItemRoster.Count; i++)
            {
                ItemRosterElement rosterElement = party.ItemRoster[i];
                ItemObject item = rosterElement.EquipmentElement.Item;
                float stackValue = rosterElement.Amount * item.Value;
                if (item.IsMountable)
                    mountCargoValue += stackValue;
                else if (!item.IsFood)
                    nonFoodCargoValue += stackValue;
            }
            float mountSellFactor = mountCargoValue > party.PartyTradeGold * 0.1f
                ? Math.Min(3f, (float)Math.Pow(
                    (mountCargoValue + 1000f) / (party.PartyTradeGold * 0.1f + 1000f), 0.33f))
                : 1f;
            float goodsSellFactor = 1f + Math.Min(3f, (float)Math.Pow(
                nonFoodCargoValue / ((party.MemberRoster.TotalManCount + 5f) * 100f), 0.33f));
            float nativeSellFactor = mountSellFactor * goodsSellFactor;
            if (party.Army != null)
                nativeSellFactor = (float)Math.Sqrt(nativeSellFactor);
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
                "; isDisbanding=" + party.IsDisbanding +
                "; leaderId=" + Safe(party.LeaderHero?.StringId) +
                "; leaderState=" + (party.LeaderHero?.HeroState.ToString() ?? "-") +
                "; leaderAge=" + (party.LeaderHero?.Age.ToString("0.00", CultureInfo.InvariantCulture) ?? "-") +
                "; leaderAlive=" + (party.LeaderHero?.IsAlive.ToString() ?? "-") +
                "; leaderNoncombatant=" + (party.LeaderHero?.IsNoncombatant.ToString() ?? "-") +
                "; leaderCommander=" + (party.LeaderHero?.IsCommander.ToString() ?? "-") +
                "; leaderTraveling=" + (party.LeaderHero?.IsTraveling.ToString() ?? "-") +
                "; leaderFugitive=" + (party.LeaderHero?.IsFugitive.ToString() ?? "-") +
                "; leaderParty=" + Safe(party.LeaderHero?.PartyBelongedTo?.StringId) +
                "; leaderPrisonerParty=" + Safe(DescribePartyBaseId(party.LeaderHero?.PartyBelongedToAsPrisoner)) +
                "; mapFaction=" + Safe(party.MapFaction?.StringId) +
                "; factionMinor=" + (party.MapFaction?.IsMinorFaction ?? false) +
                "; factionKingdom=" + (party.MapFaction?.IsKingdomFaction ?? false) +
                "; estimatedStrength=" + party.Party.EstimatedStrength.ToString("0.00", CultureInfo.InvariantCulture) +
                "; armyStrength=" + (party.Army?.EstimatedStrength ?? party.Party.EstimatedStrength)
                    .ToString("0.00", CultureInfo.InvariantCulture) +
                "; aggressiveness=" + party.Aggressiveness.ToString("0.000", CultureInfo.InvariantCulture) +
                "; attackInitiative=" + party.Ai.AttackInitiative.ToString("0.000", CultureInfo.InvariantCulture) +
                "; avoidInitiative=" + party.Ai.AvoidInitiative.ToString("0.000", CultureInfo.InvariantCulture) +
                "; alerted=" + party.Ai.IsAlerted +
                "; baseSpeed=" + party.LastCalculatedBaseSpeed.ToString("0.00", CultureInfo.InvariantCulture) +
                "; initiative=" + DescribeInitiative(party) +
                "; desiredNavigation=" + party.DesiredAiNavigationType +
                "; default=" + party.DefaultBehavior +
                "; short=" + party.ShortTermBehavior +
                "; currentSettlement=" + Safe(party.CurrentSettlement?.StringId) +
                "; currentSettlementGold=" + (party.CurrentSettlement?.SettlementComponent?.Gold ?? 0) +
                "; targetSettlement=" + Safe(party.TargetSettlement?.StringId) +
                "; targetParty=" + Safe(party.TargetParty?.StringId) +
                "; shortTarget=" + Safe(party.ShortTermTargetParty?.StringId) +
                "; targetPoint=" + party.TargetPosition.ToVec2() +
                "; position=" + party.GetPosition2D +
                "; food=" + party.Food.ToString("0.00", CultureInfo.InvariantCulture) +
                "; foodChange=" + party.FoodChange.ToString("0.00", CultureInfo.InvariantCulture) +
                "; foodDays=" + foodDays +
                "; nonFoodCargoValue=" + nonFoodCargoValue.ToString("0", CultureInfo.InvariantCulture) +
                "; mountCargoValue=" + mountCargoValue.ToString("0", CultureInfo.InvariantCulture) +
                "; nativeSellFactor=" + nativeSellFactor.ToString("0.000", CultureInfo.InvariantCulture) +
                "; gold=" + party.PartyTradeGold +
                "; leaderGold=" + (party.LeaderHero?.Gold ?? 0) +
                "; leaderHomeSettlement=" + Safe(party.LeaderHero?.HomeSettlement?.StringId) +
                "; leaderTimeAtHome=" + (party.LeaderHero?.PassedTimeAtHomeSettlement
                    .ToString("0.00", CultureInfo.InvariantCulture) ?? "-") +
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

        private static string DescribePartyBaseId(PartyBase? party) =>
            party?.MobileParty?.StringId ?? party?.Settlement?.StringId ?? string.Empty;

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

        private static string DescribeInitiative(MobileParty party)
        {
            if (!InitiativeByPartyId.TryGetValue(party.StringId,
                    out InitiativeSnapshot? snapshot))
                return "missing";
            return snapshot.Behavior + "@" + Safe(snapshot.TargetPartyId) +
                   ",score=" + snapshot.Score.ToString("0.0000", CultureInfo.InvariantCulture) +
                   ",enemyVec=" + snapshot.AverageEnemyVector +
                   ",hour=" + snapshot.CampaignHour.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string DescribeObservedCases(MobileParty party)
        {
            EnsureObservedPartyCache();
            return ObservedCasesByPartyId.TryGetValue(party.StringId,
                out string? description) ? description : "-";
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

        private static bool IsPoliceHero(Hero? hero) =>
            hero?.Clan != null && string.Equals(hero.Clan.StringId,
                PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);

        private static bool IsPoliceRelatedParty(MobileParty? party) =>
            party != null && (ShouldTraceParty(party) ||
                string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                    StringComparison.OrdinalIgnoreCase) ||
                IsPoliceHero(party.LeaderHero));

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
                : value!.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");

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
        internal static void WriteObservedAuction(MobileParty party,
            IReadOnlyCollection<(AIBehaviorData, float)> scores) { }
        internal static void WriteObservedResolved(MobileParty party) { }
        internal static void CaptureInitiative(MobileParty party,
            AiBehavior behavior, MobileParty? target, float score,
            TaleWorlds.Library.Vec2 averageEnemyVector) { }
        internal static void WriteAction(MobileParty party, string action, string details) { }
        internal static void WritePartyLifecycle(MobileParty? party, string action, string details) { }
        internal static void WriteHeroLifecycle(Hero? hero, string action, string details) { }
        internal static void WriteMapEvent(MapEvent? mapEvent, string stage) { }
        internal static bool ShouldTraceParty(MobileParty? party) => false;
        internal static bool ShouldTraceObservedParty(MobileParty? party) => false;
        internal static void RefreshObservedPartyCache() { }
    }
#endif
}
