# 代码健康

> 当前状态：已确认的配装、紧急补兵、编号与调兵状态缺陷已修复；通用吞异常包装已移除；正常卡顿耗时尚未测定
> 最后验收：`e16ff95`（异常留痕 + 销毁队伍模板）
> 已复核至：`9bd6ce3`（2026-10-14）
> 覆盖源码：`tools/Check-CodeHygiene.py` `tools/Check-StateFreshness.py` `tools/tests/test_project_checks.py` `GwpRuntimeFaultWatch.cs`
> 待实测：开新档后的第一次实玩（删掉无依据兜底后，原先静默跳过的故障会直接抛出，崩了由 agent 读崩溃报告修根因）；退出崩溃的最小差异对照；正常客户端在开发版 `Assets` 布局下偶发原生释放故障

## 客户端退出阶段的原生故障

2026-09-27 18:58 与 2026-09-28 00:55 两次客户端退出均在日志写出 `Managed Interface deleted` 后，由 `TaleWorlds.Native.dll` 报 `0xc0000005`；Windows 事件偏移分别为 `0x74e0c3`、`0x74e2b1`。两份转储的调用栈落在相同的原生退出释放循环，前者为无效指针写入、后者为无效指针读取；模组故障日志在退出时没有新增托管异常。7 月的转储 `31280` 也落在该原生释放调用链；可确认是旧的退出故障类别复发，尚不能证明损坏内存的是同一对象。同一 `Assets` 布局曾于 19:41 正常退出，因此故障是间歇性的。（依据：WER、RGL 日志与 cdb 转储 2026-07-20、2026-09-28）

当前 C# 源码没有运行时修改盾牌网格或材质，黑金盾仍引用静态网格；历史上已修复的运行时盾牌变更不能直接当作这次的根因。转储只有有限内存和公开导出符号，尚不能指认具体资源或原生对象。若继续隔离，应保持本机 live 的开发版布局，在隔离副本中分别对叠加层、无模组客户端及资源发布布局做可重复的退出对照，并记录 Windows 事件；一次正常退出不足以排除间歇故障。（依据：源码复核；崩溃转储；用户裁定的 live 布局）

## 性能：按频率排查，不按代码量

2026-10-10 对当前 168 个源码文件重新盘点，发现 `MobileParty.All` 的 95 处文字出现（含注释，不等于运行次数）。当前存在嵌套扫描，不能沿用旧调查的「没有 O(n²)、否掉性能优化」结论。

- 火誓者每小时 `TendFaithParties` 遍历全部登记队，每条记录的 `FindParty` 都线性遍历全部 `MobileParty.All`：登记队 F、地图队 P 时为 F×P。
- 每小时 `RefreshArmoryPlans` 也复用上述线性查找。出队 `TakeBestSets` 已直接按出队名册配装，不再经过留守村的增长缺额查询。现行实现有复用入口，却没有复用同一轮查询结果。
- 军械库 `BuildPlan` 把库存按每一件展开成列表，逐兵调用 `Take -> FindIndex + RemoveAt`；移走列表前项需要移动后续元素，库存大时也有二次方成本。已有小时级 `_plans` 缓存，不能说没有缓存；`TakeBestSets` 则直接重建计划，日结 `RebalanceArmories` 前后各重建全库。
- 上次卡死的两份主线程快照已落在火誓者统计 / 军械库链上，但现场受零人派队无限增长污染。它证明这条链会放大失控，不能据此量化正常存档的卡顿占比，也不能证明历次卡顿都是同一原因。（依据：源码、实机诊断 2026-10-09；复核 2026-10-10）

原版 v1.4.8 `CampaignObjectManager.Find<T>` 内部也是线性查表，不是换一个原版方法就能得到索引。后续先测同一小时各段实际耗时，再决定是否以单次处理范围内的快照合并重复查询；全局长期缓存另需证明创建、销毁和读档的一致性。附近敌人已用原版 `FindNextLocatable` 空间搜索，无依据改成另一套邻域查询没有收益。（依据：C#反编译 v1.4.8；源码复核 2026-10-10）

## 兵员转移与临时队编号

共享规则与原版依据只在 [临时队](temporary-parties.md) 定义。练兵、订单、追截、送信归队与火誓者名册转移统一调用 `GwpTroopTransfer`，各自保留原选兵策略；建队统一调用 `GwpPartyIdentity`。实际 v1.4.8 名册与注册表回归 173 项断言通过，包含伤兵、经验余数、多来源回滚及改名后连续建队查重。没有新增存档字段。（依据：源码、C#反编译与实际游戏 DLL 离线执行 2026-10-14）

## 循环可靠性复核

循环按明确进展核对终止依据：协力增员后候选被差事占用排除，补护卫的人数上限递增，配乐队列会出队，位读取会前进或抛出越界，UI 沿父级前进；没有再确认一处与旧巡逻相同的零人数循环。临时队查重不再随机重试，候选后缀每次严格递增，受有限注册表条目数约束。

出队少领装备已修复（见 [军械库](firesworn-armory.md)），直接抽取生产方法、替换引擎边界的回归通过；调兵状态和编号另用实际游戏 DLL 验证。机械卫生全零只证明当前脚本的四种规则，没有证明上述语义和性能。（依据：源码、离线执行与运行时反射 2026-10-10）

## 兜底必须兜住过东西

用户裁定（2026-10-05）：兜底必须真的兜住过东西，没触发过的就是垃圾。没人用的常量、成员同理。

**现状**：无依据的通用 `Guard` 与全部 30 处包装已移除；开发版和玩家版均直接执行业务回调，故障观察器只记录异常，不改变业务失败行为。其余 catch 的机械检查全零，实际依据仍逐站点复核。（依据：源码与日志复核 2026-10-14）

带依据注释的站点包括：
   - 读档时 `Hero.FindFirst` 因英雄全局表未建好抛空引用（`GwpData` 两处、`GreyWardenTrainingBehavior`）；
   - 自定义战斗里 `Clan.PlayerClan` 空引用（配乐）；
   - 原版 `Army.DisperseInternal` 空引用（协力解散）；
   - 野外执法拉不起战斗；
   - 没装 NavalDLC 时补丁绑定失败（`SubModule`）；
   - 首次异常处理器（CLR 语义：处理器里抛出的异常会顶替正在处理的原异常）。

原有依据来自实机故障日志及 journal。当前范围需逐站点复核，不能把同一个旧档错误当作新回调的触发证据。


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
| 兵员人数、伤兵与经验搬运 | `GwpTroopTransfer` | 各功能选兵后调用公共搬运入口；语义由实际游戏名册回归检查，不声称正则覆盖所有扣人加人代码 |
| 临时队唯一编号 | `GwpPartyIdentity` | `FindNextUniqueStringId<MobileParty>`；查重与严格递增在本入口完成 |
| 讲和（含玩家战斗时序、灰袍交战理由） | `GwpPeace` | `FactionManager.SetNeutral`、`MakePeaceAction.Apply`；规则见 [调停](warden-mediation.md) |
| 大地图战力 | `GwpStrength` | `EstimatedStrength`、`GetPowerOfParty`、`GetTroopPower`、`GetTotalLandStrength*` |
| 谁算灰袍 | `GwpCommon.IsGreyWarden*` | 家族 ID `PoliceClanId` |
| 案件状态 | `PoliceEnforcementBehavior.CaseLifecycle.cs` | `CrimePool`/`CrimeState` 的 `EndTask`、`ReleaseTask`、`CloseCase`、`ReopenCase` |
| 差事占用 | `GwpWardenDuties` | 其他系统的占用判断（`IsVillageReliefParty` 等） |
| 守村经验分配与容量 | `GwpFireswornBehavior.GrantGuardExperience`（`GwpFireswornExperience.cs`） | `CanTroopGainXp`；烧人经验调用这一入口 |
| 守村灰袍升级付款人 | `GwpFireswornUpgradePayment` | 升级模型只选择当前兵种，原版Owner钩子只查询这一处；公库钱包仍由 `PoliceResourceManager` 定义 |
| 灰袍领主进聚落的意愿（候选、招兵数、打分、各项改动的来由） | `GwpWardenVisitDesire` | 别处对 `AiVisitSettlementBehavior` 打补丁 |

`tools/Check-CodeHygiene.py` 机械检查这张表，并找函数体完全相同的两个方法（重写的引擎事件除外）；
预提交钩子遇到就拒绝提交。出现新的共享概念时，给它一个归属，在脚本的 `OWNERS` 里加一行。（依据：用户裁定 2026-10-07）

## 接手与机械检查

- 战力归属包括尚未组队的士兵：`GwpStrength.LeaderlessRoster` 负责换算，火誓者只提供选出的兵种和数量；战力计算参数与整理前一致。
- 火誓者按村庄、仪式、媒体、军械库、经济和队伍拆分，入口在 [firesworn.md](firesworn.md)。文档保留当前规则与验收项，旧说法只在 journal 追溯。
- `Check-StateFreshness.py` 提示复核头部的格式、日期或提交不存在；不会新增提交硬闸。格式规范与提交稳定点只在 [AGENTS.md](../../../AGENTS.md) 定义。
- `python -m unittest discover -s tools/tests -p test_project_checks.py -v` 回归检查战力归属漏检、复核头部、提示与硬闸的区别。机械全零不代表正文语义正确，交接时仍需对照本次改到的源码。（依据：流程）
