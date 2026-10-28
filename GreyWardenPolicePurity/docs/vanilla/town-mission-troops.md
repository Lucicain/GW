# 在城镇任务里带兵：队伍、指挥与开战

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.MountAndBlade` 的 `Mission`（`GetAgentTeam`、`CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase`、
> `Teams.Add`）、`Team.SetPlayerRole`、`OrderController`、`AssignPlayerRoleInTeamMissionController`、`AgentBuildData`、
> `PartyAgentOrigin`、`BattleAgentLogic`；`SandBox` 的 `MissionFightHandler`、`HideoutMissionController`、
> `DefaultVisualOrderProvider`/`VisualOrderFactory`/`HideoutVisualOrderProvider`、`SandBox.View` 的任务界面注册；
> `TournamentFightMissionController.GetSimulationAttackPower`；`DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission`
> 模组接入：[进城搜捕](../state/city-search.md)「第1段的实现」「城防增援」

城镇走动任务本来不是战斗任务：没有部队 AI、没有下令界面、所有人默认中立。这页讲在它里面带兵打仗会碰到的原版规则。
结论在 2026-10-21～10-28 进城搜捕开发中由 C# 反编译查实，2026-10-28 从 state 迁来。

## 怎么运作

**1. 兵放进哪支队伍。** `Mission.GetAgentTeam`：任务有盟友队伍时（城镇任务的基础分队会建一支），只把「受玩家指挥」的兵
放进玩家队伍。`PartyAgentOrigin`（挂在玩家主队）的兵受玩家指挥；`SimpleAgentOrigin` 对普通兵没有所属部队，
会被分进盟友队伍，下令界面里看不到。伤亡由 `BattleAgentLogic` 经 `PartyAgentOrigin` 写回主队名册，英雄的伤亡与血量一并处理。

**2. 编队交给谁。** 建队时任务尚未指定玩家队伍，编队一律交 AI。城镇任务没有部队 AI，不把编队交给玩家
（设玩家为指挥官、下令者与编队归属）士兵就原地不动；直接改编队命令也不会转为玩家控制。
`AgentBuildData` 默认 `Formation=null`，`Agent.Build` 沿用这个值：新生的兵不在任何编队里。

**3. 下令界面。** 原版 `TownCenter` 的任务界面不带下令、部队摆放、编队标记。下令界面的命令来自最后登记且可用的命令表；
原版默认表只在非「友好任务」里可用，城镇任务一直是友好任务，所以表是空的，只剩拖拽摆位。
藏身处另登记了自己的表（`HideoutVisualOrderProvider`）。按住 Tab 离开也依赖任务的友好标记。

**4. 中立的人打不伤。** `CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase`：攻击非敌人时取消伤害、让出手者僵直，
不触发 `OnAgentHit`。要在「打中中立守卫」时开战，只能在 `Mission.MeleeHitCallback` 之前认出这一下。

**5. 原版的场内开打。** `MissionFightHandler.SetTeamsForFightAndDuel` 把双方设为警觉（`WatchState.Alarmed`）。
`StartCustomFight` 结束时把参战者放进新建的 `Team` 对象；`Team` 没有相等重写，玩家的兵会脱离玩家队伍、不能再指挥。
有场景导航器（`AgentNavigator`）的人物需经 `RefreshBehaviorGroups` 才切到警觉组（每秒一次，见
[谁逃跑、谁还击](town-alarm-and-flight.md)）；`SpawnTroop` 生成的兵没有导航器。

**6. 带兵上限与实力。** 亲自突击藏身处的带兵上限 15 人，「小股战术」技能另加。比武模拟胜负用
`GetSimulationAttackPower`，按装备与对应技能算攻防分；全身无甲时护甲项为 0/0，结果里防御为 NaN。

## 容易想错的地方

- 用 `SimpleAgentOrigin` 生成想让玩家指挥的兵，兵会跑进盟友队伍。
- 宣战和 Charge 命令不会让没警觉的兵进入战斗。
- 新生的援军没有编队，只设警觉不等于在冲锋。
- 别用 `StartCustomFight` 打带兵的群战，打完玩家就指挥不了自己的兵。
- 小巷斗殴的 `MissionAlleyHandler.StartCommonAreaBattle` 按 Soldier/Guard 职业把人收进卫兵名单，
  `EndFight` 要求每人有 `AgentNavigator`/`FightBehavior`；`SpawnTroop` 的兵没有导航器，会空引用。
