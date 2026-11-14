# 部队页的升阶与确认

> 游戏版本：v1.4.8、v1.5.7 共同的托管流程
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/PartyScreenLogic.cs`、`PartyScreenData.cs`，`TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.Party/PartyVM.cs`、`PartyCharacterVM.cs`，`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/PartyUpgraderCampaignBehavior.cs`，`TaleWorlds.CampaignSystem/Helpers/PartyScreenHelper.cs`
> 模组接入：[玩家兵源与声望](../state/warden-recruitment.md#玩家兵源与声望方案)

## 怎么运作

1. `PartyCharacterVM.InitializeUpgrades` 为每条分支根据经验、金币、所需物品计算可升人数，并设置部队页与升阶弹窗共用的按钮状态。`PartyVM.ExecuteUpgrade` 据单击或批量快捷键构造 `PartyCommand`，交给 `PartyScreenLogic.AddCommand`。
2. `PartyScreenLogic.UpgradeTroop` 先走 `ValidateCommand`。通过后修改当前名册的经验、人数和伤兵，处理需要的升级物品，同时修改待结算金币和 `CurrentData.UpgradedTroopsHistory`。同一来源与目标的重复命令在记录中合并。队伍页更新后重新计算其他兵种的可升人数。
3. `DoneLogic` 先检查金币并运行该屏的确认委托。委托返回成功后才转移金币、发 `OnPlayerUpgradedTroops`、清空本轮记录并更新初始快照。`Reset` 将名册及记录恢复为打开时的状态，`ResetToLastSavedPartyScreenData` 可以恢复最近保存的界面快照。原版强制应用仍经过 `DoneLogic`。地图导航条的「不应用」直接经 `MapNavigationHelper.SwitchToANewScreen` 弹出界面，不调用 `DoneLogic` 或 `Reset`；部队页通常直接持有主队名册。
4. 战后领回解救士兵和从其他队伍调整兵员也复用 `PartyScreenLogic`，初始右侧和最终右侧名册可用于检查实际净增。AI 自动升阶走 `PartyUpgraderCampaignBehavior.UpgradeReadyTroops`，该方法跳过玩家主队。
