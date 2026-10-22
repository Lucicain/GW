using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>原版拖尸控制器的搬运者适配；物理、松手及拖拽动作仍由原版执行。</summary>
    internal sealed class GwpCitySearchBodyTransport : CorpseDraggingMissionLogic, IPlayerInputEffector
    {
        private static readonly Action<CorpseDraggingMissionLogic, Agent, sbyte> SetBody =
            AccessTools.MethodDelegate<Action<CorpseDraggingMissionLogic, Agent, sbyte>>(
                AccessTools.Method(typeof(CorpseDraggingMissionLogic), "SetDraggedCorpse"));
        private static readonly AccessTools.FieldRef<CorpseDraggingMissionLogic, Agent> DraggedBody =
            AccessTools.FieldRefAccess<CorpseDraggingMissionLogic, Agent>("_draggedCorpse");
        private Agent? _body;
        internal Agent? Carrier { get; private set; }
        private bool _closed;
        private float _nextMove;
        private bool _bodyLost;
#if GWP_DIAGNOSTICS
        private bool _reportedAttach;
        private bool _attachTried;
        private float _carrierSampleAt = -1f;
#endif
        private GwpCitySearchBehavior Search => Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();

#if GWP_DIAGNOSTICS
        private float _takenAt = -1f;

        /// <summary>
        /// 取证 2026-10-22：大厅击昏目标 0.35 秒后接身体，原版 SetDraggedCorpse 本帧空引用。
        /// 该方法内能为空的只有尸体的装备、外观、外观实体和换成搬运兵的 MainAgent，逐一记下。
        /// </summary>
        private void WriteAttachCheck()
        {
            _attachTried = true;
            MBAgentVisuals? visuals = _body!.AgentVisuals;
            GwpAiDiagnostics.WriteCitySearch("BODY_ATTACH_TRY", "body=" + _body.State
                + "; sinceDown=" + (_takenAt < 0f ? "spawned" : (Mission.CurrentTime - _takenAt).ToString("0.00"))
                + "; equipment=" + (_body.Equipment != null) + "; visuals=" + (visuals != null)
                + "; entity=" + (visuals?.GetEntity() != null) + "; corpse=" + _body.IsAddedAsCorpse()
                + "; primary=" + _body.GetPrimaryWieldedItemIndex() + "; offhand=" + _body.GetOffhandWieldedItemIndex()
                + "; carrierActive=" + Carrier?.IsActive());
        }
#endif

        internal void TakeTarget(Agent target)
        {
            _body = target;
#if GWP_DIAGNOSTICS
            _takenAt = Mission.CurrentTime;
#endif
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_transport_target)));
        }

        internal bool IsTargetNear(Vec3 position) => _body != null && _body.State != AgentState.Deleted
            && _body.Position.Distance(position) <= GwpTuning.CitySearch.GateExitDistance;

        // 本行为不消费玩家按键；原版为搬运者生成的蹲下/收刀输入由下方转换为NPC动作。
        Agent.EventControlFlag IPlayerInputEffector.OnCollectPlayerEventControlFlags() => Agent.EventControlFlag.None;
        public override bool IsThereAgentAction(Agent userAgent, Agent otherAgent) => false;

        public override void OnMissionTick(float dt)
        {
            if (_closed || !Search.SearchActive || !Search.BodyPending || Agent.Main?.IsActive() != true) return;
            if (_body == null) SpawnBodyAtEntrance();
            if (_body == null || _body.State == AgentState.Deleted)
            {
                if (!_bodyLost)
                {
                    _bodyLost = true;
#if GWP_DIAGNOSTICS
                    GwpAiDiagnostics.WriteCitySearch("BODY_LOST", "state=" + _body?.State);
#endif
                    Search.FinishSearch(false);
                    InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_transport_failed)));
                }
                return;
            }
            if (Carrier != null && !Carrier.IsActive()) ReleaseCarrier();
            if (Carrier == null)
            {
                Carrier = Mission.Agents.Where(agent => agent.IsActive() && agent.IsAIControlled && agent.IsHuman
                        && agent.Team == Mission.PlayerTeam && agent.Origin is not GwpBattleSupportOrigin)
                    .OrderBy(agent => agent.Position.Distance(_body.Position)).FirstOrDefault();
                if (Carrier == null)
                {
                    Search.FinishSearch(false);
                    InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(GwpTextCatalog.gwp_cs_transport_failed)));
                    return;
                }
#if GWP_DIAGNOSTICS
                _reportedAttach = false;
                _attachTried = false;
                _carrierSampleAt = Mission.CurrentTime + 10f;
                GwpAiDiagnostics.WriteCitySearch("BODY_CARRIER", "troop=" + Carrier.Character.StringId);
#endif
            }
#if GWP_DIAGNOSTICS
            if (_carrierSampleAt > 0f && Mission.CurrentTime >= _carrierSampleAt)
            {
                _carrierSampleAt = -1f;
                GwpAiDiagnostics.WriteCitySearch("BODY_PROGRESS", "carrier=" + Carrier.Position
                    + "; body=" + _body.Position + "; player=" + Agent.Main.Position
                    + "; attached=" + (DraggedBody(this) != null) + "; upperAction=" + Carrier.GetCurrentAction(1));
            }
#endif
            if (DraggedBody(this) == null)
            {
                if (Carrier.Position.Distance(_body.Position) > GwpTuning.CitySearch.SecureTargetDistance)
                {
                    MoveCarrier(_body.GetWorldPosition(), false);
                    return;
                }
                MoveCarrier(Carrier.GetWorldPosition(), false);
                Carrier.SetWieldedItemIndexAsClient(Agent.HandIndex.MainHand, EquipmentIndex.None, true, false, 0);
                Carrier.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, EquipmentIndex.None, true, false, 0);
                // 原版拖尸遇到上身攻击/收刀动作会松手，先等本次动作结束再接上物理。
                if (Carrier.GetCurrentAction(1) != ActionIndexCache.act_none) return;
#if GWP_DIAGNOSTICS
                if (!_attachTried) WriteAttachCheck();
#endif
                SetBody(this, _body, 0);
                base.OnCollectPlayerEventControlFlags();
                Carrier.SetCrouchMode(true);
#if GWP_DIAGNOSTICS
                if (!_reportedAttach)
                {
                    _reportedAttach = true;
                    GwpAiDiagnostics.WriteCitySearch("BODY_ATTACHED", "carrier=" + Carrier.Character.StringId);
                }
#endif
            }
            base.OnMissionTick(dt);
            if (DraggedBody(this) == null) return;
            if (Mission.CurrentTime >= _nextMove)
            {
                _nextMove = Mission.CurrentTime + .25f;
                WorldPosition destination = Carrier.Position.Distance(Agent.Main.Position) > GwpTuning.CitySearch.SecureTargetDistance
                    ? Agent.Main.GetWorldPosition() : Carrier.GetWorldPosition();
                MoveCarrier(destination, true);
            }
        }

        private void MoveCarrier(WorldPosition destination, bool dragging)
        {
            Agent.AIScriptedFrameFlags flags = Agent.AIScriptedFrameFlags.NoAttack;
            if (dragging) flags |= Agent.AIScriptedFrameFlags.Crouch | Agent.AIScriptedFrameFlags.Drag | Agent.AIScriptedFrameFlags.DoNotRun;
            Carrier!.SetScriptedPosition(ref destination, false, flags);
        }

        public override void OnFixedMissionTick(float fixedDt)
        {
            if (!_closed && Carrier?.IsActive() == true && _body?.State != AgentState.Deleted)
                base.OnFixedMissionTick(fixedDt);
        }

        private void SpawnBodyAtEntrance()
        {
            // 这里只重建同一具倒地人物的场景表现；无伤亡回调的Origin不再击杀/击昏英雄或扣名册。
            Hero target = Search.SearchTarget;
            Vec3 position = Mission.GetRandomPositionAroundPoint(Agent.Main.Position, 1f, 2f);
            var origin = new GreyWardenSafePartyAgentOrigin(PartyBase.MainParty, target.CharacterObject, false, 0);
            _body = Mission.SpawnAgent(new AgentBuildData(target.CharacterObject).TroopOrigin(origin)
                .Team(Team.Invalid).NoHorses(true).CivilianEquipment(true)
                .Banner(target.Clan?.Banner ?? target.MapFaction.Banner)
                .ClothingColor1(target.MapFaction.Color).ClothingColor2(target.MapFaction.Color2)
                .InitialPosition(position).InitialDirection(Agent.Main.LookDirection.AsVec2));
            _body.MakeDead(Search.BodyKilled, ActionIndexCache.act_none);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState state, KillingBlow blow)
        {
            if (affectedAgent == Carrier) ReleaseCarrier();
        }

        private void ReleaseCarrier()
        {
            if (Carrier == null) return;
            if (DraggedBody(this) != null) SetBody(this, null!, -1);
            base.OnCollectPlayerEventControlFlags();
            if (Carrier.IsActive())
            {
                Carrier.DisableScriptedMovement();
                Carrier.SetCrouchMode(false);
                Carrier.SetDraggingMode(false);
                Carrier.WieldInitialWeapons();
            }
            Carrier = null;
        }

        protected override void OnEndMission()
        {
            ReleaseCarrier();
            _closed = true;
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            ReleaseCarrier();
            _closed = true;
            base.OnRemoveBehavior();
        }
    }

    /// <summary>
    /// 只改原版拖尸方法中取得搬运者的一处依赖；其他任务仍返回原玩家。
    /// 不在模组加载时打：被改写的 OnMissionTick 读 ActionIndexCache，打补丁会当场运行它的静态初始化，
    /// 而引擎在模组初始化之后才读 action_types.xml，于是整局所有原版动作编号都成 -1，竞技场看台观众按 -1
    /// 预载动画越界读、原生崩溃（实机转储与引擎日志 2026-10-22；离线 Harmony 实验同结果）。首次进搜捕场景时再打。
    /// </summary>
    internal static class GwpCitySearchBodyCarrierPatch
    {
        private static bool _applied;

        internal static void EnsureApplied()
        {
            if (_applied) return;
            _applied = true;
            var harmony = new Harmony(SubModule.HarmonyId);
            var transpiler = new HarmonyMethod(typeof(GwpCitySearchBodyCarrierPatch), nameof(Transpiler));
            foreach (MethodBase target in Targets()) harmony.Patch(target, transpiler: transpiler);
        }

        private static IEnumerable<MethodBase> Targets() => new[]
        {
            AccessTools.Method(typeof(CorpseDraggingMissionLogic), "SetDraggedCorpse"),
            AccessTools.Method(typeof(CorpseDraggingMissionLogic), nameof(CorpseDraggingMissionLogic.OnMissionTick)),
            AccessTools.Method(typeof(CorpseDraggingMissionLogic), nameof(CorpseDraggingMissionLogic.OnFixedMissionTick)),
            AccessTools.Method(typeof(CorpseDraggingMissionLogic), nameof(CorpseDraggingMissionLogic.OnCollectPlayerEventControlFlags))
        };

        private static Agent GetCarrier(Mission mission, CorpseDraggingMissionLogic behavior) =>
            behavior is GwpCitySearchBodyTransport transport ? transport.Carrier! : mission.MainAgent;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo mainAgent = AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.MainAgent));
            MethodInfo carrier = AccessTools.Method(typeof(GwpCitySearchBodyCarrierPatch), nameof(GetCarrier));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(mainAgent))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return new CodeInstruction(OpCodes.Call, carrier);
                }
                else yield return instruction;
            }
        }
    }
}
