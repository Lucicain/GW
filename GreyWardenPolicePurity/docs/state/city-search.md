# 进城搜捕：无部队住城领主的旧案

> 当前状态：第1段已部署；首测对话与说服正常、士兵不跟随且不能指挥，已按查实原因修复部署待复测；大厅、单挑/群殴、杀出城未做
> 最后验收：未建立检查点
> 覆盖源码：`GwpCitySearchBehavior.cs` `GwpCitySearchMission.cs` `GwpCitySearchMissionViews.cs` `GwpPersuasionRound.cs` `PlayerBountyBehavior.CitySearch.cs` `tools/tests/test_city_search.py`
> 待实测：见文末

缺口本身（罪责保留但无部队住城者没人能接）见[案件](case-enforcement.md#藏城目标与战后连续交战)。本页只写进城搜捕。

## 裁定

- 所有灰袍 NPC 和玩家都能办住城旧案，两者做法分开；先做玩家部分。（依据：用户裁定2026-10-21）
- 灰袍 NPC：灰袍与目标所在聚落的势力正在交战时不进城，等恢复中立再办。玩家不是灰袍势力，不加这条限制。（依据：用户裁定2026-10-21）
- 在城里动手就是对该势力宣战；大厅卫兵和城里同势力的领主都会加入战斗。（依据：用户裁定2026-10-21）
- 玩家流程参照剿灭藏身处：带一定数量的兵从城外走进城，此时双方不敌对；卫兵上前盘问，一次简单说服通过后可带兵在城里走到领主大厅；进大厅是新场景，兵一起带进去；大厅里一次较难说服，成功则直接带走、目标成为俘虏；失败则双方各选单挑或群殴（对方先选），选择不一致时再说服决定按谁的来；单挑去竞技场，群殴当场开打。（依据：用户裁定2026-10-21）
- 玩家群殴即玩家家族对目标所在势力宣战（灰袍有调停机制处理后续）。（依据：用户裁定2026-10-21）
- 卫兵那关说服失败：玩家不攻击卫兵、直接退出就作罢；攻击卫兵就是宣战，要一路打进去，打进大厅后只能走群殴，不再说服。（依据：用户裁定2026-10-21）
- 城镇单挑去竞技场；城堡没有竞技场，就在大厅里现场单挑，旁人围观（仿藏身处头目单挑）。（依据：用户裁定2026-10-21）
- 群殴只有卫兵和场景里同势力的人参战；驻军与外面的部队不陆续进场。离开时要回到庭院/城镇中心场景再打一波守军杀出去，跑到城外才能退出任务。（依据：用户裁定2026-10-21）
- 带兵上限与原版亲自突击藏身处相同：15 人，「小股战术」技能另加；选兵沿用原版选兵界面。（依据：用户裁定2026-10-21；C#反编译 v1.4.8 DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission）
- 任务场景照常生成城里的平民；玩家与随行士兵穿战斗装备，不穿常服。（依据：用户裁定2026-10-21）

## 无部队领主在哪里（原版）

- **为什么长期无部队**：家族同时带兵的队伍数受家族等级上限限制，人多于名额时多出的领主一直不带兵；组队还要求个人指挥分（战术×3、统御×2 等）加家族分超过 100。（依据：C#反编译 v1.4.8 HeroSpawnCampaignBehavior.ConsiderSpawningLordParties/GetBestAvailableCommander）
- **放到哪座聚落**：原版每天在本阵营的城镇与城堡（`Town.AllFiefs`）间按分数加权随机挪人，只接受分数≥10的本阵营聚落：本阵营 +10、城镇再 +100、自家族城镇 +500/城堡 +100、有比武 +400、有同族在城 +100（城堡 +10）、被围或敌对为极小值、饥荒 ×0.1、留在原地 ×3。所以多在城镇，但家族只有城堡或亲族在城堡时会住城堡。逃狱/获释者每天 30% 挪动，本阵营无处可去时才看中立、最后敌对城镇。（依据：C#反编译 v1.4.8 HeroSpawnCampaignBehavior.OnHeroDailyTick/FindASuitableSettlementToTeleportForHero/GetMoveScoreForHero）
- **场景里站在哪**：城镇和城堡里，无部队的成年贵族（`HeroesWithoutParty`）一律放在领主大厅 `lordshall`；城堡同样有领主大厅与庭院 `center` 场景。村庄中心只用于身在村庄的无部队英雄，原版挪人不选村庄。（依据：C#反编译 v1.4.8 DefaultHeroAgentLocationModel.GetLocationForHero、CastleEncounter）

## 原版城内玩法（可借鉴）

| 玩法 | 原版入口 | 要点 |
|---|---|---|
| 城镇/城堡走动 | `OpenTownCenterMission`、`OpenCastleCourtyardMission` | 市民、巡逻与站岗卫兵（来自驻军）、要人；`MissionFightHandler` 可在场景内临时开打（`StartCustomFight`/拳斗） |
| 领主大厅 | `OpenIndoorMission`（`lordshall`） | 进入权限：本家族或同阵营直接进；中立时有犯罪记录或家族等级<3 需贿赂城堡卫兵；交战方只能伪装潜入 |
| 大厅战斗 | `OpenSiegeLordsHallFightMission` | 攻城胜利后在领主大厅里打守方，证明大厅场景支持战斗 AI |
| 伪装潜入 | `OpenDisguiseMission`（敌对城镇进中心时自动走这条） | `DisguiseMissionLogic`：伪装巡逻卫兵、联络人对话；被看见（`IsVisible`）或进入个人范围（`IsInPersonalZone`）计为暴露 |
| 越狱 | `OpenPrisonBreakMission` | 地牢潜行：`StealthAreaMissionLogic`、拖尸体 `CorpseDraggingMissionLogic`、失败计数、巡逻点；按俘虏收贿赂价、每城 7 天冷却 |
| 小巷斗殴 | `OpenAlleyFightMission` | 带自己的人打帮派 |
| 先谈后打 | `OpenCombatMissionWithDialogue` | 对话破裂转入战斗 |
| 城内犯罪 | `MissionCrimeHandler` | 离开场景时若对该势力犯罪评级严重，转入「城内犯罪」菜单 |

NavalDLC 剧情另用窃听（`EavesdroppingMissionLogic`）、破坏（`SabotageMissionController`）和搜尸（`SearchBodyMissionHandler`）；越狱与伪装的潜行依赖场景里预置的潜行点与标签，领主大厅原版没有潜行玩法。（依据：SandBox.dll v1.4.8 C#反编译、NavalDLC.dll 元数据 2026-10-21）

## 玩家进城搜捕可借用的原版部件

- **带兵进城**：全部城镇中心场景都有城门外出生点 `sp_outside_near_town_main_gate`（原版「进城时遭遇战斗」用它在城里摆双方兵）；城堡只有约一半有，全部都有 `sp_player_outside` 可作退路。按名册摆兵、不依赖地图战斗事件的先例是小巷斗殴 `AlleyFightMissionHandler`（逐个 `SpawnTroop`，自行登记双方存活）；兵跟随玩家的编队写法见藏身处 `HideoutMissionController`。（依据：本机 settlements.xml 与场景标签核对、SandBox.dll v1.4.8 反编译 2026-10-21）
- **卫兵上前盘问**：全部城镇中心有大厅门口卫兵位 `sp_guard_lords_hall_gate`；NPC 主动走近开口的写法见 `MissionAlleyHandler`（距离 <5 且看得见玩家时 `MissionConversationLogic.StartConversation`）。（依据：同上）
- **大厅里摆兵开打**：全部城镇与城堡的领主大厅场景都有攻城后大厅战斗用的 `sp_defender_infantry_lords_hall`、`sp_defender_archer_lords_hall`、`sp_battle_set`、`sp_player`。原版大厅战斗 `OpenSiegeLordsHallFightMission` 的兵员来自地图战斗事件，和平进城没有这个事件，需按名册自行生成。（依据：同上）
- **说服**：野外执法已用原版 `PersuasionTask`（统御/魅力/诡道/交易，难度按 `PersuasionArgumentStrength`），简单与较难说服可复用这一套。（依据：源码 `GwpFieldArrestLines.BuildReservations`）
- **单挑与群殴切换**：藏身处头目对话后在同一场景切单挑或群战（单挑时双方其余人移出队伍、原地围观，结束后按胜负归队）。竞技场单挑 `OpenArenaDuelMission` 可指定对手并回调结果，原版任务「间谍派对」就这样用；**城堡没有竞技场**（城堡只有中心、领主大厅、地牢）。（依据：SandBox.dll v1.4.8 反编译、settlements.xml 2026-10-21）

## 城里打起来时平民与卫兵的原版行为

- 原版有这套机制：任务里存在「正在打架」（`MissionFightHandler.IsThereActiveFight`）时，没拿武器的平民把玩家本人当作危险来源，触发 `FleeBehavior`（逃向保护者、逃进门、逃到某处）；拿武器的人以及职业为士兵、卫兵、狱卒的人算「好斗」，走 `FightBehavior` 参战。受惊由 `AlarmedBehaviorGroup` 按看见敌人、听到声响、看到尸体累积警觉值触发。（依据：SandBox.dll v1.4.8 反编译 2026-10-21）
- 原版的危险来源只认玩家本人，且要经 `MissionFightHandler` 开打；我们带兵群殴时平民是否也躲开我方士兵，实现时要专门验收，必要时补上危险来源。（依据：同上）
- 任务控制器设 `DoesMissionRequireCivilianEquipment = false` 即按战斗装备生成玩家，藏身处、越狱、村庄任务都是这样。（依据：同上）

## 第1段的实现

- **接案**：住城判断 `CrimeRecord.ShelterOf`：目标活着、未被俘/禁用/逃亡、没有部队，且在城镇或城堡的 `HeroesWithoutParty` 里。玩家候选池用 `GwpPlayerCaseAssignment.CanPlayerTakeCase`（野外可追捕或住城可搜捕）；NPC 派案仍用 `CanTakeCase`，不接住城的案子。（依据：用户裁定2026-10-21；设计）
- **委托状态**：「追捕中」改为记着目标部队或目标本人即算；住城目标没有部队 ID。住城的目标不算失去踪迹、不开放无责撤回，事务页与任务日志显示「藏身于某城的领主大厅」。目标重新带兵后照原样按部队追捕。（依据：设计；离线等价核对2026-10-21）
- **入口**：城镇与城堡菜单在委托目标正住在这里时出现「带兵进城搜捕」，受伤时禁用；选兵用原版藏身处选兵界面，上限取原版亲自突击藏身处的人数。（依据：用户裁定2026-10-21）
- **场景**：沿用原版城镇中心任务的全部行为，只换控制器：穿战斗装备，在城门外出生点生成玩家（城堡没有时依次退到 `sp_player_outside`、`spawnpoint_player_outside`），按名册生成所选士兵。城里居民与卫兵照常生成。玩家能用的门全部关闭（平民仍可穿门逃跑），进大厅属于第2段。（依据：设计）
- **指挥部下**：用自有任务名 `GwpCitySearch`，界面为原版城镇中心那组加上下令、部队摆放、编队标记；原版 `TownCenter` 不带下令界面，所以不能沿用。（依据：SandBox.View v1.4.8 反编译、实机反馈 2026-10-21）
- **士兵跟随**：生成后照原版战斗任务的做法把编队交给玩家（设玩家为指挥官、下令者与编队归属为玩家），再经原版下令控制器下「跟我来」。原版建队时任务尚未指定玩家队伍，编队一律交 AI；城镇任务没有部队 AI，不交出来士兵就原地不动，直接改编队命令也不会转为玩家控制。（依据：C#反编译 v1.4.8 Mission.Teams.Add/Team.SetPlayerRole/OrderController.AfterSetOrder/AssignPlayerRoleInTeamMissionController；实机反馈 2026-10-21）
- **卫兵盘问**：守卫（原版算执法者的士兵、卫兵、狱卒）走到 6 米内就上前盘问；玩家可说明来意走一次简单说服（一道关卡，论点与难度都为 Easy），或表示出城。说服成败只提示，不阻止玩家行走。（依据：用户裁定2026-10-21；数值为设计）
- **动手即宣战**：我方任何人打到守卫或场景里与城主同势力的英雄，经 `PlayerBountyBehavior.DeclarePlayerCaseWar` 让玩家阵营对城主势力宣战并登记调停申请，再把守卫与同势力的人放进敌方队伍、交给场景行为里的战斗行为，切战斗模式，我方经下令控制器冲锋；守方全部倒下即打完，恢复跟随；交战中不能离开场景。（依据：用户裁定2026-10-21）
- 不要用原版 `MissionFightHandler.StartCustomFight` 打带兵的群战：它结束时把参战者放进新建的 Team 对象，队伍无相等重写，我方会脱离玩家队伍、不能再指挥。（依据：C#反编译 v1.4.8 MissionFightHandler.ResetTeamsForFightAndDuel、Team）
- 已知缺陷（待取证）：原版平民逃跑以 `MissionFightHandler` 有进行中的打斗为前提，换成队伍群战后平民是否仍跑开未证实，由 `CIVILIAN_FLIGHT` 取证。
- **伤亡**：我方普通兵的阵亡与重伤在离开场景时写回主队名册；英雄的伤情交给原版。（依据：设计）
- 已知缺陷：第1段打完卫兵后仍可直接退出城外，「杀出去才能退出」属于第4段；打进去后只能群殴的限制在第2、3段实现。

## 待实测

- 接一宗住城的委托：事务页候选与当前委托显示所在城镇/城堡，任务日志写「当前所在」，不出现无责撤回。
- 到那座城镇或城堡，菜单出现「带兵进城搜捕」，选兵界面默认挑最强的、上限15人（有小股战术另加）。
- 进场后玩家与士兵穿战斗装备、在城门附近出现，士兵跟着走；能用下令界面指挥（停下、跟随等）；城里居民和卫兵照常。
- 靠近守卫时被盘问；说服成功与失败各一次；选「我们这就出城」不弹不放行提示。
- 对守卫动手：提示与城主势力开战，守卫与场景里同势力的人参战，平民跑开；打完士兵恢复跟随，离开后主队名册扣除伤亡。
