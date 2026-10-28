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
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
        private bool EscapingCity => !InHall && (Search.HasLeftHall || _place == GwpCitySearchPlace.Escape);
        private bool _transitioning;
        private string? _pendingHallAction;
        private Agent? _hallTarget;
        private readonly Dictionary<string, int> _spawnedTroops = new Dictionary<string, int>();
        /// <summary>
        /// 大厅谈崩后对方先选打法（用户裁定 2026-10-26）：比较群殴与自己单挑玩家的赢面，再看性格。
        /// 实力用原版比武模拟胜负所用的 GetSimulationAttackPower，按装备与对应技能算攻防分；原版模拟里双方同时互攻，
        /// 每回合从对方防御分中扣去己方攻击分，先被扣完的一方输（SandBox v1.4.8 TournamentFightMissionController 反编译）。
        /// 赢面取「对方攻×防 / 我方攻×防」的对数：群殴按双方在场参战者当场装备，单挑按目标与玩家各自的战斗装备。
        /// 勇武、荣誉高的偏向单挑，低的偏向躲在卫兵后面；偏向大小为设计值。
        /// </summary>
        internal bool OpponentPrefersDuel()
        {
            (float ours, float theirs) = CombatBalance();
            float targetAttack = 0f, targetDefence = 0f, playerAttack = 0f, playerDefence = 0f;
            SimulationPower(_target.CharacterObject, _target.CharacterObject.FirstBattleEquipment, ref targetAttack, ref targetDefence);
            SimulationPower(CharacterObject.PlayerCharacter, CharacterObject.PlayerCharacter.FirstBattleEquipment, ref playerAttack, ref playerDefence);
            float melee = 2f * (float)System.Math.Log(theirs / ours);
            float duel = (float)System.Math.Log(targetAttack * targetDefence / (playerAttack * playerDefence));
            float lean = GwpTuning.CitySearch.DuelTraitLean
                * (_target.GetTraitLevel(DefaultTraits.Valor) + _target.GetTraitLevel(DefaultTraits.Honor));
            bool prefersDuel = duel + lean > melee;
            return prefersDuel;
        }

        /// <summary>搜捕现场的参战实力。说服与打法选择共用，只数活跃随行者及实际持械守方，不数平民和未入场预备兵。</summary>
        internal (float ours, float theirs) CombatBalance()
        {
            float ourAttack = 0f, ourDefence = 0f, theirAttack = 0f, theirDefence = 0f;
            foreach (Agent agent in Mission.Agents.Where(agent => agent.IsActive()))
            {
                if (agent.Character is not CharacterObject character) continue;
                if (IsOurs(agent)) SimulationPower(character, agent.SpawnEquipment, ref ourAttack, ref ourDefence);
                else if (IsDefender(agent)) SimulationPower(character, agent.SpawnEquipment, ref theirAttack, ref theirDefence);
            }
            return ((float)System.Math.Sqrt(ourAttack * ourDefence), (float)System.Math.Sqrt(theirAttack * theirDefence));
        }

        private static void SimulationPower(CharacterObject character, Equipment equipment, ref float attack, ref float defence)
        {
            character.GetSimulationAttackPower(out float a, out float d, equipment);
            // 依据：安装版1.4.8裸装DLL负对照，护甲分/重量为0/0，污染整队与说服概率。
            // 零重量时护甲贡献为零；保留原版基础防御、盾牌及坐骑贡献。正常装备仍直接用原版结果。
            if (float.IsNaN(d) && equipment.GetTotalWeightOfArmor(true) == 0f)
                d = UnarmoredDefence(character, equipment);
            attack += a;
            defence += d;
        }

        private static float UnarmoredDefence(CharacterObject character, Equipment equipment)
        {
            // 原版CharacterObject.GetSimulationAttackPower中与护甲无关的防御项（v1.4.8）。
            float defence = 4000f;
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                EquipmentElement element = equipment[slot];
                if (element.IsEmpty || !element.Item.PrimaryWeapon.IsShield) continue;
                float skill = element.Item.RelevantSkill == null ? 1f
                    : 0.3f + character.GetSkillValue(element.Item.RelevantSkill) / 300f * 0.7f;
                defence += skill * element.Item.Effectiveness * 10f;
            }
            float mount = 0f;
            for (EquipmentIndex slot = EquipmentIndex.ArmorItemEndSlot; slot <= EquipmentIndex.HorseHarness; slot++)
                if (!equipment[slot].IsEmpty) mount += equipment[slot].Item.Effectiveness;
            float riding = equipment.Horse.Item?.RelevantSkill == null ? 1f
                : 0.3f + character.GetSkillValue(equipment.Horse.Item.RelevantSkill) / 300f * 0.7f;
            return defence + mount * riding * 5f;
        }

        internal bool IsHallTalk =>InHall && _stage == Stage.HallTalking && _hallTarget != null
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
                if (Search.HallResolved)
                {
                    _stage = Stage.HallDone;
                    DisablePlayerPassages();
                    return;
                }
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
            else if (Search.HallDuelLocked)
            {
                _stage = Stage.HallDone;
            }
            else if (_place == GwpCitySearchPlace.Escape)
            {
                _stage = Stage.Escaping;
                if (_warDeclared && !Search.EscapeCleared) _pendingFight = "escape";
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_escape_notice)));
            }
            else
            {
                _stage = Search.GateState switch
                {
                    GwpCitySearchGate.Allowed => Stage.Allowed,
                    GwpCitySearchGate.Cleared => Stage.FightOver,
                    GwpCitySearchGate.Leaving => Stage.Leaving,
                    GwpCitySearchGate.Refused => Stage.Refused,
                    GwpCitySearchGate.Withdrawing => Stage.Withdrawing,
                    _ => Stage.Entering
                };
                _inwardLimit = (Agent.Main?.Position.Distance(_gate) ?? 0f) + GwpTuning.CitySearch.InwardTolerance;
                if (_warDeclared) _pendingFight = "street";
            }
            DisablePlayerPassages();
        }

        private bool CanUsePassage(PassageUsePoint passage)
        {
            // 放行回调发生在对话中；门的许可不能因对话尚未关闭而永久关掉。
            if (_stage == Stage.Duel || _stage == Stage.Failed || _pendingFight != null || _pendingHallAction != null) return false;
            if (Search.HallDuelLocked) return false;
            if (_stage == Stage.Fighting && !Search.SearchWar) return false;
            if (InHall) return passage.ToLocationId == "center";
            if (passage.ToLocationId == "lordshall" && Search.SearchWar && _stage != Stage.FightOver) return false;
            if (passage.ToLocationId == "arena") return Search.DuelAgreed && !Search.UsesHallDuel;
            return passage.ToLocationId == "lordshall" && (Search.HallAccessible || Search.SearchWar) && !Search.HallResolved;
        }

        internal bool TryUseSearchPassage(PassageUsePoint passage, Agent user)
        {
            if (TargetMustStay(user)) return true;
            if (user != Agent.Main) return false;
            if (CanUsePassage(passage))
                _pendingHallAction = passage.ToLocationId == "lordshall" ? "enter_hall"
                    : passage.ToLocationId == "arena" ? "enter_arena" : "enter_street";
            return true; // 走门也只切地点，不改案件进度，整队随行。
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
                switch (action)
                {
                    case "enter_hall": TransitionTo(GwpCitySearchPlace.Hall); return true;
                    case "enter_street":
                        TransitionTo(Search.Returning ? GwpCitySearchPlace.Escape : GwpCitySearchPlace.Street);
                        return true;
                    case "enter_arena": TransitionTo(GwpCitySearchPlace.Arena); return true;
                    case "leave_hall":
                        Search.WithdrawFromHall();
                        _stage = Stage.HallDone;
                        DisablePlayerPassages();
                        break;
                    case "missing": Search.FinishSearch(false); Mission.EndMission(); return true;
                    case "surrender":
                        Search.PeacefulCapture();
                        _stage = Stage.HallDone;
                        DisablePlayerPassages();
                        InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_walk_out_notice)));
                        break;
                    case "duel":
                        Search.AgreeDuel();
                        _stage = Stage.HallDone;
                        if (Search.UsesHallDuel) BeginHallDuel();
                        else BeginDuelWalk("center");
                        DisablePlayerPassages();
                        if (!Search.UsesHallDuel)
                            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_walk_arena_notice)));
                        break;
                    case "hall_duel_start": StartHallDuel(); break;
                    case "duel_won":
                        FinishHallDuel();
                        Search.WonHall();
                        _stage = Stage.HallDone;
                        Mission.SetMissionMode(MissionMode.StartUp, false);
                        Order(OrderType.FollowMe);
                        DisablePlayerPassages();
                        break;
                    case "duel_lost":
                        FinishHallDuel();
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
            return false;
        }

        /// <summary>
        /// 押人出城、谈崩后撤离或说了要走，走到已核实的城外出生点附近才算到了城外；守军仍在战斗也可冲出城门。
        /// 到了也不自动离场，仍由原版按住 Tab 的 0.6 秒进度条办理（用户裁定 2026-10-26；Mission.OnEndMissionRequest 反编译）。
        /// </summary>
        private bool AtGateExit => (_place == GwpCitySearchPlace.Escape || _stage == Stage.Leaving || _stage == Stage.Withdrawing)
            && _gateExitAvailable && _stage != Stage.Failed && _pendingFight == null && Agent.Main?.IsActive() == true
            && Agent.Main.Position.Distance(_gate) <= GwpTuning.CitySearch.GateExitDistance;

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
                if (_hallTarget?.State == AgentState.Routed || _hallTarget?.State == AgentState.Deleted)
                {
                    Search.WithdrawFromHall();
                    _stage = Stage.HallDone;
                    DisablePlayerPassages();
                    return;
                }
                Search.WonHall();
                _stage = Stage.HallDone;
                DisablePlayerPassages();
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_walk_out_notice)));
            }
            else
            {
                if (_place == GwpCitySearchPlace.Entry) Search.GateState = GwpCitySearchGate.Cleared;
                else if (_place == GwpCitySearchPlace.Escape) Search.EscapeCleared = true;
                DisablePlayerPassages();
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(_place == GwpCitySearchPlace.Entry
                    ? GwpTextCatalog.gwp_cs_hall_notice : GwpTextCatalog.gwp_cs_escape_notice)));
            }
        }

        private void NoteDuelRemoval(Agent agent)
        {
            if (_stage != Stage.Duel) return;
            if (agent == Agent.Main) _pendingHallAction = "duel_lost";
            else if (agent == _hallTarget) _pendingHallAction = "duel_won";
        }

        private bool CanLeaveSearch() => !Search.HallDuelLocked && _stage != Stage.Duel
            && _stage != Stage.Failed && _pendingFight == null && _pendingHallAction == null
            && !Campaign.Current.ConversationManager.IsConversationFlowActive
            && (Search.SearchWar ? !InHall && _gateExitAvailable && Agent.Main?.IsActive() == true
                && Agent.Main.Position.Distance(_gate) <= GwpTuning.CitySearch.GateExitDistance : _stage != Stage.Fighting);

        private void EndSearchScene()
        {
            FinishHallDuel();
            if (!_transitioning && _outsideExitRequested && Search.SearchActive && CanLeaveSearch())
                Search.FinishSearch(true);
            var bounty = Campaign.Current.GetCampaignBehavior<PlayerBountyBehavior>();
            bounty.RecordCitySearchLosses(_target, _killed.Values.Sum());
            if (Search.SearchActive)
            {
                SaveStreetScene();
                SaveCitySoldiers();
                Search.SaveSurvivors(SearchSurvivors());
                if (!_transitioning) Search.PauseSearch();
            }
        }

        internal IEnumerable<Agent>? CivilianDanger(Agent civilian) => _stage == Stage.Fighting && ShouldFlee(civilian)
            ? Mission.Agents.Where(agent => agent.IsActive() && IsOurs(agent)).ToArray() : null;

        /// <summary>目标不走逃跑出口；约好单挑后允许走出大厅换装，或沿原版路线赴竞技场。</summary>
        internal bool TargetMustStay(Agent? agent) => agent != null && agent == _hallTarget && Search.SearchActive && !_transitioning
            && (InHall ? !(Search.DuelAgreed && !Search.SearchWar)
                : Search.SearchWar || !Search.DuelAgreed || Search.UsesHallDuel);

        // 单挑按竞技场原版只击昏，群殴让原版伤亡模型裁定；不改武器标记。
        public AgentState GetAgentState(Agent affectedAgent, float deathProbability, out bool usedSurgery)
        {
            usedSurgery = false;
            return _stage == Stage.Duel ? AgentState.Unconscious : AgentState.None;
        }
    }

    [HarmonyPatch]
    internal static class GwpCitySearchTargetRetreatPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CommonAIComponent), nameof(CommonAIComponent.Panic));
            yield return AccessTools.Method(typeof(CommonAIComponent), nameof(CommonAIComponent.Retreat));
        }

        private static bool Prefix(Agent ___Agent) =>
            GwpCitySearchMissionController.Current?.TargetMustStay(___Agent) != true;
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
