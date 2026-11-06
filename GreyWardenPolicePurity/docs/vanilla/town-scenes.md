# 城镇与城堡的走动场景：地点、人物与切场景

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.CampaignSystem` 的 `HeroSpawnCampaignBehavior`、`DefaultHeroAgentLocationModel`、`LocationComplex`、
> `LocationEncounter`；`SandBox` 的 `SandBoxMissions`、`PassageUsePoint`、`MissionLocationLogic`、`SandBoxHelpers`、
> `MissionState`；场景标签与地点来自原版 `settlements.xml`、`location_complex_templates.xml` 与场景 XML
> 模组接入：[进城搜捕](../state/city-search.md)

这些结论在 2026-10-21～10-28 进城搜捕开发中由 C# 反编译与场景 XML 查实，原依据随条写在 state 中，2026-10-28 迁来本页。

## 无部队的领主住在哪

- **为什么长期无部队**：家族同时带兵的队伍数受家族等级上限限制，人多于名额时多出的领主一直不带兵；组队还要求
  个人指挥分（战术×3、统御×2 等）加家族分超过 100（`HeroSpawnCampaignBehavior.ConsiderSpawningLordParties`、
  `GetBestAvailableCommander`）。
- **放到哪座聚落**：先在本阵营的城镇与城堡（`hero.MapFaction.Fiefs`）里按分数加权随机，分数低于门槛的记 0
  （日常挪动的在役领主门槛为 10）：本阵营 +10、城镇再 +100、自家族城镇 +500/城堡 +100、有比武 +400、有同族在城 +100
  （城堡 +10）、被围或敌对为极小值、饥荒 ×0.1、留在原地 ×3。本阵营一处都选不中，才从全部聚落（`Town.AllFiefs`）里
  看非交战方的城镇与城堡，最后看交战方的城镇。所以多在城镇，家族只有城堡或亲族在城堡时住城堡。逃狱/获释者每天 30% 挪动
  （`OnHeroDailyTick`、`FindASuitableSettlementToTeleportForHero`、`GetMoveScoreForHero`）。
- **场景里站在哪**：城镇和城堡里，无部队的成年贵族（`HeroesWithoutParty`）一律在领主大厅 `lordshall`
  （`DefaultHeroAgentLocationModel.GetLocationForHero`）。城堡有中心（庭院）、领主大厅、地牢，**没有竞技场**。

### 日常换城与重新带队

2026-11-06 按 v1.4.8 源码复核：`HeroSpawnCampaignBehavior.OnHeroDailyTick` 对在役英雄走独立分支，符合
`CanHeroMoveToAnotherSettlement` 就调用选城（最低分 10），再 `TeleportHeroAction.ApplyImmediateTeleportToSettlement`，
没有大地图行军过程。每日 30% 的随机门槛属于逃亡/获释分支；普通在役无部队领主不能套用这个概率。
逃亡/获释分支（非特殊英雄，玩家同伴必走，所在聚落与本方交战时也必走）直接选城传送并转回在役，不经
`CanMoveToSettlement` 询问。获释状态的来源之一是 `EndCaptivityAction`：在聚落地牢里被释放的英雄转为
`Released`，并加入该聚落的无部队英雄名单，所以他看起来仍是“住在城里”，但次日起可能被这一分支挪走。

资格排除玩家家族、要人、玩家本人、部队领袖、俘虏、禁用英雄、总督、有所属部队、流浪者、特殊职业和未成年人；
当前聚落有比武或被围也不挪动，最后经 `Hero.CanMoveToSettlement` 的事件询问允许与否，任务可另行阻止。
选城权重见上文；留原地只是乘三，仍可能选中别处，公式没有距离或旅行耗时项。因此城间突然变更位置属于该原版机制。

另一路是家族日结的 `ConsiderSpawningLordParties`：空余名额与指挥资格满足后直接生成真实领主部队。
`GetBestAvailableCommander` 两轮都调用 `GetHeroPartyCommandScore`，只接受高于初始值 0 的候选。
第一轮优先允许领军的成年领主，第二轮只对非玩家家族重选，不检查 `CanLeadParty`。
`SpawnLordParty` 经 `SettlementHelper.GetBestSettlementToSpawnAround` 另选生成位置，考虑领地类型、势力、家族、
战事和距离等因素；必要时退回本方初始据点或同文化聚落。原版不保证在英雄当前所住城堡生成。
选定后以城门为中心取可达点生成部队。英雄获得 `PartyBelongedTo`，不再符合无部队住城资格。

已有部队换领袖不走组建评分。`TeleportationCampaignBehavior.DailyTickParty` 可为符合条件的非战斗领袖选家族替任者，
成年事件可用刚成年的英雄替任；`DisbandPartyCampaignBehavior.OnPartyDisbandStarted` 可给待解散的非玩家家族部队
选城中的无队成员。前两条在选好人后才撤下原领袖，后一条没有人选时保留待解散流程。接任调用
`TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader`，英雄先离城、进入旅行状态，再归入部队。

总督也会让无队领主离城。`ClanVariablesCampaignBehavior.DailyTickClan` 每天对非玩家家族调
`UpdateGovernorsOfClan`：封地按类型、繁荣、附属村、文化与离王国中心距离排序，逐个从 `clan.AliveLords`
里挑能当总督、无部队、本轮未用过的领主，现任总督打满分、其他人乘 0.75。选中者不在该城时，
`ChangeGovernorAction` 走 `ApplyDelayedTeleportToSettlementAsGovernor`，人先离城旅行再上任；已在该城则就地上任。
当上总督后 `GovernorOf` 非空，日常换城不再选他。

## 原版城内玩法

| 玩法 | 入口 | 要点 |
|---|---|---|
| 城镇/城堡走动 | `OpenTownCenterMission`、`OpenCastleCourtyardMission` | 市民、巡逻与站岗卫兵（来自驻军）、要人；`MissionFightHandler` 可临时开打 |
| 领主大厅 | `OpenIndoorMission`（`lordshall`） | 本家族或同阵营直接进；中立时有犯罪记录或家族等级<3 需贿赂卫兵；交战方只能伪装潜入 |
| 大厅战斗 | `OpenSiegeLordsHallFightMission` | 攻城胜利后打守方；兵员来自地图战斗事件，和平进城没有这个事件 |
| 伪装潜入 | `OpenDisguiseMission` | 敌对城镇进中心时自动走这条；被看见或进入个人范围计为暴露 |
| 越狱 | `OpenPrisonBreakMission` | 地牢潜行、拖尸体、巡逻点；按俘虏收贿赂价，每城 7 天冷却 |
| 小巷斗殴 | `OpenAlleyFightMission` | 带自己的人打帮派；`AlleyFightMissionHandler` 逐个 `SpawnTroop`、自行登记双方存活 |
| 先谈后打 | `OpenCombatMissionWithDialogue` | 对话破裂转入战斗 |
| 竞技场单挑 | `OpenArenaDuelMission` | 可指定对手并回调结果（原版任务「间谍派对」这样用）；自行按 `sp_arena` 摆双方 |
| 城内犯罪 | `MissionCrimeHandler` | 离场时对该势力犯罪评级严重则转入「城内犯罪」菜单 |

NavalDLC 剧情另有窃听、破坏、搜尸；潜行依赖场景预置的潜行点与标签，领主大厅原版没有潜行玩法。

## 场景里的点位

- 全部城镇中心有城门外出生点 `sp_outside_near_town_main_gate`；城堡约一半有，全部有 `sp_player_outside`。
- 全部城镇中心有大厅门口卫兵位 `sp_guard_lords_hall_gate`；城堡未必有。
- 全部城镇与城堡的领主大厅有 `sp_defender_infantry_lords_hall`、`sp_defender_archer_lords_hall`、`sp_battle_set`。
  玩家出生的**标签**是 `spawnpoint_player`；`sp_player` 是预制体名称，不是标签。
- 守方增援点 `defender_*`、`defender_*_reinforcement`，卫兵点 `sp_guard`、`sp_guard_with_spear`、`sp_guard_patrol`、
  `sp_guard_castle`。原版单机攻城只取一个固定守方增援点（`DefaultMissionDeploymentPlan`），在 empire_town_g
  这一处离城门外出生点仅 2.1 米。
- NPC 主动走近搭话的写法：`MissionAlleyHandler` 在距离 <5 且看得见玩家时 `MissionConversationLogic.StartConversation`。

## 地点人物与切场景

- **地点人物**：每个地点（`center`、`lordshall`、`arena`…）有自己的 `LocationCharacter` 名单，任务按名单生成人物。
  人物死亡或昏迷后移出名单；离开场景时各地点的非英雄人物被清除，下次进入由人口逻辑重新创建同模板的新人
  （例如店员，`WorkshopsCharactersCampaignBehavior` 重新 `new SimpleAgentOrigin`）。原版没有跨任务保留尸体的路径。
- **AI 过门**：`PassageUsePoint.OnUse` 把人物 `LocationComplex.ChangeLocation` 到门后的地点并 `FadeOut`。
  玩家过门：设 `NextLocation`/`PreviousLocation` 后 `EndMission`，回到地图状态再开下一场。
- **只改名单不会生成人**：`LocationComplex.ChangeLocation` 会通知，但 `LocationEncounter.OnCharacterLocationChanged`
  只转发「空地点↔当前地点」。在 `center`→`lordshall` 两个非空地点间挪人，不会在当前场景生成 Agent；
  要显式调用 `SpawnEnteringLocationCharacter`。
- **玩家从哪扇门出现**：`MissionLocationLogic.OnBeforePlayerAgentSpawn` 按 `PreviousLocation` 找通往它的真实门框。
- **随从**：`MissionLocationLogic.SpawnCharactersAccompanyingPlayer` 会把主队随从带进场景。
- **开新任务的时序**：`MissionState.OpenNew` 返回已创建、尚未 `Mission.Initialize` 的任务；`Initialize` 才写入
  `Mission.Current`。要给新任务挂行为，用 `OpenNew` 的返回值，不能在返回当刻读 `Mission.Current`。
- **结束任务的顺序**：`EndMissionInternal` 先调各行为的 `OnEndMissionInternal`，再 `Teams.Clear`（`PlayerTeam` 随之清空），
  最后才移除行为。所以 `OnRemoveBehavior` 里已读不到玩家队伍。
- **离场**：和平时按住 Tab 走 0.6 秒离场进度条；5 米内有敌人时 `BasicLeaveMissionLogic` 拦下。

## 容易想错的地方

- 城堡没有竞技场，城镇大厅门口卫兵位在城堡未必存在。
- 走出大厅回到街道，是一场**新的** Mission：街上的旧 Agent 已销毁，死伤不会自己留下，店员会换成新人重生。
- 在两个非空地点之间 `ChangeLocation` 不会让人出现在你眼前。
