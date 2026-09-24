# 战术切换与"前后踱步"

> 当前状态：**已结案、不改：踱步与射手异常均由原版战术规则决定，模组没有代码进入战术/编队决策路径；监控已退休**
> 最后验收：未建立检查点（无功能改动留在生产源码中）
> 覆盖源码：无（原版行为；调查用的 `GwpBattleCommandTrace.cs` 已删除）
> 待实测：双刀射手换武器修复后的表现，见文末「未闭合的疑点」第一条

## 结论

**不修改原版战术资格或评分。** 用户裁定：原版的问题不改。后续不得自行恢复任何隔离候选。
调查过程（2026-09-21 至 09-23 各场实机日志与对照）见 `docs/journal/2026-09.md`。

## 各现象的原版成因

| 现象 | 原版机制 |
|---|---|
| 步兵前后踱步（上层战术来回换） | 隘口自反馈循环：`TacticHoldChokePoint.IsTacticalPositionEligible` 要求主步兵 `MaximumWidth` ≥ 隘口宽；横队合格被选中，到位后 `BehaviorDefend` 在有盾时切盾墙，盾墙宽度不够又失格，退回横队再合格。`TeamAIComponent` 每 5 秒决策，现用战术 ×1.5 |
| 步兵前后踱步（战术不变、行为互换） | 同一战术内 `FormationAI.FindBestBehavior` 在守高地 / 战术冲锋间换选；`BehaviorComponent.GetAIWeight` 先乘导航目标位置处罚，守高地目标被导航替代时写入 0.2 处罚后逐步恢复 |
| 射手跟着步兵换令 | `TacticDefensiveEngagement.HasBattleBeenJoined()` 遇主步兵在冲锋即判已接战，射手在 Skirmish / SkirmishLine 间联动 |
| 射手“有箭却拔刀往前走” | 原版射手行为调整位置时整队 `HoldYourFire`（`BehaviorFireFromInfantryCover`：离主步兵和掩护点都远；`BehaviorScreenedSkirmish`：按几何关系），原生个体 AI 停火即换近战，不看箭袋。普通弓手同样如此，双刀只是更显眼 |
| 持刀时射程为 0、随后前冲 | 逐人原生射程取当前手持武器，持刀即 0；`BehaviorSkirmish` 在开火比例低且敌距 > 编队最大射程时进入接近，射程 0 时接近状态只能靠开火比例回升退出 |
| 射手老和步兵在一起 | `TacticDefensiveRing`：步兵围圈、射手 `FireFromInfantryCover` 在圈内。权重用 `min(步兵比, 射手比)`、地形、兵力比，比例来自装备而非手持，1:1 配比最高 |
| 射手转冲锋 / 往前送死 | `BehaviorCharge.CalculateAIWeight` 只看距离与速度；`TeamAIComponent.CheckIsDefenseApplicable` 在守方挨远程打远多于还击时关闭所有防守战术，转 `TacticFullScaleAttack`（灰袍长弓 156 米短于帝国弩 166 米时常见） |
| 终盘全员冲锋 | `TacticCharge.TickOccasionally` 给所有编队冲锋权重 10000，不查箭袋 |

隘口循环的触发窗口（单兵直径 0.76、横队间隔 0.76、盾墙间隔 0）：横队宽 ≈ 1.52n ≥ 隘口宽、
盾墙宽 0.76n < 隘口宽。`battle_terrain_biome_096` 的 `(904.036, 541.919)` 宽 40 隘口 →
守方主步兵 **27–52 名持盾兵**必然循环，与兵种来源无关。关模组对照局兵种不同，不构成同条件对照。

## 模组对战术系统的接触面

- 普通战斗中没有任何模组代码给编队下令、改阵型、写战术或行为权重
  （这些调用只在野外切磋任务 `GreyWardenFieldSparringMissionController`）。
- 当前 Harmony 补丁点没有一个进入战术或编队决策。
- 属性模型 `GwpAgentStatCalculateModel` 经 `BaseModel` 链到原版，全部虚方法透传，只给灰袍士兵加
  任务内临时技能；原版战术、行为、查询系统不读士气，死战不退不影响战术选择。
- 已知间接影响：灰袍不溃逃（战斗更长）、灰袍长弓射程较短、双刀让原版换近战更显眼。

## 相关原生机制（备查，不要据此改评分）

- `FormationAI.FindBestBehavior` 对现用行为额外 1.2–2 倍保留优势，新行为保留 10 秒
- `BehaviorSkirmish` 内部有 Shooting / Approaching / PullingBack 状态
- `MakingRangedAttackRatio` 是最近 10 秒发过射击动作的**人数占比**、缓存 3 秒，不是命中率
- `FormationQuerySystem.MaximumMissileRange` 取成员原生射程最大值，缓存 10 秒，只在读取 `Value` 且过期时刷新
- `BehaviorScreenedSkirmish` 等评分方法**本身会更新状态**，不能为打印再调用

## 已撤回/已隔离的候选（不要恢复）

| 候选 | 处置 |
|---|---|
| `GwpChokePointEligibilityPatch`（按预计盾墙覆盖宽度额外核对隘口资格） | 已从生产源码删除，未建立提交 |
| `GwpChokePointPolicy.cs` + `GwpChokePointFormationPatch.cs` | 停放在 `.codex_tmp/choke-spacing-candidate-not-deployed-20260921/`，从未部署，不是稳定回滚点 |

## 监控（已退休）

调查用的 `GwpBattleCommandTrace` 及双刀 AI 里的记录调用、测试桩已删除，日志中相关记录已清除。
`417a17b` 版可从 Git 取回；此后工作树追加的零射程、闲置近战、换武器计数监控从未提交，
需要时按本文件描述重写。

## 未闭合的疑点

用户裁定不再追查：

- 敌人贴身时，灰袍双刀射手在弓 / 双刀间 0.3–3 秒翻转。2026-09-24 反汇编找到了灰袍特有的成因，
  不是原版通则：原版每次决策都清理它不认识的副刀，而副刀在手时弓的得分被旗手惩罚压到 1e-6。
  修复见 [`dual-blade.md`](dual-blade.md)「原版 AI 换武器机制」「双刀切换实现」。
  修复后是否与普通弓手一致，待实测。
