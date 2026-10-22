# 诊断系统

> 当前状态：事务派遣入口健康探针已退休；军团费用失败、村民出车容量/货仓与专运标记诊断仍在役
> 最后验收：2026-10-21 事务派遣入口用户未发现问题，已核对点击与选兵日志；其他功能仍按各自验收范围
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpPartyDutyMovementPatch.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs` `GwpCommon.cs` `tools/Analyze-Firesworn.py` `tools/tests/test_firesworn_logs.py` `PoliceRaidDeterrenceModel.cs` `tools/Capture-StackOverflow.ps1` `GwpWardenDispatchDialogue.cs`
> 待实测：投案修复后正常游玩核对，若仍故障则取军团参数；退出原生释放与收养交接仍随所属功能核对

## 原则

搜捕新增TARGET_DOWN、BODY_CARRIER、BODY_ATTACHED、BODY_PROGRESS及DUEL_WALK，回答倒地是否登记、由谁搬运、十秒后身体与搬运兵位置、两段正常步行的目的地。均只在诊断版按状态转换或每名搬运兵一次采样，验收后退休；具体边界归[进城搜捕](city-search.md#城防增援与场景衔接)。（依据：本轮正常游玩取证设计2026-10-22）

直接招守村兵使用 `GUARD_ENLISTED`，记录实际兵种 composition、人数、随兵军械和留守量；统计器读取同一事件。该功能待用户实测低阶优先与留守正确后退休健康路径记录。（依据：本轮直招功能验证设计 2026-10-15）

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内，
纯 trace 的退休只改变开发者日志；通用 `Guard` 已移除，故障观察器不吞业务异常，开发与玩家版执行相同业务。军械库每日汇总及异常统计整段仅在诊断版编译，玩家版不执行这些遍历与字符串构造。（依据：源码复核2026-10-14）

收养驻留新增三个状态转换事件：`ADOPTION_STAY_STARTED_OR_RESUMED`、`ADOPTION_STAY_PAUSED`、`ADOPTION_STAY_FINISHED`，只在开始/恢复、暂停或收尾记录目标村和剩余小时，全部在诊断编译开关内。用于核对赴约/返程不计时及完成释放；用户确认收养交接可用时同轮退休。实现随[收养](village-relief.md)维护。（依据：已复现实机缺陷后的验收诊断 2026-10-17）

军团费用失败探针ARMY_INFLUENCE_FAILURE与ARMY_TOTAL_INFLUENCE_FAILURE仅在诊断版编入，记录实际参数并原样传播异常，成功不写；根因、证据缺口与退休条件只在[地图AI](warden-ai.md#军团影响力计算空英雄故障)维护。（依据：实机崩溃与取证需要2026-10-19）

普通案宣战增加既有区域评估快照，停滞记录增加lastInterceptorGate；只复用业务已经计算的值，不重复调用原版评分。证据缺口、字段与退休条件随[协力](case-assistance.md#区域宣战与持续接战)和[追截队](case-interceptors.md#卡拉多格追捕的证据边界)维护；尚未闭合，不宣称已修复首次接触问题。（依据：本轮实机证据缺口2026-10-20）

## 谁来管

首队准备失败时诊断版增加CASE_DECLARATION_WAITING_INTERCEPTOR，沿现有门槛记录原因；玩家支援等待记录额外区分localReady与interceptorReady。均复用业务判定，不额外评分、不新增定时器。随[宣战/首队联动](case-interceptors.md#常规追截与战力复核)验收退休。（依据：空宣战已复现及诊断设计2026-10-20）

诊断的加、删、加强与日志清理由 agent 自己决定并执行，不等用户下指令，也不请用户
去读日志。遇到难以解决的问题或体验问题时，agent 先加强相关诊断取证，并配合 C# 反编译
与 `TaleWorlds.Native.dll` 反汇编查实原版规则（方法见
[`../reference/investigation-methods.md`](../reference/investigation-methods.md)）。
用户说"测过了没问题"时，agent 要自己读在役日志核对待观察项。（依据：用户裁定 2026-09-26）

## 加与删的判据

**卡住时加强。** 开发停在某个具体问题上时，加宽该功能的 trace、补上下一次测试需要
的字段，并在 state 文件里写清每条新行要回答什么问题。

**确认后退休。** 用户在实机确认功能可用时，**在建立检查点的同一个任务里**退休它的
诊断。不要让一个已经定下来的功能每 tick 汇报自己正常。（依据：流程）

**按健康路径 vs 失败路径切，不按主题切。** 删掉在一切正常时会触发的 trace；
有依据的 `catch` 里的留痕保留——它们在健康的一局里保持沉默，是未来游戏版本弄坏东西时的信号。
没有依据的 catch 不该存在（用户裁定 2026-10-05，见 `code-health.md`）。

**整个方法体只有一条 trace**，说明这个方法或补丁只为记录而存在，连同 trace 一起删。

**数据也要删。** 诊断系统退休时，同时删掉游戏 Documents 目录下的日志文件、
一次性崩溃转储和调查已闭合的反编译输出。仍在生效的诊断可以留，但它积攒的旧日志
不应该留——findings 记进 state 之后，旧日志没有进一步用途。

**游戏的崩溃报告同样归 agent 管**：`C:\ProgramData\Mount and Blade II Bannerlord\crashes\` 下每份报告带
700～850 MB 的转储（Modding Kit 的编辑器版本每条警告都会生成一份）。agent 自己读（转储用 WinDbg 的
`cdbX64.exe`，见 `../reference/investigation-methods.md`），结论写进 journal 后直接删，不需要再问用户。（依据：用户裁定）

**按当前覆盖范围命名。** 范围变了就改系统名和日志文件名，不要留一个误导的名字。（依据：流程）
（例：战场编队采样扩到双方所有编队后曾整体改名，现已退休。）

领主普通装备加成的 `COMBAT` 采样字段与退休条件见[lord-combat.md](lord-combat.md)，不使用案件时间戳，竞技场场景也可记录；领主与最高阶步兵/弓手的共享锁血转换采样只在[last-stand.md](last-stand.md)维护。

## 运兵在役诊断

自主练兵送老兵已退休，TRAINING_TRANSPORT_DISPATCHED随机制删除；旧在途实体仍可用DELIVERED记录实际交付，随接续验收退休，规则只在[挂名练兵官](warden-training.md)维护。BALANCE_DISPATCHED增加真实capacity/weight与danger，DAMNATION增加surplusBurned/promotedToGuard，用于核对分批运量及超额两换一；确认后退休健康路径。（依据：用户裁定及诊断流程2026-10-20）

## 日志文件

村民运输验收沿用MARKET_DISPATCHED，容量、货仓、专运/下一趟待办字段和退休条件只在[村民运输](firesworn-villagers.md#普通贸易专运与分账)定义；不额外循环记录健康状态。（依据：诊断流程 2026-10-19）

经济分析器按完整行去重并保留时间戳时区；公库卖货收入只统计当前MARKET_HOME.goldToTreasury，不把nativeTradeGold村税混入。装车估值可能重复出车且与回款跨窗口，不据旧MARKET_SOLD缺席计算“卖出0%”或推断城镇缺钱；信众净户数减少也不等于没有自然增长。（依据：日志、生产卖货源码与分析器回归 2026-10-19）

周期50人兜底的派出记录与退休条件只在[追截队](case-interceptors.md#周期兜底的定位与当前缺口)维护。

AI快照的 `ordinaryCaseEligible` 当前只调用 `PoliceStats.CanHandleOrdinaryCase`，代表队伍活跃、领主活跃与灰袍归属，不含差事占用、使者约定、可接目标或战力门槛；不能把True当成完整派案资格。完整接案还经过 `GwpWardenDuties` 等所属入口。（依据：源码与实机日志 2026-10-17）

负声望赎罪在役阶段记录与退休条件只在[赎罪委派](atonement.md)定义，实测后由agent读取案件日志核对。

游戏 Documents 目录下：

| 文件 | 内容 |
|---|---|
| `GreyWarden-Faults.log` | 故障与静默吞异常留痕，只追加不滚动。**健康的一局里只有 `FAULT_WATCH_ARMED` 一行** |
| `GreyWarden-AI-Diagnostics.log` | AI / 欲望拍卖 / 协力编成 / 地图战斗采样，每局开新文件，旧的滚进归档 |
| `GreyWarden-Case-Events.log` | 案件生命周期事件 |
| `GreyWarden-Diagnostics-Archive` | 滚动归档 |

## 火誓者监控字段

`GreyWarden-AI-Diagnostics.log` 里所有 `| FIRESWORN |` 行，按 stage 分：

| 类别 | stage |
|---|---|
| 每日快照 | `DAILY_VILLAGE`（信众户与比例、在村守村兵构成、实体名册中灰袍各兵种经验 guardXp、本村灰袍总人数 population、上限 cap、缺额 headroom、原版民兵、祭司势力、其他要人 others（职业:势力，没有祭司时也写）、是否最强、格子、军械库按类件数、武装率）、`DAILY_LORD`、`DAILY_TOTALS` |
| 留守队经验 | `GUARD_EXPERIENCE`（来源 damnation，earned/实际granted/容量不足unused）；`DAMNATION` 附 experience/experienceGranted。核对烧人经验在村内累积、同版存读接续后退休，分配规则只在村庄与仪式state定义。分析器按来源汇总经验，不与新增兵数相加。 |
| 守村升级付款 | `GUARD_UPGRADE_PAYMENT`（party/troop/payer、quoted与实际paid、付款后treasury），仅观察真实原版金币交易，报价不重复计算。核对灰袍兵由灰袍公库付款、杂兵不改归属后退休。 |
| 火誓者来源 | `SWORN_GROWTH`（驻留/自然增长信众户 fromWardensHouseholds/fromHearthHouseholds、补兵 regrown、补前缺额 headroomBefore）、`RECONSTRUCTION_SWORN`（added为实际增户）、`CONGREGATION_STARTED`、`CONGREGATION_COMPLETED`（gain为实际增户）、`CONGREGATION_CANCELLED`、`MISSION_DISPATCHED`、`MISSION_JOINED`、`DAMNATION`（烧了谁、生成什么）；分析器将户、兵、人次分项标注，旧集会日志按households前后值求实际增户。 |
| 俘虏与地牢 | `HANDOVER`、`DEFENCE_SPOILS` |
| 缴获 | `ARMORY_LOOT`（每场战斗每本账一行） |
| 招兵 | `PRIEST_RECRUITED`（从守村队带走几个）、`ZEALOTS_ENLISTED`、`KITS_MOVED`、`PRIEST_SPAWNED`、`PRIEST_HOME_RESTORED`、`PRIEST_DISAPPEAR_BLOCKED` |
| 无领主队 | `FAITH_PARTY_FORMED`、`DETACHMENT_SETTLED`（追截队多余的人在信众村落脚）、`BALANCE_DISPATCHED`、`BALANCE_DELIVERED`、`MARKET_DISPATCHED`、`MARKET_SOLD`、`FAITH_PARTY_HOME`、`FAITH_PARTY_TURNED_BACK`、`FAITH_PARTY_LOST` |
| 领主进村意愿 | `DAILY_LORD` 和领主类异常里的 `faithVisit`：上一小时分最高的信众村和分数 |
| 军械库 | `ARMORY_SURPLUS_*`、`ARMORY_LOST_IN_DEFEAT`、`ARMORY_RELOCATED` |
| 保护费 | `PROTECTION_FEE` |

烧村抑制调查使用Case-Events中的`RAID_SCORE_SAMPLE`，每位领主每24游戏小时在普通村/有信众比例村各最多一行：真实原版返回baseScore、个人deterrenceMultiplier、faithShare、faithMultiplier、合计multiplier/result、当前注册activeModel与候选村。只旁观本次计算，不为打印重算评分；只编入GWP_DIAGNOSTICS，玩法无依赖，不保存采样冷却。用户确认抑制链路后退休此健康路径诊断。（依据：用户反馈、流程2026-10-16）
| 仪式画面 | `RITUAL_MENU_SHOWN` |
| 每日异常 | `ANOMALY`，kind：`FAITH_PARTY_ADRIFT`、`FAITH_PARTY_TRIP_LONG`、`PRISONERS_PILING`、`LORD_HOLDING_AT_SETTLEMENT`、`LORD_UNDERSTRENGTH`、`PRIEST_SLOTS_EMPTY`、`ARMORY_UNARMED`、`GOODS_BACKLOG`、`ARMORY_NON_POSITIVE`、`TREASURY_LOW` |

**分析脚本**：`python tools/Analyze-Firesworn.py`（`--all` 读全部日志）。只取最近一次启动游戏以来的监控行（含中途滚动归档的部分），
输出总览与收支、每座有火誓者的村子（人数来源、守村队、祭司势力、军械库按类）、新信众村出现的速度、每位领主、无领主队与仪式、异常汇总，
最后列出需要看的问题；当前、归档与启动标记都按UTF-8 BOM读取。

区域业务评估不写成真实initiative结果；`GwpInitiativeDiagnosticsPatch` 跳过同步预估，只在区域守卫之后旁观原版正式结果。区域数据使用业务评估已产生的原值，不为了打印重跑原版评分。（依据：流程、离线执行2026-10-18）

进城搜捕的 `CITY_SEARCH` 行写进 AI 日志：SELECT_TROOPS、MISSION_OPEN（场景与城堡标记）、SPAWNED（实际用的出生点、兵数、关门数、守卫数）、ORDERS（各编队人数、是否已转玩家控制、移动命令）、FOLLOW_CHECK（进场 10 秒后士兵离玩家的平均与最远距离）、ORDER_ISSUED（每次下令的命令、任务模式、作用与选中编队数）、ORDER_CHECK（下令 5 秒后士兵平均位移与编队命令）、GUARD_CHALLENGE、GUARD_PERSUASION（声望档、论点强弱、关系、犯罪评级与折成的加减）、GUARD_DECISION、REFUSED_CHOICE（谈崩后选打或撤）、HEADING_IN（说要走或选撤后往城里走：警告或开战，离进城处多远）、FIGHT_START（开战起因、双方人数与 30 米内平民数）、RESERVES_READY（真实预备兵数、绑定守卫、来源队）、RESERVES_NO_POINT（缺少实际出生点时每3秒至多一条）、CIVILIAN_FLIGHT（8 秒后跑开或消失的平民数）、FIGHT_END、MISSION_END（阵亡与重伤写回数）。玩家宣战另写 FIELD_ARREST 的 CASE_PLAYER_WAR。用来在玩家正常游玩时核对第1段场景行为，不需要玩家专门复现；用户确认第1段可用后退休健康路径，CIVILIAN_FLIGHT 留到群殴阶段验收。规则见[进城搜捕](city-search.md)。（依据：流程 2026-10-21）

## 在役

| 系统 | 为什么还留着 | 退休条件 |
|---|---|---|
| `GwpAiDiagnostics`（AI 日志与 `GreyWarden-Case-Events.log`） | 案件收尾、战力归属与差事避让的共享判断改动仍待实测；各子系统已提取 state | 用户确认对应行为可用后，按所属 state 的待观察项退休健康路径 |
| `WAR_PURSUIT_STALLED`（ACTION 行，`PoliceEnforcementBehavior.WarPursuitTrace.cs`） | 宣战追捕为什么两天还抓不到：已宣战、有灰袍承办的案子每满 24 小时记双方速度、距离变化、战力、罪犯在不在聚落 / 军团 / 战斗 / 围城、双方原版行为、在外的追截队与支援队 | 查清原因、改掉两天支援队保底后退休 |
| 协力与使者：`ASSISTANCE_WAITING_FOR_ENVOY`（组长等使者结果没调人，案子或使者阶段变了才再写）、`ASSISTANCE_HELPER_CASE_KEPT` / `_PAUSED` / `_RESUMED`（协办人带着自己的案子入组、暂停、协力结束接着办）、`ASSISTANCE_MEMBER_LEFT`（协办人约了比武离组）、`ENVOY_REPORT`（每次谈判结果，带是否协办人） | 用户要监控军团效率：协力不再打断有使者的案子、协办人回来接着办（用户裁定 2026-10-16） | 用户确认协力与使者配合可用后退休健康路径 |
| 宗教：`PRIEST_DISAPPEAR_BLOCKED`（原版本要按低势力删掉的祭司）、`PRIEST_HOME_RESTORED`（家乡村补回祭司）；`DAILY_VILLAGE` 的 `others`（其他要人职业:势力，没有祭司时也写） | 用户要监控宗教：家乡村祭司曾被原版删掉，比例不够再也来不了人（实机诊断 2026-10-16） | 用户确认信众村祭司稳定后退休 |
| `GwpAiDiagnostics.WriteFaith`：AI 日志里的 `\| FIRESWORN \|` 行（stage 清单见本页「火誓者监控字段」） | 信众村经济整套待实测：用户试玩暂无明显 bug，传教次数拉平被缺兵村阻塞仍在核查；保留派队、村庄兵力与经济快照用于定位，完整验收范围尚未逐项确认 | 用户确认信众村经济可用后，删健康路径的事件行，每日快照一并删 |
| `WARDEN_KINGDOM_JOIN_BLOCKED` / `WARDEN_BELLUM_EXILE_EXCLUDED` | 新入国动作及内战流亡排除来源待观察，见 kingdom-membership.md | 新入国拦截验收后退休 |
| `WARDEN_AUTO_EQUIPMENT_REJECTED` | 灰袍英雄每个实例最多一条，真实 RBM 候选已离线确认 | 实机进城装备保持确认后退休 |
| 故障观察：`GwpRuntimeFaultWatch` 的 CLR 事件观察、`GwpEngineAssertDiagnostics`；已发生故障的 `DUAL_BLADE_INVALID_ACTION`、`DISPATCH_BARTER_PERSONA_FAILED` | 健康时沉默 | 有依据的失败路径保留 |
| `tools/Capture-StackOverflow.ps1`（用户运行：cdb启动游戏，记录全会话；致命异常先存寄存器、带地址的原生栈和/mip转储，再读SOS及全部线程；栈溢出退出码另在RtlExitUserProcess/NtTerminateProcess入口拦截，正常退出不截停） | 中途无提示退出，异常回调可能缺席；原版与CLR的两条退出入口均需留证 | 闪退定位并修复后连工具退休；新增终止断点尚待真实进程验证 |

案件事件（`GreyWarden-Case-Events.log`）、ACTION、AUCTION、地图战斗与火誓者行仍在役；主队 NPC 代缴的失败路径 `COMPANION_FINE_INCOMPLETE` 保留。其余已退休诊断（仪式声音、每小时部队状态、进聚落意愿对账、送信队 AI、援军与音乐档位、战马伤害、双刀互斥、战场编队采样等）的代码、日志与 WER 均已删除，**不再可读**；退休时间与结论在 journal。需要重新调查时从新复现取证，不要把已退休路径写成仍可用文件。（依据：流程）

## 限流是必需的

挂在每小时心跳或每 tick 上的诊断必须限流，否则会把日志刷爆，并毁掉
`GreyWarden-Faults.log` "健康即空文件" 这条性质。现行做法：

- `GwpFaultTrace.WriteQuiet` **按站点去重，每个位置每局只记一次**
- 容量等待不刷空批次日志
- 战场类采样按秒级间隔，不逐帧

## 不要为了打印而调用原版（依据：C#反编译 v1.4.8，2026-09-25 复核）

`BehaviorScreenedSkirmish` 等评分方法**本身会更新状态**。要读原版评分只能用
postfix 旁观真实返回值，不能主动再调一次。
`NavmeshlessTargetPositionPenalty` getter 会推进处罚计时，同理不要为打印去读。（依据：C#反编译 v1.4.8：getter 会推进计时，满 10 秒时把处罚写回 1；2026-09-25 复核）

军械全兵种分配新增 `ARMORY_PLAN`，仅方案变化时记录持有者、兵种顺序、件数及首套库装；不在每帧/每次出兵打印，用户确认高阶补空位及低阶换装后退休。定义见[军械库](firesworn-armory.md)。（依据：流程、源码复核 2026-10-15）

原版村民队合并的在役健康取证为VILLAGERS_RAISED（本次新增、转化、队内信众前后、当前比例和户数）、VILLAGERS_EQUIPPED（仅灰袍名额替换、领装和留守）、MARKET_DISPATCHED与MARKET_HOME（兼运/分账），均只编入诊断；用户确认比例补员、换兵和结账可用后同轮退休。（依据：流程2026-10-16）
