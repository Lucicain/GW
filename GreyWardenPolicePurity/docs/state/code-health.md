# 代码健康

> 当前状态：预览实际使用失效待机缓存已确认，按原版名字重新解析的修复已部署待验收；缓存提前初始化来源仍在取证
> 最后验收：`e16ff95`（异常留痕 + 销毁队伍模板）
> 覆盖源码：`tools/Check-CodeHygiene.py` `tools/Check-StateFreshness.py` `tools/Generate-CodeMap.py` `tools/tests/test_code_map.py` `tools/tests/test_project_checks.py` `GwpRuntimeFaultWatch.cs` `GwpCharacterPreviewDiagnostics.cs` `GwpCharacterPreviewActions.cs` `tools/tests/test_preview_action_cache.py` `tools/tests/test_assistance_recursion.py`
> 待实测：正常打开部队/人物预览，核对实际待机恢复和身体姿态；agent读缓存初始化调用来源；既有兜底退休与退出释放问题仍待核对

已知缺陷：客户端退出阶段曾间歇发生原生释放崩溃，具体对象与根因未明；发行版打包美术布局是否复现尚未验证。2026-10-16 19:29 又出现一次，转储同为 TaleWorlds.Native 偏移 0x74e2b1、在 Managed Interface deleted 之后。（依据：WER、转储与日志2026-09-28、2026-10-16）

## 文档一致性现状

当前规则由所属 state 定义，设定总文档只作世界观与玩法概览。接手顺序和代码定位在根 AGENTS 与 [state 索引](README.md) 维护；CodeGraph 是可重建的源码索引，不是第二份项目记忆。它用准确类型/方法名和文件名时定位较好；泛化英语问题、猜错方法名或过低文件上限可能返回相邻代码、同名桩而遗漏目标正文；动态抽取 C# 的 Python 测试未建立完整调用边，需从所属 state 的验证入口查找。不能单凭它的结果排序认定生产归属或测试缺失。（依据：用户要求跨 agent 接手；本仓库实际查询与流程 2026-10-18）

`Generate-CodeMap.py` 按装饰类的实际作用域提取 Harmony 声明，包含动态 `TargetMethod(s)`、显式接口成员与条件编译内的类。动态目标保持未静态解析，不猜实际绑定；声明集合的回归与运行时加载预检是两项不同证据。`Check-StateFreshness` 仍只检查机械一致性，源码/文档同时 dirty 不证明正文正确。（依据：生成器与专项回归 2026-10-18）

已知缺陷：部分源码注释仍描述自建卖货队、冻结 AI 或纯骑兵追截等旧实现；这些旧注释尚未逐项校正，后续改对应功能时核对。（依据：源码注释与当前实现交叉核对 2026-10-18）

## 人物预览横躺调查

已知缺陷：预览默认待机缓存在真实游戏内为-1，动态同名解析为4008，主角/骑士/步兵/弓手/狂信者实际动作也为-1；外观出生/本地/全局竖轴保持直立，仅有比例缩放。已修复预览引用无效动作这一故障点，谁在动作数据加载前初始化了缓存仍未归因，其他静态动作条目是否受影响也未逐项证实。8月1.5.2撤回Agent/MissionWeapon补丁、9月撤回成长包装曾恢复预览，是历史对照；b242d20的Agent.FadeOut诊断现已移出启动扫描、首次搜捕时延后绑定，但不把此隔离直接称为责任方已证实。（依据：用户截图、03:17至03:18实机诊断、安装版DLL与补丁差异2026-10-28）

安装版CharacterTableau的用户旋转只绕竖轴，预览使用独立AgentVisuals。Framework宿主的单独补丁/完整启动扫描未复现提前解析，不能据此排除真实游戏缓存失效；现场已补齐这部分证据。生产修复GwpCharacterPreviewActions仅在OnGameStart后绑定GetIdleAction，当原版返回负编号时用原版ActionIndexCache.Create重新解析act_inventory_idle_start；有效默认/自定义动作不变，不写原版只读静态字段、模型矩阵或骨骼资源。test_preview_action_cache在实际安装版DLL上做负对照（提前读取得到缓存-1、原版返回-1；装修复后返回动态编号，自定义有效动作保持，重复安装只有一个补丁），不代表原生画面已经验收。（依据：实机诊断、安装版反编译、生产实现与离线负对照2026-10-28）

CHARACTER_PREVIEW取证在诊断版OnGameStart后安装，每外观3条、间隔0.5秒、每进程60条，保留动作和矩阵快照。ACTION_CACHE_EARLY_MISS在本模块入口最先绑定生成桥接类ManagedCallbacks.ScriptingInterfaceOfIMBAnimation.GetActionCodeWithName：实际方法仅UTF8封送/原生委托调用，整类不含ActionIndexCache引用。不能早装MBAnimation同名包装，它的空名分支读取act_none缓存。inventory/taunt/arena三个名字首次负编号各记一次栈；ACTION_CACHE_READY在OnGameStart记缓存/动态编号与早期缺失数，均写既有故障日志。用于定位最初责任方，不据此宣称缓存来源已经查明；确认功能后退休健康路径，归因完成后退休早期取证。（依据：安装版桥接类/包装反编译、独立审查与取证设计2026-10-28）

## 启动补丁与静态初始化

Harmony启动期预编译目标可能触发其引用类的静态初始化；绑定成功不证明初始化未失败。2026-10-20名牌RefreshBinding引用CampaignUIHelper.IssueQuestFlagsValues，文本尚未加载时其静态字段调用GameTexts.FindText抛空引用，之后地图创建持续报类型初始化失败。标识改挂安全属性写入，业务见[玩家案件](player-justice.md#办案目标地图标识)。Invoke-ModuleLoadPreflight在补丁安装期间记录这条已发生的初始化异常，并检查PartyNameplateVM补丁目标对CampaignUIHelper静态字段的直接读取，任一命中即失败；该检查只覆盖这条明确故障依赖，不代表全部静态初始化或原生启动均已模拟。（依据：实机栈、安装版反编译与离线负对照2026-10-20）

2026-10-22 第二例：搜捕拖尸补丁在模组加载时改写原版 `CorpseDraggingMissionLogic.OnMissionTick`，该方法读 `ActionIndexCache`（beforefieldinit 值类型，静态字段按名字向引擎查动作编号）。打补丁当场运行其静态初始化，而引擎在模组初始化结束后才读 `action_types.xml`，整局所有原版动作编号都成 -1；竞技场看台观众按 -1 预载动画，原生越界读崩溃。改为首次进搜捕场景时再打。这例初始化不抛异常，只留下错值，上面的启动预检拦不住。（依据：WER 转储与引擎日志时序 2026-10-22；C++反汇编 v1.4.8 TaleWorlds.Native+0x6FE2B0/+0x5A4050；离线 Harmony 实验，提交 422885c；该补丁随拖运于2026-10-22删除，规则仍成立）

- 不要在模组加载时给读取「按名字向引擎解析的静态缓存」的原版方法打补丁（`ActionIndexCache`、`GameTexts` 之类）：静态初始化会在引擎数据加载前跑完并永久留下错值。这类补丁在首次需要时再打。（依据：上两例 2026-10-20、2026-10-22）

## 双向控件绑定与名称修饰

原版生成的PartyNameplateItem在VM通知后更新TextWidget.Text，Text变化又回写VM.FullName。对属性输入无条件追加标签会破坏原版同值终止条件，使每次回写产生新值、无限通知。办案标识从原版原名缓存生成，业务与验证见[玩家案件](player-justice.md#办案目标地图标识)；不能仅凭没有回写的VM桩断言界面无递归。（依据：2026-10-20实机栈溢出退出码、安装版生成回调负对照）

## 协力职责与战力的无限递归

当前主办职责只指向罪犯，协办只指向主办，不反向寻找协办；职责与日志意图解析不进入区域战力评估。原版区域计算在作用域内采集，实际接战检查复用本次采集，不再次递归评估；区域行为与持续出战守卫只在[协力](case-assistance.md#区域宣战与持续接战)定义和维护。`test_assistance_recursion.py` 编译生产职责/日志方法，以拒绝区域战力重入的边界覆盖；原版计算与采集适配由区域专项回归验证。（依据：源码、生产方法离线执行 2026-10-18）

## 客户端退出阶段的原生故障

2026-09-27 18:58 与 2026-09-28 00:55 两次客户端退出均在日志写出 `Managed Interface deleted` 后，由 `TaleWorlds.Native.dll` 报 `0xc0000005`；Windows 事件偏移分别为 `0x74e0c3`、`0x74e2b1`。两份转储的调用栈落在相同的原生退出释放循环，前者为无效指针写入、后者为无效指针读取；模组故障日志在退出时没有新增托管异常。7 月的转储 `31280` 也落在该原生释放调用链；可确认是旧的退出故障类别复发，尚不能证明损坏内存的是同一对象。同一 `Assets` 布局曾于 19:41 正常退出，因此故障是间歇性的。2026-10-16 23:10 的`GreyWarden-Crash-20261016-225524.dmp`为用户主动关闭时的同类故障：RGL已完成Final_cleanup与Managed Interface deleted，Native+0x74e20a读取无效地址，分配器元数据读出0xFEEEFEEE；小转储不含相关堆页，不能指认某件装备、某支军团或某个原生对象，保留此份现场区分两类故障。

当前 C# 源码没有运行时修改盾牌网格或材质，黑金盾仍引用静态网格；历史上已修复的运行时盾牌变更不能直接当作这次的根因。若继续隔离，应保持本机 live 的开发版布局，在隔离副本中分别对叠加层、无模组客户端及资源发布布局做可重复的退出对照，并记录 Windows 事件；一次正常退出不足以排除间歇故障。（依据：WER、RGL 日志与 cdb 转储 2026-07-20、2026-09-28；用户确认与C++反汇编2026-10-16；源码复核；用户裁定的 live 布局）

## 性能：按频率排查，不按代码量

2026-10-10 对当前 168 个源码文件重新盘点，发现 `MobileParty.All` 的 95 处文字出现（含注释，不等于运行次数）。当前存在嵌套扫描，不能沿用旧调查的「没有 O(n²)、否掉性能优化」结论。

- 火誓者小时/日结各建立本轮部队 ID 快照，`FindParty` 优先字典查找，本轮结束 finally 释放；本轮新建的队伍可走实际全表查找。登记队 F、地图队 P 的反复查找由 F×P 改为建立一次 P 后查 F 次，不引入长期全局队伍缓存。（依据：源码复核 2026-10-15）
- 军械配装保留缓存；库存对象/`ItemRoster.VersionNo`、两种受装人数或预留编制变化才重算，日结前后复核也复用未变计划；`BuildPlan` 在实际变化时仍有逐件展开及 `FindIndex + RemoveAt` 的成本，尚未改数量桶，不能声称消除了所有二次方路径。纯化仍每小时检查资格，名册对象/`TroopRoster.VersionNo` 未变就不扫描兵种；缓存是名册弱引用，不保存工作状态，读档自然重新检查；招兵、转兵、伤兵变化等原版名册写入都会更新版本；战斗中不记录已检查版本，结束后仍会处理。（依据：C#反编译 v1.4.8 ItemRoster、TroopRoster.AddToCountsAtIndex/UpdateVersion；源码复核 2026-10-15）
- 练兵偏好每小时只扫灰袍队，维持玩家订单优先和配比；自动运兵派出只在日结检查。村庄观察和志愿兵格子仍每小时核对，因为原版会改民兵/志愿兵，这次不降低这些业务频率。（依据：源码复核 2026-10-15）
- 旧卡死主线程曾落在火誓者统计/军械链，但受零人队无限增长污染。正常存档各段耗时与改善幅度尚未实测，离线缓存命中不能换算成游戏帧率。原版 v1.4.8 `CampaignObjectManager.Find<T>` 仍是线性查表；附近敌人已经用 `FindNextLocatable` 空间搜索，本轮不替换。后续若仍卡顿，再用实机采样定位真实热点。（依据：实机诊断 2026-10-09；离线执行 2026-10-15；C#反编译）

## 兵员转移与临时队编号

共享规则与原版依据只在 [临时队](temporary-parties.md) 定义。练兵、订单、追截、送信归队与火誓者名册转移统一调用 `GwpTroopTransfer`，各自保留原选兵策略；建队统一调用 `GwpPartyIdentity`。实际 v1.4.8 名册与注册表回归覆盖伤兵、经验余数、多来源回滚及改名后连续建队查重。没有新增存档字段。（依据：源码、C#反编译与实际游戏 DLL 离线执行 2026-10-14）

## 循环可靠性复核

循环按明确进展核对终止依据：协力增员后候选被差事占用排除，补护卫的人数上限递增，配乐队列会出队，位读取会前进或抛出越界，UI 沿父级前进；没有再确认一处与旧巡逻相同的零人数循环。临时队查重不再随机重试，候选后缀每次严格递增，受有限注册表条目数约束。

出队少领装备已修复（见 [军械库](firesworn-armory.md)），直接抽取生产方法、替换引擎边界的回归通过；调兵状态和编号另用实际游戏 DLL 验证。机械卫生全零只证明当前脚本的四种规则，没有证明上述语义和性能。（依据：源码、离线执行与运行时反射 2026-10-10）

## 兜底必须兜住过东西

用户裁定（2026-10-05）：兜底必须真的兜住过东西，没触发过的就是垃圾。没人用的常量、成员同理。

**现状**：无依据的通用 `Guard` 与全部 30 处包装已移除；开发版和玩家版均直接执行业务回调，故障观察器只记录异常，不改变业务失败行为。其余 catch 的机械检查全零，实际依据仍逐站点复核。（依据：源码与日志复核 2026-10-14）

带依据注释的站点包括：
   - 读档时 `Hero.FindFirst` 因英雄全局表未建好抛空引用（`GwpData` 两处）；
   - 自定义战斗里 `Clan.PlayerClan` 空引用（配乐）；
   - 原版 `Army.DisperseInternal` 空引用（协力解散）；
   - 野外执法拉不起战斗；
   - 没装 NavalDLC 时补丁绑定失败（`SubModule`）；
   - 首次异常处理器（CLR 语义：处理器里抛出的异常会顶替正在处理的原异常）。

原有依据来自实机故障日志及 journal。当前范围需逐站点复核，不能把同一个旧档错误当作新回调的触发证据。

**以后**：
- 不写「以防万一」的 catch，让异常暴露，读崩溃、修根因。故障实际发生过，才在那一处加 catch 并写依据。
- `tools/Check-CodeHygiene.py` 检查两条：每个不重新抛出的 catch 都有 `依据：`；每个声明都有人引用。
  预提交钩子遇到违规会拒绝提交，新鲜度检查也会点名。（依据：用户裁定 2026-10-05）
- 删掉兜底的代价：原先被静默跳过的故障，现在会直接抛出。有的在战役心跳里，可能让游戏崩溃。
  这正是要的，崩了就读崩溃报告、修根因。

`GwpFaultTrace.WriteQuiet` 仍按站点去重（每个位置每局一次），调用点由编译器填。

## 双 BOM 陷阱

`GwpBattleReinforcementBehavior.cs`、`GwpBribeBarterable.cs`、
`PoliceAntiWarDeclaration.cs` 曾是**双 BOM** 文件头。第二个 BOM 会被当成正文，
使第一行实际是 `﻿using System;`——按行匹配的工具（包括自写脚本）会因此失配。
已归一化为单 BOM。

**写脚本处理源码时用 `utf-8-sig` 解码，并留意这个历史缺陷。**

## 有意不做的重构

14 个超过 120 行的方法（最长 `TrySpawnImmediateCaseInterceptor` 261 行、
`UpdateTasks` 221 行、`OnSessionLaunched` 211 行）**没有拆**。

收益是主观的，风险是实在的：这些方法早退分支密集，拆错一条就是行为变化。
**等到确实要改那块功能时顺手拆**，不要为了好看单独动它。（依据：设计）

## 联机适配

已停工并归档到 `coop-bridge` 分支。Coop 官方 mod 支持在其路线图 V3.0（0/2 未动）。
实测停在两处：「玩家可见文本发去了无人的服务器窗口」和「野外抓捕瞄不到玩家」。
剩下的不是补丁量，而是要重做整个玩家可见层。`main` 不带联机代码。

## 共享概念的归属

同一个判断写两份迟早分叉（2026-10-07 查出：战力三种算法、「是不是灰袍」8 份私有副本、案件状态 11 个文件都能改、
8 份手抄的差事避让清单；修补都只修了出问题的那一份）。现在每个共享概念只有一个归属方：

| 概念 | 归属 | 别处直接用就会被拦的写法 |
|---|---|---|
| 搜捕单挑路线与大厅占用 | `GwpCitySearchBehavior.UsesHallDuel` / `HallDuelLocked` | 按真实竞技场场景名决定；菜单、走门、Tab与存读档共用入口，规则归[搜捕](city-search.md) |
| 搜捕敌方追击 | `GwpCitySearchMissionController.SetCityPursuer` | 初始守方与新援军共用原版下马兵种编队、冲锋及自动选敌，不改我方编队与血量归属 |
| 人物外观原生渲染修复 | `GwpCitySearchNativeVisualFix` | 对已查明故障RVA的修复只在此处安装；签名、生命周期及验收归[搜捕](city-search.md) |
| 和平投案退团、押人与原队收尾 | `GwpVoluntarySurrender` | 玩家当面投案与使者立即/战后投案均用此入口；规则归[使者](case-envoy.md#投案与原版军团退队衔接) |
| 兵员人数、伤兵与经验搬运 | `GwpTroopTransfer` | 各功能选兵后调用公共搬运入口；语义由实际游戏名册回归检查，不声称正则覆盖所有扣人加人代码 |
| 玩家词句与中英变量 | `PlayerText/strings.json`、`Generate-PlayerText.py` | 构建拒绝新内联带键整句与陈旧生成文件；详见[玩家文字](player-text.md) |
| 临时队海陆交接资格 | `GwpCommon.CanCompletePartyHandover` | 海陆相同判断只在公共入口实现；距离、接收人资格归各业务，规则见[临时队](temporary-parties.md#海陆交接资格) |
| 临时队唯一编号 | `GwpPartyIdentity` | `FindNextUniqueStringId<MobileParty>`；查重与严格递增在本入口完成 |
| 玩家为本案宣战（求援成立、进城搜捕动手） | `PlayerBountyBehavior.DeclarePlayerCaseWar` | `DeclareWarAction.Apply*`；宣战同时登记调停申请通道 |
| 讲和（含玩家战斗时序、灰袍交战理由） | `GwpPeace` | `FactionManager.SetNeutral`、`MakePeaceAction.Apply`；规则见 [调停](warden-mediation.md) |
| 战场完整名册、预备兵与增援通知 | `IGwpBattleSceneSource`、`GwpBattleSceneContext` | 援军与音乐只查此入口；野战转接原供应器，城内由搜捕提供真实名册 |
| 无战役伤亡回调的场景人物来源 | `GreyWardenSafePartyAgentOrigin` | 切磋、搜捕守军与提取身体表现复用此来源；玩家随行兵仍用原版PartyAgentOrigin结算伤亡 |
| 大地图战力 | `GwpStrength` | `EstimatedStrength`、`GetPowerOfParty`、`GetTroopPower`、`GetTotalLandStrength*` |
| 玩家搜捕单案机会 | `GwpCitySearchCasePolicy` | CrimeRecord保存玩家亲自/协同次数，仅在此读取/消耗；重新接委托不重置，NPC派队不占次数 |
| 搜捕街道伤亡快照 | `GwpCitySearchBehavior.SaveStreetBodies` / `StreetBodies` | 死伤外观/位置/结果仅经此保存读取；不跨Mission持有Agent引用 |
| 搜捕现场模拟攻防 | `GwpCitySearchMissionController.CombatBalance` / `SimulationPower` | `GetSimulationAttackPower`；现场说服与打法选择共用，范围见[搜捕](city-search.md) |
| 谁算灰袍 | `GwpCommon.IsGreyWarden*` | 家族 ID `PoliceClanId` |
| 信众村民队的登记与生命周期介入 | `GwpFireswornBehavior.IsFaithVillager`、`GwpFaithVillagers.cs` | 别处给VillagerCampaignBehavior或SellGoodsForTradeAction加补丁；军械、装货、分账统一查询登记入口。规则归[村民队](firesworn-villagers.md) |
| 案件状态 | `PoliceEnforcementBehavior.CaseLifecycle.cs` | `CrimePool`/`CrimeState` 的 `EndTask`、`ReleaseTask`、`CloseCase`、`ReopenCase` |
| 玩家承办权限、占案与目标预留 | `GwpPlayerCaseAssignment` | 其他调用方直查普通悬赏或赎罪各自的权限/持案方法；共享规则与专项回归见[赎罪](atonement.md) |
| 差事占用 | `GwpWardenDuties` | 其他系统的占用判断（`IsVillageReliefParty` 等） |
| 守村经验分配与容量 | `GwpFireswornBehavior.GrantGuardExperience`（`GwpFireswornExperience.cs`） | `CanTroopGainXp`；烧人经验调用这一入口 |
| 守村灰袍升级付款人 | `GwpFireswornUpgradePayment` | 升级模型只选择当前兵种，原版Owner钩子只查询这一处；公库钱包仍由 `PoliceResourceManager` 定义 |
| 灰袍领主进聚落的意愿（候选、招兵数、打分、各项改动的来由） | `GwpWardenVisitDesire` | 别处对 `AiVisitSettlementBehavior` 打补丁 |
| 灰袍巡区（哪位领主守哪座城、信众村按巡区分散） | `GwpPatrolBeats` | 别处自己按文化挑城、排领主序号 |

`tools/Check-CodeHygiene.py` 机械检查这张表，并找函数体完全相同的两个方法（重写的引擎事件除外）；
预提交钩子遇到就拒绝提交。出现新的共享概念时，给它一个归属，在脚本的 `OWNERS` 里加一行。（依据：用户裁定 2026-10-07）

## 接手与机械检查

- CodeGraph 先定位源码与调用链，state 决定当前规则，测试和原版取证确认实际行为；动态图中没有边或测试时，仍需核对真实入口，不据此删代码。`python -m unittest discover -s tools/tests -p test_project_checks.py -v` 回归检查战力归属漏检、引用失效核对、提示与硬闸的区别；机械全零不代表正文语义正确，交接时仍需对照本次改到的源码。（依据：流程）
- 火誓者按村庄、仪式、媒体、军械库、经济和队伍拆分，入口在 [firesworn.md](firesworn.md)。文档保留当前规则与验收项，旧说法只在 journal 追溯。
- `Check-StateFreshness.py` 提示 state 正文引用了已不存在的文件、类型或成员；不会新增提交硬闸。改动牵连到哪些规则由 `Check-ChangeImpact.py` 点名，流程见 [workflow](workflow.md#改动流程的细节)。
