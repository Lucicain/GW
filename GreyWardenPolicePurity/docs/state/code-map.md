# 代码地图（自动生成）

> **这个文件由 `tools/Generate-CodeMap.py` 生成，不要手改。**
> 源码改了就重新生成：`python tools/Generate-CodeMap.py`
> 它描述代码**的形状**，不描述行为——行为看同目录其他 state 文件。
>
> **读法：前两节（补丁点、注册顺序）通读；「类型索引」「文件索引」是查表用的，
> 用 grep 查某个类型或文件，不要整份读进上下文。**

172 个源文件、52,809 行、332 个公开类型、74 个 Harmony 补丁点（分布在 74 个补丁类里）。

## 原版接触面（Harmony 补丁）

**这张表是这个模组与 Bannerlord 的全部契约。** 换游戏版本时先核对它。

这里数的是源码里 `[HarmonyPatch(typeof(...))]` 的**补丁点**。
`Verify-GameCompat.ps1` 的 `PATCH_OK` 数的是**成功绑定的补丁类**，口径不同，
两个数字不应直接相减。

| 原版类型 | 成员 | 方式 | 补丁类 | 文件 |
|---|---|---|---|---|
| `AchievementsCampaignBehavior` | `CheckAchievementSystemActivity` | Prefix | `GwpAchievementActivityPatch` | GwpAchievementCompatibilityPatch.cs |
| `AgentStatCalculateModel` | `GetEffectiveSkill` | Postfix | `GwpBattleMasteryEffectiveSkillPatch` | GwpAgentStatCalculateModel.cs |
| `AiEngagePartyBehavior` | `AiHourlyTick` | Finalizer/Postfix/Prefix | `GwpAssistanceArmyNativeEngageDesirePatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `AiPartyThinkBehavior` | `PartyHourlyAiTick` | Prefix | `GwpPartyThinkDirectAttackLockPatch` | GwpPartyDutyMovementPatch.cs |
| `AiVisitSettlementBehavior` | `AiHourlyTick` | Postfix | `GwpWardenVisitScorePatch` | GwpWardenVisitDesire.cs |
| `AiVisitSettlementBehavior` | `FillSettlementsToVisitWithDistancesAsDays` | Postfix | `GwpWardenVisitCandidatesPatch` | GwpWardenVisitDesire.cs |
| `AiVisitSettlementBehavior` | `GetApproximateVolunteersCanBeRecruitedDataFromSettlement` | Postfix | `GwpWardenVisitVolunteersPatch` | GwpWardenVisitDesire.cs |
| `BarterManager` | `ApplyAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterManager` | `CancelAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionCancelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterManager` | `IsOfferAcceptable` | Postfix | `GwpAssetAcceptancePatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `ExecuteAutoBalance` | Prefix | `GwpAssetAutoOfferPatch` | GwpAssetAutoOfferPatch.cs |
| `BarterVM` | `ExecuteCancel` | Prefix | `GwpDispatchVmCancelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `ExecuteOffer` | Prefix | `GwpAssetOfferValidationPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `RefreshOfferLabel` | Postfix | `GwpDispatchOfferLabelPatch` | GwpAssetOfferValidationPatch.cs |
| `BarterVM` | `SendOffer` | Postfix | `GwpDispatchOfferDisplayPatch` | GwpAssetOfferValidationPatch.cs |
| `BeHostileAction` | `ApplyEncounterHostileAction` | Prefix | `GwpHostilityRelationPatch` | PoliceAntiWarDeclaration.cs |
| `CampaignEventDispatcher` | `AiHourlyTick` | Postfix | `GwpFinalDesireAuctionPatch` | GwpPartyDutyMovementPatch.cs |
| `CharacterHelper` | `CanUseItem` | Prefix | `GwpWardenAutoEquipmentPatch` | GwpWardenAutoEquipmentPatch.cs |
| `CharacterRelationManager` | `SetHeroRelation` | Prefix | `GreyWardenNotableRelationWritePatch` | GreyWardenNotableRelationsBehavior.cs |
| `CommonAIComponent` | `Panic` | Prefix | `GwpWardenPanicPatch` | GwpWardenResolve.cs |
| `CommonAIComponent` | `Retreat` | Prefix | `GwpWardenRetreatPatch` | GwpWardenResolve.cs |
| `CraftingTemplate` | `All` (Getter) | Postfix | `GwpDualBladeCraftingTemplateVisibilityPatch` | GwpDualBladeActionSetPatch.cs |
| `CustomBattleData` | `get_Characters` | Finalizer/Postfix | `GwpCustomBattleCommanderListPatch` | GwpDualBladeActionSetPatch.cs |
| `DefaultArmyManagementCalculationModel` | `CalculateDailyCohesionChange` | Postfix | `GwpAssistanceArmyCohesionPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `DefaultArmyManagementCalculationModel` | `CalculatePartyInfluenceCost` | Prefix | `GwpLeaderlessSupportArmyInfluencePatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `DefaultBattleMissionAgentSpawnLogic` | `IsSideDepleted` | Postfix | `GwpBattleSupportDepletionPatch` | GwpBattleReinforcementBehavior.cs |
| `DefaultMobilePartyAIModel` | `GetBestInitiativeBehavior` | Postfix | `GwpInitiativeDiagnosticsPatch` | GwpPartyDutyMovementPatch.cs |
| `DefaultPersuasionModel` | `GetChances` | Postfix | `GwpNegotiationChancePatch` | GwpNegotiationChancePatch.cs |
| `DestroyPartyAction` | `ApplyForDisbanding` | Prefix | `GwpShipLoanReturnOnDisbandPatch` | PoliceResourceManager.cs |
| `DestroyPartyAction` | `ApplyInternal` | Prefix | `GwpShipLoanReturnPatch` | PoliceResourceManager.cs |
| `DumpIntegrityCampaignBehavior` | `IsGameIntegrityAchieved` | Postfix | `GwpDumpIntegrityAchievementPatch` | GwpAchievementCompatibilityPatch.cs |
| `EncounterGameMenuBehavior` | `army_encounter_background_on_init` | Prefix | `GwpAssistanceArmyEncounterBackgroundPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_item_on_condition` | Prefix | `GwpLeaderlessSupportConversationItemPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_on_condition` | Postfix | `GwpLeaderlessSupportConversationMenuPatch` | GwpAssistanceArmyLifecyclePatch.cs |
| `EnterSettlementAction` | `ApplyForParty` | Prefix | `GwpCaseSettlementEntryPatch` | GwpCaseSettlementEntryPatch.cs |
| `Equipment` | `GetRandomEquipmentElements` | Prefix | `GwpWholePresetEquipmentPatch` | GwpTroopCombat.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaClanPageWidgetPatch` | GwpEncyclopediaClanPageVM.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaHeroPageWidgetPatch` | GwpEncyclopediaHeroPageVM.cs |
| `GauntletMovie` | `Load` | Postfix | `GwpSingleQueryPopupWidgetPatch` | GwpSingleQueryLinkPatch.cs |
| `GiveGoldAction` | `ApplyBetweenCharacters` | Postfix/Prefix | `GwpFireswornUpgradePaymentTracePatch` | GwpFireswornUpgradePayment.cs |
| `HumanAIComponent` | `RefreshBehaviorValues` | Prefix | `GwpWardenRetreatBehaviorPatch` | GwpWardenResolve.cs |
| `KingdomElection` | `DetermineOfficialSupport` | Postfix | `GwpFiefPublicSupportPatch` | GwpFiefPublicSupportPatch.cs |
| `MapEvent` | `LootCasualtyCharacter` | Prefix | `GwpArmoryCasualtyLootPatch` | GwpFireswornArmory.cs |
| `MapInfoVM` | `CreateItems` | Postfix | `GwpMapInfoCreateItemsPatch` | GwpMapBarReputationPatch.cs |
| `MapInfoVM` | `UpdatePlayerInfo` | Postfix | `GwpMapInfoUpdatePatch` | GwpMapBarReputationPatch.cs |
| `MapNavigationHandler` | `OnCreateElements` | Postfix | `GwpAffairsNavigationPatch` | GwpAffairsNavigation.cs |
| `MapScreen` | `OnInitialize` | Postfix | `GwpRitualMapSoundViewPatch` | GwpRitualMapSoundView.cs |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveHeldShieldParticlePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveShieldHitParticlePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `MeleeHitCallback` | Postfix/Prefix | `GwpPassiveHeldShieldMeleePatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `RegisterBlow` | Prefix | `GwpPassiveShieldRegisterBlowPatch` | GwpShieldBashGuardPatch.cs |
| `Mission` | `SpawnAgent` | Prefix | `GwpArmorySpawnPatch` | GwpFireswornArmory.cs |
| `Mission` | `SpawnAgent` | Prefix | `GwpWardenTroopGenderPatch` | GwpWardenTroopGenderPatch.cs |
| `MissionCombatMechanicsHelper` | `ComputeBlowDamage` | Prefix | `GwpDualWieldDamageTypePatch` | GwpDualWieldingPatch.cs |
| `MissionCombatMechanicsHelper` | `IsCollisionBoneDifferentThanWeaponAttachBone` | Postfix | `GwpDualWieldCollisionPatch` | GwpDualWieldingPatch.cs |
| `MissionNameMarkerVM` | `Tick` | Postfix | `GwpDuelOpponentMarkerPatch` | GwpDuelOpponentMarkerPatch.cs |
| `MobileParty` | `CalculateSpeed` | Prefix | `GwpEscortSpeedCapPatch` | GwpEscortSpeedCapPatch.cs |
| `MobileParty` | `GetBehaviorText` | Postfix | `GwpLocationDutyBehaviorTextPatch` | GwpPartyDutyMovementPatch.cs |
| `PartyBase` | `Owner` (Getter) | Postfix | `GwpFireswornUpgradePayerPatch` | GwpFireswornUpgradePayment.cs |
| `PartyCharacterVM` | `ExecuteTalk` | Prefix | `GwpPartyCharacterExecuteTalkPatch` | GwpPartyScreenTroopTalkPatch.cs |
| `PartyCharacterVM` | `UpdateTalkable` | Postfix | `GwpPartyCharacterTalkablePatch` | GwpPartyScreenTroopTalkPatch.cs |
| `PartyUpgraderCampaignBehavior` | `UpgradeReadyTroops` | Finalizer/Prefix | `GwpFireswornUpgradeScopePatch` | GwpFireswornUpgradePayment.cs |
| `PersonaSoftspokenTag` | `IsApplicableTo` | Finalizer | `GwpDispatchBarterFaultDiagnostics` | GwpDispatchBarterFaultDiagnostics.cs |
| `PlayerTownVisitCampaignBehavior` | `game_menu_village_on_init` | Postfix | `GwpFireswornVillageLockPatch` | GwpFireswornMenus.cs |
| `PrisonerReleaseCampaignBehavior` | `DailyHeroTick` | Prefix | `GwpDispatchHeroEscapePatch` | GwpDispatchPrisonerCustody.cs |
| `PrisonerReleaseCampaignBehavior` | `HourlyPartyTick` | Prefix | `GwpDispatchOverflowEscapePatch` | GwpDispatchPrisonerCustody.cs |
| `SetPartyAiAction` | `GetActionForGoingAroundParty` | Prefix | `GwpPlayerEnforcementEngageActionPatch` | GwpPartyDutyMovementPatch.cs |
| `SetPartyAiAction` | `GetActionForPatrollingAroundPoint` | Prefix | `GwpLocationDutyRefreshPatch` | GwpPartyDutyMovementPatch.cs |
| `Settlement` | `RemoveMilitiasFromParty` | Prefix | `GwpFireswornGuardStaysPatch` | GwpFireswornMenus.cs |
| `SettlementNameplateNotificationsVM` | `RegisterEvents` | Postfix | `GwpSettlementNoticesRegisterPatch` | GwpSettlementNotices.cs |
| `SettlementNameplateNotificationsVM` | `UnloadEvents` | Postfix | `GwpSettlementNoticesUnloadPatch` | GwpSettlementNotices.cs |
| `SettlementVisual` | `RefreshPartyIcon` | Postfix/Prefix | `GwpDamnationSmokePatch` | GwpDamnationSmokePatch.cs |
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
- `GwpWardenMarriageBehavior`
- `GreyWardenVillageReconstructionBehavior`
- `GreyWardenIssueResolutionBehavior`
- `GreyWardenTrainingBehavior`
- `GreyWardenPlayerRequestBehavior`
- `GreyWardenLeaderBalanceBehavior`
- `GreyWardenNotableRelationsBehavior`
- `GreyWardenTroopRequestBehavior`
- `GreyWardenSparringBehavior`
- `GwpFireswornBehavior`
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
- `GwpLeaderlessFoodModel`
- `GwpFireswornMilitiaModel`
- `GwpFireswornVolunteerModel`
- `GwpWardenWageModel`
- `GwpFaithBattleRewardModel`
- `GwpFaithCombatSimulationModel`
- `GwpFaithHealingModel`

**任务行为**，按注册顺序（顺序影响生效时机）：

- `GwpDualBladeAiBehavior`
- `GwpWarhorseDamageTransferBehavior`
- `GwpKickBehavior`
- `GwpAlternativeAttackControlBehavior`
- `GwpPassiveShieldBreakBehavior`
- `GwpBattleSceneContext`
- `GwpSyndicateMusicBehavior`
- `GwpBattleReinforcementBehavior`

## 类型索引

**文件名不预测内容。** `CrimePool` 在 `GwpData.cs`、`GwpFaultTrace` 在
`GwpDualBladeActionSetPatch.cs`、`GwpPassiveShieldBreakBehavior` 在
`GwpShieldBashGuardPatch.cs`。要找一个类型就查这张表，不要照名字猜文件。

- `AssistanceTaskSnapshot` → PoliceEnforcementBehavior.Assistance.cs ←
- `AtonementFlowState` → GwpFlowStates.cs ←
- `AtonementQuest` → PoliceEnforcementBehavior.AtonementQuest.cs
- `Bounty` → GwpTuning.cs ←
- `BountyHunterQuest` → PlayerBountyBehavior.QuestAndNotification.cs ←
- `BountyMapNotification` → PlayerBountyBehavior.QuestAndNotification.cs ←
- `BountyMapNotificationItemVM` → PlayerBountyBehavior.QuestAndNotification.cs ←
- `CaseTaskEnd` → PoliceEnforcementBehavior.CaseLifecycle.cs ←
- `Clip` → GwpMusicScore.cs ←
- `CrimePool` → GwpData.cs ←
- `CrimeRecord` → GwpData.cs ←
- `CrimeState` → GwpRuntimeState.cs ←
- `Deterrence` → GwpTuning.cs ←
- `DeterrenceSource` → GwpAiDeterrenceDialogueCatalog.cs ←
- `DeterrenceTier` → GwpAiDeterrenceDialogueCatalog.cs ←
- `DeterrenceVoice` → GwpAiDeterrenceDialogueCatalog.cs ←
- `DutyKind` → GreyWardenFamilyBehavior.cs ←
- `Enforcement` → GwpTuning.cs ←
- `Entry` → GwpDispatchCargo.cs ←
- `Envelope` → GwpMusicScore.cs ←
- `FaithPartyKind` → GwpFireswornBehavior.cs ←
- `Family` → GwpTuning.cs ←
- `FiefRequestStage` → GreyWardenPlayerRequestBehavior.cs ←
- `FieldArrest` → GwpTuning.cs ←
- `Firesworn` → GwpTuning.cs ←
- `GreyWardenAdoptionLogEntry` → GreyWardenAdoptionLogEntry.cs
- `GreyWardenDeserterFilterBehavior` → GreyWardenDeserterFilterBehavior.cs
- `GreyWardenDesertersCampaignBehavior` → GreyWardenDesertersCampaignBehavior.cs
- `GreyWardenDutyScheduler` → GreyWardenDutyScheduler.cs
- `GreyWardenFamilyBehavior` → GreyWardenFamilyBehavior.cs
- `GreyWardenFieldSparringMissionController` → GreyWardenFieldSparringMissionController.cs
- `GreyWardenIssueResolutionBehavior` → GreyWardenIssueResolutionBehavior.cs
- `GreyWardenLeaderBalanceBehavior` → GreyWardenLeaderBalanceBehavior.cs
- `GreyWardenLoreBehavior` → GreyWardenLoreBehavior.cs
- `GreyWardenNotableRelationActionPatch` → GreyWardenNotableRelationsBehavior.cs ←
- `GreyWardenNotableRelationsBehavior` → GreyWardenNotableRelationsBehavior.cs
- `GreyWardenNotableRelationWritePatch` → GreyWardenNotableRelationsBehavior.cs ←
- `GreyWardenPartyDesireBehavior` → GreyWardenPartyDesireBehavior.cs
- `GreyWardenPlayerRequestBehavior` → GreyWardenPlayerRequestBehavior.cs
- `GreyWardenSafePartyAgentOrigin` → GreyWardenSafeTroopSupplier.cs ←
- `GreyWardenSafeTroopSupplier` → GreyWardenSafeTroopSupplier.cs
- `GreyWardenSparringBehavior` → GreyWardenSparringBehavior.cs
- `GreyWardenTrainingBehavior` → GreyWardenTrainingBehavior.cs
- `GreyWardenTroopRequestBehavior` → GreyWardenTroopRequestBehavior.Cohort.cs , GreyWardenTroopRequestBehavior.cs
- `GreyWardenVillageAdoptionBehavior` → GreyWardenVillageAdoptionBehavior.cs
- `GreyWardenVillageReconstructionBehavior` → GreyWardenVillageReconstructionBehavior.cs
- `GreyWardenVillageRewardBehavior` → GreyWardenVillageRewardBehavior.cs
- `GreyWardenVillageRewardSliderScreen` → GreyWardenVillageRewardSliderScreen.cs
- `GreyWardenVillageRewardSliderVM` → GreyWardenVillageRewardSliderScreen.cs ←
- `GwpAchievementActivityPatch` → GwpAchievementCompatibilityPatch.cs ←
- `GwpAchievementCompatibility` → GwpAchievementCompatibilityPatch.cs
- `GwpAdultCommanderLoadoutPatch` → GwpAdultCommanderLoadoutPatch.cs
- `GwpAffairsIcon` → GwpAffairsNavigation.cs ←
- `GwpAffairsNavigationElement` → GwpAffairsNavigation.cs ←
- `GwpAffairsNavigationPatch` → GwpAffairsNavigation.cs ←
- `GwpAgentApplyDamageModel` → GwpAgentApplyDamageModel.cs
- `GwpAgentStatCalculateModel` → GwpAgentStatCalculateModel.cs
- `GwpAiDeterrenceDialogueCatalog` → GwpAiDeterrenceDialogueCatalog.cs
- `GwpAiDeterrenceState` → GwpAiDeterrenceState.cs
- `GwpAiDiagnostics` → GwpAiDiagnostics.cs , GwpAiDiagnostics.cs
- `GwpAlternativeAttackControl` → GwpAlternativeAttackControl.cs
- `GwpAlternativeAttackControlBehavior` → GwpAlternativeAttackControlBehavior.cs
- `GwpArmoryCasualtyLootPatch` → GwpFireswornArmory.cs ←
- `GwpArmorySpawnPatch` → GwpFireswornArmory.cs ←
- `GwpArmyExitDisorganizedPatch` → GwpArmyExitDisorganizedPatch.cs
- `GwpAssetAcceptancePatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpAssetAutoOfferPatch` → GwpAssetAutoOfferPatch.cs
- `GwpAssetOfferValidationPatch` → GwpAssetOfferValidationPatch.cs
- `GwpAssetPayment` → GwpAssetPayment.cs
- `GwpAssistanceArmyCohesionPatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpAssistanceArmyDisbandGuardPatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpAssistanceArmyEncounterBackgroundPatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpAssistanceArmyNativeEngageDesirePatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpAssistanceArmyTooltipPatch` → GwpAssistanceArmyTooltipPatch.cs
- `GwpBattleMasteryEffectiveSkillPatch` → GwpAgentStatCalculateModel.cs ←
- `GwpBattleReinforcementBehavior` → GwpBattleReinforcementBehavior.cs
- `GwpBattleSceneContext` → GwpBattleSceneContext.cs
- `GwpBattleScenePolicy` → GwpBattleScenePolicy.cs
- `GwpBattleSupportChecks` → GwpBattleScenePolicy.cs ←
- `GwpBattleSupportDepletionPatch` → GwpBattleReinforcementBehavior.cs ←
- `GwpBattleSupportOrigin` → GwpBattleSupportOrigin.cs
- `GwpBellumExileCandidatePatch` → GwpKingdomMembershipPatch.cs ←
- `GwpBribeBarterable` → GwpBribeBarterable.cs
- `GwpCaseArchiveItemVM` → GwpCaseArchiveScreen.cs ←
- `GwpCaseArchiveScreen` → GwpCaseArchiveScreen.cs
- `GwpCaseArchiveVM` → GwpCaseArchiveScreen.cs ←
- `GwpCaseReceipt` → GwpCaseReceipt.cs
- `GwpCaseSettlementEntryPatch` → GwpCaseSettlementEntryPatch.cs
- `GwpCaseSettlementRules` → GwpCaseSettlementRules.cs
- `GwpCommon` → GwpCommon.cs
- `GwpCrimeCategory` → GwpCrimeCategory.cs
- `GwpCrimeCategoryClassifier` → GwpCrimeCategory.cs ←
- `GwpCrimeDesireAuction` → GwpCrimeDesireAuction.cs
- `GwpCustomBattleCommanderListPatch` → GwpDualBladeActionSetPatch.cs ←
- `GwpCustomBattleCommanderListSupport` → GwpDualBladeActionSetPatch.cs ←
- `GwpDamnationSmokePatch` → GwpDamnationSmokePatch.cs
- `GwpDispatchBarterFaultDiagnostics` → GwpDispatchBarterFaultDiagnostics.cs
- `GwpDispatchBarterScreen` → GwpDispatchBarterScreen.cs
- `GwpDispatchCargo` → GwpDispatchCargo.cs
- `GwpDispatchHeroEscapePatch` → GwpDispatchPrisonerCustody.cs ←
- `GwpDispatchOfferDisplayPatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpDispatchOfferLabelPatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpDispatchOverflowEscapePatch` → GwpDispatchPrisonerCustody.cs ←
- `GwpDispatchPhase` → GwpWardenDispatch.cs ←
- `GwpDispatchPurpose` → GwpWardenDispatch.cs ←
- `GwpDispatchRecord` → GwpWardenDispatch.cs ←
- `GwpDispatchSelectionCancelPatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpDispatchSelectionPatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpDispatchVmCancelPatch` → GwpAssetOfferValidationPatch.cs ←
- `GwpDualBladeActionGate` → GwpDualBladeActionGate.cs
- `GwpDualBladeAgents` → GwpDualBladeAiBehavior.cs ←
- `GwpDualBladeAgentState` → GwpDualBladeAiBehavior.cs ←
- `GwpDualBladeAiBehavior` → GwpDualBladeAiBehavior.cs
- `GwpDualBladeAttackArmor` → GwpDualBladeAttackArmor.cs
- `GwpDualBladeCraftingTemplateVisibilityPatch` → GwpDualBladeActionSetPatch.cs ←
- `GwpDualBladeFightGripComponent` → GwpDualBladeAiBehavior.cs ←
- `GwpDualBladeGroundPickup` → GwpDualBladeGroundPickupPatch.cs
- `GwpDualBladeGroundPickupPatch` → GwpDualBladeGroundPickupPatch.cs
- `GwpDualBladeItemSetup` → GwpDualBladeItemSetup.cs
- `GwpDualBladeLoadout` → GwpDualBladeActionSetPatch.cs ←
- `GwpDualBladePairKeeperComponent` → GwpDualBladeAiBehavior.cs ←
- `GwpDualWieldCollisionPatch` → GwpDualWieldingPatch.cs ←
- `GwpDualWieldDamageTypePatch` → GwpDualWieldingPatch.cs ←
- `GwpDuelOpponentMarkerPatch` → GwpDuelOpponentMarkerPatch.cs
- `GwpDumpIntegrityAchievementPatch` → GwpAchievementCompatibilityPatch.cs ←
- `GwpEncyclopediaClanPageExtension` → GwpEncyclopediaClanPageVM.cs ←
- `GwpEncyclopediaClanPageExtensionPatch` → GwpEncyclopediaClanPageVM.cs ←
- `GwpEncyclopediaClanPageWidgetPatch` → GwpEncyclopediaClanPageVM.cs ←
- `GwpEncyclopediaHeroPageExtension` → GwpEncyclopediaHeroPageVM.cs ←
- `GwpEncyclopediaHeroPageExtensionPatch` → GwpEncyclopediaHeroPageVM.cs ←
- `GwpEncyclopediaHeroPageWidgetPatch` → GwpEncyclopediaHeroPageVM.cs ←
- `GwpEngineAssertDiagnostics` → GwpEngineAssertDiagnostics.cs
- `GwpEscortSpeedCapPatch` → GwpEscortSpeedCapPatch.cs
- `GwpFaithBattleRewardModel` → GwpFaithBattleRewardModel.cs
- `GwpFaithCombatSimulationModel` → GwpFireswornArmory.cs ←
- `GwpFaithHealingModel` → GwpWardenVisitDesire.cs ←
- `GwpFaultTrace` → GwpDualBladeActionSetPatch.cs ←
- `GwpFiefPublicSupportPatch` → GwpFiefPublicSupportPatch.cs
- `GwpFieldArrestBehavior` → GwpFieldArrestBehavior.cs , GwpFieldArrestBehavior.Grace.cs
- `GwpFieldArrestHostility` → GwpFieldArrestBehavior.cs ←
- `GwpFieldArrestLines` → GwpFieldArrestLines.cs
- `GwpFieldArrestPricing` → GwpCrimeCategory.cs ←
- `GwpFieldCollectionBarterable` → GwpFieldCollectionBarterable.cs
- `GwpFieldDialogueVoice` → GwpFieldDialogueVoice.cs
- `GwpFieldFineBarterable` → GwpFieldFineBarterable.cs
- `GwpFieldReportLedger` → GwpFieldReportLedger.cs
- `GwpFinalDesireAuctionPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpFireswornArmoryView` → GwpFireswornArmoryView.cs
- `GwpFireswornArt` → GwpFireswornArt.cs
- `GwpFireswornBehavior` → GwpFireswornArmory.cs , GwpFireswornBehavior.cs , GwpFireswornDungeon.cs , GwpFireswornExperience.cs , GwpFireswornParties.cs , GwpFireswornRituals.cs
- `GwpFireswornGuardStaysPatch` → GwpFireswornMenus.cs ←
- `GwpFireswornMenus` → GwpFireswornMenus.cs
- `GwpFireswornMilitiaModel` → GwpFireswornMenus.cs ←
- `GwpFireswornPartyDialogs` → GwpFireswornPartyDialogs.cs
- `GwpFireswornSoundMenuActivatePatch` → GwpFireswornSoundView.cs ←
- `GwpFireswornSoundView` → GwpFireswornSoundView.cs
- `GwpFireswornUpgradePayerPatch` → GwpFireswornUpgradePayment.cs ←
- `GwpFireswornUpgradePayment` → GwpFireswornUpgradePayment.cs
- `GwpFireswornUpgradePaymentTracePatch` → GwpFireswornUpgradePayment.cs ←
- `GwpFireswornUpgradeScopePatch` → GwpFireswornUpgradePayment.cs ←
- `GwpFireswornVillageLockPatch` → GwpFireswornMenus.cs ←
- `GwpFireswornVolunteerModel` → GwpWardenRecruitment.cs ←
- `GwpFlacReader` → GwpFlacReader.cs
- `GwpGauntletWidgetUtility` → GwpGauntletWidgetUtility.cs
- `GwpHostilityRelationPatch` → PoliceAntiWarDeclaration.cs ←
- `GwpIds` → GwpIds.cs
- `GwpInitiativeDiagnosticsPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpKickBehavior` → GwpKickBehavior.cs
- `GwpKickInputComponent` → GwpKickInputComponent.cs
- `GwpKingdomMembershipPatch` → GwpKingdomMembershipPatch.cs
- `GwpLandlessShipPurchaseTownPatch` → GwpLandlessShipTradePatch.cs ←
- `GwpLandlessShipSaleTownPatch` → GwpLandlessShipTradePatch.cs ←
- `GwpLandlessShipTrade` → GwpLandlessShipTradePatch.cs
- `GwpLeaderlessFoodModel` → GwpLeaderlessFoodModel.cs
- `GwpLeaderlessSupportArmyInfluencePatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpLeaderlessSupportConversationItemPatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpLeaderlessSupportConversationMenuPatch` → GwpAssistanceArmyLifecyclePatch.cs ←
- `GwpLinkedInquiryState` → GwpSingleQueryLinkPatch.cs ←
- `GwpLocationDutyBehaviorTextPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpLocationDutyRefreshPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpMapBarReputation` → GwpMapBarReputationPatch.cs
- `GwpMapInfoCreateItemsPatch` → GwpMapBarReputationPatch.cs ←
- `GwpMapInfoUpdatePatch` → GwpMapBarReputationPatch.cs ←
- `GwpMusicBattlePolicy` → GwpMusicBattlePolicy.cs
- `GwpMusicLimiter` → GwpMusicOutput.cs ←
- `GwpMusicOutput` → GwpMusicOutput.cs
- `GwpMusicScore` → GwpMusicScore.cs
- `GwpNavalCustomBattleCommanderListPatch` → GwpDualBladeActionSetPatch.cs ←
- `GwpNegotiationChancePatch` → GwpNegotiationChancePatch.cs
- `GwpNegotiationPolicy` → GwpNegotiationPolicy.cs
- `GwpNoticeItemVM` → GwpSettlementNotices.cs ←
- `GwpOffenderDesire` → GwpOffenderDesire.cs
- `GwpOffenderDesires` → GwpOffenderDesire.cs ←
- `GwpPartyCharacterExecuteTalkPatch` → GwpPartyScreenTroopTalkPatch.cs ←
- `GwpPartyCharacterTalkablePatch` → GwpPartyScreenTroopTalkPatch.cs ←
- `GwpPartyIdentity` → GwpPartyIdentity.cs
- `GwpPartyScreenTroopTalk` → GwpPartyScreenTroopTalkPatch.cs
- `GwpPartySizeLimitModel` → GwpPartySizeLimitModel.cs
- `GwpPartyThinkDirectAttackLockPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpPassiveHeldShieldCollision` → GwpShieldBashGuardPatch.cs ←
- `GwpPassiveHeldShieldMeleePatch` → GwpShieldBashGuardPatch.cs ←
- `GwpPassiveHeldShieldParticlePatch` → GwpShieldBashGuardPatch.cs ←
- `GwpPassiveShieldBreakBehavior` → GwpShieldBashGuardPatch.cs ←
- `GwpPassiveShieldHitParticlePatch` → GwpShieldBashGuardPatch.cs ←
- `GwpPassiveShieldRegisterBlowPatch` → GwpShieldBashGuardPatch.cs ←
- `GwpPeace` → GwpPeace.cs
- `GwpPlayerEnforcementEngageActionPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpPlayerRequestDeferral` → GwpPlayerRequestDeferral.cs
- `GwpPoliceWarReasonService` → GwpPoliceWarReasonService.cs
- `GwpRitualMapSoundView` → GwpRitualMapSoundView.cs
- `GwpRitualMapSoundViewPatch` → GwpRitualMapSoundView.cs ←
- `GwpRitualSoundOutput` → GwpRitualSoundOutput.cs
- `GwpRuntimeFaultWatch` → GwpRuntimeFaultWatch.cs
- `GwpRuntimeState` → GwpRuntimeState.cs
- `GwpSaveableTypeDefiner` → GwpSaveableTypeDefiner.cs
- `GwpSettlementNotices` → GwpSettlementNotices.cs
- `GwpSettlementNoticesRegisterPatch` → GwpSettlementNotices.cs ←
- `GwpSettlementNoticesUnloadPatch` → GwpSettlementNotices.cs ←
- `GwpSettlementReconsiderationDecision` → GwpSettlementReconsiderationDecision.cs
- `GwpShipLoanReturnOnDisbandPatch` → PoliceResourceManager.cs ←
- `GwpShipLoanReturnPatch` → PoliceResourceManager.cs ←
- `GwpSingleQueryPopupClearPatch` → GwpSingleQueryLinkPatch.cs ←
- `GwpSingleQueryPopupWidgetPatch` → GwpSingleQueryLinkPatch.cs ←
- `GwpStrength` → GwpStrength.cs
- `GwpSyndicateMusicBehavior` → GwpSyndicateMusicBehavior.cs
- `GwpText` → GwpText.cs
- `GwpTextKeys` → GwpTextKeys.cs
- `GwpTextureSprite` → GwpAffairsNavigation.cs ←
- `GwpTroopCombat` → GwpTroopCombat.cs
- `GwpTroopTransfer` → GwpTroopTransfer.cs
- `GwpTuning` → GwpTuning.cs
- `GwpWardenAutoEquipmentPatch` → GwpWardenAutoEquipmentPatch.cs
- `GwpWardenDispatchBehavior` → GwpWardenDispatchBehavior.cs
- `GwpWardenDispatchDialogue` → GwpWardenDispatchDialogue.cs
- `GwpWardenDuties` → GwpWardenDuties.cs
- `GwpWardenMarriageBehavior` → GwpWardenMarriageBehavior.cs
- `GwpWardenPanicPatch` → GwpWardenResolve.cs ←
- `GwpWardenRecruitment` → GwpWardenRecruitment.cs
- `GwpWardenResolve` → GwpWardenResolve.cs
- `GwpWardenRetreatBehaviorPatch` → GwpWardenResolve.cs ←
- `GwpWardenRetreatPatch` → GwpWardenResolve.cs ←
- `GwpWardenTroopGenderPatch` → GwpWardenTroopGenderPatch.cs
- `GwpWardenVisitCandidatesPatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenVisitDesire` → GwpWardenVisitDesire.cs
- `GwpWardenVisitScorePatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenVisitVolunteersPatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenWageModel` → GwpWardenRecruitment.cs ←
- `GwpWarhorseDamageTransferBehavior` → GwpTroopCombat.cs ←
- `GwpWeaponTrait` → GwpTroopCombat.cs ←
- `GwpWeaponTraits` → GwpTroopCombat.cs ←
- `GwpWholePresetEquipmentPatch` → GwpTroopCombat.cs ←
- `HeroCrimeStats` → GwpData.cs ←
- `IssueDutyStage` → GreyWardenIssueResolutionBehavior.cs ←
- `IssueResolution` → GwpTuning.cs ←
- `IssueTaskSnapshot` → GreyWardenIssueResolutionBehavior.cs ←
- `Item` → GwpCaseReceipt.cs ←
- `LocalBalance` → GwpStrength.cs ←
- `Patrol` → GwpTuning.cs ←
- `PaymentScope` → GwpFireswornUpgradePayment.cs ←
- `PendingReport` → GwpFieldReportLedger.cs ←
- `PlayerBehaviorMonitor` → PlayerBehaviorMonitor.cs
- `PlayerBehaviorPool` → GwpData.cs ←
- `PlayerBountyBehavior` → PlayerBountyBehavior.CaseSettlement.cs , PlayerBountyBehavior.cs , PlayerBountyBehavior.DialogueAndNotification.cs , PlayerBountyBehavior.Encyclopedia.cs , PlayerBountyBehavior.QuestAndNotification.cs , PlayerBountyBehavior.Support.cs
- `PlayerBountyFlowState` → GwpFlowStates.cs ←
- `PlayerRequests` → GwpTuning.cs ←
- `PlayerRequestTaskSnapshot` → GreyWardenPlayerRequestBehavior.cs ←
- `PlayerState` → GwpRuntimeState.cs ←
- `PlayerTroopOrderSnapshot` → GreyWardenTroopRequestBehavior.cs ←
- `PlayerTroopOrderStage` → GreyWardenTroopRequestBehavior.cs ←
- `PoliceAIDeterrenceBehavior` → PoliceAIDeterrenceBehavior.cs
- `PoliceAntiRecruitmentModel` → PoliceAntiRecruitmentModel.cs
- `PoliceAntiVanillaWarBehavior` → PoliceAntiVanillaWarBehavior.cs
- `PoliceAntiWarDeclaration` → PoliceAntiWarDeclaration.cs
- `PoliceClanTierModel` → PoliceClanTierModel.cs
- `PoliceCrimeMonitorEnhanced` → PoliceCrimeMonitorEnhanced.cs
- `PoliceEnforcementBehavior` → PoliceEnforcementBehavior.Assistance.cs , PoliceEnforcementBehavior.AtonementQuest.cs , PoliceEnforcementBehavior.CaseLifecycle.cs , PoliceEnforcementBehavior.cs , PoliceEnforcementBehavior.DelayPatrols.cs , PoliceEnforcementBehavior.Dialogue.cs , PoliceEnforcementBehavior.Envoy.cs , PoliceEnforcementBehavior.Helpers.cs , PoliceEnforcementBehavior.IdleDuties.cs , PoliceEnforcementBehavior.WarPursuitTrace.cs
- `PoliceHeroCreationModel` → PoliceHeroCreationModel.cs
- `PoliceMarriageModel` → PoliceMarriageModel.cs
- `PoliceMobilePartyAIModel` → PoliceMobilePartyAIModel.cs
- `PolicePartyTroopUpgradeModel` → PolicePartyTroopUpgradeModel.cs
- `PolicePatrolBehavior` → PolicePatrolBehavior.cs , PolicePatrolBehavior.Encyclopedia.cs , PolicePatrolBehavior.Helpers.cs
- `PolicePrisonerImmunityBehavior` → PolicePrisonerImmunityBehavior.cs
- `PoliceRaidDeterrenceModel` → PoliceRaidDeterrenceModel.cs
- `PoliceResourceManager` → PoliceResourceManager.cs
- `PoliceShipDamageModel` → PoliceShipModels.cs ←
- `PoliceShipModelSupport` → PoliceShipModels.cs ←
- `PoliceShipParametersModel` → PoliceShipModels.cs ←
- `PoliceStats` → GwpData.cs ←
- `PoliceTask` → GwpData.cs ←
- `PoliceTaskFlowState` → GwpFlowStates.cs ←
- `Reconstruction` → GwpTuning.cs ←
- `ReconstructionStage` → GreyWardenVillageReconstructionBehavior.cs ←
- `ReconstructionTaskSnapshot` → GreyWardenVillageReconstructionBehavior.cs ←
- `ReliefStage` → GreyWardenVillageAdoptionBehavior.cs ←
- `RitualKind` → GwpFireswornBehavior.cs ←
- `Step` → GwpDualBladeAiBehavior.cs ←
- `SubModule` → SubModule.cs
- `Temperament` → GwpFieldArrestLines.cs ←
- `Threat` → GwpStrength.cs ←
- `Training` → GwpTuning.cs ←
- `TrainingTaskSnapshot` → GreyWardenTrainingBehavior.cs ←
- `TrainingTaskStage` → GreyWardenTrainingBehavior.cs ←
- `TroopRequest` → GwpTuning.cs ←
- `VillageReliefTaskSnapshot` → GreyWardenVillageAdoptionBehavior.cs ←
- `VillageReward` → GwpTuning.cs ←
- `Voice` → GwpMusicScore.cs ←
- `WardenDuty` → GwpWardenDuties.cs ←
- `WardenStandingTier` → GwpFieldArrestBehavior.cs ←

`←` 标出文件名里找不到该类型名的 171 个，它们靠 Glob 找不到。

## 文件索引

### 音乐（4 个文件）

- `GwpMusicScore.cs` — 378 行 · `GwpMusicScore` : IDisposable，另含 3 个类型
- `GwpMusicOutput.cs` — 175 行 · `GwpMusicOutput` : IDisposable，另含 1 个类型
- `GwpSyndicateMusicBehavior.cs` — 161 行 · `GwpSyndicateMusicBehavior` : MissionBehavior
- `GwpMusicBattlePolicy.cs` — 113 行 · `GwpMusicBattlePolicy`

### 战场援军（4 个文件）

- `GwpBattleReinforcementBehavior.cs` — 243 行 · `GwpBattleReinforcementBehavior` : MissionBehavior，另含 1 个类型
- `GwpBattleScenePolicy.cs` — 74 行 · `GwpBattleScenePolicy`，另含 1 个类型
- `GwpBattleSceneContext.cs` — 73 行 · `GwpBattleSceneContext` : MissionBehavior
- `GwpBattleSupportOrigin.cs` — 37 行 · `GwpBattleSupportOrigin` : IAgentOriginBase

### 战场：死战不退（1 个文件）

- `GwpWardenResolve.cs` — 64 行 · `GwpWardenResolve`，另含 3 个类型

### 战场：双刀/近战（11 个文件）

- `GwpShieldBashGuardPatch.cs` — 1212 行 · `GwpPassiveHeldShieldCollision` : MissionBehavior，另含 5 个类型
- `GwpDualBladeAiBehavior.cs` — 753 行 · `GwpDualBladeAgentState` : MissionBehavior，另含 5 个类型
- `GwpAlternativeAttackControlBehavior.cs` — 358 行 · `GwpAlternativeAttackControlBehavior` : MissionBehavior
- `GwpDualBladeActionSetPatch.cs` — 314 行 · `GwpFaultTrace`，另含 5 个类型
- `GwpKickInputComponent.cs` — 216 行 · `GwpKickInputComponent` : AgentComponent
- `GwpAlternativeAttackControl.cs` — 177 行 · `GwpAlternativeAttackControl`
- `GwpDualBladeGroundPickupPatch.cs` — 168 行 · `GwpDualBladeGroundPickup`，另含 1 个类型
- `GwpKickBehavior.cs` — 85 行 · `GwpKickBehavior` : MissionBehavior
- `GwpDualBladeItemSetup.cs` — 63 行 · `GwpDualBladeItemSetup`
- `GwpDualBladeActionGate.cs` — 61 行 · `GwpDualBladeActionGate`
- `GwpDualBladeAttackArmor.cs` — 42 行 · `GwpDualBladeAttackArmor`

### 诊断（4 个文件）

- `GwpAiDiagnostics.cs` — 593 行 · `GwpAiDiagnostics`，另含 1 个类型
- `GwpRuntimeFaultWatch.cs` — 87 行 · `GwpRuntimeFaultWatch`
- `GwpEngineAssertDiagnostics.cs` — 34 行 · `GwpEngineAssertDiagnostics`
- `GwpDispatchBarterFaultDiagnostics.cs` — 23 行 · `GwpDispatchBarterFaultDiagnostics`

### 执法：案件与巡逻（32 个文件）

- `PoliceEnforcementBehavior.Assistance.cs` — 1896 行 · `PoliceEnforcementBehavior`，另含 1 个类型
- `PoliceEnforcementBehavior.cs` — 1471 行 · `PoliceEnforcementBehavior` : CampaignBehaviorBase
- `GwpFieldArrestBehavior.cs` — 1307 行 · `GwpFieldArrestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `PoliceEnforcementBehavior.DelayPatrols.cs` — 1271 行 · `PoliceEnforcementBehavior`
- `PolicePatrolBehavior.cs` — 1014 行 · `PolicePatrolBehavior` : CampaignBehaviorBase
- `PoliceResourceManager.cs` — 722 行 · `PoliceResourceManager` : CampaignBehaviorBase，另含 2 个类型
- `PoliceEnforcementBehavior.Envoy.cs` — 712 行 · `PoliceEnforcementBehavior`
- `GwpAiDeterrenceState.cs` — 692 行 · `GwpAiDeterrenceState`
- `PoliceEnforcementBehavior.Dialogue.cs` — 586 行 · `PoliceEnforcementBehavior`
- `PoliceEnforcementBehavior.Helpers.cs` — 481 行 · `PoliceEnforcementBehavior`
- `PoliceCrimeMonitorEnhanced.cs` — 459 行 · `PoliceCrimeMonitorEnhanced` : CampaignBehaviorBase
- `PoliceAIDeterrenceBehavior.cs` — 427 行 · `PoliceAIDeterrenceBehavior` : CampaignBehaviorBase
- `PoliceAntiWarDeclaration.cs` — 412 行 · `PoliceAntiWarDeclaration` : CampaignBehaviorBase，另含 1 个类型
- `GwpFieldArrestLines.cs` — 360 行 · `GwpFieldArrestLines`，另含 1 个类型
- `GwpAiDeterrenceDialogueCatalog.cs` — 339 行 · `DeterrenceSource`，另含 3 个类型
- `PolicePatrolBehavior.Helpers.cs` — 311 行 · `PolicePatrolBehavior`
- `PoliceEnforcementBehavior.AtonementQuest.cs` — 254 行 · `PoliceEnforcementBehavior` : QuestBase，另含 1 个类型
- `PolicePrisonerImmunityBehavior.cs` — 121 行 · `PolicePrisonerImmunityBehavior` : CampaignBehaviorBase
- `PoliceEnforcementBehavior.CaseLifecycle.cs` — 120 行 · `PoliceEnforcementBehavior`，另含 1 个类型
- `PoliceShipModels.cs` — 99 行 · `PoliceShipModelSupport` : CampaignShipDamageModel，另含 2 个类型
- `PoliceEnforcementBehavior.WarPursuitTrace.cs` — 86 行 · `PoliceEnforcementBehavior`
- `PolicePartyTroopUpgradeModel.cs` — 76 行 · `PolicePartyTroopUpgradeModel` : DefaultPartyTroopUpgradeModel
- `PoliceAntiVanillaWarBehavior.cs` — 74 行 · `PoliceAntiVanillaWarBehavior` : CampaignBehaviorBase
- `GwpFieldArrestBehavior.Grace.cs` — 71 行 · `GwpFieldArrestBehavior`
- `PoliceMobilePartyAIModel.cs` — 71 行 · `PoliceMobilePartyAIModel` : DefaultMobilePartyAIModel
- `PoliceAntiRecruitmentModel.cs` — 67 行 · `PoliceAntiRecruitmentModel` : DefaultDiplomacyModel
- `PolicePatrolBehavior.Encyclopedia.cs` — 49 行 · `PolicePatrolBehavior`
- `PoliceMarriageModel.cs` — 36 行 · `PoliceMarriageModel` : DefaultMarriageModel
- `PoliceHeroCreationModel.cs` — 33 行 · `PoliceHeroCreationModel` : DefaultHeroCreationModel
- `PoliceClanTierModel.cs` — 32 行 · `PoliceClanTierModel` : DefaultClanTierModel
- `PoliceRaidDeterrenceModel.cs` — 30 行 · `PoliceRaidDeterrenceModel` : DefaultTargetScoreCalculatingModel
- `PoliceEnforcementBehavior.IdleDuties.cs` — 12 行 · `PoliceEnforcementBehavior`

### 执法：玩家悬赏（7 个文件）

- `GwpCaseArchiveScreen.cs` — 948 行 · `GwpCaseArchiveScreen` : ViewModel，另含 2 个类型
- `PlayerBountyBehavior.cs` — 934 行 · `PlayerBountyBehavior` : CampaignBehaviorBase
- `PlayerBountyBehavior.DialogueAndNotification.cs` — 867 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.CaseSettlement.cs` — 679 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.QuestAndNotification.cs` — 187 行 · `PlayerBountyBehavior` : QuestBase，另含 3 个类型
- `PlayerBountyBehavior.Support.cs` — 170 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.Encyclopedia.cs` — 49 行 · `PlayerBountyBehavior`

### 使者与交兵（6 个文件）

- `GreyWardenTroopRequestBehavior.cs` — 1332 行 · `GreyWardenTroopRequestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpWardenDispatchBehavior.cs` — 902 行 · `GwpWardenDispatchBehavior` : CampaignBehaviorBase
- `GwpWardenDispatchDialogue.cs` — 485 行 · `GwpWardenDispatchDialogue`
- `GreyWardenTroopRequestBehavior.Cohort.cs` — 304 行 · `GreyWardenTroopRequestBehavior`
- `GwpWardenDispatch.cs` — 101 行 · `GwpDispatchPurpose`，另含 2 个类型
- `GwpBribeBarterable.cs` — 62 行 · `GwpBribeBarterable` : Barterable

### 练兵与切磋（3 个文件）

- `GreyWardenFieldSparringMissionController.cs` — 2541 行 · `GreyWardenFieldSparringMissionController` : MissionLogic
- `GreyWardenSparringBehavior.cs` — 878 行 · `GreyWardenSparringBehavior` : CampaignBehaviorBase
- `GreyWardenTrainingBehavior.cs` — 664 行 · `GreyWardenTrainingBehavior` : CampaignBehaviorBase，另含 2 个类型

### 家族与村庄（5 个文件）

- `GreyWardenVillageAdoptionBehavior.cs` — 862 行 · `GreyWardenVillageAdoptionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenFamilyBehavior.cs` — 616 行 · `GreyWardenFamilyBehavior` : CampaignBehaviorBase，另含 1 个类型
- `GreyWardenVillageReconstructionBehavior.cs` — 531 行 · `GreyWardenVillageReconstructionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenVillageRewardSliderScreen.cs` — 255 行 · `GreyWardenVillageRewardSliderScreen` : ViewModel，另含 1 个类型
- `GreyWardenVillageRewardBehavior.cs` — 194 行 · `GreyWardenVillageRewardBehavior` : CampaignBehaviorBase

### 战斗数值模型（1 个文件）

- `GwpAgentApplyDamageModel.cs` — 844 行 · `GwpAgentApplyDamageModel` : AgentApplyDamageModel

### 大地图欲望与 AI（3 个文件）

- `PlayerBehaviorMonitor.cs` — 879 行 · `PlayerBehaviorMonitor` : CampaignBehaviorBase
- `GreyWardenPartyDesireBehavior.cs` — 816 行 · `GreyWardenPartyDesireBehavior` : CampaignBehaviorBase
- `GwpArmyExitDisorganizedPatch.cs` — 75 行 · `GwpArmyExitDisorganizedPatch`

### 玩家交互（1 个文件）

- `GreyWardenPlayerRequestBehavior.cs` — 997 行 · `GreyWardenPlayerRequestBehavior` : CampaignBehaviorBase，另含 2 个类型

### 数据与共用（4 个文件）

- `GwpData.cs` — 1125 行 · `CrimeRecord`，另含 5 个类型
- `GwpTuning.cs` — 405 行 · `GwpTuning`，另含 13 个类型
- `GwpCommon.cs` — 352 行 · `GwpCommon`
- `GwpIds.cs` — 94 行 · `GwpIds`

### 入口（1 个文件）

- `SubModule.cs` — 168 行 · `SubModule` : MBSubModuleBase

### 其他（85 个文件）

- `GwpFireswornBehavior.cs` — 1286 行 · `GwpFireswornBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpStrength.cs` — 1049 行 · `GwpStrength`，另含 2 个类型
- `GwpFireswornArmory.cs` — 999 行 · `GwpFireswornBehavior` : CombatSimulationModel，另含 3 个类型
- `GwpFireswornParties.cs` — 687 行 · `GwpFireswornBehavior`
- `GwpTroopCombat.cs` — 445 行 · `GwpWarhorseDamageTransferBehavior` : MissionBehavior，另含 4 个类型
- `GwpEncyclopediaHeroPageVM.cs` — 418 行 · `GwpEncyclopediaHeroPageExtension`，另含 2 个类型
- `GwpFieldReportLedger.cs` — 415 行 · `GwpFieldReportLedger` : CampaignBehaviorBase，另含 1 个类型
- `GreyWardenIssueResolutionBehavior.cs` — 405 行 · `GreyWardenIssueResolutionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpPoliceWarReasonService.cs` — 362 行 · `GwpPoliceWarReasonService`
- `GreyWardenDesertersCampaignBehavior.cs` — 328 行 · `GreyWardenDesertersCampaignBehavior` : CampaignBehaviorBase
- `GwpFireswornRituals.cs` — 315 行 · `GwpFireswornBehavior`
- `GwpWardenVisitDesire.cs` — 279 行 · `GwpWardenVisitDesire` : PartyHealingModel，另含 4 个类型
- `GwpAssetPayment.cs` — 243 行 · `GwpAssetPayment`
- `GwpAssistanceArmyLifecyclePatch.cs` — 242 行 · `GwpAssistanceArmyDisbandGuardPatch`，另含 6 个类型
- `GreyWardenNotableRelationsBehavior.cs` — 232 行 · `GreyWardenNotableRelationsBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpFlacReader.cs` — 217 行 · `GwpFlacReader` : IDisposable
- `GwpAffairsNavigation.cs` — 197 行 · `GwpAffairsNavigationElement` : MapNavigationElementBase，另含 3 个类型
- `GwpFireswornDungeon.cs` — 197 行 · `GwpFireswornBehavior`
- `GwpFireswornMenus.cs` — 194 行 · `GwpFireswornMenus` : SettlementMilitiaModel，另含 3 个类型
- `GreyWardenLeaderBalanceBehavior.cs` — 193 行 · `GreyWardenLeaderBalanceBehavior` : CampaignBehaviorBase
- `GwpEncyclopediaClanPageVM.cs` — 193 行 · `GwpEncyclopediaClanPageExtension`，另含 2 个类型
- `GreyWardenLoreBehavior.cs` — 191 行 · `GreyWardenLoreBehavior` : CampaignBehaviorBase
- `GwpAgentStatCalculateModel.cs` — 190 行 · `GwpAgentStatCalculateModel` : AgentStatCalculateModel，另含 1 个类型
- `GwpPartyDutyMovementPatch.cs` — 189 行 · `GwpFinalDesireAuctionPatch`，另含 5 个类型
- `GreyWardenSafeTroopSupplier.cs` — 188 行 · `GreyWardenSafeTroopSupplier` : IMissionTroopSupplier，另含 1 个类型
- `GwpOffenderDesire.cs` — 178 行 · `GwpOffenderDesire`，另含 1 个类型
- `GwpFaithBattleRewardModel.cs` — 167 行 · `GwpFaithBattleRewardModel` : BattleRewardModel
- `GwpNegotiationPolicy.cs` — 154 行 · `GwpNegotiationPolicy`
- `GwpRuntimeState.cs` — 153 行 · `GwpRuntimeState`，另含 2 个类型
- `GwpFieldDialogueVoice.cs` — 147 行 · `GwpFieldDialogueVoice`
- `GwpFiefPublicSupportPatch.cs` — 145 行 · `GwpFiefPublicSupportPatch`
- `GwpLandlessShipTradePatch.cs` — 144 行 · `GwpLandlessShipTrade`，另含 2 个类型
- `GwpAchievementCompatibilityPatch.cs` — 142 行 · `GwpAchievementCompatibility`，另含 2 个类型
- `GwpWardenDuties.cs` — 140 行 · `WardenDuty`，另含 1 个类型
- `GwpRitualMapSoundView.cs` — 137 行 · `GwpRitualMapSoundView` : MapView，另含 1 个类型
- `GwpFireswornPartyDialogs.cs` — 133 行 · `GwpFireswornPartyDialogs`
- `GwpRitualSoundOutput.cs` — 133 行 · `GwpRitualSoundOutput` : IDisposable
- `GwpMapBarReputationPatch.cs` — 131 行 · `GwpMapBarReputation`，另含 2 个类型
- `GwpPeace.cs` — 115 行 · `GwpPeace`
- `GwpFireswornUpgradePayment.cs` — 107 行 · `GwpFireswornUpgradePayment`，另含 4 个类型
- `GwpAssetOfferValidationPatch.cs` — 105 行 · `GwpAssetOfferValidationPatch`，另含 6 个类型
- `GwpDispatchBarterScreen.cs` — 103 行 · `GwpDispatchBarterScreen`
- `GwpWardenRecruitment.cs` — 103 行 · `GwpWardenRecruitment` : VolunteerModel，另含 2 个类型
- `GreyWardenAdoptionLogEntry.cs` — 98 行 · `GreyWardenAdoptionLogEntry` : LogEntry
- `GwpSingleQueryLinkPatch.cs` — 96 行 · `GwpLinkedInquiryState`，另含 2 个类型
- `GwpDualWieldingPatch.cs` — 93 行 · `GwpDualWieldCollisionPatch`，另含 1 个类型
- `GwpSettlementNotices.cs` — 93 行 · `GwpSettlementNotices` : SettlementNotificationItemBaseVM，另含 3 个类型
- `GwpFireswornArmoryView.cs` — 84 行 · `GwpFireswornArmoryView`
- `GwpFireswornSoundView.cs` — 84 行 · `GwpFireswornSoundView` : MenuView，另含 1 个类型
- `GwpWardenMarriageBehavior.cs` — 84 行 · `GwpWardenMarriageBehavior` : CampaignBehaviorBase
- `GwpSettlementReconsiderationDecision.cs` — 82 行 · `GwpSettlementReconsiderationDecision` : SettlementClaimantDecision
- `GwpPartyScreenTroopTalkPatch.cs` — 72 行 · `GwpPartyScreenTroopTalk`，另含 2 个类型
- `GwpKingdomMembershipPatch.cs` — 71 行 · `GwpKingdomMembershipPatch`，另含 1 个类型
- `GwpCrimeCategory.cs` — 69 行 · `GwpCrimeCategory`，另含 2 个类型
- `GwpText.cs` — 69 行 · `GwpText`
- `GwpAssistanceArmyTooltipPatch.cs` — 66 行 · `GwpAssistanceArmyTooltipPatch`
- `GwpSaveableTypeDefiner.cs` — 65 行 · `GwpSaveableTypeDefiner` : SaveableTypeDefiner
- `GwpFireswornArt.cs` — 64 行 · `GwpFireswornArt`
- `GreyWardenDeserterFilterBehavior.cs` — 59 行 · `GreyWardenDeserterFilterBehavior` : CampaignBehaviorBase
- `GwpDispatchCargo.cs` — 52 行 · `GwpDispatchCargo`，另含 1 个类型
- `GwpFireswornExperience.cs` — 52 行 · `GwpFireswornBehavior`
- `GwpEscortSpeedCapPatch.cs` — 50 行 · `GwpEscortSpeedCapPatch`
- `GwpCaseReceipt.cs` — 48 行 · `GwpCaseReceipt`，另含 1 个类型
- `GwpWardenAutoEquipmentPatch.cs` — 48 行 · `GwpWardenAutoEquipmentPatch`
- `GwpFieldCollectionBarterable.cs` — 47 行 · `GwpFieldCollectionBarterable` : Barterable
- `GwpTroopTransfer.cs` — 47 行 · `GwpTroopTransfer`
- `GwpFieldFineBarterable.cs` — 44 行 · `GwpFieldFineBarterable` : Barterable
- `GwpLeaderlessFoodModel.cs` — 41 行 · `GwpLeaderlessFoodModel` : MobilePartyFoodConsumptionModel
- `GwpWardenTroopGenderPatch.cs` — 41 行 · `GwpWardenTroopGenderPatch`
- `GwpGauntletWidgetUtility.cs` — 39 行 · `GwpGauntletWidgetUtility`
- `GwpAssetAutoOfferPatch.cs` — 36 行 · `GwpAssetAutoOfferPatch`
- `GwpDamnationSmokePatch.cs` — 36 行 · `GwpDamnationSmokePatch`
- `GwpDuelOpponentMarkerPatch.cs` — 36 行 · `GwpDuelOpponentMarkerPatch`
- `GwpPartySizeLimitModel.cs` — 36 行 · `GwpPartySizeLimitModel` : DefaultPartySizeLimitModel
- `GwpCrimeDesireAuction.cs` — 35 行 · `GwpCrimeDesireAuction`
- `GreyWardenDutyScheduler.cs` — 33 行 · `GreyWardenDutyScheduler`
- `GwpCaseSettlementEntryPatch.cs` — 33 行 · `GwpCaseSettlementEntryPatch`
- `GwpAdultCommanderLoadoutPatch.cs` — 32 行 · `GwpAdultCommanderLoadoutPatch`
- `GwpCaseSettlementRules.cs` — 31 行 · `GwpCaseSettlementRules`
- `GwpDispatchPrisonerCustody.cs` — 30 行 · `GwpDispatchHeroEscapePatch`，另含 1 个类型
- `GwpFlowStates.cs` — 27 行 · `AtonementFlowState`，另含 2 个类型
- `GwpPartyIdentity.cs` — 23 行 · `GwpPartyIdentity`
- `GwpTextKeys.cs` — 22 行 · `GwpTextKeys`
- `GwpPlayerRequestDeferral.cs` — 21 行 · `GwpPlayerRequestDeferral`
- `GwpNegotiationChancePatch.cs` — 18 行 · `GwpNegotiationChancePatch`
