using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 警察和平系统
    /// 
    /// 策略：战斗结束后立即和平（警察不长期保持战争状态）
    /// 野怪不需要和平处理
    /// </summary>
    public class PoliceAntiWarDeclaration : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnBattleEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            bool policeInvolved = false;
            bool patrolInvolved = false;
            bool playerInvolved = false;
            IFaction enemyFaction = null;

            foreach (var party in mapEvent.InvolvedParties)
            {
                if (party?.MobileParty == null) continue;

                if (IsPoliceParty(party.MobileParty))
                {
                    policeInvolved = true;
                    // 纠察队参与的战斗，由 PolicePatrolBehavior 自己处理和平
                    if (party.MobileParty.StringId != null &&
                        party.MobileParty.StringId.StartsWith("gwp_patrol_"))
                        patrolInvolved = true;
                }
                else if (party.MobileParty.IsMainParty)
                {
                    playerInvolved = true;
                }
                else if (party.MobileParty.ActualClan != null)
                {
                    enemyFaction = party.MobileParty.ActualClan.MapFaction;
                }
            }

            // 纠察队的战斗不在这里和平，由 PolicePatrolBehavior 在惩罚后统一处理
            if (patrolInvolved) return;

            // 核心修复（v2）：不能用 CrimePool.IsPlayerHunted 判断——
            // 玩家被击败后 MainParty.IsActive == false，导致 IsOffenderValid() 返回 false，
            // IsPlayerHunted 误判为 false，守卫被跳过，SetNeutral 被调用，
            // Bannerlord 引擎自动释放俘虏，玩家在惩罚前就被释放了。
            //
            // 正确做法：直接检查战斗胜负——若警察在胜利方且玩家参战，
            // 说明玩家刚被警察击败，绝不能此时和平。
            // 必须等 PoliceEnforcementBehavior.OnMapEventEnded 处理押送 + 惩罚后再和平。
            bool policeOnWinningSide = false;
            if (mapEvent.HasWinner && mapEvent.Winner != null)
            {
                foreach (var p in mapEvent.Winner.Parties)
                {
                    if (p?.Party?.IsMobile == true && IsPoliceParty(p.Party.MobileParty))
                    {
                        policeOnWinningSide = true;
                        break;
                    }
                }
            }

            if (playerInvolved && policeInvolved && policeOnWinningSide)
            {
                return;
            }

            // ★ 功能 3：玩家打赢执法警察 → 主动与警察势力和平
            // 原代码只处理 policeInvolved && enemyFaction != null（警察打其他NPC），
            // 但警察自身是"敌人"时 enemyFaction 为 null，导致执法警察战败后持续宣战 → -4声望
            // 纠察队由 PolicePatrolBehavior.OnPlayerVictory()+MakePeaceWithPoliceClan() 已处理，此处排除
            if (policeInvolved && playerInvolved && !policeOnWinningSide && !patrolInvolved)
            {
                IFaction playerFaction = Hero.MainHero?.MapFaction;
                if (playerFaction != null && FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
                {
                    try { FactionManager.SetNeutral(policeClan, playerFaction); } catch { }
                }
            }

            if (policeInvolved && enemyFaction != null)
            {
                if (enemyFaction is Clan c && c.IsOutlaw && c.IsBanditFaction)
                    return;

                if (FactionManager.IsAtWarAgainstFaction(policeClan, enemyFaction))
                {
                    try { FactionManager.SetNeutral(policeClan, enemyFaction); } catch { }
                }
            }
        }

        private bool IsPoliceParty(MobileParty party)
        {
            if (party == null) return false;

            // 警察家族部队
            if (party.ActualClan != null &&
                string.Equals(party.ActualClan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase))
                return true;

            // 纠察队（CustomPartyComponent，可能 ActualClan 未生效）
            if (party.StringId != null && party.StringId.StartsWith("gwp_patrol_"))
                return true;

            return false;
        }
    }
}
