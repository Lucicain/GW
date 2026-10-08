# 代码健康

> 当前状态：机械卫生检查通过；人工复核发现通用 Guard 依据与覆盖范围不匹配；客户端偶发原生退出崩溃根因未定
> 最后验收：`e16ff95`（异常留痕 + 销毁队伍模板）
> 已复核至：`a898237`（2026-10-08）
> 覆盖源码：`tools/Check-CodeHygiene.py` `tools/Check-StateFreshness.py` `tools/tests/test_project_checks.py` `GwpRuntimeFaultWatch.cs`
> 待实测：开新档后的第一次实玩（删掉无依据兜底后，原先静默跳过的故障会直接抛出，崩了由 agent 读崩溃报告修根因）；退出崩溃的最小差异对照；正常客户端在开发版 `Assets` 布局下偶发原生释放故障

## 客户端退出阶段的原生故障

2026-09-27 18:58 与 2026-09-28 00:55 两次客户端退出均在日志写出 `Managed Interface deleted` 后，由 `TaleWorlds.Native.dll` 报 `0xc0000005`；Windows 事件偏移分别为 `0x74e0c3`、`0x74e2b1`。两份转储的调用栈落在相同的原生退出释放循环，前者为无效指针写入、后者为无效指针读取；模组故障日志在退出时没有新增托管异常。7 月的转储 `31280` 也落在该原生释放调用链；可确认是旧的退出故障类别复发，尚不能证明损坏内存的是同一对象。同一 `Assets` 布局曾于 19:41 正常退出，因此故障是间歇性的。（依据：WER、RGL 日志与 cdb 转储 2026-07-20、2026-09-28）

当前 C# 源码没有运行时修改盾牌网格或材质，黑金盾仍引用静态网格；历史上已修复的运行时盾牌变更不能直接当作这次的根因。转储只有有限内存和公开导出符号，尚不能指认具体资源或原生对象。若继续隔离，应保持本机 live 的开发版布局，在隔离副本中分别对叠加层、无模组客户端及资源发布布局做可重复的退出对照，并记录 Windows 事件；一次正常退出不足以排除间歇故障。（依据：源码复核；崩溃转储；用户裁定的 live 布局）

## 性能：按频率排查，不按代码量

全量扫描 47,582 行 / 136 文件后**否掉了性能优化这个方向**：

- 92 处 `MobileParty.All` 全局遍历，**没有一处落在每队伍心跳里** —— 不存在 O(n²)
- 57 处 `GetCampaignBehavior<T>()`（原版是线性扫描），集中在对话与 UI 路径，
  不在热循环
- 208 处 AI 诊断调用整体包在 `GWP_DIAGNOSTICS` 里，**玩家包零开销**

真正在每帧烧 CPU 的只有两处（音乐渲染已优化，见 [`music.md`](music.md)）。

**担心 CPU 压力时，先按执行频率分层，不要按代码量猜。**（依据：流程；2026-09-17 全量扫描）

## 兜底必须兜住过东西

用户裁定（2026-10-05）：兜底必须真的兜住过东西，没触发过的就是垃圾。没人用的常量、成员同理。

**现状（2026-10-07 复核）**：源码共 14 个 catch，其中 5 个原样 `throw;`，9 个带 `依据：` 注释。机械检查为零不证明注释能支持当前所有调用路径；下述 `Guard` 仍有实际欠账。

带依据注释的站点包括：
   - 读档时 `Hero.FindFirst` 因英雄全局表未建好抛空引用（`GwpData` 两处、`GreyWardenTrainingBehavior`）；
   - `GwpRuntimeFaultWatch.Guard` 引用旧档 `gwp_case_support_requested` 类型不匹配；这条证据不足以支持目前覆盖的全部回调，见下方已知缺陷；
   - 自定义战斗里 `Clan.PlayerClan` 空引用（配乐）；
   - 原版 `Army.DisperseInternal` 空引用（协力解散）；
   - 野外执法拉不起战斗；
   - 没装 NavalDLC 时补丁绑定失败（`SubModule`）；
   - 首次异常处理器（CLR 语义：处理器里抛出的异常会顶替正在处理的原异常）。

原有依据来自实机故障日志及 journal。当前范围需逐站点复核，不能把同一个旧档错误当作新回调的触发证据。

- **已知缺陷**：`GwpRuntimeFaultWatch.Guard` 在开发版吞异常、玩家版直接执行，现有 28 处调用覆盖火誓者更新与战斗结算、派单、案件回执和会话回调；唯一 catch 依据仍是 2026-09-15 的旧档字段类型错误，不能证明这些路径都需要兜底。当前 `GreyWarden-Faults.log` 无 `GUARDED_FAILURE` 记录，未证明这些路径现在实际失败。诊断封装会改变两版的失败行为，后续清理应逐站点移除无依据吞异常；跨版本旧档读取已取消，不据此恢复兼容。（依据：源码与本机日志复核 2026-10-07；用户裁定 2026-10-05）

**以后**：
- 不写「以防万一」的 catch，让异常暴露，读崩溃、修根因。故障实际发生过，才在那一处加 catch 并写依据。
- `tools/Check-CodeHygiene.py` 检查两条：每个不重新抛出的 catch 都有 `依据：`；每个声明都有人引用。
  预提交钩子遇到违规会拒绝提交，新鲜度检查也会点名。（依据：用户裁定 2026-10-05）
- 删掉兜底的代价：原先被静默跳过的故障，现在会直接抛出。有的在战役心跳里，可能让游戏崩溃。
  这正是要的，崩了就读崩溃报告、修根因。

`GwpFaultTrace.WriteQuiet` 仍按站点去重（每个位置每局一次），调用点由编译器填。

## 双 BOM 陷阱

`GwpBattleReinforcementBehavior.cs`、`GwpBribeBarterable.cs`、
`PoliceAntiWarDeclaration.cs` 曾是**双 BOM** 文件头。第二个 BOM 会被当成正文，
使第一行实际是 `﻿using System;`——按行匹配的工具（包括自写脚本）会因此失配。
已归一化为单 BOM。

**写脚本处理源码时用 `utf-8-sig` 解码，并留意这个历史缺陷。**

## 有意不做的重构

14 个超过 120 行的方法（最长 `TrySpawnImmediateCaseInterceptor` 261 行、
`UpdateTasks` 221 行、`OnSessionLaunched` 211 行）**没有拆**。

收益是主观的，风险是实在的：这些方法早退分支密集，拆错一条就是行为变化。
**等到确实要改那块功能时顺手拆**，不要为了好看单独动它。（依据：设计）

## 联机适配

已停工并归档到 `coop-bridge` 分支。Coop 官方 mod 支持在其路线图 V3.0（0/2 未动）。
实测停在两处：「玩家可见文本发去了无人的服务器窗口」和「野外抓捕瞄不到玩家」。
剩下的不是补丁量，而是要重做整个玩家可见层。`main` 不带联机代码。

## 共享概念的归属

同一个判断写两份迟早分叉（2026-10-07 查出：战力三种算法、「是不是灰袍」8 份私有副本、案件状态 11 个文件都能改、
8 份手抄的差事避让清单；修补都只修了出问题的那一份）。现在每个共享概念只有一个归属方：

| 概念 | 归属 | 别处直接用就会被拦的写法 |
|---|---|---|
| 大地图战力 | `GwpStrength` | `EstimatedStrength`、`GetPowerOfParty`、`GetTroopPower`、`GetTotalLandStrength*` |
| 谁算灰袍 | `GwpCommon.IsGreyWarden*` | 家族 ID `PoliceClanId` |
| 案件状态 | `PoliceEnforcementBehavior.CaseLifecycle.cs` | `CrimePool`/`CrimeState` 的 `EndTask`、`ReleaseTask`、`CloseCase`、`ReopenCase` |
| 差事占用 | `GwpWardenDuties` | 其他系统的占用判断（`IsVillageReliefParty` 等） |
| 灰袍领主进聚落的意愿（候选、招兵数、打分、各项改动的来由） | `GwpWardenVisitDesire` | 别处对 `AiVisitSettlementBehavior` 打补丁 |

`tools/Check-CodeHygiene.py` 机械检查这张表，并找函数体完全相同的两个方法（重写的引擎事件除外）；
预提交钩子遇到就拒绝提交。出现新的共享概念时，给它一个归属，在脚本的 `OWNERS` 里加一行。（依据：用户裁定 2026-10-07）

## 接手与机械检查

- 战力归属包括尚未组队的士兵：`GwpStrength.LeaderlessRoster` 负责换算，火誓者只提供选出的兵种和数量；战力计算参数与整理前一致。
- 火誓者按村庄、仪式、媒体、军械库、经济和队伍拆分，入口在 [firesworn.md](firesworn.md)。文档保留当前规则与验收项，旧说法只在 journal 追溯。
- `Check-StateFreshness.py` 提示复核头部的格式、日期或提交不存在；不会新增提交硬闸。格式规范与提交稳定点只在 [AGENTS.md](../../../AGENTS.md) 定义。
- `python -m unittest discover -s tools/tests -p test_project_checks.py -v` 回归检查战力归属漏检、复核头部、提示与硬闸的区别。机械全零不代表正文语义正确，交接时仍需对照本次改到的源码。（依据：流程）
