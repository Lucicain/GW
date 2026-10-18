# 案件生命周期与执法

> 当前状态：人物追责保留、使者独立、真实进城跟随与动态出战门槛已修复，已部署待实测
> 最后验收：`9d005bb`
> 已复核至：`5b0591f`（2026-10-18）
> 覆盖源码：`PoliceEnforcementBehavior*.cs` `PoliceAIDeterrenceBehavior.cs` `GwpArmyExitDisorganizedPatch.cs` `GwpData.cs` `GwpRuntimeState.cs` `GwpEncyclopediaHeroPageVM.cs` `tools/CaseSettlement.Tests/**` `tools/tests/test_case_envoy_scenarios.py` `tools/tests/test_sheltered_enforcement.py`
> 待实测：第三方击溃/俘虏或人物暂离后罪责与伤亡保留、恢复带队自动重选；普通改追与使者原目标独立；进城跟随和战后恢复目标均不强制开打；同版本存读冷却接续

强制差事不再取消自动练兵换兵队：它由独立实体执行，不占领主差事，见 [运兵](warden-training.md)。（依据：用户裁定、源码复核 2026-10-15）

本文件覆盖**人物追责与承办生命周期**。巡逻、悬赏、使者送单、练兵分别见
[`patrol.md`](patrol.md)、[`player-justice.md`](player-justice.md)、[`dispatch.md`](dispatch.md)、[`warden-training.md`](warden-training.md)。

协力职责与战力的无限递归已切断，生产方法回归通过；根因、触发条件和证据只在[代码健康](code-health.md#协力职责与战力的无限递归)维护。（依据：生产复现2026-10-16）

负声望赎罪单独见[赎罪委派](atonement.md)。玩家占案统一查询 `GwpPlayerCaseAssignment`；案件转给玩家时，生命周期入口同时撤回普通承办人与使者。每小时在普通追捕、宣战之前释放玩家已持有的案子；显式请求的带路支援仍按原分支执行。（依据：源码复核2026-10-15）

## 人物追责与回退

本轮修复的回退基线为 `7573179`；取回 `PoliceEnforcementBehavior*.cs`、`GreyWardenPartyDesireBehavior.cs`、`GwpCommon.cs`、`GwpCaseSettlementEntryPatch.cs`、`GwpStrength.cs`、`GwpData.cs`、`PoliceMobilePartyAIModel.cs`、`PlayerBountyBehavior.CaseSettlement.cs`、`GwpTuning.cs` 及其 state。新版本不做旧格式迁移，同版本进行中状态必须恢复。（依据：流程；用户裁定）

- 追责键是罪犯英雄ID，同一人未了犯罪累计在同一人物账目；当前主办任务、协办组和已出使者是不同的临时行动。（依据：用户裁定 2026-10-17）
- 承办人失效、罪犯被第三方打败/俘虏或暂时没队伍均不清未了罪责；重新带队后按人解析新队继续候选。开放人物账目不按100条裁剪。（依据：用户裁定与实现 2026-10-17）
- 未经实际处置结清的罪责，只有核实人物永久退出才可被动清除，不能把失队、失联、暂时禁用统称死亡。暂不可执行的记录保留犯罪次数与伤亡，退出可执行候选；恢复活动并重新带队后自动恢复，不需要再次犯罪。（依据：用户裁定 2026-10-17）
- 本机原版普通人物永久“失踪”走 `KillCharacterAction.ApplyByRemove` → `Lost` → `MakeDead` → `CharacterStates.Dead`，百科失踪文案也来自 `Lost`。因此当前被动清理的明确终态为 `IsDead`，包含已完成的这种移除，不限于战死/老死。战斗/围城中可能先挂 `DeathMark` 延后执行，未完成时不提前清账；家族被消灭本身也不替代人物终态。（依据：C#反编译 v1.4.8.119303，2026-10-17）
- `Disabled` 不是永久退出的通用标记：`IssueBase.StartIssueWithAlternativeSolution` 暂派同伴和 `ApplyHeirSelectionAction` 玩家退休均会使用它。当前统一暂缓追捕并保留账目，不以 `Disabled` 或 `DeadOrDisabledHeroes` 名单单独销账；退休也不会仅凭该状态误清。`Fugitive`、`Released`、`Traveling`、被俘和英雄暂未解析均不销账。未核实的新退出路径须另查实际动作，不能从名字推断永久性。（依据：C#反编译；用户裁定 2026-10-17）
- 最近可接目标允许在普通追踪阶段改追；已派使者固定原罪犯并完成交涉。编队/区域两层定义只在[协力](case-assistance.md)维护。

## 自动接案的实际顺序

- 犯罪入口、类别存续、人命与震慑只在[立案](crime-intake.md)定义。台账按英雄归并，当前案重复犯罪累加件数；人物带队变化后通过 `CrimeRecord.Offender` 实时解析部队。
- 开放人物追责不按数量裁剪；入账不要求灰袍空闲或能打赢。（依据：用户裁定与实现 2026-10-17）
- 待派案要求开放、无人承办、人物存活且未被俘/禁用、目标部队活跃且当前不在聚落，也未被玩家承办；玩家本人通缉按其独立规则。已接案目标进城则继续原版跟随，不按新案门槛直接撤案。（依据：源码复核 2026-10-17）
- 每小时先处理现有支援与使者、清台账，再允许现任承办人改追近案，然后派新案，随后维护协力，最后推进任务（含使者与宣战）。因此同小时新接案可直接进入使者/协力判断。（依据：源码复核 2026-10-17）
- 普通承办人要求活跃的灰袍领主队、差事登记处允许接普通案；`PoliceResourceManager.IsReady` 当前只看队伍活跃，不另设粮、钱、人数或伤员门槛。补给通过原版欲望处理。（依据：源码复核 2026-10-17）
- 三轮派单：先每位可用领主挑案发位置在信众村6距离内的最近可接案，再由护商/护村本职挑对应类别最近案，最后剩余领主挑任意类别最近案。每轮领主按部队ID顺序处理；“最近”是对当前罪犯位置的直线距离，不是全局最短路匹配、案龄或危害排序。（依据：源码复核 2026-10-17）
- 普通追踪可改追：尚未宣战、非协力占用、没军团、没未返程的非使者派出队，间隔至少6小时，新目标距离小于原来的60%，并通过同一接案门槛；有专职者按专职类别筛选。旧案留台账，不算办成；6小时改追冷却入档。（依据：源码复核 2026-10-17）
- 接下任务后先等[使者](case-envoy.md)结果；派不出或谈崩、已宣战等分支才进入武力处理。协力人数与现场宣战分两次判断，完整编成见[协力](case-assistance.md)。（依据：源码复核 2026-10-17）
- 有协力组时宣战前先以承诺战力比较区域敌军，再找距离与现场战力均满足的行动方；没有协力组的普通案件直接进入现场判断。非分散编队仅组长可触发，分散编队也检查协办领主。战争是势力级，攻击授权仍按案件目标限制；宣战后战力与移动继续变化，接战/逃跑由原版短期AI决定。（依据：源码复核 2026-10-17）
- 使者谈成、玩家接案、犯人败亡、承办人失效分别进入所属结案/撤人流程；案卷是否保留只由下节生命周期定义。

## 藏城目标与战后连续交战

- 未宣战追踪统一 `EscortParty`，原版 `GetFollowBehavior` 对驻城对象返回 `GoToSettlement`，离城后恢复移动目标。模组不再替换成城门点移动，不强逐罪犯、不命令敌人攻击、不冻结敌方决策，也不阻止目标重新入城。（依据：用户裁定；本机原版方法离线执行 2026-10-17）
- 同城谈崩、区域优势成立时仍允许宣布敌对；没有另加城内宣战禁令。原版 `PrisonerCaptureCampaignBehavior.OnWarDeclared` 对双方自有城镇/城堡中符合条件的带队敌方领主走 `LeaveSettlementAction`，无部队英雄另有俘虏分支，中立第三方城市不由该遍历处理。普通驻城队不直接开野战。（依据：C#反编译、12项离线执行 2026-10-17）
- 实际主动攻击与派截击须人在野外且重新满足区域优势。协力期间不授权协办自己的旧目标；协力结束后恢复旧目标也要重新比较现场力量。（依据：设计；16项生产授权回归 2026-10-17）
- 原版可出战驻城领主的区域资格见[协力](case-assistance.md#区域宣战与持续接战)。这些测试不等于用户描述的城门援军或连续战斗现场已被完整重放。

## 用户裁定的三条规则

1. 部队被打光、人被抓，**都算案件了结，都要上震慑**。
2. 雇佣兵转移阵营导致战斗脱离 —— **案件跟着人走，不跟着势力走**。
3. 人不是灰袍打掉的（逃亡或被别人俘虏）—— **这个人依然被通缉**，等他重新建队，
   灰袍照样找他麻烦。

## 主队中的 NPC 旧案

- 案卷身份是 `CrimeId` / `OffenderHeroId` 对应的英雄，不是当前部队领袖。NPC 入玩家主队后，`Offender` 指向主队只用于地图追踪；读档和部队变化不得把嫌犯改写为玩家。嫌犯离队且暂时无所属部队时，撤承办任务并保留卷宗，等其重新建队。（依据：用户裁定 2026-09-27；存档字段与源码核查）
- 灰袍可拦截带着有旧案 NPC 的主队；对话说出该 NPC 姓名，用 NPC 案件基础费与该 NPC 负声望定价。玩家持有足额金币且公库收款人有效时才可整笔代缴至司法公库；缴清后只结该 NPC 案、清其负声望、登记惩戒，玩家本人的声望及 `PLAYER_WANTED` 不动。金币不足时可离开，本案保留、接触受冷却限制。多案接触每帧至多遍历一次地图队伍，选最近的承办队。（依据：用户裁定 2026-09-27；设计；源码复核）
- 玩家本人案件只能由 `PLAYER_WANTED` 标识。战败押送、玩家罚款、正声望撤玩家通缉、任务不可被玩家委托/协力征用以及新玩家通缉接管旧案，不能仅凭目标部队是主队来判定。接触与对话入口仍需按主队判断，才能拦截带着嫌犯的玩家。英雄百科页按英雄 ID 查案；玩家显示自己的灰袍声望，同队成员不共享犯罪数字。（依据：用户反馈与存档核查 2026-09-27；源码复核）

## 震慑口径

- 一次惩戒落到某人身上，不论灰袍办的还是玩家办的，都走同一个入口
  `PoliceAIDeterrenceBehavior.RegisterEnforcementOutcome`：本人震慑、同族转述、同场目击。
- **执法/被捕计数**：实际押走、和平付款/私钱和兑现约斗（含罪犯胜而免罚）都计入现有次数；只打光部队、人跑掉仍上震慑但不计次数。和平计数经用户确认保留，详细定义见[震慑](crime-intake.md#震慑gwpaideterrencestate)。（依据：用户裁定 2026-10-17；保留战败未俘原规则）
- `RegisterDefeatDeterrenceIfNotCaptured` 先排除 `IsPrisoner`，避免与
  `OnHeroPrisonerTaken` 重复登记；同一个人一小时内的重复登记（`SamePunishmentHours`）视为同一次。

## 战争跟着人走

根因：`DeclareWar` 记的是**当时**的 `criminalClan.MapFaction`，而
`ReconcileTaskWarStatesWithDiplomacy` 复查用**现在**的。犯人并入别家军团后两者对不上，
战争被当成"已经和平"撤销，原版 initiative 当场失去 `IsEnemy` 资格，仗打一半散场。

- 复查发现阵营对不上时，**先对他当前的势力重新宣战**（`DeclareWar(task, offender)`
  自己按当前 `ActualClan.MapFaction` 取目标并刷新 `task.WarTarget`），成功写
  `CASE_WAR_RETARGETED_TO_CURRENT_FACTION` 并保持 `WarPursuit`；只有重新宣战也不
  成立才走撤销路径。
- 不会来回翻：`GwpPoliceWarReasonService.TaskMatchesFaction` 同时认 `task.WarTarget`
  和实时的 `offender.ActualClan.MapFaction`。
- 玩家目标、土匪、案卷已关的任务不走这条。

## 案件生命周期：唯一入口

承办任务怎么结束、案卷怎么销，只走 `PoliceEnforcementBehavior.CaseLifecycle.cs`。
别的系统要结案调 `PoliceEnforcementBehavior.CloseCase`，玩家接手调 `ReleaseWardensFromCase`。
不得在执法行为之外直接调 `EndTask` / `ReleaseTask` / `CloseCase` / `ReopenCase`。（依据：设计 2026-10-07；归属检查拦截）

`EndCaseTask(承办人, 结局, 原因)` 按固定顺序做全套善后：协力组（办成按办成收尾，否则解散）→
恢复 AI、清意图 → 清宣战记录 → 台账 → 办成则记办案经费、通知差事完成 → 恢复和平 → 刷新 → 立即重新决策。讲和时序由 [调停](warden-mediation.md) 的共享入口守卫，战斗尚未收尾时登记现有待办，案件仍当场结清。

| 结局 | 台账 | 什么时候 |
|---|---|---|
| `Solved` 办成 | 结案 | 灰袍打赢目标（`offender_defeated_in_battle`）、延迟纠察队打垮目标 |
| `Closed` 结案不算办成 | 结案 | 实际处置已经完成的外部结案（`CloseCase`） |
| `Released` 撤人留案 | 留案；确认人物死亡/原版永久移除终态才销 | 承办人战败/失效、部队消失、目标暂时追不到、协力凑不出兵、行政改派（村庄善后、玩家委托、玩家通缉改派）、玩家接手 |

- 撤人留案不看部队是否仍在；`CrimePool.ReleaseTask` 遇到尚未核实永久退出的人物就保留原账目。终态与暂缓规则见上方「人物追责与回退」。
- `CrimeRecord.Offender` 按英雄解析（`hero.PartyBelongedTo` 优先），`IsOffenderPursuable()` 统一判断当前可执行资格；暂缓原因消失且重新带队后，原账目自动回到候选，不需要另行“重新通缉”。
- 外部结案（玩家当面了结、玩家押人、委托交差、被捕清账、同伴代缴）都经 `CloseCase`：正在承办的灰袍
  先按 `Closed` 收尾，再销卷，同步处理承办人的协力组、宣战记录和AI。
- 承办部队消失或案卷目标无法恢复的任务，每小时由 `CleanCaseLedger` 按 `Released` 收尾；死人的案卷移出台账。
- 押送被俘玩家结束那一段是玩家案件自己的流程（有防崩溃的步骤顺序），不走 `EndCaseTask`。

### 协力凑不出兵

- `FailAssistanceCase` 按 `Released` 撤人留案，不销案。
- **目标正在打仗时不判失败**：目标或编队任何参与者在战斗
  直接 return。那一仗的结果才是答案，中途撤案会把已咬上去的截击队和承办人拆散。所以承办人正在打仗时也不新建协力组（协办的人加入不了这一仗，凑不够又不判失败，组和军团只剩承办人挂着；实机诊断 2026-10-08）。
- 防空转：`GwpTuning.Enforcement.AssistanceFailureCooldownHours = 72`，退回台账的案子
  72小时内排在普通可接目标之后；没有其他可接目标且本体力量已可应付时允许重试。冷却由 `SyncCooldownsAndRetries` 存档并恢复；同版本读档不会清掉72小时等待。（依据：源码复核 2026-10-17）
- 改追最近案件（`RetargetOrdinaryCasesToNearest`）等于接新案，和接案走同一道门 `CanPickUpCase`：
  冷却、`IsCaseWithinReach`，以及 [灰袍使者](case-envoy.md) 给了宽限、等使者收钱的案子不接。2026-09-25 日志里 `lord_1_55` 凑不出兵退回 2 小时后就被同一领主
  就近改追回来，当场再次凑不出兵。
- 不要让改追绕过接案门槛。（依据：实机诊断 2026-09-25）

## 接案门槛

记录犯罪与接下承办任务是两步。难度只限制接案，不阻止案件进入台账；冷却、等待使者收款和难度都由 `CanPickUpCase` 检查。（依据：源码复核 2026-10-17）

**难度上限**（`IsCaseWithinReach`）：目标战力超过当前可征调力量的九成，就不自动接案。

- `GetMaximumMusterableStrength` = 承办人自己 + 一天内能到且差事允许征调的灰袍领主
  （复用 `GetAvailableAssistanceCandidates` 的资格判定）
- 目标战力用 `GwpStrength.CaseTarget`：他所在战斗组（攻城营、**已挂靠**的军团或本队），
  **刻意不含身边路过的人** —— 那一档是临场变量，仅由宣战与主动接战门槛处理。名义上属于某军团、
  还在赶去会合的领主只算他本人（`GwpStrength.CombatGroupOf`，2026-10-07 起）。悬赏难度用同一个数。
- `CaseIntakeStrengthMargin = 0.9`（留一成余量）

**兵员下限：已撤除，不要重新加上**（依据：实机诊断2026-09-17；用户指出原推理不成立）。`HasManpowerForNewCase` 与 `CaseIntakeManpowerRatio` 不在当前源码；接案按可集结战力判断，不按本队人数设门槛。

## 补兵与差事竞价

`ProcessFinalDesires` 不给缺兵另设让位或加分；普通差事仍为 0.99，巡逻候选被压低，其他原版进城候选保留原分。
无军团时 69/159 人的灰袍领主进城原分可达 4–6，照原版去补兵。（依据：源码复核、实机诊断 2026-10-15）

## 协力编队：在编、空组释放、会合与招兵统计

现行定义集中在[协力编成](case-assistance.md)：逻辑组与原版军团的区别、征调与使者等待、协办旧案暂停、会合、招兵统计、追速分散、普通支援和玩家带路。

## 目标战力怎么算

接案/编成的本体力量、主动出战的区域力量及排除规则统一见[协力](case-assistance.md#稳定力量与区域力量)。区域不把城墙内静态守军名册当成周围领主；已经实际参战的目标侧名单仍按MapEvent读取，不能将“排除附近守军”写成实际战场从无守军参与。

## 协力组动态缩编

门槛、时长、放人次序见[协力缩编](case-assistance.md#协力组动态缩编)。

## 协力军团读档后补建定时事件

原版不为无王国军团重建定时事件，处理见[协力读档](case-assistance.md#协力军团读档后补建定时事件)。

## 脱离军团不吃混乱

仅跳过脱离军团入口、保留战后混乱，见[协力脱团](case-assistance.md#脱离军团不吃混乱)。

## 结案留痕

`CrimePool.EndTask` 带 `reason` 参数（默认 `unspecified`），每次销案写一行
`CASE_CLOSED`，含 `crime` / `offender` / `offenderActive` / `offenderPrisoner` /
`civilianCasualties` / `warDeclared` / `reason`。

具名理由：`dispatch_target_invalid`、`target_invalid`、`offender_defeated_in_battle`、
`case_owner_defeated_in_battle`、`owner_party_gone_after_battle`、
`assistance_strength_insufficient`、`*_offender_gone`，以及
`FailTaskBecauseOwnerCannotLead` 透传的既有 reason。

## 实机观察结论（2026-09-26 读 2026-09-17 至 09-25 诊断日志）

- `CASE_CLOSED` reason：`helper_released_to_assistance` 67、`target_invalid` 16、
  `released_for_forced_duty` 7、`offender_settled_by_delay_patrol` 3、
  `offender_defeated_in_battle` 3、`owner_cannot_lead_after_map_event` 1。无 `unspecified`。
- `CASE_KEPT_OPEN_OWNER_RELEASED` 36 次，全部是 `assistance_strength_insufficient`；
  之后都会被重新指派，但 `lord_5_13` 在 54 小时里被两名领主轮流接了又放 10 次，远短于
  72 小时冷却。9 月 25 日日志证实改追最近案件能绕过冷却（`lord_1_55`），已按上节修正；
  9 月 20 日那段的 AI 日志已滚动丢失，无法逐条归因。
- `CASE_WAR_RETARGETED_TO_CURRENT_FACTION`：日志期内没有触发，连锁开战无证据。
- `ASSISTANCE_MEMBER_RELEASED_SURPLUS`：9 月 25 日一局 15 次，放人后 10–20 小时再入组的
  占多数，是否算抖动取决于目标身边战力是否真的变了；未见同一小时内来回。

## 地图条声望图标

显示位置、交互限制与尚未实施的方案归 [玩家声望](player-justice.md)「声望图标的交互边界与待办」。

## 立即追截队

追截队归队、多余的人去哪里，见 [立即追截队](case-interceptors.md)。

宣战追捕每满 24 小时记一条 `WAR_PURSUIT_STALLED`（见 diagnostics.md），用来查两天支援队为什么会触发。

## 测试

`tools/CaseSettlement.Tests` **155 项**；`tools/Verify-CrimeReceipts.ps1`。
均为构建期验证。

`tools/tests/test_case_envoy_scenarios.py` 的45项断言运行真实人物案卷、任务结束/释放、使者维护及序列化方法：第三方俘虏、失队、禁用、未解析英雄均保留未了案；恢复带队或禁用解除后无需新犯罪重新入选，未解析人物的次数/伤亡经存读仍保留。另确认承办失败留责、换目标不撤使者、改追冷却恢复。UI、外交、军团收尾与引擎存档容器为受控边界，不等于游戏存读档验收。（依据：离线执行 2026-10-17）
