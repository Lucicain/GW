using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        internal sealed class AssistanceTaskSnapshot
        {
            public string LeaderPartyId { get; set; } = string.Empty;
            public string HelperPartyId { get; set; } = string.Empty;
            public string CrimeId { get; set; } = string.Empty;
            public string TargetPartyId { get; set; } = string.Empty;
            public CampaignTime AssignedTime { get; set; }
        }

        private sealed class LordAssistanceGroup
        {
            public string LeaderPartyId { get; set; } = string.Empty;
            public string CrimeId { get; set; } = string.Empty;
            public string TargetPartyId { get; set; } = string.Empty;
            public List<string> MemberPartyIds { get; set; } = new List<string>();
            public bool DispersedForSpeed { get; set; }
            public float LastArmySpeedAtDispersal { get; set; }
            public List<string> SpeedDetachedPartyIds { get; set; } =
                new List<string>();
            public string SpeedCatcherPartyId { get; set; } = string.Empty;
        }

        private sealed class AssistanceThreatSnapshot
        {
            public float Strength { get; set; }
            public float MaximumSpeed { get; set; }
            public string FastestCombatGroupId { get; set; } = string.Empty;
            public float JoiningRadius { get; set; }
            public float ThreatRadius { get; set; }
            public List<string> CombatGroups { get; } = new List<string>();
        }

        private sealed class LocalStrengthDeclarationSnapshot
        {
            public MobileParty Actor { get; set; } = null!;
            public MobileParty MovementTarget { get; set; } = null!;
            public float FriendlyLocalStrength { get; set; }
            public float EnemyLocalStrength { get; set; }
            public string FriendlyCombatGroups { get; set; } = string.Empty;
            public float Distance { get; set; }
            public bool StrengthReady { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private readonly Dictionary<string, LordAssistanceGroup> _assistanceGroups =
            new Dictionary<string, LordAssistanceGroup>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _assistanceAssignedHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private static bool IsGreyWardenLordParty(MobileParty? party)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            return party?.IsActive == true && party.IsLordParty &&
                   party.LeaderHero?.IsActive == true && policeClan != null &&
                   party.ActualClan == policeClan;
        }

        private static bool CanContinueLeadingPoliceTask(MobileParty? party) =>
            IsGreyWardenLordParty(party) &&
            party!.LeaderHero?.IsPrisoner != true &&
            party.LeaderHero?.IsFugitive != true;

        private void SyncAssistanceData(IDataStore dataStore)
        {
            List<string> leaders = null!;
            List<string> crimes = null!;
            List<string> targets = null!;
            List<string> members = null!;
            List<string> memberTimes = null!;
            List<int> dispersedForSpeed = null!;
            List<float> dispersedArmySpeeds = null!;
            List<string> speedDetachedParties = null!;

            if (dataStore.IsSaving)
            {
                List<LordAssistanceGroup> groups = _assistanceGroups.Values.ToList();
                leaders = groups.Select(group => group.LeaderPartyId).ToList();
                crimes = groups.Select(group => group.CrimeId).ToList();
                targets = groups.Select(group => group.TargetPartyId).ToList();
                members = groups.Select(group => string.Join(";", group.MemberPartyIds)).ToList();
                memberTimes = groups.Select(group => string.Join(";", group.MemberPartyIds.Select(
                    memberId => _assistanceAssignedHours.TryGetValue(memberId, out double hours)
                        ? hours.ToString("R", CultureInfo.InvariantCulture)
                        : "0"))).ToList();
                dispersedForSpeed = groups.Select(group =>
                    group.DispersedForSpeed ? 1 : 0).ToList();
                dispersedArmySpeeds = groups.Select(group =>
                    group.LastArmySpeedAtDispersal).ToList();
                speedDetachedParties = groups.Select(group =>
                    string.Join(";", group.SpeedDetachedPartyIds)).ToList();
            }

            dataStore.SyncData("gwp_enf_assist_leaders", ref leaders);
            dataStore.SyncData("gwp_enf_assist_crimes", ref crimes);
            dataStore.SyncData("gwp_enf_assist_targets", ref targets);
            dataStore.SyncData("gwp_enf_assist_members", ref members);
            dataStore.SyncData("gwp_enf_assist_member_times", ref memberTimes);
            dataStore.SyncData("gwp_enf_assist_speed_dispersed",
                ref dispersedForSpeed);
            dataStore.SyncData("gwp_enf_assist_speed_thresholds",
                ref dispersedArmySpeeds);
            dataStore.SyncData("gwp_enf_assist_speed_detached",
                ref speedDetachedParties);

            if (!dataStore.IsLoading) return;

            _assistanceGroups.Clear();
            _assistanceAssignedHours.Clear();
            if (leaders == null) return;
            List<string> loadedCrimes = crimes ?? new List<string>();
            List<string> loadedTargets = targets ?? new List<string>();
            List<string> loadedMembers = members ?? new List<string>();
            List<string> loadedMemberTimes = memberTimes ?? new List<string>();
            List<int> loadedSpeedDispersed =
                dispersedForSpeed ?? new List<int>();
            List<float> loadedDispersedArmySpeeds =
                dispersedArmySpeeds ?? new List<float>();
            List<string> loadedSpeedDetachedParties =
                speedDetachedParties ?? new List<string>();

            for (int index = 0; index < leaders.Count; index++)
            {
                string leader = leaders[index] ?? string.Empty;
                if (leader.Length == 0) continue;
                string memberText = index < loadedMembers.Count
                    ? loadedMembers[index] ?? string.Empty
                    : string.Empty;
                string detachedText =
                    index < loadedSpeedDetachedParties.Count
                        ? loadedSpeedDetachedParties[index] ?? string.Empty
                        : string.Empty;
                var loadedGroup = new LordAssistanceGroup
                {
                    LeaderPartyId = leader,
                    CrimeId = index < loadedCrimes.Count ? loadedCrimes[index] ?? string.Empty : string.Empty,
                    TargetPartyId = index < loadedTargets.Count ? loadedTargets[index] ?? string.Empty : string.Empty,
                    MemberPartyIds = memberText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    DispersedForSpeed =
                        index < loadedSpeedDispersed.Count &&
                        loadedSpeedDispersed[index] != 0,
                    LastArmySpeedAtDispersal =
                        index < loadedDispersedArmySpeeds.Count
                            ? Math.Max(0f, loadedDispersedArmySpeeds[index])
                            : 0f,
                    SpeedDetachedPartyIds = detachedText.Split(
                            new[] { ';' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };
                if (loadedGroup.DispersedForSpeed &&
                    loadedGroup.SpeedDetachedPartyIds.Count == 0)
                {
                    // The previous implementation fully dissolved every
                    // speed-dispersed army. Preserve that exact live state when
                    // loading a save made before per-lord detachment existed.
                    loadedGroup.SpeedDetachedPartyIds.Add(leader);
                    loadedGroup.SpeedDetachedPartyIds.AddRange(
                        loadedGroup.MemberPartyIds);
                }
                else if (!loadedGroup.DispersedForSpeed)
                    loadedGroup.SpeedDetachedPartyIds.Clear();
                _assistanceGroups[leader] = loadedGroup;
                string timeText = index < loadedMemberTimes.Count
                    ? loadedMemberTimes[index] ?? string.Empty
                    : string.Empty;
                string[] times = timeText.Split(new[] { ';' }, StringSplitOptions.None);
                for (int memberIndex = 0;
                     memberIndex < _assistanceGroups[leader].MemberPartyIds.Count;
                     memberIndex++)
                {
                    string memberId = _assistanceGroups[leader].MemberPartyIds[memberIndex];
                    _assistanceAssignedHours[memberId] = memberIndex < times.Length &&
                        double.TryParse(times[memberIndex], NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double hours)
                            ? hours
                            : CampaignTime.Now.ToHours;
                }
            }
            CrimePool.TrimOpenCasesToCapacity(
                CrimePool.MaxTaskPoolEntries);
        }

        private void UpdateLordAssistance()
        {
            CleanupInvalidAssistanceGroups();

            foreach (MobileParty leader in PoliceStats.GetAllPoliceParties()
                         .Where(IsGreyWardenLordParty)
                         .OrderBy(party => party.StringId, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (IsAssistanceMember(leader.StringId))
                    continue;

                PoliceTask? task = CrimeState.GetTask(leader.StringId);
                if (task != null)
                    TryCaptureAssistanceLeaderSoloSpeed(leader, task);
                if (_assistanceGroups.TryGetValue(leader.StringId,
                        out LordAssistanceGroup? group))
                {
                    if (!IsAssistanceGroupCaseStillActive(group, task))
                    {
                        ReleaseAssistanceGroup(leader.StringId, "leader_case_ended");
                        continue;
                    }

                    MobileParty? activeTarget =
                        GetActiveAssistanceCaseTarget(group, task);
                    if (activeTarget == null)
                        continue;
                    group.TargetPartyId = activeTarget.StringId;

                    AssistanceThreatSnapshot targetThreat =
                        GetNativeCombatStrengthSnapshot(leader, activeTarget);
                    MobileParty movementTarget =
                        ResolveAssistanceMovementTarget(activeTarget);
                    float targetMovementSpeed =
                        GetTheoreticalBaseSpeed(movementTarget);
                    float targetStrength = targetThreat.Strength;
                    float committedStrength =
                        GetCommittedAssistanceStrength(leader, group);
                    if (group.DispersedForSpeed)
                    {
                        Army? remainingArmy =
                            MaintainSpeedDispersedPursuit(
                                leader, group, activeTarget);
                        if (!EnsureCommittedStrengthAdvantage(leader, task!,
                                activeTarget, group, remainingArmy,
                                targetStrength))
                        {
                            FailAssistanceCase(leader, task!, group,
                                targetStrength,
                                "no_remaining_eligible_force_while_dispersed");
                            continue;
                        }

                        committedStrength =
                            GetCommittedAssistanceStrength(leader, group);
                        if (!CanReformAssistanceArmyAfterSpeedDispersal(
                                task!, targetMovementSpeed))
                            continue;

                        group.DispersedForSpeed = false;
                        group.SpeedDetachedPartyIds.Clear();
                        group.SpeedCatcherPartyId = string.Empty;
                        GwpAiDiagnostics.WriteAction(leader,
                            "ASSISTANCE_ARMY_SPEED_REFORMING",
                            FormatStrengthDiagnostic(leader, activeTarget,
                                committedStrength, targetStrength) +
                            "; speedTarget=" + movementTarget.StringId +
                            "; offenderSpeed=" +
                            activeTarget.LastCalculatedBaseSpeed.ToString(
                                "0.00", CultureInfo.InvariantCulture) +
                            "; speedTargetCachedBaseSpeed=" +
                            movementTarget.LastCalculatedBaseSpeed.ToString(
                                "0.00", CultureInfo.InvariantCulture) +
                            "; speedTargetTheoreticalSpeed=" +
                            targetMovementSpeed.ToString(
                                "0.00", CultureInfo.InvariantCulture) +
                            "; theoreticalLeaderSoloSpeedAtAssignment=" +
                            task!.LeaderSoloSpeedAtAssignment.ToString(
                                "0.00", CultureInfo.InvariantCulture));
                        GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                            leader);
                    }

                    Army? army = MaintainAssistanceArmy(leader, group);
                    if (army == null)
                        continue;

                    if (!EnsureCommittedStrengthAdvantage(leader, task!,
                            activeTarget, group, army, targetStrength))
                    {
                        FailAssistanceCase(leader, task!, group, targetStrength,
                            "no_remaining_eligible_force");
                        continue;
                    }

                    targetThreat =
                        GetNativeCombatStrengthSnapshot(leader, activeTarget);
                    targetStrength = targetThreat.Strength;
                    movementTarget =
                        ResolveAssistanceMovementTarget(activeTarget);
                    targetMovementSpeed =
                        GetTheoreticalBaseSpeed(movementTarget);
                    if (ShouldDisperseAssistanceArmyForSpeed(
                            army, task!, targetMovementSpeed))
                    {
                        DisperseAssistanceArmyForSpeed(leader, group,
                            activeTarget, army, task!,
                            GetCommittedAssistanceStrength(leader, group),
                            targetStrength, targetThreat);
                        continue;
                    }

                    if (!AreAllMembersAssembled(leader, group, army))
                        continue;

                    float assembledStrength = army.EstimatedStrength;
                    if (assembledStrength <= targetStrength)
                    {
                        if (!EnsureCommittedStrengthAdvantage(leader, task!,
                                activeTarget, group, army, targetStrength))
                            FailAssistanceCase(leader, task!, group,
                                targetStrength,
                                "assembled_army_still_insufficient");
                        continue;
                    }

                    continue;
                }

                MobileParty? offender =
                    GetAssistanceEvaluationTarget(leader, task);
                if (offender == null)
                    continue;
                if (!TryCaptureAssistanceLeaderSoloSpeed(leader, task!))
                    continue;

                float leaderStrength = GetNativePartyStrength(leader);
                float initialTargetStrength =
                    GetNativeCombatStrengthSnapshot(leader, offender).Strength;
                if (leaderStrength > initialTargetStrength)
                    continue;

                var newGroup = new LordAssistanceGroup
                {
                    LeaderPartyId = leader.StringId,
                    CrimeId = task!.TargetCrimeId,
                    TargetPartyId = offender.StringId
                };
                _assistanceGroups[leader.StringId] = newGroup;
                Army? newArmy = CreateOrRecoverAssistanceArmy(leader);
                if (newArmy == null)
                {
                    _assistanceGroups.Remove(leader.StringId);
                    continue;
                }

                if (!EnsureCommittedStrengthAdvantage(leader, task, offender,
                        newGroup, newArmy, initialTargetStrength))
                {
                    FailAssistanceCase(leader, task, newGroup,
                        initialTargetStrength,
                        "no_remaining_eligible_force_at_assignment");
                    continue;
                }

                GwpAiDiagnostics.WriteAction(leader,
                    "ASSISTANCE_ARMY_STRENGTH_REQUIRED",
                    FormatStrengthDiagnostic(leader, offender, leaderStrength,
                        initialTargetStrength) +
                    "; committedStrength=" +
                    GetCommittedAssistanceStrength(leader, newGroup).ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; memberCount=" + newGroup.MemberPartyIds.Count);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);

                if (newGroup.MemberPartyIds.Count == 0 &&
                    newArmy.LeaderParty == leader && newArmy.Parties.Count <= 1)
                {
                    _assistanceGroups.Remove(leader.StringId);
                    GwpAssistanceArmyDisbandGuardPatch
                        .ApplyAuthorizedObjectiveFinished(newArmy);
                }
            }
        }

        private static MobileParty? GetAssistanceEvaluationTarget(
            MobileParty leader, PoliceTask? task)
        {
            if (task == null || task.PolicePartyId != leader.StringId ||
                task.IsEscortingPlayer || task.IsPlayerBountyEscort ||
                task.TargetCrime?.HasOpenCase != true)
                return null;

            MobileParty? offender = task.TargetCrime.Offender;
            if (offender?.IsActive != true || offender.Party == null ||
                offender.Party.NumberOfHealthyMembers <= 0)
                return null;

            // Player enforcement must still reach its dialogue decision before
            // a refused arrest can request armed assistance.
            if (offender.IsMainParty && !task.WarDeclared)
                return null;

            return offender;
        }

        private static MobileParty? GetActiveAssistanceCaseTarget(
            LordAssistanceGroup group, PoliceTask? task)
        {
            if (!IsAssistanceGroupCaseStillActive(group, task) ||
                task?.TargetCrime?.HasOpenCase != true)
                return null;

            MobileParty? offender = task.TargetCrime.Offender;
            return offender?.IsActive == true && offender.Party != null &&
                   offender.Party.NumberOfHealthyMembers > 0
                ? offender
                : null;
        }

        private static bool IsAssistanceGroupCaseStillActive(
            LordAssistanceGroup group, PoliceTask? task) =>
            task != null &&
            string.Equals(task.PolicePartyId, group.LeaderPartyId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(task.TargetCrimeId, group.CrimeId,
                StringComparison.OrdinalIgnoreCase);

        private string DescribeAssistanceValidity(MobileParty? leader, PoliceTask? task)
        {
            MobileParty? offender = task?.TargetCrime?.Offender;
            Clan? policeClan = PoliceStats.GetPoliceClan();
            bool atWar = policeClan != null && offender?.MapFaction != null &&
                FactionManager.IsAtWarAgainstFaction(policeClan, offender.MapFaction);
            bool crimeMatches = leader != null &&
                (!_assistanceGroups.TryGetValue(leader.StringId, out LordAssistanceGroup? group) ||
                 string.Equals(group.CrimeId, task?.TargetCrimeId,
                     StringComparison.OrdinalIgnoreCase));

            return "leaderValid=" + IsGreyWardenLordParty(leader) +
                   "; taskExists=" + (task != null) +
                   "; taskOwnerMatches=" + (leader != null && task?.PolicePartyId == leader.StringId) +
                   "; warDeclared=" + (task?.WarDeclared ?? false) +
                   "; flow=" + (task?.FlowState.ToString() ?? "-") +
                   "; escortingPlayer=" + (task?.IsEscortingPlayer ?? false) +
                   "; bountyEscort=" + (task?.IsPlayerBountyEscort ?? false) +
                   "; caseOpen=" + (task?.TargetCrime?.HasOpenCase ?? false) +
                   "; playerTarget=" + (offender?.IsMainParty ?? false) +
                   "; offenderExists=" + (offender != null) +
                   "; offenderActive=" + (offender?.IsActive ?? false) +
                   "; offenderHasParty=" + (offender?.Party != null) +
                   "; offenderHealthy=" + (offender?.Party?.NumberOfHealthyMembers ?? -1) +
                   "; offenderFaction=" + (offender?.MapFaction?.StringId ?? "-") +
                   "; atWar=" + atWar +
                   "; crimeMatches=" + crimeMatches +
                   "; offenderArmyLeader=" + (offender?.Army?.LeaderParty?.StringId ?? "-") +
                   "; offenderAttachedTo=" + (offender?.AttachedTo?.StringId ?? "-") +
                   "; offenderSiegeLeader=" + (offender?.BesiegerCamp?.LeaderParty?.StringId ?? "-") +
                   "; offenderSiegeSettlement=" + (offender?.BesiegedSettlement?.StringId ?? "-") +
                   "; offenderMapEvent=" + (offender?.MapEvent?.EventType.ToString() ?? "-") +
                   "; offenderMapEventFinalized=" + (offender?.MapEvent?.IsFinalized ?? false);
        }

        private static float GetNativePartyStrength(MobileParty party)
        {
            return party?.Party == null
                ? 0f
                : Math.Max(0f, party.Party.EstimatedStrength);
        }

        private static MobileParty ResolveAssistanceMovementTarget(
            MobileParty offender)
        {
            return offender.BesiegerCamp?.LeaderParty ??
                   offender.Army?.LeaderParty ??
                   offender.AttachedTo ??
                   offender;
        }

        private static bool TryCaptureAssistanceLeaderSoloSpeed(
            MobileParty leader, PoliceTask task)
        {
            if (task.HasTheoreticalLeaderSoloSpeedAtAssignment &&
                task.LeaderSoloSpeedAtAssignment > 0.01f)
                return true;
            if (leader?.IsActive != true || leader.Army != null ||
                leader.AttachedTo != null)
                return false;

            float cachedBaseSpeed = Math.Max(
                0f, leader.LastCalculatedBaseSpeed);
            float soloSpeed = GetTheoreticalBaseSpeed(leader);
            if (soloSpeed <= 0.01f)
                return false;

            task.LeaderSoloSpeedAtAssignment = soloSpeed;
            task.HasTheoreticalLeaderSoloSpeedAtAssignment = true;
            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_LEADER_SOLO_SPEED_LOCKED",
                "theoreticalLeaderSoloSpeedAtAssignment=" +
                soloSpeed.ToString("0.00", CultureInfo.InvariantCulture) +
                "; cachedBaseSpeed=" +
                cachedBaseSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; disorganized=" + leader.IsDisorganized +
                "; task=" + task.TargetCrimeId);
            return true;
        }

        private static float GetTheoreticalBaseSpeed(MobileParty? party)
        {
            if (party?.IsActive != true || Campaign.Current?.Models
                    ?.PartySpeedCalculatingModel == null)
                return 0f;

            ExplainedNumber speed = Campaign.Current.Models
                .PartySpeedCalculatingModel.CalculateBaseSpeed(
                    party, includeDescriptions: true);
            float factors = speed.SumOfFactors;
            // Bannerlord's base-speed model includes the temporary post-battle
            // disorganized penalty as a -0.40 factor. The case threshold must
            // represent the party's normal pursuit capability, not the hour in
            // which it happened to finish a battle.
            if (party.IsDisorganized)
                factors += 0.4f;

            float normalized = speed.BaseNumber * (1f + factors);
            // Wet weather is unusually applied inside Bannerlord's base-speed
            // model rather than CalculateFinalSpeed. Remove only those two
            // explicitly explained environmental lines so the saved threshold
            // remains a theoretical normal-condition speed.
            string cavalryWeatherPenalty =
                new TextObject("{=Cb0k9KM8}Cavalry weather penalty")
                    .ToString();
            string mountedFootmenWeatherPenalty =
                new TextObject(
                    "{=JAKoFNgt}Footmen on horses weather penalty")
                    .ToString();
            foreach ((string name, float number) in speed.GetLines())
            {
                if ((string.Equals(name, cavalryWeatherPenalty,
                         StringComparison.Ordinal) ||
                     string.Equals(name, mountedFootmenWeatherPenalty,
                         StringComparison.Ordinal)) &&
                    number < 0f)
                {
                    normalized -= number;
                }
            }

            float minimum = Campaign.Current.Models
                .PartySpeedCalculatingModel.MinimumSpeed;
            return Math.Max(minimum, normalized);
        }

        private static AssistanceThreatSnapshot
            GetNativeCombatStrengthSnapshot(
                MobileParty observer, MobileParty offender)
        {
            var snapshot = new AssistanceThreatSnapshot();
            if (observer?.IsActive != true ||
                offender?.IsActive != true || offender.Party == null)
                return snapshot;

            MobileParty combatTarget =
                ResolveAssistanceMovementTarget(offender);
            MapEvent? mapEvent = combatTarget.MapEvent ?? offender.MapEvent;
            BattleSideEnum targetSide = combatTarget.Party.Side !=
                                        BattleSideEnum.None
                ? combatTarget.Party.Side
                : offender.Party.Side;
            var countedCombatGroups = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            if (mapEvent?.IsFinalized == false &&
                targetSide != BattleSideEnum.None)
            {
                foreach (MapEventParty participant in
                         mapEvent.PartiesOnSide(targetSide))
                {
                    if (participant.Party?.IsActive != true)
                        continue;

                    float strength = Math.Max(
                        0f, participant.Party.EstimatedStrength);
                    snapshot.Strength += strength;
                    string participantId =
                        participant.Party.MobileParty?.StringId ??
                        participant.Party.Id;
                    snapshot.CombatGroups.Add(
                        participantId + ":" + strength.ToString(
                            "0.00", CultureInfo.InvariantCulture));
                    MobileParty? participantMobile =
                        participant.Party.MobileParty;
                    if (participantMobile != null)
                    {
                        countedCombatGroups.Add(
                            GetCombatGroupKey(participantMobile));
                        IncludeMaximumCombatGroupSpeed(
                            snapshot, participantMobile);
                    }
                }
            }
            else
            {
                float baseStrength =
                    GetNativeCombatGroupStrength(combatTarget);
                snapshot.Strength = baseStrength;
                countedCombatGroups.Add(GetCombatGroupKey(combatTarget));
                snapshot.CombatGroups.Add(
                    GetCombatGroupKey(combatTarget) + ":" +
                    baseStrength.ToString(
                        "0.00", CultureInfo.InvariantCulture));
                IncludeMaximumCombatGroupSpeed(snapshot, combatTarget);
            }

            snapshot.JoiningRadius = Math.Max(0f,
                Campaign.Current?.Models?.EncounterModel
                    ?.GetEncounterJoiningRadius ?? 0f);
            if (snapshot.JoiningRadius <= 0.01f)
                return snapshot;
            // DefaultMobilePartyAIModel uses a joining-radius inner ring and a
            // second, tapered ring out to twice that radius while evaluating
            // local strength. Use the same envelope rather than crediting every
            // nearby party at full strength.
            snapshot.ThreatRadius = snapshot.JoiningRadius * 2f;

            Vec2 center = mapEvent?.IsFinalized == false
                ? mapEvent.Position.ToVec2()
                : combatTarget.Position.ToVec2();
            float observerDistance =
                observer.Position.ToVec2().Distance(center);
            float supportRadiusFactor = 1f + Math.Max(
                0f,
                (observerDistance - 1f) /
                Math.Max(0.01f,
                    (snapshot.JoiningRadius - 1f) * 2f));
            supportRadiusFactor = Math.Min(2f, supportRadiusFactor);
            float effectiveSupportRadius =
                snapshot.JoiningRadius * supportRadiusFactor;
            LocatableSearchData<MobileParty> data =
                MobileParty.StartFindingLocatablesAroundPosition(
                    center, snapshot.ThreatRadius);
            for (MobileParty nearby =
                     MobileParty.FindNextLocatable(ref data);
                 nearby != null;
                 nearby = MobileParty.FindNextLocatable(ref data))
            {
                MobileParty nearbyGroup =
                    ResolveAssistanceMovementTarget(nearby);
                string groupKey = GetCombatGroupKey(nearbyGroup);
                if (countedCombatGroups.Contains(groupKey) ||
                    !CanNearbyCombatGroupJoinTarget(
                        nearbyGroup, combatTarget, mapEvent, targetSide))
                    continue;

                float groupDistance =
                    nearbyGroup.Position.ToVec2().Distance(center);
                if (groupDistance > effectiveSupportRadius)
                    continue;

                float supportFactor = 1f;
                if (groupDistance > snapshot.JoiningRadius &&
                    supportRadiusFactor > 1.0001f)
                {
                    supportFactor = 1f -
                        (groupDistance - snapshot.JoiningRadius) /
                        (snapshot.JoiningRadius *
                         (supportRadiusFactor - 1f));
                    supportFactor = Math.Max(0f,
                        Math.Min(1f, supportFactor));
                }

                float strength = GetNativeCombatGroupStrength(
                    nearbyGroup) * supportFactor;
                if (strength <= 0f)
                    continue;

                countedCombatGroups.Add(groupKey);
                snapshot.Strength += strength;
                IncludeMaximumCombatGroupSpeed(snapshot, nearbyGroup);
                snapshot.CombatGroups.Add(
                    groupKey + ":" + strength.ToString(
                        "0.00", CultureInfo.InvariantCulture));
            }

            snapshot.Strength = Math.Max(0f, snapshot.Strength);
            return snapshot;
        }

        private static void IncludeMaximumCombatGroupSpeed(
            AssistanceThreatSnapshot snapshot, MobileParty party)
        {
            MobileParty movementGroup =
                ResolveAssistanceMovementTarget(party);
            float speed = GetTheoreticalBaseSpeed(movementGroup);
            if (speed <= snapshot.MaximumSpeed)
                return;

            snapshot.MaximumSpeed = speed;
            snapshot.FastestCombatGroupId =
                GetCombatGroupKey(movementGroup);
        }

        private static float GetNativeCombatGroupStrength(
            MobileParty combatTarget)
        {
            if (combatTarget?.IsActive != true ||
                combatTarget.Party == null)
                return 0f;

            if (combatTarget.BesiegerCamp != null)
            {
                try
                {
                    float siegeStrength = combatTarget.BesiegerCamp
                        .GetInvolvedPartiesForEventType()
                        .Sum(party =>
                            Math.Max(0f, party.EstimatedStrength));
                    if (siegeStrength > 0f)
                        return siegeStrength;
                }
                catch { }
            }

            Army? army = combatTarget.Army;
            return army != null &&
                   (combatTarget.AttachedTo != null ||
                    army.LeaderParty == combatTarget)
                ? Math.Max(0f, army.EstimatedStrength)
                : GetNativePartyStrength(combatTarget);
        }

        private static string GetCombatGroupKey(MobileParty party)
        {
            MobileParty groupLeader =
                ResolveAssistanceMovementTarget(party);
            return groupLeader.Army?.LeaderParty?.StringId ??
                   groupLeader.StringId;
        }

        private static bool CanNearbyCombatGroupJoinTarget(
            MobileParty candidate, MobileParty combatTarget,
            MapEvent? mapEvent, BattleSideEnum targetSide)
        {
            if (candidate?.IsActive != true || candidate.Party == null ||
                candidate.Party.NumberOfHealthyMembers <= 0 ||
                candidate.AttachedTo != null ||
                candidate.Position.IsOnLand !=
                combatTarget.Position.IsOnLand)
                return false;

            if (mapEvent?.IsFinalized == false &&
                targetSide != BattleSideEnum.None)
            {
                try
                {
                    return mapEvent.CanPartyJoinBattle(
                        candidate.Party, targetSide);
                }
                catch
                {
                    return false;
                }
            }

            if (candidate.MapEvent != null)
                return false;
            if (candidate.IsGarrison || candidate.IsMilitia ||
                candidate.CurrentSettlement?.SiegeEvent != null ||
                candidate.BesiegerCamp?.LeaderParty != null &&
                candidate.BesiegerCamp.LeaderParty != candidate)
                return false;

            IFaction? targetFaction = combatTarget.MapFaction;
            IFaction? candidateFaction = candidate.MapFaction;
            if (targetFaction == null || candidateFaction == null)
                return false;
            return candidateFaction == targetFaction &&
                   candidate.Aggressiveness > 0.01f;
        }

        private static float GetCommittedAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group)
        {
            float strength = GetNativePartyStrength(leader);
            foreach (string memberId in group.MemberPartyIds.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                MobileParty? member = FindActiveParty(memberId);
                if (IsGreyWardenLordParty(member))
                    strength += GetNativePartyStrength(member!);
            }
            return Math.Max(0f, strength);
        }

        private static float GetNativeFriendlyLocalStrength(
            MobileParty actor, MobileParty offender,
            out string friendlyCombatGroups)
        {
            MobileParty movementTarget =
                ResolveAssistanceMovementTarget(offender);
            float joiningRadius = Math.Max(0f,
                Campaign.Current?.Models?.EncounterModel
                    ?.GetEncounterJoiningRadius ?? 0f);
            float actorStrength = GetNativeCombatGroupStrength(actor);
            float strength = actorStrength;
            var strengthGroups = new List<string>
            {
                GetCombatGroupKey(actor) + ":" +
                actorStrength.ToString("0.00", CultureInfo.InvariantCulture) +
                "@1.00"
            };
            if (joiningRadius <= 0.01f)
            {
                friendlyCombatGroups = string.Join(",", strengthGroups);
                return strength;
            }

            Vec2 targetPosition = movementTarget.Position.ToVec2();
            float actorDistance =
                actor.Position.ToVec2().Distance(targetPosition);
            float supportRadiusFactor = 1f + Math.Max(
                0f,
                (actorDistance - 1f) /
                Math.Max(0.01f, (joiningRadius - 1f) * 2f));
            supportRadiusFactor = Math.Min(2f, supportRadiusFactor);
            float effectiveSupportRadius =
                joiningRadius * supportRadiusFactor;
            var countedGroups = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                GetCombatGroupKey(actor)
            };

            LocatableSearchData<MobileParty> data =
                MobileParty.StartFindingLocatablesAroundPosition(
                    actor.Position.ToVec2(), joiningRadius * 3f);
            for (MobileParty nearby =
                     MobileParty.FindNextLocatable(ref data);
                 nearby != null;
                 nearby = MobileParty.FindNextLocatable(ref data))
            {
                MobileParty nearbyGroup =
                    ResolveAssistanceMovementTarget(nearby);
                string groupKey = GetCombatGroupKey(nearbyGroup);
                if (countedGroups.Contains(groupKey) ||
                    !CanNearbyFriendlyGroupJoinActor(
                        nearbyGroup, actor))
                    continue;

                float groupDistance =
                    nearbyGroup.Position.ToVec2().Distance(targetPosition);
                if (groupDistance > joiningRadius * 2f)
                    continue;

                bool sharesTarget =
                    IsFriendlyGroupCoordinatingOnTarget(
                        nearbyGroup, movementTarget);
                if (!sharesTarget && groupDistance > effectiveSupportRadius)
                    continue;

                float supportFactor = 1f;
                if (!sharesTarget && groupDistance > joiningRadius &&
                    supportRadiusFactor > 1.0001f)
                {
                    supportFactor = 1f -
                        (groupDistance - joiningRadius) /
                        (joiningRadius * (supportRadiusFactor - 1f));
                    supportFactor = Math.Max(
                        0f, Math.Min(1f, supportFactor));
                }

                float fullStrength =
                    GetNativeCombatGroupStrength(nearbyGroup);
                float contributedStrength =
                    fullStrength * supportFactor;
                strength += contributedStrength;
                strengthGroups.Add(
                    groupKey + ":" +
                    contributedStrength.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "@" + supportFactor.ToString(
                        "0.00", CultureInfo.InvariantCulture));
                countedGroups.Add(groupKey);
            }
            friendlyCombatGroups = string.Join(",", strengthGroups);
            return Math.Max(0f, strength);
        }

        private static bool IsFriendlyGroupCoordinatingOnTarget(
            MobileParty candidate, MobileParty movementTarget)
        {
            PartyBase? aiTarget = candidate.Army?.LeaderParty?.Ai
                                      ?.AiBehaviorPartyBase ??
                                  candidate.Ai?.AiBehaviorPartyBase;
            if (aiTarget == movementTarget.Party)
                return true;

            MapEvent? targetEvent = movementTarget.MapEvent;
            if (aiTarget?.MapEvent != null && targetEvent != null &&
                aiTarget.MapEvent == targetEvent)
                return true;

            if (TryGetAssistanceDuty(candidate,
                    out MobileParty? dutyTarget, out _) &&
                dutyTarget?.IsActive == true)
            {
                return ResolveAssistanceMovementTarget(dutyTarget) ==
                       movementTarget;
            }

            return false;
        }

        private static bool CanNearbyFriendlyGroupJoinActor(
            MobileParty candidate, MobileParty actor)
        {
            if (candidate?.IsActive != true || candidate.Party == null ||
                candidate.Party.NumberOfHealthyMembers <= 0 ||
                candidate.AttachedTo != null ||
                candidate.Position.IsOnLand != actor.Position.IsOnLand ||
                candidate.MapEvent != null ||
                candidate.IsGarrison || candidate.IsMilitia ||
                candidate.CurrentSettlement?.SiegeEvent != null ||
                candidate.BesiegerCamp?.LeaderParty != null &&
                candidate.BesiegerCamp.LeaderParty != candidate)
                return false;

            return candidate.MapFaction != null &&
                   candidate.MapFaction == actor.MapFaction &&
                   candidate.Aggressiveness > 0.01f;
        }

        private static LocalStrengthDeclarationSnapshot
            EvaluateLocalDeclarationStrength(
                MobileParty actor, MobileParty offender)
        {
            MobileParty movementTarget =
                ResolveAssistanceMovementTarget(offender);
            AssistanceThreatSnapshot enemy =
                GetNativeCombatStrengthSnapshot(actor, offender);
            float friendlyStrength =
                GetNativeFriendlyLocalStrength(
                    actor, offender, out string friendlyCombatGroups);
            float distance = actor.Position.ToVec2().Distance(
                movementTarget.Position.ToVec2());
            bool strengthReady =
                friendlyStrength > enemy.Strength;
            return new LocalStrengthDeclarationSnapshot
            {
                Actor = actor,
                MovementTarget = movementTarget,
                FriendlyLocalStrength = friendlyStrength,
                EnemyLocalStrength = enemy.Strength,
                FriendlyCombatGroups = friendlyCombatGroups,
                Distance = distance,
                StrengthReady = strengthReady,
                Reason = strengthReady
                    ? "friendly_local_strength_ready"
                    : "friendly_local_strength_not_ready"
            };
        }

        private List<MobileParty> GetAvailableAssistanceCandidates(
            MobileParty leader)
        {
            return PoliceStats.GetAllPoliceParties()
                .Where(candidate =>
                    IsAvailableAssistanceCandidate(candidate, leader))
                .OrderBy(candidate =>
                    candidate.GetPosition2D.Distance(leader.GetPosition2D))
                .ThenBy(candidate => candidate.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool EnsureCommittedStrengthAdvantage(MobileParty leader,
            PoliceTask task, MobileParty offender, LordAssistanceGroup group,
            Army? army, float targetStrength)
        {
            float committedStrength =
                GetCommittedAssistanceStrength(leader, group);
            if (committedStrength > targetStrength)
                return true;

            List<MobileParty> candidates =
                GetAvailableAssistanceCandidates(leader);
            float maximumStrength = committedStrength +
                candidates.Sum(GetNativePartyStrength);
            if (maximumStrength <= targetStrength)
                return false;

            while (GetCommittedAssistanceStrength(leader, group) <=
                   targetStrength)
            {
                if (!TryAddAssistanceMember(
                        leader, task, offender, group, army))
                    return false;
            }
            return true;
        }

        private static string FormatStrengthDiagnostic(
            MobileParty observer, MobileParty offender,
            float ourStrength, float targetStrength)
        {
            AssistanceThreatSnapshot threat =
                GetNativeCombatStrengthSnapshot(observer, offender);
            return "target=" + offender.StringId +
                   "; ourStrength=" + ourStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; targetStrength=" + targetStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; joiningRadius=" +
                   threat.JoiningRadius.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; threatRadius=" +
                   threat.ThreatRadius.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; enemyMaximumSpeed=" +
                   threat.MaximumSpeed.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; fastestEnemyGroup=" +
                   (threat.FastestCombatGroupId.Length == 0
                       ? "-"
                       : threat.FastestCombatGroupId) +
                   "; targetCombatGroups=" +
                   string.Join(",", threat.CombatGroups);
        }

        private void FailAssistanceCase(MobileParty leader, PoliceTask task,
            LordAssistanceGroup? group, float targetStrength, string reason)
        {
            IFaction? warTarget = task.WarTarget ??
                task.TargetCrime?.Offender?.ActualClan?.MapFaction;
            float committedStrength = group == null
                ? GetNativePartyStrength(leader)
                : GetCommittedAssistanceStrength(leader, group);
            float maximumStrength = committedStrength +
                GetAvailableAssistanceCandidates(leader)
                    .Sum(GetNativePartyStrength);
            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_CASE_FAILED_STRENGTH",
                "target=" +
                (task.TargetCrime?.Offender?.StringId ?? "-") +
                "; committedStrength=" + committedStrength.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; maximumStrength=" + maximumStrength.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; targetStrength=" + targetStrength.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; reason=" + reason);

            if (group != null)
                ReleaseAssistanceGroup(leader.StringId,
                    "strength_insufficient");
            RestoreAi(leader);
            ClearTaskWarTracking(leader.StringId, true);
            GreyWardenPartyDesireBehavior.ClearIntent(leader);
            CrimeState.EndTask(leader.StringId);
            CrimeState.RefreshAccepting();
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan != null && warTarget != null &&
                !GwpPoliceWarReasonService.HasLegitimateWarReason(warTarget))
            {
                GwpCommon.TrySetNeutral(policeClan, warTarget);
            }
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
        }

        private static bool ShouldDisperseAssistanceArmyForSpeed(
            Army army, PoliceTask task, float targetMovementSpeed)
        {
            if (army?.LeaderParty?.IsActive != true ||
                army.Parties.Count <= 1)
                return false;

            float leaderSoloSpeed =
                task.LeaderSoloSpeedAtAssignment;
            return leaderSoloSpeed > 0.01f &&
                   targetMovementSpeed > leaderSoloSpeed;
        }

        private static bool CanReformAssistanceArmyAfterSpeedDispersal(
            PoliceTask task, float targetMovementSpeed)
        {
            float leaderSoloSpeed =
                task.LeaderSoloSpeedAtAssignment;
            return leaderSoloSpeed > 0.01f &&
                   targetMovementSpeed <= leaderSoloSpeed;
        }

        private void DisperseAssistanceArmyForSpeed(MobileParty leader,
            LordAssistanceGroup group, MobileParty offender, Army army,
            PoliceTask task,
            float assembledStrength, float targetStrength,
            AssistanceThreatSnapshot targetThreat)
        {
            int supportCount = army.Parties.Count(party =>
                GwpCommon.IsEnforcementDelayPatrolParty(party));
            float armySpeed = leader.LastCalculatedBaseSpeed;
            float offenderSpeed = offender.LastCalculatedBaseSpeed;
            MobileParty speedTarget =
                ResolveAssistanceMovementTarget(offender);
            float targetMovementSpeed =
                GetTheoreticalBaseSpeed(speedTarget);
            group.DispersedForSpeed = true;
            group.LastArmySpeedAtDispersal = Math.Max(
                0f, task.LeaderSoloSpeedAtAssignment);
            group.SpeedDetachedPartyIds.Clear();
            group.SpeedCatcherPartyId = string.Empty;

            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsGreyWardenLordParty(member))
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                if (member!.Army == army)
                    member.Army = null;
                AddSpeedDetachedParty(group, member.StringId);
                GreyWardenPartyDesireBehavior.ClearIntent(member);
                RequestAssistanceTargetIntent(
                    member, group, offender, 0.99f);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                    member);
            }

            AddSpeedDetachedParty(group, leader.StringId);
            if (army != null && IsArmyOwnedByGroup(army, group))
            {
                try
                {
                    GwpAssistanceArmyDisbandGuardPatch
                        .ApplyAuthorizedObjectiveFinished(army);
                }
                catch { }
            }
            RequestAssistanceTargetIntent(
                leader, group, offender, 0.99f);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                leader);
            SendDelayPatrolsAfterSpeedDispersal(group, offender);

            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_ARMY_SPEED_FULL_DISPERSAL",
                FormatStrengthDiagnostic(leader, offender, assembledStrength,
                    targetStrength) +
                "; armyMaximumSpeed=" + armySpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; offenderSpeed=" + offenderSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; speedTarget=" + speedTarget.StringId +
                "; speedTargetCachedBaseSpeed=" +
                speedTarget.LastCalculatedBaseSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; speedTargetTheoreticalSpeed=" +
                targetMovementSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; theoreticalLeaderSoloSpeedAtAssignment=" +
                task.LeaderSoloSpeedAtAssignment.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; regionalEnemyMaximumSpeed=" +
                targetThreat.MaximumSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; lordMembers=" + group.MemberPartyIds.Count +
                "; supports=" + supportCount);
        }

        private Army? MaintainSpeedDispersedPursuit(MobileParty leader,
            LordAssistanceGroup group, MobileParty? offender)
        {
            Army? army = leader.Army;
            group.SpeedDetachedPartyIds.RemoveAll(partyId =>
                !string.Equals(partyId, leader.StringId,
                    StringComparison.OrdinalIgnoreCase) &&
                !group.MemberPartyIds.Contains(
                    partyId, StringComparer.OrdinalIgnoreCase));

            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsGreyWardenLordParty(member))
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                if (member!.Army != null)
                    member.Army = null;
                AddSpeedDetachedParty(group, member.StringId);
            }

            AddSpeedDetachedParty(group, leader.StringId);
            if (army != null && IsArmyOwnedByGroup(army, group))
            {
                try
                {
                    GwpAssistanceArmyDisbandGuardPatch
                        .ApplyAuthorizedObjectiveFinished(army);
                }
                catch { }
            }
            army = null;

            if (offender?.IsActive != true || offender.Party == null ||
                offender.Party.NumberOfHealthyMembers <= 0)
                return army;

            group.TargetPartyId = offender.StringId;
            RequestAssistanceTargetIntent(leader, group, offender, 0.99f);
            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsGreyWardenLordParty(member))
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                RequestAssistanceTargetIntent(
                    member!, group, offender, 0.99f);
            }
            return army;
        }

        private void AdvanceAssistanceSpeedDispersal(MobileParty leader,
            LordAssistanceGroup group, MobileParty offender, Army? army,
            float committedStrength, float targetStrength)
        {
            MobileParty speedTarget =
                ResolveAssistanceMovementTarget(offender);
            float targetSpeed = speedTarget.LastCalculatedBaseSpeed;
            MobileParty? catcher = GetAssistanceSpeedCatcher(
                leader, group, army, targetSpeed);
            if (catcher != null)
            {
                if (catcher != leader)
                    army = ConsolidateSpeedDispersalAroundCatcher(
                        leader, group, offender, catcher, army,
                        committedStrength, targetStrength);

                if (!string.Equals(group.SpeedCatcherPartyId,
                        catcher.StringId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    group.SpeedCatcherPartyId = catcher.StringId;
                    GwpAiDiagnostics.WriteAction(leader,
                        "ASSISTANCE_SPEED_CATCHER_READY",
                        FormatStrengthDiagnostic(leader, offender,
                            committedStrength, targetStrength) +
                        "; catcher=" + catcher.StringId +
                        "; catcherSpeed=" +
                        catcher.LastCalculatedBaseSpeed.ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                        "; catcherArmy=" +
                        (catcher.Army?.LeaderParty?.StringId ?? "-") +
                        "; catcherAttachedTo=" +
                        (catcher.AttachedTo?.StringId ?? "-") +
                        "; speedTarget=" + speedTarget.StringId +
                        "; speedTargetSpeed=" + targetSpeed.ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                        "; detachedLords=" +
                        group.SpeedDetachedPartyIds.Count);
                }
                return;
            }

            group.SpeedCatcherPartyId = string.Empty;
            bool detachedPartyStillTransitioning =
                group.SpeedDetachedPartyIds
                    .Select(FindActiveParty)
                    .Any(party => IsGreyWardenLordParty(party) &&
                                  (party!.Army != null ||
                                   party.AttachedTo != null));
            if (detachedPartyStillTransitioning)
                return;

            MobileParty? nextMember = group.MemberPartyIds
                .Where(memberId =>
                    !IsSpeedDetached(group, memberId))
                .Select(FindActiveParty)
                .Where(member => IsGreyWardenLordParty(member) &&
                                 army != null &&
                                 member!.Army == army &&
                                 member.AttachedTo == leader)
                .OrderByDescending(member =>
                    member!.LastCalculatedBaseSpeed)
                .ThenBy(member => member!.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (nextMember != null)
            {
                nextMember.Army = null;
                AddSpeedDetachedParty(group, nextMember.StringId);
                GreyWardenPartyDesireBehavior.ClearIntent(nextMember);
                RequestAssistanceTargetIntent(
                    nextMember, group, offender, 0.99f);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                    nextMember);
                WriteSpeedMemberDetachedDiagnostic(
                    leader, group, offender, nextMember, speedTarget,
                    army, committedStrength, targetStrength);
                return;
            }

            bool hasUndetachedMember =
                group.MemberPartyIds.Any(memberId =>
                    !IsSpeedDetached(group, memberId) &&
                    IsGreyWardenLordParty(
                        FindActiveParty(memberId)));
            if (hasUndetachedMember ||
                IsSpeedDetached(group, leader.StringId))
                return;

            AddSpeedDetachedParty(group, leader.StringId);
            if (army != null && IsArmyOwnedByGroup(army, group))
            {
                try
                {
                    GwpAssistanceArmyDisbandGuardPatch
                        .ApplyAuthorizedObjectiveFinished(army);
                }
                catch { }
            }
            RequestAssistanceTargetIntent(
                leader, group, offender, 0.99f);
            SendDelayPatrolsAfterSpeedDispersal(group, offender);
            WriteSpeedMemberDetachedDiagnostic(
                leader, group, offender, leader, speedTarget, null,
                committedStrength, targetStrength);
        }

        private static MobileParty? GetAssistanceSpeedCatcher(
            MobileParty leader, LordAssistanceGroup group, Army? army,
            float targetSpeed)
        {
            if (targetSpeed <= 0.01f)
                return null;

            MobileParty? detachedCatcher = group.SpeedDetachedPartyIds
                .Select(FindActiveParty)
                .Where(party => IsGreyWardenLordParty(party) &&
                                party!.Army == null &&
                                party.AttachedTo == null &&
                                party!.LastCalculatedBaseSpeed > targetSpeed)
                .OrderByDescending(party =>
                    party!.LastCalculatedBaseSpeed)
                .ThenBy(party => party!.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (detachedCatcher != null)
                return detachedCatcher;

            return !IsSpeedDetached(group, leader.StringId) &&
                   army?.LeaderParty == leader &&
                   leader.LastCalculatedBaseSpeed > targetSpeed
                ? leader
                : null;
        }

        private Army? ConsolidateSpeedDispersalAroundCatcher(
            MobileParty leader, LordAssistanceGroup group,
            MobileParty offender, MobileParty catcher, Army? army,
            float committedStrength, float targetStrength)
        {
            int priorDetachedCount =
                group.SpeedDetachedPartyIds.Count;
            group.SpeedDetachedPartyIds.RemoveAll(partyId =>
                !string.Equals(partyId, catcher.StringId,
                    StringComparison.OrdinalIgnoreCase));
            AddSpeedDetachedParty(group, catcher.StringId);

            army = CreateOrRecoverAssistanceArmy(leader);
            int reassignedCount = 0;
            if (army != null)
            {
                RequestAssistanceTargetIntent(
                    leader, group, offender, 0.99f);
                foreach (string memberId in
                         group.MemberPartyIds.ToList())
                {
                    if (string.Equals(memberId, catcher.StringId,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    MobileParty? member = FindActiveParty(memberId);
                    if (!IsGreyWardenLordParty(member))
                    {
                        RemoveAssistanceMember(group, memberId);
                        continue;
                    }
                    if (member!.Army != null &&
                        member.Army != army)
                    {
                        RemoveAssistanceMember(group, memberId);
                        continue;
                    }

                    GreyWardenPartyDesireBehavior.ClearIntent(member);
                    if (member.Army == null)
                    {
                        member.Army = army;
                        reassignedCount++;
                    }
                    TryMergeArmyMember(army, leader, member);
                    GreyWardenPartyDesireBehavior
                        .RequestImmediateRethink(member);
                }
            }

            RequestAssistanceTargetIntent(
                catcher, group, offender, 0.99f);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                catcher);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                leader);

            if (priorDetachedCount !=
                    group.SpeedDetachedPartyIds.Count ||
                reassignedCount > 0)
            {
                MobileParty speedTarget =
                    ResolveAssistanceMovementTarget(offender);
                GwpAiDiagnostics.WriteAction(leader,
                    "ASSISTANCE_SPEED_SPLIT_CONSOLIDATED",
                    FormatStrengthDiagnostic(leader, offender,
                        committedStrength, targetStrength) +
                    "; catcher=" + catcher.StringId +
                    "; catcherSpeed=" +
                    catcher.LastCalculatedBaseSpeed.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; speedTarget=" + speedTarget.StringId +
                    "; speedTargetSpeed=" +
                    speedTarget.LastCalculatedBaseSpeed.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; priorDetached=" + priorDetachedCount +
                    "; retainedDetached=" +
                    group.SpeedDetachedPartyIds.Count +
                    "; reassignedToArmy=" + reassignedCount +
                    "; army=" +
                    (army?.LeaderParty?.StringId ?? "-"));
            }

            return army;
        }

        private static void WriteSpeedMemberDetachedDiagnostic(
            MobileParty leader, LordAssistanceGroup group,
            MobileParty offender, MobileParty detached,
            MobileParty speedTarget, Army? remainingArmy,
            float committedStrength, float targetStrength)
        {
            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_SPEED_MEMBER_DETACHED",
                FormatStrengthDiagnostic(leader, offender, committedStrength,
                    targetStrength) +
                "; detached=" + detached.StringId +
                "; detachedSpeedNow=" +
                detached.LastCalculatedBaseSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; detachedArmy=" +
                (detached.Army?.LeaderParty?.StringId ?? "-") +
                "; detachedAttachedTo=" +
                (detached.AttachedTo?.StringId ?? "-") +
                "; speedTarget=" + speedTarget.StringId +
                "; speedTargetSpeed=" +
                speedTarget.LastCalculatedBaseSpeed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; remainingArmySpeed=" +
                (remainingArmy?.LeaderParty?.LastCalculatedBaseSpeed ?? 0f)
                    .ToString("0.00", CultureInfo.InvariantCulture) +
                "; detachedLords=" +
                group.SpeedDetachedPartyIds.Count +
                "; totalLords=" +
                (group.MemberPartyIds.Count + 1));
        }

        private static bool IsSpeedDetached(LordAssistanceGroup group,
            string partyId) =>
            group.SpeedDetachedPartyIds.Contains(
                partyId, StringComparer.OrdinalIgnoreCase);

        private static void AddSpeedDetachedParty(
            LordAssistanceGroup group, string partyId)
        {
            if (!IsSpeedDetached(group, partyId))
                group.SpeedDetachedPartyIds.Add(partyId);
        }

        private static void RequestAssistanceTargetIntent(MobileParty party,
            LordAssistanceGroup group, MobileParty offender, float priority)
        {
            MobileParty movementTarget =
                ResolveAssistanceMovementTarget(offender);
            GreyWardenPartyDesireBehavior.RequestPursuit(
                party, movementTarget, priority);
        }

        private void SendDelayPatrolsAfterSpeedDispersal(
            LordAssistanceGroup group, MobileParty offender)
        {
            foreach (DelayPatrolState state in _delayPatrolStates.Values.ToList())
            {
                if (state.Returning ||
                    !string.Equals(state.TargetPartyId, group.TargetPartyId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                MobileParty? patrol = FindActiveParty(state.PatrolPartyId);
                if (patrol == null) continue;
                DetachDelayPatrolFromArmy(patrol);
                RequestAssistanceTargetIntent(patrol, group, offender, 8f);
            }
        }

        private static Army? CreateOrRecoverAssistanceArmy(MobileParty leader)
        {
            Army? army = leader.Army;
            if (army != null)
                return army.LeaderParty == leader && army.Kingdom == null ? army : null;

            try
            {
                // This is Bannerlord's real Army object. A null Kingdom keeps the
                // independent Grey Warden clan independent; no hidden faction is made.
                return new Army(null, leader, Army.ArmyTypes.Patrolling);
            }
            catch (Exception exception)
            {
                GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_CREATE_FAILED",
                    "error=" + exception.GetType().Name);
                return null;
            }
        }

        private Army? MaintainAssistanceArmy(MobileParty leader,
            LordAssistanceGroup group)
        {
            Army? army = CreateOrRecoverAssistanceArmy(leader);
            if (army == null) return null;

            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsGreyWardenLordParty(member))
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                if (member!.Army != null && member.Army != army)
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                GreyWardenPartyDesireBehavior.ClearIntent(member);
                if (member.Army == null)
                {
                    member.Army = army;
                    GwpAiDiagnostics.WriteAction(member, "ASSISTANCE_ARMY_REJOINED",
                        "leader=" + leader.StringId + "; crime=" + group.CrimeId);
                }

                TryMergeArmyMember(army, leader, member);
            }

            return army;
        }

        private bool TryAddAssistanceMember(MobileParty leader, PoliceTask task,
            MobileParty offender, LordAssistanceGroup group, Army? army)
        {
            MobileParty? helper = PoliceStats.GetAllPoliceParties()
                .Where(candidate => IsAvailableAssistanceCandidate(candidate, leader))
                .OrderBy(candidate => candidate.GetPosition2D.Distance(leader.GetPosition2D))
                .ThenBy(candidate => candidate.StringId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (helper == null)
                return false;

            ReleaseCandidateCaseToPool(helper);
            group.MemberPartyIds.Add(helper.StringId);
            group.MemberPartyIds = group.MemberPartyIds
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _assistanceAssignedHours[helper.StringId] = CampaignTime.Now.ToHours;
            CrimePool.TrimOpenCasesToCapacity(
                CrimePool.MaxTaskPoolEntries);

            if (army != null)
            {
                // Assigning Army invokes Bannerlord's OnAddPartyInternal,
                // including its native same-clan influence cost (zero).
                helper.Army = army;
                TryMergeArmyMember(army, leader, helper);
            }
            else
            {
                if (group.DispersedForSpeed)
                    AddSpeedDetachedParty(group, helper.StringId);
                RequestAssistanceTargetIntent(
                    helper, group, offender, 0.99f);
            }
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(helper);
            GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_MEMBER_ADDED",
                "helper=" + helper.StringId + "; target=" + offender.StringId +
                "; crime=" + task.TargetCrimeId + "; memberCount=" + group.MemberPartyIds.Count +
                "; armyKingdom=" + (army?.Kingdom?.StringId ?? "-") +
                "; speedDispersed=" + group.DispersedForSpeed);
            GwpAiDiagnostics.WriteAction(helper, "ASSISTANCE_ARMY_JOINED",
                "leader=" + leader.StringId + "; target=" + offender.StringId +
                "; crime=" + task.TargetCrimeId);
            return true;
        }

        private bool IsAvailableAssistanceCandidate(MobileParty? candidate, MobileParty leader)
        {
            if (!IsGreyWardenLordParty(candidate) || candidate == leader ||
                 candidate!.MapEvent != null || candidate.LeaderHero?.IsPrisoner == true ||
                 candidate.Army != null || IsAssistanceOccupied(candidate) ||
                 GreyWardenTrainingBehavior.ShouldReserveFromNewDuties(candidate) ||
                 GreyWardenVillageReconstructionBehavior
                     .ShouldReserveFromOrdinaryCases(candidate) ||
                 GreyWardenIssueResolutionBehavior
                     .ShouldReserveFromOrdinaryCases(candidate) ||
                 GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest(candidate) ||
                 GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder(candidate) ||
                 GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(candidate))
                return false;

            PoliceTask? task = CrimeState.GetTask(candidate.StringId);
            return task?.IsEscortingPlayer != true &&
                   task?.IsPlayerBountyEscort != true &&
                   task?.TargetCrime?.Offender?.IsMainParty != true;
        }

        private void ReleaseCandidateCaseToPool(MobileParty helper)
        {
            PoliceTask? oldTask = CrimeState.GetTask(helper.StringId);
            if (oldTask != null)
            {
                CrimeRecord? oldCrime = oldTask.TargetCrime;
                IFaction? oldWarTarget = oldTask.WarTarget;
                RestoreAi(helper);
                ClearTaskWarTracking(helper.StringId, true);
                CrimeState.EndTask(helper.StringId);
                if (oldCrime?.Offender?.IsActive == true)
                    CrimeState.ReopenCase(oldCrime);

                Clan? policeClan = PoliceStats.GetPoliceClan();
                if (policeClan != null && oldWarTarget != null &&
                    !GwpPoliceWarReasonService.HasLegitimateWarReason(oldWarTarget))
                    GwpCommon.TrySetNeutral(policeClan, oldWarTarget);
            }

            GreyWardenPartyDesireBehavior.ClearIntent(helper);
        }

        private static void TryMergeArmyMember(Army army, MobileParty leader,
            MobileParty member)
        {
            if (member.Army != army || member.AttachedTo == leader ||
                member.MapEvent != null || leader.MapEvent != null ||
                member.CurrentSettlement != null ||
                member.IsCurrentlyAtSea != leader.IsCurrentlyAtSea)
                return;

            float contactDistance = member.IsCurrentlyAtSea
                ? Campaign.Current.Models.EncounterModel.MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy
                : Campaign.Current.Models.EncounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;
            if ((member.Position - leader.Position).LengthSquared <
                contactDistance * contactDistance)
                army.AddPartyToMergedParties(member);
        }

        private static bool AreAllMembersAssembled(MobileParty leader,
            LordAssistanceGroup group, Army army)
        {
            if (group.MemberPartyIds.Count == 0) return false;
            foreach (string memberId in group.MemberPartyIds)
            {
                MobileParty? member = FindActiveParty(memberId);
                if (member == null || member.Army != army ||
                    member.AttachedTo != leader)
                    return false;
            }
            return true;
        }

        private void CleanupInvalidAssistanceGroups()
        {
            foreach (LordAssistanceGroup group in _assistanceGroups.Values.ToList())
            {
                PoliceTask? task = CrimeState.GetTask(group.LeaderPartyId);
                if (IsAssistanceGroupCaseStillActive(group, task))
                    continue;

                MobileParty? leader = FindParty(group.LeaderPartyId);
                if (leader != null)
                    GwpAiDiagnostics.WriteAction(leader,
                        "ASSISTANCE_CASE_ENDED",
                        "taskExists=" + (task != null) +
                        "; taskOwner=" + (task?.PolicePartyId ?? "-") +
                        "; groupLeader=" + group.LeaderPartyId +
                        "; taskCrime=" + (task?.TargetCrimeId ?? "-") +
                        "; groupCrime=" + group.CrimeId);
                ReleaseAssistanceGroup(group.LeaderPartyId, "leader_case_ended");
            }
        }

        private void HandleTaskOwnerMapEventEnded(MapEvent mapEvent)
        {
            foreach (KeyValuePair<string, PoliceTask> entry in
                     CrimeState.ActiveTasks.ToList())
            {
                string ownerId = entry.Key;
                PoliceTask task = entry.Value;
                bool ownerWasInEvent = mapEvent.InvolvedParties.Any(
                    party => string.Equals(party?.MobileParty?.StringId,
                        ownerId,
                        StringComparison.OrdinalIgnoreCase));
                if (!ownerWasInEvent)
                    continue;

                MobileParty? owner = FindParty(ownerId);
                if (CanContinueLeadingPoliceTask(owner))
                    continue;

                if (owner != null)
                    GwpAiDiagnostics.WriteAction(owner,
                        "TASK_FAILED_OWNER_CANNOT_LEAD_AFTER_BATTLE",
                        "crime=" + (task.TargetCrimeId ?? "-") +
                        "; assistance=" +
                        _assistanceGroups.ContainsKey(ownerId) +
                        "; leader=" +
                        (owner.LeaderHero?.StringId ?? "-") +
                        "; leaderActive=" +
                        (owner.LeaderHero?.IsActive.ToString() ?? "-") +
                        "; leaderPrisoner=" +
                        (owner.LeaderHero?.IsPrisoner.ToString() ?? "-") +
                        "; leaderFugitive=" +
                        (owner.LeaderHero?.IsFugitive.ToString() ?? "-"));

                FailTaskBecauseOwnerCannotLead(ownerId, owner,
                    "owner_cannot_lead_after_map_event");
            }
        }

        private void FailTaskBecauseOwnerCannotLead(string ownerId,
            MobileParty? owner, string reason)
        {
            // The assistance Army must be dissolved while its group still
            // identifies every attached helper. EndTask comes afterwards so
            // no leaderless native Army survives this same event callback.
            ReleaseAssistanceGroup(ownerId, reason);
            if (owner?.IsActive == true)
            {
                RestoreAi(owner);
                GreyWardenPartyDesireBehavior.ClearIntent(owner);
            }
            ClearTaskWarTracking(ownerId, true);
            CrimeState.EndTask(ownerId);
            CrimeState.RefreshAccepting();
            if (owner?.IsActive == true)
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(owner);
        }

        private void RemoveAssistanceMember(LordAssistanceGroup group, string memberId)
        {
            group.MemberPartyIds.RemoveAll(id => string.Equals(id, memberId,
                StringComparison.OrdinalIgnoreCase));
            group.SpeedDetachedPartyIds.RemoveAll(id => string.Equals(
                id, memberId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(group.SpeedCatcherPartyId, memberId,
                    StringComparison.OrdinalIgnoreCase))
                group.SpeedCatcherPartyId = string.Empty;
            _assistanceAssignedHours.Remove(memberId);
        }

        private void CompleteAssistanceTasks(string leaderId)
        {
            if (!_assistanceGroups.TryGetValue(leaderId, out LordAssistanceGroup? group))
                return;

            // Each current member is a separate assistance entry in the Case Ledger.
            // A completed source case therefore completes and funds each entry once.
            foreach (string memberId in group.MemberPartyIds.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                PoliceResourceManager.CreditSuccessfulCaseCompletion();
                GwpPlayerRequestDeferral.NotifyDutyCompleted(
                    FindParty(memberId), "assistance_case");
            }

            ReleaseAssistanceGroup(leaderId, "case_target_defeated");
        }

        private void ReleaseAssistanceGroup(string leaderId, string reason)
        {
            if (!_assistanceGroups.TryGetValue(leaderId, out LordAssistanceGroup? group))
                return;

            MobileParty? leader = FindParty(leaderId);
            // Also clears the decision lock written by the briefly deployed
            // assembly prototype. Current assistance never disables long-term AI.
            if (leader?.IsActive == true)
                RestoreAi(leader);
            _assistanceGroups.Remove(leaderId);
            Army? army = leader?.Army;
            if (army == null || army.LeaderParty != leader)
            {
                army = group.MemberPartyIds.Select(FindParty)
                    .Select(member => member?.Army)
                    .FirstOrDefault(candidate => candidate?.LeaderParty?.StringId == leaderId);
            }

            if (army != null && IsArmyOwnedByGroup(army, group))
            {
                try
                {
                    GwpAssistanceArmyDisbandGuardPatch
                        .ApplyAuthorizedObjectiveFinished(army);
                }
                catch { }
            }

            foreach (string memberId in group.MemberPartyIds)
            {
                _assistanceAssignedHours.Remove(memberId);
                MobileParty? member = FindParty(memberId);
                if (member?.IsActive != true) continue;
                GreyWardenPartyDesireBehavior.ClearIntent(member);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(member);
                GwpAiDiagnostics.WriteAction(member, "ASSISTANCE_ARMY_RELEASED",
                    "leader=" + leaderId + "; reason=" + reason);
            }
            if (leader?.IsActive == true)
                GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_DISBANDED",
                    "reason=" + reason + "; released=" + group.MemberPartyIds.Count);
            CrimePool.TrimOpenCasesToCapacity(
                CrimePool.MaxTaskPoolEntries);
        }

        private static bool IsArmyOwnedByGroup(Army army, LordAssistanceGroup group) =>
            army.Kingdom == null && army.LeaderParty != null &&
            string.Equals(army.LeaderParty.StringId, group.LeaderPartyId,
                StringComparison.OrdinalIgnoreCase);

        private bool IsAssistanceOccupied(MobileParty? party)
        {
            if (party == null) return false;
            return _assistanceGroups.ContainsKey(party.StringId) ||
                   IsAssistanceMember(party.StringId);
        }

        internal static bool IsPartyOccupiedByAssistance(MobileParty? party) =>
            _instance != null && _instance.IsAssistanceOccupied(party);

        private void ReleaseAssistanceForPlayerRequest(MobileParty party)
        {
            string? leaderId = _assistanceGroups.ContainsKey(party.StringId)
                ? party.StringId
                : _assistanceGroups.Values.FirstOrDefault(group =>
                    group.MemberPartyIds.Contains(party.StringId,
                        StringComparer.OrdinalIgnoreCase))?.LeaderPartyId;
            if (!string.IsNullOrWhiteSpace(leaderId))
                ReleaseAssistanceGroup(leaderId, "player_request_priority");
        }

        private bool IsAssistanceMember(string partyId) =>
            _assistanceGroups.Values.Any(group => group.MemberPartyIds.Contains(
                partyId, StringComparer.OrdinalIgnoreCase));

        private static MobileParty? FindParty(string partyId) =>
            MobileParty.All.FirstOrDefault(party => string.Equals(
                party.StringId, partyId, StringComparison.OrdinalIgnoreCase));

        private static MobileParty? FindActiveParty(string partyId) =>
            MobileParty.All.FirstOrDefault(party => party.IsActive &&
                string.Equals(party.StringId, partyId, StringComparison.OrdinalIgnoreCase));

        internal static bool IsActiveAssistanceArmy(Army? army)
        {
            if (_instance == null || army?.LeaderParty == null || army.Kingdom != null)
                return false;
            return _instance._assistanceGroups.TryGetValue(
                       army.LeaderParty.StringId, out LordAssistanceGroup? group) &&
                   IsArmyOwnedByGroup(army, group);
        }

        internal static bool TryGetAssistanceDuty(MobileParty? party,
            out MobileParty? target, out AiBehavior behavior)
        {
            target = null;
            behavior = AiBehavior.None;
            if (_instance == null || party?.IsActive != true)
                return false;

            LordAssistanceGroup? group = null;
            bool isLeader = _instance._assistanceGroups.TryGetValue(
                party.StringId, out group);
            if (!isLeader)
            {
                group = _instance._assistanceGroups.Values.FirstOrDefault(candidate =>
                    candidate.MemberPartyIds.Contains(party.StringId,
                        StringComparer.OrdinalIgnoreCase));
            }
            if (group == null)
                return false;

            bool speedDetached =
                IsSpeedDetached(group, party.StringId);
            if (!isLeader && !speedDetached)
            {
                target = FindActiveParty(group.LeaderPartyId);
                behavior = AiBehavior.EscortParty;
                return target != null && target != party;
            }

            MobileParty? offender = FindActiveParty(group.TargetPartyId);
            if (offender == null)
                return false;

            target = ResolveAssistanceMovementTarget(offender);
            // Native GoAroundParty is the lord AI's own keep-out pursuit. Its
            // short-term implementation first seeks the farthest valid point in
            // the native outer ring, while initiative remains free to engage or
            // flee after diplomacy changes.
            behavior = AiBehavior.GoAroundParty;
            return target != null && target != party;
        }

        private float GetAssistanceContactDistance(MobileParty leader,
            MobileParty offender, out MobileParty contactParty)
        {
            MobileParty movementTarget =
                ResolveAssistanceMovementTarget(offender);
            contactParty = leader;
            float closestDistance = leader.GetPosition2D.Distance(
                movementTarget.GetPosition2D);

            if (!_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group))
                return closestDistance;

            foreach (string memberId in group.MemberPartyIds)
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsGreyWardenLordParty(member))
                    continue;

                float memberDistance = member!.GetPosition2D.Distance(
                    movementTarget.GetPosition2D);
                if (memberDistance >= closestDistance)
                    continue;

                closestDistance = memberDistance;
                contactParty = member;
            }

            return closestDistance;
        }

        private static float GetNativeMaximumGoAroundDistance()
        {
            float joiningRadius = Math.Max(0f,
                Campaign.Current?.Models?.EncounterModel
                    ?.GetEncounterJoiningRadius ?? 0f);
            if (joiningRadius <= 0.01f)
                return GwpTuning.Enforcement.WarDistance;

            // MobilePartyAi.GetGoAroundPartyBehavior passes joiningRadius*1.15
            // to GetDefendingPosition. Its first (outermost) native attempt is
            // defendRadius²*0.5 before it falls inward only for navigation.
            float defendRadius = joiningRadius * 1.15f;
            return defendRadius * defendRadius * 0.5f;
        }

        private bool HasAssistanceEngagementStrengthAdvantage(
            MobileParty leader, MobileParty offender,
            out float engagementStrength, out float targetStrength)
        {
            targetStrength =
                GetNativeCombatStrengthSnapshot(leader, offender).Strength;
            engagementStrength = GetNativePartyStrength(leader);
            if (!_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group))
                return true;

            // Group size is fixed by the same native-style regional comparison.
            // It never shrinks when the target weakens; a target that grows past
            // all eligible Grey Warden strength is failed before contact.
            engagementStrength =
                GetCommittedAssistanceStrength(leader, group);

            return engagementStrength > targetStrength;
        }

        private bool TryGetNativeDeclarationCandidate(
            MobileParty leader, MobileParty offender, float warDistance,
            out MobileParty actor,
            out LocalStrengthDeclarationSnapshot prediction)
        {
            var candidates = new List<MobileParty>();
            if (_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group) &&
                group.DispersedForSpeed)
            {
                candidates.Add(leader);
                candidates.AddRange(group.MemberPartyIds
                    .Select(FindActiveParty)
                    .Where(IsGreyWardenLordParty)
                    .Cast<MobileParty>());
            }
            else
            {
                // A formed assistance army is one native combat group. Only
                // its leader owns the initiative decision; a helper still
                // travelling to the army must not trigger an early war.
                candidates.Add(leader);
            }

            LocalStrengthDeclarationSnapshot? closestPrediction = null;
            foreach (MobileParty candidate in candidates
                         .Distinct()
                         .OrderBy(candidate =>
                             candidate.Position.ToVec2().Distance(
                                 ResolveAssistanceMovementTarget(offender)
                                     .Position.ToVec2())))
            {
                LocalStrengthDeclarationSnapshot current =
                    EvaluateLocalDeclarationStrength(
                        candidate, offender);
                closestPrediction ??= current;
                if (current.Distance > warDistance ||
                    !current.StrengthReady)
                    continue;

                actor = candidate;
                prediction = current;
                return true;
            }

            actor = closestPrediction?.Actor ?? leader;
            prediction = closestPrediction ??
                         EvaluateLocalDeclarationStrength(
                             leader, offender);
            return false;
        }

        private static string FormatLocalStrengthDeclarationDiagnostic(
            LocalStrengthDeclarationSnapshot prediction,
            float committedStrength, float requiredStrength)
        {
            return "actor=" + prediction.Actor.StringId +
                   "; movementTarget=" +
                   prediction.MovementTarget.StringId +
                   "; distance=" + prediction.Distance.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; friendlyLocalStrength=" +
                   prediction.FriendlyLocalStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; friendlyLocalGroups=" +
                   (prediction.FriendlyCombatGroups.Length == 0
                       ? "-"
                       : prediction.FriendlyCombatGroups) +
                   "; enemyLocalStrength=" +
                   prediction.EnemyLocalStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; committedStrength=" +
                   committedStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; requiredRegionalStrength=" +
                   requiredStrength.ToString(
                       "0.00", CultureInfo.InvariantCulture) +
                   "; strengthReady=" +
                   prediction.StrengthReady +
                   "; reason=" + prediction.Reason;
        }

        private void RefreshAssistanceDutyAfterWarDeclaration(
            MobileParty leader)
        {
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
            if (!_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group))
                return;

            foreach (string memberId in group.MemberPartyIds)
            {
                MobileParty? member = FindActiveParty(memberId);
                if (member != null)
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                        member);
            }
        }

        private bool TryGetDelaySupportAssistanceArmy(
            string sourceTaskPolicePartyId, string targetPartyId,
            out MobileParty? leader, out Army? army)
        {
            leader = null;
            army = null;

            LordAssistanceGroup? group = null;
            if (!string.IsNullOrEmpty(sourceTaskPolicePartyId))
                _assistanceGroups.TryGetValue(sourceTaskPolicePartyId, out group);
            if (group == null && !string.IsNullOrEmpty(targetPartyId))
                group = _assistanceGroups.Values.FirstOrDefault(candidate =>
                    string.Equals(candidate.TargetPartyId, targetPartyId,
                        StringComparison.OrdinalIgnoreCase));
            if (group == null) return false;
            if (group.DispersedForSpeed &&
                IsSpeedDetached(group, group.LeaderPartyId))
                return false;

            PoliceTask? task = CrimeState.GetTask(group.LeaderPartyId);
            if (!IsAssistanceGroupCaseStillActive(group, task)) return false;
            if (!string.IsNullOrEmpty(targetPartyId) &&
                !string.Equals(group.TargetPartyId, targetPartyId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            leader = FindActiveParty(group.LeaderPartyId);
            if (!IsGreyWardenLordParty(leader)) return false;
            army = group.DispersedForSpeed
                ? MaintainSpeedDispersedPursuit(leader!, group,
                    FindActiveParty(group.TargetPartyId))
                : MaintainAssistanceArmy(leader!, group);
            return army != null && army.LeaderParty == leader;
        }

        internal static bool IsAuthorizedAssistanceTarget(MobileParty? party, MobileParty? target)
        {
            if (_instance == null || party?.IsActive != true || target?.IsActive != true)
                return false;
            foreach (LordAssistanceGroup group in _instance._assistanceGroups.Values)
            {
                bool belongsToGroup = string.Equals(group.LeaderPartyId, party.StringId,
                        StringComparison.OrdinalIgnoreCase) ||
                    group.MemberPartyIds.Contains(party.StringId,
                        StringComparer.OrdinalIgnoreCase) ||
                    (party.Army != null && IsArmyOwnedByGroup(party.Army, group));
                if (!belongsToGroup) continue;

                MobileParty? offender = FindActiveParty(group.TargetPartyId);
                if (offender == null) continue;
                MobileParty combatTarget =
                    ResolveAssistanceMovementTarget(offender);
                if (target == offender || target == combatTarget)
                    return true;
            }
            return false;
        }

        internal static string GetAssistanceDiagnostic(MobileParty? party)
        {
            if (_instance == null || party == null) return "none";
            if (_instance._assistanceGroups.TryGetValue(party.StringId,
                    out LordAssistanceGroup? led))
            {
                Army? army = party.Army;
                int attached = army == null ? 0 : led.MemberPartyIds.Count(id =>
                    FindActiveParty(id)?.AttachedTo == party);
                int supportCount = army?.Parties.Count(candidate =>
                    GwpCommon.IsEnforcementDelayPatrolParty(candidate)) ?? 0;
                int attachedSupportCount = army?.Parties.Count(candidate =>
                    GwpCommon.IsEnforcementDelayPatrolParty(candidate) &&
                    candidate.AttachedTo == party) ?? 0;
                int detachedMembers = led.MemberPartyIds.Count(id =>
                    IsSpeedDetached(led, id));
                int expectedAttached =
                    led.MemberPartyIds.Count - detachedMembers;
                MobileParty? diagnosticTarget =
                    FindActiveParty(led.TargetPartyId);
                AssistanceThreatSnapshot? diagnosticThreat =
                    diagnosticTarget == null
                        ? null
                        : GetNativeCombatStrengthSnapshot(
                            party, diagnosticTarget);
                MobileParty contactParty = party;
                float contactDistance = diagnosticTarget == null
                    ? float.MaxValue
                    : _instance.GetAssistanceContactDistance(
                        party, diagnosticTarget, out contactParty);
                MobileParty? movementTarget = diagnosticTarget == null
                    ? null
                    : ResolveAssistanceMovementTarget(diagnosticTarget);
                PoliceTask? diagnosticTask =
                    CrimeState.GetTask(led.LeaderPartyId);
                float engagementStrength = GetNativePartyStrength(party);
                if (diagnosticTarget != null)
                {
                    _instance.HasAssistanceEngagementStrengthAdvantage(
                        party, diagnosticTarget, out engagementStrength,
                        out _);
                }
                return "armyLeader:members=" + led.MemberPartyIds.Count +
                       ",attached=" + attached +
                       ",assembling=" + (attached < expectedAttached) +
                       ",speedDispersed=" + led.DispersedForSpeed +
                       ",speedDetached=" +
                       led.SpeedDetachedPartyIds.Count +
                       ",speedCatcher=" +
                       (led.SpeedCatcherPartyId.Length == 0
                           ? "-"
                           : led.SpeedCatcherPartyId) +
                       ",committedStrength=" +
                        GetCommittedAssistanceStrength(party, led).ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                        ",engagementStrength=" +
                        engagementStrength.ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                        ",targetStrength=" +
                       (diagnosticThreat != null
                           ? diagnosticThreat.Strength.ToString(
                               "0.00", CultureInfo.InvariantCulture)
                           : "n/a") +
                        ",targetJoiningRadius=" +
                        (diagnosticThreat?.JoiningRadius.ToString(
                             "0.00", CultureInfo.InvariantCulture) ??
                         "n/a") +
                        ",targetThreatRadius=" +
                        (diagnosticThreat?.ThreatRadius.ToString(
                             "0.00", CultureInfo.InvariantCulture) ??
                          "n/a") +
                        ",enemyMaximumSpeed=" +
                        (diagnosticThreat?.MaximumSpeed.ToString(
                             "0.00", CultureInfo.InvariantCulture) ??
                         "n/a") +
                        ",targetMovementCachedBaseSpeed=" +
                        (movementTarget?.LastCalculatedBaseSpeed.ToString(
                             "0.00", CultureInfo.InvariantCulture) ??
                         "n/a") +
                        ",targetMovementTheoreticalSpeed=" +
                        (movementTarget == null
                            ? "n/a"
                            : GetTheoreticalBaseSpeed(movementTarget).ToString(
                                "0.00", CultureInfo.InvariantCulture)) +
                        ",theoreticalLeaderSoloSpeedAtAssignment=" +
                        (diagnosticTask?.LeaderSoloSpeedAtAssignment.ToString(
                             "0.00", CultureInfo.InvariantCulture) ??
                         "n/a") +
                        ",leaderSpeedTheoretical=" +
                        (diagnosticTask?.HasTheoreticalLeaderSoloSpeedAtAssignment
                             == true) +
                        ",fastestEnemyGroup=" +
                        (diagnosticThreat == null ||
                         diagnosticThreat.FastestCombatGroupId.Length == 0
                            ? "-"
                            : diagnosticThreat.FastestCombatGroupId) +
                        ",targetCombatGroups=" +
                       (diagnosticThreat == null
                           ? "n/a"
                           : string.Join("|",
                               diagnosticThreat.CombatGroups)) +
                       ",supports=" + supportCount +
                        ",attachedSupports=" + attachedSupportCount +
                        ",armyKingdom=" + (army?.Kingdom?.StringId ?? "-") +
                        ",target=" + led.TargetPartyId +
                        ",movementTarget=" +
                        (movementTarget?.StringId ?? "-") +
                        ",contactParty=" + contactParty.StringId +
                        ",contactDistance=" +
                        (contactDistance == float.MaxValue
                            ? "n/a"
                            : contactDistance.ToString(
                                "0.00", CultureInfo.InvariantCulture));
            }
            foreach (LordAssistanceGroup group in _instance._assistanceGroups.Values)
                if (group.MemberPartyIds.Contains(party.StringId,
                        StringComparer.OrdinalIgnoreCase))
                {
                    MobileParty? leader = FindActiveParty(group.LeaderPartyId);
                    string distance = leader == null ? "n/a" :
                        party.GetPosition2D.Distance(leader.GetPosition2D).ToString("0.00");
                    return "armyMember:leader=" + group.LeaderPartyId +
                           ",inArmy=" + (party.Army?.LeaderParty == leader) +
                           ",attached=" + (party.AttachedTo == leader) +
                           ",speedDispersed=" + group.DispersedForSpeed +
                           ",speedDetached=" +
                           IsSpeedDetached(group, party.StringId) +
                           ",distance=" + distance + ",target=" + group.TargetPartyId;
                }
            if (party.Army?.LeaderParty != null &&
                _instance._assistanceGroups.TryGetValue(
                    party.Army.LeaderParty.StringId, out LordAssistanceGroup? supportGroup) &&
                IsArmyOwnedByGroup(party.Army, supportGroup))
            {
                MobileParty leader = party.Army.LeaderParty;
                return "armySupport:leader=" + leader.StringId +
                       ",attached=" + (party.AttachedTo == leader) +
                       ",distance=" + party.GetPosition2D.Distance(
                           leader.GetPosition2D).ToString("0.00") +
                       ",target=" + supportGroup.TargetPartyId;
            }
            return "none";
        }

        internal static int GetActiveAssistanceTaskCount() =>
            _instance?._assistanceGroups.Values.Sum(group => group.MemberPartyIds.Count) ?? 0;

        internal static IReadOnlyList<AssistanceTaskSnapshot> GetAssistanceTaskSnapshots()
        {
            var result = new List<AssistanceTaskSnapshot>();
            if (_instance == null) return result;
            foreach (LordAssistanceGroup group in _instance._assistanceGroups.Values)
                foreach (string helperId in group.MemberPartyIds)
                    result.Add(new AssistanceTaskSnapshot
                    {
                        LeaderPartyId = group.LeaderPartyId,
                        HelperPartyId = helperId,
                        CrimeId = group.CrimeId,
                        TargetPartyId = group.TargetPartyId,
                        AssignedTime = CampaignTime.Hours((float)(
                            _instance._assistanceAssignedHours.TryGetValue(helperId,
                                out double hours) ? hours : CampaignTime.Now.ToHours))
                    });
            return result;
        }
    }
}
