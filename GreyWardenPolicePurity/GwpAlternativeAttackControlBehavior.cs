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
        private readonly Dictionary<int, Dictionary<SkillObject, int>> _masteryByAgentIndex =
            new();
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

        internal static int GetMasteryBonus(Agent? agent, SkillObject skill)
        {
            if (agent == null)
                return 0;

            GwpAlternativeAttackControlBehavior? behavior = agent.Mission?
                .GetMissionBehavior<GwpAlternativeAttackControlBehavior>();
            return behavior != null
                && behavior._masteryByAgentIndex.TryGetValue(
                    agent.Index,
                    out Dictionary<SkillObject, int>? skills)
                && skills.TryGetValue(skill, out int bonus)
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

            // Mastery is earned by a kick or bash that lands. The engine raises this
            // event once per contact with damage: the native hit, or the one-point
            // fallback contact this behavior injects when the native action missed,
            // never both. The AI's kick input repeats every frame of the action, so it
            // must never be what earns mastery (2026-09-24).
            if (affectedAgent != null
                && affectorAgent != null
                && affectorAgent.IsHuman
                && affectorAgent.IsEnemyOf(affectedAgent)
                && IsAlternativeAttack(in attackCollisionData, in blow)
                && GwpKickBehavior.IsEligibleGreyWarden(affectorAgent))
            {
                AddMeleeMastery(affectorAgent);
            }

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

            SkillObject? skill = shooterAgent.Equipment[weaponIndex].CurrentUsageItem?.RelevantSkill;
            if (skill != DefaultSkills.Bow && skill != DefaultSkills.Crossbow && skill != DefaultSkills.Throwing)
                return;

            AddMasteryBonus(shooterAgent, skill,
                skill == DefaultSkills.Throwing ? GwpTuning.BattleGrowth.MasteryPerThrow : GwpTuning.BattleGrowth.MasteryPerShot);
            shooterAgent.UpdateAgentStats();
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent == null
                || !affectedAgent.IsHuman
                || agentState != AgentState.Killed && agentState != AgentState.Unconscious)
            {
                return;
            }

            // 骑着的马冲撞算骑手的。
            Agent? killer = affectorAgent?.IsMount == true
                ? affectorAgent.RiderAgent
                : affectorAgent;
            if (killer == null
                || !killer.IsHuman
                || !killer.IsEnemyOf(affectedAgent)
                || !GwpKickBehavior.IsEligibleGreyWarden(killer))
            {
                return;
            }

            // 用致命一击的武器类别，不能读此刻手里的武器（射出后可能已经换武器）。
            SkillObject? skill = affectorAgent?.IsMount == true ? DefaultSkills.Riding
                : blow.AttackType == AgentAttackType.Kick ? DefaultSkills.Athletics
                : blow.AttackType == AgentAttackType.Bash ? DefaultSkills.OneHanded
                : WeaponComponentData.GetRelevantSkillFromWeaponClass((WeaponClass)blow.WeaponClass);
            if (skill == null) return;
            AddMasteryBonus(killer, skill,
                GwpCommon.IsGreyWardenLordAgent(killer) ? GwpTuning.BattleGrowth.MasteryPerLordKill : GwpTuning.BattleGrowth.MasteryPerSoldierKill);
            killer.UpdateAgentStats();
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            base.OnAgentDeleted(affectedAgent);
            if (affectedAgent == null)
                return;

            int index = affectedAgent.Index;
            _pendingActions.Remove(index);
            _masteryByAgentIndex.Remove(index);
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            _pendingActions.Clear();
            _masteryByAgentIndex.Clear();
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

        private void AddMeleeMastery(Agent attacker)
        {
            AddMasteryBonus(attacker, DefaultSkills.OneHanded, GwpTuning.BattleGrowth.MeleeMasteryPerAlternativeAction);
            AddMasteryBonus(attacker, DefaultSkills.Athletics, GwpTuning.BattleGrowth.MeleeMasteryPerAlternativeAction);
            attacker.UpdateAgentStats();
        }

        private void AddMasteryBonus(
            Agent agent,
            SkillObject skill,
            int amount)
        {
            int agentIndex = agent.Index;
            if (!_masteryByAgentIndex.TryGetValue(agentIndex, out Dictionary<SkillObject, int>? bonuses))
                _masteryByAgentIndex[agentIndex] = bonuses = new Dictionary<SkillObject, int>();
            int current = bonuses.TryGetValue(skill, out int existing)
                ? existing
                : 0;
            // 本场临时值；原版各项效果自行限幅，不另设模组累计/技能上限（用户裁定2026-10-20）。
            bonuses[skill] = current + amount;
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
