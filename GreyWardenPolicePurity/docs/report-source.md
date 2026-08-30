# 双刀携带入水崩溃：深度调研报告源

日期：2026-08-31  
范围：GreyWarden 当前开发树、Bannerlord 本地反编译程序集/转储/日志，以及 TaleWorlds 官方 API 文档。版本号和 1.5-21 发布流程不在本报告范围内。

## 执行结论

用户已经把触发条件收窄到“人物携带双刀，主手与副手同时出鞘，跨入水体”。独立掉落双刀安全，单刀出鞘或全收刀安全，普通武器安全。最合理的工程修复不是继续改死亡掉落或碰撞体，而是在原生 Agent 水体更新之前让双刀进入已知安全状态：瞬时收起副手和主手，保留装备槽，离开水体后恢复。

代码已在 `GwpDualBladeWaterSafetyPatch.cs` 实现，并部署到 live 模组。它是 Native 崩溃的前置规避，不声称修改或修复 TaleWorlds.Native 内部的无效句柄访问。

## 证据与推理台账

| 结论 | 证据 | 可信度 |
|---|---|---|
| 触发条件是“双刀人物 + 两把同时出鞘 + 入水” | 用户连续复验；一把出鞘/全收刀、普通武器、独立掉落实体均安全 | 已确认 |
| 崩溃发生在 Agent 携带路径而非掉落实体路径 | 独立掉落双刀入水安全；崩溃转储为 `TaleWorlds.Native.dll + 0x586f0b`、`0xc0000005` | 高 |
| 崩溃靠近原生 Agent Tick 的水体/持握状态转换 | `Mission.TickAgentsAndTeamsImp` 调用 `Agent.Tick`；本地 IL 中原版攀爬逻辑在 `IsInWater()` 时先收主、副手 | 高 |
| `CollisionBodyName` 不是充分修复 | 三把刀字段写入成功，但携带入水仍崩溃；掉落路径会由 `Mission.RecalculateBody` 重建形状且已实测安全 | 高 |
| 将双刀在水体更新前收起可规避当前崩溃 | 直接阻断已知危险状态；采用原版已有的 `TryToSheathWeaponInHand(..., Instant)` 模式 | 高（代码层），待用户实机确认 |

## 一手来源

### 官方 API

- [TaleWorlds Agent API](https://apidoc.bannerlord.com/v/1.3.4/class_tale_worlds_1_1_mount_and_blade_1_1_agent.html)：`IsInWater`、`TryToSheathWeaponInHand`、`TryToWieldWeaponInSlot`、`Position`、`MovementMode` 等接口。
- [TaleWorlds Mission API](https://apidoc.bannerlord.com/v/1.4.7/class_tale_worlds_1_1_mount_and_blade_1_1_mission.html)：`GetWaterLevelAtPosition`/`GetWaterLevelAtPositionMT`、`TickAgentsAndTeamsImp`、`AddTickAction` 等水体和任务更新接口。
- [TaleWorlds MissionBehavior API](https://apidoc.bannerlord.com/v/1.3.14/class_tale_worlds_1_1_mount_and_blade_1_1_mission_behavior.html)：任务行为生命周期接口，作为对照但本修复没有把关键收刀放在 `OnMissionTick` 之后。

### 本地反编译与项目证据

- 修复代码：[`GwpDualBladeWaterSafetyPatch.cs`](C:/Users/lucif/source/repos/GreyWardenPolicePurity/GreyWardenPolicePurity/GwpDualBladeWaterSafetyPatch.cs)，Harmony 前置挂在 `Mission.TickAgentsAndTeamsImp`。
- 本地反编译文本：仓库根目录的 [`mission.txt`](C:/Users/lucif/source/repos/GreyWardenPolicePurity/mission.txt)（`OnTick`、`TickAgentsAndTeamsImp`、`RecalculateBody`）、[`agent.cs.txt`](C:/Users/lucif/source/repos/GreyWardenPolicePurity/agent.cs.txt)（装备生成和 `WeaponEquipped`）、[`agent.il.txt`](C:/Users/lucif/source/repos/GreyWardenPolicePurity/agent.il.txt)（攀爬机的 `IsInWater` 后主/副手即时收刀）。
- 本地物品数据：`_Module/ModuleData/items.xml`、`gwp_crafting_pieces.xml`、`weapon_descriptions.xml`、`item_usage_sets.xml`；ROT 中可工作的静态双刀定义也使用显式 `body_name` 与 `recalculate_body=false`，但这不能解释“只在人物携带双刀入水时崩溃”。
- 本地崩溃/跟踪记录：`docs/maintenance-plan.md` 中 2026-08-31 条目，包含 `TaleWorlds.Native.dll + 0x586f0b`、`0xc0000005`、碰撞体字段和掉落/携带路径隔离结果。

## 修复边界

- 保护目标是所有实际携带完整双刀的 Agent，因此玩家和 AI 同时覆盖。
- 只在两把武器均处于持握状态且 Agent 已入水或短时投影即将跨过水面时触发。
- 收刀顺序为副手、主手；两把物品仍留在 `Weapon0`/`Weapon1`，回到陆地后即时恢复。
- 不改变双刀 XML、伤害/击倒、拾取和独立掉落规则；不处理调查旧问题；不改 1.5-21 版本号或正式包。

## 局限与后续

本修复绕开已知 Native 危险状态，而非拥有 TaleWorlds.Native 源码后修正其内部索引。当前已完成编译、部署和哈希核对，但尚未把“修复版落水不退出”写成用户确认结果；用户确认后再建立稳定 Git checkpoint。若未来游戏本体改变水体 Tick 顺序，应优先检查该前置点是否仍位于 `Agent.Tick` 之前。
