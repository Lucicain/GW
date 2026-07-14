using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Gives Grey Warden troops and lords the engine's native kick action and
    /// attaches an AI-input component that can actually press the kick key.
    /// No animation is forced: the engine itself chooses kick or shield bash
    /// from the agent's current block/shield state.
    /// </summary>
    public sealed class GwpKickBehavior : MissionBehavior
    {
        private const float GreyWardenAiKick = 1f;
        private const float GreyWardenAlternativeAttackStunMultiplier = 2f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            ConfigureKickCapability(agent);
        }

        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);

            // OnAgentCreated runs before Mission.BuildAgent initializes agent
            // components, which is the correct point to attach this component.
            // The two-second scheduler is exclusively an AI input provider.
            // Never attach it to the player-controlled agent; the player's E
            // key remains entirely native and has no mod cooldown.
            if (!IsEligibleGreyWarden(agent) || !agent.IsAIControlled)
                return;

            agent.AddComponent(new GwpKickInputComponent(agent));
        }

        private static void ConfigureKickCapability(Agent? agent)
        {
            if (!IsEligibleGreyWarden(agent))
                return;

            // Only enhance Grey Warden agents. Vanilla and other mods' agents
            // are deliberately left completely untouched.
            Agent greyWarden = agent!;
            greyWarden.SetAgentFlags(greyWarden.GetAgentFlags() | AgentFlag.CanKick);
            greyWarden.AgentDrivenProperties.AiKick = GreyWardenAiKick;

            // Native, persistent combat properties. Set once when the agent is
            // built; no timer, polling, extra blow, or forced reaction is used.
            greyWarden.AgentDrivenProperties.KickStunDurationMultiplier =
                GreyWardenAlternativeAttackStunMultiplier;
            greyWarden.AgentDrivenProperties.ShieldBashStunDurationMultiplier =
                GreyWardenAlternativeAttackStunMultiplier;
        }

        internal static bool IsEligibleGreyWarden(Agent? agent)
        {
            BasicCharacterObject? basicCharacter = agent?.Character;
            if (basicCharacter == null)
                return false;

            string characterId = basicCharacter.StringId;

            // CustomGame registers NPCCharacter as BasicCharacterObject, not
            // CampaignSystem.CharacterObject. ID matching therefore has to
            // happen before the campaign-only cast.
            if (characterId == GwpIds.CustomBattleCommanderId
                || GwpCommon.IsGreyWardenTroopId(characterId))
                return true;

            if (basicCharacter is not CharacterObject character)
                return false;

            if (character.HeroObject != null)
                return GwpCommon.IsGreyWardenLord(character.HeroObject);

            return false;
        }
    }
}
