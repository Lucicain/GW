using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            {
                // ======================
                // 模型覆盖：AI招募评分拦截（必须在所有 Behavior 之前注册）
                // ======================
                starter.AddModel(new PoliceAntiRecruitmentModel());

                // ======================
                // 核心功能：全地图犯罪监控
                // ======================
                starter.AddBehavior(new PoliceCrimeMonitorEnhanced());

                // ======================
                // AI宣战逻辑
                // ======================
                starter.AddBehavior(new PoliceAntiWarDeclaration());
                // ======================
                // 核心功能5：警察惩戒罪犯
                // ======================
                starter.AddBehavior(new PoliceEnforcementBehavior());

                // ======================
                // 核心功能6：警察资源管理（发薪+补给）
                // ======================
                starter.AddBehavior(new PoliceResourceManager());

                // ======================
                // 核心功能7：玩家行为监控
                // ======================
                starter.AddBehavior(new PlayerBehaviorMonitor());

                // ======================
                // 核心功能8：灰袍纠察队（罚款/追讨/奖励）
                // ======================
                starter.AddBehavior(new PolicePatrolBehavior());

                // ======================
                // 核心功能9：玩家悬赏猎人（穿戴黑袍指挥官全套时可接案）
                // ======================
                starter.AddBehavior(new PlayerBountyBehavior());

            }
        }

        // ── 战场行为注入 ──────────────────────────────────────────────────────────
        // 注意：不在此处过滤 IsFieldBattle，因为该属性在 OnMissionBehaviorInitialize
        // 阶段尚未完成初始化，可能始终为 false。由 Behavior 内部的 AfterStart() 判断。
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // 只在 Campaign 游戏中注入（过滤多人模式）
            // 注意：CharacterObject.Find 在此仍处于 Campaign 层，可正常解析
            if (Game.Current?.GameType is Campaign)
            {
                CharacterObject infantry = CharacterObject.Find("gwheavyinfantry");
                CharacterObject archer   = CharacterObject.Find("gwarcher");
                CharacterObject cavalry  = CharacterObject.Find("gwknight");
                mission.AddMissionBehavior(new GwpBattleReinforcementBehavior(infantry, archer, cavalry));
            }
        }
    }
}