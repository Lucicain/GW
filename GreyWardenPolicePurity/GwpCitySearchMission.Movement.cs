using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private readonly List<Agent> _hallPosts = new();

        internal bool SuppressFollowerTalk(Agent agent) => _ours.ContainsKey(agent) && !agent.Character.IsHero;

        private void PostHallTroops()
        {
            if (!InHall || Search.SearchWar) return;
            Order(OrderType.StandYourGround);
            Vec3[] anchors = Mission.Agents.Where(agent => agent.IsActive() && IsWatch(agent))
                .Select(agent => agent.Position).ToArray();
            if (anchors.Length == 0) anchors = Mission.Scene.FindEntitiesWithTag("defender_infantry")
                .Select(entity => entity.GetGlobalFrame().origin).ToArray();
            var occupied = new List<Vec3>();
            int slot = 0;
            foreach (Agent agent in _ours.Keys.Where(ally => ally.IsActive() && ally.IsAIControlled))
            {
                if (anchors.Length == 0) break;
                Vec3 anchor = anchors[slot++ % anchors.Length];
                for (int attempt = 0; attempt < GwpTuning.CitySearch.HallPostAttempts; attempt++)
                {
                    Vec3 position = Mission.GetRandomPositionAroundPoint(anchor, 2f, GwpTuning.CitySearch.HallPostRadius);
                    if (occupied.Any(point => point.Distance(position) < GwpTuning.CitySearch.HallPostSpacing)
                        || _hallTarget != null && _hallTarget.Position.Distance(position) < GwpTuning.CitySearch.GuardChallengeDistance
                        || Agent.Main != null && Agent.Main.Position.Distance(position) < GwpTuning.CitySearch.SecureTargetDistance) continue;
                    WorldPosition world = new WorldPosition(Mission.Scene, System.UIntPtr.Zero, position, true);
                    agent.SetScriptedPosition(ref world, false);
                    _hallPosts.Add(agent);
                    occupied.Add(position);
                    break;
                }
            }
        }

        private void ReleaseHallPosts(OrderType order, MBReadOnlyList<Formation> formations,
            OrderController controller, params object[] args)
        {
            foreach (Agent agent in _hallPosts.Where(ally => ally.IsActive())) agent.DisableScriptedMovement();
            _hallPosts.Clear();
        }

        private void ResumeDuelWalk()
        {
            if (!Search.DuelAgreed || Search.SearchWar || _hallTarget?.IsActive() != true || Agent.Main == null) return;
            if (!InHall)
                _hallTarget.TeleportToPosition(Mission.GetRandomPositionAroundPoint(Agent.Main.Position, 2f, 3f));
            BeginDuelWalk(InHall ? "center" : "arena");
        }

        private void BeginDuelWalk(string destination)
        {
            if (!Search.DuelAgreed || Search.SearchWar || _hallTarget?.IsActive() != true) return;
            Passage? door = Mission.GetMissionBehavior<MissionAgentHandler>().TownPassageProps
                .OfType<Passage>().FirstOrDefault(passage => passage.ToLocation?.StringId == destination);
            if (door == null) return;
            // 原版正常换地点的脚本行为：导航到通道，PassageAI负责过门、ChangeLocation及FadeOut。
            ScriptBehavior.AddUsableMachineTarget(_hallTarget, door);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("DUEL_WALK", "from=" + _place + "; to=" + destination);
#endif
        }

        internal bool AllowsDuelArenaEntry(LocationCharacter character, Location destination) =>
            Search.DuelAgreed && !Search.SearchWar && destination.StringId == "arena"
            && character.Character == _target.CharacterObject;
    }

    [HarmonyPatch(typeof(MissionConversationLogic), nameof(MissionConversationLogic.IsThereAgentAction))]
    internal static class GwpCitySearchFollowerTalkPatch
    {
        private static void Postfix(Agent otherAgent, ref bool __result)
        {
            if (GwpCitySearchMissionController.Current?.SuppressFollowerTalk(otherAgent) == true) __result = false;
        }
    }

    [HarmonyPatch(typeof(Location), nameof(Location.CanAIEnter))]
    internal static class GwpCitySearchDuelArenaEntryPatch
    {
        private static void Postfix(Location __instance, LocationCharacter character, ref bool __result)
        {
            if (GwpCitySearchMissionController.Current?.AllowsDuelArenaEntry(character, __instance) == true) __result = true;
        }
    }
}
