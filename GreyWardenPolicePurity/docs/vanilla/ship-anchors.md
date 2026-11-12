# 船队锚点与下水时间

> 游戏版本：v1.4.8、v1.5.7（NavalDLC）
> 源码：`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Naval/AnchorPoint.cs`，`managed/NavalDLC/NavalDLC.GameComponents/NavalDLCPartyTransitionModel.cs` 的 `GetTransitionTimeForEmbarking`、`GetAnchorReachDurationInHours`
> 模组接入：[临时队借船](../state/temporary-parties.md)

## 怎么运作

锚点的 `Position` 是水上的泊船位置；上岸时保存的陆地坐标供 `GetInteractionPosition` 返回。锚点无效时，这个方法返回无效坐标。1.4.8 还可用 `GetLastUsedDisembarkPosition` 直接取已保存的陆地坐标。

1.4.8 的 `SetPosition` 同时清掉移动目标。`SetSettlement` 写入港口的 `PortPosition`，把城门 `GatePosition` 保存为陆地坐标。1.5.7 将港口保存在 `Settlement` 属性；设置它时同步港口与城门坐标，改 `Position` 则清掉原港口。`ResetPosition` 清除当前位置与到达时间。船队还在移动时，`Position` 与 `Settlement` 不返回尚未到达的目的地。

两版下水都先判断是否有有效锚点。没有时等待 48 小时；计算出的距离小于 10 时立即下水；其余用 `距离^0.95 / 35` 小时，限在 3 至 48 小时。若部队当前在聚落，两版都用从聚落到锚点水上位置的航行距离。若不在聚落，1.4.8 算从部队到锚点陆地坐标的距离，1.5.7 算锚点到本次海陆交接终点的航行距离；没有有效交接终点则等到上限。

## 容易想错的地方

- 海上泊位和陆地交互点各有用途，复制其一不能代表复制了整个锚点。1.5.7 还分别记录港口身份与移动到达时间。
- 两版即使锚点和交接位置相同，岸边等待时间也可因原版取距离方式变化而不同。
