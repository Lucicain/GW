using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    // One independent three-chance rescue per side, only in a player-present mission.
    public sealed class GwpBattleReinforcementBehavior : MissionBehavior
    {
        private sealed class SideSupport
        {
            internal readonly GwpBattleSupportChecks Checks = new();
            internal Team Team = null!;
            internal IAgentOriginBase Owner = null!;
            internal MatrixFrame Frame;
            internal readonly Queue<int> Troops = new();
            internal int Remaining => Troops.Count;
            internal float NextBatch;
            internal bool Notified;
        }
        private readonly SideSupport[] _sides = { new(), new() };
        private readonly BasicCharacterObject _infantry, _archer, _cavalry;
        private GwpBattleSceneContext? _context;
        private readonly bool[] _sideChanged = new bool[2];
        private bool _started, _disposed;
        private float _time, _hornStopAt;
        private int _hornPlayed;
        private SoundEvent? _horn;
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        internal bool HasPendingReinforcementSpawn(BattleSideEnum side) =>
            side != BattleSideEnum.None && _sides[(int)side].Remaining > 0;
        internal bool HasLivingSupport(BattleSideEnum side)
        {
            if (_disposed) return false;
            foreach (Agent agent in Mission.Agents)
                if (agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && agent.Team?.Side == side
                    && agent.Origin is GwpBattleSupportOrigin) return true;
            return false;
        }

        public GwpBattleReinforcementBehavior(BasicCharacterObject infantry,
            BasicCharacterObject archer, BasicCharacterObject cavalry)
        { _infantry = infantry; _archer = archer; _cavalry = cavalry; }

        public override void OnMissionTick(float dt)
        {
            if (_disposed) return;
            if (Mission.MissionEnded || Mission.MissionResult != null) { Stop(); return; }
            _context ??= Mission.GetMissionBehavior<GwpBattleSceneContext>();
            if (_context == null || !_context.TryCapture() || !_context.Spawn!.IsDeploymentOver) return;
            _time += dt;
            if (!_started)
            {
                _started = true;
                Evaluate(BattleSideEnum.Defender, null);
                Evaluate(BattleSideEnum.Attacker, null);
            }
            if (_horn != null && _time >= _hornStopAt)
            {
                StopHorn();
                if (_hornPlayed < 2) StartHorn();
            }
            foreach (SideSupport side in _sides)
            {
                if (side.Remaining <= 0 || _time < side.NextBatch) continue;
                SpawnBatch(side);
                side.NextBatch = _time + .6f;
            }
            for (int i = 0; i < _sideChanged.Length; i++)
                if (_sideChanged[i]) { _sideChanged[i] = false; Evaluate((BattleSideEnum)i, null); }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            // Native can finish supplying its last reserve without a casualty.
            // Recheck after the spawn callback has updated reserve counters.
            if (agent.IsHuman && agent.Team != null && agent.Team.Side != BattleSideEnum.None
                && agent.Origin is not GwpBattleSupportOrigin) _sideChanged[(int)agent.Team.Side] = true;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow blow)
        {
            if (!_started || _disposed || Mission.MissionEnded || Mission.MissionResult != null
                || affectedAgent?.Team == null || !affectedAgent.IsHuman) return;
            Evaluate(affectedAgent.Team.Side, affectedAgent);
        }

        private void Evaluate(BattleSideEnum sideId, Agent? removed)
        {
            if (sideId == BattleSideEnum.None) return;
            SideSupport side = _sides[(int)sideId];
            if (side.Checks.Complete) return;
            Agent? recipient = null;
            bool playerEligible = IsEligiblePlayer();
            int alive = 0;
            foreach (Agent agent in Mission.Agents)
            {
                if (agent == removed || !agent.IsHuman || !agent.IsActive() || agent.IsRunningAway
                    || agent.Team?.Side != sideId) continue;
                alive++;
                bool eligible = agent == Mission.MainAgent ? playerEligible
                    : GwpCommon.IsGreyWardenAffiliatedCharacter(agent.Character);
                if (eligible && agent.Origin != null && (recipient == null || agent == Mission.MainAgent)) recipient = agent;
            }
            int reserves = sideId == BattleSideEnum.Defender
                ? _context!.Spawn!.NumberOfRemainingDefenderTroops
                : _context!.Spawn!.NumberOfRemainingAttackerTroops;
            int oldAttempts = side.Checks.Attempts;
            // Player-side progression survives the player being incapacitated;
            // a remaining Warden can receive that side's rescue on their behalf.
            bool playerSide = sideId == Mission.PlayerTeam.Side && playerEligible;
            int reputation = playerSide && Campaign.Current != null ? PlayerBehaviorPool.Reputation : 100;
            int opening = _context!.OpeningCount(sideId);
            bool success = side.Checks.Try(opening, alive, reserves, recipient != null, () => MBRandom.RandomFloat, reputation);
#if GWP_DIAGNOSTICS
            if (side.Checks.Attempts != oldAttempts || success)
                GwpFaultTrace.Write("BATTLE_SCENE_SUPPORT", details:
                    $"side={sideId} opening={opening} alive={alive} reserves={reserves} attempts={side.Checks.Attempts} playerSide={playerSide} reputation={reputation} success={success}");
#endif
            if (!success)
            {
                if (oldAttempts == 0 && side.Checks.Attempts > 0 && recipient == Mission.MainAgent)
                    MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(
                        GwpText.Get("{=gwp_gwpbattlereinforcementbehavior_001}You pray in your heart for God's favor.")), 0);
                return;
            }
            side.Team = recipient!.Team;
            side.Owner = recipient.Origin;
            side.Frame = GetReinforcementFrame(recipient);
            BattleSideEnum enemySide = sideId == BattleSideEnum.Defender ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            float enemyPower = _context!.RemainingPower(enemySide);
            float share = GwpBattleScenePolicy.PowerShare(playerSide, reputation);
            float infantryPower = _infantry.GetPower(), archerPower = _archer.GetPower(), cavalryPower = _cavalry.GetPower();
            float plannedPower = 0;
            foreach (int kind in GwpBattleScenePolicy.Plan(enemyPower, share, infantryPower, archerPower, cavalryPower))
            {
                side.Troops.Enqueue(kind);
                plannedPower += kind == 0 ? infantryPower : kind == 1 ? archerPower : cavalryPower;
            }
#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("BATTLE_SCENE_SUPPORT", details:
                $"plan side={sideId} share={share:F3} enemyPower={enemyPower:F2} budget={enemyPower * share:F2} power={plannedPower:F2} count={side.Remaining}");
#endif
            side.NextBatch = _time;
            // First batch is synchronous so a last-survivor rescue reaches the
            // scene before the next simulation tick can end the battle.
            SpawnBatch(side);
            side.NextBatch = _time + .6f;
        }

        private bool IsEligiblePlayer()
        {
            if (Mission.MainAgent == null) return false;
            if (Campaign.Current == null)
                return GwpCommon.IsGreyWardenAffiliatedCharacter(Mission.MainAgent.Character);
            return Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()?.IsRecruitedByGreyWardens == true
                && PlayerBehaviorPool.Reputation >= 20;
        }

        private MatrixFrame GetReinforcementFrame(Agent recipient)
        {
            Vec3 position = recipient.Position;
            Vec2 edge = Mission.GetClosestBoundaryPosition(position.AsVec2);
            Vec2 facing = position.AsVec2 - edge;
            facing = facing.LengthSquared > .01f ? facing.Normalized() : Vec2.Forward;
            Mat3 rotation = Mat3.Identity;
            rotation.f = facing.ToVec3(); rotation.u = Vec3.Up;
            rotation.s = Vec3.CrossProduct(rotation.f, rotation.u).NormalizedCopy();
            // Keep the arrival inside the map rather than scattering half the
            // batch outside the boundary, and use local ground elevation.
            Vec2 arrival = edge + facing * 6f;
            if (!Mission.IsPositionInsideBoundaries(arrival)) arrival = position.AsVec2;
            return new MatrixFrame(rotation, arrival.ToVec3(position.Z));
        }

        private void SpawnBatch(SideSupport side)
        {
            if (Mission.MissionEnded || Mission.MissionResult != null) { side.Troops.Clear(); return; }
            int count = Math.Min(10, side.Remaining), spawned = 0, attempted = 0;
            Agent? first = null;
            for (int i = 0; i < count; i++)
            {
                // Leave room for both rider and horse; resume when space opens.
                if (_context!.Spawn!.NumberOfAgents + 2 > DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission) break;
                int kind = side.Troops.Dequeue();
                attempted++;
                BasicCharacterObject troop = kind == 0 ? _infantry : kind == 1 ? _archer : _cavalry;
                FormationClass formationClass = kind == 0 ? FormationClass.Infantry
                    : kind == 1 ? FormationClass.Ranged : FormationClass.Cavalry;
                try
                {
                    Vec2 offset = new Vec2(MBRandom.RandomFloatRanged(-1, 1), MBRandom.RandomFloatRanged(-1, 1));
                    if (offset.LengthSquared > .001f) offset = offset.Normalized() * MBRandom.RandomFloatRanged(1, 5);
                    Vec3 position = side.Frame.origin + new Vec3(offset.X, offset.Y, 0);
                    if (!Mission.IsPositionInsideBoundaries(position.AsVec2)) position = side.Frame.origin;
                    position.z = Mission.Scene.GetGroundHeightAtPosition(position);
                    Vec2 direction = side.Frame.rotation.f.AsVec2;
                    var data = new AgentBuildData(new GwpBattleSupportOrigin(troop, side.Owner))
                        .Team(side.Team).InitialPosition(in position).InitialDirection(in direction)
                        .Formation(side.Team.GetFormation(formationClass));
                    Agent agent = Mission.SpawnAgent(data);
                    if (agent == null) continue;
                    agent.SetWatchState(Agent.WatchState.Alarmed);
                    first ??= agent; spawned++;
                }
                catch (Exception error) { GwpFaultTrace.WriteQuiet(error); }
            }
            // Consume attempted slots, including failures: no infinite retry wave.
            if (first != null && !side.Notified)
            {
                side.Notified = true;
                Mission.GetMissionBehavior<GwpSyndicateMusicBehavior>()?.NotifyReinforcementArrival(side.Team.Side, spawned);
                bool friendly = side.Team.Side == Mission.PlayerTeam.Side;
                MBInformationManager.AddQuickInformation(new TaleWorlds.Localization.TextObject(GwpText.Get(friendly
                    ? "{=gwp_gwpbattlereinforcementbehavior_002}Grey Warden reinforcements have arrived!"
                    : "{=gwp_battle_support_enemy}Grey Warden reinforcements have joined the enemy!")), 0);
                if (_horn == null) { _hornPlayed = 0; StartHorn(); }
            }
#if GWP_DIAGNOSTICS
            if (attempted > 0)
                GwpFaultTrace.Write("BATTLE_SCENE_SUPPORT", details:
                    $"arrival side={side.Team.Side} spawned={spawned} remaining={side.Remaining}");
#endif
        }

        private void StartHorn()
        {
            if (Mission.Scene == null) return;
            int id = SoundEvent.GetEventIdFromString("gwp/support/horn");
            if (id < 0) return;
            _horn = SoundEvent.CreateEvent(id, Mission.Scene);
            _hornStopAt = _time + 4.5f; _hornPlayed++; _horn.Play();
        }
        private void StopHorn() { _horn?.Stop(); _horn?.Release(); _horn = null; }
        private void Stop()
        {
            if (_disposed) return;
            _disposed = true; StopHorn();
            foreach (SideSupport side in _sides) side.Troops.Clear();
        }
        protected override void OnEndMission() { Stop(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Stop(); base.OnRemoveBehavior(); }
    }

    // Manually spawned scene troops are intentionally absent from the native
    // supplier's campaign roster counters. They must still be allowed to fight
    // after its last original troop dies. Once they are gone native wins again.
    [HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.IsSideDepleted))]
    internal static class GwpBattleSupportDepletionPatch
    {
        [HarmonyPostfix]
        internal static void After(DefaultBattleMissionAgentSpawnLogic __instance, BattleSideEnum side, ref bool __result)
        {
            if (!__result || __instance.Mission.MissionEnded || __instance.Mission.MissionResult != null) return;
            if (__instance.Mission.GetMissionBehavior<GwpBattleReinforcementBehavior>()?.HasLivingSupport(side) == true)
                __result = false;
        }
    }
}
