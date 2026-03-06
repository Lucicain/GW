﻿using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家悬赏猎人系统
    ///
    /// 招募流程：
    ///   声望 >= 阈值 → 派20人招募使者 → DoMeeting 对话 →
    ///   接受：发放黑袍指挥官套装 + 永久标记已接受
    ///   拒绝：永久标记已拒绝，不再发起招募
    ///
    /// 接任务条件（三选一都满足才生效）：
    ///   1. 已接受招募（_recruitmentAccepted）
    ///   2. 声望 >= 阈值
    ///   3. 穿戴黑袍指挥官全套
    ///
    /// 任务流程：
    ///   右侧通知图标 → 点击弹窗 → 接受 → 追击目标 →
    ///   胜利后前往警察领主对话领取赏金 → 灰袍调停战争
    /// </summary>
    public partial class PlayerBountyBehavior : CampaignBehaviorBase
    {
        // ---- 持久化状态 ----
        private string _activeBountyTargetId = null!;
        private string _activeBountyTargetName = null!;     // 目标显示名（读档后恢复任务标题用）
        private string _activeBountyTargetFactionId = null!;
        private int _activeBountyTargetSize = 0;
        private bool _waitingForCollection = false;
        private int _pendingReward = 0;
        private bool _recruitmentOffered = false;  // 是否已发出过招募邀请（拒绝或接受后均置true，防重复）
        private bool _recruitmentAccepted = false; // 玩家是否接受了招募
        private string _escortPolicePartyId = null!; // 当前护送玩家追捕的警察部队 StringId（null=无护送，向族长领赏）

        // ---- 运行时状态（不持久化）----
        private CampaignTime _lastOfferTime = CampaignTime.Zero;
        private CampaignTime _lastIntelReportTime = CampaignTime.Zero; // 运行时，不持久化（读档后立即触发一次，好体验）
        private BountyHunterQuest _activeQuest = null!;
        private string _recruitmentPatrolId = null!;       // 当前在场的招募使者队ID
        private Settlement _recruitmentPatrolOrigin = null!; // 使者出发的定居点（返回目标）
        private bool _recruitmentPatrolReturning = false;  // 是否已进入返回阶段
        /// <summary>
        /// 读档后重连 _activeQuest 的等待标志。
        /// OnSessionLaunched 中置 true（若有活跃悬赏）；重连成功或创建兜底 Quest 后置 false。
        ///
        /// 两条重连路径（按触发顺序）：
        ///   A. InitializeQuestOnGameLoad → OnQuestLoadedFromSave → _activeQuest = Quest-A（主路径）
        ///   B. OnHourlyTick 首次：若 A 未触发（SyncData 早于 InitializeQuestOnGameLoad），
        ///      此时查 QM → 找到 Quest-A 重连；否则创建 Quest-B（旧存档兼容）
        /// </summary>
        private bool _awaitingQuestReconnect = false;

        private static bool _notificationTypeRegistered = false;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // ── 招募状态（用 int 存 bool，兼容性更好）────────────────────────────────
            int offeredInt  = _recruitmentOffered  ? 1 : 0;
            int acceptedInt = _recruitmentAccepted ? 1 : 0;
            dataStore.SyncData("gwp_recruitment_offered",  ref offeredInt);
            dataStore.SyncData("gwp_recruitment_accepted", ref acceptedInt);

            // ── 悬赏任务持久化状态 ────────────────────────────────────────────────────
            // 存档时把当前值序列化；读档时恢复（基元类型 ref 直接支持）
            int waitingInt = _waitingForCollection ? 1 : 0;
            dataStore.SyncData("gwp_bounty_target_id",        ref _activeBountyTargetId);
            dataStore.SyncData("gwp_bounty_target_name",      ref _activeBountyTargetName); // 读档后补回任务标题
            dataStore.SyncData("gwp_bounty_target_faction_id",ref _activeBountyTargetFactionId);
            dataStore.SyncData("gwp_bounty_target_size",      ref _activeBountyTargetSize);
            dataStore.SyncData("gwp_bounty_waiting",          ref waitingInt);
            dataStore.SyncData("gwp_bounty_pending_reward",   ref _pendingReward);
            dataStore.SyncData("gwp_bounty_escort_party_id",  ref _escortPolicePartyId); // 护送警察部队 ID

            if (dataStore.IsLoading)
            {
                _recruitmentOffered    = offeredInt  != 0;
                _recruitmentAccepted   = acceptedInt != 0;
                _waitingForCollection  = waitingInt  != 0;
                // null 保护：旧存档没有此 key 时 SyncData 不修改变量，保持 null
                _activeBountyTargetName ??= "";
                // _escortPolicePartyId 读档后保持原值（null 或 ID）
                // UpdateEscortPatrol 在 OnHourlyTick 中会验证部队是否仍然存活

                // 运行时状态读档时清零（不持久化）
                _recruitmentPatrolId        = null!;
                _recruitmentPatrolOrigin    = null!;
                _recruitmentPatrolReturning = false;
                _awaitingQuestReconnect     = false; // 由 TryRestoreBountyQuestOnSessionStart 按需设置
            }
        }

        #region 招募使者部队

        private void SpawnRecruitmentPatrol()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            Settlement spawnPoint = FindNearestTown(MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero);
            if (spawnPoint == null) return;

            Hero clanLeader = policeClan.Leader;
            if (clanLeader == null) return;

            string patrolId = GwpIds.RecruitmentPatrolPrefix + MBRandom.RandomInt(10000, 99999);

            try
            {
                MobileParty patrol = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    spawnPoint.GatePosition,
                    1f,
                    spawnPoint,
                    new TextObject("灰袍招募使者"),
                    policeClan,
                    policeClan.DefaultPartyTemplate,
                    clanLeader,
                    "", "",
                    5f,
                    false);

                patrol.StringId = patrolId;
                patrol.ActualClan = policeClan;
                patrol.MemberRoster.Clear();

                CharacterObject infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
                if (infantry != null)
                    patrol.MemberRoster.AddToCounts(infantry, GwpTuning.Bounty.RecruitmentPatrolSize);

                PoliceResourceManager.ReplenishFood(patrol, 5);
                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.Ai.SetInitiative(1f, 0f, 999f);

                _recruitmentPatrolId = patrolId;
                _recruitmentPatrolOrigin = spawnPoint;  // 记录出发点，供返回时使用
                _recruitmentPatrolReturning = false;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"灰袍招募使者正从 {spawnPoint.Name} 出发前来拜访...",
                    Colors.Cyan));
            }
            catch (Exception ex)
            {
                // 生成招募使者失败（内部错误，静默忽略）
                _ = ex;
            }
        }

        private void UpdateRecruitmentPatrol()
        {
            if (_recruitmentPatrolId == null) return;

            var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == _recruitmentPatrolId);
            if (patrol == null || !patrol.IsActive)
            {
                _recruitmentPatrolId = null!;
                _recruitmentPatrolReturning = false;
                return;
            }

            // ── 返回阶段（招募已结束或粮草耗尽）────────────────────────────────────
            if (_recruitmentPatrolReturning)
            {
                Settlement target = _recruitmentPatrolOrigin ?? FindNearestTown(patrol.GetPosition2D);
                if (target == null)
                {
                    // 找不到目标，直接销毁
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                    _recruitmentPatrolId = null!;
                    _recruitmentPatrolReturning = false;
                    return;
                }

                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.SetMoveGoToSettlement(target, MobileParty.NavigationType.Default, false);

                float dist = patrol.GetPosition2D.Distance(target.GetPosition2D);
                if (dist < 3f)
                {
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                    _recruitmentPatrolId = null!;
                    _recruitmentPatrolReturning = false;
                }
                return;
            }

            // ── 触发返回的条件 ───────────────────────────────────────────────────────
            // 招募已结束或粮草耗尽 → 立刻切换为返回状态并下达移动命令
            if (_recruitmentOffered || patrol.ItemRoster.TotalFood <= 0)
            {
                // TriggerPatrolReturn 已在 Consequence 中提前调用，这里只是 hourly tick 的兜底
                if (!_recruitmentPatrolReturning)
                    TriggerPatrolReturn();
                return;
            }

            // ── 正常追踪阶段 ─────────────────────────────────────────────────────────
            MobileParty player = MobileParty.MainParty;
            if (player != null && player.IsActive)
                patrol.SetMoveEngageParty(player, MobileParty.NavigationType.Default);
        }

        /// <summary>
        /// 立刻将招募使者部队切换为返回状态，并下达前往定居点的移动命令。
        /// 可在对话 Consequence 中安全调用（不销毁部队，仅改变 AI 命令）。
        /// </summary>
        private void TriggerPatrolReturn()
        {
            _recruitmentPatrolReturning = true;
            if (_recruitmentPatrolId == null) return;

            var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == _recruitmentPatrolId);
            if (patrol == null || !patrol.IsActive) return;

            Settlement target = _recruitmentPatrolOrigin ?? FindNearestTown(patrol.GetPosition2D);
            if (target == null) return;

            patrol.Ai.SetDoNotMakeNewDecisions(true);
            patrol.SetMoveGoToSettlement(target, MobileParty.NavigationType.Default, false);
        }

        private void DestroyRecruitmentPatrol()
        {
            if (_recruitmentPatrolId == null) return;
            var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == _recruitmentPatrolId);
            if (patrol != null && patrol.IsActive)
                try { DestroyPartyAction.Apply(null, patrol); } catch { }
            _recruitmentPatrolId = null!;
        }

        private bool IsRecruitmentPatrol(MobileParty party) =>
            party?.StringId?.StartsWith(GwpIds.RecruitmentPatrolPrefix, StringComparison.Ordinal) == true;

        #endregion

        #region 悬赏护送部队

        /// <summary>
        /// 每小时更新护送警察部队的 AI 行动。
        ///
        /// 追击阶段（_activeBountyTargetId != null）：
        ///   护送方跟随玩家（每小时更新目标点），玩家冲向犯人时护送方可作为援军加入。
        ///   注意：PlayerBountyBehavior.OnHourlyTick 在 PoliceEnforcementBehavior 之后
        ///   注册，因此每次 tick 中我们覆盖 PoliceEnforcementBehavior 设置的移动命令。
        ///
        /// 等待领赏阶段（_waitingForCollection == true）：
        ///   护送方原地等待玩家前来领取赏金（SetDoNotMakeNewDecisions 已阻止其移动）。
        ///
        /// 护送方消失时：
        ///   降级为族长领赏路径（清除 _escortPolicePartyId）。
        /// </summary>
        private void UpdateEscortPatrol()
        {
            if (string.IsNullOrEmpty(_escortPolicePartyId)) return;

            var escort = MobileParty.All.FirstOrDefault(p => p.StringId == _escortPolicePartyId);
            if (escort == null || !escort.IsActive)
            {
                // 护送方消失 → 降级为族长领赏路径
                _escortPolicePartyId = null!;
                if (_waitingForCollection || !string.IsNullOrEmpty(_activeBountyTargetId))
                    InformationManager.DisplayMessage(new InformationMessage(
                        "护送警察已失联，请前往警察族长处领取赏金。", Colors.Yellow));
                return;
            }

            // ── 自愈：读档后 IsPlayerBountyEscort 标志丢失（CrimePool 不持久化），在此补设 ──
            CrimePool.SetBountyEscortFlag(_escortPolicePartyId, true);

            // ── 粮食检查：直接补粮，不让护送方跑去城市补给（避免脱离跟随） ──
            if (escort.ItemRoster.TotalFood <= 0)
                PoliceResourceManager.ReplenishFood(escort, 5);

            // ── 追击阶段：距目标足够近时宣战（护送方保持跟随玩家，不主动接战）──────────
            // 逻辑：护送方宣战 → 玩家宣战并与犯人交战 → 引擎自动将附近的护送方拉入战斗。
            // 不切换为 SetMoveEngageParty：若护送方先接战，玩家尚未宣战则无法参战（原版机制）。
            if (!string.IsNullOrEmpty(_activeBountyTargetId))
            {
                var criminal = MobileParty.All.FirstOrDefault(
                    p => p.StringId == _activeBountyTargetId && p.IsActive);

                if (criminal != null)
                {
                    float dist = escort.GetPosition2D.Distance(criminal.GetPosition2D);
                    if (dist < GwpTuning.Bounty.EscortEngageDistance)
                        TryDeclareWarForEscort(criminal); // 仅宣战，AI 命令由下方 SetMoveEscortParty 统一下达
                }
            }

            // ── 追击阶段 + 等待领赏阶段：均跟随玩家 ──
            bool isActive = !string.IsNullOrEmpty(_activeBountyTargetId) || _waitingForCollection;
            if (!isActive) return;

            MobileParty player = MobileParty.MainParty;
            if (player == null || !player.IsActive) return;

            // ★ SetMoveEscortParty：引擎持续追踪目标部队位置，无需每小时手动更新坐标
            escort.Ai.SetDoNotMakeNewDecisions(true);
            escort.SetMoveEscortParty(player, MobileParty.NavigationType.Default, false);
        }

        /// <summary>
        /// 为护送方宣战：与 PoliceEnforcementBehavior.DeclareWar 逻辑一致。
        /// 同时跳过盗匪派系（不需要宣战即可交战）。
        /// </summary>
        private static void TryDeclareWarForEscort(MobileParty criminal)
        {
            try
            {
                Clan policeClan = PoliceStats.GetPoliceClan();
                if (policeClan == null) return;

                Clan criminalClan = criminal.ActualClan;
                if (criminalClan == null) return;

                // 盗匪派系无需宣战即可交战
                if (criminalClan.IsOutlaw && criminalClan.IsBanditFaction) return;

                IFaction target = criminalClan.MapFaction;
                if (target == null) return;
                if (target == policeClan || target == policeClan.MapFaction) return;

                if (!FactionManager.IsAtWarAgainstFaction(policeClan, target))
                    FactionManager.DeclareWar(policeClan, target);
            }
            catch { }
        }

        /// <summary>
        /// 每2天向活跃任务日志追加一条侦察情报：护送警察的探子目击目标位置。
        /// 使用任务日志（不用 DisplayMessage），让玩家在任务界面自然获知敌人动向。
        /// </summary>
        private void UpdateIntelReport()
        {
            if (_activeQuest == null || !_activeQuest.IsOngoing) return;
            if ((CampaignTime.Now - _lastIntelReportTime).ToDays < GwpTuning.Bounty.IntelReportIntervalDays) return;
            _lastIntelReportTime = CampaignTime.Now;

            // 目标当前位置
            var target = MobileParty.All.FirstOrDefault(
                p => p.StringId == _activeBountyTargetId && p.IsActive);
            if (target == null) return;
            string sightingLocation = GetNearestSettlementName(target.GetPosition2D);

            // 情报来源：护送方名称；无护送方时用通用名称
            string reporterName = "灰袍侦察队";
            if (!string.IsNullOrEmpty(_escortPolicePartyId))
            {
                var escort = MobileParty.All.FirstOrDefault(
                    p => p.StringId == _escortPolicePartyId && p.IsActive);
                if (escort != null)
                    reporterName = escort.Name.ToString();
            }

            _activeQuest.WriteLog(
                $"【侦情】{reporterName} 的探子来报：目标在 {sightingLocation} 附近目击。");
        }

        /// <summary>
        /// 释放护送方 AI 限制，让其恢复正常巡逻行为（任务完成或取消时调用）。
        /// </summary>
        private void ReleaseEscortAi()
        {
            if (string.IsNullOrEmpty(_escortPolicePartyId)) return;
            var escort = MobileParty.All.FirstOrDefault(p => p.StringId == _escortPolicePartyId);
            if (escort != null && escort.IsActive)
            {
                try
                {
                    CrimePool.SetBountyEscortFlag(_escortPolicePartyId, false); // 恢复正常任务处理
                    escort.Ai.SetDoNotMakeNewDecisions(false);
                }
                catch { }
            }
        }

        #endregion

        #region 每小时检查

        private void OnHourlyTick()
        {
            if (Hero.MainHero == null) return;

            // ── 读档后任务重连（首次 Tick 兜底）──────────────────────────────────────
            // 若 InitializeQuestOnGameLoad（主路径）已触发并重连了 _activeQuest，
            // _awaitingQuestReconnect 已为 false，此块跳过。
            // 若 InitializeQuestOnGameLoad 早于 SyncData 触发（hasBountyTask=false 导致早返回），
            // 此处作为兜底：查 QM 找 Quest-A 重连；若 QM 无记录则创建新 Quest。
            if (_awaitingQuestReconnect)
            {
                _awaitingQuestReconnect = false;
                bool hasBountyTask = !string.IsNullOrEmpty(_activeBountyTargetId) || _waitingForCollection;
                if (hasBountyTask && _activeQuest == null)
                {
                    // ★ 优先从 QM 查找已加载的 Quest-A（SpecialQuestType 确保它不被引擎取消）
                    bool reconnected = false;
                    try
                    {
                        var existing = Campaign.Current?.QuestManager?.Quests
                            ?.OfType<BountyHunterQuest>()
                            ?.FirstOrDefault(q => q.IsOngoing);
                        if (existing != null)
                        {
                            _activeQuest = existing;
                            reconnected  = true;
                            if (!string.IsNullOrEmpty(_activeBountyTargetId))
                                existing.WriteLog(
                                    $"读档恢复：继续追踪目标（{_activeBountyTargetName ?? "未知目标"}）。");
                            else if (_waitingForCollection)
                                existing.WriteLog("读档恢复：目标已击败，前往领取赏金。");
                        }
                    }
                    catch { }

                    // QM 中无活跃任务 → 旧存档兼容，安全创建新 Quest
                    if (!reconnected)
                    {
                        Clan policeClan = PoliceStats.GetPoliceClan();
                        Hero? policeLeader = policeClan?.Leader;
                        if (policeLeader != null)
                        {
                            try
                            {
                                _activeQuest = new BountyHunterQuest(
                                    policeLeader,
                                    _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop,
                                    _activeBountyTargetName ?? "未知目标");
                                _activeQuest.StartQuest();
                                if (!string.IsNullOrEmpty(_activeBountyTargetId))
                                    _activeQuest.WriteLog(
                                        $"读档恢复（兜底）：继续追踪目标（{_activeBountyTargetName ?? "未知目标"}）。");
                                else if (_waitingForCollection)
                                    _activeQuest.WriteLog("读档恢复（兜底）：目标已击败，前往领取赏金。");
                            }
                            catch { _activeQuest = null!; }
                        }
                    }
                }
            }

            // 维护招募使者部队（每小时刷新行进命令）
            UpdateRecruitmentPatrol();

            // 维护悬赏护送部队（每小时刷新跟随命令）
            UpdateEscortPatrol();

            // 声望达标且尚未招募过 → 生成招募使者
            if (!_recruitmentOffered &&
                !_recruitmentAccepted &&
                _recruitmentPatrolId == null &&
                PlayerBehaviorPool.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold)
            {
                SpawnRecruitmentPatrol();
            }

            // 有活跃悬赏任务时，验证目标是否仍在地图上
            if (!string.IsNullOrEmpty(_activeBountyTargetId))
            {
                bool targetAlive = MobileParty.All.Any(
                    p => p.StringId == _activeBountyTargetId &&
                         (p.IsActive || p.MapEvent != null));
                if (!targetAlive)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "悬赏目标已消失（可能已被他人击败），任务自动取消",
                        Colors.Yellow));
                    try { _activeQuest?.FailQuestTargetGone(); } catch { }
                    _activeQuest                 = null!;
                    _activeBountyTargetId        = null!;
                    _activeBountyTargetName      = null!;
                    _activeBountyTargetFactionId = null!;
                    _activeBountyTargetSize      = 0;
                    // 释放护送方 AI，让其恢复正常行为
                    ReleaseEscortAi();
                    _escortPolicePartyId         = null!;
                    return;
                }

                // ★ 每2天向任务日志追加一条侦察情报（护送方探子目击目标位置）
                UpdateIntelReport();
                return;
            }

            if (_waitingForCollection) return;

            // ── 接任务三条件 ──────────────────────────────────────────────────────────
            if (!_recruitmentAccepted) return;                                        // 条件1：已接受招募
            if (PlayerBehaviorPool.Reputation < GwpTuning.Bounty.RecruitmentReputationThreshold) return; // 条件2：声望足够
            if (!IsWearingCommanderSet()) return;                                     // 条件3：穿戴套装
            // ─────────────────────────────────────────────────────────────────────────

            if ((CampaignTime.Now - _lastOfferTime).ToDays < GwpTuning.Bounty.OfferCooldownDays) return;
            _lastOfferTime = CampaignTime.Now;

            Vec2 playerPos = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
            CrimeRecord? crime = CrimePool.GetNearestNonPlayerFromAll(playerPos);

            if (crime == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "已识别黑袍指挥官装备，当前暂无悬赏任务可接",
                    Colors.White));
                return;
            }

            OfferBounty(crime);
        }

        #endregion

        #region 战斗结束（检测目标被击败）

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            if (string.IsNullOrEmpty(_activeBountyTargetId)) return;
            if (!mapEvent.HasWinner || mapEvent.Winner == null) return;

            bool playerWon = false;
            foreach (var p in mapEvent.Winner.Parties)
            {
                if (p?.Party?.IsMobile == true && p.Party.MobileParty?.IsMainParty == true)
                { playerWon = true; break; }
            }
            if (!playerWon) return;

            MapEventSide loserSide = (mapEvent.Winner == mapEvent.AttackerSide)
                ? mapEvent.DefenderSide : mapEvent.AttackerSide;
            if (loserSide == null) return;

            MobileParty? defeatedTarget = null;
            foreach (var p in loserSide.Parties)
            {
                if (p?.Party?.IsMobile == true &&
                    p.Party.MobileParty?.StringId == _activeBountyTargetId)
                { defeatedTarget = p.Party.MobileParty; break; }
            }
            if (defeatedTarget == null) return;

            // 用接任务时快照的人数计算赏金（战后残余人数趋近0，不能用战后数值）
            _pendingReward = _activeBountyTargetSize * GwpTuning.Bounty.RewardPerTroop;
            _activeBountyTargetId = null!;
            _waitingForCollection = true;

            try { _activeQuest?.WriteLog($"目标已击败！前往领取赏金 {_pendingReward} 第纳尔。"); } catch { }

            Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            string leaderName = policeLeader?.Name?.ToString() ?? "警察领主";
            string rewardHint = !string.IsNullOrEmpty(_escortPolicePartyId)
                ? "找到你的护送警察对话，直接领取赏金"
                : $"前往 {leaderName} 处领取赏金";
            InformationManager.DisplayMessage(new InformationMessage(
                $"悬赏目标已击败！{rewardHint}（{_pendingReward} 第纳尔）",
                Colors.Green));
        }

        #endregion

        #region 读档回调

        /// <summary>
        /// OnSessionLaunched 中调用（SyncData 已完成，所有持久化字段均已正确加载）。
        ///
        /// 读档时序（已调试确认）：
        ///   1. QuestBase.SyncData + InitializeQuestOnGameLoad（QuestManager.OnGameLoaded 阶段）
        ///      ← BountyHunterQuest.SpecialQuestType 非空 → 引擎调用 InitializeQuestOnGameLoad
        ///         而非 CompleteQuestWithCancel。若此时 behavior SyncData 已完成，
        ///         OnQuestLoadedFromSave 回调直接重连 _activeQuest，此处设的标志仅会被首次 Tick 清除。
        ///   2. CampaignBehavior.SyncData() — _activeBountyTargetId 等恢复
        ///   3. OnSessionLaunched → 此方法 ← 设 _awaitingQuestReconnect 标志
        ///   4. OnHourlyTick（首次）← 若 _activeQuest 未被步骤1重连，在此从 QM 查找重连
        ///
        /// 新档时行为：
        ///   _activeBountyTargetId == null && !_waitingForCollection → 立刻 return，不设标志。
        /// </summary>
        private void TryRestoreBountyQuestOnSessionStart()
        {
            bool hasBountyTask = !string.IsNullOrEmpty(_activeBountyTargetId) || _waitingForCollection;
            if (!hasBountyTask) return;

            // 仅设标志；若 InitializeQuestOnGameLoad 已重连（_activeQuest != null），
            // 首次 OnHourlyTick 会检测到后立即清除此标志，不做额外操作。
            _awaitingQuestReconnect = true;
        }

        /// <summary>
        /// 由 BountyHunterQuest.InitializeQuestOnGameLoad() 回调（QuestManager.OnGameLoaded 阶段）。
        /// 若 behavior.SyncData() 已先于 InitializeQuestOnGameLoad 执行，hasBountyTask=true，
        /// 可在此直接重连 _activeQuest，无需等待首次 OnHourlyTick。
        /// 若 SyncData 尚未执行，hasBountyTask=false → 早返回 → 由首次 Tick 兜底重连。
        /// </summary>
        internal void OnQuestLoadedFromSave(BountyHunterQuest quest)
        {
            bool hasBountyTask = !string.IsNullOrEmpty(_activeBountyTargetId) || _waitingForCollection;

            if (!hasBountyTask || quest == null || !quest.IsOngoing)
                return; // 新档、任务已完结、或 quest 引擎内部已 Fail → 首次 Tick 兜底

            // ★ 成功重连：Quest-A 存活，直接绑定 _activeQuest，不创建 Quest-B
            _activeQuest = quest;
            _awaitingQuestReconnect = false; // 通知首次 Tick 无需兜底

            if (!string.IsNullOrEmpty(_activeBountyTargetId))
                quest.WriteLog($"读档恢复：继续追踪目标（{_activeBountyTargetName ?? "未知目标"}）。");
            else if (_waitingForCollection)
                quest.WriteLog("读档恢复：目标已击败，前往护送警察或警察领主处领取赏金。");
        }

        #endregion

        #region 辅助

        private static string GetNearestSettlementName(Vec2 position)
        {
            Settlement nearest = null!;
            float nearestDistSq = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
                if (s == null) continue;
                Vec2 sPos = s.GetPosition2D;
                float dx = sPos.x - position.x;
                float dy = sPos.y - position.y;
                float distSq = dx * dx + dy * dy;
                if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = s; }
            }
            return nearest?.Name?.ToString() ?? "未知位置";
        }

        private static Settlement FindNearestTown(Vec2 position)
        {
            Settlement nearest = null!;
            float minDist = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsTown) continue;
                float dist = position.Distance(s.GetPosition2D);
                if (dist < minDist) { minDist = dist; nearest = s; }
            }
            return nearest;
        }

        private bool IsWearingCommanderSet()
        {
            var equipment = Hero.MainHero?.BattleEquipment;
            if (equipment == null) return false;
            var wornIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < 12; i++)
            {
                var elem = equipment[i];
                if (!elem.IsEmpty && elem.Item != null)
                    wornIds.Add(elem.Item.StringId);
            }
            return GwpIds.CommanderSetItemIds.All(wornIds.Contains);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // ★ 重要：三个嵌套类均声明为 internal（非 private）
        //   Bannerlord 存档系统通过反射按类型名找到这些类。
        //   private 类无法被外部反射访问 → 存档时抛异常 → "无法存档"。
        //   internal 类可被同程序集反射访问，存档系统可正常序列化/反序列化。
        // ══════════════════════════════════════════════════════════════════════

    }
}
