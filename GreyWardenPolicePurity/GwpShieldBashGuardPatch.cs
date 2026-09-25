using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Gives the hand-held Grey Warden giant shield the same passive coverage
    /// shape that the engine uses while the item is carried on the back.
    ///
    /// The important distinction in Bannerlord's WeaponData is:
    ///   Item.BodyName          -> passive/back Shape
    ///   Item.CollisionBodyName -> active shield CollisionShape
    ///
    /// Community implementations (Shields - They Block Things and RBM) use
    /// public AttackCollisionData reconstruction rather than mutating native
    /// private fields.  A shield carried on the back retains the engine's
    /// CollidedWithShieldOnBack path.  A shield already wielded in the off hand
    /// is reconstructed as an ordinary shield block so the engine, rather than
    /// a forced animation, owns its reaction and break feedback.
    /// </summary>
    internal static class GwpPassiveHeldShieldCollision
    {
        private const string MetalWeaponOnMetalShieldSound =
            "event:/mission/combat/impact/metal_weapon/metal_shield";
        private const string MetalOnMetalHitParticle =
            "psys_game_metal_metal_coll";
        private const string MetalShieldBrokenSound =
            "event:/mission/combat/shield/metal_broken";
        private const string ShieldBreakParticle =
            "psys_game_shield_break";

        private static int _shieldHitSoundId = -1;
        private static int _metalHitParticleId = -1;
        private static int _shieldBreakSoundId = -1;
        private static int _shieldBreakParticleId = -1;
        private static string? _loadedShieldCollisionBodyName;
        private static bool _shieldCollisionBoundsValid;
        private static BoundingBox _shieldCollisionBounds;

        [ThreadStatic]
        private static bool _processingSyntheticHeldShieldBlock;

        private delegate void GetDefendCollisionResultsDelegate(
            Agent attackerAgent,
            Agent defenderAgent,
            CombatCollisionResult collisionResult,
            int attackerWeaponSlotIndex,
            bool isAlternativeAttack,
            StrikeType strikeType,
            Agent.UsageDirection attackDirection,
            float collisionDistanceOnWeapon,
            float attackProgress,
            bool attackIsParried,
            bool isPassiveUsageHit,
            bool isHeavyAttack,
            ref float defenderStunPeriod,
            ref float attackerStunPeriod,
            ref bool crushedThrough,
            ref bool chamber);

        private static readonly GetDefendCollisionResultsDelegate?
            NativeGetDefendCollisionResults =
                CreateGetDefendCollisionResultsDelegate();

        // Align to the authored active shield collision body by using the
        // live off-hand bone plus WeaponComponentData.Frame.  The two broad
        // shield-face axes receive the player's requested 30% coverage
        // expansion, while the thin depth axis receives a 20% expansion.
        private const float PassiveShieldFaceCoverageScale = 1.30f;
        private const float PassiveShieldDepthScale = 1.20f;
        private static readonly List<PendingShieldBreak>
            PendingShieldBreaks = new();

        private readonly struct PendingShieldBreak
        {
            internal readonly Agent Agent;
            internal readonly EquipmentIndex Slot;

            internal PendingShieldBreak(Agent agent, EquipmentIndex slot)
            {
                Agent = agent;
                Slot = slot;
            }
        }

        internal static bool TryConvertHeldShieldMeleeHit(
            Agent? attacker,
            Agent? victim,
            ref AttackCollisionData collisionData,
            out Vec3 shieldImpactPosition)
        {
            shieldImpactPosition = collisionData.CollisionGlobalPosition;
            if (attacker == null
                || victim == null
                || !attacker.IsHuman
                || !victim.IsHuman
                || !attacker.IsEnemyOf(victim)
                || collisionData.IsAlternativeAttack
                || collisionData.AttackBlockedWithShield
                || !collisionData.IsColliderAgent
                || !TryGetHeldGiantShield(
                    victim,
                    out WeakGameEntity shieldEntity,
                    out string collisionBodyName)
                || !IncomingMeleeSegmentHitsPassiveBody(
                    attacker,
                    victim,
                    collisionBodyName,
                    in collisionData,
                    out shieldImpactPosition,
                    out Vec3 shieldImpactNormal))
            {
                return false;
            }

            if (!ConvertHeldShieldMeleeHitToBlock(
                    attacker,
                    victim,
                    ref collisionData,
                    shieldImpactPosition,
                    shieldImpactNormal))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Applies the Grey Warden's action-only extension of the unraised
        /// shield passive.  While the native kick or weapon-bash animation is
        /// in progress, a held giant shield protects against every incoming
        /// melee body hit, rather than only hits whose weapon segment crosses
        /// the shield volume.  The collision is still reconstructed as the
        /// same ordinary passive held-shield block, so all durability and
        /// feedback use the one shared passive-shield path below.
        /// </summary>
        internal static bool TryConvertAlternativeAttackForcedPassiveGuard(
            Agent? attacker,
            Agent? victim,
            ref AttackCollisionData collisionData)
        {
            if (attacker == null
                || victim == null
                || !attacker.IsHuman
                || !victim.IsHuman
                || !attacker.IsEnemyOf(victim)
                || !collisionData.IsColliderAgent
                || collisionData.CollisionResult
                    != CombatCollisionResult.StrikeAgent
                || collisionData.AttackBlockedWithShield
                || !GwpKickBehavior.IsEligibleGreyWarden(victim)
                || !GwpKickInputComponent
                    .IsPerformingAlternativeAttack(victim)
                || !TryGetHeldGiantShield(victim, out _, out _))
            {
                return false;
            }

            Vec3 impactNormal = collisionData.CollisionGlobalNormal;
            if (impactNormal.LengthSquared < 0.0001f)
                impactNormal = Vec3.Up;

            // Deliberately do not call IncomingMeleeSegmentHitsPassiveBody:
            // this guard is the action-time guarantee requested for kick and
            // shield-bash, not the normal geometry-only passive interception.
            return ConvertHeldShieldMeleeHitToBlock(
                attacker,
                victim,
                ref collisionData,
                collisionData.CollisionGlobalPosition,
                impactNormal);
        }

        private static bool ConvertHeldShieldMeleeHitToBlock(
            Agent attacker,
            Agent victim,
            ref AttackCollisionData collisionData,
            Vec3 shieldImpactPosition,
            Vec3 shieldImpactNormal)
        {
            MissionWeapon heldShield = victim.WieldedOffhandWeapon;
            WeaponComponentData? heldShieldUsage =
                heldShield.CurrentUsageItem;
            if (heldShieldUsage == null)
                return false;

            sbyte shieldBone = victim.Monster.OffHandItemBoneIndex;
            if (shieldBone < 0)
                return false;

            int shieldPhysicsMaterialIndex =
                collisionData.PhysicsMaterialIndex;
            if (!string.IsNullOrEmpty(heldShieldUsage.PhysicsMaterial))
            {
                shieldPhysicsMaterialIndex = PhysicsMaterial.GetFromName(
                    heldShieldUsage.PhysicsMaterial).Index;
            }

            float defenderStunPeriod =
                collisionData.DefenderStunPeriod;
            float attackerStunPeriod =
                collisionData.AttackerStunPeriod;
            CalculateNativeDefendStun(
                attacker,
                victim,
                in collisionData,
                ref defenderStunPeriod,
                ref attackerStunPeriod);

            // This is the public reconstruction pattern used by RBM for a
            // CollidedWithShieldOnBack melee contact.  The off-hand slot is a
            // real wielded shield here, so AttackBlockedWithShield is safe and
            // lets Mission calculate the ordinary shield reaction/damage.
            // Do not tag this as a back-shield hit.  In Bannerlord 1.4.7 that
            // tag selects a different native collision path and prevents the
            // held shield from using the ordinary shield damage/break chain.
            // CollisionResult.Blocked already prevents a body RegisterBlow.
            AttackCollisionData converted =
                AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    _attackBlockedWithShield: true,
                    // The injected native guard direction is selected from the
                    // incoming attack using the engine/community mapping, so
                    // this synthetic shield interception is a correct-side
                    // block rather than the old body hit.
                    _correctSideShieldBlock: true,
                    _isAlternativeAttack:
                        collisionData.IsAlternativeAttack,
                    _isColliderAgent: collisionData.IsColliderAgent,
                    _collidedWithShieldOnBack: false,
                    _isMissile: collisionData.IsMissile,
                    _isMissileBlockedWithWeapon:
                        collisionData.MissileBlockedWithWeapon,
                    _missileHasPhysics: collisionData.MissileHasPhysics,
                    _entityExists: collisionData.EntityExists,
                    _thrustTipHit: collisionData.ThrustTipHit,
                    _missileGoneUnderWater:
                        collisionData.MissileGoneUnderWater,
                    _missileGoneOutOfBorder:
                        collisionData.MissileGoneOutOfBorder,
                    collisionResult: CombatCollisionResult.Blocked,
                    affectorWeaponSlotOrMissileIndex:
                        collisionData.AffectorWeaponSlotOrMissileIndex,
                    StrikeType: collisionData.StrikeType,
                    DamageType: collisionData.DamageType,
                    // The original unmanaged contact was a body wound.  A
                    // stock held-shield contact reports the off-hand item bone
                    // and the shield's physics material instead.
                    CollisionBoneIndex: shieldBone,
                    VictimHitBodyPart: collisionData.VictimHitBodyPart,
                    AttackBoneIndex: collisionData.AttackBoneIndex,
                    AttackDirection: collisionData.AttackDirection,
                    PhysicsMaterialIndex: shieldPhysicsMaterialIndex,
                    CollisionHitResultFlags:
                        collisionData.CollisionHitResultFlags,
                    AttackProgress: collisionData.AttackProgress,
                    CollisionDistanceOnWeapon:
                        collisionData.CollisionDistanceOnWeapon,
                    AttackerStunPeriod: attackerStunPeriod,
                    DefenderStunPeriod: defenderStunPeriod,
                    MissileTotalDamage: collisionData.MissileTotalDamage,
                    MissileInitialSpeed:
                        collisionData.MissileStartingBaseSpeed,
                    ChargeVelocity: collisionData.ChargeVelocity,
                    FallSpeed: collisionData.FallSpeed,
                    WeaponRotUp: collisionData.WeaponRotUp,
                    _weaponBlowDir: collisionData.WeaponBlowDir,
                    // The native callback first reported a body wound behind
                    // the shield. Move the effective contact back to the
                    // shield-box entry point so thrusts react at the shield
                    // face rather than visually travelling into the body.
                    CollisionGlobalPosition: shieldImpactPosition,
                    MissileVelocity: collisionData.MissileVelocity,
                    MissileStartingPosition:
                        collisionData.MissileStartingPosition,
                    VictimAgentCurVelocity:
                        collisionData.VictimAgentCurVelocity,
                    GroundNormal: shieldImpactNormal);

            converted.BaseMagnitude = collisionData.BaseMagnitude;
            converted.MovementSpeedDamageModifier =
                collisionData.MovementSpeedDamageModifier;
            converted.SelfInflictedDamage =
                collisionData.SelfInflictedDamage;
            converted.InflictedDamage = collisionData.InflictedDamage;
            converted.AbsorbedByArmor = collisionData.AbsorbedByArmor;
            converted.IsShieldBroken = collisionData.IsShieldBroken;
            converted.IsSneakAttack = collisionData.IsSneakAttack;
            collisionData = converted;
            return true;
        }

        internal static bool IsProcessingSyntheticHeldShieldBlock =>
            _processingSyntheticHeldShieldBlock;

        internal static void BeginSyntheticHeldShieldBlock()
        {
            _processingSyntheticHeldShieldBlock = true;
        }

        internal static void EndSyntheticHeldShieldBlock()
        {
            _processingSyntheticHeldShieldBlock = false;
        }

        internal static void PlayNativeMetalShieldImpact(
            Agent attacker,
            Agent victim,
            Vec3 collisionPosition)
        {
            Mission? mission = Mission.Current;
            if (mission == null)
                return;

            if (_shieldHitSoundId < 0)
            {
                _shieldHitSoundId = SoundEvent.GetEventIdFromString(
                    MetalWeaponOnMetalShieldSound);
            }

            if (_shieldHitSoundId >= 0)
            {
                mission.MakeSound(
                    _shieldHitSoundId,
                    collisionPosition,
                    soundCanBePredicted: false,
                    isReliable: true,
                    relatedAgent1: attacker.Index,
                    relatedAgent2: victim.Index);
            }
        }

        internal static void SetNativeMetalShieldHitParticles(
            ref HitParticleResultData hitParticleResultData)
        {
            if (_metalHitParticleId < 0)
            {
                _metalHitParticleId =
                    ParticleSystemManager.GetRuntimeIdByName(
                        MetalOnMetalHitParticle);
            }

            // This is the same HitParticleResultData replacement pattern used
            // by Shields - They Block Things and RBM.  Supplying the stock
            // metal/metal collision effect for every phase prevents the old
            // body hit from leaking blood at any weapon penetration phase.
            hitParticleResultData.StartHitParticleIndex =
                _metalHitParticleId;
            hitParticleResultData.ContinueHitParticleIndex =
                _metalHitParticleId;
            hitParticleResultData.EndHitParticleIndex =
                _metalHitParticleId;
        }

        private static GetDefendCollisionResultsDelegate?
            CreateGetDefendCollisionResultsDelegate()
        {
            MethodInfo? method = AccessTools.Method(
                typeof(MissionCombatMechanicsHelper),
                "GetDefendCollisionResults");
            if (method == null)
                return null;

            return (GetDefendCollisionResultsDelegate)
                Delegate.CreateDelegate(
                    typeof(GetDefendCollisionResultsDelegate),
                    method);
        }

        private static void CalculateNativeDefendStun(
            Agent attacker,
            Agent victim,
            in AttackCollisionData collisionData,
            ref float defenderStunPeriod,
            ref float attackerStunPeriod)
        {
            GetDefendCollisionResultsDelegate? calculate =
                NativeGetDefendCollisionResults;
            if (calculate == null)
                return;

            bool crushedThrough = false;
            bool chamber = false;
            calculate(
                attacker,
                victim,
                CombatCollisionResult.Blocked,
                collisionData.AffectorWeaponSlotOrMissileIndex,
                collisionData.IsAlternativeAttack,
                (StrikeType)collisionData.StrikeType,
                collisionData.AttackDirection,
                collisionData.CollisionDistanceOnWeapon,
                collisionData.AttackProgress,
                attackIsParried: false,
                isPassiveUsageHit: attacker.IsDoingPassiveAttack,
                isHeavyAttack: false,
                ref defenderStunPeriod,
                ref attackerStunPeriod,
                ref crushedThrough,
                ref chamber);
        }

        internal static bool HasHeldGiantShield(Agent victim)
        {
            return TryGetHeldGiantShield(victim, out _, out _);
        }

        internal static bool HasGiantShieldCarriedOnBack(Agent victim)
        {
            EquipmentIndex wieldedOffhand =
                victim.GetOffhandWieldedItemIndex();
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots;
                 slot++)
            {
                if (slot == wieldedOffhand)
                    continue;

                MissionWeapon weapon = victim.Equipment[slot];
                if (!weapon.IsEmpty
                    && weapon.Item != null
                    && GwpIds.IsGreyWardenLargeShieldItemId(
                        weapon.Item.StringId)
                    && weapon.IsShield())
                {
                    return true;
                }
            }

            return false;
        }

        private static void PlayShieldBreakFeedback(
            Agent victim,
            EquipmentIndex shieldSlot,
            in AttackCollisionData collisionData)
        {
            // Use the shield entity itself, not the body wound behind it.  This
            // is especially important for a shield carried on the back: the
            // native collision point can be on the protected body bone while
            // the visible shield centre is offset behind the agent.
            MatrixFrame effectGlobal = MatrixFrame.Identity;
            WeakGameEntity shieldEntity =
                victim.GetWeaponEntityFromEquipmentSlot(shieldSlot);
            if (shieldEntity.IsValid)
            {
                effectGlobal = shieldEntity.GetGlobalFrame();
            }
            else
            {
                effectGlobal.origin =
                    collisionData.CollisionGlobalPosition;
            }
            Vec3 impactPosition = effectGlobal.origin;
            Mission? mission = Mission.Current;
            if (mission != null)
            {
                if (_shieldBreakSoundId < 0)
                {
                    _shieldBreakSoundId = SoundEvent.GetEventIdFromString(
                        MetalShieldBrokenSound);
                }

                if (_shieldBreakSoundId >= 0)
                {
                    mission.MakeSound(
                        _shieldBreakSoundId,
                        impactPosition,
                        soundCanBePredicted: false,
                        isReliable: true,
                        relatedAgent1: -1,
                        relatedAgent2: -1);
                }
            }

            Mission? currentMission = Mission.Current;
            if (currentMission == null)
                return;

            if (_shieldBreakParticleId < 0)
            {
                _shieldBreakParticleId =
                    ParticleSystemManager.GetRuntimeIdByName(
                        ShieldBreakParticle);
            }
            if (_shieldBreakParticleId < 0)
                return;

            // The stock break effect is a one-shot world-space burst.  Using
            // Scene.CreateBurstParticle is the same official path used by
            // destructible objects and keeps the burst visible even though the
            // shield is safely removed on the following mission tick.
            currentMission.Scene.CreateBurstParticle(
                _shieldBreakParticleId,
                effectGlobal);
        }

        private static bool TryGetHeldGiantShield(
            Agent victim,
            out WeakGameEntity shieldEntity,
            out string collisionBodyName)
        {
            shieldEntity = WeakGameEntity.Invalid;
            collisionBodyName = string.Empty;

            MissionWeapon shield = victim.WieldedOffhandWeapon;
            if (shield.IsEmpty
                || shield.Item == null
                || shield.CurrentUsageItem == null
                || !shield.CurrentUsageItem.IsShield
                || !GwpIds.IsGreyWardenLargeShieldItemId(
                    shield.Item.StringId)
                || string.IsNullOrEmpty(shield.Item.CollisionBodyName))
            {
                return false;
            }

            EquipmentIndex shieldSlot =
                victim.GetOffhandWieldedItemIndex();
            if (shieldSlot == EquipmentIndex.None)
                return false;

            shieldEntity =
                victim.GetWeaponEntityFromEquipmentSlot(shieldSlot);
            if (!shieldEntity.IsValid)
                return false;

            // CollisionBodyName is shield_body_name (bo_wlarge_shield), the
            // same authored shield shape selected by the active-block path.
            collisionBodyName = shield.Item.CollisionBodyName;
            return true;
        }

        private static bool IncomingMeleeSegmentHitsPassiveBody(
            Agent attacker,
            Agent victim,
            string collisionBodyName,
            in AttackCollisionData collisionData,
            out Vec3 shieldImpactPosition,
            out Vec3 shieldImpactNormal)
        {
            shieldImpactPosition = collisionData.CollisionGlobalPosition;
            shieldImpactNormal = collisionData.CollisionGlobalNormal;
            int weaponSlotValue =
                collisionData.AffectorWeaponSlotOrMissileIndex;
            if (weaponSlotValue <
                    (int)EquipmentIndex.WeaponItemBeginSlot
                || weaponSlotValue >=
                    (int)EquipmentIndex.NumAllWeaponSlots
                || attacker.Equipment[
                    (EquipmentIndex)weaponSlotValue].IsEmpty)
            {
                // Kicks and other attacks without a physical weapon cannot
                // collide with the shield body.
                return false;
            }

            sbyte attackHandBone = collisionData.AttackBoneIndex;
            if (attackHandBone < 0)
                attackHandBone = attacker.Monster.MainHandItemBoneIndex;
            if (attackHandBone < 0)
                return false;

            Vec3 hit = collisionData.CollisionGlobalPosition;

            // CollisionGlobalPosition is the exact native wound/contact
            // point. AttackBoneIndex is the hand/item attachment bone for
            // this specific attack. Transform that live bone frame through
            // AgentVisuals exactly as Mission does for collision bones, then
            // test the finite hand-to-wound segment requested by the user.
            MatrixFrame handLocal = attacker.AgentVisuals
                .GetSkeleton()
                .GetBoneEntitialFrameWithIndex(attackHandBone);
            MatrixFrame attackerFrame =
                attacker.AgentVisuals.GetGlobalFrame();
            MatrixFrame handGlobal =
                attackerFrame.TransformToParent(in handLocal);
            Vec3 start = handGlobal.origin;
            if ((hit - start).LengthSquared < 0.0001f)
                return false;

            sbyte shieldBone = victim.Monster.OffHandItemBoneIndex;
            if (shieldBone < 0)
                return false;

            // The weapon entity frame returned for a wielded item is rooted
            // in agent/entity space and is not shield-local. Start from the
            // animated off-hand item bone...
            MatrixFrame shieldBoneLocal = victim.AgentVisuals
                .GetSkeleton()
                .GetBoneEntitialFrameWithIndex(shieldBone);
            MatrixFrame victimFrame = victim.AgentVisuals.GetGlobalFrame();
            MatrixFrame shieldBoneGlobal =
                victimFrame.TransformToParent(in shieldBoneLocal);

            // ...then apply the item's authored WeaponComponentData.Frame.
            // For wlarge_shield this is the XML position (0,-0.15,0) and
            // rotation (-90,-90,-8). Omitting this frame was exactly why the
            // collision box sat beside the visible shield instead of on it.
            WeaponComponentData? shieldUsage =
                victim.WieldedOffhandWeapon.CurrentUsageItem;
            if (shieldUsage == null)
                return false;
            MatrixFrame authoredItemFrame = shieldUsage.Frame;
            MatrixFrame shieldFrame = shieldBoneGlobal.TransformToParent(
                in authoredItemFrame);
            Vec3 localStart =
                shieldFrame.TransformToLocalNonOrthogonal(in start);
            Vec3 localEnd =
                shieldFrame.TransformToLocalNonOrthogonal(in hit);

            EnsureShieldCollisionBoundsLoaded(collisionBodyName);
            if (!_shieldCollisionBoundsValid)
                return false;

            GetPassiveShieldCoverageBounds(
                in _shieldCollisionBounds,
                out Vec3 expandedMin,
                out Vec3 expandedMax);

            if (!SegmentIntersectsBox(
                    localStart,
                    localEnd,
                    expandedMin,
                    expandedMax,
                    out float closestIntersection))
            {
                return false;
            }

            Vec3 localImpact = localStart
                + (localEnd - localStart) * closestIntersection;
            shieldImpactPosition =
                shieldFrame.TransformToParent(in localImpact);
            Vec3 localImpactNormal = GetBoxImpactNormal(
                in localImpact,
                in expandedMin,
                in expandedMax);
            shieldImpactNormal = shieldFrame.rotation.TransformToParent(
                in localImpactNormal);
            shieldImpactNormal.Normalize();
            return true;
        }

        private static void GetPassiveShieldCoverageBounds(
            in BoundingBox authoredBounds,
            out Vec3 expandedMinimum,
            out Vec3 expandedMaximum)
        {
            Vec3 center = (authoredBounds.min + authoredBounds.max) * 0.5f;
            Vec3 halfExtent = (authoredBounds.max - authoredBounds.min) * 0.5f;

            // Shield collision bodies have one very thin local axis. Expand
            // the two face dimensions by 30% and that depth axis by 20%.
            if (halfExtent.x <= halfExtent.y && halfExtent.x <= halfExtent.z)
            {
                halfExtent.x *= PassiveShieldDepthScale;
                halfExtent.y *= PassiveShieldFaceCoverageScale;
                halfExtent.z *= PassiveShieldFaceCoverageScale;
            }
            else if (halfExtent.y <= halfExtent.x && halfExtent.y <= halfExtent.z)
            {
                halfExtent.x *= PassiveShieldFaceCoverageScale;
                halfExtent.y *= PassiveShieldDepthScale;
                halfExtent.z *= PassiveShieldFaceCoverageScale;
            }
            else
            {
                halfExtent.x *= PassiveShieldFaceCoverageScale;
                halfExtent.y *= PassiveShieldFaceCoverageScale;
                halfExtent.z *= PassiveShieldDepthScale;
            }

            expandedMinimum = center - halfExtent;
            expandedMaximum = center + halfExtent;
        }

        private static Vec3 GetBoxImpactNormal(
            in Vec3 localImpact,
            in Vec3 minimum,
            in Vec3 maximum)
        {
            float nearestDistance = MathF.Abs(localImpact.x - minimum.x);
            Vec3 result = new Vec3(-1f, 0f, 0f);

            float distance = MathF.Abs(maximum.x - localImpact.x);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                result = new Vec3(1f, 0f, 0f);
            }

            distance = MathF.Abs(localImpact.y - minimum.y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                result = new Vec3(0f, -1f, 0f);
            }

            distance = MathF.Abs(maximum.y - localImpact.y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                result = new Vec3(0f, 1f, 0f);
            }

            distance = MathF.Abs(localImpact.z - minimum.z);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                result = new Vec3(0f, 0f, -1f);
            }

            distance = MathF.Abs(maximum.z - localImpact.z);
            if (distance < nearestDistance)
                result = new Vec3(0f, 0f, 1f);

            return result;
        }

        private static void EnsureShieldCollisionBoundsLoaded(
            string collisionBodyName)
        {
            if (_loadedShieldCollisionBodyName == collisionBodyName)
                return;

            _loadedShieldCollisionBodyName = collisionBodyName;
            _shieldCollisionBoundsValid = false;
            PhysicsShape? shape = PhysicsShape.GetFromResource(
                collisionBodyName,
                mayReturnNull: true);
            if (shape == null)
                return;

            shape.GetBoundingBox(out BoundingBox bounds);
            Vec3 size = bounds.max - bounds.min;
            int usableAxes = 0;
            if (size.x > 0.001f)
                usableAxes++;
            if (size.y > 0.001f)
                usableAxes++;
            if (size.z > 0.001f)
                usableAxes++;
            if (usableAxes < 2)
                return;

            _shieldCollisionBounds = bounds;
            _shieldCollisionBoundsValid = true;
        }

        internal static bool ApplyPassiveShieldDurabilityDamage(
            Agent victim,
            ref AttackCollisionData collisionData,
            bool heldPassiveBlock)
        {
            if (!TryGetPassiveShieldSlot(
                    victim,
                    heldPassiveBlock,
                    out EquipmentIndex shieldSlot))
            {
                return false;
            }

            MissionWeapon shield = victim.Equipment[shieldSlot];
            int oldHitPoints = shield.HitPoints;
            if (oldHitPoints <= 0)
                return false;

            // After Mission.MeleeHitCallback, held-passive contacts already
            // contain the engine's calculated shield damage, through the damage
            // model (the shield's trait already applied there). Back contacts contain
            // the cancelled blow's damage, so the shield's trait applies here. A
            // passive interception costs what an active block would
            // (2026-09-24: one durability rule, no triple loss); never free.
            float baseDamage = MathF.Max(1, collisionData.InflictedDamage);
            GwpWeaponTrait? shieldTrait = GwpWeaponTraits.Of(shield.Item);
            if (!heldPassiveBlock && shieldTrait != null)
                baseDamage *= shieldTrait.ShieldDamageTakenMultiplier;
            int durabilityDamage = MathF.Max(1, MathF.Round(baseDamage));

            int newHitPoints = MathF.Max(
                0,
                oldHitPoints - durabilityDamage);

            if (newHitPoints > 0)
            {
                victim.ChangeWeaponHitPoints(
                    shieldSlot,
                    (short)newHitPoints);
                return false;
            }

            // Do not remove equipment from inside MeleeHitCallback while the
            // native collision code still owns references to that shield.
            // Emit the stock shield-break burst/sound while the impact and
            // skeleton are still valid, leave one point for this callback,
            // then perform zero-HP -> removal on the next mission tick.
            PlayShieldBreakFeedback(
                victim,
                shieldSlot,
                in collisionData);
            victim.ChangeWeaponHitPoints(shieldSlot, 1);
            QueueShieldBreak(victim, shieldSlot);
            collisionData.IsShieldBroken = false;
            return true;
        }

        internal static void ProcessQueuedShieldBreaks()
        {
            for (int i = PendingShieldBreaks.Count - 1; i >= 0; i--)
            {
                PendingShieldBreak pending = PendingShieldBreaks[i];
                PendingShieldBreaks.RemoveAt(i);
                Agent agent = pending.Agent;
                if (agent == null || !agent.IsActive())
                    continue;

                MissionWeapon weapon = agent.Equipment[pending.Slot];
                if (weapon.IsEmpty
                    || weapon.Item == null
                    || !GwpIds.IsGreyWardenLargeShieldItemId(
                        weapon.Item.StringId)
                    || weapon.HitPoints > 1)
                {
                    continue;
                }

                agent.ChangeWeaponHitPoints(pending.Slot, 0);
                agent.RemoveEquippedWeapon(pending.Slot);
            }
        }

        private static bool TryGetPassiveShieldSlot(
            Agent victim,
            bool heldPassiveBlock,
            out EquipmentIndex shieldSlot)
        {
            shieldSlot = EquipmentIndex.None;
            EquipmentIndex heldSlot = victim.GetOffhandWieldedItemIndex();
            if (heldPassiveBlock)
            {
                if (heldSlot == EquipmentIndex.None)
                    return false;
                MissionWeapon held = victim.Equipment[heldSlot];
                if (held.IsEmpty
                    || held.Item == null
                    || !GwpIds.IsGreyWardenLargeShieldItemId(
                        held.Item.StringId))
                {
                    return false;
                }

                shieldSlot = heldSlot;
                return true;
            }

            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots;
                 slot++)
            {
                if (slot == heldSlot)
                    continue;
                MissionWeapon weapon = victim.Equipment[slot];
                if (!weapon.IsEmpty
                    && weapon.Item != null
                    && GwpIds.IsGreyWardenLargeShieldItemId(
                        weapon.Item.StringId)
                    && weapon.IsShield())
                {
                    shieldSlot = slot;
                    return true;
                }
            }

            return false;
        }

        private static void QueueShieldBreak(
            Agent agent,
            EquipmentIndex slot)
        {
            foreach (PendingShieldBreak pending in PendingShieldBreaks)
            {
                if (pending.Agent == agent && pending.Slot == slot)
                    return;
            }

            PendingShieldBreaks.Add(new PendingShieldBreak(agent, slot));
        }

        private static bool SegmentIntersectsBox(
            Vec3 start,
            Vec3 end,
            Vec3 minimum,
            Vec3 maximum,
            out float entryTime)
        {
            entryTime = 0f;
            float exitTime = 1f;
            Vec3 delta = end - start;
            return ClipSegmentAxis(
                    start.x,
                    delta.x,
                    minimum.x,
                    maximum.x,
                    ref entryTime,
                    ref exitTime)
                && ClipSegmentAxis(
                    start.y,
                    delta.y,
                    minimum.y,
                    maximum.y,
                    ref entryTime,
                    ref exitTime)
                && ClipSegmentAxis(
                    start.z,
                    delta.z,
                    minimum.z,
                    maximum.z,
                    ref entryTime,
                    ref exitTime);
        }

        private static bool ClipSegmentAxis(
            float start,
            float delta,
            float minimum,
            float maximum,
            ref float entryTime,
            ref float exitTime)
        {
            const float epsilon = 0.00001f;
            if (MathF.Abs(delta) < epsilon)
                return start >= minimum && start <= maximum;

            float first = (minimum - start) / delta;
            float second = (maximum - start) / delta;
            if (first > second)
            {
                float swap = first;
                first = second;
                second = swap;
            }

            entryTime = MathF.Max(entryTime, first);
            exitTime = MathF.Min(exitTime, second);
            return entryTime <= exitTime;
        }

    }

    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class GwpPassiveHeldShieldMeleePatch
    {
        private enum ShieldInterceptionKind
        {
            None,
            AlternativeAttackForcedPassive,
            PassiveHeld
        }

        [HarmonyPrefix]
        private static void BeforeMeleeHit(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction,
            out ShieldInterceptionKind __state)
        {
            __state = ShieldInterceptionKind.None;
            if (GwpPassiveHeldShieldCollision
                .TryConvertAlternativeAttackForcedPassiveGuard(
                    attacker,
                    victim,
                    ref collisionData))
            {
                __state = ShieldInterceptionKind
                    .AlternativeAttackForcedPassive;
                GwpPassiveHeldShieldCollision.BeginSyntheticHeldShieldBlock();
                colReaction = MeleeCollisionReaction.Bounced;
                GwpPassiveHeldShieldCollision.PlayNativeMetalShieldImpact(
                    attacker,
                    victim,
                    collisionData.CollisionGlobalPosition);
                return;
            }

            if (!GwpPassiveHeldShieldCollision.TryConvertHeldShieldMeleeHit(
                    attacker,
                    victim,
                    ref collisionData,
                    out _))
            {
                return;
            }

            __state = ShieldInterceptionKind.PassiveHeld;
            GwpPassiveHeldShieldCollision.BeginSyntheticHeldShieldBlock();
            colReaction = MeleeCollisionReaction.Bounced;
            GwpPassiveHeldShieldCollision.PlayNativeMetalShieldImpact(
                attacker,
                victim,
                collisionData.CollisionGlobalPosition);
        }

        [HarmonyPostfix]
        private static void AfterMeleeHit(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction,
            ShieldInterceptionKind __state)
        {
            if (victim == null || collisionData.IsMissile)
            {
                if (__state != ShieldInterceptionKind.None)
                {
                    GwpPassiveHeldShieldCollision
                        .EndSyntheticHeldShieldBlock();
                }
                return;
            }

            if (__state == ShieldInterceptionKind
                .AlternativeAttackForcedPassive)
            {
                try
                {
                    // The native callback began as a body wound, so it has no
                    // stock held-shield durability event: apply the shield
                    // damage here. Nothing else - like a native shield block
                    // the defender takes no health loss and no extra reaction
                    // (2026-09-25).
                    colReaction = MeleeCollisionReaction.Bounced;
                    GwpPassiveHeldShieldCollision
                        .ApplyPassiveShieldDurabilityDamage(
                            victim,
                            ref collisionData,
                            heldPassiveBlock: true);
                }
                finally
                {
                    GwpPassiveHeldShieldCollision
                        .EndSyntheticHeldShieldBlock();
                }
                return;
            }

            if (__state == ShieldInterceptionKind.PassiveHeld)
            {
                try
                {
                    // The native OnShieldDamaged callback is only emitted for
                    // a collision the unmanaged layer originally recognized
                    // as a shield. This interception began as a body hit, so
                    // apply the already-calculated shield damage explicitly.
                    // The rest is native's block: no health loss, no extra
                    // reaction on the defender (2026-09-25).
                    colReaction = MeleeCollisionReaction.Bounced;
                    GwpPassiveHeldShieldCollision
                        .ApplyPassiveShieldDurabilityDamage(
                            victim,
                            ref collisionData,
                            heldPassiveBlock: true);
                }
                finally
                {
                    GwpPassiveHeldShieldCollision
                        .EndSyntheticHeldShieldBlock();
                }
                return;
            }

            if (attacker == null
                || !attacker.IsEnemyOf(victim)
                || !collisionData.CollidedWithShieldOnBack
                || !GwpPassiveHeldShieldCollision
                    .HasGiantShieldCarriedOnBack(victim))
            {
                return;
            }

            // A shield on the back is not the wielded VictimShield selected by
            // AttackInformation, so Bannerlord 1.4.7 has no corresponding
            // native durability callback for this contact. Keep only that
            // established passive/back-shield durability path manual; the body
            // blow is cancelled (GwpPassiveShieldRegisterBlowPatch) and no
            // extra reaction is added (2026-09-25).
            GwpPassiveHeldShieldCollision
                .ApplyPassiveShieldDurabilityDamage(
                    victim,
                    ref collisionData,
                    heldPassiveBlock: false);
        }
    }

    /// <summary>
    /// A synthetic passive-held interception enters MeleeHitCallback from a
    /// body contact, so the stock method would otherwise keep the body's blood
    /// particles.  Community passive-shield implementations patch this exact
    /// decision point.  Restrict the replacement to the synchronous converted
    /// callback and return Bannerlord's stock metal-on-metal hit effect.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "DecideAgentHitParticles")]
    internal static class GwpPassiveHeldShieldParticlePatch
    {
        [HarmonyPrefix]
        private static bool BeforeDecideAgentHitParticles(
            ref HitParticleResultData hprd)
        {
            if (!GwpPassiveHeldShieldCollision
                    .IsProcessingSyntheticHeldShieldBlock)
            {
                return true;
            }

            GwpPassiveHeldShieldCollision
                .SetNativeMetalShieldHitParticles(ref hprd);
            return false;
        }
    }

    internal sealed class GwpPassiveShieldBreakBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType =>
            MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            GwpPassiveHeldShieldCollision.ProcessQueuedShieldBreaks();
        }
    }

    /// <summary>
    /// Community-proven passive shield handling from
    /// "Shields - They Block Things": a CollidedWithShieldOnBack contact
    /// notifies mission behaviors but does not register a body blow.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    internal static class GwpPassiveShieldRegisterBlowPatch
    {
        [HarmonyPrefix]
        private static bool BeforeRegisterBlow(
            Mission __instance,
            Agent attacker,
            Agent victim,
            WeakGameEntity realHitEntity,
            Blow b,
            ref AttackCollisionData collisionData,
            in MissionWeapon attackerWeapon)
        {
            if (victim == null
                || !collisionData.CollidedWithShieldOnBack
                || (!GwpPassiveHeldShieldCollision.HasHeldGiantShield(victim)
                    && !GwpPassiveHeldShieldCollision
                        .HasGiantShieldCarriedOnBack(victim)))
            {
                return true;
            }

            b.VictimBodyPart = collisionData.VictimHitBodyPart;
            foreach (MissionBehavior missionBehavior
                     in __instance.MissionBehaviors)
            {
                missionBehavior.OnRegisterBlow(
                    attacker,
                    victim,
                    realHitEntity,
                    b,
                    ref collisionData,
                    in attackerWeapon);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Mission), "DecideAgentHitParticles")]
    internal static class GwpPassiveShieldHitParticlePatch
    {
        [HarmonyPrefix]
        private static bool BeforeDecideHitParticles(
            Agent victim,
            in AttackCollisionData collisionData,
            ref HitParticleResultData hprd)
        {
            if (victim == null
                || collisionData.IsMissile
                || !collisionData.CollidedWithShieldOnBack
                || (!GwpPassiveHeldShieldCollision.HasHeldGiantShield(victim)
                    && !GwpPassiveHeldShieldCollision
                        .HasGiantShieldCarriedOnBack(victim)))
            {
                return true;
            }

            // The original contact was reported against the body before it
            // was reclassified. Never emit stale body blood for a passive
            // shield block.
            hprd.Reset();
            return false;
        }
    }
}
