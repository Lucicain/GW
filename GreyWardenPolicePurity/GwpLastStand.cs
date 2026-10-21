using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍锁血：领主10秒，新兵1秒、轻步兵2秒、重步兵/弓箭手4秒（用户裁定2026-10-21）；致命伤留1血。
    /// 这段时间内亲手（含骑着的马冲撞）杀死或打晕一个敌人，就按锁血以来对敌人造成的实际伤害回血，锁血结束，
    /// 之后再挨致命伤还能再锁；时限内没杀到人就放开锁血，留着 1 血，这一场不再触发。
    /// 锁血期间当前有效技能乘 <see cref="GwpTuning.LastStand.SkillMultiplier"/>（<see cref="IsStanding"/>）。
    /// 状态只属于这一个场景（场景结束即消失），不进存档。
    /// </summary>
    internal sealed class GwpLastStandBehavior : MissionBehavior
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
                || collision.AttackBlockedWithShield || collision.CollidedWithShieldOnBack || collision.MissileBlockedWithWeapon)
                return damage;
            float seconds = DurationFor(victim);
            if (seconds <= 0f) return damage;
            GwpLastStandBehavior? behavior = victim.Mission?.GetMissionBehavior<GwpLastStandBehavior>();
            return behavior == null ? damage : behavior.Limit(victim, damage, seconds);
        }

        // 按精确兵种给窗口；狂信者和骑士（包括下马）不授予。
        internal static float DurationFor(Agent? agent)
        {
            if (agent?.IsHuman != true) return 0f;
            if (GwpCommon.IsGreyWardenLordAgent(agent)) return GwpTuning.LastStand.LordSeconds;
            BasicCharacterObject? character = agent.Character;
            if (character == null || character.IsHero) return 0f;
            return character.StringId switch
            {
                GwpIds.NewRecruitId => GwpTuning.LastStand.RecruitSeconds,
                GwpIds.PoliceRecruitId => GwpTuning.LastStand.LightInfantrySeconds,
                GwpIds.HeavyInfantryId or GwpIds.ArcherId => GwpTuning.LastStand.EliteTroopSeconds,
                _ => 0f
            };
        }

        /// <summary>这个Agent此刻是否在锁血中（当前有效技能按倍数读取）。</summary>
        internal static bool IsStanding(Agent? agent)
        {
            GwpLastStandBehavior? behavior = agent?.Mission?.GetMissionBehavior<GwpLastStandBehavior>();
            return behavior != null && behavior._active.TryGetValue(agent!, out Stand? stand)
                && behavior.Mission.CurrentTime < stand.EndsAt;
        }

        private float Limit(Agent victim, float damage, float seconds)
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
            _active[victim] = new Stand { EndsAt = now + seconds };
            victim.UpdateAgentStats();
#if GWP_DIAGNOSTICS
            TraceStand(victim, "LAST_STAND_START",
                "seconds=" + seconds + "; endsAt=" + (now + seconds) + "; health=" + victim.Health + "; incoming=" + damage + "; allowed=" + Math.Min(damage, survivable));
#endif
            return Math.Min(damage, survivable);
        }

        public override void OnMissionTick(float dt)
        {
            if (_active.Count == 0)
                return;
            float now = Mission.CurrentTime;
            foreach (Agent fighter in _active.Where(entry => now >= entry.Value.EndsAt).Select(entry => entry.Key).ToList())
                Release(fighter);
        }

        /// <summary>锁血以来对敌人造成的实际扣血（被挡下的不算；致命一击在原版先报命中、后报阵亡，所以算在内）。</summary>
        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon,
            bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData,
            float damagedHp, float hitDistance, float shotDifficulty)
        {
            if (_active.Count == 0 || isBlocked || damagedHp <= 0f)
                return;
            Agent? fighter = Striker(affectorAgent);
            if (fighter != null && _active.TryGetValue(fighter, out Stand? stand) && affectedAgent.IsEnemyOf(fighter))
                stand.Damage += damagedHp;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            _active.Remove(affectedAgent);
            _spent.Remove(affectedAgent);
            if (_active.Count == 0 || !affectedAgent.IsHuman
                || agentState != AgentState.Killed && agentState != AgentState.Unconscious)
                return;
            Agent? fighter = Striker(affectorAgent);
            if (fighter == null || !_active.TryGetValue(fighter, out Stand? stand)
                || Mission.CurrentTime >= stand.EndsAt || !affectedAgent.IsEnemyOf(fighter))
                return;
            _active.Remove(fighter);
            fighter.UpdateAgentStats();
            fighter.Health = Math.Min(fighter.HealthLimit, fighter.Health + stand.Damage);
#if GWP_DIAGNOSTICS
            TraceStand(fighter, "LAST_STAND_HEAL",
                "victim=" + affectedAgent.Index + "; state=" + agentState + "; healed=" + stand.Damage + "; health=" + fighter.Health);
#endif
        }

        private void Release(Agent fighter)
        {
            _active.Remove(fighter);
            _spent.Add(fighter);
            fighter.UpdateAgentStats();
#if GWP_DIAGNOSTICS
            TraceStand(fighter, "LAST_STAND_EXPIRED", "health=" + fighter.Health);
#endif
        }

#if GWP_DIAGNOSTICS
        // 每个兵种/英雄每种转换只取前三例，验收该锁血功能后退休。
        private readonly Dictionary<string, int> _traceSamples = new();
        private void TraceStand(Agent agent, string stage, string details)
        {
            string key = agent.Character.StringId + ":" + stage;
            _traceSamples.TryGetValue(key, out int count);
            if (count >= 3) return;
            _traceSamples[key] = count + 1;
            GwpAiDiagnostics.WriteCombat(stage, "character=" + agent.Character.StringId + "; agent=" + agent.Index
                + "; time=" + Mission.CurrentTime + "; " + details);
        }
#endif

        // 骑着的马冲撞算骑手的。
        private static Agent? Striker(Agent? affector) =>
            affector?.IsMount == true ? affector.RiderAgent : affector;
    }
}
