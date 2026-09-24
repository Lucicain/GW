# 灰袍兵种：等级、装备与战斗加成

> 当前状态：**已部署待实测**：2026-09-24 用户按加成清单重定规则（盾耐久统一两倍、临时技能上限 500、箭击倒 25%、去掉出招免伤与掉盾、骑士破防 25% 且架枪必破武器招架、骑枪回退 210 cm）
> 最后验收：未建立检查点
> 覆盖源码：`GwpTroopCombat.cs` `GwpAgentApplyDamageModel.cs` `GwpAgentStatCalculateModel.cs`（兵种数据在 `_Module/ModuleData/spnpccharacters.xml`）
> 待实测：新规则下三兵种的强弱；架枪必破武器招架是否引发崩溃（原生从不让被动攻击破防）

双刀、踢与盾击、替代攻击的击倒概率写在 [dual-blade.md](dual-blade.md)；本文件只写兵种本身。

## 兵种与等级

| 兵种 | id | 等级（阶） | 装备 |
|---|---|---|---|
| 灰袍新兵 | `gwnewrecruit` | 16（3 阶） | 三套预设，只差身甲 |
| 灰袍轻步兵 | `gwrecruit` | 21（4 阶） | 两套：步兵甲无盾 / 弓手甲 + 大盾 + 披风 |
| 灰袍重步兵 | `gwheavyinfantry` | 31（6 阶） | 卡拉德杖（原版 `empire_mace_3_t4`，Calradic Mace，帝国军团兵的副武器）、灰袍单手剑、灰袍大盾、重甲 |
| 灰袍弓箭手 | `gwarcher` | 31（6 阶） | 双刀、贵族长弓、穿刺箭 |
| 灰袍骑士 | `gwknight` | 31（6 阶） | 灰袍骑枪（可骑乘架枪，也可步行架枪）、灰袍双手剑；2026-09-24 起不再配单手剑和大盾 |

- 2026-09-24 用户：同阶碾压原版，所以各兵种等级标签整体升一阶（原 11/16/26）。
  技能与装备不变。军饷、招募价、升级经验、自动战斗强度随原版按等级变化。
- 模组内部只用 `Tier` 排序（练兵、订单、延迟纠察），整体升阶不改变相对顺序。
- 替代攻击击倒概率按兵种 id 区分，不看等级。

### 灰袍骑枪（`gwlance`，`items.xml`）

- 用原版 `TwoHandedPolearm` 锻造模板拼成。一个用法要生效，武器的每个部件都必须在该用法描述
  （`Native/ModuleData/weapon_descriptions.xml`）允许的部件清单里。
- 原握柄 `spear_handle_19`（210 cm）不在步行架枪 `TwoHandedPolearm_Bracing` 的清单里。原版握柄中只有
  `spear_handle_23`（250 cm）同时允许单手、双手、骑乘架枪、步行架枪四种用法。
- 现为 `spear_handle_23`、`scale_factor="84"`，长 210 cm，保留尾锤 `spear_pommel_9`。
  - 2026-09-24 曾放到 100（约同原版骑枪长度），用户随后要求回退。
- 没有改原版用法定义：那样会让原版所有用 19 号握柄的矛都能步行架枪。

### 整套预设（`GwpWholePresetEquipmentPatch`）

- 原版 `Equipment.GetRandomEquipmentElements` 把武器 0–1 格、武器 2–3 格、全部护甲，
  各自从一套**独立随机**的预设里取，所以多预设兵种会混搭。
- 这个前缀补丁让上表五个兵种（非英雄、战斗装备、预设 ≥ 2 套）**整套取同一套预设**。
- 保留原版的逐件随机品质。有种子时仍按种子确定，同一个兵每次出场长相一致。
- 只有一套预设的兵种走原版。

## 战斗加成

全部经伤害模型或普通战斗事件实现，**不补丁任何原生命中回调**。
踢与盾击的按阶击倒、越打越强的临时技能、大盾被动覆盖见 [dual-blade.md](dual-blade.md)。

### 弓箭手（`gwarcher`）

- **箭击倒 25%**：箭命中未骑乘的人、造成伤害时判定（2026-09-24 由 50% 降低）。
  - 在 `DecideAgentShrugOffBlow`（原版对一次身体命中问的第一个问题）里掷骰，中了就不许硬抗；
  - 随后 `DecideAgentKnockedDownByBlow` 返回真、`DecideAgentKnockedBackByBlow` 返回假（原地倒）。
- **箭对盾两倍伤害**：`CalculateShieldDamage` 对灰袍弓手的箭把原生盾伤 × 2。
- **一个箭袋，箭数翻倍**：`InitializeMissionEquipment` 在原生（含 Deep Quivers 等技能加成）之后，把箭袋里的
  箭数 × 2，并用 `SetAmountOfSlot(…, true)` 同步提高该箭袋的容量，拾箭也按新容量。箭袋数量不变。
- **双刀出招不被打断，伤害全吃**（攻击护甲，见 dual-blade.md）。2026-09-24 曾加过减伤 50%，已按用户要求去掉。

### 重步兵（`gwheavyinfantry`）

- 配卡拉德杖（见上表）。
- **大盾耐久两倍，主动、被动一个规则**：
  - 主动格挡：`CalculateShieldDamage` 把原生盾伤 × 0.5。
  - 被动覆盖挡下：`GwpShieldBashGuardPatch.ApplyPassiveShieldDurabilityDamage` 扣的就是这一次的盾伤。
    手持时这个值已经过伤害模型减半；背在背上时在这里减半。
  - 此前被动挡下扣 3 倍、每次至少 3 点，2026-09-24 用户要求统一，已删除；现在每次至少 1 点。
  - 灰袍弓手的箭 × 2 后再减半，打重步兵的盾时等于原生。

### 骑士（`gwknight`）

| 效果 | 普通攻击 | 架枪（骑乘或步行，原生“被动攻击”） |
|---|---|---|
| 击倒步行的敌人 | 12.5% | 50% |
| 打下马（敌方骑手，有伤害且不致死；骑士骑乘或步行都算） | 12.5% | 25% |
| 突破格挡（只对敌人） | 25%，招架与举盾都算 | 被武器招架**必破**；被盾挡住不破 |

- 击倒与打下马在 `DecideAgentShrugOffBlow` 掷骰，中了不许硬抗。
  - 击倒：`DecideAgentKnockedDownByBlow` 返回真、击退返回假。
  - 打下马：`DecideAgentDismountedByBlow` 返回真。
  - 踢和盾击走 dual-blade.md 的按阶规则，不参与。
- 破防在 `DecideCrushedThrough`；是否架枪由原生参数 `isPassiveUsageHit` 标明。
  ⚠️ 原生从不让被动攻击破防（只允许 `CanCrushThrough` 武器的上劈），这是本模组越出原生的一处。
- **马冲撞**：在原版冲撞规则内放宽，伤害不变（`GwpTroopCombat.IsKnightCharge`，只对骑手为灰袍骑士的马撞步行的人）。
  - 原生冲撞走 `Mission.ChargeDamageCallback`，攻击者是马，只依次问击退、击倒。
  - 撞飞：原生要求正面系数 ≥ 0.7，这里放宽到 ≥ 0.5。
  - 撞倒：仍须已撞飞、未硬抗；原生门槛“伤害 ≥ 最大血量 × (击倒抗性 − 冲撞穿透)”× 0.5。
  - 被冲撞的骑手完全按原版。
- **出招不被打断，伤害全吃**（攻击护甲，见 dual-blade.md）：手持灰袍骑枪或双手剑，正在蓄力、出手或架枪
  （`GwpTroopCombat.IsKnightAttacking`）。2026-09-24 曾加过减伤 50%，已去掉。
- **骑士与其坐骑硬抗门槛 × 3**：原生 `DamageInterruptAttackthreshold*`（砍/刺/钝均 5）→ 15，低于门槛不打断、
  不减速（`CalculateStaggerThresholdDamage`）。撞人本身的物理减速在引擎里，改不到。
- **马受惊人立减半**：原生判定人立、且骑手是灰袍骑士时，只有 50% 生效（`KnightHorseRears`）。
- **没有掉盾效果**：骑士的击倒不会让对方丢盾（2026-09-24 用户要求整体删除，此前两种击倒曾为 25%→50%）。
- 架枪命中后照原版收枪；“架枪不收枪”已删除。骑士在骑枪与双手剑之间的选择全由原生决定。

### 原版：骑兵冲撞把人撞倒（调研，未改）

原生 `MissionCombatMechanicsHelper` 与 `SandboxAgentApplyDamageModel`、`SandboxAgentStatCalculateModel`：

- **冲撞伤害**：
  - 攻击者是马（`MountChargeDamage` 设在马的驱动属性上），它的冲撞系数参与计算。
  - 相对速度 v 取骑手速度减去受害者在冲锋方向上的速度分量；d 为正面系数，取
    受害者相对碰撞点的方向与冲锋方向的点积，限 0–1。
  - 力度 = (v × d)² × d × 马冲撞伤害，按钝器过护甲。速度的平方主导，擦边或慢速几乎无伤害。
- **先要击退**：冲撞才会问击退，且 d ≥ 0.7（近乎正面撞上）才击退。
- **再判击倒**：必须已击退。满足条件时 “伤害 ≥ 最大血量 × max(0, 击倒抗性 − 0.4)” 即倒地：
  - 击倒抗性 = 0.4 + 0.001 × 运动技能（骑乘者另 +0.1），冲撞穿透固定为 0.4；
  - 所以步行者只要冲撞伤害 ≥ 最大血量 × 运动技能 / 1000 就倒。
    灰袍重步兵运动 130、血量 100，要 13 点；运动 30 的原版新兵要 3 点。
- **运动技能越高越难撞倒；重甲削弱钝器冲撞伤害**。灰袍步兵两项都高，这是原版规则下骑兵难以撞倒他们的原因。
- 本模组：灰袍骑士的马撞步行者时，撞飞角度放宽到 0.5、撞倒门槛减半，伤害不变（见「骑士」）。

### 高速冲锋被长矛一戳就停马

原生 `DecideMountRearedByBlow`（`MissionCombatMechanicsHelper`）的条件**全部**满足时，马受惊人立（`BlowFlags.MakesRear`），冲锋就此停住：

- 攻击者**步行**，武器带 `WideGrip`，长度 > 120 cm（长矛、长枪类）。
- 刺击，且枪尖命中（`ThrustTipHit`）。
- 被刺的是带 `CanRear` 标志的坐骑（马匹由引擎设置）。
- 马前进速度 > 5 m/s。
- 从马的正面刺中：刺击方向与马朝向的点积 < −0.35，命中点在马的前进方向上。
- 对马的伤害 ≥ `MakesRearAttackDamageThreshold` × 难度倍率。
  - 该参数在 `Native/ModuleData/managed_core_parameters.xml` 里为 **15**。
  - 难度倍率只对玩家与玩家一方生效（Sandbox 难度模型）。

冲锋速度会计入刺击伤害，高速时 15 点几乎总能达到，所以正面长矛刺中疾驰的马基本必停。
这不是概率判定。

**现行（2026-09-24 用户：不改原版机制，在它基础上概率降一半）**：
- 条件与门槛照原版，模组先委托原生判定。
- 原生判定人立、且这匹马的骑手是灰袍骑士时，只有 50% 真的人立（`KnightHorseRearShare`）。
- 其他马匹完全照原版。

## 崩溃：下马骑士在马匹附近与骑兵交战（2026-09-24，已不再复现，原因未单独确认）

- 现象：灰袍骑士下马后，在马匹附近与骑兵交战时引擎崩溃（`0xC0000005`，原生工作线程读空对象，
  栈上无托管帧）。
- 不再崩溃的那次测试**同时**有两处改动，所以只能确定原因在这两者之中：
  1. 破防不分敌我 → 只对敌人。最后一次崩溃前 0.03 秒正是一次对友军灰袍骑士的破防，用户认为这是原因。
  2. 关掉打下马。此前步行骑士强制打人下马，原生只允许步行钩类劈砍或 `CanDismount` 刺击。
- 现行：破防只对敌人；步行骑士打人下马已按用户要求恢复（概率减半）。
  - 若崩溃重现，真凶就是步行打下马。
  - 若不重现，就是友军破防。
- 排查中的其他怀疑不成立，均已恢复：
  - 骑枪尾锤、缩放 84；
  - 架枪 50% 破防。
- 排查中发现并保留的修正：破防只对敌人（原先会对友军灰袍骑士破防）。
- 排查用的逐次记录 `GWP_TROOP_EFFECT` 与开关文件 `GreyWarden-TroopEffects.txt` 已退休并删除。
  过程见 `docs/journal/2026-09.md` 同日各条。

## 与呼喊回归的关系

首轮实测后用户没有反馈呼喊问题（“没有问题，主要是效果”）。
2026-09-03 的二分把“一开战就没有呼喊”定位到弓箭效果那一半：
- 当时是 `Mission.MissileHitCallback` 的前置、后置、finalizer 补丁，
  加上 `Mission.HandleMissileCollisionReaction` 的前置补丁；
- 关掉这两个补丁类后呼喊恢复，但没有继续细分是哪一个。

这两个补丁类及 `GwpArcherArrowEffects.cs` 已删除。现行实现不补丁这两个方法，只用伤害模型。

## 诊断

已退休（2026-09-24 用户认可平衡后，同一轮删除）：
- 每场汇总 `GWP_TROOP_COMBAT`，统计击倒、冲撞撞倒、掉盾、打下马、破防、盾伤、攻击护甲减伤、箭袋翻倍、整套预设、马免人立；
- 只在诊断版注册的 `GwpTroopCombatBehavior`；
- 此前已退休的逐次记录 `GWP_TROOP_EFFECT` 与排查开关。

再调数值时按需加回，写法见 `docs/journal/2026-09.md` 同日各条。

## 回退

- 本轮以前的状态见提交 `d70aee3`：
  - 弓箭补丁在 `GwpArcherArrowEffects.cs`，两个补丁类 `Prepare() => false`；
  - 伤害模型里是 10% 击倒 / 5% 穿盾；
  - 骑士没有任何加成，重步兵会碎盾，没有整套预设；
  - 等级为 11/16/26。
- 回退时取回以下文件，删掉 `GwpTroopCombat.cs`，重建并镜像：
  `GwpArcherArrowEffects.cs`、`GwpAgentApplyDamageModel.cs`、`GwpAgentStatCalculateModel.cs`、
  `SubModule.cs`、`spnpccharacters.xml`。

## 未做

- 骑士在骑枪与双手剑之间的选择全由原生引擎决定。托管层只有 `AiWeaponFavorMultiplier{Melee,Ranged,Polearm}`（原生都设 1）
  与 `InvalidateAIWeaponSelections`。曾提议先加监控观察原生何时切换，未实施。
