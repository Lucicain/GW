# 战后逃兵：灰袍兵不当逃兵

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 覆盖源码：`GreyWardenDesertersCampaignBehavior.cs` `GreyWardenDeserterFilterBehavior.cs`
> 待实测：无

- 原版 `DesertersCampaignBehavior` 在注册时整个移除（`SubModule` 里的 `RemoveBehaviors`），由 `GreyWardenDesertersCampaignBehavior` 代替。
  逻辑照搬原版：战后把溃逃兵和阵亡兵拼成逃兵队，在附近村子周围生成，同类队伍会合并。
  - 至少 15 人才成队，最多 40 人；
  - 普通战斗最多 3 队，军团战斗最多 5 队；
  - 有 90% 的概率生成。
- 唯一的区别：拼队之前剔除英雄和所有灰袍兵（`GwpCommon.RemoveGreyWardenTroops`），灰袍兵种永远不会出现在逃兵队里。（依据：设计）
- `GreyWardenDeserterFilterBehavior` 在读档和会话启动时，再把地图上所有逃兵队（`deserters` 家族）里的灰袍兵清掉一遍；
  清完没人的逃兵队直接销毁。
- 灰袍兵的判据是 ID 前缀 `gw`，所以火誓狂热者 `gwzealot` 也同样不会当逃兵。
