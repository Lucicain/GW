# 烧村：AI 选不选这个村、烧村怎么进行

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultTargetScoreCalculatingModel.cs`（`GetTargetScoreForFaction`）、
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.MapEvents/RaidEventComponent.cs`、
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CharacterRelationCampaignBehavior.cs`
> 模组接入：[火誓者村庄](../state/firesworn-villages.md)「信众户与守村队」、[火誓者队伍](../state/firesworn-parties.md)、[立案](../state/crime-intake.md#信众村烧村评分)

## 怎么运作

**1. 谁来打分。** `AiMilitaryBehavior` 在攻城、烧村、防守、巡逻几类候选里比分取最高；烧村候选的分来自
`GetTargetScoreForFaction(..., Army.ArmyTypes.Raider, ...)`，是下面各项相乘。

**2. 村里的守备，以及「打不过就不来」。** 守备只数**此刻在村里**的部队：`Aggressiveness > 0.01` 的部队、驻军和民兵，
各按估算战力相加（主队算一半；玩家人在村里时整体再乘 0.8）。如果进攻方战力小于（守备 + 王国份额项 + 0.1）× 0.75，
直接返回 0，不烧。也就是村里守备约为进攻方的 1.33 倍以上时，AI 不会选它。

**3. 村外的部队。** 只有村子**已经在被打**（村子身上有 MapEvent）时，才把附近同 `MapFaction` 的部队算进来：
在一个平均领主日行程内，按距离折算权重（夹在 0.3～0.8 之间）。村子还没被打时，村外部队对「选不选这个村」没有任何影响。

**4. 其余乘数。**
- 村子价值：`(Settlement 价值 / 50000)^0.5`，村子已受损时 `0.875 × (1 + (1 − 耐久))`，烧过一半的村更招人。
- 缺钱：进攻方家族金币低于 10000 时加分，单队最多 +0.5，军团减半。
- 关系：进攻方家族与**村主家族**的关系为正时乘 `1 − 0.8 × √关系 / 10`（关系 100 → ×0.2，25 → ×0.6）；为负时最多乘到 1.05。
- 玩家是国王时的外交优先级 ×0.65 或 ×1.3；已是当前目标 ×1.3、正在烧 ×1.5；距离越近越高，烧村上限 ×3；进攻姿态 ×1.2。

**5. 烧村过程。** `RaidEventComponent` 建立烧村事件时，村子的原版民兵队加入守方，先打一场；守方还有人时不开始烧。
守方没人以后每小时扣村子耐久（具体公式未查），每扣一笔，户数减 `扣的耐久 × 0.5 × 当前户数`。
耐久归零村子变成「已烧毁」；守方打赢则恢复正常。烧完后 `CharacterRelationCampaignBehavior` 按 `RaidDamage` 扣进攻方与村里要人的关系。

## 容易想错的地方

- 原版烧村分里**没有**仁慈等性格项。常说的「关系影响烧村」是第 4 步的关系项，而且是和**村主家族**的关系，不是和村民或别的家族。
- 在村外巡逻的部队不能让村子少被选中：选目标时只数村里的守备（第 2 步），村外部队只在村子已被打时才算（第 3 步）。
- 只针对进攻领主的乘数（例如按领主算的震慑）只会把他从「烧村」推向别的行动；他仍要烧时，挑哪个村的排序不变。
