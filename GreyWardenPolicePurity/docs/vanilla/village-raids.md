# 烧村：AI 选不选这个村、烧村怎么进行

> 游戏版本：烧村评分与过程 v1.4.8，玩家介入与转场 v1.5.7
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultTargetScoreCalculatingModel.cs`（`GetTargetScoreForFaction`）、
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.MapEvents/RaidEventComponent.cs`、
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CharacterRelationCampaignBehavior.cs`
> 玩家介入源码：`TaleWorlds.CampaignSystem.CampaignBehaviors/EncounterGameMenuBehavior.cs` 的 `game_menu_join_encounter_help_defenders_on_consequence` 与 `game_menu_encounter_on_init`，`PlayerEncounter.RestartPlayerEncounter`、`StartBattleInternal`，`FieldBattleEventComponent.UpdateMapEventSettlement`，`Helpers/MenuHelper.cs` 的 `EncounterAttackConsequence`、`EncounterOrderAttack`，`BeHostileAction.ApplyEncounterHostileAction`。
> 模组接入：[火誓者村庄](../state/firesworn-villages.md)「信众户与守村队」、[火誓者队伍](../state/firesworn-parties.md)、[立案](../state/crime-intake.md#信众村烧村评分)、[玩家现场介入](../state/player-justice.md#现场介入正在发生的犯罪与执法)

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

## 玩家援救村庄的转场

1. 玩家在 `join_encounter` 选择帮助守方时，原版先记下劫掠者首队、双方要转移的移动部队及原劫掠是否进入掠夺阶段，再 `FinalizeEvent` 结束原 `Raid`。原事件的 `MapEventEnded` 在新遭遇创建前派发，报告的是原事件的结算。
2. `RestartPlayerEncounter` 先接收防守方，再接收进攻方。若玩家等待被打断或劫掠尚未进入掠夺阶段，新遭遇将玩家列为防守方。已经进入掠夺阶段且玩家来援时，新遭遇将玩家列为进攻方。`encounter` 初始化启动新战斗，普通两支移动队走 `FieldBattleEventComponent`。随后原版恢复两侧移动部队，并转交 `WasEverInLootingPhase`。
3. 新野战可以根据位置选一座附近的村庄作为 `MapEventSettlement`，由此进入 `land_raid` 场景。战斗类型仍由 `FieldBattleEventComponent.GetBattleType` 决定。靠近村庄的普通野战与救援劫掠后的野战都可使用该场景。
4. 点击进攻或派兵进攻时，`MenuHelper` 可调用 `BeHostileAction.ApplyEncounterHostileAction`。对未与玩家势力交战且符合原版条件的一方，原版在同一调用里先扣关系，再以 `CausedByPlayerHostility` 宣战。战前已经交战时没有这次新宣战。
5. 战后玩家可以在 `MapEventEnded` 前选择俘虏或释放败方领主。释放可走 `MakeHeroFugitiveAction`，领主与原部队的关联会改变。战后回查领主目前带的队伍，无法恢复出战时选择援救哪个村。
6. 战斗若还有下一轮，`FieldBattleEventComponent.OnPlayerEncounterContinueBattle` 在同一 `MapEvent` 上结算本轮并重置胜方。回到地图菜单后，`MapScreen` 的 `IMapStateHandler.AfterTick` 可以发出快速存档；`MapState` 在判断菜单状态前处理存档队列。`SandBoxGameManager` 恢复遭遇菜单并调用 `PlayerEncounter.OnLoad`，同版野战继续使用存档里的 `MapEvent` 和两侧。

### 个人击倒与地图战斗

战役启动各场任务时，`StatisticsCampaignBehavior.OnMissionStarted` 加入 `StatisticsMissionLogic`。角色移除回调将被移除者与造成移除的角色传给 `StatisticsCampaignBehavior.OnAgentRemoved`。当后者是 `Agent.Main` 时，原版个人击倒或击杀累计加一；玩家部队的击倒有独立计数。`GetNumberOfTroopsKnockedOrKilledByPlayer` 只返回累计人数。逐人阶级可在移除回调根据受影响者的 `CharacterObject.Tier` 读取。

`MapEvent.PlayerMapEvent` 指向玩家主队当前的地图事件。战斗任务的个体移除和地图事件最终结算是两个时机。两轮间回地图可以保存，继续战斗时用前节所述的同一个 `MapEvent`。（依据：v1.4.8、v1.5.7 的 `StatisticsCampaignBehavior` 与 `MapEvent` 托管源码）

## 容易想错的地方

- 原版烧村分里**没有**仁慈等性格项。常说的「关系影响烧村」是第 4 步的关系项，而且是和**村主家族**的关系，不是和村民或别的家族。
- 在村外巡逻的部队不能让村子少被选中：选目标时只数村里的守备（第 2 步），村外部队只在村子已被打时才算（第 3 步）。
- 只针对进攻领主的乘数（例如按领主算的震慑）只会把他从「烧村」推向别的行动；他仍要烧时，挑哪个村的排序不变。
