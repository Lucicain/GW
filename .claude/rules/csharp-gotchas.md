---
paths:
  - "GreyWardenPolicePurity/**/*.cs"
  - "tools/**/*.cs"
---

# 这个仓库的 C# 陷阱

都是实际踩过的，不是通用建议。完整背景见 `GreyWardenPolicePurity/docs/state/code-health.md`。

## 不写没兜住过东西的兜底（依据：用户裁定 2026-10-05）

兜底必须真的兜住过东西；从没触发过的兜底是垃圾。

- 默认不写 try/catch：让异常暴露，读崩溃、修根因。
- 只有某次故障**已经实际发生过**（实机日志、崩溃转储），才在那一处加 catch，并在 catch 里写明它兜过什么：

  ```csharp
  catch (Exception gwpQuietFailure)
  {
      // 依据：实机诊断 2026-09-26（原版 Army.DisperseInternal 抛 NullReferenceException）
      GwpFaultTrace.WriteQuiet(gwpQuietFailure);
  }
  ```

- 清理后原样 `throw;` 的 catch 不算兜底，可以写。
- 「以防万一」「将来版本可能改名」「诊断不能影响游戏」都不是依据。
- `tools/Check-CodeHygiene.py` 检查每个不重新抛出的 catch 都带 `依据：`，预提交钩子据此拒绝提交。

## 没人用的常量和成员当场删（依据：用户裁定 2026-10-05）

改掉一套机制时，把它的调参常量、文本键、辅助方法一起删掉。新代码要用数字，就用或改 `GwpTuning` 里的那一个，
不要另写字面量、把旧旋钮留在那里和代码断开。`Check-CodeHygiene.py` 报出任何无人引用的声明，预提交钩子就拒绝提交。
引擎按约定调用的成员（Harmony 钩子、Gauntlet 命令、存档自动生成、接口成员、interop 字段）不算，脚本里有白名单。

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
