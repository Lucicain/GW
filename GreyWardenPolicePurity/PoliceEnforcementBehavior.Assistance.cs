using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

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
            public int BlockedHours { get; set; }
        }

        private readonly Dictionary<string, LordAssistanceGroup> _assistanceGroups =
            new Dictionary<string, LordAssistanceGroup>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _independentBlockedHours =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _assistanceAssignedHours =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private static bool IsGreyWardenLordParty(MobileParty? party)
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            return party?.IsActive == true && party.IsLordParty &&
                   party.LeaderHero?.IsActive == true && policeClan != null &&
                   party.ActualClan == policeClan;
        }

        private void SyncAssistanceData(IDataStore dataStore)
        {
            List<string> leaders = null!;
            List<string> crimes = null!;
            List<string> targets = null!;
            List<string> members = null!;
            List<string> memberTimes = null!;
            List<int> blocked = null!;

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
                blocked = groups.Select(group => group.BlockedHours).ToList();
            }

            dataStore.SyncData("gwp_enf_assist_leaders", ref leaders);
            dataStore.SyncData("gwp_enf_assist_crimes", ref crimes);
            dataStore.SyncData("gwp_enf_assist_targets", ref targets);
            dataStore.SyncData("gwp_enf_assist_members", ref members);
            dataStore.SyncData("gwp_enf_assist_member_times", ref memberTimes);
            dataStore.SyncData("gwp_enf_assist_blocked", ref blocked);

            if (!dataStore.IsLoading) return;

            _assistanceGroups.Clear();
            _independentBlockedHours.Clear();
            _assistanceAssignedHours.Clear();
            if (leaders == null) return;
            List<string> loadedCrimes = crimes ?? new List<string>();
            List<string> loadedTargets = targets ?? new List<string>();
            List<string> loadedMembers = members ?? new List<string>();
            List<string> loadedMemberTimes = memberTimes ?? new List<string>();
            List<int> loadedBlocked = blocked ?? new List<int>();

            for (int index = 0; index < leaders.Count; index++)
            {
                string leader = leaders[index] ?? string.Empty;
                if (leader.Length == 0) continue;
                string memberText = index < loadedMembers.Count
                    ? loadedMembers[index] ?? string.Empty
                    : string.Empty;
                _assistanceGroups[leader] = new LordAssistanceGroup
                {
                    LeaderPartyId = leader,
                    CrimeId = index < loadedCrimes.Count ? loadedCrimes[index] ?? string.Empty : string.Empty,
                    TargetPartyId = index < loadedTargets.Count ? loadedTargets[index] ?? string.Empty : string.Empty,
                    MemberPartyIds = memberText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    BlockedHours = index < loadedBlocked.Count ? Math.Max(0, loadedBlocked[index]) : 0
                };
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
                CrimePool.MaxTaskPoolEntries - CrimePool.GetForcedTaskCount());
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
                {
                    _independentBlockedHours.Remove(leader.StringId);
                    continue;
                }

                PoliceTask? task = CrimeState.GetTask(leader.StringId);
                if (_assistanceGroups.TryGetValue(leader.StringId,
                        out LordAssistanceGroup? group))
                {
                    // Creation prerequisites are deliberately not lifecycle
                    // prerequisites. Once assistance has been accepted, the
                    // formation belongs to the leader's case until that exact
                    // task ends or is replaced. Peace, a temporarily inactive
                    // offender, or a battle settlement must not dissolve it.
                    if (!IsAssistanceGroupCaseStillActive(group, task))
                    {
                        ReleaseAssistanceGroup(leader.StringId, "leader_case_ended");
                        continue;
                    }

                    Army? army = MaintainAssistanceArmy(leader, group);
                    if (army == null)
                    {
                        // Native army dispersal (for example starvation) is
                        // allowed, but it does not cancel the assistance task.
                        // Keep the group and retry native Army creation later.
                        group.BlockedHours = 0;
                        _independentBlockedHours.Remove(leader.StringId);
                        continue;
                    }

                    MobileParty? groupOffender = GetValidAssistanceOffender(leader, task);
                    if (groupOffender == null)
                    {
                        // The case can temporarily return to its peaceful
                        // approach phase. Preserve the assembled army while the
                        // leader's task advances back to a valid pursuit state.
                        group.BlockedHours = 0;
                        _independentBlockedHours.Remove(leader.StringId);
                        continue;
                    }

                    group.TargetPartyId = groupOffender.StringId;
                    if (leader.DefaultBehavior == AiBehavior.GoAroundParty &&
                        leader.TargetParty == groupOffender)
                    {
                        // A prior native resupply visit may have populated the
                        // Army objective settlement. Once the case pursuit wins
                        // again, remove that stale gathering point so the Army
                        // hourly tick cannot redirect a briefly holding leader.
                        army.AiBehaviorObject = null;
                    }
                    if (group.MemberPartyIds.Count == 0)
                    {
                        group.BlockedHours = 0;
                        TryAddAssistanceMember(leader, task!, groupOffender, group, army);
                        continue;
                    }
                    if (!AreAllMembersAssembled(leader, group, army))
                    {
                        group.BlockedHours = 0;
                        continue;
                    }

                    group.BlockedHours = IsCasePursuitBlocked(leader, groupOffender)
                        ? group.BlockedHours + 1
                        : 0;
                    if (group.BlockedHours >= GwpTuning.Enforcement.AssistanceBlockedHours)
                        TryAddAssistanceMember(leader, task!, groupOffender, group, army);
                    continue;
                }

                MobileParty? offender = GetValidAssistanceOffender(leader, task);
                if (offender == null)
                {
                    _independentBlockedHours.Remove(leader.StringId);
                    continue;
                }

                int blockedHours = IsCasePursuitBlocked(leader, offender)
                    ? (_independentBlockedHours.TryGetValue(leader.StringId, out int prior)
                        ? prior + 1
                        : 1)
                    : 0;
                _independentBlockedHours[leader.StringId] = blockedHours;
                if (blockedHours < GwpTuning.Enforcement.AssistanceBlockedHours)
                    continue;

                var newGroup = new LordAssistanceGroup
                {
                    LeaderPartyId = leader.StringId,
                    CrimeId = task!.TargetCrimeId,
                    TargetPartyId = offender.StringId,
                    BlockedHours = blockedHours
                };
                Army? newArmy = CreateOrRecoverAssistanceArmy(leader);
                if (newArmy != null &&
                    TryAddAssistanceMember(leader, task, offender, newGroup, newArmy))
                    _assistanceGroups[leader.StringId] = newGroup;
                else if (newArmy != null && newArmy.LeaderParty == leader &&
                         newArmy.Parties.Count <= 1)
                    DisbandArmyAction.ApplyByObjectiveFinished(newArmy);
            }
        }

        private MobileParty? GetValidAssistanceOffender(MobileParty leader, PoliceTask? task)
        {
            if (task == null || task.PolicePartyId != leader.StringId || !task.WarDeclared ||
                task.FlowState != PoliceTaskFlowState.WarPursuit ||
                task.IsEscortingPlayer || task.IsPlayerBountyEscort ||
                task.TargetCrime?.HasOpenCase != true)
                return null;

            MobileParty? offender = task.TargetCrime.Offender;
            if (offender?.IsActive != true || offender.Party == null ||
                offender.Party.NumberOfHealthyMembers <= 0)
                return null;

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null || offender.MapFaction == null ||
                !FactionManager.IsAtWarAgainstFaction(policeClan, offender.MapFaction))
                return null;

            if (_assistanceGroups.TryGetValue(leader.StringId, out LordAssistanceGroup? group) &&
                !string.Equals(group.CrimeId, task.TargetCrimeId,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return offender;
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

        private static bool IsCasePursuitBlocked(MobileParty leader, MobileParty offender)
        {
            if (leader.MapEvent != null || leader.CurrentSettlement != null ||
                offender.CurrentSettlement != null)
                return false;
            if (leader.GetPosition2D.Distance(offender.GetPosition2D) >
                GwpTuning.Enforcement.AssistanceContactDistance)
                return false;
            if (leader.DefaultBehavior != AiBehavior.GoAroundParty ||
                leader.TargetParty != offender)
                return false;
            return MobileParty.IsFleeBehavior(leader.ShortTermBehavior);
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
            MobileParty offender, LordAssistanceGroup group, Army army)
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
            group.BlockedHours = 0;
            _assistanceAssignedHours[helper.StringId] = CampaignTime.Now.ToHours;
            CrimePool.TrimOpenCasesToCapacity(
                CrimePool.MaxTaskPoolEntries - Math.Max(
                    CrimePool.GetForcedTaskCount(),
                    _assistanceAssignedHours.Count +
                    GreyWardenVillageAdoptionBehavior.GetTaskSnapshotCount()));
            _independentBlockedHours.Remove(leader.StringId);
            _independentBlockedHours.Remove(helper.StringId);

            // Assigning Army invokes Bannerlord's OnAddPartyInternal, including
            // its native same-clan influence-cost calculation (which is zero).
            helper.Army = army;
            TryMergeArmyMember(army, leader, helper);
            GwpAiDiagnostics.WriteAction(leader, "ASSISTANCE_ARMY_MEMBER_ADDED",
                "helper=" + helper.StringId + "; target=" + offender.StringId +
                "; crime=" + task.TargetCrimeId + "; memberCount=" + group.MemberPartyIds.Count +
                "; armyKingdom=" + (army.Kingdom?.StringId ?? "-"));
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
            if ((member.Position - leader.Position).LengthSquared < contactDistance)
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

        private void RemoveAssistanceMember(LordAssistanceGroup group, string memberId)
        {
            group.MemberPartyIds.RemoveAll(id => string.Equals(id, memberId,
                StringComparison.OrdinalIgnoreCase));
            _assistanceAssignedHours.Remove(memberId);
            _independentBlockedHours.Remove(memberId);
        }

        private void ReleaseAssistanceGroup(string leaderId, string reason)
        {
            if (!_assistanceGroups.TryGetValue(leaderId, out LordAssistanceGroup? group))
                return;
            _assistanceGroups.Remove(leaderId);
            _independentBlockedHours.Remove(leaderId);

            MobileParty? leader = FindParty(leaderId);
            Army? army = leader?.Army;
            if (army == null || army.LeaderParty != leader)
            {
                army = group.MemberPartyIds.Select(FindParty)
                    .Select(member => member?.Army)
                    .FirstOrDefault(candidate => candidate?.LeaderParty?.StringId == leaderId);
            }

            if (army != null && IsArmyOwnedByGroup(army, group))
            {
                try { DisbandArmyAction.ApplyByObjectiveFinished(army); }
                catch { }
            }

            foreach (string memberId in group.MemberPartyIds)
            {
                _independentBlockedHours.Remove(memberId);
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
                CrimePool.MaxTaskPoolEntries - CrimePool.GetForcedTaskCount());
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

            PoliceTask? task = CrimeState.GetTask(group.LeaderPartyId);
            if (!IsAssistanceGroupCaseStillActive(group, task)) return false;
            if (!string.IsNullOrEmpty(targetPartyId) &&
                !string.Equals(group.TargetPartyId, targetPartyId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            leader = FindActiveParty(group.LeaderPartyId);
            if (!IsGreyWardenLordParty(leader)) return false;
            army = MaintainAssistanceArmy(leader!, group);
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
                MobileParty combatTarget = offender.BesiegerCamp?.LeaderParty ??
                    offender.Army?.LeaderParty ?? offender.AttachedTo ?? offender;
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
                return "armyLeader:members=" + led.MemberPartyIds.Count +
                       ",attached=" + attached + ",blocked=" + led.BlockedHours +
                       ",supports=" + supportCount +
                       ",attachedSupports=" + attachedSupportCount +
                       ",armyKingdom=" + (army?.Kingdom?.StringId ?? "-") +
                       ",target=" + led.TargetPartyId;
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
            int blocked = _instance._independentBlockedHours.TryGetValue(
                party.StringId, out int value) ? value : 0;
            return blocked > 0 ? "independent:blocked=" + blocked : "none";
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
