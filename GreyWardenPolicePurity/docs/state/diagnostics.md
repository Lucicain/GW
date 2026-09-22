# 诊断系统

> 当前状态：现行规则
> 最后验收：不适用
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs`
> 待实测：无

## 原则

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内（源码中 18 个文件带此守卫），
所以退休一条 trace 只关乎开发者信噪比，**与玩家收到什么无关**。

## 加与删的判据

**卡住时加强。** 开发停在某个具体问题上时，加宽该功能的 trace、补上下一次测试需要
的字段，并在 state 文件里写清每条新行要回答什么问题。

**确认后退休。** 用户在实机确认功能可用时，**在建立检查点的同一个任务里**退休它的
诊断。不要让一个已经定下来的功能每 tick 汇报自己正常。

**按健康路径 vs 失败路径切，不按主题切。** 删掉在一切正常时会触发的 trace；
保留 `catch` 里和 "this should never happen" 分支里的——它们在健康的一局里保持沉默，
是未来游戏版本弄坏东西时唯一的信号。

**整个方法体只有一条 trace**，说明这个方法或补丁只为记录而存在，连同 trace 一起删。

**数据也要删。** 诊断系统退休时，同时删掉游戏 Documents 目录下的日志文件、
一次性崩溃转储和调查已闭合的反编译输出。仍在生效的诊断可以留，但它积攒的旧日志
不应该留——findings 记进 state 之后，旧日志没有进一步用途。

**按当前覆盖范围命名。** 范围变了就改系统名和日志文件名，不要留一个误导的名字。
（例：`GwpRangedCommandTrace` 扩到双方所有编队后已改名 `GwpBattleCommandTrace`。）

## 日志文件

游戏 Documents 目录下：

| 文件 | 内容 |
|---|---|
| `GreyWarden-Faults.log` | 故障与静默吞异常留痕。**健康的一局里这个文件应当是空的**，里面出现的任何一行都值得读 |
| `GreyWarden-AI-Diagnostics.log` | AI / 战场 / 欲望拍卖采样 |
| `GreyWarden-Case-Events.log` | 案件生命周期事件 |
| `GreyWarden-Diagnostics-Archive` | 滚动归档 |

源码中共 317 个 trace 标签。

## 在役、待退休

| 系统 | 覆盖 | 退休条件 |
|---|---|---|
| `GwpBattleCommandTrace`（`BATTLE_COMMAND_*`、`BATTLE_TACTIC_SCORE`、`BATTLE_WEAPON_CLASSIFY`） | 双方活跃人类编队的阵型/宽度/战术评分/武器分类 | 踱步问题结案后整体退休，见 [`battle-tactics.md`](battle-tactics.md) |
| `WARDEN_RESOLVE_BLOCK` | 死战不退拦截了哪条原生逃跑路径 | 用户已确认功能正常 → **下次动这块代码时退休** |
| `BATTLE_SCENE_OPENING` / `BATTLE_SCENE_SUPPORT` | 音乐资格分母、援军触发层/概率/预算/到场 | 用户已确认功能正常 → **下次动这块代码时退休** |
| 音乐策略档位诊断（每 10 任务秒 / 变档输出，含 `recent5`、`waiting-for-fresh-exchange`） | 强度映射是否按预期升降 | 用户已确认音乐正常 → **下次动这块代码时退休** |

已退休的：`GwpArcherContactTrace` 及其两类 Harmony 补丁（双刀互斥验收时删除）。
其日志、WER 与一次性分析输出**不再可读**；需要重新调查时从新复现取证，
不要把已退休路径写成仍可用文件。

## 限流是必需的

挂在每小时心跳或每 tick 上的诊断必须限流，否则会把日志刷爆，并毁掉
`GreyWarden-Faults.log` "健康即空文件" 这条性质。现行做法：

- `GwpFaultTrace.WriteQuiet` **按站点去重，每个位置每局只记一次**
- `WARDEN_RESOLVE_BLOCK` 每 Mission 每来源最多 8 条，由 `ConditionalWeakTable`
  随 Mission 回收
- 容量等待不刷空批次日志
- 战场采样 2 秒一次，不逐帧

## 不要为了打印而调用原版

`BehaviorScreenedSkirmish` 等评分方法**本身会更新状态**。要读原版评分只能用
postfix 旁观真实返回值，不能主动再调一次。
