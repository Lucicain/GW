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
    internal sealed partial class GwpCitySearchMissionController : IGwpBattleSceneSource
    {
        private readonly Queue<(GreyWardenSafePartyAgentOrigin Origin, float Health)> _cityReserves = new();
        private readonly Dictionary<Agent, GreyWardenSafePartyAgentOrigin> _citySoldiers = new();
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
        bool IGwpBattleSceneSource.AllowsWardenRescue => false;
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

        private static string CitySoldierKey(GreyWardenSafePartyAgentOrigin origin) =>
            ((PartyBase)origin.BattleCombatant).MobileParty.StringId + "|" + origin.Troop.StringId;

        private static bool HasBattleWeapon(CharacterObject character) => character.BattleEquipments.Any(equipment =>
            Enumerable.Range(0, 5).Any(slot => equipment[(EquipmentIndex)slot].Item?.Weapons
                .Any(weapon => weapon.WeaponFlags.HasAnyFlag(WeaponFlags.WeaponMask)) == true));

        /// <summary>原版城内居民先生成；已在场的同兵种守卫占用真实名册，不能在预备兵中重复出现。</summary>
        private void PrepareCityReserves(IEnumerable<MobileParty> parties)
        {
            if (_cityReservesReady) return;
            var ordinals = new Dictionary<string, int>();
            int reserveBudget = InHall ? 0 : Search.HasLeftHall ? int.MaxValue
                : Search.ClaimEntryWave() ? GwpTuning.CitySearch.EntryReserveLimit : 0;
            foreach (MobileParty party in parties)
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    CharacterObject character = element.Character;
                    // 英雄只沿用原版人口，不能在大厅/街上复制另一位领主。
                    if (character.IsHero || !HasBattleWeapon(character)) continue;
                    string sourceKey = party.StringId + "|" + character.StringId;
                    int healthy = System.Math.Max(0, element.Number - element.WoundedNumber - Search.EnemyLossCount(sourceKey));
                    for (int i = 0; i < healthy; i++)
                    {
                        Agent? present = _defenders.FirstOrDefault(agent => agent.Character == character
                            && !_citySoldiers.ContainsKey(agent)
                            && (agent.Origin.BattleCombatant is not PartyBase actual || actual.MobileParty == null || actual == party.Party));
                        if (present == null && reserveBudget == 0) break;
                        var origin = new GreyWardenSafePartyAgentOrigin(party.Party, character, false, i);
                        string key = CitySoldierKey(origin);
                        ordinals.TryGetValue(key, out int ordinal);
                        ordinals[key] = ordinal + 1;
                        float health = Search.SavedEnemyHealth(key, ordinal);
                        if (present != null)
                        {
                            present.Origin = origin;
                            _citySoldiers.Add(present, origin);
                            if (health > 0f) present.Health = System.Math.Min(present.HealthLimit, health);
                        }
                        else if (reserveBudget > 0)
                        {
                            _cityReserves.Enqueue((origin, health));
                            reserveBudget--;
                        }
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

        // 原版守方点（攻城布阵组）与城内卫兵站岗、巡逻出生点（SandBox v1.4.8 反编译与场景 XML 2026-10-26）。
        private static readonly string[] CityReserveTags =
        {
            "defender_infantry_reinforcement", "defender_ranged_reinforcement", "defender_infantry", "defender_ranged",
            "sp_guard", "sp_guard_with_spear", "sp_guard_patrol", "sp_guard_castle"
        };

        /// <summary>
        /// 原版单机攻城只取一个固定守方增援点，empire_town_g 那一处离城门外出生点 2.1 米，撤离时援兵就刷在玩家身边。
        /// 改为在原版守方点与城内卫兵出生点中取离玩家最远的一处，同原版野战援军从远端进场。（用户裁定 2026-10-26）
        /// </summary>
        private GameEntity? CityReserveSpawn()
        {
            if (Agent.Main?.IsActive() != true) return null;
            Vec3 player = Agent.Main.Position;
            GameEntity? best = CityReserveTags.SelectMany(tag => Mission.Scene.FindEntitiesWithTag(tag))
                .OrderByDescending(point => point.GlobalPosition.Distance(player)).FirstOrDefault();
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("RESERVE_POINT", "place=" + _place + "; fromPlayer="
                + (best == null ? "-" : best.GlobalPosition.Distance(player).ToString("0.0")) + "; remaining=" + _cityReserves.Count);
#endif
            return best;
        }

        private void TickCityReserves()
        {
            if (_stage != Stage.Fighting || !_cityReservesReady || _cityReserves.Count == 0
                || Mission.CurrentTime < _nextCityReserve) return;
            _nextCityReserve = Mission.CurrentTime + GwpTuning.CitySearch.ReserveInterval;
            int living = _defenders.Count(agent => agent.IsActive() && !agent.IsRunningAway);
            int count = System.Math.Min(GwpTuning.CitySearch.ReserveBatch,
                GwpTuning.CitySearch.DefenderLimit - living);
            if (count <= 0) return;
            // 没有真实点不能把人刷在世界原点；保留名册供下一地点。
            GameEntity? spawn = CityReserveSpawn();
            if (spawn == null) return;
            MatrixFrame frame = spawn.GetGlobalFrame();
            int arrived = 0;
            while (count-- > 0 && _cityReserves.Count > 0
                && Mission.AllAgents.Count(agent => agent.IsActive()) + 2 <= DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission)
            {
                var reserve = _cityReserves.Peek();
                var character = (CharacterObject)reserve.Origin.Troop;
                // 原版方法只接受导航网格内的位置，保留大厅出生点所在楼层的高度。
                Vec3 position = Mission.GetRandomPositionAroundPoint(frame.origin, .5f, 2f);
                Agent agent = Mission.SpawnAgent(new AgentBuildData(character)
                    .TroopOrigin(reserve.Origin).Team(Mission.PlayerEnemyTeam).Banner(reserve.Origin.Banner)
                    .ClothingColor1(reserve.Origin.FactionColor).ClothingColor2(reserve.Origin.FactionColor2)
                    .NoHorses(true).CivilianEquipment(false).IsReinforcement(true)
                    .InitialPosition(position).InitialDirection(frame.rotation.f.AsVec2));
                agent.WieldInitialWeapons();
                _cityReserves.Dequeue();
                _citySoldiers.Add(agent, reserve.Origin);
                if (reserve.Health > 0f) agent.Health = System.Math.Min(agent.HealthLimit, reserve.Health);
                if (agent.HasWeapon())
                {
                    agent.SetTeam(Mission.PlayerEnemyTeam, true);
                    _defenders.Add(agent);
                    SetSearchFighter(agent);
                    arrived++;
                }
                else agent.SetTeam(Team.Invalid, true);
            }
            // 一批是一波：配乐按波数决定增援提示，不能每个兵各算一波。
            if (arrived > 0) _cityReinforcementsSpawned?.Invoke(Mission.PlayerEnemyTeam.Side, arrived);
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
                .Concat(_cityReserves.Select(entry => (CitySoldierKey(entry.Origin), entry.Health))));
        }

        private void NoteCitySoldierRemoval(Agent agent, AgentState state)
        {
            if ((state == AgentState.Killed || state == AgentState.Unconscious || state == AgentState.Routed)
                && _citySoldiers.TryGetValue(agent, out GreyWardenSafePartyAgentOrigin origin))
            {
                Search.RecordEnemyLoss(CitySoldierKey(origin));
                _citySoldiers.Remove(agent);
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
#if GWP_DIAGNOSTICS
            CaptureSearchVisual(agent, "build");
#endif
            // 共用支援系统生成的临时援军也必须唤醒；只在本场使用，不放进主队跨场景名册。
            if (!_cityReservesReady || _stage != Stage.Fighting || !agent.IsHuman
                || agent.Origin is not GwpBattleSupportOrigin) return;
            if (agent.HasWeapon()) SetSearchFighter(agent);
            else agent.SetTeam(Team.Invalid, true);
        }
    }
}
