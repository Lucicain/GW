using System;
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
    public class PlayerBountyBehavior : CampaignBehaviorBase
    {
        // ---- 悬赏猎人常量 ----
        private const float OfferCooldownDays = 2f; // 追捕任务推送冷却：每2天推送一次
        private const float IntelReportIntervalDays = 2f; // 每2天向任务日志追加一条侦察情报
        private const int RewardPerTroop = 200;
        /// <summary>
        /// 护送方与目标距离低于此值时自动宣战并接战（与 PoliceEnforcementBehavior.WarDistance 保持一致）。
        /// </summary>
        private const float EscortEngageDistance = 3f;

        // ---- 招募系统常量 ----
        private const int RecruitmentReputationThreshold = 20;
        private const int RecruitmentPatrolSize = 20;
        private const string RecruitmentPatrolPrefix = "gwp_recruit_";

        // ---- 持久化状态 ----
        private string _activeBountyTargetId = null;
        private string _activeBountyTargetName = null;     // 目标显示名（读档后恢复任务标题用）
        private string _activeBountyTargetFactionId = null;
        private int _activeBountyTargetSize = 0;
        private bool _waitingForCollection = false;
        private int _pendingReward = 0;
        private bool _recruitmentOffered = false;  // 是否已发出过招募邀请（拒绝或接受后均置true，防重复）
        private bool _recruitmentAccepted = false; // 玩家是否接受了招募
        private string _escortPolicePartyId = null; // 当前护送玩家追捕的警察部队 StringId（null=无护送，向族长领赏）

        // ---- 运行时状态（不持久化）----
        private CampaignTime _lastOfferTime = CampaignTime.Zero;
        private CampaignTime _lastIntelReportTime = CampaignTime.Zero; // 运行时，不持久化（读档后立即触发一次，好体验）
        private BountyHunterQuest _activeQuest = null;
        private string _recruitmentPatrolId = null;       // 当前在场的招募使者队ID
        private Settlement _recruitmentPatrolOrigin = null; // 使者出发的定居点（返回目标）
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

        // 黑袍指挥官全套装备ID（五件）
        private static readonly HashSet<string> CommanderSetIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "wcomlegs",
            "wcomgloves",
            "wcomarmorhv",
            "wcomshoulder",
            "wcomhelmethv",
            "wharnesscom"
        };

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
                _recruitmentPatrolId        = null;
                _recruitmentPatrolOrigin    = null;
                _recruitmentPatrolReturning = false;
                _awaitingQuestReconnect     = false; // 由 TryRestoreBountyQuestOnSessionStart 按需设置
            }
        }

        #region 对话注册

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // ★ 每次新会话（新档/读档）必须重置此 static 标志。
            // 原因：_notificationTypeRegistered 是 static 字段，在进程生命周期内持续存在。
            // 会话1注册后置 true → 会话2 TryRegisterNotificationType() 直接 return →
            // 新 MapNotificationView 未注册类型 → 通知图标消失 → 玩家永远看不到悬赏任务。
            _notificationTypeRegistered = false;

            // ── 招募对话（招募使者接触玩家时触发）────────────────────────────────────
            starter.AddDialogLine(
                "gwp_recruit_start",
                "start",
                "gwp_recruit_options",
                "{GWP_RECRUIT_GREETING}",
                RecruitDialogCondition,
                null,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_accept",
                "gwp_recruit_options",
                "gwp_recruit_accept_response",
                "我接受，愿为灰袍守卫效力。",
                null, null, 100);

            starter.AddDialogLine(
                "gwp_recruit_accept_response",
                "gwp_recruit_accept_response",
                "close_window",
                "很好。这套装备能让我们认出你的身份。击败通缉犯后，前往我们首领处领取赏金。"
                + "务必记住——追捕时引发的战争，灰袍会在任务结算后出面调停。",
                null,
                OnRecruitAcceptConsequence,
                100);

            starter.AddPlayerLine(
                "gwp_recruit_refuse",
                "gwp_recruit_options",
                "gwp_recruit_refuse_response",
                "不，我不感兴趣。",
                null, null, 100);

            starter.AddDialogLine(
                "gwp_recruit_refuse_response",
                "gwp_recruit_refuse_response",
                "close_window",
                "随你。若你改变主意，为时已晚——此机会不会再来。",
                null,
                OnRecruitRefuseConsequence,
                100);

            // ── 招募已完成时的兜底对话 ──────────────────────────────────────────────
            // LeaveEncounter = true 正常情况下已足够；这条对话防止极端情况下
            // 遭遇系统在 close_window 后再次触发对话，导致"我不能和你说话"→战斗准备。
            starter.AddDialogLine(
                "gwp_recruit_already_done",
                "start",
                "close_window",
                "我们的事务已了结，请继续前行。",
                () =>
                {
                    var conv = MobileParty.ConversationParty;
                    if (conv == null || !IsRecruitmentPatrol(conv)) return false;
                    if (!_recruitmentOffered) return false;
                    // 兜底：再次确保遭遇被和平关闭
                    if (PlayerEncounter.IsActive)
                        PlayerEncounter.LeaveEncounter = true;
                    return true;
                },
                () => TriggerPatrolReturn(), // ★ 立刻重定向，防止巡逻队继续追玩家
                100);

            // ── 赏金领取（向护送警察对话，优先）────────────────────────────────────
            // 有护送方时，玩家与护送警察对话领取赏金；无护送方时降级为族长路径。
            starter.AddPlayerLine(
                "gwp_bounty_escort_collect",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_escort_reward_response",
                "关于那个悬赏任务，我已击败目标，来结算赏金。",
                EscortBountyRewardCondition,
                null,
                101); // 优先级略高，防止与族长选项同时出现时冲突

            starter.AddDialogLine(
                "gwp_bounty_escort_reward_response",
                "gwp_bounty_escort_reward_response",
                "lord_pretalk",
                "{GWP_BOUNTY_REWARD_RESPONSE}",
                null,
                BountyRewardConsequence, // 复用现有结算逻辑
                100);

            // ── 赏金领取（向警察领主对话，无护送时的兜底路径）────────────────────────
            starter.AddPlayerLine(
                "gwp_bounty_collect_option",
                "lord_talk_speak_diplomacy_2",
                "gwp_bounty_reward_response",
                "关于那个悬赏任务，我已经完成了。",
                BountyRewardCondition,
                null,
                100);

            starter.AddDialogLine(
                "gwp_bounty_reward_response",
                "gwp_bounty_reward_response",
                "lord_pretalk",
                "{GWP_BOUNTY_REWARD_RESPONSE}",
                null,
                BountyRewardConsequence,
                100);

            // ── 读档后悬赏任务恢复（兜底）─────────────────────────────────────────
            // 此时 SyncData 已完成，所有持久化字段均已正确加载，可以安全访问。
            TryRestoreBountyQuestOnSessionStart();
        }

        #endregion

        #region 招募对话逻辑

        private bool RecruitDialogCondition()
        {
            MobileParty conversationParty = MobileParty.ConversationParty;
            if (conversationParty == null) return false;
            if (!IsRecruitmentPatrol(conversationParty)) return false;
            if (_recruitmentOffered || _recruitmentAccepted) return false;

            MBTextManager.SetTextVariable("GWP_RECRUIT_GREETING",
                "旅行者，稍等。灰袍守卫注意到你近来的善行，我们想与你谈一笔生意。"
                + "作为悬赏猎人，你可以协助我们追捕通缉犯，并获得丰厚赏金。"
                + "但需提醒：追捕行动可能被当地国家视为入侵，引发战争。"
                + "不过，任务完成并领取赏金后，灰袍守卫会出面调停，"
                + "确保你不会承担战争后果。你是否愿意加入？");
            return true;
        }

        private void OnRecruitAcceptConsequence()
        {
            _recruitmentAccepted = true;
            _recruitmentOffered = true;
            GiveCommanderEquipment();
            // ★ 不在此处销毁部队 ★
            // DestroyPartyAction 在对话 Consequence 中立即执行会导致
            // MapConversationTableau.OnTick 仍在渲染时访问已销毁的角色装备槽
            // → ArgumentOutOfRangeException。
            // UpdateRecruitmentPatrol 会在下一次每小时 Tick（对话安全关闭后）执行销毁。

            // ★ 通知遭遇系统：对话结束后和平收场，不进入战斗准备界面 ★
            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            // ★ 立刻重定向巡逻队回定居点，防止其继续追玩家触发循环对话 ★
            TriggerPatrolReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                "你已成为灰袍悬赏猎人！黑袍指挥官套装已加入行李，穿戴后即可接受悬赏任务。",
                Colors.Green));
        }

        private void OnRecruitRefuseConsequence()
        {
            _recruitmentOffered = true;
            // 同理，不在 Consequence 中销毁部队，交由下一 Tick 处理

            // ★ 通知遭遇系统和平收场 ★
            if (PlayerEncounter.IsActive)
                PlayerEncounter.LeaveEncounter = true;

            // ★ 立刻重定向巡逻队回定居点 ★
            TriggerPatrolReturn();

            InformationManager.DisplayMessage(new InformationMessage(
                "你拒绝了灰袍守卫的招募，此机会不会再来。",
                Colors.Yellow));
        }

        /// <summary>
        /// 将黑袍指挥官全套五件装备加入玩家行李。
        /// 同时输出调试信息，方便确认每件装备是否成功找到。
        /// </summary>
        private static void GiveCommanderEquipment()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return;

            // 使用有序列表保证遍历顺序一致，便于比对调试日志
            var ids = new List<string>(CommanderSetIds);
            int given = 0;

            foreach (string itemId in ids)
            {
                // 先用 MBObjectManager，失败时再遍历全部 ItemObject 兜底
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    // 大小写不敏感的全量搜索兜底
                    foreach (ItemObject candidate in Game.Current.ObjectManager.GetObjectTypeList<ItemObject>())
                    {
                        if (candidate.StringId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                        {
                            item = candidate;
                            break;
                        }
                    }
                }

                if (item != null)
                {
                    roster.AddToCounts(new EquipmentElement(item), 1);
                    given++;
                }
            }
        }

        #endregion

        #region 招募使者部队

        private void SpawnRecruitmentPatrol()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            Settlement spawnPoint = FindNearestTown(MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero);
            if (spawnPoint == null) return;

            Hero clanLeader = policeClan.Leader;
            if (clanLeader == null) return;

            string patrolId = RecruitmentPatrolPrefix + MBRandom.RandomInt(10000, 99999);

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

                CharacterObject infantry = CharacterObject.Find("gwheavyinfantry");
                if (infantry != null)
                    patrol.MemberRoster.AddToCounts(infantry, RecruitmentPatrolSize);

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
                _recruitmentPatrolId = null;
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
                    _recruitmentPatrolId = null;
                    _recruitmentPatrolReturning = false;
                    return;
                }

                patrol.Ai.SetDoNotMakeNewDecisions(true);
                patrol.SetMoveGoToSettlement(target, MobileParty.NavigationType.Default, false);

                float dist = patrol.GetPosition2D.Distance(target.GetPosition2D);
                if (dist < 3f)
                {
                    try { DestroyPartyAction.Apply(null, patrol); } catch { }
                    _recruitmentPatrolId = null;
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
            _recruitmentPatrolId = null;
        }

        private bool IsRecruitmentPatrol(MobileParty party) =>
            party?.StringId?.StartsWith(RecruitmentPatrolPrefix) == true;

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
                _escortPolicePartyId = null;
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
                    if (dist < EscortEngageDistance)
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
            if ((CampaignTime.Now - _lastIntelReportTime).ToDays < IntelReportIntervalDays) return;
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

        #region 强制对话拦截（招募使者遭遇玩家时）

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            bool recruitInvolved = false;
            bool playerInvolved = false;

            foreach (var p in mapEvent.InvolvedParties)
            {
                if (p.MobileParty != null && IsRecruitmentPatrol(p.MobileParty)) recruitInvolved = true;
                if (p.MobileParty != null && p.MobileParty.IsMainParty) playerInvolved = true;
            }

            if (recruitInvolved && playerInvolved &&
                PlayerEncounter.IsActive && PlayerEncounter.EncounteredParty != null)
            {
                try { PlayerEncounter.DoMeeting(); } catch { }
            }
        }

        #endregion

        #region 赏金领取对话

        /// <summary>
        /// 护送方对话领赏条件：有护送方 + 等待领赏 + 正在和护送方对话。
        /// 优先于族长路径（优先级 101 vs 100）。
        /// </summary>
        private bool EscortBountyRewardCondition()
        {
            if (!_waitingForCollection) return false;
            if (string.IsNullOrEmpty(_escortPolicePartyId)) return false;
            var convParty = MobileParty.ConversationParty;
            if (convParty?.StringId != _escortPolicePartyId) return false;

            MBTextManager.SetTextVariable("GWP_BOUNTY_REWARD_RESPONSE",
                $"出色的工作。任务已完成，这是约定的赏金：{_pendingReward} 第纳尔。");
            return true;
        }

        /// <summary>
        /// 族长对话领赏条件：无护送方（或护送方已失联）+ 等待领赏 + 正在和族长对话。
        /// 作为护送路径不可用时的兜底。
        /// </summary>
        private bool BountyRewardCondition()
        {
            if (!_waitingForCollection) return false;
            if (!string.IsNullOrEmpty(_escortPolicePartyId)) return false; // 有护送方时走护送路径
            Hero conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero == null) return false;
            Hero policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader == null || conversationHero != policeLeader) return false;

            MBTextManager.SetTextVariable("GWP_BOUNTY_REWARD_RESPONSE",
                $"出色的工作。按照约定，这是你应得的赏金：{_pendingReward} 第纳尔。希望我们还有合作的机会。");
            return true;
        }

        private void BountyRewardConsequence()
        {
            try
            {
                int reward = _pendingReward;
                Hero.MainHero.ChangeHeroGold(reward);
                try { _activeQuest?.SucceedQuest(); } catch { }
                InformationManager.DisplayMessage(new InformationMessage(
                    $"已从警察领主处领取悬赏赏金：{reward} 第纳尔",
                    Colors.Green));
                MakePeaceWithCriminalFaction();
            }
            catch { }
            finally
            {
                // 释放护送方 AI 限制，让其恢复正常巡逻
                ReleaseEscortAi();

                _escortPolicePartyId         = null;
                _waitingForCollection        = false;
                _pendingReward               = 0;
                _activeBountyTargetSize      = 0;
                _activeBountyTargetName      = null;
                _activeBountyTargetFactionId = null;
                _activeQuest                 = null;
            }
        }

        private void MakePeaceWithCriminalFaction()
        {
            if (string.IsNullOrEmpty(_activeBountyTargetFactionId)) return;
            try
            {
                IFaction playerFaction = Hero.MainHero?.MapFaction;
                if (playerFaction == null) return;

                IFaction criminalFaction = null;
                foreach (Kingdom k in Kingdom.All)
                    if (k.StringId == _activeBountyTargetFactionId) { criminalFaction = k; break; }
                if (criminalFaction == null)
                    foreach (Clan c in Clan.All)
                        if (c.StringId == _activeBountyTargetFactionId) { criminalFaction = c; break; }

                if (criminalFaction == null || criminalFaction == playerFaction) return;
                if (FactionManager.IsAtWarAgainstFaction(playerFaction, criminalFaction))
                {
                    MakePeaceAction.Apply(playerFaction, criminalFaction);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"灰袍调停：已与 {criminalFaction.Name} 达成和平", Colors.Green));
                }
            }
            catch { }
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
                        Clan policeClan   = PoliceStats.GetPoliceClan();
                        Hero policeLeader = policeClan?.Leader;
                        if (policeLeader != null)
                        {
                            try
                            {
                                _activeQuest = new BountyHunterQuest(
                                    policeLeader,
                                    _activeBountyTargetSize * RewardPerTroop,
                                    _activeBountyTargetName ?? "未知目标");
                                _activeQuest.StartQuest();
                                if (!string.IsNullOrEmpty(_activeBountyTargetId))
                                    _activeQuest.WriteLog(
                                        $"读档恢复（兜底）：继续追踪目标（{_activeBountyTargetName ?? "未知目标"}）。");
                                else if (_waitingForCollection)
                                    _activeQuest.WriteLog("读档恢复（兜底）：目标已击败，前往领取赏金。");
                            }
                            catch { _activeQuest = null; }
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
                PlayerBehaviorPool.Reputation >= RecruitmentReputationThreshold)
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
                    _activeQuest                 = null;
                    _activeBountyTargetId        = null;
                    _activeBountyTargetName      = null;
                    _activeBountyTargetFactionId = null;
                    _activeBountyTargetSize      = 0;
                    // 释放护送方 AI，让其恢复正常行为
                    ReleaseEscortAi();
                    _escortPolicePartyId         = null;
                    return;
                }

                // ★ 每2天向任务日志追加一条侦察情报（护送方探子目击目标位置）
                UpdateIntelReport();
                return;
            }

            if (_waitingForCollection) return;

            // ── 接任务三条件 ──────────────────────────────────────────────────────────
            if (!_recruitmentAccepted) return;                                        // 条件1：已接受招募
            if (PlayerBehaviorPool.Reputation < RecruitmentReputationThreshold) return; // 条件2：声望足够
            if (!IsWearingCommanderSet()) return;                                     // 条件3：穿戴套装
            // ─────────────────────────────────────────────────────────────────────────

            if ((CampaignTime.Now - _lastOfferTime).ToDays < OfferCooldownDays) return;
            _lastOfferTime = CampaignTime.Now;

            Vec2 playerPos = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
            CrimeRecord crime = CrimePool.GetNearestNonPlayerFromAll(playerPos);

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

        #region 悬赏派发（右侧通知面板）

        private void OfferBounty(CrimeRecord crime)
        {
            if (!crime.IsOffenderValid()) return;
            TryRegisterNotificationType();
            var notification = new BountyMapNotification(crime);
            // 通知系统异常时静默忽略（不再弹窗兜底，通知系统已稳定）
            try { Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(notification); } catch { }
        }

        private static void TryRegisterNotificationType()
        {
            if (_notificationTypeRegistered) return;
            _notificationTypeRegistered = true;
            try
            {
                var mapScreen = ScreenManager.TopScreen as MapScreen;
                mapScreen?.MapNotificationView?.RegisterMapNotificationType(
                    typeof(BountyMapNotification),
                    typeof(BountyMapNotificationItemVM));
            }
            catch { }
        }

        internal void ShowBountyInquiry(CrimeRecord crime)
        {
            if (crime == null || !crime.IsOffenderValid()) return;
            if (!string.IsNullOrEmpty(_activeBountyTargetId)) return;

            MobileParty target = crime.Offender;
            int targetSize = target.Party.NumberOfAllMembers;
            int estimatedReward = targetSize * RewardPerTroop;
            string nearestSettlement = GetNearestSettlementName(target.GetPosition2D);

            string description =
                $"目标势力：{target.Name}\n" +
                $"犯罪类型：{crime.CrimeType}\n" +
                $"最后目击：{nearestSettlement} 附近\n" +
                $"队伍规模：{targetSize} 人\n" +
                $"预计赏金：约 {estimatedReward} 第纳尔\n" +
                $"（按接任务时人数 × {RewardPerTroop} 结算）\n\n" +
                $"完成后前往警察家族领主处领取赏金。";

            InformationManager.ShowInquiry(
                new InquiryData(
                    "灰袍悬赏任务",
                    description,
                    true, true,
                    "接受任务", "拒绝",
                    () => AcceptBounty(crime),
                    () => { },
                    "event:/ui/panels/quest_start"),
                true);
        }

        private void AcceptBounty(CrimeRecord crime)
        {
            if (!crime.IsOffenderValid())
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "目标已失效，悬赏任务取消", Colors.Red));
                return;
            }

            _activeBountyTargetId   = crime.Offender.StringId;
            _activeBountyTargetName = crime.Offender.Name.ToString(); // 持久化，读档后恢复任务标题
            _activeBountyTargetFactionId = crime.Offender.MapFaction?.StringId;
            _activeBountyTargetSize = crime.Offender.Party.NumberOfAllMembers;

            // 查找已分配该犯罪任务的警察部队，绑定为护送方
            // 护送方将跟随玩家追击，击败目标后玩家可直接向护送方领取赏金
            _escortPolicePartyId = CrimePool.GetAssignedPolicePartyId(crime.Offender.StringId);
            if (_escortPolicePartyId != null)
            {
                CrimePool.SetBountyEscortFlag(_escortPolicePartyId, true); // 阻止 PoliceEnforcementBehavior 干预
                InformationManager.DisplayMessage(new InformationMessage(
                    "灰袍护送方已就位，跟随你追击目标。击败后直接向护送警察领取赏金。",
                    Colors.Cyan));
            }

            Hero policeLeader = PoliceStats.GetPoliceClan()?.Leader;
            if (policeLeader != null)
            {
                try
                {
                    _activeQuest = new BountyHunterQuest(
                        policeLeader,
                        _activeBountyTargetSize * RewardPerTroop,
                        crime.Offender.Name.ToString());
                    _activeQuest.StartQuest();
                    string lastSeenNear = GetNearestSettlementName(crime.Offender.GetPosition2D);
                    _activeQuest.WriteLog(
                        $"目标：{crime.Offender.Name}（当前 {_activeBountyTargetSize} 人）。\n" +
                        $"最后目击位置：{lastSeenNear} 附近。\n" +
                        $"击败后前往警察领主处领取赏金约 {_activeBountyTargetSize * RewardPerTroop} 第纳尔。");
                }
                catch { _activeQuest = null; }
            }

            int estimatedGold = _activeBountyTargetSize * RewardPerTroop;
            InformationManager.DisplayMessage(new InformationMessage(
                $"已接受悬赏任务：追击 {crime.Offender.Name}" +
                $"（{_activeBountyTargetSize} 人），赏金约 {estimatedGold} 第纳尔",
                Colors.Cyan));
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

            MobileParty defeatedTarget = null;
            foreach (var p in loserSide.Parties)
            {
                if (p?.Party?.IsMobile == true &&
                    p.Party.MobileParty?.StringId == _activeBountyTargetId)
                { defeatedTarget = p.Party.MobileParty; break; }
            }
            if (defeatedTarget == null) return;

            // 用接任务时快照的人数计算赏金（战后残余人数趋近0，不能用战后数值）
            _pendingReward = _activeBountyTargetSize * RewardPerTroop;
            _activeBountyTargetId = null;
            _waitingForCollection = true;

            try { _activeQuest?.WriteLog($"目标已击败！前往领取赏金 {_pendingReward} 第纳尔。"); } catch { }

            Hero policeLeader = PoliceStats.GetPoliceClan()?.Leader;
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
            Settlement nearest = null;
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
            Settlement nearest = null;
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
            return CommanderSetIds.IsSubsetOf(wornIds);
        }

        #endregion

        // ══════════════════════════════════════════════════════════════════════
        // ★ 重要：三个嵌套类均声明为 internal（非 private）
        //   Bannerlord 存档系统通过反射按类型名找到这些类。
        //   private 类无法被外部反射访问 → 存档时抛异常 → "无法存档"。
        //   internal 类可被同程序集反射访问，存档系统可正常序列化/反序列化。
        // ══════════════════════════════════════════════════════════════════════

        #region 任务日志（QuestBase）

        /// <summary>
        /// ★ internal（非 private）：存档系统需通过反射访问此类型。
        /// ★ SyncData 必须 override：保存 _targetName，否则读档后标题为空。
        /// ★ InitializeQuestOnGameLoad 读档时自动 Fail：任务是当局会话的显示层，
        ///   实际悬赏状态由 PlayerBountyBehavior.SyncData 持久化。
        /// </summary>
        internal sealed class BountyHunterQuest : QuestBase
        {
            // [SaveableField] 让 Bannerlord 存档系统在序列化/反序列化时自动保存此字段。
            // 不加此标注则读档后 _targetName = null，任务标题变为"灰袍悬赏：未知目标"。
            // ID=1 在本类内唯一；基类 QuestBase 使用 100~107，无冲突。
            [SaveableField(1)]
            private string _targetName;

            /// <summary>
            /// 正常构造器：接受悬赏任务时调用。
            /// questGiver 须为警察领主；rewardGold 用于任务日志显示。
            /// </summary>
            public BountyHunterQuest(Hero questGiver, int rewardGold, string targetName)
                : base(
                    "gwp_bounty_quest_" + MBRandom.RandomInt(1000, 9999),
                    questGiver,
                    CampaignTime.DaysFromNow(45),
                    rewardGold)
            {
                _targetName = targetName ?? "未知目标";
            }

            /// <summary>
            /// 无参构造器：供存档系统反序列化时调用（安全兜底）。
            ///
            /// Bannerlord 通常通过 FormatterServices.GetUninitializedObject 创建实例
            /// （完全绕过构造器），但部分版本或自定义序列化器可能会调用无参构造器。
            /// 此构造器使用安全的哑值（id="gwp_bounty_quest_0", questGiver=null 等），
            /// 实际字段在 InitializeQuestOnGameLoad 中会立即被 Fail 处理，无需正确值。
            /// </summary>
            internal BountyHunterQuest()
                : base("gwp_bounty_quest_0", null, CampaignTime.Never, 0)
            {
                _targetName = "";
            }

            public override TextObject Title =>
                new TextObject($"灰袍悬赏：{_targetName ?? "未知目标"}");
            public override bool IsRemainingTimeHidden => false;

            /// <summary>
            /// ★ 关键：必须返回非空字符串，才能让本 Quest 通过 QuestManager.OnGameLoaded() 的检查。
            ///
            /// Bannerlord 在每次读档时，对 QuestManager 中的每个 Quest 执行：
            ///   if (有关联 IssueBase || IsSpecialQuest)
            ///       InitializeQuestOnGameLoad(); // 正常恢复
            ///   else
            ///       CompleteQuestWithCancel();   // 直接取消！进"旧任务"
            ///
            /// 本 Quest 没有关联 IssueBase，因此必须通过 IsSpecialQuest 告知引擎
            /// "这是一个独立的特殊任务，不需要 IssueBase 也应当正常恢复"。
            /// IsSpecialQuest 的实现就是 string.IsNullOrEmpty(SpecialQuestType) == false。
            /// </summary>
            public override string SpecialQuestType => "GwpBountyHunterQuest";

            protected override void SetDialogs() { }

            protected override void InitializeQuestOnGameLoad()
            {
                // ★ 由 QuestManager.OnGameLoaded() 调用（因 SpecialQuestType 非空，引擎不会取消本 Quest）。
                // 通过 behavior 回调重连运行时引用。若 behavior.SyncData() 已先执行，
                // OnQuestLoadedFromSave 中 hasBountyTask=true → 直接重连 _activeQuest。
                // 若 SyncData 尚未执行 → 早返回 → 首次 OnHourlyTick 兜底从 QM 查找重连。
                try
                {
                    var b = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
                    b?.OnQuestLoadedFromSave(this);
                }
                catch { }
            }

            internal void WriteLog(string text)
            {
                try { AddLog(new TextObject(text), false); } catch { }
            }

            internal void SucceedQuest()
            {
                try
                {
                    AddLog(new TextObject("你击败了悬赏目标并成功领取了赏金。"), false);
                    CompleteQuestWithSuccess();
                }
                catch { }
            }

            internal void FailQuestTargetGone()
            {
                try { CompleteQuestWithFail(new TextObject("目标已失踪，悬赏任务取消。")); } catch { }
            }

        }

        #endregion

        #region 右侧通知数据层（InformationData）

        /// <summary>
        /// ★ internal（非 private）：存档系统需通过反射访问。
        /// ★ 不存储 CrimeRecord/PlayerBountyBehavior 引用：这两者不可序列化。
        ///   只存 offender 的 StringId 和显示名，均为可序列化的 string。
        /// ★ 无参构造器：存档系统重建对象时调用。
        /// </summary>
        internal sealed class BountyMapNotification : InformationData
        {
            internal string OffenderStringId { get; private set; }
            private string _offenderName;

            // ★ 存档系统重建时需要无参构造器
            internal BountyMapNotification() : base(new TextObject("")) { }

            internal BountyMapNotification(CrimeRecord crime)
                : base(new TextObject($"追缉目标：{crime?.Offender?.Name}"))
            {
                OffenderStringId = crime?.Offender?.StringId;
                _offenderName    = crime?.Offender?.Name?.ToString() ?? "未知目标";
            }

            public override TextObject TitleText =>
                new TextObject($"灰袍悬赏：{_offenderName ?? "未知目标"}");
            public override string SoundEventPath => "event:/ui/notification/quest_start";

            public override bool IsValid()
            {
                if (OffenderStringId == null) return false;
                return MobileParty.All.Any(p => p.StringId == OffenderStringId && p.IsActive);
            }
        }

        #endregion

        #region 右侧通知ViewModel层（MapNotificationItemBaseVM）

        /// <summary>★ internal（非 private）：与 BountyMapNotification 同理。</summary>
        internal sealed class BountyMapNotificationItemVM : MapNotificationItemBaseVM
        {
            public BountyMapNotificationItemVM(BountyMapNotification data) : base(data)
            {
                NotificationIdentifier = "armycreation";
                string offenderId = data.OffenderStringId;
                _onInspect = () =>
                {
                    ExecuteRemove();
                    // 通过 StringId 从 CrimePool 查找 CrimeRecord
                    var behavior = Campaign.Current
                        ?.GetCampaignBehavior<PlayerBountyBehavior>();
                    if (behavior == null) return;
                    CrimeRecord crime = CrimePool.GetByOffenderId(offenderId);
                    if (crime != null)
                        behavior.ShowBountyInquiry(crime);
                    else
                        InformationManager.DisplayMessage(new InformationMessage(
                            "该悬赏目标已失效", Colors.Yellow));
                };
            }
        }

        #endregion

    }
}
