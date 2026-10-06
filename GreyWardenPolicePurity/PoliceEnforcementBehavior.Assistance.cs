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

        private readonly Dictionary<string, LordAssistanceGroup> _assistanceGroups =
            new Dictionary<string, LordAssistanceGroup>(StringComparer.OrdinalIgnoreCase);
        // 协力组连续多少轮被判定为战力富余。放人要求连续成立，见 TryShrinkAssistanceGroup。
        // 不存档：只是防抖计数，读档后从零数起，最多晚几个小时放人。
        private readonly Dictionary<string, int> _assistanceSurplusTicks =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // 凑不出兵而退回台账的案子，在这里记一个时刻做冷却。
        private readonly Dictionary<string, double> _caseStrengthCooldownHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _assistanceAssignedHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _playerBountyEscortGroups =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>能参加协力的灰袍领主队：部队与领主都还在场。</summary>
        private static bool IsActiveGreyWardenLordParty(MobileParty? party)
        {
            return party?.IsActive == true && party.LeaderHero?.IsActive == true &&
                   GwpCommon.IsGreyWardenLordParty(party);
        }

        private static bool CanContinueLeadingPoliceTask(MobileParty? party) =>
            IsActiveGreyWardenLordParty(party) &&
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
            _playerBountyEscortGroups.Clear();
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
                         .Where(IsActiveGreyWardenLordParty)
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

                    if (task?.IsPlayerBountyEscort == true)
                    {
                        MaintainAssistancePlayerBountyEscort(leader, group);
                        continue;
                    }

                    ExitAssistancePlayerBountyEscort(leader, group);

                    MobileParty? activeTarget =
                        GetActiveAssistanceCaseTarget(group, task);
                    if (activeTarget == null)
                        continue;
                    group.TargetPartyId = activeTarget.StringId;

                    GwpStrength.Threat targetThreat =
                        GwpStrength.EnemyAround(leader, activeTarget);
                    MobileParty movementTarget =
                        GwpStrength.CombatGroupOf(activeTarget);
                    float targetMovementSpeed =
                        GwpStrength.TheoreticalBaseSpeed(movementTarget);
                    float targetStrength = targetThreat.Strength;
                    // 目标战力是现场战斗群的和，会随旁边的人来去而变。编成必须
                    // 跟着变，否则路人一走就变成五百打一百。
                    TryShrinkAssistanceGroup(leader, group, targetStrength);
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
                        GwpStrength.EnemyAround(leader, activeTarget);
                    targetStrength = targetThreat.Strength;
                    movementTarget =
                        GwpStrength.CombatGroupOf(activeTarget);
                    targetMovementSpeed =
                        GwpStrength.TheoreticalBaseSpeed(movementTarget);
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

                    float assembledStrength = GwpStrength.CombatGroup(army.LeaderParty);
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

                float leaderStrength = GwpStrength.Party(leader);
                float initialTargetStrength =
                    GwpStrength.EnemyAround(leader, offender).Strength;
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
                    // A player commission is the player's to abandon, not ours.
                    // Too weak today simply means no war is declared yet.
                    if (task.IsPlayerBountyEscort)
                    {
                        GwpAiDiagnostics.WriteAction(leader,
                            "PLAYER_BOUNTY_SUPPORT_FORCE_EXHAUSTED",
                            FormatStrengthDiagnostic(leader, offender,
                                GetCommittedAssistanceStrength(leader, newGroup),
                                initialTargetStrength) + "; commissionKept=True");
                        continue;
                    }
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
                task.IsEscortingPlayer ||
                task.TargetCrime?.HasOpenCase != true)
                return null;

            // A player commission raises a force by the ordinary rules, but only
            // once the request for help has actually been delivered.
            if (task.IsPlayerBountyEscort &&
                PlayerBountyBehavior.AwaitingSupportRequest(task))
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

        private static bool IsAssistancePlayerBountyEscort(
            LordAssistanceGroup group)
        {
            PoliceTask? task = CrimeState.GetTask(group.LeaderPartyId);
            return task?.IsPlayerBountyEscort == true &&
                   IsAssistanceGroupCaseStillActive(group, task);
        }

        private void MaintainAssistancePlayerBountyEscort(
            MobileParty leader, LordAssistanceGroup group)
        {
            MobileParty player = MobileParty.MainParty;
            if (player?.IsActive != true)
                return;

            Army? army = group.DispersedForSpeed
                ? null
                : MaintainAssistanceArmy(leader, group);
            GrowPlayerBountyAssistanceStrength(leader, group, army);
            if (!_playerBountyEscortGroups.Add(group.LeaderPartyId))
                return;

            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
            foreach (string memberId in group.MemberPartyIds)
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                    FindActiveParty(memberId));

            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_PLAYER_BOUNTY_ESCORT_STARTED",
                "members=" + group.MemberPartyIds.Count +
                "; speedDispersed=" + group.DispersedForSpeed +
                "; armyActive=" + (army?.LeaderParty == leader));
        }

        /// <summary>
        /// 求援送到之后，按普通案件那一套第一层判定继续征调灰袍，直到已承诺战力
        /// 高于目标区域战力。与普通案件唯一的差别是：全部合格力量仍然不够时，
        /// 不把玩家的委托作废——玩家的案子由玩家自己决定要不要继续。
        /// </summary>
        private void GrowPlayerBountyAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group, Army? army) =>
            GwpRuntimeFaultWatch.Guard("PLAYER_BOUNTY_SUPPORT_GROWTH",
                () => AdvancePlayerBountyAssistanceStrength(leader, group, army));

        private void AdvancePlayerBountyAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group, Army? army)
        {
            PoliceTask? task = CrimeState.GetTask(leader.StringId);
            if (task == null || PlayerBountyBehavior.AwaitingSupportRequest(task))
                return;

            MobileParty? offender = GetActiveAssistanceCaseTarget(group, task);
            // 没有真实军团时不做增员：TryAddAssistanceMember 要往军团里塞人。
            if (offender == null || army?.LeaderParty == null)
                return;

            group.TargetPartyId = offender.StringId;
            float targetStrength =
                GwpStrength.EnemyAround(leader, offender).Strength;
            if (EnsureCommittedStrengthAdvantage(
                    leader, task, offender, group, army, targetStrength))
                return;

            GwpAiDiagnostics.WriteAction(leader,
                "PLAYER_BOUNTY_SUPPORT_FORCE_EXHAUSTED",
                FormatStrengthDiagnostic(leader, offender,
                    GetCommittedAssistanceStrength(leader, group),
                    targetStrength) +
                "; commissionKept=True");
        }

        private void ExitAssistancePlayerBountyEscort(
            MobileParty leader, LordAssistanceGroup group)
        {
            if (!_playerBountyEscortGroups.Remove(group.LeaderPartyId))
                return;

            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
            foreach (string memberId in group.MemberPartyIds)
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(
                    FindActiveParty(memberId));

            GwpAiDiagnostics.WriteAction(leader,
                "ASSISTANCE_PLAYER_BOUNTY_ESCORT_ENDED",
                "members=" + group.MemberPartyIds.Count +
                "; speedDispersed=" + group.DispersedForSpeed);
        }

        internal static void RefreshPlayerBountyAssistanceEscort(
            string leaderPartyId)
        {
            if (_instance == null ||
                string.IsNullOrWhiteSpace(leaderPartyId) ||
                !_instance._assistanceGroups.TryGetValue(
                    leaderPartyId, out LordAssistanceGroup? group))
                return;

            MobileParty? leader = FindActiveParty(group.LeaderPartyId);
            if (leader == null)
                return;

            if (IsAssistancePlayerBountyEscort(group))
                _instance.MaintainAssistancePlayerBountyEscort(leader, group);
            else
                _instance.ExitAssistancePlayerBountyEscort(leader, group);
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
            float soloSpeed = GwpStrength.TheoreticalBaseSpeed(leader);
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

        private static float GetCommittedAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group)
        {
            float strength = GwpStrength.Party(leader);
            foreach (string memberId in group.MemberPartyIds.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                MobileParty? member = FindActiveParty(memberId);
                if (IsActiveGreyWardenLordParty(member))
                    strength += GwpStrength.Party(member!);
            }
            return Math.Max(0f, strength);
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
                candidates.Sum(GwpStrength.Party);
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
            GwpStrength.Threat threat =
                GwpStrength.EnemyAround(observer, offender);
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

        /// <summary>
        /// 灰袍凑不出足够兵力办这宗案子。
        ///
        /// 这**不是**案件了结：人没被抓，部队也没被打完，只是我们这一轮不够强。
        /// 所以案卷必须留在台账上（`ReleaseTasksForOffender` 撤承办、不销卷），
        /// 等我方变强或目标身边的人散了再重新指派；以前这里直接 `EndTask`，
        /// 连案卷一起销毁——实机抓到过一宗 `civilianCasualties=497` 的案子就是
        /// 这么凭空消失的。
        ///
        /// 另外：目标正在打的时候不判失败。那一仗的结果才是答案，中途撤案会把
        /// 已经咬上去的截击队和承办人一起拆散。
        /// </summary>
        private void FailAssistanceCase(MobileParty leader, PoliceTask task,
            LordAssistanceGroup? group, float targetStrength, string reason)
        {
            MobileParty? offenderParty = task.TargetCrime?.Offender;
            if (offenderParty?.MapEvent != null || leader.MapEvent != null)
                return;

            IFaction? warTarget = task.WarTarget ??
                task.TargetCrime?.Offender?.ActualClan?.MapFaction;
            float committedStrength = group == null
                ? GwpStrength.Party(leader)
                : GetCommittedAssistanceStrength(leader, group);
            float maximumStrength = committedStrength +
                GetAvailableAssistanceCandidates(leader)
                    .Sum(GwpStrength.Party);
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

            EndCaseTask(leader.StringId, CaseTaskEnd.Released, "assistance_strength_insufficient");
            MarkCaseStrengthCooldown(task.TargetCrimeId);
            // 任务上没记宣战对象时（宣战可能来自延迟纠察队），按目标所在势力再查一次。
            RestorePeaceWithoutEnforcementReason(warTarget);
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
            GwpStrength.Threat targetThreat)
        {
            int supportCount = army.Parties.Count(party =>
                GwpCommon.IsEnforcementDelayPatrolParty(party));
            float armySpeed = leader.LastCalculatedBaseSpeed;
            float offenderSpeed = offender.LastCalculatedBaseSpeed;
            MobileParty speedTarget =
                GwpStrength.CombatGroupOf(offender);
            float targetMovementSpeed =
                GwpStrength.TheoreticalBaseSpeed(speedTarget);
            group.DispersedForSpeed = true;
            group.LastArmySpeedAtDispersal = Math.Max(
                0f, task.LeaderSoloSpeedAtAssignment);
            group.SpeedDetachedPartyIds.Clear();
            group.SpeedCatcherPartyId = string.Empty;

            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsActiveGreyWardenLordParty(member))
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
                GwpAssistanceArmyDisbandGuardPatch
                    .ApplyAuthorizedObjectiveFinished(army);
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
                if (!IsActiveGreyWardenLordParty(member))
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
                GwpAssistanceArmyDisbandGuardPatch
                    .ApplyAuthorizedObjectiveFinished(army);
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
                if (!IsActiveGreyWardenLordParty(member))
                {
                    RemoveAssistanceMember(group, memberId);
                    continue;
                }

                RequestAssistanceTargetIntent(
                    member!, group, offender, 0.99f);
            }
            return army;
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
                GwpStrength.CombatGroupOf(offender);
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

            // This is Bannerlord's real Army object. A null Kingdom keeps the
            // independent Grey Warden clan independent; no hidden faction is made.
            return new Army(null, leader, Army.ArmyTypes.Patrolling);
        }

        private Army? MaintainAssistanceArmy(MobileParty leader,
            LordAssistanceGroup group)
        {
            Army? army = CreateOrRecoverAssistanceArmy(leader);
            if (army == null) return null;

            foreach (string memberId in group.MemberPartyIds.ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsActiveGreyWardenLordParty(member))
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

        /// <summary>
        /// 全家族此刻最多能凑出来的战力：承办人自己，加上所有还能被拉进协力组的
        /// 灰袍领主。用于接案门槛——超过这个数的案子接了也办不成，只会被反复接起
        /// 又反复因凑不出兵退回台账。
        /// </summary>
        internal static float GetMaximumMusterableStrength(MobileParty? leader)
        {
            if (_instance == null || leader?.IsActive != true) return 0f;
            return GwpStrength.Party(leader) +
                   _instance.GetAvailableAssistanceCandidates(leader)
                       .Sum(GwpStrength.Party);
        }

        private bool IsAvailableAssistanceCandidate(MobileParty? candidate, MobileParty leader)
        {
            return IsActiveGreyWardenLordParty(candidate) && candidate != leader &&
                   candidate!.MapEvent == null && candidate.LeaderHero?.IsPrisoner != true &&
                   candidate.Army == null &&
                   GwpWardenDuties.IsFreeFor(candidate, WardenDuty.Assistance);
        }

        private void ReleaseCandidateCaseToPool(MobileParty helper)
        {
            EndCaseTask(helper.StringId, CaseTaskEnd.Released, "helper_released_to_assistance");
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
                ? Campaign.Current.Models.EncounterModel
                    .MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy
                : Campaign.Current.Models.EncounterModel
                    .MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;
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

                FailTaskBecauseOwnerCannotLead(ownerId,
                    "owner_cannot_lead_after_map_event");
            }
        }

        private void FailTaskBecauseOwnerCannotLead(string ownerId, string reason) =>
            EndCaseTask(ownerId, CaseTaskEnd.Closed, reason);

        /// <summary>
        /// 把一支灰袍队伍从它当前所有的协力职责里摘出来：组长就解散自己那一组，协办人就
        /// 退出所在组并脱离军团。玩家的求援要指派谁，就得先让他从别人的案子上抽身，
        /// 否则他的欲望仍旧指向原来那个目标，看上去就是"接了跟随却不跟玩家"。
        /// </summary>
        internal static void ReleasePartyFromAssistance(string? partyId, string reason)
        {
            if (_instance == null || string.IsNullOrWhiteSpace(partyId)) return;

            if (_instance._assistanceGroups.ContainsKey(partyId!))
                _instance.ReleaseAssistanceGroup(partyId!, reason);

            foreach (LordAssistanceGroup group in _instance._assistanceGroups.Values.ToList())
            {
                if (!group.MemberPartyIds.Contains(partyId!, StringComparer.OrdinalIgnoreCase) &&
                    !group.SpeedDetachedPartyIds.Contains(partyId!, StringComparer.OrdinalIgnoreCase))
                    continue;
                _instance.RemoveAssistanceMember(group, partyId!);
                MobileParty? releasedParty = FindActiveParty(partyId!);
                if (releasedParty != null)
                    GwpAiDiagnostics.WriteAction(releasedParty, "ASSISTANCE_MEMBER_RELEASED",
                        "leader=" + group.LeaderPartyId + "; reason=" + reason);
            }

            MobileParty? party = FindActiveParty(partyId!);
            if (party == null) return;
            if (party.Army != null && party.Army.LeaderParty != party) party.Army = null;
            GreyWardenPartyDesireBehavior.ClearIntent(party);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
        }

        /// <summary>
        /// 按目标当前的现场战力回收多余协办人。
        ///
        /// `GwpStrength.EnemyAround` 算的是"真打起来现场会来多少人"：以目标为
        /// 圆心，内圈 `joiningRadius` 全额计入、外圈到 `threatRadius` 按距离衰减。所以一个
        /// 路过的领主就能把目标战力抬高一倍，协力组因此成立。以前这个编成一旦定下就
        /// 只增不减（"never shrinks when the target weakens"），那个人走了之后就会出现
        /// 五百打一百、还把好几名灰袍从别的案子上拖住的局面。
        ///
        /// 回滞是刻意的：入组看 `committed &lt;= target`，放人要求放完之后仍然
        /// `committed &gt; target * AssistanceReleaseMargin`。两个门槛之间留一档，
        /// 路人来回走才不会让协力组跟着拆装。每轮最多放一个，且有最短在编时长。
        /// </summary>
        private void TryShrinkAssistanceGroup(MobileParty leader,
            LordAssistanceGroup group, float targetStrength)
        {
            if (group.MemberPartyIds.Count == 0 || leader?.IsActive != true ||
                leader.MapEvent != null || targetStrength <= 0f)
                return;

            float committed = GetCommittedAssistanceStrength(leader, group);
            float keepAbove =
                targetStrength * GwpTuning.Enforcement.AssistanceReleaseMargin;
            if (committed <= keepAbove)
            {
                _assistanceSurplusTicks.Remove(leader.StringId);
                return;
            }

            // 目标战力是现场战斗群之和，会随身边的人来去而抖。只看单轮就放人，会
            // 变成一个路过的领主走两步、协力组就拉一个放一个。要求富余连续成立
            // 若干轮才动手，抖动自然被滤掉。
            _assistanceSurplusTicks.TryGetValue(leader.StringId, out int surplusTicks);
            surplusTicks++;
            _assistanceSurplusTicks[leader.StringId] = surplusTicks;
            if (surplusTicks < GwpTuning.Enforcement.AssistanceSurplusConfirmTicks)
                return;

            double now = CampaignTime.Now.ToHours;
            MobileParty? offender = FindActiveParty(group.TargetPartyId);
            MobileParty? movementTarget = offender == null
                ? null
                : GwpStrength.CombatGroupOf(offender);

            MobileParty? release = null;
            float releaseStrength = 0f;
            float releaseDistance = float.MinValue;

            foreach (string memberId in group.MemberPartyIds
                         .Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            {
                MobileParty? member = FindActiveParty(memberId);
                // 正在打的人不能抽走，抽走等于把他从战斗里拔出来。
                if (!IsActiveGreyWardenLordParty(member) || member!.MapEvent != null)
                    continue;
                if (_assistanceAssignedHours.TryGetValue(memberId, out double joined) &&
                    now - joined < GwpTuning.Enforcement.AssistanceMinimumMemberHours)
                    continue;

                float strength = GwpStrength.Party(member);
                if (committed - strength <= keepAbove) continue;

                // 放最远的那个：他对接下来这一仗贡献最小，被硬拖过半张地图的
                // 观感也最差。
                float distance = movementTarget == null
                    ? 0f
                    : member.GetPosition2D.Distance(movementTarget.GetPosition2D);
                if (release != null && distance <= releaseDistance) continue;
                release = member;
                releaseStrength = strength;
                releaseDistance = distance;
            }

            if (release == null) return;

            // 放完一个就把计数清零：下一个还要再等满一轮确认，不会一口气拆空。
            _assistanceSurplusTicks.Remove(leader.StringId);

            Army? army = leader.Army;
            if (army != null && release.Army == army)
                release.Army = null;
            RemoveAssistanceMember(group, release.StringId);
            GreyWardenPartyDesireBehavior.ClearIntent(release);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(release);
            GwpAiDiagnostics.WriteAction(release, "ASSISTANCE_MEMBER_RELEASED_SURPLUS",
                "leader=" + leader.StringId +
                "; target=" + (movementTarget?.StringId ?? group.TargetPartyId) +
                "; targetStrength=" + targetStrength.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; committedBefore=" + committed.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; releasedStrength=" + releaseStrength.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; committedAfter=" +
                    GetCommittedAssistanceStrength(leader, group).ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                "; keepAbove=" + keepAbove.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; distanceToTarget=" + releaseDistance.ToString(
                    "0.00", CultureInfo.InvariantCulture) +
                "; remainingMembers=" + group.MemberPartyIds.Count +
                "; surplusTicks=" + GwpTuning.Enforcement.AssistanceSurplusConfirmTicks);
            CrimeState.RefreshAccepting();
        }

        /// <summary>
        /// 凑不出兵的案子退回台账之后，下一小时很可能又被指派给同样凑不出兵的人。
        /// 冷却期让它先在台账上躺一阵，等我方变强、或目标身边那几个人散了再说。
        /// 只活在本次运行内：读档后最多多试一次，无害。
        /// </summary>
        private void MarkCaseStrengthCooldown(string? crimeId)
        {
            if (string.IsNullOrWhiteSpace(crimeId)) return;
            _caseStrengthCooldownHours[crimeId!] = CampaignTime.Now.ToHours;
        }

        internal static bool IsCaseUnderStrengthCooldown(CrimeRecord? crime)
        {
            if (_instance == null || crime == null ||
                string.IsNullOrWhiteSpace(crime.CrimeId))
                return false;
            return _instance._caseStrengthCooldownHours.TryGetValue(
                       crime.CrimeId, out double failedAt) &&
                   CampaignTime.Now.ToHours - failedAt <
                       GwpTuning.Enforcement.AssistanceFailureCooldownHours;
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
                GwpCommon.TryResetAi(leader);
            _playerBountyEscortGroups.Remove(leaderId);
            _assistanceSurplusTicks.Remove(leaderId);
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
                catch (Exception gwpQuietFailure) { /* 依据：实机诊断 2026-09-26（原版 Army.DisperseInternal 抛 NullReferenceException） */ GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
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
                ReleaseAssistanceGroup(leaderId!, "player_request_priority");
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

        private static readonly System.Reflection.FieldInfo? ArmyHourlyTickField =
            HarmonyLib.AccessTools.Field(typeof(Army), "_hourlyTickEvent");
        private static readonly System.Reflection.MethodInfo? ArmyOnAfterLoad =
            HarmonyLib.AccessTools.Method(typeof(Army), "OnAfterLoad");

        /// <summary>
        /// 原版读档只对王国军团调 <c>Army.OnAfterLoad</c>（<c>Campaign.InitializeCampaignObjectsOnAfterLoad</c>
        /// 遍历 <c>kingdom.Armies</c>），由它重建不入档的两个定时事件。协力军团不属于任何王国，
        /// 读档后没有定时事件：成员并不进军团，解散时 <c>DisperseInternal</c> 删除定时事件撞空引用。
        /// 这里补上同一步。
        /// </summary>
        private void RestoreEnforcementArmyTicks()
        {
            if (ArmyHourlyTickField == null || ArmyOnAfterLoad == null) return;
            foreach (MobileParty party in MobileParty.All)
            {
                Army? army = party.Army;
                if (army == null || army.LeaderParty != party || army.Kingdom != null ||
                    !GwpCommon.IsGreyWardenClanParty(party))
                    continue;
                if (ArmyHourlyTickField.GetValue(army) == null)
                    ArmyOnAfterLoad.Invoke(army, null);
            }
        }

        internal static bool IsActiveAssistanceArmy(Army? army)
        {
            if (_instance == null || army?.LeaderParty == null || army.Kingdom != null)
                return false;
            return _instance._assistanceGroups.TryGetValue(
                       army.LeaderParty.StringId, out LordAssistanceGroup? group) &&
                   IsArmyOwnedByGroup(army, group);
        }

        internal static bool TryGetAssistanceDuty(MobileParty? party,
            out MobileParty? target, out AiBehavior behavior,
            out bool playerBountyEscort, out bool offenderPursuit)
        {
            target = null;
            behavior = AiBehavior.None;
            playerBountyEscort = false;
            offenderPursuit = false;
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
            playerBountyEscort = IsAssistancePlayerBountyEscort(group);
            if (playerBountyEscort)
            {
                // Support always moves with the player, declared war or not. The
                // player finds the offender; the Wardens are there to fight beside
                // him when he does, not to run the pursuit themselves.

                target = !isLeader && !speedDetached
                    ? FindActiveParty(group.LeaderPartyId)
                    : MobileParty.MainParty;
                behavior = AiBehavior.EscortParty;
                return target?.IsActive == true && target != party;
            }

            if (!isLeader && !speedDetached)
            {
                target = FindActiveParty(group.LeaderPartyId);
                behavior = AiBehavior.EscortParty;
                return target != null && target != party;
            }

            MobileParty? offender = FindActiveParty(group.TargetPartyId);
            if (offender == null)
                return false;

            target = GwpStrength.CombatGroupOf(offender);
            // Native GoAroundParty is the lord AI's own keep-out pursuit. Its
            // short-term implementation first seeks the farthest valid point in
            // the native outer ring, while initiative remains free to engage or
            // flee after diplomacy changes.
            //
            // 那个环实测就是 `EncounterJoiningRadius(3.0) * 1.15` 的平方半径，
            // 即 5.95 —— 走到环上这支队伍就站住了，够不着 WarDistance 的宣战判定。
            // 所以未宣战、且本队正独自行动（尚未组成军团，或已因速度分散脱离军团）
            // 时改用原版跟随：它直接指向目标真实位置，同样不含交战语义。已经以军团
            // 整体行军的组长仍走 GoAroundParty（宣战判定另有配套的环距离），宣战后
            // 也一律换回 GoAroundParty，接战交还原版 initiative。
            offenderPursuit = true;
            behavior = IsUndeclaredSoloAssistancePursuit(party, group, target)
                ? AiBehavior.EscortParty
                : AiBehavior.GoAroundParty;
            return target != null && target != party;
        }

        /// <summary>
        /// 本队此刻正独自追一个尚未宣战的目标：没有军团阵型要维持，目标也没有
        /// 躲进定居点。这是唯一允许用原版跟随代替 <see cref="AiBehavior.GoAroundParty"/>
        /// 的局面——跟随会一路跟进聚落，承办人会在城里满足宣战距离并可能当场被俘，
        /// 所以目标进城后必须退回既有的环外围堵与驱逐流程。
        /// </summary>
        private static bool IsUndeclaredSoloAssistancePursuit(MobileParty party,
            LordAssistanceGroup group, MobileParty? movementTarget)
        {
            return party.Army == null &&
                   CrimeState.GetTask(group.LeaderPartyId)?.WarDeclared != true &&
                   !GwpCommon.IsShelteredOffender(movementTarget);
        }

        private float GetAssistanceContactDistance(MobileParty leader,
            MobileParty offender, out MobileParty contactParty)
        {
            MobileParty movementTarget =
                GwpStrength.CombatGroupOf(offender);
            contactParty = leader;
            float closestDistance = leader.GetPosition2D.Distance(
                movementTarget.GetPosition2D);

            if (!_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group))
                return closestDistance;

            foreach (string memberId in group.MemberPartyIds)
            {
                MobileParty? member = FindActiveParty(memberId);
                if (!IsActiveGreyWardenLordParty(member))
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
                GwpStrength.EnemyAround(leader, offender).Strength;
            engagementStrength = GwpStrength.Party(leader);
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
            out GwpStrength.LocalBalance prediction)
        {
            // On a player commission the player himself is part of the force that
            // will engage, so he must count toward the local friendly strength.
            bool includePlayer =
                CrimeState.GetTask(leader.StringId)?.IsPlayerBountyEscort == true;
            var candidates = new List<MobileParty>();
            if (_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group) &&
                group.DispersedForSpeed)
            {
                candidates.Add(leader);
                candidates.AddRange(group.MemberPartyIds
                    .Select(FindActiveParty)
                    .Where(IsActiveGreyWardenLordParty)
                    .Cast<MobileParty>());
            }
            else
            {
                // A formed assistance army is one native combat group. Only
                // its leader owns the initiative decision; a helper still
                // travelling to the army must not trigger an early war.
                candidates.Add(leader);
            }

            GwpStrength.LocalBalance? closestPrediction = null;
            foreach (MobileParty candidate in candidates
                         .Distinct()
                         .OrderBy(candidate =>
                             candidate.Position.ToVec2().Distance(
                                 GwpStrength.CombatGroupOf(offender)
                                     .Position.ToVec2())))
            {
                GwpStrength.LocalBalance current =
                    GwpStrength.Compare(
                        candidate, offender, includePlayer);
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
                         GwpStrength.Compare(
                             leader, offender, includePlayer);
            return false;
        }

        private static string FormatLocalStrengthDeclarationDiagnostic(
            GwpStrength.LocalBalance prediction,
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
            if (!IsActiveGreyWardenLordParty(leader)) return false;
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
                    GwpStrength.CombatGroupOf(offender);
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
                GwpStrength.Threat? diagnosticThreat =
                    diagnosticTarget == null
                        ? null
                        : GwpStrength.EnemyAround(
                            party, diagnosticTarget);
                MobileParty contactParty = party;
                float contactDistance = diagnosticTarget == null
                    ? float.MaxValue
                    : _instance.GetAssistanceContactDistance(
                        party, diagnosticTarget, out contactParty);
                MobileParty? movementTarget = diagnosticTarget == null
                    ? null
                    : GwpStrength.CombatGroupOf(diagnosticTarget);
                PoliceTask? diagnosticTask =
                    CrimeState.GetTask(led.LeaderPartyId);
                float engagementStrength = GwpStrength.Party(party);
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
                            : GwpStrength.TheoreticalBaseSpeed(movementTarget).ToString(
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
