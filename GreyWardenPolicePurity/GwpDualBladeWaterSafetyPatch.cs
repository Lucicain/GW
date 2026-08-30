using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Bannerlord's native water transition is safe for an ordinary weapon and
    /// for a dual-blade loadout with one or zero weapons drawn, but the native
    /// path crashes when both of our blades are still wielded.  The transition
    /// happens inside Agent.Tick, which is called by Mission's
    /// TickAgentsAndTeamsImp.  Sheath the pair immediately before that native
    /// tick whenever the agent is already in water or is about to cross the
    /// water surface.  This leaves both items in the equipment slots and
    /// restores them after the agent is back on land.
    ///
    /// The placement mirrors the vanilla climbing-machine path: it calls
    /// Agent.TryToSheathWeaponInHand with the Instant action type before the
    /// native agent update, rather than waiting for OnMissionTick after the
    /// water transition has already been processed.
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp))]
    internal static class GwpDualBladeWaterSafetyPatch
    {
        private const float WaterEntryMargin = 0.30f;
        private const float MinimumProbeSeconds = 0.10f;
        private const float MaximumProbeSeconds = 0.35f;

        private static readonly HashSet<Agent> SafetySheathedAgents = new();
        private static Mission? TrackedMission;

        [HarmonyPrefix]
        private static void BeforeNativeAgentTick(float dt, bool tickPaused)
        {
            _ = tickPaused;

            Mission? mission = Mission.Current;
            if (mission == null || mission.MissionEnded)
            {
                SafetySheathedAgents.Clear();
                TrackedMission = null;
                return;
            }

            // Agent instances belong to a mission.  Clear the weakly scoped
            // set when Bannerlord swaps missions, even if the previous mission
            // ended between two native ticks.
            if (!ReferenceEquals(TrackedMission, mission))
            {
                SafetySheathedAgents.Clear();
                TrackedMission = mission;
            }

            try
            {
                foreach (Agent agent in mission.Agents)
                {
                    if (!agent.IsActive())
                    {
                        SafetySheathedAgents.Remove(agent);
                        continue;
                    }

                    if (!GwpDualBladeLoadout.IsDualBladeCombatant(agent))
                    {
                        SafetySheathedAgents.Remove(agent);
                        continue;
                    }

                    if (SafetySheathedAgents.Contains(agent))
                    {
                        if (IsWaterEntryImminent(mission, agent, dt))
                        {
                            // A native request can be deferred while another
                            // action is finishing.  Keep retrying any blade
                            // that is still drawn instead of assuming the
                            // first request was accepted.
                            SheathAnyRemainingBlade(agent);
                            continue;
                        }

                        if (RestorePairOnLand(agent))
                            SafetySheathedAgents.Remove(agent);
                        continue;
                    }

                    EquipmentIndex mainHand = agent.GetPrimaryWieldedItemIndex();
                    EquipmentIndex offHand = agent.GetOffhandWieldedItemIndex();
                    if (mainHand == EquipmentIndex.None
                        || offHand == EquipmentIndex.None
                        || !IsWaterEntryImminent(mission, agent, dt))
                    {
                        continue;
                    }

                    // The off-hand blade is the custom dual-wield attachment;
                    // sheath it first so that even a partially accepted native
                    // request leaves at most one blade drawn.  Sheathing the
                    // main hand immediately afterwards gives the native water
                    // path the same all-sheathed state as vanilla.
                    SheathAnyRemainingBlade(agent);

                    SafetySheathedAgents.Add(agent);
                    GwpDualBladeTrace.Write(
                        "DUAL_BLADE_WATER_SAFETY_SHEATH",
                        agent,
                        "main=" + mainHand
                            + "; offhand=" + offHand
                            + "; inWater=" + agent.IsInWater());
                }
            }
            catch (Exception exception)
            {
                // A diagnostics or safety guard must never turn a native
                // transition into a managed mission failure.  The next tick
                // will retry any agent that was not successfully sheathed.
                GwpDualBladeTrace.Write(
                    "DUAL_BLADE_WATER_SAFETY_FAILED",
                    details: exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void SheathAnyRemainingBlade(Agent agent)
        {
            if (agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None)
            {
                agent.TryToSheathWeaponInHand(
                    Agent.HandIndex.OffHand,
                    Agent.WeaponWieldActionType.Instant);
            }

            if (agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.None)
            {
                agent.TryToSheathWeaponInHand(
                    Agent.HandIndex.MainHand,
                    Agent.WeaponWieldActionType.Instant);
            }
        }

        private static bool IsWaterEntryImminent(
            Mission mission,
            Agent agent,
            float dt)
        {
            if (agent.IsInWater())
                return true;

            Vec3 position = agent.Position;
            if (IsWaterAtOrAbove(
                    mission,
                    position.AsVec2,
                    position.z))
            {
                return true;
            }

            float probeSeconds = MathF.Min(
                MathF.Max(dt * 2f, MinimumProbeSeconds),
                MaximumProbeSeconds);
            Vec3 velocity = agent.GetRealGlobalVelocity();
            Vec3 projected = position + velocity * probeSeconds;
            return IsWaterAtOrAbove(
                mission,
                projected.AsVec2,
                projected.z);
        }

        private static bool IsWaterAtOrAbove(
            Mission mission,
            Vec2 position,
            float agentHeight)
        {
            // This is the same renderer choice used by Bannerlord's own
            // SpawnedItemEntity water handling: renderer water in singleplayer,
            // simulation water in multiplayer.
            float waterLevel = mission.GetWaterLevelAtPositionMT(
                position,
                useWaterRenderer: !GameNetwork.IsMultiplayer);

            return !float.IsNaN(waterLevel)
                && !float.IsInfinity(waterLevel)
                && waterLevel > -1000f
                && waterLevel >= agentHeight - WaterEntryMargin;
        }

        private static bool RestorePairOnLand(Agent agent)
        {
            if (!agent.IsOnLand()
                || !GwpDualBladeLoadout.IsDualBladeCombatant(agent))
            {
                return false;
            }

            try
            {
                if (agent.GetOffhandWieldedItemIndex() == EquipmentIndex.None)
                {
                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.Weapon0,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: false);
                }

                if (agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None)
                {
                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.Weapon1,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: false);
                }

                GwpDualBladeTrace.Write(
                    "DUAL_BLADE_WATER_SAFETY_RESTORE",
                    agent,
                    "main=" + agent.GetPrimaryWieldedItemIndex()
                        + "; offhand=" + agent.GetOffhandWieldedItemIndex());
                return agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None
                    && agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.None;
            }
            catch (Exception exception)
            {
                GwpDualBladeTrace.Write(
                    "DUAL_BLADE_WATER_SAFETY_RESTORE_FAILED",
                    agent,
                    exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }
    }
}
