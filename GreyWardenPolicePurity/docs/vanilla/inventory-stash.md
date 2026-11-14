# 储藏双栏

> 游戏版本：v1.5.7
> 源码：`TaleWorlds.CampaignSystem/Helpers/InventoryScreenHelper.cs`、`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Inventory/InventoryLogic.cs`、`TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.Inventory/SPInventoryVM.cs`、`SandBox.GauntletUI/SandBox.GauntletUI/GauntletInventoryScreen.cs`
> 模组接入：[玩家军械库](../state/firesworn-armory.md#玩家军械库)、[向祭司交付](../state/firesworn-economy.md#玩家向祭司交付)

## 怎么运作

1. `InventoryScreenHelper.OpenScreenAsStash` 建立 `InventoryState` 并用 `Stash` 模式初始化 `InventoryLogic`。原版左侧绑定传入的储藏名册，右侧绑定主队背包，库存面板提供单项、整叠及批量转移。
2. `SPInventoryVM` 发送 `TransferCommand`，`InventoryLogic.ProcessTransferCommand` 分流库存侧和个人装备侧，转移时直接修改初始化所绑定的名册。`InventoryLogic.Reset` 清空两侧并从初始快照恢复，也复原角色装备，随后发 `AfterReset`。取消关闭时同样先重置。
3. 正常关闭经 `InventoryScreenHelper.CloseScreen` 调用 `DoneLogic` 后执行额外委托。地图导航的「应用」由 `GauntletInventoryScreen.ApplyChanges` 直接调用 `DoneLogic`，「放弃」调用 `Reset(true)`。`DoneLogic` 即使处于储藏模式，仍会发送原版物品交换事件。

## 容易想错的地方

- `Stash` 沿用角色装备栏。即使关闭金币交易，装备侧仍有自己的转移命令。
- 初始名册若为真实背包，选中期间就会改动背包；需要确认后才动真实物品的场景，可给面板绑定选择用副本。
