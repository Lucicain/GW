# 诊断系统

> 当前状态：现行规则；入国来源与自动换装候选取证随修复合入 main，已验收读档退出正常 trace 已退休
> 最后验收：未建立检查点
> 已复核至：`a1d6479`（2026-10-07；在役范围、Guard 与仪式声音取证复核）
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs`
> 待实测：无

## 原则

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内（源码中 19 个文件带此守卫），
纯 trace 的退休只改变开发者日志；`GwpRuntimeFaultWatch.Guard` 额外吞异常，会造成开发与玩家版失败行为不同，这是待清理的例外，见 `code-health.md`。

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
有依据的 `catch` 里的留痕保留——它们在健康的一局里保持沉默，是未来游戏版本弄坏东西时的信号。
没有依据的 catch 不该存在（用户裁定 2026-10-05，见 `code-health.md`）。

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
| `GwpAiDiagnostics`（AI 日志与 `GreyWarden-Case-Events.log`） | 案件收尾、战力归属与差事避让的共享判断改动仍待实测；各子系统已提取 state | 用户确认对应行为可用后，按所属 state 的待观察项退休健康路径 |
| `GwpAiDiagnostics.WriteDesire`：AI 日志里的 `DESIRE` 行（见 `warden-ai.md`「进聚落的意愿」） | 灰袍领主为什么想去哪儿：逐因子对账原版进城分 | 原版分与重算分长期对得上、用户确认灰袍领主进退正常后退休 |
| `GwpAiDiagnostics.WriteFaith`：AI 日志里的 `\| FIRESWORN \|` 行（stage 清单见 `firesworn-economy.md`「监控」） | 信众村经济整套待实测：看俘虏、地牢、烧人、两支常驻队、领主进村、军械库、收入之间配合得怎样；每日有村、领主、总量快照 | 用户确认信众村经济可用后，删健康路径的事件行，每日快照一并删 |
| `RITUAL_SOUND_START` / `RITUAL_SOUND_STOP`（`GwpFireswornSoundView`） | 记录原始帧数/采样率、提交增益、自然结束与菜单退出原因，取证一次播放与退出立即停止；配图已验收，贴图专项输出已退休 | 用户确认传教、烧人音效完整一次、原音调、退出停止后，同轮删除这两条健康 trace 及声音调查输出 |
| `WARDEN_KINGDOM_JOIN_BLOCKED` / `WARDEN_BELLUM_EXILE_EXCLUDED` | 新入国动作及内战流亡排除来源待观察，见 kingdom-membership.md | 新入国拦截验收后退休 |
| `WARDEN_AUTO_EQUIPMENT_REJECTED` | 灰袍英雄每个实例最多一条，真实 RBM 候选已离线确认 | 实机进城装备保持确认后退休 |
| 故障观察：`GwpRuntimeFaultWatch` 的 CLR 事件观察、`GwpEngineAssertDiagnostics`；已发生故障的 `DUAL_BLADE_INVALID_ACTION`、`DISPATCH_BARTER_PERSONA_FAILED` | 健康时沉默 | 有依据的失败路径保留 |
| `GwpRuntimeFaultWatch.Guard` | 旧档错误依据被扩用到 28 处回调，开发版额外吞异常 | 待逐站点清理，不能按「故障观察」永久保留；见 code-health.md |

已退休的：送信队的 AI 诊断（2026-10-02 为查卖俘虏折返临时纳入，用户确认修复后退出）；`PLAYER_JUSTICE` 的 `companionCases` 字段与 `COMPANION_FINE_SETTLED`（2026-10-02 用户确认主队 NPC 旧案后删除；失败路径 `COMPANION_FINE_INCOMPLETE` 保留）；援军 `BATTLE_SCENE_OPENING` / `BATTLE_SCENE_SUPPORT` 与音乐档位 `SYNDICATE_BATTLE_DYNAMICS`（2026-09-26 用户确认全部实测项后删除，同时清掉了累积 2.9 MB 的 `GreyWarden-Faults.log` 与 9 月 15–17 日归档）；`GwpWarhorseDamageTrace`（2026-09-25 用户确认骑兵表现后删除代码）；`WARDEN_RESOLVE_BLOCK`（2026-09-25 士气改动时删除）；`GwpArcherContactTrace` 及其两类 Harmony 补丁（双刀互斥验收时删除）；
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
