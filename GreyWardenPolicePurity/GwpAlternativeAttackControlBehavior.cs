using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// One shared action resolver for both AI-controlled and player-controlled
    /// Grey Wardens. A native kick/bash hit is observed first and counts as
    /// one normal action outcome. If no native hit occurs, the one closest
    /// enemy receives the requested fallback knockback/knockdown decision.
    /// </summary>
    internal sealed class GwpAlternativeAttackControlBehavior : MissionBehavior
    {
        private const float NativeHitObservationWindowSeconds = 0.55f;
        private readonly Dictionary<int, PendingAlternativeAttack>
            _pendingActions = new();
        private Agent? _observedPlayerAgent;
        private bool _playerActionObserved;

        private sealed class PendingAlternativeAttack
        {
            internal readonly Agent Attacker;
            internal readonly float ResolveAt;
            internal readonly Agent? FallbackTarget;
            internal readonly HashSet<int> NativeHitTargetIndices = new();

            internal PendingAlternativeAttack(
                Agent attacker,
                float resolveAt,
                Agent? fallbackTarget)
            {
                Attacker = attacker;
                ResolveAt = resolveAt;
                FallbackTarget = fallbackTarget;
            }
        }

        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        internal static void BeginAction(Agent attacker)
        {
            if (attacker == null)
                return;

            GwpAlternativeAttackControlBehavior? behavior = attacker.Mission
                .GetMissionBehavior<GwpAlternativeAttackControlBehavior>();
            behavior?.QueueAction(attacker);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            ObservePlayerAlternativeAttack();

            if (_pendingActions.Count == 0)
                return;

            float now = Mission.CurrentTime;
            List<int>? readyActions = null;
            foreach (KeyValuePair<int, PendingAlternativeAttack> pair
                in _pendingActions)
            {
                if (now < pair.Value.ResolveAt)
                    continue;

                readyActions ??= new List<int>();
                readyActions.Add(pair.Key);
            }

            if (readyActions == null)
                return;

            foreach (int attackerIndex in readyActions)
            {
                if (!_pendingActions.TryGetValue(
                        attackerIndex,
                        out PendingAlternativeAttack? pending))
                {
                    continue;
                }

                // Remove before synthetic fallback contacts are registered:
                // their own alternative-attack callback must never be mistaken
                // for the action's real native hit.
                _pendingActions.Remove(attackerIndex);
                ResolveFallbackTargets(pending);
            }
        }

        public override void OnAgentHit(
            Agent affectedAgent,
            Agent affectorAgent,
            in MissionWeapon affectorWeapon,
            in Blow blow,
            in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(
                affectedAgent,
                affectorAgent,
                in affectorWeapon,
                in blow,
                in attackCollisionData);

            if (affectedAgent == null
                || affectorAgent == null
                || !_pendingActions.TryGetValue(
                    affectorAgent.Index,
                    out PendingAlternativeAttack? pending)
                || !IsAlternativeAttack(in attackCollisionData, in blow))
            {
                return;
            }

            // A real kick/bash contact already received the normal Grey
            // Warden 40/60/80% damage-model roll. One actual native contact
            // is enough: this action must not also inject a fallback hit.
            pending.NativeHitTargetIndices.Add(affectedAgent.Index);
        }

        private void ObservePlayerAlternativeAttack()
        {
            Agent? playerAgent = Mission.MainAgent;
            if (playerAgent != _observedPlayerAgent)
            {
                _observedPlayerAgent = playerAgent;
                _playerActionObserved = false;
            }

            if (playerAgent == null
                || playerAgent.IsAIControlled
                || !GwpKickBehavior.IsEligibleGreyWarden(playerAgent)
                || !GwpKickInputComponent
                    .IsPerformingAlternativeAttack(playerAgent))
            {
                _playerActionObserved = false;
                return;
            }

            if (_playerActionObserved)
                return;

            _playerActionObserved = true;
            QueueAction(playerAgent);
        }

        private void QueueAction(Agent attacker)
        {
            if (!attacker.IsActive()
                || !attacker.IsHuman
                || attacker.MountAgent != null
                || !GwpKickBehavior.IsEligibleGreyWarden(attacker)
                || _pendingActions.ContainsKey(attacker.Index))
            {
                return;
            }

            Agent? fallbackTarget = GwpAlternativeAttackControl
                .GetNearestEnemyTarget(attacker);
            if (fallbackTarget == null)
                return;

            _pendingActions.Add(
                attacker.Index,
                new PendingAlternativeAttack(
                    attacker,
                    Mission.CurrentTime + NativeHitObservationWindowSeconds,
                    fallbackTarget));
        }

        private static void ResolveFallbackTargets(
            PendingAlternativeAttack pending)
        {
            Agent attacker = pending.Attacker;
            if (!attacker.IsActive())
                return;

            if (pending.NativeHitTargetIndices.Count > 0)
                return;

            ApplyFallbackToTarget(
                attacker,
                pending.FallbackTarget);
        }

        private static void ApplyFallbackToTarget(
            Agent attacker,
            Agent? target)
        {
            if (target == null
                || !target.IsActive()
                || !target.IsHuman
                || target.MountAgent != null
                || !attacker.IsEnemyOf(target))
            {
                return;
            }

            // The candidate was selected inside two metres when this native
            // action began. Do not perform a second distance test after the
            // short hit-observation window: a tiny step during the animation
            // must not turn a guaranteed missed-action fallback into nothing.
            GwpAlternativeAttackControl.Apply(attacker, target);
        }

        private static bool IsAlternativeAttack(
            in AttackCollisionData collisionData,
            in Blow blow)
        {
            return collisionData.IsAlternativeAttack
                || blow.AttackType == AgentAttackType.Kick
                || blow.AttackType == AgentAttackType.Bash;
        }
    }
}
