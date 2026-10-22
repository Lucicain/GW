using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController : IAgentStateDecider
    {
        private GwpCitySearchBehavior Search => Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
        private GwpCitySearchPlace _place;
        private bool InHall => _place == GwpCitySearchPlace.Hall;
        private bool _transitioning;
        private string? _pendingHallAction;
        private Agent? _hallTarget;
        private readonly Dictionary<string, int> _spawnedTroops = new Dictionary<string, int>();
        private readonly Dictionary<Agent, Team> _spectators = new Dictionary<Agent, Team>();
        internal bool IsHallTalk => InHall && _stage == Stage.HallTalking && _hallTarget != null
            && ReferenceEquals(Campaign.Current.ConversationManager.OneToOneConversationAgent, _hallTarget);

        private void RestoreTroopHealth(Agent agent, CharacterObject character)
        {
            _spawnedTroops.TryGetValue(character.StringId, out int ordinal);
            _spawnedTroops[character.StringId] = ordinal + 1;
            float health = Search.SavedHealth(character.StringId, ordinal);
            if (health > 0f) agent.Health = System.Math.Min(agent.HealthLimit, health);
        }

        private void BeginSearchStage()
        {
            _place = Search.SearchPlace;
            _warDeclared = Search.SearchWar;
            _hallTarget = Mission.Agents.FirstOrDefault(agent => agent.Character == _target.CharacterObject && agent.IsActive());
            if (InHall)
            {
                // 目标应由原版大厅人口生成。核对后缺席说明委托对象已经不在大厅，不生造第二个领主。
                if (_hallTarget == null)
                {
                    _pendingHallAction = "missing";
                    return;
                }
                _stage = Stage.HallReady;
                if (_warDeclared) _pendingFight = "forced_hall";
                else InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_hall_notice)));
            }
            else if (_place == GwpCitySearchPlace.Escape)
            {
                _stage = Stage.Escaping;
                if (_warDeclared) _pendingFight = "escape";
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_escape_notice)));
            }
            DisablePlayerPassages();
        }

        private bool CanUsePassage(PassageUsePoint passage) => !InHall
            && _place == GwpCitySearchPlace.Entry && passage.ToLocationId == "lordshall"
            && (_stage == Stage.Allowed || _stage == Stage.FightOver);

        internal bool TryUseSearchPassage(PassageUsePoint passage, Agent user)
        {
            if (user != Agent.Main) return false;
            if (CanUsePassage(passage)) _pendingHallAction = "enter_hall";
            return true; // 搜捕里的玩家门由本控制器办理，不让原版单独把玩家送走。
        }

        internal void AcceptHallSurrender() => _pendingHallAction = "surrender";
        internal void LeaveHall() => _pendingHallAction = "leave_hall";
        internal void ChooseHallCombat(bool duel) => _pendingHallAction = duel ? "duel" : "melee";

        private bool TickSearchProgress()
        {
            if (!Search.SearchActive) return false;
            if (_transitioning) return true;
            if (Campaign.Current.ConversationManager.IsConversationFlowActive) return false;
            if (_pendingHallAction != null)
            {
                string action = _pendingHallAction;
                _pendingHallAction = null;
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteCitySearch("PROGRESS", "place=" + _place + "; action=" + action);
#endif
                switch (action)
                {
                    case "enter_hall": TransitionTo(GwpCitySearchPlace.Hall); return true;
                    case "leave_hall": TransitionTo(GwpCitySearchPlace.Escape); return true;
                    case "missing": Search.FinishSearch(false); Mission.EndMission(); return true;
                    case "surrender":
                        Search.PeacefulCapture();
                        Mission.EndMission();
                        return true;
                    case "duel":
                        if (_settlement.IsTown) { TransitionTo(GwpCitySearchPlace.Arena); return true; }
                        StartHallDuel();
                        break;
                    case "duel_won":
                        RestoreSpectators();
                        Search.WonHall();
                        TransitionTo(GwpCitySearchPlace.Escape);
                        return true;
                    case "duel_lost":
                        RestoreSpectators();
                        Search.FinishSearch(false);
                        Mission.EndMission();
                        return true;
                    case "melee": _pendingFight = "hall_melee"; break;
                }
            }
            if (_stage == Stage.HallReady && !_warDeclared && _pendingFight == null
                && Agent.Main?.IsActive() == true && _hallTarget?.IsActive() == true
                && Agent.Main.Position.Distance(_hallTarget.Position) <= GwpTuning.CitySearch.GuardChallengeDistance)
            {
                _stage = Stage.HallTalking;
                Mission.GetMissionBehavior<MissionConversationLogic>().StartConversation(_hallTarget, false);
            }
            EndFightIfDefendersDown();
            if (_pendingHallAction != null) return false;
            // 逃离必须真的走到城门外出生点，不能用 Tab 从大厅或城中直接跳到大地图。
            if (_place == GwpCitySearchPlace.Escape && _pendingFight == null && Agent.Main?.IsActive() == true
                && Agent.Main.Position.Distance(_gate) <= GwpTuning.CitySearch.GateExitDistance)
            {
                Search.FinishSearch(true);
                Mission.EndMission();
                return true;
            }
            return false;
        }

        private void TransitionTo(GwpCitySearchPlace place)
        {
            _transitioning = true;
            Search.QueuePlace(place);
            Mission.EndMission();
        }

        private void OnSearchFightWon()
        {
            if (!Search.SearchActive) return;
            if (InHall)
            {
                Search.WonHall();
                _pendingHallAction = "leave_hall";
            }
            else
            {
                DisablePlayerPassages();
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(_place == GwpCitySearchPlace.Entry
                    ? GwpTextCatalog.gwp_cs_hall_notice : GwpTextCatalog.gwp_cs_escape_notice)));
            }
        }

        private void StartHallDuel()
        {
            if (_hallTarget?.IsActive() != true || Agent.Main?.IsActive() != true) return;
            _stage = Stage.Duel;
            Mission.GetMissionBehavior<MissionConversationLogic>().DisableStartConversation(true);
            // 原版藏身处：旁人暂离双方队伍，固定原位围观，不创建另一套队伍。
            foreach (Agent agent in Mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman
                         && agent != Agent.Main && agent != _hallTarget))
            {
                _spectators[agent] = agent.Team;
                agent.SetTeam(Team.Invalid, true);
                WorldPosition position = agent.GetWorldPosition();
                agent.SetScriptedPosition(ref position, false);
                agent.SetLookAgent(_hallTarget);
            }
            _hallTarget.SetTeam(Mission.PlayerEnemyTeam, true);
            Mission.PlayerEnemyTeam.SetIsEnemyOf(Mission.PlayerTeam, true);
            _hallTarget.SetWatchState(Agent.WatchState.Alarmed);
            AlarmedBehaviorGroup? alarmed = _hallTarget.GetComponent<CampaignAgentComponent>()?.AgentNavigator
                ?.GetBehaviorGroup<AlarmedBehaviorGroup>();
            if (alarmed != null)
            {
                alarmed.DisableCalmDown = true;
                alarmed.AddBehavior<FightBehavior>();
                alarmed.SetScriptedBehavior<FightBehavior>();
            }
            Mission.SetMissionMode(MissionMode.Duel, false);
        }

        private void RestoreSpectators()
        {
            foreach (var entry in _spectators.Where(entry => entry.Key.IsActive()))
            {
                entry.Key.DisableScriptedMovement();
                entry.Key.SetTeam(entry.Value, true);
            }
            _spectators.Clear();
        }

        private void NoteDuelRemoval(Agent agent)
        {
            if (_stage != Stage.Duel) return;
            if (agent == Agent.Main) _pendingHallAction = "duel_lost";
            else if (agent == _hallTarget) _pendingHallAction = "duel_won";
        }

        private bool CanLeaveSearch() => !_warDeclared && !InHall && _place == GwpCitySearchPlace.Entry
            && _stage != Stage.Fighting && _stage != Stage.Duel;

        private void EndSearchScene()
        {
            var bounty = Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>();
            bounty.RecordCitySearchLosses(_target, _killed.Values.Sum());
            if (_target.IsDead && InHall) bounty.RecordCitySearchDefeat(_target);
            if (_transitioning)
                Search.SaveSurvivors(_ours.Where(entry => entry.Key.IsActive())
                    .Select(entry => (entry.Value, entry.Key.Health)));
            else if (Search.SearchActive)
                Search.FinishSearch(false);
        }

        internal IEnumerable<Agent>? CivilianDanger(Agent civilian) => _stage == Stage.Fighting
            && !IsOurs(civilian) && !IsDefender(civilian)
            ? Mission.Agents.Where(agent => agent.IsActive() && IsOurs(agent)).ToArray() : null;

        // 单挑按竞技场原版只击昏，群殴让原版伤亡模型裁定；不改武器标记。
        public AgentState GetAgentState(Agent affectedAgent, float deathProbability, out bool usedSurgery)
        {
            usedSurgery = false;
            return _stage == Stage.Duel ? AgentState.Unconscious : AgentState.None;
        }
    }

    [HarmonyPatch(typeof(PassageUsePoint), nameof(PassageUsePoint.OnUse))]
    internal static class GwpCitySearchPassagePatch
    {
        private static bool Prefix(PassageUsePoint __instance, Agent userAgent) =>
            GwpCitySearchMissionController.Current?.TryUseSearchPassage(__instance, userAgent) != true;
    }

    /// <summary>原版平民逃跑仅把自定义打斗中的玩家本人当危险源；搜捕群战补上所有实际在场的我方兵。</summary>
    [HarmonyPatch(typeof(MissionFightHandler), nameof(MissionFightHandler.GetDangerSources))]
    internal static class GwpCitySearchCivilianDangerPatch
    {
        private static void Postfix(Agent ownerAgent, ref IEnumerable<Agent> __result)
        {
            IEnumerable<Agent>? danger = GwpCitySearchMissionController.Current?.CivilianDanger(ownerAgent);
            if (danger != null) __result = danger;
        }
    }

    // 原版走动会额外带入家族随从；自选搜捕名册已包含英雄，不能在名单之外再生成一批或重复生成同一英雄。
    [HarmonyPatch(typeof(MissionLocationLogic), nameof(MissionLocationLogic.SpawnCharactersAccompanyingPlayer))]
    internal static class GwpCitySearchCompanionsPatch
    {
        private static bool Prefix() => GwpCitySearchMissionController.Current == null;
    }
}
