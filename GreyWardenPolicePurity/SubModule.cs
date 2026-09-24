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
            GwpRuntimeFaultWatch.Arm();

            // Self-contained library patch; no separate Harmony module or
            // launcher dependency is required.
            Harmony harmony = new(HarmonyId);
            // Patch one class at a time. PatchAll aborts the whole assembly on the
            // first failure, so a single patch whose target moved in a new game
            // build would silently take every later patch down with it.
            foreach (Type type in typeof(SubModule).Assembly.GetTypes())
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception exception)
                {
                    GwpFaultTrace.Write(
                        "SUBMODULE_PATCH_FAILED",
                        details: type.FullName + " -> "
                            + exception.GetType().FullName + ":" + exception.Message);
                    // Never turn an optional enhancement into a startup failure if a
                    // future game build changes the callback it hooks.
                    Debug.Print("[GreyWarden] patch failed for " + type.FullName
                        + ": " + exception);
                }
            }
        }

        // Item XMLs are not deserialized yet in OnGameStart, so the blades'
        // setup waits until object initialization finishes - for every game,
        // since each game reloads its items into a new object manager.
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            GwpDualBladeItemSetup.Apply(game);
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
            GwpSyndicateMusicBehavior.Pump();
            // 战役心跳跟着时间流走，地图暂停时不一定推进。派遣的对话与分兵界面必须
            // 在玩家点完按钮的下一帧就弹出来，所以挂在与时间无关的应用心跳上。
            GwpRuntimeFaultWatch.Guard("DISPATCH_PUMP", GwpWardenDispatchDialogue.Pump);
        }

        private static void RegisterCampaignComponents(CampaignGameStarter starter)
        {
            starter.RemoveBehaviors<DesertersCampaignBehavior>();

            starter.AddModel(new PoliceHeroCreationModel());
            starter.AddModel(new PoliceClanTierModel());
            starter.AddModel(new PoliceAntiRecruitmentModel());
            starter.AddModel(new PolicePartyTroopUpgradeModel());
            starter.AddModel(new GwpPartySizeLimitModel());
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
            starter.AddBehavior(new GwpFieldArrestBehavior());
            starter.AddBehavior(new GwpFieldReportLedger());
            starter.AddBehavior(new GwpWardenDispatchBehavior());
            starter.AddBehavior(new GreyWardenVillageAdoptionBehavior());
            starter.AddBehavior(new GreyWardenVillageRewardBehavior());
            starter.AddBehavior(new GreyWardenLoreBehavior());
            starter.AddBehavior(new GreyWardenFamilyBehavior());
            starter.AddBehavior(new GreyWardenVillageReconstructionBehavior());
            starter.AddBehavior(new GreyWardenIssueResolutionBehavior());
            starter.AddBehavior(new GreyWardenTrainingBehavior());
            starter.AddBehavior(new GreyWardenPlayerRequestBehavior());
            starter.AddBehavior(new GreyWardenLeaderBalanceBehavior());
            starter.AddBehavior(new GreyWardenNotableRelationsBehavior());
            starter.AddBehavior(new GreyWardenTroopRequestBehavior());
            starter.AddBehavior(new GreyWardenSparringBehavior());
            // 行为只负责状态与事件；最终欲望过滤由 dispatcher postfix 在
            // 所有原版评分器完成后执行，不能依赖 MbEvent 的注册顺序。
            starter.AddBehavior(new GreyWardenPartyDesireBehavior());
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
            // Keeps an AI dual wielder's pair in hand - the archer's after it
            // switches out of the bow, the AI commander's throughout. A
            // mission behaviour rather than a patch: previews break on
            // per-call Agent and MissionWeapon patches, never on these.
            mission.AddMissionBehavior(new GwpDualBladeAiBehavior());
            mission.AddMissionBehavior(new GwpKickBehavior());
            mission.AddMissionBehavior(
                new GwpAlternativeAttackControlBehavior());
            mission.AddMissionBehavior(new GwpPassiveShieldBreakBehavior());

            mission.AddMissionBehavior(new GwpBattleSceneContext());
            mission.AddMissionBehavior(new GwpSyndicateMusicBehavior());

            BasicCharacterObject infantry = Game.Current!.ObjectManager.GetObject<BasicCharacterObject>(GwpIds.HeavyInfantryId);
            BasicCharacterObject archer = Game.Current.ObjectManager.GetObject<BasicCharacterObject>(GwpIds.ArcherId);
            BasicCharacterObject cavalry = Game.Current.ObjectManager.GetObject<BasicCharacterObject>(GwpIds.KnightId);
            if (infantry != null && archer != null && cavalry != null)
                mission.AddMissionBehavior(new GwpBattleReinforcementBehavior(infantry, archer, cavalry));
        }
    }
}
