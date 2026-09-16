﻿using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;

        /// <summary>
        /// 这一场战斗里，因为玩家跟灰袍一起动手才打起来的势力。只在本场战斗内有效、
        /// 不进存档：战斗一结束就中立掉，此外一概不管。
        /// </summary>
        private IFaction? _playerHostilityWarFromThisBattle;

        /// <summary>
        /// 等灰袍出面了结的势力。只有"替灰袍打的那几仗"才进这张表，玩家自己的战争不进。
        /// </summary>
        private List<string> _mediationRequests = new List<string>();
        private static PoliceAntiWarDeclaration? _instance;

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnBattleEnded);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        }

        public PoliceAntiWarDeclaration() => _instance = this;

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("gwp_mediation_requests", ref _mediationRequests);
            _mediationRequests ??= new List<string>();
        }

        /// <summary>
        /// 只记玩家亲手动手引发的宣战（CausedByPlayerHostility）。是不是"跟灰袍一起
        /// 打的这一仗"留到战斗结束时按参战双方判定，避免依赖宣战与遭遇战的先后顺序。
        /// </summary>
        private void OnWarDeclared(IFaction faction1, IFaction faction2,
            DeclareWarAction.DeclareWarDetail detail)
        {
            if (detail != DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility) return;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (playerFaction == null || policeClan == null) return;

            IFaction? other = faction1 == playerFaction ? faction2
                : faction2 == playerFaction ? faction1
                : null;
            if (other == null || other == playerFaction || other == policeClan) return;
            if (other is Clan bandit && bandit.IsBanditFaction) return;

            _playerHostilityWarFromThisBattle = other;
        }

        /// <summary>
        /// 记下"这一战是替灰袍打的"。合格的口径有两条，满足其一即可：本场玩家这一侧
        /// 有灰袍部队；或者对面那位本来就挂在我们案卷上（办案、撞见打村民打商队、烧村，
        /// 都会先入册）。合格的不当场讲和——玩家可能正在办事，冷不丁和平会打断他；
        /// 记账，等他自己去找灰袍出面。不合格的一概不记：玩家为别的缘由开的战、
        /// 王国决议开的战、以及他自己承接的悬赏（另有调停流程）。
        /// </summary>
        private void ResolvePlayerAssistWar(MapEvent mapEvent)
        {
            IFaction? pending = _playerHostilityWarFromThisBattle;
            // 无论本场是否成立，都不让这个标记跨到下一场战斗。
            _playerHostilityWarFromThisBattle = null;
            if (pending == null) return;

            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null || playerFaction == pending) return;
            if (!FactionManager.IsAtWarAgainstFaction(playerFaction, pending)) return;

            MapEventSide? playerSide = FindPlayerSide(mapEvent);
            if (playerSide == null) return;
            if (!playerSide.OtherSide.Parties.Any(entry => entry.Party?.MapFaction == pending))
                return;

            // 一、制止正在发生的三类案件
            bool stoppedCrimeInProgress = StoppedCrimeInProgress(mapEvent, playerSide);
            // 二、看见灰袍在打，过去帮忙
            bool wardenOnPlayerSide = playerSide.Parties.Any(entry =>
                entry.Party?.IsMobile == true && IsPoliceParty(entry.Party.MobileParty));
            // 三、玩家自己承办的案子
            bool playerHeldTheCase = playerSide.OtherSide.Parties.Any(entry =>
                entry.Party?.MobileParty?.LeaderHero != null &&
                PlayerBountyBehavior.IsCaseHeldByPlayer(
                    entry.Party.MobileParty.LeaderHero.StringId));
            if (!stoppedCrimeInProgress && !wardenOnPlayerSide && !playerHeldTheCase) return;

            if (_mediationRequests.Contains(pending.StringId, StringComparer.OrdinalIgnoreCase))
                return;
            _mediationRequests.Add(pending.StringId);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_warden_mediation_owed}The Wardens will speak for you over {VAR_1} when you ask them to.",
                    "VAR_1", pending.Name), Colors.Cyan));
            GwpAiDiagnostics.WritePlayerJusticeState("MEDIATION_REQUEST_RECORDED",
                "faction=" + pending.StringId +
                "; stoppedCrimeInProgress=" + stoppedCrimeInProgress +
                "; wardenOnPlayerSide=" + wardenOnPlayerSide +
                "; playerHeldTheCase=" + playerHeldTheCase);
        }

        /// <summary>
        /// 由别处直接登记一笔"替灰袍打出来的战争"。支援领主按任务逻辑替玩家宣的战
        /// 走的是 <c>DeclareWarAction.ApplyByDefault</c>，不带 CausedByPlayerHostility，
        /// 本类的 WarDeclared 监听看不到，必须由发起方自己报进来。
        /// </summary>
        internal static void RecordMediationRequest(IFaction? faction, string reason)
        {
            if (_instance == null || faction == null) return;
            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null || faction == playerFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(playerFaction, faction)) return;
            if (_instance._mediationRequests.Contains(faction.StringId,
                    StringComparer.OrdinalIgnoreCase)) return;

            _instance._mediationRequests.Add(faction.StringId);
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_warden_mediation_owed}The Wardens will speak for you over {VAR_1} when you ask them to.",
                    "VAR_1", faction.Name), Colors.Cyan));
            GwpAiDiagnostics.WritePlayerJusticeState("MEDIATION_REQUEST_RECORDED",
                "faction=" + faction.StringId + "; reason=" + reason);
        }

        /// <summary>灰袍手上是否还有替玩家了结的战争。</summary>
        internal static bool HasMediationRequests() =>
            _instance != null && _instance.LiveMediationFactions().Any();

        private IEnumerable<IFaction> LiveMediationFactions()
        {
            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null) yield break;
            foreach (string id in _mediationRequests.ToList())
            {
                IFaction? faction = ResolveFaction(id);
                if (faction == null || faction == playerFaction ||
                    !FactionManager.IsAtWarAgainstFaction(playerFaction, faction))
                {
                    // 势力没了，或者玩家自己已经讲和——这笔不欠了。
                    _mediationRequests.Remove(id);
                    continue;
                }
                yield return faction;
            }
        }

        private static IFaction? ResolveFaction(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (Kingdom kingdom in Kingdom.All)
                if (string.Equals(kingdom.StringId, id, StringComparison.OrdinalIgnoreCase))
                    return kingdom;
            foreach (Clan clan in Clan.All)
                if (string.Equals(clan.StringId, id, StringComparison.OrdinalIgnoreCase))
                    return clan;
            return null;
        }

        /// <summary>
        /// 灰袍出面，把因为帮他们办事而结下的仇一口气了结。用原版 MakePeaceAction 而不是
        /// 裸的 FactionManager.SetNeutral——后者不会把定居点标记为待重绘，地图上会一直
        /// 红着；前者还会派发原版的和平事件。
        /// </summary>
        internal static int ApplyWardenMediation()
        {
            if (_instance == null) return 0;
            IFaction? playerFaction = Hero.MainHero?.MapFaction;
            if (playerFaction == null) return 0;

            int settled = 0;
            foreach (IFaction faction in _instance.LiveMediationFactions().ToList())
            {
                try
                {
                    MakePeaceAction.Apply(playerFaction, faction);
                    settled++;
                    GwpAiDiagnostics.WritePlayerJusticeState("MEDIATION_PEACE_APPLIED",
                        "faction=" + faction.StringId);
                }
                catch { }
                _instance._mediationRequests.RemoveAll(id =>
                    string.Equals(id, faction.StringId, StringComparison.OrdinalIgnoreCase));
            }
            return settled;
        }

        /// <summary>
        /// 这一仗本身就是三类案件正在发生：要么是一场对村庄的劫掠，要么玩家这一侧
        /// 站着正在挨打的村民或商队。只看本场，不翻旧账——旧案另有承办流程。
        /// </summary>
        private static bool StoppedCrimeInProgress(MapEvent mapEvent, MapEventSide playerSide)
        {
            if (mapEvent.IsRaid && mapEvent.MapEventSettlement?.IsVillage == true) return true;
            return playerSide.Parties.Any(entry =>
                entry.Party?.IsMobile == true &&
                (entry.Party.MobileParty.IsVillager || entry.Party.MobileParty.IsCaravan));
        }

        private static MapEventSide? FindPlayerSide(MapEvent mapEvent)
        {
            foreach (MapEventParty entry in mapEvent.AttackerSide.Parties)
                if (entry.Party?.MobileParty?.IsMainParty == true) return mapEvent.AttackerSide;
            foreach (MapEventParty entry in mapEvent.DefenderSide.Parties)
                if (entry.Party?.MobileParty?.IsMainParty == true) return mapEvent.DefenderSide;
            return null;
        }

        private void OnBattleEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;

            ResolvePlayerAssistWar(mapEvent);

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            bool policeInvolved = false;
            bool patrolInvolved = false;
            bool delayPatrolInvolved = false;
            bool regularPoliceInvolved = false;
            bool playerInvolved = false;
            bool policeWasExecutingPlayerTask = false;
            IFaction enemyFaction = null!;

            foreach (var party in mapEvent.InvolvedParties)
            {
                if (party?.MobileParty == null) continue;

                if (IsPoliceParty(party.MobileParty))
                {
                    policeInvolved = true;
                    if (GwpCommon.IsPatrolParty(party.MobileParty))
                        patrolInvolved = true;
                    else if (GwpCommon.IsEnforcementDelayPatrolParty(party.MobileParty))
                        delayPatrolInvolved = true;
                    else
                        regularPoliceInvolved = true;

                    PoliceTask? task = CrimeState.GetTask(party.MobileParty.StringId);
                    if (task?.TargetCrime?.Offender?.IsMainParty == true)
                        policeWasExecutingPlayerTask = true;
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

            // 延迟纠察队单独作战时不在这里和平（用于拖住战线）；
            // 但若正式警察也在同一场战斗中，则战后必须和平。
            if (delayPatrolInvolved && !regularPoliceInvolved) return;

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

            if (playerInvolved &&
                policeInvolved &&
                policeOnWinningSide &&
                policeWasExecutingPlayerTask)
            {
                return;
            }

            // ★ 功能 3：玩家打赢执法警察 → 主动与警察势力和平
            // 原代码只处理 policeInvolved && enemyFaction != null（警察打其他NPC），
            // 但警察自身是"敌人"时 enemyFaction 为 null，导致执法警察战败后持续宣战 → -4声望
            // 纠察队由 PolicePatrolBehavior.OnPlayerVictory()+MakePeaceWithPoliceClan() 已处理，此处排除
            if (policeInvolved &&
                playerInvolved &&
                !policeOnWinningSide &&
                !patrolInvolved &&
                policeWasExecutingPlayerTask)
            {
                IFaction? playerFaction = Hero.MainHero?.MapFaction;
                // 玩家参与的讲和必须走 MakePeaceAction：裸的 SetNeutral 不会把定居点
                // 标记为待重绘，灰袍的领地会在玩家地图上一直红着。
                if (playerFaction != null) MakePeaceAction.Apply(policeClan, playerFaction);
            }

            if (policeInvolved && enemyFaction != null)
            {
                if (enemyFaction is Clan c && c.IsOutlaw && c.IsBanditFaction)
                    return;

                // 一次地图战斗结束不代表执法任务结束。仍有案件、玩家纠察或悬赏
                // 理由时维持战争，避免目标撤退后立即和平并拆散追击。
                if (GwpPoliceWarReasonService.HasLegitimateWarReason(enemyFaction))
                    return;

                // 战后讲和一直没有留痕，实机里分不清"玩家没开战"和"开了又被收掉"。
                GwpAiDiagnostics.WritePlayerJusticeState("POLICE_BATTLE_PEACE",
                    "faction=" + enemyFaction.StringId +
                    "; playerInvolved=" + playerInvolved +
                    "; regularPolice=" + regularPoliceInvolved +
                    "; delayPatrol=" + delayPatrolInvolved);
                GwpCommon.TrySetNeutral(policeClan, enemyFaction);
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
            if (GwpCommon.IsPatrolParty(party))
                return true;

            return false;
        }
    }
}
