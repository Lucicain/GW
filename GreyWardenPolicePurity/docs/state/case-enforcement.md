# 案件生命周期与执法

> 当前状态：已部署，v1.4-r12 已发布；部分条目待实机观察
> 最后验收：`131abb0`（v1.4-r12），远端核验见 [`build-and-deploy.md`](build-and-deploy.md)
> 已复核至：`e16ff95`（89 处裸吞异常改为留痕，行为未变，见 `code-health.md`）；2026-09-23 工作树新增测试桩 `tools/CaseSettlement.Tests/FaultTraceStub.cs` 修复测试编译（177 项通过），生产行为未变
> 覆盖源码：`PoliceEnforcementBehavior*.cs` `PoliceAIDeterrenceBehavior.cs` `GwpArmyExitDisorganizedPatch.cs` `GwpData.cs` `GwpRuntimeState.cs` `tools/CaseSettlement.Tests/**`
> 待实测：`CASE_CLOSED` 的 reason 分布、`CASE_KEPT_OPEN_OWNER_RELEASED` 之后重新建队
> 能否被重新指派、`CASE_WAR_RETARGETED_TO_CURRENT_FACTION` 会否对大王国连锁开战、
> `ASSISTANCE_MEMBER_RELEASED_SURPLUS` 会否抖动

> ⚠️ 本文件只覆盖**案件生命周期与协力编成**。巡逻、悬赏、使者送单、练兵等
> 子系统尚未从流水提取，见 [`README.md`](README.md) 的"尚未提取"清单。

## 用户裁定的三条规则

1. 部队被打光、人被抓，**都算案件了结，都要上震慑**。
2. 雇佣兵转移阵营导致战斗脱离 —— **案件跟着人走，不跟着势力走**。
3. 人不是灰袍打掉的（逃亡或被别人俘虏）—— **这个人依然被通缉**，等他重新建队，
   灰袍照样找他麻烦。

## 震慑口径

- `PoliceAIDeterrenceBehavior.RegisterWardenBrokeOffenderParty` 转调既有的
  `RegisterPlayerEnforcementOutcome`（方法名里的 Player 是历史遗留，内容与承办方
  无关：本人震慑、同族转述、同场目击）。
- **`countAsArrest: false`** —— 打光部队但没拿到人时震慑照上，但不计入"被捕次数"。
  被捕次数是履历，不能靠打光部队刷。
- `RegisterDefeatDeterrenceIfNotCaptured` 先排除 `IsPrisoner`，避免与
  `OnHeroPrisonerTaken` 重复登记；私有方法自带的 `SamePunishmentHours=1` 去重是
  第二道保险。

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

## 结案 vs 换承办人

`CrimePool.EndTask` 会**连案卷一起销毁**（`HasOpenCase=false` + `_ledger.Remove`）。
调用方必须清楚自己要的是"销案"还是"换承办人"（后者用 `RetargetTask`）。

`RetireTaskKeepingCaseIfOffenderAlive`：人还活着就只撤承办任务
（`CrimeState.ReleaseTasksForOffender`，卷宗保留），解散协力组，写
`CASE_KEPT_OPEN_OWNER_RELEASED`（带 `offenderPrisoner` / `offenderFugitive`）；
**人真死了才 `EndTask`**。

走这条路的情形：

- `criminal == null`（`offender_party_missing`）
- `!criminal.IsActive`（`offender_party_inactive`）
- 协力凑不出兵（原先直接销案，已改）

**不需要另写"重新通缉"逻辑**：`CrimeRecord.Offender` 本来就按英雄解析
（`hero.PartyBelongedTo` 优先），`IsOffenderPursuable()` 要求 `Offender?.IsActive`。
他一天没队伍就一天不被指派，重新拉起队伍的当轮自动重新变成可追捕案件。
**这条规则靠既有数据结构成立，没有新增状态机。**

灰袍自己打赢那条不受影响 —— `OnMapEventEnded` 当场以 `offender_defeated_in_battle`
结案。

### 协力凑不出兵

- `FailAssistanceCase` 走 `RetireTaskKeepingCaseIfOffenderAlive`，不再销案。
- **目标正在打仗时不判失败**：`offenderParty.MapEvent != null || leader.MapEvent != null`
  直接 return。那一仗的结果才是答案，中途撤案会把已咬上去的截击队和承办人拆散。
- 防空转：`GwpTuning.Enforcement.AssistanceFailureCooldownHours = 72`，退回台账的案子
  72 小时内不再被指派。冷却只活在本次运行内（读档后最多多试一次，无害）。

## 立案门槛

两道门槛都设在**接案这一步**，不影响已经在办的案子。

**难度上限**（`IsCaseWithinReach`）：目标战力超过全家族能凑出来的战力就不立案。

- `GetMaximumMusterableStrength` = 承办人自己 + 所有还能被拉进协力组的灰袍领主
  （复用 `GetAvailableAssistanceCandidates` 的资格判定）
- `GetCaseIntakeTargetStrength` 用 `GetNativeCombatGroupStrength`，**刻意不含身边
  路过的人** —— 那一档是临场变量，由协力编成随时增减应对
- `CaseIntakeStrengthMargin = 0.9`（留一成余量）

**兵员下限：已撤除，不要重新加上**（依据：实机诊断 2026-09-17 的 AUCTION 读数；用户指出原推理不成立）。 `HasManpowerForNewCase` 与
`CaseIntakeManpowerRatio` 已删除。原因记录在案：

- 「没案子时原版欲望会带他去招兵」是**错的**。`SuppressAssignedPatrolScores` 只改
  `AiBehavior.PatrolAroundPoint`；`SuppressLeaderlessMergeScores` 只对无英雄队生效。
  **进城候选从来没有被压制过。** 实机 AUCTION 读数：`EscortParty=0.9900`，
  `GoToSettlement@...=0.5994` 起全是原版原值，被改的只有 `PatrolAroundPoint`
  （`2.9566 → 0.0300`）。领主不去补兵纯粹是 0.599 排不过 0.99。
- 「没案子」这个前提不成立：六名领主 ~328 个样本里 `dutyIntent=none` 只出现
  **5 次（1.5%）**。
- 就算真闲下来也不会去招兵：原版巡逻候选 `2.9566` 直接盖过 `GoToSettlement` 的 `0.5994`。
- 结论：设兵员门槛**只会让弱队既不接案也不补兵**，净增一个卡死状态。

真要解决"几十个人也敢接案"，需要一条**显式的招兵职责**（兵员低于阈值时给出指向
定居点、分数 > ~3.0 的候选，补满后释放）。那是新功能，不是阈值调整，等用户裁定。

## 兵员不足时差事让位

`ApplyReinforcementRelief` 在 `ProcessFinalDesires` 算出 `dutyScore` 之后介入：
兵员不足时把差事分压到**原版最佳非巡逻候选的九成**（`DutyReliefScoreFactor`），
让原版自己的进城候选排到前面。巡逻压制保留。

**只管兵员，不管粮和伤**：缺粮/重伤的原版候选能升到 1~19.6，本来就压得过 0.99；
"人少"走的是普通进城候选（0.35~0.85），永远排不过 0.99。问题纯粹在排序。

## 目标战力怎么算

守军和民兵**不算**：

- 无战斗时 `CanNearbyCombatGroupJoinTarget` 直接排除
  `candidate.IsGarrison || candidate.IsMilitia`，另排除已在别的 `MapEvent` 里的、
  被围城的、非围城主的围城营。
- `GetNativeCombatGroupStrength` 只取围城营总和 / 军团战力 / 本队战力三者之一，
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

`TryShrinkAssistanceGroup` 挂在 `UpdateLordAssistance` 既有组分支里、`targetStrength`
算出之后。

起因：`targetStrength` 是**现场战斗群之和**（内圈 `joiningRadius` 全额、外圈到
`threatRadius` 按距离衰减），一个路过的领主就能把目标战力抬高一倍。旧实现编成
只增不减，路人走了就变成五百打一百。

- **回滞是刻意的**：入组看 `committed <= target`，放人要求放完之后仍然
  `committed > target * AssistanceReleaseMargin(1.25)`。两个门槛之间留一档，
  路人来回走才不会让协力组跟着拆装。
- 每轮最多放一个。
- `AssistanceMinimumMemberHours(6)` 挡住"来了又走"。
- 正在 `MapEvent` 里的人不抽；组长在打仗时整组不缩编。
- 选**离目标最远**的那个放 —— 他对接下来这一仗贡献最小。
- 写 `ASSISTANCE_MEMBER_RELEASED_SURPLUS`。

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

## 地图条声望图标

- 当前位置：`SecondaryInfoItems`，插在 `influence` 之后（按用户要求"挨着影响力"）。
- **代价已告知用户**：那一排只在信息条展开（`IsInfoBarExtended`）时显示；
  `PrimaryInfoItems`（第纳尔所在）才是常驻。用户在"挨着影响力"与"常驻可见"之间
  选了前者。
- **点击打开事务界面：做不到。** `MapBar.xml` 两排的 `ItemTemplate` 外层是
  `HintWidget : Widget`，反射确认 `Widget` 没有任何 click 事件（只有 `EventFire`
  与各类 `PropertyChanged`），点击是 `ButtonWidget` 独有。金币和影响力自己也点不动。
  要改只能整份覆盖 SandBox 的 `MapBar.xml`（219 行），会在游戏更新时失效并与任何
  改地图条的模组冲突。
- **可行的最小方案（未做，等用户点头）**：照 `GwpCaseArchiveScreen.Open()` 的现成套路
  （`new GauntletLayer` + `AddLayer` + `LoadMovie`）加一个常驻小层，一个
  `ButtonWidget` 带图标与数字，`Command.Click` 调 `GwpCaseArchiveScreen.Show()`，
  `IsFocusLayer = false`、不设 `InputRestrictions`。不覆盖任何原版文件。

## 测试

`tools/CaseSettlement.Tests` **177 项**；`tools/Verify-CrimeReceipts.ps1`。
均为构建期验证。
