using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox;
using SandBox.AI;
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
            var anchors = Mission.Agents.Where(agent => agent.IsActive() && IsWatch(agent))
                .Select(agent => (Position: agent.Position, Direction: agent.Frame.rotation.f.AsVec2)).ToArray();
            if (anchors.Length == 0) anchors = Mission.Scene.FindEntitiesWithTag("defender_infantry")
                .Select(entity => entity.GetGlobalFrame())
                .Select(frame => (Position: frame.origin, Direction: frame.rotation.f.AsVec2)).ToArray();
            var occupied = new List<Vec3>();
            int slot = 0;
            foreach (Agent agent in _ours.Keys.Where(ally => ally.IsActive() && ally.IsAIControlled))
            {
                if (anchors.Length == 0) break;
                var anchor = anchors[slot++ % anchors.Length];
                for (int attempt = 0; attempt < GwpTuning.CitySearch.HallPostAttempts; attempt++)
                {
                    Vec3 position = Mission.GetRandomPositionAroundPoint(anchor.Position, 2f, GwpTuning.CitySearch.HallPostRadius);
                    if (occupied.Any(point => point.Distance(position) < GwpTuning.CitySearch.HallPostSpacing)
                        || _hallTarget != null && _hallTarget.Position.Distance(position) < GwpTuning.CitySearch.GuardChallengeDistance
                        || Agent.Main != null && Agent.Main.Position.Distance(position) < GwpTuning.CitySearch.HallPostPlayerClearance) continue;
                    WorldPosition world = new WorldPosition(Mission.Scene, System.UIntPtr.Zero, position, true);
                    // 位置和朝向一起交给原版导航，随对应卫兵（或守卫点）面向站岗方向。
                    agent.SetScriptedPositionAndDirection(ref world, anchor.Direction.RotationInRadians, false);
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
            if (!InHall && _settlement.IsCastle)
            {
                PrepareCastleDuel();
                return;
            }
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

#if GWP_DIAGNOSTICS
        private float _nextDuelPace;

        /// <summary>
        /// 取证 2026-10-26：实机反馈赴约目标跑得离谱地快。每秒记目标与玩家的实际速度、原版速度上限与相隔距离，
        /// 判断是满速冲刺本身快（无甲常服、运动技能高），还是另有加速或瞬移。验收赴约后退休。
        /// </summary>
        private void WriteDuelPace()
        {
            if (_hallTarget?.IsActive() != true || Agent.Main?.IsActive() != true || !IsDuelWalker(_hallTarget)
                || Mission.CurrentTime < _nextDuelPace) return;
            _nextDuelPace = Mission.CurrentTime + 1f;
            GwpAiDiagnostics.WriteCitySearch("DUEL_PACE", "place=" + _place
                + "; target=" + _hallTarget.GetCurrentVelocity().Length.ToString("0.00")
                + "/" + _hallTarget.GetMaximumForwardUnlimitedSpeed().ToString("0.00")
                + " limit=" + _hallTarget.GetMaximumSpeedLimit().ToString("0.00")
                + "; player=" + Agent.Main.GetCurrentVelocity().Length.ToString("0.00")
                + "/" + Agent.Main.GetMaximumForwardUnlimitedSpeed().ToString("0.00")
                + "; gap=" + _hallTarget.Position.Distance(Agent.Main.Position).ToString("0.0"));
        }
#endif

        internal bool IsDuelWalker(Agent agent) =>
            Search.DuelAgreed && !Search.SearchWar && agent == _hallTarget;

        internal bool AllowsDuelArenaEntry(LocationCharacter character, Location destination) =>
            Search.DuelAgreed && !Search.SearchWar && destination.StringId == "arena"
            && character.Character == _target.CharacterObject;
    }

    /// <summary>
    /// 赴约的目标跑过去（用户裁定 2026-10-22）。原版穿门的 PassageAI 对未受惊者固定加 DoNotRun，
    /// 只对本次赴约目标去掉这一项，寻路与过门仍走原版（C#反编译 v1.4.8 SandBox.AI.PassageAI）。
    /// </summary>
    [HarmonyPatch(typeof(PassageAI), "GetScriptedFrameFlags")]
    internal static class GwpCitySearchDuelRunPatch
    {
        private static void Postfix(Agent agent, ref Agent.AIScriptedFrameFlags __result)
        {
            if (GwpCitySearchMissionController.Current?.IsDuelWalker(agent) == true)
                __result &= ~Agent.AIScriptedFrameFlags.DoNotRun;
        }
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
