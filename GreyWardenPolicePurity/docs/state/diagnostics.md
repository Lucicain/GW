# 诊断系统

> 当前状态：现行规则；灰袍战马伤害转移专项诊断已随新归属更新，待实机验证
> 最后验收：未建立检查点
> 已复核至：2026-09-25 旧归属五场伤害日志与新归属源码
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs` `GwpTroopCombat.cs` `GwpAgentApplyDamageModel.cs`（后两者仅骑兵专项监控）
> 待实测：新归属下灰袍战马、骑手、普通马的伤害和控制对照，见 `troop-combat.md`

## 原则

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内（源码中 18 个文件带此守卫），
所以退休一条 trace 只关乎开发者信噪比，**与玩家收到什么无关**。

## 加与删的判据

**卡住时加强。** 开发停在某个具体问题上时，加宽该功能的 trace、补上下一次测试需要
的字段，并在 state 文件里写清每条新行要回答什么问题。

**确认后退休。** 用户在实机确认功能可用时，**在建立检查点的同一个任务里**退休它的
诊断。不要让一个已经定下来的功能每 tick 汇报自己正常。（依据：流程）

**按健康路径 vs 失败路径切，不按主题切。** 删掉在一切正常时会触发的 trace；
保留 `catch` 里和 "this should never happen" 分支里的——它们在健康的一局里保持沉默，
是未来游戏版本弄坏东西时唯一的信号。

**整个方法体只有一条 trace**，说明这个方法或补丁只为记录而存在，连同 trace 一起删。

**数据也要删。** 诊断系统退休时，同时删掉游戏 Documents 目录下的日志文件、
一次性崩溃转储和调查已闭合的反编译输出。仍在生效的诊断可以留，但它积攒的旧日志
不应该留——findings 记进 state 之后，旧日志没有进一步用途。

**按当前覆盖范围命名。** 范围变了就改系统名和日志文件名，不要留一个误导的名字。（依据：流程）
（例：`GwpRangedCommandTrace` 扩到双方所有编队后曾改名 `GwpBattleCommandTrace`，现已退休。）

## 日志文件

游戏 Documents 目录下：

| 文件 | 内容 |
|---|---|
| `GreyWarden-Faults.log` | 故障与静默吞异常留痕。在役的 `BATTLE_SCENE_*`、音乐档位诊断也写这里，它们退休后**健康的一局里这个文件应当是空的** |
| `GreyWarden-AI-Diagnostics.log` | AI / 战场 / 欲望拍卖采样 |
| `GreyWarden-Case-Events.log` | 案件生命周期事件 |
| `GreyWarden-Diagnostics-Archive` | 滚动归档 |

骑兵伤害调查的 `GreyWarden-Knight-Damage.log`、`GreyWarden-Warhorse-Damage.log`（含 `.previous`）已于 2026-09-25 随诊断退休删除，
不再可读；结论在 journal。

## 在役、待退休

| 系统 | 覆盖 | 退休条件 |
|---|---|---|
| `BATTLE_SCENE_OPENING` / `BATTLE_SCENE_SUPPORT` | 音乐资格分母、援军触发层/概率/预算/到场 | 用户已确认功能正常 → **下次动这块代码时退休** |
| 音乐策略档位诊断（每 10 任务秒 / 变档输出，含 `recent5`、`waiting-for-fresh-exchange`） | 强度映射是否按预期升降 | 用户已确认音乐正常 → **下次动这块代码时退休** |

已退休的：`GwpWarhorseDamageTrace`（2026-09-25 用户确认骑兵表现后删除代码）；`WARDEN_RESOLVE_BLOCK`（2026-09-25 士气改动时删除）；`GwpArcherContactTrace` 及其两类 Harmony 补丁（双刀互斥验收时删除）；
`GwpBattleCommandTrace` 全套战场采样与 `GwpArcherSwitchObserver`（2026-09-23 踱步调查结案删除，
日志行一并清除，结论见 [`battle-tactics.md`](battle-tactics.md)）。
其日志、WER 与一次性分析输出**不再可读**；需要重新调查时从新复现取证，
不要把已退休路径写成仍可用文件。（依据：流程）

## 限流是必需的

挂在每小时心跳或每 tick 上的诊断必须限流，否则会把日志刷爆，并毁掉
`GreyWarden-Faults.log` "健康即空文件" 这条性质。现行做法：

- `GwpFaultTrace.WriteQuiet` **按站点去重，每个位置每局只记一次**
- 容量等待不刷空批次日志
- 战场类采样按秒级间隔，不逐帧

## 不要为了打印而调用原版（依据：C#反编译 v1.4.8，2026-09-25 复核）

`BehaviorScreenedSkirmish` 等评分方法**本身会更新状态**。要读原版评分只能用
postfix 旁观真实返回值，不能主动再调一次。
`NavmeshlessTargetPositionPenalty` getter 会推进处罚计时，同理不要为打印去读。（依据：C#反编译 v1.4.8：getter 会推进计时，满 10 秒时把处罚写回 1；2026-09-25 复核）
