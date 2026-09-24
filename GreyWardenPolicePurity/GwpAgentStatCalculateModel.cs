using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Preserves the active game mode's stat model and exposes the temporary
    /// battle masteries earned by individual Grey Warden agents. No character
    /// or save-game skill is mutated: the effective values exist only while
    /// the current mission and its agent objects exist.
    /// </summary>
    internal sealed class GwpAgentStatCalculateModel : AgentStatCalculateModel
    {
        internal const int MasteredSkillValue = 1000;

        private readonly AgentStatCalculateModel _fallbackModel =
            new CustomBattleAgentStatCalculateModel();

        private AgentStatCalculateModel NativeModel =>
            BaseModel ?? _fallbackModel;

        public override void InitializeAgentStats(
            Agent agent,
            Equipment spawnEquipment,
            AgentDrivenProperties agentDrivenProperties,
            AgentBuildData agentBuildData) =>
            NativeModel.InitializeAgentStats(
                agent,
                spawnEquipment,
                agentDrivenProperties,
                agentBuildData);

        public override void InitializeMissionEquipment(Agent agent)
        {
            NativeModel.InitializeMissionEquipment(agent);
            GwpTroopCombat.DoubleArcherQuivers(agent);
        }

        public override void InitializeAgentStatsAfterDeploymentFinished(
            Agent agent) =>
            NativeModel.InitializeAgentStatsAfterDeploymentFinished(agent);

        public override void InitializeMissionEquipmentAfterDeploymentFinished(
            Agent agent) =>
            NativeModel.InitializeMissionEquipmentAfterDeploymentFinished(agent);

        public override void UpdateAgentStats(
            Agent agent,
            AgentDrivenProperties agentDrivenProperties)
        {
            NativeModel.UpdateAgentStats(agent, agentDrivenProperties);
            // Native resets the favor to 1 on every update (SetAiRelatedProperties),
            // so this never compounds. See GwpDualBladeAgents.RangedFavorScale.
            agentDrivenProperties.AiWeaponFavorMultiplierRanged *=
                GwpDualBladeAgents.RangedFavorScale(agent);
        }

        public override float GetDifficultyModifier() =>
            NativeModel.GetDifficultyModifier();

        public override bool CanAgentRideMount(Agent agent, Agent targetMount) =>
            NativeModel.CanAgentRideMount(agent, targetMount);

        public override bool HasHeavyArmor(Agent agent) =>
            NativeModel.HasHeavyArmor(agent);

        public override float GetEffectiveArmorEncumbrance(
            Agent agent,
            Equipment equipment) =>
            NativeModel.GetEffectiveArmorEncumbrance(agent, equipment);

        public override float GetEffectiveMaxHealth(Agent agent) =>
            NativeModel.GetEffectiveMaxHealth(agent);

        public override float GetEnvironmentSpeedFactor(Agent agent) =>
            NativeModel.GetEnvironmentSpeedFactor(agent);

        public override float GetWeaponInaccuracy(
            Agent agent,
            WeaponComponentData weapon,
            int weaponSkill) =>
            NativeModel.GetWeaponInaccuracy(agent, weapon, weaponSkill);

        public override float GetDetachmentCostMultiplierOfAgent(
            Agent agent,
            IDetachment detachment) =>
            NativeModel.GetDetachmentCostMultiplierOfAgent(agent, detachment);

        public override float GetInteractionDistance(Agent agent) =>
            NativeModel.GetInteractionDistance(agent);

        public override float GetMaxCameraZoom(Agent agent) =>
            NativeModel.GetMaxCameraZoom(agent);

        // The battle mastery bonus is applied once, at the base raw read that every
        // model chain ends in (GwpBattleMasteryEffectiveSkillPatch); never here.
        public override int GetEffectiveSkill(Agent agent, SkillObject skill) =>
            NativeModel.GetEffectiveSkill(agent, skill);

        public override int GetEffectiveSkillForWeapon(
            Agent agent,
            WeaponComponentData weapon) =>
            NativeModel.GetEffectiveSkillForWeapon(agent, weapon);

        public override float GetWeaponDamageMultiplier(
            Agent agent,
            WeaponComponentData weapon) =>
            NativeModel.GetWeaponDamageMultiplier(agent, weapon);

        public override float GetEquipmentStealthBonus(Agent agent) =>
            NativeModel.GetEquipmentStealthBonus(agent);

        public override float GetSneakAttackMultiplier(
            Agent agent,
            WeaponComponentData weapon) =>
            NativeModel.GetSneakAttackMultiplier(agent, weapon);

        public override float GetKnockBackResistance(Agent agent) =>
            NativeModel.GetKnockBackResistance(agent);

        public override float GetKnockDownResistance(
            Agent agent,
            StrikeType strikeType = StrikeType.Invalid) =>
            NativeModel.GetKnockDownResistance(agent, strikeType);

        public override float GetDismountResistance(Agent agent) =>
            NativeModel.GetDismountResistance(agent);

        public override float GetBreatheHoldMaxDuration(
            Agent agent,
            float baseBreatheHoldMaxDuration) =>
            NativeModel.GetBreatheHoldMaxDuration(
                agent,
                baseBreatheHoldMaxDuration);

        public override string GetMissionDebugInfoForAgent(Agent agent) =>
            NativeModel.GetMissionDebugInfoForAgent(agent);

        internal static int ApplyBattleMastery(
            Agent? agent,
            SkillObject? skill,
            int nativeSkill)
        {
            if (agent == null || skill == null)
                return nativeSkill;

            int bonus = 0;
            if (skill == DefaultSkills.OneHanded
                || skill == DefaultSkills.Athletics)
            {
                bonus = GwpAlternativeAttackControlBehavior
                    .GetMeleeMasteryBonus(agent);
            }
            else if (skill == DefaultSkills.Bow)
            {
                bonus = GwpAlternativeAttackControlBehavior
                    .GetBowMasteryBonus(agent);
            }

            return bonus > 0 && nativeSkill < MasteredSkillValue
                ? Math.Min(MasteredSkillValue, nativeSkill + bonus)
                : nativeSkill;
        }
    }

    /// <summary>
    /// Adds the mission-local battle mastery to AgentStatCalculateModel.GetEffectiveSkill,
    /// the base method that reads the character's raw skill. Every stat model reaches it
    /// exactly once per read: Sandbox's override starts from base.GetEffectiveSkill and adds
    /// perks on top, both Naval models and this mod's model delegate to their BaseModel, and
    /// the Custom Battle model does not override it (v1.4.8). Patching only here is what
    /// keeps one bonus from being added two or three times (2026-09-24): the Sandbox and
    /// Naval overrides used to be patched too, on top of this mod's own wrapper adding it.
    /// A postfix only, as before; no prefix or finalizer wraps the base method.
    /// </summary>
    [HarmonyPatch(typeof(AgentStatCalculateModel), nameof(AgentStatCalculateModel.GetEffectiveSkill))]
    internal static class GwpBattleMasteryEffectiveSkillPatch
    {
        private static void Postfix(
            Agent agent,
            SkillObject skill,
            ref int __result) =>
            __result = GwpAgentStatCalculateModel.ApplyBattleMastery(
                agent,
                skill,
                __result);
    }
}
