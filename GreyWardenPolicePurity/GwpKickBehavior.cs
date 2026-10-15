using System.Linq;
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
#if GWP_DIAGNOSTICS
            // 比武大会刷人时 OnAgentBuild 早于任务装备就绪，读 WieldedWeapon 会抛空引用
            // （实机诊断 2026-10-16，TournamentFightMissionController.SpawnAgentWithRandomItems）。只读生成装备。
            if (GwpCommon.IsGreyWardenLordAgent(agent))
                TraceLordCombat(agent, "READY", "mode=" + Mission.Mode
                    + "; weapons=" + string.Join(",", Enumerable.Range(0, 4)
                        .Select(slot => agent.SpawnEquipment?[(EquipmentIndex)slot].Item?.StringId)
                        .Where(id => id != null))
                    + "; horse=" + agent.SpawnEquipment?[EquipmentIndex.Horse].Item?.StringId);
#endif
        }

#if GWP_DIAGNOSTICS
        // Mission-local sampling only; no persistent campaign state. Retire on acceptance.
        private readonly System.Collections.Generic.Dictionary<string, int> _lordCombatSamples = new();

        internal void TraceLordCombat(Agent agent, string effect, string details)
        {
            if (!GwpCommon.IsGreyWardenLordAgent(agent))
                return;
            string key = agent.Index + ":" + effect;
            _lordCombatSamples.TryGetValue(key, out int samples);
            if (samples >= 3)
                return;
            _lordCombatSamples[key] = samples + 1;
            GwpAiDiagnostics.WriteCombat("LORD_" + effect,
                "hero=" + agent.Character.StringId + "; agent=" + agent.Index + "; " + details);
        }
#endif

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
