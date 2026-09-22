# 双刀 AI 与踢/盾击互斥

> 当前状态：用户已实测确认（「打了一把没报错了」）
> 最后验收：**`99f9426227284c7eba315b127e2a5fd422048bb1`**
> 覆盖源码：`GwpDualBlade*.cs` `GwpKick*.cs` `GwpShieldBashGuardPatch.cs` `GwpAlternativeAttack*.cs` `tools/DualBladeTests/**`
> 已复核至：`417a17b`（只加了一处诊断计数，功能未变）
> 待实测：无。改动双刀/踢盾击应以此提交为回退基线

## 已修复的崩溃

骑兵冲入弓兵群时原生崩溃（WER 27640 等）。根因：**原生换弓与模组踢击窗口重叠，
随后副手缺失，盾击分支读空武器**。

650 号射手的时序（来自接触日志，已固定证据副本）：

```
seq3997/t111.891  主1 副0，DefendShield，目标骑兵88/坐骑89
seq4007/t111.913  模组追加 Kick 至 112.263
seq4014           进入动作 31
seq4037/t111.951  原生发出 Wield2 + Sheath1
seq4038           模组仍追加 Kick
seq4052           同任务时间变为 主1 副-1，动作 31 继续
seq4408/t112.276  仍空副手盾击
```

注意 `KickAllEnd` 是枚举值 31 与 `WeaponBash` 同值导致的 ToString 别名，
**不代表动作已结束**。

## 修复实现

`GwpDualBladeActionGate`，只限已登记且带远程备选的双刀 AI：

- 踢/盾击动作进行中，暂时过滤 `Wield0–3`、`Sheath0/1` 和替代武器切换；动作结束后
  恢复原生换武器。
- 尚未接受的 Kick 遇到换武器时，**让换武器优先**，清除 Kick 与请求窗口。
- 不开始新 Kick 的条件：持弓、补刀序列中、未完成开场持弓、待射击命令、双手未稳定
  一个任务 tick。
- AI 直接进行的出生换弓、补刀/回退、射击命令换弓也检查活动动作、原生 Kick 和模组
  请求窗口。
- `RangedWieldPending` 保留射击命令至可执行时只调用一次；已持弓、停火、无弹药、
  近期近战、超时仍取消。**取消条件优先于强制换弓**，避免旧行为先换弓再取消。
- 移动标志和其他普通士兵不变。

历史教训：曾试过放宽收刀，引发 **384 次补刀循环**。不要在无证据时直接放开收刀。

## 双刀武器分类（原生事实，不要再推翻）

- `weapon_descriptions.xml` 两种模板均为 `OneHandedSword` + `MeleeWeapon`。
- NPC 射手默认 Ranged：Item0 副刀 `gwdualbladeoffhandai`、Item1 主刀
  `gwdualblademainhand`、Item2 贵族长弓、Item3 穿刺箭。
- `GwpDualBladeNpcItemSetup` 只给 **NPC** 副刀每种 usage 追加
  `HasHitPoints | CanBlockRanged` 与 500 耐久，**保留 `MeleeWeapon`**；玩家副刀不追加。
- `dual:shield` 是**动作使用特征**，不能据字符串认定为盾类。

### 副刀不是盾

原生 `WeaponComponentData.IsShield` 要求：先无 `WeaponMask`
（`MeleeWeapon | RangedWeapon`），再同时具备 `HasHitPoints` 和 `CanBlockRanged`。
保留 `MeleeWeapon` 的副刀因此 `IsShield = false`。
`MissionWeapon.GatherInformationFromWeapon`、`MissionEquipment.ContainsShield`、
`Agent.HasShieldCached`、`QueryLibrary.HasShield` 全部沿用此判断。
`FormationQuerySystem` 持盾比例 >= 0.4 才判有盾。

**不要为了分类去清除 `MeleeWeapon`** —— 历史上清除武器掩码已有原生崩溃记录；
底层二进制的格挡/换械决策仍可能读这些旗标。

运行时实测已排除"副刀被统计为盾、弓手因此变盾兵"的猜测：NPC 副刀
`flags=MeleeWeapon,HasHitPoints,CanBlockRanged`，`IsMelee=True`、`IsShield=False`；
玩家副刀只有 `MeleeWeapon`，同样 `IsShield=False`。

### 兵种统计

`Agent.IsRangedCached` 读取**整套装备**有无带弹药的非消耗性远程武器，
`QueryLibrary` 再结合是否骑乘判步兵/弓手/骑兵/骑射——**不是只看当前手持**。
所以有可用弓箭时拔双刀不会把弓手算成步兵；箭耗尽可以改变该统计，但不代表立刻换编队。

## 测试

`tools/DualBladeTests` 链接真实 AI/输入源码加内存桩，**18 项通过**：650 时序重放、
动作中保留武器、结束恢复换弓、尚未接受 Kick 与换武器冲突、请求窗口不补刀、
空手不 Kick、击退不补刀、延期射击一次执行、普通士兵不变。

桩不模拟原生物理，不能代替骑兵冲阵实测。

## 诊断现状

接触监控（`GwpArcherContactTrace` 及其两类 Harmony 补丁）已随验收**退休删除**。
`GwpDualBladeActionGate` 仅保留 `alternative && !paired` 的异常分支，标签
`DUAL_BLADE_INVALID_ACTION`，不再记录正常拦截。

`GwpDualBladeAiBehavior` 在单独收刀被拦时调一次 `GwpBattleCommandTrace.NoteGripBlock`
（`417a17b`，仅计数，在 `GWP_DIAGNOSTICS` 内）——踱步调查结束时随该监控一起退休。

退休后的日志、WER 及一次性分析输出**不再可读**；需要重新调查时应从新复现取证，
不要把已退休路径当成仍可用的文件。
