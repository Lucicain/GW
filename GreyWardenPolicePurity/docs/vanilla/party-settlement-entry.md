# 部队与军团访问聚落

> 游戏版本：v1.4.8
> 源码：`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/SetPartyAiAction.cs` 的 `ApplyInternal`；`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/EncounterManager.cs` 的 `StartSettlementEncounter`
> 模组接入：[躲城与野外交涉](../state/field-arrest.md)、[使者投案](../state/case-envoy.md)

## 怎么运作

`SetPartyAiAction.ApplyInternal` 的 `GoToSettlement` 分支下达 `SetMoveGoToSettlement`。
若部队是军团统帅，同一分支还将军团类型设为 Defender，并把 `Army.AiBehaviorObject` 指向聚落；没有排除军团首领。

`EncounterManager.StartSettlementEncounter` 中，在符合该分支的进城条件下，正在前往目标聚落或逃向城门的部队调用 `EnterSettlementAction.ApplyForParty`。
附属部队的军团首领已经进城时，也满足这里的进城条件。
函数随后对非玩家军团首领遍历 `AttachedParties`（村庄的立即离开分支除外），为没有 MapEvent 的附属部队递归调用 `StartSettlementEncounter`；各成员仍经过进城条件判断。

## 容易想错的地方

- 军团并非不能进城：原版明确支持首领访问以及附属部队接续入城，不能因此把统帅排除在访问候选外。
- 已属于军团但尚未附属到首领的部队，不等同于 `AttachedParties`；上述循环不是把所有远处成员瞬间送进城。
- 这些托管分支说明原版支持的执行路径，不证明某个具体存档中的寻路、避敌或到达时机已经验证。
