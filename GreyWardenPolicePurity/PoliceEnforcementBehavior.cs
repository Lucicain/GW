﻿using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
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
    /// <summary>
    /// 警察惩戒系统
    ///
    /// 玩家被抓完整流程：
    /// 1. OnMapEventEnded：标记 IsEscortingPlayer，冻结AI，SetMoveGoToSettlement 朝最近城堡行进
    /// 2. UpdateTasks（每小时）：维持AI冻结 + 重发行进命令（防止引擎覆盖）
    /// 3. OnTick（每帧）：距城堡 &lt; EscortPunishDistance(3格) 时执行惩罚；
    ///    CurrentSettlement!=null 为兜底防崩溃路径
    /// 4. ExecutePunishment：★关键顺序★ 先 EndCaptivity + 清空花名册，
    ///    再 MakePeace，避免 SetNeutral 内部触发二次释放导致崩溃
    /// </summary>
    public partial class PoliceEnforcementBehavior : CampaignBehaviorBase
    {
        private const float WarDistance = 3f;
        private const float WarDistancePlayer = 15f;
        private const int ShelteredForceBattleIntervalHours = 6;

        // 距城堡多少格时触发惩罚。
        // 3格 > 引擎自动入城触发距离（约1-2格），确保 OnTick 距离判断先于自动入城发生。
        // 兜底：CurrentSettlement != null 紧急检测防止极端情况下的崩溃。
        private const float EscortPunishDistance = 3f;

        // 对话临时变量（警察执法拦截：让玩家选择缴纳或战斗）
        private int _dialogFine = 0;
        private MobileParty _dialogPolice = null!;
        private PoliceTask _dialogTask = null!;
        private bool _enforcementBarterInProgress = false;
        private bool _atonementActive = false;
        private string _atonementTargetPartyId = string.Empty;
        private string _atonementTargetName = string.Empty;
        private int _atonementReputationReward = 0;
        private float _atonementDeadlineHours = 0f;
        private bool _enforcementAtonementAssigned = false;
        private readonly Dictionary<string, int> _shelteredTargetHoursByTaskId =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public override void RegisterEvents()
        {
            PoliceCrimeMonitorEnhanced.OnCrimeDetected += HandleCrimeDetected;
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            CrimePool.SyncData(dataStore);
            dataStore.SyncData("gwp_enf_war_check_day_counter", ref _warStatusCheckDayCounter);
            dataStore.SyncData("gwp_enf_atone_active", ref _atonementActive);
            dataStore.SyncData("gwp_enf_atone_target_id", ref _atonementTargetPartyId);
            dataStore.SyncData("gwp_enf_atone_target_name", ref _atonementTargetName);
            dataStore.SyncData("gwp_enf_atone_target_faction_id", ref _atonementTargetFactionId);
            dataStore.SyncData("gwp_enf_atone_reward", ref _atonementReputationReward);
            dataStore.SyncData("gwp_enf_atone_deadline_hours", ref _atonementDeadlineHours);
            dataStore.SyncData("gwp_enf_atone_waiting_turnin", ref _atonementWaitingForTurnIn);
            dataStore.SyncData("gwp_enf_atone_target_size", ref _atonementTargetSizeSnapshot);
            SyncWarTargetStreakData(dataStore);
            SyncDelayPatrolStateData(dataStore);
            if (dataStore.IsLoading)
            {
                if (_warStatusCheckDayCounter < 0 || _warStatusCheckDayCounter > 1)
                    _warStatusCheckDayCounter = 0;
                _shelteredTargetHoursByTaskId.Clear();
                _enforcementAtonementAssigned = false;
                _atonementQuest = null!;
                _awaitingAtonementQuestReconnect = false;
                _lastAtonementIntelReportTime = CampaignTime.Zero;
                PlayerBehaviorPool.SetAtonementTaskActive(_atonementActive || _atonementWaitingForTurnIn);
            }
        }

        #region 对话系统（执法拦截：玩家可选择缴纳罚金或战斗）

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 警察执法开场白（高优先级100，确保覆盖默认对话）
            starter.AddDialogLine(
                "gwp_enforcement_start",
                "start",
                "gwp_enforcement_options",
                "{GWP_ENFORCEMENT_GREETING}",
                EnforcementDialogCondition,
                null,
                100);

            // 选项1：缴纳正式罚金（Barter 界面）
            starter.AddPlayerLine(
                "gwp_enforcement_pay",
                "gwp_enforcement_options",
                "gwp_enforcement_pay_barter_pre",
                "{GWP_ENFORCEMENT_PAY_TEXT}",
                EnforcementPayCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_pre",
                "gwp_enforcement_pay_barter_screen",
                "按灰袍法令，先把正式罚金缴清。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_screen",
                "gwp_enforcement_pay_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnEnforcementPayBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_success",
                "gwp_enforcement_pay_barter_post",
                "close_window",
                "罚金确认。本轮案件结案，你可以离开了。",
                EnforcementBarterSuccessfulCondition,
                OnEnforcementPayAcceptedConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_pay_barter_post_failed",
                "gwp_enforcement_pay_barter_post",
                "gwp_enforcement_options",
                "你的出价低于正式罚金。你可以继续出价，或拒绝执法。",
                () => !EnforcementBarterSuccessfulCondition(),
                OnEnforcementPayRejectedConsequence,
                100);

            // 选项1.5：认罪认罚（赎罪任务）
            starter.AddPlayerLine(
                "gwp_enforcement_atonement",
                "gwp_enforcement_options",
                "gwp_enforcement_atonement_result",
                "我认罪认罚，请给我赎罪任务。",
                EnforcementAtonementCondition,
                OnEnforcementAtonementConsequence,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_success",
                "gwp_enforcement_atonement_result",
                "close_window",
                "{GWP_ENFORCEMENT_ATONEMENT_TEXT}",
                () => _enforcementAtonementAssigned,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_failed",
                "gwp_enforcement_atonement_result",
                "gwp_enforcement_options",
                "当前无法分配赎罪任务。你可以继续缴纳罚金，或拒绝执法。",
                () => !_enforcementAtonementAssigned,
                null,
                100);

            // 选项2：拒绝，武力解决
            starter.AddPlayerLine(
                "gwp_enforcement_fight",
                "gwp_enforcement_options",
                "gwp_enforcement_fight_response",
                "拒绝执法，开战。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_fight_response",
                "gwp_enforcement_fight_response",
                "close_window",
                "拒捕记录在案。灰袍守卫，执行抓捕！",
                null,
                OnEnforcementFightConsequence,
                100);

            // 任务交付：目标击败后可向族长或任意灰袍警察交付赎罪任务
            starter.AddPlayerLine(
                "gwp_enforcement_atonement_turnin",
                "lord_talk_speak_diplomacy_2",
                "gwp_enforcement_atonement_turnin_response",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_OPTION}",
                EnforcementAtonementTurnInCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_enforcement_atonement_turnin_response",
                "gwp_enforcement_atonement_turnin_response",
                "lord_pretalk",
                "{GWP_ENFORCEMENT_ATONEMENT_TURNIN_TEXT}",
                null,
                OnEnforcementAtonementTurnInConsequence,
                100);

            PlayerBehaviorPool.SetAtonementTaskActive(_atonementActive || _atonementWaitingForTurnIn);
            TryRestoreAtonementQuestOnSessionStart();
        }

        /// <summary>
        /// 对话触发条件：对话方是正式警察部队（gw家族，非纠察队前缀）
        /// 且该部队有针对玩家的任务、尚未宣战
        /// </summary>
        private bool EnforcementDialogCondition()
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;

            // 只处理正式警察部队（gw家族，非纠察队 gwp_patrol_ 前缀）
            if (conversationParty.ActualClan != policeClan) return false;
            if (GwpCommon.IsPatrolParty(conversationParty)) return false;

            // 该警察必须有针对玩家的任务，且尚未宣战（宣战后走战斗流程）
            var task = CrimePool.GetTask(conversationParty.StringId);
            if (task == null) return false;
            if (task.TargetCrime?.Offender?.IsMainParty != true) return false;
            if (task.WarDeclared) return false;
            if (task.IsEscortingPlayer) return false;

            int rep = PlayerBehaviorPool.Reputation;
            _dialogFine = Math.Abs(rep) * 300;
            _dialogPolice = conversationParty;
            _dialogTask = task;

            int playerGold = Hero.MainHero.Gold;
            string payInfo = playerGold >= _dialogFine
                ? $"你当前携带 {playerGold} 金，可直接缴清。"
                : $"你当前携带 {playerGold} 金，可在谈判界面继续出价，或改选认罪认罚。";

            MBTextManager.SetTextVariable("GWP_ENFORCEMENT_GREETING",
                $"站住！灰袍守卫正在执行逮捕。你当前负声望 {Math.Abs(rep)}，" +
                $"本案正式罚金 {_dialogFine} 金。{payInfo}");

            return true;
        }

        /// <summary>
        /// 缴纳按钮条件：同时动态设置按钮文本
        /// </summary>
        private bool EnforcementPayCondition()
        {
            string text = $"缴纳正式罚金（{_dialogFine} 金，清除通缉）";
            MBTextManager.SetTextVariable("GWP_ENFORCEMENT_PAY_TEXT", text);
            return true;
        }

        private bool EnforcementAtonementCondition()
        {
            if (_atonementActive || _atonementWaitingForTurnIn) return false;
            if (_dialogFine <= 0) return false;
            return Hero.MainHero.Gold < _dialogFine;
        }

        private void OnEnforcementAtonementConsequence()
        {
            _enforcementAtonementAssigned = TryAssignAtonementTask();
        }

        private bool TryAssignAtonementTask()
        {
            if (_atonementActive || _atonementWaitingForTurnIn) return false;

            CrimeRecord targetCrime = CrimePool.GetNearestNonPlayerFromAll(
                MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero);
            if (targetCrime == null || targetCrime.Offender == null || !targetCrime.Offender.IsActive)
                return false;

            MobileParty offender = targetCrime.Offender;
            int targetSizeSnapshot = Math.Max(1, offender.Party?.NumberOfAllMembers ?? 1);
            int rewardRep = Math.Max(1, (int)Math.Ceiling(targetSizeSnapshot / 10f));

            _atonementActive = true;
            _atonementWaitingForTurnIn = false;
            _atonementTargetPartyId = offender.StringId ?? string.Empty;
            _atonementTargetName = offender.Name?.ToString() ?? "未知目标";
            _atonementTargetFactionId = offender.MapFaction?.StringId ?? string.Empty;
            _atonementTargetSizeSnapshot = targetSizeSnapshot;
            _atonementReputationReward = rewardRep;
            _atonementDeadlineHours = (float)(CampaignTime.Now.ToHours + AtonementDeadlineDays * 24f);
            _lastAtonementIntelReportTime = CampaignTime.Now;
            StartAtonementQuest();
            PlayerBehaviorPool.SetAtonementTaskActive(true);

            CrimePool.EndPlayerHunt();
            if (_dialogPolice != null && _dialogPolice.IsActive)
            {
                RestoreAi(_dialogPolice);
                PoliceResourceManager.StartResupply(_dialogPolice);
                PoliceResourceManager.ForceImmediateMoveToResupply(_dialogPolice);
            }
            MakePeaceWithPoliceAndVictims();

            MBTextManager.SetTextVariable("GWP_ENFORCEMENT_ATONEMENT_TEXT",
                $"赎罪任务已下达：追捕 {_atonementTargetName}（接案规模 {targetSizeSnapshot} 人）。" +
                $"完成可恢复最多 {_atonementReputationReward} 点声望（最高恢复到 0）；" +
                $"失败将追加 5 点负声望。");

            InformationManager.DisplayMessage(new InformationMessage(
                $"赎罪任务已记录到任务面板：击败 {_atonementTargetName} 后，向族长或任意灰袍警察交付（{AtonementDeadlineDays:0} 天内，失败声望 -5）。",
                Colors.Yellow));

            try { GwpCommon.TryFinishPlayerEncounter(); } catch { }
            return true;
        }

        private bool EnforcementBarterSuccessfulCondition()
        {
            return _enforcementBarterInProgress
                && Campaign.Current?.BarterManager != null
                && Campaign.Current.BarterManager.LastBarterIsAccepted;
        }

        private void OnEnforcementPayBarterConsequence()
        {
            _enforcementBarterInProgress =
                StartEnforcementPaymentBarter(_dialogPolice, _dialogFine, "缴纳正式罚金");
        }

        private void OnEnforcementPayRejectedConsequence()
        {
            _enforcementBarterInProgress = false;
        }

        /// <summary>
        /// 正式罚金缴纳成功：扣款 + 声望归零 + 任务结束 + 和平
        /// </summary>
        private void OnEnforcementPayAcceptedConsequence()
        {
            try
            {
                int paid = PoliceResourceManager.CollectFine(_dialogFine);

                PlayerBehaviorPool.ResetReputation(0);
                CrimePool.EndPlayerHunt();

                if (_dialogPolice != null && _dialogPolice.IsActive)
                {
                    RestoreAi(_dialogPolice);
                    PoliceResourceManager.StartResupply(_dialogPolice);
                    PoliceResourceManager.ForceImmediateMoveToResupply(_dialogPolice);
                }

                MakePeaceWithPoliceAndVictims();

                InformationManager.DisplayMessage(new InformationMessage(
                    $"正式罚金已收 {paid} 金，通缉已解除。",
                    Colors.Yellow));

                try { GwpCommon.TryFinishPlayerEncounter(); } catch { }
            }
            catch { }
            finally
            {
                _enforcementBarterInProgress = false;
                _enforcementAtonementAssigned = false;
                _dialogFine = 0;
                _dialogPolice = null!;
                _dialogTask = null!;
            }
        }

        /// <summary>
        /// 拒绝结果：宣战 + 警察立即接战
        /// </summary>
        private void OnEnforcementFightConsequence()
        {
            try
            {
                if (_dialogTask != null && MobileParty.MainParty != null)
                    DeclareWar(_dialogTask, MobileParty.MainParty);

                if (_dialogPolice != null && _dialogPolice.IsActive)
                    _dialogPolice.SetMoveEngageParty(MobileParty.MainParty, NavigationType.Default);

                InformationManager.DisplayMessage(new InformationMessage(
                    "你拒绝执法，灰袍守卫将强制抓捕。",
                    Colors.Red));
            }
            catch { }
            finally
            {
                _enforcementBarterInProgress = false;
                _enforcementAtonementAssigned = false;
                _dialogFine = 0;
                _dialogPolice = null!;
                _dialogTask = null!;
            }
        }

        private bool StartEnforcementPaymentBarter(MobileParty policePartyMobile, int amount, string barterDisplayName)
        {
            if (policePartyMobile == null || !policePartyMobile.IsActive || MobileParty.MainParty == null)
                return false;

            Hero? barterHero = Hero.OneToOneConversationHero
                               ?? policePartyMobile.LeaderHero
                               ?? GetEnforcementBarterHero();
            if (barterHero == null)
                return false;

            PartyBase policeParty = policePartyMobile.Party;
            PartyBase playerParty = MobileParty.MainParty.Party;
            if (policeParty == null || playerParty == null)
                return false;

            int paymentAmount = Math.Max(1, amount);
            var fineBarter = new GwpBribeBarterable(
                barterHero,
                Hero.MainHero,
                policeParty,
                playerParty,
                paymentAmount,
                barterDisplayName);

            try
            {
                Campaign.Current.BarterManager.StartBarterOffer(
                    Hero.MainHero,
                    barterHero,
                    playerParty,
                    policeParty,
                    null,
                    InitializeEnforcementBarterContext,
                    0,
                    false,
                    new[] { fineBarter });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InitializeEnforcementBarterContext(Barterable barterable, BarterData args, object obj)
        {
            return barterable is GwpBribeBarterable;
        }

        private Hero? GetEnforcementBarterHero()
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

        private void UpdateAtonementTask()
        {
            TryReconnectAtonementQuestOnHourlyTick();
            if (!_atonementActive) return;

            if (CampaignTime.Now.ToHours >= _atonementDeadlineHours)
            {
                FailAtonementTask("赎罪任务超时，声望 -5。");
                return;
            }

            MobileParty target = MobileParty.All.FirstOrDefault(p =>
                p.StringId == _atonementTargetPartyId && p.IsActive);
            if (target == null)
            {
                FailAtonementTask("赎罪目标已失踪，判定任务失败，声望 -5。");
                return;
            }

            if ((CampaignTime.Now - _lastAtonementIntelReportTime).ToDays >= AtonementIntelReportIntervalDays)
            {
                _lastAtonementIntelReportTime = CampaignTime.Now;
                AppendAtonementIntelLog(target);
            }
        }

        private void HandleAtonementMapEventEnded(MapEvent mapEvent)
        {
            if (!_atonementActive || mapEvent == null) return;

            bool playerInvolved = false;
            bool targetInvolved = false;
            foreach (var p in mapEvent.InvolvedParties)
            {
                MobileParty party = p?.MobileParty;
                if (party == null) continue;
                if (party.IsMainParty) playerInvolved = true;
                if (party.StringId == _atonementTargetPartyId) targetInvolved = true;
            }

            if (!playerInvolved || !targetInvolved) return;

            bool playerWon = false;
            if (mapEvent.HasWinner && mapEvent.Winner != null)
            {
                foreach (var p in mapEvent.Winner.Parties)
                {
                    if (p?.Party?.IsMobile == true && p.Party.MobileParty?.IsMainParty == true)
                    {
                        playerWon = true;
                        break;
                    }
                }
            }

            if (playerWon)
            {
                _atonementActive = false;
                _atonementWaitingForTurnIn = true;
                _atonementDeadlineHours = 0f;
                PlayerBehaviorPool.SetAtonementTaskActive(true);

                try { _atonementQuest?.MarkReadyForTurnIn(); } catch { }
                InformationManager.DisplayMessage(new InformationMessage(
                    $"赎罪目标已击败：{_atonementTargetName}。请前往族长或任意灰袍警察交付任务。",
                    Colors.Green));
            }
            else
            {
                FailAtonementTask("赎罪任务失败，声望 -5。");
            }
        }

        private void FailAtonementTask(string reason)
        {
            PlayerBehaviorPool.ChangeReputation(-5);
            try { _atonementQuest?.FailQuestWithReason(reason); } catch { }
            InformationManager.DisplayMessage(new InformationMessage(reason, Colors.Red));
            ClearAtonementTaskState();
        }

        private void ClearAtonementTaskState()
        {
            _atonementActive = false;
            _atonementWaitingForTurnIn = false;
            _atonementTargetPartyId = string.Empty;
            _atonementTargetName = string.Empty;
            _atonementTargetFactionId = string.Empty;
            _atonementTargetSizeSnapshot = 0;
            _atonementReputationReward = 0;
            _atonementDeadlineHours = 0f;
            _lastAtonementIntelReportTime = CampaignTime.Zero;
            _awaitingAtonementQuestReconnect = false;
            _enforcementAtonementAssigned = false;
            _atonementQuest = null!;
            PlayerBehaviorPool.SetAtonementTaskActive(false);
        }

        #endregion

        #region 遭遇拦截（强制对话，在宣战前让玩家选择）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null) return;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            // 警察接近玩家（警察是进攻方）→ 强制对话
            bool policeHasPlayerTask = false;
            bool playerInvolved = false;

            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p?.MobileParty == null) continue;

                if (p.MobileParty.IsMainParty)
                {
                    playerInvolved = true;
                    continue;
                }

                // 正式警察部队，且有玩家任务、尚未宣战、非押送中
                var task = CrimePool.GetTask(p.MobileParty.StringId);
                if (task != null &&
                    task.TargetCrime?.Offender?.IsMainParty == true &&
                    !task.WarDeclared &&
                    !task.IsEscortingPlayer &&
                    p.MobileParty.ActualClan == policeClan)
                {
                    policeHasPlayerTask = true;
                }
            }

            if (!policeHasPlayerTask || !playerInvolved) return;

            // 尚未宣战时，强制进入对话模式（让玩家选择缴纳或战斗，而非直接开打）
            IFaction pf = Clan.PlayerClan?.MapFaction;
            bool atWar = pf != null && FactionManager.IsAtWarAgainstFaction(policeClan, pf);

            if (!atWar && PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
            }
        }

        #endregion

        #region 犯罪通知

        private void HandleCrimeDetected(string crimeType, MobileParty offender, Vec2 location, string victimName)
        {
            CrimePool.TryAdd(crimeType, offender, location, victimName);
        }

        #endregion

        #region 每帧检查 - 距城堡距离触发惩罚

        private void OnTick(float dt)
        {
            try
            {
                if (!PlayerCaptivity.IsCaptive) return;

                // 确认玩家被警察俘虏
                PartyBase captorParty = PlayerCaptivity.CaptorParty;
                if (captorParty == null) return;

                Clan policeClan = PoliceStats.GetPoliceClan();
                if (policeClan == null) return;

                bool isCapturedByPolice = false;
                if (captorParty.IsMobile && captorParty.MobileParty?.ActualClan == policeClan)
                    isCapturedByPolice = true;
                if (captorParty.IsSettlement && captorParty.Settlement?.OwnerClan == policeClan)
                    isCapturedByPolice = true;

                if (!isCapturedByPolice) return;

                // 找到押送任务
                var escortTask = CrimePool.ActiveTasks.Values.FirstOrDefault(t =>
                    t.IsEscortingPlayer &&
                    t.TargetCrime?.Offender?.IsMainParty == true);

                if (escortTask == null) return;

                var policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == escortTask.PolicePartyId);
                if (policeParty == null || !policeParty.IsActive) return;

                // 紧急检测：若警察已进入任意定居点（引擎自动进城），立即执行惩罚。
                // 必须在引擎的俘虏交付逻辑运行前清空花名册，否则崩溃。
                if (policeParty.CurrentSettlement != null)
                {
                    ExecutePunishment(policeParty.CurrentSettlement, escortTask);
                    return;
                }

                Settlement castle = escortTask.EscortSettlement;
                if (castle == null) return;

                // 正常触发路径：警察通过混合寻路接近城堡，距离 < EscortPunishDistance 时执行惩罚。
                // 近距离段用 GatePosition 直线导航，不触发自动入城，确保此距离判断先于入城发生。
                float distToCastle = policeParty.GetPosition2D.Distance(castle.GetPosition2D);
                if (distToCastle < EscortPunishDistance)
                {
                    ExecutePunishment(castle, escortTask);
                }
            }
            catch { }
        }

        /// <summary>
        /// 执行惩罚（距城堡3格内触发，或进入定居点时紧急触发）。
        ///
        /// ★关键顺序★：先 EndCaptivity + 清空花名册，再 MakePeaceWithPoliceAndVictims。
        ///
        /// 原因：Bannerlord 的 FactionManager.SetNeutral 内部会触发自动释放俘虏逻辑。
        /// 若玩家仍在花名册中时 SetNeutral 被调用，引擎会尝试"释放"已被
        /// EndCaptivity 管理的玩家，造成状态不一致，警察进城补给时或退城时崩溃。
        /// 先清理俘虏状态，再和平，彻底消除双重释放隐患。
        /// </summary>
        private void ExecutePunishment(Settlement castle, PoliceTask escortTask)
        {
            try
            {
                // 提前获取警察部队引用（任务被移除后局部变量仍有效）
                var policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == escortTask.PolicePartyId);

                // ★步骤1★ 先释放玩家（设 IsCaptive = false，移除玩家主英雄的俘虏状态）
                try { if (PlayerCaptivity.IsCaptive) PlayerCaptivity.EndCaptivity(); } catch { }

                // ★步骤1b★ 传送玩家到城堡大门（视觉效果：玩家被"押进"城堡）
                // 必须在 EndCaptivity 之后、花名册清理之前，此时玩家党派已脱离俘虏链
                try
                {
                    Settlement teleportTarget = castle ?? escortTask?.EscortSettlement ?? FindNearestCastle();
                    if (teleportTarget != null && MobileParty.MainParty != null)
                    {
                        MobileParty.MainParty.Position = teleportTarget.GatePosition;
                    }
                }
                catch { }

                // ★步骤2★ 强制清空花名册（防止 EndCaptivity 未完全清理，
                //          后续补给进城时引擎再次处理"主英雄俘虏"导致崩溃）
                try
                {
                    if (policeParty != null && policeParty.IsActive
                        && policeParty.PrisonRoster.TotalManCount > 0)
                    {
                        policeParty.PrisonRoster.Clear();
                    }
                }
                catch { }

                // ★步骤3★ 现在玩家已完全释放，再调用和平（SetNeutral 不会二次触发释放）
                MakePeaceWithPoliceAndVictims();

                // 步骤4：罚款（每点300金，仅收金币，不再没收背包物品）
                int rep = PlayerBehaviorPool.Reputation;
                int fine = Math.Abs(rep) * 300;
                int collected = PoliceResourceManager.CollectFineGoldOnly(fine);
                int recovered = 300 > 0 ? collected / 300 : 0;
                int repAfter = Math.Min(0, rep + recovered);

                // 步骤5：声望按实缴比例恢复（不再直接归零）
                PlayerBehaviorPool.ResetReputation(repAfter);
                if (repAfter > -11 || PlayerBehaviorPool.HasAtonementTask)
                {
                    CrimePool.EndPlayerHunt();
                }
                else
                {
                    CrimePool.EndTask(escortTask.PolicePartyId);
                    CrimePool.TryAddPlayerCrime("罚款不足", MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero, "押送罚款未缴清");
                }

                // 步骤6：显示消息
                string castleName = castle?.Name?.ToString() ?? "堡垒";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"你被押送至 {castleName}：应缴 {fine} 金，实缴 {collected} 金，声望恢复到 {repAfter}（按实缴恢复）。",
                    Colors.Yellow));

                // 步骤7：恢复警察AI，开始补给
                //（此时花名册已清空，进城补给时 ReleasePrisoners 不会遇到玩家俘虏）
                if (policeParty != null && policeParty.IsActive)
                {
                    RestoreAi(policeParty);
                    PoliceResourceManager.StartResupply(policeParty);
                }

                // 步骤8：安全调用 EndTask（EndPlayerHunt 已移除任务，此处幂等）
                CrimePool.EndTask(escortTask.PolicePartyId);
            }
            catch { }
        }

        #endregion

        #region 每小时

        private void OnHourlyTick()
        {
            UpdateAtonementTask();
            EnsureDelayPatrolStateForActiveParties();
            UpdateDelayPatrols();
            CrimePool.Clean();
            AssignTasks();
            UpdateTasks();
            CrimePool.RefreshAccepting();
        }

        private void AssignTasks()
        {
            foreach (var pp in PoliceStats.GetAllPoliceParties())
            {
                if (GwpCommon.IsEnforcementDelayPatrolParty(pp)) continue;
                // ★ 兜底：跳过无首领或首领失效的部队（防止因纠察队/英雄失效导致无首领部队接任务）
                if (pp.LeaderHero == null || !pp.LeaderHero.IsActive) continue;
                if (CrimePool.HasTask(pp.StringId)) continue;
                if (!PoliceResourceManager.IsReady(pp)) continue;

                CrimeRecord crime = CrimePool.GetNearest(pp.GetPosition2D);
                if (crime == null) continue;

                BeginTask(pp, crime);
            }
        }

        private void BeginTask(MobileParty police, CrimeRecord crime)
        {
            CrimePool.BeginTask(police.StringId, crime);

            police.SetMoveEngageParty(crime.Offender, NavigationType.Default);
            police.Ai.SetDoNotMakeNewDecisions(true);
            police.Ai.SetInitiative(1f, 0f, 999f);

            // 内部调度日志（开发调试）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP 出警] {police.Name} → {crime.Offender.Name}（{crime.CrimeType}）",
            //     Colors.Cyan));
        }

        private void UpdateTasks()
        {
            foreach (var kvp in CrimePool.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                if (pp == null || !pp.IsActive)
                {
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    continue;
                }

                // ★ 兜底：任务进行中首领失效（被俘/死亡）→ 结束任务，案件归池
                if (pp.LeaderHero == null || !pp.LeaderHero.IsActive)
                {
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    continue;
                }

                // ★ 正在为玩家悬赏护送时，完全由 PlayerBountyBehavior 管理此部队的 AI，跳过。
                if (task.IsPlayerBountyEscort)
                {
                    ClearTaskWarTracking(kvp.Key, true);
                    continue;
                }

                // 押送阶段：冻结AI，每小时重发行军命令（防止引擎覆盖方向）
                if (task.IsEscortingPlayer)
                {
                    ClearTaskWarTracking(kvp.Key, true);
                    pp.Ai.SetDoNotMakeNewDecisions(true);

                    // 安全网：若玩家已被外部机制提前释放（例如某段和平逻辑绕过了守卫），
                    // 仍执行惩罚以确保罚款和声望清零。
                    // ExecutePunishment 内部先检查 IsCaptive 再调用 EndCaptivity，安全。
                    if (!PlayerCaptivity.IsCaptive)
                    {
                        ExecutePunishment(task.EscortSettlement, task);
                        continue;
                    }

                    if (task.EscortSettlement != null)
                    {
                        // 每小时重发行进命令（防止引擎覆盖），地形寻路朝城堡行进
                        pp.SetMoveGoToSettlement(task.EscortSettlement, NavigationType.Default, false);
                    }
                    continue;
                }

                // 食物耗尽 → 案件归池，前往补给，等补完后重新接案
                bool targetSheltered = task.TargetCrime?.Offender?.IsMainParty != true &&
                                      task.TargetCrime?.Offender?.CurrentSettlement != null;
                if (pp.ItemRoster.TotalFood <= 0 && !targetSheltered)
                {
                    // 警察部队粮草耗尽内部运营（不显示给玩家）
                    // InformationManager.DisplayMessage(new InformationMessage(
                    //     $"[GWP] {pp.Name} 粮草耗尽，案件归池，前往补给", Colors.Yellow));
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    if (task.IsTargetValid()) Reassign(task.TargetCrime);
                    PoliceResourceManager.StartResupply(pp);
                    continue;
                }

                if (!task.IsTargetValid())
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    PoliceResourceManager.StartResupply(pp);
                    continue;
                }

                // 正常追击
                MobileParty criminal = task.TargetCrime.Offender;
                if (!criminal.IsMainParty && criminal.CurrentSettlement != null)
                {
                    if (HandleShelteredCriminal(pp, task, kvp.Key, criminal))
                        continue;
                }
                else
                {
                    ClearShelteredTargetTracking(kvp.Key);
                }

                if (!criminal.IsActive)
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    PoliceResourceManager.StartResupply(pp);
                    continue;
                }

                float dist = pp.GetPosition2D.Distance(criminal.GetPosition2D);

                float warDist = criminal.IsMainParty ? WarDistancePlayer : WarDistance;

                bool isPatrolRange = criminal.IsMainParty &&
                    PlayerBehaviorPool.Reputation >= -4 &&
                    PlayerBehaviorPool.Reputation <= -1;

                // 玩家目标不自动宣战——改由对话系统让玩家选择缴纳或战斗。
                // 只有玩家在对话中选择"战斗"后（OnEnforcementFightConsequence）才宣战。
                // 非玩家目标仍在接近时自动宣战。
                if (!task.WarDeclared && dist < warDist && !isPatrolRange && !criminal.IsMainParty)
                {
                    DeclareWar(task, criminal);
                }
                else if (!task.WarDeclared)
                {
                    ClearTaskWarTracking(kvp.Key, false);
                }

                try
                {
                    pp.Ai.SetDoNotMakeNewDecisions(true);
                    pp.Ai.SetInitiative(1f, 0f, 999f);
                    pp.SetMoveEngageParty(criminal, NavigationType.Default);
                }
                catch { }
            }
        }

        private void DeclareWar(PoliceTask task, MobileParty criminal)
        {
            try
            {
                Clan policeClan = PoliceStats.GetPoliceClan();
                if (policeClan == null) return;

                Clan criminalClan = criminal.ActualClan;
                if (criminalClan == null) return;

                task.WarDeclared = true;

                if (criminalClan.IsOutlaw && criminalClan.IsBanditFaction) return;

                IFaction target = criminalClan.MapFaction;
                if (target == null) return;

                if (target == policeClan || target == policeClan.MapFaction) return;

                task.WarTarget = target;

                if (!FactionManager.IsAtWarAgainstFaction(policeClan, target))
                {
                    FactionManager.DeclareWar(policeClan, target);
                }
            }
            catch { }
        }

        #endregion

        #region 战斗结束

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            HandleDelayPatrolBattleEnded(mapEvent);
            HandleAtonementMapEventEnded(mapEvent);
            if (!mapEvent.IsFieldBattle) return;

            foreach (var kvp in CrimePool.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                if (!task.WarDeclared) continue;

                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                if (pp == null)
                {
                    if (!InEvent(task.TargetCrime.Offender, mapEvent)) continue;
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    Reassign(task.TargetCrime);
                    CrimePool.RefreshAccepting();
                    continue;
                }

                if (!InEvent(pp, mapEvent)) continue;
                if (!InEvent(task.TargetCrime.Offender, mapEvent)) continue;

                bool policeWon = IsOnWinningSide(pp, mapEvent);

                if (policeWon)
                {
                    // ★关键修复★：不能用 CrimePool.IsPlayerCrime() 判断——
                    // 玩家被击败后 MainParty.IsActive == false，
                    // IsPlayerCrime 内部调用 IsOffenderValid() → Offender.IsActive → false，
                    // 导致误判为非玩家犯罪，走错路径（StartResupply → 进城补给 → 崩溃）。
                    // 改用 Offender.IsMainParty 直接判断，不依赖 IsActive。
                    if (task.TargetCrime?.Offender?.IsMainParty == true)
                    {
                        // 玩家被击败 → 押送至最近城堡（IsCastle）→ OnTick 距离触发惩罚
                        task.IsEscortingPlayer = true;

                        Settlement targetCastle = FindNearestCastle();
                        task.EscortSettlement = targetCastle;

                        // 冻结AI，地形寻路朝城堡行进
                        pp.Ai.SetDoNotMakeNewDecisions(true);
                        if (targetCastle != null)
                        {
                            pp.SetMoveGoToSettlement(targetCastle, NavigationType.Default, false);
                        }

                        string castleName = targetCastle?.Name?.ToString() ?? "堡垒";
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"你被 {pp.Name} 击败！正被押送至 {castleName}...",
                            Colors.Yellow));

                        continue;
                    }

                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    PoliceResourceManager.StartResupply(pp);
                }
                else
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimePool.EndTask(kvp.Key);
                    Reassign(task.TargetCrime);
                    PoliceResourceManager.StartResupply(pp);
                }

                CrimePool.RefreshAccepting();
            }
        }

        #endregion
    }
}
