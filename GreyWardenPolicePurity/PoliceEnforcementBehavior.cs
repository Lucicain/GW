using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 警察惩戒系统
    ///
    /// 玩家被抓完整流程：
    /// 1. OnMapEventEnded：标记 IsEscortingPlayer，将最近城堡登记为押送欲望目标
    /// 2. UpdateTasks（每小时）：维持AI冻结 + 重发行进命令（防止引擎覆盖）
    /// 3. OnTick（每帧）：距城堡 &lt; EscortPunishDistance(3格) 时执行惩罚；
    ///    CurrentSettlement!=null 为兜底防崩溃路径
    /// 4. ExecutePunishment：★关键顺序★ 先 EndCaptivity + 清空花名册，
    ///    再 MakePeace，避免 SetNeutral 内部触发二次释放导致崩溃
    /// </summary>
    public partial class PoliceEnforcementBehavior : CampaignBehaviorBase
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;
        private static PoliceEnforcementBehavior? _instance;

        // Peaceful player-case contact is attempted at most once per cooldown.
        // Without this guard, a native EngageParty contact can be re-issued every
        // frame while the conversation is closing and reopen the same dialogue.
        private double _nextPlayerEnforcementContactHour = -1d;

        private bool _atonementActive = false;
        private string _atonementTargetPartyId = string.Empty;
        private string _atonementTargetName = string.Empty;
        private string _atonementTargetHeroId = string.Empty;
        private int _atonementTargetCrimeCategory = (int)GwpCrimeCategory.Unknown;
        private int _atonementReputationReward = 0;
        private float _atonementDeadlineHours = 0f;
        private readonly Dictionary<string, Vec2> _shelteredPoliceLastPositionByTaskId =
            new Dictionary<string, Vec2>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _shelteredPoliceStoppedHoursByTaskId =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _ignoredInvalidShelteredBattlePartyIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _shelteredForcedPartyIdsByTaskId =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private const string ShelteredForcedTaskIdsKey =
            "GWPP_ShelteredForcedAttackTaskIds";
        private const string ShelteredForcedPartyIdsKey =
            "GWPP_ShelteredForcedAttackPartyIds";

        public PoliceEnforcementBehavior()
        {
            _instance = this;
        }

        internal static bool TryReservePolicePartyForVillageRelief(MobileParty? police)
        {
            return _instance != null && _instance.TryPreparePolicePartyForVillageRelief(police);
        }

        internal static bool TryReservePartyForPlayerRequest(MobileParty? police)
        {
            return _instance != null && _instance.TryPreparePartyForPlayerRequest(police);
        }

        internal static bool IsPlayerEnforcementApproach(
            MobileParty? police, MobileParty? target)
        {
            if (police?.IsActive != true || target?.IsMainParty != true)
                return false;

            PoliceTask? task = GwpRuntimeState.Crime.GetTask(police.StringId);
            return task?.FlowState == PoliceTaskFlowState.Pursuit &&
                   !task.WarDeclared &&
                   task.TargetCrime?.Offender?.IsMainParty == true;
        }

        internal static void RefreshPlayerBountyCaseContact(
            string policePartyId, bool playerEncounterStarted = false)
        {
            if (_instance == null || string.IsNullOrWhiteSpace(policePartyId))
                return;

            PoliceTask? task = CrimeState.GetTask(policePartyId);
            MobileParty? police = MobileParty.All.FirstOrDefault(party =>
                party.IsActive && string.Equals(party.StringId, policePartyId,
                    StringComparison.OrdinalIgnoreCase));
            if (task?.IsPlayerBountyEscort != true || police == null)
                return;

            _instance.UpdatePlayerBountyEscortCase(
                police, task, playerEncounterStarted);
        }

        private AtonementFlowState CurrentAtonementState =>
            _atonementWaitingForTurnIn
                ? AtonementFlowState.WaitingForTurnIn
                : (_atonementActive ? AtonementFlowState.Active : AtonementFlowState.Inactive);

        private bool HasAtonementTask => CurrentAtonementState != AtonementFlowState.Inactive;
        private bool IsAtonementActiveState => CurrentAtonementState == AtonementFlowState.Active;
        private bool IsAtonementWaitingForTurnInState => CurrentAtonementState == AtonementFlowState.WaitingForTurnIn;

        private void SetAtonementFlowState(AtonementFlowState state)
        {
            _atonementActive = state == AtonementFlowState.Active;
            _atonementWaitingForTurnIn = state == AtonementFlowState.WaitingForTurnIn;
            PlayerState.SetAtonementTaskActive(state != AtonementFlowState.Inactive);
        }

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
            List<string>? shelteredForcedTaskIds = null;
            List<string>? shelteredForcedPartyIds = null;
            if (dataStore.IsSaving)
            {
                shelteredForcedTaskIds = new List<string>();
                shelteredForcedPartyIds = new List<string>();
                foreach (KeyValuePair<string, HashSet<string>> entry in
                         _shelteredForcedPartyIdsByTaskId)
                {
                    foreach (string partyId in entry.Value)
                    {
                        shelteredForcedTaskIds.Add(entry.Key);
                        shelteredForcedPartyIds.Add(partyId);
                    }
                }
            }

            CrimeState.SyncData(dataStore);
            dataStore.SyncData("gwp_enf_war_check_day_counter", ref _warStatusCheckDayCounter);
            dataStore.SyncData("gwp_enf_atone_active", ref _atonementActive);
            dataStore.SyncData("gwp_enf_atone_target_id", ref _atonementTargetPartyId);
            dataStore.SyncData("gwp_enf_atone_target_name", ref _atonementTargetName);
            dataStore.SyncData("gwp_enf_atone_target_hero_id", ref _atonementTargetHeroId);
            dataStore.SyncData("gwp_enf_atone_crime_category", ref _atonementTargetCrimeCategory);
            dataStore.SyncData("gwp_enf_atone_target_faction_id", ref _atonementTargetFactionId);
            dataStore.SyncData("gwp_enf_atone_reward", ref _atonementReputationReward);
            dataStore.SyncData("gwp_enf_atone_deadline_hours", ref _atonementDeadlineHours);
            dataStore.SyncData("gwp_enf_atone_waiting_turnin", ref _atonementWaitingForTurnIn);
            dataStore.SyncData("gwp_enf_atone_target_size", ref _atonementTargetSizeSnapshot);
            dataStore.SyncData("gwp_enf_player_contact_hour",
                ref _nextPlayerEnforcementContactHour);
            SyncWarTargetStreakData(dataStore);
            SyncDelayPatrolStateData(dataStore);
            SyncAssistanceData(dataStore);
            dataStore.SyncData(ShelteredForcedTaskIdsKey,
                ref shelteredForcedTaskIds);
            dataStore.SyncData(ShelteredForcedPartyIdsKey,
                ref shelteredForcedPartyIds);
            if (dataStore.IsLoading)
            {
                if (_warStatusCheckDayCounter < 0 || _warStatusCheckDayCounter > 1)
                    _warStatusCheckDayCounter = 0;
                _shelteredPoliceLastPositionByTaskId.Clear();
                _shelteredPoliceStoppedHoursByTaskId.Clear();
                _ignoredInvalidShelteredBattlePartyIds.Clear();
                _shelteredForcedPartyIdsByTaskId.Clear();
                int forcedCount = Math.Min(
                    shelteredForcedTaskIds?.Count ?? 0,
                    shelteredForcedPartyIds?.Count ?? 0);
                for (int i = 0; i < forcedCount; i++)
                {
                    string taskId = shelteredForcedTaskIds![i];
                    string partyId = shelteredForcedPartyIds![i];
                    if (string.IsNullOrWhiteSpace(taskId) ||
                        string.IsNullOrWhiteSpace(partyId))
                        continue;

                    if (!_shelteredForcedPartyIdsByTaskId.TryGetValue(
                            taskId, out HashSet<string>? partyIds))
                    {
                        partyIds = new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);
                        _shelteredForcedPartyIdsByTaskId[taskId] = partyIds;
                    }

                    partyIds.Add(partyId);
                }
                _enforcementAtonementAssigned = false;
                _atonementQuest = null!;
                _awaitingAtonementQuestReconnect = false;
                _lastAtonementIntelReportTime = CampaignTime.Zero;
                if (double.IsNaN(_nextPlayerEnforcementContactHour) ||
                    double.IsInfinity(_nextPlayerEnforcementContactHour))
                    _nextPlayerEnforcementContactHour = -1d;
                if (!Enum.IsDefined(typeof(GwpCrimeCategory), _atonementTargetCrimeCategory))
                    _atonementTargetCrimeCategory = (int)GwpCrimeCategory.Unknown;
                PlayerState.SetAtonementTaskActive(HasAtonementTask);
            }
        }

        private void UpdateAtonementTask()
        {
            TryReconnectAtonementQuestOnHourlyTick();
            if (!IsAtonementActiveState) return;

            if (CampaignTime.Now.ToHours >= _atonementDeadlineHours)
            {
                FailAtonementTask(GwpText.Get("{=gwp_policeenforcementbehavior_001}The atonement contract times out, reputation -5."));
                return;
            }

            MobileParty target = MobileParty.All.FirstOrDefault(p =>
                p.StringId == _atonementTargetPartyId && p.IsActive);
            if (target == null)
            {
                FailAtonementTask(GwpText.Get("{=gwp_policeenforcementbehavior_002}The atonement target has disappeared, the contract has failed, and the reputation is -5."));
                return;
            }

            // 旧存档兼容：更新前已经接下的赎罪追捕没有保存犯人英雄与犯罪分类。
            // 在案件仍存在时补齐，确保战斗结算采用原案件的商路/乡土分类。
            CrimeRecord? activeCrime = CrimeState.GetByOffenderId(_atonementTargetPartyId);
            if (activeCrime != null)
            {
                if (string.IsNullOrWhiteSpace(_atonementTargetHeroId))
                    _atonementTargetHeroId = activeCrime.OffenderHeroId ?? string.Empty;
                if ((GwpCrimeCategory)_atonementTargetCrimeCategory == GwpCrimeCategory.Unknown)
                    _atonementTargetCrimeCategory = (int)activeCrime.CrimeCategory;
            }

            if ((CampaignTime.Now - _lastAtonementIntelReportTime).ToDays >= GwpTuning.Enforcement.AtonementIntelReportIntervalDays)
            {
                _lastAtonementIntelReportTime = CampaignTime.Now;
                AppendAtonementIntelLog(target);
            }
        }

        private void HandleAtonementMapEventEnded(MapEvent mapEvent)
        {
            if (!IsAtonementActiveState || mapEvent == null) return;

            bool playerInvolved = false;
            bool targetInvolved = false;
            MobileParty? completedTarget = null;
            foreach (var p in mapEvent.InvolvedParties)
            {
                MobileParty? party = p?.MobileParty;
                if (party == null) continue;
                if (party.IsMainParty) playerInvolved = true;
                if (party.StringId == _atonementTargetPartyId)
                {
                    targetInvolved = true;
                    completedTarget = party;
                }
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
                CrimeRecord? completedCrime = CrimeState.GetByOffenderId(_atonementTargetPartyId);
                Hero? completedOffender = completedTarget?.LeaderHero ?? completedCrime?.OffenderHero;
                if (completedOffender == null && !string.IsNullOrWhiteSpace(_atonementTargetHeroId))
                {
                    try
                    {
                        completedOffender = Hero.FindFirst(hero =>
                            string.Equals(hero.StringId, _atonementTargetHeroId,
                                StringComparison.OrdinalIgnoreCase));
                    }
                    catch (ArgumentNullException) { }
                }

                GwpCrimeCategory completedCategory = (GwpCrimeCategory)_atonementTargetCrimeCategory;
                if (completedCategory == GwpCrimeCategory.Unknown)
                    completedCategory = completedCrime?.CrimeCategory ?? GwpCrimeCategory.Unknown;

                Campaign.Current?.GetCampaignBehavior<PoliceAIDeterrenceBehavior>()
                    ?.RegisterPlayerCompletedCase(mapEvent, completedOffender, completedCategory);

                SetAtonementFlowState(AtonementFlowState.WaitingForTurnIn);
                _atonementDeadlineHours = 0f;

                try { _atonementQuest?.MarkReadyForTurnIn(); } catch { }
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_policeenforcementbehavior_003}Atonement quarry defeated: {VAR_1}. Report to the Warden-General or any Grey Warden.", "VAR_1", _atonementTargetName),
                    Colors.Green));
            }
            else
            {
                FailAtonementTask(GwpText.Get("{=gwp_policeenforcementbehavior_004}The atonement contract failed, reputation -5."));
            }
        }

        private void FailAtonementTask(string reason)
        {
            PlayerState.ChangeReputation(-5);
            try { _atonementQuest?.FailQuestWithReason(reason); } catch { }
            InformationManager.DisplayMessage(new InformationMessage(reason, Colors.Red));
            ClearAtonementTaskState();
        }

        private void ClearAtonementTaskState()
        {
            _atonementTargetPartyId = string.Empty;
            _atonementTargetName = string.Empty;
            _atonementTargetHeroId = string.Empty;
            _atonementTargetCrimeCategory = (int)GwpCrimeCategory.Unknown;
            _atonementTargetFactionId = string.Empty;
            _atonementTargetSizeSnapshot = 0;
            _atonementReputationReward = 0;
            _atonementDeadlineHours = 0f;
            _lastAtonementIntelReportTime = CampaignTime.Zero;
            _awaitingAtonementQuestReconnect = false;
            _enforcementAtonementAssigned = false;
            _atonementQuest = null!;
            SetAtonementFlowState(AtonementFlowState.Inactive);
        }

        #region 犯罪通知

        private void HandleCrimeDetected(string crimeType, MobileParty offender, Vec2 location, string victimName)
        {
            CrimeState.TryAdd(crimeType, offender, location, victimName);
        }

        #endregion

        #region 每帧检查 - 距城堡距离触发惩罚

        /// <summary>
        /// A wanted player is intentionally not declared at war on approach: the
        /// player must first receive the fine/atonement/refusal dialogue.  The
        /// normal task intent uses a static GoToPoint during that peaceful phase,
        /// so bridge the final few map units back to the native EngageParty
        /// contact once the assigned Warden arrives.
        /// </summary>
        private void MaintainPlayerEnforcementContact()
        {
            try
            {
                if (CampaignTime.Now.ToHours < _nextPlayerEnforcementContactHour)
                    return;

                MobileParty? player = MobileParty.MainParty;
                if (player?.IsActive != true || player.MapEvent != null ||
                    PlayerEncounter.IsActive ||
                    Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
                    return;

                string? policeId = CrimeState.GetPlayerTaskPolicePartyId();
                if (string.IsNullOrWhiteSpace(policeId))
                    return;

                PoliceTask? task = CrimeState.GetTask(policeId);
                if (task == null || task.WarDeclared || task.IsEscortingPlayer ||
                    task.FlowState != PoliceTaskFlowState.Pursuit ||
                    task.TargetCrime?.Offender?.IsMainParty != true)
                    return;

                Clan? policeClan = PoliceStats.GetPoliceClan();
                MobileParty? police = MobileParty.All.FirstOrDefault(party =>
                    party.IsActive && string.Equals(party.StringId, policeId,
                        StringComparison.OrdinalIgnoreCase));
                if (police == null || police.MapEvent != null ||
                    police.ActualClan != policeClan)
                    return;

                float distance = police.GetPosition2D.Distance(player.GetPosition2D);
                if (distance > GwpTuning.Enforcement.WarDistance)
                    return;

                // Reserve the cooldown before changing native AI state.  The
                // command can synchronously raise encounter callbacks, and the
                // reservation must already exist if that happens.
                double now = CampaignTime.Now.ToHours;
                _nextPlayerEnforcementContactHour = now +
                    GwpTuning.PlayerRequests.DeferredContactHours;

                GreyWardenPartyDesireBehavior.ClearIntent(police);
                police.Ai.SetDoNotMakeNewDecisions(false);
                police.SetMoveEngageParty(player, police.NavigationCapability);

                GwpAiDiagnostics.WriteAction(police,
                    "PLAYER_ENFORCEMENT_CONTACT_REQUESTED",
                    "distance=" + distance.ToString("0.00", CultureInfo.InvariantCulture) +
                    "; retryAfterHour=" + _nextPlayerEnforcementContactHour);
            }
            catch (Exception exception)
            {
                // A transient native map state must not turn into a per-frame
                // retry loop.  Retry on the next campaign hour instead.
                _nextPlayerEnforcementContactHour = CampaignTime.Now.ToHours + 1d;
                GwpAiDiagnostics.WritePlayerJusticeState(
                    "PLAYER_ENFORCEMENT_CONTACT_FAILED",
                    "retryAfterHour=" + _nextPlayerEnforcementContactHour +
                    "; error=" + exception.GetType().Name);
            }
        }

        private void OnTick(float dt)
        {
            try
            {
                MaintainPlayerEnforcementContact();
                MaintainShelteredCaseForcedAttacks();

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
                var escortTask = CrimeState.ActiveTasks.Values.FirstOrDefault(t =>
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

                Settlement? castle = escortTask.EscortSettlement;
                if (castle == null) return;

                // 正常触发路径：警察通过混合寻路接近城堡，距离 < EscortPunishDistance 时执行惩罚。
                // 近距离段用 GatePosition 直线导航，不触发自动入城，确保此距离判断先于入城发生。
                float distToCastle = policeParty.GetPosition2D.Distance(castle.GetPosition2D);
                if (distToCastle < GwpTuning.Enforcement.EscortPunishDistance)
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
        private void ExecutePunishment(Settlement? castle, PoliceTask escortTask)
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
                    Settlement? teleportTarget = castle ?? escortTask.EscortSettlement ?? FindNearestCastle();
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
                int rep = PlayerState.Reputation;
                int fine = Math.Abs(rep) * 300;
                int collected = PoliceResourceManager.CollectFineGoldOnly(fine);
                int recovered = 300 > 0 ? collected / 300 : 0;
                int repAfter = Math.Min(0, rep + recovered);

                // 步骤5：声望按实缴比例恢复（不再直接归零）
                PlayerState.ResetReputation(repAfter);
                if (repAfter > -11 || PlayerState.HasAtonementTask)
                {
                    CrimeState.EndPlayerHunt();
                }
                else
                {
                    CrimeState.EndTask(escortTask.PolicePartyId);
                    CrimeState.TryAddPlayerCrime(GwpText.Get("{=gwp_policeenforcementbehavior_005}Insufficient fine"), MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero, GwpText.Get("{=gwp_policeenforcementbehavior_006}Escort fine unpaid"));
                }

                // 步骤6：显示消息
                string castleName = castle?.Name?.ToString() ?? GwpText.Get("{=gwp_policeenforcementbehavior_007}Fortress");
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_policeenforcementbehavior_008}You are delivered to {VAR_1}: {VAR_2} denars due, {VAR_3} paid; standing restored to {VAR_4} in proportion to payment.", "VAR_1", castleName, "VAR_2", fine, "VAR_3", collected, "VAR_4", repAfter),
                    Colors.Yellow));

                // 步骤7：恢复警察AI，开始补给
                //（此时花名册已清空，进城补给时 ReleasePrisoners 不会遇到玩家俘虏）
                if (policeParty != null && policeParty.IsActive)
                {
                    RestoreAi(policeParty);
                    GreyWardenPartyDesireBehavior.ClearIntent(policeParty);
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(policeParty);
                }

                // 步骤8：安全调用 EndTask（EndPlayerHunt 已移除任务，此处幂等）
                CrimeState.EndTask(escortTask.PolicePartyId);
            }
            catch { }
        }

        #endregion

        #region 每小时

        private void OnHourlyTick()
        {
            UpdateAtonementTask();
            EnsureDelayPatrolStateForActiveParties();
            ReconcileTaskWarStatesWithDiplomacy();
            UpdateDelayPatrols();
            BreakInvalidShelteredBattles();
            CloseSettledPlayerHunt();
            CrimeState.Clean();
            AssignTasks();
            UpdateLordAssistance();
            UpdateTasks();
            UpdateIdlePoliceDuties();
            CrimeState.RefreshAccepting();
            GwpAiDiagnostics.RefreshObservedPartyCache();
        }

        /// <summary>
        /// Retires a pursuit of the player once the player no longer owes
        /// anything, so a settled debt cannot leave a warrant standing.
        ///
        /// The lawful fine is derived from standing, so at a standing of zero
        /// or better it is zero.  A task that outlived the payment therefore
        /// used to stop the player at every Grey Warden they met and demand
        /// nothing, and paying nothing settled nothing - the reported "paid the
        /// fine, still wanted, fine is 0, no number of payments helps".  Once
        /// there is nothing left to collect the pursuit has no subject, so it
        /// is closed here rather than being offered again.
        /// </summary>
        private void CloseSettledPlayerHunt()
        {
            if (PlayerState.Reputation < 0)
                return;

            if (CrimeState.GetPlayerCrime() == null
                && CrimeState.GetPlayerTaskPolicePartyId() == null)
            {
                return;
            }

            GwpAiDiagnostics.WritePlayerJusticeState(
                "PLAYER_HUNT_SETTLED_CLOSED",
                "reputation=" + PlayerState.Reputation +
                "; taskParty=" + (CrimeState.GetPlayerTaskPolicePartyId() ?? "-"));
            CrimeState.EndPlayerHunt();
        }

        private void AssignTasks()
        {
            if (!CrimeState.IsDispatchReady)
                return;

            List<MobileParty> available = PoliceStats.GetAllPoliceParties()
                .Where(CanAssignOrdinaryCaseNow)
                .OrderBy(party => party.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // First pass preserves each office's own priority. The second pass lets every
            // idle office holder help with any ordinary case already admitted to the pool.
            foreach (MobileParty pp in available.ToList())
            {
                if (!CrimeState.IsDispatchReady)
                    break;

                GwpCrimeCategory preferred = GetPreferredCrimeCategory(pp);
                if (preferred == GwpCrimeCategory.Unknown)
                    continue;

                CrimeRecord? crime = CrimeState.GetNearest(pp.GetPosition2D,
                    candidate => candidate.CrimeCategory == preferred);
                if (crime == null) continue;
                BeginTask(pp, crime);
                available.Remove(pp);
            }

            foreach (MobileParty pp in available)
            {
                if (!CrimeState.IsDispatchReady)
                    break;

                CrimeRecord? crime = CrimeState.GetNearest(pp.GetPosition2D);
                if (crime != null)
                    BeginTask(pp, crime);
            }
        }

        private bool CanAssignOrdinaryCaseNow(MobileParty pp)
        {
            if (!PoliceStats.CanHandleOrdinaryCase(pp)) return false;
            if (GwpCommon.IsEnforcementDelayPatrolParty(pp)) return false;
            if (GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(pp)) return false;
            if (GreyWardenVillageReconstructionBehavior.ShouldReserveFromOrdinaryCases(pp)) return false;
            if (GreyWardenIssueResolutionBehavior.ShouldReserveFromOrdinaryCases(pp)) return false;
            if (GreyWardenTrainingBehavior.ShouldReserveFromNewDuties(pp)) return false;
            if (GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest(pp))
                return false;
            if (GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder(pp))
                return false;
            if (IsAssistanceOccupied(pp)) return false;
            if (CrimeState.HasTask(pp.StringId)) return false;
            return PoliceResourceManager.IsReady(pp);
        }

        private static GwpCrimeCategory GetPreferredCrimeCategory(MobileParty party)
        {
            if (!GreyWardenFamilyBehavior.TryGetDuty(party?.LeaderHero,
                    out GreyWardenFamilyBehavior.DutyKind duty))
                return GwpCrimeCategory.Unknown;

            return duty switch
            {
                GreyWardenFamilyBehavior.DutyKind.CaravanProtection => GwpCrimeCategory.CaravanAttack,
                GreyWardenFamilyBehavior.DutyKind.VillageProtection => GwpCrimeCategory.VillageViolence,
                _ => GwpCrimeCategory.Unknown
            };
        }

        private void ReconcileTaskWarStatesWithDiplomacy()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values.ToList())
            {
                if (!task.WarDeclared || task.WarTarget == null)
                    continue;

                MobileParty? offender = task.TargetCrime?.Offender;
                IFaction? currentTarget = offender?.IsMainParty == true
                    ? Clan.PlayerClan?.MapFaction
                    : offender?.ActualClan?.MapFaction ?? offender?.MapFaction;
                if (currentTarget != null &&
                    FactionManager.IsAtWarAgainstFaction(policeClan, currentTarget))
                    continue;

                task.WarDeclared = false;
                task.WarTarget = null;
                ClearTaskWarTracking(task.PolicePartyId, true);

                MobileParty? police = MobileParty.All.FirstOrDefault(candidate =>
                    candidate.IsActive && string.Equals(candidate.StringId,
                        task.PolicePartyId, StringComparison.OrdinalIgnoreCase));
                if (police != null)
                {
                    GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
                    GwpAiDiagnostics.WriteAction(police,
                        "CASE_WAR_STATE_RESET",
                        "currentTarget=" + (currentTarget?.StringId ?? "-") +
                        "; reason=actual_diplomacy_is_peace");
                }
            }
        }

        private void BeginTask(MobileParty police, CrimeRecord crime)
        {
            CrimeState.BeginTask(police.StringId, crime);
            PoliceTask? task = CrimeState.GetTask(police.StringId);
            if (task != null)
            {
                task.IsPreparingDispatch = false;
                TryCaptureAssistanceLeaderSoloSpeed(police, task);
            }

            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);

            // 内部调度日志（开发调试）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP 出警] {police.Name} → {crime.Offender.Name}（{crime.CrimeType}）",
            //     Colors.Cyan));
        }

        private void UpdateTasks()
        {
            foreach (var kvp in CrimeState.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                // 只要承办人已经不能继续作为灰袍领主带兵，案件就立即失败。
                // 协力军团必须在删除任务前同步解散，不能留下无首领 Army
                // 等到下一轮小时清理。
                if (!CanContinueLeadingPoliceTask(pp))
                {
                    FailTaskBecauseOwnerCannotLead(kvp.Key, pp,
                        "hourly_owner_cannot_lead");
                    continue;
                }

                // 玩家负责带路，灰袍仍负责案件宣战和高速追截队。这里不进入
                // 普通自主追捕，但不能再跳过整个案件战争状态机。
                if (task.FlowState == PoliceTaskFlowState.PlayerBountyEscort)
                {
                    UpdatePlayerBountyEscortCase(pp!, task, false);
                    continue;
                }

                // 押送阶段：目的地作为高优先级欲望参与原版 AI 拍卖。
                if (task.FlowState == PoliceTaskFlowState.EscortingPlayer)
                {
                    ClearTaskWarTracking(kvp.Key, true);

                    // 安全网：若玩家已被外部机制提前释放（例如某段和平逻辑绕过了守卫），
                    // 仍执行惩罚以确保罚款和声望清零。
                    // ExecutePunishment 内部先检查 IsCaptive 再调用 EndCaptivity，安全。
                    if (!PlayerCaptivity.IsCaptive)
                    {
                        ExecutePunishment(task.EscortSettlement, task);
                        continue;
                    }

                    continue;
                }

                if (task.FlowState == PoliceTaskFlowState.PreparingDispatch)
                {
                    ClearTaskWarTracking(kvp.Key, false);

                    if (!task.IsTargetValid())
                    {
                        RestoreAi(pp);
                        CrimeState.EndTask(kvp.Key);
                        RestorePeaceAfterCaseEnd(task);
                        continue;
                    }

                    task.IsPreparingDispatch = false;
                }

                // 资源不足时保留案件；欲望层会暂时让城镇补给压过追捕。
                if (!task.IsTargetValid())
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    RestorePeaceAfterCaseEnd(task);
                    continue;
                }

                // 正常追击
                MobileParty? criminal = task.TargetCrime?.Offender;
                if (criminal == null)
                {
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    CrimeState.RefreshAccepting();
                    RestorePeaceAfterCaseEnd(task);
                    continue;
                }
                bool isShelteredTarget =
                    !criminal.IsMainParty &&
                    criminal.CurrentSettlement != null;
                if (!isShelteredTarget)
                {
                    ClearShelteredTargetTracking(kvp.Key);
                }

                if (!criminal.IsActive)
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    RestorePeaceAfterCaseEnd(task);
                    continue;
                }

                MobileParty assistanceContact = pp;
                MobileParty movementTarget =
                    ResolveAssistanceMovementTarget(criminal);
                float dist = pp.GetPosition2D.Distance(
                    movementTarget.GetPosition2D);
                if (!criminal.IsMainParty &&
                    _assistanceGroups.ContainsKey(pp.StringId))
                {
                    dist = GetAssistanceContactDistance(
                        pp, criminal, out assistanceContact);
                }

                float warDist = criminal.IsMainParty
                    ? GwpTuning.Enforcement.PlayerWarDistance
                    : GwpTuning.Enforcement.WarDistance;
                if (!criminal.IsMainParty &&
                    _assistanceGroups.ContainsKey(pp.StringId))
                {
                    warDist = Math.Max(
                        warDist, GetNativeMaximumGoAroundDistance());
                }

                bool isPatrolRange = criminal.IsMainParty &&
                    PlayerState.Reputation >= -4 &&
                    PlayerState.Reputation <= -1;
                bool assistanceReady = true;
                float assistanceEngagementStrength =
                    GetNativePartyStrength(pp);
                float assistanceTargetStrength = 0f;
                if (_assistanceGroups.ContainsKey(pp.StringId))
                {
                    assistanceReady =
                        HasAssistanceEngagementStrengthAdvantage(
                            pp, criminal,
                            out assistanceEngagementStrength,
                            out assistanceTargetStrength);
                }

                bool nativeDeclarationReady = false;
                LocalStrengthDeclarationSnapshot? declarationPrediction = null;
                if (assistanceReady && !isPatrolRange &&
                    !criminal.IsMainParty && !task.WarDeclared)
                {
                    nativeDeclarationReady =
                        TryGetNativeDeclarationCandidate(
                            pp, criminal, warDist,
                            out assistanceContact,
                            out declarationPrediction);
                    dist = declarationPrediction.Distance;
                    if (!nativeDeclarationReady && dist <= warDist)
                    {
                        GwpAiDiagnostics.WriteAction(pp,
                            "ASSISTANCE_DECLARATION_WAITING_LOCAL_STRENGTH",
                            FormatLocalStrengthDeclarationDiagnostic(
                                declarationPrediction,
                                assistanceEngagementStrength,
                                assistanceTargetStrength));
                    }
                }

                // 玩家目标不自动宣战——改由对话系统让玩家选择缴纳或战斗。
                // 只有玩家在对话中选择"战斗"后（OnEnforcementFightConsequence）才宣战。
                // 非玩家目标只有在我方实际区域战力严格高于敌方
                // 实际区域战力时才宣战；宣战后完全交还原版短期欲望。
                if (!task.WarDeclared && dist <= warDist &&
                    !isPatrolRange && !criminal.IsMainParty &&
                    assistanceReady && nativeDeclarationReady)
                {
                    if (assistanceContact != pp ||
                        _assistanceGroups.ContainsKey(pp.StringId))
                    {
                        GwpAiDiagnostics.WriteAction(pp,
                            "ASSISTANCE_DECLARATION_LOCAL_STRENGTH_READY",
                            FormatLocalStrengthDeclarationDiagnostic(
                                declarationPrediction!,
                                assistanceEngagementStrength,
                                assistanceTargetStrength));
                    }
                    DeclareWar(task, criminal);
                    RefreshAssistanceDutyAfterWarDeclaration(pp);
                    if (task.FlowState == PoliceTaskFlowState.WarPursuit)
                    {
                        TrySpawnImmediateCaseInterceptor(pp, task, criminal,
                            declarationPrediction!);
                    }
                }
                else if (!task.WarDeclared)
                {
                    ClearTaskWarTracking(kvp.Key, false);
                }

                // 藏城只改变宣战后的驱逐执行时机，不能绕过或截断上面的
                // 两层战力判定。未满足战力条件时继续围堵；本案正式宣战后
                // 才复用既有拉出定居点、禁止回城和攻击主理人的流程。
                if (isShelteredTarget &&
                    HandleShelteredCriminal(pp, task, kvp.Key, criminal))
                {
                    continue;
                }

                // 协力军团无论仍在集结追击，还是已经因目标速度而分散，
                // 都可以由主理人真实分出一支能追上目标的骑兵截击队先
                // 建立地图战斗；同案同时只保留一支，既有截击队会由其
                // 生命周期继续维护。
                if (task.FlowState == PoliceTaskFlowState.WarPursuit &&
                    _assistanceGroups.TryGetValue(pp.StringId,
                        out LordAssistanceGroup? assistanceGroup))
                {
                    TrySpawnImmediateCaseInterceptor(
                        pp, task, criminal, null);
                }

                // 不下达追击命令，也不每小时强迫重新决策；案件保底欲望会在
                // 原版正常思考周期中持续存在，并与原版补给/恢复欲望竞争。
            }
        }

        private void UpdatePlayerBountyEscortCase(MobileParty police,
            PoliceTask task, bool playerEncounterStarted)
        {
            MobileParty? player = MobileParty.MainParty;
            MobileParty? criminal = task.TargetCrime?.Offender;
            if (player?.IsActive != true || criminal?.IsActive != true ||
                criminal.IsMainParty ||
                !string.Equals(task.PolicePartyId, police.StringId,
                    StringComparison.OrdinalIgnoreCase))
                return;

            MobileParty movementTarget =
                ResolveAssistanceMovementTarget(criminal);
            float playerDistance = player.GetPosition2D.Distance(
                movementTarget.GetPosition2D);
            float declarationDistance = Math.Max(
                GwpTuning.Enforcement.WarDistance,
                GetNativeMaximumGoAroundDistance());

            if (!task.WarDeclared &&
                (playerEncounterStarted || playerDistance <= declarationDistance))
            {
                float committedWardenStrength =
                    GetNativePartyStrength(police);
                if (_assistanceGroups.TryGetValue(police.StringId,
                        out LordAssistanceGroup? group))
                {
                    committedWardenStrength =
                        GetCommittedAssistanceStrength(police, group);
                }

                AssistanceThreatSnapshot enemy =
                    GetNativeCombatStrengthSnapshot(police, criminal);
                DeclareWar(task, criminal);
                GwpAiDiagnostics.WriteAction(police,
                    "PLAYER_BOUNTY_CONTACT_DECLARING_WAR",
                    "trigger=" + (playerEncounterStarted
                        ? "player_map_event"
                        : "player_proximity") +
                    "; player=" + player.StringId +
                    "; target=" + criminal.StringId +
                    "; movementTarget=" + movementTarget.StringId +
                    "; playerDistance=" + playerDistance.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; declarationDistance=" + declarationDistance.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; playerStrength=" +
                        GetNativeCombatGroupStrength(player).ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                    "; committedWardenStrength=" +
                        committedWardenStrength.ToString(
                            "0.00", CultureInfo.InvariantCulture) +
                    "; enemyLocalStrength=" + enemy.Strength.ToString(
                        "0.00", CultureInfo.InvariantCulture) +
                    "; strengthGateIgnored=True");
                RefreshAssistanceDutyAfterWarDeclaration(police);
            }

            if (task.WarDeclared)
            {
                TrySpawnImmediateCaseInterceptor(
                    police, task, criminal, null);
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
            GwpAiDiagnostics.WriteMapEvent(mapEvent, "ENDED");
            HandleTaskOwnerMapEventEnded(mapEvent);
            HandleDelayPatrolBattleEnded(mapEvent);
            HandleAtonementMapEventEnded(mapEvent);
            if (!mapEvent.IsFieldBattle) return;

            foreach (var kvp in CrimeState.ActiveTasks.ToList())
            {
                var task = kvp.Value;
                if (!task.WarDeclared) continue;

                var pp = MobileParty.All.FirstOrDefault(p => p.StringId == task.PolicePartyId);

                if (pp == null)
                {
                    MobileParty? offender = task.TargetCrime?.Offender;
                    if (offender == null || !InEvent(offender, mapEvent)) continue;
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    CrimeState.RefreshAccepting();
                    RestorePeaceAfterCaseEnd(task);
                    continue;
                }

                if (!InEvent(pp, mapEvent)) continue;
                if (_ignoredInvalidShelteredBattlePartyIds.Remove(pp.StringId))
                    continue;
                MobileParty? activeOffender = task.TargetCrime?.Offender;
                bool playerOffender = task.TargetCrime?.Offender?.IsMainParty == true;
                // 承办警察打赢一场无关战斗不能让手中的案件自动结案。目标可能在
                // 战败结算时已经失活或失去 PartyBelongedTo，所以除了实时引用，
                // 还要用案卷保存的部队/英雄 ID 在本场参战方中核验。
                if (!WasTaskOffenderInEvent(task, mapEvent)) continue;

                bool policeWon = IsOnWinningSide(pp, mapEvent);

                if (policeWon)
                {
                    // 敌方撤退或一次非决定性交锋不等于案件完成。目标仍有可战人员
                    // 时保留案件和战争理由，领主下一轮仍按既有 0.99 欲望继续追踪。
                    if (!WasTaskOffenderActuallyDefeatedInEvent(task, mapEvent))
                        continue;

                    // 只有承办灰袍与案件目标同场且灰袍位于胜方时才发办案经费。
                    // 玩家案件随后进入押送流程，但本场胜利只会在这里计发一次。
                    PoliceResourceManager.CreditSuccessfulCaseCompletion();

                    // ★关键修复★：不能用 CrimePool.IsPlayerCrime() 判断——
                    // 玩家被击败后 MainParty.IsActive == false，
                    // IsPlayerCrime 内部调用 IsOffenderValid() → Offender.IsActive → false，
                    // 导致误判为非玩家犯罪，走错路径（StartResupply → 进城补给 → 崩溃）。
                    // 改用 Offender.IsMainParty 直接判断，不依赖 IsActive。
                    if (playerOffender)
                    {
                        CompleteAssistanceTasks(pp.StringId);

                        // 玩家被击败 → 押送至最近城堡（IsCastle）→ OnTick 距离触发惩罚
                        task.IsEscortingPlayer = true;

                        Settlement? targetCastle = FindNearestCastle();
                        task.EscortSettlement = targetCastle;

                        GreyWardenPartyDesireBehavior.RequestImmediateRethink(pp);

                        string castleName = targetCastle?.Name?.ToString() ?? GwpText.Get("{=gwp_policeenforcementbehavior_009}FORT");
                        InformationManager.DisplayMessage(new InformationMessage(
                            GwpText.Get("{=gwp_policeenforcementbehavior_010}You were defeated by {VAR_1}! Being escorted to {VAR_2}...", "VAR_1", pp.Name, "VAR_2", castleName),
                            Colors.Yellow));

                        continue;
                    }

                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    RestorePeaceAfterCaseEnd(task);
                    GwpPlayerRequestDeferral.NotifyDutyCompleted(pp,
                        "criminal_case");
                    CompleteAssistanceTasks(pp.StringId);
                }
                else
                {
                    RestoreAi(pp);
                    ClearTaskWarTracking(kvp.Key, true);
                    CrimeState.EndTask(kvp.Key);
                    RestorePeaceAfterCaseEnd(task);
                    ReleaseAssistanceGroup(pp.StringId, "case_leader_defeated");
                }

                CrimeState.RefreshAccepting();
            }
        }

        #endregion

        private static bool WasTaskOffenderInEvent(PoliceTask task, MapEvent mapEvent)
        {
            CrimeRecord? crime = task?.TargetCrime;
            if (crime == null || mapEvent == null) return false;

            MobileParty? resolved = crime.Offender;
            foreach (PartyBase? entry in mapEvent.InvolvedParties)
            {
                MobileParty? involved = entry?.MobileParty;
                if (involved == null) continue;
                if (involved == resolved) return true;
                if (crime.CrimeId == CrimePool.PlayerCrimeId && involved.IsMainParty)
                    return true;
                if (!string.IsNullOrWhiteSpace(crime.OffenderPartyId) &&
                    string.Equals(involved.StringId, crime.OffenderPartyId,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!string.IsNullOrWhiteSpace(crime.OffenderHeroId) &&
                    string.Equals(involved.LeaderHero?.StringId, crime.OffenderHeroId,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool WasTaskOffenderActuallyDefeatedInEvent(
            PoliceTask task, MapEvent mapEvent)
        {
            CrimeRecord? crime = task?.TargetCrime;
            if (crime == null || mapEvent == null) return false;

            foreach (PartyBase? entry in mapEvent.InvolvedParties)
            {
                MobileParty? involved = entry?.MobileParty;
                if (involved == null) continue;

                bool matches = involved == crime.Offender ||
                    (crime.CrimeId == CrimePool.PlayerCrimeId && involved.IsMainParty) ||
                    (!string.IsNullOrWhiteSpace(crime.OffenderPartyId) &&
                     string.Equals(involved.StringId, crime.OffenderPartyId,
                         StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(crime.OffenderHeroId) &&
                     string.Equals(involved.LeaderHero?.StringId, crime.OffenderHeroId,
                         StringComparison.OrdinalIgnoreCase));

                if (!matches) continue;
                return involved.IsActive != true || involved.Party == null ||
                       involved.Party.NumberOfHealthyMembers <= 0;
            }

            return false;
        }
    }
}
