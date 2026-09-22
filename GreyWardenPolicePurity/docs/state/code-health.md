# 代码健康

> 当前状态：现行结论
> 最后验收：`e16ff95`（异常留痕 + 销毁队伍模板）
> 覆盖源码：跨文件，不做自动核对
> 待实测：无

## 性能：按频率排查，不按代码量

全量扫描 47,582 行 / 136 文件后**否掉了性能优化这个方向**：

- 92 处 `MobileParty.All` 全局遍历，**没有一处落在每队伍心跳里** —— 不存在 O(n²)
- 57 处 `GetCampaignBehavior<T>()`（原版是线性扫描），集中在对话与 UI 路径，
  不在热循环
- 208 处 AI 诊断调用整体包在 `GWP_DIAGNOSTICS` 里，**玩家包零开销**

真正在每帧烧 CPU 的只有两处（音乐渲染已优化，见 [`music.md`](music.md)）。

**担心 CPU 压力时，先按执行频率分层，不要按代码量猜。**

## 静默吞异常一律留痕

包住引擎调用的 catch-all 本身是合理防御——清理流程中途抛出去会把后面还没清的一并
带停。问题是不留痕：故障只表现为"某个功能莫名其妙不再工作"，是最难查的一类。

原状：空 `catch` 共 93 处，**只有 4 处写了为什么吞，89 处什么都没有**。
89 处全部改为：

```csharp
catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
```

**行为不变**（照样吞），只是出事时留一条。

`GwpFaultTrace.WriteQuiet` 的两个关键设计：

- **按站点去重，每个位置每局只记一次。** 保住 `GreyWarden-Faults.log`
  "健康的一局里是空文件" 这条性质。
- **调用点由编译器填**（`CallerFilePath` / `CallerMemberName` / `CallerLineNumber`），
  不手写标签——手写的标签迟早会和代码漂移。

跳过了诊断基础设施自身（`GwpFaultTrace`、`GwpRuntimeFaultWatch`）以免递归，
以及那 4 处已写明理由的。

**新写 catch-all 时照这个模式写。**

## 辅助方法必须透传调用点

`GwpCommon.TryDestroyParty(party)` 收拢了 6 个文件里重复 11 次的
`try { DestroyPartyAction.Apply(null, X); } catch { }`。

关键：辅助方法**必须把调用点信息透传下去**（三个 `Caller*` 参数默认值），
否则 11 处的诊断会全部记成 `GwpCommon.cs` 自己那一行，等于没记。

只做 null 检查，**不加 `IsActive` 判断**——那会改变行为。

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
**等到确实要改那块功能时顺手拆**，不要为了好看单独动它。

## 联机适配

已停工并归档到 `coop-bridge` 分支。Coop 官方 mod 支持在其路线图 V3.0（0/2 未动）。
实测停在两处：「玩家可见文本发去了无人的服务器窗口」和「野外抓捕瞄不到玩家」。
剩下的不是补丁量，而是要重做整个玩家可见层。`main` 不带联机代码。
