using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Per-agent dual-blade state.
    ///
    /// Slots are the fixed ROT layout - Weapon0 off hand, Weapon1 main hand -
    /// for every dual wielder, player and AI alike. An earlier build moved the
    /// archer's blades to Weapon0/Weapon2 to make room for the bow and that is
    /// what broke the character preview: "ranged main hand plus a HeldInOffHand
    /// blade" has no pose mapping. The bow goes after the pair, never between.
    /// </summary>
    internal sealed class GwpDualBladeAgentState
    {
        internal enum Step
        {
            Settled,
            SheatheMainHand,
            WieldOffHand,
            WieldMainHand,
            Disabled
        }

        internal Agent Agent = null!;

        /// <summary>
        /// True for a loadout that also carries a ranged weapon it can actually
        /// use - the archer. Such an agent has real weapon decisions to make,
        /// and they are left to native.
        /// </summary>
        internal bool HasRangedAlternative;

        /// <summary>Slot of that ranged weapon, for the opening wield.</summary>
        internal EquipmentIndex RangedSlot = EquipmentIndex.None;


        /// <summary>Set once the agent has been handed its bow on spawn.</summary>
        internal bool OpeningWieldDone;

        /// <summary>Ticks spent waiting for native's spawn wield to land.</summary>
        internal int OpeningWieldWaitTicks;

        /// <summary>Attempts made at handing the agent its bow on deployment.</summary>
        internal int OpeningWieldAttempts;


        /// <summary>
        /// Ticks since the agent was last swinging, blocking, parried or hit in
        /// melee. Reported alongside a stuck archer so the log says whether it
        /// was standing idle or in the middle of a fight.
        /// </summary>
        internal int TicksSinceMelee = int.MaxValue;

        internal EquipmentIndex LastMain = EquipmentIndex.None;
        internal EquipmentIndex LastOff = EquipmentIndex.None;
        internal int StableHandTicks;
#if GWP_DIAGNOSTICS
        internal bool ReportedInvalidAction;
#endif


        internal Step CurrentStep;
        internal int StepFrames;
        internal int Sequences;
        internal int CooldownFrames;

        internal bool SequenceRunning =>
            CurrentStep == Step.SheatheMainHand
            || CurrentStep == Step.WieldOffHand
            || CurrentStep == Step.WieldMainHand;
    }

    /// <summary>
    /// Registry of AI agents that dual wield. Qualification is decided once,
    /// when the agent is built, and read back later by a simple lookup - the
    /// input callback must not go poking at an agent's equipment while the
    /// game is arranging troops.
    ///
    /// Membership is by equipment, not by character id: a Grey Warden archer
    /// and an AI-controlled Grey Warden commander both carry the pair and both
    /// need the same handling.
    /// </summary>
    internal static class GwpDualBladeAgents
    {
        private static readonly ConditionalWeakTable<Agent, GwpDualBladeAgentState> Registered =
            new ConditionalWeakTable<Agent, GwpDualBladeAgentState>();

        internal static GwpDualBladeAgentState? Find(Agent agent) =>
            Registered.TryGetValue(agent, out GwpDualBladeAgentState? state) ? state : null;

        internal static GwpDualBladeAgentState? TryRegister(Agent? agent)
        {
            if (agent == null
                || !agent.IsAIControlled
                || Registered.TryGetValue(agent, out _)
                || !CarriesPair(agent))
            {
                return null;
            }

            var state = new GwpDualBladeAgentState { Agent = agent };
            state.RangedSlot = FindUsableRangedSlot(agent);
            state.HasRangedAlternative = state.RangedSlot != EquipmentIndex.None;
            Registered.Add(agent, state);
            return state;
        }

        private static bool CarriesPair(Agent agent)
        {
            try
            {
                Equipment? spawnEquipment = agent.SpawnEquipment;
                if (spawnEquipment != null
                    && GwpDualBladeLoadout.IsOffHandBladeId(
                        spawnEquipment[EquipmentIndex.WeaponItemBeginSlot].Item?.StringId)
                    && IsMainBlade(spawnEquipment[EquipmentIndex.Weapon1].Item?.StringId))
                {
                    return true;
                }

                MissionEquipment? equipment = agent.Equipment;
                return equipment != null
                    && GwpDualBladeLoadout.IsOffHandBladeId(
                        equipment[EquipmentIndex.WeaponItemBeginSlot].Item?.StringId)
                    && IsMainBlade(equipment[EquipmentIndex.Weapon1].Item?.StringId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The slot holding a ranged weapon the agent can actually shoot, or
        /// None. Only the two slots past the pair are examined, which is where
        /// the archer's bow and quiver sit. Ammunition is part of the question:
        /// a bow with an empty quiver is not a weapon choice, and handing an
        /// agent one on spawn would leave it holding nothing.
        /// </summary>
        private static EquipmentIndex FindUsableRangedSlot(Agent agent)
        {
            try
            {
                MissionEquipment? equipment = agent.Equipment;
                if (equipment == null)
                    return EquipmentIndex.None;

                EquipmentIndex ranged = EquipmentIndex.None;
                bool hasAmmo = false;

                foreach (EquipmentIndex slot in new[]
                {
                    EquipmentIndex.Weapon2,
                    EquipmentIndex.Weapon3
                })
                {
                    MissionWeapon weapon = equipment[slot];
                    WeaponComponentData? usage = weapon.IsEmpty
                        ? null
                        : weapon.Item?.PrimaryWeapon;
                    if (usage == null)
                        continue;

                    if (usage.WeaponFlags.HasAnyFlag(WeaponFlags.RangedWeapon)
                        && ranged == EquipmentIndex.None)
                    {
                        ranged = slot;
                    }
                    else if (usage.IsAmmo && weapon.Amount > 0)
                    {
                        hasAmmo = true;
                    }
                }

                return hasAmmo ? ranged : EquipmentIndex.None;
            }
            catch
            {
                // A half-built agent is simply treated as pair-only.
                return EquipmentIndex.None;
            }
        }

        /// <summary>
        /// Native's AI weapon-mode scorer multiplies a two-handed weapon's score
        /// by this while the off hand holds an item without CanBlockRanged
        /// (TaleWorlds.Native 1.4.8, 0x1806b0910-0x1806b098a; constant at
        /// 0x180b2af18). It is the banner bearer's rule - a man with a banner in
        /// his left hand does not reach for a two-handed weapon - and the
        /// off-hand blade meets it, so an archer holding the pair scores its bow
        /// at a millionth and never goes back to it by itself.
        /// </summary>
        internal const float NativeOffhandTwoHandedPenalty = 1e-6f;

        /// <summary>
        /// Factor for AiWeaponFavorMultiplierRanged that cancels that penalty
        /// exactly, so native weighs bow against blades by distance as it does
        /// for any archer carrying a sword. The ranged favor is read in one
        /// place only, that same mode decision (0x1806aed8c/aedf9/aee1e), so
        /// nothing else moves. 1 whenever native's condition does not hold.
        /// </summary>
        internal static float RangedFavorScale(Agent agent)
        {
            try
            {
                GwpDualBladeAgentState? state = Find(agent);
                if (state == null || !state.HasRangedAlternative)
                    return 1f;

                EquipmentIndex off = agent.GetOffhandWieldedItemIndex();
                if (off == EquipmentIndex.None)
                    return 1f;

                WeaponComponentData? offUsage =
                    agent.Equipment[off].CurrentUsageItem;
                WeaponComponentData? rangedUsage =
                    agent.Equipment[state.RangedSlot].CurrentUsageItem;
                if (offUsage == null
                    || rangedUsage == null
                    || offUsage.WeaponFlags.HasAnyFlag(WeaponFlags.CanBlockRanged)
                    || !rangedUsage.WeaponFlags.HasAnyFlag(
                        WeaponFlags.NotUsableWithOneHand))
                {
                    return 1f;
                }

                return 1f / NativeOffhandTwoHandedPenalty;
            }
            catch (System.Exception gwpQuietFailure)
            {
                GwpFaultTrace.WriteQuiet(gwpQuietFailure);
                return 1f;
            }
        }

        private static bool IsMainBlade(string? itemId) =>
            string.Equals(itemId, GwpIds.DualBladeMainhandItemId,
                System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Keeps an AI dual wielder's pair in hand, for both loadout shapes.
    ///
    /// The two shapes need opposite things, and telling them apart is the whole
    /// design:
    ///
    /// A pair-only agent - the AI commander - carries nothing but the blades.
    /// It has no weapon decision worth making, native's periodic re-selection
    /// can only ever take the off hand away, and discarding its weapon-change
    /// input costs nothing. That is what the input component does, and it has
    /// been stable since the day it was written.
    ///
    /// An archer carries a bow as well, and every one of its weapon decisions
    /// belongs to native. Earlier builds intercepted them - dropped its request
    /// to lower the off hand, handed it a bow on its behalf, then protected
    /// that bow from being taken back - and each layer bought obedience in one
    /// situation by taking it away in another: blades that would not go back to
    /// the bow, bows that flip-flopped, and finally agents that ignored orders
    /// outright because the rescue budget behind all that machinery had run
    /// out. That is recorded in the maintenance history as a lesson about the
    /// approach, not about the tuning.
    ///
    /// Native chooses between the bow and blades, by distance, exactly as for
    /// an archer with a sword; RangedFavorScale takes the banner bearer's
    /// two-handed penalty back out of that choice. The pair is bound to the
    /// main hand: the input component keeps the off hand from being cleaned up
    /// on its own, and this behaviour draws the off-hand blade when native
    /// draws the main one. The cavalry-contact trace exposed one exception to
    /// immediate switching: Wield2+Sheath1 during WeaponBash removes the off
    /// hand before native contact uses it. GwpDualBladeActionGate keeps the
    /// current hands until that action ends, and prevents a new kick request
    /// while a weapon switch is pending.
    ///
    /// A mission behaviour and an agent component rather than Harmony patches:
    /// character previews break whenever a per-call patch is installed on Agent
    /// or MissionWeapon, reproduced repeatedly and finally with a predicate
    /// narrowed to a single immutable id read. Plain behaviours and components
    /// calling the same public methods have never done so.
    /// </summary>
    internal sealed class GwpDualBladeAiBehavior : MissionBehavior
    {
        /// <summary>Frames one step may wait for native to honour it.</summary>
        private const int MaxStepFrames = 6;

        /// <summary>
        /// Sequences allowed per melee spell before an agent is left to fight
        /// one-handed. The cap is what stops a refusal from becoming the
        /// visible draw-and-sheathe loop an earlier build produced.
        /// </summary>
        private const int MaxSequences = 6;

        private const int CooldownFrames = 180;

        /// <summary>How long to keep offering the bow on deployment.</summary>
        private const int MaxOpeningWieldWaitTicks = 1800;

        /// <summary>Ticks between two such offers.</summary>
        private const int OpeningWieldRetryTicks = 30;

        /// <summary>Seconds since being hit in melee that still counts as a fight.</summary>
        private const float RecentMeleeHitSeconds = 2f;


        private readonly List<GwpDualBladeAgentState> _switchers =
            new List<GwpDualBladeAgentState>();

        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            GwpDualBladeAgentState? state = GwpDualBladeAgents.TryRegister(agent);
            if (state == null)
                return;

            if (state.HasRangedAlternative)
            {
                _switchers.Add(state);

                // The archer keeps every weapon decision it has; the off-hand
                // blade is bound to the main one.
                if (agent.GetComponent<GwpDualBladeFightGripComponent>() == null)
                    agent.AddComponent(new GwpDualBladeFightGripComponent(agent));
                return;
            }

            // Pair only: no decisions to preserve, so the proven suppression.
            if (agent.GetComponent<GwpDualBladePairKeeperComponent>() == null)
                agent.AddComponent(new GwpDualBladePairKeeperComponent(agent));
        }

        public override void OnMissionTick(float dt)
        {
            for (int i = _switchers.Count - 1; i >= 0; i--)
            {
                GwpDualBladeAgentState state = _switchers[i];
                if (state.Agent == null || !state.Agent.IsActive())
                {
                    _switchers.RemoveAt(i);
                    continue;
                }

                Advance(state);
            }
        }

        private void Advance(GwpDualBladeAgentState state)
        {
            Agent agent = state.Agent;
            EquipmentIndex main = agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex off = agent.GetOffhandWieldedItemIndex();
            // Let a completed draw survive a tick before requesting a bash.
            state.StableHandTicks = main == state.LastMain && off == state.LastOff
                ? System.Math.Min(2, state.StableHandTicks + 1) : 0;
            bool offHandChanged = off != state.LastOff;
            state.LastMain = main;
            state.LastOff = off;

            // Native recomputes driven properties on spawn, mount, ammo and
            // usage changes, but not when a hand changes; the ranged favor
            // follows the off hand, so it is refreshed here, through native's
            // own public entry point.
            if (offHandChanged)
                agent.UpdateAgentStats();

            TrackMelee(state, agent);
            bool canChangeWeapons = GwpDualBladeActionGate.CanChangeWeapons(agent);

            // An archer deploys holding its bow, not the pair. Native's spawn
            // wield takes the first two slots and so hands it the blades, which
            // is right for the pair-only loadout and wrong here: the troop
            // walks onto the field in melee stance.
            //
            // Native's spawn wield runs after OnAgentBuild, so this cannot be
            // done at build time; the first tick is the earliest point that
            // sticks. It corrects a slot order, not a decision - native has not
            // made any decision this early.
            if (!state.OpeningWieldDone)
            {
                // Done as soon as the bow is actually in hand. Asking once was
                // not enough: the live log showed archers reported as having
                // been handed a bow and then standing in melee stance for the
                // rest of the deployment, because the request is refused while
                // the battle has not started - the trace only recorded that it
                // was made. So it is repeated until it takes.
                if (main == state.RangedSlot)
                {
                    state.OpeningWieldDone = true;
                }
                else if (main == EquipmentIndex.Weapon1
                    && off == EquipmentIndex.WeaponItemBeginSlot
                    && canChangeWeapons
                    && state.OpeningWieldWaitTicks % OpeningWieldRetryTicks == 0)
                {
                    state.OpeningWieldAttempts++;
                    state.StableHandTicks = 0;
                    agent.TryToWieldWeaponInSlot(
                        state.RangedSlot,
                        Agent.WeaponWieldActionType.InstantAfterPickUp,
                        isWieldedOnSpawn: true);
                }

                // Not forever, and not once the agent has other things to do:
                // an agent that comes up holding something else, that gets into
                // a fight, or that has simply waited long enough is left alone.
                if (++state.OpeningWieldWaitTicks >= MaxOpeningWieldWaitTicks
                    || state.TicksSinceMelee < int.MaxValue)
                {
                    state.OpeningWieldDone = true;
                }

                if (!state.OpeningWieldDone)
                    return;
            }

            // A refusal stands down for the rest of this melee spell only; the
            // next time native takes the agent out of melee stance - the bow,
            // empty hands - the budget starts again.
            if (state.CurrentStep == GwpDualBladeAgentState.Step.Disabled)
            {
                if (main == EquipmentIndex.Weapon1
                    || (main == EquipmentIndex.None
                        && off == EquipmentIndex.WeaponItemBeginSlot))
                {
                    return;
                }

                state.CurrentStep = GwpDualBladeAgentState.Step.Settled;
                state.Sequences = 0;
            }

            // Object usage is native's to arrange, hands included.
            if (agent.IsUsingGameObject)
                return;

            // Paired: nothing to do but watch for the fault.
            if (main == EquipmentIndex.Weapon1
                && off == EquipmentIndex.WeaponItemBeginSlot)
            {
                state.CurrentStep = GwpDualBladeAgentState.Step.Settled;
                state.StepFrames = 0;
                return;
            }

            // Holding the main blade alone, or the off hand alone. Anything
            // else - the bow, a reload, empty hands mid-switch - is native
            // going about its business and is not touched.
            bool inMeleeStance = main == EquipmentIndex.Weapon1
                || (main == EquipmentIndex.None
                    && off == EquipmentIndex.WeaponItemBeginSlot);
            if (!inMeleeStance && !state.SequenceRunning)
            {
                state.CurrentStep = GwpDualBladeAgentState.Step.Settled;
                state.Sequences = 0;
                return;
            }

            // Main blade in hand means the off-hand blade belongs in the other
            // hand. That is the whole rule, and it is an invariant about this
            // mod's own weapon pair rather than a decision about anything else.
            //
            // Every earlier attempt to be cleverer than this went wrong in the
            // same way: it tried to work out *why* native had taken the off
            // hand away - going for the bow? tidying? obeying an order? - and
            // then acted on the guess. The guesses were wrong in turn, and each
            // one traded obedience in one situation for a draw-and-sheathe loop
            // in another. Whether the agent should be in melee at all, when it
            // should raise its bow, and whether it obeys the player are not
            // questions this behaviour has any business answering; native
            // answers them. Only an active kick/bash temporarily reserves the
            // weapons it needs for contact.
            //
            // Keeping the pair together once it is up is prevention, not
            // repair: the component drops the lone sheath that would break it.
            // This redraw only has to cover the moment native wields the main
            // blade on its own, which is when a shield would go up for a native
            // archer.
            if (state.CooldownFrames > 0)
            {
                state.CooldownFrames--;
                return;
            }

            // Never take a weapon out of an agent's hand mid-swing.
            if (!canChangeWeapons || !IsIdle(agent))
                return;

            switch (state.CurrentStep)
            {
                case GwpDualBladeAgentState.Step.Settled:
                    if (state.Sequences >= MaxSequences)
                    {
                        state.CurrentStep = GwpDualBladeAgentState.Step.Disabled;
                        return;
                    }

                    state.Sequences++;
                    state.StepFrames = 0;
                    state.StableHandTicks = 0;

                    // The off hand only takes once the main hand is free.
                    if (main != EquipmentIndex.None)
                    {
                        agent.TryToSheathWeaponInHand(
                            Agent.HandIndex.MainHand,
                            Agent.WeaponWieldActionType.Instant);
                    }
                    state.CurrentStep = GwpDualBladeAgentState.Step.SheatheMainHand;
                    return;

                case GwpDualBladeAgentState.Step.SheatheMainHand:
                    if (main != EquipmentIndex.None)
                    {
                        if (++state.StepFrames > MaxStepFrames)
                            Abandon(state);
                        return;
                    }

                    state.StepFrames = 0;
                    state.StableHandTicks = 0;
                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.WeaponItemBeginSlot,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: true);
                    state.CurrentStep = GwpDualBladeAgentState.Step.WieldOffHand;
                    return;

                case GwpDualBladeAgentState.Step.WieldOffHand:
                    if (off != EquipmentIndex.WeaponItemBeginSlot)
                    {
                        if (++state.StepFrames > MaxStepFrames)
                            Abandon(state);
                        return;
                    }

                    state.StepFrames = 0;
                    state.StableHandTicks = 0;
                    agent.TryToWieldWeaponInSlot(
                        EquipmentIndex.Weapon1,
                        Agent.WeaponWieldActionType.Instant,
                        isWieldedOnSpawn: true);
                    state.CurrentStep = GwpDualBladeAgentState.Step.WieldMainHand;
                    return;

                case GwpDualBladeAgentState.Step.WieldMainHand:
                    if (main != EquipmentIndex.Weapon1
                        && ++state.StepFrames > MaxStepFrames)
                    {
                        Abandon(state);
                    }
                    return;
            }
        }

        /// <summary>
        /// Puts the main blade back and stands down for a while. The worst
        /// outcome of a refusal is an archer fighting with one blade - never a
        /// visible loop.
        /// </summary>
        private void Abandon(GwpDualBladeAgentState state)
        {
            state.StableHandTicks = 0;
            state.Agent.TryToWieldWeaponInSlot(
                EquipmentIndex.Weapon1,
                Agent.WeaponWieldActionType.Instant,
                isWieldedOnSpawn: true);
            state.CurrentStep = GwpDualBladeAgentState.Step.Settled;
            state.CooldownFrames = CooldownFrames;
            state.StepFrames = 0;
        }

        private static void TrackMelee(GwpDualBladeAgentState state, Agent agent)
        {
            if (IsMeleeCode(agent.GetCurrentActionType(0))
                || IsMeleeCode(agent.GetCurrentActionType(1))
                || WasHitInMeleeRecently(agent))
            {
                state.TicksSinceMelee = 0;
                return;
            }

            if (state.TicksSinceMelee < int.MaxValue)
                state.TicksSinceMelee++;
        }

        private static bool WasHitInMeleeRecently(Agent agent)
        {
            try
            {
                Mission? mission = agent.Mission;
                return mission != null
                    && mission.CurrentTime - agent.LastMeleeHitTime
                        < RecentMeleeHitSeconds;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsIdle(Agent agent) =>
            IsIdleCode(agent.GetCurrentActionType(0))
            && IsIdleCode(agent.GetCurrentActionType(1));

        private static bool IsIdleCode(Agent.ActionCodeType code) =>
            code == Agent.ActionCodeType.Other
            || code == Agent.ActionCodeType.Idle
            || code == Agent.ActionCodeType.Guard;

        private static bool IsMeleeCode(Agent.ActionCodeType code) =>
            code == Agent.ActionCodeType.ReadyMelee
            || code == Agent.ActionCodeType.ReleaseMelee
            || code == Agent.ActionCodeType.ParriedMelee
            || code == Agent.ActionCodeType.BlockedMelee
            || code == Agent.ActionCodeType.DefendFist
            || code == Agent.ActionCodeType.DefendShield
            || code == Agent.ActionCodeType.DefendForward1h
            || code == Agent.ActionCodeType.DefendUp1h
            || code == Agent.ActionCodeType.DefendRight1h
            || code == Agent.ActionCodeType.DefendLeft1h;
    }

    /// <summary>
    /// Binds the off-hand blade to the main one, at the only boundary native
    /// offers, and follows native's own rules for doing so (TaleWorlds.Native
    /// 1.4.8, weapon selection 0x1806afec0 and its input writer 0x1805f4bf5):
    ///
    /// - native re-picks the off hand on every weapon decision and keeps only
    ///   an item that is HeldInOffHand and CanBlockRanged; the blade is not
    ///   CanBlockRanged (a weapon does not stop arrows), so native asks for it
    ///   to be lowered on its own, every time. That is the only lone off-hand
    ///   sheath native ever sends outside object use, so it is always dropped;
    /// - a change of main weapon arrives in the same frame as the off-hand
    ///   request (the bow as Wield2+Sheath1), and passes untouched, so the pair
    ///   leaves together with native's decision to shoot;
    /// - a lone main-hand sheath is native emptying the main hand; the off-hand
    ///   blade goes with it;
    /// - while the agent uses a game object native holds back main-hand changes
    ///   and lowers the off hand first, so nothing is touched then.
    ///
    /// Every wield passes. Drawing the off-hand blade when native draws the
    /// main one - the moment native raises a shield - is GwpDualBladeAiBehavior's.
    /// </summary>
    internal sealed class GwpDualBladeFightGripComponent : AgentComponent
    {
        private const Agent.EventControlFlag Sheath =
            Agent.EventControlFlag.Sheath0
            | Agent.EventControlFlag.Sheath1;

        internal GwpDualBladeFightGripComponent(Agent agent)
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

            Agent.EventControlFlag sheath = eventFlag & Sheath;
            if (sheath == Agent.EventControlFlag.None
                || (eventFlag & Wield) != Agent.EventControlFlag.None
                || Agent.IsUsingGameObject
                || Agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.Weapon1
                || Agent.GetOffhandWieldedItemIndex()
                    != EquipmentIndex.WeaponItemBeginSlot)
            {
                return;
            }

            if ((sheath & Agent.EventControlFlag.Sheath0)
                != Agent.EventControlFlag.None)
            {
                eventFlag |= Agent.EventControlFlag.Sheath1;
                return;
            }

            eventFlag &= ~Agent.EventControlFlag.Sheath1;
        }

        private const Agent.EventControlFlag Wield =
            Agent.EventControlFlag.Wield0
            | Agent.EventControlFlag.Wield1
            | Agent.EventControlFlag.Wield2
            | Agent.EventControlFlag.Wield3;
    }

    /// <summary>
    /// Discards the weapon-change input of an agent that carries nothing but
    /// the pair, so native's periodic re-selection cannot take the off-hand
    /// blade away. Such an agent has no second weapon and therefore no choice
    /// worth making; movement, attacks, blocks and every other decision stay
    /// entirely native.
    ///
    /// This is deliberately not used for the archer. An archer has a bow, and
    /// intercepting its weapon input is what made it stop obeying orders.
    ///
    /// AgentComponent.OnAIInputSet is that selection's one managed surface, and
    /// being a component rather than a Harmony patch it stays clear of the
    /// Agent and MissionWeapon types whose per-call patches break character
    /// previews.
    /// </summary>
    internal sealed class GwpDualBladePairKeeperComponent : AgentComponent
    {
        private const Agent.EventControlFlag WeaponChange =
            Agent.EventControlFlag.Wield0
            | Agent.EventControlFlag.Wield1
            | Agent.EventControlFlag.Wield2
            | Agent.EventControlFlag.Wield3
            | Agent.EventControlFlag.Sheath0
            | Agent.EventControlFlag.Sheath1
            | Agent.EventControlFlag.ToggleAlternativeWeapon;

        internal GwpDualBladePairKeeperComponent(Agent agent)
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

            eventFlag &= ~WeaponChange;
        }
    }
}
