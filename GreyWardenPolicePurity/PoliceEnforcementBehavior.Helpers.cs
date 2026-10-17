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

        private bool TryPreparePolicePartyForVillageRelief(MobileParty? police, WardenDuty duty)
        {
            if (police == null || !police.IsActive)
                return false;

            if (police.LeaderHero == null || !police.LeaderHero.IsActive)
                return false;

            if (police.MapEvent != null && !police.MapEvent.IsFinalized)
                return false;

            if (!GwpWardenDuties.IsFreeFor(police, duty))
                return false;

            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
            {
                if (task.IsEscortingPlayer || task.IsPlayerBountyEscort ||
                    task.TargetCrimeId == CrimePool.PlayerCrimeId || task.WarDeclared)
                    return false;

                EndCaseTask(police.StringId, CaseTaskEnd.Released, "released_for_forced_duty");
            }

            GreyWardenPartyDesireBehavior.ClearIntent(police);
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
            return true;
        }

        /// <summary>
        /// Player work may release autonomous work, but respects battle, escort,
        /// player enforcement and an already arranged duel. Ordinary liability is retained.
        /// </summary>
        private bool TryPreparePartyForPlayerRequest(MobileParty? police)
        {
            if (police?.IsActive != true || police.LeaderHero?.IsActive != true)
                return false;
            if (police.MapEvent is { IsFinalized: false })
                return false;
            if (!GwpWardenDuties.IsFreeFor(police, WardenDuty.PlayerRequest))
                return false;

            ReleasePartyFromAssistance(police.StringId, "player_request_priority");
            if (police.Army != null || GwpWardenDuties.HoldsAny(police, WardenDuty.Assistance))
                return false;

            GreyWardenVillageReconstructionBehavior.ReleasePartyForForcedDuty(police);
            GreyWardenIssueResolutionBehavior.ReleasePartyForForcedDuty(police);

            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
            {
                if (task.IsEscortingPlayer || task.IsPlayerBountyEscort ||
                    task.TargetCrimeId == CrimePool.PlayerCrimeId)
                    return false;

                EndCaseTask(police.StringId, CaseTaskEnd.Released, "released_for_forced_duty");
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

        #endregion
    }
}
