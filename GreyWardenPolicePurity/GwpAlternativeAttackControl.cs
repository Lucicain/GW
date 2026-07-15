using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Applies the one extra control decision requested for a Grey Warden's
    /// accepted kick or shield-bash animation. The native animation still
    /// owns its ordinary collision and may independently hit or miss. This
    /// helper only creates the additional nearest-enemy control contact.
    /// </summary>
    internal static class GwpAlternativeAttackControl
    {
        // A one-point blunt contact is the smallest real Agent.RegisterBlow
        // that lets the stock engine execute its KnockBack/KnockDown reaction.
        // Zero-damage blows return before the native control reaction. The
        // contact is muted; the visible action retains the animation's native
        // sound and does not produce an extra blood/impact particle.
        private const int ControlContactDamage = 1;
        private const float ControlStunPeriod = 0.12f;
        internal const float MaximumTargetDistance = 2.00f;
        private const float MaximumTargetDistanceSquared =
            MaximumTargetDistance * MaximumTargetDistance;

        internal static Agent? GetNearestEnemyTarget(Agent attacker)
        {
            if (attacker == null
                || !attacker.IsActive()
                || !attacker.IsHuman
                || attacker.MountAgent != null
                || attacker.Team == null)
            {
                return null;
            }

            Agent? nearest = null;
            float nearestDistanceSquared = MaximumTargetDistanceSquared;
            foreach (Agent candidate in attacker.Mission.Agents)
            {
                if (candidate == attacker
                    || !candidate.IsActive()
                    || !candidate.IsHuman
                    || candidate.MountAgent != null
                    || !attacker.IsEnemyOf(candidate))
                {
                    continue;
                }

                float distanceSquared = attacker.Position.DistanceSquared(
                    candidate.Position);
                if (distanceSquared > MaximumTargetDistanceSquared)
                    continue;

                if (nearest == null || distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        internal static void Apply(Agent attacker, Agent target)
        {
            if (attacker == null
                || target == null
                || !attacker.IsActive()
                || !target.IsActive()
                || !target.IsHuman
                || target.MountAgent != null
                || !attacker.IsEnemyOf(target))
            {
                return;
            }

            float knockdownChance =
                GwpAgentApplyDamageModel.GetGreyWardenKnockdownChance(
                    attacker,
                    target);
            if (knockdownChance <= 0f)
                return;

            bool knockDown = GwpAgentApplyDamageModel.RollKnockdown(
                knockdownChance);
            Vec3 direction = target.Position - attacker.Position;
            if (direction.LengthSquared < 0.0001f)
                direction = attacker.LookDirection;
            direction.Normalize();

            Vec3 impactPosition = target.Position;
            impactPosition.z += target.GetEyeGlobalHeight() * 0.55f;

            Blow controlBlow = new Blow(attacker.Index)
            {
                AttackType = HasWieldedShield(attacker)
                    ? AgentAttackType.Bash
                    : AgentAttackType.Kick,
                StrikeType = StrikeType.Swing,
                DamageType = DamageTypes.Blunt,
                BlowFlag = (knockDown
                    ? BlowFlags.KnockDown
                    : BlowFlags.KnockBack) | BlowFlags.NoSound,
                GlobalPosition = impactPosition,
                Direction = direction,
                SwingDirection = direction,
                BaseMagnitude = ControlContactDamage,
                InflictedDamage = ControlContactDamage,
                DefenderStunPeriod = ControlStunPeriod,
                AttackerStunPeriod = 0f,
                BoneIndex = target.Monster.HeadLookDirectionBoneIndex,
                VictimBodyPart = BoneBodyPartType.Chest,
                NoIgnore = true,
                DamageCalculated = true
            };
            controlBlow.WeaponRecord.FillAsMeleeBlow(
                null,
                null,
                -1,
                attacker.Monster.MainHandItemBoneIndex);

            AttackCollisionData controlCollision =
                AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    _attackBlockedWithShield: false,
                    _correctSideShieldBlock: false,
                    _isAlternativeAttack: true,
                    _isColliderAgent: true,
                    _collidedWithShieldOnBack: false,
                    _isMissile: false,
                    _isMissileBlockedWithWeapon: false,
                    _missileHasPhysics: false,
                    _entityExists: false,
                    _thrustTipHit: false,
                    _missileGoneUnderWater: false,
                    _missileGoneOutOfBorder: false,
                    collisionResult: CombatCollisionResult.StrikeAgent,
                    affectorWeaponSlotOrMissileIndex: -1,
                    StrikeType: (int)StrikeType.Swing,
                    DamageType: (int)DamageTypes.Blunt,
                    CollisionBoneIndex: target.Monster.HeadLookDirectionBoneIndex,
                    VictimHitBodyPart: BoneBodyPartType.Chest,
                    AttackBoneIndex: attacker.Monster.MainHandItemBoneIndex,
                    AttackDirection: Agent.UsageDirection.AttackDown,
                    PhysicsMaterialIndex: -1,
                    CollisionHitResultFlags: CombatHitResultFlags.NormalHit,
                    AttackProgress: 0.5f,
                    CollisionDistanceOnWeapon: 0f,
                    AttackerStunPeriod: 0f,
                    DefenderStunPeriod: ControlStunPeriod,
                    MissileTotalDamage: 0f,
                    MissileInitialSpeed: 0f,
                    ChargeVelocity: 0f,
                    FallSpeed: 0f,
                    WeaponRotUp: Vec3.Up,
                    _weaponBlowDir: direction,
                    CollisionGlobalPosition: impactPosition,
                    MissileVelocity: Vec3.Zero,
                    MissileStartingPosition: Vec3.Zero,
                    VictimAgentCurVelocity: target.Velocity,
                    GroundNormal: Vec3.Up);

            target.RegisterBlow(controlBlow, in controlCollision);
        }

        private static bool HasWieldedShield(Agent agent)
        {
            MissionWeapon offhandWeapon = agent.WieldedOffhandWeapon;
            return !offhandWeapon.IsEmpty
                && offhandWeapon.CurrentUsageItem != null
                && offhandWeapon.CurrentUsageItem.IsShield;
        }
    }
}
