---
paths:
  - "GreyWardenPolicePurity/**/*.cs"
  - "tools/**/*.cs"
---

# 这个仓库的 C# 陷阱

都是实际踩过的，不是通用建议。完整背景见 `GreyWardenPolicePurity/docs/state/code-health.md`。

## 新写 catch-all 照这个模式（依据：流程；2026-09-17 与 2026-09-24 两次补扫）

```csharp
catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
```

裸 `catch { }` 会让故障表现为"某个功能莫名其妙不再工作"，是最难查的一类。
`WriteQuiet` 按站点去重（每个位置每局一次），调用点由编译器填
（`CallerFilePath`/`CallerMemberName`/`CallerLineNumber`）——**不要手写标签**，
手写的迟早和代码漂移。

诊断基础设施自身（`GwpFaultTrace`、`GwpRuntimeFaultWatch`）里不要调，会递归。

## 写辅助方法要透传调用点（依据：流程）

把重复的 try/catch 收拢成辅助方法时，**必须带三个 `Caller*` 参数默认值透传下去**，
否则所有调用点的诊断都会记成辅助方法自己那一行，等于没记。
参考 `GwpCommon.TryDestroyParty`。

## 文件头可能是双 BOM（依据：实机观察 2026-09-17，三个文件）

仓库里曾有三个文件是双 BOM，第二个 BOM 被当成正文，使第一行实际是 `﻿using System;`。
写脚本处理源码时用 `utf-8-sig` 解码，按行匹配失配时先怀疑这个。

## 不要为了分类清除 `WeaponFlags` 掩码（依据：崩溃转储 2026-08-27）

历史上清除武器掩码有原生崩溃记录。双刀副刀保留 `MeleeWeapon` 是**刻意的**，
它因此 `IsShield = false`——这是正确结果，不是 bug。见 `docs/state/dual-blade.md`。

## 不要为了打印再调一次原版评分（依据：C#反编译 v1.4.8，2026-09-25 复核）

`BehaviorScreenedSkirmish.GetAIWeight` 等方法**本身会更新状态**。要读原版评分只能用
Harmony postfix 旁观真实返回值。

## 诊断都要在 `#if GWP_DIAGNOSTICS` 内

玩家包靠这个做到零开销，并且发布前会按 UTF-16LE + ASCII 双编码核查日志名串是否
真的不在 DLL 里。挂在每小时心跳或每帧上的诊断必须限流。

## 不做存档兼容（依据：用户裁定）

不要为救旧档写心跳、迁移或开局补正。直接改写入点。
