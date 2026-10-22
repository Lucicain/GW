# 代码地图（自动生成）

> **这个文件由 `tools/Generate-CodeMap.py` 生成，不要手改。**
> 源码改了就重新生成：`python tools/Generate-CodeMap.py`
> 它描述代码**的形状**，不描述行为——行为看同目录其他 state 文件。
>
> **读法：前两节（补丁点、注册顺序）通读；「类型索引」「文件索引」是查表用的，
> 用 grep 查某个类型或文件，不要整份读进上下文。**

213 个源文件、58,287 行、412 个公开类型、112 条 Harmony 目标声明（含动态入口，共 112 个补丁类）。

## 原版接触面（Harmony 补丁）

源码中的 Harmony 类级声明清单；换游戏版本时先定位这些接触面，再运行加载兼容预检。

静态目标列出类型/成员；`TargetMethod(s)` 动态目标保留入口和源码行号，不猜运行时结果。
包含条件编译内的声明，不执行 `Prepare`。这不是实际生效列表，也不涵盖模型覆盖等非 Harmony 接触面。
`Verify-GameCompat.ps1` 的 `PATCH_OK` 是实际加载绑定的补丁类数，需按构建条件与声明集合核对。

| 原版类型 | 成员 | 方式 | 补丁类 | 文件 |
|---|---|---|---|---|
| `AchievementsCampaignBehavior` | `CheckAchievementSystemActivity` | Prefix | `GwpAchievementActivityPatch` | GwpAchievementCompatibilityPatch.cs:131 |
| `AgentStatCalculateModel` | `GetEffectiveSkill` | Postfix | `GwpBattleMasteryEffectiveSkillPatch` | GwpAgentStatCalculateModel.cs:163 |
| `AiEngagePartyBehavior` | `AiHourlyTick` | Finalizer/Postfix/Prefix | `GwpAssistanceArmyNativeEngageDesirePatch` | GwpAssistanceArmyLifecyclePatch.cs:252 |
| `AiPartyThinkBehavior` | `PartyHourlyAiTick` | Prefix | `GwpPartyThinkDirectAttackLockPatch` | GwpPartyDutyMovementPatch.cs:97 |
| `AiVisitSettlementBehavior` | `AiHourlyTick` | Postfix | `GwpWardenVisitScorePatch` | GwpWardenVisitDesire.cs:226 |
| `AiVisitSettlementBehavior` | `CalculatePartyParameters` | Postfix | `GwpWardenVisitPartyParametersPatch` | GwpWardenVisitDesire.cs:204 |
| `AiVisitSettlementBehavior` | `FillSettlementsToVisitWithDistancesAsDays` | Postfix | `GwpWardenVisitCandidatesPatch` | GwpWardenVisitDesire.cs:215 |
| `AiVisitSettlementBehavior` | `GetApproximateVolunteersCanBeRecruitedDataFromSettlement` | Postfix | `GwpWardenVisitVolunteersPatch` | GwpWardenVisitDesire.cs:241 |
| `BarterManager` | `ApplyAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionPatch` | GwpAssetOfferValidationPatch.cs:46 |
| `BarterManager` | `CancelAndFinalizePlayerBarter` | Prefix | `GwpDispatchSelectionCancelPatch` | GwpAssetOfferValidationPatch.cs:61 |
| `BarterManager` | `IsOfferAcceptable` | Postfix | `GwpAssetAcceptancePatch` | GwpAssetOfferValidationPatch.cs:35 |
| `BarterVM` | `ExecuteAutoBalance` | Prefix | `GwpAssetAutoOfferPatch` | GwpAssetAutoOfferPatch.cs:9 |
| `BarterVM` | `ExecuteCancel` | Prefix | `GwpDispatchVmCancelPatch` | GwpAssetOfferValidationPatch.cs:72 |
| `BarterVM` | `ExecuteOffer` | Prefix | `GwpAssetOfferValidationPatch` | GwpAssetOfferValidationPatch.cs:10 |
| `BarterVM` | `RefreshOfferLabel` | Postfix | `GwpDispatchOfferLabelPatch` | GwpAssetOfferValidationPatch.cs:96 |
| `BarterVM` | `SendOffer` | Postfix | `GwpDispatchOfferDisplayPatch` | GwpAssetOfferValidationPatch.cs:82 |
| `BeHostileAction` | `ApplyEncounterHostileAction` | Prefix | `GwpHostilityRelationPatch` | PoliceAntiWarDeclaration.cs:418 |
| `CampaignEventDispatcher` | `AiHourlyTick` | Postfix | `GwpFinalDesireAuctionPatch` | GwpPartyDutyMovementPatch.cs:22 |
| `CharacterHelper` | `CanUseItem` | Prefix | `GwpWardenAutoEquipmentPatch` | GwpWardenAutoEquipmentPatch.cs:18 |
| `Clan` | `Banner` (Getter) | Postfix | `GwpDutyBannerClanEmblemPatch` | GwpDutyBanner.cs:46 |
| `CommonAIComponent` | `Panic` | Prefix | `GwpWardenPanicPatch` | GwpWardenResolve.cs:26 |
| `CommonAIComponent` | `Retreat` | Prefix | `GwpWardenRetreatPatch` | GwpWardenResolve.cs:37 |
| `ConversationTagHelper` | `EducatedClass` | Postfix | `GwpFounderVoiceRegisterPatch` | GwpFounderVoiceRegisterPatch.cs:14 |
| `CraftingTemplate` | `All` (Getter) | Postfix | `GwpDualBladeCraftingTemplateVisibilityPatch` | GwpDualBladeActionSetPatch.cs:233 |
| `CustomBattleData` | `get_Characters` | Finalizer/Postfix | `GwpCustomBattleCommanderListPatch` | GwpDualBladeActionSetPatch.cs:127 |
| `DefaultArmyManagementCalculationModel` | `CalculateDailyCohesionChange` | Postfix | `GwpAssistanceArmyCohesionPatch` | GwpAssistanceArmyLifecyclePatch.cs:81 |
| `DefaultArmyManagementCalculationModel` | `CalculatePartyInfluenceCost` | Finalizer/Prefix | `GwpLeaderlessSupportArmyInfluencePatch` | GwpAssistanceArmyLifecyclePatch.cs:99 |
| `DefaultArmyManagementCalculationModel` | `CalculateTotalInfluenceCost` | Finalizer | `GwpArmyInfluenceFailureTracePatch` | GwpAssistanceArmyLifecyclePatch.cs:148 |
| `DefaultBattleMissionAgentSpawnLogic` | `IsSideDepleted` | Postfix | `GwpBattleSupportDepletionPatch` | GwpBattleReinforcementBehavior.cs:246 |
| `DefaultMobilePartyAIModel` | `GetBestInitiativeBehavior` | Postfix | `GwpInitiativeDiagnosticsPatch` | GwpPartyDutyMovementPatch.cs:116 |
| `DefaultMobilePartyAIModel` | `GetBestInitiativeBehavior` | Finalizer/Postfix/Prefix/Transpiler | `GwpNativeRegionPatch` | GwpStrength.Native.cs:158 |
| `DefaultPersuasionModel` | `GetChances` | Postfix | `GwpNegotiationChancePatch` | GwpNegotiationChancePatch.cs:8 |
| `DestroyPartyAction` | `ApplyForDisbanding` | Prefix | `GwpShipLoanReturnOnDisbandPatch` | PoliceResourceManager.cs:712 |
| `DestroyPartyAction` | `ApplyInternal` | Prefix | `GwpShipLoanReturnPatch` | PoliceResourceManager.cs:698 |
| `DumpIntegrityCampaignBehavior` | `IsGameIntegrityAchieved` | Postfix | `GwpDumpIntegrityAchievementPatch` | GwpAchievementCompatibilityPatch.cs:109 |
| `EncounterGameMenuBehavior` | `army_encounter_background_on_init` | Prefix | `GwpAssistanceArmyEncounterBackgroundPatch` | GwpAssistanceArmyLifecyclePatch.cs:178 |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_item_on_condition` | Prefix | `GwpLeaderlessSupportConversationItemPatch` | GwpAssistanceArmyLifecyclePatch.cs:200 |
| `EncounterGameMenuBehavior` | `game_menu_army_talk_to_other_members_on_condition` | Postfix | `GwpLeaderlessSupportConversationMenuPatch` | GwpAssistanceArmyLifecyclePatch.cs:226 |
| `Equipment` | `GetRandomEquipmentElements` | Prefix | `GwpWholePresetEquipmentPatch` | GwpTroopCombat.cs:449 |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaClanPageWidgetPatch` | GwpEncyclopediaClanPageVM.cs:84 |
| `GauntletMovie` | `Load` | Postfix | `GwpEncyclopediaHeroPageWidgetPatch` | GwpEncyclopediaHeroPageVM.cs:327 |
| `GauntletMovie` | `Load` | Postfix | `GwpSingleQueryPopupWidgetPatch` | GwpSingleQueryLinkPatch.cs:45 |
| `GiveGoldAction` | `ApplyBetweenCharacters` | Postfix/Prefix | `GwpFireswornUpgradePaymentTracePatch` | GwpFireswornUpgradePayment.cs:85 |
| `HumanAIComponent` | `RefreshBehaviorValues` | Prefix | `GwpWardenRetreatBehaviorPatch` | GwpWardenResolve.cs:52 |
| `KingdomElection` | `DetermineOfficialSupport` | Postfix | `GwpFiefPublicSupportPatch` | GwpFiefPublicSupportPatch.cs:19 |
| `Location` | `CanAIEnter` | Postfix | `GwpCitySearchDuelArenaEntryPatch` | GwpCitySearchMission.Movement.cs:94 |
| `MapEvent` | `LootCasualtyCharacter` | Prefix | `GwpArmoryCasualtyLootPatch` | GwpFireswornArmory.cs:781 |
| `MapEventParty` | `Update` | Postfix/Prefix | `GwpArmoryBattleRosterPatch` | GwpArmoryBattle.cs:184 |
| `MapInfoVM` | `CreateItems` | Postfix | `GwpMapInfoCreateItemsPatch` | GwpMapBarReputationPatch.cs:116 |
| `MapInfoVM` | `UpdatePlayerInfo` | Postfix | `GwpMapInfoUpdatePatch` | GwpMapBarReputationPatch.cs:124 |
| `MapNavigationHandler` | `OnCreateElements` | Postfix | `GwpAffairsNavigationPatch` | GwpAffairsNavigation.cs:72 |
| `MapScreen` | `OnInitialize` | Postfix | `GwpRitualMapSoundViewPatch` | GwpRitualMapSoundView.cs:132 |
| `MenuViewContext` | `TaleWorlds.CampaignSystem.GameState.IMenuContextHandler.OnMenuActivate` | Postfix | `GwpFireswornSoundMenuActivatePatch` | GwpFireswornSoundView.cs:73 |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveHeldShieldParticlePatch` | GwpShieldBashGuardPatch.cs:1134 |
| `Mission` | `DecideAgentHitParticles` | Prefix | `GwpPassiveShieldHitParticlePatch` | GwpShieldBashGuardPatch.cs:1210 |
| `Mission` | `MeleeHitCallback` | Prefix | `GwpCitySearchMeleeStrikePatch` | GwpCitySearchMission.cs:687 |
| `Mission` | `MeleeHitCallback` | Postfix/Prefix | `GwpPassiveHeldShieldMeleePatch` | GwpShieldBashGuardPatch.cs:982 |
| `Mission` | `RegisterBlow` | Prefix | `GwpPassiveShieldRegisterBlowPatch` | GwpShieldBashGuardPatch.cs:1171 |
| `Mission` | `SpawnAgent` | Prefix | `GwpArmorySpawnPatch` | GwpFireswornArmory.cs:794 |
| `Mission` | `SpawnAgent` | Prefix | `GwpWardenTroopGenderPatch` | GwpWardenTroopGenderPatch.cs:20 |
| `MissionCombatMechanicsHelper` | `ComputeBlowDamage` | Prefix | `GwpDualWieldDamageTypePatch` | GwpDualWieldingPatch.cs:47 |
| `MissionCombatMechanicsHelper` | `IsCollisionBoneDifferentThanWeaponAttachBone` | Postfix | `GwpDualWieldCollisionPatch` | GwpDualWieldingPatch.cs:18 |
| `MissionConversationLogic` | `IsThereAgentAction` | Postfix | `GwpCitySearchFollowerTalkPatch` | GwpCitySearchMission.Movement.cs:85 |
| `MissionFightHandler` | `GetDangerSources` | Postfix | `GwpCitySearchCivilianDangerPatch` | GwpCitySearchMission.Progress.cs:344 |
| `MissionLocationLogic` | `SpawnCharactersAccompanyingPlayer` | Prefix | `GwpCitySearchCompanionsPatch` | GwpCitySearchMission.Progress.cs:355 |
| `MissionNameMarkerVM` | `Tick` | Postfix | `GwpDuelOpponentMarkerPatch` | GwpDuelOpponentMarkerPatch.cs:12 |
| `MobileParty` | `Banner` (Getter) | Postfix | `GwpDutyBannerMapEmblemPatch` | GwpDutyBanner.cs:58 |
| `MobileParty` | `CalculateSpeed` | Prefix | `GwpEscortSpeedCapPatch` | GwpEscortSpeedCapPatch.cs:22 |
| `MobileParty` | `GetBehaviorText` | Postfix | `GwpLocationDutyBehaviorTextPatch` | GwpPartyDutyMovementPatch.cs:140 |
| `NameplateVM` | `FactionColor` (Setter) | Prefix | `GwpCaseTargetColorPatch` | GwpCaseTargetNameplatePatch.cs:34 |
| `NotablesCampaignBehavior` | `CheckAndMakeNotableDisappear` | Prefix | `GwpPriestStaysPatch` | GwpFireswornPriestStays.cs:15 |
| `PartyAgentOrigin` | `Banner` (Getter) | Postfix | `GwpDutyBannerPartyEmblemPatch` | GwpDutyBanner.cs:69 |
| `PartyBase` | `Owner` (Getter) | Postfix | `GwpFireswornUpgradePayerPatch` | GwpFireswornUpgradePayment.cs:74 |
| `PartyCharacterVM` | `ExecuteTalk` | Prefix | `GwpPartyCharacterExecuteTalkPatch` | GwpPartyScreenTroopTalkPatch.cs:48 |
| `PartyCharacterVM` | `UpdateTalkable` | Postfix | `GwpPartyCharacterTalkablePatch` | GwpPartyScreenTroopTalkPatch.cs:31 |
| `PartyNameplateVM` | `FullName` (Setter) | Prefix | `GwpCaseTargetNameplatePatch` | GwpCaseTargetNameplatePatch.cs:12 |
| `PartyUpgraderCampaignBehavior` | `UpgradeReadyTroops` | Finalizer/Prefix | `GwpFireswornUpgradeScopePatch` | GwpFireswornUpgradePayment.cs:54 |
| `PassageUsePoint` | `OnUse` | Prefix | `GwpCitySearchPassagePatch` | GwpCitySearchMission.Progress.cs:336 |
| `PersonaSoftspokenTag` | `IsApplicableTo` | Finalizer | `GwpDispatchBarterFaultDiagnostics` | GwpDispatchBarterFaultDiagnostics.cs:11 |
| `PlayerTownVisitCampaignBehavior` | `game_menu_village_on_init` | Postfix | `GwpFireswornVillageLockPatch` | GwpFireswornMenus.cs:129 |
| `PrisonerReleaseCampaignBehavior` | `DailyHeroTick` | Prefix | `GwpDispatchHeroEscapePatch` | GwpDispatchPrisonerCustody.cs:15 |
| `PrisonerReleaseCampaignBehavior` | `HourlyPartyTick` | Prefix | `GwpDispatchOverflowEscapePatch` | GwpDispatchPrisonerCustody.cs:23 |
| `SellGoodsForTradeAction` | `ApplyByVillagerTrade` | Prefix | `GwpFaithVillagerSalePatch` | GwpFaithVillagers.cs:356 |
| `SetPartyAiAction` | `GetActionForGoingAroundParty` | Prefix | `GwpPlayerEnforcementEngageActionPatch` | GwpPartyDutyMovementPatch.cs:68 |
| `SetPartyAiAction` | `GetActionForPatrollingAroundPoint` | Prefix | `GwpLocationDutyRefreshPatch` | GwpPartyDutyMovementPatch.cs:41 |
| `Settlement` | `RemoveMilitiasFromParty` | Prefix | `GwpFireswornGuardStaysPatch` | GwpFireswornMenus.cs:143 |
| `SettlementNameplateNotificationsVM` | `RegisterEvents` | Postfix | `GwpSettlementNoticesRegisterPatch` | GwpSettlementNotices.cs:82 |
| `SettlementNameplateNotificationsVM` | `UnloadEvents` | Postfix | `GwpSettlementNoticesUnloadPatch` | GwpSettlementNotices.cs:88 |
| `SettlementVisual` | `RefreshPartyIcon` | Postfix/Prefix | `GwpDamnationSmokePatch` | GwpDamnationSmokePatch.cs:16 |
| `SingleQueryPopUpVM` | `OnClearData` | Postfix | `GwpSingleQueryPopupClearPatch` | GwpSingleQueryLinkPatch.cs:87 |
| `SpawnedItemEntity` | `OnUseStopped` | Transpiler | `GwpDualBladeGroundPickupPatch` | GwpDualBladeGroundPickupPatch.cs:118 |
| `VillagerCampaignBehavior` | `AddVillagersToParty` | Postfix/Prefix | `GwpFaithVillagerReplenishPatch` | GwpFaithVillagers.cs:305 |
| `VillagerCampaignBehavior` | `CreateVillagerParty` | Postfix | `GwpFaithVillagerBirthPatch` | GwpFaithVillagers.cs:298 |
| `VillagerCampaignBehavior` | `LoadAndSendVillagerParty` | Postfix/Prefix | `GwpFaithVillagerDeparturePatch` | GwpFaithVillagers.cs:318 |
| `VillagerCampaignBehavior` | `MoveItemsToVillagerParty` | Prefix | `GwpFaithVillagerNativeLoadPatch` | GwpFaithVillagers.cs:331 |
| `VillagerCampaignBehavior` | `OnSettlementEntered` | Prefix | `GwpFaithVillagerIncomePatch` | GwpFaithVillagers.cs:362 |
| `VillagerCampaignBehavior` | `ThinkAboutSendingItemToTown` | Transpiler | `GwpFaithVillagerWarehousePatch` | GwpFaithVillagers.cs:338 |
| `动态/未静态解析` | `TargetMethod()` | Postfix | `GwpAdultCommanderLoadoutPatch` | GwpAdultCommanderLoadoutPatch.cs:15 |
| `动态/未静态解析` | `TargetMethod()` | Prefix | `GwpArmyExitDisorganizedPatch` | GwpArmyExitDisorganizedPatch.cs:28 |
| `动态/未静态解析` | `TargetMethod()` | Prefix | `GwpAssistanceArmyDisbandGuardPatch` | GwpAssistanceArmyLifecyclePatch.cs:29 |
| `动态/未静态解析` | `TargetMethod()` | Prefix | `GwpAssistanceArmyTooltipPatch` | GwpAssistanceArmyTooltipPatch.cs:26 |
| `动态/未静态解析` | `TargetMethod()` | Prefix | `GwpBellumExileCandidatePatch` | GwpKingdomMembershipPatch.cs:43 |
| `动态/未静态解析` | `TargetMethod()` | Postfix | `GwpEncyclopediaClanPageExtensionPatch` | GwpEncyclopediaClanPageVM.cs:48 |
| `动态/未静态解析` | `TargetMethod()` | Postfix | `GwpEncyclopediaHeroPageExtensionPatch` | GwpEncyclopediaHeroPageVM.cs:288 |
| `动态/未静态解析` | `TargetMethod()` | Postfix | `GwpLandlessShipPurchaseTownPatch` | GwpLandlessShipTradePatch.cs:109 |
| `动态/未静态解析` | `TargetMethod()` | Postfix | `GwpLandlessShipSaleTownPatch` | GwpLandlessShipTradePatch.cs:127 |
| `动态/未静态解析` | `TargetMethods()` | Transpiler | `GwpCitySearchBodyCarrierPatch` | GwpCitySearchBodyTransport.cs:191 |
| `动态/未静态解析` | `TargetMethods()` | Prefix | `GwpEngineAssertDiagnostics` | GwpEngineAssertDiagnostics.cs:12 |
| `动态/未静态解析` | `TargetMethods()` | Postfix | `GwpInterventionChoicePatch` | GwpPlayerIntervention.cs:131 |
| `动态/未静态解析` | `TargetMethods()` | Prefix | `GwpInterventionSelectionPatch` | GwpPlayerIntervention.cs:143 |
| `动态/未静态解析` | `TargetMethods()` | Prefix | `GwpKingdomMembershipPatch` | GwpKingdomMembershipPatch.cs:13 |
| `动态/未静态解析` | `TargetMethods()` | Finalizer/Postfix | `GwpNavalCustomBattleCommanderListPatch` | GwpDualBladeActionSetPatch.cs:156 |

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
- `GwpCitySearchBehavior`
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
- `GwpDutyBannerItemModel`
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
- `GwpLastStandBehavior`
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
- `BattleGrowth` → GwpTuning.cs ←
- `Bounty` → GwpTuning.cs ←
- `BountyHunterQuest` → PlayerBountyBehavior.QuestAndNotification.cs ←
- `CaseTaskEnd` → PoliceEnforcementBehavior.CaseLifecycle.cs ←
- `CitySearch` → GwpTuning.cs ←
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
- `EnvoyReport` → PoliceEnforcementBehavior.Envoy.cs ←
- `EnvoyStage` → PoliceEnforcementBehavior.Envoy.cs ←
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
- `GwpAffairsChoiceVM` → GwpAffairsVM.cs ←
- `GwpAffairsData` → GwpAffairsData.cs
- `GwpAffairsDispatchVM` → GwpAffairsVM.cs ←
- `GwpAffairsEntry` → GwpAffairsData.cs ←
- `GwpAffairsIcon` → GwpAffairsNavigation.cs ←
- `GwpAffairsLineVM` → GwpAffairsVM.cs ←
- `GwpAffairsNavigationElement` → GwpAffairsNavigation.cs ←
- `GwpAffairsNavigationPatch` → GwpAffairsNavigation.cs ←
- `GwpAffairsSectionVM` → GwpAffairsVM.cs ←
- `GwpAffairsState` → GwpCaseArchiveScreen.cs ←
- `GwpAffairsText` → GwpAffairsData.cs ←
- `GwpAffairsVM` → GwpAffairsVM.cs
- `GwpAgentApplyDamageModel` → GwpAgentApplyDamageModel.cs
- `GwpAgentStatCalculateModel` → GwpAgentStatCalculateModel.cs
- `GwpAiDeterrenceDialogueCatalog` → GwpAiDeterrenceDialogueCatalog.cs
- `GwpAiDeterrenceState` → GwpAiDeterrenceState.cs
- `GwpAiDiagnostics` → GwpAiDiagnostics.cs , GwpAiDiagnostics.cs
- `GwpAlternativeAttackControl` → GwpAlternativeAttackControl.cs
- `GwpAlternativeAttackControlBehavior` → GwpAlternativeAttackControlBehavior.cs
- `GwpArmoryBattleRosterPatch` → GwpArmoryBattle.cs ←
- `GwpArmoryCasualtyLootPatch` → GwpFireswornArmory.cs ←
- `GwpArmorySpawnPatch` → GwpFireswornArmory.cs ←
- `GwpArmyExitDisorganizedPatch` → GwpArmyExitDisorganizedPatch.cs
- `GwpArmyInfluenceFailureTracePatch` → GwpAssistanceArmyLifecyclePatch.cs ←
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
- `GwpCaseArchiveItemVM` → GwpCaseArchiveItemVM.cs
- `GwpCaseArchiveScreen` → GwpCaseArchiveScreen.cs
- `GwpCaseReceipt` → GwpCaseReceipt.cs
- `GwpCaseSettlementRules` → GwpCaseSettlementRules.cs
- `GwpCaseTargetColorPatch` → GwpCaseTargetNameplatePatch.cs ←
- `GwpCaseTargetNameplatePatch` → GwpCaseTargetNameplatePatch.cs
- `GwpCitySearchArenaReturn` → GwpCitySearchBehavior.Flow.cs ←
- `GwpCitySearchBehavior` → GwpCitySearchBehavior.cs , GwpCitySearchBehavior.Flow.cs , GwpCitySearchBehavior.Hall.cs , GwpCitySearchBehavior.Menu.cs
- `GwpCitySearchBodyCarrierPatch` → GwpCitySearchBodyTransport.cs ←
- `GwpCitySearchBodyTransport` → GwpCitySearchBodyTransport.cs
- `GwpCitySearchCivilianDangerPatch` → GwpCitySearchMission.Progress.cs ←
- `GwpCitySearchCompanionsPatch` → GwpCitySearchMission.Progress.cs ←
- `GwpCitySearchDuelArenaEntryPatch` → GwpCitySearchMission.Movement.cs ←
- `GwpCitySearchFollowerTalkPatch` → GwpCitySearchMission.Movement.cs ←
- `GwpCitySearchGate` → GwpCitySearchBehavior.Flow.cs ←
- `GwpCitySearchMeleeStrikePatch` → GwpCitySearchMission.cs ←
- `GwpCitySearchMission` → GwpCitySearchMission.cs
- `GwpCitySearchMissionController` → GwpCitySearchMission.cs , GwpCitySearchMission.Movement.cs , GwpCitySearchMission.Progress.cs , GwpCitySearchMission.Reinforcements.cs ←
- `GwpCitySearchMissionViews` → GwpCitySearchMissionViews.cs
- `GwpCitySearchPassagePatch` → GwpCitySearchMission.Progress.cs ←
- `GwpCitySearchPlace` → GwpCitySearchBehavior.Flow.cs ←
- `GwpCitySearchVisualOrderProvider` → GwpCitySearchMissionViews.cs ←
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
- `GwpDutyBanner` → GwpDutyBanner.cs
- `GwpDutyBannerClanEmblemPatch` → GwpDutyBanner.cs ←
- `GwpDutyBannerItemModel` → GwpDutyBanner.cs ←
- `GwpDutyBannerMapEmblemPatch` → GwpDutyBanner.cs ←
- `GwpDutyBannerPartyEmblemPatch` → GwpDutyBanner.cs ←
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
- `GwpFaithVillagerBirthPatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerDeparturePatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerIncomePatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerNativeLoadPatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerReplenishPatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerSalePatch` → GwpFaithVillagers.cs ←
- `GwpFaithVillagerWarehousePatch` → GwpFaithVillagers.cs ←
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
- `GwpFireswornBehavior` → GwpArmoryAllocation.cs , GwpArmoryBalance.cs , GwpArmoryBattle.cs , GwpArmoryBattleSave.cs , GwpArmoryOwnership.cs , GwpFaithVillagers.cs , GwpFireswornAffairs.cs , GwpFireswornArmory.cs , GwpFireswornBehavior.cs , GwpFireswornDungeon.cs , GwpFireswornExperience.cs , GwpFireswornParties.cs , GwpFireswornRituals.cs , GwpPlayerArmory.cs , GwpSurplusGuardRitual.cs , GwpTrainingTransports.cs
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
- `GwpFounderVoiceRegisterPatch` → GwpFounderVoiceRegisterPatch.cs
- `GwpGauntletWidgetUtility` → GwpGauntletWidgetUtility.cs
- `GwpHostilityRelationPatch` → PoliceAntiWarDeclaration.cs ←
- `GwpIds` → GwpIds.cs
- `GwpInitiativeDiagnosticsPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpInterventionChoicePatch` → GwpPlayerIntervention.cs ←
- `GwpInterventionSelectionPatch` → GwpPlayerIntervention.cs ←
- `GwpKickBehavior` → GwpKickBehavior.cs
- `GwpKickInputComponent` → GwpKickInputComponent.cs
- `GwpKingdomMembershipPatch` → GwpKingdomMembershipPatch.cs
- `GwpLandlessShipPurchaseTownPatch` → GwpLandlessShipTradePatch.cs ←
- `GwpLandlessShipSaleTownPatch` → GwpLandlessShipTradePatch.cs ←
- `GwpLandlessShipTrade` → GwpLandlessShipTradePatch.cs
- `GwpLastStandBehavior` → GwpLastStand.cs ←
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
- `GwpNativeBattleSceneSource` → GwpBattleSceneSource.cs ←
- `GwpNativeRegionPatch` → GwpStrength.Native.cs ←
- `GwpNavalCustomBattleCommanderListPatch` → GwpDualBladeActionSetPatch.cs ←
- `GwpNegotiationChancePatch` → GwpNegotiationChancePatch.cs
- `GwpNegotiationPolicy` → GwpNegotiationPolicy.cs
- `GwpNegotiationResistance` → GwpNegotiationResistance.cs
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
- `GwpPatrolBeats` → GwpPatrolBeats.cs
- `GwpPeace` → GwpPeace.cs
- `GwpPersuasionRound` → GwpPersuasionRound.cs
- `GwpPlayerCaseAssignment` → GwpPlayerCaseAssignment.cs
- `GwpPlayerEnforcementEngageActionPatch` → GwpPartyDutyMovementPatch.cs ←
- `GwpPlayerIntervention` → GwpPlayerIntervention.cs
- `GwpPlayerRequestDeferral` → GwpPlayerRequestDeferral.cs
- `GwpPoliceWarReasonService` → GwpPoliceWarReasonService.cs
- `GwpPriestStaysPatch` → GwpFireswornPriestStays.cs ←
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
- `GwpStrength` → GwpStrength.cs , GwpStrength.Native.cs
- `GwpSyndicateMusicBehavior` → GwpSyndicateMusicBehavior.cs
- `GwpText` → GwpText.cs
- `GwpTextCatalog` → GwpTextCatalog.g.cs
- `GwpTextKeys` → GwpTextKeys.cs
- `GwpTextureSprite` → GwpAffairsNavigation.cs ←
- `GwpTroopCombat` → GwpTroopCombat.cs
- `GwpTroopTransfer` → GwpTroopTransfer.cs
- `GwpTuning` → GwpTuning.cs
- `GwpVoluntarySurrender` → GwpVoluntarySurrender.cs
- `GwpWardenAutoEquipmentPatch` → GwpWardenAutoEquipmentPatch.cs
- `GwpWardenDispatchBehavior` → GwpWardenDispatchBehavior.Affairs.cs , GwpWardenDispatchBehavior.cs
- `GwpWardenDispatchDialogue` → GwpWardenDispatchDialogue.cs
- `GwpWardenDuties` → GwpWardenDuties.cs
- `GwpWardenMarriageBehavior` → GwpWardenMarriageBehavior.cs
- `GwpWardenPanicPatch` → GwpWardenResolve.cs ←
- `GwpWardenPurification` → GwpWardenPurification.cs
- `GwpWardenRecruitment` → GwpWardenRecruitment.cs
- `GwpWardenResolve` → GwpWardenResolve.cs
- `GwpWardenRetreatBehaviorPatch` → GwpWardenResolve.cs ←
- `GwpWardenRetreatPatch` → GwpWardenResolve.cs ←
- `GwpWardenTroopGenderPatch` → GwpWardenTroopGenderPatch.cs
- `GwpWardenVisitCandidatesPatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenVisitDesire` → GwpWardenVisitDesire.cs
- `GwpWardenVisitPartyParametersPatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenVisitScorePatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenVisitVolunteersPatch` → GwpWardenVisitDesire.cs ←
- `GwpWardenWageModel` → GwpWardenRecruitment.cs ←
- `GwpWarhorseDamageTransferBehavior` → GwpTroopCombat.cs ←
- `GwpWeaponTrait` → GwpTroopCombat.cs ←
- `GwpWeaponTraits` → GwpTroopCombat.cs ←
- `GwpWholePresetEquipmentPatch` → GwpTroopCombat.cs ←
- `HeroCrimeStats` → GwpData.cs ←
- `IGwpBattleSceneSource` → GwpBattleSceneSource.cs ←
- `IGwpSceneReinforcementSource` → GwpBattleSceneSource.cs ←
- `IssueDutyStage` → GreyWardenIssueResolutionBehavior.cs ←
- `IssueResolution` → GwpTuning.cs ←
- `IssueTaskSnapshot` → GreyWardenIssueResolutionBehavior.cs ←
- `Item` → GwpCaseReceipt.cs ←
- `LastStand` → GwpTuning.cs ←
- `LocalBalance` → GwpStrength.cs ←
- `NativeContact` → GwpStrength.Native.cs ←
- `NativeFrame` → GwpStrength.Native.cs ←
- `Patrol` → GwpTuning.cs ←
- `PaymentScope` → GwpFireswornUpgradePayment.cs ←
- `PendingReport` → GwpFieldReportLedger.cs ←
- `PlayerBehaviorMonitor` → PlayerBehaviorMonitor.cs
- `PlayerBehaviorPool` → GwpData.cs ←
- `PlayerBountyBehavior` → PlayerBountyBehavior.Affairs.cs , PlayerBountyBehavior.CaseSettlement.cs , PlayerBountyBehavior.CitySearch.cs , PlayerBountyBehavior.cs , PlayerBountyBehavior.Custody.cs , PlayerBountyBehavior.DialogueAndNotification.cs , PlayerBountyBehavior.Encyclopedia.cs , PlayerBountyBehavior.QuestAndNotification.cs , PlayerBountyBehavior.Support.cs
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
- `PoliceEnforcementBehavior` → PoliceEnforcementBehavior.Assistance.cs , PoliceEnforcementBehavior.AtonementQuest.cs , PoliceEnforcementBehavior.CaseLifecycle.cs , PoliceEnforcementBehavior.cs , PoliceEnforcementBehavior.DelayPatrols.cs , PoliceEnforcementBehavior.Dialogue.cs , PoliceEnforcementBehavior.Envoy.cs , PoliceEnforcementBehavior.Formation.cs , PoliceEnforcementBehavior.Helpers.cs , PoliceEnforcementBehavior.IdleDuties.cs , PoliceEnforcementBehavior.WarPursuitTrace.cs
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
- `Training` → GwpTuning.cs ←
- `TrainingTaskSnapshot` → GreyWardenTrainingBehavior.cs ←
- `TrainingTaskStage` → GreyWardenTrainingBehavior.cs ←
- `TroopRequest` → GwpTuning.cs ←
- `VillageReliefTaskSnapshot` → GreyWardenVillageAdoptionBehavior.cs ←
- `VillageReward` → GwpTuning.cs ←
- `Voice` → GwpMusicScore.cs ←
- `WardenDuty` → GwpWardenDuties.cs ←
- `WardenStandingTier` → GwpFieldArrestBehavior.cs ←

`←` 标出文件名里找不到该类型名的 213 个，它们靠 Glob 找不到。

## 文件索引

### 音乐（4 个文件）

- `GwpMusicScore.cs` — 378 行 · `GwpMusicScore` : IDisposable，另含 3 个类型
- `GwpMusicOutput.cs` — 188 行 · `GwpMusicOutput` : IDisposable，另含 1 个类型
- `GwpSyndicateMusicBehavior.cs` — 161 行 · `GwpSyndicateMusicBehavior` : MissionBehavior
- `GwpMusicBattlePolicy.cs` — 113 行 · `GwpMusicBattlePolicy`

### 战场援军（5 个文件）

- `GwpBattleReinforcementBehavior.cs` — 257 行 · `GwpBattleReinforcementBehavior` : MissionBehavior，另含 1 个类型
- `GwpBattleSceneContext.cs` — 82 行 · `GwpBattleSceneContext` : MissionBehavior
- `GwpBattleScenePolicy.cs` — 74 行 · `GwpBattleScenePolicy`，另含 1 个类型
- `GwpBattleSceneSource.cs` — 47 行 · `IGwpSceneReinforcementSource` : IGwpBattleSceneSource，另含 2 个类型
- `GwpBattleSupportOrigin.cs` — 37 行 · `GwpBattleSupportOrigin` : IAgentOriginBase

### 战场：死战不退（1 个文件）

- `GwpWardenResolve.cs` — 64 行 · `GwpWardenResolve`，另含 3 个类型

### 战场：双刀/近战（11 个文件）

- `GwpShieldBashGuardPatch.cs` — 1236 行 · `GwpPassiveHeldShieldCollision` : MissionBehavior，另含 5 个类型
- `GwpDualBladeAiBehavior.cs` — 753 行 · `GwpDualBladeAgentState` : MissionBehavior，另含 5 个类型
- `GwpAlternativeAttackControlBehavior.cs` — 362 行 · `GwpAlternativeAttackControlBehavior` : MissionBehavior
- `GwpDualBladeActionSetPatch.cs` — 314 行 · `GwpFaultTrace`，另含 5 个类型
- `GwpKickInputComponent.cs` — 216 行 · `GwpKickInputComponent` : AgentComponent
- `GwpAlternativeAttackControl.cs` — 177 行 · `GwpAlternativeAttackControl`
- `GwpDualBladeGroundPickupPatch.cs` — 168 行 · `GwpDualBladeGroundPickup`，另含 1 个类型
- `GwpKickBehavior.cs` — 114 行 · `GwpKickBehavior` : MissionBehavior
- `GwpDualBladeItemSetup.cs` — 63 行 · `GwpDualBladeItemSetup`
- `GwpDualBladeActionGate.cs` — 61 行 · `GwpDualBladeActionGate`
- `GwpDualBladeAttackArmor.cs` — 42 行 · `GwpDualBladeAttackArmor`

### 诊断（4 个文件）

- `GwpAiDiagnostics.cs` — 605 行 · `GwpAiDiagnostics`，另含 1 个类型
- `GwpRuntimeFaultWatch.cs` — 88 行 · `GwpRuntimeFaultWatch`
- `GwpEngineAssertDiagnostics.cs` — 34 行 · `GwpEngineAssertDiagnostics`
- `GwpDispatchBarterFaultDiagnostics.cs` — 23 行 · `GwpDispatchBarterFaultDiagnostics`

### 执法：案件与巡逻（33 个文件）

- `PoliceEnforcementBehavior.Assistance.cs` — 1873 行 · `PoliceEnforcementBehavior`，另含 1 个类型
- `PoliceEnforcementBehavior.cs` — 1412 行 · `PoliceEnforcementBehavior` : CampaignBehaviorBase
- `PoliceEnforcementBehavior.DelayPatrols.cs` — 1353 行 · `PoliceEnforcementBehavior`
- `GwpFieldArrestBehavior.cs` — 1306 行 · `GwpFieldArrestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `PolicePatrolBehavior.cs` — 1014 行 · `PolicePatrolBehavior` : CampaignBehaviorBase
- `PoliceEnforcementBehavior.Envoy.cs` — 1007 行 · `PoliceEnforcementBehavior`，另含 2 个类型
- `PoliceResourceManager.cs` — 721 行 · `PoliceResourceManager` : CampaignBehaviorBase，另含 2 个类型
- `GwpAiDeterrenceState.cs` — 698 行 · `GwpAiDeterrenceState`
- `PoliceEnforcementBehavior.Dialogue.cs` — 599 行 · `PoliceEnforcementBehavior`
- `PoliceCrimeMonitorEnhanced.cs` — 459 行 · `PoliceCrimeMonitorEnhanced` : CampaignBehaviorBase
- `PoliceAIDeterrenceBehavior.cs` — 427 行 · `PoliceAIDeterrenceBehavior` : CampaignBehaviorBase
- `PoliceAntiWarDeclaration.cs` — 424 行 · `PoliceAntiWarDeclaration` : CampaignBehaviorBase，另含 1 个类型
- `PoliceEnforcementBehavior.AtonementQuest.cs` — 361 行 · `PoliceEnforcementBehavior` : QuestBase，另含 1 个类型
- `GwpFieldArrestLines.cs` — 360 行 · `GwpFieldArrestLines`，另含 1 个类型
- `GwpAiDeterrenceDialogueCatalog.cs` — 339 行 · `DeterrenceSource`，另含 3 个类型
- `PolicePatrolBehavior.Helpers.cs` — 311 行 · `PolicePatrolBehavior`
- `PoliceEnforcementBehavior.Helpers.cs` — 155 行 · `PoliceEnforcementBehavior`
- `PoliceEnforcementBehavior.CaseLifecycle.cs` — 122 行 · `PoliceEnforcementBehavior`，另含 1 个类型
- `PolicePrisonerImmunityBehavior.cs` — 121 行 · `PolicePrisonerImmunityBehavior` : CampaignBehaviorBase
- `PoliceShipModels.cs` — 99 行 · `PoliceShipModelSupport` : CampaignShipDamageModel，另含 2 个类型
- `PoliceEnforcementBehavior.WarPursuitTrace.cs` — 97 行 · `PoliceEnforcementBehavior`
- `PoliceEnforcementBehavior.Formation.cs` — 89 行 · `PoliceEnforcementBehavior`
- `PolicePartyTroopUpgradeModel.cs` — 76 行 · `PolicePartyTroopUpgradeModel` : DefaultPartyTroopUpgradeModel
- `PoliceMobilePartyAIModel.cs` — 75 行 · `PoliceMobilePartyAIModel` : DefaultMobilePartyAIModel
- `PoliceAntiVanillaWarBehavior.cs` — 74 行 · `PoliceAntiVanillaWarBehavior` : CampaignBehaviorBase
- `GwpFieldArrestBehavior.Grace.cs` — 70 行 · `GwpFieldArrestBehavior`
- `PoliceAntiRecruitmentModel.cs` — 67 行 · `PoliceAntiRecruitmentModel` : DefaultDiplomacyModel
- `PoliceRaidDeterrenceModel.cs` — 58 行 · `PoliceRaidDeterrenceModel` : DefaultTargetScoreCalculatingModel
- `PolicePatrolBehavior.Encyclopedia.cs` — 49 行 · `PolicePatrolBehavior`
- `PoliceMarriageModel.cs` — 36 行 · `PoliceMarriageModel` : DefaultMarriageModel
- `PoliceHeroCreationModel.cs` — 33 行 · `PoliceHeroCreationModel` : DefaultHeroCreationModel
- `PoliceClanTierModel.cs` — 32 行 · `PoliceClanTierModel` : DefaultClanTierModel
- `PoliceEnforcementBehavior.IdleDuties.cs` — 12 行 · `PoliceEnforcementBehavior`

### 执法：玩家悬赏（11 个文件）

- `PlayerBountyBehavior.cs` — 908 行 · `PlayerBountyBehavior` : CampaignBehaviorBase
- `PlayerBountyBehavior.DialogueAndNotification.cs` — 753 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.CaseSettlement.cs` — 675 行 · `PlayerBountyBehavior`
- `GwpCaseArchiveItemVM.cs` — 568 行 · `GwpCaseArchiveItemVM` : ViewModel
- `PlayerBountyBehavior.Support.cs` — 168 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.QuestAndNotification.cs` — 144 行 · `PlayerBountyBehavior` : QuestBase，另含 1 个类型
- `PlayerBountyBehavior.Custody.cs` — 142 行 · `PlayerBountyBehavior`
- `GwpCaseArchiveScreen.cs` — 94 行 · `GwpAffairsState` : GameState，另含 1 个类型
- `PlayerBountyBehavior.Affairs.cs` — 81 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.Encyclopedia.cs` — 49 行 · `PlayerBountyBehavior`
- `PlayerBountyBehavior.CitySearch.cs` — 29 行 · `PlayerBountyBehavior`

### 使者与交兵（7 个文件）

- `GreyWardenTroopRequestBehavior.cs` — 1335 行 · `GreyWardenTroopRequestBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpWardenDispatchBehavior.cs` — 932 行 · `GwpWardenDispatchBehavior` : CampaignBehaviorBase
- `GwpWardenDispatchDialogue.cs` — 575 行 · `GwpWardenDispatchDialogue`
- `GreyWardenTroopRequestBehavior.Cohort.cs` — 304 行 · `GreyWardenTroopRequestBehavior`
- `GwpWardenDispatch.cs` — 109 行 · `GwpDispatchPurpose`，另含 2 个类型
- `GwpBribeBarterable.cs` — 62 行 · `GwpBribeBarterable` : Barterable
- `GwpWardenDispatchBehavior.Affairs.cs` — 59 行 · `GwpWardenDispatchBehavior`

### 练兵与切磋（3 个文件）

- `GreyWardenFieldSparringMissionController.cs` — 2541 行 · `GreyWardenFieldSparringMissionController` : MissionLogic
- `GreyWardenSparringBehavior.cs` — 878 行 · `GreyWardenSparringBehavior` : CampaignBehaviorBase
- `GreyWardenTrainingBehavior.cs` — 127 行 · `GreyWardenTrainingBehavior` : CampaignBehaviorBase，另含 2 个类型

### 家族与村庄（5 个文件）

- `GreyWardenVillageAdoptionBehavior.cs` — 788 行 · `GreyWardenVillageAdoptionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenFamilyBehavior.cs` — 614 行 · `GreyWardenFamilyBehavior` : CampaignBehaviorBase，另含 1 个类型
- `GreyWardenVillageReconstructionBehavior.cs` — 550 行 · `GreyWardenVillageReconstructionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GreyWardenVillageRewardSliderScreen.cs` — 255 行 · `GreyWardenVillageRewardSliderScreen` : ViewModel，另含 1 个类型
- `GreyWardenVillageRewardBehavior.cs` — 194 行 · `GreyWardenVillageRewardBehavior` : CampaignBehaviorBase

### 战斗数值模型（1 个文件）

- `GwpAgentApplyDamageModel.cs` — 858 行 · `GwpAgentApplyDamageModel` : AgentApplyDamageModel

### 大地图欲望与 AI（3 个文件）

- `GreyWardenPartyDesireBehavior.cs` — 789 行 · `GreyWardenPartyDesireBehavior` : CampaignBehaviorBase
- `PlayerBehaviorMonitor.cs` — 743 行 · `PlayerBehaviorMonitor` : CampaignBehaviorBase
- `GwpArmyExitDisorganizedPatch.cs` — 75 行 · `GwpArmyExitDisorganizedPatch`

### 玩家交互（1 个文件）

- `GreyWardenPlayerRequestBehavior.cs` — 1024 行 · `GreyWardenPlayerRequestBehavior` : CampaignBehaviorBase，另含 2 个类型

### 数据与共用（4 个文件）

- `GwpData.cs` — 1116 行 · `CrimeRecord`，另含 5 个类型
- `GwpTuning.cs` — 460 行 · `GwpTuning`，另含 16 个类型
- `GwpCommon.cs` — 366 行 · `GwpCommon`
- `GwpIds.cs` — 95 行 · `GwpIds`

### 入口（1 个文件）

- `SubModule.cs` — 175 行 · `SubModule` : MBSubModuleBase

### 其他（119 个文件）

- `GwpTextCatalog.g.cs` — 1429 行 · `GwpTextCatalog`
- `GwpFireswornBehavior.cs` — 1337 行 · `GwpFireswornBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpFireswornArmory.cs` — 881 行 · `GwpFireswornBehavior` : CombatSimulationModel，另含 3 个类型
- `GwpCitySearchMission.cs` — 693 行 · `GwpCitySearchMission` : MissionLogic，另含 2 个类型
- `GwpFireswornParties.cs` — 560 行 · `GwpFireswornBehavior`
- `GwpTroopCombat.cs` — 484 行 · `GwpWarhorseDamageTransferBehavior` : MissionBehavior，另含 4 个类型
- `GreyWardenIssueResolutionBehavior.cs` — 422 行 · `GreyWardenIssueResolutionBehavior` : CampaignBehaviorBase，另含 2 个类型
- `GwpEncyclopediaHeroPageVM.cs` — 418 行 · `GwpEncyclopediaHeroPageExtension`，另含 2 个类型
- `GwpFieldReportLedger.cs` — 415 行 · `GwpFieldReportLedger` : CampaignBehaviorBase，另含 1 个类型
- `GwpAffairsVM.cs` — 371 行 · `GwpAffairsChoiceVM` : ViewModel，另含 4 个类型
- `GwpFaithVillagers.cs` — 370 行 · `GwpFireswornBehavior`，另含 7 个类型
- `GwpPoliceWarReasonService.cs` — 362 行 · `GwpPoliceWarReasonService`
- `GwpCitySearchMission.Progress.cs` — 360 行 · `GwpCitySearchMissionController` : IAgentStateDecider，另含 3 个类型
- `GwpCitySearchBehavior.Flow.cs` — 331 行 · `GwpCitySearchPlace` : MissionLogic，另含 3 个类型
- `GreyWardenDesertersCampaignBehavior.cs` — 328 行 · `GreyWardenDesertersCampaignBehavior` : CampaignBehaviorBase
- `GwpFireswornRituals.cs` — 325 行 · `GwpFireswornBehavior`
- `GwpWardenVisitDesire.cs` — 303 行 · `GwpWardenVisitDesire` : PartyHealingModel，另含 5 个类型
- `GwpAssistanceArmyLifecyclePatch.cs` — 297 行 · `GwpAssistanceArmyDisbandGuardPatch`，另含 7 个类型
- `GwpStrength.cs` — 292 行 · `GwpStrength`，另含 1 个类型
- `GwpAssetPayment.cs` — 243 行 · `GwpAssetPayment`
- `GwpAffairsData.cs` — 241 行 · `GwpAffairsEntry`，另含 2 个类型
- `GwpStrength.Native.cs` — 240 行 · `GwpStrength`，另含 3 个类型
- `GwpCitySearchBehavior.cs` — 227 行 · `GwpCitySearchBehavior` : CampaignBehaviorBase
- `GwpArmoryAllocation.cs` — 224 行 · `GwpFireswornBehavior`
- `GwpCitySearchBodyTransport.cs` — 220 行 · `GwpCitySearchBodyTransport` : CorpseDraggingMissionLogic，另含 1 个类型
- `GwpFlacReader.cs` — 217 行 · `GwpFlacReader` : IDisposable
- `GwpFireswornMenus.cs` — 202 行 · `GwpFireswornMenus` : SettlementMilitiaModel，另含 3 个类型
- `GwpCitySearchMission.Reinforcements.cs` — 201 行 · `GwpCitySearchMissionController` : IGwpBattleSceneSource
- `GwpFireswornDungeon.cs` — 199 行 · `GwpFireswornBehavior`
- `GwpWardenDuties.cs` — 199 行 · `WardenDuty`，另含 1 个类型
- `GwpAffairsNavigation.cs` — 196 行 · `GwpAffairsNavigationElement` : MapNavigationElementBase，另含 3 个类型
- `GreyWardenLeaderBalanceBehavior.cs` — 193 行 · `GreyWardenLeaderBalanceBehavior` : CampaignBehaviorBase
- `GwpEncyclopediaClanPageVM.cs` — 192 行 · `GwpEncyclopediaClanPageExtension`，另含 2 个类型
- `GreyWardenLoreBehavior.cs` — 191 行 · `GreyWardenLoreBehavior` : CampaignBehaviorBase
- `GwpArmoryBattle.cs` — 191 行 · `GwpFireswornBehavior`，另含 1 个类型
- `GreyWardenSafeTroopSupplier.cs` — 188 行 · `GreyWardenSafeTroopSupplier` : IMissionTroopSupplier，另含 1 个类型
- `GwpPartyDutyMovementPatch.cs` — 184 行 · `GwpFinalDesireAuctionPatch`，另含 5 个类型
- `GwpOffenderDesire.cs` — 178 行 · `GwpOffenderDesire`，另含 1 个类型
- `GwpAgentStatCalculateModel.cs` — 175 行 · `GwpAgentStatCalculateModel` : AgentStatCalculateModel，另含 1 个类型
- `GwpFaithBattleRewardModel.cs` — 167 行 · `GwpFaithBattleRewardModel` : BattleRewardModel
- `GwpLastStand.cs` — 163 行 · `GwpLastStandBehavior` : MissionBehavior
- `GwpPlayerIntervention.cs` — 154 行 · `GwpPlayerIntervention`，另含 2 个类型
- `GwpRuntimeState.cs` — 153 行 · `GwpRuntimeState`，另含 2 个类型
- `GwpFieldDialogueVoice.cs` — 147 行 · `GwpFieldDialogueVoice`
- `GwpFiefPublicSupportPatch.cs` — 145 行 · `GwpFiefPublicSupportPatch`
- `GwpLandlessShipTradePatch.cs` — 144 行 · `GwpLandlessShipTrade`，另含 2 个类型
- `GwpAchievementCompatibilityPatch.cs` — 142 行 · `GwpAchievementCompatibility`，另含 2 个类型
- `GwpPersuasionRound.cs` — 140 行 · `GwpPersuasionRound`
- `GwpFireswornPartyDialogs.cs` — 137 行 · `GwpFireswornPartyDialogs`
- `GwpRitualMapSoundView.cs` — 137 行 · `GwpRitualMapSoundView` : MapView，另含 1 个类型
- `GwpRitualSoundOutput.cs` — 133 行 · `GwpRitualSoundOutput` : IDisposable
- `GwpMapBarReputationPatch.cs` — 131 行 · `GwpMapBarReputation`，另含 2 个类型
- `GwpCitySearchBehavior.Hall.cs` — 120 行 · `GwpCitySearchBehavior`
- `GwpPeace.cs` — 115 行 · `GwpPeace`
- `GwpNegotiationResistance.cs` — 112 行 · `GwpNegotiationResistance`
- `GwpFireswornUpgradePayment.cs` — 107 行 · `GwpFireswornUpgradePayment`，另含 4 个类型
- `GwpAssetOfferValidationPatch.cs` — 105 行 · `GwpAssetOfferValidationPatch`，另含 6 个类型
- `GwpArmoryBalance.cs` — 104 行 · `GwpFireswornBehavior`
- `GwpDispatchBarterScreen.cs` — 103 行 · `GwpDispatchBarterScreen`
- `GwpWardenRecruitment.cs` — 103 行 · `GwpWardenRecruitment` : VolunteerModel，另含 2 个类型
- `GwpCitySearchMission.Movement.cs` — 102 行 · `GwpCitySearchMissionController`，另含 2 个类型
- `GwpArmoryBattleSave.cs` — 99 行 · `GwpFireswornBehavior`
- `GreyWardenAdoptionLogEntry.cs` — 98 行 · `GreyWardenAdoptionLogEntry` : LogEntry
- `GwpSingleQueryLinkPatch.cs` — 96 行 · `GwpLinkedInquiryState`，另含 2 个类型
- `GwpWardenMarriageBehavior.cs` — 94 行 · `GwpWardenMarriageBehavior` : CampaignBehaviorBase
- `GwpDualWieldingPatch.cs` — 93 行 · `GwpDualWieldCollisionPatch`，另含 1 个类型
- `GwpSettlementNotices.cs` — 93 行 · `GwpSettlementNotices` : SettlementNotificationItemBaseVM，另含 3 个类型
- `GwpFireswornSoundView.cs` — 84 行 · `GwpFireswornSoundView` : MenuView，另含 1 个类型
- `GwpFireswornArmoryView.cs` — 82 行 · `GwpFireswornArmoryView`
- `GwpSettlementReconsiderationDecision.cs` — 82 行 · `GwpSettlementReconsiderationDecision` : SettlementClaimantDecision
- `GwpDutyBanner.cs` — 78 行 · `GwpDutyBanner` : DefaultBannerItemModel，另含 4 个类型
- `GwpTrainingTransports.cs` — 76 行 · `GwpFireswornBehavior`
- `GwpPartyScreenTroopTalkPatch.cs` — 72 行 · `GwpPartyScreenTroopTalk`，另含 2 个类型
- `GwpKingdomMembershipPatch.cs` — 71 行 · `GwpKingdomMembershipPatch`，另含 1 个类型
- `GwpCrimeCategory.cs` — 69 行 · `GwpCrimeCategory`，另含 2 个类型
- `GwpText.cs` — 69 行 · `GwpText`
- `GwpAssistanceArmyTooltipPatch.cs` — 66 行 · `GwpAssistanceArmyTooltipPatch`
- `GwpCitySearchMissionViews.cs` — 66 行 · `GwpCitySearchMissionViews` : DefaultVisualOrderProvider，另含 1 个类型
- `GwpFireswornArt.cs` — 64 行 · `GwpFireswornArt`
- `GwpTroopTransfer.cs` — 64 行 · `GwpTroopTransfer`
- `GwpCitySearchBehavior.Menu.cs` — 63 行 · `GwpCitySearchBehavior`
- `GwpPatrolBeats.cs` — 63 行 · `GwpPatrolBeats`
- `GwpWardenPurification.cs` — 62 行 · `GwpWardenPurification`
- `GreyWardenDeserterFilterBehavior.cs` — 59 行 · `GreyWardenDeserterFilterBehavior` : CampaignBehaviorBase
- `GwpSaveableTypeDefiner.cs` — 59 行 · `GwpSaveableTypeDefiner` : SaveableTypeDefiner
- `GwpNegotiationPolicy.cs` — 54 行 · `GwpNegotiationPolicy`
- `GwpPlayerCaseAssignment.cs` — 54 行 · `GwpPlayerCaseAssignment`
- `GwpDispatchCargo.cs` — 52 行 · `GwpDispatchCargo`，另含 1 个类型
- `GwpFireswornExperience.cs` — 52 行 · `GwpFireswornBehavior`
- `GwpEscortSpeedCapPatch.cs` — 50 行 · `GwpEscortSpeedCapPatch`
- `GwpSurplusGuardRitual.cs` — 50 行 · `GwpFireswornBehavior`
- `GwpArmoryOwnership.cs` — 49 行 · `GwpFireswornBehavior`
- `GwpCaseReceipt.cs` — 48 行 · `GwpCaseReceipt`，另含 1 个类型
- `GwpWardenAutoEquipmentPatch.cs` — 48 行 · `GwpWardenAutoEquipmentPatch`
- `GwpFieldCollectionBarterable.cs` — 47 行 · `GwpFieldCollectionBarterable` : Barterable
- `GwpPlayerArmory.cs` — 47 行 · `GwpFireswornBehavior`
- `GwpCaseTargetNameplatePatch.cs` — 44 行 · `GwpCaseTargetNameplatePatch`，另含 1 个类型
- `GwpFieldFineBarterable.cs` — 44 行 · `GwpFieldFineBarterable` : Barterable
- `GwpLeaderlessFoodModel.cs` — 41 行 · `GwpLeaderlessFoodModel` : MobilePartyFoodConsumptionModel
- `GwpWardenTroopGenderPatch.cs` — 41 行 · `GwpWardenTroopGenderPatch`
- `GwpVoluntarySurrender.cs` — 40 行 · `GwpVoluntarySurrender`
- `GwpGauntletWidgetUtility.cs` — 39 行 · `GwpGauntletWidgetUtility`
- `GwpAssetAutoOfferPatch.cs` — 36 行 · `GwpAssetAutoOfferPatch`
- `GwpDamnationSmokePatch.cs` — 36 行 · `GwpDamnationSmokePatch`
- `GwpDuelOpponentMarkerPatch.cs` — 36 行 · `GwpDuelOpponentMarkerPatch`
- `GwpPartySizeLimitModel.cs` — 36 行 · `GwpPartySizeLimitModel` : DefaultPartySizeLimitModel
- `GwpCrimeDesireAuction.cs` — 35 行 · `GwpCrimeDesireAuction`
- `GreyWardenDutyScheduler.cs` — 33 行 · `GreyWardenDutyScheduler`
- `GwpAdultCommanderLoadoutPatch.cs` — 32 行 · `GwpAdultCommanderLoadoutPatch`
- `GwpCaseSettlementRules.cs` — 31 行 · `GwpCaseSettlementRules`
- `GwpFireswornPriestStays.cs` — 31 行 · `GwpPriestStaysPatch`
- `GwpDispatchPrisonerCustody.cs` — 30 行 · `GwpDispatchHeroEscapePatch`，另含 1 个类型
- `GwpFlowStates.cs` — 27 行 · `AtonementFlowState`，另含 2 个类型
- `GwpFireswornAffairs.cs` — 26 行 · `GwpFireswornBehavior`
- `GwpFounderVoiceRegisterPatch.cs` — 26 行 · `GwpFounderVoiceRegisterPatch`
- `GwpPartyIdentity.cs` — 23 行 · `GwpPartyIdentity`
- `GwpTextKeys.cs` — 22 行 · `GwpTextKeys`
- `GwpPlayerRequestDeferral.cs` — 21 行 · `GwpPlayerRequestDeferral`
- `GwpNegotiationChancePatch.cs` — 20 行 · `GwpNegotiationChancePatch`
