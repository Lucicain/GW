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
    ///   接受：发放黑袍指挥官套装 + 标记已接受
    ///   拒绝：不再派使者，但仍可在领主处按当前门槛申请
    ///   主动退出：重新加入门槛按 20→40→60 递增，第三次退出后永久关闭
    ///
    /// 接任务条件（三选一都满足才生效）：
    ///   1. 已接受招募（_recruitmentAccepted）
    ///   2. 声望 >= 阈值
    ///   3. 穿戴黑袍指挥官全套
    ///
    /// 任务流程：
    ///   右侧通知图标 → 从最近、较难、较简单三份原版选单中选择 → 接受 → 追击目标 →
    ///   胜利后前往警察领主对话领取赏金 → 灰袍调停战争
    /// </summary>
    public partial class PlayerBountyBehavior : CampaignBehaviorBase
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;

        // ---- 持久化状态 ----
        private string _activeBountyTargetId = null!;
        private string _activeBountyTargetName = null!;     // 目标显示名（读档后恢复任务标题用）
        private string _activeBountyTargetFactionId = null!;
        private string _activeBountyTargetHeroId = null!;
        private int _activeBountyCrimeCategory = (int)GwpCrimeCategory.Unknown;
        private bool _waitingForCollection = false;
        private int _activeBountyReward = 0;
        // 代收但尚未上缴的罚金，以及对应案子的罪责点（决定领主给多少办案报酬）。
        private int _pendingFieldFine = 0;
        // 案子本身该罚多少（与实收可能不同：付不起的人被押走）。报酬上限按它算。
        private int _pendingFieldFineAssessed = 0;
        private int _pendingFieldFineSeverity = 0;
        // 靠俘虏了结的案子：没有罚金进账，酬劳改由司法公库支付，上限用应缴罚金。
        private int _pendingPrisonerAssessed = 0;
        // 本次追捕中玩家部队的阵亡，用于计算领主的抚恤部分。
        private int _bountyPlayerCasualties = 0;
        private double _activeBountyDeadlineHours = -1d;
        private string _activeBountyPlayerFactionId = null!;
        private bool _playerFactionWasAtWarWhenBountyAccepted = false;
        private bool _bountyTargetEncounterStarted = false;
        private bool _recruitmentOffered = false;  // 是否已发出过招募邀请（拒绝或接受后均置true，防重复）
        private bool _recruitmentAccepted = false; // 玩家是否接受了招募
        private int _voluntaryExitCount = 0;       // 主动退出次数：1/2/3 对应下次 40/60/永久关闭
        internal bool IsRecruitedByGreyWardens => _recruitmentAccepted;
        private string _escortPolicePartyId = null!; // 当前护送玩家追捕的警察部队 StringId（null=无护送，向族长领赏）

        // ---- 运行时状态（不持久化）----
        private CampaignTime _lastOfferTime = CampaignTime.Zero;
        private CampaignTime _lastIntelReportTime = CampaignTime.Zero; // 运行时，不持久化（读档后立即触发一次，好体验）
        private BountyHunterQuest _activeQuest = null!;
        private string _recruitmentPatrolId = null!;       // 当前在场的招募使者队ID
        private Settlement _recruitmentPatrolOrigin = null!; // 使者出发的定居点（返回目标）
        private bool _recruitmentPatrolReturning = false;  // 是否已进入返回阶段
        private double _recruitmentPatrolDispatchHour = -1d; // 本轮追赶玩家的起始小时
        private static bool _notificationTypeRegistered = false;

        private PlayerBountyFlowState CurrentBountyState =>
            !string.IsNullOrEmpty(_activeBountyTargetId)
                ? PlayerBountyFlowState.HuntingTarget
                : (_waitingForCollection ? PlayerBountyFlowState.WaitingForCollection : PlayerBountyFlowState.Idle);

        private bool HasBountyTask => CurrentBountyState != PlayerBountyFlowState.Idle;
        private bool IsTrackingBountyTarget => CurrentBountyState == PlayerBountyFlowState.HuntingTarget;
        private bool IsWaitingForBountyCollection => CurrentBountyState == PlayerBountyFlowState.WaitingForCollection;
        private bool HasEscortPoliceParty => !string.IsNullOrEmpty(_escortPolicePartyId);

        /// <summary>接单需声望达标且穿着指挥官套装。</summary>
        private bool MeetsBountyEquipmentAndStanding() =>
            PlayerState.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold
            && IsWearingCommanderSet();

        /// <summary>
        /// 这名罪犯是不是玩家当前接下的那件案子的目标。别人犯了罪与玩家无关——
        /// 只有灰袍把案子交到他手上，他才有立场上去拦人、宣告罪状、收罚金。
        /// </summary>
        internal bool IsActiveBountyTarget(Hero? offender)
        {
            if (!_recruitmentAccepted || offender == null || !IsTrackingBountyTarget) return false;

            if (!string.IsNullOrWhiteSpace(_activeBountyTargetHeroId)
                && string.Equals(_activeBountyTargetHeroId, offender.StringId,
                    StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(_activeBountyTargetId)
                   && string.Equals(_activeBountyTargetId, offender.PartyBelongedTo?.StringId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private void ClearActiveBountyTarget()
        {
            _activeBountyTargetId = null!;
            _activeBountyTargetName = null!;
            _activeBountyTargetFactionId = null!;
            _activeBountyTargetHeroId = null!;
            _activeBountyCrimeCategory = (int)GwpCrimeCategory.Unknown;
        }

        /// <summary>
        /// A case settled face to face in the field, without a battle. If it is the bounty
        /// the player is carrying, the warrant moves straight to its turn-in stage - the
        /// pursuit is over, it simply ended in words. The fine travels with the player as
        /// the order's money until he reports; whether he ever does is up to him.
        /// </summary>
        internal void NotifyCaseSettledInField(
            Hero? offender,
            int collectedFine,
            int assessedFine,
            int severityPoints)
        {
            if (!IsCommissionTarget(offender)) return;
            _fieldCaseContract = true;
            _assignedCaseFine = assessedFine;
            _assignedCaseStanding = severityPoints;
            _pendingFieldFineSeverity = Math.Max(_pendingFieldFineSeverity, severityPoints);

            if (offender == null || !IsTrackingBountyTarget)
                return;

            bool isBountyTarget =
                (!string.IsNullOrWhiteSpace(_activeBountyTargetHeroId) &&
                 string.Equals(_activeBountyTargetHeroId, offender.StringId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(_activeBountyTargetId) &&
                    string.Equals(_activeBountyTargetId, offender.PartyBelongedTo?.StringId, StringComparison.OrdinalIgnoreCase));
            if (!isBountyTarget)
                return;

            NotifyCasePeacefullyResolved(offender, collectedFine);
            EnterBountyCollectionState();
            _activeBountyDeadlineHours = -1d;
            StopBountyEscortAfterTargetDefeat();
            try { _activeQuest?.MarkReadyForTurnIn(); } catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            ShowFieldSettlementNotice(collectedFine);
        }

        /// <summary>
        /// 谈判结案和战场击败是两回事，通知也要分开：这里没有"被击败"，
        /// 有的是一笔代收的罚金和一份待领的办案酬劳。
        /// </summary>
        private void ShowFieldSettlementNotice(int collectedFine)
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_field_settle_notice_title}Case settled on the road"),
                    GwpText.Get(
                        "{=gwp_field_settle_notice_body}The pursuit has ended. You must account for the commission and deliver the {VAR_1} denars collected as fines. Report to a Grey Warden lord; any shortfall must also be explained.",
                        "VAR_1", collectedFine.ToString()),
                    true,
                    false,
                    GwpText.Get("{=gwp_common_understood}Understood"),
                    string.Empty,
                    null,
                    null,
                    "event:/ui/notification/quest_finished"),
                true);
        }

        /// <summary>
        /// A case the player closed by taking the man himself. There is no fine to carry,
        /// so the order pays the same expenses out of the judicial treasury instead, and
        /// the prisoner is what has to reach a Warden lord.
        /// </summary>
        internal void NotifyCaseClosedByCapture(Hero? offender, int assessedFine, int standingPoints)
        {
            if (offender == null || !IsCommissionTarget(offender)
                || !offender.IsPrisoner
                || offender.PartyBelongedToAsPrisoner != MobileParty.MainParty?.Party) return;
            _fieldCaseContract = true;
            _pendingPrisonerHeroId = offender.StringId;
            _pendingPrisonerAssessed = Math.Max(0, assessedFine);
            _pendingFieldFineSeverity = Math.Max(0, standingPoints);

            EnterBountyCollectionState();
            _activeBountyDeadlineHours = -1d;
            StopBountyEscortAfterTargetDefeat();
            try { _activeQuest?.MarkReadyForTurnIn(); } catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        private void EnterBountyCollectionState()
        {
            _activeBountyTargetId = null!;
            _waitingForCollection = true;
        }

        private void ClearBountyTaskState()
        {
            _supportRequested = false;
            Campaign.Current?.GetCampaignBehavior<GwpFieldArrestBehavior>()?.ClearFieldGrace();
            ReleaseEscortAi();
            _escortPolicePartyId = null!;
            _waitingForCollection = false;
            _activeBountyReward = 0;
            _activeBountyDeadlineHours = -1d;
            _activeBountyPlayerFactionId = null!;
            _playerFactionWasAtWarWhenBountyAccepted = false;
            _bountyTargetEncounterStarted = false;
            ClearActiveBountyTarget();
            _activeQuest = null!;
            _fieldCaseContract = false;
            _assignedCaseFine = 0;
            _assignedCaseStanding = 0;
            _caseSubmitted = -1;
            _caseSubmittedPrisoner = false;
            _pendingPrisonerHeroId = string.Empty;
            _pendingPrisonerAssessed = 0;
            _pendingFieldFine = 0;
            _pendingFieldFineAssessed = 0;
            _pendingFieldFineSeverity = 0;
            _bountyPlayerCasualties = 0;
            ClearCaseOutcomeState();
        }

        private void EndBountyTaskState(bool tryRestorePeace)
        {
            if (tryRestorePeace)
                MakePeaceWithCriminalFaction();
            ClearBountyTaskState();
        }

        private void HandleBountyTimeout()
        {
            if (!HasBountyTask) return;

            // 已经把人打垮或已经了结过的案子不因为期限作废——活已经干完了，
            // 剩下的只是回去交差。期限只针对还没有任何结果的追捕。
            if (HasCompletedEnforcement)
            {
                if (IsTrackingBountyTarget)
                {
                    EnterBountyCollectionState();
                    _activeBountyDeadlineHours = -1d;
                    StopBountyEscortAfterTargetDefeat();
                    try { _activeQuest?.MarkReadyForTurnIn(); } catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_bounty_contract_expired_but_done}The pursuit window has closed, but the man was already dealt with. Report to any Grey Warden lord."),
                        Colors.Yellow));
                }
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_bounty_contract_timed_out}The bounty contract has expired. The pursuit is ended and any assigned Warden escort has been recalled."),
                Colors.Yellow));
            EndBountyTaskState(tryRestorePeace: true);
        }

        internal void OnBountyQuestTimedOut(BountyHunterQuest quest)
        {
            if (quest == null || !HasBountyTask) return;
            if (_activeQuest != null && !ReferenceEquals(_activeQuest, quest)) return;
            HandleBountyTimeout();
        }

        private void StopBountyEscortAfterTargetDefeat()
        {
            ReleaseEscortAi();
            _escortPolicePartyId = null!;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, ReconcileCaseOnTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Support is stored once, inside gwp_case_outcome_state. The old
            // standalone bool is read only by the migration path.
            dataStore.SyncData("gwp_case_submitted_value", ref _caseSubmitted);
            dataStore.SyncData("gwp_case_submitted_prisoner", ref _caseSubmittedPrisoner);
            dataStore.SyncData("gwp_case_contract", ref _fieldCaseContract);
            dataStore.SyncData("gwp_case_assigned_fine", ref _assignedCaseFine);
            dataStore.SyncData("gwp_case_assigned_standing", ref _assignedCaseStanding);
            dataStore.SyncData("gwp_case_prisoner_id", ref _pendingPrisonerHeroId);
            SyncCaseOutcomeData(dataStore);
            // ── 招募状态（用 int 存 bool，兼容性更好）────────────────────────────────
            int offeredInt  = _recruitmentOffered  ? 1 : 0;
            int acceptedInt = _recruitmentAccepted ? 1 : 0;
            dataStore.SyncData("gwp_recruitment_offered",  ref offeredInt);
            dataStore.SyncData("gwp_recruitment_accepted", ref acceptedInt);
            dataStore.SyncData("gwp_recruitment_voluntary_exit_count", ref _voluntaryExitCount);
            dataStore.SyncData("gwp_recruitment_patrol_dispatch_hour", ref _recruitmentPatrolDispatchHour);

            // ── 悬赏任务持久化状态 ────────────────────────────────────────────────────
            // 存档时把当前值序列化；读档时恢复（基元类型 ref 直接支持）
            int waitingInt = _waitingForCollection ? 1 : 0;
            int playerWasAtWarInt = _playerFactionWasAtWarWhenBountyAccepted ? 1 : 0;
            int targetEncounterStartedInt = _bountyTargetEncounterStarted ? 1 : 0;
            dataStore.SyncData("gwp_bounty_target_id",        ref _activeBountyTargetId);
            dataStore.SyncData("gwp_bounty_target_name",      ref _activeBountyTargetName); // 读档后补回任务标题
            dataStore.SyncData("gwp_bounty_target_faction_id",ref _activeBountyTargetFactionId);
            dataStore.SyncData("gwp_bounty_target_hero_id",   ref _activeBountyTargetHeroId);
            dataStore.SyncData("gwp_bounty_crime_category",   ref _activeBountyCrimeCategory);
            dataStore.SyncData("gwp_bounty_waiting",          ref waitingInt);
            dataStore.SyncData("gwp_bounty_reward",           ref _activeBountyReward);
            dataStore.SyncData("gwp_field_fine_pending",      ref _pendingFieldFine);
            dataStore.SyncData("gwp_field_fine_severity",     ref _pendingFieldFineSeverity);
            dataStore.SyncData("gwp_field_fine_assessed",     ref _pendingFieldFineAssessed);
            dataStore.SyncData("gwp_field_prisoner_assessed", ref _pendingPrisonerAssessed);
            dataStore.SyncData("gwp_bounty_player_casualties", ref _bountyPlayerCasualties);
            dataStore.SyncData("gwp_bounty_escort_party_id",  ref _escortPolicePartyId); // 护送警察部队 ID
            dataStore.SyncData("gwp_bounty_deadline_hours", ref _activeBountyDeadlineHours);
            dataStore.SyncData("gwp_bounty_player_faction_id", ref _activeBountyPlayerFactionId);
            dataStore.SyncData("gwp_bounty_player_was_at_war", ref playerWasAtWarInt);
            dataStore.SyncData("gwp_bounty_target_encounter_started", ref targetEncounterStartedInt);

            if (dataStore.IsLoading)
            {
                _recruitmentOffered    = offeredInt  != 0;
                _recruitmentAccepted   = acceptedInt != 0;
                _voluntaryExitCount = Math.Max(0, Math.Min(
                    _voluntaryExitCount,
                    GwpTuning.Bounty.MaximumVoluntaryExits));
                if (!Enum.IsDefined(typeof(GwpCrimeCategory), _activeBountyCrimeCategory))
                    _activeBountyCrimeCategory = (int)GwpCrimeCategory.Unknown;
                _waitingForCollection  = waitingInt  != 0;
                _playerFactionWasAtWarWhenBountyAccepted = playerWasAtWarInt != 0;
                _bountyTargetEncounterStarted = targetEncounterStartedInt != 0;
                _activeBountyTargetName ??= "";
                _activeBountyPlayerFactionId ??= "";

                // 运行时状态读档时清零（不持久化）
                _recruitmentPatrolId        = null!;
                _recruitmentPatrolOrigin    = null!;
                _recruitmentPatrolReturning = false;
                _activeQuest                = null!;
            }
        }

        #region 招募使者部队

        private List<MobileParty> GetActiveRecruitmentPatrols() =>
            MobileParty.All
                .Where(p => p != null && p.IsActive && IsRecruitmentPatrol(p))
                .ToList();

        private void ReconcileRecruitmentPatrolState()
        {
            List<MobileParty> patrols = GetActiveRecruitmentPatrols();
            if (patrols.Count == 0)
            {
                _recruitmentPatrolId = null!;
                _recruitmentPatrolOrigin = null!;
                _recruitmentPatrolReturning = false;
                _recruitmentPatrolDispatchHour = -1d;
                return;
            }

            MobileParty trackedPatrol = null!;
            if (!string.IsNullOrEmpty(_recruitmentPatrolId))
                trackedPatrol = patrols.FirstOrDefault(p => p.StringId == _recruitmentPatrolId);

            trackedPatrol ??= patrols[0];
            _recruitmentPatrolId = trackedPatrol.StringId;
            _recruitmentPatrolOrigin ??= FindNearestTown(trackedPatrol.GetPosition2D);
            if (_recruitmentPatrolDispatchHour < 0d)
                _recruitmentPatrolDispatchHour = CampaignTime.Now.ToHours;

            if ((_recruitmentOffered || _recruitmentAccepted) && !_recruitmentPatrolReturning)
                _recruitmentPatrolReturning = true;
        }

        private Settlement GetRecruitmentPatrolReturnTarget(MobileParty patrol)
        {
            if (patrol == null) return null!;

            if (!string.IsNullOrEmpty(_recruitmentPatrolId) &&
                patrol.StringId == _recruitmentPatrolId &&
                _recruitmentPatrolOrigin != null)
            {
                return _recruitmentPatrolOrigin;
            }

            return FindNearestTown(patrol.GetPosition2D);
        }

        private void DestroyRecruitmentPatrolParty(MobileParty patrol)
        {
            if (patrol != null && patrol.IsActive)
            {
                GwpCommon.TryDestroyParty(patrol);
            }

            if (patrol != null && patrol.StringId == _recruitmentPatrolId)
            {
                _recruitmentPatrolId = null!;
                _recruitmentPatrolOrigin = null!;
                _recruitmentPatrolReturning = false;
                _recruitmentPatrolDispatchHour = -1d;
            }
        }

        private void SpawnRecruitmentPatrol()
        {
            MobileParty? recruitmentPlayer = MobileParty.MainParty;
            if (recruitmentPlayer == null) return;
            ReconcileRecruitmentPatrolState();
            if (GetActiveRecruitmentPatrols().Count > 0) return;

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
                    new TextObject(GwpText.Get("{=gwp_playerbountybehavior_001}Grey Warden herald")),
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

                // 招募使者没有英雄领队，原版不会替它进城采购。若生成时不主动
                // 配粮，它会在下一个小时检查时因 TotalFood == 0 立刻掉头，玩家
                // 永远等不到邀请。沿用其他一次性灰袍队的二十日口粮规则。
                PoliceResourceManager.ProvisionTemporaryDutyParty(patrol);

                GreyWardenPartyDesireBehavior.RequestApproach(patrol, recruitmentPlayer, 8f);

                _recruitmentPatrolId = patrolId;
                _recruitmentPatrolOrigin = spawnPoint;  // 记录出发点，供返回时使用
                _recruitmentPatrolReturning = false;
                _recruitmentPatrolDispatchHour = CampaignTime.Now.ToHours;

                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_002}A Grey Warden herald is riding from {VAR_1} to meet you...", "VAR_1", spawnPoint.Name),
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
            ReconcileRecruitmentPatrolState();
            List<MobileParty> patrols = GetActiveRecruitmentPatrols();
            if (patrols.Count == 0) return;

            MobileParty player = MobileParty.MainParty;
            foreach (MobileParty patrol in patrols)
            {
                bool isTrackedPatrol = !string.IsNullOrEmpty(_recruitmentPatrolId) &&
                                       patrol.StringId == _recruitmentPatrolId;

                // 兼容已经被旧逻辑困在城里的存档：这种使者长期断粮后可能全员
                // 负伤，既无法出城，也会一直占着唯一招募使者名额。清掉它，让
                // 本小时末的资格检查立即生成一支健康、带粮的新使者队。
                if (isTrackedPatrol &&
                    !_recruitmentOffered &&
                    !_recruitmentAccepted &&
                    patrol.Party.NumberOfHealthyMembers <= 0)
                {
                    DestroyRecruitmentPatrolParty(patrol);
                    continue;
                }

                // 旧存档中仍有健康成员但已经断粮的使者可直接恢复，不应把缺粮
                // 当成放弃招募的理由。
                if (isTrackedPatrol && !_recruitmentOffered && !_recruitmentAccepted)
                    PoliceResourceManager.ProvisionTemporaryDutyParty(patrol);

                bool chaseTimedOut = isTrackedPatrol &&
                    !_recruitmentOffered &&
                    !_recruitmentAccepted &&
                    _recruitmentPatrolDispatchHour >= 0d &&
                    CampaignTime.Now.ToHours - _recruitmentPatrolDispatchHour >=
                        GwpTuning.Bounty.RecruitmentPursuitTimeoutDays * 24d;

                if (chaseTimedOut && !_recruitmentPatrolReturning)
                {
                    // 玩家持续赶路时不让同一支使者无限横跨地图。使者先进入离
                    // 自己最近的城镇销毁；下一次资格检查再从离玩家最近的城镇
                    // 派出全新的队伍，因此派遣点会随玩家当前位置刷新。
                    _recruitmentPatrolReturning = true;
                    _recruitmentPatrolOrigin = FindNearestTown(patrol.GetPosition2D);
                }

                bool shouldReturn = _recruitmentPatrolReturning ||
                                    _recruitmentOffered ||
                                    _recruitmentAccepted ||
                                    !isTrackedPatrol;

                if (shouldReturn)
                {
                    if (isTrackedPatrol &&
                        !_recruitmentPatrolReturning &&
                        (_recruitmentOffered || _recruitmentAccepted))
                    {
                        _recruitmentPatrolReturning = true;
                    }

                    Settlement target = GetRecruitmentPatrolReturnTarget(patrol);
                    if (target == null)
                    {
                        DestroyRecruitmentPatrolParty(patrol);
                        continue;
                    }

                    GreyWardenPartyDesireBehavior.RequestVisit(patrol, target, 8f);

                    float dist = patrol.GetPosition2D.Distance(target.GetPosition2D);
                    if (patrol.CurrentSettlement == target || dist < 3f)
                        DestroyRecruitmentPatrolParty(patrol);

                    continue;
                }

                if (player != null && player.IsActive)
                {
                    float contactDistance = patrol.GetPosition2D.Distance(player.GetPosition2D);
                    if (contactDistance <= GwpTuning.Bounty.RecruitmentContactDistance)
                    {
                        // 远距离仍走统一欲望层；接近后恢复原版 EngageParty 接触，
                        // 由 OnMapEventStarted 把中立遭遇转为招募对话。
                        GreyWardenPartyDesireBehavior.ClearIntent(patrol);
                        patrol.Ai.SetDoNotMakeNewDecisions(false);
                        patrol.SetMoveEngageParty(player, patrol.NavigationCapability);
                    }
                    else
                    {
                        GreyWardenPartyDesireBehavior.RequestApproach(patrol, player, 8f);
                    }
                }
            }
        }

        /// <summary>
        /// 立刻将招募使者部队切换为返回状态，并下达前往定居点的移动命令。
        /// 可在对话 Consequence 中安全调用（不销毁部队，仅改变 AI 命令）。
        /// </summary>
        private void TriggerPatrolReturn()
        {
            _recruitmentPatrolReturning = true;
            ReconcileRecruitmentPatrolState();

            foreach (MobileParty patrol in GetActiveRecruitmentPatrols())
            {
                Settlement target = GetRecruitmentPatrolReturnTarget(patrol);
                if (target == null) continue;

                GreyWardenPartyDesireBehavior.RequestVisit(patrol, target, 8f);

                // RequestVisit updates the Grey Warden desire layer, but the
                // native party can keep its old EngageParty command until the
                // next AI check. Apply the native return command immediately so
                // TargetParty/ShortTermTargetParty are cleared before another
                // encounter can be created while both parties still overlap.
                // The short do-not-attack window is the same native safeguard
                // PlayerEncounter uses when separating parties after combat.
                try
                {
                    patrol.Ai.SetDoNotAttackMainParty(2);
                    patrol.Ai.SetDoNotMakeNewDecisions(false);
                    patrol.SetMoveGoToSettlement(
                        target,
                        patrol.NavigationCapability,
                        false);
                    WriteRecruitmentTrace(patrol, "RECRUIT_NATIVE_RETURN_APPLIED",
                        "cleared native EngageParty and ordered immediate travel to " +
                        target.StringId);
                }
                catch (Exception ex)
                {
                    WriteRecruitmentTrace(patrol, "RECRUIT_NATIVE_RETURN_FAILED",
                        ex.GetType().Name + ":" + ex.Message);
                }
            }
        }

        private void DestroyRecruitmentPatrol()
        {
            foreach (MobileParty patrol in GetActiveRecruitmentPatrols())
                DestroyRecruitmentPatrolParty(patrol);

            _recruitmentPatrolId = null!;
            _recruitmentPatrolOrigin = null!;
            _recruitmentPatrolReturning = false;
            _recruitmentPatrolDispatchHour = -1d;
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
        /// 护送方消失时：
        ///   清除护送引用；任务本身仍可继续，完成后可向任意灰袍领主交付。
        /// </summary>
        private void UpdateEscortPatrol()
        {
            if (!HasEscortPoliceParty) return;

            var escort = MobileParty.All.FirstOrDefault(p => p.StringId == _escortPolicePartyId);
            if (escort == null || !escort.IsActive)
            {
                // 护送方消失 → 清除护送引用，任务仍可继续
                _escortPolicePartyId = null!;
                if (HasBountyTask)
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_playerbountybehavior_003}The escorting Warden has been lost. The warrant remains active, and any Grey Warden lord can settle it once the quarry falls."), Colors.Yellow));
                return;
            }

            // 保持悬赏与承办案件的护送关系一致，读档后也可立即恢复职责。
            CrimeState.SetBountyEscortFlag(_escortPolicePartyId, true);

            // ── 仅追击阶段跟随玩家；目标落败后立即解除护送职责 ──
            if (!IsTrackingBountyTarget) return;

            MobileParty player = MobileParty.MainParty;
            if (player == null || !player.IsActive) return;

            // 跟随职责进入原版欲望拍卖；资源危急时允许先补给再回来。
            // 求援成立与否只决定灰袍能否为这宗案子开战，不改变行动目的：
            // 始终跟着玩家。罪犯由玩家去找，灰袍到场是为了和他一起打。
            GreyWardenPartyDesireBehavior.RequestEscort(escort, player, 8f);
        }

        /// <summary>
        /// 每2天向活跃任务日志追加一条侦察情报：护送警察的探子目击目标位置。
        /// 使用任务日志（不用 DisplayMessage），让玩家在任务界面自然获知敌人动向。
        /// </summary>
        /// <summary>
        /// 本案罪犯当前所在的部队。接案时记下的部队 ID 会随着他换队或重建部队失效，
        /// 按 ID 找不到就以英雄本人为准重新解析并回写。普通案件用的是
        /// <c>TargetCrime.Offender</c>，同样跟着英雄走，这里对齐它。
        /// </summary>
        private MobileParty? ResolveBountyTargetParty()
        {
            MobileParty? party = CaseHero?.PartyBelongedTo;
            if (party?.IsActive != true && !string.IsNullOrEmpty(_activeBountyTargetId))
                party = MobileParty.All.FirstOrDefault(candidate => candidate.IsActive &&
                    string.Equals(candidate.StringId, _activeBountyTargetId,
                        StringComparison.OrdinalIgnoreCase));
            if (party?.IsActive != true) return null;
            if (!string.Equals(_activeBountyTargetId, party.StringId, StringComparison.OrdinalIgnoreCase))
                _activeBountyTargetId = party.StringId;
            return party;
        }

        private void UpdateIntelReport()
        {
            if (_activeQuest == null || !_activeQuest.IsOngoing) return;
            if ((CampaignTime.Now - _lastIntelReportTime).ToDays < GwpTuning.Bounty.IntelReportIntervalDays) return;
            _lastIntelReportTime = CampaignTime.Now;

            MobileParty? target = ResolveBountyTargetParty();
            if (target == null)
            {
                // 断了线也要说一句。原来是直接 return，任务界面一片安静，玩家只会以为
                // 情报系统坏了。
                _activeQuest.WriteLog(GwpText.Create(
                    "{=gwp_bounty_intel_lost}[Investigation] The scouts have lost the trail. There is no sighting to report this time."));
                return;
            }

            Settlement? sightingSettlement = FindNearestSettlement(target.GetPosition2D);
            TextObject sightingLocation = sightingSettlement?.EncyclopediaLinkWithName ??
                                          GwpText.Create(
                                              "{=gwp_playerbountybehavior_020}unknown location");

            // 情报来源：护送方名称；无护送方时用通用名称
            string reporterName = GwpText.Get("{=gwp_playerbountybehavior_004}Grey Warden reconnaissance party");
            if (HasEscortPoliceParty)
            {
                var escort = MobileParty.All.FirstOrDefault(
                    p => p.StringId == _escortPolicePartyId && p.IsActive);
                if (escort != null)
                    reporterName = escort.Name.ToString();
            }

            _activeQuest.WriteLog(
                GwpText.Create("{=gwp_playerbountybehavior_005}[Investigation] The spies from {VAR_1} came to report: The target was sighted near {VAR_2}.", "VAR_1", reporterName, "VAR_2", sightingLocation));
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
                    CrimeState.SetBountyEscortFlag(_escortPolicePartyId, false); // 恢复正常任务处理
                    PoliceEnforcementBehavior.RefreshPlayerBountyAssistanceEscort(
                        _escortPolicePartyId);
                    escort.Ai.SetDoNotMakeNewDecisions(false);
                }
                catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            }
        }

        #endregion

        #region 每小时检查

        private void OnHourlyTick()
        {
            if (Hero.MainHero == null) return;
            ReconcileAssignedCase();

            if (IsTrackingBountyTarget &&
                _activeBountyDeadlineHours > 0d &&
                CampaignTime.Now.ToHours >= _activeBountyDeadlineHours)
            {
                if (_activeQuest != null && _activeQuest.IsOngoing)
                    _activeQuest.TimeOutQuest();
                else
                    HandleBountyTimeout();
                return;
            }

            // 维护招募使者部队（每小时刷新行进命令）
            UpdateRecruitmentPatrol();

            // 维护悬赏护送部队（每小时刷新跟随命令）
            UpdateEscortPatrol();

            // 声望达标且尚未招募过 → 生成招募使者
            if (!_recruitmentOffered &&
                !_recruitmentAccepted &&
                PlayerState.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold)
            {
                ReconcileRecruitmentPatrolState();
                if (_recruitmentPatrolId == null)
                    SpawnRecruitmentPatrol();
            }

            // 任务未由玩家完成时，不再区分目标消失等失败原因；统一等待四十五日超时。
            if (IsTrackingBountyTarget)
            {
                UpdateIntelReport();
                return;
            }

            if (IsWaitingForBountyCollection) return;

            // ── 接任务三条件 ──────────────────────────────────────────────────────────
            if (!_recruitmentAccepted) return;                                        // 条件1：已接受招募
            if (!MeetsBountyEquipmentAndStanding()) return;                           // 条件2、3：声望与套装
            // ─────────────────────────────────────────────────────────────────────────

            if ((CampaignTime.Now - _lastOfferTime).ToDays < GwpTuning.Bounty.OfferCooldownDays) return;
            _lastOfferTime = CampaignTime.Now;

            if (CrimeState.GetAvailablePlayerBounties().Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_playerbountybehavior_012}The black-robed commander's equipment has been identified. There is currently no bounty contract available."),
                    Colors.White));
                return;
            }

            OfferBountySelection();
        }

        #endregion

        #region 战斗结束（检测目标被击败）

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            if (!HasBountyTask || CaseHero == null) return;
            bool targetInBattle = mapEvent.InvolvedParties.Any(p => p?.MobileParty?.LeaderHero == CaseHero
                || (!string.IsNullOrEmpty(_activeBountyTargetId) && p?.MobileParty?.StringId == _activeBountyTargetId));
            bool playerInBattle = mapEvent.InvolvedParties.Any(p => p?.MobileParty == MobileParty.MainParty);
            if (!targetInBattle || !playerInBattle) return;
            _caseBattleToReconcile = mapEvent;
            _caseBattleHeroId = _activeBountyTargetHeroId;
            foreach (var side in new[] { mapEvent.AttackerSide, mapEvent.DefenderSide })
                foreach (var party in side.Parties)
                    if (party.Party == MobileParty.MainParty?.Party)
                        _bountyPlayerCasualties += Math.Max(0, party.DiedInBattle.TotalManCount);

            RecordCaseBattleOutcome(mapEvent);

            // Native prisoner selection has not completed at this event. Reconcile
            // custody and deterrence together after the encounter returns to the map.
        }

        /// <summary>
        /// 这一场谁赢了。灰袍惩戒罪犯本来就只有"把他打垮"这一招，所以玩家把本案
        /// 目标打垮就是执法达成；人有没有被押走，是另一条账。
        /// </summary>
        private void RecordCaseBattleOutcome(MapEvent mapEvent)
        {
            if (!mapEvent.HasWinner || mapEvent.Winner == null) return;
            MapEventSide? playerSide = FindMapEventSide(mapEvent, MobileParty.MainParty?.Party);
            PartyBase? targetParty = CaseHero?.PartyBelongedTo?.Party
                ?? MobileParty.All.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(_activeBountyTargetId) &&
                    string.Equals(p.StringId, _activeBountyTargetId, StringComparison.OrdinalIgnoreCase))?.Party;
            MapEventSide? targetSide = FindMapEventSide(mapEvent, targetParty);
            if (playerSide == null || targetSide == null || playerSide == targetSide) return;

            NotifyCaseBattleOutcome(CaseHero,
                playerWon: mapEvent.Winner == playerSide,
                targetOnLosingSide: mapEvent.Winner != targetSide);
        }

        private static MapEventSide? FindMapEventSide(MapEvent mapEvent, PartyBase? party)
        {
            if (party == null) return null;
            foreach (MapEventSide side in new[] { mapEvent.AttackerSide, mapEvent.DefenderSide })
                if (side != null && side.Parties.Any(entry => entry?.Party == party))
                    return side;
            return null;
        }

        #endregion

        #region 读档恢复

        /// <summary>在会话启动后直接重连原版任务；缺失显示层时按当前状态重建一次。</summary>
        private void TryRestoreBountyQuestOnSessionStart()
        {
            if (!HasBountyTask) return;

            if ((!_fieldCaseContract && _activeBountyReward <= 0) ||
                (IsTrackingBountyTarget && _activeBountyDeadlineHours <= 0d))
            {
                ClearBountyTaskState();
                return;
            }

            if (IsTrackingBountyTarget &&
                CampaignTime.Now.ToHours >= _activeBountyDeadlineHours)
            {
                HandleBountyTimeout();
                return;
            }

            try
            {
                _activeQuest = Campaign.Current?.QuestManager?.Quests
                    ?.OfType<BountyHunterQuest>()
                    ?.FirstOrDefault(quest => quest.IsOngoing)!;

                if (_activeQuest == null)
                {
                    Hero? policeLeader = PoliceStats.GetPoliceClan()?.Leader;
                    if (policeLeader == null) return;

                    CampaignTime dueTime = IsTrackingBountyTarget
                        ? CampaignTime.Hours((float)_activeBountyDeadlineHours)
                        : CampaignTime.Never;
                    _activeQuest = new BountyHunterQuest(
                        policeLeader,
                        _activeBountyReward,
                        _activeBountyTargetName ?? GwpText.Get("{=gwp_common_unknown_target}Unknown target"),
                        dueTime);
                    _activeQuest.StartQuest();
                }

                if (IsTrackingBountyTarget)
                    _activeQuest.ChangeQuestDueTime(
                        CampaignTime.Hours((float)_activeBountyDeadlineHours));
                else
                    _activeQuest.MarkReadyForTurnIn();
            }
            catch { _activeQuest = null!; }
        }

        #endregion

        #region 辅助

        private static Settlement? FindNearestSettlement(Vec2 position)
            => GwpCommon.FindNearestSettlement(position);

        private static string GetNearestSettlementName(Vec2 position) =>
            FindNearestSettlement(position)?.Name?.ToString() ??
            GwpText.Get("{=gwp_playerbountybehavior_020}unknown location");

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
