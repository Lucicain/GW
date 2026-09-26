# 诊断系统

> 当前状态：现行规则；2026-09-26 退休援军、开场资格与音乐档位的健康路径诊断
> 最后验收：未建立检查点
> 已复核至：2026-09-26 工作树（`GwpBattleReinforcementBehavior.cs` `GwpBattleSceneContext.cs` `GwpSyndicateMusicBehavior.cs` 的诊断删除）
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs`
> 待实测：无

## 原则

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内（源码中 18 个文件带此守卫），
所以退休一条 trace 只关乎开发者信噪比，**与玩家收到什么无关**。

## 谁来管

诊断的加、删、加强与日志清理由 agent 自己决定并执行，不等用户下指令，也不请用户
去读日志。遇到难以解决的问题或体验问题时，agent 先加强相关诊断取证，并配合 C# 反编译
与 `TaleWorlds.Native.dll` 反汇编查实原版规则（方法见
[`../reference/investigation-methods.md`](../reference/investigation-methods.md)）。
用户说"测过了没问题"时，agent 要自己读在役日志核对待观察项。（依据：用户裁定 2026-09-26）

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

**游戏的崩溃报告同样归 agent 管**：`C:\ProgramData\Mount and Blade II Bannerlord\crashes\` 下每份报告带
700～850 MB 的转储（Modding Kit 的编辑器版本每条警告都会生成一份）。agent 自己读（转储用 WinDbg 的
`cdbX64.exe`，见 `../reference/investigation-methods.md`），结论写进 journal 后直接删，不需要再问用户。（依据：用户裁定）

**按当前覆盖范围命名。** 范围变了就改系统名和日志文件名，不要留一个误导的名字。（依据：流程）
（例：`GwpRangedCommandTrace` 扩到双方所有编队后曾改名 `GwpBattleCommandTrace`，现已退休。）

## 日志文件

游戏 Documents 目录下：

| 文件 | 内容 |
|---|---|
| `GreyWarden-Faults.log` | 故障与静默吞异常留痕，只追加不滚动。**健康的一局里只有 `FAULT_WATCH_ARMED` 一行** |
| `GreyWarden-AI-Diagnostics.log` | AI / 欲望拍卖 / 协力编成 / 地图战斗采样，每局开新文件，旧的滚进归档 |
| `GreyWarden-Case-Events.log` | 案件生命周期事件 |
| `GreyWarden-Diagnostics-Archive` | 滚动归档 |

骑兵伤害调查的 `GreyWarden-Knight-Damage.log`、`GreyWarden-Warhorse-Damage.log`（含 `.previous`）已于 2026-09-25 随诊断退休删除，
不再可读；结论在 journal。

## 在役

| 系统 | 为什么还留着 | 退休条件 |
|---|---|---|
| `GwpAiDiagnostics`（AI 日志与 `GreyWarden-Case-Events.log`） | 2026-09-25 调停 0 人队崩溃、2026-09-26 改追绕过冷却都靠它取证；覆盖的巡逻、练兵、协力等子系统多数尚未提取 | 对应子系统提取成 state 且无待观察项时，按主题拆掉健康路径 |
| 失败路径：`GwpRuntimeFaultWatch`、`GwpEngineAssertDiagnostics`、`DUAL_BLADE_INVALID_ACTION`、`DISPATCH_BARTER_PERSONA_FAILED` | 健康时沉默 | 不退休 |

已退休的：援军 `BATTLE_SCENE_OPENING` / `BATTLE_SCENE_SUPPORT` 与音乐档位 `SYNDICATE_BATTLE_DYNAMICS`（2026-09-26 用户确认全部实测项后删除，同时清掉了累积 2.9 MB 的 `GreyWarden-Faults.log` 与 9 月 15–17 日归档）；`GwpWarhorseDamageTrace`（2026-09-25 用户确认骑兵表现后删除代码）；`WARDEN_RESOLVE_BLOCK`（2026-09-25 士气改动时删除）；`GwpArcherContactTrace` 及其两类 Harmony 补丁（双刀互斥验收时删除）；
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
