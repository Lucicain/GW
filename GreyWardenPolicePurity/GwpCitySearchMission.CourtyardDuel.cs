using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects.Usables;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController
    {
        private readonly Dictionary<Agent, Vec3> _courtSeats = new();
        private bool _courtPreparing;
        private Vec3 _courtCenter, _courtTargetStart;
        private float _nextCourtCheck;
        private bool IsCourtAppointment => Search.UsesCourtyardDuel && Search.DuelAgreed && !Search.SearchWar;

        private Vec3? CourtGround(Vec3 position)
        {
            if (!Mission.IsPositionInsideBoundaries(position.AsVec2)
                || Mission.IsPositionInsideAnyBlockerNavMeshFace2D(position.AsVec2)) return null;
            var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, position, false);
            if (world.GetNavMesh() == UIntPtr.Zero) return null;
            Vec3 ground = world.GetNavMeshVec3();
            return ground.AsVec2.Distance(position.AsVec2) <= 0.25f ? ground : null;
        }

        private bool CourtLineClear(Vec3 from, Vec3 to)
        {
            var a = new WorldPosition(Mission.Scene, UIntPtr.Zero, from, true);
            var b = new WorldPosition(Mission.Scene, UIntPtr.Zero, to, true);
            if (!Mission.Scene.IsLineToPointClear(ref a, ref b, 0.5f)) return false;
            // 固定物理射线不把正在走位的人当作家具；低处与上身高度都查，桌面/栏杆不能漏过。
            if (from.DistanceSquared(to) < 0.01f) return true;
            foreach (float height in new[] { 0.45f, 1.5f })
                if (Mission.Scene.RayCastForClosestEntityOrTerrainFixedPhysics(from + Vec3.Up * height,
                        to + Vec3.Up * height, out float distance, 0.25f)) return false;
            return true;
        }

        private bool CanWalkToCourt(Agent agent, Vec3 position)
        {
            WorldPosition from = agent.GetWorldPosition();
            var to = new WorldPosition(Mission.Scene, UIntPtr.Zero, position, true);
            return Mission.Scene.GetPathDistanceBetweenPositions(ref from, ref to, 0.5f, out float distance);
        }

        private void PrepareCourtDuel()
        {
            if (!IsCourtAppointment || InHall || _hallTarget?.IsActive() != true || Agent.Main == null) return;
            var door = Mission.GetMissionBehavior<MissionAgentHandler>().TownPassageProps.OfType<Passage>()
                .FirstOrDefault(passage => passage.ToLocation?.StringId == "lordshall");
            Vec3 entrance = door?.GameEntity.GlobalPosition ?? Agent.Main.Position;
            // 先冻结本场真实名册，再为同一批人找场地，不边集合边换人。
            var watchers = _ours.Keys.Where(agent => agent.IsActive() && agent.IsAIControlled)
                .Concat(Mission.Agents.Where(agent => agent.IsActive() && IsWatch(agent)
                    && agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator?.GetBehaviorGroup<DailyBehaviorGroup>()
                        ?.GetBehavior<PatrollingGuardBehavior>() != null)
                    .OrderBy(agent => agent.Position.DistanceSquared(entrance)))
                .Distinct().Take(GwpTuning.CitySearch.CourtDuelSeats).ToList();
            var candidates = new List<Vec3>();
            var cells = new HashSet<(int x, int y, int z)>();
            int count = Mission.Scene.GetNavMeshFaceCount();
            for (int face = 0; face < count; face++)
            {
                Vec3 point = Vec3.Zero;
                Mission.Scene.GetNavMeshCenterPosition(face, ref point);
                if (point.Distance(entrance) <= GwpTuning.CitySearch.CourtDuelSearchDistance
                    && cells.Add(((int)Math.Floor(point.x / 2f), (int)Math.Floor(point.y / 2f), (int)Math.Floor(point.z))))
                    candidates.Add(point);
            }
            Vec3 SeatFor(Vec3 center, int index) => GwpDuelGround.Seat(center,
                index * GwpTuning.CitySearch.CourtDuelSeats / Math.Max(1, watchers.Count));
            bool Reachable(Vec3 point)
            {
                if (!CanWalkToCourt(Agent.Main, point) || !CanWalkToCourt(_hallTarget, point)) return false;
                if (Mission.Agents.Any(agent => agent.IsActive() && agent.IsHuman && agent != Agent.Main && agent != _hallTarget
                        && !watchers.Contains(agent) && agent.Position.Distance(point)
                            < GwpTuning.CitySearch.CourtDuelRadius + GwpTuning.CitySearch.CourtDuelClearance)) return false;
                for (int index = 0; index < watchers.Count; index++)
                {
                    Vec3? seat = CourtGround(SeatFor(point, index));
                    if (!seat.HasValue || !CanWalkToCourt(watchers[index], seat.Value)) return false;
                }
                return true;
            }
            if (!GwpDuelGround.TryPick(candidates, entrance, CourtGround, CourtLineClear, Reachable, out _courtCenter))
            {
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteCitySearch("COURT_DUEL_SITE_FAILED", "settlement=" + _settlement.StringId
                    + "; entrance=" + entrance + "; candidates=" + candidates.Count);
#endif
                return; // 约定保留且可正常Tab暂停；不在未经验证的位置开打或瞬移人物。
            }
            _courtTargetStart = CourtGround(new Vec3(_courtCenter.x + GwpTuning.CitySearch.CourtDuelStartOffset,
                _courtCenter.y, _courtCenter.z))!.Value;
            // 所有选定者的站位和路径都已参与选址验证；固定32席，现有人不足留空，不造人。
            var destinations = watchers.Select((agent, index) => (Agent: agent,
                Position: CourtGround(SeatFor(_courtCenter, index))!.Value)).ToList();
            Order(OrderType.StandYourGround);
            foreach (var entry in destinations)
            {
                _spectators[entry.Agent] = entry.Agent.Team;
                _courtSeats[entry.Agent] = entry.Position;
                entry.Agent.SetTeam(Team.Invalid, true);
                SetCourtPosition(entry.Agent, entry.Position, _courtCenter);
            }
            EquipCourtDuelist(_hallTarget);
            EquipCourtDuelist(Agent.Main);
            SetCourtPosition(_hallTarget, _courtTargetStart, _courtCenter);
            _courtPreparing = true;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("COURT_DUEL_SITE", "settlement=" + _settlement.StringId
                + "; center=" + _courtCenter + "; radius=" + GwpTuning.CitySearch.CourtDuelRadius
                + "; watchers=" + _courtSeats.Count + "; candidates=" + candidates.Count);
#endif
        }

        private static void EquipCourtDuelist(Agent agent)
        {
            // 原版竞技场单挑也用各自战斗装备。武器在本场景内补齐，不写入英雄装备或主队库存。
            var equipment = new Equipment(((CharacterObject)agent.Character).FirstBattleEquipment);
            bool melee = false;
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                melee |= !equipment[slot].IsEmpty && equipment[slot].Item.PrimaryWeapon?.IsMeleeWeapon == true;
            if (!melee) equipment[EquipmentIndex.Weapon0] = new EquipmentElement(
                MBObjectManager.Instance.GetObject<ItemObject>("wooden_sword_t1"));
            agent.UpdateSpawnEquipmentAndRefreshVisuals(equipment);
        }

        private void SetCourtPosition(Agent agent, Vec3 destination, Vec3 facing)
        {
            Vec2 direction = (facing - destination).AsVec2;
            var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, destination, true);
            var daily = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator?.GetBehaviorGroup<DailyBehaviorGroup>();
            if (daily != null)
            {
                var behavior = daily.GetBehavior<GwpCitySearchPositionBehavior>() ?? daily.AddBehavior<GwpCitySearchPositionBehavior>();
                behavior.Set(world, direction.RotationInRadians);
                daily.SetScriptedBehavior<GwpCitySearchPositionBehavior>();
            }
            else agent.SetScriptedPositionAndDirection(ref world, direction.RotationInRadians, false);
        }

        private static void ReleaseCourtPosition(Agent agent)
        {
            var navigator = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            var daily = navigator?.GetBehaviorGroup<DailyBehaviorGroup>();
            if (daily?.HasBehavior<GwpCitySearchPositionBehavior>() == true)
            {
                daily.RemoveBehavior<GwpCitySearchPositionBehavior>();
                navigator!.ClearTarget();
            }
            agent.DisableScriptedMovement();
        }

        private bool TickCourtDuel()
        {
            if (!_courtPreparing) return false;
            if (!Search.SearchActive || !IsCourtAppointment || _stage == Stage.Failed)
            {
                RestoreSpectators();
                return false;
            }
            if (_hallTarget?.IsActive() != true || _courtSeats.Any(entry => !entry.Key.IsActive()))
            {
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteCitySearch("COURT_DUEL_ASSEMBLY_CANCELLED", "center=" + _courtCenter);
#endif
                RestoreSpectators();
                return false;
            }
            if (Mission.CurrentTime < _nextCourtCheck) return true;
            _nextCourtCheck = Mission.CurrentTime + 0.5f;
            float tolerance = GwpTuning.CitySearch.CourtDuelArrivalDistance;
            if (Agent.Main?.IsActive() != true
                || _hallTarget.Position.Distance(_courtTargetStart) > tolerance
                || _courtSeats.Any(entry => entry.Key.Position.Distance(entry.Value) > tolerance)
                || Mission.Agents.Any(agent => agent.IsActive() && agent.IsHuman && agent != Agent.Main && agent != _hallTarget
                    && !_courtSeats.ContainsKey(agent) && agent.Position.Distance(_courtCenter) < GwpTuning.CitySearch.CourtDuelFightRadius)
                || Agent.Main.Position.Distance(_courtCenter) > GwpTuning.CitySearch.CourtDuelFightRadius) return true;
            ReleaseCourtPosition(_hallTarget);
            _courtPreparing = false;
            StartCourtDuel();
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("COURT_DUEL_STARTED", "center=" + _courtCenter + "; watchers=" + _courtSeats.Count);
#endif
            return true;
        }
    }

    /// <summary>站位期间持有原版导航目标，巡逻行为不能在下一tick重新把卫兵叫走。</summary>
    internal sealed class GwpCitySearchPositionBehavior : AgentBehavior
    {
        private WorldPosition _position;
        private float _rotation;
        private bool _sent;
        public GwpCitySearchPositionBehavior(AgentBehaviorGroup group) : base(group) { }
        internal void Set(WorldPosition position, float rotation) { _position = position; _rotation = rotation; _sent = false; }
        public override float GetAvailability(bool isSimulation) => 0f;
        public override void Tick(float dt, bool isSimulation)
        {
            if (_sent) return;
            Navigator.SetTargetFrame(_position, _rotation, 0.8f, 0.05f,
                Agent.AIScriptedFrameFlags.None, disableClearTargetWhenTargetIsReached: true);
            _sent = true;
        }
        public override string GetDebugInfo() => "Court duel position";
    }
}
