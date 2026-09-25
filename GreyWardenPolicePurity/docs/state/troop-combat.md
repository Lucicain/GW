# 灰袍兵种：等级、装备与战斗加成

> 当前状态：**已部署待实测**：2026-09-25 攻击加成从兵种改绑到灰袍武器（武器词条），新增 `gwmace`、`gwarrows`；同日战马加强减半、箭数恢复原版、删除骑士霸体与远程减半、骑手伤害与马各担一半；此前基线提交 `6b4b1dd`
> 最后验收：未建立检查点
> 已复核至：2026-09-25 当前战斗源码、装备数据；新 live DLL `4954B739…034F96`
> 覆盖源码：`GwpTroopCombat.cs` `GwpAgentApplyDamageModel.cs` `GwpAgentStatCalculateModel.cs`（大盾被动耐久在 `GwpShieldBashGuardPatch.cs`，见 dual-blade.md；兵种数据在 `_Module/ModuleData/spnpccharacters.xml`、`items.xml`、`gwp_monsters.xml`）
> 待实测：武器词条——步兵剑与杖的击倒/打下马/破防、双刀固定 40% 击倒、领主与玩家拿灰袍武器同样生效、新杖与新箭在预设里正常出现；伤害各担一半后骑兵与步兵、弓手的强弱；战马冲撞与抗控制；架枪必破武器招架的稳定性

双刀、踢与盾击、替代攻击的击倒概率写在 [dual-blade.md](dual-blade.md)；本文件写兵种、装备与武器词条。

## 兵种与等级

| 兵种 | id | 等级（阶） | 装备 |
|---|---|---|---|
| 灰袍新兵 | `gwnewrecruit` | 16（3 阶） | 三套预设，只差身甲 |
| 灰袍轻步兵 | `gwrecruit` | 21（4 阶） | 两套：步兵甲无盾 / 弓手甲 + 大盾 + 披风 |
| 灰袍重步兵 | `gwheavyinfantry` | 31（6 阶） | 灰袍卡拉德杖 `gwmace`（原版 Calradic Mace 的复制品）、灰袍单手剑、灰袍大盾、重甲 |
| 灰袍弓箭手 | `gwarcher` | 31（6 阶） | 双刀、贵族长弓、灰袍穿刺箭 `gwarrows` |
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
- 现为 `spear_handle_23`、`scale_factor="100"`，约 250 cm，保留尾锤 `spear_pommel_9`。
  - 2026-09-24 先放到 100、又回退到 84（210 cm），当日用户再要求加长，近身改由双手剑负责（见「骑士」）。
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

### 武器词条（`GwpWeaponTraits`，`GwpTroopCombat.cs`）

2026-09-25 起攻击加成**绑在武器上，不绑兵种**（用户：统一度量衡，步兵也要有）：谁拿灰袍武器谁就有，
包括领主、玩家、新兵；灰袍兵拿原版武器则没有。按物品 id 查表，数值沿用此前各兵种的值。

| 物品 | 击倒步行者 | 打下马 | 被武器招架时破防 | 其他 |
|---|---|---|---|---|
| 灰袍单手剑 `gwonehandedsword`、灰袍卡拉德杖 `gwmace`、灰袍双手剑 `gwtwohandedsword` | 12.5% | 12.5% | 25% | |
| 灰袍骑枪 `gwlance` | 12.5% | 12.5% | 25% | 架枪（骑乘或步行）：击倒 50%、打下马 25%、**必破**武器招架 |
| 双刀主手、副刀 | 40% | 12.5% | 25% | |
| 灰袍穿刺箭 `gwarrows` | 25% | — | — | 对盾伤害 × 2 |
| 灰袍大盾 `wlarge_shield`、`wlarge_shield_black` | — | — | — | 自身受到的盾伤 × 0.5（耐久两倍） |

- 物品来源：`gwmace` 是原版 `empire_mace_3_t4`（Calradic Mace）的复制品，`gwarrows` 是原版 `piercing_arrows`
  的复制品（箭数 23），只改 id 和名字，好让词条不落到原版物品上。重步兵、弓箭手预设已换成这两件。
- 近战取攻击者那一格武器（`AttackCollisionData.AffectorWeaponSlotOrMissileIndex`）；箭按投射物序号在
  `Mission.MissilesList` 里找回物品。
- 击倒与打下马在 `DecideAgentShrugOffBlow`（原版对一次身体命中问的第一个问题）里掷骰，中了不许硬抗。
  - 目标没骑马 → 掷击倒：`DecideAgentKnockedDownByBlow` 返回真、击退返回假（原地倒）。
  - 目标是骑手 → 掷打下马（有伤害且不致死）：`DecideAgentDismountedByBlow` 返回真。
  - 替代攻击（踢、盾击）不走武器词条，走 dual-blade.md 的按阶规则。
- 破防在 `DecideCrushedThrough`，只对敌人、只对武器招架，举盾照原版；是否架枪由原生参数 `isPassiveUsageHit` 标明。
  ⚠️ 原生从不让被动攻击破防（只允许 `CanCrushThrough` 武器的上劈），这是本模组越出原生的一处。
- 盾伤在 `CalculateShieldDamage`：先乘攻击武器的 `ShieldDamageMultiplier`，再乘被击中的盾的
  `ShieldDamageTakenMultiplier`。灰袍箭打灰袍大盾等于原生。
- 大盾被动覆盖挡下：`GwpShieldBashGuardPatch.ApplyPassiveShieldDurabilityDamage` 扣的就是这一次的盾伤；
  手持时已经过伤害模型减半，背在背上时在那里按大盾词条减半。每次至少 1 点。
- 没有掉盾效果（2026-09-24 用户要求整体删除）。

### 弓箭手（`gwarcher`）

- **箭数原版**：2026-09-24 至 25 曾把箭袋里的箭数 × 2，2026-09-25 按用户要求恢复原版（`DoubleArcherQuivers` 已删）。
- **双刀出招不被打断，伤害全吃**（攻击护甲，见 dual-blade.md）。2026-09-24 曾加过减伤 50%，已按用户要求去掉。

### 骑士与灰袍战马

- **灰袍战马冲撞**：在原版冲撞规则内放宽，伤害不变（`GwpTroopCombat.IsWarhorseCharge`，只对有骑手的 `gw_warhorse` 撞步行的人，骑手身份不限）。
  - 原生冲撞走 `Mission.ChargeDamageCallback`，攻击者是马，只依次问击退、击倒。
  - 撞飞：原生要求正面系数 ≥ 0.7，这里放宽到 ≥ 0.6。
  - 撞倒：仍须已撞飞、未硬抗；原生门槛“伤害 ≥ 最大血量 × (击倒抗性 − 冲撞穿透)”× 0.75。
  - 2026-09-25 两项加强幅度减半（原为 ≥ 0.5 与 × 0.5）。
  - 被冲撞的骑手完全按原版。
- **没有出招霸体**：2026-09-24 至 25 骑士用骑枪或双手剑出招、架枪时不被打断、击退、击倒、打下马，2026-09-25 按用户要求删除（`IsKnightAttacking` 已删）。
- **只有灰袍战马小伤害硬抗门槛 × 2**（2026-09-25 前为 × 3）：原生 `DamageInterruptAttackthreshold*`（砍/刺/钝均 5）→ 10，低于门槛不打断、不因受击反应减速（`CalculateStaggerThresholdDamage`）；骑士本人恢复原版门槛，也没有出招霸体。撞人本身的物理减速在引擎里。
- **灰袍战马受惊人立少四分之一**：原生判定人立后，`gw_warhorse` 只有 75% 生效（`WarhorseRears`，2026-09-25 前为 50%），不看骑手兵种。
- 架枪命中后照原版收枪；“架枪不收枪”已删除。
- **灰袍战马**（`gwwarhorse`，`items.xml`；物种 `gw_warhorse`，`gwp_monsters.xml`）：
  - 外观同原版 Canterion Charger（`t2_empire_horse`）；兵种预设只配给灰袍骑士，任何人实际骑上它都享受下述马相关规则；灰袍领主模板仍是原版马。
  - 体长 118（缩放 1.18）、重 505、冲撞伤害 26（同 Canterion；当日先用 36，用户反映撞击伤害太高）、速度 50、机动 60。
    马本身的碰撞质量约 830，为 Canterion（460 × 1.12³ ≈ 646）的 1.28 倍（质量 = 重量 × 缩放³ + 骑手 + 负重）。
    2026-09-25 前为体长 125、重 550，约 1074，即 1.66 倍（此前文档误写为 1.4 倍）。
  - 血量同 Canterion（200 + 10）。当日曾给两倍，用户认为太高。
  - 冲撞门槛 3.65 m/s（原版马 4.3，2026-09-25 前为 3.0）：速度降下来也还算冲撞，击退照「马冲撞」规则。
  - 物种继承原版 `horse`，只覆盖 `relative_speed_limit_for_charge`。
- **没有冲撞掉武器**：2026-09-24 曾加过撞倒时 25% 让对方丢武器，当日按用户要求删除。
- **选武器完全按原版**（2026-09-24 用户：别改选择武器欲望系统）。当日试过的固定近战偏好 × 5
  （实测远处拿剑、冲近架枪）和"缠斗时 × 20"都已删除。
- **没有远程减伤**：2026-09-24 至 25 曾让投射物打灰袍战马及其骑手 × 0.5，2026-09-25 按用户要求删除，现为原版。
- **伤害马和骑手各担一半**：任何角色骑在活着的 `gw_warhorse` 上时，身体被命中的伤害一半（四舍五入，`WarhorseRiderDamageShare`）转给这匹马，其余骑手自己吃；马本身被命中的伤害只由马承担，不转给骑手。灰袍骑士骑其他马时照原版受伤。2026-09-25 前为全部转给马、骑手 0 伤害。
  - 以坐骑的 `Monster.StringId == gw_warhorse` 判定，在原生伤害流水线最后一步 `ApplyGeneralDamageModifiers` 处理（`GwpTroopCombat.ApplyWarhorseDamageRules`），不以骑手兵种 id 判定。
  - **转移的是骑手本应扣的实际伤害**：原版先按骑手命中部位、护甲、战斗难度等算出 `InflictedDamage`，再调用本模组伤害模型；转移时不把 `AbsorbedByArmor` 加回。玩家难度倍率对亲自骑乘者生效，普通 NPC 不因此受益。
  - 转移的伤害下一帧照原生溺水伤害的做法登记到马身上（`Blow` + `GetAttackCollisionDataForDebugPurpose`
    + `RegisterBlow`），击杀记在原攻击者名下（`GwpWarhorseDamageTransferBehavior`）。
  - 不转移：被盾挡住、打在背后的盾、被武器挡开、摔落伤害，这些照原版（盾照样掉耐久）。
  - 骑手留下的一半照原版结算：扣血、受击反应、打下马判定都按这一半伤害。只有这一击转给马的份额四舍五入为 0 时（如 1 点），整击留给骑手。
  - 自定义战斗的玩家难度是全局选项“玩家受到伤害”：当前游戏 DLL 将配置值 `0/1/2` 映射为 `0.25/0.5/1` 倍，`2` 为“写实”；中文简单档的百分比副标题与 DLL 数值不符。难度模型把被击中的坐骑映射回骑手。旧归属版本的 25/25 次转移实测见 journal，不能代替这次新归属的实机验收。

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

### 原版：骑兵选武器与冲撞的数据入口（反汇编实证，2026-09-24，未改）

地址与方法同 [dual-blade.md](dual-blade.md)「原版 AI 换武器机制」。

- **骑枪还是双手剑**（模式决策 `0x1806ae9f0`）：
  - 近战分 = 5 ÷ (距离 + 0.01) × 武器分 × `AiWeaponFavorMultiplierMelee`；
    长杆分 = (5 ÷ (距离 + 0.01) **+ 10**) × 武器分 × `…Polearm`。
    +10 只给用法组带 `PassiveUsage`（可架枪）的长杆，也就是骑乘时的骑枪（`0x1806aeefc`）。
  - 当前手持武器正处于 `PassiveUsage` 用法（正在架枪）时，近战分不计算，保持骑枪（`0x1806af00f`）。
  - 结果：骑乘时两把武器分数相近的话，2 米处骑枪约 12.5 对双手剑 2.5，骑士几乎总用骑枪。
    步行时架枪用法要求不骑马，没有 +10，按距离公平比较。
  - 出生手持第一件非副手武器，即 Item0 骑枪。
- **冲撞入口**：
  - 能否算冲撞：物种参数 `relative_speed_limit_for_charge`（原版马 4.3 m/s，骆驼 5.0），
    出生时经 `Agent_spawn_data`（偏移 0x60）写入引擎 agent（`0x1805cd567`）。
  - 碰撞质量：出生数据里的体重取**马匹物品的 `weight`**（`FillSpawnData`），
    引擎总质量 = 体重 × 体型缩放³ + 武器负重 + 护甲负重 + 骑手（`0x180694a00`）。
    马的体型缩放 = `body_length` ÷ 100（`SetInitialAgentScale`）。
  - 冲撞伤害（托管）= (相对速度 × 正面系数)² × 正面系数 × `MountChargeDamage`；
    `MountChargeDamage` = 马匹物品 `charge_damage` × 0.01（含马具修正）。
  - 撞人后减速在引擎物理里，受质量影响，具体公式未解码。托管层没有减速代码
    （`SelfInflictedDamage` 只用于多人误伤）。
- 灰袍骑士现用原版 `t2_empire_horse`（Canterion Charger）：重 460、`body_length` 112、冲撞 26、速度 50、
  机动 60，物种 `horse`。原版顶级战马可到重 550、冲撞 36。

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
- 原生判定人立、且这匹马是灰袍战马时，只有 75% 真的人立（`WarhorseRearShare`；2026-09-25 前为 50%，更早按骑手是灰袍骑士判断）。
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

没有在役诊断。战马伤害调查用的 `GwpWarhorseDamageTrace` 在 2026-09-25 用户确认骑兵表现后删除
（结论见 journal 2026-09-24/25 各条）；它的日志已一并删除。

已退休（2026-09-24 用户认可平衡后，同一轮删除）：
- 每场汇总 `GWP_TROOP_COMBAT`，统计击倒、冲撞撞倒、掉盾、打下马、破防、盾伤、攻击护甲减伤、箭袋翻倍、整套预设、马免人立；
- 只在诊断版注册的 `GwpTroopCombatBehavior`；
- 此前已退休的逐次记录 `GWP_TROOP_EFFECT` 与排查开关。

再调数值时按需加回，写法见 `docs/journal/2026-09.md` 同日各条。

## 回退

- 武器词条（2026-09-25，未提交）之前：把 `.codex_tmp/weapon-traits-prechange-20260925/` 中的
  `GwpTroopCombat.cs`、`GwpAgentApplyDamageModel.cs`、`GwpShieldBashGuardPatch.cs` 覆盖同名源码，
  三个 XML 覆盖 `_Module/ModuleData/items.xml`、`spnpccharacters.xml`、
  `Languages/CNs/std_gwp_strings_xml-zho-CN.xml`，重建并镜像。那时效果按兵种：弓手箭 25% 击倒与盾伤 × 2、
  重步兵大盾 × 0.5、骑士表中各值、双刀击倒 = 踢击按阶概率 × 0.5；步兵普通攻击无加成。

- 本次将骑乘效果从 `gwknight` 转到 `gw_warhorse` 前的未提交实现：基线提交 `6b4b1dd` 加 `.codex_tmp/horse-ownership-prechange-20260925/` 中的 `GwpTroopCombat.cs`、`GwpAgentApplyDamageModel.cs`、`SubModule.cs` 快照；分别覆盖同名源码、重建并镜像即可恢复旧归属。快照的 SHA-256 依次为 `D4837288…75344F6`、`7650EE18…AF08FEA`、`A6640C08…E62974ABF`。旧 state 头部与规则另存同目录 `troop-combat.md`，若回退也要原地恢复状态说明。

- 骑兵加强（2026-09-24，未提交）之前的状态即提交 `6b4b1dd`：撤回 `GwpTroopCombat.cs`、
  `GwpAgentApplyDamageModel.cs`、`GwpAgentStatCalculateModel.cs`、`items.xml`、`spnpccharacters.xml`、
  `SubModule.xml`、`SubModule.cs`、中文字符串的差异，并删除 `gwp_monsters.xml`。

- 本轮以前的状态见提交 `d70aee3`：
  - 弓箭补丁在 `GwpArcherArrowEffects.cs`，两个补丁类 `Prepare() => false`；
  - 伤害模型里是 10% 击倒 / 5% 穿盾；
  - 骑士没有任何加成，重步兵会碎盾，没有整套预设；
  - 等级为 11/16/26。
- 回退时取回以下文件，删掉 `GwpTroopCombat.cs`，重建并镜像：
  `GwpArcherArrowEffects.cs`、`GwpAgentApplyDamageModel.cs`、`GwpAgentStatCalculateModel.cs`、
  `SubModule.cs`、`spnpccharacters.xml`。

## 未做

- 单项武器分（`0x1806b0e90`）约为"伤害 × 出手速度，按伤害类型对护甲打折"，距离只在步行双手武器 > 20 米处用到；
  未完整解码。
- 撞后减速的引擎公式未解码，战马加重、加大对减速的实际效果待实测。
