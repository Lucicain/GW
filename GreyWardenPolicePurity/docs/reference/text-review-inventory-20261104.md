# 未审玩家文字：子系统分批清单

2026-11-04 盘点，供逐批出审阅稿。本清单只分组，不改 `PlayerText/strings.json`、生成文件或代码。规矩见[玩家文字](../state/player-text.md)，格式沿用[进城搜捕审阅稿](text-review-city-search-20261026.md)与[纠察队与灰袍领主拦截审阅稿](text-review-player-enforcement-20261104.md)。

## 怎么分的

- 范围：`PlayerText/strings.json` 当前 1758 条。
- 已审、跳过（271 条）：进城搜捕与城内支援（`gwp_cs_*` 及两份审阅稿提到的键）、宗教文本（[faith-text-review](faith-text-review/README.md) 118 条）、纠察队与灰袍领主拦截（[审阅稿](text-review-player-enforcement-20261104.md)）。
- [可疑用词核实稿](text-review-term-check-20261104.md)只核了「印/法令/卷宗/誓/审判/罚金」这几个词，没有审整句措辞；那 91 条照常归入下面各子系统，词的结论直接引用核实稿，不重复核实；核实稿已给改稿的条目，子系统稿只写「见核实稿」。
- 引用位置：扫描全部 `.cs` 里的 `GwpTextCatalog.<键>` / `English_<键>`、模块 XML 里的 `{=<键>}`，再加上 `GwpFieldArrestLines` 动态拼出来的 `gwp_terms_<打算>_<技能>` 与 `gwp_rebuff_<话题>_<技能>`。按第一个引用文件归组。
- 剩余 1487 条：有引用 1243 条，分成下面 16 批；无引用 244 条，玩家看不到，不审措辞，按 player-text 规则保留不删。

## 分批清单

| # | 子系统 | 条数 | 主要源码 | 审阅稿 |
|---|---|---|---|---|
| 1 | 野外执法：开场、劝说论据与罪犯的打算 | 148 | `GwpFieldArrestLines.cs`、`GwpOffenderDesire.cs` | `text-review-field-arrest-open-20261104.md` |
| 2 | 野外执法：罪犯的口吻与回应 | 144 | `GwpFieldDialogueVoice.cs` | `text-review-field-arrest-voice-20261104.md` |
| 3 | 野外执法：收款、宽限、约斗与结果 | 59 | `GwpFieldArrestBehavior.cs`、`GwpAssetPayment.cs`、`GwpFieldReportLedger.cs`、`GwpFieldArrestBehavior.Grace.cs`、`GwpAssetOfferValidationPatch.cs`、`GwpBribeBarterable.cs` | `text-review-field-arrest-result-20261104.md` |
| 4 | AI 领主震慑台词与震慑状态 | 146 | `GwpAiDeterrenceDialogueCatalog.cs`、`GwpAiDeterrenceState.cs` | `text-review-ai-deterrence-20261104.md` |
| 5 | 悬赏委托、入会退会与执勤旗 | 108 | `PlayerBountyBehavior.*.cs` | `text-review-bounty-membership-20261104.md` |
| 6 | 灰袍事务界面与案件台账 | 188 | `GwpCaseArchiveItemVM.cs`、`GwpAffairsVM.cs`、`GwpAffairsData.cs`、`PlayerBountyBehavior.Affairs.cs`、`GwpWardenDispatchBehavior.Affairs.cs`、`GwpFireswornAffairs.cs` | `text-review-affairs-ui-20261104.md` |
| 7 | 玩家使者与派遣 | 67 | `GwpWardenDispatchDialogue.cs`、`GwpWardenDispatchBehavior.cs`、`GwpPartyScreenTroopTalkPatch.cs` | `text-review-envoy-dispatch-20261104.md` |
| 8 | 封地申诉、调停讲和与灰袍宣战理由 | 49 | `GwpPoliceWarReasonService.cs`、`GreyWardenPlayerRequestBehavior.cs`、`PoliceAntiWarDeclaration.cs`、`GwpPeace.cs`、`GwpSettlementReconsiderationDecision.cs` | `text-review-fief-mediation-20261104.md` |
| 9 | 练兵订单与运兵 | 24 | `GreyWardenTroopRequestBehavior*.cs`、`GwpFireswornBehavior.cs`、`GwpFireswornPartyDialogs.cs`、`GwpFireswornParties.cs` | `text-review-troop-training-20261104.md` |
| 10 | 玩家罪行记录与声望通知 | 68 | `PlayerBehaviorMonitor.cs`、`GwpText.cs`、`PoliceCrimeMonitorEnhanced.cs`、`GwpMapBarReputationPatch.cs`、`PoliceAntiRecruitmentModel.cs` | `text-review-player-conduct-20261104.md` |
| 11 | 灰袍领主执法其余：赎罪、拖延、纠察队其余提示 | 53 | `PoliceEnforcementBehavior*.cs`、`PolicePatrolBehavior*.cs` | `text-review-enforcement-rest-20261104.md` |
| 12 | 家族、百科与婚姻收养 | 108 | `GreyWardenFamilyBehavior.cs`、`GwpEncyclopediaHeroPageVM.cs`、`GwpEncyclopediaClanPageVM.cs`、`GwpData.cs`、`GreyWardenAdoptionLogEntry.cs`、`spspecialcharacters.xml` | `text-review-family-encyclopedia-20261104.md` |
| 13 | 切磋与约斗场 | 20 | `GreyWardenSparringBehavior.cs`、`GreyWardenFieldSparringMissionController.cs` | `text-review-sparring-20261104.md` |
| 14 | 村庄打赏与重建 | 12 | `GreyWardenVillageRewardSliderScreen.cs`、`GreyWardenVillageRewardBehavior.cs`、`GreyWardenVillageReconstructionBehavior.cs`、`GreyWardenIssueResolutionBehavior.cs` | `text-review-village-relief-20261104.md` |
| 15 | 物品与兵种名称 | 36 | `items.xml`、`spnpccharacters.xml`、`sphpcustombattle.xml`、`gwp_crafting_pieces.xml` | `text-review-items-troops-20261104.md` |
| 16 | 战场援军、介入与地图杂项 | 13 | `GwpPlayerIntervention.cs`、`GwpBattleReinforcementBehavior.cs`、`GwpPartyDutyMovementPatch.cs`、`GwpCitySearchBehavior*.cs` | `text-review-map-battle-misc-20261104.md` |
| | 合计 | 1243 | | |

军械库、教众、村民队与运兵队名称等宗教相关文字已在 faith-text-review 里审过并实装，不在上表。

## 无引用的 244 条

源码、模块 XML 与动态拼键都查不到，玩家看不到，不审措辞。按键前缀：`gwp_playerbountybehavior` 33、`gwp_fa` 29、`gwp_gwppolicewarreasonservice` 25、`gwp_gwpcasearchivescreen` 20、`gwp_bounty` 18、`gwp_case` 18、`gwp_gwpaideterrencestate` 17、`gwp_greywardentrooprequestbehavior` 15、`gwp_affairs` 12、`gwp_gwpencyclopediaheropagevm` 10、`gwp_report` 9、`gwp_playerbehaviormonitor` 7、`gwp_fieldarrest` 7、`gwp_sparring` 4、`gwp_gwpencyclopediaclanpagevm` 3、`gwp_troop` 3、`gwp_policeenforcementbehavior` 2、`gwp_policepatrolbehavior` 2、`gwp_greywardenfamilybehavior` 2、`gwp_field` 2、`gwp_dispatch` 2、`gwp_common` 2、`gwp_issue` 1、`gwp_training` 1。

另有一些键代码里有引用、实际却走不到（例如罪犯的打算里从不抽到的几种），在各子系统稿里单独标「不可达」，同样不审措辞。
