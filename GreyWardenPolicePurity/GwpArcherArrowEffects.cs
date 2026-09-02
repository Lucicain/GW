using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// One synchronous missile-hit scope shared by the damage model and the
    /// two native callbacks that own shield collision. It carries only two
    /// random decisions in memory; no per-arrow collection, polling, or file
    /// I/O is involved.
    /// </summary>
    internal static class GwpArcherArrowHitState
    {
        [ThreadStatic]
        private static bool _active;

        [ThreadStatic]
        private static Agent? _attacker;

        [ThreadStatic]
        private static Agent? _victim;

        [ThreadStatic]
        private static bool _isBodyContact;

        [ThreadStatic]
        private static bool _isShieldContact;

        [ThreadStatic]
        private static bool _forceKnockdown;

        [ThreadStatic]
        private static bool _forceShieldPass;

        [ThreadStatic]
        private static bool _shieldPassApplied;

        internal static void Begin(
            Agent? attacker,
            Agent? victim,
            in AttackCollisionData collisionData)
        {
            Clear();
            _active = true;
            _attacker = attacker;
            _victim = victim;
            _isShieldContact = victim != null
                && collisionData.IsMissile
                && collisionData.AttackBlockedWithShield;
            _isBodyContact = victim != null
                && collisionData.IsMissile
                && collisionData.IsColliderAgent
                && !collisionData.AttackBlockedWithShield
                && !collisionData.MissileBlockedWithWeapon
                && !collisionData.CollidedWithShieldOnBack;
        }

        internal static void RollForArcherArrow(
            float knockdownChance,
            float shieldPassChance)
        {
            if (!_active
                || _attacker == null
                || _victim == null
                || !_attacker.IsEnemyOf(_victim))
            {
                return;
            }

            if (_isBodyContact)
                _forceKnockdown = MBRandom.RandomFloat < knockdownChance;

            if (_isShieldContact)
                _forceShieldPass = MBRandom.RandomFloat < shieldPassChance;
        }

        internal static bool ShieldPassGranted =>
            _active && _isShieldContact && _forceShieldPass;

        internal static bool ShouldForceKnockdown(
            Agent? attacker,
            Agent? victim,
            in AttackCollisionData collisionData,
            in Blow blow) =>
            _active
            && _forceKnockdown
            && ReferenceEquals(_attacker, attacker)
            && ReferenceEquals(_victim, victim)
            && collisionData.IsMissile
            && blow.IsMissile;

        internal static bool ShouldForceKnockdown(
            Agent? victim,
            in AttackCollisionData collisionData,
            in Blow blow) =>
            _active
            && _forceKnockdown
            && ReferenceEquals(_victim, victim)
            && collisionData.IsMissile
            && blow.IsMissile;

        internal static void ForceShieldPass(
            ref Mission.MissileCollisionReaction collisionReaction,
            bool attachedToShield)
        {
            if (!ShieldPassGranted || !attachedToShield)
                return;

            collisionReaction = Mission.MissileCollisionReaction.PassThrough;
            _shieldPassApplied = true;
        }

        internal static void Complete(ref bool missileStopped)
        {
            if (_shieldPassApplied)
                missileStopped = false;

            Clear();
        }

        internal static void Abort() => Clear();

        private static void Clear()
        {
            _active = false;
            _attacker = null;
            _victim = null;
            _isBodyContact = false;
            _isShieldContact = false;
            _forceKnockdown = false;
            _forceShieldPass = false;
            _shieldPassApplied = false;
        }
    }

    /// <summary>
    /// Opens the hit-local state before the damage model is asked for missile
    /// flags, then fixes the callback's returned "stopped" boolean when the
    /// selected arrow was routed through a shield.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MissileHitCallback")]
    internal static class GwpArcherArrowMissileHitPatch
    {
        [HarmonyPrefix]
        private static void BeforeMissileHit(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim) =>
            GwpArcherArrowHitState.Begin(
                attacker,
                victim,
                in collisionData);

        [HarmonyPostfix]
        private static void AfterMissileHit(ref bool __result) =>
            GwpArcherArrowHitState.Complete(ref __result);

        [HarmonyFinalizer]
        private static Exception? FinalizeMissileHit(Exception? __exception)
        {
            GwpArcherArrowHitState.Abort();
            return __exception;
        }
    }

    /// <summary>
    /// Mission.MissileHitCallback decides shield penetration before calling
    /// this public router. Replacing only its reaction argument avoids copying
    /// that large native method: the missile remains alive and the native side
    /// receives the matching "not stopped" result from the callback postfix.
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.HandleMissileCollisionReaction))]
    internal static class GwpArcherArrowShieldPassPatch
    {
        [HarmonyPrefix]
        private static void BeforeHandleMissileReaction(
            ref Mission.MissileCollisionReaction collisionReaction,
            bool attachedToShield) =>
            GwpArcherArrowHitState.ForceShieldPass(
                ref collisionReaction,
                attachedToShield);
    }
}
