using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private readonly HashSet<Agent> _streetLocationActors = new();
        private readonly HashSet<Agent> _restoredStreetBodies = new();
        private readonly Dictionary<Agent, GwpCityStreetBody> _streetDefeatedBodies = new();
        private readonly Dictionary<string, int> _streetLocationDeaths = new();

        private void PrepareStreetReturn()
        {
            if (InHall) return;
            foreach (GwpCityStreetBody body in Search.StreetBodies.Where(body => body.LocationActor))
                _streetLocationDeaths[body.Character] = _streetLocationDeaths.TryGetValue(body.Character, out int count) ? count + 1 : 1;
        }

        internal bool SuppressDefeatedStreetActor(LocationCharacter character)
        {
            if (InHall || character.Character.IsHero || !_streetLocationDeaths.TryGetValue(character.Character.StringId, out int count) || count == 0)
                return false;
            _streetLocationDeaths[character.Character.StringId] = count - 1;
            return true;
        }

        private void RestoreStreetBodies()
        {
            if (InHall) return;
            foreach (Agent agent in Mission.Agents.Where(agent => agent.IsHuman && agent != Agent.Main))
                _streetLocationActors.Add(agent);
            foreach (GwpCityStreetBody body in Search.StreetBodies)
            {
                CharacterObject character = MBObjectManager.Instance.GetObject<CharacterObject>(body.Character);
                BodyProperties.FromString(body.Body, out BodyProperties properties);
                // 原版MissionNetworkComponent重建非活跃人物也先CreateAgent，再MakeDead；不再次调用Die记战果。
                Agent agent = Mission.SpawnAgent(new AgentBuildData(character)
                    .TroopOrigin(new SimpleAgentOrigin(character)).Team(Team.Invalid)
                    .Equipment(Equipment.CreateFromEquipmentCode(body.Equipment)).EquipmentSeed(body.Seed).BodyProperties(properties)
                    .ClothingColor1(body.Color1).ClothingColor2(body.Color2).NoHorses(true).CivilianEquipment(false)
                    .InitialPosition(body.Position).InitialDirection(body.Direction));
                _restoredStreetBodies.Add(agent);
                var animation = agent.Monster.FillAnimationSystemData(MBActionSet.GetActionSet(body.ActionSet),
                    agent.Character.GetStepSize(), hasClippingPlane: false);
                agent.SetActionSet(ref animation);
                agent.MakeDead(body.Killed, ActionIndexCache.Create(body.Action));
            }
        }

        private void NoteStreetDefeat(Agent agent, AgentState state)
        {
            if (InHall || _restoredStreetBodies.Contains(agent) || !agent.IsHuman || agent.Character.IsHero
                || state != AgentState.Killed && state != AgentState.Unconscious) return;
            _streetDefeatedBodies[agent] = new GwpCityStreetBody
            {
                Character = agent.Character.StringId, Equipment = agent.SpawnEquipment.CalculateEquipmentCode(),
                Body = agent.BodyPropertiesValue.ToString(), Action = "act_death_by_arrow_pelvis",
                ActionSet = agent.ActionSet.IsValid ? agent.ActionSet.GetName() : "as_human_warrior",
                Killed = state == AgentState.Killed, LocationActor = _streetLocationActors.Contains(agent),
                Seed = agent.BodyPropertiesSeed, Color1 = agent.ClothingColor1, Color2 = agent.ClothingColor2,
                Position = agent.Position, Direction = agent.LookDirection.AsVec2
            };
        }

        private void SaveStreetScene()
        {
            if (InHall) return;
            // 已删除的Agent不在AllAgents里；只用它退场前缓存的值，不再读其原生对象。
            foreach (Agent agent in Mission.AllAgents)
                if (_streetDefeatedBodies.TryGetValue(agent, out GwpCityStreetBody body))
                {
                    body.Position = agent.Position;
                    body.Direction = agent.LookDirection.AsVec2;
                    ActionIndexCache action = agent.GetCurrentAction(0);
                    if (action.Index >= 0) body.Action = action.GetName();
                }
            Search.SaveStreetBodies(Search.StreetBodies.Concat(_streetDefeatedBodies.Values).ToArray());
            _streetDefeatedBodies.Clear();
        }
    }

    internal static class GwpCityStreetSpawnPatch
    {
        private static bool _installed;
        internal static void Install()
        {
            if (_installed) return;
            new Harmony(SubModule.HarmonyId).Patch(AccessTools.Method(typeof(MissionAgentHandler), nameof(MissionAgentHandler.SpawnDefaultLocationCharacter)),
                prefix: new HarmonyMethod(typeof(GwpCityStreetSpawnPatch), nameof(BeforeSpawn)));
            _installed = true;
        }

        private static bool BeforeSpawn(LocationCharacter locationCharacter, ref Agent __result)
        {
            if (GwpCitySearchMissionController.Current?.SuppressDefeatedStreetActor(locationCharacter) != true) return true;
            __result = null!;
            return false;
        }
    }
}
