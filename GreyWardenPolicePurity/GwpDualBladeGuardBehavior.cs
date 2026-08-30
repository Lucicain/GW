using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Draws the Twinblade Guard's off-hand blade. This is the establishment
    /// half; GwpDualBladeShieldDirectionPatch is the retention half.
    ///
    /// Why a MissionBehavior and not a patch: previews break whenever a
    /// per-call Harmony patch is installed on Agent or MissionWeapon,
    /// reproduced repeatedly and finally with a predicate narrowed to a single
    /// immutable id read. Mission and ArrangementOrder patches and plain
    /// mission behaviours have all been observed preview-safe. This touches
    /// neither toxic type - it only calls public Agent methods on agents it
    /// already tracks.
    ///
    /// Why across frames: asking for the off hand and the main hand in one
    /// frame never works. Native WieldInitialWeapons does exactly that and ends
    /// with no off hand, and an earlier build issued both input flags together
    /// 596 times without the pair ever forming. The same three calls spread one
    /// per tick reached paired=True 553 times out of 597 - they simply could
    /// not survive the formation sheathing the blade again, which is what the
    /// companion patch now prevents.
    /// </summary>
    internal sealed class GwpDualBladeGuardBehavior : MissionBehavior
    {
        private enum Step
        {
            Idle,
            SheatheMainHand,
            WieldOffHand,
            WieldMainHand
        }

        private sealed class Guard
        {
            internal Agent Agent = null!;
            internal Step Step;
            internal int Sequences;
        }

        // Retention is handled by the shield-direction patch, so a couple of
        // sequences is plenty. Bounding it means a refusal can never become the
        // visible draw-and-sheathe loop an earlier build produced.
        private const int MaxSequences = 3;

        private readonly List<Guard> _guards = new List<Guard>();

        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (agent != null
                && agent.IsAIControlled
                && agent.Character?.StringId == GwpIds.TwinbladeTroopId)
            {
                _guards.Add(new Guard { Agent = agent });
            }
        }

        public override void OnMissionTick(float dt)
        {
            for (int i = _guards.Count - 1; i >= 0; i--)
            {
                Guard guard = _guards[i];
                if (!guard.Agent.IsActive())
                {
                    _guards.RemoveAt(i);
                    continue;
                }

                Advance(guard);
            }
        }

        private static void Advance(Guard guard)
        {
            Agent agent = guard.Agent;
            EquipmentIndex main = agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex off = agent.GetOffhandWieldedItemIndex();

            // Paired, or not holding the main blade yet.
            if (off == EquipmentIndex.WeaponItemBeginSlot)
            {
                guard.Step = Step.Idle;
                return;
            }

            if (main != EquipmentIndex.Weapon1)
            {
                guard.Step = Step.Idle;
                return;
            }

            // Never take the main hand away mid-swing.
            if (!IsIdle(agent))
                return;

            switch (guard.Step)
            {
                case Step.Idle:
                    if (guard.Sequences >= MaxSequences)
                        return;

                    guard.Sequences++;
                    agent.TryToSheathWeaponInHand(
                        Agent.HandIndex.MainHand,
                        Agent.WeaponWieldActionType.Instant);
                    guard.Step = Step.SheatheMainHand;
                    return;

                case Step.SheatheMainHand:
                    // The off hand only takes once the main hand is free.
                    if (main != EquipmentIndex.None)
                        return;

                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.WeaponItemBeginSlot,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: true);
                    guard.Step = Step.WieldOffHand;
                    return;

                case Step.WieldOffHand:
                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.Weapon1,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: true);
                    guard.Step = Step.WieldMainHand;
                    return;

                default:
                    GwpDualBladeTrace.Write(
                        "GUARD_PAIR_RESULT",
                        agent,
                        "sequence=" + guard.Sequences
                        + "; main=" + agent.GetPrimaryWieldedItemIndex()
                        + "; offhand=" + agent.GetOffhandWieldedItemIndex());
                    guard.Step = Step.Idle;
                    return;
            }
        }

        private static bool IsIdle(Agent agent) =>
            IsIdleCode(agent.GetCurrentActionType(0))
            && IsIdleCode(agent.GetCurrentActionType(1));

        private static bool IsIdleCode(Agent.ActionCodeType code) =>
            code == Agent.ActionCodeType.Other
            || code == Agent.ActionCodeType.Idle
            || code == Agent.ActionCodeType.Guard;
    }
}
