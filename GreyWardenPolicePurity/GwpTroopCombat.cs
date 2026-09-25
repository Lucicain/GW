using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Applies, on the mission tick, the damage a Grey Warden warhorse's rider took and
    /// passed on to that horse. The hit being resolved when
    /// the damage is known is not the place to register a second blow.
    /// </summary>
    internal sealed class GwpWarhorseDamageTransferBehavior : MissionBehavior
    {
        public GwpWarhorseDamageTransferBehavior()
        {
            GwpTroopCombat.ClearPendingHorseDamage();
        }

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            try
            {
                GwpTroopCombat.ApplyPendingHorseDamage();
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            GwpTroopCombat.ClearPendingHorseDamage();
        }
    }

    /// <summary>
    /// One Grey Warden weapon's combat effects. Chances are per body hit on an enemy.
    /// </summary>
    internal sealed class GwpWeaponTrait
    {
        // Knock an enemy on foot down; knock an enemy rider off his horse (non-lethal hit);
        // break a weapon parry (never a shield).
        internal float Knockdown;
        internal float Dismount;
        internal float ParryBreak;
        // Couched or braced (native's passive attack): lance only.
        internal float PassiveKnockdown;
        internal float PassiveDismount;
        internal bool PassiveAlwaysBreaksParry;
        // Damage this weapon does to a shield, and damage this shield takes.
        internal float ShieldDamageMultiplier = 1f;
        internal float ShieldDamageTakenMultiplier = 1f;
    }

    /// <summary>
    /// Grey Warden combat effects belong to the weapon, not to the troop (2026-09-25, user:
    /// one measure for every soldier). Whoever wields the item - any troop, a lord, the
    /// player - gets its effects. The values are the ones the troops had: the knight's
    /// ordinary attack for every Grey Warden melee weapon, his couched lance, the archers'
    /// dual blades and arrows, the heavy infantry's great shield. Kicks and shield bashes
    /// are not weapon attacks and keep their per-troop rules (dual-blade.md); the warhorse
    /// has its own (gw_warhorse).
    /// </summary>
    internal static class GwpWeaponTraits
    {
        internal const string OneHandedSwordItemId = "gwonehandedsword";
        internal const string MaceItemId = "gwmace";
        internal const string TwoHandedSwordItemId = "gwtwohandedsword";
        internal const string LanceItemId = "gwlance";
        internal const string ArrowsItemId = "gwarrows";

        private static GwpWeaponTrait Melee() => new GwpWeaponTrait
        {
            Knockdown = 0.125f,
            Dismount = 0.125f,
            ParryBreak = 0.25f
        };

        private static readonly System.Collections.Generic.Dictionary<string, GwpWeaponTrait> ByItemId =
            new System.Collections.Generic.Dictionary<string, GwpWeaponTrait>(StringComparer.OrdinalIgnoreCase)
            {
                [OneHandedSwordItemId] = Melee(),
                [MaceItemId] = Melee(),
                [TwoHandedSwordItemId] = Melee(),
                [LanceItemId] = new GwpWeaponTrait
                {
                    Knockdown = 0.125f,
                    Dismount = 0.125f,
                    ParryBreak = 0.25f,
                    PassiveKnockdown = 0.5f,
                    PassiveDismount = 0.25f,
                    PassiveAlwaysBreaksParry = true
                },
                // Either blade of the pair: the archers' former 40% (80% tier x 0.5).
                [GwpIds.DualBladeMainhandItemId] = new GwpWeaponTrait { Knockdown = 0.4f, Dismount = 0.125f, ParryBreak = 0.25f },
                [GwpIds.DualBladeOffhandItemId] = new GwpWeaponTrait { Knockdown = 0.4f, Dismount = 0.125f, ParryBreak = 0.25f },
                // Arrows knock only men on foot down, as the archers' did.
                [ArrowsItemId] = new GwpWeaponTrait { Knockdown = 0.25f, ShieldDamageMultiplier = 2f },
                // Twice the durability; still breakable.
                [GwpIds.LargeShieldItemId] = new GwpWeaponTrait { ShieldDamageTakenMultiplier = 0.5f },
                [GwpIds.BlackLargeShieldItemId] = new GwpWeaponTrait { ShieldDamageTakenMultiplier = 0.5f },
            };

        internal static GwpWeaponTrait? Of(ItemObject? item) =>
            item != null && ByItemId.TryGetValue(item.StringId, out GwpWeaponTrait trait) ? trait : null;

        /// <summary>
        /// The weapon behind a hit: the attacker's weapon slot for melee, the missile itself
        /// (looked up by index, as native's MissileHitCallback does) for a missile.
        /// </summary>
        internal static GwpWeaponTrait? OfHit(Agent attacker, in AttackCollisionData collision)
        {
            try
            {
                int index = collision.AffectorWeaponSlotOrMissileIndex;
                if (index < 0)
                    return null;
                if (!collision.IsMissile)
                    return index < (int)EquipmentIndex.NumAllWeaponSlots
                        ? Of(attacker.Equipment[(EquipmentIndex)index].Item)
                        : null;
                Mission? mission = attacker.Mission;
                if (mission == null)
                    return null;
                foreach (Mission.Missile missile in mission.MissilesList)
                {
                    if (missile.Index == index)
                        return Of(missile.Weapon.Item);
                }
                return null;
            }
            catch (Exception gwpQuietFailure)
            {
                GwpFaultTrace.WriteQuiet(gwpQuietFailure);
                return null;
            }
        }
    }

    /// <summary>
    /// Grey Warden troop combat effects decided through the native damage model
    /// and ordinary mission events only; no native hit callback is patched (the
    /// 2026-09 arrow patches on Mission.MissileHitCallback coincided with lost
    /// battle shouts).
    /// </summary>
    internal static class GwpTroopCombat
    {
        // A Grey Warden warhorse charging a man on foot, inside native's own charge rules
        // (2026-09-24, user: strengthen the native mechanism, not a new roll; damage unchanged):
        // knock-back from a frontal factor of 0.6 instead of 0.7, and the knock-down damage
        // threshold (max HP x (resistance - penetration)) x 0.75. (2026-09-25: each warhorse
        // gain halved, from 0.5 and x 0.5.)
        internal const float WarhorseChargeKnockBackFront = 0.6f;
        internal const float WarhorseChargeKnockDownThresholdScale = 0.75f;
        // Only the warhorse shrugs off twice the native small-hit threshold (x 3 until
        // 2026-09-25). Its rider uses native's threshold, even while mounted.
        internal const float WarhorseStaggerThresholdScale = 2f;
        // Share of native rears a Grey Warden warhorse actually suffers (0.5 until 2026-09-25).
        internal const float WarhorseRearShare = 0.75f;
        // Share of a hit on the rider's body that the warhorse takes instead; the rider keeps
        // the rest (2026-09-25: half each; it used to be all of it).
        internal const float WarhorseRiderDamageShare = 0.5f;
        private const string WarhorseMonsterId = "gw_warhorse";

        private struct PendingHorseDamage
        {
            internal Agent Horse;
            internal int AttackerIndex;
            internal int Damage;
            internal DamageTypes DamageType;
            internal Vec3 Direction;
        }

        private static readonly System.Collections.Generic.List<PendingHorseDamage> PendingHorseDamages =
            new System.Collections.Generic.List<PendingHorseDamage>();

        // Decided in DecideAgentShrugOffBlow, the first model question native
        // asks for a body hit, and consumed by the matching reaction question.
        [ThreadStatic] private static Agent? _victim;
        [ThreadStatic] private static bool _knockdown;
        [ThreadStatic] private static bool _dismount;

        internal static bool IsTroop(Agent? agent, string troopId) =>
            string.Equals(agent?.Character?.StringId, troopId, StringComparison.OrdinalIgnoreCase);

        private static bool Roll(float chance) => MBRandom.RandomFloat < chance;

        internal static void Reset()
        {
            _victim = null;
            _knockdown = _dismount = false;
        }

        /// <summary>
        /// Rolls archer knockdown, knight knockdown or knight dismount for one body hit.
        /// Returns true when an effect was granted, so native must not shrug it off.
        /// </summary>
        internal static bool RollBodyHit(Agent victim, in AttackCollisionData collision, in Blow blow)
        {
            Reset();
            if (!collision.IsColliderAgent || collision.InflictedDamage <= 0 || !victim.IsHuman || !victim.IsActive())
                return false;
            Agent? attacker = blow.OwnerId >= 0 ? Mission.Current?.FindAgentWithIndex(blow.OwnerId) : null;
            if (attacker == null || !attacker.IsEnemyOf(victim))
                return false;
            // Kicks and shield bashes keep their own knockdown rules (dual-blade.md).
            if (collision.IsAlternativeAttack)
                return false;
            // The weapon that landed decides, whoever holds it (GwpWeaponTraits).
            GwpWeaponTrait? trait = GwpWeaponTraits.OfHit(attacker, in collision);
            if (trait == null)
                return false;
            bool passive = !collision.IsMissile && attacker.IsDoingPassiveAttack;
            Agent? mount = victim.MountAgent;
            if (mount != null && mount.RiderAgent == victim)
                _dismount = victim.Health - collision.InflictedDamage >= 1f
                    && Roll(passive ? trait.PassiveDismount : trait.Dismount);
            else if (mount == null)
                _knockdown = Roll(passive ? trait.PassiveKnockdown : trait.Knockdown);
            if (!_knockdown && !_dismount)
                return false;
            _victim = victim;
            return true;
        }

        /// <summary>
        /// A Grey Warden warhorse carrying any rider and charging an enemy on foot. Native's ChargeDamageCallback
        /// asks only knock-back and then knock-down, with the horse as the attacker; riders hit
        /// by a charge stay entirely native.
        /// </summary>
        internal static bool IsWarhorseCharge(Agent? horse, Agent victim, in AttackCollisionData collision) =>
            collision.IsHorseCharge && IsWarhorse(horse) && horse!.RiderAgent != null
            && victim.IsHuman && victim.MountAgent == null;

        /// <summary>Native's charge knock-back test (MissionCombatMechanicsHelper) with the wider front.</summary>
        internal static bool ChargeKnocksBack(Agent horse, Agent victim, in AttackCollisionData collision)
        {
            Vec2 toVictim = (victim.Position.AsVec2 - collision.CollisionGlobalPosition.AsVec2).Normalized();
            return Math.Max(0f, Vec2.DotProduct(toVictim, horse.GetMovementDirection())) >= WarhorseChargeKnockBackFront;
        }

        /// <summary>
        /// Native's charge knock-down test (DecideCombatEffect: damage at least max HP x
        /// (knock-down resistance - horse charge penetration)) with that threshold scaled.
        /// Still only after a knock-back, as native requires.
        /// </summary>
        internal static bool ChargeKnocksDown(Agent victim, in AttackCollisionData collision, in Blow blow)
        {
            if ((blow.BlowFlag & BlowFlags.KnockBack) == 0 || (blow.BlowFlag & BlowFlags.ShrugOff) != 0)
                return false;
            float resistance = MissionGameModels.Current.AgentStatCalculateModel.GetKnockDownResistance(victim);
            float penetration = MissionGameModels.Current.AgentApplyDamageModel.GetHorseChargePenetration();
            float threshold = victim.HealthLimit * Math.Max(0f, resistance - penetration) * WarhorseChargeKnockDownThresholdScale;
            if (collision.InflictedDamage < threshold)
                return false;
            return true;
        }

        /// <summary>
        /// The last step of native's damage pipeline for a Grey Warden warhorse:
        /// while the rider is mounted on the living warhorse, half of a hit on the rider's body passes to the horse - the
        /// horse takes it next tick, the rider takes the rest now. Hits on the horse stay on the
        /// horse. Blocked hits, shields on the back and falls stay native, so a shield still
        /// wears and a fall still hurts.
        /// </summary>
        internal static float ApplyWarhorseDamageRules(in AttackInformation info, in AttackCollisionData collision, float damage)
        {
            Agent? victim = info.VictimAgent;
            if (victim == null || damage <= 0f || !IsWarhorseOrRider(victim))
                return damage;

            Agent? horse = victim.MountAgent;
            if (victim.IsMount
                || horse == null || !horse.IsActive() || horse.Health <= 0f
                || collision.IsFallDamage
                || !collision.IsColliderAgent
                || collision.CollisionResult != CombatCollisionResult.StrikeAgent
                || collision.AttackBlockedWithShield
                || collision.CollidedWithShieldOnBack
                || collision.MissileBlockedWithWeapon)
            {
                return damage;
            }

            int transferred = (int)Math.Round(damage * WarhorseRiderDamageShare);
            if (transferred > 0)
            {
                lock (PendingHorseDamages)
                {
                    PendingHorseDamages.Add(new PendingHorseDamage
                    {
                        Horse = horse,
                        AttackerIndex = info.AttackerAgent?.Index ?? horse.Index,
                        Damage = transferred,
                        DamageType = (DamageTypes)collision.DamageType,
                        Direction = collision.WeaponBlowDir
                    });
                }
            }
            return Math.Max(0f, damage - Math.Max(0, transferred));
        }

        internal static void ClearPendingHorseDamage()
        {
            lock (PendingHorseDamages)
                PendingHorseDamages.Clear();
        }

        /// <summary>
        /// Registers the passed-on damage on each horse the way native registers damage that
        /// has no weapon behind it (Mission's RegisterDrownBlow tick action), credited to the
        /// original attacker.
        /// </summary>
        internal static void ApplyPendingHorseDamage()
        {
            PendingHorseDamage[] pending;
            lock (PendingHorseDamages)
            {
                if (PendingHorseDamages.Count == 0)
                    return;
                pending = PendingHorseDamages.ToArray();
                PendingHorseDamages.Clear();
            }

            foreach (PendingHorseDamage item in pending)
            {
                Agent horse = item.Horse;
                if (horse == null || !horse.IsActive() || horse.Health <= 0f)
                    continue;

                Vec3 direction = item.Direction.LengthSquared > 0f ? item.Direction : horse.LookDirection;
                Blow blow = new Blow(item.AttackerIndex);
                blow.DamageType = item.DamageType;
                blow.BoneIndex = horse.Monster.HeadLookDirectionBoneIndex;
                blow.BaseMagnitude = item.Damage;
                blow.GlobalPosition = horse.Position;
                blow.DamagedPercentage = 1f;
                blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
                blow.SwingDirection = direction;
                blow.Direction = direction;
                blow.InflictedDamage = item.Damage;
                blow.DamageCalculated = true;
                AttackCollisionData collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    _attackBlockedWithShield: false, _correctSideShieldBlock: false, _isAlternativeAttack: false,
                    _isColliderAgent: true, _collidedWithShieldOnBack: false, _isMissile: false,
                    _isMissileBlockedWithWeapon: false, _missileHasPhysics: false, _entityExists: false,
                    _thrustTipHit: false, _missileGoneUnderWater: false, _missileGoneOutOfBorder: false,
                    CombatCollisionResult.StrikeAgent, -1, 0, 2, blow.BoneIndex, BoneBodyPartType.Chest,
                    horse.Monster.MainHandItemBoneIndex, Agent.UsageDirection.AttackLeft, -1,
                    CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
                    Vec3.Up, blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, horse.Velocity, Vec3.Up);
                horse.RegisterBlow(blow, in collisionData);
            }
        }

        internal static bool PendingKnockdown(Agent victim) => _knockdown && ReferenceEquals(_victim, victim);

        internal static bool TakeKnockdown(Agent victim)
        {
            if (!PendingKnockdown(victim))
                return false;
            _knockdown = false;
            return true;
        }

        internal static bool TakeDismount(Agent victim)
        {
            if (!_dismount || !ReferenceEquals(_victim, victim))
                return false;
            _dismount = false;
            return true;
        }

        /// <summary>
        /// Block break by the attacker's weapon against an enemy: an active attack can break
        /// a weapon parry at the weapon's chance, never a shield; a couched or braced lance
        /// always breaks a weapon parry. Native itself never crushes a passive attack.
        /// </summary>
        internal static bool WeaponCrushesBlock(Agent? attacker, Agent? defender, WeaponComponentData? defendItem, bool isPassiveUsageHit)
        {
            if (defendItem == null || defender == null || attacker == null || !attacker.IsEnemyOf(defender)
                || defendItem.IsShield)
            {
                return false;
            }
            GwpWeaponTrait? trait = GwpWeaponTraits.Of(attacker.WieldedWeapon.Item);
            if (trait == null)
                return false;
            return isPassiveUsageHit ? trait.PassiveAlwaysBreaksParry : Roll(trait.ParryBreak);
        }

        /// <summary>Called only after native decided this mount rears.</summary>
        internal static bool WarhorseRears(Agent mount)
        {
            if (!IsWarhorse(mount) || Roll(WarhorseRearShare))
                return true;
            return false;
        }

        /// <summary>The Grey Warden warhorse, identified by its monster rather than its rider.</summary>
        internal static bool IsWarhorse(Agent? agent) =>
            agent?.IsMount == true
            && string.Equals(agent.Monster?.StringId, WarhorseMonsterId, StringComparison.OrdinalIgnoreCase);

        /// <summary>The warhorse or anyone currently riding it while it is alive.</summary>
        internal static bool IsWarhorseOrRider(Agent? agent) =>
            IsWarhorse(agent)
            || (agent?.MountAgent is Agent horse && IsWarhorse(horse)
                && horse.IsActive() && horse.Health > 0f);


    }

    /// <summary>
    /// Native picks weapons 0-1, weapons 2-3 and all armour from three independently
    /// random presets, so a soldier with several presets wears a mixture. A Grey
    /// Warden troop takes one whole preset instead, with native's own per-item
    /// quality roll. Seeded spawns stay deterministic as in native.
    /// </summary>
    [HarmonyPatch(typeof(Equipment), nameof(Equipment.GetRandomEquipmentElements))]
    internal static class GwpWholePresetEquipmentPatch
    {
        private static readonly string[] Troops =
        {
            GwpIds.NewRecruitId, GwpIds.PoliceRecruitId, GwpIds.HeavyInfantryId, GwpIds.ArcherId, GwpIds.KnightId,
        };

        [HarmonyPrefix]
        private static bool Before(BasicCharacterObject character, bool randomEquipmentModifier,
            Equipment.EquipmentType equipmentType, int seed, ref Equipment __result)
        {
            try
            {
                if (equipmentType != Equipment.EquipmentType.Battle || character == null || character.IsHero
                    || !Troops.Contains(character.StringId, StringComparer.OrdinalIgnoreCase))
                    return true;
                var presets = character.BattleEquipments.ToList();
                if (presets.Count < 2)
                    return true;
                int chosen = seed == -1 ? MBRandom.RandomInt(presets.Count) : new Random(seed).Next() % presets.Count;
                var equipment = new Equipment(Equipment.EquipmentType.Battle);
                for (int i = 0; i < 12; i++)
                {
                    EquipmentElement element = presets[chosen].GetEquipmentFromSlot((EquipmentIndex)i);
                    if (randomEquipmentModifier)
                    {
                        ItemModifier? modifier = element.Item?.ItemComponent?.ItemModifierGroup?.GetRandomItemModifierLootScoreBased();
                        if (modifier != null)
                            element.SetModifier(modifier);
                    }
                    equipment[i] = element;
                }
                __result = equipment;
                return false;
            }
            catch (Exception gwpQuietFailure)
            {
                GwpFaultTrace.WriteQuiet(gwpQuietFailure);
                return true;
            }
        }
    }
}
