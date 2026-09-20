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

        /// <summary>Slot of its ammunition, read when reporting a stuck agent.</summary>
        internal EquipmentIndex AmmoSlot = EquipmentIndex.None;

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

        /// <summary>
        /// The firing order this agent was carrying last tick, so a change to
        /// it can be seen, and whether the change was an instruction to start
        /// shooting that has not been carried out yet.
        /// </summary>
        internal int LastFiringOrder = -1;
        internal bool RangedRequested;
        internal int RangedRequestedTicks;
        internal bool RangedWieldPending;
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
            state.RangedSlot = FindUsableRangedSlot(agent, out EquipmentIndex ammoSlot);
            state.AmmoSlot = ammoSlot;
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
        private static EquipmentIndex FindUsableRangedSlot(
            Agent agent,
            out EquipmentIndex ammoSlot)
        {
            ammoSlot = EquipmentIndex.None;
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
                        ammoSlot = slot;
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
    /// Native still chooses between the bow and blades. The cavalry-contact
    /// trace exposed one exception to immediate switching: Wield2+Sheath1
    /// during WeaponBash removes the off hand before native contact uses it.
    /// GwpDualBladeActionGate keeps the current hands until that action ends,
    /// and prevents a new kick request while a weapon switch is pending.
    /// This behavior also draws the missing off-hand blade in melee stance.
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

                // The archer keeps every weapon decision it has, except that it
                // may not put its off hand away in the middle of a fight.
                if (agent.GetComponent<GwpDualBladeFightGripComponent>() == null)
                    agent.AddComponent(new GwpDualBladeFightGripComponent(agent, state));
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
            state.LastMain = main;
            state.LastOff = off;

            TrackMelee(state, agent);
            bool canChangeWeapons = GwpDualBladeActionGate.CanChangeWeapons(agent);
            TrackFiringOrder(state, agent, main, canChangeWeapons);

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

            if (state.CurrentStep == GwpDualBladeAgentState.Step.Disabled)
                return;

            // An order to start shooting is outstanding. Native is on its way
            // to the bow and this behaviour keeps out of it entirely - no
            // invariant, no redraw - until the bow is up, a fight starts, or
            // the order is taken back.
            if (state.RangedRequested)
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

        /// <summary>
        /// Watches the agent's own firing order for the moment it turns into
        /// "shoot", and holds that as an outstanding instruction until it has
        /// been carried out.
        ///
        /// This is the signal the whole thing turned on. Inside a formation -
        /// standing, moving, advancing, falling back - native expresses its
        /// switch to the bow as a lone off-hand sheath and nothing else, so the
        /// invariant that holds the pair together in a fight was silently
        /// swallowing the switch: the live log shows a formation on Move being
        /// told to fire and still sitting at pair=199 a second and a half
        /// later, four times running, while the same formation told to charge
        /// had 131 bows up within the same interval. Under a charge the agents
        /// leave the formation and ask with a wield and a sheath together,
        /// which was never blocked - hence the difference the player could feel
        /// but nothing in the weapon logic could see.
        ///
        /// A player's order and an AI commander's order are the same value in
        /// the same field, so this reads both without caring which it was.
        /// </summary>
        private static void TrackFiringOrder(
            GwpDualBladeAgentState state,
            Agent agent,
            EquipmentIndex main,
            bool canChangeWeapons)
        {
            int firing;
            try
            {
                firing = agent.GetFiringOrder();
            }
            catch
            {
                return;
            }

            bool mayShoot =
                firing == (int)FiringOrder.RangedWeaponUsageOrderEnum.FireAtWill;

            if (mayShoot
                && state.LastFiringOrder
                    == (int)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire
                && !state.RangedRequested)
            {
                state.RangedRequested = true;
                state.RangedRequestedTicks = 0;
                state.RangedWieldPending = true;
                // Inside a formation native does this in two steps - lower the
                // off hand on one decision, raise the bow on a later one - and
                // both are visible, which is the double weapon-change the
                // player sees on every order except a charge. Under a charge
                // the agent is loose and asks for the wield and the sheath in
                // the same frame, so it happens in one movement.
                //
                // Once an active kick/bash ends, the bow goes into
                // the agent's hand by the same call that hands every
                // archer its bow on deployment - and that call makes exactly
                // this transition, from the pair to the bow. Native keeps the
                // decision: if it wants melee after all, its own wield says so
                // a moment later and nothing here stands in the way.
            }

            state.LastFiringOrder = firing;

            if (!state.RangedRequested)
                return;

            // Carried out, overtaken by a fight, taken back, or long enough
            // ago that native is plainly not acting on it - in which case the
            // pair may as well be back in hand.
            if (main == state.RangedSlot
                || !mayShoot
                || !HasAmmo(state)
                || state.TicksSinceMelee <= MeleeTicksCancellingRangedOrder
                || ++state.RangedRequestedTicks >= MaxRangedRequestTicks)
            {
                state.RangedRequested = false;
                state.RangedWieldPending = false;
                return;
            }

            // Preserve the order until the active kick/bash releases its hands.
            if (state.RangedWieldPending && canChangeWeapons
                && main == EquipmentIndex.Weapon1)
            {
                state.RangedWieldPending = false;
                state.StableHandTicks = 0;
                agent.TryToWieldWeaponInSlot(state.RangedSlot,
                    Agent.WeaponWieldActionType.WithAnimation, isWieldedOnSpawn: false);
            }
        }

        /// <summary>
        /// An agent that is trading blows is not going to raise a bow whatever
        /// the order says, and native will not try; the pair matters more.
        /// </summary>
        private const int MeleeTicksCancellingRangedOrder = 60;

        /// <summary>Ten seconds; after that the order has plainly not taken.</summary>
        private const int MaxRangedRequestTicks = 600;

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

        private static bool HasAmmo(GwpDualBladeAgentState state)
        {
            try
            {
                return state.AmmoSlot != EquipmentIndex.None
                    && state.Agent.Equipment[state.AmmoSlot].Amount > 0;
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
    /// Holds the pair together while the main blade is in hand, and does
    /// nothing else whatsoever.
    ///
    /// Native asks for the off hand to be lowered about once every one and a
    /// third seconds - its decision cycle - for any agent holding something
    /// there that is not a shield, whatever that agent is doing. The blade
    /// keeps MeleeWeapon so that it can deal damage, which means it can never
    /// satisfy WeaponComponentData.IsShield, which means native will never stop
    /// asking. A Twinblade Guard, which has no bow at all, gets exactly the
    /// same request, so it is not native heading for a ranged weapon.
    ///
    /// So that one request is dropped, and only while the main blade is
    /// actually in hand. Everything else is native's:
    ///
    /// - every wield, melee or ranged, passes untouched, at all times;
    /// - a sheath that arrives together with a wield passes too, because that
    ///   is the agent switching weapons and native lowers the off hand itself
    ///   as part of raising a bow;
    /// - once the main blade is no longer in hand there is no pair to hold, so
    ///   nothing is dropped at all.
    ///
    /// When to fight, when to shoot, and whether to obey the player are
    /// therefore decided entirely by native, exactly as they are for a soldier
    /// carrying no dual blades at all.
    /// </summary>
    internal sealed class GwpDualBladeFightGripComponent : AgentComponent
    {
        private const Agent.EventControlFlag Sheath =
            Agent.EventControlFlag.Sheath0
            | Agent.EventControlFlag.Sheath1;

        private readonly GwpDualBladeAgentState _state;

        internal GwpDualBladeFightGripComponent(
            Agent agent,
            GwpDualBladeAgentState state)
            : base(agent)
        {
            _state = state;
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

            if ((eventFlag & Sheath) == Agent.EventControlFlag.None)
                return;

            // Part of a switch the agent is making, not a lone tidy-up.
            if ((eventFlag & Wield) != Agent.EventControlFlag.None)
                return;

            // The invariant only applies while the pair is what the agent is
            // holding.
            if (Agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.Weapon1
                || Agent.GetOffhandWieldedItemIndex()
                    != EquipmentIndex.WeaponItemBeginSlot)
            {
                return;
            }

            // The invariant stands aside for exactly one thing: an order to
            // start shooting that has not been carried out yet. Inside a
            // formation this lone sheath is the only way native has of
            // beginning that switch, so swallowing it swallows the order.
            //
            // Granting it on a looser test - any time the agent was not
            // fighting - was tried and was worse: pair rebuilds beyond the
            // first went from 11 in a battle to 384, because native took the
            // free hand without raising a bow and the redraw came back around.
            // An outstanding order is a fact about the agent, not a guess about
            // native's intentions, and it clears itself the moment the bow is
            // up.
            if (_state.RangedRequested)
                return;

            eventFlag &= ~Sheath;
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
