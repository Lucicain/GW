# 原版任务部队的地图标记

> 游戏版本：v1.4.8、v1.5.7
> 源码：`SandBox.ViewModelCollection/SandBox.ViewModelCollection.Map.Tracker/MapTrackerProvider.cs`（v1.4.8）；`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/MapTrackerManager.cs`、`VisualTrackerManager.cs`、`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/MapTrackerCampaignBehavior.cs`、`SandBox.GauntletUI/SandBox.GauntletUI.Map/GauntletMapTrackersView.cs`、`SandBox.ViewModelCollection/SandBox.ViewModelCollection.Map.Tracker/MapMobilePartyTrackItemVM.cs`（v1.5.7）
> 模组接入：[玩家送信队](../state/dispatch.md#路上)

## 怎么运作

1. v1.4.8 的 `MapTrackerProvider` 在建队、任务占用变化、重建界面标记时判断收录资格。无领队的任务队须满足 `IsCurrentlyUsedByAQuest` 且已在 `VisualTrackerManager` 登记。先登记视觉追踪，再设置任务占用会触发 `OnPartyQuestStatusChanged` 建立标记。
2. v1.5.7 的 `MapTrackerCampaignBehavior` 把队伍事件转给 `MapTrackerManager.Refresh`。任务队以及普通自定义队不在自动收录的玩家家族领主队和商队范围内。`AddMapTracker` 将队伍加入存档保存的手动列表，再通知界面创建标记。重复登记同一对象会被忽略。地图视图建立时从 `GetAllTrackers` 读取自动和手动列表，因此也能恢复在途标记。
3. `MapMobilePartyTrackItemVM` 在队伍未被附属且原版部队实体不可见时启用地图标记。`VisualTrackerManager.CheckTracked` 仍决定该标记的追踪状态。队伍进入视野后由地图上的实际部队显示。
4. `RemoveMapTracker` 从手动列表移除队伍并通知界面。原版 `OnPartyRemoved` 也调用这一入口，部队从地图移除时清理手动登记和视觉追踪。

## 容易想错的地方

- v1.5.7 里 `VisualTrackerManager.RegisterObject` 只登记视觉追踪状态。创建任务队标记走 `MapTrackerManager.AddMapTracker`，例如原版 `LocateAndRescueTravellerTutorialQuest`。
