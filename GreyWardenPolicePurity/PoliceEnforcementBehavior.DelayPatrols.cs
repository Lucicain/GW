using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        private const int DelayPatrolPartySize = 50;

        // 0/1: 距离下次两日检查还差几天
        private int _warStatusCheckDayCounter = 0;

        // 兼容旧存档字段：原“连续两次命中才派支援”逻辑已废弃。
        // 现在改为每两日检查到宣战势力有犯人就立即派支援。
        private readonly Dictionary<string, int> _warTargetSeenStreak =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DelayPatrolState> _delayPatrolStates =
            new Dictionary<string, DelayPatrolState>(StringComparer.OrdinalIgnoreCase);

        private sealed class DelayPatrolState
        {
            public string PatrolPartyId { get; set; } = string.Empty;
            public string SourceTaskPolicePartyId { get; set; } = string.Empty;
            public string TargetPartyId { get; set; } = string.Empty;
            public string WarTargetId { get; set; } = string.Empty;
            public string ReturnSettlementId { get; set; } = string.Empty;
            public bool Returning { get; set; }
        }

        private void SyncWarTargetStreakData(IDataStore dataStore)
        {
            List<string> keys = null!;
            List<int> values = null!;

            if (dataStore.IsSaving)
            {
                keys = _warTargetSeenStreak.Keys.ToList();
                values = keys.Select(k => _warTargetSeenStreak[k]).ToList();
            }

            dataStore.SyncData("gwp_enf_war_streak_keys", ref keys);
            dataStore.SyncData("gwp_enf_war_streak_values", ref values);

            if (!dataStore.IsLoading) return;

            _warTargetSeenStreak.Clear();
            if (keys == null || values == null) return;

            int count = Math.Min(keys.Count, values.Count);
            for (int i = 0; i < count; i++)
            {
                string key = keys[i];
                if (string.IsNullOrEmpty(key)) continue;
                _warTargetSeenStreak[key] = values[i];
            }
        }

        private void SyncDelayPatrolStateData(IDataStore dataStore)
        {
            List<string> patrolIds = null!;
            List<string> sourceTaskIds = null!;
            List<string> targetPartyIds = null!;
            List<string> warTargetIds = null!;
            List<string> returnSettlementIds = null!;
            List<int> returningFlags = null!;

            if (dataStore.IsSaving)
            {
                List<DelayPatrolState> states = _delayPatrolStates.Values.ToList();
                patrolIds = states.Select(s => s.PatrolPartyId).ToList();
                sourceTaskIds = states.Select(s => s.SourceTaskPolicePartyId).ToList();
                targetPartyIds = states.Select(s => s.TargetPartyId).ToList();
                warTargetIds = states.Select(s => s.WarTargetId).ToList();
                returnSettlementIds = states.Select(s => s.ReturnSettlementId).ToList();
                returningFlags = states.Select(s => s.Returning ? 1 : 0).ToList();
            }

            dataStore.SyncData("gwp_enf_dp_ids", ref patrolIds);
            dataStore.SyncData("gwp_enf_dp_source_ids", ref sourceTaskIds);
            dataStore.SyncData("gwp_enf_dp_target_ids", ref targetPartyIds);
            dataStore.SyncData("gwp_enf_dp_war_target_ids", ref warTargetIds);
            dataStore.SyncData("gwp_enf_dp_return_settlement_ids", ref returnSettlementIds);
            dataStore.SyncData("gwp_enf_dp_return_flags", ref returningFlags);

            if (!dataStore.IsLoading) return;

            _delayPatrolStates.Clear();
            if (patrolIds == null) return;

            int count = patrolIds.Count;
            for (int i = 0; i < count; i++)
            {
                string patrolId = patrolIds[i];
                if (string.IsNullOrEmpty(patrolId)) continue;

                _delayPatrolStates[patrolId] = new DelayPatrolState
                {
                    PatrolPartyId = patrolId,
                    SourceTaskPolicePartyId = i < (sourceTaskIds?.Count ?? 0) ? sourceTaskIds[i] : string.Empty,
                    TargetPartyId = i < (targetPartyIds?.Count ?? 0) ? targetPartyIds[i] : string.Empty,
                    WarTargetId = i < (warTargetIds?.Count ?? 0) ? warTargetIds[i] : string.Empty,
                    ReturnSettlementId = i < (returnSettlementIds?.Count ?? 0) ? returnSettlementIds[i] : string.Empty,
                    Returning = i < (returningFlags?.Count ?? 0) && returningFlags[i] != 0
                };
            }
        }

        private void EnsureDelayPatrolStateForActiveParties()
        {
            foreach (MobileParty patrol in MobileParty.All.ToList())
            {
                if (patrol == null || !patrol.IsActive) continue;
                if (!GwpCommon.IsEnforcementDelayPatrolParty(patrol)) continue;
                if (_delayPatrolStates.ContainsKey(patrol.StringId)) continue;

                // 读档后若支援队已经“卡进城”，直接清理
                if (patrol.CurrentSettlement != null)
                {
                    if (TryDestroyDelayPatrolParty(patrol))
                    {
                        // no-op
                    }
                    continue;
                }

                MobileParty nearestOffender = FindNearestTrackedOffender(patrol);
                Settlement returnSettlement = GwpCommon.FindNearestTown(patrol);

                _delayPatrolStates[patrol.StringId] = new DelayPatrolState
                {
                    PatrolPartyId = patrol.StringId,
                    SourceTaskPolicePartyId = string.Empty,
                    TargetPartyId = nearestOffender?.StringId ?? string.Empty,
                    WarTargetId = nearestOffender?.MapFaction?.StringId ?? string.Empty,
                    ReturnSettlementId = returnSettlement?.StringId ?? string.Empty,
                    Returning = nearestOffender == null
                };
            }
        }

        private static MobileParty FindNearestTrackedOffender(MobileParty patrol)
        {
            if (patrol == null) return null;

            MobileParty best = null;
            float bestDist = float.MaxValue;

            foreach (MobileParty offender in CrimeState.GetAllTrackedOffenders(includePlayer: false))
            {
                if (offender == null || !offender.IsActive) continue;
                float dist = patrol.GetPosition2D.Distance(offender.GetPosition2D);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = offender;
                }
            }

            return best;
        }

        private void OnDailyTick()
        {
            _warStatusCheckDayCounter++;
            if (_warStatusCheckDayCounter < 2) return;
            _warStatusCheckDayCounter = 0;

            CheckPersistentWarTargetsEveryTwoDays();
            EnsureNearestPoliceForWantedPlayer();
        }

        private void CheckPersistentWarTargetsEveryTwoDays()
        {
            // 用户要求：每两日检查时，先清理所有“卡在定居点”的支援队残留
            CleanupDelayPatrolsInsideSettlements();

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            var currentTargets = new Dictionary<string, IFaction>(StringComparer.OrdinalIgnoreCase);
            var representativeTaskIdByTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (task.FlowState != PoliceTaskFlowState.WarPursuit) continue;
                if (task.TargetCrime?.Offender?.IsMainParty == true) continue;
                if (task.WarTarget == null) continue;
                if (string.IsNullOrEmpty(task.WarTarget.StringId)) continue;
                if (!FactionManager.IsAtWarAgainstFaction(policeClan, task.WarTarget)) continue;

                string targetId = task.WarTarget.StringId;
                currentTargets[targetId] = task.WarTarget;
                if (!representativeTaskIdByTarget.ContainsKey(targetId))
                    representativeTaskIdByTarget[targetId] = task.PolicePartyId;
            }

            foreach (var kv in currentTargets)
            {
                string targetId = kv.Key;
                IFaction targetFaction = kv.Value;

                List<MobileParty> offenders = CrimeState.GetTrackedOffendersByFaction(targetFaction);
                if (offenders.Count > 0)
                {
                    string representativeTaskId = representativeTaskIdByTarget.TryGetValue(targetId, out string taskId)
                        ? taskId
                        : string.Empty;

                    SpawnDelayPatrolsForOffenders(offenders, representativeTaskId, targetId);
                }
                else
                {
                    GwpCommon.TrySetNeutral(policeClan, targetFaction);
                    MarkDelayPatrolsReturningForTarget(targetId);
                    _warTargetSeenStreak.Remove(targetId);
                }
            }

            CleanupStalePoliceWarsWithoutReasons(policeClan);
        }

        private void CleanupStalePoliceWarsWithoutReasons(Clan policeClan)
        {
            foreach (IFaction targetFaction in GwpPoliceWarReasonService.GetCurrentPoliceWarFactions(policeClan).ToList())
            {
                if (targetFaction == null)
                    continue;

                if (GwpPoliceWarReasonService.HasLegitimateWarReason(targetFaction))
                    continue;

                TryApplyPlayerAutoPeacePenalty(targetFaction);
                GwpCommon.TrySetNeutral(policeClan, targetFaction);

                if (!string.IsNullOrEmpty(targetFaction.StringId))
                {
                    MarkDelayPatrolsReturningForTarget(targetFaction.StringId);
                    _warTargetSeenStreak.Remove(targetFaction.StringId);
                }
            }
        }

        private void CleanupDelayPatrolsInsideSettlements()
        {
            int removed = 0;

            foreach (var kv in _delayPatrolStates.ToList())
            {
                MobileParty patrol = MobileParty.All.FirstOrDefault(p => p.StringId == kv.Key);
                if (patrol == null || !patrol.IsActive)
                {
                    _delayPatrolStates.Remove(kv.Key);
                    continue;
                }

                if (patrol.CurrentSettlement == null) continue;

                if (TryDestroyDelayPatrolParty(patrol))
                    removed++;
                _delayPatrolStates.Remove(kv.Key);
            }

            foreach (MobileParty patrol in MobileParty.All.ToList())
            {
                if (patrol == null || !patrol.IsActive) continue;
                if (!GwpCommon.IsEnforcementDelayPatrolParty(patrol)) continue;
                if (patrol.CurrentSettlement == null) continue;
                if (_delayPatrolStates.ContainsKey(patrol.StringId)) continue;

                if (TryDestroyDelayPatrolParty(patrol))
                    removed++;
            }

        }

        private static bool TryDestroyDelayPatrolParty(MobileParty patrol)
        {
            if (patrol == null || !patrol.IsActive) return false;
            try
            {
                DestroyPartyAction.Apply(null, patrol);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int SpawnDelayPatrolsForOffenders(
            IEnumerable<MobileParty> offenders,
            string representativeTaskId,
            string warTargetId)
        {
            int spawned = 0;
            foreach (MobileParty offender in offenders)
            {
                if (offender == null || !offender.IsActive) continue;

                bool alreadyTracked = _delayPatrolStates.Values.Any(s =>
                    !s.Returning &&
                    string.Equals(s.TargetPartyId, offender.StringId, StringComparison.OrdinalIgnoreCase));
                if (alreadyTracked) continue;

                string sourceTaskId = FindSourcePoliceTaskForOffender(offender, representativeTaskId, warTargetId);
                if (SpawnSingleDelayPatrol(offender, sourceTaskId, warTargetId))
                    spawned++;
            }
            return spawned;
        }

        private string FindSourcePoliceTaskForOffender(
            MobileParty offender,
            string representativeTaskId,
            string warTargetId)
        {
            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (task.TargetCrime?.Offender?.StringId == offender.StringId)
                    return task.PolicePartyId;
            }

            if (!string.IsNullOrEmpty(representativeTaskId))
                return representativeTaskId;

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (task.WarTarget?.StringId == warTargetId && !string.IsNullOrEmpty(task.PolicePartyId))
                    return task.PolicePartyId;
            }

            return string.Empty;
        }

        private bool SpawnSingleDelayPatrol(MobileParty targetParty, string sourceTaskPolicePartyId, string warTargetId)
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;

            MobileParty sourcePoliceParty = null;
            if (!string.IsNullOrEmpty(sourceTaskPolicePartyId))
            {
                sourcePoliceParty = MobileParty.All.FirstOrDefault(p =>
                    p.StringId == sourceTaskPolicePartyId && p.IsActive);
            }

            Settlement spawnSettlement = sourcePoliceParty != null
                ? GwpCommon.FindNearestTown(sourcePoliceParty.GetPosition2D)
                : GwpCommon.FindNearestTown(targetParty.GetPosition2D);
            if (spawnSettlement == null) return false;

            string patrolId = GwpCommon.EnforcementDelayPatrolIdPrefix + MBRandom.RandomInt(10000, 99999);

            try
            {
                MobileParty patrol = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    spawnSettlement.GatePosition,
                    1f,
                    spawnSettlement,
                    new TextObject("灰袍纠察支援队"),
                    policeClan,
                    policeClan.DefaultPartyTemplate,
                    null,
                    "",
                    "",
                    5f,
                    false);
                if (patrol == null) return false;

                patrol.StringId = patrolId;
                patrol.ActualClan = policeClan;
                patrol.MemberRoster.Clear();
                FillDelayPatrolTroops(patrol);
                PoliceResourceManager.ReplenishFood(patrol, 5);

                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.Ai.SetInitiative(1f, 0f, 999f);
                patrol.SetMoveEngageParty(targetParty, NavigationType.Default);

                _delayPatrolStates[patrolId] = new DelayPatrolState
                {
                    PatrolPartyId = patrolId,
                    SourceTaskPolicePartyId = sourceTaskPolicePartyId ?? string.Empty,
                    TargetPartyId = targetParty.StringId,
                    WarTargetId = warTargetId ?? string.Empty,
                    ReturnSettlementId = spawnSettlement.StringId,
                    Returning = false
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void FillDelayPatrolTroops(MobileParty patrol)
        {
            CharacterObject infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
            CharacterObject archer = CharacterObject.Find(GwpIds.ArcherId);
            CharacterObject recruit = CharacterObject.Find(GwpIds.PoliceRecruitId);

            int infantryCount = (int)(DelayPatrolPartySize * 0.6f);
            int archerCount = DelayPatrolPartySize - infantryCount;

            if (infantry != null)
                patrol.MemberRoster.AddToCounts(infantry, infantryCount);
            else if (recruit != null)
                patrol.MemberRoster.AddToCounts(recruit, infantryCount);

            if (archer != null)
                patrol.MemberRoster.AddToCounts(archer, archerCount);
            else if (recruit != null)
                patrol.MemberRoster.AddToCounts(recruit, archerCount);
        }

        private void UpdateDelayPatrols()
        {
            foreach (var kv in _delayPatrolStates.ToList())
            {
                DelayPatrolState state = kv.Value;
                MobileParty patrol = MobileParty.All.FirstOrDefault(p => p.StringId == state.PatrolPartyId);
                if (patrol == null || !patrol.IsActive)
                {
                    _delayPatrolStates.Remove(kv.Key);
                    continue;
                }

                if (patrol.CurrentSettlement != null)
                {
                    if (TryDestroyDelayPatrolParty(patrol))
                    {
                        _delayPatrolStates.Remove(kv.Key);
                    }
                    continue;
                }

                if (state.Returning)
                {
                    Settlement returnSettlement = Settlement.FindFirst(s => s.StringId == state.ReturnSettlementId)
                                                  ?? GwpCommon.FindNearestTown(patrol);
                    if (returnSettlement == null)
                    {
                        TryDestroyDelayPatrolParty(patrol);
                        _delayPatrolStates.Remove(kv.Key);
                        continue;
                    }

                    patrol.Ai.SetDoNotMakeNewDecisions(true);
                    patrol.SetMoveGoToSettlement(returnSettlement, NavigationType.Default, false);

                    float dist = patrol.GetPosition2D.Distance(returnSettlement.GetPosition2D);
                    if (dist < 3f)
                    {
                        TryDestroyDelayPatrolParty(patrol);
                        _delayPatrolStates.Remove(kv.Key);
                    }
                    continue;
                }

                MobileParty target = MobileParty.All.FirstOrDefault(p =>
                    p.StringId == state.TargetPartyId && p.IsActive);
                if (target == null)
                {
                    MarkDelayPatrolReturning(state.PatrolPartyId);
                    continue;
                }

                if (patrol.ItemRoster.TotalFood <= 0)
                {
                    MarkDelayPatrolReturning(state.PatrolPartyId);
                    continue;
                }

                PoliceResourceManager.ReplenishFood(patrol, 2);
                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.Ai.SetInitiative(1f, 0f, 999f);
                patrol.SetMoveEngageParty(target, NavigationType.Default);
            }
        }

        private void HandleDelayPatrolBattleEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;

            HashSet<string> involvedWarTargetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var involved in mapEvent.InvolvedParties)
            {
                MobileParty party = involved?.MobileParty;
                if (!GwpCommon.IsEnforcementDelayPatrolParty(party)) continue;

                if (_delayPatrolStates.TryGetValue(party.StringId, out DelayPatrolState? state) &&
                    !string.IsNullOrEmpty(state.WarTargetId))
                {
                    involvedWarTargetIds.Add(state.WarTargetId);
                }

                MarkDelayPatrolReturning(party.StringId);
            }

            if (DelayPatrolWonBattle(mapEvent))
                CleanupDefeatedTrackedOffendersAfterDelayPatrolVictory(mapEvent);

            foreach (string warTargetId in involvedWarTargetIds)
                TryResolveDelayPatrolWarTargetImmediately(warTargetId);
        }

        private bool DelayPatrolWonBattle(MapEvent mapEvent)
        {
            if (mapEvent?.HasWinner != true || mapEvent.Winner == null)
                return false;

            foreach (MapEventParty? winner in mapEvent.Winner.Parties)
            {
                MobileParty? winnerParty = winner?.Party?.MobileParty;
                if (GwpCommon.IsEnforcementDelayPatrolParty(winnerParty))
                    return true;
            }

            return false;
        }

        private void CleanupDefeatedTrackedOffendersAfterDelayPatrolVictory(MapEvent mapEvent)
        {
            if (mapEvent?.Winner == null) return;

            MapEventSide? loserSide = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loserSide == null) return;

            foreach (MapEventParty? losingPartyEntry in loserSide.Parties)
            {
                MobileParty? losingParty = losingPartyEntry?.Party?.MobileParty;
                if (losingParty == null || losingParty.IsMainParty) continue;
                if (IsGreyWardenPoliceParty(losingParty)) continue;

                ResolveTrackedOffenderDefeatByDelayPatrol(losingParty.StringId);
            }
        }

        private void ResolveTrackedOffenderDefeatByDelayPatrol(string? offenderId)
        {
            if (string.IsNullOrEmpty(offenderId))
                return;

            foreach (var kv in CrimeState.ActiveTasks.ToList())
            {
                PoliceTask task = kv.Value;
                if (!string.Equals(task.TargetCrime?.Offender?.StringId, offenderId, StringComparison.OrdinalIgnoreCase))
                    continue;

                MobileParty? policeParty = MobileParty.All.FirstOrDefault(p =>
                    p.StringId == task.PolicePartyId && p.IsActive);
                if (policeParty != null)
                {
                    RestoreAi(policeParty);
                    PoliceResourceManager.StartResupply(policeParty);
                }

                ClearTaskWarTracking(kv.Key, true);
                CrimeState.EndTask(kv.Key);
            }

            CrimeState.RemovePendingCrimeByOffenderId(offenderId);
        }

        private void TryResolveDelayPatrolWarTargetImmediately(string? warTargetId)
        {
            if (string.IsNullOrEmpty(warTargetId))
                return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return;

            IFaction? targetFaction = ResolveWarTargetFaction(warTargetId);
            if (targetFaction == null)
                return;

            if (!FactionManager.IsAtWarAgainstFaction(policeClan, targetFaction))
                return;

            if (GwpPoliceWarReasonService.HasLegitimateWarReason(targetFaction))
                return;

            TryApplyPlayerAutoPeacePenalty(targetFaction);
            GwpCommon.TrySetNeutral(policeClan, targetFaction);
            MarkDelayPatrolsReturningForTarget(warTargetId);
            _warTargetSeenStreak.Remove(warTargetId);
        }

        private void TryApplyPlayerAutoPeacePenalty(IFaction targetFaction)
        {
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction == null || targetFaction == null)
                return;

            if (!string.Equals(playerFaction.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return;

            PlayerState.ChangeReputation(-4);
            InformationManager.DisplayMessage(new InformationMessage(
                $"与灰袍守卫处于战争状态，声望 -4。当前声望：{PlayerState.Reputation}",
                Colors.Red));
        }

        private static IFaction? ResolveWarTargetFaction(string warTargetId)
        {
            if (string.IsNullOrEmpty(warTargetId))
                return null;

            Kingdom? kingdom = Kingdom.All.FirstOrDefault(k =>
                string.Equals(k.StringId, warTargetId, StringComparison.OrdinalIgnoreCase));
            if (kingdom != null)
                return kingdom;

            Clan? clan = Clan.All.FirstOrDefault(c =>
                string.Equals(c.StringId, warTargetId, StringComparison.OrdinalIgnoreCase));
            return clan;
        }

        private static bool IsGreyWardenPoliceParty(MobileParty? party)
        {
            if (party == null) return false;
            if (GwpCommon.IsPatrolParty(party) || GwpCommon.IsEnforcementDelayPatrolParty(party))
                return true;

            return string.Equals(
                party.ActualClan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }

        private void ClearDelayPatrolRuntimeState()
        {
            _warTargetSeenStreak.Clear();
            _delayPatrolStates.Clear();
        }

        private void ClearTaskWarTracking(string policeTaskId, bool markDelayPatrolReturning)
        {
            ClearShelteredTargetTracking(policeTaskId);
            if (!markDelayPatrolReturning || string.IsNullOrEmpty(policeTaskId)) return;
            MarkDelayPatrolsReturningForTask(policeTaskId);
        }

        private void MarkDelayPatrolsReturningForTask(string sourceTaskPolicePartyId)
        {
            foreach (DelayPatrolState state in _delayPatrolStates.Values)
            {
                if (!string.Equals(state.SourceTaskPolicePartyId, sourceTaskPolicePartyId, StringComparison.OrdinalIgnoreCase))
                    continue;
                state.Returning = true;
            }
        }

        private void MarkDelayPatrolsReturningForTarget(string warTargetId)
        {
            foreach (DelayPatrolState state in _delayPatrolStates.Values)
            {
                if (!string.Equals(state.WarTargetId, warTargetId, StringComparison.OrdinalIgnoreCase))
                    continue;
                state.Returning = true;
            }
        }

        private void MarkDelayPatrolReturning(string patrolId)
        {
            if (string.IsNullOrEmpty(patrolId)) return;
            if (_delayPatrolStates.TryGetValue(patrolId, out DelayPatrolState state))
                state.Returning = true;
        }

        private void EnsureNearestPoliceForWantedPlayer()
        {
            MobileParty playerParty = MobileParty.MainParty;
            if (playerParty == null || !playerParty.IsActive) return;
            if (PlayerState.Reputation > -11) return;
            if (PlayerState.HasAtonementTask) return;

            if (!CrimeState.IsPlayerHunted)
            {
                CrimeState.TryAddPlayerCrime(
                    "累计犯罪",
                    playerParty.GetPosition2D,
                    $"声望已达 {PlayerState.Reputation}");
            }

            MobileParty nearestPolice = FindNearestPolicePartyForPlayerCase(playerParty.GetPosition2D);
            if (nearestPolice == null) return;

            string nearestId = nearestPolice.StringId;
            string currentPlayerPoliceId = CrimeState.GetPlayerTaskPolicePartyId() ?? string.Empty;
            if (string.Equals(currentPlayerPoliceId, nearestId, StringComparison.OrdinalIgnoreCase))
            {
                PoliceResourceManager.CancelResupply(nearestPolice);
                nearestPolice.Ai.SetDoNotMakeNewDecisions(true);
                nearestPolice.Ai.SetInitiative(1f, 0f, 999f);
                nearestPolice.SetMoveEngageParty(playerParty, NavigationType.Default);
                return;
            }

            // 旧追捕方（若存在）恢复 AI，让其可继续常规执法
            if (!string.IsNullOrEmpty(currentPlayerPoliceId))
            {
                MobileParty oldPolice = MobileParty.All.FirstOrDefault(p =>
                    p.StringId == currentPlayerPoliceId && p.IsActive);
                if (oldPolice != null)
                    RestoreAi(oldPolice);
                ClearTaskWarTracking(currentPlayerPoliceId, true);
            }

            // 最近警察若有旧案，先清掉战争追踪并交回犯罪池（由 CrimePool 内部处理）
            PoliceTask nearestTask = CrimeState.GetTask(nearestId);
            if (nearestTask != null && nearestTask.TargetCrime?.Offender?.IsMainParty != true)
            {
                ClearTaskWarTracking(nearestId, true);
            }

            if (!CrimeState.TryAssignPlayerCrimeToPolice(nearestId))
                return;

            // 若最近警察正处于补给流程，强制取消并立即转向玩家
            PoliceResourceManager.CancelResupply(nearestPolice);
            nearestPolice.Ai.SetDoNotMakeNewDecisions(true);
            nearestPolice.Ai.SetInitiative(1f, 0f, 999f);
            nearestPolice.SetMoveEngageParty(playerParty, NavigationType.Default);
        }

        private static MobileParty FindNearestPolicePartyForPlayerCase(Vec2 playerPos)
        {
            MobileParty best = null;
            float bestDist = float.MaxValue;

            foreach (MobileParty police in PoliceStats.GetAllPoliceParties())
            {
                if (police == null || !police.IsActive) continue;
                if (GwpCommon.IsPatrolParty(police)) continue;
                if (GwpCommon.IsEnforcementDelayPatrolParty(police)) continue;
                if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(police)) continue;
                if (police.LeaderHero == null || !police.LeaderHero.IsActive) continue;

                PoliceTask task = CrimeState.GetTask(police.StringId);
                if (task?.IsEscortingPlayer == true) continue;
                if (task?.IsPlayerBountyEscort == true) continue;

                float dist = police.GetPosition2D.Distance(playerPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = police;
                }
            }

            return best;
        }

    }
}
