using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal sealed partial class GwpCitySearchMissionController : IGwpBattleSceneSource, IGwpSceneReinforcementSource
    {
        private readonly Queue<(PartyAgentOrigin Origin, float Health)> _cityReserves = new();
        private readonly Dictionary<Agent, PartyAgentOrigin> _citySoldiers = new();
        private readonly List<(string Key, float Health)> _routedCitySoldiers = new();
        private readonly List<IAgentOriginBase>[] _cityOpening = { new(), new() };
        private bool _cityReservesReady;
        private float _nextCityReserve;
        private event System.Action<BattleSideEnum, int>? _cityReinforcementsSpawned;
        event System.Action<BattleSideEnum, int> IGwpBattleSceneSource.OnReinforcementsSpawned
        {
            add => _cityReinforcementsSpawned += value;
            remove => _cityReinforcementsSpawned -= value;
        }

        bool IGwpBattleSceneSource.IsDeploymentOver => _cityReservesReady;
        bool IGwpBattleSceneSource.OpeningAgentsAlreadySpawned => true;
        int IGwpBattleSceneSource.NumberOfAgents => Mission.AllAgents.Count(agent => agent.IsActive());
        int IGwpBattleSceneSource.NumberOfRemainingDefenderTroops =>
            Mission.PlayerEnemyTeam.Side == BattleSideEnum.Defender ? _cityReserves.Count : 0;
        int IGwpBattleSceneSource.NumberOfRemainingAttackerTroops =>
            Mission.PlayerEnemyTeam.Side == BattleSideEnum.Attacker ? _cityReserves.Count : 0;
        int IGwpBattleSceneSource.GetTotalNumberOfTroopsForSide(BattleSideEnum side) => _cityOpening[(int)side].Count;
        IEnumerable<IAgentOriginBase> IGwpBattleSceneSource.GetAllTroopsForSide(BattleSideEnum side) => _cityOpening[(int)side];

        private MobileParty[] CityDefenseParties() => _settlement.Parties
            .Concat(new[] { _settlement.Town.GarrisonParty, _settlement.MilitiaPartyComponent?.MobileParty })
            .OfType<MobileParty>().Where(party => party != MobileParty.MainParty && party.IsActive
                && (party.MapFaction == _settlement.MapFaction
                    || FactionManager.IsAtWarAgainstFaction(party.MapFaction, Hero.MainHero.MapFaction)))
            .Distinct().ToArray();

        private static string CitySoldierKey(PartyAgentOrigin origin) =>
            origin.Party.MobileParty.StringId + "|" + origin.Troop.StringId;

        private static bool HasBattleWeapon(CharacterObject character) => character.BattleEquipments.Any(equipment =>
            Enumerable.Range(0, 5).Any(slot => equipment[(EquipmentIndex)slot].Item?.Weapons
                .Any(weapon => weapon.WeaponFlags.HasAnyFlag(WeaponFlags.WeaponMask)) == true));

        /// <summary>原版城内居民先生成；已在场的同兵种守卫占用真实名册，不能在预备兵中重复出现。</summary>
        private void PrepareCityReserves(IEnumerable<MobileParty> parties)
        {
            if (_cityReservesReady) return;
            var ordinals = new Dictionary<string, int>();
            foreach (MobileParty party in parties)
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    CharacterObject character = element.Character;
                    // 英雄只沿用原版人口，不能在大厅/街上复制另一位领主。
                    if (character.IsHero || !HasBattleWeapon(character)) continue;
                    for (int i = 0; i < element.Number - element.WoundedNumber; i++)
                    {
                        var origin = new PartyAgentOrigin(party.Party, character);
                        string key = CitySoldierKey(origin);
                        ordinals.TryGetValue(key, out int ordinal);
                        ordinals[key] = ordinal + 1;
                        float health = Search.SavedEnemyHealth(key, ordinal);
                        Agent? present = _defenders.FirstOrDefault(agent => agent.Character == character
                            && !_citySoldiers.ContainsKey(agent)
                            && (agent.Origin is not PartyAgentOrigin actual || actual.Party == party.Party));
                        if (present != null)
                        {
                            present.Origin = origin;
                            _citySoldiers.Add(present, origin);
                            if (health > 0f) present.Health = System.Math.Min(present.HealthLimit, health);
                        }
                        else _cityReserves.Enqueue((origin, health));
                    }
                }
            foreach (Agent agent in Mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman
                         && (IsOurs(agent) || _defenders.Contains(agent)) && agent.Origin != null))
                _cityOpening[(int)(IsOurs(agent) ? Mission.PlayerTeam.Side : Mission.PlayerEnemyTeam.Side)].Add(agent.Origin);
            foreach (var reserve in _cityReserves) _cityOpening[(int)Mission.PlayerEnemyTeam.Side].Add(reserve.Origin);
            _cityReservesReady = true;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("RESERVES_READY", "place=" + _place + "; roster=" + _cityReserves.Count
                + "; boundGuards=" + _citySoldiers.Count + "; parties=" + string.Join("|", parties.Select(party => party.StringId)));
#endif
        }

        /// <summary>沿用原版守方增援点→守方初始点顺序；大厅也用实际defender_infantry标签。</summary>
        private GameEntity? CityReserveSpawn(BasicCharacterObject character)
        {
            string formation = character.IsRanged ? "ranged" : "infantry";
            return Mission.Scene.FindEntityWithTag("defender_" + formation + "_reinforcement")
                ?? Mission.Scene.FindEntityWithTag("defender_infantry_reinforcement")
                ?? Mission.Scene.FindEntityWithTag("defender_" + formation)
                ?? Mission.Scene.FindEntityWithTag("defender_infantry");
        }

        bool IGwpSceneReinforcementSource.TryGetReinforcementFrame(BattleSideEnum side, BasicCharacterObject troop, out MatrixFrame frame)
        {
            GameEntity? spawn = side == Mission.PlayerEnemyTeam.Side ? CityReserveSpawn(troop)
                : Mission.Scene.FindEntityWithTag(InHall ? GwpCitySearchMission.HallSpawnTag : GwpCitySearchMission.GateSpawnTag);
            if (spawn == null && !InHall && side == Mission.PlayerTeam.Side)
                spawn = Mission.Scene.FindEntityWithTag("sp_player_outside")
                    ?? Mission.Scene.FindEntityWithTag("spawnpoint_player_outside");
            frame = spawn?.GetGlobalFrame() ?? MatrixFrame.Identity;
            return spawn != null;
        }

        private void TickCityReserves()
        {
            if (_stage != Stage.Fighting || !_cityReservesReady || _cityReserves.Count == 0
                || Mission.CurrentTime < _nextCityReserve) return;
            _nextCityReserve = Mission.CurrentTime + GwpTuning.CitySearch.ReserveInterval;
            int living = _defenders.Count(agent => agent.IsActive() && !agent.IsRunningAway);
            int count = System.Math.Min(GwpTuning.CitySearch.ReserveBatch,
                GwpTuning.CitySearch.DefenderLimit - living);
            while (count-- > 0 && _cityReserves.Count > 0
                && Mission.AllAgents.Count(agent => agent.IsActive()) + 2 <= DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission)
            {
                var reserve = _cityReserves.Peek();
                var character = (CharacterObject)reserve.Origin.Troop;
                GameEntity? spawn = CityReserveSpawn(character);
                if (spawn == null)
                {
#if GWP_DIAGNOSTICS
                    GwpAiDiagnostics.WriteCitySearch("RESERVES_NO_POINT", "place=" + _place + "; remaining=" + _cityReserves.Count);
#endif
                    return; // 没有真实点不能把人刷在世界原点；保留名册供下一地点。
                }
                MatrixFrame frame = spawn.GetGlobalFrame();
                // 原版方法只接受导航网格内的位置，保留大厅出生点所在楼层的高度。
                Vec3 position = Mission.GetRandomPositionAroundPoint(frame.origin, .5f, 2f);
                Agent agent = Mission.SpawnTroop(reserve.Origin, false, false, false, true, 0, 0, true, true,
                    position, frame.rotation.f.AsVec2);
                _cityReserves.Dequeue();
                _citySoldiers.Add(agent, reserve.Origin);
                if (reserve.Health > 0f) agent.Health = System.Math.Min(agent.HealthLimit, reserve.Health);
                if (agent.HasWeapon())
                {
                    agent.SetTeam(Mission.PlayerEnemyTeam, true);
                    _defenders.Add(agent);
                    SetSearchFighter(agent);
                    _cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, 1);
                }
                else agent.SetTeam(Team.Invalid, true);
            }
        }

        private bool HasCityDefenders() => _cityReserves.Count > 0
            || Mission.Agents.Any(IsCityEnemyFighter);

        private bool IsCityEnemyFighter(Agent agent) => agent.IsActive() && !agent.IsRunningAway && agent.IsHuman
            && agent.Team == Mission.PlayerEnemyTeam && agent.HasWeapon();

        private void SaveCitySoldiers()
        {
            if (!_cityReservesReady) return;
            Search.SaveEnemySurvivors(_citySoldiers.Where(entry => entry.Key.IsActive())
                .Select(entry => (CitySoldierKey(entry.Value), entry.Key.Health))
                .Concat(_routedCitySoldiers)
                .Concat(_cityReserves.Select(entry => (CitySoldierKey(entry.Origin), entry.Health))));
        }

        private void NoteCitySoldierRemoval(Agent agent, AgentState state)
        {
            if (state == AgentState.Routed && _citySoldiers.TryGetValue(agent, out PartyAgentOrigin origin))
                _routedCitySoldiers.Add((CitySoldierKey(origin), agent.Health));
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            // 共用支援系统生成的临时援军也必须唤醒；只在本场使用，不放进主队跨场景名册。
            if (!_cityReservesReady || _stage != Stage.Fighting || !agent.IsHuman
                || agent.Origin is not GwpBattleSupportOrigin) return;
            if (agent.HasWeapon()) SetSearchFighter(agent);
            else agent.SetTeam(Team.Invalid, true);
        }
    }
}
