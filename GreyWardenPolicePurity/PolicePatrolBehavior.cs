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
    /// <summary>
    /// 纠察队巡逻系统，处理 -1 到 -10 的犯罪追踪。
    ///
    /// 核心机制：
    ///   玩家犯罪达标 → 生成对话系统 → 自定义对话选项（缴纳/拒绝）
    ///   投降 → 自动缴纳罚金 → 押送回最近城镇点 → 释放 → 和平
    /// </summary>
    public partial class PolicePatrolBehavior : CampaignBehaviorBase
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;

        private readonly List<string> _activePatrolIds = new List<string>();
        private readonly List<string> _returningPatrolIds = new List<string>();

        // 对话结束后延迟一帧销毁纠察队（对话 Consequence 中不能直接 Destroy）
        private bool _destroyAllPatrolsOnNextTick = false;
        private bool _suppressPatrolMeetings = false;

        private bool _playerRefused = false;
        private bool _warDeclared = false;
        private int _dayCounter = 0;      // 每隔天触发一次检测
        private int _bribeProtectionDays = 0; // 谈判放行后的保护期天数（在这期间内不再派出新的纠察队）
        private int _dialogBribeAmount = 0; // 对话中计算出的谈判目标金额
        // 记录映射队的驻地点（战败后押送回此处）
        private Settlement _patrolOriginSettlement = null!;

        // 玩家被纠察队犯罪成功后正在被押送回驻地点
        private bool _playerCapturedByPatrol = false;
        private string _escortPatrolId = null!;

        // 指引AI策略（AI确认无公开API可接管俘虏管理）
        // 纠察队胜利后，不会自动俘虏玩家。纠察队犯罪成功后，手动原地选择。
        // 修改在UpdateEscort 中保持距离前先调用 EndCaptivity() 断开锁链（已实现）。
        // 注意ExecutePunishment() 中 mainParty.Position = settlement.GatePosition 负责传送（正常）


        // 对话中使用的临时变量
        private int _dialogFine = 0;
        private MobileParty _dialogPatrol = null!;
        private static readonly bool DebugPatrol = false;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        private void OnTick(float dt)
        {
            if (!_destroyAllPatrolsOnNextTick) return;

            _destroyAllPatrolsOnNextTick = false;
            DebugLog("已下发返程命令，等待到达定居点后销毁");
        }

        private void FreezeAndScheduleDestroyAllPatrols()
        {
            ReturnAllPatrols();
            _destroyAllPatrolsOnNextTick = true;
            DebugLog("已调度：返程至驻地后销毁纠察队");
        }

        private void DebugLog(string text)
        {
            if (!DebugPatrol) return;
            InformationManager.DisplayMessage(new InformationMessage($"[GWP-Patrol] {text}", Colors.Magenta));
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                // 加载存档清空运行时状态，防止跨存档继承残留
                _activePatrolIds.Clear();
                _returningPatrolIds.Clear();
                _playerRefused = false;
                _warDeclared = false;
                _playerCapturedByPatrol = false;
                _escortPatrolId = null!;
                _patrolOriginSettlement = null!;
                _dialogFine = 0;
                _dialogBribeAmount = 0;
                _dialogPatrol = null!;
                _dayCounter = 0;
                _bribeProtectionDays = 0;
                _destroyAllPatrolsOnNextTick = false;
                _suppressPatrolMeetings = false;
            }

            // 同步放行保护期
            dataStore.SyncData("gwp_patrol_bribe_protect", ref _bribeProtectionDays);
        }

        #region 对话注册

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 纠察队开场白（高优先级100，确保覆盖默认对话）
            starter.AddDialogLine(
                "gwp_patrol_start",
                "start",
                "gwp_patrol_options",
                "{GWP_PATROL_GREETING}",
                PatrolDialogCondition,
                null,
                100);

            // 选项1：缴纳正式罚金（Barter窗口）
            starter.AddPlayerLine(
                "gwp_patrol_pay",
                "gwp_patrol_options",
                "gwp_patrol_pay_barter_pre",
                "{GWP_PATROL_PAY_TEXT}",
                PatrolPayCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_patrol_pay_barter_pre",
                "gwp_patrol_pay_barter_pre",
                "gwp_patrol_pay_barter_screen",
                "按灰袍法令，先把正式罚金缴清。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_patrol_pay_barter_screen",
                "gwp_patrol_pay_barter_screen",
                "gwp_patrol_pay_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnPayBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_patrol_pay_barter_post_success",
                "gwp_patrol_pay_barter_post",
                "close_window",
                "罚金确认。本轮案件到此结案。",
                PatrolBarterSuccessfulCondition,
                OnPatrolFineBarterAcceptedConsequence,
                100);

            starter.AddDialogLine(
                "gwp_patrol_pay_barter_post_failed",
                "gwp_patrol_pay_barter_post",
                "gwp_patrol_options",
                "你的出价低于正式罚金。你可以继续出价，或拒绝执法。",
                () => !PatrolBarterSuccessfulCondition(),
                null,
                100);

            // 选项1.5：谈判放行（Barter窗口，保留声望）
            starter.AddPlayerLine(
                "gwp_patrol_negotiate_barter",
                "gwp_patrol_options",
                "gwp_patrol_barter_pre",
                "{GWP_PATROL_NEGOTIATE_TEXT}",
                PatrolNegotiateBarterCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_patrol_barter_pre",
                "gwp_patrol_barter_pre",
                "gwp_patrol_barter_screen",
                "可以，报出你的放行价码。",
                null,
                null,
                100);

            starter.AddDialogLine(
                "gwp_patrol_barter_screen",
                "gwp_patrol_barter_screen",
                "gwp_patrol_barter_post",
                "{=!}Barter screen goes here",
                null,
                OnNegotiateBarterConsequence,
                100);

            starter.AddDialogLine(
                "gwp_patrol_barter_post_success",
                "gwp_patrol_barter_post",
                "close_window",
                "报价通过。今日暂不追究。",
                PatrolBarterSuccessfulCondition,
                OnPatrolBarterAcceptedConsequence,
                100);

            starter.AddDialogLine(
                "gwp_patrol_barter_post_failed",
                "gwp_patrol_barter_post",
                "close_window",
                "报价未达底线。开始执法！",
                () => !PatrolBarterSuccessfulCondition(),
                OnPatrolBarterRejectedConsequence,
                100);

            // 选项2：拒绝缴纳
            starter.AddPlayerLine(
                "gwp_patrol_refuse",
                "gwp_patrol_options",
                "gwp_patrol_refuse_response",
                "我拒绝执法。",
                null,
                null,
                100);

            // 拒绝后NPC回应
            starter.AddDialogLine(
                "gwp_patrol_refuse_response",
                "gwp_patrol_refuse_response",
                "close_window",
                "拒绝执法已记录。纠察队，执行抓捕！",
                null,
                OnRefuseConsequence,
                100);
        }

        /// <summary>
        /// 对话触发条件：对话方是纠察队 + 玩家有负声望
        /// </summary>
        private bool PatrolDialogCondition()
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;
            if (!IsPatrol(conversationParty)) return false;

            if (_suppressPatrolMeetings)
            {
                DebugLog("返程抑制中，拒绝再次进入纠察队对话");
                try
                {
                    if (PlayerEncounter.IsActive)
                    {
                        PlayerEncounter.LeaveEncounter = true;
                        PlayerEncounter.Finish(false);
                    }
                }
                catch { }
                return false;
            }

            int rep = PlayerState.Reputation;
            if (rep >= 0) return false;

            // 计算罚款并设对话变量
            _dialogFine = Math.Abs(rep) * GwpTuning.Patrol.FinePerPoint;
            _dialogBribeAmount = _dialogFine / GwpTuning.Patrol.NegotiationDivisor;
            if (_dialogBribeAmount < 1) _dialogBribeAmount = 1;
            _dialogPatrol = conversationParty;
            int playerGold = Hero.MainHero.Gold;

            DebugLog($"进入纠察队对话：party={conversationParty.StringId}, rep={rep}, fine={_dialogFine}");

            string canPay = playerGold >= _dialogFine
                ? $"你当前携带 {playerGold} 金，可直接缴清。"
                : $"你当前携带 {playerGold} 金，可在谈判界面继续出价或选择拒绝。";

            MBTextManager.SetTextVariable("GWP_PATROL_GREETING",
                $"站住！纠察队正在执法。你当前负声望 {Math.Abs(rep)}，正式罚金 {_dialogFine} 金。{canPay}");

            return true;
        }

        /// <summary>
        /// 缴纳按钮条件：同时动态设置按钮文本
        /// </summary>
        private bool PatrolPayCondition()
        {
            string payText = $"缴纳正式罚金（{_dialogFine} 金，结束本轮追捕）";
            MBTextManager.SetTextVariable("GWP_PATROL_PAY_TEXT", payText);
            return true;
        }

        private bool PatrolNegotiateBarterCondition()
        {
            Hero barterHero = GetPatrolBarterHero();
            MBTextManager.SetTextVariable("GWP_PATROL_NEGOTIATE_TEXT",
                $"谈判放行（目标 {_dialogBribeAmount} 金，声望不变）");
            return barterHero != null && _dialogPatrol != null && _dialogPatrol.IsActive;
        }

        private bool PatrolBarterSuccessfulCondition()
        {
            return Campaign.Current?.BarterManager != null && Campaign.Current.BarterManager.LastBarterIsAccepted;
        }

        /// <summary>
        /// 拒绝缴纳结果：标记拒绝 + 纠察队接战
        /// 若已经宣战了，宣战后战斗依然继续
        /// </summary>
        private void OnRefuseConsequence()
        {
            _playerRefused = true;
            _suppressPatrolMeetings = false;

            // 玩家拒绝，立即宣战，纠察队发起战斗
            DeclareWarOnPlayer();

            if (_dialogPatrol != null && _dialogPatrol.IsActive)
            {
                GwpCommon.TrySetAggressiveAi(_dialogPatrol);
                _dialogPatrol.SetMoveEngageParty(MobileParty.MainParty, MobileParty.NavigationType.Default);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "你拒绝执法，纠察队将进行武力抓捕。",
                Colors.Red));
        }

        private void OnNegotiateBarterConsequence()
        {
            StartPatrolPaymentBarter(_dialogPatrol, _dialogBribeAmount, "缴纳通融费");
        }

        private void OnPayBarterConsequence()
        {
            StartPatrolPaymentBarter(_dialogPatrol, _dialogFine, "缴纳正式罚金");
        }

        private void OnPatrolBarterAcceptedConsequence()
        {
            _bribeProtectionDays = 4;
            MakePeaceWithPoliceAndVictims();
            EndDialogueAndDismissPatrols();
            InformationManager.DisplayMessage(new InformationMessage(
                "放行谈判达成：你获得 4 天保护期（声望不变）。",
                Colors.Yellow));
        }

        private void OnPatrolFineBarterAcceptedConsequence()
        {
            PlayerState.ResetReputation(0);
            MakePeaceWithPoliceAndVictims();
            EndDialogueAndDismissPatrols();
            InformationManager.DisplayMessage(new InformationMessage(
                "正式罚金已缴清，当前通缉已解除，纠察队将撤离。",
                Colors.Green));
        }

        private void OnPatrolBarterRejectedConsequence()
        {
            _playerRefused = true;
            _suppressPatrolMeetings = false;
            DeclareWarOnPlayer();

            if (_dialogPatrol != null && _dialogPatrol.IsActive)
            {
                GwpCommon.TrySetAggressiveAi(_dialogPatrol);
                _dialogPatrol.SetMoveEngageParty(MobileParty.MainParty, MobileParty.NavigationType.Default);
            }
        }

        private void EndDialogueAndDismissPatrols()
        {
            _warDeclared = false;
            _playerRefused = false;
            _suppressPatrolMeetings = true;

            DebugLog($"对话结束，开始遣返。active={_activePatrolIds.Count}, returning={_returningPatrolIds.Count}");
            FreezeAndScheduleDestroyAllPatrols();

            try
            {
                if (PlayerEncounter.IsActive)
                {
                    GwpCommon.TryFinishPlayerEncounter();
                    DebugLog("PlayerEncounter.Finish(false) 已调用");
                }
            }
            catch { }
        }

        #endregion

        #region 每日检查

        private void OnDailyTick()
        {
            // 玩家被俘虏不处理（正在纠察队押送中）——跳过所有检测
            // 原因：每日检查调用 MakePeaceWithPoliceClan() 会解除战争状态，导致玩家意外被释放，
            //        且声望扣4分，会导致新的额外惩罚。
            if (PlayerCaptivity.IsCaptive) return;

            // 每隔天触发一次检测
            _dayCounter++;
            
            if (_bribeProtectionDays > 0)
            {
                _bribeProtectionDays--;
                if (_bribeProtectionDays == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "放行保护期已结束，纠察队将恢复例行检查。", Colors.Red));
                }
            }

            if (_dayCounter < 2) return;
            _dayCounter = 0;

            // 检测玩家与纠察队所属处于战争状态 → 声望扣4分并强制和平
            Clan gwClan = PoliceStats.GetPoliceClan();
            IFaction? playerFac = Clan.PlayerClan?.MapFaction;
            if (gwClan != null && playerFac != null &&
                FactionManager.IsAtWarAgainstFaction(gwClan, playerFac))
            {
                PlayerState.ChangeReputation(-4);
                MakePeaceWithPoliceClan();
                InformationManager.DisplayMessage(new InformationMessage(
                    $"与灰袍守卫处于战争状态，声望 -4。当前声望：{PlayerState.Reputation}",
                    Colors.Red));
            }

            int rep = PlayerState.Reputation;

            // 正面声望：取消所有通缉和巡逻
            if (rep > 0)
            {
                // 取消所有巡逻队
                ReturnAllPatrols();

                // 取消所有针对玩家的警察通缉
                if (CrimeState.IsPlayerHunted)
                {
                    foreach (var pp in PoliceStats.GetAllPoliceParties())
                    {
                        var task = CrimeState.GetTask(pp.StringId);
                        if (task != null && task.TargetCrime?.Offender?.IsMainParty == true)
                        {
                            GwpCommon.TryResetAi(pp);
                            PoliceResourceManager.StartResupply(pp);
                        }
                    }
                    CrimeState.EndPlayerHunt();
                    MakePeaceWithPoliceClan();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "当前声望为正，现有通缉已取消，追捕全部撤回。",
                        Colors.Green));
                }
            }
            // 负面声望：每日检查，确保合适的惩罚措施到位
            else if (rep < 0)
            {
                CheckAndEnforcePunishment(rep);
            }
        }

        /// <summary>
        /// 检查并确保合适的惩罚设施到位（每日触发）
        /// </summary>
        private void CheckAndEnforcePunishment(int reputation)
        {
            // -1 到 -10：派出纠察队巡逻追踪
            // 每日检查：若无纠察队，则生成一支（上限仅允许一支）
            if (reputation >= -10 && reputation <= -1)
            {
                // 如果在放行保护期内，不再出动纠察队
                if (_bribeProtectionDays > 0) return;

                if (!HasAnyPatrol())
                {
                    int mag = Math.Abs(reputation);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"你当前负声望 {mag}，纠察队已出动（约 {mag * GwpTuning.Patrol.PatrolSize} 人）。",
                        Colors.Yellow));
                    SpawnPatrol(mag);
                    _warDeclared = false;
                    _playerRefused = false;
                }
            }
            // 低于-11：转为追捕状态，由正式警察接管
            else if (reputation <= -11)
            {
                if (PlayerState.HasAtonementTask)
                    return;

                // 解散纠察队（已超出管辖范围），交由正式警察处理
                if (_activePatrolIds.Count > 0)
                    ReturnAllPatrols();

                // 检查是否已发出通缉令
                if (!CrimeState.IsPlayerHunted)
                {
                    CrimeState.TryAddPlayerCrime(
                        "累计犯罪",
                        MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero,
                        $"声望已达 {reputation}");

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"你已进入重罪区间（负声望 {Math.Abs(reputation)}），正式警察开始追捕。",
                        Colors.Red));
                }
            }
        }

        /// <summary>
        /// 判断当前是否已存在纠察队（活跃或返回途中）
        /// </summary>
        private bool HasAnyPatrol()
        {
            // 纠察队是否“仍在地图执法中”：城内队伍不计入，避免阻塞下一轮出队。
            if (_activePatrolIds.Any(id =>
            {
                var p = MobileParty.All.FirstOrDefault(x => x.StringId == id);
                return p != null && p.IsActive && p.CurrentSettlement == null;
            })) return true;

            if (_returningPatrolIds.Any(id =>
            {
                var p = MobileParty.All.FirstOrDefault(x => x.StringId == id);
                return p != null && p.IsActive && p.CurrentSettlement == null;
            })) return true;

            return MobileParty.All.Any(p => p.IsActive && IsPatrol(p) && p.CurrentSettlement == null);
        }

        #endregion

        #region 每小时

        private void OnHourlyTick()
        {
            try
            {
                CleanDeadPatrols();
                CleanupPatrolsInsideSettlements();
                UpdateReturningPatrols();
                TryReleasePatrolMeetingSuppression();

                if (_suppressPatrolMeetings)
                {
                    TryFinishSuppressedPatrolEncounter();
                    return;
                }

                // === 押送状态：玩家被俘虏后正在被纠察队送回驻地点 ===
                if (_playerCapturedByPatrol)
                {
                    UpdateEscort();
                    return;
                }

                // 玩家拒绝且纠察队全部消失（未通过战斗解决）→ 额外扣分
                if (_playerRefused && _activePatrolIds.Count == 0)
                {
                    PlayerState.ChangeReputation(-1);
                    MakePeaceWithPoliceClan();
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"你拒绝执法且案件未完成，声望 -1。当前声望：{PlayerState.Reputation}",
                        Colors.Red));
                    _playerRefused = false;
                    return;
                }

                // 玩家拒绝后，持续强制追击，避免纠察队原地不动
                if (_playerRefused)
                {
                    MobileParty player = MobileParty.MainParty;
                    if (player != null && player.IsActive)
                    {
                        foreach (string patrolId in _activePatrolIds.ToList())
                        {
                            var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == patrolId);
                            if (patrol == null || !patrol.IsActive) continue;
                            GwpCommon.TrySetAggressiveAi(patrol);
                            patrol.SetMoveEngageParty(player, MobileParty.NavigationType.Default);
                        }
                    }
                    return;
                }

                int rep = PlayerState.Reputation;

                // 只在 -1 到 -10 范围内工作
                if (rep >= 0 || rep <= -11)
                {
                    if (_activePatrolIds.Count > 0)
                        ReturnAllPatrols();
                    return;
                }

                // 追击玩家，控制接近，宣战后会触发对话（交互）
                foreach (string patrolId in _activePatrolIds.ToList())
                {
                    var patrol = MobileParty.All.FirstOrDefault(p => p.StringId == patrolId);
                    if (patrol == null || !patrol.IsActive) continue;

                    // 粮草耗尽 → 纠察队返回
                    if (patrol.ItemRoster.TotalFood <= 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            "纠察队补给耗尽，正在撤回。", Colors.Yellow));
                        ReturnAllPatrols();
                        return;
                    }

                    MobileParty player = MobileParty.MainParty;
                    if (player == null || !player.IsActive) continue;

                    // 未宣战+Engage=强制和平接触，自动弹出对话（纠察队找到玩家）
                    patrol.SetMoveEngageParty(player, MobileParty.NavigationType.Default);
                }
            }
            catch (Exception)
            {
                // InformationManager.DisplayMessage(new InformationMessage(
                //     $"[GWP Error] 纠察队Tick异常：{ex.Message}", Colors.Red));
            }
        }

        #endregion

        #region 宣战逻辑

        /// <summary>
        /// 纠察队开启战斗时的宣战（敌同步追击逻辑）
        /// </summary>
        private void DeclareWarOnPlayer()
        {
            _warDeclared = true;

            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction == null) return;

            if (!FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
            {
                try
                {
                    FactionManager.DeclareWar(policeClan, playerFaction);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "你拒绝执法，纠察队将强制缉拿。",
                        Colors.Yellow));
                }
                catch { }
            }
        }

        #endregion

        #region 生成纠察队

        private void SpawnPatrol(int repMagnitude = 1)
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            _patrolOriginSettlement = FindNearestTown(MobileParty.MainParty);
            if (_patrolOriginSettlement == null) return;

            Hero clanLeader = policeClan.Leader;
            if (clanLeader == null) return;

            string patrolId = GwpIds.PatrolIdPrefix + MBRandom.RandomInt(10000, 99999);

            try
            {
                // 使用只有 owner 的重载（不传入 leader，避免族长被转移到纠察队）
                // 对话系统用 MobileParty.ConversationParty 识别，不需要 leader hero
                MobileParty patrol = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    _patrolOriginSettlement.GatePosition,
                    1f,
                    _patrolOriginSettlement,
                    new TextObject("纠察队巡逻"),
                    policeClan,
                    policeClan.DefaultPartyTemplate,
                    null,           // ★ 修复 Bug 1：原为 clanLeader，会导致族长成为纠察队 LeaderHero
                                    //   → 族长执行领地迁移 → 执行领地报错 → 玩家加载报错
                                    //   mod 自管犯罪状态（_playerCapturedByPatrol），不需要原生 owner
                    "", "",
                    5f,
                    false);         // avoidHostileActions=false，让玩家可敌对并触发对话

                patrol.StringId = patrolId;
                patrol.ActualClan = policeClan;

                patrol.MemberRoster.Clear();
                FillPatrolTroops(patrol, repMagnitude);
                // 5天食物量，确保足够追踪和返回
                PoliceResourceManager.ReplenishFood(patrol, 5);
                // 赋发船只（仅当导航DLC/可选海战 DLC 时，无DLC时默认忽略）
                PoliceResourceManager.GivePoliceShips(patrol);

                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.Ai.SetInitiative(1f, 0f, 999f);

                _activePatrolIds.Add(patrolId);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"纠察队已从 {_patrolOriginSettlement.Name} 出发，正在追踪你。",
                    Colors.Yellow));
            }
            catch (Exception)
            {
                // InformationManager.DisplayMessage(new InformationMessage(
                //     $"[GWP Error] 生成纠察队失败：{ex.Message}", Colors.Red));
            }
        }

        private void FillPatrolTroops(MobileParty patrol, int repMagnitude)
        {
            if (patrol == null) return;

            // 纠察队规模 = 违法点数 × PatrolSize（每分20人）
            // 例如声望-3 → 60人
            int totalSize = Math.Max(1, repMagnitude) * GwpTuning.Patrol.PatrolSize;

            CharacterObject infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
            CharacterObject archer = CharacterObject.Find(GwpIds.ArcherId);

            if (infantry != null && archer != null)
            {
                int infantryCount = (int)(totalSize * 0.6f);
                int archerCount = totalSize - infantryCount;
                patrol.MemberRoster.AddToCounts(infantry, infantryCount);
                patrol.MemberRoster.AddToCounts(archer, archerCount);
            }
            else if (infantry != null)
            {
                patrol.MemberRoster.AddToCounts(infantry, totalSize);
            }
            else if (archer != null)
            {
                patrol.MemberRoster.AddToCounts(archer, totalSize);
            }
        }

        #endregion

        #region 战斗处理

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null) return;

                bool patrolInvolved = false;
                bool playerInvolved = false;
                MobileParty involvedPatrol = null!;

                foreach (var p in mapEvent.InvolvedParties)
                {
                    if (p?.MobileParty == null) continue;
                    if (IsPatrol(p.MobileParty))
                    {
                        patrolInvolved = true;
                        involvedPatrol = p.MobileParty;
                    }
                    if (p.MobileParty.IsMainParty) playerInvolved = true;
                }

                if (!patrolInvolved || !playerInvolved) return;

                // 投降/战败兜底：玩家已被俘则直接走押送惩罚
                if (PlayerCaptivity.IsCaptive)
                {
                    DebugLog("MapEventEnded 检测到玩家已被俘，进入押送流程");
                    OnPatrolVictory(involvedPatrol);
                    _playerRefused = false;
                    return;
                }

                // 只有已经宣战且玩家拒绝缴纳后才处理战斗结果
                if (!_warDeclared || !_playerRefused) return;

                bool patrolWon = false;
                if (mapEvent.HasWinner && mapEvent.Winner != null)
                {
                    foreach (var p in mapEvent.Winner.Parties)
                    {
                        if (p?.Party?.IsMobile == true && IsPatrol(p.Party.MobileParty))
                        {
                            patrolWon = true;
                            break;
                        }
                    }

                    if (!patrolWon)
                    {
                        var loser = (mapEvent.Winner == mapEvent.AttackerSide)
                            ? mapEvent.DefenderSide
                            : mapEvent.AttackerSide;
                        if (loser != null)
                        {
                            foreach (var p in loser.Parties)
                            {
                                if (p?.Party?.IsMobile == true && p.Party.MobileParty.IsMainParty)
                                {
                                    patrolWon = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (_warDeclared && _playerRefused)
                {
                    // 投降等收束路径可能没有 winner 信息，按纠察队执法成功处理
                    patrolWon = true;
                    DebugLog("MapEventEnded 无 winner，按投降路径处理为纠察队胜利");
                }

                if (patrolWon)
                {
                    OnPatrolVictory(involvedPatrol);
                }
                else
                {
                    OnPlayerVictory();
                    MakePeaceWithPoliceClan();
                    CleanDeadPatrols();
                    ReturnAllPatrols();
                }

                _playerRefused = false;
            }
            catch (Exception)
            {
                // InformationManager.DisplayMessage(new InformationMessage(
                //     $"[GWP Error] 纠察队战斗处理异常：{ex.Message}", Colors.Red));
                // ★ 修改：兜底恢复和平，确保即便异常后事件也不残留
                try { MakePeaceWithPoliceAndVictims(); } catch { }
                _playerRefused = false;
            }
        }

        internal void OnPatrolVictory(MobileParty patrol)
        {
            patrol = ResolveEscortPatrol(patrol);
            if (patrol == null) return;

            _playerCapturedByPatrol = true;
            _escortPatrolId = patrol.StringId;
            _suppressPatrolMeetings = false;

            Settlement? escortTarget = _patrolOriginSettlement
                                       ?? FindNearestTown(patrol)
                                       ?? (MobileParty.MainParty != null ? FindNearestTown(MobileParty.MainParty) : null);
            if (escortTarget != null)
                _patrolOriginSettlement = escortTarget;

            if (patrol != null && patrol.IsActive && escortTarget != null)
            {
                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.SetMoveGoToSettlement(
                    escortTarget,
                    MobileParty.NavigationType.Default,
                    false);
            }

            if (patrol != null)
            {
                _activePatrolIds.Remove(patrol.StringId);
                _returningPatrolIds.Remove(patrol.StringId);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"你被纠察队击败，正被押送至 {(_patrolOriginSettlement?.Name?.ToString() ?? "驻地点")} 接受处罚...",
                Colors.Yellow));
        }

        private void OnPlayerVictory()
        {
            PlayerState.ChangeReputation(-1);

            InformationManager.DisplayMessage(new InformationMessage(
                $"你击败纠察队，声望 -1。当前声望：{PlayerState.Reputation}",
                Colors.Red));
        }





        #endregion

        #region 押送检测

        private void UpdateEscort()
        {
            if (!_playerCapturedByPatrol) return;

            MobileParty escort = null!;
            if (_escortPatrolId != null)
                escort = MobileParty.All.FirstOrDefault(p => p.StringId == _escortPatrolId);

            if (escort == null || !escort.IsActive)
            {
                Settlement fallback = FindNearestTown(MobileParty.MainParty) ?? _patrolOriginSettlement;
                ExecutePunishment(fallback);

                _playerCapturedByPatrol = false;
                _escortPatrolId = null!;
                CleanDeadPatrols();
                TryReleasePatrolMeetingSuppression();
                return;
            }

            if (_patrolOriginSettlement == null)
                _patrolOriginSettlement = FindNearestTown(escort) ?? FindNearestTown(MobileParty.MainParty);

            if (_patrolOriginSettlement != null)
            {
                escort.Ai.SetDoNotMakeNewDecisions(true);
                escort.SetMoveGoToSettlement(
                    _patrolOriginSettlement,
                    MobileParty.NavigationType.Default,
                    false);
            }

            if (_patrolOriginSettlement != null)
            {
                float dist = escort.GetPosition2D.Distance(_patrolOriginSettlement.Position.ToVec2());
                if (dist < 5f)
                {
                    try { if (PlayerCaptivity.IsCaptive) PlayerCaptivity.EndCaptivity(); } catch { }
                    ExecutePunishment(_patrolOriginSettlement);

                    _playerCapturedByPatrol = false;
                    _escortPatrolId = null!;

                    try { DestroyPartyAction.Apply(null, escort); } catch { }

                    CleanDeadPatrols();
                    TryReleasePatrolMeetingSuppression();
                }
            }
        }

        private void ExecutePunishment(Settlement settlement)
        {
            try
            {
                if (PlayerCaptivity.IsCaptive)
                {
                    try { PlayerCaptivity.EndCaptivity(); } catch { }
                }

                MobileParty mainParty = MobileParty.MainParty;
                if (mainParty != null && settlement != null)
                {
                    try
                    {
                        mainParty.Position = settlement.GatePosition;
                        mainParty.Party.SetVisualAsDirty();
                    }
                    catch { }
                }

                int rep = PlayerState.Reputation;
                int fine = Math.Abs(rep) * GwpTuning.Patrol.FinePerPoint;
                int paid = CollectFine(fine);
                int recovered = GwpTuning.Patrol.FinePerPoint > 0 ? paid / GwpTuning.Patrol.FinePerPoint : 0;
                int repAfter = Math.Min(0, rep + recovered);
                PlayerState.ResetReputation(repAfter);

                MakePeaceWithPoliceAndVictims();

                string townName = settlement?.Name?.ToString() ?? "最近城镇";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"你被押送到 {townName}：应缴 {fine} 金，实缴 {paid} 金，声望恢复到 {repAfter}（按实缴恢复）。",
                    Colors.Yellow));
            }
            catch (Exception)
            {
                // InformationManager.DisplayMessage(new InformationMessage(
                //     $"[GWP Error] 执行惩罚异常：{ex.Message}", Colors.Red));
                // ★ 修改：兜底恢复和平，确保即便异常后事件也不残留
                try { MakePeaceWithPoliceAndVictims(); } catch { }
            }
        }

        #endregion

        #region 罚金收取

        private int CollectFine(int fine)
        {
            int collected = PoliceResourceManager.CollectFineGoldOnly(fine);
            InformationManager.DisplayMessage(new InformationMessage(
                $"纠察队已收取罚金 {collected} 金（应缴 {fine} 金，仅收金币）。",
                Colors.Yellow));
            return collected;
        }

        #endregion
    }
}
