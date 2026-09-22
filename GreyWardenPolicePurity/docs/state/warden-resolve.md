# 灰袍死战不退

> 当前状态：用户已实测满意（2026-09-23 确认死战部分）
> 最后验收：**`b68b43c`**（本地检查点，未发布）
> 待实测：无（用户已确认）

实现在 `GwpWardenResolve.cs`（独立文件，回退只需移出此文件 + 同步测试）。

## 覆盖谁

按**个体身份**处理，沿用 `GwpCommon` 的灰袍兵种 / 自定义指挥官 / 灰袍家族英雄识别：

- 敌友双方、开场部队、原生补员、模组支援的**灰袍人类 AI 战斗人员**。
- 混编中的普通兵**不获得免疫**，照常逃跑。
- 不拦截：人控玩家、无主战马、多人、非 Battle 模式、已出战斗结果/结束的清场路径。

## 改了什么

四个 Harmony 入口：

| 入口 | 行为 |
|---|---|
| `CommonAIComponent.Morale` setter | 符合范围时把写入值改为 100，避免战斗士气冲击 |
| `CommonAIComponent.Panic` | 在广播与 `IsPanicked` 改变**之前**拦截 |
| `CommonAIComponent.Retreat` | 在 `IsRetreating` 及原生逃跑目的地改变**之前**拦截，给该士兵设 Charge 行为参数 |
| `HumanAIComponent.RefreshBehaviorValues` | 只把该灰袍的 Retreat 行为参数改为 Charge，防止原生 `OnApply` 后续刷新覆盖 |

保持不变：正常移动、坚守、后撤、迂回指令；共享编队的 `MovementOrder`、队伍/阵营、
成员列表；战役名册。世界地图士气、离队与自动结算不改。
战斗未结束时，灰袍也不执行编队的逃离战场 `Retreat` 命令。

## 为什么是这四个入口

原生依据（v1.4.8 反编译）：

- `CommonAIComponent` 低士气触发 Panic，`MissionAgentPanicHandler` 之后调用 `Retreat`。
- `MovementOrder.OnUnitJoinOrLeave` 遇 Retreat 编队会**不检查士气直接命令新加入者
  Retreat**，`RetreatAux` 也逐人调用。

因此只在 SpawnBatch 补一次士气覆盖不了第二条，也覆盖不了之后受创。已否决的方案：
单次加士气、每 tick 恢复逃兵、在原生编队遍历中移兵。

原生 `Formation.GetOrderPositionOfUnit` 对 Retreat 返回 Invalid，
`HumanAIComponent.GetFormationFrame` 不锁定撤退编队位置；拦截逃跑模式后保留自主战斗。

## 诊断

`WARDEN_RESOLVE_BLOCK`（在 `GWP_DIAGNOSTICS` 内）记录 panic/retreat 来源、side、
士气、编队命令、是否 `GwpBattleSupportOrigin`。每 Mission 每来源最多 8 条，由
`ConditionalWeakTable` 随 Mission 回收，**不逐 tick 刷日志**。

用户已确认功能正常 → 按 [`diagnostics.md`](diagnostics.md) 的规则，这套健康路径
拦截诊断应在下一次动这块代码时退休。

## 测试

`tools/BattleSceneTests` 中的 18 项（总 138 项的一部分）：援军加入已 Retreat 编队、
后续行为刷新、敌友初始兵、混编普通兵继续正常逃跑、共享编队不变、士气负冲击、
直接 Panic、普通后撤/移动/坚守、马/玩家/非战斗/多人/战后排除、诊断限流。

测试为引擎边界桩，不验证游戏内 AI 寻敌。
