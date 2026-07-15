using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Preserves the active game mode's stat model and changes only the
    /// effective maximum health of Grey Warden-affiliated human agents.
    /// The engine calls GetEffectiveMaxHealth while it initializes every
    /// mission agent, so this covers troops, core lords, and future members of
    /// the Grey Warden clan without a mission tick or a health refill loop.
    /// </summary>
    internal sealed class GwpAgentStatCalculateModel : AgentStatCalculateModel
    {
        private const float GreyWardenHealthMultiplier = 1.5f;

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

        public override void InitializeMissionEquipment(Agent agent) =>
            NativeModel.InitializeMissionEquipment(agent);

        public override void InitializeAgentStatsAfterDeploymentFinished(
            Agent agent) =>
            NativeModel.InitializeAgentStatsAfterDeploymentFinished(agent);

        public override void InitializeMissionEquipmentAfterDeploymentFinished(
            Agent agent) =>
            NativeModel.InitializeMissionEquipmentAfterDeploymentFinished(agent);

        public override void UpdateAgentStats(
            Agent agent,
            AgentDrivenProperties agentDrivenProperties) =>
            NativeModel.UpdateAgentStats(agent, agentDrivenProperties);

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

        public override float GetEffectiveMaxHealth(Agent agent)
        {
            float nativeHealth = NativeModel.GetEffectiveMaxHealth(agent);
            return agent != null
                && agent.IsHuman
                && GwpCommon.IsGreyWardenAffiliatedCharacter(agent.Character)
                    ? nativeHealth * GreyWardenHealthMultiplier
                    : nativeHealth;
        }

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
    }
}
