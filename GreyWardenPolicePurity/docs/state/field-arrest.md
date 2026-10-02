# 野外执法：肯不肯谈、战力口径、谈崩冷却

> 当前状态：战力按"当场会一起上阵"口径、冷却台词不提时限，用户已实测确认（2026-10-02）
> 最后验收：`04ff2d6`（2026-10-02 用户确认）
> 已复核至：`04ff2d6`（回绝台词改写，本文件「谈崩冷却」已写）
> 覆盖源码：`GwpFieldArrestBehavior.cs` `GwpFieldArrestLines.cs` `GwpNegotiationPolicy.cs`
> 待实测：无

本文件只写**开口前的判定**：肯不肯谈、战力怎么算、谈崩后的冷却。两层说服的说辞、
罚金定价、宽限、决斗与收缴尚未提取（仍只在 journal 里）。

## 入口

与承办目标（`PlayerBountyBehavior.HasCommissionAuthority`）在野外单独对话；任一方在定居点里
不走这条路。

## 肯不肯谈

`PrepareFieldArrest` 时掷一次：

- `GwpNegotiationPolicy.RefusalChance`：钱够付罚金、荣誉+仁慈+慷慨 ≥ 2 且荣誉、仁慈都不为负的人
  一律肯听；否则 `clamp((第一层抗性 − 45) / 65, 0, 0.8)`。
- 概率 ≥ 0.55 就拒谈（有宽限约定时不拒）。拒谈直接进"动手或走人"。

第一层抗性 `LayerOneResistance`（0–100）= 基线 50 + 实力摆幅 + 性格 + 玩家分量 − 被抓过的教训
+ 名望差与劣迹压力 + 钱包压力 − 慷慨×4。实力摆幅 = `(他/(他+我) − 0.5) × 2 × 45`，
所以实力比是最大的单项，口径错了会直接决定拒不拒谈。

## 战力口径（`FieldStrength`）

当场会一起上阵的战力：本队；本队已并入军团（是军团长，或 `AttachedTo` 军团长）时，加上
军团长和已挂靠的成员。单队战力用原版 `MilitaryPowerModel.GetPowerOfParty(…, Attacker, PlainBattle)`，
与原版口径一致。第一层、第二层抗性和诊断都用它。

原版 `GetTotalLandStrengthWithFollowers` 在队伍护送别人时（原版去军团会合就是 `EscortParty`）
算的是被护送的那支，还默认把没汇合的军团成员一起算进去。2026-09-26 实机：本人弱于玩家的目标
赶去军团途中算出 1281 对 149，抗性 90、拒谈。

- 不要用 `GetTotalLandStrengthWithFollowers` 判断谈判双方实力。（依据：C#反编译 v1.4.8，2026-09-26；实机诊断 2026-09-26）

## 谈崩冷却

拒谈或第一层没谈拢时记 24 小时（`NegotiationCooldownHours`），期间对他只能"再谈一次"，
得到一句按性格的回绝（`GwpFieldArrestLines.Rebuffed`）。

回绝台词只表达"答复不变"。

- 回绝台词不得提今天、明天、天亮等时限，那是把冷却机制念给玩家听。（依据：用户裁定 2026-09-26）

## 诊断

`TALK_WILLINGNESS`（双方战力、抗性、拒谈概率）、`NEGOTIATION_CHANCE_CONTEXT`、
`NEGOTIATION_COOLDOWN_SET`，写案件日志。
