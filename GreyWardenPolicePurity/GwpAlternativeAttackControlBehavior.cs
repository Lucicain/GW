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
        private readonly Dictionary<int, int> _meleeMasteryBonusByAgentIndex =
            new();
        private readonly Dictionary<int, int> _bowMasteryBonusByAgentIndex =
            new();
        private Agent? _observedPlayerAgent;
        private bool _playerActionObserved;

        private const int MeleeMasteryPerAlternativeAction = 50;
        private const int BowMasteryPerArrow = 10;

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

        internal static int GetMeleeMasteryBonus(Agent? agent)
        {
            if (agent == null)
                return 0;

            GwpAlternativeAttackControlBehavior? behavior = agent.Mission?
                .GetMissionBehavior<GwpAlternativeAttackControlBehavior>();
            return behavior != null
                && behavior._meleeMasteryBonusByAgentIndex.TryGetValue(
                    agent.Index,
                    out int bonus)
                        ? bonus
                        : 0;
        }

        internal static int GetBowMasteryBonus(Agent? agent)
        {
            if (agent == null)
                return 0;

            GwpAlternativeAttackControlBehavior? behavior = agent.Mission?
                .GetMissionBehavior<GwpAlternativeAttackControlBehavior>();
            return behavior != null
                && behavior._bowMasteryBonusByAgentIndex.TryGetValue(
                    agent.Index,
                    out int bonus)
                        ? bonus
                        : 0;
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

        public override void OnAgentShootMissile(
            Agent shooterAgent,
            EquipmentIndex weaponIndex,
            TaleWorlds.Library.Vec3 position,
            TaleWorlds.Library.Vec3 velocity,
            TaleWorlds.Library.Mat3 orientation,
            bool hasRigidBody,
            int forcedMissileIndex)
        {
            base.OnAgentShootMissile(
                shooterAgent,
                weaponIndex,
                position,
                velocity,
                orientation,
                hasRigidBody,
                forcedMissileIndex);

            if (shooterAgent == null
                || !shooterAgent.IsHuman
                || !GwpKickBehavior.IsEligibleGreyWarden(shooterAgent))
            {
                return;
            }

            MissionWeapon bow = shooterAgent.Equipment[weaponIndex];
            if (bow.CurrentUsageItem?.RelevantSkill != DefaultSkills.Bow)
                return;

            if (AddMasteryBonus(
                    _bowMasteryBonusByAgentIndex,
                    shooterAgent.Index,
                    BowMasteryPerArrow))
            {
                shooterAgent.UpdateAgentStats();
            }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            base.OnAgentDeleted(affectedAgent);
            if (affectedAgent == null)
                return;

            int index = affectedAgent.Index;
            _pendingActions.Remove(index);
            _meleeMasteryBonusByAgentIndex.Remove(index);
            _bowMasteryBonusByAgentIndex.Remove(index);
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            _pendingActions.Clear();
            _meleeMasteryBonusByAgentIndex.Clear();
            _bowMasteryBonusByAgentIndex.Clear();
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

            // Mastery belongs to the deliberate alternative-attack action,
            // not to whichever native or fallback contact later resolves it.
            // Award exactly once when the action is accepted into this shared
            // AI/player resolver, even if no nearby target is available.
            AddMeleeMastery(attacker);

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

        private void AddMeleeMastery(Agent attacker)
        {
            if (AddMasteryBonus(
                    _meleeMasteryBonusByAgentIndex,
                    attacker.Index,
                    MeleeMasteryPerAlternativeAction))
            {
                attacker.UpdateAgentStats();
            }
        }

        private static bool AddMasteryBonus(
            IDictionary<int, int> bonuses,
            int agentIndex,
            int amount)
        {
            int current = bonuses.TryGetValue(agentIndex, out int existing)
                ? existing
                : 0;
            int updated = Math.Min(
                GwpAgentStatCalculateModel.MasteredSkillValue,
                current + amount);
            if (updated == current)
                return false;

            bonuses[agentIndex] = updated;
            return true;
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
            // action began. Do not repeat its distance test after the short
            // observation window.
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
