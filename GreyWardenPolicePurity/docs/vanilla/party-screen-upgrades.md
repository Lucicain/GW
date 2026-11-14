# 部队页的升阶与确认

> 游戏版本：v1.4.8、v1.5.7 共同的托管流程
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyScreenLogic.cs`、`PartyScreenData.cs`，`TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.Party/PartyVM.cs`、`PartyCharacterVM.cs`，`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/PartyUpgraderCampaignBehavior.cs`，`TaleWorlds.CampaignSystem/Helpers/PartyScreenHelper.cs`
> 模组接入：[玩家兵源与声望](../state/warden-recruitment.md#玩家兵源与声望方案)

## 怎么运作

1. `PartyCharacterVM.InitializeUpgrades` 为每条分支根据经验、金币、所需物品计算可升人数，并设置部队页与升阶弹窗共用的按钮状态。`PartyVM.ExecuteUpgrade` 据单击或批量快捷键构造 `PartyCommand`，交给 `PartyScreenLogic.AddCommand`。
2. `PartyScreenLogic.UpgradeTroop` 先走 `ValidateCommand`。通过后修改当前名册的经验、人数和伤兵，处理需要的升级物品，同时修改待结算金币和 `CurrentData.UpgradedTroopsHistory`。同一来源与目标的重复命令在记录中合并。队伍页更新后重新计算其他兵种的可升人数。
3. `DoneLogic` 先检查金币并运行该屏的确认委托。委托返回成功后才转移金币、发 `OnPlayerUpgradedTroops`、清空本轮记录并更新初始快照。`Reset` 将名册及记录恢复为打开时的状态，`ResetToLastSavedPartyScreenData` 可以恢复最近保存的界面快照。原版强制应用仍经过 `DoneLogic`。地图导航条的「不应用」直接经 `MapNavigationHelper.SwitchToANewScreen` 弹出界面，不调用 `DoneLogic` 或 `Reset`；部队页通常直接持有主队名册。
4. 战后领回解救士兵和从其他队伍调整兵员也复用 `PartyScreenLogic`，初始右侧和最终右侧名册可用于检查实际净增。AI 自动升阶走 `PartyUpgraderCampaignBehavior.UpgradeReadyTroops`，该方法跳过玩家主队。

## 赎金选人与结算

v1.5.7 的酒馆赎金商在 `TavernEmployeesCampaignBehavior` 的对话结果里打开 `PartyScreenHelper.OpenScreenAsRansom`。它创建 `Ransom` 模式的 `PartyState`，右侧是主队成员和俘虏的副本，左侧从空名册开始。把俘虏移去左侧时，`TransferableWithTrade` 按赎金模型调整预览金额；确认委托 `SellPrisonersDoneHandler` 才交给 `SellPrisonersAction` 实际卖出和付款。返回对话后先接一条 NPC 答复，再重建玩家选项；若直接衔接玩家主选项，它们会在界面尚未成交时便被 `OnConversationContinue` 生成。

`CreateBasicInitDataWithMainParty` 默认会给出主队真实名册，`PartyScreenData.BindRostersFrom` 还会经右侧所有者绑定真实背包。纯选择用途需显式替换成副本，因为 `ResetUsing` 会清空再按打开时的状态重填。`TroopRoster.CloneRosterData` 复制人数和伤兵；确认侧另从实际源名册核对并搬运经验。`Ransom` 只显示俘虏，`TroopsManage` 只显示成员。普通 `Transferable` 只移动待选名册。`IsTroopUpgradesDisabled` 同时关闭升级和部队页招降路径。模组接入见[玩家向祭司交付](../state/firesworn-economy.md#玩家向祭司交付)。（依据：v1.5.7 C# 反编译 `TavernEmployeesCampaignBehavior`、`ConversationManager`、`MissionConversationVM`、`PartyScreenHelper`、`PartyScreenLogic`、`PartyScreenData`、`PartyVM`、`TroopRoster`，2026-11-15）
