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

                foreach (var victim in PlayerBehaviorPool.VictimFactions)
                {
                    if (victim == null || victim == playerFaction) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(playerFaction, victim)) continue;

                    try
                    {
                        MakePeaceAction.Apply(playerFaction, victim);
                    }
                    catch { }
                }

                PlayerBehaviorPool.ClearVictimFactions();
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
            CrimePool.TryAdd(crime.CrimeType, crime.Offender, crime.Location, crime.VictimName);
        }

        private void ClearShelteredTargetTracking(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            _shelteredTargetHoursByTaskId.Remove(taskId);
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

            // 围堵期间自动补粮，避免警察因缺粮脱离任务导致"消失后重刷"
            PoliceResourceManager.ReplenishFood(policeParty, 2);
            policeParty.Ai.SetDoNotMakeNewDecisions(true);
            policeParty.Ai.SetInitiative(1f, 0f, 999f);
            policeParty.SetMoveEngageParty(criminal, NavigationType.Default);

            if (shelteredHours % GwpTuning.Enforcement.ShelteredForceBattleIntervalHours == 0)
            {
                TryForceStartBattle(policeParty, criminal);
            }

            if (criminal.CurrentSettlement == null)
            {
                ClearShelteredTargetTracking(taskId);
                return false;
            }

            return true;
        }

        private static bool TryForceStartBattle(MobileParty attacker, MobileParty defender)
        {
            if (attacker == null || defender == null) return false;
            if (!attacker.IsActive || !defender.IsActive) return false;
            if (attacker.MapEvent != null || defender.MapEvent != null) return false;
            if (attacker.CurrentSettlement != null) return false;
            if (string.Equals(attacker.StringId, defender.StringId, StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                StartBattleAction.ApplyStartBattle(attacker, defender);
                return attacker.MapEvent != null || defender.MapEvent != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
