# 灰袍入国：v14 基线玩家反馈调查

> 当前状态：v14 入国写入入口与内战流亡候选已修复、部署，待用户装齐原模组后实测
> 最后验收：未建立检查点
> 已复核至：`ac66f86`（2026-10-06）
> 覆盖源码：`PoliceAntiRecruitmentModel.cs` `GwpKingdomMembershipPatch.cs` `_Module/ModuleData/spclans.xml` `tools/Verify-KingdomMembership.ps1`
> 待实测：原模组装齐后反馈档能否载入；新档首次日结/周结灰袍是否保持独立；同版存读接续与拦截调用来源

## 工作线

先处理玩家 bug，火誓者开发搁置；本工作树起点为正式 v1.4-r14，暂不带入 main 的火誓者改动。（依据：用户裁定 2026-10-06）

- 分支：`codex/v14-kingdom-join-fix`，基线 `v1.4-r14` / `ac66f86`。
- 工作树：`C:/Users/lucif/.codex/worktrees/v14-kingdom-join-fix/GreyWardenPolicePurity`。
- 暂存的火誓者工作树：`C:/Users/lucif/source/repos/GreyWardenPolicePurity`，main 最新游戏代码存档 `0b63c01`。
- 当前 live 从本工作树部署；两边的 state 结论不能混用。恢复火誓者时由 main 重新构建部署并比对镜像。（依据：流程）
- v14 Client/Editor DLL SHA-256：`0d098ec0f463ea801e01d96553f35aad1f4eb658bc47efdeecc50bc3ad6e76ab`。
- 切换前 runtime 与 DLL 备份：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/pre-v14-live-20261006/`。live 的 Assets、AssetSources、RuntimeDataCache 保留开发资源；客户端实际加载 Assets，代码与运行时数据以 v14 为基线，美术不是 v14 玩家包。用户核对后明确保持开发布局，美术不作为本次入国调查原因。（依据：用户裁定 2026-10-06）
- 入国修复前 DLL/PDB 回退副本：`C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/v14-before-kingdom-fix-20261006/`，修复前 DLL SHA-256：`621d84aa097cbff0194b4b802e71d7818fbc4369d88c5632820640b569802e9f`。

## 反馈存档

- 原件：`C:/Users/lucif/Downloads/saveauto1.sav`。
- 测试副本：`C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Game Saves/GW-kingdom-report-20261006.sav`，2026-10-06 已放好并告知用户。
- 两份 SHA-256：`14be06c799b23d6a9a93c9227bdca77908335e2e989069e251354f11233afda5`。
- 元数据：游戏 `v1.4.8.119303`，灰袍 `v1.4.12.0`（r12）；反馈口头称 r13，两种来源分开记录。角色“李”，第 38.09289 天。
- 原件和副本均未改；用户负责下载第三方模组，下载后核对实际 DLL 再安装。（依据：用户裁定 2026-10-06）

## 存档的模组清单

下列版本来自存档头；中文名来自作者或汉化发布页。FireArrows 仅确认 ID，未确认其具体中文发布包。

| 中文名 / ID | 存档版本 | 当前本机状态（按安装目录与崩溃活动列表核对） |
|---|---|---|
| 家谱 / Bannerlord.FamilyTree | 1.4.8 | 已装同版本 |
| 收获与生产 / HarvestAndProduction | 1.6.17 | 缺少 |
| 内战 / BellumCivile | 1.4.0 | 已装同版本，实际 DLL 已反编译 |
| 誓约亲卫 / Comitatus | 0.8.9 | 已装 0.9.67，版本不同 |
| FireArrows（具体中文包待确认） | 1.0.0 | 缺少 |
| 打击感强化 / HitstopEffects | 1.0.0 | 缺少 |
| 涅墨西斯、劲敌 / LT_Nemesis | 1.6.1 | 已装同版本 |
| 婚姻和生育 / MarriageFertility | 0.6.7 | 已装同版本 |
| RTS 战略视角 / RTSCamera | 5.4.16 | 已装同版本 |
| RTS 战略视角指挥系统 / RTSCamera.CommandSystem | 5.4.16 | 缺少 |
| RBM：真实战斗 / RBM | 4.5.2 | 缺少 |
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
- 只修写入入口，不迁回旧档中已经入国的灰袍，不添加版本间迁移；反馈原件和测试副本未修改。（依据：用户裁定 2026-10-05）

## 反馈档载入崩溃

2026-10-06 22:54 的实际崩溃在原版 CraftingOrder.InitializeCraftingOrderOnLoad。CDB/SOS 确认订单模板为 ThrowingAxe，首零件 axe_craft_26_head 的 IsValid=true，但当前 ThrowingAxe.Pieces 不接纳它。Crafting.CraftedItemGenerationHelper.GenerateCraftedItem 返回 null，订单恢复继续读取空武器的 WeaponDesign（JIT +0x39）而抛 NullReferenceException。

原版投掷斧模板不含该零件，灰袍 XML 只增加两套专用双刀模板；尚未核对存档所用 RBM 的原始模板，不能把具体模板扩展归因于 RBM，也不能判为灰袍存档兼容故障。本次启动未装 RBM、HarvestAndProduction、FireArrows、HitstopEffects、RTSCamera.CommandSystem，Comitatus 用 0.9.67 而非存档 0.8.9；用户已表示马上补装原模组。先恢复原模组环境后核对载入，不为这次报错加入迁移或吞异常补丁。（依据：用户裁定 2026-10-05；实机诊断、C# 反编译、崩溃转储）

取证：`C:/ProgramData/Mount and Blade II Bannerlord/crashes/2026-10-06_11.54.34/dump.dmp`；分析输出在主工作树 `.codex_tmp/v14-load-crash-{managed,order,template,piece}.txt`，调查未闭合，暂留转储。另有内战 DynamicRelationBehavior 在 SyncData 重建关系索引时 Hero.FindFirst 的空 source 异常，以及第三方重复容器注册、RTS 本地化断言；它们不是本次最终崩溃栈。

## 验证与后续

开发诊断与无诊断隔离构建均 0 警告/0 错误；GameCompat PASS、MEMBER_FAIL_COUNT=0、PATCH_OK=70、PATCH_FAIL=0、PREFLIGHT PASS（613 类型）；真实游戏程序集和实际内战 DLL 离线回归 32 断言，无内战 28 断言，全通过。覆盖四种入国入口、大小写 ID、债务/佣兵副作用不变、普通家族原入口与灰袍离国放行、内战候选与可选绑定；不启动游戏。

已部署开发诊断 DLL/PDB 到 Client/Editor；两端与隔离 DLL 哈希一致，live 镜像 66 个仓库文件无缺失/差异/散落。未发布或推送。下一步用户补齐模组后核对反馈档加载，并用新档观察首次日结、周结与同版本存读后灰袍保持独立；已入国的跨版本反馈档用于取证，不自动修复其既有归属。
