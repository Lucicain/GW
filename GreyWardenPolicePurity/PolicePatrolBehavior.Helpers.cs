﻿using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    public partial class PolicePatrolBehavior
    {
        #region 辅助

        private bool IsPatrol(MobileParty party)
        {
            return GwpCommon.IsPatrolParty(party);
        }

        /// <summary>
        /// 与警察氏族恢复和平（解除战争状态，不影响通缉任务）
        /// </summary>
        private void MakePeaceWithPoliceClan()
        {
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan != null && FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
            {
                GwpCommon.TrySetNeutral(policeClan, playerFaction);
            }
        }

        /// <summary>
        /// 与警察氏族 + 受害方势力中的国家恢复和平（纠察队停止、缴纳罚金或被惩罚释放时）
        /// 注意：只停止 VictimFactions 中的国家，其他正在交战的国家不受影响
        /// </summary>
        private void MakePeaceWithPoliceAndVictims()
        {
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan != null && FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
            {
                GwpCommon.TrySetNeutral(policeClan, playerFaction);
            }

            foreach (var victim in PlayerBehaviorPool.VictimFactions)
            {
                if (victim == null || victim == playerFaction) continue;

                // Fix: victim factions never formally at war (only crime rating)
                // DeclareWarOnPlayer() only declares war between policeClan and playerFaction.
                // IsAtWarAgainstFaction(playerFaction, victim) is always false -> old code skipped.
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, victim))
                {
                    // Formal war (rare) -> MakePeaceAction
                    try
                    {
                        MakePeaceAction.Apply(playerFaction, victim);
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"灰袍调停：你与 {victim.Name} 已恢复和平。",
                            Colors.Green));
                    }
                    catch { }
                }
                else
                {
                    // Crime-only state (common) -> clear criminal rating
                    try
                    {
                        ChangeCrimeRatingAction.Apply(victim, -1000f, true);
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"灰袍调停：已清除你在 {victim.Name} 的犯罪评级。",
                            Colors.Green));
                    }
                    catch { }
                }
            }

            PlayerBehaviorPool.ClearVictimFactions();
        }

        private void ReturnAllPatrols()
        {
            var patrols = MobileParty.All.Where(IsPatrol).ToList();
            foreach (var patrol in patrols)
            {
                if (patrol == null || !patrol.IsActive) continue;

                // 进城后的纠察队不再参与地图执法，直接清理，避免“活体卡住”阻塞下一轮惩罚。
                if (patrol.CurrentSettlement != null)
                {
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                    _returningPatrolIds.Remove(patrol.StringId);
                    continue;
                }

                patrol.Ai.SetDoNotMakeNewDecisions(true);
                Settlement town = _patrolOriginSettlement ?? FindNearestTown(patrol);
                if (town != null)
                {
                    patrol.SetMoveGoToSettlement(town, MobileParty.NavigationType.Default, false);
                    if (!_returningPatrolIds.Contains(patrol.StringId))
                        _returningPatrolIds.Add(patrol.StringId);
                }
                else
                {
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                }
            }

            _activePatrolIds.Clear();
            DebugLog($"已下发返程命令，候选数量={patrols.Count}，returning={_returningPatrolIds.Count}");
        }

        private void UpdateReturningPatrols()
        {
            for (int i = _returningPatrolIds.Count - 1; i >= 0; i--)
            {
                string id = _returningPatrolIds[i];
                var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == id);

                if (patrol == null || !patrol.IsActive)
                {
                    _returningPatrolIds.RemoveAt(i);
                    continue;
                }

                // 修复：返程途中若已进入定居点，直接销毁，避免“在城里永久存活”。
                if (patrol.CurrentSettlement != null)
                {
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                    _returningPatrolIds.RemoveAt(i);
                    continue;
                }

                Settlement target = _patrolOriginSettlement ?? FindNearestTown(patrol);
                if (target != null)
                {
                    patrol.Ai.SetDoNotMakeNewDecisions(true);
                    patrol.SetMoveGoToSettlement(target, MobileParty.NavigationType.Default, false);

                    float dist = patrol.GetPosition2D.Distance(target.Position.ToVec2());
                    if (dist < 3f)
                    {
                        DebugLog($"纠察队已到达 {target.Name}，执行销毁：{id}");
                        try { DestroyPartyAction.Apply(null, patrol); } catch { }

                        var stillAlive = MobileParty.All.FirstOrDefault(p => p.StringId == id);
                        if (stillAlive == null || !stillAlive.IsActive)
                        {
                            _returningPatrolIds.RemoveAt(i);
                        }
                        else
                        {
                            // 销毁失败时保留 returning 状态，下一小时继续重试，避免“活着但失联”
                            try
                            {
                                stillAlive.Ai.SetDoNotMakeNewDecisions(true);
                                stillAlive.SetMoveGoToSettlement(target, MobileParty.NavigationType.Default, false);
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private void CleanDeadPatrols()
        {
            _activePatrolIds.RemoveAll(id =>
            {
                var p = MobileParty.All.FirstOrDefault(x => x.StringId == id);
                return p == null || !p.IsActive;
            });

            _returningPatrolIds.RemoveAll(id =>
            {
                var p = MobileParty.All.FirstOrDefault(x => x.StringId == id);
                return p == null || !p.IsActive;
            });
        }

        /// <summary>
        /// 清理已进入定居点的纠察队。
        /// 这些队伍不会再执行地图追捕，若不清理会让 HasAnyPatrol() 长期返回 true，阻断后续惩罚生成。
        /// </summary>
        private void CleanupPatrolsInsideSettlements()
        {
            foreach (var patrol in MobileParty.All.Where(p => p != null && p.IsActive && IsPatrol(p)).ToList())
            {
                if (patrol.CurrentSettlement == null) continue;

                try { DestroyPartyAction.Apply(null, patrol); } catch { }
                _activePatrolIds.Remove(patrol.StringId);
                _returningPatrolIds.Remove(patrol.StringId);
            }
        }

        private void TryReleasePatrolMeetingSuppression()
        {
            if (!_suppressPatrolMeetings) return;
            if (_playerCapturedByPatrol) return;

            bool hasTrackedPatrol = _activePatrolIds.Count > 0 || _returningPatrolIds.Count > 0;
            if (hasTrackedPatrol) return;

            // 仅“地图上可行动”的纠察队才阻止解除抑制；城内残留队伍会被清理，不应继续阻塞。
            bool hasWorldPatrol = MobileParty.All.Any(p => p.IsActive && IsPatrol(p) && p.CurrentSettlement == null);
            if (hasWorldPatrol) return;

            _suppressPatrolMeetings = false;
            DebugLog("返程销毁已完成，恢复正常会话。");
        }

        private void TryFinishSuppressedPatrolEncounter()
        {
            try
            {
                if (!PlayerEncounter.IsActive) return;
                var encountered = PlayerEncounter.EncounteredParty?.MobileParty;
                if (encountered == null || !IsPatrol(encountered)) return;

                GwpCommon.TryFinishPlayerEncounter();
            }
            catch { }
        }

        private Hero? GetPatrolBarterHero()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return null;

            Hero leader = policeClan.Leader;
            if (leader != null && leader.IsActive && !leader.IsDead && !leader.IsChild)
                return leader;

            return policeClan.Heroes.FirstOrDefault(h =>
                h != null &&
                h.IsActive &&
                !h.IsDead &&
                !h.IsChild &&
                !h.IsPrisoner);
        }

        private bool StartPatrolPaymentBarter(MobileParty patrolPartyMobile, int amount, string barterDisplayName)
        {
            if (patrolPartyMobile == null || !patrolPartyMobile.IsActive || MobileParty.MainParty == null)
                return false;

            Hero barterHero = Hero.OneToOneConversationHero ?? GetPatrolBarterHero();
            if (barterHero == null)
                return false;

            PartyBase patrolParty = patrolPartyMobile.Party;
            PartyBase playerParty = MobileParty.MainParty.Party;
            if (patrolParty == null || playerParty == null)
                return false;

            int negotiationAmount = Math.Max(1, amount);

            var patrolBribe = new GwpBribeBarterable(
                barterHero,
                Hero.MainHero,
                patrolParty,
                playerParty,
                negotiationAmount,
                barterDisplayName);

            try
            {
                Campaign.Current.BarterManager.StartBarterOffer(
                    Hero.MainHero,
                    barterHero,
                    playerParty,
                    patrolParty,
                    null,
                    InitializePatrolBribeBarterContext,
                    0,
                    false,
                    new[] { patrolBribe });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializePatrolBribeBarterContext(Barterable barterable, BarterData args, object obj)
        {
            return barterable is GwpBribeBarterable;
        }

        private MobileParty ResolveEscortPatrol(MobileParty patrolHint)
        {
            if (patrolHint != null && patrolHint.IsActive) return patrolHint;

            if (_dialogPatrol != null && _dialogPatrol.IsActive && IsPatrol(_dialogPatrol))
                return _dialogPatrol;

            foreach (string id in _activePatrolIds)
            {
                var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == id);
                if (patrol != null && patrol.IsActive) return patrol;
            }

            return MobileParty.All.FirstOrDefault(p => p.IsActive && IsPatrol(p));
        }

        private Settlement FindNearestTown(MobileParty party)
        {
            return GwpCommon.FindNearestTown(party)!;
        }

        #endregion

        #region 遭遇战拦截

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            // 拦截 MapEvent 开始事件，在纠察队发接触时强制进入对话（绕过战斗入口）
            // 检查是否为纠察队 vs 玩家
            bool isPatrolEvent = false;
            bool isPlayerInvolved = false;
            bool hasActivePatrolInvolved = false;
            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p.MobileParty != null && IsPatrol(p.MobileParty))
                {
                    isPatrolEvent = true;
                    if (!_returningPatrolIds.Contains(p.MobileParty.StringId))
                        hasActivePatrolInvolved = true;
                }
                if (p.MobileParty != null && p.MobileParty.IsMainParty)
                    isPlayerInvolved = true;
            }

            if (!isPatrolEvent || !isPlayerInvolved)
                return;

            if (_suppressPatrolMeetings)
            {
                try
                {
                    if (PlayerEncounter.IsActive)
                    {
                        PlayerEncounter.LeaveEncounter = true;
                        PlayerEncounter.Finish(false);
                    }
                }
                catch { }
                return;
            }

            Clan policeClan = PoliceStats.GetPoliceClan();
            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            bool atWar = policeClan != null && playerFaction != null &&
                         FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction);

            if (!atWar && hasActivePatrolInvolved && PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                // 未宣战时强制开启对话，防止引擎自动进入战斗模式
                PlayerEncounter.DoMeeting();
            }
        }

       #endregion
    }
}
