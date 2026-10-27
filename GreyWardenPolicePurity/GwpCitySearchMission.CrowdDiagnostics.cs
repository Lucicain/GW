#if GWP_DIAGNOSTICS
using System.Collections.Generic;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private readonly List<(Agent Agent, Vec3 Start, string Identity, bool Flee)> _crowdAtFight = new();

        private static string CrowdWeapons(Agent agent) => string.Join(",", Enumerable.Range(0, (int)EquipmentIndex.NumAllWeaponSlots)
            .Where(slot => !agent.Equipment[slot].IsEmpty)
            .Select(slot => slot + ":" + agent.Equipment[slot].Item.StringId));

        private void CaptureCrowd()
        {
            _crowdAtFight.Clear();
            foreach (Agent agent in Mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman
                         && agent.IsAIControlled && !IsOurs(agent)))
            {
                var character = agent.Character as CharacterObject;
                string identity = "index=" + agent.Index + "; id=" + character?.StringId
                    + "; occupation=" + character?.Occupation + "; name=" + character?.Name
                    + "; initialArmed=" + agent.HasWeapon() + "; initialDefender=" + IsDefender(agent)
                    + "; weapons=" + CrowdWeapons(agent);
                _crowdAtFight.Add((agent, agent.Position, identity, ShouldFlee(agent)));
            }
        }

        private void WriteCrowdSnapshot(string sample)
        {
            foreach (var entry in _crowdAtFight)
            {
                Agent agent = entry.Agent;
                // 移除后只读事先保存的身份，不访问已离场人物的装备、位置或导航器。
                bool active = agent.IsActive();
                string details = "sample=" + sample + "; place=" + _place + "; " + entry.Identity
                    + "; initialFlee=" + entry.Flee + "; active=" + active;
                if (active)
                {
                    var navigator = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
                    var group = navigator?.GetActiveBehaviorGroup();
                    details += "; armed=" + agent.HasWeapon() + "; defender=" + IsDefender(agent) + "; flee=" + ShouldFlee(agent)
                        + "; moved=" + agent.Position.Distance(entry.Start).ToString("0.0")
                        + "; position=" + agent.Position + "; watch=" + agent.CurrentWatchState
                        + "; group=" + group?.GetType().Name + "; behavior=" + navigator?.GetActiveBehavior()?.GetType().Name
                        + "; scripted=" + group?.ScriptedBehavior?.GetType().Name
                        + "; target=" + navigator?.TargetUsableMachine?.GetType().Name;
                }
                GwpAiDiagnostics.WriteCitySearch("CROWD_STATE", details);
            }
        }

        private void WriteCrowdRemoval(Agent agent, AgentState state)
        {
            foreach (var entry in _crowdAtFight.Where(entry => entry.Agent == agent))
                GwpAiDiagnostics.WriteCitySearch("CROWD_REMOVED", "place=" + _place + "; " + entry.Identity + "; state=" + state);
        }
    }
}
#endif
