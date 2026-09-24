using System;
using System.Linq;
#if GWP_DIAGNOSTICS
using System.Globalization;
using System.IO;
using System.Threading;
#endif
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
#if GWP_DIAGNOSTICS
    /// <summary>Bounded, mission-local evidence for the rider-to-horse damage investigation.</summary>
    internal static class GwpWarhorseDamageTrace
    {
        private const int MaxLines = 1000;
        private static readonly object Sync = new object();
        private static int _lines;
        private static int _nextId;

        internal static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord", "GreyWarden-Warhorse-Damage.log");

        internal static void StartMission()
        {
            Interlocked.Exchange(ref _nextId, 0);
            lock (Sync)
            {
                _lines = 0;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024)
                    {
                        string previous = LogPath + ".previous";
                        if (File.Exists(previous)) File.Delete(previous);
                        File.Move(LogPath, previous);
                    }
                    File.AppendAllText(LogPath,
                        "# mission | time=" + DateTime.Now.ToString("O")
                        + " | build=" + typeof(GwpWarhorseDamageTrace).Module.ModuleVersionId
                        + " | limit=" + MaxLines + Environment.NewLine);
                }
                catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            }
        }

        internal static bool Wants(Agent? attacker, Agent? victim) =>
            attacker?.IsMainAgent == true || victim?.IsMainAgent == true
            || victim?.RiderAgent?.IsMainAgent == true
            || GwpTroopCombat.IsWarhorseOrRider(victim);

        internal static int BeginTransfer(Agent? attacker, Agent? rider)
        {
            try { return Wants(attacker, rider) ? Interlocked.Increment(ref _nextId) : 0; }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); return 0; }
        }

        internal static string AgentName(Agent? agent) => agent == null
            ? "-"
            : agent.Index + ":" + ((agent.IsMount ? agent.Monster?.StringId : agent.Character?.StringId) ?? "-")
                + (agent.IsMainAgent ? ":main" : string.Empty);

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        internal static void RecordCalculation(
            in AttackInformation info, in AttackCollisionData collision,
            float modelInput, float nativeDamage, float finalDamage)
        {
            try
            {
                Agent? victim = info.VictimAgent;
                if (!collision.IsColliderAgent || !Wants(info.AttackerAgent, victim))
                    return;
                Write("calc", "attacker=" + AgentName(info.AttackerAgent)
                    + " victim=" + AgentName(victim)
                    + " mount=" + AgentName(victim?.MountAgent)
                    + " rider=" + AgentName(victim?.RiderAgent)
                    + " weapon=" + (info.AttackerWeapon.Item?.StringId ?? "-")
                    + " part=" + collision.VictimHitBodyPart
                    + " result=" + collision.CollisionResult
                    + " missile=" + collision.IsMissile
                    + " shield=" + collision.AttackBlockedWithShield
                    + " backShield=" + collision.CollidedWithShieldOnBack
                    + " weaponBlock=" + collision.MissileBlockedWithWeapon
                    + " armor=" + Number(info.ArmorAmountFloat)
                    + " difficulty=" + Number(info.CombatDifficultyMultiplier)
                    + " boneMultiplier=" + Number(info.DamageMultiplierOfBone)
                    + " magnitude=" + Number(collision.BaseMagnitude)
                    + " speedModifier=" + Number(collision.MovementSpeedDamageModifier)
                    + " absorbed=" + collision.AbsorbedByArmor
                    + " preModel=" + collision.InflictedDamage
                    + " modelInput=" + Number(modelInput)
                    + " native=" + Number(nativeDamage)
                    + " final=" + Number(finalDamage)
                    + " victimHp=" + Number(victim?.Health ?? -1f));
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        internal static void RecordActual(Agent? victim, Agent? attacker, in Blow blow)
        {
            try
            {
                if (!Wants(attacker, victim)) return;
                Write("hit", "attacker=" + AgentName(attacker)
                    + " victim=" + AgentName(victim)
                    + " rider=" + AgentName(victim?.RiderAgent)
                    + " inflicted=" + blow.InflictedDamage
                    + " absorbed=" + Number(blow.AbsorbedByArmor)
                    + " hpAfter=" + Number(victim?.Health ?? -1f));
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        internal static void RecordQueue(
            int id, Agent? attacker, Agent rider, Agent horse, float damage, int queued)
        {
            if (id == 0) return;
            try
            {
                Write(queued > 0 ? "queue" : "rounded-to-zero",
                    "id=" + id + " attacker=" + AgentName(attacker)
                    + " rider=" + AgentName(rider) + " horse=" + AgentName(horse)
                    + " nativeAfterRules=" + Number(damage) + " queued=" + queued
                    + " horseHp=" + Number(horse.Health));
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        internal static void RecordTransfer(int id, string stage, Agent? horse, int queued, float before)
        {
            if (id == 0) return;
            try
            {
                Write(stage, "id=" + id + " horse=" + AgentName(horse)
                    + " queued=" + queued + " hpBefore=" + Number(before)
                    + " hpAfter=" + Number(horse?.Health ?? -1f));
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        internal static void Write(string stage, string details)
        {
            lock (Sync)
            {
                if (_lines >= MaxLines) return;
                try
                {
                    File.AppendAllText(LogPath,
                        DateTime.Now.ToString("O") + " | " + stage + " | " + details
                        + Environment.NewLine);
                    _lines++;
                }
                catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            }
        }
    }
#endif

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
#if GWP_DIAGNOSTICS
            GwpWarhorseDamageTrace.StartMission();
#endif
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

#if GWP_DIAGNOSTICS
        public override void OnAgentHit(
            Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
            in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
            GwpWarhorseDamageTrace.RecordActual(affectedAgent, affectorAgent, in blow);
        }
#endif
    }

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
        // A Grey Warden warhorse charging a man on foot, inside native's own charge rules
        // (2026-09-24, user: strengthen the native mechanism, not a new roll; damage unchanged):
        // knock-back from a frontal factor of 0.6 instead of 0.7, and the knock-down damage
        // threshold (max HP x (resistance - penetration)) x 0.75. (2026-09-25: each warhorse
        // gain halved, from 0.5 and x 0.5.)
        internal const float WarhorseChargeKnockBackFront = 0.6f;
        internal const float WarhorseChargeKnockDownThresholdScale = 0.75f;
        internal const float KnightDismountChance = 0.125f;
        internal const float KnightCouchDismountChance = 0.25f;
        internal const float KnightCrushChance = 0.25f;
        // Only the warhorse shrugs off twice the native small-hit threshold (x 3 until
        // 2026-09-25). Its rider uses native's threshold, even while mounted.
        internal const float WarhorseStaggerThresholdScale = 2f;
        // Heavy infantry shields take half damage: twice the durability, still breakable.
        internal const float HeavyShieldDamageMultiplier = 0.5f;
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
#if GWP_DIAGNOSTICS
            internal int TraceId;
#endif
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
#if GWP_DIAGNOSTICS
            int traceId = GwpWarhorseDamageTrace.BeginTransfer(info.AttackerAgent, victim);
#endif
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
                        Direction = collision.WeaponBlowDir,
#if GWP_DIAGNOSTICS
                        TraceId = traceId
#endif
                    });
                }
            }
#if GWP_DIAGNOSTICS
            GwpWarhorseDamageTrace.RecordQueue(
                traceId, info.AttackerAgent, victim, horse, damage, transferred);
#endif
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
                {
#if GWP_DIAGNOSTICS
                    GwpWarhorseDamageTrace.RecordTransfer(item.TraceId, "discard", horse, item.Damage, -1f);
#endif
                    continue;
                }

#if GWP_DIAGNOSTICS
                float horseHpBefore = horse.Health;
#endif
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
#if GWP_DIAGNOSTICS
                GwpWarhorseDamageTrace.RecordTransfer(
                    item.TraceId, "applied", horse, item.Damage, horseHpBefore);
#endif
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
        /// Knight block break against an enemy: an active attack can break a weapon
        /// parry, never a shield; a couched or braced lance always breaks a weapon
        /// parry. Native itself never crushes a passive attack.
        /// </summary>
        internal static bool KnightCrushesBlock(Agent? attacker, Agent? defender, WeaponComponentData? defendItem, bool isPassiveUsageHit)
        {
            if (defendItem == null || defender == null || !IsTroop(attacker, GwpIds.KnightId) || !attacker!.IsEnemyOf(defender))
                return false;
            return !defendItem.IsShield && (isPassiveUsageHit || Roll(KnightCrushChance));
        }

        // Couched on horseback or braced on foot: both are the lance's passive attack.
        internal static bool IsKnightCouch(Agent? attacker) =>
            attacker != null && attacker.IsDoingPassiveAttack && IsTroop(attacker, GwpIds.KnightId);

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

        internal static bool IsHeavyInfantryShield(Agent? victim) => IsTroop(victim, GwpIds.HeavyInfantryId);

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
