# 代码地图（自动生成）

> **这个文件由 `tools/Generate-CodeMap.py` 生成，不要手改。**
> 源码改了就重新生成：`python tools/Generate-CodeMap.py`
> 它描述代码**的形状**，不描述行为——行为看同目录其他 state 文件。

141 个源文件、47,612 行、265 个公开类型、55 个 Harmony 补丁点（分布在 55 个补丁类里）。

## 原版接触面（Harmony 补丁）

**这张表是这个模组与 Bannerlord 的全部契约。** 换游戏版本时先核对它。

这里数的是源码里 `[HarmonyPatch(typeof(...))]` 的**补丁点**。
`Verify-GameCompat.ps1` 的 `PATCH_OK` 数的是**成功绑定的补丁类**，口径不同，
两个数字不应直接相减。

| 原版类型 | 成员 | 方式 | 补丁类 | 文件 |
|---|---|---|---|---|
| `AchievementsCampaignBehavior` | `CheckAchievementSystemActivity` | Prefix | `GwpAchievementActivityPatch` | GwpAchievementCompatibilityPatch.cs |
| `AiEngagePartyBehavior` | `AiHourlyTick` | Finalizer/Postfix/Prefix | `GwpAssistanceArmyNativeEngageDesirePatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `AiPartyThinkBehavior` | `PartyHourlyAiTick` | Postfix/Prefix | `GwpPartyThinkResolvedDiagnosticsPatch` | GwpPartyDutyMovementPatch.cs |
| `BarterManager` | `ApplyAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterManager` | `CancelAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionCancelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterManager` | `IsOfferAcceptable` | Postfix | `GwpAssetAcceptancePatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `ExecuteAutoBalance` | Prefix | `GwpAssetAutoOfferPatch` | GwpAssetAutoOfferPatch.cs |
| `BarterVM` | `ExecuteCancel` | Prefix | `GwpDispatchVmCancelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `ExecuteOffer` | Prefix | `GwpAssetOfferValidationPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `RefreshOfferLabel` | Postfix | `GwpDispatchOfferLabelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `SendOffer` | Postfix | `GwpDispatchOfferDisplayPatch` | GwpAssetOfferValidationPatch.cs |
| `BehaviorComponent` | `GetAIWeight` | Postfix | `GwpBattleBehaviorScoreTrace` | GwpBattleCommandTrace.cs |
| `CampaignEventDispatcher` | `AiHourlyTick` | Postfix | `GwpFinalDesireAuctionPatch` | GwpPartyDutyMovementPatch.cs |
| `CharacterRelationManager` | `SetHeroRelation` | Prefix | `GreyWardenNotableRelationWritePatch` | GreyWardenNotableRelationsBehavior.cs |
| `CommonAIComponent` | `Morale` (Setter) | Prefix | `GwpWardenMoralePatch` | GwpWardenResolve.cs |
| `CommonAIComponent` | `Panic` | Prefix | `GwpWardenPanicPatch` | GwpWardenResolve.cs |
| `CommonAIComponent` | `Retreat` | Prefix | `GwpWardenRetreatPatch` | GwpWardenResolve.cs |
| `CraftingTemplate` | `All` (Getter) | Postfix | `GwpDualBladeCraftingTemplateVisibilityPatch` | GwpDualBladeActionSetPatch.cs |
| `CustomBattleData` | `get_Characters` | Finalizer/Postfix | `GwpCustomBattleCommanderListPatch` | GwpDualBladeActionSetPatch.cs |
| `DefaultArmyManagementCalculationModel` | `CalculateDailyCohesionChange` | Postfix | `GwpAssistanceArmyCohesionPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `DefaultArmyManagementCalculationModel` | `CalculatePartyInfluenceCost` | Prefix | `GwpLeaderlessSupportArmyInfluencePatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `DefaultBattleMissionAgentSpawnLogic` | `IsSideDepleted` | Postfix | `GwpBattleSupportDepletionPatch` | GwpBattleReinforcementBehavior.cs |
| `DefaultMobilePartyAIModel` | `GetBestInitiativeBehavior` | Postfix | `GwpInitiativeDiagnosticsPatch` | GwpPartyDutyMovementPatch.cs |
| `DefaultPersuasionModel` | `GetChances` | Postfix | `GwpNegotiationChancePatch` | GwpNegotiationChancePatch.cs |
| `DumpIntegrityCampaignBehavior` | `IsGameIntegrityAchieved` | Postfix | `GwpDumpIntegrityAchievementPatch` | GwpAchievementCompatibilityPatch.cs |
| `EncounterGameMenuBehavior` | `army_encounter_background_on_init` | Prefix | `GwpAssistanceArmyEncounterBackgroundPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_item_on_condition` | Prefix | `GwpLeaderlessSupportConversationItemPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_on_condition` | Postfix | `GwpLeaderlessSupportConversationMenuPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EnterSettlementAction` | `ApplyForParty` | Prefix | `GwpCaseSettlementEntryPatch` | GwpCaseSettlementEntryPatch.cs |
| `FoodConsumptionBehavior` | `DailyTickParty` | Finalizer/Prefix | `GwpDispatchCargoFoodPatch` | GwpDispatchCargo.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaClanPageWidgetPatch` | GwpEncyclopediaClanPageVM.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaHeroPageWidgetPatch` | GwpEncyclopediaHeroPageVM.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpSingleQueryPopupWidgetPatch` | GwpSingleQueryLinkPatch.cs |
| `HumanAIComponent` | `RefreshBehaviorValues` | Prefix | `GwpWardenRetreatBehaviorPatch` | GwpWardenResolve.cs |
| `KingdomElection` | `DetermineOfficialSupport` | Postfix | `GwpFiefPublicSupportPatch` | GwpFiefPublicSupportPatch.cs |
| `MapInfoVM` | `CreateItems` | Postfix | `GwpMapInfoCreateItemsPatch` | GwpMapBarReputationPatch.cs |
| `MapInfoVM` | `UpdatePlayerInfo` | Postfix | `GwpMapInfoUpdatePatch` | GwpMapBarReputationPatch.cs |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveHeldShieldParticlePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveShieldHitParticlePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `HandleMissileCollisionReaction` | Prefix | `GwpArcherArrowShieldPassPatch` | GwpArcherArrowEffects.cs |
| `Mission` | `MeleeHitCallback` | Postfix/Prefix | `GwpPassiveHeldShieldMeleePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `MissileHitCallback` | Finalizer/Postfix/Prefix | `GwpArcherArrowMissileHitPatch` | GwpArcherArrowEffects.cs |
| `Mission` | `RegisterBlow` | Prefix | `GwpPassiveShieldRegisterBlowPatch` | GwpShieldBashGuardPatch.cs |
| `MissionCombatMechanicsHelper` | `ComputeBlowDamage` | Prefix | `GwpDualWieldDamageTypePatch` | GwpDualWieldingPatch.cs |
| `MissionCombatMechanicsHelper` | `IsCollisionBoneDifferentThanWeaponAttachBone` | Postfix | `GwpDualWieldCollisionPatch` | GwpDualWieldingPatch.cs |
| `MissionNameMarkerVM` | `Tick` | Postfix | `GwpDuelOpponentMarkerPatch` | GwpDuelOpponentMarkerPatch.cs |
| `MobileParty` | `CalculateSpeed` | Prefix | `GwpEscortSpeedCapPatch` | GwpEscortSpeedCapPatch.cs |
| `MobileParty` | `GetBehaviorText` | Postfix | `GwpLocationDutyBehaviorTextPatch` | GwpPartyDutyMovementPatch.cs |
| `PartyCharacterVM` | `ExecuteTalk` | Prefix | `GwpPartyCharacterExecuteTalkPatch` | GwpPartyScreenTroopTalkPatch.cs |
| `PartyCharacterVM` | `UpdateTalkable` | Postfix | `GwpPartyCharacterTalkablePatch` | GwpPartyScreenTroopTalkPatch.cs |
| `PersonaSoftspokenTag` | `IsApplicableTo` | Finalizer | `GwpDispatchBarterFaultDiagnostics` | GwpDispatchBarterFaultDiagnostics.cs |
| `SetPartyAiAction` | `GetActionForGoingAroundParty` | Prefix | `GwpPlayerEnforcementEngageActionPatch` | GwpPartyDutyMovementPatch.cs |
| `SetPartyAiAction` | `GetActionForPatrollingAroundPoint` | Prefix | `GwpLocationDutyRefreshPatch` | GwpPartyDutyMovementPatch.cs |
| `SingleQueryPopUpVM` | `OnClearData` | Postfix | `GwpSingleQueryPopupClearPatch` | GwpSingleQueryLinkPatch.cs |
| `SpawnedItemEntity` | `OnUseStopped` | Transpiler | `GwpDualBladeGroundPickupPatch` | GwpDualBladeGroundPickupPatch.cs |

## 注册入口（SubModule.cs）

**战役行为**，按注册顺序：

- `PoliceCrimeMonitorEnhanced`
- `PoliceAntiWarDeclaration`
- `PoliceAntiVanillaWarBehavior`
- `PoliceAIDeterrenceBehavior`
- `GreyWardenDesertersCampaignBehavior`
- `GreyWardenDeserterFilterBehavior`
- `PolicePrisonerImmunityBehavior`
- `PoliceEnforcementBehavior`
- `PoliceResourceManager`
- `PlayerBehaviorMonitor`
- `PolicePatrolBehavior`
- `PlayerBountyBehavior`
- `GwpFieldArrestBehavior`
- `GwpFieldReportLedger`
- `GwpWardenDispatchBehavior`
- `GreyWardenVillageAdoptionBehavior`
- `GreyWardenVillageRewardBehavior`
- `GreyWardenLoreBehavior`
- `GreyWardenFamilyBehavior`
- `GreyWardenVillageReconstructionBehavior`
- `GreyWardenIssueResolutionBehavior`
- `GreyWardenTrainingBehavior`
- `GreyWardenPlayerRequestBehavior`
- `GreyWardenLeaderBalanceBehavior`
- `GreyWardenNotableRelationsBehavior`
- `GreyWardenTroopRequestBehavior`
- `GreyWardenSparringBehavior`
- `GreyWardenPartyDesireBehavior`

**模型覆盖**：

- `GwpAgentApplyDamageModel`
- `GwpAgentStatCalculateModel`
- `PoliceHeroCreationModel`
- `PoliceClanTierModel`
- `PoliceAntiRecruitmentModel`
- `PolicePartyTroopUpgradeModel`
- `GwpPartySizeLimitModel`
- `PoliceMobilePartyAIModel`
- `PoliceMarriageModel`
- `PoliceRaidDeterrenceModel`
- `PoliceShipDamageModel`
- `PoliceShipParametersModel`

**任务行为**，按注册顺序（顺序影响生效时机）：

- `GwpDualBladeAiBehavior`
- `GwpBattleCommandTrace`
- `GwpKickBehavior`
- `GwpAlternativeAttackControlBehavior`
- `GwpPassiveShieldBreakBehavior`
- `GwpBattleSceneContext`
- `GwpSyndicateMusicBehavior`
- `GwpBattleReinforcementBehavior`

## 文件索引

### 音乐（4 个文件）

- `GwpMusicScore.cs` — 277 行 · `GwpMusicScore` : IDisposable，另含 1 个类型
- `GwpSyndicateMusicBehavior.cs` — 200 行 · `GwpSyndicateMusicBehavior` : MissionBehavior
- `GwpMusicOutput.cs` — 118 行 · `GwpMusicOutput` : IDisposable
- `GwpMusicBattlePolicy.cs` — 100 行 · `GwpMusicBattlePolicy`

### 战场援军（4 个文件）

- `GwpBattleReinforcementBehavior.cs` — 266 行 · `GwpBattleReinforcementBehavior` : MissionBehavior，另含 1 个类型
- `GwpBattleSceneContext.cs` — 81 行 · `GwpBattleSceneContext` : MissionBehavior
- `GwpBattleScenePolicy.cs` — 74 行 · `GwpBattleScenePolicy`，另含 1 个类型
- `GwpBattleSupportOrigin.cs` — 37 行 · `GwpBattleSupportOrigin` : IAgentOriginBase

### 战场：死战不退（1 个文件）

- `GwpWardenResolve.cs` — 96 行 · `GwpWardenResolve`，另含 4 个类型

### 战场：双刀/近战（11 个文件）

- `GwpShieldBashGuardPatch.cs` — 1434 行 · `GwpPassiveHeldShieldCollision` : MissionBehavior，另含 5 个类型
- `GwpDualBladeAiBehavior.cs` — 850 行 · `GwpDualBladeAgentState` : MissionBehavior，另含 5 个类型
- `GwpDualBladeActionSetPatch.cs` — 383 行 · `GwpFaultTrace`，另含 5 个类型
- `GwpAlternativeAttackControlBehavior.cs` — 347 行 · `GwpAlternativeAttackControlBehavior` : MissionBehavior
- `GwpKickInputComponent.cs` — 216 行 · `GwpKickInputComponent` : AgentComponent
- `GwpAlternativeAttackControl.cs` — 177 行 · `GwpAlternativeAttackControl`
- `GwpDualBladeGroundPickupPatch.cs` — 168 行 · `GwpDualBladeGroundPickup`，另含 1 个类型
- `GwpDualBladeNpcItemSetup.cs` — 131 行 · `GwpDualBladeNpcItemSetup`
- `GwpKickBehavior.cs` — 85 行 · `GwpKickBehavior` : MissionBehavior
- `GwpDualBladeActionGate.cs` — 61 行 · `GwpDualBladeActionGate`
- `GwpDualBladeAttackArmor.cs` — 38 行 · `GwpDualBladeAttackArmor`

### 诊断（5 个文件）

- `GwpAiDiagnostics.cs` — 664 行 · `GwpAiDiagnostics`，另含 1 个类型
- `GwpBattleCommandTrace.cs` — 226 行 · `GwpBattleCommandTrace` : MissionBehavior，另含 2 个类型
- `GwpRuntimeFaultWatch.cs` — 107 行 · `GwpRuntimeFaultWatch`
- `GwpEngineAssertDiagnostics.cs` — 38 行 · `GwpEngineAssertDiagnostics`
- `GwpDispatchBarterFaultDiagnostics.cs` — 27 行 · `GwpDispatchBarterFaultDiagnostics`

### 执法：案件与巡逻（29 个文件）

- `PoliceEnforcementBehavior.Assistance.cs` — 2750 行 · `PoliceEnforcementBehavior`，另含 1 个类型
- `PoliceEnforcementBehavior.cs` — 1518 行 · `PoliceEnforcementBehavior` : CampaignBehaviorBase
- `PoliceEnforcementBehavior.DelayPatrols.cs` — 1455 行 · `PoliceEnforcementBehavior`
- `GwpFieldArrestBehavior.cs` — 1391 行 · `GwpFieldArrestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `PolicePatrolBehavior.cs` — 1073 行 · `PolicePatrolBehavior` : CampaignBehaviorBase
- `PoliceResourceManager.cs` — 814 行 · `PoliceResourceManager` : CampaignBehaviorBase
- `GwpAiDeterrenceState.cs` — 733 行 · `GwpAiDeterrenceState`
- `PoliceEnforcementBehavior.Helpers.cs` — 594 行 · `PoliceEnforcementBehavior`
- `PoliceEnforcementBehavior.Dialogue.cs` — 550 行 · `PoliceEnforcementBehavior`
- `PoliceCrimeMonitorEnhanced.cs` — 475 行 · `PoliceCrimeMonitorEnhanced` : CampaignBehaviorBase
- `PoliceAIDeterrenceBehavior.cs` — 468 行 · `PoliceAIDeterrenceBehavior` : CampaignBehaviorBase
- `PolicePatrolBehavior.Helpers.cs` — 398 行 · `PolicePatrolBehavior`
- `GwpFieldArrestLines.cs` — 360 行 · `GwpFieldArrestLines`，另含 1 个类型
- `PoliceAntiWarDeclaration.cs` — 358 行 · `PoliceAntiWarDeclaration` : CampaignBehaviorBase
- `GwpAiDeterrenceDialogueCatalog.cs` — 339 行 · `DeterrenceSource`，另含 3 个类型
- `PoliceEnforcementBehavior.AtonementQuest.cs` — 299 行 · `PoliceEnforcementBehavior` : QuestBase，另含 1 个类型
- `PolicePrisonerImmunityBehavior.cs` — 139 行 · `PolicePrisonerImmunityBehavior` : CampaignBehaviorBase
- `PoliceShipModels.cs` — 103 行 · `PoliceShipModelSupport` : CampaignShipDamageModel，另含 2 个类型
- `PolicePartyTroopUpgradeModel.cs` — 74 行 · `PolicePartyTroopUpgradeModel` : DefaultPartyTroopUpgradeModel
- `GwpFieldArrestBehavior.Grace.cs` — 71 行 · `GwpFieldArrestBehavior`
- `PoliceMobilePartyAIModel.cs` — 71 行 · `PoliceMobilePartyAIModel` : DefaultMobilePartyAIModel
- `PoliceAntiRecruitmentModel.cs` — 67 行 · `PoliceAntiRecruitmentModel` : DefaultDiplomacyModel
- `PoliceAntiVanillaWarBehavior.cs` — 62 行 · `PoliceAntiVanillaWarBehavior` : CampaignBehaviorBase
- `PolicePatrolBehavior.Encyclopedia.cs` — 49 行 · `PolicePatrolBehavior`
- `PoliceClanTierModel.cs` — 40 行 · `PoliceClanTierModel` : DefaultClanTierModel
- `PoliceMarriageModel.cs` — 36 行 · `PoliceMarriageModel` : DefaultMarriageModel
- `PoliceHeroCreationModel.cs` — 34 行 · `PoliceHeroCreationModel` : DefaultHeroCreationModel
- `PoliceRaidDeterrenceModel.cs` — 30 行 · `PoliceRaidDeterrenceModel` : DefaultTargetScoreCalculatingModel
- `PoliceEnforcementBehavior.IdleDuties.cs` — 12 行 · `PoliceEnforcementBehavior`

### 执法：玩家悬赏（7 个文件）

- `PlayerBountyBehavior.cs` — 979 行 · `PlayerBountyBehavior` : CampaignBehaviorBase
- `GwpCaseArchiveScreen.cs` — 954 行 · `GwpCaseArchiveScreen` : ViewModel，另含 2 个类型
- `PlayerBountyBehavior.DialogueAndNotification.cs` — 942 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.CaseSettlement.cs` — 743 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.QuestAndNotification.cs` — 205 行 · `PlayerBountyBehavior` : QuestBase，另含 3 个类型
- `PlayerBountyBehavior.Support.cs` — 178 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.Encyclopedia.cs` — 49 行 · `PlayerBountyBehavior`

### 使者与交兵（6 个文件）

- `GreyWardenTroopRequestBehavior.cs` — 1352 行 · `GreyWardenTroopRequestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpWardenDispatchBehavior.cs` — 1095 行 · `GwpWardenDispatchBehavior` : CampaignBehaviorBase
- `GwpWardenDispatchDialogue.cs` — 536 行 · `GwpWardenDispatchDialogue`
- `GreyWardenTroopRequestBehavior.Cohort.cs` — 293 行 · `GreyWardenTroopRequestBehavior`
- `GwpWardenDispatch.cs` — 109 行 · `GwpDispatchPurpose`，另含 2 个类型
- `GwpBribeBarterable.cs` — 62 行 · `GwpBribeBarterable` : Barterable

### 练兵与切磋（3 个文件）

- `GreyWardenFieldSparringMissionController.cs` — 2579 行 · `GreyWardenFieldSparringMissionController` : MissionLogic
- `GreyWardenSparringBehavior.cs` — 955 行 · `GreyWardenSparringBehavior` : CampaignBehaviorBase
- `GreyWardenTrainingBehavior.cs` — 809 行 · `GreyWardenTrainingBehavior` : CampaignBehaviorBase，另含 2 个类型

### 家族与村庄（5 个文件）

- `GreyWardenVillageAdoptionBehavior.cs` — 919 行 · `GreyWardenVillageAdoptionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenFamilyBehavior.cs` — 624 行 · `GreyWardenFamilyBehavior` : CampaignBehaviorBase，另含 1 个类型
- `GreyWardenVillageReconstructionBehavior.cs` — 518 行 · `GreyWardenVillageReconstructionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenVillageRewardSliderScreen.cs` — 255 行 · `GreyWardenVillageRewardSliderScreen` : ViewModel，另含 1 个类型
- `GreyWardenVillageRewardBehavior.cs` — 194 行 · `GreyWardenVillageRewardBehavior` : CampaignBehaviorBase

### 战斗数值模型（1 个文件）

- `GwpAgentApplyDamageModel.cs` — 877 行 · `GwpAgentApplyDamageModel` : AgentApplyDamageModel

### 大地图欲望与 AI（3 个文件）

- `PlayerBehaviorMonitor.cs` — 931 行 · `PlayerBehaviorMonitor` : CampaignBehaviorBase
- `GreyWardenPartyDesireBehavior.cs` — 837 行 · `GreyWardenPartyDesireBehavior` : CampaignBehaviorBase
- `GwpArmyExitDisorganizedPatch.cs` — 85 行 · `GwpArmyExitDisorganizedPatch`

### 玩家交互（1 个文件）

- `GreyWardenPlayerRequestBehavior.cs` — 1010 行 · `GreyWardenPlayerRequestBehavior` : CampaignBehaviorBase，另含 2 个类型

### 数据与共用（4 个文件）

- `GwpData.cs` — 1185 行 · `CrimeRecord`，另含 6 个类型
- `GwpTuning.cs` — 318 行 · `GwpTuning`，另含 12 个类型
- `GwpCommon.cs` — 261 行 · `GwpCommon`
- `GwpIds.cs` — 99 行 · `GwpIds`

### 入口（1 个文件）

- `SubModule.cs` — 159 行 · `SubModule` : MBSubModuleBase

### 其他（56 个文件）

- `GwpFieldReportLedger.cs` — 435 行 · `GwpFieldReportLedger` : CampaignBehaviorBase，另含 1 个类型
- `GwpEncyclopediaHeroPageVM.cs` — 417 行 · `GwpEncyclopediaHeroPageExtension`，另含 2 个类型
- `GreyWardenIssueResolutionBehavior.cs` — 390 行 · `GreyWardenIssueResolutionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpPoliceWarReasonService.cs` — 389 行 · `GwpPoliceWarReasonService`
- `GreyWardenDesertersCampaignBehavior.cs` — 328 行 · `GreyWardenDesertersCampaignBehavior` : CampaignBehaviorBase
- `GwpAssetPayment.cs` — 254 行 · `GwpAssetPayment`
- `GwpAssistanceArmyLifecyclePatch.cs` — 237 行 · `GwpAssistanceArmyDisbandGuardPatch`，另含 6 个类型
- `GreyWardenNotableRelationsBehavior.cs` — 232 行 · `GreyWardenNotableRelationsBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenLoreBehavior.cs` — 217 行 · `GreyWardenLoreBehavior` : CampaignBehaviorBase
- `GwpAgentStatCalculateModel.cs` — 210 行 · `GwpAgentStatCalculateModel` : AgentStatCalculateModel，另含 1 个类型
- `GreyWardenLeaderBalanceBehavior.cs` — 193 行 · `GreyWardenLeaderBalanceBehavior` : CampaignBehaviorBase
- `GwpArcherArrowEffects.cs` — 193 行 · `GwpArcherArrowHitState`，另含 2 个类型
- `GwpEncyclopediaClanPageVM.cs` — 193 行 · `GwpEncyclopediaClanPageExtension`，另含 2 个类型
- `GreyWardenSafeTroopSupplier.cs` — 188 行 · `GreyWardenSafeTroopSupplier` : IMissionTroopSupplier，另含 1 个类型
- `GwpPartyDutyMovementPatch.cs` — 185 行 · `GwpFinalDesireAuctionPatch`，另含 5 个类型
- `GwpLandlessShipTradePatch.cs` — 160 行 · `GwpLandlessShipTrade`，另含 2 个类型
- `GwpOffenderDesire.cs` — 160 行 · `GwpOffenderDesire`，另含 1 个类型
- `GwpRuntimeState.cs` — 152 行 · `GwpRuntimeState`，另含 2 个类型
- `GwpFieldDialogueVoice.cs` — 147 行 · `GwpFieldDialogueVoice`
- `GwpFiefPublicSupportPatch.cs` — 145 行 · `GwpFiefPublicSupportPatch`
- `GwpAchievementCompatibilityPatch.cs` — 142 行 · `GwpAchievementCompatibility`，另含 2 个类型
- `GwpMapBarReputationPatch.cs` — 140 行 · `GwpMapBarReputation`，另含 2 个类型
- `GwpAssetOfferValidationPatch.cs` — 105 行 · `GwpAssetOfferValidationPatch`，另含 6 个类型
- `GwpDispatchBarterScreen.cs` — 103 行 · `GwpDispatchBarterScreen`
- `GwpDualWieldingPatch.cs` — 100 行 · `GwpDualWieldCollisionPatch`，另含 1 个类型
- `GreyWardenAdoptionLogEntry.cs` — 98 行 · `GreyWardenAdoptionLogEntry` : LogEntry
- `GwpSingleQueryLinkPatch.cs` — 96 行 · `GwpLinkedInquiryState`，另含 2 个类型
- `GwpPartyScreenTroopTalkPatch.cs` — 94 行 · `GwpPartyScreenTroopTalk`，另含 2 个类型
- `GwpDispatchCargo.cs` — 86 行 · `GwpDispatchCargo`，另含 2 个类型
- `GwpSettlementReconsiderationDecision.cs` — 82 行 · `GwpSettlementReconsiderationDecision` : SettlementClaimantDecision
- `GwpAssistanceArmyTooltipPatch.cs` — 74 行 · `GwpAssistanceArmyTooltipPatch`
- `GwpCrimeCategory.cs` — 69 行 · `GwpCrimeCategory`，另含 2 个类型
- `GwpText.cs` — 69 行 · `GwpText`
- `GwpSaveableTypeDefiner.cs` — 65 行 · `GwpSaveableTypeDefiner` : SaveableTypeDefiner
- `GwpEscortSpeedCapPatch.cs` — 60 行 · `GwpEscortSpeedCapPatch`
- `GreyWardenDeserterFilterBehavior.cs` — 59 行 · `GreyWardenDeserterFilterBehavior` : CampaignBehaviorBase
- `GwpNegotiationPolicy.cs` — 56 行 · `GwpNegotiationPolicy`
- `GwpCaseReceipt.cs` — 48 行 · `GwpCaseReceipt`，另含 1 个类型
- `GwpFieldCollectionBarterable.cs` — 47 行 · `GwpFieldCollectionBarterable` : Barterable
- `GwpFieldFineBarterable.cs` — 44 行 · `GwpFieldFineBarterable` : Barterable
- `GwpAdultCommanderLoadoutPatch.cs` — 42 行 · `GwpAdultCommanderLoadoutPatch`
- `GwpGauntletWidgetUtility.cs` — 39 行 · `GwpGauntletWidgetUtility`
- `GwpAssetAutoOfferPatch.cs` — 36 行 · `GwpAssetAutoOfferPatch`
- `GwpDuelOpponentMarkerPatch.cs` — 36 行 · `GwpDuelOpponentMarkerPatch`
- `GwpCrimeDesireAuction.cs` — 35 行 · `GwpCrimeDesireAuction`
- `GreyWardenDutyScheduler.cs` — 33 行 · `GreyWardenDutyScheduler`
- `GwpCaseSettlementEntryPatch.cs` — 33 行 · `GwpCaseSettlementEntryPatch`
- `GwpCaseSettlementRules.cs` — 31 行 · `GwpCaseSettlementRules`
- `GwpPartySizeLimitModel.cs` — 31 行 · `GwpPartySizeLimitModel` : DefaultPartySizeLimitModel
- `GwpTextKeys.cs` — 28 行 · `GwpTextKeys`
- `GwpFlowStates.cs` — 27 行 · `AtonementFlowState`，另含 2 个类型
- `GwpLegacySave.cs` — 27 行 · `GwpLegacySave`
- `GwpDispatchSupplyRules.cs` — 23 行 · `GwpDispatchSupplyRules`
- `GwpPlayerRequestDeferral.cs` — 21 行 · `GwpPlayerRequestDeferral`
- `GwpNegotiationChancePatch.cs` — 18 行 · `GwpNegotiationChancePatch`
- `GwpBlackLordShieldBehavior.cs` — 14 行 · `GwpBlackLordShieldWeaponDataPatch`
