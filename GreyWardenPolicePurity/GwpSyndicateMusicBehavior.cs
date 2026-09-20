using System;
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
                    _spawn = Mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
                    if (_spawn == null) { _checked = true; return; }
                    if (!HasGreyWardenPresence()) { if (_spawn.IsDeploymentOver) _checked = true; return; }
                    _checked = true;
                    _ourSide = Mission.PlayerTeam.Side;
                    if (_ourSide == BattleSideEnum.None || MBMusicManager.Current == null) return;
                    string module = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(SubModule).Assembly.Location)!, "../.."));
                    _owner?.Teardown();
                    _output = new GwpMusicOutput(Path.Combine(module, "Music", "Lab"));
                    MBMusicManager.Current.PauseMusicManagerSystem(); _nativePaused = true;
                    psai.net.PsaiCore.Instance.StopMusic(true, 0f);
                    _spawn.OnReinforcementsSpawned += NotifyReinforcementArrival;
                    _owner = this;
                    Trace("takeover intro; PCM unity gain; lab score; no PSAI channels");
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
                    _battle = true; _policy.Initialize(enemy / ours);
                    _output.Want(_policy.Tier); Trace($"deployment complete; ours={ours:F2} enemy={enemy:F2} tier={_policy.Tier}");
                    if (_reinforcementCount > 0) { _output.Reinforcement(); _reinforcementCount = 0; }
                }
                bool ourDefender = _ourSide == BattleSideEnum.Defender;
                int ourReserves = ourDefender ? _spawn.NumberOfRemainingDefenderTroops : _spawn.NumberOfRemainingAttackerTroops;
                int enemyReserves = ourDefender ? _spawn.NumberOfRemainingAttackerTroops : _spawn.NumberOfRemainingDefenderTroops;
                bool pendingWardens = Mission.GetMissionBehavior<GwpBattleReinforcementBehavior>()?.HasPendingReinforcementSpawn == true;
                bool ended = (ours <= 0 && ourReserves == 0 && !pendingWardens) || (enemy <= 0 && enemyReserves == 0);
                _emptyTime = ended ? _emptyTime + sample : 0;
                if (_elapsed >= 5 && _emptyTime >= 3) { EndMusic("no fighting force or reserves on one side"); return; }
                if (ours > 0 && enemy > 0 && _policy.Update(enemy / ours, sample))
                { _output.Want(_policy.Tier); Trace($"power ours={ours:F2} enemy={enemy:F2} ratio={enemy / ours:F3} tier={_policy.Tier}"); }
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
        internal void NotifyReinforcementArrival(BattleSideEnum side, int count)
        {
            if (_disposed || _outro || count <= 0) return;
            Trace($"reinforcement side={side} count={count}");
            if (!_battle) _reinforcementCount += count; else _output?.Reinforcement();
        }
        private void EndMusic(string reason) { _outro = true; _output?.Want("outro"); Trace("outro: " + reason); }
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
                while (_output.Notices.TryDequeue(out string? line)) Trace(line);
            }
            catch (Exception e) { Fail(e); }
        }
        private void Fail(Exception e) { GwpFaultTrace.WriteQuiet(e); Teardown(); }
        private static void Trace(string line)
        {
#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("SYNDICATE_MUSIC_V2", details: line);
#endif
        }
        private static bool HasGreyWardenPresence()
        {
            if (Campaign.Current != null)
            {
                if (string.Equals(Clan.PlayerClan?.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase)) return true;
                if (Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()?.IsRecruitedByGreyWardens == true) return true;
            }
            foreach (Agent agent in Mission.Current.Agents)
                if (agent.Character?.StringId?.StartsWith("gw", StringComparison.OrdinalIgnoreCase) == true) return true;
            return false;
        }
        private void Teardown()
        {
            if (_disposed) return;
            _disposed = true;
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
