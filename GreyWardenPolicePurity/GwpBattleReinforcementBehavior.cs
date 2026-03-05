﻿using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 战场即时增援行为（MissionBehavior）。
    ///
    /// 触发方式（事件驱动，零轮询）：
    ///   本场战斗共有两次增援判定机会：
    ///     第一次：己方存活人数降至 ≤ 20 时触发判定。
    ///       - 判定失败：提示"你在心中祈祷神明的眷顾"，等待第二次机会。
    ///       - 判定成功：立即召唤增援，本场不再有第二次机会。
    ///     第二次：仅剩玩家一人存活时触发判定。
    ///       - 判定失败：静默（不显示提示）。
    ///       - 判定成功：立即召唤增援。
    ///   两次机会用完后不再响应后续阵亡事件。
    ///
    /// 概率：声望20=10%，声望100=50%（Reputation / 200，上限 50%）。
    /// 声望门槛：声望 &lt; 20 时不触发任何判定。
    ///
    /// 增援构成：4成步兵、4成弓手、2成骑兵，共 20+声望 人，分批从边缘涌入。
    /// </summary>
    public class GwpBattleReinforcementBehavior : MissionBehavior
    {
        // ── 配置 ─────────────────────────────────────────────────────────────────
        private const int   ReputationMinimum = 20;   // 最低声望门槛
        private const int   AliveThreshold    = 20;   // 第一次判定：存活人数阈值（固定）
        private const int   BatchSize         = 10;   // 每批生成人数
        private const float BatchInterval     = 0.6f; // 批次间隔（秒）
        private const float HornDuration      = 4.5f; // 每次号角播放时长（秒）
        private const int   HornPlayCount     = 2;    // 号角播放次数
        // 总增援人数：20 + 声望（声望20→40人，声望100→120人）

        // ── 状态 ─────────────────────────────────────────────────────────────────
        private bool  _firstCheckDone  = false; // 第一次判定（≤20人）是否已完成
        private bool  _allChecksDone   = false; // 两次机会均已用完，不再响应阵亡事件
        private bool  _isFieldBattle   = false;
        private float _elapsedSeconds  = 0f;    // 仅用于号角续播计时
        private float _batchTimer      = 0f;
        private int   _batchesDone     = 0;
        private int   _totalTroopCount = 0;
        private bool  _isSpawning      = false;
        private SoundEvent? _hornSound  = null!;
        private float       _hornStopAt = -1f;
        private int         _hornPlayed = 0;

        // ── 兵种（由 SubModule 注入，在 Campaign 层解析）─────────────────────────
        private readonly CharacterObject _infantry;
        private readonly CharacterObject _archer;
        private readonly CharacterObject _cavalry;

        public GwpBattleReinforcementBehavior(
            CharacterObject infantry,
            CharacterObject archer,
            CharacterObject cavalry)
        {
            _infantry = infantry;
            _archer   = archer;
            _cavalry  = cavalry;
        }

        // ── MissionBehavior 接口 ─────────────────────────────────────────────────

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
        }

        public override void AfterStart()
        {
            _isFieldBattle = Mission?.IsFieldBattle ?? false;
        }

        /// <summary>
        /// Tick 仅处理号角续播与分批生成，不做任何触发条件检查。
        /// </summary>
        public override void OnMissionTick(float dt)
        {
            if (!_isFieldBattle) return;

            _elapsedSeconds += dt;

            // 号角到达预定时长后停止；未达到播放次数则续播
            if (_hornSound != null && _hornStopAt > 0f && _elapsedSeconds >= _hornStopAt)
            {
                _hornSound.Stop();
                _hornSound.Release();
                _hornSound  = null!;
                _hornStopAt = -1f;

                if (_hornPlayed < HornPlayCount)
                    StartHorn();
            }

            // 分批生成
            if (_isSpawning)
            {
                _batchTimer += dt;
                int totalBatches = Math.Max(_totalTroopCount / BatchSize, 1);

                if (_batchesDone < totalBatches && _batchTimer >= BatchInterval * _batchesDone)
                {
                    Agent firstAgent = SpawnBatch();

                    if (_batchesDone == 0 && firstAgent != null)
                        PlayArrivalHorn(firstAgent);

                    _batchesDone++;

                    if (_batchesDone >= totalBatches)
                        _isSpawning = false;
                }
            }
        }

        // ── 事件驱动触发 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 每当己方成员阵亡时自动调用，依次处理两次判定机会。
        /// </summary>
        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow)
        {
            if (_allChecksDone || !_isFieldBattle) return;

            Mission m = Mission.Current;
            if (m == null) return;

            // 只关心己方战斗人员阵亡
            if (affectedAgent?.Team != m.PlayerTeam) return;

            // 声望不足，不触发任何判定
            if (PlayerBehaviorPool.Reputation < ReputationMinimum) return;

            if (!_firstCheckDone)
            {
                // ── 第一次机会：存活人数降至 ≤ 20 ─────────────────────────────
                if (GetAliveCount(m.PlayerTeam) > AliveThreshold) return;

                _firstCheckDone = true;

                float chance = Math.Min(PlayerBehaviorPool.Reputation / 200f, 0.5f);
                if (MBRandom.RandomFloat < chance)
                {
                    // 判定成功，立即召援，本场不再有第二次机会
                    _allChecksDone = true;
                    TriggerReinforcement();
                    return;
                }

                // 判定失败，提示祈祷，等待第二次机会
                MBInformationManager.AddQuickInformation(
                    new TaleWorlds.Localization.TextObject("{=*}你在心中祈祷神明的眷顾。"),
                    0);
            }
            else
            {
                // ── 第二次机会：仅剩玩家一人存活 ─────────────────────────────
                if (!IsOnlyPlayerLeft(m)) return;

                _allChecksDone = true;

                float chance = Math.Min(PlayerBehaviorPool.Reputation / 200f, 0.5f);
                if (MBRandom.RandomFloat < chance)
                {
                    TriggerReinforcement();
                }
                // 判定失败：静默，不显示任何提示
            }
        }

        // ── 辅助 ──────────────────────────────────────────────────────────────────

        private void TriggerReinforcement()
        {
            _isSpawning      = true;
            _batchesDone     = 0;
            _batchTimer      = 0f;
            _totalTroopCount = 20 + PlayerBehaviorPool.Reputation;

            MBInformationManager.AddQuickInformation(
                new TaleWorlds.Localization.TextObject("{=*}灰袍守卫增援到了！"),
                0);
        }

        private static bool IsOnlyPlayerLeft(Mission m)
        {
            Agent mainAgent = m.MainAgent;
            if (mainAgent == null || !mainAgent.IsActive()) return false;
            // 阵亡事件触发时该 Agent 已被移出 ActiveAgents，存活数 ≤ 1 即仅剩玩家
            return GetAliveCount(m.PlayerTeam) <= 1;
        }

        // ── 触发判断 ──────────────────────────────────────────────────────────────

        private static int GetAliveCount(Team team)
        {
            int n = 0;
            foreach (Agent a in team.ActiveAgents) if (a.IsActive()) n++;
            return n;
        }

        // ── 分批生成 ──────────────────────────────────────────────────────────────

        private Agent? SpawnBatch()
        {
            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded) return null;

            Team playerTeam = mission.PlayerTeam;
            if (playerTeam == null) return null;

            MatrixFrame spawnFrame = GetReinforcementFrame(mission);

            // 兵种比例：4 步兵 + 4 弓手 + 2 骑兵
            int infantryCount = (int)(BatchSize * 0.4f); // 4
            int archerCount   = (int)(BatchSize * 0.4f); // 4
            int cavalryCount  = BatchSize - infantryCount - archerCount; // 2

            Agent? firstSpawned = null!;
            firstSpawned = SpawnTroops(_infantry, infantryCount, mission, playerTeam, spawnFrame, FormationClass.Infantry, ref firstSpawned);
            SpawnTroops(_archer,   archerCount,  mission, playerTeam, spawnFrame, FormationClass.Ranged,   ref firstSpawned);
            SpawnTroops(_cavalry,  cavalryCount, mission, playerTeam, spawnFrame, FormationClass.Cavalry,  ref firstSpawned);

            return firstSpawned;
        }

        private static MatrixFrame GetReinforcementFrame(Mission mission)
        {
            try
            {
                Agent mainAgent = mission.MainAgent;
                if (mainAgent != null)
                {
                    Vec3 playerPos = mainAgent.Position;
                    Vec2 edgePos2D = mission.GetClosestBoundaryPosition(playerPos.AsVec2);
                    Vec3 edgePos   = edgePos2D.ToVec3(playerPos.Z);
                    Vec2 facingDir = (playerPos.AsVec2 - edgePos2D);
                    if (facingDir.LengthSquared > 0.01f) facingDir = facingDir.Normalized();
                    else facingDir = Vec2.Forward;

                    Mat3 rot = Mat3.Identity;
                    rot.f = facingDir.ToVec3();
                    rot.u = Vec3.Up;
                    rot.s = Vec3.CrossProduct(rot.f, rot.u).NormalizedCopy();
                    return new MatrixFrame(rot, edgePos);
                }
            }
            catch { }

            return MatrixFrame.Identity;
        }

        private Agent? SpawnTroops(
            CharacterObject character,
            int count,
            Mission mission,
            Team team,
            MatrixFrame baseFrame,
            FormationClass formationClass,
            ref Agent? firstAgentOut)
        {
            if (character == null || count <= 0) return firstAgentOut;

            PartyBase party = MobileParty.MainParty?.Party;
            if (party == null) return firstAgentOut;

            Formation formation = team.GetFormation(formationClass);

            for (int i = 0; i < count; i++)
            {
                try
                {
                    Vec2 offset = new Vec2(
                        MBRandom.RandomFloatRanged(-1f, 1f),
                        MBRandom.RandomFloatRanged(-1f, 1f));
                    offset = offset.Normalized() * MBRandom.RandomFloatRanged(1f, 5f);

                    Vec3 pos = baseFrame.origin + new Vec3(offset.X, offset.Y, 0f);
                    Vec2 dir = baseFrame.rotation.f.AsVec2;
                    if (dir.LengthSquared < 0.01f) dir = Vec2.Forward;

                    var origin    = new PartyAgentOrigin(party, character, -1, new UniqueTroopDescriptor(), false);
                    var buildData = new AgentBuildData(origin)
                        .Team(team)
                        .InitialPosition(in pos)
                        .InitialDirection(in dir)
                        .Formation(formation);

                    Agent agent = mission.SpawnAgent(buildData);
                    if (agent != null)
                    {
                        agent.SetWatchState(Agent.WatchState.Alarmed);
                        firstAgentOut ??= agent;
                    }
                }
                catch { }
            }

            if (formation != null)
                formation.SetControlledByAI(true, false);

            return firstAgentOut;
        }

        // ── 号角 ──────────────────────────────────────────────────────────────────

        private void PlayArrivalHorn(Agent agent)
        {
            if (StartHorn()) return;

            if (agent != null && agent.IsActive())
                agent.MakeVoice(SkinVoiceManager.VoiceType.Charge,
                                SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
        }

        /// <summary>
        /// 创建并播放一次号角，记录停止时间，返回是否成功。
        /// OnMissionTick 检测到停止时间到达后自动调用以续播下一次。
        /// </summary>
        private bool StartHorn()
        {
            if (Mission.Current?.Scene == null) return false;
            int soundId = SoundEvent.GetEventIdFromString("gwp/support/horn");
            if (soundId < 0) return false;

            _hornSound  = SoundEvent.CreateEvent(soundId, Mission.Current.Scene);
            _hornStopAt = _elapsedSeconds + HornDuration;
            _hornPlayed++;
            _hornSound.Play();
            return true;
        }
    }
}
