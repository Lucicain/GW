using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍领主锁血（用户裁定 2026-10-16）：一下致命伤只把血打到 1，之后 10 秒里血量不再低于 1。
    /// 这段时间内亲手（含骑着的马冲撞）杀死或打晕一个敌人，就按锁血以来对敌人造成的实际伤害回血，锁血结束，
    /// 之后再挨致命伤还能再锁；时限内没杀到人就放开锁血，留着 1 血，这一场不再触发。
    /// 锁血期间战斗成长乘 <see cref="GwpTuning.LordCombat.LastStandGrowthMultiplier"/>（<see cref="IsStanding"/>）。
    /// 状态只属于这一个场景（场景结束即消失），不进存档。
    /// </summary>
    internal sealed class GwpLordLastStandBehavior : MissionBehavior
    {
        private sealed class Stand
        {
            internal float EndsAt;
            internal float Damage;
        }

        private readonly Dictionary<Agent, Stand> _active = new Dictionary<Agent, Stand>();
        private readonly HashSet<Agent> _spent = new HashSet<Agent>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        /// 伤害模型最后一步（<see cref="GwpAgentApplyDamageModel.ApplyGeneralDamageModifiers"/>）。引擎把结果四舍五入后
        /// 从血量里扣，血量低于 1 就死（C#反编译 v1.4.8 Mission.GetAttackCollisionResults、Agent.HandleBlow）。
        /// 盾挡、背盾、兵器挡箭时这里算的是盾的伤害，不碰。
        /// </summary>
        internal static float LimitDamage(in AttackInformation info, in AttackCollisionData collision, float damage)
        {
            Agent? victim = info.VictimAgent;
            if (victim == null || damage <= 0f
                || collision.AttackBlockedWithShield || collision.CollidedWithShieldOnBack || collision.MissileBlockedWithWeapon
                || !GwpCommon.IsGreyWardenLordAgent(victim))
                return damage;
            GwpLordLastStandBehavior? behavior = victim.Mission?.GetMissionBehavior<GwpLordLastStandBehavior>();
            return behavior == null ? damage : behavior.Limit(victim, damage);
        }

        /// <summary>这位领主此刻是否在锁血中（战斗成长按倍数记）。</summary>
        internal static bool IsStanding(Agent? agent)
        {
            GwpLordLastStandBehavior? behavior = agent?.Mission?.GetMissionBehavior<GwpLordLastStandBehavior>();
            return behavior != null && behavior._active.TryGetValue(agent!, out Stand? stand)
                && behavior.Mission.CurrentTime < stand.EndsAt;
        }

        private float Limit(Agent victim, float damage)
        {
            float now = Mission.CurrentTime;
            float survivable = Math.Max(0f, (float)Math.Floor(victim.Health - 1f));
            if (_active.TryGetValue(victim, out Stand? stand))
            {
                if (now < stand.EndsAt)
                    return Math.Min(damage, survivable);
                Release(victim);
            }
            // 场景本身不让死（不死状态、对话、过场）时原版不扣血，不白白用掉这一次。
            if (_spent.Contains(victim) || victim.CurrentMortalityState != Agent.MortalityState.Mortal
                || victim.Health - (float)Math.Round(damage) >= 1f)
                return damage;
            _active[victim] = new Stand { EndsAt = now + GwpTuning.LordCombat.LastStandSeconds };
#if GWP_DIAGNOSTICS
            Mission.GetMissionBehavior<GwpKickBehavior>()?.TraceLordCombat(victim, "LAST_STAND_START",
                "health=" + victim.Health + "; incoming=" + damage + "; allowed=" + Math.Min(damage, survivable));
#endif
            return Math.Min(damage, survivable);
        }

        public override void OnMissionTick(float dt)
        {
            if (_active.Count == 0)
                return;
            float now = Mission.CurrentTime;
            foreach (Agent lord in _active.Where(entry => now >= entry.Value.EndsAt).Select(entry => entry.Key).ToList())
                Release(lord);
        }

        /// <summary>锁血以来对敌人造成的实际扣血（被挡下的不算；致命一击在原版先报命中、后报阵亡，所以算在内）。</summary>
        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon,
            bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData,
            float damagedHp, float hitDistance, float shotDifficulty)
        {
            if (_active.Count == 0 || isBlocked || damagedHp <= 0f)
                return;
            Agent? lord = Striker(affectorAgent);
            if (lord != null && _active.TryGetValue(lord, out Stand? stand) && affectedAgent.IsEnemyOf(lord))
                stand.Damage += damagedHp;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            _active.Remove(affectedAgent);
            _spent.Remove(affectedAgent);
            if (_active.Count == 0 || !affectedAgent.IsHuman
                || agentState != AgentState.Killed && agentState != AgentState.Unconscious)
                return;
            Agent? lord = Striker(affectorAgent);
            if (lord == null || !_active.TryGetValue(lord, out Stand? stand)
                || Mission.CurrentTime >= stand.EndsAt || !affectedAgent.IsEnemyOf(lord))
                return;
            _active.Remove(lord);
            lord.Health = Math.Min(lord.HealthLimit, lord.Health + stand.Damage);
#if GWP_DIAGNOSTICS
            Mission.GetMissionBehavior<GwpKickBehavior>()?.TraceLordCombat(lord, "LAST_STAND_HEAL",
                "victim=" + affectedAgent.Index + "; state=" + agentState + "; healed=" + stand.Damage + "; health=" + lord.Health);
#endif
        }

        private void Release(Agent lord)
        {
            _active.Remove(lord);
            _spent.Add(lord);
#if GWP_DIAGNOSTICS
            Mission.GetMissionBehavior<GwpKickBehavior>()?.TraceLordCombat(lord, "LAST_STAND_EXPIRED", "health=" + lord.Health);
#endif
        }

        // 骑着的马冲撞算骑手的。
        private static Agent? Striker(Agent? affector) =>
            affector?.IsMount == true ? affector.RiderAgent : affector;
    }
}
