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
            int adopted = 0;
            int destroyedInSettlement = 0;

            foreach (MobileParty patrol in MobileParty.All.ToList())
            {
                if (patrol == null || !patrol.IsActive) continue;
                if (!GwpCommon.IsEnforcementDelayPatrolParty(patrol)) continue;
                if (_delayPatrolStates.ContainsKey(patrol.StringId)) continue;

                // 读档后若支援队已经“卡进城”，直接清理
                if (patrol.CurrentSettlement != null)
                {
                    if (TryDestroyDelayPatrolParty(patrol))
                        destroyedInSettlement++;
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
                adopted++;
            }

            if (adopted > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[灰袍支援队恢复] 已接管 {adopted} 支读档后的纠察支援队。",
                    Colors.Yellow));
            }

            if (destroyedInSettlement > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[灰袍支援队清理] 已销毁 {destroyedInSettlement} 支卡在定居点内的残留支援队。",
                    Colors.Yellow));
            }
        }

        private static MobileParty FindNearestTrackedOffender(MobileParty patrol)
        {
            if (patrol == null) return null;

            MobileParty best = null;
            float bestDist = float.MaxValue;

            foreach (MobileParty offender in CrimePool.GetAllTrackedOffenders(includePlayer: false))
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
        }

        private void CheckPersistentWarTargetsEveryTwoDays()
        {
            // 用户要求：每两日检查时，先清理所有“卡在定居点”的支援队残留
            CleanupDelayPatrolsInsideSettlements();

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            var currentTargets = new Dictionary<string, IFaction>(StringComparer.OrdinalIgnoreCase);
            var representativeTaskIdByTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (PoliceTask task in CrimePool.ActiveTasks.Values)
            {
                if (!task.WarDeclared) continue;
                if (task.IsEscortingPlayer || task.IsPlayerBountyEscort) continue;
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

                List<MobileParty> offenders = CrimePool.GetTrackedOffendersByFaction(targetFaction);
                if (offenders.Count > 0)
                {
                    string representativeTaskId = representativeTaskIdByTarget.TryGetValue(targetId, out string taskId)
                        ? taskId
                        : string.Empty;

                    int spawned = SpawnDelayPatrolsForOffenders(offenders, representativeTaskId, targetId);
                    if (spawned > 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"灰袍已派出 {spawned} 支纠察支援队支援战线。",
                            Colors.Yellow));
                    }
                }
                else
                {
                    GwpCommon.TrySetNeutral(policeClan, targetFaction);
                    MarkDelayPatrolsReturningForTarget(targetId);
                    _warTargetSeenStreak.Remove(targetId);
                }
            }

            ReportDelayPatrolStatusSnapshot();
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

            if (removed > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[灰袍支援队清理] 每两日巡检已销毁 {removed} 支城内残留支援队。",
                    Colors.Yellow));
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
            foreach (PoliceTask task in CrimePool.ActiveTasks.Values)
            {
                if (task.TargetCrime?.Offender?.StringId == offender.StringId)
                    return task.PolicePartyId;
            }

            if (!string.IsNullOrEmpty(representativeTaskId))
                return representativeTaskId;

            foreach (PoliceTask task in CrimePool.ActiveTasks.Values)
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
            CharacterObject infantry = CharacterObject.Find(GwpCommon.HeavyInfantryId);
            CharacterObject archer = CharacterObject.Find(GwpCommon.ArcherId);
            CharacterObject recruit = CharacterObject.Find("gwrecruit");

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

            foreach (var involved in mapEvent.InvolvedParties)
            {
                MobileParty party = involved?.MobileParty;
                if (!GwpCommon.IsEnforcementDelayPatrolParty(party)) continue;
                MarkDelayPatrolReturning(party.StringId);
            }
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

        private void ReportDelayPatrolStatusSnapshot()
        {
            if (_delayPatrolStates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[灰袍支援队巡检] 当前场上无纠察支援队。",
                    Colors.Gray));
                return;
            }

            var activePatrols = new List<(MobileParty Patrol, DelayPatrolState State)>();
            foreach (var kv in _delayPatrolStates)
            {
                MobileParty patrol = MobileParty.All.FirstOrDefault(p => p.StringId == kv.Key);
                if (patrol == null || !patrol.IsActive) continue;
                activePatrols.Add((patrol, kv.Value));
            }

            if (activePatrols.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[灰袍支援队巡检] 当前场上无可追踪的纠察支援队。",
                    Colors.Gray));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"[灰袍支援队巡检] 当前场上 {activePatrols.Count} 支纠察支援队。",
                Colors.Yellow));

            const int maxLines = 8;
            int shown = 0;
            foreach (var item in activePatrols)
            {
                if (shown >= maxLines) break;

                MobileParty patrol = item.Patrol;
                DelayPatrolState state = item.State;
                Settlement nearest = FindNearestNamedSettlement(patrol.GetPosition2D);
                string stage = state.Returning ? "返航" : "追击";

                string locationText;
                if (nearest == null)
                {
                    locationText = "附近无城镇/城堡/村庄";
                }
                else
                {
                    float distance = patrol.GetPosition2D.Distance(nearest.GetPosition2D);
                    locationText = $"最近{GetSettlementTypeName(nearest)} {nearest.Name}（距离 {distance:0.0}）";
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[支援队定位] {patrol.Name} | 状态:{stage} | {locationText}",
                    Colors.Cyan));
                shown++;
            }

            int omitted = activePatrols.Count - shown;
            if (omitted > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[支援队定位] 其余 {omitted} 支队伍已省略显示。",
                    Colors.Cyan));
            }
        }

        private static Settlement FindNearestNamedSettlement(Vec2 position)
        {
            Settlement nearest = null;
            float minDistance = float.MaxValue;

            foreach (Settlement settlement in Settlement.All)
            {
                if (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage)
                    continue;

                float distance = position.Distance(settlement.GetPosition2D);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = settlement;
                }
            }

            return nearest;
        }

        private static string GetSettlementTypeName(Settlement settlement)
        {
            if (settlement == null) return "定居点";
            if (settlement.IsTown) return "城镇";
            if (settlement.IsCastle) return "城堡";
            if (settlement.IsVillage) return "村庄";
            return "定居点";
        }
    }
}
