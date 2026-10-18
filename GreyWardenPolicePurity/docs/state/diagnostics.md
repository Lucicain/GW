# 诊断系统

> 当前状态：用户已确认本轮游玩正常；协力崩溃探针与内存采样已退休，退出原生故障独立取证
> 最后验收：未建立检查点
> 已复核至：`5b0591f`（2026-10-18）
> 覆盖源码：`GwpAiDiagnostics.cs` `GwpRuntimeFaultWatch.cs` `GwpEngineAssertDiagnostics.cs` `GwpCommon.cs` `tools/Analyze-Firesworn.py` `PoliceRaidDeterrenceModel.cs` `tools/Capture-StackOverflow.ps1`
> 待实测：退出阶段原生释放故障；收养交接等未覆盖项仍随所属功能核对

## 原则

直接招守村兵使用 `GUARD_ENLISTED`，记录实际兵种 composition、人数、随兵军械和留守量；统计器读取同一事件。该功能待用户实测低阶优先与留守正确后退休健康路径记录。（依据：本轮直招功能验证设计 2026-10-15）

诊断是**在建工程的脚手架**，不是模组的永久旁白。每条 trace 都有生命周期：
在某个东西正在建或正在出问题时加上，在那件事被确认之后删掉。

全部诊断在 `#if GWP_DIAGNOSTICS` 内，
纯 trace 的退休只改变开发者日志；通用 `Guard` 已移除，故障观察器不吞业务异常，开发与玩家版执行相同业务。军械库每日汇总及异常统计整段仅在诊断版编译，玩家版不执行这些遍历与字符串构造。（依据：源码复核2026-10-14）

收养驻留新增三个状态转换事件：`ADOPTION_STAY_STARTED_OR_RESUMED`、`ADOPTION_STAY_PAUSED`、`ADOPTION_STAY_FINISHED`，只在开始/恢复、暂停或收尾记录目标村和剩余小时，全部在诊断编译开关内。用于核对赴约/返程不计时及完成释放；用户确认收养交接可用时同轮退休。实现随[收养](village-relief.md)维护。（依据：已复现实机缺陷后的验收诊断 2026-10-17）

## 谁来管

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
（例：`GwpRangedCommandTrace` 扩到双方所有编队后曾改名 `GwpBattleCommandTrace`，现已退休。）

领主普通装备加成的 `COMBAT` 采样字段与退休条件见[lord-combat.md](lord-combat.md)，不使用案件时间戳，竞技场场景也可记录。

## 运兵在役诊断

自主练兵旧排队/驻留/自队经验日志随机制删除。`TRAINING_TRANSPORT_DISPATCHED` 记录出队人货，`DELIVERED` 记录实际交付人数与落脚村；交换/归队日志随机制删除；阶段与退休条件只在 [运兵](warden-training.md) 定义。日志在诊断版编译，用户确认运兵可用时同轮退休健康路径记录。（依据：流程、源码复核 2026-10-15）

## 日志文件

AI快照的 `ordinaryCaseEligible` 当前只调用 `PoliceStats.CanHandleOrdinaryCase`，代表队伍活跃、领主活跃与灰袍归属，不含差事占用、使者约定、可接目标或战力门槛；不能把True当成完整派案资格。完整接案还经过 `GwpWardenDuties` 等所属入口。本次约珥实际被善后占用时该字段仍True。（依据：源码与实机日志 2026-10-17）

负声望赎罪在役阶段记录与退休条件只在[赎罪委派](atonement.md)定义，实测后由agent读取案件日志核对。

游戏 Documents 目录下：

| 文件 | 内容 |
|---|---|
| `GreyWarden-Faults.log` | 故障与静默吞异常留痕，只追加不滚动。**健康的一局里只有 `FAULT_WATCH_ARMED` 一行** |
| `GreyWarden-AI-Diagnostics.log` | AI / 欲望拍卖 / 协力编成 / 地图战斗采样，每局开新文件，旧的滚进归档 |
| `GreyWarden-Case-Events.log` | 案件生命周期事件 |
| `GreyWarden-Diagnostics-Archive` | 滚动归档 |

骑兵伤害调查的 `GreyWarden-Knight-Damage.log`、`GreyWarden-Warhorse-Damage.log`（含 `.previous`）已于 2026-09-25 随诊断退休删除，
不再可读；结论在 journal。

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

2026-10-17用户确认当前游玩未发现问题；新会话约179战役小时、军团解散26步正常返回，未见新增异常。军团分段/导航面探针和每分钟内存采样已退休，并清除其专属历史日志行；故障观察与未覆盖功能诊断仍保留，退出原生释放故障单独调查。（依据：用户确认与实机日志2026-10-17）

已退休的：仪式双声音交接经用户 2026-10-08 确认完全修复，`RITUAL_MAP_SOUND_*` / `RITUAL_MENU_SOUND_*` 两份 Trace、调用与反射探测已删除；共享日志中只清除相应声音行，其他诊断保留。已闭合的声音调查目录已删除，媒体验收与清理状态见 `firesworn-media.md`。
2026-10-08 用户说没有大 bug、很多监控可以退休：每小时部队状态 `HOURLY_STATE` / `DIRECT_ATTACK_STATE` / `RESOLVED` /
`OBSERVED_AUCTION` / `OBSERVED_RESOLVED`、进聚落意愿对账 `DESIRE`（连同 `GwpWardenVisitDesire.Trace.cs` 和 `tools/Analyze-Desire.py`；
3413 个候选读到的原版分全是 0，对账早已失效）、仪式声音 `RITUAL_SOUND_*` / `RITUAL_MAP_SOUND_*`。
案件事件（`GreyWarden-Case-Events.log`）、ACTION、AUCTION、地图战斗、火誓者行仍在役：查宣战追捕要用。送信队的 AI 诊断（2026-10-02 为查卖俘虏折返临时纳入，用户确认修复后退出）；`PLAYER_JUSTICE` 的 `companionCases` 字段与 `COMPANION_FINE_SETTLED`（2026-10-02 用户确认主队 NPC 旧案后删除；失败路径 `COMPANION_FINE_INCOMPLETE` 保留）；援军 `BATTLE_SCENE_OPENING` / `BATTLE_SCENE_SUPPORT` 与音乐档位 `SYNDICATE_BATTLE_DYNAMICS`（2026-09-26 用户确认全部实测项后删除，同时清掉了累积 2.9 MB 的 `GreyWarden-Faults.log` 与 9 月 15–17 日归档）；`GwpWarhorseDamageTrace`（2026-09-25 用户确认骑兵表现后删除代码）；`WARDEN_RESOLVE_BLOCK`（2026-09-25 士气改动时删除）；`GwpArcherContactTrace` 及其两类 Harmony 补丁（双刀互斥验收时删除）；
`GwpBattleCommandTrace` 全套战场采样与 `GwpArcherSwitchObserver`（2026-09-23 踱步调查结案删除，
日志行一并清除，结论见 [`battle-tactics.md`](battle-tactics.md)）。
其日志、WER 与一次性分析输出**不再可读**；需要重新调查时从新复现取证，
不要把已退休路径写成仍可用文件。（依据：流程）

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
