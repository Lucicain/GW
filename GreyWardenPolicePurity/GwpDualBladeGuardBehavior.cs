using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Keeps the Twinblade Guard's pair in hand once the battle starts.
    ///
    /// The deployment phase settled what was actually wrong. Guards stand in
    /// deployment holding both blades, stably, with no help from this mod -
    /// native's own spawn wield pairs them correctly. The instant the battle
    /// begins the off-hand blade is gone. So establishment was never the
    /// problem and retention is, and the disruptor is not the formation: the
    /// ArrangementOrder postfix already stopped the repeated sheathing, and
    /// deployment has no active AI. What starts at battle start is the native
    /// AI's own weapon selection, which only ever keeps a shield in an off
    /// hand.
    ///
    /// That selection has exactly one managed surface: AgentComponent
    /// .OnAIInputSet, where native hands over its EventControlFlag by
    /// reference. It is a component, not a Harmony patch, so it stays clear of
    /// the Agent and MissionWeapon types whose per-call patches break the
    /// character previews.
    ///
    /// A guard carries nothing but the two blades, so there is no weapon choice
    /// worth making: every wield and sheath request is simply dropped, and the
    /// pair native already gave it stays where it is. The previous behaviour -
    /// sheathing and re-drawing the main blade to force a pair - is gone; that
    /// was what the user saw as the main sword being drawn three times.
    /// </summary>
    internal sealed class GwpDualBladeGuardBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (agent != null
                && agent.IsAIControlled
                && agent.Character?.StringId == GwpIds.TwinbladeTroopId
                && agent.GetComponent<GwpDualBladeGuardInputComponent>() == null)
            {
                agent.AddComponent(new GwpDualBladeGuardInputComponent(agent));
            }
        }
    }

    internal sealed class GwpDualBladeGuardInputComponent : AgentComponent
    {
        private const Agent.EventControlFlag WeaponChange =
            Agent.EventControlFlag.Wield0
            | Agent.EventControlFlag.Wield1
            | Agent.EventControlFlag.Wield2
            | Agent.EventControlFlag.Wield3
            | Agent.EventControlFlag.Sheath0
            | Agent.EventControlFlag.Sheath1
            | Agent.EventControlFlag.ToggleAlternativeWeapon;

        private bool _logged;

        internal GwpDualBladeGuardInputComponent(Agent agent)
            : base(agent)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnFormationSet()
        {
            base.OnFormationSet();
            if (!Agent.GetHasOnAiInputSetCallback())
                Agent.SetHasOnAiInputSetCallback(true);
        }

        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            _ = movementFlag;
            _ = inputVector;

            if ((eventFlag & WeaponChange) == Agent.EventControlFlag.None)
                return;

            // Movement, attacks, blocks and every other decision stay entirely
            // native; only the weapon-selection half of the input is dropped.
            Agent.EventControlFlag before = eventFlag;
            eventFlag &= ~WeaponChange;

            if (!_logged)
            {
                _logged = true;
                GwpDualBladeTrace.Write(
                    "GUARD_WEAPON_SELECTION_SUPPRESSED",
                    Agent,
                    "before=" + before
                    + "; main=" + Agent.GetPrimaryWieldedItemIndex()
                    + "; offhand=" + Agent.GetOffhandWieldedItemIndex());
            }
        }
    }
}
