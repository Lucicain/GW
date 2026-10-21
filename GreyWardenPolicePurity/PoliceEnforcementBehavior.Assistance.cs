using Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
            public string PendingReleaseReason { get; set; } = string.Empty;
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
            List<string> releaseReasons = null!;
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
                releaseReasons = groups.Select(group => group.PendingReleaseReason).ToList();
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
            dataStore.SyncData("gwp_enf_assist_release_reasons", ref releaseReasons);
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
                    PendingReleaseReason = releaseReasons[index],
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

        }

        private void UpdateLordAssistance()
        {
            CleanupInvalidAssistanceGroups();
            ReconcileAssistanceFormations();
            foreach (MobileParty leader in PoliceStats.GetAllPoliceParties()
                         .Where(IsActiveGreyWardenLordParty)
                         .OrderBy(party => party.StringId, StringComparer.OrdinalIgnoreCase).ToList())
            {
                if (IsAssistanceMember(leader.StringId)) continue;
                ReleaseUnownedAssistanceArmy(leader);
                PoliceTask? task = CrimeState.GetTask(leader.StringId);
                if (task != null) TryCaptureAssistanceLeaderSoloSpeed(leader, task);
                MobileParty? offender = GetAssistanceEvaluationTarget(leader, task);
                if (offender == null || FormationInBattle(leader)) continue;

                if (!_assistanceGroups.TryGetValue(leader.StringId, out LordAssistanceGroup? group))
                {
                    // 使者有结果之前不调人（用户裁定 2026-10-16）：谈成就结案，用不着协力；谈崩了再组。
                    if (IsAwaitingEnvoyResult(leader, task!, offender))
                    {
#if GWP_DIAGNOSTICS
                        TraceAssistanceEnvoyWait(leader, task!);
#endif
                        continue;
                    }
                    group = TryFormAssistanceGroup(leader, task!, offender);
                    if (group == null) continue;
                }

                group.TargetPartyId = offender.StringId;
                if (task!.IsPlayerBountyEscort)
                {
                    MaintainAssistancePlayerBountyEscort(leader, group);
                    continue;
                }
                ExitAssistancePlayerBountyEscort(leader, group);
                // 征调、缩编只看目标自己的战斗组；周围援军只决定此刻能否宣战、接战。
                // 依据：用户裁定 2026-10-17。
                float needed = GetAssistanceRequiredStrength(offender);
                float target = GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(offender));
                TryShrinkAssistanceGroup(leader, group, needed);
                if (!ReconcileAssistanceFormation(leader, group)) continue;
                Army? existing = group.DispersedForSpeed ? null : leader.Army;
                if (!EnsureCommittedStrengthAdvantage(leader, task, offender, group, existing, needed))
                {
                    FailAssistanceCase(leader, task, group, needed, "no_remaining_eligible_force");
                    continue;
                }
                if (!ReconcileAssistanceFormation(leader, group)) continue;

                MobileParty movementTarget = GwpStrength.CombatGroupOf(offender);
                float targetSpeed = GwpStrength.TheoreticalBaseSpeed(movementTarget);
                if (group.DispersedForSpeed)
                {
                    MaintainSpeedDispersedPursuit(leader, group, offender);
                    if (!CanReformAssistanceArmyAfterSpeedDispersal(task, targetSpeed)) continue;
                    group.DispersedForSpeed = false;
                    group.SpeedDetachedPartyIds.Clear();
                    group.SpeedCatcherPartyId = string.Empty;
#if GWP_DIAGNOSTICS
                    GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_SPEED_REFORMING",
                        "target=" + offender.StringId + "; localStrengthReady=True");
#endif
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
                }
                Army? army = MaintainAssistanceArmy(leader, group);
                if (army == null) continue;
                if (ShouldDisperseAssistanceArmyForSpeed(army, task, targetSpeed))
                    DisperseAssistanceArmyForSpeed(leader, group, offender, army, task,
                        GetCommittedAssistanceStrength(leader, group), target);
            }
        }

        /// <summary>
        /// 这宗案子还在等使者的结果：使者在路上、宽限或约了比武，或者还没派、但承办人派得出（他在打仗时
        /// 仗打完就派）。判断和 <see cref="HoldWarForEnvoy"/> 同一套：那边不宣战的时候，这边也不调人。
        /// 已宣战的、玩家的、玩家请来支援的案子不派使者，不等。
        /// </summary>
        private bool IsAwaitingEnvoyResult(MobileParty leader, PoliceTask task, MobileParty offender)
        {
            CrimeRecord? crime = task.TargetCrime;
            if (task.WarDeclared || task.IsPlayerBountyEscort || offender.IsMainParty || crime == null)
                return false;
            if (_envoyCases.TryGetValue(crime.CrimeId, out EnvoyCase? envoyCase))
                return envoyCase.Stage != EnvoyStage.Failed;
            return leader.MapEvent != null || EnvoyBlocker(leader, offender) == null;
        }

#if GWP_DIAGNOSTICS
        // 组长 → 上一次记下的等待内容。只在变化时写一行，不进存档。
        private readonly Dictionary<string, string> _assistanceEnvoyWaitTrace =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private void TraceAssistanceEnvoyWait(MobileParty leader, PoliceTask task)
        {
            string stage = _envoyCases.TryGetValue(task.TargetCrimeId, out EnvoyCase? envoyCase)
                ? envoyCase.Stage.ToString()
                : "not_sent";
            string current = task.TargetCrimeId + ":" + stage;
            if (_assistanceEnvoyWaitTrace.TryGetValue(leader.StringId, out string? last) && last == current) return;
            _assistanceEnvoyWaitTrace[leader.StringId] = current;
            GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_WAITING_FOR_ENVOY",
                "crime=" + task.TargetCrimeId + "; envoyStage=" + stage +
                "; leaderStrength=" + GwpStrength.Party(leader).ToString("0") +
                "; needed=" + GwpStrength.CaseTarget(task.TargetCrime).ToString("0"));
        }
#endif

        /// <summary>
        /// 组长自己打不过时组一个协力组：先征调协办人，再由
        /// <see cref="MaintainAssistanceArmy"/> 建军团。征不到人时不登记空组（玩家委托照留，下一小时再试），
        /// 以免每小时建了又拆、反复重新竞价。凑不出兵的普通案件按原规则退回台账；他正在打仗时
        /// <see cref="FailAssistanceCase"/> 先不判（等那一仗的结果），这时同样不留空组。
        /// </summary>
        private static float GetAssistanceRequiredStrength(MobileParty offender) =>
            GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(offender));

        private LordAssistanceGroup? TryFormAssistanceGroup(MobileParty leader, PoliceTask task,
            MobileParty offender)
        {
            if (!TryCaptureAssistanceLeaderSoloSpeed(leader, task)) return null;
            float targetStrength = GetAssistanceRequiredStrength(offender);
            var group = new LordAssistanceGroup
            {
                LeaderPartyId = leader.StringId,
                CrimeId = task.TargetCrimeId,
                TargetPartyId = offender.StringId
            };
            float initialStrength = task.IsPlayerBountyEscort
                ? GetCommittedAssistanceStrength(leader, group)
                : GwpStrength.Party(leader);
            if (initialStrength >= targetStrength)
                return null;

            _assistanceGroups[leader.StringId] = group;
            if (!EnsureCommittedStrengthAdvantage(leader, task, offender, group, null, targetStrength) &&
                !task.IsPlayerBountyEscort)
            {
                FailAssistanceCase(leader, task, group, targetStrength,
                    "no_remaining_eligible_force_at_assignment");
                // 判了就已经随案收尾；先不判时组还登记着，下面按有没有人决定留不留。
                if (!_assistanceGroups.TryGetValue(leader.StringId, out LordAssistanceGroup? kept) || kept != group)
                    return null;
            }
            if (!HasFormationParticipants(group))
            {
                _assistanceGroups.Remove(leader.StringId);
                return null;
            }
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
            return group;
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

            GrowPlayerBountyAssistanceStrength(leader, group, group.DispersedForSpeed ? null : leader.Army);
            if (!ReconcileAssistanceFormation(leader, group)) return;
            Army? army = group.DispersedForSpeed
                ? null
                : MaintainAssistanceArmy(leader, group);
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
        /// 求援送到之后，把玩家实际战斗组计入承诺力量，再按第一层判定征调灰袍，
        /// 直到已承诺战力高于目标本体战力。全部合格力量仍然不够时，
        /// 不把玩家的委托作废——玩家的案子由玩家自己决定要不要继续。
        /// </summary>
        private void GrowPlayerBountyAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group, Army? army) =>
            AdvancePlayerBountyAssistanceStrength(leader, group, army);

        private void AdvancePlayerBountyAssistanceStrength(
            MobileParty leader, LordAssistanceGroup group, Army? army)
        {
            PoliceTask? task = CrimeState.GetTask(leader.StringId);
            if (task == null || PlayerBountyBehavior.AwaitingSupportRequest(task))
                return;

            MobileParty? offender = GetActiveAssistanceCaseTarget(group, task);
            if (offender == null)
                return;

            group.TargetPartyId = offender.StringId;
            float targetStrength = GetAssistanceRequiredStrength(offender);
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
            var parties = new HashSet<MobileParty> { leader };
            foreach (string memberId in group.MemberPartyIds.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                MobileParty? member = FindActiveParty(memberId);
                if (IsActiveGreyWardenLordParty(member))
                    parties.Add(member!);
            }
            if (_instance != null)
                parties.UnionWith(_instance.FormationSupports(group));

            float playerStrength = 0f;
            PoliceTask? task = CrimeState.GetTask(leader.StringId);
            MobileParty player = MobileParty.MainParty;
            if (task?.IsPlayerBountyEscort == true &&
                !PlayerBountyBehavior.AwaitingSupportRequest(task) &&
                player?.IsActive == true && player.LeaderHero?.IsPrisoner != true)
            {
                // 玩家求援时承诺参战；只算实际战斗组，未挂靠的军团成员不算。
                // 这不是现场力量，宣战仍由原版区域比较另行确认。
                MobileParty playerGroup = GwpStrength.CombatGroupOf(player);
                playerStrength = GwpStrength.CombatGroup(playerGroup);
                parties.RemoveWhere(party => GwpStrength.CombatGroupOf(party) == playerGroup);
            }
            return Math.Max(0f, parties.Sum(GwpStrength.Party) + playerStrength);
        }

        private List<MobileParty> GetAvailableAssistanceCandidates(
            MobileParty leader)
        {
            return PoliceStats.GetAllPoliceParties()
                .Where(candidate => IsAvailableAssistanceCandidate(candidate, leader))
                .Select(candidate => new { Party = candidate, Hours = GetAssistanceTravelHours(candidate, leader) })
                .Where(candidate => candidate.Hours <= CampaignTime.HoursInDay * GwpTuning.Enforcement.AssistanceMaximumTravelDays)
                .OrderBy(candidate => candidate.Hours)
                .ThenBy(candidate => candidate.Party.StringId, StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate.Party)
                .ToList();
        }

        private static float GetAssistanceTravelHours(MobileParty candidate, MobileParty leader)
        {
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(candidate, leader,
                out MobileParty.NavigationType navigation, out float distance);
            float speed = GwpStrength.TheoreticalBaseSpeed(candidate);
            return navigation == MobileParty.NavigationType.None || speed <= 0.01f
                ? float.MaxValue : distance / speed;
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

            foreach (MobileParty candidate in candidates)
            {
                if (GetCommittedAssistanceStrength(leader, group) > targetStrength) break;
                if (!TryAddAssistanceMember(
                        leader, task, offender, group, army, candidate))
                    return false;
            }
            return GetCommittedAssistanceStrength(leader, group) > targetStrength;
        }

        private static string FormatStrengthDiagnostic(
            MobileParty observer, MobileParty offender,
            float ourStrength, float targetStrength)
        {
            return "actor=" + observer.StringId + "; target=" + offender.StringId +
                   "; ourStrength=" + ourStrength.ToString("0.00", CultureInfo.InvariantCulture) +
                   "; targetStrength=" + targetStrength.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 灰袍凑不出足够兵力办这宗案子。
        ///
        /// 这**不是**案件了结：人没被抓，部队也没被打完，只是我们这一轮不够强。
        /// 所以案卷必须留在台账上（`ReleaseTasksForOffender` 撤承办、不销卷），
        /// 等我方变强或目标本体战斗组变弱后再重新指派；以前这里直接 `EndTask`，
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
            if (offenderParty?.MapEvent != null || FormationInBattle(leader))
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
            GwpPeace.EndPoliceWar(warTarget);
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
            float assembledStrength, float targetStrength)
        {
            if (FormationInBattle(leader)) return;
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

                if (member!.MapEvent != null) continue;
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
            if (RequestAssistanceDutyIntent(party, priority))
                return;
            MobileParty movementTarget =
                GwpStrength.CombatGroupOf(offender);
            GreyWardenPartyDesireBehavior.RequestPursuit(
                party, movementTarget, priority);
        }

        /// <summary>
        /// 把这支队伍此刻的协力职责（会合、跟随组长或追目标，判断只在 <see cref="TryGetAssistanceDuty"/>）
        /// 落成欲望层意图。没有协力职责时返回 false，由调用方另作安排。
        /// </summary>
        private static bool RequestAssistanceDutyIntent(MobileParty party, float priority)
        {
            if (!TryGetAssistanceDuty(party, out MobileParty? dutyTarget, out AiBehavior behavior, out _, out _) ||
                dutyTarget == null)
                return false;
            if (behavior == AiBehavior.EscortParty)
                GreyWardenPartyDesireBehavior.RequestEscort(party, dutyTarget, priority);
            else
                GreyWardenPartyDesireBehavior.RequestPursuit(party, dutyTarget, priority);
            return true;
        }



        private void SendDelayPatrolsAfterSpeedDispersal(
            LordAssistanceGroup group, MobileParty offender)
        {
            foreach (MobileParty patrol in FormationSupports(group).ToList())
            {
                if (patrol.MapEvent != null) continue;
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
            if (!ReconcileAssistanceFormation(leader, group) || FormationInBattle(leader))
                return null;
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

            foreach (MobileParty support in FormationSupports(group).Where(party => party.MapEvent == null))
            {
                if (support.Army != army) support.Army = army;
                TryMergeArmyMember(army, leader, support);
            }
            return army;
        }

        private bool TryAddAssistanceMember(MobileParty leader, PoliceTask task,
            MobileParty offender, LordAssistanceGroup group, Army? army, MobileParty helper)
        {
            if (!IsAvailableAssistanceCandidate(helper, leader))
                return false;

            // 协办人手上的案子留着（用户裁定 2026-10-16）：协力期间先放着，他已派出的使者照常谈，
            // 谈成就结案；谈崩的等协力结束他接着办（UpdateTasks 里按协办人跳过）。
#if GWP_DIAGNOSTICS
            PoliceTask? kept = CrimeState.GetTask(helper.StringId);
            if (kept != null)
                GwpAiDiagnostics.WriteAction(helper, "ASSISTANCE_HELPER_CASE_KEPT",
                    "leader=" + leader.StringId + "; ownCrime=" + kept.TargetCrimeId +
                    "; ownEnvoy=" + (_envoyCases.TryGetValue(kept.TargetCrimeId, out EnvoyCase? ownEnvoy)
                        ? ownEnvoy.Stage.ToString() : "-") +
                    "; warDeclared=" + kept.WarDeclared);
#endif
            group.MemberPartyIds.Add(helper.StringId);
            group.MemberPartyIds = group.MemberPartyIds
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _assistanceAssignedHours[helper.StringId] = CampaignTime.Now.ToHours;

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
                if (group.DispersedForSpeed)
                    RequestAssistanceTargetIntent(helper, group, offender, 0.99f);
                else
                    GreyWardenPartyDesireBehavior.RequestEscort(helper, leader, 0.99f);
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

        /// <summary>他自己的使者约了比武，正在赶去或留城等赛：调不走。</summary>
        internal static bool IsPartyHeldForDuel(MobileParty party) =>
            DuelForParty(party) != null;

        private static EnvoyCase? DuelForParty(MobileParty party) =>
            _instance?._envoyCases.Values.FirstOrDefault(envoyCase =>
                (envoyCase.Stage == EnvoyStage.Duel || envoyCase.Stage == EnvoyStage.DuelDecided) &&
                string.Equals(envoyCase.WardenPartyId, party.StringId, StringComparison.OrdinalIgnoreCase));

        internal static Settlement? GetDuelVenue(MobileParty party)
        {
            EnvoyCase? duel = DuelForParty(party);
            return duel == null ? null : Settlement.Find(duel.TownId);
        }

        /// <summary>协办人离开所在的协力组和它的军团（例如他自己的使者约了比武）。</summary>
        private void LeaveAssistanceAsMember(MobileParty party, string reason)
        {
            LordAssistanceGroup? group = _assistanceGroups.Values.FirstOrDefault(candidate =>
                candidate.MemberPartyIds.Contains(party.StringId, StringComparer.OrdinalIgnoreCase));
            if (group == null) return;
            RemoveAssistanceMember(group, party.StringId);
            if (party.Army != null && party.Army.LeaderParty != party)
                party.Army = null;
            GwpAiDiagnostics.WriteAction(party, "ASSISTANCE_MEMBER_LEFT",
                "leader=" + group.LeaderPartyId + "; reason=" + reason);
        }

        /// <summary>案件台账用：这支部队正在协助哪位组长；不是协办人返回 null。</summary>
        internal static MobileParty? AssistingLeaderOf(MobileParty? party)
        {
            if (_instance == null || party == null) return null;
            LordAssistanceGroup? group = _instance._assistanceGroups.Values.FirstOrDefault(candidate =>
                candidate.MemberPartyIds.Contains(party.StringId, StringComparer.OrdinalIgnoreCase));
            return group == null ? null : FindActiveParty(group.LeaderPartyId);
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
            EndCaseTask(ownerId, CaseTaskEnd.Released, reason);

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
            _instance.ReconcileAssistanceFormations();
        }

        /// <summary>
        /// 目标本队、已挂靠军团或攻城营变弱时回收多余协办人；区域援军不参与缩编。
        /// 放人保留现有余量、连续确认与最短在编时间，每轮最多放一人。
        /// 依据：用户裁定 2026-10-17。
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

            // 补兵、伤兵恢复等也会改变本体战力，保留连续确认避免临界值反复征调。
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
        /// 冷却期内优先别的可接目标；只剩此人且本体已可应付时允许重试。
        /// 时钟由 SyncCooldownsAndRetries 入档，同版本读档继续。
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
            if (leader != null && FormationInBattle(leader) ||
                FormationParticipants(group).Any(member => member.MapEvent != null))
            {
                // 战斗期间不拆军团；待释放状态入档，战后由编队核对完成收尾。
                group.PendingReleaseReason = reason;
                return;
            }
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
            {
                GreyWardenPartyDesireBehavior.ClearIntent(leader);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(leader);
                GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_DISBANDED",
                    "reason=" + reason + "; released=" + group.MemberPartyIds.Count);
            }

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
                   IsArmyOwnedByGroup(army, group) && !group.DispersedForSpeed &&
                   IsAssistanceGroupCaseStillActive(group, CrimeState.GetTask(group.LeaderPartyId)) &&
                   _instance.HasFormationParticipants(group);
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
                        StringComparer.OrdinalIgnoreCase) ||
                    _instance.FormationSupports(candidate).Contains(party));
            }
            if (group == null || group.PendingReleaseReason.Length != 0 || !IsAssistanceGroupCaseStillActive(group, CrimeState.GetTask(group.LeaderPartyId)))
                return false;

            bool speedDetached =
                IsSpeedDetached(group, party.StringId) || group.DispersedForSpeed &&
                _instance.FormationSupports(group).Contains(party);
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

            if (!isLeader)
            {
                target = FindActiveParty(group.LeaderPartyId);
                behavior = AiBehavior.EscortParty;
                return target != null && target != party;
            }

            MobileParty? offender = FindActiveParty(group.TargetPartyId);
            if (offender == null)
                return false;

            target = GwpStrength.CombatGroupOf(offender);
            // 未宣战统一跟随真实移动组，包括其进入聚落；已宣战由原版 GoAroundParty 维持追击距离。
            offenderPursuit = true;
            behavior = IsUndeclaredAssistancePursuit(group)
                ? AiBehavior.EscortParty
                : AiBehavior.GoAroundParty;
            return target != null && target != party;
        }

        /// <summary>宣战前统一使用真实跟随，进出城交给原版行为。依据：用户裁定 2026-10-17。</summary>
        private static bool IsUndeclaredAssistancePursuit(LordAssistanceGroup group) =>
            CrimeState.GetTask(group.LeaderPartyId)?.WarDeclared != true;

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
                GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(offender));
            engagementStrength = GwpStrength.Party(leader);
            if (!_assistanceGroups.TryGetValue(leader.StringId,
                    out LordAssistanceGroup? group))
                return true;

            // 承诺战力只决定征调多少人；下面的宣战候选另外按现场实际战力判断。
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
                // 跟随可以进城；双方都回到野外才因本案宣战（用户裁定 2026-10-18）。
                if (candidate.CurrentSettlement != null || offender.CurrentSettlement != null ||
                    current.Distance > warDistance ||
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
                   "; nativeComparisons=" +
                   (prediction.NativeComparisons.Length == 0
                       ? "-"
                       : prediction.NativeComparisons) +
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
            if (group == null || group.PendingReleaseReason.Length != 0) return false;
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
            return party?.IsActive == true && target?.IsActive == true &&
                TryGetAssistanceCase(party, out _, out MobileParty? offender) &&
                (target == offender || target == GwpStrength.CombatGroupOf(offender!));
        }

        private static bool TryGetAssistanceCase(MobileParty party, out PoliceTask? task, out MobileParty? offender)
        {
            task = null;
            offender = null;
            if (_instance == null) return false;
            foreach (LordAssistanceGroup group in _instance._assistanceGroups.Values)
            {
                if (party.StringId != group.LeaderPartyId &&
                    !group.MemberPartyIds.Contains(party.StringId, StringComparer.OrdinalIgnoreCase) &&
                    !_instance.FormationSupports(group).Contains(party)) continue;
                if (group.PendingReleaseReason.Length != 0) return false;
                task = CrimeState.GetTask(group.LeaderPartyId);
                offender = GetActiveAssistanceCaseTarget(group, task);
                return offender != null;
            }
            return false;
        }

        internal static bool TryGetNativeCaseTarget(MobileParty actor, out MobileParty? offender)
        {
            offender = null;
            if (_instance == null || actor?.IsActive != true || actor.CurrentSettlement != null) return false;
            PoliceTask? task;
            if (!TryGetAssistanceCase(actor, out task, out offender))
            {
                if (IsPartyOccupiedByAssistance(actor)) return false;
                task = CrimeState.GetTask(actor.StringId);
                offender = task?.TargetCrime?.Offender;
            }
            return task != null && offender?.IsActive == true && offender.CurrentSettlement == null &&
                !_instance.IsAwaitingEnvoyResult(actor, task, offender);
        }

        internal static bool NativeCaseIncludesPlayer(MobileParty actor)
        {
            return TryGetAssistanceCase(actor, out PoliceTask? task, out _) ?
                task?.IsPlayerBountyEscort == true : CrimeState.GetTask(actor.StringId)?.IsPlayerBountyEscort == true;
        }

        internal static bool CanInitiateCaseAttack(MobileParty actor, MobileParty target,
            MobileParty? excludedFriendly = null)
        {
            if (target?.IsActive != true || target.CurrentSettlement != null ||
                !TryGetNativeCaseTarget(actor, out MobileParty? offender) ||
                target != offender && target != GwpStrength.CombatGroupOf(offender!)) return false;
            MobileParty combatTarget = GwpStrength.CombatGroupOf(offender!);
            // During native initiative the raw regional sums have just been captured.
            // Reuse them instead of recursively invoking the initiative evaluator.
            return GwpStrength.IsInNativeEvaluation(actor)
                ? GwpStrength.NativeAttackReady(actor, combatTarget)
                : GwpStrength.Compare(actor, offender!, NativeCaseIncludesPlayer(actor), excludedFriendly).StrengthReady;
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
                       (diagnosticTarget != null
                           ? GwpStrength.CombatGroup(GwpStrength.CombatGroupOf(diagnosticTarget))
                               .ToString("0.00", CultureInfo.InvariantCulture) : "n/a") +
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
