using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Bannerlord's native water transition is safe for an ordinary weapon and
    /// for a dual-blade loadout with at most one blade drawn, but it crashes
    /// when both of our blades are still wielded.  The transition happens
    /// inside Agent.Tick, which Mission.TickAgentsAndTeamsImp drives, so the
    /// pair is put away immediately before that native tick.
    ///
    /// The guard has two stages instead of one.  Near the waterline only the
    /// off-hand blade goes away, which already leaves the loadout in the
    /// single-blade state that never crashes while costing the agent almost
    /// nothing - it keeps fighting with the main blade.  At the surface itself
    /// both blades go away, matching what vanilla does to a swimming agent in
    /// ClimbingMachineDetachment.  Both stages leave the items in their
    /// equipment slots; nothing is deleted and dropping is untouched.
    ///
    /// Every threshold is asymmetric and every release waits out a dwell time.
    /// The first version of this guard used one margin for both directions,
    /// which made the state flip every frame for a whole formation standing
    /// near a shoreline: 567 sheath and 569 re-wield calls in under two
    /// minutes, each one re-creating the blades' native meshes and physics.
    /// The player survived that because a swimmer stays decisively past the
    /// threshold, but a soldier fighting at the waterline was re-armed on the
    /// exact frame it went under.  Hysteresis is what makes this work for
    /// soldiers, so do not collapse the entry and release margins back
    /// together.
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp))]
    internal static class GwpDualBladeWaterSafetyPatch
    {
        /// <summary>How much of the pair is currently put away.</summary>
        private enum GuardStage
        {
            /// <summary>Clear of water; the agent may carry both blades.</summary>
            None = 0,

            /// <summary>Near the waterline; the off-hand blade is away.</summary>
            OffHandOnly = 1,

            /// <summary>At or under the surface; both blades are away.</summary>
            BothHands = 2,
        }

        // Clearance is the agent's feet above the water surface.  Entry
        // margins are small so the guard costs nothing away from water;
        // release margins are far larger so leaving a stage takes a real
        // change of position, not a frame of animation noise.
        private const float OffHandEntryClearance = 1.50f;
        private const float OffHandReleaseClearance = 3.00f;
        private const float BothHandsEntryClearance = 0.35f;
        private const float BothHandsReleaseClearance = 1.20f;
        private const float ReleaseDwellSeconds = 0.75f;

        private const float MinimumProbeSeconds = 0.15f;
        private const float MaximumProbeSeconds = 0.50f;
        private const float NoWaterClearance = 1000f;

        private sealed class GuardRecord
        {
            internal GuardStage Stage;

            /// <summary>
            /// Mission time when the agent first qualified to leave its
            /// current stage, or a negative value while it does not qualify.
            /// </summary>
            internal float ReleaseCandidateSince = -1f;

            /// <summary>
            /// Traced once per stage change so a whole battle produces a
            /// handful of lines instead of one per agent per frame.
            /// </summary>
            internal bool StageTraced;
        }

        private static readonly ConditionalWeakTable<Agent, GuardRecord> Records =
            new ConditionalWeakTable<Agent, GuardRecord>();

        [HarmonyPrefix]
        private static void BeforeNativeAgentTick(
            Mission __instance,
            float dt,
            bool tickPaused)
        {
            _ = tickPaused;

            if (__instance == null || __instance.MissionEnded)
                return;

            try
            {
                // Indexed rather than enumerated: a wield call can reach back
                // into the mission, and a native agent tick must not be
                // preceded by a broken enumerator.
                var agents = __instance.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    Agent agent = agents[i];
                    if (agent == null
                        || !agent.IsActive()
                        || !GwpDualBladeLoadout.IsDualBladeCombatant(agent))
                    {
                        continue;
                    }

                    UpdateAgent(__instance, agent, dt);
                }
            }
            catch (Exception exception)
            {
                // A safety guard must never turn a native transition into a
                // managed mission failure.  The next tick retries everything.
                GwpDualBladeTrace.Write(
                    "DUAL_BLADE_WATER_SAFETY_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void UpdateAgent(Mission mission, Agent agent, float dt)
        {
            GuardRecord record = Records.GetValue(agent, _ => new GuardRecord());

            float clearance = GetWaterClearance(mission, agent, dt);
            bool inWater = agent.IsInWater();

            GuardStage required = inWater
                ? GuardStage.BothHands
                : StageForClearance(
                    clearance,
                    OffHandEntryClearance,
                    BothHandsEntryClearance);

            // The same clearance read against the wider release margins.  A
            // stage is only left when even these forgiving thresholds no
            // longer call for it.
            GuardStage held = inWater
                ? GuardStage.BothHands
                : StageForClearance(
                    clearance,
                    OffHandReleaseClearance,
                    BothHandsReleaseClearance);

            if (required > record.Stage)
            {
                SetStage(record, required);
                record.ReleaseCandidateSince = -1f;
            }
            else if (held < record.Stage && agent.IsOnLand() && !inWater)
            {
                if (record.ReleaseCandidateSince < 0f)
                {
                    record.ReleaseCandidateSince = mission.CurrentTime;
                }
                else if (mission.CurrentTime - record.ReleaseCandidateSince
                    >= ReleaseDwellSeconds)
                {
                    SetStage(record, held);
                    record.ReleaseCandidateSince = -1f;
                }
            }
            else
            {
                record.ReleaseCandidateSince = -1f;
            }

            ApplyStage(agent, record, clearance, inWater);
        }

        private static void SetStage(GuardRecord record, GuardStage stage)
        {
            if (record.Stage == stage)
                return;

            record.Stage = stage;
            record.StageTraced = false;
        }

        private static GuardStage StageForClearance(
            float clearance,
            float offHandClearance,
            float bothHandsClearance)
        {
            if (clearance <= bothHandsClearance)
                return GuardStage.BothHands;

            return clearance <= offHandClearance
                ? GuardStage.OffHandOnly
                : GuardStage.None;
        }

        /// <summary>
        /// Brings the hands in line with the stage.  This runs every tick, not
        /// only on a stage change, because a native sheath request can be
        /// refused while an attack is finishing - which is exactly the state
        /// an AI soldier is in when it fights its way into the water.
        /// </summary>
        private static void ApplyStage(
            Agent agent,
            GuardRecord record,
            float clearance,
            bool inWater)
        {
            bool changed = false;

            if (record.Stage >= GuardStage.OffHandOnly
                && agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None)
            {
                agent.TryToSheathWeaponInHand(
                    Agent.HandIndex.OffHand,
                    Agent.WeaponWieldActionType.Instant);
                changed = true;
            }

            if (record.Stage >= GuardStage.BothHands
                && agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.None)
            {
                agent.TryToSheathWeaponInHand(
                    Agent.HandIndex.MainHand,
                    Agent.WeaponWieldActionType.Instant);
                changed = true;
            }

            // Only ever restore what this guard put away, and only in the
            // order native uses on spawn: the off-hand slot first, then the
            // main hand.
            if (record.Stage < GuardStage.OffHandOnly
                && agent.GetOffhandWieldedItemIndex() == EquipmentIndex.None
                && HoldsBlade(agent, EquipmentIndex.WeaponItemBeginSlot))
            {
                agent.TryToWieldWeaponInSlot(
                    EquipmentIndex.WeaponItemBeginSlot,
                    Agent.WeaponWieldActionType.Instant,
                    isWieldedOnSpawn: false);
                changed = true;
            }

            if (record.Stage < GuardStage.BothHands
                && agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None
                && HoldsBlade(agent, EquipmentIndex.Weapon1))
            {
                agent.TryToWieldWeaponInSlot(
                    EquipmentIndex.Weapon1,
                    Agent.WeaponWieldActionType.Instant,
                    isWieldedOnSpawn: false);
                changed = true;
            }

            if (record.StageTraced || !changed)
                return;

            record.StageTraced = true;
            GwpDualBladeTrace.Write(
                "DUAL_BLADE_WATER_SAFETY_STAGE",
                agent,
                "stage=" + record.Stage
                    + "; clearance=" + clearance.ToString("0.00")
                    + "; inWater=" + inWater
                    + "; main=" + agent.GetPrimaryWieldedItemIndex()
                    + "; offhand=" + agent.GetOffhandWieldedItemIndex());
        }

        private static bool HoldsBlade(Agent agent, EquipmentIndex slot)
        {
            try
            {
                MissionEquipment? equipment = agent.Equipment;
                return equipment != null && !equipment[slot].IsEmpty;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// How far the agent's feet are above the water surface, taking a
        /// short look ahead along its current velocity so that a soldier
        /// falling off a deck is covered before it touches the surface.
        /// Returns a large value where there is no water.
        /// </summary>
        private static float GetWaterClearance(
            Mission mission,
            Agent agent,
            float dt)
        {
            Vec3 position = agent.Position;
            float clearance = ClearanceAt(mission, position);

            float probeSeconds = MathF.Min(
                MathF.Max(dt * 3f, MinimumProbeSeconds),
                MaximumProbeSeconds);
            Vec3 projected = position + agent.GetRealGlobalVelocity() * probeSeconds;

            return MathF.Min(clearance, ClearanceAt(mission, projected));
        }

        private static float ClearanceAt(Mission mission, Vec3 position)
        {
            // The renderer choice matches Bannerlord's own SpawnedItemEntity
            // water handling: renderer water in singleplayer, simulation water
            // in multiplayer.
            float waterLevel = mission.GetWaterLevelAtPositionMT(
                position.AsVec2,
                useWaterRenderer: !GameNetwork.IsMultiplayer);

            // A scene with no water reports a sentinel far below the map.
            if (float.IsNaN(waterLevel)
                || float.IsInfinity(waterLevel)
                || waterLevel <= -1000f)
            {
                return NoWaterClearance;
            }

            return position.z - waterLevel;
        }
    }
}
