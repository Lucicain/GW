# 构建、校验与部署

> 当前状态：搜捕分阶段增援、身体提取、大厅站位及两段赴约已部署；112类补丁加载、运行时镜像及Client/Editor哈希通过，待实机验收
> 最后验收：`f00ca9b`（2026-10-17 用户确认同业务开发构建；正式包经离线/远端校验）
> 覆盖源码：`GreyWardenPolicePurity.csproj` `tools/*.ps1` `tools/New-PlayerPackage.py`
> 待实测：辅助模组 Better Time 的4/10/30倍地图快进、Useful Skips一键新档；游戏由用户启动

## 当前开发部署

当前 live 是含体验型搜捕、分阶段城防增援、双方伤亡分账、目标原版拖运提取、大厅分散站位及两段正常赴约的诊断版开发构建，版本号仍1.4.15.0，2026-10-22构建部署。Client/Editor DLL SHA-256同为 `07BE0F7938007C50D7A4235EA5E82AE5B0B58390462B345D223616F17A3D68FD`；75项运行时镜像无缺失/差异/散落，112类补丁及637类型/1951成员引用通过；诊断版与隔离玩家版构建均0警告0错误。17项搜捕专项（生产方法/原版对照及旧逻辑负对照）、144项共享战场、155项案件结算、62项讲和及文本/机械检查通过。新增搬运/赴约诊断事件双编码不进入玩家DLL；实际导航与布娃娃牵引效果仍待用户验收。（依据：构建、离线测试、部署与哈希核对2026-10-22）

## 开发测试辅助模组

用户希望安装现成辅助模组，让观察 AI、开新档和布置测试条件更方便；本次不修改灰袍或第三方模组代码，旧模组也可作为功能参考。（依据：用户裁定 2026-10-18）

| 候选 | 用途 | 当前核查 |
|---|---|---|
| [Better Time](https://www.nexusmods.com/mountandblade2bannerlord/mods/2849?tab=files) | 地图加速，三档默认 4/8/16，MCM 可设 1～128 倍 | 已安装1.1.3（Nexus file 69933），配置4/10/30倍；作者声明已测1.3/1.4/1.5 |
| [Useful Skips](https://www.nexusmods.com/mountandblade2bannerlord/mods/4896?tab=files) | 跳开场、介绍、角色创建和教程，一键进新战役 | 已安装1.0.2（Nexus file 50825，标明1.4.x）；四个跳过开关均true。1.0.3面向1.5.x，未装 |
| [GameMaster](https://steamcommunity.com/sharedfiles/filedetails/?id=3624152079)＋[Commander](https://steamcommunity.com/sharedfiles/filedetails/?id=3670027574) | 控制台＋菜单建立人物/家族/部队，调整聚落，快速布置场景 | 有配套 1.4.x 版本；可选，尚未安装或验收 |
| [RTS Camera](https://github.com/lzh-mb-mod/RTSCamera) | 战场自由视角、暂停、慢动作 | 本机已装 RTSCamera 与 CommandSystem；不凭目录认定本局启用 |
| [Super Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=2875225186) | 战场快进 | 作者说明 RBM 已集成，本机已有 RBM，先利用现有功能 |

启动器 `C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml` 已勾选两模组及Harmony/UIExtenderEx/ButterLib/MCM必需前置，保留其他模组选择；原文件已有两份前置记录（Modules与工坊并存），未另行整理。MCM配置在同级 `ModSettings/Global/BetterTime/BetterTime.json` 和 `ModSettings/Global/UsefulSkips/UsefulSkips.json`；Better Time的3键/原快进4倍、4键/额外按钮10倍、按住Ctrl+空格30倍。

安装清单与原配置备份：`C:/Users/lucif/Downloads/Bannerlord-DeveloperTools-20261018/`（`installed-files.sha256.json`、`LauncherData.before.xml`、`UsefulSkips.before.json`）。卸载可删两新模块目录、恢复这两配置备份，BetterTime配置为本次新建；不涉及灰袍源码读档。原7z保留在Downloads。本轮未启动游戏，待用户确认一键新档及地图快进。（依据：安装与配置核查2026-10-18）

本机 Native 声明 v1.4.8。原版 `Campaign.SpeedUpMultiplier` 默认 4；作弊模式控制台 `campaign.set_campaign_speed_multiplier 10` 可设快进倍率，普通模式上限 15、开发模式 30；倍率不保证实际吞吐。官方 `FastMode` 模块设置 `CampaignOptions.AccelerationMode`，不能据名称当作地图快进。（依据：本机模块与 C#反编译 2026-10-18）

## 两个开关决定一切

`GreyWardenPolicePurity.csproj`：

| 属性 | 默认 | 含义 |
|---|---|---|
| `GwpDiagnosticsEnabled` | `true` | 定义 `GWP_DIAGNOSTICS`，所有诊断随之编入 |
| `DeployToLiveModule` | `true` | 输出直接写进 live 测试模块 |

- 默认（两者 true）= 普通开发构建，DLL 落到
  `<GameFolder>\Modules\GreyWarden\bin\<GameBinariesFolder>\`。
- `DeployToLiveModule=false` 时输出隔离到 `bin\<Configuration>\isolated\`；
  命令行显式 `OutputPath` 优先。
- `GameFolder` = `D:\steam\steamapps\common\Mount & Blade II Bannerlord`。
- `<Version>` 是模组修订号（`1.4.12` ↔ 正式 `v1.4-r12`），**不是 Bannerlord 版本
  依赖闸门**。

### 游戏世代检测

`GwpDetectTargetGame` 从引用程序集检测，不手工设置。1.5.0 改了
`AgentApplyDamageModel.CalculatePassiveAttackDamage` 的签名
（`(BasicCharacterObject, in AttackCollisionData, float)` →
`(in AttackInformation, in AttackCollisionData, float)`），单个程序集无法同时满足，
因此**每个游戏世代各构建一次包**。

## 校验脚本（`tools/`）

| 脚本 | 作用 | 通过标准 |
|---|---|---|
| `Invoke-ModuleLoadPreflight.ps1` | 加载安装版依赖并检查类型/补丁绑定、启动名牌补丁的界面静态字段依赖与实际初始化异常 | 类型/成员无缺失，补丁全部成功；EARLY_UI_FIELD_FAIL_COUNT与STATIC_INIT_FAIL_COUNT均0；数量变化有解释 |
| `Verify-GameCompat.ps1` | 检查 DLL 与当前安装的 Bannerlord 是否兼容，防止换游戏版本后静默产出游戏拒绝加载的模组 | `GAME COMPAT: PASS`、`MEMBER_FAIL_COUNT=0`、`PATCH_FAIL=0` |
| `Verify-LiveModule.ps1` | **双向**比对开发 runtime，检查开发布局和双端监控 DLL | 无缺失/差异/散落；三类可编辑目录非空；无客户端/服务器打包目录；Client/Editor DLL 哈希一致且五组监控串存在；两份玩家 README 一致 |
| `Verify-ContentKeys.ps1` | XML / 字面量 / 生成的本地化键，只读 | `MISSING=0`。未被引用的字符串**不是死内容的证据，永不删除** |
| `Verify-ClanBanner.ps1` | 新旗帜的安装版 XSD、实际 Banner DLL 与保留图标离线检查；`-Installed` 另核对家族引用、注册和 XML 镜像；状态归 [旗帜](clan-banner.md) | XSD/ID/managed Banner/installed XML PASS；不代表引擎渲染通过 |
| `Verify-CrimeReceipts.ps1` | 犯罪回执检查，只读，不打开或修改存档 | PASS |
| `Watch-GreyWardenAI.ps1` | 实机 AI 诊断日志跟看 | — |
| `New-PlayerPackage.py` | 从隔离玩家DLL和_Module生成本地ZIP及逐项manifest，ASCII/UTF-16检查无监控、逐项解压哈希 | `PLAYER PACKAGE: PASS`；不自动上传 |
| `Generate-CodeMap.py` | 从源码重建 `docs/state/code-map.md`；PostToolUse 钩子自动跑 | 不要手改产物（依据：流程） |
| `Check-StateFreshness.py` | 报 state 引用失效的文件与符号、源码归属、state 堆积、残留的已取消头部与遗留物；生成进度汇总 | 被点名的当轮处理 |
| `Check-CodeHygiene.py` | catch 依据、无人引用声明、绕过共享归属、重复函数体；预提交硬闸 | 四项全零 |
| `Analyze-Firesworn.py` | 只读：汇总最近一次启动游戏以来的火誓者监控行，主动列出平衡问题（见 `diagnostics.md`「火誓者监控字段」） | 不是闸门，读结果 |

`Invoke-ModuleLoadPreflight.ps1` 只能用 Windows PowerShell 5.1：它在 .NET Framework 里加载游戏程序集，pwsh 下 Harmony 撞上已移除的 BinaryFormatter，68 个补丁全部报失败（2026-10-05 实测）。`Verify-GameCompat.ps1` 与 `Verify-ContentKeys.ps1` 只能用 PowerShell 7（pwsh）——前者需要 System.Reflection.Metadata，后者在 5.1 下读 XML 报解析错误（2026-09-26 实测）。`Verify-LiveModule.ps1`、`Verify-CrimeReceipts.ps1` 两边都能跑。两个 `.py` 检查用 python。

## 测试工程（`tools/`）

| 工程 | 数量 | 覆盖 |
|---|---|---|
| `tests/test_assistance_recursion.py` | 10场景 | 编译生产协力职责与日志意图入口，以拒绝区域评估重入的边界覆盖附近支援、挂靠/脱离、强主办与速度分散 |
| `tests/test_code_map.py` | 3项 | 动态目标、显式接口名、组合属性、条件编译、类作用域及当前仓库补丁声明集合完整性；不替代运行时绑定 |
| `tests/test_native_regional_strength.py`、`test_regional_threat.py` | 原版对照28、采集适配40断言 | 原版区域/最终欲望生产方法及实际 Harmony 采集、预览外交作用域与还原、既有强敌规避、避免重复原版评分；参见[协力](case-assistance.md#原版对照与回归范围) |
| `tests/test_resident_counterattack.py` | 23断言 | 原版双方initiative/移动与目标离城申请联动，未手设目标出城；原生地图/引擎动作提交仍未覆盖 |
| `tests/test_pursuit_city_behavior.py` | 63断言 | 完整原版移动/接触方法对照；initiative及导航有效性为输入，不用于证明目标会自主留城或离城 |
| `tests/test_native_interceptor_contact.py` | 8断言 | 当前生产追截直攻命令与新反编译完整接触入口：普通驻城不逐出、离城恢复接触、交互距离、已有同阵营战斗例外及行动方驻城门槛 |
| `tests/test_case_interceptors.py` | 35断言 | 生产追截抽兵、混编与人数、补派时机及抽兵后复核失败回滚；规则见[追截](case-interceptors.md) |
| `tests/test_case_dispatch.py` | 生产派单回归 | 职业优先与就近排序、取消信众村远单优先；规则见[案件](case-enforcement.md#自动接案的实际顺序) |
| `Verify-EnvoyNavigation.ps1` | 270断言 | 实际游戏 DLL 与生产临时队船只/导航时序；边界与诊断结论见[临时队](temporary-parties.md) |
| `tests/test_player_text.py` | 4 | 中英输出、只读陈旧检查、新键生成、换行等价、占位符丢失拒绝及新增内联文本拒绝 |
| `tests/test_project_checks.py` | 14 | 战力/讲和/守村经验容量/玩家委派/海陆交接归属、引用失效核对、严格模式保留原硬闸；运行 `python -m unittest discover -s tools/tests -p test_project_checks.py -v` |
| `tests/test_atonement.py` | 42断言 | 抽取实际赎罪、委派入口与使者方法，覆盖未入会权限、交接、存档字段恢复、和平/战斗完成、多嫌犯、原版期限、实际申报、取消交付与不重复领奖；引擎边界为桩 |
| `tests/test_battle_peace.py` | 62断言 | 反编译当前游戏的结果方法，编译生产 `GwpPeace` 与执法重试，旧代码负对照复现空引用，核对三种战果、脱离主队、收尾后讲和、正反对去重、交战理由与玩家讲和方式 |
| `tests/test_firesworn_outfitting.py` | 53断言 | 抽取实际配装、抽兵、护送加人和归队方法，核对少领反例、混编/库存/品质、重复领装、原版民兵留村、家里至少留 5 人与归队不重复人数；引擎名册与评分为桩 |
| `tests/test_armory_allocation.py` | 88断言 | 抽取生产全阶分配/缓存、实战发装与结算取值、出队和招募搬装、分兵种战损；高阶保留原配及坐骑/双刀，低阶择强、预设空位、弹药配对、四格上限、技能/品质/存量守恒及祭司最后一人带装 |
| `tests/test_training_transport.py` | 51断言 | 抽取生产派队/50%门槛及75%概率翻倍/50%数量公式、单程落村/改道、SyncData与老兵搬装回调，核对健康老兵、全队分母、留守、减员、无弱兵、失效原队、冷却及同版存读；引擎行为为桩 |
| `tests/test_lord_combat.py` | 81断言 | 编译生产身份/武器解析器、实际命中、盾伤模型、盾体选择、耐久/延后破盾、当前骑手变更/死亡与分伤；引擎边界为桩 |
| `Verify-FounderProfiles.ps1` | 85断言 | 实际原版语体/选音、六组演员、战斗声音与性格合法互异；生产Harmony补丁范围与婚后接续 |
| `Verify-PartyTransfers.ps1` | 177断言 | Windows PowerShell 5.1加载实际游戏和模块DLL，核对人数/伤兵/经验守恒、精确回滚及改名后的连续编号查重 |
| `tests/test_faith_logistics.py` | 1029断言（含500轮重复重算） | 生产首兵建队/回读/死亡、实际归队伤兵和经验、已有民兵不清空、祭司严格领先及势力守恒、全量交俘/交后访问需求消失、两村超额分流/押送容量、混编落村和地牢同版存读；引擎边界为桩 |
| `tests/test_faith_villagers.py` | 31断言 | 生产出生/补员比例纠偏、只交换灰袍名额、伤情经验守恒、真实原版守方枚举、混装成交/村税/公库分账、余货和财产损失、物品品质标记存读；原版规则来自本机反编译，引擎边界为桩 |
| `tests/test_raid_score.py` | 67断言（玩家/开发两种编译） | 真实信众比例查询、0～100%梯度与最多减50%、原震慑叠乘、无祭司/未登记/非村庄/户数边界、单次原评分及两类有界诊断 |
| `tests/test_firesworn_logs.py` | 3 | 带BOM的启动标记/归档与启动切片、带时区不同秒精度、完整行去重、当前村民回款与村税分账口径；2026-10-19专项通过 |
| `BattleSceneTests` | 144 | 援军三层/预算/Origin/原生耗尽、城内出生点与关闭随机赠援、死战不退 |
| `DualBladeTests` | 27 | 双刀 AI 与踢/盾击互斥 |
| `tests/test_duty_priority.py`、`test_duty_scenarios.py`、`test_village_relief.py` | 91断言 | 真实差事准入、约斗/收养目标解析、玩家征调/求援、后台约斗先后交接、暂停/返回及三类驻留任务保存恢复；引擎移动/比武/收养副作用受控 |
| `tests/test_duty_banner.py` | 40断言＋XML配置核对 | 生产执勤身份/领取资格、补领/入退会/重入、家族/地图/场景纹章与副本隔离/编辑器/视觉刷新、奖励池排除及升级保护；原版Hero旗槽/奖励池方法反编译执行，引擎边界为桩 |
| `tests/test_affairs.py` | 6项，权限15/VM36/家族入口53断言、真实输入11/帧处理5断言及XML核对 | 候选/酬劳/权限/过期合同、稳定ID刷新/接受、百科不退事务、未入会彩蛋入口、成员工作/历史回报分开、五页空态/六职能、原版完成区/头像边框/换行/三滚动区；输入直接执行安装版DLL，其他引擎边界为桩 |
| `tests/test_city_search*.py` | 17项，含44项城防、16项搬运、11项站位/赴约、42项协调器及48项场景推进断言 | 卫兵、原版队伍/命令表/竞技场生命周期/通道/出生与随从负对照、双方伤亡分账、入口一波/大厅零增援/回程耗尽、搬运兵换人/跨场景/身体出口门槛、同版保存读回和正常ScriptBehavior门目标；场景加载/导航/动作/物理仍为引擎边界 |
| `MusicTests` | 58847439 断言（多为逐采样） | FLAC 逐位无损、Wwise 曲线与片段包络、输出限制器、升档节奏、过渡规则、静音设备生命周期。`--fingerprint` 固定场景指纹；`--compare-old` 与旧预渲染对照；`--dump` 导出混音测响度 |
| `CaseSettlement.Tests` | 155 | 案件结算、派遣记录同版本存读往返 |
| `ChokePointTests` | — | 隘口候选已撤回，其 bin/obj 仅为被忽略的生成缓存，不参与构建或部署 |

测试分别使用引擎边界桩、反编译原版方法回放或实际游戏 DLL，具体看各行；**离线通过不验证真实战场位置、AI 行军、原生物理或游戏内观感。**

## live 镜像是硬要求

`_Module` 下的可部署运行时文件是开发期事实来源。改动后**立即**复制同一版本到
`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`。

- 这条与 Git 发布无关，工作树可以一直未提交。
- 每次部署后**比对哈希**，不要假定复制成功。（依据：流程）
- runtime 同步允许覆盖只读目标文件：现有 `ModuleData/sphpcustombattle.xslt` 在源和 live 均带 ReadOnly，未设置 `OverwriteReadOnlyFiles` 会使默认构建在已输出 Client DLL 后中断，Editor 尚未更新。（依据：实机诊断 2026-10-07 MSB3021；流程）
- 任何可部署源文件与 live 对应文件有差异时，**不得开始或接受实机测试**。（依据：流程）
- 本机 live 一直是开发版布局：编辑器专用的 `Assets`、`AssetSources`、`RuntimeDataCache`
  常驻 live 模块，随时能进 Modding Kit 继续改；发布后、实机测试前都不挪走。（依据：用户裁定）
  这三个目录不属于 `_Module` 的镜像范围，校验只检查非空，不比对编辑中的内容。
  `AssetPackages`、`DsAssetPackages` 同样不向开发 live 镜像，live 中连空目录也不保留；
  用户版打包美术保存在仓库 `_Module/AssetPackages` 和发布 staging，开发版使用可编辑美术。
  Client/Editor 都保留监控 DLL，构建拒绝 `DeployToLiveModule=true` 与 `GwpDiagnosticsEnabled=false` 的组合。
  玩家版反之：打包美术、无监控、无可编辑目录。生成的 `bin` 与 `Shaders` 可以只存在于 live。
  （依据：用户裁定 2026-10-07）详见 `../reference/asset-pipeline.md`。
- 单文件改动（如新增中文键）先单文件镜像并比 hash，再随构建全量同步。
- 部署前确认游戏没开：进程名是 `TaleWorlds.MountAndBlade.Launcher`、`Watchdog`、`Bannerlord`
  （按 `Bannerlord|TaleWorlds|Launcher|Watchdog` 匹配）。只查名字里带 `Bannerlord` 会漏掉启动器，
  2026-09-25 就因此在游戏运行时构建，复制 DLL 被占用失败（MSB3027）。复制失败时 live 仍是旧 DLL，
  校验脚本比的是旧文件，结果不能当作新版本的验证。

## 正式发布包

> 发布流程的完整清单见 [`../reference/release-checklist.md`](../reference/release-checklist.md)。
> **普通开发构建不得创建发布 ZIP。**

- 玩家 DLL 必须以
  `-t:Rebuild -p:GwpDiagnosticsEnabled=false -p:DeployToLiveModule=false -p:OutputPath=<staging>`
  隔离编译，**不回写 live**。
- **绝不**用诊断关闭的玩家 DLL 替换 live 测试 DLL；打包后 live 要重建回诊断版，
  再跑一次 `Verify-LiveModule`。
- ZIP 内只有一个顶层 `GreyWarden/` 目录和普通客户端运行时内容。排除
  `tools`、PowerShell 脚本、日志、诊断输出、开发笔记、PDB、编辑器二进制、
  `Assets`、`AssetSources`、`RuntimeDataCache`、`DsAssetPackages`、`Shaders/`、`bin/Win64_Shipping_wEditor/`、
  Client `.pdb`、`shader_compile_report.log`。
- `DsAssetPackages/` 是 Kit 勾选专用服务器发布时生成的服务器资源包，玩家客户端不用。发布后从 live 移到
  `.codex_tmp/ds-asset-packages-<版本>/` 留给日后联机适配；不放进 `_Module`、不留在 live、不进客户端发布包。
  开发版 live 不得留下发行版专有的东西。（依据：用户裁定 2026-10-02）
- 游戏父 `Modules` 目录只保留**最新一对** `GreyWarden-<version>.zip` + `.sha256`，
  验证后删除更旧的本地包对。

### 监控串核查（r11 的教训）

玩家包必须**实证无监控，不是假定**。按 **UTF-16LE 与 ASCII 双编码**核查
——只用 ASCII grep 会全假阴性：

```
GreyWarden-AI-Diagnostics
GreyWarden-Case-Events
GreyWarden-Faults
GreyWarden-Diagnostics-Archive
Mount and Blade II Bannerlord
```

五条全部 absent 才算通过。同一批串在 live 诊断 DLL 里应有若干 PRESENT，
构成有效对照。

### 从 GitHub 重新下载后核验

不是只比本地：下载远端 zip，比对字节数与 SHA-256，重新解出包内玩家 DLL 再查一遍
监控串。归档在 `build-check/package-<version>/`：`manifest.json`（逐项哈希）、
玩家 DLL 副本、`release-notes.md`、`.zip.sha256`。

release 正文由中英 README 的对应段落**程序化抽取**，不手写，避免与 README 漂移。

## 玩家 README

`_Module/README.md` 与 `README_EN.md` 是**发布产物**，只在用户说开发完成、要发新版本
时更新。开发期一律不动。写法见 `.claude/rules/release-notes.md`。

## 已发布版本与下一版的对比基线

- 最新正式版为 [v1.4-r15](https://github.com/Lucicain/GW/releases/tag/v1.4-r15)（模组版本1.4.15），2026-10-17发布并设为latest。tag指向`d199c54`；游戏内容截至`f00ca9b`，最后一笔仅发行说明与记录。（依据：用户批准、远端Release与tag核验2026-10-17）
- **下一版的玩家说明对比`v1.4-r15`。** 本次r15说明对比的是上个正式替换包`v1.4-r14-refresh`。（依据：发布基线）
- 上一正式版 v1.4-r14（模组版本 1.4.14）。2026-10-07 同号替换了下载包，加入入国拦截、读档离国、自动换装候选保护，
  源码 `89a9972`，tag `v1.4-r14-refresh`。原 tag `v1.4-r14` 仍指 `ac66f86`（替换前内容），已推送的 tag 不移动。（依据：用户裁定 2026-10-07）
- 旧r15本地测试包已由本次正式包替换，不再作为现行下载包；线上没有该测试版Release可下架。最新美术包SHA-256 `753aaf1d009691006ac207fa869db44a63a33caa68b17621f2938833c1b93fb6`（148项）。核验归档仍为 `build-check/package-v1.4-r15/`，旧资源回退点见local-scratch。（依据：用户裁定与文件/远端核对2026-10-17）

## 当前正式玩家包

v1.4-r15 正式包（2026-10-17 用户确认后发布，沿用 1.4.15、不移动已有 tag）：78 项，ZIP SHA-256 `bcd77a8f198db4d324ca8e289944888500d04035bf16b00cc193d6bedac82dab`，玩家 DLL `df96a0a8e928f905208d88a9ef757f91f3dfd07fda6c6eac659928f34f732f8c`；五组监控标记双编码全无。Release 只有 ZIP 与 SHA256 两项附件；核验证据与远端元数据在 `build-check/package-v1.4-r15/`。（依据：发布核验 2026-10-17）
