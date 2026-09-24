# 灰袍死战不退

> 当前状态：**已部署待实测**：2026-09-25 士气不再强制拉满，只保证不溃逃；死战部分此前用户已实测满意（2026-09-23）
> 最后验收：**`b68b43c`**（本地检查点，未发布）
> 覆盖源码：`GwpWardenResolve.cs`
> 待实测：士气照原版升降后，灰袍仍不恐慌、不逃跑；士气显示与原版一致

实现在 `GwpWardenResolve.cs`（独立文件，回退只需移出此文件 + 同步测试）。

## 覆盖谁

按**个体身份**处理，沿用 `GwpCommon` 的灰袍兵种 / 自定义指挥官 / 灰袍家族英雄识别：

- 敌友双方、开场部队、原生补员、模组支援的**灰袍人类 AI 战斗人员**。
- 混编中的普通兵**不获得免疫**，照常逃跑。
- 不拦截：人控玩家、无主战马、多人、非 Battle 模式、已出战斗结果/结束的清场路径。

## 改了什么

**士气照原版升降，不拉满**（2026-09-25 用户：不要拉满，只需要别溃逃）。此前的
`CommonAIComponent.Morale` setter 补丁（把写入值改为 100）已删除。

三个 Harmony 入口，只管"不溃逃"：

| 入口 | 行为 |
|---|---|
| `CommonAIComponent.Panic` | 在广播与 `IsPanicked` 改变**之前**拦截；低士气走到这里也不会恐慌 |
| `CommonAIComponent.Retreat` | 在 `IsRetreating` 及原生逃跑目的地改变**之前**拦截，给该士兵设 Charge 行为参数 |
| `HumanAIComponent.RefreshBehaviorValues` | 只把该灰袍的 Retreat 行为参数改为 Charge，防止原生 `OnApply` 后续刷新覆盖 |

保持不变：正常移动、坚守、后撤、迂回指令；共享编队的 `MovementOrder`、队伍/阵营、
成员列表；战役名册。世界地图士气、离队与自动结算不改。
战斗未结束时，灰袍也不执行编队的逃离战场 `Retreat` 命令。

## 为什么是这三个入口

原生依据（v1.4.8 反编译）：

- `CommonAIComponent` 低士气触发 Panic，`MissionAgentPanicHandler` 之后调用 `Retreat`。
- `MovementOrder.OnUnitJoinOrLeave` 遇 Retreat 编队会**不检查士气直接命令新加入者
  Retreat**，`RetreatAux` 也逐人调用。

因此只加士气覆盖不了第二条，拦 Panic 与 Retreat 才是"不溃逃"的充分条件，士气本身不必改。
已否决的方案：单次加士气、每 tick 恢复逃兵、在原生编队遍历中移兵。

原生 `Formation.GetOrderPositionOfUnit` 对 Retreat 返回 Invalid，
`HumanAIComponent.GetFormationFrame` 不锁定撤退编队位置；拦截逃跑模式后保留自主战斗。

## 诊断

已退休：健康路径拦截诊断 `WARDEN_RESOLVE_BLOCK` 在 2026-09-25 改动这块代码时删除
（用户 2026-09-23 已确认死战功能正常）。

## 测试

`tools/BattleSceneTests` 中的 18 项（总 138 项的一部分）：援军加入已 Retreat 编队、
后续行为刷新、敌友初始兵、混编普通兵继续正常逃跑、共享编队不变、士气负冲击、
直接 Panic、普通后撤/移动/坚守、马/玩家/非战斗/多人/战后排除、诊断限流。

测试为引擎边界桩，不验证游戏内 AI 寻敌。
