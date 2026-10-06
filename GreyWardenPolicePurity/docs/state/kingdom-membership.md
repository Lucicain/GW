# 灰袍入国：v14 基线玩家反馈调查

> 当前状态：用户确认读档离国可用，正常退出诊断已退休；装备候选保护见 warden-gear.md，补兵预算按用户裁定暂缓
> 最后验收：`9dc76be`（2026-10-07 用户确认读档退出可用）
> 已复核至：`9dc76be`（2026-10-07）
> 覆盖源码：`PoliceAntiRecruitmentModel.cs` `PoliceAntiVanillaWarBehavior.cs` `GwpKingdomMembershipPatch.cs` `_Module/ModuleData/spclans.xml` `tools/Verify-KingdomMembership.ps1`
> 待实测：另存读档后保持独立、案件继续及新入国拦截；装备候选保护见 warden-gear.md；补兵预算按用户裁定暂缓

## 工作线

先处理玩家 bug，火誓者开发搁置；本工作树起点为正式 v1.4-r14，暂不带入 main 的火誓者改动。（依据：用户裁定 2026-10-06）

- 分支：`codex/v14-kingdom-join-fix`，基线 `v1.4-r14` / `ac66f86`。
- 工作树：`C:/Users/lucif/.codex/worktrees/v14-kingdom-join-fix/GreyWardenPolicePurity`。
- 暂存的火誓者工作树：`C:/Users/lucif/source/repos/GreyWardenPolicePurity`，main 最新游戏代码存档 `0b63c01`。
- 当前 live 从本工作树部署；两边的 state 结论不能混用。恢复火誓者时由 main 重新构建部署并比对镜像。（依据：流程）
- v14 Client/Editor DLL SHA-256：`babde02eef0b5ebcfc26d9d4aac86e04e743804e7e268fbbd5dbc50d8dda1c2b`。
- 切换前 runtime 与 DLL 备份：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/pre-v14-live-20261006/`。live 的 Assets、AssetSources、RuntimeDataCache 保留开发资源；客户端实际加载 Assets，代码与运行时数据以 v14 为基线，美术不是 v14 玩家包。用户核对后明确保持开发布局，美术不作为本次入国调查原因。（依据：用户裁定 2026-10-06）
- 入国修复前 DLL/PDB 回退副本：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/v14-before-kingdom-fix-20261006/`，修复前 DLL SHA-256：`621d84aa097cbff0194b4b802e71d7818fbc4369d88c5632820640b569802e9f`。

## 反馈存档

- 原件：`C:/Users/lucif/Downloads/saveauto1.sav`。
- 测试副本：`C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Game Saves/GW-kingdom-report-20261006.sav`，2026-10-06 已放好并告知用户。
- 原件及导入时副本 SHA-256：`14be06c799b23d6a9a93c9227bdca77908335e2e989069e251354f11233afda5`。用户游玩后于 2026-10-06 23:20:10 保存测试副本；该次副本 SHA-256 为 `cdb4c87004ec30298ad5547ee2438eb139ac004581ab860ff010204741fac349`。最新保存为 2026-10-07 00:37:15，SHA-256 `1861c61b69900ef714e2286d80c94991254f3d050671aac280b1512f4744ee4a`，头部灰袍版本 r14、第 50.83261 天。
- 元数据：游戏 `v1.4.8.119303`，灰袍 `v1.4.12.0`（r12）；反馈口头称 r13，两种来源分开记录。角色“李”，第 38.09289 天。
- agent 未改原件或测试副本；副本包含用户游玩后的保存。用户负责下载第三方模组，下载后核对实际 DLL 再安装。（依据：用户裁定 2026-10-06）

## 存档的模组清单

下列版本来自存档头；中文名来自作者或汉化发布页。FireArrows 仅确认 ID，未确认其具体中文发布包。

| 中文名 / ID | 存档版本 | 当前本机状态（按安装目录与崩溃活动列表核对） |
|---|---|---|
| 家谱 / Bannerlord.FamilyTree | 1.4.8 | 已装同版本 |
| 收获与生产 / HarvestAndProduction | 1.6.17 | 已装同版本，studfarm 定义与 DLL 已核对 |
| 内战 / BellumCivile | 1.4.0 | 已装同版本，实际 DLL 已反编译 |
| 誓约亲卫 / Comitatus | 0.8.9 | 已装 0.9.67，版本不同 |
| FireArrows（具体中文包待确认） | 1.0.0 | 缺少 |
| 打击感强化 / HitstopEffects | 1.0.0 | 缺少 |
| 涅墨西斯、劲敌 / LT_Nemesis | 1.6.1 | 已装同版本 |
| 婚姻和生育 / MarriageFertility | 0.6.7 | 已装同版本 |
| RTS 战略视角 / RTSCamera | 5.4.16 | 已装同版本 |
| RTS 战略视角指挥系统 / RTSCamera.CommandSystem | 5.4.16 | 已装同版本 |
| RBM：真实战斗 / RBM | 4.5.2 | 已装 4.5.4，版本不同 |
| 女性头模 / xxFemaleHead | e1.0.1 | 相同 |
| Harmony | 2.4.2.248 | 相同 |
| ButterLib | 2.11.1 | 本机 2.12.0 |
| UIExtenderEx | 2.13.3 | 本机 3.0.1 |
| MCM / Bannerlord.MBOptionScreen | 5.12.2 | 本机 5.12.3 |

另有原版 Native、SandBoxCore、Sandbox、StoryMode、CustomBattle。存档没有外交模组 Bannerlord.Diplomacy；本机虽已安装，不能据此算入反馈环境。

中文来源：[家谱](https://bbs.mountblade.com.cn/download_2253.html)、[收获与生产](https://bbs.mountblade.com.cn/download_2366.html)、[内战](https://bbs.mountblade.com.cn/download_2350.html)、[誓约亲卫](https://bbs.mountblade.com.cn/thread-2116620-1-1.html)、[打击感强化](https://bbs.mountblade.com.cn/download_2709.html)、[涅墨西斯](https://bbs.mountblade.com.cn/download_2043.html)、[劲敌](https://www.nexusmods.com/mountandblade2bannerlord/mods/12865)、[婚姻和生育](https://bbs.mountblade.com.cn/download_1423.html)、[RTS 名称](https://bbs.mountblade.com.cn/forum.php?mod=viewthread&tid=2116735)、[RBM](https://bbs.mountblade.com.cn/thread-2116708-1-1.html)。页面可能有更新版，本次环境以存档版本为记录。

## 入国修复

- 现有外交模型只挡评分；原版 1.4.8 ChangeKingdomAction 入国动作没有二次查询模型。灰袍 XML 为无国、无城镇城堡的贵族家族，符合内战流亡收容候选。依据：C# 反编译与模块 XML。
- 本机 BellumCivile 1.4.0 实际 DLL SHA-256：`3b88f28550298863b922e73bdfe1b4344b381546cd339784ee14dfbbfb8b9f29`。反编译确认 IsRecoverableExileCandidate 未排除灰袍，首次 OnDailyTick 与后续周结收容直接调用 ApplyByJoinToKingdom；与公开源码调查一致。尚待实机确定反馈档中已发生的入国是否来自此调用。
- GwpKingdomMembershipPatch 在普通加入、叛国加入、佣兵加入、建国四个公开入口执行前拦截 gw 家族的非空目标王国，债务、阵营关系、佣兵状态与事件都不写入；普通家族与灰袍离国入口保留原版行为。叛国入口在内部动作后还发 OnClanDefected，因此不能只跳过 ApplyInternal。（依据：C# 反编译、离线回归）
- GwpBellumExileCandidatePatch 在实际内战候选入口排除 gw，避免选收容国、询问玩家和虚假收容通知；通过反射可选挂钩，没有 DLL 依赖，未装内战时跳过。若内战程序集在灰袍挂补丁后才加载，候选补丁无法绑定，但四个原版入国入口仍受保护。（依据：设计）
- GWP_DIAGNOSTICS 内暂留 WARDEN_BELLUM_EXILE_EXCLUDED / WARDEN_KINGDOM_JOIN_BLOCKED，记录目标与实际调用栈。用户实测确认后退休这两条诊断。（依据：流程）
- 灰袍王国归属只在读档完成时统一检查一次，有归属则走原版退出流程；不按存档身份做特例，不增加两日、日结、周结或心跳轮询。（依据：用户裁定 2026-10-06）
- 读档检查位于已注册的 PoliceAntiVanillaWarBehavior.OnGameLoadFinished；该阶段原版对象、阵营、部队缓存已恢复。普通成员调用 ApplyByLeaveKingdom，佣兵调用 ApplyByLeaveKingdomAsMercenary，通知关闭。原版负责王国成员/英雄/部队缓存、军团脱离、债务/留国期限与佣兵状态、和平及事件；普通离国归还真实封地，不伪造原因绕过收尾。已经独立或无灰袍家族时不调用退出。
- 两日检查原本只做执法支援与无正当理由战争清理（PoliceEnforcementBehavior.DelayPatrols），从未包含离国；本修复不改变这两项职责。（依据：源码核对）
- 用户确认读档退出可用；00:17:56、00:22:39 两次都从 empire_s 退出到 none，佣兵 false、封地 0，军团关联 4→0，后续灰袍日志 mapFaction=gw。正常退出的 STARTED / FINISHED trace 同轮退休，四条既有记录清理；入国来源 trace 未验收，仍在役。（依据：用户确认、实机诊断 2026-10-07）
- 本次读档修复前 DLL/PDB 回退：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/v14-before-load-independence-20261006/`；SHA-256 `0d098ec0f463ea801e01d96553f35aad1f4eb658bc47efdeecc50bc3ad6e76ab`。

- 诊断退休前 DLL/PDB 备份：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/v14-before-exit-trace-retirement-20261007/`；SHA-256 `80d7a36658cc8122703cf6d625bb4ce7b68c3e983e68c2b80b6de5e5701fc3ad`。正常退出 trace 与四条已验收记录已清理，其余共享日志保留。

## 同一外交行为中的宣战监听

PoliceAntiVanillaWarBehavior 还监听 WarDeclared。只识别灰袍家族对象或 ID 为 gw 的一方；对方若为玩家家族当前 MapFaction，则保留原行为。其他对手只有 GwpPoliceWarReasonService 判定有正当战争理由时保留战争，无理由则调用 GwpCommon.TrySetNeutral。该宣战处理不包含入国或退出动作，也不把灰袍所属王国误当灰袍家族。

SyncData 无状态：本行为只响应宣战和读档完成事件，没有进行中的工作需要单独保存；归属、佣兵、军团及外交状态由原版存档负责。

## 反馈档载入崩溃

最新 23:12 载入崩溃已越过锻造订单，最终异常为原版 Workshop.AfterLoad 的 NullReferenceException。CDB/SOS：工坊 WorkshopType.StringId=`studfarm`，IsReady=false、IsRegistered=false、_productions=null；原版 +0x17 读取 WorkshopType.Productions.Count 崩溃。不是工坊对象或生产进度数组为空，而是存档引用的类型没有当前 XML 定义。（依据：崩溃转储、C# 反编译）

收获与生产主体 1.6.17 已安装到 `D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/HarvestAndProduction/`。SubModule.xml 版本、客户端 DLL 均已核对；`ModuleData/sandbox_spworkshops/spworkshops.xml` 明确定义 studfarm 和两项 Production，SubModule.xml 将 WorkshopTypes 注册到 Campaign / CampaignStoryMode，直接确认此前缺失类型的来源。用户已确认进游戏；23:19:13 的 AI 会话及随后真实灰袍部队行动、案件事件日志确认战役恢复运行，种马场载入阻塞已解除。

不在灰袍添加种马场假定义或吞读档异常，按原模组定义恢复测试环境。（依据：设计；用户裁定 2026-10-05）本机 FireArrows、HitstopEffects 也仍缺；RBM 已装 4.5.4（存档 4.5.2），RTS 指挥系统已装 5.4.16；Comitatus 为 0.9.67（存档 0.8.9）。用户负责下载，agent 本轮只读核对安装，没有下载第三方包或修改运行时模块。

此前投掷斧崩溃取证确认 ThrowingAxe 模板不接纳 axe_craft_26_head，生成武器返回 null，原版订单恢复解引用空武器；灰袍只增加两套双刀模板。安装 RBM 后最新启动未再出现这条最终崩溃，具体模板扩展来源尚未读取 RBM 对应实现，不能据此判为灰袍旧档迁移问题。

锻造、种马场两个载入调查已闭合，原转储目录已不在；结论已落 state，本轮退休其七份 CDB 输出。共享 AI/Faults/Case 日志仍用于入国与执法调查，不删除在役内容。另有内战 SyncData 重建关系索引、第三方容器注册、RTS 本地化与 HarvestAndProduction 探测海战特长的 first-chance 异常；本轮游戏成功运行并正常退出，不能据这些 first-chance 行判定新的致命崩溃。

## 反馈档已有的归属

用户确认进游戏并看到灰袍加入其他国家；AI 日志最早的灰袍采样（23:19:26）中 gw_leader_2_party_1 / gw_leader_4_party_1 的 mapFaction=empire_s，确认是南帝国。当前会话没有 WARDEN_KINGDOM_JOIN_BLOCKED / WARDEN_BELLUM_EXILE_EXCLUDED 或新补丁绑定失败记录；目前证据更符合载入了既有归属，尚不构成修复后新入国的证据。

原版 Clan._kingdom 有 SaveableField(57)，读档恢复既有王国不会走四个入国动作，因此只拦写入不能纠正这类存档。当前统一在读档完成事件走原版离国；不读取旧格式、不检查模组版本、不添加迁移标记或存档身份特例。原件及测试副本均没有被 agent 直接编辑，也不自动覆盖保存；载入后的状态由用户另存确认。（依据：用户裁定 2026-10-06；C# 反编译、实机诊断）

## 验证与后续

开发诊断与无诊断隔离构建均 0 警告/0 错误；GameCompat PASS、MEMBER_FAIL_COUNT=0、PATCH_OK=71、PATCH_FAIL=0、PREFLIGHT PASS（614 类型；自动装备候选补丁新增 1 个类型）。真实游戏程序集和实际内战 DLL 离线回归 43 断言，无内战 39 断言，全通过。原有四类入国拦截与普通家族放行仍覆盖；新增真实 CampaignEvents 注册/派发测试，验证无家族/独立时跳过、普通/佣兵退出入口、重复事件不重复退出、日结/周结不做归属查询。离国动作边界用桩替代，实机确认南帝国成员退出、军团关联 4→0；佣兵与真实封地分支没有本次样本，未启动游戏。

已部署开发诊断 DLL/PDB 到 Client/Editor；两端与隔离 DLL 哈希一致，live 镜像 66 个仓库文件无缺失/差异/散落。四份修复源码按 main 的现行卫生检查审计，0 个 catch 缺依据、0 个声明无人引用。本轮整合进原 v1.4-r14 发布候选，远端替换进行中。另存为新名字再读，核对仍独立且原案件能继续；用户已保存至第 50.83261 天，但尚无该次保存后再次加载的日志验收。
