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
    /// 进城搜捕复用原版居民、卫兵、对话与恐慌；搜捕控制器独立办理战斗，不挂小巷斗殴。
    /// 玩家穿战斗装备，带自选部队从城门进入。
    /// </summary>
    internal static class GwpCitySearchMission
    {
        /// <summary>原版 sp_player 预制体继承的实际标签，预制体名称本身不算标签。</summary>
        internal const string HallSpawnTag = "spawnpoint_player";

        /// <summary>原版「进城时遭遇战斗」用的城门外出生点：城镇场景全有，城堡约一半有（场景标签核对 2026-10-21）。</summary>
        internal const string GateSpawnTag = "sp_outside_near_town_main_gate";

        /// <summary>自有任务名：界面组见 <see cref="GwpCitySearchMissionViews"/>，带下令界面。</summary>
        internal const string MissionName = "GwpCitySearch";

        internal static void Open(Settlement settlement, Hero target, TroopRoster troops)
        {
            var search = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
            bool hall = search.SearchPlace == GwpCitySearchPlace.Hall;
            Location center = settlement.LocationComplex.GetLocationWithId(hall ? "lordshall" : "center");
            int wallLevel = settlement.Town.GetWallLevel();
            string scene = center.GetSceneName(wallLevel);
            string levels = Campaign.Current.Models.LocationModel.GetCivilianUpgradeLevelTag(wallLevel);
            GwpCityStreetSpawnPatch.Install();
#if GWP_DIAGNOSTICS
            GwpCitySearchFadeDiagnosticsPatch.EnsureApplied();
#endif
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
                new MissionCrimeHandler(),
                new MissionConversationLogic(),
                new MissionAgentHandler(),
                new MissionLocationLogic(center, center.StringId == "lordshall" ? HallSpawnTag
                    : Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>().SearchPlace != GwpCitySearchPlace.Entry
                        || Campaign.Current.GameMenuManager.PreviousLocation != null ? null : GateSpawnTag),
                new HeroSkillHandler(),
                new MissionFightHandler(),
                new MissionFacialAnimationHandler(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(),
                new VisualTrackerMissionBehavior(),
                new EquipmentControllerLeaveLogic()
            });
            if (center.StringId == "lordshall")
            {
                // 与原版 Indoor 同样不运行工坊、小巷、城镇入口准备；保留大厅人口与场景物品。
                list.RemoveAll(behavior => behavior is MissionSettlementPrepareLogic || behavior is WorkshopMissionHandler
                    || behavior is NotableSpawnPointHandler);
                list.Add(new LocationItemSpawnHandler());
                list.Add(new BattleSurgeonLogic());
            }
            return list.ToArray();
        }
    }

    /// <summary>
    /// 场景里的搜捕：玩家带兵从城门进来，卫兵上前盘问并说服；谈崩后玩家选打进去或撤，选撤、或一开始就说要走
    /// 却往城里去、或对守卫动手，都是玩家阵营对该势力宣战，卫兵和场景里同势力的人一起打（用户裁定 2026-10-21、2026-10-22）。
    /// 同一搜捕跨城镇中心、大厅与撤离场景推进，场景间的随行者和伤情由战役行为保存。
    /// </summary>
    internal sealed partial class GwpCitySearchMissionController : MissionLogic
    {
        private enum Stage { Entering, Questioned, Allowed, Leaving, Refused, Withdrawing, Fighting, FightOver, HallReady, HallTalking, Duel, Escaping, Failed, HallDone }

        internal static GwpCitySearchMissionController? Current { get; private set; }

        private readonly Settlement _settlement;
        private readonly Hero _target;
        private readonly TroopRoster _troops;
        private readonly Dictionary<Agent, CharacterObject> _ours = new Dictionary<Agent, CharacterObject>();
        private readonly Dictionary<CharacterObject, int> _killed = new Dictionary<CharacterObject, int>();
        private readonly List<Agent> _defenders = new List<Agent>();
        private Stage _stage = Stage.Entering;
        private Agent? _guard;
        private string? _pendingFight;
        private Vec3 _gate;
        private bool _gateExitAvailable;
        private bool _outsideExitRequested;
        private float _inwardLimit;
        private bool _warned;
        private bool _warDeclared;
        private float _nextChallengeCheck;

        internal GwpCitySearchMissionController(Settlement settlement, Hero target, TroopRoster troops)
        {
            _settlement = settlement;
            _target = target;
            _troops = troops;
            _place = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>().SearchPlace;
        }

        internal TextObject TargetName => _target.Name;
        internal Settlement Settlement => _settlement;

        internal bool IsGuardTalk => !InHall && _stage == Stage.Questioned && _guard != null
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
            if (InHall)
                SandBoxHelpers.MissionHelper.SpawnPlayer(civilianEquipment: false, noHorses: true,
                    spawnTag: GwpCitySearchMission.HallSpawnTag);
            else
                SandBoxHelpers.MissionHelper.SpawnPlayer(spawn, civilianEquipment: false, noHorses: true);
            _gate = spawn?.GetGlobalFrame().origin ?? Agent.Main?.Position ?? Vec3.Zero;
            _gateExitAvailable = !InHall && spawn != null;
            CampaignMission.Current.Location.RemoveAllCharacters(character => character.Character.HeroObject?.PartyBelongedTo == MobileParty.MainParty);
            PrepareStreetReturn();
            Mission.GetMissionBehavior<MissionAgentHandler>().SpawnLocationCharacters();
            RestoreStreetBodies();
            DisablePlayerPassages();
            SpawnTroops();
            BeginSearchStage();
            PostHallTroops();
            Mission.PlayerTeam.PlayerOrderController.OnOrderIssued += ReleaseHallPosts;
            ResumeDuelWalk();
        }


        /// <summary>玩家走门时带整队衔接大厅、街道和竞技场；其他人物沿用原版。</summary>
        private int DisablePlayerPassages()
        {
            int count = 0;
            foreach (UsableMachine machine in Mission.ActiveMissionObjects.FindAllWithType<UsableMachine>())
                foreach (StandingPoint point in machine.StandingPoints)
                    if (point is PassageUsePoint passage)
                    {
                        passage.SetIsDisabledForPlayersSynched(!CanUsePassage(passage));
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
                    RestoreTroopHealth(agent, character);
                    count++;
                }
            }
            TakeCommand(main);
            OrderInitialSearchFollowers();
            return count;
        }

        private void OrderInitialSearchFollowers()
        {
            if (!EscapingCity || !_gateExitAvailable)
            {
                Order(OrderType.FollowMe);
                return;
            }
            OrderController controller = Mission.PlayerTeam.PlayerOrderController;
            controller.SelectAllFormations();
            controller.SetOrderWithPosition(OrderType.Move, new WorldPosition(Mission.Scene, System.UIntPtr.Zero, _gate, true));
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
#if GWP_DIAGNOSTICS
            TickSearchVisuals();
#endif
            TickCityReserves();
            if (TickHallDuel()) return;
            if (TickSearchProgress()) return;
            if (_pendingFight != null && !Campaign.Current.ConversationManager.IsConversationFlowActive)
            {
                string cause = _pendingFight;
                _pendingFight = null;
                StartFight(cause);
            }
            if (Mission.CurrentTime >= _nextChallengeCheck)
            {
                _nextChallengeCheck = Mission.CurrentTime + 0.5f;
                if (_stage == Stage.Entering) TryChallenge();
                else if (_stage == Stage.Leaving || _stage == Stage.Withdrawing) CheckHeadingIn();
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
            Mission.GetMissionBehavior<MissionConversationLogic>().StartConversation(guard, false);
        }

        internal void OnGuardDecision(bool allowed, string result)
        {
            if (_stage != Stage.Questioned) return;
            _stage = allowed ? Stage.Allowed : Stage.Refused;
            Search.GateState = allowed ? GwpCitySearchGate.Allowed : GwpCitySearchGate.Refused;
            if (allowed)
            {
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_allowed_notice)));
                DisablePlayerPassages();
            }
        }

        /// <summary>一见卫兵就说要走：答应了出城，再往里走先警告一次，不听就打（用户裁定 2026-10-22）。</summary>
        internal void OnPromisedToLeave()
        {
            if (_stage != Stage.Questioned) return;
            BeginLeaving(Stage.Leaving, GwpTextCatalog.gwp_cs_leave_notice);
        }

        /// <summary>谈崩后选打进去：对话关上后开战（用户裁定 2026-10-22）。</summary>
        internal void OnChoseFight()
        {
            if (_stage != Stage.Refused) return;
            _pendingFight = "chose_fight";
        }

        /// <summary>谈崩后选撤：撤出城外搜捕作罢，再往里走就开战，不再警告（用户裁定 2026-10-22）。</summary>
        internal void OnChoseWithdraw()
        {
            if (_stage != Stage.Refused) return;
            BeginLeaving(Stage.Withdrawing, GwpTextCatalog.gwp_cs_withdraw_notice);
        }

        private void BeginLeaving(Stage stage, string noticeKey)
        {
            _stage = stage;
            Search.GateState = stage == Stage.Leaving ? GwpCitySearchGate.Leaving : GwpCitySearchGate.Withdrawing;
            Agent? main = Agent.Main;
            _inwardLimit = (main == null ? 0f : main.Position.Distance(_gate)) + GwpTuning.CitySearch.InwardTolerance;
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(noticeKey)));
        }

        /// <summary>
        /// 说了要走却往城里去：以进城处为准，比答应时再远出一段就算违抗。说要走的那次先警告并重新起算，
        /// 第二次、或谈崩后选了撤的，直接开战。
        /// </summary>
        private void CheckHeadingIn()
        {
            Agent? main = Agent.Main;
            if (main == null || Campaign.Current.ConversationManager.IsConversationFlowActive) return;
            float distance = main.Position.Distance(_gate);
            if (distance <= _inwardLimit) return;
            if (_stage == Stage.Leaving && !_warned)
            {
                _warned = true;
                _inwardLimit = distance + GwpTuning.CitySearch.InwardTolerance;
                MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_cs_leave_warning));
                return;
            }
            _pendingFight = _stage == Stage.Leaving ? "ignored_warning" : "broke_withdrawal";
        }

        /// <summary>
        /// 不放行之后（谈崩、说了要走、选了撤），我方的兵器碰到守卫就记下，下一帧开战；谈之前或已放行时不算动手
        /// （用户裁定 2026-10-21）。守卫开战前是中立的，原版对中立的人一律取消伤害、
        /// 不触发 OnAgentHit，所以只能在近战碰撞回调里认出这一下（C#反编译 v1.4.8 Mission.MeleeHitCallback
        /// 2026-10-21）。箭矢回调禁止补丁（troop-combat.md），射守卫不开战。不在原生回调里换队伍、宣战，留到任务帧里做。
        /// </summary>
        internal void NoteStrike(Agent? attacker, Agent? victim)
        {
            if (_stage != Stage.Refused && _stage != Stage.Leaving && _stage != Stage.Withdrawing) return;
            if (_pendingFight != null || attacker == null || victim == null) return;
            if (IsOurs(attacker) && IsDefender(victim)) _pendingFight = "struck";
        }

        /// <summary>开战：军事守方抵抗，普通居民即便持械也逃散。</summary>
        private void StartFight(string cause)
        {
            // 宣战可能让原版立即移走驻城队，先冻结当时确实驻城的来源。
            MobileParty[] defenseParties = CityDefenseParties();
            Search.MarkSearchWar();
            if (!_warDeclared)
            {
                _warDeclared = true;
                Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>()?.DeclarePlayerCaseWar(
                    _settlement.MapFaction, "city_search_" + cause, GwpTextCatalog.gwp_cs_war_notice);
            }
            _defenders.Clear();
            _defenders.AddRange(Mission.Agents.Where(agent => agent.IsActive() && IsDefender(agent)));
            _stage = Stage.Fighting;

            // 不用原版 MissionFightHandler：它打完把参战者放进新建的 Team 对象，
            // 我方会脱离玩家队伍、无法再指挥（Team 无相等重写，C#反编译 v1.4.8 2026-10-21）。
            Mission.PlayerEnemyTeam.SetIsEnemyOf(Mission.PlayerTeam, true);
            // 原版城内打架会唤醒双方。Charge只改编队命令，不会把未警戒的随行兵切进战斗AI。
            foreach (Agent ally in _ours.Keys.Where(agent => agent.IsActive()))
                SetSearchFighter(ally);
            foreach (Agent defender in _defenders)
            {
                defender.SetTeam(Mission.PlayerEnemyTeam, true);
                SetCityPursuer(defender);
            }
            PrepareCityReserves(defenseParties);
            foreach (Agent civilian in Mission.Agents.Where(agent => agent.IsActive() && agent.IsAIControlled && ShouldFlee(agent)))
            {
                civilian.SetTeam(Team.Invalid, true);
                civilian.SetWatchState(Agent.WatchState.Alarmed);
                AlarmedBehaviorGroup? alarmed = civilian.GetComponent<CampaignAgentComponent>()?.AgentNavigator
                    ?.GetBehaviorGroup<AlarmedBehaviorGroup>();
                if (alarmed == null) continue;
                alarmed.DisableCalmDown = true;
                alarmed.AddBehavior<FleeBehavior>();
                alarmed.SetScriptedBehavior<FleeBehavior>();
            }
            Mission.SetMissionMode(MissionMode.Battle, false);
            Mission.GetMissionBehavior<MissionConversationLogic>().DisableStartConversation(true);
            if (!EscapingCity) Order(OrderType.Charge);
        }

        private static void SetSearchFighter(Agent agent)
        {
            if (!agent.IsAIControlled) return;
            agent.DisableScriptedMovement();
            agent.SetWatchState(Agent.WatchState.Alarmed);
            // SpawnTroop生成的随行兵没有城市场景导航器；有导航器的人物同原版强制进入Fight。
            AgentNavigator? navigator = agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator;
            AlarmedBehaviorGroup? alarmed = navigator?.GetBehaviorGroup<AlarmedBehaviorGroup>();
            if (alarmed == null) return;
            alarmed.DisableCalmDown = true;
            alarmed.AddBehavior<FightBehavior>();
            alarmed.SetScriptedBehavior<FightBehavior>();
            // 原版自动切组最多等1秒；先退出巡逻、释放物件，再交给战斗编队，避免未登记detachment查表。
            navigator!.RefreshBehaviorGroups(false);
        }

        private void RestoreSearchFollowers()
        {
            foreach (Agent ally in Mission.Agents.Where(agent => IsOurs(agent) && agent.IsActive() && agent.IsAIControlled))
            {
                AlarmedBehaviorGroup? alarmed = ally.GetComponent<CampaignAgentComponent>()?.AgentNavigator
                    ?.GetBehaviorGroup<AlarmedBehaviorGroup>();
                if (alarmed != null)
                {
                    alarmed.DisableScriptedBehavior();
                    alarmed.DisableCalmDown = false;
                }
                ally.SetWatchState(Agent.WatchState.Patrolling);
            }
        }

        private void EndFightIfDefendersDown()
        {
            if (_stage != Stage.Fighting || _defenders.Any(agent => agent.IsActive()) || HasCityDefenders()) return;
            _stage = Stage.FightOver;
            Mission.SetMissionMode(MissionMode.StartUp, false);
            RestoreSearchFollowers();
            if (!EscapingCity) Order(OrderType.FollowMe);
            OnSearchFightWon();
        }

        public override InquiryData OnEndMissionRequest(out bool canPlayerLeave)
        {
            canPlayerLeave = CanLeaveSearch();
            // 原版还会遍历其余离场逻辑并等待0.6秒；请求通过不代表已经卸载场景。
            _outsideExitRequested = canPlayerLeave && (Search.SearchWar || AtGateExit);
            if (!canPlayerLeave && !Search.HallDuelLocked)
                MBInformationManager.AddQuickInformation(GwpText.Create(Search.SearchWar
                    ? GwpTextCatalog.gwp_cs_hostile_exit_blocked : GwpTextCatalog.gwp_cs_route_blocked));
            return null!;
        }


        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            NoteStreetDefeat(affectedAgent, agentState);
#if GWP_DIAGNOSTICS
            WriteSearchVisualRemoval(affectedAgent, agentState.ToString());
#endif
            NoteCitySoldierRemoval(affectedAgent, agentState);
            NoteHallDuelRemoval(affectedAgent, agentState);
            NoteDuelRemoval(affectedAgent);
            if (affectedAgent == Agent.Main && agentState != AgentState.Deleted)
            {
                _pendingHallAction = null;
                _pendingFight = null;
                _transitioning = false;
                _stage = Stage.Failed;
                Search.FinishSearch(false);
                return; // 原版 LeaveMissionLogic 办理倒地后退出，不能把失败带入下一场景。
            }
            if (InHall && affectedAgent == _hallTarget && Search.SearchActive && _stage == Stage.Fighting
                && (agentState == AgentState.Killed || agentState == AgentState.Unconscious))
            {
                Search.MarkTargetDown(agentState == AgentState.Killed);
            }
            EndFightIfDefendersDown();
            if (!_ours.TryGetValue(affectedAgent, out CharacterObject character)) return;
            if (agentState == AgentState.Killed)
                _killed[character] = _killed.TryGetValue(character, out int n) ? n + 1 : 1;
        }

        protected override void OnEndMission()
        {
            // 原版 EndMissionInternal 在本回调之后清空 Teams，晚到 OnRemoveBehavior 已没有 PlayerTeam。
            Mission.PlayerTeam.PlayerOrderController.OnOrderIssued -= ReleaseHallPosts;
            EndSearchScene();
        }

        private bool IsOurs(Agent agent) => agent == Agent.Main || _ours.ContainsKey(agent)
            || agent.Team == Mission.PlayerTeam && agent.Origin is GwpBattleSupportOrigin;

        /// <summary>守卫：原版算作执法者的士兵、卫兵、狱卒，不含我方带进来的兵。</summary>
        private bool IsWatch(Agent agent) => agent.IsHuman && !IsOurs(agent)
            && !_restoredStreetBodies.Contains(agent)
            && agent.Character is CharacterObject character && MissionFightHandler.IsAgentJusticeWarrior(character);

        /// <summary>用户裁定2026-10-28：只由卫兵/巡逻和真实城防兵员抵抗，目标始终留场。</summary>
        private bool IsDefender(Agent agent) => agent.IsHuman && !IsOurs(agent)
            && !_restoredStreetBodies.Contains(agent)
            && (agent == _hallTarget || agent.HasWeapon()
                && agent.Character is not CharacterObject { HeroObject: { IsNotable: true } }
                && (IsWatch(agent) || _citySoldiers.ContainsKey(agent)));

        /// <summary>非我方且不参战的人逃散，不再以职业把无武器打手或士兵排除在外。</summary>
        private bool ShouldFlee(Agent agent) => agent.IsHuman && !IsOurs(agent) && !_restoredStreetBodies.Contains(agent) && !IsDefender(agent);
    }

    /// <summary>近战碰到人：交给进城搜捕认是不是对守卫动手。参数名须与原版回调一致，Harmony 按名绑定。</summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class GwpCitySearchMeleeStrikePatch
    {
        private static void Prefix(Agent attacker, Agent victim) =>
            GwpCitySearchMissionController.Current?.NoteStrike(attacker, victim);
    }
}
