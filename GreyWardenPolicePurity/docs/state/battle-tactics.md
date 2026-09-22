# 战术切换与"前后踱步"

> 当前状态：**已明确不做** —— 用户裁定「如果是原版的问题就不改」
> 最后验收：不适用（无功能改动留在生产源码中）
> 待实测：用户尚未提交同地图换普通兵种的对照结果

## 结论

**不修改原版战术资格或评分。** 目前仅保留只读监控。
后续不得自行恢复任何隔离候选。

用户的原话要求：如果是原版的问题就不改，由用户换其他兵种对照。

## 现象与已确认的因果链

委托 NPC 指挥时，灰袍步兵与射手一起前后踱步，`TacticHoldChokePoint` 与
`TacticDefensiveEngagement` 约每 5 秒交替。

2026-09-21 13:04 场次（27 人主步兵、27 人真盾）读到完整链条：

```
t26.49  横队，最大宽 40.280  → 选中 40 米隘口(904.036,541.919)，守隘口评分 3.53963423
t66.49  行军 Loose，最大宽 79.800 → 守隘口评分 7.035155
t71.50  到位变 ShieldWall，最大宽 20.520 → 守隘口评分 0，DefensiveEngagement 1.13418853
t76.50  转回 Line，最大宽 40.280 → 守隘口评分 5.05661964，再次胜出
t81.50  盾墙 20.520 → 评分再归 0
```

**原版用当前阵型选位置，自己到位后的盾墙使该位置不合资格，退出战术变回横队又重新
合格。** 这是原版自反馈循环。

已排除的猜测：

- 不是双刀被算作盾（见 [`dual-blade.md`](dual-blade.md) 的分类事实与运行时读数）
- 不是全队换弓卡住：40 名灰袍持弓、可用远程、FireAtWill 均 40，
  `weaponLocked`/`pendingBow`/`gripBlocks2s`/`fleeing` 均 0，且持续有人射箭
- 不是音乐或援军直接下的移动命令
- 不是新死战机制：那只改 Retreat，见 [`warden-resolve.md`](warden-resolve.md)

**仍未做关闭模组的同场原版对照**，因此不能泛称所有间接模组影响都已排除。

## 相关原生机制（备查，不要据此改评分）

v1.4.8 反编译：

- `TeamAIComponent` 每 5 秒决策，当前战术权重乘 1.5
- `HoldChokePoint` 在存在相连远程防御点时给弓手 `BehaviorDefend` 权重 10；
  `DefensiveEngagement` 改回 `SkirmishLine`/`ScreenedSkirmish`
- 隘口资格读取主步兵 `MaximumWidth`；地点评分 = `Interval×(人数−1) + UnitDiameter×人数`
- `BehaviorDefend` 近点且 `HasShield` 时切盾墙；`HoldHighGround` 激活 Line
- `FormationAI.FindBestBehavior` 对当前行为额外 1.2–2 倍保留优势，
  新激活 `PreserveExpireTime = 当前 + 10 秒`
- `BehaviorSkirmish` 内部另有 Shooting/Approaching/PullingBack 状态；
  低开火比例、射程不足或持续 5–10 秒未有效开火会前进
- `MakingRangedAttackRatio` 是最近 10 秒发过射击动作的**人数占比**、缓存 3 秒，
  **不是命中率**

因此：「行为名字没变」不能证明没有来回下移动命令；「允许射击」不能证明有人实际射箭；
**不能为了打印再次调用 `GetAIWeight`** —— `BehaviorScreenedSkirmish` 等评分方法本身
会更新状态。

## 已撤回/已隔离的候选（不要恢复）

| 候选 | 处置 |
|---|---|
| `GwpChokePointEligibilityPatch`（按预计盾墙覆盖宽度额外核对隘口资格） | 已从生产源码**删除**。曾通过 24 项回归，无用户实机测试，未建立提交 |
| `GwpChokePointPolicy.cs` + `GwpChokePointFormationPatch.cs`（按预计盾墙间隔修正据点资格/评分） | 停放在 `.codex_tmp/choke-spacing-candidate-not-deployed-20260921/`，从未部署。隔离后源文件已去掉过期诊断调用，旧候选 DLL 不对应当前草稿，**不是稳定回滚点** |

## 在役监控

`GwpBattleCommandTrace`（原名 `GwpRangedCommandTrace`），全部在 `GWP_DIAGNOSTICS` 内：

- `BATTLE_COMMAND_SESSION` / `BATTLE_COMMAND_SAMPLE`：覆盖双方所有活跃人类编队，
  含阵型、间隔、宽度、最大宽度、实际 `HasShieldCached` 人数、灰袍持盾人数、
  原生缓存持盾比例
- `BATTLE_TACTIC_SCORE`：只在原版 `GetTacticWeight` **真实返回后**记录原始分值，
  不主动重算或改写评分
- `BATTLE_WEAPON_CLASSIFY`：每场每种双刀配置只记一次运行时实际分类

编队状态反射仅 `GetValue`，未修改原生私有字段。异常时停用本场采样并 `WriteQuiet`
一次。问题验收后整体退休。

## 下一步对照建议

保持同地图、守方、双方规模和委托指挥，**一次只改一项**：

1. 先把 27 名灰袍盾步兵换为 27 名普通盾步兵
2. 再单独替换弓手

现有监控覆盖普通兵，能读取相同两种战术的分值与宽度，**无需为对照再改 AI**。
