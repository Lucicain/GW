using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>Battle events select lab states; the sample-clock renderer owns musical timing.</summary>
    public sealed class GwpSyndicateMusicBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        private static GwpSyndicateMusicBehavior? _owner;
        private DefaultBattleMissionAgentSpawnLogic? _spawn;
        private GwpMusicOutput? _output;
        private readonly GwpMusicBattlePolicy _policy = new();
        private bool _checked, _nativePaused, _battle, _outro, _disposed;
        private float _sampleTime, _elapsed, _emptyTime;
        private int _reinforcementCount;
        private BattleSideEnum _ourSide;
        private readonly HashSet<Agent> _countedCasualties = new();
#if GWP_DIAGNOSTICS
        private float _decisionTraceSeconds;
        private int _damageEvents, _casualtyEvents;
#endif
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        private static readonly uint ProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        public override void OnMissionTick(float dt)
        {
            if (_disposed) return;
            try
            {
                if (!_checked)
                {
                    if (Mission?.Agents == null || Mission.Agents.Count == 0 || Mission.PlayerTeam == null) return;
                    var context = Mission.GetMissionBehavior<GwpBattleSceneContext>();
                    if (context == null || !context.TryCapture()) return;
                    _spawn = context.Spawn!;
                    _checked = true;
                    if (!context.MusicEligible) return;
                    _ourSide = Mission.PlayerTeam.Side;
                    if (_ourSide == BattleSideEnum.None || MBMusicManager.Current == null) return;
                    string module = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
                    _owner?.Teardown();
                    _output = new GwpMusicOutput(Path.Combine(module, "Music", "Lab"));
                    MBMusicManager.Current.PauseMusicManagerSystem(); _nativePaused = true;
                    psai.net.PsaiCore.Instance.StopMusic(true, 0f);
                    _spawn.OnReinforcementsSpawned += NotifyReinforcementArrival;
                    _owner = this;
                    Pump();
                }
                if (_output == null || _outro) return;
                if (Mission.MissionResult != null || Mission.MissionEnded) { EndMusic("native battle result"); return; }
                if (!_spawn!.IsDeploymentOver) return;
                _elapsed += dt; _sampleTime += dt;
                if (_sampleTime < .5f) return;
                float sample = _sampleTime; _sampleTime = 0;
                ReadPower(out float ours, out float enemy);
                if (!_battle)
                {
                    // Initial spawn counters may be zero. Never initialize from that transient state.
                    if (ours <= 0 || enemy <= 0) return;
                    _battle = true; _policy.Initialize(ours, enemy);
                    _output.Want(_policy.Tier);
#if GWP_DIAGNOSTICS
                    TraceDecision(ours, enemy, "deployment-complete");
#endif
                    if (_reinforcementCount > 0) { _output.Reinforcement(); _reinforcementCount = 0; }
                }
                bool ourDefender = _ourSide == BattleSideEnum.Defender;
                int ourReserves = ourDefender ? _spawn.NumberOfRemainingDefenderTroops : _spawn.NumberOfRemainingAttackerTroops;
                int enemyReserves = ourDefender ? _spawn.NumberOfRemainingAttackerTroops : _spawn.NumberOfRemainingDefenderTroops;
                var support = Mission.GetMissionBehavior<GwpBattleReinforcementBehavior>();
                BattleSideEnum enemySide = ourDefender ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
                bool ended = (ours <= 0 && ourReserves == 0 && support?.HasPendingReinforcementSpawn(_ourSide) != true)
                    || (enemy <= 0 && enemyReserves == 0 && support?.HasPendingReinforcementSpawn(enemySide) != true);
                _emptyTime = ended ? _emptyTime + sample : 0;
                if (_elapsed >= 5 && _emptyTime >= 3) { EndMusic("no fighting force or reserves on one side"); return; }
                bool changed = _policy.Update(ours, enemy, sample);
                if (changed) _output.Want(_policy.Tier);
#if GWP_DIAGNOSTICS
                _decisionTraceSeconds += sample;
                if (changed || _decisionTraceSeconds >= 10)
                { _decisionTraceSeconds = 0; TraceDecision(ours, enemy, changed ? "tier-change" : "sample"); }
#endif
            }
            catch (Exception e) { Fail(e); }
        }
        private void ReadPower(out float ours, out float enemy)
        {
            ours = enemy = 0;
            foreach (Agent agent in Mission.Agents)
            {
                if (!agent.IsHuman || !agent.IsActive() || agent.Team == null || agent.Team.Side == BattleSideEnum.None || agent.IsRunningAway) continue;
                float power = Math.Max(.01f, agent.CharacterPowerCached);
                if (agent.Team.Side == _ourSide) ours += power; else enemy += power;
            }
        }
        private bool Tracks(Agent agent) => _battle && !_disposed && !_outro && agent != null
            && agent.IsHuman && agent.Team != null && agent.Team.Side != BattleSideEnum.None;
        public override void OnScoreHit(Agent affectedAgent, Agent affectorAgent, WeaponComponentData attackerWeapon,
            bool isBlocked, bool isSiegeEngineHit, in Blow blow, in AttackCollisionData collisionData,
            float damagedHp, float hitDistance, float shotDifficulty)
        {
            if (!Tracks(affectedAgent) || isBlocked || damagedHp <= 0 || affectorAgent?.Team == null
                || affectorAgent.Team.Side == BattleSideEnum.None || affectorAgent.Team.Side == affectedAgent.Team.Side) return;
            float fraction = Math.Min(1, damagedHp / Math.Max(1, affectedAgent.HealthLimit));
            _policy.RecordDamage(Math.Max(.01f, affectedAgent.CharacterPowerCached) * fraction);
#if GWP_DIAGNOSTICS
            _damageEvents++;
#endif
        }
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            if (!Tracks(affectedAgent) || (agentState != AgentState.Killed && agentState != AgentState.Unconscious)
                || !_countedCasualties.Add(affectedAgent)) return;
            _policy.RecordCasualty(Math.Max(.01f, affectedAgent.CharacterPowerCached));
#if GWP_DIAGNOSTICS
            _casualtyEvents++;
#endif
        }
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (Tracks(agent)) _policy.AddReinforcementPower(Math.Max(.01f, agent.CharacterPowerCached));
        }
        internal void NotifyReinforcementArrival(BattleSideEnum side, int count)
        {
            if (_disposed || _outro || count <= 0) return;
#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("SYNDICATE_BATTLE_DYNAMICS", details: $"reinforcement side={side} count={count}");
#endif
            if (!_battle) _reinforcementCount += count; else _output?.Reinforcement();
        }
        private void EndMusic(string reason)
        {
            _outro = true; _output?.Want("outro");
#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("SYNDICATE_BATTLE_DYNAMICS", details: "outro: " + reason);
#endif
        }
        // Application ticks run even while the mission is paused/in menus.
        internal static void Pump() => _owner?.PumpInstance();
        private void PumpInstance()
        {
            try
            {
                if (_output == null) return;
                if (_output.Failure != null) { Fail(_output.Failure); return; }
                if (Mission.Current != Mission) { Teardown(); return; }
                float gain = NativeConfig.DisableSound ? 0 : NativeOptions.GetConfig(NativeOptions.NativeOptionsType.MasterVolume)
                    * NativeOptions.GetConfig(NativeOptions.NativeOptionsType.MusicVolume);
                if (NativeOptions.GetConfig(NativeOptions.NativeOptionsType.KeepSoundInBackground) < .5f)
                {
                    GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
                    if (foreground != ProcessId) gain = 0;
                }
                _output.Volume = gain;
                _output.Paused = MissionState.Current?.Paused == true && Mission.Mode != MissionMode.Deployment;
            }
            catch (Exception e) { Fail(e); }
        }
        private void Fail(Exception e) { GwpFaultTrace.WriteQuiet(e); Teardown(); }
#if GWP_DIAGNOSTICS
        private void TraceDecision(float ours, float enemy, string trigger)
        {
            GwpFaultTrace.Write("SYNDICATE_BATTLE_DYNAMICS", details:
                $"{trigger} t={_elapsed:F1} ours={ours:F2} enemy={enemy:F2} ratio={enemy / Math.Max(.01f, ours):F3} "
                + $"hits={_damageEvents} casualties={_casualtyEvents} damage20={_policy.Damage20:F2} losses20={_policy.Casualties20:F2} "
                + $"exchange={_policy.Exchange:F3} recent5={_policy.RecentExchange:F3} attrition={_policy.Attrition:F3} pressure={_policy.Pressure:F3} "
                + $"quiet={_policy.QuietSeconds:F1} tension={_policy.Tension:F3} tier={_policy.Tier} reason={_policy.Reason}");
        }
#endif
        private void Teardown()
        {
            if (_disposed) return;
            _disposed = true;
            _countedCasualties.Clear();
            if (_spawn != null) _spawn.OnReinforcementsSpawned -= NotifyReinforcementArrival;
            if (_owner == this) _owner = null;
            try { _output?.Dispose(); }
            catch (Exception e) { GwpFaultTrace.WriteQuiet(e); }
            finally
            {
                _output = null;
                if (_nativePaused) { _nativePaused = false; MBMusicManager.Current?.UnpauseMusicManagerSystem(); }
            }
        }
        protected override void OnEndMission() { Teardown(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Teardown(); base.OnRemoveBehavior(); }
        public override void OnMissionStateFinalized() { Teardown(); base.OnMissionStateFinalized(); }
    }
}
