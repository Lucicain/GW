using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Gives all Grey Warden troops and lords the engine's native kick action,
    /// including agents currently controlled by the player. Grey Warden AI
    /// also receives a strong preference for using it.
    /// </summary>
    public sealed class GwpKickBehavior : MissionBehavior
    {
        // Vanilla tops out at roughly 0.3. A value of 1 makes kicking a
        // signature close-range option while the engine still validates range,
        // facing and action state before starting the native animation.
        private const float GreyWardenAiKick = 1f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            EnableGreyWardenKick(agent);
        }

        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);
            EnableGreyWardenKick(agent);
        }

        private static void EnableGreyWardenKick(Agent? agent)
        {
            if (agent?.IsHuman != true)
                return;

            if (agent.Character is not CharacterObject character)
                return;

            bool isGreyWarden = character.HeroObject != null
                ? GwpCommon.IsGreyWardenLord(character.HeroObject)
                : GwpCommon.IsGreyWardenTroop(character);

            if (!isGreyWarden)
                return;

            agent.SetAgentFlags(agent.GetAgentFlags() | AgentFlag.CanKick);
            // Harmless while player-controlled, and already in place if the
            // engine later hands this agent back to AI control.
            agent.AgentDrivenProperties.AiKick = GreyWardenAiKick;
        }
    }
}
