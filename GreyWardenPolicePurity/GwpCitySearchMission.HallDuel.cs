using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private enum HallDuelPhase { None, Clearing, Leaving, Changing, Returning, Ready, Fighting, Finished }
        private HallDuelPhase _hallDuelPhase;
        private Passage? _hallDuelDoor;
        private LocationCharacter? _hallDuelCharacter;
        private readonly Dictionary<Agent, Vec3> _hallDepartures = new();
        private readonly HashSet<Agent> _hallExiting = new();
        private readonly HashSet<LocationCharacter> _hallExitCharacters = new();
        private readonly HashSet<Agent> _hallRemoved = new();
        private readonly Dictionary<Agent, float> _hallExitedTroops = new();
        private float _hallDuelReturnAt, _hallDuelHealth;
        private bool _hallDuelSpawnEquipment, _hallDuelPrompted;

        internal bool IsHallDuelTalk => Search.HallDuelLocked && InHall && _hallDuelPhase == HallDuelPhase.Ready
            && ReferenceEquals(Campaign.Current.ConversationManager.OneToOneConversationAgent, _hallTarget);

        internal void ChooseHallDuelStart() => _pendingHallAction = "hall_duel_start";

        private void BeginHallDuel()
        {
            if (!InHall || !Search.HallDuelLocked || _hallDuelPhase != HallDuelPhase.None || _hallTarget == null) return;
            _hallDuelDoor = Mission.GetMissionBehavior<MissionAgentHandler>().TownPassageProps.OfType<Passage>()
                .FirstOrDefault(passage => passage.ToLocation?.StringId == "center");
            if (_hallDuelDoor == null) return;
            _hallDuelCharacter = CampaignMission.Current.Location.GetLocationCharacter(_hallTarget.Origin);
            _hallDuelPhase = HallDuelPhase.Clearing;
            _hallTarget.SetTeam(Team.Invalid, true);
            _hallPosts.Clear();
            Order(OrderType.StandYourGround);
            foreach (Agent agent in Mission.Agents.Where(agent => !_hallRemoved.Contains(agent) && agent.IsActive() && agent.IsHuman
                         && agent != Agent.Main && agent != _hallTarget).ToArray()) OrderHallDeparture(agent);
            DisablePlayerPassages();
        }

        private void OrderHallDeparture(Agent agent)
        {
            LocationCharacter? character = CampaignMission.Current.Location.GetLocationCharacter(agent.Origin);
            if (character != null) _hallExitCharacters.Add(character);
            WorldFrame frame = _hallDuelDoor!.PilotStandingPoint.GetUserFrameForAgent(agent);
            _hallDepartures[agent] = frame.Origin.GetGroundVec3();
            agent.SetTeam(Team.Invalid, true);
            SetHallDuelPosition(agent, frame.Origin, frame.Rotation.f.AsVec2.RotationInRadians);
        }

        private void CompleteHallDeparture(Agent agent)
        {
            // FadeOut请求不等于已经退场；只在OnAgentRemoved回调后移出待离场名单。
            _hallExiting.Add(agent);
            if (_ours.ContainsKey(agent)) _hallExitedTroops[agent] = agent.Health;
            if (agent == _hallTarget) _hallDuelHealth = agent.Health;
            LocationCharacter? character = CampaignMission.Current.Location.GetLocationCharacter(agent.Origin);
            if (character != null)
                _settlement.LocationComplex.ChangeLocation(character, CampaignMission.Current.Location, _hallDuelDoor!.ToLocation);
            // 与原版PassageUsePoint相同：走到大门后离场，不在座位或大厅中间直接消失。
            agent.FadeOut(false, true);
        }

        private void NoteHallDuelRemoval(Agent agent, AgentState state)
        {
            if (_hallDuelPhase == HallDuelPhase.None || _hallDuelPhase == HallDuelPhase.Finished) return;
            _hallDepartures.Remove(agent);
            _hallExiting.Remove(agent);
            _hallRemoved.Add(agent);
            if (agent == _hallTarget && _hallDuelPhase == HallDuelPhase.Leaving && state == AgentState.Routed)
            {
                _hallDuelPhase = HallDuelPhase.Changing;
                _hallDuelReturnAt = Mission.CurrentTime + GwpTuning.CitySearch.HallDuelChangeSeconds;
            }
        }

        private bool TickHallDuel()
        {
            if (!Search.HallDuelLocked || !InHall || _hallDuelPhase == HallDuelPhase.None
                || _hallDuelPhase == HallDuelPhase.Fighting || _hallDuelPhase == HallDuelPhase.Finished) return false;
            foreach (Agent agent in Mission.Agents.Where(agent => !_hallRemoved.Contains(agent) && agent.IsActive() && agent.IsHuman
                         && agent != Agent.Main && agent != _hallTarget && !_hallDepartures.ContainsKey(agent)).ToArray()) OrderHallDeparture(agent);
            foreach (var entry in _hallDepartures.ToArray())
                if (!_hallRemoved.Contains(entry.Key) && !_hallExiting.Contains(entry.Key)
                    && entry.Key.Position.Distance(entry.Value) <= GwpTuning.CitySearch.HallDuelDoorDistance)
                    CompleteHallDeparture(entry.Key);
            if (_pendingHallAction != null || _pendingFight != null) return false;
            if (_hallDuelPhase == HallDuelPhase.Clearing && _hallDepartures.Count == 0)
            {
                _hallDuelPhase = HallDuelPhase.Leaving;
                OrderHallDeparture(_hallTarget!);
            }
            else if (_hallDuelPhase == HallDuelPhase.Changing && _hallDepartures.Count == 0 && Mission.CurrentTime >= _hallDuelReturnAt)
                ReenterHallDuel();
            else if (_hallDuelPhase == HallDuelPhase.Returning && _hallTarget?.IsActive() == true && Agent.Main?.IsActive() == true
                     && _hallTarget.Position.Distance(Agent.Main.Position) <= GwpTuning.CitySearch.HallDuelTalkDistance)
            {
                _hallDuelPhase = HallDuelPhase.Ready;
                Vec2 direction = (Agent.Main.Position - _hallTarget.Position).AsVec2;
                SetHallDuelPosition(_hallTarget, _hallTarget.GetWorldPosition(), direction.RotationInRadians);
            }
            if (_hallDuelPhase == HallDuelPhase.Ready && !_hallDuelPrompted && _hallDepartures.Count == 0
                && !Campaign.Current.ConversationManager.IsConversationFlowActive)
            {
                _hallDuelPrompted = true;
                Mission.GetMissionBehavior<MissionConversationLogic>().StartConversation(_hallTarget!, false);
            }
            return true;
        }

        private void ReenterHallDuel()
        {
            LocationCharacter character = _hallDuelCharacter!;
            AgentData data = character.AgentData;
            Equipment previousEquipment = data.AgentOverridenEquipment;
            bool previousCivilian = data.AgentCivilianEquipment;
            _hallDuelPhase = HallDuelPhase.Returning;
            _hallDuelSpawnEquipment = true;
            try
            {
                // 原版LocationEncounter只转发空地点↔当前地点；两个非空地点间搬迁不会生成场景人物。
                _settlement.LocationComplex.ChangeLocation(character, _hallDuelDoor!.ToLocation, CampaignMission.Current.Location);
                Mission.GetMissionBehavior<MissionAgentHandler>().SpawnEnteringLocationCharacter(character, _hallDuelDoor.ToLocation);
            }
            finally
            {
                // AgentBuildData持有同一AgentData引用；临时出场装备不能留在地点人物数据里。
                data.Equipment(previousEquipment).CivilianEquipment(previousCivilian);
                _hallDuelSpawnEquipment = false;
            }
            _hallTarget = Mission.Agents.First(agent => !_hallRemoved.Contains(agent) && agent.Character == _target.CharacterObject && agent.IsActive());
            _hallTarget.SetTeam(Team.Invalid, true);
            _hallTarget.Health = Math.Min(_hallTarget.HealthLimit, _hallDuelHealth);
            ScriptBehavior.AddAgentTarget(_hallTarget, Agent.Main);
        }

        internal void PrepareHallDuelSpawn(AgentBuildData data)
        {
            if (!_hallDuelSpawnEquipment || data.AgentCharacter != _target.CharacterObject) return;
            var equipment = new Equipment(_target.CharacterObject.FirstBattleEquipment);
            bool melee = false;
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                melee |= !equipment[slot].IsEmpty && equipment[slot].Item.PrimaryWeapon?.IsMeleeWeapon == true;
            if (!melee) equipment[EquipmentIndex.Weapon0] = new EquipmentElement(MBObjectManager.Instance.GetObject<ItemObject>("wooden_sword_t1"));
            data.CivilianEquipment(false).Equipment(equipment);
        }

        internal bool BlocksHallDuelEntry(LocationCharacter character, Location location) => Search.HallDuelLocked
            && location.StringId == "lordshall" && !(_hallDuelSpawnEquipment && character == _hallDuelCharacter);

        internal bool AllowsHallDuelExit(LocationCharacter character, Location location) => Search.HallDuelLocked
            && location == _hallDuelDoor?.ToLocation && _hallExitCharacters.Contains(character);

        private void StartHallDuel()
        {
            if (_hallDuelPhase != HallDuelPhase.Ready || _hallTarget?.IsActive() != true || Agent.Main?.IsActive() != true) return;
            if (Mission.Agents.Any(agent => !_hallRemoved.Contains(agent) && agent.IsActive() && agent.IsHuman && agent != Agent.Main && agent != _hallTarget))
            {
                _pendingHallAction = "hall_duel_start";
                return;
            }
            ReleaseHallDuelPosition(_hallTarget);
            _hallDuelPhase = HallDuelPhase.Fighting;
            _stage = Stage.Duel;
            Mission.GetMissionBehavior<MissionConversationLogic>().DisableStartConversation(true);
            _hallTarget.SetTeam(Mission.PlayerEnemyTeam, true);
            Mission.PlayerEnemyTeam.SetIsEnemyOf(Mission.PlayerTeam, true);
            SetSearchFighter(_hallTarget);
            Mission.SetMissionMode(MissionMode.Duel, false);
        }

        private void FinishHallDuel()
        {
            _hallDuelPhase = HallDuelPhase.Finished;
            _hallDepartures.Clear();
            _hallExiting.Clear();
            _hallExitCharacters.Clear();
            if (_hallTarget != null && !_hallRemoved.Contains(_hallTarget) && _hallTarget.IsActive()) ReleaseHallDuelPosition(_hallTarget);
        }

        private IEnumerable<(CharacterObject Character, float Health)> SearchSurvivors()
        {
            foreach (var entry in _ours)
                if (_hallExitedTroops.TryGetValue(entry.Key, out float health)) yield return (entry.Value, health);
                else if (!_hallRemoved.Contains(entry.Key) && entry.Key.IsActive()) yield return (entry.Value, entry.Key.Health);
        }

        private void SetHallDuelPosition(Agent agent, WorldPosition position, float rotation)
        {
            var daily = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator?.GetBehaviorGroup<DailyBehaviorGroup>();
            if (daily != null)
            {
                var behavior = daily.GetBehavior<GwpHallDuelPositionBehavior>() ?? daily.AddBehavior<GwpHallDuelPositionBehavior>();
                behavior.Set(position, rotation);
                daily.SetScriptedBehavior<GwpHallDuelPositionBehavior>();
            }
            else agent.SetScriptedPositionAndDirection(ref position, rotation, false);
        }

        private static void ReleaseHallDuelPosition(Agent agent)
        {
            var navigator = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            var daily = navigator?.GetBehaviorGroup<DailyBehaviorGroup>();
            if (daily?.HasBehavior<GwpHallDuelPositionBehavior>() == true)
            {
                daily.RemoveBehavior<GwpHallDuelPositionBehavior>();
                navigator!.ClearTarget();
            }
            agent.DisableScriptedMovement();
        }
    }

    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
    internal static class GwpHallDuelSpawnEquipmentPatch
    {
        private static void Prefix(AgentBuildData agentBuildData) => GwpCitySearchMissionController.Current?.PrepareHallDuelSpawn(agentBuildData);
    }

    internal sealed class GwpHallDuelPositionBehavior : AgentBehavior
    {
        private WorldPosition _position;
        private float _rotation;
        private bool _sent;
        public GwpHallDuelPositionBehavior(AgentBehaviorGroup group) : base(group) { }
        internal void Set(WorldPosition position, float rotation) { _position = position; _rotation = rotation; _sent = false; }
        public override float GetAvailability(bool isSimulation) => 0f;
        public override void Tick(float dt, bool isSimulation)
        {
            if (_sent) return;
            Navigator.SetTargetFrame(_position, _rotation, 0.8f, 0.05f, Agent.AIScriptedFrameFlags.None,
                disableClearTargetWhenTargetIsReached: true);
            _sent = true;
        }
        public override string GetDebugInfo() => "大厅单挑站位";
    }
}
