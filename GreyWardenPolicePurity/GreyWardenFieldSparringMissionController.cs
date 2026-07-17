using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Conversation.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed class GreyWardenFieldSparringMissionController : MissionLogic
    {
        private enum BoutPhase
        {
            NativeSpawning,
            PreparingFormations,
            Marching,
            OpponentAdvancing,
            AwaitingApproach,
            Conversation,
            PreparingDuel,
            Fighting,
            VictoryPending,
            Finished,
            Aborted
        }

        private enum DuelStyle
        {
            Mounted,
            Foot
        }

        private enum MountedLoanStage
        {
            None,
            Delivering,
            Dismounting,
            Returning,
            WaitingForPlayer
        }

        private sealed class FormationTarget
        {
            internal Formation Formation { get; }
            internal Team Team { get; }
            internal WorldPosition Position { get; set; }
            internal Vec2 Facing { get; }

            internal FormationTarget(
                Formation formation,
                Team team,
                WorldPosition position,
                Vec2 facing)
            {
                Formation = formation;
                Team = team;
                Position = position;
                Facing = facing;
            }
        }

        private const float FormationPreparationSeconds = 0.75f;
        private const float MinimumNativeDeploymentSeparation = 50f;
        private const float DesiredFrontGap = 100f;
        private const float MinimumAcceptableFrontGap = 90f;
        private const float MaximumAcceptableFrontGap = 110f;
        private const float DuelArenaLineClearance = 4f;
        private const float DuelArenaHalfWidth = 100f;
        private const float FormationLateralGap = 2f;
        private const float FormationArrivalTolerance = 5f;
        private const float FormationVelocityToleranceSquared = 0.25f;
        private const float FormationStableSeconds = 1f;
        private const float MarchTimeoutSeconds = 150f;
        private const float ConversationInteractionDistance =
            Agent.MaxInteractionDistance;
        private const float OpponentArrivalTolerance = 2f;
        private const float OpponentStoppedNearTolerance = 5f;
        private const float LoanCourierStoppedNearTolerance = 6f;
        private const float OpponentStableSeconds = 0.5f;
        private const float OpponentAdvanceTimeoutSeconds = 15f;
        private const float VictoryTransitionDelay = 0.35f;
        private const float VictoryReactionLeadSeconds = 1.5f;
        private const float FootDismountGraceSeconds = 20f;
        private const float NativeDismountTimeoutSeconds = 10f;
        private const float SafetyRefreshInterval = 0.4f;
        private const float FormationOrderRefreshInterval = 0.4f;
        private const float GapCorrectionInterval = 1f;

        private readonly Hero _opponent;
        private readonly List<FormationTarget> _formationTargets = new();
        private readonly List<Agent> _spectators = new();
        private readonly Dictionary<Agent, Team> _spectatorOriginalTeams = new();
        private readonly Dictionary<Agent, Formation?>
            _spectatorOriginalFormations = new();
        private readonly Dictionary<Agent, WorldPosition>
            _spectatorHoldPositions = new();

        private DefaultBattleMissionAgentSpawnLogic? _spawnLogic;
        private AgentVictoryLogic? _victoryLogic;
        private Agent? _playerAgent;
        private Agent? _opponentAgent;
        private BoutPhase _phase = BoutPhase.NativeSpawning;
        private Vec2 _battleAxis = Vec2.Forward;
        private Vec2 _meetingCenter = Vec2.Zero;
        private WorldPosition _opponentMeetingPosition = WorldPosition.Invalid;
        private WorldPosition _opponentMountHoldPosition = WorldPosition.Invalid;
        private float _phaseStartedAt;
        private float _formationsStableSince = -1f;
        private float _nextSafetyRefresh;
        private float _nextFormationOrderRefresh;
        private float _nextGapCorrection;
        private float _opponentStableSince = -1f;
        private bool _abortMission;
        private bool _canLeave;
        private BattleSideEnum _winnerSide = BattleSideEnum.None;
        private bool _playerWon;
        private bool _victoryReactionStarted;
        private bool _interactionAvailableLogged;
        private bool _opponentAdvanceTimeoutLogged;
        private bool _opponentAdvanceOrderIssued;
        private DuelStyle _duelStyle = DuelStyle.Mounted;
        private Agent? _opponentDuelMount;
        private Agent? _loanCourier;
        private Agent? _loanMount;
        private Formation? _temporaryDismountFormation;
        private WorldPosition _loanDeliveryPosition = WorldPosition.Invalid;
        private WorldPosition _loanCourierReturnPosition = WorldPosition.Invalid;
        private MountedLoanStage _mountedLoanStage;
        private float _footDismountDeadline;
        private bool _playerFootDismountConfirmed;
        private bool _opponentNativeDismountStarted;
        private bool _opponentDismountActionLogged;
        private float _opponentNativeDismountStartedAt;
        private AgentControllerType _opponentControllerBeforeDismount;
        private bool _loanNativeDismountStarted;
        private bool _loanDismountActionLogged;
        private float _loanNativeDismountStartedAt;
        private AgentControllerType _loanControllerBeforeDismount;
        private bool _ruleViolationLoss;
        private float _victoryReactionStartedAt;

        internal Hero Opponent => _opponent;

        internal bool CanStartMountedDuel =>
            _opponentAgent?.MountAgent?.IsActive() == true
            && (_playerAgent?.MountAgent?.IsActive() == true
                || (FindLoanCourier() != null
                    && FindEmptyOpponentFormation() != null));

        internal bool IsStartConversationActive =>
            _phase == BoutPhase.AwaitingApproach
            || _phase == BoutPhase.Conversation;

        internal GreyWardenFieldSparringMissionController(Hero opponent)
        {
            _opponent = opponent;
        }

        internal static void StartMountedDuel()
        {
            Mission.Current?
                .GetMissionBehavior<GreyWardenFieldSparringMissionController>()?
                .StartDuelInternal(DuelStyle.Mounted);
        }

        internal static void StartFootDuel()
        {
            Mission.Current?
                .GetMissionBehavior<GreyWardenFieldSparringMissionController>()?
                .StartDuelInternal(DuelStyle.Foot);
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _spawnLogic = Mission.GetMissionBehavior<
                DefaultBattleMissionAgentSpawnLogic>();
            _victoryLogic = Mission.GetMissionBehavior<AgentVictoryLogic>();
            Mission.GetMissionBehavior<MissionConversationLogic>()?
                .DisableStartConversation(isDisabled: true);
        }

        public override void AfterStart()
        {
            base.AfterStart();
            try
            {
                if (_spawnLogic == null)
                    throw new InvalidOperationException(
                        "native battle spawn logic is missing");

                // CustomBattleMissionSpawnHandler owns native initialization.
                // This controller only disables later reinforcement waves and
                // keeps the two native teams at peace until the duel begins.
                _spawnLogic.SetReinforcementsSpawnEnabled(false);
                Mission.FocusableObjectInformationProvider.AddInfoCallback(
                    GetFocusableObjectInteractionInfoTexts);
                SetTeamsHostile(isHostile: false);
                _phase = BoutPhase.NativeSpawning;
                _phaseStartedAt = Mission.CurrentTime;
                Debug.Print(
                    "[GreyWarden Sparring] waiting for native field "
                    + "deployment to finish");
            }
            catch (Exception exception)
            {
                AbortMission("initialize native field battle", exception);
            }
        }

        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);
            if (!agent.IsHuman)
                return;

            BasicCharacterObject character =
                agent.Origin?.Troop ?? agent.Character;
            if (ReferenceEquals(
                    character,
                    CharacterObject.PlayerCharacter))
            {
                _playerAgent = agent;
            }
            else if (ReferenceEquals(
                         character,
                         _opponent.CharacterObject))
            {
                _opponentAgent = agent;
            }

            MakeAgentSafeForStaging(agent);
        }

        public override void OnAgentInteraction(
            Agent userAgent,
            Agent agent,
            sbyte agentBoneIndex)
        {
            base.OnAgentInteraction(userAgent, agent, agentBoneIndex);
            if (!IsThereAgentAction(userAgent, agent))
                return;

            _phase = BoutPhase.Conversation;
            MissionConversationLogic? conversationLogic = Mission
                .GetMissionBehavior<MissionConversationLogic>();
            if (conversationLogic == null)
                throw new InvalidOperationException(
                    "field sparring mission has no conversation logic");

            _opponentAgent!.IsLookDirectionLocked = false;
            conversationLogic.DisableStartConversation(isDisabled: false);
            conversationLogic.StartConversation(
                _opponentAgent,
                setActionsInstantly: false);
            Debug.Print(
                "[GreyWarden Sparring] player interaction opened centre "
                + "conversation");
        }

        public override bool IsThereAgentAction(
            Agent userAgent,
            Agent otherAgent)
        {
            return IsLoanHorseInteraction(userAgent, otherAgent)
                || (_phase == BoutPhase.AwaitingApproach
                && userAgent == _playerAgent
                && otherAgent == _opponentAgent
                && _playerAgent?.IsActive() == true
                && _opponentAgent?.IsActive() == true
                && _playerAgent.GetDistanceTo(_opponentAgent)
                    <= ConversationInteractionDistance
                && !Campaign.Current.ConversationManager
                    .IsConversationInProgress);
        }

        private bool IsLoanHorseInteraction(
            Agent userAgent,
            Agent otherAgent)
        {
            Agent? player = _playerAgent;
            Agent? loanMount = _loanMount;
            return _phase == BoutPhase.PreparingDuel
                && _duelStyle == DuelStyle.Mounted
                && (_mountedLoanStage == MountedLoanStage.Returning
                    || _mountedLoanStage
                        == MountedLoanStage.WaitingForPlayer)
                && player != null
                && loanMount != null
                && userAgent == player
                && otherAgent == loanMount
                && player.MountAgent == null
                && loanMount.RiderAgent == null
                && loanMount.IsActive()
                && player.GetDistanceTo(loanMount)
                    <= Agent.MaxMountInteractionDistance;
        }

        private void GetFocusableObjectInteractionInfoTexts(
            Agent requesterAgent,
            IFocusable focusableObject,
            bool isInteractable,
            out FocusableObjectInformation focusableObjectInformation)
        {
            focusableObjectInformation = default;
            if (_phase != BoutPhase.AwaitingApproach
                || requesterAgent != _playerAgent
                || focusableObject is not Agent focusedAgent
                || focusedAgent != _opponentAgent)
            {
                focusableObjectInformation.IsActive = false;
                return;
            }

            focusableObjectInformation.PrimaryInteractionText =
                _opponent.Name;
            if (isInteractable)
            {
                MBTextManager.SetTextVariable(
                    "USE_KEY",
                    HyperlinkTexts.GetKeyHyperlinkText(
                        HotKeyManager.GetHotKeyId(
                            "CombatHotKeyCategory",
                            13),
                        1f),
                    false);
                focusableObjectInformation.SecondaryInteractionText =
                    GameTexts.FindText("str_key_action");
                focusableObjectInformation.SecondaryInteractionText
                    .SetTextVariable(
                        "KEY",
                        GameTexts.FindText("str_ui_agent_interaction_use"));
                focusableObjectInformation.SecondaryInteractionText
                    .SetTextVariable(
                        "ACTION",
                        new TextObject("{=gwp_sparring_talk}Talk"));
            }

            focusableObjectInformation.IsActive = true;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_abortMission)
            {
                _abortMission = false;
                Mission.EndMission();
                return;
            }

            try
            {
                switch (_phase)
                {
                    case BoutPhase.NativeSpawning:
                        TickNativeSpawning();
                        break;
                    case BoutPhase.PreparingFormations:
                        TickFormationPreparation();
                        break;
                    case BoutPhase.Marching:
                        TickMarching();
                        break;
                    case BoutPhase.OpponentAdvancing:
                        TickOpponentAdvance();
                        break;
                    case BoutPhase.AwaitingApproach:
                        TickAwaitingApproach();
                        break;
                    case BoutPhase.Conversation:
                        TickConversation();
                        break;
                    case BoutPhase.PreparingDuel:
                        TickDuelPreparation();
                        break;
                    case BoutPhase.Fighting:
                        TickDuel();
                        break;
                    case BoutPhase.VictoryPending:
                        TickVictoryTransition();
                        break;
                }
            }
            catch (Exception exception)
            {
                AbortMission("tick phase " + _phase, exception);
            }
        }

        private void TickNativeSpawning()
        {
            SetTeamsHostile(isHostile: false);
            RefreshPreDuelSafety(force: false);
            HoldNewlySpawnedFormations();
            if (_spawnLogic?.IsInitialSpawnOver != true)
                return;

            _spawnLogic.StopSpawner(BattleSideEnum.Defender);
            _spawnLogic.StopSpawner(BattleSideEnum.Attacker);
            _spawnLogic.SetReinforcementsSpawnEnabled(false);

            _playerAgent ??= Mission.Agents.FirstOrDefault(
                agent => agent.IsHuman
                    && agent.IsActive()
                    && ReferenceEquals(
                        agent.Origin?.Troop ?? agent.Character,
                        CharacterObject.PlayerCharacter));
            _opponentAgent ??= Mission.Agents.FirstOrDefault(
                agent => agent.IsHuman
                    && agent.IsActive()
                    && ReferenceEquals(
                        agent.Origin?.Troop ?? agent.Character,
                        _opponent.CharacterObject));
            if (_playerAgent?.IsActive() != true
                || _opponentAgent?.IsActive() != true)
            {
                throw new InvalidOperationException(
                    "the native spawner did not create both duelists");
            }

            // The player remains freely controlled at the native deployment
            // line while the armies receive their automatic march orders.
            _playerAgent.Formation = null;
            RefreshPreDuelSafety(force: true);

            Vec2 playerCenter = GetTeamCenter(
                Mission.PlayerTeam,
                _playerAgent);
            Vec2 opponentCenter = GetTeamCenter(
                Mission.PlayerEnemyTeam,
                _opponentAgent);
            _battleAxis = opponentCenter - playerCenter;
            float deploymentSeparation = _battleAxis.Length;
            Debug.Print(
                "[GreyWarden Sparring] native deployment check: "
                + $"spawnPath={Mission.HasSpawnPath}; "
                + $"fieldBattle={Mission.IsFieldBattle}; "
                + $"player=({playerCenter.x:0.0},{playerCenter.y:0.0}); "
                + $"opponent=({opponentCenter.x:0.0},{opponentCenter.y:0.0}); "
                + $"separation={deploymentSeparation:0.0}");
            if (!Mission.HasSpawnPath
                || !Mission.IsFieldBattle
                || deploymentSeparation < MinimumNativeDeploymentSeparation)
            {
                throw new InvalidOperationException(
                    "the native field deployment did not place the armies "
                    + "on opposite sides");
            }

            _battleAxis *= 1f / deploymentSeparation;
            _meetingCenter = (playerCenter + opponentCenter) * 0.5f;

            PrepareTeamFormations(Mission.PlayerTeam, _battleAxis);
            PrepareTeamFormations(Mission.PlayerEnemyTeam, -_battleAxis);
            _phase = BoutPhase.PreparingFormations;
            _phaseStartedAt = Mission.CurrentTime;
            Debug.Print(
                "[GreyWarden Sparring] native armies spawned at their "
                + $"field deployment lines; agents={Mission.Agents.Count}");
        }

        private void TickFormationPreparation()
        {
            RefreshPreDuelSafety(force: false);
            if (Mission.CurrentTime - _phaseStartedAt
                < FormationPreparationSeconds)
            {
                return;
            }

            _formationTargets.Clear();
            BuildFormationTargets(
                Mission.PlayerTeam,
                _battleAxis,
                isPlayerSide: true);
            BuildFormationTargets(
                Mission.PlayerEnemyTeam,
                -_battleAxis,
                isPlayerSide: false);
            if (_formationTargets.Count == 0)
            {
                _meetingCenter = (_playerAgent!.Position.AsVec2
                    + _opponentAgent!.Position.AsVec2) * 0.5f;
                _phase = BoutPhase.Marching;
                _phaseStartedAt = Mission.CurrentTime;
                Debug.Print(
                    "[GreyWarden Sparring] both parties have no spectator "
                    + "formations; advancing opponent directly");
                FinishRanksAndAdvanceOpponent();
                return;
            }

            IssueFormationOrders();
            _phase = BoutPhase.Marching;
            _phaseStartedAt = Mission.CurrentTime;
            _nextFormationOrderRefresh = Mission.CurrentTime
                + FormationOrderRefreshInterval;
            _nextGapCorrection = Mission.CurrentTime + 1f;
            Debug.Print(
                "[GreyWarden Sparring] both native armies ordered to "
                + $"advance; formations={_formationTargets.Count}");
        }

        private void TickMarching()
        {
            SetTeamsHostile(isHostile: false);
            RefreshPreDuelSafety(force: false);
            if (Mission.CurrentTime >= _nextFormationOrderRefresh)
            {
                _nextFormationOrderRefresh = Mission.CurrentTime
                    + FormationOrderRefreshInterval;
                IssueFormationOrders();
            }

            if (Mission.CurrentTime >= _nextGapCorrection)
            {
                _nextGapCorrection = Mission.CurrentTime
                    + GapCorrectionInterval;
                if (AreFormationTargetsReached()
                    && AreFormationTargetsStill())
                {
                    CorrectFormationGap();
                    IssueFormationOrders();
                }
            }

            float frontGap = CalculateRankFrontGap(out _, out _);
            bool positionsReady = AreFormationTargetsReached();
            bool ranksStill = AreFormationTargetsStill();
            bool hasBothRanks = HasActiveFormationTarget(Mission.PlayerTeam)
                && HasActiveFormationTarget(Mission.PlayerEnemyTeam);
            bool gapReady = !hasBothRanks
                || (frontGap >= MinimumAcceptableFrontGap
                    && frontGap <= MaximumAcceptableFrontGap);
            if (positionsReady && ranksStill && gapReady)
            {
                if (_formationsStableSince < 0f)
                    _formationsStableSince = Mission.CurrentTime;
                else if (Mission.CurrentTime - _formationsStableSince
                    >= FormationStableSeconds)
                {
                    FinishRanksAndAdvanceOpponent();
                }
            }
            else
            {
                _formationsStableSince = -1f;
            }

            if (_phase == BoutPhase.Marching
                && Mission.CurrentTime - _phaseStartedAt
                    >= MarchTimeoutSeconds)
            {
                Debug.Print(
                    "[GreyWarden Sparring] formation march timed out; "
                    + "continuing from current ranks, gap="
                    + FormatFrontGap(frontGap));
                FinishRanksAndAdvanceOpponent();
            }
        }

        private void TickOpponentAdvance()
        {
            SetTeamsHostile(isHostile: false);
            RefreshPreDuelSafety(force: false);
            HoldFrozenSpectators();
            if (_opponentAgent?.IsActive() != true)
                throw new InvalidOperationException(
                    "the challenged lord disappeared during the advance");

            if (!_opponentAdvanceOrderIssued)
                DirectOpponentToMeetingPoint();
            float distance = _opponentAgent.Position.AsVec2.Distance(
                _opponentMeetingPosition.AsVec2);
            bool stopped = _opponentAgent.MovementVelocity.LengthSquared
                <= FormationVelocityToleranceSquared;
            bool arrived = distance <= OpponentArrivalTolerance;
            bool stoppedNear = stopped
                && distance <= OpponentStoppedNearTolerance;
            bool timedOut = Mission.CurrentTime - _phaseStartedAt
                >= OpponentAdvanceTimeoutSeconds;
            if (!arrived && !stoppedNear)
            {
                if (timedOut && !_opponentAdvanceTimeoutLogged)
                {
                    _opponentAdvanceTimeoutLogged = true;
                    Debug.Print(
                        "[GreyWarden Sparring] opponent is still advancing "
                        + $"after timeout threshold; distance={distance:0.0}");
                }
                _opponentStableSince = -1f;
                return;
            }

            if (_opponentStableSince < 0f)
            {
                _opponentStableSince = Mission.CurrentTime;
                return;
            }

            if (Mission.CurrentTime - _opponentStableSince
                < OpponentStableSeconds)
            {
                return;
            }

            LockOpponentAtMeetingPoint();
            _phase = BoutPhase.AwaitingApproach;
            _phaseStartedAt = Mission.CurrentTime;
            Debug.Print(
                "[GreyWarden Sparring] opponent reached the field centre; "
                + "awaiting player approach");
        }

        private void TickAwaitingApproach()
        {
            SetTeamsHostile(isHostile: false);
            RefreshPreDuelSafety(force: false);
            HoldFrozenSpectators();
            HoldOpponentAtMeetingPoint();
            MissionConversationLogic? conversationLogic = Mission
                .GetMissionBehavior<MissionConversationLogic>();
            if (conversationLogic == null)
                throw new InvalidOperationException(
                    "field sparring mission has no conversation logic");

            // This battle mission mode is rejected by the stock conversation
            // behavior's action test. Keep it disabled globally so frozen
            // ranks never become talkable; this controller supplies the one
            // valid opponent interaction through IsThereAgentAction instead.
            conversationLogic.DisableStartConversation(isDisabled: true);
            bool interactionAvailable = _playerAgent != null
                && _opponentAgent != null
                && IsThereAgentAction(_playerAgent, _opponentAgent);
            if (interactionAvailable && !_interactionAvailableLogged)
            {
                _interactionAvailableLogged = true;
                Debug.Print(
                    "[GreyWarden Sparring] centre interaction action is "
                    + "available");
            }
        }

        private void TickConversation()
        {
            SetTeamsHostile(isHostile: false);
            HoldFrozenSpectators();
            HoldOpponentAtMeetingPoint();
        }

        private void TickDuelPreparation()
        {
            SetTeamsHostile(isHostile: false);
            RefreshPreDuelSafety(force: false);
            HoldFrozenSpectators();
            if (_duelStyle == DuelStyle.Foot)
            {
                TickFootDuelPreparation();
                return;
            }

            HoldOpponentAtMeetingPoint();
            TickMountedLoanPreparation();
        }

        private void TickFootDuelPreparation()
        {
            if (_playerAgent?.IsActive() != true
                || _opponentAgent?.IsActive() != true)
            {
                throw new InvalidOperationException(
                    "a duelist disappeared during foot-bout preparation");
            }

            Agent? opponentMount = _opponentAgent.MountAgent;
            if (opponentMount != null)
            {
                TickNativeDismount(
                    _opponentAgent,
                    opponentMount,
                    ref _opponentNativeDismountStarted,
                    ref _opponentDismountActionLogged,
                    ref _opponentNativeDismountStartedAt,
                    ref _opponentControllerBeforeDismount,
                    "opposing lord");
                return;
            }

            RestoreControllerAfterNativeDismount(
                _opponentAgent,
                _opponentControllerBeforeDismount);
            ReleaseNativeFootMount(_opponentDuelMount);
            BeginDuelCombat();
        }

        private void TickMountedLoanPreparation()
        {
            if (_playerAgent?.IsActive() != true
                || _loanCourier?.IsActive() != true
                || _loanMount?.IsActive() != true)
            {
                throw new InvalidOperationException(
                    "the mounted-bout loan horse became unavailable");
            }

            switch (_mountedLoanStage)
            {
                case MountedLoanStage.Delivering:
                    float deliveryDistance = _loanCourier.Position.AsVec2
                        .Distance(_loanDeliveryPosition.AsVec2);
                    bool courierStopped = _loanCourier.MovementVelocity
                        .LengthSquared
                        <= FormationVelocityToleranceSquared;
                    if (deliveryDistance <= 2f
                        || (deliveryDistance
                                <= LoanCourierStoppedNearTolerance
                            && courierStopped))
                    {
                        _loanDeliveryPosition = _loanCourier.GetWorldPosition();
                        _loanCourier.DisableScriptedMovement();
                        _loanMount.DisableScriptedMovement();
                        _loanCourier.SetMaximumSpeedLimit(0f, false);
                        _loanMount.SetMaximumSpeedLimit(0f, false);
                        _loanCourier.SetAutomaticTargetSelection(false);
                        _loanCourier.SetTargetAgent(null);
                        _loanCourier.SetLookAgent(null);
                        _mountedLoanStage = MountedLoanStage.Dismounting;
                        ApplyTemporaryDismountOrder(_loanCourier);
                        BeginNativeDismount(
                            _loanCourier,
                            _loanMount,
                            ref _loanNativeDismountStarted,
                            ref _loanDismountActionLogged,
                            ref _loanNativeDismountStartedAt,
                            ref _loanControllerBeforeDismount,
                            "loan courier");
                        Debug.Print(
                            "[GreyWarden Sparring] loan courier stopped at "
                            + "the delivery point and began native dismount; "
                            + $"distance={deliveryDistance:0.0}");
                    }
                    break;
                case MountedLoanStage.Dismounting:
                    if (_loanCourier.MountAgent == null)
                    {
                        RestoreControllerAfterNativeDismount(
                            _loanCourier,
                            _loanControllerBeforeDismount);
                        ReleaseTemporaryDismountFormation(_loanCourier);
                        PrepareLoanHorseForPlayer();
                        SendLoanCourierBackToRank();
                    }
                    else
                    {
                        TickNativeDismount(
                            _loanCourier,
                            _loanMount,
                            ref _loanNativeDismountStarted,
                            ref _loanDismountActionLogged,
                            ref _loanNativeDismountStartedAt,
                            ref _loanControllerBeforeDismount,
                            "loan courier");
                    }
                    break;
                case MountedLoanStage.Returning:
                    if (_loanCourier.Position.AsVec2.Distance(
                            _loanCourierReturnPosition.AsVec2) <= 3f)
                    {
                        FreezeLoanCourierAtRank();
                        _mountedLoanStage = MountedLoanStage.WaitingForPlayer;
                    }
                    break;
            }

            if (_mountedLoanStage == MountedLoanStage.WaitingForPlayer
                && _playerAgent.MountAgent == _loanMount)
            {
                _loanMount.SetTeam(Mission.PlayerTeam, true);
                BeginDuelCombat();
            }
        }

        private void TickDuel()
        {
            if (_duelStyle == DuelStyle.Foot
                && !_playerFootDismountConfirmed
                && _playerAgent?.IsActive() == true)
            {
                if (_playerAgent.MountAgent == null)
                {
                    _playerFootDismountConfirmed = true;
                    Debug.Print(
                        "[GreyWarden Sparring] player dismounted during "
                        + "the foot-duel grace period");
                }
                else if (Mission.CurrentTime >= _footDismountDeadline)
                {
                    Debug.Print(
                        "[GreyWarden Sparring] player failed the foot-duel "
                        + "dismount rule after 20 seconds");
                    ResolveDuel(playerWon: false, ruleViolation: true);
                    return;
                }
            }

            if (_playerAgent?.IsActive() == true
                && _opponentAgent?.IsActive() == true)
            {
                _playerAgent.SetTargetAgent(_opponentAgent);
                _opponentAgent.SetTargetAgent(_playerAgent);
            }
        }

        private void TickVictoryTransition()
        {
            if (!_victoryReactionStarted)
            {
                if (Mission.CurrentTime - _phaseStartedAt
                    < VictoryTransitionDelay)
                {
                    return;
                }

                _victoryLogic?.SetCheerActionGroup(
                    AgentVictoryLogic.CheerActionGroupEnum.HighCheerActions);
                _victoryLogic?.SetCheerReactionTimerSettings(0.25f, 3f);
                _victoryLogic?.SetTimersOfVictoryReactionsOnBattleEnd(
                    _winnerSide);
                _victoryReactionStarted = true;
                _victoryReactionStartedAt = Mission.CurrentTime;
                return;
            }

            if (Mission.CurrentTime - _victoryReactionStartedAt
                < VictoryReactionLeadSeconds)
            {
                return;
            }

            _canLeave = true;
            _phase = BoutPhase.Finished;

            string result = _ruleViolationLoss
                ? GwpText.Get(
                    "{=gwp_sparring_field_rule_loss}You did not dismount in time. You lose.")
                : _playerWon
                    ? GwpText.Get(
                        "{=gwp_sparring_field_win}{OPPONENT} gives up. You won.",
                        "OPPONENT",
                        _opponent.Name)
                    : GwpText.Get(
                        "{=gwp_sparring_field_loss}You lost to {OPPONENT}.",
                        "OPPONENT",
                        _opponent.Name);
            MBInformationManager.AddQuickInformation(new TextObject(result));
            Debug.Print(
                "[GreyWarden Sparring] hideout-style duel resolved; "
                + $"winner={_winnerSide}");
        }

        private void HoldNewlySpawnedFormations()
        {
            foreach (Team team in Mission.Teams)
            {
                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation.CountOfUnits == 0)
                        continue;

                    formation.SetControlledByAI(false, false);
                    formation.SetFiringOrder(
                        FiringOrder.FiringOrderHoldYourFire);
                    formation.SetMovementOrder(
                        MovementOrder.MovementOrderStop);
                }
            }
        }

        private void PrepareTeamFormations(Team team, Vec2 facing)
        {
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits == 0)
                    continue;

                formation.SetControlledByAI(false, false);
                formation.SetArrangementOrder(
                    ArrangementOrder.ArrangementOrderLine);
                formation.SetFiringOrder(
                    FiringOrder.FiringOrderHoldYourFire);
                formation.SetFacingOrder(
                    FacingOrder.FacingOrderLookAtDirection(facing));
                formation.SetMovementOrder(
                    MovementOrder.MovementOrderStop);
            }
        }

        private void BuildFormationTargets(
            Team team,
            Vec2 facing,
            bool isPlayerSide)
        {
            List<Formation> formations = team.FormationsIncludingEmpty
                .Where(formation => formation.CountOfUnits > 0)
                .ToList();
            if (formations.Count == 0)
                return;

            float totalWidth = formations.Sum(GetFormationWidth)
                + FormationLateralGap * (formations.Count - 1);
            float cursor = -totalWidth * 0.5f;
            Vec2 lateral = new Vec2(-_battleAxis.y, _battleAxis.x);
            foreach (Formation formation in formations)
            {
                float width = GetFormationWidth(formation);
                float lateralOffset = cursor + width * 0.5f;
                cursor += width + FormationLateralGap;

                float frontOffset = GetFormationFrontOffset(
                    formation,
                    isPlayerSide);
                Vec2 sideOffset = _battleAxis
                    * (DesiredFrontGap * 0.5f + frontOffset);
                Vec2 center = isPlayerSide
                    ? _meetingCenter - sideOffset
                    : _meetingCenter + sideOffset;
                center += lateral * lateralOffset;

                WorldPosition position = CreateReachableWorldPosition(
                    formation.CachedMedianPosition,
                    center);
                _formationTargets.Add(
                    new FormationTarget(
                        formation,
                        team,
                        position,
                        facing));
            }
        }

        private float GetFormationFrontOffset(
            Formation formation,
            bool isPlayerSide)
        {
            float medianProjection = Project(
                formation.CachedMedianPosition.AsVec2,
                _battleAxis);
            float frontProjection = isPlayerSide
                ? float.MinValue
                : float.MaxValue;
            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman
                    || !agent.IsActive()
                    || agent.Formation != formation
                    || agent == _playerAgent
                    || agent == _opponentAgent)
                {
                    continue;
                }

                float projection = Project(agent.Position.AsVec2, _battleAxis);
                frontProjection = isPlayerSide
                    ? Math.Max(frontProjection, projection)
                    : Math.Min(frontProjection, projection);
            }

            if (frontProjection == float.MinValue
                || frontProjection == float.MaxValue)
            {
                return Math.Max(0.75f, formation.Depth * 0.5f);
            }

            return Math.Max(
                0.75f,
                isPlayerSide
                    ? frontProjection - medianProjection
                    : medianProjection - frontProjection);
        }

        private static float GetFormationWidth(Formation formation)
        {
            float width = formation.Width;
            if (float.IsNaN(width)
                || float.IsInfinity(width)
                || width < 2f)
            {
                width = Math.Max(
                    2f,
                    (float)Math.Sqrt(formation.CountOfUnits) * 1.5f);
            }

            return Math.Min(width, 80f);
        }

        private void IssueFormationOrders()
        {
            foreach (FormationTarget target in _formationTargets)
            {
                Formation formation = target.Formation;
                if (formation.CountOfUnits == 0)
                    continue;

                formation.SetControlledByAI(false, false);
                formation.SetArrangementOrder(
                    ArrangementOrder.ArrangementOrderLine);
                formation.SetFiringOrder(
                    FiringOrder.FiringOrderHoldYourFire);
                formation.SetFacingOrder(
                    FacingOrder.FacingOrderLookAtDirection(target.Facing));
                formation.SetMovementOrder(
                    MovementOrder.MovementOrderMove(target.Position));
            }
        }

        private bool AreFormationTargetsReached()
        {
            int activeTargets = 0;
            int reachedTargets = 0;
            foreach (FormationTarget target in _formationTargets)
            {
                if (target.Formation.CountOfUnits == 0)
                    continue;

                activeTargets++;
                float tolerance = Math.Max(
                    FormationArrivalTolerance,
                    Math.Min(12f, target.Formation.Depth * 0.4f + 3f));
                if (target.Formation.CachedMedianPosition.AsVec2.Distance(
                        target.Position.AsVec2)
                    <= tolerance)
                {
                    reachedTargets++;
                }
            }

            return activeTargets > 0
                && reachedTargets == activeTargets;
        }

        private bool AreFormationTargetsStill()
        {
            int activeTargets = 0;
            int stillTargets = 0;
            foreach (FormationTarget target in _formationTargets)
            {
                if (target.Formation.CountOfUnits == 0)
                    continue;

                activeTargets++;
                if (target.Formation.CachedCurrentVelocity.LengthSquared
                    <= FormationVelocityToleranceSquared)
                {
                    stillTargets++;
                }
            }

            return activeTargets > 0
                && stillTargets == activeTargets;
        }

        private bool HasActiveFormationTarget(Team team)
        {
            return _formationTargets.Any(target =>
                target.Team == team
                && target.Formation.CountOfUnits > 0);
        }

        private static string FormatFrontGap(float frontGap)
        {
            return float.IsNaN(frontGap) || float.IsInfinity(frontGap)
                ? "single-sided"
                : frontGap.ToString("0.0");
        }

        private void CorrectFormationGap()
        {
            float gap = CalculateRankFrontGap(out _, out _);
            if (float.IsNaN(gap) || float.IsInfinity(gap))
                return;

            float correction = MathF.Clamp(
                (gap - DesiredFrontGap) * 0.5f,
                -8f,
                8f);
            if (Math.Abs(correction) < 0.75f)
                return;

            foreach (FormationTarget target in _formationTargets)
            {
                Vec2 shift = target.Team == Mission.PlayerTeam
                    ? _battleAxis * correction
                    : -_battleAxis * correction;
                target.Position = CreateReachableWorldPosition(
                    target.Formation.CachedMedianPosition,
                    target.Position.AsVec2 + shift);
            }
        }

        private float CalculateRankFrontGap(
            out float playerFront,
            out float opponentFront)
        {
            playerFront = float.MinValue;
            opponentFront = float.MaxValue;
            bool hasPlayerRank = false;
            bool hasOpponentRank = false;

            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman
                    || !agent.IsActive()
                    || agent == _playerAgent
                    || agent == _opponentAgent)
                {
                    continue;
                }

                float projection = Project(agent.Position.AsVec2, _battleAxis);
                if (agent.Team == Mission.PlayerTeam)
                {
                    playerFront = Math.Max(playerFront, projection);
                    hasPlayerRank = true;
                }
                else if (agent.Team == Mission.PlayerEnemyTeam)
                {
                    opponentFront = Math.Min(opponentFront, projection);
                    hasOpponentRank = true;
                }
            }

            if (!hasPlayerRank || !hasOpponentRank)
                return float.NaN;

            return opponentFront - playerFront;
        }

        private void FinishRanksAndAdvanceOpponent()
        {
            if (_phase != BoutPhase.Marching)
                return;

            float gap = CalculateRankFrontGap(
                out float playerFront,
                out float opponentFront);
            float currentProjection = Project(_meetingCenter, _battleAxis);
            float desiredProjection = currentProjection;
            if (playerFront != float.MinValue
                && opponentFront != float.MaxValue)
            {
                desiredProjection = (playerFront + opponentFront) * 0.5f;
            }
            else if (opponentFront != float.MaxValue)
            {
                // A lone player has no friendly rank. Place the bout in front
                // of the opponent's actual settled line instead of inventing
                // a second formation and a synthetic rank.
                desiredProjection = opponentFront
                    - DesiredFrontGap * 0.5f;
            }
            else if (playerFront != float.MinValue)
            {
                desiredProjection = playerFront
                    + DesiredFrontGap * 0.5f;
            }

            _meetingCenter += _battleAxis
                * (desiredProjection - currentProjection);

            if (_opponentAgent?.IsActive() != true)
                throw new InvalidOperationException(
                    "the challenged lord is unavailable after formation");

            _opponentAgent.Formation = null;
            _opponentMountHoldPosition = WorldPosition.Invalid;
            _opponentMeetingPosition = CreateReachableWorldPosition(
                _opponentAgent.GetWorldPosition(),
                _meetingCenter);
            _meetingCenter = _opponentMeetingPosition.AsVec2;
            FreezeRanksForCeremony();
            _opponentStableSince = -1f;
            _opponentAdvanceOrderIssued = false;
            _opponentAdvanceTimeoutLogged = false;
            _phase = BoutPhase.OpponentAdvancing;
            _phaseStartedAt = Mission.CurrentTime;
            DirectOpponentToMeetingPoint();
            Debug.Print(
                "[GreyWarden Sparring] ranks formed after native march; "
                + "frontGap=" + FormatFrontGap(gap)
                + "; opponent advancing to centre");
        }

        private void DirectOpponentToMeetingPoint()
        {
            if (_opponentAgent?.IsActive() != true
                || !_opponentMeetingPosition.IsValid)
            {
                return;
            }

            WorldPosition position = _opponentMeetingPosition;
            Vec2 direction = -_battleAxis;
            _opponentAgent.SetAutomaticTargetSelection(false);
            _opponentAgent.SetTargetAgent(null);
            _opponentAgent.SetLookAgent(null);
            _opponentAgent.SetMortalityState(
                Agent.MortalityState.Invulnerable);
            _opponentAgent.SetWatchState(Agent.WatchState.Patrolling);
            _opponentAgent.DisableScriptedMovement();
            _opponentAgent.MountAgent?.DisableScriptedMovement();
            _opponentAgent.SetMaximumSpeedLimit(-1f, false);
            _opponentAgent.MountAgent?.SetMaximumSpeedLimit(-1f, false);
            _opponentAgent.SetScriptedPositionAndDirection(
                ref position,
                direction.RotationInRadians,
                true,
                Agent.AIScriptedFrameFlags.None);
            _opponentAdvanceOrderIssued = true;
            _opponentAgent.LookDirection = new Vec3(
                direction.x,
                direction.y,
                0f);
            _opponentAgent.IsLookDirectionLocked = true;
            _opponentAgent.SetMovementDirection(in direction);
        }

        private void RefreshPreDuelSafety(bool force)
        {
            if (!force && Mission.CurrentTime < _nextSafetyRefresh)
                return;

            _nextSafetyRefresh = Mission.CurrentTime
                + SafetyRefreshInterval;
            foreach (Agent agent in Mission.Agents)
                MakeAgentSafeForStaging(agent);

            foreach (Team team in Mission.Teams)
            {
                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation.CountOfUnits == 0)
                        continue;

                    formation.SetFiringOrder(
                        FiringOrder.FiringOrderHoldYourFire);
                    formation.SetControlledByAI(false, false);
                }
            }
        }

        private static void MakeAgentSafeForStaging(Agent agent)
        {
            if (!agent.IsHuman || !agent.IsActive())
                return;

            agent.SetMortalityState(Agent.MortalityState.Invulnerable);
            agent.SetAutomaticTargetSelection(false);
            agent.SetTargetAgent(null);
            if (agent.IsAIControlled)
                agent.SetWatchState(Agent.WatchState.Patrolling);
        }

        private void FreezeRanksForCeremony()
        {
            _spectators.Clear();
            _spectatorOriginalTeams.Clear();
            _spectatorOriginalFormations.Clear();
            _spectatorHoldPositions.Clear();
            foreach (Agent spectator in Mission.Agents.ToList())
            {
                if (!spectator.IsHuman
                    || !spectator.IsActive()
                    || spectator == _playerAgent
                    || spectator == _opponentAgent)
                {
                    continue;
                }

                Team originalTeam = spectator.Team;
                if (originalTeam != Mission.PlayerTeam
                    && originalTeam != Mission.PlayerEnemyTeam)
                {
                    continue;
                }

                WorldPosition holdPosition = spectator.GetWorldPosition();
                Vec2 direction = _meetingCenter
                    - holdPosition.AsVec2;
                if (direction.LengthSquared < 0.01f)
                {
                    direction = originalTeam == Mission.PlayerTeam
                        ? _battleAxis
                        : -_battleAxis;
                }
                else
                {
                    direction.Normalize();
                }

                _spectators.Add(spectator);
                _spectatorOriginalTeams[spectator] = originalTeam;
                _spectatorOriginalFormations[spectator] =
                    spectator.Formation;
                _spectatorHoldPositions[spectator] = holdPosition;
                spectator.Formation = null;
                spectator.SetAutomaticTargetSelection(false);
                spectator.SetTargetAgent(null);
                spectator.SetLookAgent(null);
                spectator.SetMortalityState(
                    Agent.MortalityState.Invulnerable);
                spectator.SetMaximumSpeedLimit(0f, false);
                spectator.MountAgent?.SetMaximumSpeedLimit(0f, false);
                spectator.SetWatchState(Agent.WatchState.Patrolling);
                spectator.SetScriptedPositionAndDirection(
                    ref holdPosition,
                    direction.RotationInRadians,
                    false,
                    Agent.AIScriptedFrameFlags.NoAttack
                    | Agent.AIScriptedFrameFlags.DoNotRun
                    | Agent.AIScriptedFrameFlags.ConsiderRotation);
                spectator.LookDirection = new Vec3(
                    direction.x,
                    direction.y,
                    0f);
                spectator.IsLookDirectionLocked = true;
                spectator.SetMovementDirection(in direction);
            }

            Debug.Print(
                "[GreyWarden Sparring] spectator ranks locked at fixed "
                + $"positions; agents={_spectators.Count}");
        }

        private void HoldFrozenSpectators()
        {
            foreach (Agent spectator in _spectators)
            {
                if (!spectator.IsActive()
                    || !_spectatorHoldPositions.TryGetValue(
                        spectator,
                        out WorldPosition holdPosition))
                {
                    continue;
                }

                Vec2 direction = _meetingCenter - holdPosition.AsVec2;
                if (direction.LengthSquared < 0.01f)
                {
                    direction = _spectatorOriginalTeams.TryGetValue(
                            spectator,
                            out Team originalTeam)
                        && originalTeam == Mission.PlayerTeam
                            ? _battleAxis
                            : -_battleAxis;
                }
                else
                {
                    direction.Normalize();
                }

                spectator.SetAutomaticTargetSelection(false);
                spectator.SetTargetAgent(null);
                spectator.SetMaximumSpeedLimit(0f, false);
                spectator.MountAgent?.SetMaximumSpeedLimit(0f, false);
                spectator.SetScriptedPositionAndDirection(
                    ref holdPosition,
                    direction.RotationInRadians,
                    false,
                    Agent.AIScriptedFrameFlags.NoAttack
                    | Agent.AIScriptedFrameFlags.DoNotRun
                    | Agent.AIScriptedFrameFlags.ConsiderRotation);
            }
        }

        private void LockOpponentAtMeetingPoint()
        {
            if (_opponentAgent?.IsActive() != true)
                return;

            float remainingDistance = _opponentAgent.Position.AsVec2.Distance(
                _opponentMeetingPosition.AsVec2);
            _opponentMeetingPosition = _opponentAgent.GetWorldPosition();
            _meetingCenter = _opponentMeetingPosition.AsVec2;
            Agent? mount = _opponentAgent.MountAgent;
            if (mount?.IsActive() == true)
                _opponentMountHoldPosition = mount.GetWorldPosition();

            _opponentAgent.DisableScriptedMovement();
            mount?.DisableScriptedMovement();
            _opponentAgent.SetMaximumSpeedLimit(0f, false);
            mount?.SetMaximumSpeedLimit(0f, false);

            WorldPosition riderPosition = _opponentMeetingPosition;
            _opponentAgent.SetScriptedPosition(
                ref riderPosition,
                false,
                Agent.AIScriptedFrameFlags.NoAttack
                | Agent.AIScriptedFrameFlags.DoNotRun);
            if (mount?.IsActive() == true
                && _opponentMountHoldPosition.IsValid)
            {
                WorldPosition mountPosition = _opponentMountHoldPosition;
                mount.SetScriptedPosition(
                    ref mountPosition,
                    false,
                    Agent.AIScriptedFrameFlags.NoAttack
                    | Agent.AIScriptedFrameFlags.DoNotRun);
            }

            HoldOpponentAtMeetingPoint();
            Debug.Print(
                "[GreyWarden Sparring] opponent locked at centre; "
                + $"remainingDistance={remainingDistance:0.0}");
        }

        private void HoldOpponentAtMeetingPoint()
        {
            if (_opponentAgent?.IsActive() != true
                || !_opponentMeetingPosition.IsValid)
            {
                return;
            }

            Vec2 direction = -_battleAxis;
            _opponentAgent.SetAutomaticTargetSelection(false);
            _opponentAgent.SetTargetAgent(null);
            _opponentAgent.SetLookAgent(null);
            _opponentAgent.SetMaximumSpeedLimit(0f, false);
            Agent? mount = _opponentAgent.MountAgent;
            mount?.SetMaximumSpeedLimit(0f, false);
            _opponentAgent.LookDirection = new Vec3(
                direction.x,
                direction.y,
                0f);
            _opponentAgent.IsLookDirectionLocked = true;
            if (mount?.IsActive() == true)
            {
                mount.SetAutomaticTargetSelection(false);
                mount.SetTargetAgent(null);
                mount.SetLookAgent(null);
                mount.LookDirection = new Vec3(
                    direction.x,
                    direction.y,
                    0f);
                mount.IsLookDirectionLocked = true;
            }
        }

        private void InstallDuelArenaBoundary()
        {
            float halfLength = DesiredFrontGap * 0.5f
                - DuelArenaLineClearance;
            float gap = CalculateRankFrontGap(
                out float playerFront,
                out float opponentFront);
            float centerProjection = Project(_meetingCenter, _battleAxis);
            if (!float.IsNaN(gap) && !float.IsInfinity(gap))
            {
                halfLength = Math.Min(
                    centerProjection - playerFront,
                    opponentFront - centerProjection)
                    - DuelArenaLineClearance;
            }
            else if (opponentFront != float.MaxValue)
            {
                halfLength = Math.Min(
                    halfLength,
                    opponentFront - centerProjection
                    - DuelArenaLineClearance);
            }

            halfLength = Math.Max(20f, halfLength);
            Vec2 lateral = new Vec2(-_battleAxis.y, _battleAxis.x);
            Vec2 forwardOffset = _battleAxis * halfLength;
            Vec2 lateralOffset = lateral * DuelArenaHalfWidth;
            var points = new List<Vec2>
            {
                _meetingCenter - forwardOffset - lateralOffset,
                _meetingCenter + forwardOffset - lateralOffset,
                _meetingCenter + forwardOffset + lateralOffset,
                _meetingCenter - forwardOffset + lateralOffset
            };

            const string boundaryName = "walk_area";
            if (Mission.Boundaries.ContainsKey(boundaryName))
                Mission.Boundaries.Remove(boundaryName);
            Mission.Boundaries.Add(boundaryName, points);
            Debug.Print(
                "[GreyWarden Sparring] native duel boundary installed; "
                + $"length={halfLength * 2f:0.0}; "
                + $"width={DuelArenaHalfWidth * 2f:0.0}");
        }

        private void SetTeamsHostile(bool isHostile)
        {
            Team? playerTeam = Mission.PlayerTeam;
            Team? opponentTeam = Mission.PlayerEnemyTeam;
            if (playerTeam == null || opponentTeam == null)
                return;

            playerTeam.SetIsEnemyOf(opponentTeam, isHostile);
            opponentTeam.SetIsEnemyOf(playerTeam, isHostile);
        }

        private void StartDuelInternal(DuelStyle duelStyle)
        {
            if (_phase != BoutPhase.Conversation
                || _playerAgent?.IsActive() != true
                || _opponentAgent?.IsActive() != true)
            {
                return;
            }

            Mission.GetMissionBehavior<MissionConversationLogic>()?
                .DisableStartConversation(isDisabled: true);

            _duelStyle = duelStyle;
            _opponentDuelMount = _opponentAgent.MountAgent;
            _ruleViolationLoss = false;
            if (duelStyle == DuelStyle.Foot)
            {
                _opponentAgent.DisableScriptedMovement();
                _opponentAgent.MountAgent?.DisableScriptedMovement();
                _footDismountDeadline = Mission.CurrentTime
                    + FootDismountGraceSeconds;
                _playerFootDismountConfirmed =
                    _playerAgent.MountAgent == null;
                _opponentNativeDismountStarted = false;
                _opponentDismountActionLogged = false;
                _phase = BoutPhase.PreparingDuel;
                _phaseStartedAt = Mission.CurrentTime;
                TickFootDuelPreparation();
                Debug.Print(
                    "[GreyWarden Sparring] foot duel selected; opposing lord "
                    + "began the native dismount action");
                return;
            }

            if (_playerAgent.MountAgent == null)
            {
                if (!StartMountedLoanPreparation())
                {
                    AbortMission(
                        "prepare mounted loan",
                        new InvalidOperationException(
                            "no mounted spectator can provide a horse"));
                }
                return;
            }

            BeginDuelCombat();
        }

        private void BeginDuelCombat()
        {
            if (_playerAgent?.IsActive() != true
                || _opponentAgent?.IsActive() != true)
            {
                return;
            }

            InstallDuelArenaBoundary();
            FreezeSpectatorsForDuel();
            _playerAgent.DisableScriptedMovement();
            _opponentAgent.DisableScriptedMovement();
            _playerAgent.MountAgent?.DisableScriptedMovement();
            _opponentAgent.MountAgent?.DisableScriptedMovement();
            _playerAgent.SetMaximumSpeedLimit(-1f, false);
            _opponentAgent.SetMaximumSpeedLimit(-1f, false);
            _playerAgent.SetRidingOrder(
                RidingOrder.RidingOrderEnum.Free);
            _opponentAgent.SetRidingOrder(
                RidingOrder.RidingOrderEnum.Free);
            _playerAgent.MountAgent?.SetMaximumSpeedLimit(-1f, false);
            _opponentAgent.MountAgent?.SetMaximumSpeedLimit(-1f, false);
            _playerAgent.IsLookDirectionLocked = false;
            _opponentAgent.IsLookDirectionLocked = false;
            if (_opponentAgent.MountAgent != null)
                _opponentAgent.MountAgent.IsLookDirectionLocked = false;
            SetTeamsHostile(isHostile: true);
            _playerAgent.SetMortalityState(Agent.MortalityState.Mortal);
            _opponentAgent.SetMortalityState(Agent.MortalityState.Mortal);
            _playerAgent.MountAgent?.SetMortalityState(
                Agent.MortalityState.Mortal);
            _opponentAgent.MountAgent?.SetMortalityState(
                Agent.MortalityState.Mortal);
            _playerAgent.SetAutomaticTargetSelection(false);
            _opponentAgent.SetAutomaticTargetSelection(false);
            _playerAgent.SetTargetAgent(_opponentAgent);
            _opponentAgent.SetTargetAgent(_playerAgent);
            _opponentAgent.SetWatchState(Agent.WatchState.Alarmed);
            _playerAgent.WieldInitialWeapons();
            _opponentAgent.WieldInitialWeapons();

            _phase = BoutPhase.Fighting;
            _phaseStartedAt = Mission.CurrentTime;
            MBInformationManager.AddQuickInformation(
                new TextObject(
                    GwpText.Get(
                        "{=gwp_sparring_begin}The match has started.")));
        }

        private void BeginNativeDismount(
            Agent rider,
            Agent mount,
            ref bool started,
            ref bool actionLogged,
            ref float startedAt,
            ref AgentControllerType previousController,
            string label)
        {
            if (started)
                return;

            rider.DisableScriptedMovement();
            mount.DisableScriptedMovement();
            previousController = rider.Controller;
            if (rider.Controller == AgentControllerType.AI)
                rider.Controller = AgentControllerType.None;
            rider.SetMaximumSpeedLimit(0f, false);
            mount.SetMaximumSpeedLimit(0f, false);
            rider.SetRidingOrder(RidingOrder.RidingOrderEnum.Dismount);
            rider.EventControlFlags |= Agent.EventControlFlag.Dismount;
            started = true;
            actionLogged = false;
            startedAt = Mission.CurrentTime;
            Debug.Print(
                "[GreyWarden Sparring] " + label
                + " AI paused for native dismount");
        }

        private void TickNativeDismount(
            Agent rider,
            Agent mount,
            ref bool started,
            ref bool actionLogged,
            ref float startedAt,
            ref AgentControllerType previousController,
            string label)
        {
            BeginNativeDismount(
                rider,
                mount,
                ref started,
                ref actionLogged,
                ref startedAt,
                ref previousController,
                label);

            rider.SetMaximumSpeedLimit(0f, false);
            mount.SetMaximumSpeedLimit(0f, false);
            rider.EventControlFlags |= Agent.EventControlFlag.Dismount;
            if (!actionLogged
                && rider.GetCurrentActionType(0)
                    == Agent.ActionCodeType.Dismount)
            {
                actionLogged = true;
                Debug.Print(
                    "[GreyWarden Sparring] " + label
                    + " entered the native dismount animation");
            }

            if (Mission.CurrentTime - startedAt
                >= NativeDismountTimeoutSeconds)
            {
                throw new InvalidOperationException(
                    label + " did not complete the native dismount within "
                    + NativeDismountTimeoutSeconds + " seconds; actionType="
                    + rider.GetCurrentActionType(0)
                    + "; eventFlags=" + rider.EventControlFlags
                    + "; controller=" + rider.Controller);
            }
        }

        private static void RestoreControllerAfterNativeDismount(
            Agent rider,
            AgentControllerType previousController)
        {
            if (rider.Controller != previousController)
                rider.Controller = previousController;
            rider.SetRidingOrder(RidingOrder.RidingOrderEnum.Free);
            Debug.Print(
                "[GreyWarden Sparring] native dismount completed and the "
                + "rider controller was restored");
        }

        private Agent? FindLoanCourier()
        {
            if (Mission.PlayerEnemyTeam == null)
                return null;

            return _spectators
                .Where(agent => agent.IsActive()
                    && agent.MountAgent?.IsActive() == true
                    && _spectatorOriginalTeams.TryGetValue(
                        agent,
                        out Team team)
                    && team == Mission.PlayerEnemyTeam)
                .OrderBy(agent => agent.Position.AsVec2.DistanceSquared(
                    _meetingCenter))
                .FirstOrDefault();
        }

        private Formation? FindEmptyOpponentFormation()
        {
            return Mission.PlayerEnemyTeam?.FormationsIncludingEmpty
                .FirstOrDefault(formation => formation.CountOfUnits == 0);
        }

        private void ApplyTemporaryDismountOrder(Agent agent)
        {
            _temporaryDismountFormation ??= FindEmptyOpponentFormation();
            Formation? formation = _temporaryDismountFormation;
            if (formation == null)
            {
                agent.SetRidingOrder(RidingOrder.RidingOrderEnum.Dismount);
                return;
            }

            if (agent.Formation != formation)
            {
                agent.Formation = formation;
                Debug.Print(
                    "[GreyWarden Sparring] one-agent temporary formation "
                    + "created for the dismount order");
            }
            formation.SetControlledByAI(false, false);
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
            formation.SetFiringOrder(FiringOrder.FiringOrderHoldYourFire);
            formation.SetFacingOrder(
                FacingOrder.FacingOrderLookAtDirection(-_battleAxis));
            formation.SetRidingOrder(RidingOrder.RidingOrderDismount);
            agent.SetRidingOrder(RidingOrder.RidingOrderEnum.Dismount);
        }

        private void ReleaseTemporaryDismountFormation(Agent agent)
        {
            Formation? formation = _temporaryDismountFormation;
            if (agent.Formation == formation)
                agent.Formation = null;
            formation?.SetRidingOrder(RidingOrder.RidingOrderFree);
            _temporaryDismountFormation = null;
        }

        private bool StartMountedLoanPreparation()
        {
            Agent? courier = FindLoanCourier();
            Agent? mount = courier?.MountAgent;
            Formation? dismountFormation = FindEmptyOpponentFormation();
            if (courier == null
                || mount?.IsActive() != true
                || dismountFormation == null
                || !_spectatorHoldPositions.TryGetValue(
                    courier,
                    out _loanCourierReturnPosition))
            {
                return false;
            }

            _loanCourier = courier;
            _loanMount = mount;
            _loanNativeDismountStarted = false;
            _loanDismountActionLogged = false;
            _temporaryDismountFormation = dismountFormation;
            _spectators.Remove(courier);
            Vec2 lateral = new Vec2(-_battleAxis.y, _battleAxis.x);
            _loanDeliveryPosition = CreateReachableWorldPosition(
                courier.GetWorldPosition(),
                _meetingCenter + lateral * 5f);
            courier.Formation = null;
            courier.DisableScriptedMovement();
            mount.DisableScriptedMovement();
            courier.SetMaximumSpeedLimit(-1f, false);
            mount.SetMaximumSpeedLimit(-1f, false);
            courier.SetAutomaticTargetSelection(false);
            courier.SetTargetAgent(null);
            courier.SetLookAgent(null);
            courier.IsLookDirectionLocked = false;
            courier.SetMortalityState(Agent.MortalityState.Invulnerable);
            mount.SetMortalityState(Agent.MortalityState.Invulnerable);
            courier.SetWatchState(Agent.WatchState.Patrolling);
            Vec2 direction = -_battleAxis;
            WorldPosition destination = _loanDeliveryPosition;
            courier.SetScriptedPositionAndDirection(
                ref destination,
                direction.RotationInRadians,
                true,
                Agent.AIScriptedFrameFlags.None);
            _mountedLoanStage = MountedLoanStage.Delivering;
            _phase = BoutPhase.PreparingDuel;
            _phaseStartedAt = Mission.CurrentTime;
            Debug.Print(
                "[GreyWarden Sparring] mounted duel selected without a "
                + "player horse; enemy cavalry courier advancing");
            return true;
        }

        private void PrepareLoanHorseForPlayer()
        {
            if (_loanMount?.IsActive() != true)
                return;

            _loanMount.SetMaximumSpeedLimit(0f, false);
            _loanMount.SetAutomaticTargetSelection(false);
            _loanMount.SetTargetAgent(null);
            _loanMount.SetMortalityState(Agent.MortalityState.Invulnerable);
            float playerRidingSkill = _playerAgent?.Character
                .GetSkillValue(DefaultSkills.Riding) ?? 0f;
            float mountDifficulty = _loanMount.GetAgentDrivenPropertyValue(
                DrivenProperty.MountDifficulty);
            if (mountDifficulty > playerRidingSkill)
            {
                _loanMount.SetAgentDrivenPropertyValueFromConsole(
                    DrivenProperty.MountDifficulty,
                    playerRidingSkill);
            }
            WorldPosition holdPosition = _loanMount.GetWorldPosition();
            _loanMount.SetScriptedPosition(
                ref holdPosition,
                false,
                Agent.AIScriptedFrameFlags.NoAttack
                | Agent.AIScriptedFrameFlags.DoNotRun);
            Debug.Print(
                "[GreyWarden Sparring] loan horse ready for the player");
        }

        private void SendLoanCourierBackToRank()
        {
            if (_loanCourier?.IsActive() != true
                || !_loanCourierReturnPosition.IsValid)
            {
                return;
            }

            _loanCourier.DisableScriptedMovement();
            _loanCourier.SetMaximumSpeedLimit(-1f, false);
            _loanCourier.SetRidingOrder(
                RidingOrder.RidingOrderEnum.Free);
            _loanCourier.IsLookDirectionLocked = false;
            Vec2 direction = _battleAxis;
            WorldPosition returnPosition = _loanCourierReturnPosition;
            _loanCourier.SetScriptedPositionAndDirection(
                ref returnPosition,
                direction.RotationInRadians,
                true,
                Agent.AIScriptedFrameFlags.None);
            _mountedLoanStage = MountedLoanStage.Returning;
            Debug.Print(
                "[GreyWarden Sparring] loan courier dismounted and is "
                + "returning to the enemy rank");
        }

        private void FreezeLoanCourierAtRank()
        {
            if (_loanCourier?.IsActive() != true
                || !_loanCourierReturnPosition.IsValid)
            {
                return;
            }

            Vec2 direction = _meetingCenter
                - _loanCourierReturnPosition.AsVec2;
            if (direction.LengthSquared < 0.01f)
                direction = -_battleAxis;
            else
                direction.Normalize();
            WorldPosition holdPosition = _loanCourierReturnPosition;
            if (_spectatorOriginalFormations.TryGetValue(
                    _loanCourier,
                    out Formation? originalFormation)
                && originalFormation?.Team == Mission.PlayerEnemyTeam)
            {
                _loanCourier.Formation = originalFormation;
            }
            _loanCourier.SetMaximumSpeedLimit(0f, false);
            _loanCourier.SetScriptedPositionAndDirection(
                ref holdPosition,
                direction.RotationInRadians,
                false,
                Agent.AIScriptedFrameFlags.NoAttack
                | Agent.AIScriptedFrameFlags.DoNotRun
                | Agent.AIScriptedFrameFlags.ConsiderRotation);
            _loanCourier.LookDirection = new Vec3(
                direction.x,
                direction.y,
                0f);
            _loanCourier.IsLookDirectionLocked = true;
            if (!_spectators.Contains(_loanCourier))
                _spectators.Add(_loanCourier);
            Debug.Print(
                "[GreyWarden Sparring] loan courier returned to the enemy "
                + "rank; waiting for the player to mount");
        }

        private static void ReleaseNativeFootMount(Agent? mount)
        {
            if (mount?.IsActive() != true || mount.RiderAgent != null)
                return;

            mount.DisableScriptedMovement();
            mount.SetMaximumSpeedLimit(-1f, false);
            Debug.Print(
                "[GreyWarden Sparring] dismounted duel horse released to "
                + "native mission behavior");
        }

        private void FreezeSpectatorsForDuel()
        {
            if (_spectators.Count == 0)
                FreezeRanksForCeremony();

            foreach (Agent spectator in _spectators)
            {
                if (!spectator.IsActive()
                    || !_spectatorOriginalTeams.TryGetValue(
                        spectator,
                        out Team originalTeam)
                    || !_spectatorHoldPositions.TryGetValue(
                        spectator,
                        out WorldPosition holdPosition))
                {
                    continue;
                }

                spectator.Formation = null;
                spectator.SetTeam(Team.Invalid, true);
                spectator.SetScriptedPosition(
                    ref holdPosition,
                    false,
                    Agent.AIScriptedFrameFlags.NoAttack
                    | Agent.AIScriptedFrameFlags.DoNotRun);
                spectator.SetMaximumSpeedLimit(0f, false);
                spectator.MountAgent?.SetMaximumSpeedLimit(0f, false);
                spectator.SetAutomaticTargetSelection(false);
                spectator.SetTargetAgent(null);
                spectator.SetLookAgent(
                    originalTeam == Mission.PlayerTeam
                        ? _playerAgent
                        : _opponentAgent);
                spectator.SetMortalityState(
                    Agent.MortalityState.Invulnerable);
                spectator.MountAgent?.SetMortalityState(
                    Agent.MortalityState.Invulnerable);
            }
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow killingBlow)
        {
            base.OnAgentRemoved(
                affectedAgent,
                affectorAgent,
                agentState,
                killingBlow);
            if (_phase != BoutPhase.Fighting
                || (affectedAgent != _playerAgent
                    && affectedAgent != _opponentAgent))
            {
                return;
            }

            foreach (Agent spectator in _spectators)
            {
                if (spectator.IsActive()
                    && spectator.GetLookAgent() == affectedAgent)
                {
                    spectator.SetLookAgent(null);
                }
            }

            ResolveDuel(
                playerWon: affectedAgent == _opponentAgent,
                ruleViolation: false);
        }

        private void ResolveDuel(bool playerWon, bool ruleViolation)
        {
            if (_phase == BoutPhase.VictoryPending
                || _phase == BoutPhase.Finished)
            {
                return;
            }

            _playerWon = playerWon;
            _ruleViolationLoss = ruleViolation;
            GreyWardenSparringBehavior.QueueFieldResultConversation(
                _opponent,
                _playerWon,
                _ruleViolationLoss);
            _winnerSide = _playerWon
                ? Mission.PlayerTeam.Side
                : Mission.PlayerEnemyTeam.Side;
            Team winnerTeam = _playerWon
                ? Mission.PlayerTeam
                : Mission.PlayerEnemyTeam;
            Agent? winner = _playerWon
                ? _playerAgent
                : _opponentAgent;

            SetTeamsHostile(isHostile: false);
            foreach (Agent? duelist in new[] { _playerAgent, _opponentAgent })
            {
                if (duelist?.IsActive() != true)
                    continue;

                duelist.SetAutomaticTargetSelection(false);
                duelist.SetTargetAgent(null);
                duelist.SetMortalityState(Agent.MortalityState.Invulnerable);
            }

            if (winner?.IsActive() == true)
            {
                winner.SetWatchState(Agent.WatchState.Alarmed);
            }

            foreach (Agent spectator in _spectators)
            {
                if (!spectator.IsActive()
                    || !_spectatorOriginalTeams.TryGetValue(
                        spectator,
                        out Team originalTeam)
                    || originalTeam != winnerTeam)
                {
                    continue;
                }

                spectator.SetTeam(winnerTeam, true);
                if (_spectatorOriginalFormations.TryGetValue(
                        spectator,
                        out Formation? originalFormation)
                    && originalFormation?.Team == winnerTeam)
                {
                    spectator.Formation = originalFormation;
                }

                spectator.DisableScriptedMovement();
                spectator.SetMaximumSpeedLimit(-1f, false);
                spectator.MountAgent?.SetMaximumSpeedLimit(-1f, false);
                spectator.IsLookDirectionLocked = false;
                spectator.SetAutomaticTargetSelection(false);
                spectator.SetTargetAgent(null);
                spectator.SetMortalityState(
                    Agent.MortalityState.Invulnerable);
                spectator.SetWatchState(Agent.WatchState.Alarmed);
            }

            // TickVictoryTransition applies the stock hideout boss-duel
            // victory preset before AgentVictoryLogic starts its reactions.
            _phase = BoutPhase.VictoryPending;
            _phaseStartedAt = Mission.CurrentTime;
        }

        public override InquiryData OnEndMissionRequest(
            out bool canPlayerLeave)
        {
            canPlayerLeave = _canLeave;
            if (!canPlayerLeave)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject(
                        GwpText.Get(
                            "{=gwp_sparring_cannot_leave}The match isn't over yet.")));
            }

            return null!;
        }

        private Vec2 GetTeamCenter(Team team, Agent? excludedAgent)
        {
            Vec2 sum = Vec2.Zero;
            int count = 0;
            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman
                    || !agent.IsActive()
                    || agent.Team != team
                    || agent == excludedAgent)
                {
                    continue;
                }

                sum += agent.Position.AsVec2;
                count++;
            }

            if (count > 0)
                return sum * (1f / count);
            if (excludedAgent?.IsActive() == true
                && excludedAgent.Team == team)
            {
                return excludedAgent.Position.AsVec2;
            }

            throw new InvalidOperationException(
                $"team {team.Side} has no active field agents");
        }

        private static float Project(Vec2 position, Vec2 axis)
        {
            return position.x * axis.x + position.y * axis.y;
        }

        private WorldPosition CreateReachableWorldPosition(
            WorldPosition source,
            Vec2 desired)
        {
            try
            {
                Vec3 reachable = Mission.Scene
                    .GetLastPointOnNavigationMeshFromWorldPositionToDestination(
                        ref source,
                        desired);
                return new WorldPosition(
                    Mission.Scene,
                    UIntPtr.Zero,
                    reachable,
                    hasValidZ: false);
            }
            catch (Exception exception)
            {
                Debug.Print(
                    "[GreyWarden Sparring] navigation clamp failed: "
                    + exception.Message);
                return new WorldPosition(
                    Mission.Scene,
                    UIntPtr.Zero,
                    new Vec3(desired, source.GetGroundVec3().z),
                    hasValidZ: false);
            }
        }

        private void AbortMission(string phase, Exception exception)
        {
            if (_phase == BoutPhase.Aborted)
                return;

            _phase = BoutPhase.Aborted;
            _abortMission = true;
            Debug.Print(
                "[GreyWarden Sparring] field mission failed during "
                + phase + ": " + exception);
            MBInformationManager.AddQuickInformation(
                new TextObject(
                    GwpText.Get(
                        "{=gwp_sparring_spawn_failed}The terrain isn't suitable for forming up, so the match is cancelled.")));
        }
    }
}
