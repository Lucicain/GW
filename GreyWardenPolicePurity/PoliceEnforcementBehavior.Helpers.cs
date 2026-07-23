using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    public partial class PoliceEnforcementBehavior
    {
        #region 辅助

        private bool InEvent(MobileParty party, MapEvent mapEvent)
        {
            if (party == null || mapEvent == null) return false;
            return mapEvent.InvolvedParties.Any(p => p.MobileParty == party);
        }

        private bool TryPreparePolicePartyForVillageRelief(MobileParty? police)
        {
            if (police == null || !police.IsActive)
                return false;

            if (police.LeaderHero == null || !police.LeaderHero.IsActive)
                return false;

            if (police.MapEvent != null && !police.MapEvent.IsFinalized)
                return false;

            if (IsAssistanceOccupied(police))
                return false;

            GreyWardenVillageReconstructionBehavior.ReleasePartyForForcedDuty(police);
            GreyWardenIssueResolutionBehavior.ReleasePartyForForcedDuty(police);

            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
            {
                if (task.IsEscortingPlayer || task.IsPlayerBountyEscort || task.TargetCrime?.Offender?.IsMainParty == true)
                    return false;

                IFaction? warTarget = task.WarTarget;
                RestoreAi(police);
                ClearTaskWarTracking(police.StringId, true);
                CrimeState.EndTask(police.StringId);
                CrimeRecord? displacedCrime = task.TargetCrime;
                if (displacedCrime?.Offender?.IsActive == true)
                    CrimeState.ReopenCase(displacedCrime);

                Clan? policeClan = PoliceStats.GetPoliceClan();
                if (policeClan != null &&
                    warTarget != null &&
                    !GwpPoliceWarReasonService.HasLegitimateWarReason(warTarget))
                {
                    GwpCommon.TrySetNeutral(policeClan, warTarget);
                }
            }

            GreyWardenPartyDesireBehavior.ClearIntent(police);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
            return true;
        }

        /// <summary>
        /// A filed player request outranks every autonomous Grey Warden duty.
        /// Never interrupt a live battle or an existing player-bounty escort;
        /// ordinary work is returned to its pool before the specialist travels.
        /// </summary>
        private bool TryPreparePartyForPlayerRequest(MobileParty? police)
        {
            if (police?.IsActive != true || police.LeaderHero?.IsActive != true)
                return false;
            if (police.MapEvent is { IsFinalized: false })
                return false;

            ReleaseAssistanceForPlayerRequest(police);
            if (police.Army != null)
                return false;

            GreyWardenTrainingBehavior.ReleasePartyForForcedDuty(police);
            GreyWardenVillageReconstructionBehavior.ReleasePartyForForcedDuty(police);
            GreyWardenIssueResolutionBehavior.ReleasePartyForForcedDuty(police);

            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
            {
                if (task.IsEscortingPlayer || task.IsPlayerBountyEscort ||
                    task.TargetCrime?.Offender?.IsMainParty == true)
                    return false;

                IFaction? warTarget = task.WarTarget;
                RestoreAi(police);
                ClearTaskWarTracking(police.StringId, true);
                CrimeState.EndTask(police.StringId);
                CrimeRecord? displacedCrime = task.TargetCrime;
                if (displacedCrime?.Offender?.IsActive == true)
                    CrimeState.ReopenCase(displacedCrime);

                Clan? policeClan = PoliceStats.GetPoliceClan();
                if (policeClan != null && warTarget != null &&
                    !GwpPoliceWarReasonService.HasLegitimateWarReason(warTarget))
                    GwpCommon.TrySetNeutral(policeClan, warTarget);
            }

            GreyWardenPartyDesireBehavior.ClearIntent(police);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
            return true;
        }

        private bool IsOnWinningSide(MobileParty party, MapEvent mapEvent)
        {
            if (!mapEvent.HasWinner || mapEvent.Winner == null) return false;

            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p?.Party?.IsMobile == true && p.Party.MobileParty == party)
                    return true;
            }
            return false;
        }

        private void RestoreAi(MobileParty party)
        {
            if (party == null || !party.IsActive) return;
            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.RethinkAtNextHourlyTick = true;
            }
            catch { }
        }

        private void MakePeaceWithPoliceAndVictims()
        {
            try
            {
                IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction == null) return;

                Clan policeClan = PoliceStats.GetPoliceClan();
                GwpCommon.TrySetNeutral(policeClan, playerFaction);

                foreach (var victim in PlayerState.VictimFactions)
                {
                    if (victim == null || victim == playerFaction) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(playerFaction, victim)) continue;

                    try
                    {
                        MakePeaceAction.Apply(playerFaction, victim);
                    }
                    catch { }
                }

                PlayerState.ClearVictimFactions();
            }
            catch { }
        }

        private Settlement? FindNearestTown()
        {
            var player = MobileParty.MainParty;
            if (player == null) return null;

            Vec2 pos = player.GetPosition2D;
            Settlement? best = null;
            float bestDist = float.MaxValue;

            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsTown) continue;
                float d = pos.Distance(s.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// 查找玩家附近最近的城堡（严格使用 IsCastle）。
        ///
        /// 修复说明：原 FindNearestFortress 使用 (!s.IsCastle &amp;&amp; !s.IsFortification) 条件，
        /// 但 IsFortification 在 Bannerlord 中对城镇和城堡均为 true，
        /// 导致函数实际上也会返回城镇，警察带着俘虏进城触发引擎崩溃。
        /// 现在只用 IsCastle 精确匹配，城堡通常不允许非所有者自由进出。
        /// </summary>
        private Settlement? FindNearestCastle()
        {
            var player = MobileParty.MainParty;
            if (player == null) return null;

            Vec2 pos = player.GetPosition2D;
            Settlement? best = null;
            float bestDist = float.MaxValue;

            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsCastle) continue;  // 只选城堡，IsFortification 会误包含城镇
                float d = pos.Distance(s.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = s; }
            }

            // 极端情况：地图上找不到城堡，降级用城镇
            if (best == null)
                best = FindNearestTown();

            return best;
        }

        private void Reassign(CrimeRecord? crime)
        {
            CrimeState.ReopenCase(crime);
        }

        private void ClearShelteredTargetTracking(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            _shelteredPoliceLastPositionByTaskId.Remove(taskId);
            _shelteredPoliceStoppedHoursByTaskId.Remove(taskId);
        }

        private void BreakInvalidShelteredBattles()
        {
            foreach (var kvp in CrimeState.ActiveTasks.ToList())
            {
                PoliceTask task = kvp.Value;
                MobileParty? policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);
                MobileParty? criminal = task.TargetCrime?.Offender;

                if (policeParty == null || !policeParty.IsActive) continue;
                if (criminal == null || !criminal.IsActive || criminal.IsMainParty) continue;
                if (criminal.CurrentSettlement == null) continue;
                if (policeParty.MapEvent == null || policeParty.MapEvent.IsFinalized) continue;
                if (policeParty.MapEvent.IsPlayerMapEvent) continue;

                float distToShelter = policeParty.GetPosition2D.Distance(criminal.CurrentSettlement.GetPosition2D);
                if (distToShelter <= GwpTuning.Enforcement.WarDistance) continue;

                _ignoredInvalidShelteredBattlePartyIds.Add(policeParty.StringId);

                try
                {
                    policeParty.MapEvent.FinalizeEvent();
                }
                catch
                {
                    _ignoredInvalidShelteredBattlePartyIds.Remove(policeParty.StringId);
                }
            }
        }

        private bool HandleShelteredCriminal(
            MobileParty policeParty,
            PoliceTask task,
            string taskId,
            MobileParty criminal)
        {
            if (policeParty == null || !policeParty.IsActive) return true;
            if (criminal == null || !criminal.IsActive) return false;
            if (criminal.IsMainParty) return false;

            Settlement shelter = criminal.CurrentSettlement;
            if (shelter == null)
            {
                ClearShelteredTargetTracking(taskId);
                return false;
            }

            float distToShelter = policeParty.GetPosition2D.Distance(shelter.GetPosition2D);
            float distToGate = policeParty.GetPosition2D.Distance(shelter.GatePosition.ToVec2());
            int stoppedHours = UpdateShelteredPoliceStoppedHours(taskId, policeParty);

            // 围堵仍是案件保底欲望；原版补给/恢复欲望可自然打断并在完成后回来。

            // 躲进定居点时，必须先让“当前这条任务”进入战争追捕状态。
            // 即便两边已经被别的警察任务拖入战争，也不能跳过这一步直接隔空强制开战。
            if (!task.WarDeclared && distToShelter <= GwpTuning.Enforcement.WarDistance)
            {
                DeclareWar(task, criminal);
            }

            if (task.WarDeclared &&
                distToGate <= GwpTuning.Enforcement.ShelteredGateDistance &&
                stoppedHours >= GwpTuning.Enforcement.ShelteredGateHoldHours)
            {
                TryForceExpelShelteredCriminal(policeParty, criminal, taskId);
            }

            if (criminal.CurrentSettlement == null)
            {
                ClearShelteredTargetTracking(taskId);
                return false;
            }

            return true;
        }

        private int UpdateShelteredPoliceStoppedHours(string taskId, MobileParty policeParty)
        {
            if (string.IsNullOrEmpty(taskId) || policeParty == null)
                return 0;

            Vec2 currentPosition = policeParty.GetPosition2D;
            if (!_shelteredPoliceLastPositionByTaskId.TryGetValue(taskId, out Vec2 previousPosition))
            {
                _shelteredPoliceLastPositionByTaskId[taskId] = currentPosition;
                _shelteredPoliceStoppedHoursByTaskId[taskId] = 0;
                return 0;
            }

            float movedDistance = currentPosition.Distance(previousPosition);
            int stoppedHours = movedDistance <= GwpTuning.Enforcement.ShelteredGateStopTolerance
                ? (_shelteredPoliceStoppedHoursByTaskId.TryGetValue(taskId, out int lastStoppedHours)
                    ? lastStoppedHours + 1
                    : 1)
                : 0;

            _shelteredPoliceLastPositionByTaskId[taskId] = currentPosition;
            _shelteredPoliceStoppedHoursByTaskId[taskId] = stoppedHours;
            return stoppedHours;
        }

        private bool TryForceExpelShelteredCriminal(
            MobileParty attacker,
            MobileParty defender,
            string taskId)
        {
            if (attacker == null || defender == null) return false;
            if (!attacker.IsActive || !defender.IsActive) return false;
            if (attacker.CurrentSettlement != null) return false;
            if (string.Equals(attacker.StringId, defender.StringId, StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                Settlement? defenderSettlement = defender.CurrentSettlement;
                if (defenderSettlement != null)
                {
                    MobileParty expelParty = defender;
                    MobileParty? armyLeader = defender.Army?.LeaderParty;

                    // 目标若属于军团，且军团领队也在同一座城里，
                    // 直接把领队整支拉出城，原版会递归带出附属军团成员。
                    if (armyLeader != null &&
                        armyLeader.IsActive &&
                        armyLeader.CurrentSettlement == defenderSettlement)
                    {
                        expelParty = armyLeader;
                    }

                    var forcedPartyIds = new HashSet<string>(
                        expelParty.AttachedParties
                            .Where(party => party?.IsActive == true)
                            .Select(party => party.StringId),
                        StringComparer.OrdinalIgnoreCase)
                    {
                        expelParty.StringId,
                        defender.StringId
                    };

                    LeaveSettlementAction.ApplyForParty(expelParty);

                    // 目标被逐出城后不再原地等待。直接命令目标（若属于军团则命令
                    // 军团领队）进攻当前案件的承办领主。关闭原版新欲望决策，
                    // 案件结束前只允许执行这一条进攻指令。
                    if (!string.IsNullOrWhiteSpace(taskId))
                    {
                        UpdateShelteredForcedPartyTracking(taskId, forcedPartyIds);
                        TryForceShelteredCaseAttack(defender, attacker);
                    }
                }

                return defender.CurrentSettlement == null;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsSettlementEntryBlockedByActiveCase(
            MobileParty? enteringParty,
            Settlement? settlement)
        {
            if (enteringParty?.IsActive != true || settlement == null ||
                enteringParty.IsMainParty)
                return false;

            return _instance?.IsPartyBlockedFromSettlementByShelteredCase(
                enteringParty) == true;
        }

        internal static void RedirectShelteredCasePartyToAssignee(
            MobileParty? enteringParty)
        {
            if (enteringParty?.IsActive != true) return;
            _instance?.TryForceShelteredCaseAttackForTrackedParty(enteringParty);
        }

        private bool IsPartyBlockedFromSettlementByShelteredCase(
            MobileParty enteringParty)
        {
            foreach (var entry in _shelteredForcedPartyIdsByTaskId)
            {
                PoliceTask? task = CrimeState.GetTask(entry.Key);
                if (!IsShelteredForcedAttackTaskActive(task))
                    continue;
                if (entry.Value.Contains(enteringParty.StringId))
                    return true;
            }

            return false;
        }

        private void TryForceShelteredCaseAttackForTrackedParty(
            MobileParty trackedParty)
        {
            foreach (var entry in _shelteredForcedPartyIdsByTaskId)
            {
                if (!entry.Value.Contains(trackedParty.StringId)) continue;

                PoliceTask? task = CrimeState.GetTask(entry.Key);
                if (!IsShelteredForcedAttackTaskActive(task)) continue;

                MobileParty? offender = task!.TargetCrime?.Offender;
                MobileParty? assignee = MobileParty.All.FirstOrDefault(party =>
                    party.IsActive &&
                    string.Equals(party.StringId, task.PolicePartyId,
                        StringComparison.OrdinalIgnoreCase));
                TryForceShelteredCaseAttack(offender, assignee);
                return;
            }
        }

        private void MaintainShelteredCaseForcedAttacks()
        {
            foreach (string taskId in _shelteredForcedPartyIdsByTaskId.Keys.ToList())
            {
                PoliceTask? task = CrimeState.GetTask(taskId);
                if (!IsShelteredForcedAttackTaskActive(task))
                {
                    ReleaseShelteredForcedAttack(taskId);
                    continue;
                }

                MobileParty? offender = task!.TargetCrime?.Offender;
                MobileParty? assignee = MobileParty.All.FirstOrDefault(party =>
                    party.IsActive &&
                    string.Equals(party.StringId, task.PolicePartyId,
                        StringComparison.OrdinalIgnoreCase));
                if (offender?.IsActive != true || assignee?.IsActive != true)
                    continue;

                MobileParty forceParty = GetShelteredForcedMovementParty(offender);
                var forcedPartyIds = new HashSet<string>(
                    forceParty.AttachedParties
                        .Where(party => party?.IsActive == true)
                        .Select(party => party.StringId),
                    StringComparer.OrdinalIgnoreCase)
                {
                    forceParty.StringId,
                    offender.StringId
                };
                UpdateShelteredForcedPartyTracking(taskId, forcedPartyIds);

                TryForceShelteredCaseAttack(offender, assignee);
            }
        }

        private void UpdateShelteredForcedPartyTracking(
            string taskId,
            HashSet<string> currentPartyIds)
        {
            if (_shelteredForcedPartyIdsByTaskId.TryGetValue(
                    taskId, out HashSet<string>? previousPartyIds))
            {
                foreach (string releasedPartyId in previousPartyIds
                             .Where(partyId => !currentPartyIds.Contains(partyId))
                             .ToList())
                {
                    MobileParty? releasedParty = MobileParty.All.FirstOrDefault(party =>
                        party.IsActive &&
                        string.Equals(party.StringId, releasedPartyId,
                            StringComparison.OrdinalIgnoreCase));
                    if (releasedParty != null)
                        RestoreAi(releasedParty);
                }
            }

            _shelteredForcedPartyIdsByTaskId[taskId] = currentPartyIds;
        }

        private void ReleaseShelteredForcedAttack(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId) ||
                !_shelteredForcedPartyIdsByTaskId.TryGetValue(
                    taskId, out HashSet<string>? forcedPartyIds))
                return;

            foreach (MobileParty party in MobileParty.All.Where(party =>
                         party.IsActive && forcedPartyIds.Contains(party.StringId)).ToList())
            {
                RestoreAi(party);
            }

            _shelteredForcedPartyIdsByTaskId.Remove(taskId);
        }

        private static MobileParty GetShelteredForcedMovementParty(
            MobileParty offender)
        {
            MobileParty? armyLeader = offender.Army?.LeaderParty;
            return armyLeader?.IsActive == true ? armyLeader : offender;
        }

        private bool TryForceShelteredCaseAttack(
            MobileParty? offender,
            MobileParty? assignee)
        {
            if (offender?.IsActive != true || assignee?.IsActive != true)
                return false;
            if (offender.IsMainParty)
                return false;

            MobileParty forceParty = GetShelteredForcedMovementParty(offender);
            if (!forceParty.IsActive ||
                forceParty.CurrentSettlement != null ||
                forceParty.MapEvent != null ||
                assignee.CurrentSettlement != null ||
                assignee.MapEvent != null ||
                string.Equals(forceParty.StringId, assignee.StringId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                forceParty.Ai.SetDoNotMakeNewDecisions(false);
                forceParty.SetMoveEngageParty(
                    assignee, forceParty.NavigationCapability);
                forceParty.Ai.SetDoNotMakeNewDecisions(true);
                return true;
            }
            catch
            {
                // 即使本帧的移动命令因原版瞬时状态失败，也不能重新开放
                // 其他欲望；下一帧会再次尝试写入唯一的进攻目标。
                try { forceParty.Ai.SetDoNotMakeNewDecisions(true); } catch { }
                return false;
            }
        }

        private static bool IsShelteredForcedAttackTaskActive(PoliceTask? task)
        {
            if (task == null || !task.WarDeclared || !task.IsTargetValid())
                return false;

            MobileParty? offender = task.TargetCrime?.Offender;
            return offender?.IsActive == true && !offender.IsMainParty;
        }

        #endregion
    }
}
