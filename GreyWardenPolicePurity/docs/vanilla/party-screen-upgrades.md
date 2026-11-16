# 部队页的升阶与确认

> 游戏版本：v1.4.8、v1.5.7 共同的托管流程
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyScreenLogic.cs`、`PartyScreenData.cs`，`TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.Party/PartyVM.cs`、`PartyCharacterVM.cs`，`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/PartyUpgraderCampaignBehavior.cs`，`TaleWorlds.CampaignSystem/Helpers/PartyScreenHelper.cs`
> 模组接入：[玩家兵源与声望](../state/warden-recruitment.md#玩家兵源与声望方案)

## 怎么运作

1. `PartyCharacterVM.InitializeUpgrades` 为每条分支根据经验、金币、所需物品计算可升人数，并设置部队页与升阶弹窗共用的按钮状态。`PartyVM.ExecuteUpgrade` 据单击或批量快捷键构造 `PartyCommand`，交给 `PartyScreenLogic.AddCommand`。
2. `PartyScreenLogic.UpgradeTroop` 先走 `ValidateCommand`。通过后修改当前名册的经验、人数和伤兵，处理需要的升级物品，同时修改待结算金币和 `CurrentData.UpgradedTroopsHistory`。同一来源与目标的重复命令在记录中合并。队伍页更新后重新计算其他兵种的可升人数。
3. `DoneLogic` 先检查金币并运行该屏的确认委托。委托返回成功后才转移金币、发 `OnPlayerUpgradedTroops`、清空本轮记录并更新初始快照。`Reset` 将名册及记录恢复为打开时的状态，`ResetToLastSavedPartyScreenData` 可以恢复最近保存的界面快照。原版强制应用仍经过 `DoneLogic`。地图导航条的「不应用」直接经 `MapNavigationHelper.SwitchToANewScreen` 弹出界面，不调用 `DoneLogic` 或 `Reset`；部队页通常直接持有主队名册。
4. 战后领回解救士兵和从其他队伍调整兵员也复用 `PartyScreenLogic`，初始右侧和最终右侧名册可用于检查实际净增。AI 自动升阶走 `PartyUpgraderCampaignBehavior.UpgradeReadyTroops`，该方法跳过玩家主队。

v1.5.7 的 AI 自动升级先用模型的 `IsTroopUpgradeable` 筛兵种，再把该行名册副本传给 `GetPossibleUpgradeTargets`。该方法从副本的总人数减伤兵数开始，以共享经验、工资和升级价格进一步收窄人数，随后 `UpgradeTroop` 按候选人数在真实名册扣经验并换兵。分支权重由模型提供。

## 底部居中的变化预期

部队页底部居中有四行「你将获得/失去……」：`PartyVM` 的 `GoldChangeText`、`MoraleChangeText`、`HorseChangeText`、`InfluenceChangeText`，按 `PartyScreenLogic.CurrentData` 的金币、士气、马匹、影响力变化量生成，各自由 `PartyGoldChange` 等事件刷新；`PartyScreen.xml` 用 `AutoHideRichTextWidget`，空字符串时整块隐藏。升阶花的金币、赎金得的金币都显示在这里。每次命令后 `PartyVM.Update` 末尾、打开时和非取消重置时都会调用私有的 `RefreshTopInformation`；非取消重置（含批量升级弹窗取消时的 `ResetToLastSavedPartyScreenData`）的末尾还会再调一遍四个 `OnParty*Changed`，覆盖之前写进这几行的文字。（依据：v1.4.8、v1.5.7 C#反编译 `PartyVM`，安装版 `SandBox/GUI/Prefabs/Party/PartyScreen.xml`，2026-11-15）

## 借部队页挑人

`PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty` 可传左侧名册、成员与俘虏各自的转移状态、可转移判定、模式、左侧名称、标题、左侧人数上限，以及完成、完成条件、取消、取消是否可用、关闭五个委托。右侧默认绑定主队真实名册，挑人用途需改成副本。`TroopsManage` 只显示成员，`Ransom` 只显示俘虏，`IsTroopUpgradesDisabled` 关掉升阶。转移照原版：单击一个，Shift 五个，Ctrl 全部。

完成按钮是否可用只看金币是否够付和完成条件委托，条件委托返回的说明是完成按钮的悬停提示；右侧超出人数上限时人数标红，点完成先弹「是否超员」的确认框（`PartyVM.ExecuteDone`）。`PartyScreenHelper.ClosePartyPresentation` 在完成时先跑 `DoneLogic`（其中调用完成委托），取消时跳过，随后调用 `OnPartyScreenClosed`：取消时先调取消委托，再发关闭事件。非普通模式（`TroopsManage`、`Ransom` 等）下 `PartyNavigationElement.IsLockingNavigation` 为真，导航条与快捷键都切不走；普通模式从导航条切走时，选应用走 `DoneLogic(true)`，选放弃直接弹出界面，不调用重置和这些委托。（依据：v1.5.7 C#反编译 `PartyScreenLogicInitializationData`、`PartyScreenLogic.IsDoneActive`/`DoneLogic`/`OnPartyScreenClosed`、`PartyScreenHelper`，2026-11-15）

## 赎金选人与结算

普通俘虏的默认赎金以无招募者的兵种招募价的四分之一为底，卖家的相关特长及仁慈可继续修正。例如 v1.5.7 中，16 级无马且无职业加价的普通步兵属三阶，招募价为 100 第纳尔，赎金基价为 25。（依据：v1.5.7 `DefaultRansomValueCalculationModel.PrisonerRansomValue`、`DefaultPartyWageModel.GetTroopRecruitmentCost`、`DefaultCharacterStatsModel.GetTier`）

v1.5.7 的酒馆赎金商在 `TavernEmployeesCampaignBehavior` 的对话结果里打开 `PartyScreenHelper.OpenScreenAsRansom`。它创建 `Ransom` 模式的 `PartyState`，右侧是主队成员和俘虏的副本，左侧从空名册开始。把俘虏移去左侧时，`TransferableWithTrade` 按赎金模型调整预览金额；确认委托 `SellPrisonersDoneHandler` 才交给 `SellPrisonersAction` 实际卖出和付款。返回对话后先接一条 NPC 答复，再重建玩家选项；若直接衔接玩家主选项，它们会在界面尚未成交时便被 `OnConversationContinue` 生成。

`CreateBasicInitDataWithMainParty` 默认会给出主队真实名册，`PartyScreenData.BindRostersFrom` 还会经右侧所有者绑定真实背包。纯选择用途需显式替换成副本，因为 `ResetUsing` 会清空再按打开时的状态重填。`TroopRoster.CloneRosterData` 复制人数和伤兵；确认侧另从实际源名册核对并搬运经验。`Ransom` 只显示俘虏，`TroopsManage` 只显示成员。普通 `Transferable` 只移动待选名册。`IsTroopUpgradesDisabled` 同时关闭升级和部队页招降路径。模组接入见[玩家向祭司交付](../state/firesworn-economy.md#玩家向祭司交付)。（依据：v1.5.7 C# 反编译 `TavernEmployeesCampaignBehavior`、`ConversationManager`、`MissionConversationVM`、`PartyScreenHelper`、`PartyScreenLogic`、`PartyScreenData`、`PartyVM`、`TroopRoster`，2026-11-15）
