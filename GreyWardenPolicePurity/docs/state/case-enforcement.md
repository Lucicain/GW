# 案件生命周期与执法

> 当前状态：原有案件行为已验收；协力编队统一已部署待实测；使者有结果前不调人、协办人保留自己的案子已部署待实测
> 最后验收：`9d005bb`
> 已复核至：`1782a40`（2026-10-16）
> 覆盖源码：`PoliceEnforcementBehavior*.cs` `PoliceAIDeterrenceBehavior.cs` `GwpArmyExitDisorganizedPatch.cs` `GwpData.cs` `GwpRuntimeState.cs` `GwpEncyclopediaHeroPageVM.cs` `tools/CaseSettlement.Tests/**`
> 待实测：协力组不再反复拉人放人；最后一人走后军团消失、案子还在；组长打不过先会合；军团长缺兵会进城招兵；使者在路上时不调人，谈崩才组协力；协办人协力结束后接着办自己谈崩的案子；agent 核对 `ASSISTANCE_WAITING_FOR_ENVOY`、`ASSISTANCE_HELPER_CASE_*`

强制差事不再取消自动练兵换兵队：它由独立实体执行，不占领主差事，见 [运兵](warden-training.md)。（依据：用户裁定、源码复核 2026-10-15）

本文件覆盖**案件生命周期与协力编成**。巡逻、悬赏、使者送单、练兵分别见
[`patrol.md`](patrol.md)、[`player-justice.md`](player-justice.md)、[`dispatch.md`](dispatch.md)、[`warden-training.md`](warden-training.md)。

负声望赎罪单独见[赎罪委派](atonement.md)。玩家占案统一查询 `GwpPlayerCaseAssignment`；案件转给玩家时，生命周期入口同时撤回普通承办人与使者。每小时在普通追捕、宣战之前释放玩家已持有的案子；显式请求的带路支援仍按原分支执行。（依据：源码复核2026-10-15）

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
- **被捕次数**：人真的落到手里（被押走、交清罚金、兑现谈成的处置）才算一次被捕；只打光部队、
  人跑掉，震慑照上但不计被捕——被捕次数是履历，不能靠打光部队刷。灰袍和玩家同一条规则，
  调用方用 `countAsArrest` 明说。（依据：用户裁定 2026-09）
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
| `Closed` 结案不算办成 | 结案 | 承办人战败或不能再带队、承办部队消失、被别人了结（`CloseCase`） |
| `Released` 撤人留案 | 留案，人死了才销 | 目标失效或暂时追不到、协力凑不出兵、行政改派（村庄善后、玩家委托、玩家通缉改派）、玩家接手 |

- 撤人留案只看**人还活不活着**（`CrimePool.ReleaseTask`），不看他的部队还在不在：部队被别人打散、
  人还活着时案卷照留。以前行政改派看部队、目标失效看人，两套规则并存。
- **不需要另写"重新通缉"逻辑**：`CrimeRecord.Offender` 按英雄解析（`hero.PartyBelongedTo` 优先），
  `IsOffenderPursuable()` 要求 `Offender?.IsActive`。他一天没队伍就一天不被指派，重新拉起队伍的当轮
  自动重新变成可追捕案件。
- 外部结案（玩家当面了结、玩家押人、委托交差、被捕清账、同伴代缴）都经 `CloseCase`：正在承办的灰袍
  先按 `Closed` 收尾，再销卷。以前直接删任务，承办人的协力组、宣战记录、AI 只能等两日扫除兜底。
- 承办部队消失或案卷目标无法恢复的任务，每小时由 `CleanCaseLedger` 按 `Closed` 收尾；死人的案卷移出台账。
- 押送被俘玩家结束那一段是玩家案件自己的流程（有防崩溃的步骤顺序），不走 `EndCaseTask`。

### 协力凑不出兵

- `FailAssistanceCase` 按 `Released` 撤人留案，不销案。
- **目标正在打仗时不判失败**：`offenderParty.MapEvent != null || leader.MapEvent != null`
  直接 return。那一仗的结果才是答案，中途撤案会把已咬上去的截击队和承办人拆散。所以承办人正在打仗时也不新建协力组（协办的人加入不了这一仗，凑不够又不判失败，组和军团只剩承办人挂着；实机诊断 2026-10-08）。
- 防空转：`GwpTuning.Enforcement.AssistanceFailureCooldownHours = 72`，退回台账的案子
  72 小时内不再被指派。冷却只活在本次运行内（读档后最多多试一次，无害）。
- 改追最近案件（`RetargetOrdinaryCasesToNearest`）等于接新案，和接案走同一道门 `CanPickUpCase`：
  冷却、`IsCaseWithinReach`，以及 [灰袍使者](case-envoy.md) 给了宽限、等使者收钱的案子不接。2026-09-25 日志里 `lord_1_55` 凑不出兵退回 2 小时后就被同一领主
  就近改追回来，当场再次凑不出兵。
- 不要让改追绕过接案门槛。（依据：实机诊断 2026-09-25）

## 立案门槛

两道门槛都设在**接案这一步**，不影响已经在办的案子。

**难度上限**（`IsCaseWithinReach`）：目标战力超过全家族能凑出来的战力就不立案。

- `GetMaximumMusterableStrength` = 承办人自己 + 所有还能被拉进协力组的灰袍领主
  （复用 `GetAvailableAssistanceCandidates` 的资格判定）
- 目标战力用 `GwpStrength.CaseTarget`：他所在战斗组（攻城营、**已挂靠**的军团或本队），
  **刻意不含身边路过的人** —— 那一档是临场变量，由协力编成随时增减应对。名义上属于某军团、
  还在赶去会合的领主只算他本人（`GwpStrength.CombatGroupOf`，2026-10-07 起）。悬赏难度用同一个数。
- `CaseIntakeStrengthMargin = 0.9`（留一成余量）

**兵员下限：已撤除，不要重新加上**（依据：实机诊断2026-09-17；用户指出原推理不成立）。`HasManpowerForNewCase` 与 `CaseIntakeManpowerRatio` 不在当前源码；接案按可集结战力判断，不按本队人数设门槛。

## 补兵与差事竞价

`ProcessFinalDesires` 不给缺兵另设让位或加分；普通差事仍为 0.99，巡逻候选被压低，其他原版进城候选保留原分。
无军团时 69/159 人的灰袍领主进城原分可达 4–6，照原版去补兵。（依据：源码复核、实机诊断 2026-10-15）

## 协力编队：在编、空组释放、会合与招兵统计

谁算在编、空了怎么收、要不要先会合，只在 `PoliceEnforcementBehavior.Formation.cs` 判断，不各写一份。（依据：设计 2026-10-15）
军团维护、原版解散保护、会合意图都调它。只放编队，不结案——案件结束仍走上面的生命周期入口。

- **在编**：有效的协办领主，加上本案还在参与的普通临时支援队。返程队、使者、独立追截的极速截击队不算，不靠它们保留军团。
- **空组释放**：一个有效协力都不剩（缩编、被俘、部队没了、改派、支援返程或销毁），就放掉协力组和军团壳子，案子照留、
  不算办成也不讲和。正在打仗时不从战斗里拆人，仗打完再核对。灰袍领主带着没有协力组认领的巡逻军团，也用原版解散入口放掉。
  （依据：用户裁定 2026-10-15：没有协力组时应放掉军团壳子）
- **先征调再建军团**：组长打不过时先按承诺战力征调协办人，有人到编才登记协力组、建军团；一个都征不到就不登记空组，
  下一小时再试（玩家委托照留，普通案件按「协力凑不出兵」退回台账）。玩家求援增员也不再要求「已有军团」。
- **等使者**：组长的案子还在等使者的结果（使者在路上、宽限中、约了比武，或还没派而他派得出、他在打仗时仗后就派）时不征调协办人，
  判断与使者的「这一小时不宣战」同一套（`IsAwaitingEnvoyResult`）。谈崩、派不出使者、已宣战、玩家案件与玩家求援照常组。
  （依据：用户裁定 2026-10-16：使者有结果前不调人；谈成自然就结束）
- **协办人自己的案子**：手上有普通案件的领主也能被征调，案子留在他手上，协力期间暂停：不派使者、不宣战、不派周期支援
  （`UpdateTasks` 与 `GetEligibleDelaySupportTasks` 按协办人跳过）。已派出的使者照常谈，谈成就结案，谈崩的等协力结束他接着办。
  自己的使者约了比武（赶路或留城等赛）的不征调；协力中约成比武就离组赴约。（依据：用户裁定 2026-10-16，取代「协力抽调就退回台账」）
- **原版解散保护**只给有承办任务、有实际协力、不在速度分散中的编队；军团长死亡或部队被移除照常放行，王国军团不受影响。
- **会合**：组长靠自己和身边友军还打不过、需要的协办人又不在身边时，组长去最近、走得到的那位协办人那里，协办人去找组长
  （`GetAssistanceRendezvous` → `TryGetAssistanceDuty`）。到了一起、或现场已占优就继续追目标。会合目的地每小时由已存档的
  名单和当前位置现算，不另存。速度分散的编队缺现场力量时也会合，不各追各的。补给、疗伤照常参与竞价。
- **招兵统计**：原版 `AiVisitSettlementBehavior.CalculatePartyParameters` 对军团长只数挂靠部队、不数军团长本队，成员还在路上时
  人数为 0，伤员比 0/0 得 NaN，招兵候选整个丢掉。灰袍协力军团长改用「本队 + 实际挂靠的部队」这一份名单算人数、伤员、
  兵额比例和原版正常兵额（`GwpWardenVisitPartyParametersPatch`，信众村加分同一份名单）；后面的招兵公式不动。进城后原版
  `RecruitmentCampaignBehavior.OnBeforeSettlementEntered` 本来就给军团长和挂靠部队各自招兵。
  （依据：C#反编译 v1.4.8；实机诊断 2026-10-15：两位领主各带单队军团 160–170 小时没有正常进城候选）
- 承诺战力只决定征调多少人；现场宣战另按 `GwpStrength.Compare` 的现场战力判断，两者不混用。
- 离线回归 `tools/tests/test_assistance.py`（63 项）抽取生产方法，对照本机原版招兵与解散保护代码，含原版零挂靠 NaN 的反例。

## 目标战力怎么算

守军和民兵**不算**：

- 无战斗时 `GwpStrength` 的周边部队筛选直接排除
  `candidate.IsGarrison || candidate.IsMilitia`，另排除已在别的 `MapEvent` 里的、
  被围城的、非围城主的围城营。
- `GwpStrength.CombatGroup` 只取围城营总和 / 军团战力 / 本队战力三者之一，
  **任何情况下都不含定居点守军**。
- 唯一例外是目标已在打一场仗（`mapEvent.IsFinalized == false`），走
  `mapEvent.CanPartyJoinBattle` —— 那是真的已在场上的部队，算进去是对的。

**已修的遗漏**：守军/民兵/围城的排除原先写在战斗分支**之后**，导致"无战斗时守军
不算、一旦打起来守军就算"两条路口径相反。实机后果：

```
lord_2_20_1_party_1                       : 132.37   ← 犯人本队
garrison_party_castle_S6_clan_sturgia_8_1 : 327.88   ← 城堡守军
militias_of_..._castle_S6_...             : 246.30   ← 城堡民兵
targetStrength = 706.55  >  maximumStrength = 682.70
```

犯人本人只占 132.37，守军加民兵占 81%，于是判定整个家族都打不过 → 撤案讲和 →
正在打的仗失去交战依据 → 无结果结束，人走掉。排除已提到战斗分支之前。

## 协力组动态缩编

`TryShrinkAssistanceGroup` 挂在 `UpdateLordAssistance` 既有组分支里。

- 要不要征调帮手只看目标自己那一组（`GwpStrength.CaseTarget`），和接案门槛同一个数。（依据：实机诊断 2026-10-15）
  「自己那一组」是他和已挂靠的军团、攻城营。放人时按它与「现场战斗群之和」（`EnemyAround`，含身边路过的人）中较大的那个留足余量，
  放完不会立刻又低于需要。路人只影响现场宣战判断，不再拉人放人——以前路人来去让协力组每十几小时拉一个、放一个。
- 他正在打仗时「凑不出兵」先不判、等那一仗的结果，新组这时也不登记，不会每小时建了又拆。（依据：实机诊断 2026-10-15：围城中的目标让空组连续 12 小时建了又拆）

- **回滞是刻意的**：入组看 `committed <= target`，放人要求放完之后仍然
  `committed > target * AssistanceReleaseMargin(1.5)`。两个门槛之间留一档，
  路人来回走才不会让协力组跟着拆装。
- 每轮最多放一个。
- `AssistanceMinimumMemberHours(12)` 挡住"来了又走"。
- 正在 `MapEvent` 里的人不抽；组长在打仗时整组不缩编。
- 选**离目标最远**的那个放 —— 他对接下来这一仗贡献最小。
- 写 `ASSISTANCE_MEMBER_RELEASED_SURPLUS`。

## 协力军团读档后补建定时事件

协力军团是 `new Army(null, leader, Patrolling)`，不属于任何王国。原版 `Army` 的两个定时事件
（每小时：士气/凝聚力/解散检查；每 0.1 小时：把赶到军团长身边的成员并入）标为 `[CachedData]`
不入档，读档后由 `Army.OnAfterLoad` 重建，而原版只在 `Campaign.InitializeCampaignObjectsOnAfterLoad`
里对 `kingdom.Armies` 调它。所以读档后协力军团没有定时事件：成员并不进去，解散时
`DisperseInternal` 最后删除定时事件撞空引用（2026-09-26 两次读档后各出现一次，已被捕获）。

`RestoreEnforcementArmyTicks`（`OnGameLoadFinishedEvent`）对灰袍家族领导的无王国军团、
定时事件为空时调同一个 `OnAfterLoad`。每次读档都需要这一步，不是旧档补正。
（依据：C#反编译 v1.4.8，2026-09-26；运行时反射确认字段与方法存在）

## 脱离军团不吃混乱

`GwpArmyExitDisorganizedPatch`：原版
`DisorganizedStateCampaignBehavior.OnPartyRemovedFromArmy` 对每一支脱离军团的队伍
无条件 `SetDisorganized(true)`（−40% 速度、6 小时）。那是为"集结一次、打一仗、散伙"
的王国军团写的；灰袍协力军团按目标现场战力随时拉人放人，每次进出吃一记 40%。

- **只跳过"脱离军团"这一个入口**，战斗结束/突围/菜单进攻照常生效。
- 不走 `PartyImpairmentModel.CanGetDisorganized` 的模型覆盖 —— 那是全局开关，
  会把战后混乱一起关掉，超出用户要求。
- 事件签名只给 `MobileParty` 不给 Army，无法细分"只有脱离灰袍军团才跳过"；
  灰袍是无封地独立家族、不加入王国军团，实际等价。写 `ARMY_EXIT_DISORGANIZED_SKIPPED`。

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
