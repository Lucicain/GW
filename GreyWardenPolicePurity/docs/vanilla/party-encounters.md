# 原版地图部队会面

> 游戏版本：v1.5.7
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Party/MobileParty.cs` 的 `SetMoveEngageParty`、`OnPartyInteraction`，同目录 `MobilePartyAi.cs` 的 `GetBehaviors`、`PartyBase.cs` 的 `IInteractablePoint.CanPartyInteract`；`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/EncounterManager.cs` 的 `HandleEncounterForMobileParty`、`StartPartyEncounter`；`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Encounters/PlayerEncounter.cs` 的 `Init`、`DoMeetingInternal`、`Finish`；`DefaultEncounterGameMenuModel.GetEncounterMenu`、`EncounterGameMenuBehavior.game_menu_encounter_meeting_on_init`。
> 模组接入：[玩家入会](../state/player-justice.md#入会与退会)、[灰袍地图 AI](../state/warden-ai.md#欲望竞价greywardenpartydesirebehavior)

## 怎么运作

1. `SetMoveEngageParty` 把目标部队写进 `TargetParty`。`MobilePartyAi.GetBehaviors` 用目标的实时位置继续求路。单独下 `GoToPoint` 只有坐标，`EscortParty` 则走原版跟随。
2. `HandleEncounterForMobileParty` 检查当前短期行为、双方状态和接触资格。主动接触部队走 `EngageParty`，普通 `GoToPoint` 到达坐标和 `EscortParty` 跟随都不会通过这条部队接触分支。`PartyBase.CanPartyInteract` 再核对海陆状态和当前实际接触距离。目标在聚落里时，普通地图会面等目标离开。
3. 接触后通过 `MobileParty.OnPartyInteraction` 调 `EncounterManager.StartPartyEncounter`。涉及主队时创建或更新 `PlayerEncounter`。未卷入战斗、攻城等特殊分支的普通两队相遇，由 `DefaultEncounterGameMenuModel` 选择 `encounter_meeting`，此时 `startBattle`、`joinBattle` 均为 `false`。海战剧情的专用相遇由 NavalDLC 的 `NavalEncounterMenuModel` 决定，普通会面交回原版基础模型。
4. `encounter_meeting` 第一次初始化就调用 `DoMeeting`。`DoMeetingInternal` 设置 `_meetingDone`，陆地打开地图对话，海上打开专用会面场景。回到会面菜单时，`LeaveEncounter` 已置位便结束遭遇，否则菜单可继续进入战斗。

## 返程与再次接触

仅关闭对话会留下原版部队的行进命令。两队还重叠时，任务方可先切换到 `SetMoveGoToSettlement`，由 `ResetAllMovementParameters` 清理旧移动目标，再结束本次遭遇。原版 `FinishEncounterInternal` 对敌对双方的特定非胜利离场也会给遇到的队伍两小时 `DoNotAttackMainParty`，该保护用于压制短期进攻。持续下达 `EngageParty` 的任务仍需自行结束。
