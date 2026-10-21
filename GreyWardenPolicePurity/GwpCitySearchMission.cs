using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Towns;
using SandBox.Objects;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 进城搜捕的城镇中心场景：沿用原版城镇中心任务的全部行为（居民、卫兵、对话、打架、恐慌），
    /// 只把原版控制器换成 <see cref="GwpCitySearchMissionController"/>，让玩家穿战斗装备、带兵从城门进城。
    /// </summary>
    internal static class GwpCitySearchMission
    {
        /// <summary>原版「进城时遭遇战斗」用的城门外出生点：城镇场景全有，城堡约一半有（场景标签核对 2026-10-21）。</summary>
        internal const string GateSpawnTag = "sp_outside_near_town_main_gate";

        /// <summary>自有任务名：界面组见 <see cref="GwpCitySearchMissionViews"/>，带下令界面。</summary>
        internal const string MissionName = "GwpCitySearch";

        internal static void Open(Settlement settlement, Hero target, TroopRoster troops)
        {
            Location center = settlement.LocationComplex.GetLocationWithId("center");
            int wallLevel = settlement.Town.GetWallLevel();
            string scene = center.GetSceneName(wallLevel);
            string levels = Campaign.Current.Models.LocationModel.GetCivilianUpgradeLevelTag(wallLevel);
            GwpAiDiagnostics.WriteCitySearch("MISSION_OPEN", "settlement=" + settlement.StringId
                + "; castle=" + settlement.IsCastle + "; scene=" + scene + "; levels=" + levels
                + "; troops=" + troops.TotalManCount);
            MissionState.OpenNew(MissionName,
                SandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, levels, false, DecalAtlasGroup.Town),
                mission => Behaviors(settlement, target, troops, center));
        }

        private static MissionBehavior[] Behaviors(Settlement settlement, Hero target, TroopRoster troops, Location center)
        {
            var list = new List<MissionBehavior>
            {
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new MissionBasicTeamLogic(),
                new MissionSettlementPrepareLogic(),
                new GwpCitySearchMissionController(settlement, target, troops),
                new MissionAgentLookHandler(),
                new SandBoxMissionHandler()
            };
            if (settlement.IsTown) list.Add(new WorkshopMissionHandler(settlement));
            list.AddRange(new MissionBehavior[]
            {
                new BasicLeaveMissionLogic(),
                new LeaveMissionLogic(),
                new BattleAgentLogic(),
                new MountAgentLogic(),
                new NotableSpawnPointHandler(),
                new MissionAgentPanicHandler(),
                new AgentHumanAILogic(),
                new MissionAlleyHandler(),
                new MissionCrimeHandler(),
                new MissionConversationLogic(),
                new MissionAgentHandler(),
                new MissionLocationLogic(center, GateSpawnTag),
                new HeroSkillHandler(),
                new MissionFightHandler(),
                new MissionFacialAnimationHandler(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(),
                new VisualTrackerMissionBehavior(),
                new EquipmentControllerLeaveLogic()
            });
            return list.ToArray();
        }
    }

    /// <summary>
    /// 场景里的搜捕：玩家带兵从城门进来，卫兵上前盘问并做一次简单说服；对守卫或同势力的人动手
    /// 即玩家阵营对该势力宣战，卫兵和场景里同势力的人一起打（用户裁定 2026-10-21）。
    /// 进领主大厅、大厅交涉与杀出城属于后续阶段，这一版先关掉玩家能用的门。
    /// </summary>
    internal sealed class GwpCitySearchMissionController : MissionLogic
    {
        private enum Stage { Entering, Questioned, Allowed, Refused, Fighting, FightOver }

        internal static GwpCitySearchMissionController? Current { get; private set; }

        private readonly Settlement _settlement;
        private readonly Hero _target;
        private readonly TroopRoster _troops;
        private readonly Dictionary<Agent, CharacterObject> _ours = new Dictionary<Agent, CharacterObject>();
        private readonly Dictionary<CharacterObject, int> _killed = new Dictionary<CharacterObject, int>();
        private readonly Dictionary<CharacterObject, int> _wounded = new Dictionary<CharacterObject, int>();
        private readonly List<(Agent Agent, Vec3 Start)> _civiliansAtFight = new List<(Agent, Vec3)>();
        private readonly List<Agent> _defenders = new List<Agent>();
        private Stage _stage = Stage.Entering;
        private Agent? _guard;
        private Agent? _struck;
        private bool _warDeclared;
        private float _nextChallengeCheck;
        private float _civilianSampleAt = -1f;
        private int _healthyAtStart;

        internal GwpCitySearchMissionController(Settlement settlement, Hero target, TroopRoster troops)
        {
            _settlement = settlement;
            _target = target;
            _troops = troops;
        }

        internal TextObject TargetName => _target.Name;

        internal bool IsGuardTalk => _stage == Stage.Questioned && _guard != null
            && ReferenceEquals(Campaign.Current.ConversationManager.OneToOneConversationAgent, _guard);

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Current = this;
        }

        public override void OnRemoveBehavior()
        {
            if (Current == this) Current = null;
            base.OnRemoveBehavior();
        }

        public override void OnCreated()
        {
            base.OnCreated();
            // 带兵进城搜捕穿战斗装备，不穿常服（用户裁定 2026-10-21）。
            Mission.DoesMissionRequireCivilianEquipment = false;
        }

        public override void AfterStart()
        {
            Mission.SetMissionMode(MissionMode.StartUp, true);
            Mission.IsInventoryAccessible = false;
            Mission.IsQuestScreenAccessible = true;

            string spawnTag = GwpCitySearchMission.GateSpawnTag;
            GameEntity? spawn = Mission.Scene.FindEntityWithTag(spawnTag);
            if (spawn == null)
            {
                spawnTag = "sp_player_outside";
                spawn = Mission.Scene.FindEntityWithTag(spawnTag);
            }
            if (spawn == null)
            {
                spawnTag = "spawnpoint_player_outside";
                spawn = Mission.Scene.FindEntityWithTag(spawnTag);
            }
            SandBoxHelpers.MissionHelper.SpawnPlayer(spawn, civilianEquipment: false, noHorses: true);
            Mission.GetMissionBehavior<MissionAgentHandler>().SpawnLocationCharacters();
            int passages = DisablePlayerPassages();
            _healthyAtStart = MobileParty.MainParty?.MemberRoster.TotalHealthyCount ?? 0;
            int spawned = SpawnTroops();
            GwpAiDiagnostics.WriteCitySearch("SPAWNED", "settlement=" + _settlement.StringId
                + "; spawnTag=" + (spawn == null ? "none" : spawnTag) + "; troops=" + spawned
                + "; passagesClosed=" + passages + "; watch=" + Mission.Agents.Count(IsWatch));
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("ORDERS", "formations=" + DescribeFormations());
            _followSampleAt = Mission.CurrentTime + 10f;
            Mission.PlayerTeam.PlayerOrderController.OnOrderIssued += WriteOrderIssued;
#endif
        }

#if GWP_DIAGNOSTICS
        private float _followSampleAt = -1f;
        private float _orderSampleAt = -1f;
        private readonly Dictionary<Agent, Vec3> _positionsAtOrder = new Dictionary<Agent, Vec3>();

        /// <summary>取证：玩家下令界面有没有发出命令；5 秒后兵有没有照做（编队命令与位移）。</summary>
        private void WriteOrderIssued(OrderType orderType, MBReadOnlyList<Formation> appliedFormations,
            OrderController orderController, params object[] delegateParams)
        {
            GwpAiDiagnostics.WriteCitySearch("ORDER_ISSUED", "order=" + orderType + "; mode=" + Mission.Mode
                + "; applied=" + appliedFormations.Count + "; selected=" + orderController.SelectedFormations.Count
                + "; formations=" + DescribeFormations());
            _positionsAtOrder.Clear();
            foreach (Agent agent in _ours.Keys.Where(agent => agent.IsActive()))
                _positionsAtOrder[agent] = agent.Position;
            _orderSampleAt = Mission.CurrentTime + 5f;
        }

        private void WriteOrderCheck()
        {
            List<float> moved = _positionsAtOrder.Where(entry => entry.Key.IsActive())
                .Select(entry => entry.Key.Position.Distance(entry.Value)).ToList();
            GwpAiDiagnostics.WriteCitySearch("ORDER_CHECK", "troops=" + moved.Count
                + "; avgMoved=" + (moved.Count == 0 ? 0f : moved.Average()).ToString("0.0")
                + "; formations=" + DescribeFormations());
        }

        /// <summary>取证：有兵的编队是否已转为玩家控制、当前移动命令。</summary>
        private string DescribeFormations() => string.Join(",", Mission.PlayerTeam.FormationsIncludingEmpty
            .Where(formation => formation.CountOfUnits > 0)
            .Select(formation => formation.FormationIndex + ":" + formation.CountOfUnits
                + (formation.IsAIControlled ? ":ai:" : ":player:") + formation.GetReadonlyMovementOrderReference().OrderType));

        /// <summary>取证：进场 10 秒后我方士兵离玩家多远，证明跟随生效。</summary>
        private void WriteFollowCheck()
        {
            Agent? main = Agent.Main;
            if (main == null) return;
            List<float> distances = _ours.Keys.Where(agent => agent.IsActive())
                .Select(agent => agent.Position.Distance(main.Position)).ToList();
            GwpAiDiagnostics.WriteCitySearch("FOLLOW_CHECK", "troops=" + distances.Count
                + "; avg=" + (distances.Count == 0 ? 0f : distances.Average()).ToString("0.0")
                + "; max=" + (distances.Count == 0 ? 0f : distances.Max()).ToString("0.0")
                + "; formations=" + DescribeFormations());
        }
#endif

        /// <summary>原版门会把玩家单独送进新场景、兵就丢了；进大厅属于下一阶段，先只对玩家关门，平民仍可穿门逃开。</summary>
        private int DisablePlayerPassages()
        {
            int count = 0;
            foreach (UsableMachine machine in Mission.ActiveMissionObjects.FindAllWithType<UsableMachine>())
                foreach (StandingPoint point in machine.StandingPoints)
                    if (point is PassageUsePoint passage)
                    {
                        passage.SetIsDisabledForPlayersSynched(true);
                        count++;
                    }
            return count;
        }

        private int SpawnTroops()
        {
            Agent? main = Agent.Main;
            if (main == null) return 0;
            int count = 0;
            foreach (TroopRosterElement element in _troops.GetTroopRoster())
            {
                CharacterObject character = element.Character;
                if (character == null || character.IsPlayerCharacter) continue;
                for (int i = 0; i < element.Number - element.WoundedNumber; i++)
                {
                    Vec3 position = Mission.GetRandomPositionAroundPoint(main.Position, 1f, 4f);
                    // 来源挂主队：原版 GetAgentTeam 在有盟友队伍时只把「受玩家指挥」的兵放进玩家队伍，
                    // SimpleAgentOrigin 对普通兵没有所属部队，会被分进盟友队伍、无法指挥（C#反编译 v1.4.8，实机日志 2026-10-21）。
                    // 伤亡也由原版 BattleAgentLogic 经它直接写进主队名册。
                    Agent agent = Mission.SpawnTroop(new PartyAgentOrigin(PartyBase.MainParty, character), true, true, false, false,
                        0, 0, false, false, position, main.LookDirection.AsVec2);
                    _ours[agent] = character;
                    count++;
                }
            }
            TakeCommand(main);
            Order(OrderType.FollowMe);
            return count;
        }

        /// <summary>
        /// 把编队交给玩家指挥。原版建队时任务还没指定玩家队伍，编队一律交 AI；城镇任务没有部队 AI，
        /// 不交出来兵就原地不动。做法照原版 AssignPlayerRoleInTeamMissionController（C#反编译 v1.4.8 2026-10-21）。
        /// </summary>
        private void TakeCommand(Agent main)
        {
            Team team = Mission.PlayerTeam;
            team.SetPlayerRole(true, false);
            team.PlayerOrderController.Owner = main;
            foreach (Formation formation in team.FormationsIncludingEmpty)
                formation.PlayerOwner = main;
        }

        /// <summary>经原版下令控制器下令，与玩家按键下令同一条路，下令后编队转为玩家控制。</summary>
        private void Order(OrderType order)
        {
            Agent? main = Agent.Main;
            OrderController controller = Mission.PlayerTeam.PlayerOrderController;
            if (main == null || controller.Owner == null) return;
            controller.SelectAllFormations();
            if (order == OrderType.FollowMe) controller.SetOrderWithAgent(order, main);
            else controller.SetOrder(order);
        }

        public override void OnMissionTick(float dt)
        {
            if (_struck != null)
            {
                Agent struck = _struck;
                _struck = null;
                StartFight(struck);
            }
            if (_stage == Stage.Entering && Mission.CurrentTime >= _nextChallengeCheck)
            {
                _nextChallengeCheck = Mission.CurrentTime + 0.5f;
                TryChallenge();
            }
#if GWP_DIAGNOSTICS
            if (_followSampleAt > 0f && Mission.CurrentTime >= _followSampleAt)
            {
                _followSampleAt = -1f;
                WriteFollowCheck();
            }
            if (_orderSampleAt > 0f && Mission.CurrentTime >= _orderSampleAt)
            {
                _orderSampleAt = -1f;
                WriteOrderCheck();
            }
#endif
            if (_civilianSampleAt > 0f && Mission.CurrentTime >= _civilianSampleAt)
            {
                _civilianSampleAt = -1f;
                WriteCivilianFlight();
            }
        }

        private void TryChallenge()
        {
            Agent? main = Agent.Main;
            if (main == null || Campaign.Current.ConversationManager.IsConversationFlowActive) return;
            Agent? guard = Mission.Agents
                .Where(agent => agent.IsActive() && agent.IsAIControlled && IsWatch(agent)
                    && agent.Position.Distance(main.Position) <= GwpTuning.CitySearch.GuardChallengeDistance)
                .OrderBy(agent => agent.Position.Distance(main.Position))
                .FirstOrDefault();
            if (guard == null) return;
            _guard = guard;
            _stage = Stage.Questioned;
            GwpAiDiagnostics.WriteCitySearch("GUARD_CHALLENGE", "guard=" + guard.Character.StringId
                + "; distance=" + guard.Position.Distance(main.Position).ToString("0.0"));
            Mission.GetMissionBehavior<MissionConversationLogic>().StartConversation(guard, false);
        }

        internal void OnGuardDecision(bool allowed, string result)
        {
            if (_stage != Stage.Questioned) return;
            _stage = allowed ? Stage.Allowed : Stage.Refused;
            GwpAiDiagnostics.WriteCitySearch("GUARD_DECISION", "allowed=" + allowed + "; result=" + result);
            if (result != "player_leaves")
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(allowed
                    ? GwpTextCatalog.gwp_cs_allowed_notice : GwpTextCatalog.gwp_cs_refused_notice)));
        }

        /// <summary>
        /// 卫兵那关谈崩之后，我方的兵器碰到守卫就记下，下一帧开战；谈崩前或已放行时不算动手（用户裁定 2026-10-21）。
        /// 守卫开战前是中立的，原版对中立的人一律取消伤害、
        /// 不触发 OnAgentHit，所以只能在近战碰撞回调里认出这一下（C#反编译 v1.4.8 Mission.MeleeHitCallback
        /// 2026-10-21）。箭矢回调禁止补丁（troop-combat.md），射守卫不开战。不在原生回调里换队伍、宣战，留到任务帧里做。
        /// </summary>
        internal void NoteStrike(Agent? attacker, Agent? victim)
        {
            if (_stage != Stage.Refused || _struck != null || attacker == null || victim == null) return;
            if (IsOurs(attacker) && IsDefender(victim)) _struck = victim;
        }

        /// <summary>对守卫或同势力的人动手就是开战：玩家阵营对城主势力宣战，卫兵和场景里同势力的人一起打。</summary>
        private void StartFight(Agent struck)
        {
            if (!_warDeclared)
            {
                _warDeclared = true;
                Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()?.DeclarePlayerCaseWar(
                    _settlement.MapFaction, "city_search_watch_struck", GwpTextCatalog.gwp_cs_war_notice);
            }
            int ours = Mission.Agents.Count(agent => agent.IsActive() && IsOurs(agent));
            _defenders.Clear();
            _defenders.AddRange(Mission.Agents.Where(agent => agent.IsActive() && IsDefender(agent)));
            _stage = Stage.Fighting;
            Agent? main = Agent.Main;
            _civiliansAtFight.Clear();
            if (main != null)
                foreach (Agent agent in Mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman
                             && agent.IsAIControlled && !IsOurs(agent) && !IsDefender(agent)
                             && agent.Position.Distance(main.Position) <= 30f))
                    _civiliansAtFight.Add((agent, agent.Position));
            _civilianSampleAt = Mission.CurrentTime + 8f;
            GwpAiDiagnostics.WriteCitySearch("FIGHT_START", "struck=" + struck.Character?.StringId
                + "; war=" + _warDeclared + "; ours=" + ours + "; theirs=" + _defenders.Count
                + "; civiliansNear=" + _civiliansAtFight.Count);

            // 不用原版 MissionFightHandler：它打完把参战者放进新建的 Team 对象，
            // 我方会脱离玩家队伍、无法再指挥（Team 无相等重写，C#反编译 v1.4.8 2026-10-21）。
            Mission.PlayerEnemyTeam.SetIsEnemyOf(Mission.PlayerTeam, true);
            foreach (Agent defender in _defenders)
            {
                defender.SetTeam(Mission.PlayerEnemyTeam, true);
                if (!defender.IsAIControlled) continue;
                defender.SetWatchState(Agent.WatchState.Alarmed);
                // 同原版城里开打：交给场景行为里的战斗行为，并不再自行平静下来。
                AlarmedBehaviorGroup? alarmed = defender.GetComponent<CampaignAgentComponent>()?.AgentNavigator
                    ?.GetBehaviorGroup<AlarmedBehaviorGroup>();
                if (alarmed == null) continue;
                alarmed.DisableCalmDown = true;
                alarmed.AddBehavior<FightBehavior>();
                alarmed.SetScriptedBehavior<FightBehavior>();
            }
            Mission.SetMissionMode(MissionMode.Battle, false);
            Order(OrderType.Charge);
        }

        private void EndFightIfDefendersDown()
        {
            if (_stage != Stage.Fighting || _defenders.Any(agent => agent.IsActive())) return;
            _stage = Stage.FightOver;
            Mission.SetMissionMode(MissionMode.StartUp, false);
            GwpAiDiagnostics.WriteCitySearch("FIGHT_END", "oursLeft="
                + Mission.Agents.Count(agent => agent.IsActive() && IsOurs(agent)));
            Order(OrderType.FollowMe);
        }

        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            canPlayerLeave = _stage != Stage.Fighting;
            if (!canPlayerLeave)
                MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_cs_fight_not_over));
            return null!;
        }

        /// <summary>取证：开打时 30 米内的平民，8 秒后有多少跑开了（原版只把玩家本人当危险来源）。</summary>
        private void WriteCivilianFlight()
        {
            int moved = _civiliansAtFight.Count(entry => !entry.Agent.IsActive() || entry.Agent.Position.Distance(entry.Start) > 5f);
            GwpAiDiagnostics.WriteCitySearch("CIVILIAN_FLIGHT", "civilians=" + _civiliansAtFight.Count + "; movedOrGone=" + moved);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            EndFightIfDefendersDown();
            if (!_ours.TryGetValue(affectedAgent, out CharacterObject character)) return;
            Dictionary<CharacterObject, int>? bucket = agentState == AgentState.Killed ? _killed
                : agentState == AgentState.Unconscious ? _wounded : null;
            if (bucket != null) bucket[character] = bucket.TryGetValue(character, out int n) ? n + 1 : 1;
        }

        protected override void OnEndMission()
        {
            // 伤亡已由原版 BattleAgentLogic 经 PartyAgentOrigin 写进主队名册，这里只记录，核对是否只扣一次。
            GwpAiDiagnostics.WriteCitySearch("MISSION_END", "stage=" + _stage + "; war=" + _warDeclared
                + "; killed=" + _killed.Values.Sum() + "; wounded=" + _wounded.Values.Sum()
                + "; healthyBefore=" + _healthyAtStart
                + "; healthyNow=" + (MobileParty.MainParty?.MemberRoster.TotalHealthyCount ?? 0));
        }

        private bool IsOurs(Agent agent) => agent == Agent.Main || _ours.ContainsKey(agent);

        /// <summary>守卫：原版算作执法者的士兵、卫兵、狱卒，不含我方带进来的兵。</summary>
        private bool IsWatch(Agent agent) => agent.IsHuman && !IsOurs(agent)
            && agent.Character is CharacterObject character && MissionFightHandler.IsAgentJusticeWarrior(character);

        /// <summary>会出手的人：守卫，加上场景里与城主同势力的英雄。</summary>
        private bool IsDefender(Agent agent) => IsWatch(agent) || agent.IsHuman && !IsOurs(agent)
            && agent.Character is CharacterObject { IsHero: true } character && character.HeroObject is { } hero
            && hero != Hero.MainHero && hero.MapFaction == _settlement.MapFaction;
    }

    /// <summary>近战碰到人：交给进城搜捕认是不是对守卫动手。参数名须与原版回调一致，Harmony 按名绑定。</summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class GwpCitySearchMeleeStrikePatch
    {
        private static void Prefix(Agent attacker, Agent victim) =>
            GwpCitySearchMissionController.Current?.NoteStrike(attacker, victim);
    }
}
