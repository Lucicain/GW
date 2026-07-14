using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Modifies the native AI's final input for one eligible Grey Warden.
    /// OnAIInputSet is invoked by the engine after it has calculated the AI
    /// input, so the Kick flag is not overwritten by the normal AI tick.
    /// </summary>
    internal sealed class GwpKickInputComponent : AgentComponent
    {
        // At this distance the native kick/bash can connect. Prefer the AI's
        // own target, then use the engine's native closest-enemy query.
        // 1.9m allowed the AI to request an alternative attack before the
        // native kick/shield contact could reach. Wait until the opponent is
        // firmly inside close combat range so fewer attempts whiff.
        private const float MaximumTargetDistance = 1.20f;
        private const float MaximumTargetDistanceSquared =
            MaximumTargetDistance * MaximumTargetDistance;
        private const float FacehugDistance = 0.95f;
        private const float FacehugDistanceSquared =
            FacehugDistance * FacehugDistance;

        // Once a tactical target is available, request an alternative attack
        // when the action cooldown expires. There is no separate trigger roll.
        private const float KickCooldownSeconds = 7.0f;
        private const float ShieldBashCooldownSeconds = 2.0f;
        private const float InputRequestWindowSeconds = 0.35f;
        private const float ShieldBashGuardWindowSeconds = 1.0f;
        private const float FailedTargetProbeCooldownSeconds = 0.25f;

        private float _nextKickTime;
        private float _requestKickUntil;
        private float _shieldBashGuardUntil;
        private float _nextTargetProbeTime;
        internal GwpKickInputComponent(Agent agent) : base(agent) { }

        public override void Initialize()
        {
            base.Initialize();

            // Spread the first attempts across a few frames in 200-v-200
            // battles instead of making every Grey Warden fire simultaneously.
            _nextKickTime = Agent.Mission.CurrentTime
                + (Agent.Index % 11) * 0.07f;

            // This enables the native Agent.OnAIInputSet callback for only the
            // agents carrying this component. No Harmony/external mod needed.
            Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnFormationSet()
        {
            base.OnFormationSet();

            // Event-driven re-arm only when the agent changes formation. There
            // is no periodic callback-state polling on every Grey Warden.
            if (!Agent.GetHasOnAiInputSetCallback())
                Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            float now = Agent.Mission.CurrentTime;
            MaintainShieldGuardDuringBash(now, ref movementFlag);

            if (!CanSupplyKickInput(now))
                return;

            if (now > _requestKickUntil)
            {
                bool hasWieldedShield = HasWieldedShield();
                float actionCooldown = hasWieldedShield
                    ? ShieldBashCooldownSeconds
                    : KickCooldownSeconds;
                _nextKickTime = now
                    + actionCooldown
                    + (Agent.Index % 7) * 0.09f;
                _requestKickUntil = now + InputRequestWindowSeconds;
                if (hasWieldedShield)
                {
                    _shieldBashGuardUntil = now
                        + ShieldBashGuardWindowSeconds;
                }
            }

            // This is the same EventControlFlag used by the player's Kick key.
            // If the AI is currently blocking with a shield, Bannerlord turns
            // this input into a shield bash; otherwise it performs a kick.
            eventFlag |= Agent.EventControlFlag.Kick;
            MaintainShieldGuardDuringBash(now, ref movementFlag);
        }

        private void MaintainShieldGuardDuringBash(
            float now,
            ref Agent.MovementControlFlag movementFlag)
        {
            if (now > _shieldBashGuardUntil || !HasWieldedShield())
                return;

            // Supply the native block input while starting the bash and while
            // its alternative-attack animation is actually active. This is
            // equivalent to the player holding Block while pressing Kick: the
            // shield remains a real directional collision surface rather than
            // granting blanket invulnerability. Stop as soon as the bash ends.
            bool isStartingBash = now <= _requestKickUntil;
            bool isPerformingBash = IsPerformingAlternativeAttack(Agent);
            if (!isStartingBash && !isPerformingBash)
                return;

            Agent.MovementControlFlag defendFlag =
                Agent.GetDefendMovementFlag()
                & Agent.MovementControlFlag.DefendMask;
            if ((defendFlag & Agent.MovementControlFlag.DefendDirMask)
                == Agent.MovementControlFlag.None)
            {
                defendFlag |= Agent.MovementControlFlag.DefendAuto;
            }

            defendFlag |= Agent.MovementControlFlag.DefendBlock;
            movementFlag &= ~(Agent.MovementControlFlag.AttackMask
                | Agent.MovementControlFlag.DefendMask);
            movementFlag |= defendFlag;
        }

        private bool HasWieldedShield()
        {
            MissionWeapon offhandWeapon = Agent.WieldedOffhandWeapon;
            return !offhandWeapon.IsEmpty
                && offhandWeapon.CurrentUsageItem != null
                && offhandWeapon.CurrentUsageItem.IsShield;
        }

        private bool CanSupplyKickInput(float now)
        {
            if (!Agent.IsActive()
                || !Agent.IsAIControlled
                || !Agent.IsHuman
                || Agent.MountAgent != null
                || Agent.IsUsingGameObject)
            {
                return false;
            }

            // Keep supplying the pressed input briefly. A single frame is very
            // often rejected because the AI is still finishing an attack or
            // block transition; the engine accepts it on the first legal frame.
            if (now <= _requestKickUntil)
                return true;

            if (now < _nextKickTime
                || now < _nextTargetProbeTime
                || Agent.Team == null)
                return false;

            Agent? target = Agent.ImmediateEnemy;
            if (!IsValidCloseEnemy(target))
                target = Agent.GetTargetAgent();
            if (!IsValidCloseEnemy(target))
            {
                target = Agent.Mission.GetClosestEnemyAgent(
                    Agent.Team,
                    Agent.Position,
                    MaximumTargetDistance);
            }

            if (IsValidCloseEnemy(target))
                return IsTacticalAlternativeAttackTarget(target!);

            // When formations are still approaching each other, do not run a
            // native closest-enemy query on every AI frame for every soldier.
            _nextTargetProbeTime = now + FailedTargetProbeCooldownSeconds;
            return false;
        }

        private bool IsValidCloseEnemy(Agent? target)
        {
            return target != null
                && target.IsActive()
                && target.IsHuman
                && Agent.IsEnemyOf(target)
                && Agent.Position.DistanceSquared(target.Position)
                    <= MaximumTargetDistanceSquared;
        }

        private bool IsTacticalAlternativeAttackTarget(Agent target)
        {
            // Kicks are a guard-break tool, not a replacement for sword
            // attacks: kick an opponent who is actively defending. A shield
            // bearer may also bash while already defending against somebody
            // crowding inside facehug range, which creates space without
            // throwing away an ordinary attack already in progress.
            return IsAgentDefending(target)
                || (Agent.Position.DistanceSquared(target.Position)
                        <= FacehugDistanceSquared
                    && IsAgentDefending(Agent));
        }

        private static bool IsAgentDefending(Agent agent)
        {
            // Check both combat animation channels because the action set can
            // place the defend action on either one.
            return IsDefendAction(agent.GetCurrentActionType(0))
                || IsDefendAction(agent.GetCurrentActionType(1));
        }

        private static bool IsDefendAction(Agent.ActionCodeType actionType)
        {
            return actionType >= Agent.ActionCodeType.DefendAllBegin
                && actionType < Agent.ActionCodeType.DefendAllEnd;
        }

        internal static bool IsPerformingAlternativeAttack(Agent? agent)
        {
            return agent != null
                && (IsAlternativeAttackAction(agent.GetCurrentActionType(0))
                    || IsAlternativeAttackAction(
                        agent.GetCurrentActionType(1)));
        }

        private static bool IsAlternativeAttackAction(
            Agent.ActionCodeType actionType)
        {
            return actionType >= Agent.ActionCodeType.AlternativeAttackAllBegin
                && actionType < Agent.ActionCodeType.AlternativeAttackAllEnd;
        }
    }
}
