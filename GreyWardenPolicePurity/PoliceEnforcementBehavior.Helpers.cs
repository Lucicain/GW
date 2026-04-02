﻿using System;
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
                if (displacedCrime?.Offender != null && displacedCrime.Offender.IsActive)
                {
                    CrimeState.TryAdd(
                        displacedCrime.CrimeType,
                        displacedCrime.Offender,
                        displacedCrime.Location,
                        displacedCrime.VictimName);
                }

                Clan? policeClan = PoliceStats.GetPoliceClan();
                if (policeClan != null &&
                    warTarget != null &&
                    !GwpPoliceWarReasonService.HasLegitimateWarReason(warTarget))
                {
                    GwpCommon.TrySetNeutral(policeClan, warTarget);
                }
            }

            GwpCommon.TryResetAi(police);
            PoliceResourceManager.CancelResupply(police);
            PoliceResourceManager.StartResupply(police);
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

        private void RegisterDeterrenceForDefeatedNonPlayerLords(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner || mapEvent.Winner == null)
                return;

            MapEventSide? loserSide = mapEvent.Winner == mapEvent.AttackerSide
                ? mapEvent.DefenderSide
                : mapEvent.AttackerSide;
            if (loserSide == null)
                return;

            HashSet<string> processedHeroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var involvedParty in loserSide.Parties)
            {
                PartyBase? losingPartyBase = involvedParty?.Party;
                MobileParty? losingParty = losingPartyBase?.MobileParty;
                Hero? leader = losingParty?.LeaderHero
                               ?? losingPartyBase?.LeaderHero
                               ?? losingParty?.Owner
                               ?? losingPartyBase?.Owner;
                if (leader == null)
                    continue;

                if (losingParty?.IsMainParty == true || leader == Hero.MainHero)
                    continue;

                if (leader.Clan != null &&
                    string.Equals(leader.Clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (losingParty?.ActualClan != null &&
                    string.Equals(losingParty.ActualClan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!processedHeroIds.Add(leader.StringId))
                    continue;

                PoliceAIDeterrenceBehavior.RegisterEnforcementVictoryAgainst(leader, losingParty);
            }

            if (PoliceAIDeterrenceBehavior.TryBuildHighestDeterrenceSnapshot(out string debugText))
            {
                InformationManager.DisplayMessage(new InformationMessage(debugText, Colors.Cyan));
            }
        }

        private void RestoreAi(MobileParty party)
        {
            if (party == null || !party.IsActive) return;
            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.SetInitiative(0f, 0f, 0f);
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
            if (crime?.Offender == null) return;
            CrimeState.TryAdd(crime.CrimeType, crime.Offender, crime.Location, crime.VictimName);
        }

        private void ClearShelteredTargetTracking(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            _shelteredTargetHoursByTaskId.Remove(taskId);
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

            int shelteredHours = 0;
            _shelteredTargetHoursByTaskId.TryGetValue(taskId, out shelteredHours);
            shelteredHours++;
            _shelteredTargetHoursByTaskId[taskId] = shelteredHours;
            float distToShelter = policeParty.GetPosition2D.Distance(shelter.GetPosition2D);
            float distToGate = policeParty.GetPosition2D.Distance(shelter.GatePosition.ToVec2());
            int stoppedHours = UpdateShelteredPoliceStoppedHours(taskId, policeParty);

            // 围堵期间自动补粮，避免警察因缺粮脱离任务导致"消失后重刷"
            PoliceResourceManager.ReplenishFood(policeParty, 2);
            policeParty.Ai.SetDoNotMakeNewDecisions(true);
            policeParty.Ai.SetInitiative(1f, 0f, 999f);
            policeParty.SetMoveEngageParty(criminal, NavigationType.Default);

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
                TryForceExpelShelteredCriminal(policeParty, criminal);
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

        private static bool TryForceExpelShelteredCriminal(MobileParty attacker, MobileParty defender)
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

                    LeaveSettlementAction.ApplyForParty(expelParty);

                    try { expelParty.SetMoveModeHold(); } catch { }
                    foreach (MobileParty attachedParty in expelParty.AttachedParties)
                    {
                        try { attachedParty.SetMoveModeHold(); } catch { }
                    }

                    // 让刚被逼出城的目标先停住，避免它立刻重新钻回定居点，
                    // 然后由警察已有的追击命令自然接管战斗。
                    try { defender.SetMoveModeHold(); } catch { }
                }

                return defender.CurrentSettlement == null;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
