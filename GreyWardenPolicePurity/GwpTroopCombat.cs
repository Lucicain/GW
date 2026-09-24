using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Grey Warden troop combat effects decided through the native damage model
    /// and ordinary mission events only; no native hit callback is patched (the
    /// 2026-09 arrow patches on Mission.MissileHitCallback coincided with lost
    /// battle shouts).
    /// </summary>
    internal static class GwpTroopCombat
    {
        internal const float ArcherKnockdownChance = 0.25f;
        internal const float ArcherShieldDamageMultiplier = 2f;
        // Knight effects: every attack, and a couched or braced lance at the higher chance.
        // Knockdown 12.5%/50%, dismount and block break 12.5%/25% (2026-09-24, cavalry too
        // strong: every ordinary-attack chance halved, couched knockdown kept at 50%).
        internal const float KnightChance = 0.125f;
        internal const float KnightCouchChance = 0.5f;
        // A Grey Warden knight's horse charging a man on foot, inside native's own charge rules
        // (2026-09-24, user: strengthen the native mechanism, not a new roll; damage unchanged):
        // knock-back from a frontal factor of 0.5 instead of 0.7, and the knock-down damage
        // threshold (max HP x (resistance - penetration)) halved.
        internal const float KnightChargeKnockBackFront = 0.5f;
        internal const float KnightChargeKnockDownThresholdScale = 0.5f;
        internal const float KnightDismountChance = 0.125f;
        internal const float KnightCouchDismountChance = 0.25f;
        internal const float KnightCrushChance = 0.25f;
        // Native shrug-off: a hit at or below the stagger threshold (5 damage by default)
        // plays no hit reaction. Grey Warden knights and their horses shrug off three times
        // as much, so a horse in a crowd is not slowed by every small hit (2026-09-24).
        internal const float KnightStaggerThresholdScale = 3f;
        // Heavy infantry shields take half damage: twice the durability, still breakable.
        internal const float HeavyShieldDamageMultiplier = 0.5f;
        // Share of native rears a Grey Warden knight's horse actually suffers.
        internal const float KnightHorseRearShare = 0.5f;

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
            Agent? mount = victim.MountAgent;
            if (collision.IsMissile)
            {
                if (mount == null && IsTroop(attacker, GwpIds.ArcherId))
                    _knockdown = Roll(ArcherKnockdownChance);
            }
            // Kicks and shield bashes keep their own knockdown rules (dual-blade.md).
            else if (!collision.IsAlternativeAttack && IsTroop(attacker, GwpIds.KnightId))
            {
                bool couch = IsKnightCouch(attacker);
                // Mounted or on foot alike; the user asked for no condition beyond the chance.
                if (mount != null && mount.RiderAgent == victim)
                    _dismount = victim.Health - collision.InflictedDamage >= 1f
                        && Roll(couch ? KnightCouchDismountChance : KnightDismountChance);
                else if (mount == null)
                {
                    _knockdown = Roll(couch ? KnightCouchChance : KnightChance);
                }
            }
            if (!_knockdown && !_dismount)
                return false;
            _victim = victim;
            return true;
        }

        /// <summary>
        /// A Grey Warden knight's horse charging an enemy on foot. Native's ChargeDamageCallback
        /// asks only knock-back and then knock-down, with the horse as the attacker; riders hit
        /// by a charge stay entirely native.
        /// </summary>
        internal static bool IsKnightCharge(Agent? horse, Agent victim, in AttackCollisionData collision) =>
            collision.IsHorseCharge && horse != null && horse.IsMount && IsTroop(horse.RiderAgent, GwpIds.KnightId)
            && victim.IsHuman && victim.MountAgent == null;

        /// <summary>Native's charge knock-back test (MissionCombatMechanicsHelper) with the wider front.</summary>
        internal static bool ChargeKnocksBack(Agent horse, Agent victim, in AttackCollisionData collision)
        {
            Vec2 toVictim = (victim.Position.AsVec2 - collision.CollisionGlobalPosition.AsVec2).Normalized();
            return Math.Max(0f, Vec2.DotProduct(toVictim, horse.GetMovementDirection())) >= KnightChargeKnockBackFront;
        }

        /// <summary>
        /// Native's charge knock-down test (DecideCombatEffect: damage at least max HP x
        /// (knock-down resistance - horse charge penetration)) with that threshold scaled.
        /// Still only after a knock-back, as native requires. A fall may shake the shield loose.
        /// </summary>
        internal static bool ChargeKnocksDown(Agent victim, in AttackCollisionData collision, in Blow blow)
        {
            if ((blow.BlowFlag & BlowFlags.KnockBack) == 0 || (blow.BlowFlag & BlowFlags.ShrugOff) != 0)
                return false;
            float resistance = MissionGameModels.Current.AgentStatCalculateModel.GetKnockDownResistance(victim);
            float penetration = MissionGameModels.Current.AgentApplyDamageModel.GetHorseChargePenetration();
            float threshold = victim.HealthLimit * Math.Max(0f, resistance - penetration) * KnightChargeKnockDownThresholdScale;
            if (collision.InflictedDamage < threshold)
                return false;
            return true;
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
        /// Knight block break against an enemy (2026-09-24): an active attack breaks any
        /// block at the knight chance; a couched or braced lance always breaks a weapon
        /// parry and never a shield. Native itself never crushes a passive attack.
        /// </summary>
        internal static bool KnightCrushesBlock(Agent? attacker, Agent? defender, WeaponComponentData? defendItem, bool isPassiveUsageHit)
        {
            if (defendItem == null || defender == null || !IsTroop(attacker, GwpIds.KnightId) || !attacker!.IsEnemyOf(defender))
                return false;
            return isPassiveUsageHit ? !defendItem.IsShield : Roll(KnightCrushChance);
        }

        // Couched on horseback or braced on foot: both are the lance's passive attack.
        internal static bool IsKnightCouch(Agent? attacker) =>
            attacker != null && attacker.IsDoingPassiveAttack && IsTroop(attacker, GwpIds.KnightId);

        /// <summary>Called only after native decided this mount rears.</summary>
        internal static bool KnightHorseRears(Agent mount)
        {
            if (!mount.IsMount || !IsTroop(mount.RiderAgent, GwpIds.KnightId) || Roll(KnightHorseRearShare))
                return true;
            return false;
        }

        /// <summary>A Grey Warden knight, or the horse one is riding.</summary>
        internal static bool IsKnightOrKnightHorse(Agent? agent) =>
            agent != null && (IsTroop(agent, GwpIds.KnightId) || (agent.IsMount && IsTroop(agent.RiderAgent, GwpIds.KnightId)));

        /// <summary>
        /// A Grey Warden knight attacking with the lance or the greatsword: readying or
        /// releasing a strike (either action channel), or holding a couched or braced lance.
        /// Like the archers' dual blades, such an attack is not interrupted.
        /// </summary>
        internal static bool IsKnightAttacking(Agent agent)
        {
            if (!IsTroop(agent, GwpIds.KnightId) || !agent.IsActive())
                return false;
            string? item = agent.WieldedWeapon.Item?.StringId;
            if (item != KnightLanceItemId && item != KnightGreatswordItemId)
                return false;
            return agent.IsDoingPassiveAttack
                || IsStrike(agent.GetCurrentActionType(0)) || IsStrike(agent.GetCurrentActionType(1));
        }

        private const string KnightLanceItemId = "gwlance";
        private const string KnightGreatswordItemId = "gwtwohandedsword";

        private static bool IsStrike(Agent.ActionCodeType action) =>
            action == Agent.ActionCodeType.ReadyMelee || action == Agent.ActionCodeType.ReleaseMelee;

        internal static bool IsHeavyInfantryShield(Agent? victim) => IsTroop(victim, GwpIds.HeavyInfantryId);

        /// <summary>
        /// Doubles each quiver of a Grey Warden archer, raising its maximum too, in the
        /// same place and way native adds quiver perks (after they have been applied).
        /// </summary>
        internal static void DoubleArcherQuivers(Agent agent)
        {
            if (!IsTroop(agent, GwpIds.ArcherId))
                return;
            MissionEquipment equipment = agent.Equipment;
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon weapon = equipment[i];
                if (!weapon.IsEmpty && weapon.CurrentUsageItem?.WeaponClass == WeaponClass.Arrow && weapon.Amount > 0)
                {
                    equipment.SetAmountOfSlot(i, (short)Math.Min(short.MaxValue, weapon.Amount * 2), true);
                }
            }
        }
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
