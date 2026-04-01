﻿using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is not Campaign || gameStarterObject is not CampaignGameStarter starter) return;
            RegisterCampaignComponents(starter);
        }

        private static void RegisterCampaignComponents(CampaignGameStarter starter)
        {
            starter.AddModel(new PoliceAntiRecruitmentModel());
            starter.AddModel(new PoliceMarriageModel());
            starter.AddModel(new PoliceRaidDeterrenceModel());
            starter.AddModel(new PoliceShipDamageModel());
            starter.AddModel(new PoliceShipParametersModel());
            starter.AddBehavior(new PoliceCrimeMonitorEnhanced());
            starter.AddBehavior(new PoliceAntiWarDeclaration());
            starter.AddBehavior(new PoliceAntiVanillaWarBehavior());
            starter.AddBehavior(new PoliceAIDeterrenceBehavior());
            starter.AddBehavior(new PolicePrisonerImmunityBehavior());
            starter.AddBehavior(new PoliceEnforcementBehavior());
            starter.AddBehavior(new PoliceResourceManager());
            starter.AddBehavior(new PlayerBehaviorMonitor());
            starter.AddBehavior(new PolicePatrolBehavior());
            starter.AddBehavior(new PlayerBountyBehavior());
            starter.AddBehavior(new GreyWardenVillageAdoptionBehavior());
            starter.AddBehavior(new GreyWardenVillageRewardBehavior());
            starter.AddBehavior(new GreyWardenLoreBehavior());
            starter.AddBehavior(new GreyWardenFamilyBehavior());
            starter.AddBehavior(new GreyWardenTroopRequestBehavior());
        }

        // 不在此处过滤 IsFieldBattle，因为该属性在 OnMissionBehaviorInitialize
        // 阶段尚未完成初始化，可能始终为 false。由 Behavior 内部的 AfterStart() 判断。
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // 只在 Campaign 游戏中注入（过滤多人模式）
            if (Game.Current?.GameType is not Campaign) return;

            CharacterObject infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
            CharacterObject archer = CharacterObject.Find(GwpIds.ArcherId);
            CharacterObject cavalry = CharacterObject.Find(GwpIds.KnightId);
            mission.AddMissionBehavior(new GwpBattleReinforcementBehavior(infantry, archer, cavalry));
        }
    }
}
