#if GWP_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // Temporary, observation only. Never call GetAIWeight to inspect it: some
    // native scorers also change their orders while calculating their score.
    internal sealed class GwpBattleCommandTrace : MissionBehavior
    {
        private sealed class Score
        {
            internal float Time, Native, Factor, Preserve;
        }
        private static GwpBattleCommandTrace? _current;
        private readonly Dictionary<BehaviorComponent, Score> _scores = new();
        private readonly Dictionary<Formation, List<Agent>> _units = new();
        private readonly Dictionary<Agent, int> _gripBlocks = new();
        private readonly Dictionary<Type, FieldInfo?> _states = new();
        private readonly HashSet<string> _weaponSamples = new();
        private static readonly FieldInfo? Tactic = AccessTools.Field(typeof(TeamAIComponent), "_currentTactic");
        private float _next;
        private bool _started, _failed;
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        internal static void Observe(BehaviorComponent behavior, float score)
        {
            GwpBattleCommandTrace? trace = _current;
            Formation? f = behavior.Formation;
            if (trace == null || trace._failed || f == null || f.Team.Mission != trace.Mission
                || trace.Mission.Mode != MissionMode.Battle || trace.Mission.MissionResult != null) return;
            if (!trace._scores.TryGetValue(behavior, out Score? value))
                trace._scores[behavior] = value = new Score();
            value.Time = trace.Mission.CurrentTime;
            value.Native = score;
            value.Factor = behavior.WeightFactor;
            value.Preserve = behavior.PreserveExpireTime;
        }

        internal static void NoteGripBlock(Agent agent)
        {
            GwpBattleCommandTrace? trace = _current;
            if (trace == null || trace._failed || agent.Mission != trace.Mission) return;
            trace._gripBlocks.TryGetValue(agent, out int count);
            trace._gripBlocks[agent] = count + 1;
        }

        public override void OnMissionTick(float dt)
        {
            if (_failed || GameNetwork.IsMultiplayer || Mission.MainAgent == null
                || Mission.Mode != MissionMode.Battle || Mission.MissionEnded || Mission.MissionResult != null) return;
            _current = this;
            if (Mission.CurrentTime < _next) return;
            _next = Mission.CurrentTime + 2f;
            try
            {
                if (!_started)
                {
                    _started = true;
                    GwpFaultTrace.Write("BATTLE_COMMAND_SESSION", details:
                        $"mvid={typeof(GwpBattleCommandTrace).Assembly.ManifestModule.ModuleVersionId} sample=2s");
                }
                _units.Clear();
                foreach (Agent agent in Mission.Agents)
                {
                    if (!agent.IsHuman || !agent.IsActive() || agent.Formation == null) continue;
                    if (!_units.TryGetValue(agent.Formation, out List<Agent>? units))
                        _units[agent.Formation] = units = new List<Agent>();
                    units.Add(agent);
                }
                foreach (var pair in _units)
                {
                    // Infantry orders are needed to explain joint tactic flips.
                    Sample(pair.Key, pair.Value);
                }
                _gripBlocks.Clear();
            }
            catch (Exception error) { _failed = true; GwpFaultTrace.WriteQuiet(error); }
        }

        private void Sample(Formation f, List<Agent> agents)
        {
            int grey = 0, ranged = 0, ammo = 0, fired = 0, mayFire = 0, locked = 0, pending = 0, fleeing = 0, gripBlocks = 0;
            int shields = 0, greyShields = 0;
            var details = new List<string>();
            foreach (Agent a in agents)
            {
                bool isGrey = GwpCommon.IsGreyWardenAffiliatedCharacter(a.Character);
                if (isGrey) grey++;
                if (a.HasShieldCached) { shields++; if (isGrey) greyShields++; }
                SampleBladeClassification(a);
                EquipmentIndex hand = a.GetPrimaryWieldedItemIndex();
                if (hand != EquipmentIndex.None && !a.Equipment[hand].IsEmpty
                    && a.Equipment[hand].CurrentUsageItem?.IsRangedWeapon == true) ranged++;
                if (a.IsRangedCached) ammo++;
                if (MBCommon.GetTotalMissionTime() - a.LastRangedAttackTime < 2f) fired++;
                if (a.GetFiringOrder() == (int)FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill) mayFire++;
                if (a.IsRunningAway) fleeing++;
                if (_gripBlocks.TryGetValue(a, out int blocks)) gripBlocks += blocks;
                GwpDualBladeAgentState? s = GwpDualBladeAgents.Find(a);
                if (s == null || !s.HasRangedAlternative) continue;
                if (!GwpDualBladeActionGate.CanChangeWeapons(a)) locked++;
                if (s.RangedRequested) pending++;
                if (details.Count < 3)
                    details.Add($"{a.Index}:hand={hand}/{a.GetOffhandWieldedItemIndex()},step={s.CurrentStep},"
                        + $"pending={s.RangedRequested}/{s.RangedWieldPending},ticks={s.RangedRequestedTicks},"
                        + $"meleeTicks={s.TicksSinceMelee},action={a.GetCurrentActionType(0)}/{a.GetCurrentActionType(1)},"
                        + $"input={a.EventControlFlags},ammo={(s.AmmoSlot == EquipmentIndex.None ? -1 : a.Equipment[s.AmmoSlot].Amount)}");
            }
            BehaviorComponent? active = f.AI?.ActiveBehavior;
            string state = "-";
            if (active != null)
            {
                Type type = active.GetType();
                if (!_states.TryGetValue(type, out FieldInfo? field))
                    _states[type] = field = AccessTools.Field(type, "_behaviorState");
                state = field?.GetValue(active)?.ToString() ?? "-";
            }
            FormationQuerySystem q = f.QuerySystem;
            Formation? enemy = q.ClosestSignificantlyLargeEnemyFormation?.Formation;
            string scores = string.Join(";", _scores.Where(p => p.Key.Formation == f && Mission.CurrentTime - p.Value.Time < 3f)
                .OrderByDescending(p => p.Value.Native * p.Value.Factor).Take(5)
                .Select(p => $"{p.Key.GetType().Name}:{p.Value.Native:F3}*{p.Value.Factor:F3},preserveLeft={p.Value.Preserve - Mission.CurrentTime:F1},age={Mission.CurrentTime - p.Value.Time:F1}"));
            GwpFaultTrace.Write("BATTLE_COMMAND_SAMPLE", details:
                $"t={Mission.CurrentTime:F1} team={f.Team.TeamIndex}/{f.Team.Side} formation={f.FormationIndex}/{f.PhysicalClass} ai={f.IsAIControlled} "
                + $"tactic={(f.Team.TeamAI == null ? "none" : Tactic?.GetValue(f.Team.TeamAI)?.GetType().Name ?? "unknown")} "
                + $"behavior={active?.GetType().Name ?? "none"}/{state} order={f.GetReadonlyMovementOrderReference().OrderEnum} "
                + $"pos={f.CachedAveragePosition} goal={f.OrderPosition} velocity={f.CachedCurrentVelocity} "
                + $"arrangement={f.ArrangementOrder.OrderEnum} interval={f.Interval:F2} width={f.Width:F2} maxWidth={f.MaximumWidth:F2} "
                + $"enemy={(enemy == null ? "none" : enemy.Team.TeamIndex + "/" + enemy.FormationIndex)} "
                + $"distance={(enemy == null ? -1f : (enemy.CachedAveragePosition - f.CachedAveragePosition).Length):F1} "
                + $"range={q.MaximumMissileRangeReadOnly:F1}/{q.MissileRangeAdjustedReadOnly:F1} "
                + $"nativeFireRatio={q.MakingRangedAttackRatioReadOnly:F3} n={agents.Count} grey={grey} "
                + $"rangedInHand={ranged} usableRanged={ammo} hasShield={shields} greyHasShield={greyShields} "
                + $"nativeShieldRatio={q.HasShieldUnitRatioReadOnly:F3} nativeHasShield={q.HasShieldReadOnly} fired2s={fired} fireAtWill={mayFire} "
                + $"weaponLocked={locked} pendingBow={pending} gripBlocks2s={gripBlocks} fleeing={fleeing} scores=[{scores}] agents=[{string.Join(";", details)}]");
        }

        private void SampleBladeClassification(Agent agent)
        {
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                MissionWeapon weapon = agent.Equipment[slot];
                string? id = weapon.Item?.StringId;
                if (weapon.IsEmpty || (id != GwpIds.DualBladeMainhandItemId
                    && id != GwpIds.DualBladeOffhandItemId && id != GwpIds.DualBladeOffhandAiItemId)) continue;
                for (int usage = 0; usage < weapon.WeaponsCount; usage++)
                {
                    WeaponComponentData data = weapon.GetWeaponComponentDataForUsage(usage);
                    // Capture actual mission data once per distinct configuration, not just XML.
                    string key = $"{id}/{usage}/{data.WeaponClass}/{data.WeaponFlags}/{data.MaxDataValue}";
                    if (!_weaponSamples.Add(key)) continue;
                    GwpFaultTrace.Write("BATTLE_WEAPON_CLASSIFY", details:
                        $"t={Mission.CurrentTime:F1} agent={agent.Index} item={id} slot={slot} usage={usage} "
                        + $"itemType={weapon.Item?.ItemType} class={data.WeaponClass} flags={data.WeaponFlags} "
                        + $"isMelee={data.IsMeleeWeapon} isShield={data.IsShield} maxData={data.MaxDataValue} "
                        + $"agentHasShield={agent.HasShieldCached} agentIsRanged={agent.IsRangedCached}");
                }
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_current == this) _current = null;
            _scores.Clear(); _units.Clear(); _states.Clear(); _gripBlocks.Clear(); _weaponSamples.Clear();
        }

        internal static void ObserveTactic(TacticComponent tactic, Team team, Formation? infantry,
            TacticalPosition? point, float score)
        {
            var trace = _current;
            if (trace == null || trace._failed || trace.Mission != team.Mission
                || trace.Mission.Mode != MissionMode.Battle || trace.Mission.MissionEnded || trace.Mission.MissionResult != null) return;
            float now = trace.Mission.CurrentTime;
            GwpFaultTrace.Write("BATTLE_TACTIC_SCORE", details:
                $"t={now:F2} team={team.TeamIndex}/{team.Side} tactic={tactic.GetType().Name} "
                + $"nativeWeight={score:R} current={team.TeamAI.IsCurrentTactic(tactic)} defenseApplicable={team.TeamAI.IsDefenseApplicable} "
                + $"infantry={(infantry == null ? "none" : infantry.FormationIndex.ToString())} n={infantry?.CountOfUnits} "
                + $"arrangement={infantry?.ArrangementOrder.OrderEnum} interval={infantry?.Interval:F3} "
                + $"maxWidth={infantry?.MaximumWidth:F3} diameter={infantry?.UnitDiameter:F3} "
                + $"chokeWidth={point?.Width:F3} chokePosition={point?.Position.AsVec2}");
        }

    }

    [HarmonyPatch(typeof(BehaviorComponent), nameof(BehaviorComponent.GetAIWeight))]
    internal static class GwpBattleBehaviorScoreTrace
    {
        [HarmonyPostfix]
        private static void After(BehaviorComponent __instance, float __result)
        {
            try { GwpBattleCommandTrace.Observe(__instance, __result); }
            catch (Exception error) { GwpFaultTrace.WriteQuiet(error); }
        }
    }

    [HarmonyPatch]
    internal static class GwpBattleTacticScoreTrace
    {
        private static readonly FieldInfo? Infantry = AccessTools.Field(typeof(TacticComponent), "_mainInfantry");
        private static readonly FieldInfo? Choke = AccessTools.Field(typeof(TacticHoldChokePoint), "_chokePointTacticalPosition");
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(TacticHoldChokePoint), "GetTacticWeight");
            yield return AccessTools.Method(typeof(TacticDefensiveEngagement), "GetTacticWeight");
        }
        [HarmonyPostfix]
        private static void After(TacticComponent __instance, float __result)
        {
            try
            {
                GwpBattleCommandTrace.ObserveTactic(__instance, __instance.Team, Infantry?.GetValue(__instance) as Formation,
                    __instance is TacticHoldChokePoint ? Choke?.GetValue(__instance) as TacticalPosition : null, __result);
            }
            catch (Exception error) { GwpFaultTrace.WriteQuiet(error); }
        }
    }
}
#endif
