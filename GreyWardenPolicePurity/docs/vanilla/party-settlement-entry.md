# 部队与军团访问聚落

> 游戏版本：v1.4.8
> 源码：`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/SetPartyAiAction.cs` 的 `ApplyInternal`；`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/EncounterManager.cs` 的 `StartSettlementEncounter`
> 部队行为：`TaleWorlds.CampaignSystem.Party/MobileParty.cs` 的 `SetMoveGoToSettlement`、`SetMovePatrolAroundSettlement`、`CheckExitingSettlementParallel`；`MobilePartyAi.cs` 的 `TickInternal`、`GetBehaviors`、`GetLandPatrolBehavior`；`TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors/AiPartyThinkBehavior.cs` 的 `PartyHourlyAiTick`
> 模组接入：[躲城与野外交涉](../state/field-arrest.md)、[使者投案](../state/case-envoy.md)

## 怎么运作

`SetPartyAiAction.ApplyInternal` 的 `GoToSettlement` 分支下达 `SetMoveGoToSettlement`。
若部队是军团统帅，同一分支还将军团类型设为 Defender，并把 `Army.AiBehaviorObject` 指向聚落；没有排除军团首领。

`EncounterManager.StartSettlementEncounter` 中，在符合该分支的进城条件下，正在前往目标聚落或逃向城门的部队调用 `EnterSettlementAction.ApplyForParty`。
附属部队的军团首领已经进城时，也满足这里的进城条件。
函数随后对非玩家军团首领遍历 `AttachedParties`（村庄的立即离开分支除外），为没有 MapEvent 的附属部队递归调用 `StartSettlementEncounter`；各成员仍经过进城条件判断。

`SetMoveGoToSettlement` 和 `SetMovePatrolAroundSettlement` 都能登记同一个 `TargetSettlement`，分别留下 `GoToSettlement` 和 `PatrolAroundPoint` 长期行为。进城时 `CurrentSettlement` 的 setter 记录 `LastVisitedSettlement`，离城清空当前位置时保留上一站。

短期 AI 先取长期行为，再按原版主动性决定是否覆盖它。城内部队切为攻击城外部队后，`CheckExitingSettlementParallel` 可把它加入离城队列，即使长期目标城仍是当前城。`DoNotMakeNewDecisions` 会阻止原版小时重选和大部分短期覆盖，原有长期行为仍会执行。

具体顺序是 `PartyHourlyAiTick` 看到 `DoNotMakeNewDecisions=True` 就提前返回；`MobilePartyAi.TickInternal` 仍能调用 `GetBehaviors` 和 `SetAiBehavior`。长期行为若是陆地巡逻，`GetLandPatrolBehavior` 会把它展开为 `GoToPoint`，按巡逻半径继续选可达点，部分分支回到城门中心点。因此保留同城巡逻并打开此标志，会继续绕城，靠近城门也不自动把长期行为改成访问。

## 容易想错的地方

- 军团并非不能进城：原版明确支持首领访问以及附属部队接续入城，不能因此把统帅排除在访问候选外。
- 已属于军团但尚未附属到首领的部队，不等同于 `AttachedParties`；上述循环不是把所有远处成员瞬间送进城。
- 这些托管分支说明原版支持的执行路径，不证明某个具体存档中的寻路、避敌或到达时机已经验证。
