# 构建、校验与部署

> 当前状态：live为诊断开发版，与仓库一致；各功能验收归所属state
> 最后验收：2026-11-12 用户在 v1.5.7 启动并读档进大地图；正式包基线见下文
> 覆盖源码：`GreyWardenPolicePurity.csproj` `tools/*.ps1` `tools/New-PlayerPackage.py`
> 待实测：v1.5.7 的新档、战斗结算、使者交涉与港口/岸边借船；百科称呼见家族；辅助模组的一键新档与地图快进

## 当前开发部署

live 为按安装版 Bannerlord v1.5.7 构建的 r16.2 诊断开发版（开发修订号仍为 1.4.16.2），包含[守村补兵兼容修复](firesworn-villages.md#信众户与守村队)、[任命加载修复](city-search-support.md#已接住城案的目标留城)、[日结军械摘要修复](firesworn-armory.md#开发版查看与验证)和[灰袍收养家谱及百科称呼](family.md#成员)。Client/Editor 的 SHA-256 同为 `265E344F79026864C5CA9748489DF8E79C45E3541B09222831B8DEC93A5D2870`，live 镜像与安装版兼容预检通过。（依据：构建、部署、哈希与兼容预检 2026-11-12）

v1.5.7 启动故障：`rgl_log_36640.txt` 的 `ReflectionTypeLoadException` 点名旧 DLL 中 `GwpAgentApplyDamageModel.CalculatePassiveAttackDamage` 和 `GwpFaithCombatSimulationModel.SimulateHit` 不再实现原版抽象签名。旧 DLL 在同一安装版复现类型加载失败；新 DLL 接上对应签名，并更新部队上限的海陆参数、治疗委托、自动战斗的海战计算、借船锚点和使者报价的界面刷新入口。原版拒绝的阶段见[模块加载手册](../vanilla/module-loading.md#v157-子模块类型加载失败)。预检与模块 XML 通过；用户一次正常启动到主菜单、读旧档、进大地图，日志 `rgl_log_7016.txt` 与灰袍案件日志确认地图和案件逻辑运行，此次旧报错复发 0 次。（依据：崩溃日志、安装版反编译、旧新 DLL 对照和用户实测 2026-11-12）

同日审计清理包含战役启动时清空不进存档的部队意图表与推迟讲和，待重试讲和表改为行为实例字段，删除无人写入的旧存档字段 `gwp_enf_war_streak_*` 与若干死代码、过期注释，押送费率收进 `GwpTuning`，玩家版诊断空壳标 `Conditional`，执法每帧回调取消无条件新建集合。跨战役清空的验收见[公共底座](infrastructure.md#待实测)。2026-11-07 部署的约斗、躲城、同城巡逻涉及的真实进城、首场结算和同版存读验收分归[野外执法](field-arrest.md)、[使者](case-envoy.md)和[搜捕支援](city-search-support.md)。

灰袍兼容预检只覆盖本模块的引用、补丁绑定及已知静态初始化问题，不执行前置模组的完整启动构造，不能代替启动器整条加载链；2026-10-27前置模组重复勾选导致的启动失败即在此之外。（依据：预检边界与启动故障2026-10-27）

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

安装辅助模组时的 Native 为 v1.4.8。原版 `Campaign.SpeedUpMultiplier` 默认 4；作弊模式控制台 `campaign.set_campaign_speed_multiplier 10` 可设快进倍率，普通模式上限 15、开发模式 30；倍率不保证实际吞吐。官方 `FastMode` 模块设置 `CampaignOptions.AccelerationMode`，不能据名称当作地图快进。（依据：当时的本机模块与 C#反编译 2026-10-18）

## 自动化验证边界

通用测试桥及本机独立测试模组已退休；主 DLL 不再给桥开放内部成员，源码、协议、服务、自检与专属场景一并移除。用户启动器未引用它，未发现全局 MCP 注册；既有测试存档保留，其模块列表可能仍包含已退休模组，不作为普通游玩或验证入口。历史在 `b0787bc` 存档，维护理由见[原始方案](../reference/development-maintainability-plan-20261106.md)。（依据：用户授权落地与依赖核对 2026-11-06）

开发验证按 [workflow](workflow.md#验证如何选) 选择：自有纯规则直接调用同一份生产源码，实际游戏行为由正常游玩诊断及必要现场核实；不将局部通过等同于整局、引擎动作或原生存读档通过。游戏操作与部署占用边界见 AGENTS「实机」。（依据：用户裁定 2026-11-06）

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
- `<Version>` 是模组修订号（`1.4.16.2` ↔ 正式 `v1.4-r16.2`），**不是 Bannerlord 版本
  依赖闸门**。

### 游戏世代检测

`GwpDetectTargetGame` 读取目标安装的 `Modules/Native/SubModule.xml` 版本：`148`、`150`（1.5.0 起）、`157`（1.5.7 起）分别接对应 API。没有安装目录时可用精确版本引用程序集并显式指定 `GwpTargetGame`。1.5.0 改了被动撞击伤害的签名；1.5.7 又改了自动结算等接口，因此**对目标游戏版本分别编译**，不能只看 `1.5` 前缀推定兼容。当前 r16.2 玩家目标是 1.4.8 和 1.5.7；1.4.8 用 `Bannerlord.ReferenceAssemblies` 1.4.8.119303 重建的玩家 DLL，与此前以该安装版验证的同内容 DLL 的 IL 一致。打包只给 `SubModule.xml` 写该包的修订号，共用源数据。（依据：原版接口与隔离构建对照 2026-11-12）

1.4.8 和 1.5.7 的最终玩家包以同一份灰袍玩法、资源和称呼为验收目标，版本分支限于原版接口差异；逐项核对两包数据并单独登记游戏内验证范围。（依据：用户裁定 2026-11-12）

## 校验脚本（`tools/`）

| 脚本 | 作用 | 通过标准 |
|---|---|---|
| `Invoke-ModuleLoadPreflight.ps1` | 加载安装版依赖并检查类型/补丁绑定、启动名牌补丁的界面静态字段依赖与实际初始化异常 | 类型/成员无缺失，补丁全部成功；EARLY_UI_FIELD_FAIL_COUNT与STATIC_INIT_FAIL_COUNT均0；数量变化有解释 |
| `Verify-GameCompat.ps1` | 检查 DLL 与当前安装的 Bannerlord 是否兼容，防止换游戏版本后静默产出游戏拒绝加载的模组。默认查 live 目录里已部署的 DLL，须部署后再跑，或用 `-ModuleDll` 指向新构建；部署前跑查的是旧版（依据：实机核对 2026-10-22，补丁类数未随改动变化） | `GAME COMPAT: PASS`、`MEMBER_FAIL_COUNT=0`、`PATCH_FAIL=0` |
| `Verify-LiveModule.ps1` | **双向**比对开发 runtime，检查开发布局和双端监控 DLL | 无缺失/差异/散落；三类可编辑目录非空；无客户端/服务器打包目录；Client/Editor DLL 哈希一致且五组监控串存在；两份玩家 README 一致 |
| `Verify-ContentKeys.ps1` | XML / 字面量 / 生成的本地化键，只读 | `MISSING=0`。未被引用的字符串**不是死内容的证据，永不删除** |
| `Verify-ClanBanner.ps1` | 新旗帜的安装版 XSD、实际 Banner DLL 与保留图标离线检查；`-Installed` 另核对家族引用、注册和 XML 镜像；状态归 [旗帜](clan-banner.md) | XSD/ID/managed Banner/installed XML PASS；不代表引擎渲染通过 |
| `Watch-GreyWardenAI.ps1` | 实机 AI 诊断日志跟看 | — |
| `New-PlayerPackage.py` | 从隔离玩家DLL和_Module生成本地ZIP及逐项manifest，ASCII/UTF-16检查无监控、逐项解压哈希 | `PLAYER PACKAGE: PASS`；不自动上传 |
| `Generate-CodeMap.py` | 从源码重建 `docs/state/code-map.md`；PostToolUse 钩子自动跑 | 不要手改产物（依据：流程） |
| `Check-StateFreshness.py` | 报 state 引用失效的文件与符号、源码归属、state 堆积、残留的已取消头部与遗留物；生成进度汇总 | 被点名的当轮处理 |
| `Check-CodeHygiene.py` | catch 依据、无人引用声明、绕过共享归属、重复函数体、逻辑层越界、玩家版诊断空壳未标 `Conditional`；预提交硬闸 | 六项全零 |
| `Analyze-Firesworn.py` | 只读：汇总最近一次启动游戏以来的火誓者监控行，主动列出平衡问题（见 `diagnostics.md`「火誓者监控字段」） | 不是闸门，读结果 |

`Invoke-ModuleLoadPreflight.ps1` 只能用 Windows PowerShell 5.1：它在 .NET Framework 里加载游戏程序集，pwsh 下 Harmony 撞上已移除的 BinaryFormatter，68 个补丁全部报失败（2026-10-05 实测）。`Verify-GameCompat.ps1` 与 `Verify-ContentKeys.ps1` 只能用 PowerShell 7（pwsh）——前者需要 System.Reflection.Metadata，后者在 5.1 下读 XML 报解析错误（2026-09-26 实测）。`Verify-LiveModule.ps1` 两边都能跑。两个 `.py` 检查用 python。

## 自检（`tools/`）

| 工程 | 覆盖 |
|---|---|
| `tests/test_code_map.py` | 动态目标、显式接口名、组合属性、条件编译、类作用域及当前仓库补丁声明集合完整性；不替代运行时绑定 |
| `tests/test_player_text.py` | 中英输出、只读陈旧检查、新键生成、换行等价、占位符丢失拒绝及新增内联文本拒绝 |
| `tests/test_project_checks.py` | 战力/讲和/守村经验容量/玩家委派/海陆交接归属、引用失效核对、严格模式保留原硬闸；运行 `python -m unittest discover -s tools/tests -p test_project_checks.py -v` |
| `MusicTests` | FLAC 逐位无损、Wwise 曲线与片段包络、输出限制器、升档节奏、增援提示波数与分局档位、过渡规则、静音设备生命周期。`--fingerprint` 固定场景指纹；`--compare-old` 与旧预渲染对照；`--dump` 导出混音测响度 |
| `GreyWardenPolicePurity.Core.Tests` | 直接检查同一份 Core 生产规则的分支与优先级；`dotnet run --project GreyWardenPolicePurity.Core.Tests -c Release`，不模拟引擎，不提供无样本回放通过 |

模拟游戏行为的离线测试不恢复；Core 只编译主项目的同一份纯规则源码，不引用游戏 DLL。`MusicTests` 同时是配乐校准的混音导出工具（`Measure-MusicCalibration.py` 读它的输出），继续保留。（依据：用户裁定 2026-11-04、2026-11-06）

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

- 最新正式版为 v1.4-r16.2（模组版本 1.4.16.2），2026-11-11 同号重发并设为 latest。发布页和下载地址沿用 `v1.4-r16.2`，首发 tag 指向 `55fd717`，修复包源码 tag `v1.4-r16.2-refresh` 指向 `9505b6d`。**下一版的玩家说明对比 `v1.4-r16.2-refresh`。**（依据：用户要求同号重发、远端 Release 与 tag 核验 2026-11-11） [GitHub 发布页](https://github.com/Lucicain/GW/releases/tag/v1.4-r16.2)
- 上一正式版 [v1.4-r16](https://github.com/Lucicain/GW/releases/tag/v1.4-r16)（模组版本 1.4.16），2026-10-29 发布，tag 指向 `fc87267`。
- 较早正式版 [v1.4-r15](https://github.com/Lucicain/GW/releases/tag/v1.4-r15)（模组版本1.4.15），2026-10-17发布，tag指向`d199c54`。v1.4-r14 同号替换过，替换包tag为`v1.4-r14-refresh`，原tag`v1.4-r14`不移动。美术包自r15未变，SHA-256 `753aaf1d009691006ac207fa869db44a63a33caa68b17621f2938833c1b93fb6`。

## 当前正式玩家包

v1.4-r16.2 修复包有 78 项，ZIP 为 442,781,486 字节，SHA-256 `ca14af3401e95d6725139c0cd94c666c11147cb61502e8821e891d697667d00f`。隔离玩家 DLL 的 SHA-256 为 `17F2BDFAC33D6935D2E9EA1EAC82C048DEE355AEFB6C69233D051DEEA784D5E3`。安装版 v1.4.8 兼容预检通过，115 个补丁成功绑定。Release 附件已换为修复版 ZIP 与 SHA-256 校验文件。从公开下载地址重新取包，字节数、哈希、78 项逐项清单与诊断标记双编码复核通过，两份 README 与 live 一致。游戏 Modules 目录只留这对包。核验证据在 `build-check/package-v1.4-r16.2/`，首发包核验证据在 `build-check/package-v1.4-r16.2-original/`。（依据：发布与重新下载核验 2026-11-11）

Windows PowerShell 5.1 直接加载 `build-check` 目录中的 DLL 曾报 0x80131515，兼容预检使用本机临时隔离目录的构建产物。
