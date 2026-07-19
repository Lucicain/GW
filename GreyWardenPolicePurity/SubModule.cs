using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId =
            "GreyWardenPolicePurity.ShieldBashGuard";
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Self-contained library patch; no separate Harmony module or
            // launcher dependency is required.
            try
            {
                new Harmony(HarmonyId).PatchAll(typeof(SubModule).Assembly);
            }
            catch (Exception exception)
            {
                // Never turn an optional combat enhancement into a startup
                // failure if a future game build changes the private callback.
                Debug.Print(
                    "[GreyWarden Shield Bash Guard] patch failed: "
                    + exception);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);


            // Wrap whichever native damage model this game mode registered
            // (Sandbox in Campaign, Custom in Custom Battle). The wrapper
            // changes only Grey Warden alternative-attack knockdowns.
            gameStarterObject.AddModel(new GwpAgentApplyDamageModel());
            gameStarterObject.AddModel(new GwpAgentStatCalculateModel());

            if (game.GameType is not Campaign || gameStarterObject is not CampaignGameStarter starter) return;
            RegisterCampaignComponents(starter);
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            _ = dt;
            GreyWardenSparringBehavior.OnApplicationTick();
        }

        private static void RegisterCampaignComponents(CampaignGameStarter starter)
        {
            starter.RemoveBehaviors<DesertersCampaignBehavior>();

            starter.AddModel(new PoliceClanTierModel());
            starter.AddModel(new PoliceAntiRecruitmentModel());
            starter.AddModel(new PoliceMobilePartyAIModel());
            starter.AddModel(new PoliceMarriageModel());
            starter.AddModel(new PoliceRaidDeterrenceModel());
            starter.AddModel(new PoliceShipDamageModel());
            starter.AddModel(new PoliceShipParametersModel());
            starter.AddBehavior(new PoliceCrimeMonitorEnhanced());
            starter.AddBehavior(new PoliceAntiWarDeclaration());
            starter.AddBehavior(new PoliceAntiVanillaWarBehavior());
            starter.AddBehavior(new PoliceAIDeterrenceBehavior());
            starter.AddBehavior(new GreyWardenDesertersCampaignBehavior());
            starter.AddBehavior(new GreyWardenDeserterFilterBehavior());
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
            starter.AddBehavior(new GreyWardenLeaderBalanceBehavior());
            starter.AddBehavior(new GreyWardenTroopRequestBehavior());
            starter.AddBehavior(new GreyWardenSparringBehavior());
        }

        // 不在此处过滤 IsFieldBattle，因为该属性在 OnMissionBehaviorInitialize
        // 阶段尚未完成初始化，可能始终为 false。由 Behavior 内部的 AfterStart() 判断。
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            GameType? gameType = Game.Current?.GameType;
            if (gameType == null) return;

            // 踢腿能力同时用于战役和自定义战斗。GreyWarden 本身是纯单人
            // 模组，因此这里不需要用 Campaign 类型把 CustomGame 排除掉。
            mission.AddMissionBehavior(new GwpKickBehavior());
            mission.AddMissionBehavior(
                new GwpAlternativeAttackControlBehavior());
            mission.AddMissionBehavior(new GwpPassiveShieldBreakBehavior());

            // 战场增援依赖 Campaign 数据，自定义战斗中不注入。
            if (gameType is not Campaign) return;

            CharacterObject infantry = CharacterObject.Find(GwpIds.HeavyInfantryId);
            CharacterObject archer = CharacterObject.Find(GwpIds.ArcherId);
            CharacterObject cavalry = CharacterObject.Find(GwpIds.KnightId);
            mission.AddMissionBehavior(new GwpBattleReinforcementBehavior(infantry, archer, cavalry));
        }
    }
}
