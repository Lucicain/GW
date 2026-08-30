using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Preserves the active game mode's complete damage model and changes only
    /// the native knockdown decision for Grey Warden kicks, shield bashes, and
    /// all melee attacks made with the Grey Warden paired blades.
    /// This decision is evaluated once when an eligible blow connects. There
    /// is no post-rise input lock or forced animation.
    /// </summary>
    internal sealed class GwpAgentApplyDamageModel : AgentApplyDamageModel
    {
        private const float LordKnockdownChance = 0.80f;
        private const float TierOneKnockdownChance = 0.40f;
        private const float TierTwoKnockdownChance = 0.60f;
        private const float TierThreeKnockdownChance = 0.80f;

        // Defensive fallback only. In normal Campaign and CustomGame startup,
        // IGameStarter.Initialize supplies the already registered native model.
        private readonly AgentApplyDamageModel _fallbackModel =
            new CustomAgentApplyDamageModel();

        // Mission combat asks KnockBack first and KnockDown immediately after
        // for the same connected blow. Cache one roll so a successful custom
        // knockdown suppresses long knockback, while a failed roll retains the
        // vanilla short push/stagger instead of producing no reaction at all.
        private Agent? _pendingAttacker;
        private Agent? _pendingVictim;
        private bool _pendingKnockdown;
        private bool _hasPendingKnockdownDecision;

        private AgentApplyDamageModel NativeModel => BaseModel ?? _fallbackModel;

        public override bool DecideAgentKnockedDownByBlow(
            Agent attackerAgent,
            Agent victimAgent,
            in AttackCollisionData collisionData,
            WeaponComponentData attackerWeapon,
            in Blow blow)
        {
            float chance = GetGreyWardenKnockdownChance(
                attackerAgent,
                victimAgent,
                in collisionData,
                in blow,
                attackerWeapon);

            if (chance > 0f)
            {
                if (_hasPendingKnockdownDecision
                    && ReferenceEquals(_pendingAttacker, attackerAgent)
                    && ReferenceEquals(_pendingVictim, victimAgent))
                {
                    bool result = _pendingKnockdown;
                    ClearPendingKnockdownDecision();
                    return result;
                }

                return RollKnockdown(chance);
            }

            return NativeModel.DecideAgentKnockedDownByBlow(
                attackerAgent,
                victimAgent,
                in collisionData,
                attackerWeapon,
                in blow);
        }

        private static float GetGreyWardenKnockdownChance(
            Agent? attacker,
            Agent? victim,
            in AttackCollisionData collisionData,
            in Blow blow,
            WeaponComponentData? attackerWeapon)
        {
            // Prefer the collision flag because it is the engine's direct
            // statement that this hit came from the alternative-attack input.
            // AttackType is retained as a compatibility fallback for Kick/Bash.
            bool isAlternativeAttack = collisionData.IsAlternativeAttack
                || blow.AttackType == AgentAttackType.Kick
                || blow.AttackType == AgentAttackType.Bash;
            bool isDualBladeAttack = blow.AttackType != AgentAttackType.Kick
                && blow.AttackType != AgentAttackType.Bash
                && IsDualBladeAttack(attacker, in collisionData, attackerWeapon);

            return (isAlternativeAttack || isDualBladeAttack)
                ? GetGreyWardenKnockdownChance(attacker, victim)
                : 0f;
        }

        internal static bool IsDualBladeAttack(
            Agent? attacker,
            in AttackCollisionData collisionData,
            WeaponComponentData? attackerWeapon)
        {
            if (attacker == null
                || attackerWeapon == null
                || !GwpDualBladeLoadout.IsEligibleDualBladeUser(attacker)
                || (collisionData.StrikeType != (int)StrikeType.Swing
                    && collisionData.StrikeType != (int)StrikeType.Thrust)
                )
            {
                return false;
            }

            try
            {
                MissionEquipment equipment = attacker.Equipment;
                if (!GwpDualBladeLoadout.IsOffHandBladeId(
                        equipment[EquipmentIndex.Weapon0].Item?.StringId)
                    || !IsItem(
                        equipment[EquipmentIndex.Weapon1],
                        GwpIds.DualBladeMainhandItemId))
                {
                    return false;
                }

                // Bone 20 is the authored left-hand attachment used by the
                // paired-blade action set. Every other melee attack bone is
                // the main hand; checking the corresponding wielded item
                // keeps an optional lance in Weapon2 from gaining the
                // paired-blade knockdown effect.
                bool isLeftHandAttack = collisionData.AttackBoneIndex == 20
                    && GwpDualBladeLoadout.IsOffHandBladeId(
                        attacker.WieldedOffhandWeapon.Item?.StringId);
                bool isMainHandAttack = collisionData.AttackBoneIndex != 20
                    && IsItem(
                        attacker.WieldedWeapon,
                        GwpIds.DualBladeMainhandItemId);
                return isLeftHandAttack || isMainHandAttack;
            }
            catch
            {
                // A collision can arrive while native equipment is being
                // rebuilt. Preserve the ordinary damage-model decision when
                // the pair cannot be confirmed safely.
                return false;
            }
        }

        private static bool IsItem(
            in MissionWeapon weapon,
            string itemId) =>
            !weapon.IsEmpty
            && weapon.Item?.StringId == itemId;

        internal static float GetGreyWardenKnockdownChance(
            Agent? attacker,
            Agent? victim)
        {
            if (attacker == null
                || victim == null
                || !victim.IsHuman
                || victim.MountAgent != null
                || !attacker.IsEnemyOf(victim))
            {
                return 0f;
            }

            if (!GwpKickBehavior.IsEligibleGreyWarden(attacker))
            {
                return 0f;
            }

            BasicCharacterObject? basicCharacter = attacker.Character;
            if (basicCharacter == null)
                return 0f;

            string characterId = basicCharacter.StringId;

            // Lords and the Custom Battle commander use the elite 80% rate.
            if (string.Equals(
                    characterId,
                    GwpIds.CustomBattleCommanderId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LordKnockdownChance;
            }

            if (characterId.StartsWith(
                    GwpIds.LeaderCharacterIdPrefix,
                    StringComparison.OrdinalIgnoreCase)
                || (basicCharacter is CharacterObject character
                    && character.HeroObject != null
                    && GwpCommon.IsGreyWardenLord(character.HeroObject)))
            {
                return LordKnockdownChance;
            }

            // Rank is the unit's position in this mod's troop tree, not its
            // Bannerlord numeric level and not its infantry/ranged/cavalry role.
            if (string.Equals(
                    characterId,
                    GwpIds.NewRecruitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TierOneKnockdownChance;
            }

            if (string.Equals(
                    characterId,
                    GwpIds.PoliceRecruitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TierTwoKnockdownChance;
            }

            if (string.Equals(
                    characterId,
                    GwpIds.HeavyInfantryId,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    characterId,
                    GwpIds.ArcherId,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    characterId,
                    GwpIds.KnightId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TierThreeKnockdownChance;
            }

            return 0f;
        }

        #region Native model pass-through

        public override bool IsDamageIgnored(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData) =>
            NativeModel.IsDamageIgnored(in attackInformation, in collisionData);

        public override float ApplyDamageAmplifications(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage) =>
            NativeModel.ApplyDamageAmplifications(
                in attackInformation,
                in collisionData,
                baseDamage);

        public override float ApplyDamageScaling(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage) =>
            NativeModel.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage);

        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage) =>
            NativeModel.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        public override float ApplyGeneralDamageModifiers(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage) =>
            NativeModel.ApplyGeneralDamageModifiers(
                in attackInformation,
                in collisionData,
                baseDamage);

        public override void DecideMissileWeaponFlags(
            Agent attackerAgent,
            in MissionWeapon missileWeapon,
            ref WeaponFlags missileWeaponFlags) =>
            NativeModel.DecideMissileWeaponFlags(
                attackerAgent,
                in missileWeapon,
                ref missileWeaponFlags);

        public override void CalculateDefendedBlowStunMultipliers(
            Agent attackerAgent,
            Agent defenderAgent,
            CombatCollisionResult collisionResult,
            WeaponComponentData attackerWeapon,
            WeaponComponentData defenderWeapon,
            ref float attackerStunPeriod,
            ref float defenderStunPeriod)
        {
            NativeModel.CalculateDefendedBlowStunMultipliers(
                attackerAgent,
                defenderAgent,
                collisionResult,
                attackerWeapon,
                defenderWeapon,
                ref attackerStunPeriod,
                ref defenderStunPeriod);

            // A shield bash caught by the opponent's raised shield enters the
            // defended-collision path, not the normal alternative-hit path.
            // The driven ShieldBashStunDurationMultiplier therefore does not
            // make this shield-on-shield reaction visibly longer. Add the
            // community-tested 0.5 seconds here; the engine clamps both stun
            // periods to StunPeriodMax immediately after this model callback.
            if (IsGreyWardenShieldBashAgainstShield(
                    attackerAgent,
                    defenderAgent,
                    defenderWeapon))
            {
                defenderStunPeriod += 0.5f;
            }
        }

        public override float CalculateStaggerThresholdDamage(
            Agent defenderAgent,
            in Blow blow) =>
            NativeModel.CalculateStaggerThresholdDamage(defenderAgent, in blow);

        public override float CalculateAlternativeAttackDamage(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            WeaponComponentData weapon) =>
            NativeModel.CalculateAlternativeAttackDamage(
                in attackInformation,
                in collisionData,
                weapon);

        public override float CalculatePassiveAttackDamage(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage) =>
            NativeModel.CalculatePassiveAttackDamage(
                in attackInformation,
                in collisionData,
                baseDamage);

        public override MeleeCollisionReaction DecidePassiveAttackCollisionReaction(
            Agent attacker,
            Agent defender,
            bool isFatalHit) =>
            NativeModel.DecidePassiveAttackCollisionReaction(attacker, defender, isFatalHit);

        public override void DecideWeaponCollisionReaction(
            in Blow registeredBlow,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent defender,
            in MissionWeapon attackerWeapon,
            bool isFatalHit,
            bool isShruggedOff,
            float momentumRemaining,
            out MeleeCollisionReaction colReaction) =>
            NativeModel.DecideWeaponCollisionReaction(
                in registeredBlow,
                in collisionData,
                attacker,
                defender,
                in attackerWeapon,
                isFatalHit,
                isShruggedOff,
                momentumRemaining,
                out colReaction);

        public override float CalculateShieldDamage(
            in AttackInformation attackInformation,
            float baseDamage) =>
            NativeModel.CalculateShieldDamage(in attackInformation, baseDamage);

        public override float CalculateSailFireDamage(
            Agent attackerAgent,
            IShipOrigin shipOrigin,
            float baseDamage,
            bool damageFromShipMachine) =>
            NativeModel.CalculateSailFireDamage(
                attackerAgent,
                shipOrigin,
                baseDamage,
                damageFromShipMachine);

        public override float CalculateHullFireDamage(
            float baseFireDamage,
            IShipOrigin shipOrigin) =>
            NativeModel.CalculateHullFireDamage(baseFireDamage, shipOrigin);

        public override float GetDamageMultiplierForBodyPart(
            BoneBodyPartType bodyPart,
            DamageTypes type,
            bool isHuman,
            bool isMissile) =>
            NativeModel.GetDamageMultiplierForBodyPart(bodyPart, type, isHuman, isMissile);

        public override bool CanWeaponIgnoreFriendlyFireChecks(
            WeaponComponentData weapon) =>
            NativeModel.CanWeaponIgnoreFriendlyFireChecks(weapon);

        public override bool CanWeaponDealSneakAttack(
            in AttackInformation attackInformation,
            WeaponComponentData weapon) =>
            NativeModel.CanWeaponDealSneakAttack(in attackInformation, weapon);

        public override bool CanWeaponDismount(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.CanWeaponDismount(
                attackerAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override bool CanWeaponKnockback(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.CanWeaponKnockback(
                attackerAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override bool CanWeaponKnockDown(
            Agent attackerAgent,
            Agent victimAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.CanWeaponKnockDown(
                attackerAgent,
                victimAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override bool DecideCrushedThrough(
            Agent attackerAgent,
            Agent defenderAgent,
            float totalAttackEnergy,
            Agent.UsageDirection attackDirection,
            StrikeType strikeType,
            WeaponComponentData defendItem,
            bool isPassiveUsageHit)
        {
            // Bannerlord's newer combat rules allow a raised shield to absorb
            // a shield bash while preserving the guard. A longer calculated
            // stun alone does not lower that guard, so there is no visible
            // control reaction. Route this one exact interaction through the
            // engine's native crush-through path to break the successful block
            // and play the defender's guard-broken/stun response.
            if (IsGreyWardenShieldBashAgainstShield(
                    attackerAgent,
                    defenderAgent,
                    defendItem))
            {
                return true;
            }

            return NativeModel.DecideCrushedThrough(
                attackerAgent,
                defenderAgent,
                totalAttackEnergy,
                attackDirection,
                strikeType,
                defendItem,
                isPassiveUsageHit);
        }

        private static bool IsGreyWardenShieldBashAgainstShield(
            Agent? attackerAgent,
            Agent? defenderAgent,
            WeaponComponentData? defenderWeapon)
        {
            if (attackerAgent == null
                || defenderAgent == null
                || defenderWeapon == null
                || !defenderWeapon.IsShield
                || !defenderAgent.IsHuman
                || !attackerAgent.IsEnemyOf(defenderAgent)
                || !GwpKickBehavior.IsEligibleGreyWarden(attackerAgent))
            {
                return false;
            }

            MissionWeapon offhandWeapon = attackerAgent.WieldedOffhandWeapon;
            if (offhandWeapon.IsEmpty
                || offhandWeapon.CurrentUsageItem == null
                || !offhandWeapon.CurrentUsageItem.IsShield)
            {
                return false;
            }

            return IsAlternativeAttackAction(attackerAgent.GetCurrentActionType(0))
                || IsAlternativeAttackAction(attackerAgent.GetCurrentActionType(1));
        }

        private static bool IsAlternativeAttackAction(
            Agent.ActionCodeType actionType) =>
            actionType >= Agent.ActionCodeType.AlternativeAttackAllBegin
            && actionType < Agent.ActionCodeType.AlternativeAttackAllEnd;

        public override float CalculateRemainingMomentum(
            float originalMomentum,
            in Blow blow,
            in AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            in MissionWeapon attackerWeapon,
            bool isCrushThrough) =>
            NativeModel.CalculateRemainingMomentum(
                originalMomentum,
                in blow,
                in collisionData,
                attacker,
                victim,
                in attackerWeapon,
                isCrushThrough);

        public override bool DecideAgentShrugOffBlow(
            Agent victimAgent,
            in AttackCollisionData collisionData,
            in Blow blow) =>
            NativeModel.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow);

        public override bool DecideAgentDismountedByBlow(
            Agent attackerAgent,
            Agent victimAgent,
            in AttackCollisionData collisionData,
            WeaponComponentData attackerWeapon,
            in Blow blow) =>
            NativeModel.DecideAgentDismountedByBlow(
                attackerAgent,
                victimAgent,
                in collisionData,
                attackerWeapon,
                in blow);

        public override bool DecideAgentKnockedBackByBlow(
            Agent attackerAgent,
            Agent victimAgent,
            in AttackCollisionData collisionData,
            WeaponComponentData attackerWeapon,
            in Blow blow)
        {
            // Vanilla marks every alternative attack as KnockBack. When our
            // separate probability roll also adds KnockDown, both flags are
            // applied to the same blow and the victim can be launched too far.
            // Grey Warden alternatives use only KnockDown on a successful roll,
            // so the victim falls near the contact point instead of flying away.
            float chance = GetGreyWardenKnockdownChance(
                attackerAgent,
                victimAgent,
                in collisionData,
                in blow,
                attackerWeapon);
            if (chance > 0f)
            {
                bool isDualBladeAttack = blow.AttackType != AgentAttackType.Kick
                    && blow.AttackType != AgentAttackType.Bash
                    && IsDualBladeAttack(
                        attackerAgent,
                        in collisionData,
                        attackerWeapon);
                bool knockedDown = RollKnockdown(chance);
                _pendingAttacker = attackerAgent;
                _pendingVictim = victimAgent;
                _pendingKnockdown = knockedDown;
                _hasPendingKnockdownDecision = true;

                // Success: fall at the contact point, without the simultaneous
                // long launch. A failed dual-blade attack delegates to the
                // native ordinary-melee decision so this feature adds no
                // extra push when its probability roll misses.
                if (isDualBladeAttack && !knockedDown)
                {
                    return NativeModel.DecideAgentKnockedBackByBlow(
                        attackerAgent,
                        victimAgent,
                        in collisionData,
                        attackerWeapon,
                        in blow);
                }

                return !knockedDown;
            }

            return NativeModel.DecideAgentKnockedBackByBlow(
                attackerAgent,
                victimAgent,
                in collisionData,
                attackerWeapon,
                in blow);
        }

        internal static bool RollKnockdown(float chance) =>
            chance >= 1f || MBRandom.RandomFloat < chance;

        private void ClearPendingKnockdownDecision()
        {
            _pendingAttacker = null;
            _pendingVictim = null;
            _pendingKnockdown = false;
            _hasPendingKnockdownDecision = false;
        }

        public override bool DecideMountRearedByBlow(
            Agent attackerAgent,
            Agent victimAgent,
            in AttackCollisionData collisionData,
            WeaponComponentData attackerWeapon,
            in Blow blow) =>
            NativeModel.DecideMountRearedByBlow(
                attackerAgent,
                victimAgent,
                in collisionData,
                attackerWeapon,
                in blow);

        public override bool ShouldMissilePassThroughAfterShieldBreak(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon) =>
            NativeModel.ShouldMissilePassThroughAfterShieldBreak(
                attackerAgent,
                attackerWeapon);

        public override float GetDismountPenetration(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.GetDismountPenetration(
                attackerAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override float GetKnockBackPenetration(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.GetKnockBackPenetration(
                attackerAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override float GetKnockDownPenetration(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData collisionData) =>
            NativeModel.GetKnockDownPenetration(
                attackerAgent,
                attackerWeapon,
                in blow,
                in collisionData);

        public override float GetHorseChargePenetration() =>
            NativeModel.GetHorseChargePenetration();

        #endregion
    }
}
