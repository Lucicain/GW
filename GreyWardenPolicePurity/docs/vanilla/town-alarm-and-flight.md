# 城镇任务里谁逃跑、谁还击

> 游戏版本：v1.4.8
> 源码：`SandBox/SandBox.Missions.MissionLogics/MissionFightHandler.cs`（`IsAgentAggressive`、`GetDangerSources`、`IsThereActiveFight`）、
> `SandBox/SandBox.Missions.AgentBehaviors/AlarmedBehaviorGroup.cs`、`FleeBehavior.cs`、`FightBehavior.cs`、`BehaviorSets.cs`、
> `SandBox/SandBox/AgentNavigator.cs`（`RefreshBehaviorGroups`）、`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Occupation.cs`
> 模组接入：[进城搜捕](../state/city-search.md)「城里打起来时平民与卫兵」

适用于城镇、城堡、村庄这类走动场景里由 `AgentNavigator` 驱动的人物（街上行人、商贩、卫兵、大厅里的人）。
战场上的兵走编队 AI，不经过这里。

## 怎么运作

**1. 谁有这套反应。** 走动场景生成人物时，`BehaviorSets` 给大多数行人、任务人物、流浪者装上
`AlarmedBehaviorGroup`，组里有两个行为：`FleeBehavior`（逃）和 `FightBehavior`（打）。

**2. 什么时候算「出事了」。** 只有一种来源：`MissionFightHandler` 自己发起的打斗，即 `IsThereActiveFight()`
（状态为 `Fighting`）。不经它开的战斗，例如别的任务控制器自己把人分队开打，平民这条链不会被触发。

**3. 危险是谁。** `GetDangerSources(人物)`：有打斗、且这个人物「不好斗」时，危险来源只有一个：**玩家本人**
（`Agent.Main`）。玩家的士兵、其他参战者都不算危险。好斗的人没有危险来源。

**4. 谁算好斗。** `IsAgentAggressive`：
- 手上有武器（`HasWeapon`）→ 好斗，不看职业。
- 没武器时看职业：雇佣兵（Mercenary）、恶人（Gangster、GangLeader、Bandit）、执法者（Soldier、Guard、PrisonGuard）→ 好斗。
- 其余没武器的人（商贩、工匠、镇民、村民、酒馆老板等）→ 不好斗。

**5. 进入警觉。** 导航器每 1 秒（`RefreshBehaviorGroups`）比较各行为组的分数。`AlarmedBehaviorGroup.GetScore`：
已警觉、或离最近危险来源不到 15 米（`SafetyDistance`）、或超过 15 米但看得见它，就拿 1 分并把人物设为警觉
（`SetWatchState(Alarmed)`）。警觉超过 10 秒后每秒复查，附近没危险就解除脚本移动。
好斗的人没有危险来源（第 3 步），所以**不会靠这一步警觉**。他们只有两种途径进入警觉：被 `MissionFightHandler`
拉进打斗的一方（`StartFightInternal`、`AddAgentToSide`→`ForceAgentForFight` 设 `Alarmed`），
或作为守卫被逃来的平民抱怨后设为警觉（第 7 步）。

**6. 逃还是打。** 警觉组每帧取可用度最高的行为：
- `FightBehavior`：好斗 1.0，不好斗 0.1。
- `FleeBehavior`：任务开始 3 秒内为 0；不好斗 0.9，好斗 0.1。
所以：**已经警觉的**人里，好斗的打，不好斗的逃；不看兵力对比。`FightBehavior` 没有自己的 Tick，
只保证人物有 `HumanAIComponent`；警觉状态下如何挑目标出手在引擎层，未查。

**7. 逃往哪里。** `FleeBehavior` 先惊吓 0.5–1 秒，再选目标（`LookForPlace`）：有危险时在「可用的门」
（`Passage`，走进去就离开当前场景）与「附近守卫」之间按路径评分取最高；没危险时原地找掩护。
逃到守卫身边会抱怨 2 秒，然后把那名守卫设为警觉。路线每 1 秒复查一次，目标不可达就重选。

## 容易想错的地方

- **危险只认玩家本人。** 带兵群殴时，平民不会躲开玩家的士兵；士兵打到他们身边，他们也不会自己跑。
- **拿了武器就不逃，但也不会自己参战。** 持械的商贩、劫匪装扮的人算好斗，不逃；可他们没有危险来源，
  不被拉进打斗就一直不警觉，站着旁观。原版里“会还击”只对打斗参与者和被平民叫来的守卫成立。
- **没武器的兵也不逃。** 兵种人物（Soldier/Guard/PrisonGuard）即便手上没武器也算好斗；同样要被拉进打斗或被叫来才出手。
- **逃进门就消失。** AI 用上门的使用点（`SandBox/SandBox.Objects/PassageUsePoint.cs` 的 `OnUse`）时，
  人物被 `LocationComplex.ChangeLocation` 挪到门后的地点并当场 `FadeOut`。场上看就是“跑着跑着没了”，
  他其实去了别的场景（例如大厅、酒馆）。
- **开局 3 秒不逃。** 场景刚开始时 `FleeBehavior` 可用度为 0。
- **不开 `MissionFightHandler` 的打斗，平民毫无反应。** 这条链只认它的 `Fighting` 状态。
