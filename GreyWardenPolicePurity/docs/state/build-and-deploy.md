# 构建、校验与部署

> 当前状态：main 火誓者 live 使用监控 DLL 与可编辑美术；构建排除玩家打包资源，校验同时检查布局和监控
> 最后验收：`03964e4`（v1.4-r13，含远端下载核验）
> 已复核至：`cbdfb71`（2026-10-16）
> 覆盖源码：`GreyWardenPolicePurity.csproj` `tools/*.ps1` `tools/New-PlayerPackage.py`
> 待实测：无

## 当前开发部署

当前 live 是 `a3ba641` 加上「骑兵只领马上可用的武器」的监控开发构建（领主战斗加成与锁血、守村队低阶直招、练兵单程送村、全兵种按阶配装与入库分流、新旗帜、闪退取证诊断都在内）。
Client/Editor DLL SHA-256 同为 `224ea0f2981954acff7111f270a0f7278531baedb1325a2b076e8d5fae3c39d1`；Assets、AssetSources、RuntimeDataCache 保留，没有 AssetPackages/DsAssetPackages。
构建 0 错误/警告，runtime 镜像一致；GameCompat 缺类型/成员 0、90 补丁绑定；名册转移 177 断言通过。（依据：构建、部署哈希与离线执行 2026-10-16）

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
| `Invoke-ModuleLoadPreflight.ps1` | 把构建产物对着已安装游戏加载，报出游戏启动时会撞上的类型加载与 Harmony 绑定失败 | 类型/成员引用无缺失；类型加载数与补丁绑定数与上一版一致或有已解释的增量 |
| `Verify-GameCompat.ps1` | 检查 DLL 与当前安装的 Bannerlord 是否兼容，防止换游戏版本后静默产出游戏拒绝加载的模组 | `GAME COMPAT: PASS`、`MEMBER_FAIL_COUNT=0`、`PATCH_FAIL=0` |
| `Verify-LiveModule.ps1` | **双向**比对开发 runtime，检查开发布局和双端监控 DLL | 无缺失/差异/散落；三类可编辑目录非空；无客户端/服务器打包目录；Client/Editor DLL 哈希一致且五组监控串存在；两份玩家 README 一致 |
| `Verify-ContentKeys.ps1` | XML / 字面量 / 生成的本地化键，只读 | `MISSING=0`。未被引用的字符串**不是死内容的证据，永不删除** |
| `Verify-ClanBanner.ps1` | 新旗帜的安装版 XSD、实际 Banner DLL 与保留图标离线检查；`-Installed` 另核对家族引用、注册和 XML 镜像；状态归 [旗帜](clan-banner.md) | XSD/ID/managed Banner/installed XML PASS；不代表引擎渲染通过 |
| `Verify-CrimeReceipts.ps1` | 犯罪回执检查，只读，不打开或修改存档 | PASS |
| `Watch-GreyWardenAI.ps1` | 实机 AI 诊断日志跟看 | — |
| `New-PlayerPackage.py` | 从隔离玩家DLL和_Module生成本地ZIP及逐项manifest，ASCII/UTF-16检查无监控、逐项解压哈希 | `PLAYER PACKAGE: PASS`；不自动上传 |
| `Generate-CodeMap.py` | 从源码重建 `docs/state/code-map.md`；PostToolUse 钩子自动跑 | 不要手改产物（依据：流程） |
| `Check-StateFreshness.py` | 报 state 过期、源码归属、state 堆积、复核头部格式/日期/提交存在性与遗留物；生成进度汇总 | 被点名的当轮处理 |
| `Check-CodeHygiene.py` | catch 依据、无人引用声明、绕过共享归属、重复函数体；预提交硬闸 | 四项全零 |
| `Analyze-Firesworn.py` | 只读：汇总最近一次启动游戏以来的火誓者监控行，主动列出平衡问题（见 `diagnostics.md`「火誓者监控字段」） | 不是闸门，读结果 |

`Invoke-ModuleLoadPreflight.ps1` 只能用 Windows PowerShell 5.1：它在 .NET Framework 里加载游戏程序集，pwsh 下 Harmony 撞上已移除的 BinaryFormatter，68 个补丁全部报失败（2026-10-05 实测）。`Verify-GameCompat.ps1` 与 `Verify-ContentKeys.ps1` 只能用 PowerShell 7（pwsh）——前者需要 System.Reflection.Metadata，后者在 5.1 下读 XML 报解析错误（2026-09-26 实测）。`Verify-LiveModule.ps1`、`Verify-CrimeReceipts.ps1` 两边都能跑。两个 `.py` 检查用 python。

## 测试工程（`tools/`）

| 工程 | 数量 | 覆盖 |
|---|---|---|
| `tests/test_player_text.py` | 4 | 中英输出、只读陈旧检查、新键生成、换行等价、占位符丢失拒绝及新增内联文本拒绝 |
| `tests/test_project_checks.py` | 10 | 战力/讲和/守村经验容量/玩家委派归属、复核头部格式与日期、无效提交提示、严格模式保留原硬闸；运行 `python -m unittest discover -s tools/tests -p test_project_checks.py -v` |
| `tests/test_atonement.py` | 42断言 | 抽取实际赎罪、委派入口与使者方法，覆盖未入会权限、交接、存档字段恢复、和平/战斗完成、多嫌犯、原版期限、实际申报、取消交付与不重复领奖；引擎边界为桩 |
| `tests/test_battle_peace.py` | 62断言 | 反编译当前游戏的结果方法，编译生产 `GwpPeace` 与执法重试，旧代码负对照复现空引用，核对三种战果、脱离主队、收尾后讲和、正反对去重、交战理由与玩家讲和方式 |
| `tests/test_firesworn_outfitting.py` | 53断言 | 抽取实际配装、抽兵、护送加人和归队方法，核对少领反例、混编/库存/品质、重复领装、原版民兵留村、家里至少留 5 人与归队不重复人数；引擎名册与评分为桩 |
| `tests/test_armory_allocation.py` | 60断言 | 抽取生产全阶分配/缓存、实战发装与结算取值、出队和招募搬装、分兵种战损；高阶保留原配及坐骑/双刀，低阶择强、预设空位、弹药配对、四格上限、技能/品质/存量守恒及祭司最后一人带装 |
| `tests/test_training_transport.py` | 43断言 | 抽取生产派队/50%门槛及75%概率翻倍/50%数量公式、单程落村/改道、SyncData与老兵搬装回调，核对健康老兵、全队分母、留守、减员、无弱兵、失效原队、冷却及同版存读；引擎行为为桩 |
| `tests/test_lord_combat.py` | 75断言 | 编译生产身份/武器解析器、实际命中、盾伤模型、盾体选择、耐久/延后破盾、当前骑手变更/死亡与分伤；引擎边界为桩 |
| `Verify-FounderProfiles.ps1` | 85断言 | 实际原版语体/选音、六组演员、战斗声音与性格合法互异；生产Harmony补丁范围与婚后接续 |
| `Verify-PartyTransfers.ps1` | 177断言 | Windows PowerShell 5.1加载实际游戏和模块DLL，核对人数/伤兵/经验守恒、精确回滚及改名后的连续编号查重 |
| `tests/test_firesworn_logs.py` | 1 | 带BOM的启动标记、当前日志、归档，以及启动前后过滤 |
| `BattleSceneTests` | 137 | 援军三层/预算/Origin/原生耗尽 + 死战不退 18 项 |
| `DualBladeTests` | 27 | 双刀 AI 与踢/盾击互斥 |
| `MusicTests` | 58770640 断言（多为逐采样） | FLAC 逐位无损、Wwise 曲线与片段包络、输出限制器、升档节奏、过渡规则、静音设备生命周期。`--fingerprint` 固定场景指纹；`--compare-old` 与旧预渲染对照；`--dump` 导出混音测响度 |
| `CaseSettlement.Tests` | 155 | 案件结算、派遣记录同版本存读往返 |
| `ChokePointTests` | — | 隘口候选已撤回，其 bin/obj 仅为被忽略的生成缓存，不参与构建或部署 |

测试都是引擎边界桩：**不验证真实战场位置、AI 行军、原生物理或游戏内观感。**

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

- 最新正式版 v1.4-r14（模组版本 1.4.14）。2026-10-07 同号替换了下载包，加入入国拦截、读档离国、自动换装候选保护，
  源码 `89a9972`，tag `v1.4-r14-refresh`。原 tag `v1.4-r14` 仍指 `ac66f86`（替换前内容），已推送的 tag 不移动。（依据：用户裁定 2026-10-07）
- **下一版写玩家说明时，「上一版」是 `v1.4-r14-refresh`，不是 `v1.4-r14`**：玩家手里的是替换后的包。
- 替换后的包：ZIP 434464792 字节，SHA-256 `c5d8e2f4652d9eb55ead640bdb615b51b42ec52b13b5473e0d215dcc0bdb8e0a`；
  玩家 DLL `99f3c068373ce0a9c227cf42fb0a6c69bd1bdd8a23c23cc8a6a4ce0e53a46e8f`；美术 TPAC 与原 v14 相同（`bcaae1d2…6e09`）。
- 核验归档：`build-check/package-v1.4-r14-refresh/` 与 `build-check/package-v1.4-r14-original/`（各四件，见上节）。
  替换前公开过的原 ZIP 在 `.codex_tmp/pre-refresh-v14-release-20261007/`，见 `../reference/local-scratch.md`。
- 当前本地测试版 v1.4-r15（模组版本 1.4.15）：2026-10-16 同号替换了 10-15 的本地包，只出本地 ZIP 并推送源码，不打 tag、不建 Release。
  没有监控（诊断关闭），也没有玩家军械库（开发暂停）；其余功能与开发版相同。（依据：用户裁定 2026-10-15、2026-10-16）
  ZIP `Modules/GreyWarden-v1.4-r15.zip` 442704604 字节，SHA-256 `7301efbc7b35cfc2675b43e428afbbb4373b7dbca8ee3595575456a05abe1cda`，78 项；
  玩家 DLL `2cd410d585997ca6657b73ad6cdcca1d64d641e802f148fd76108da0f84c86cd`（GameCompat 86 补丁绑定，五组监控串双编码均不存在）；
  美术 pack0 `4606781f316292b8c12a93a1054c62110469a7c34e02156bce99451bc964af22`（149 项，比旧包多旗帜材质、旗帜纹理和一个未使用的 `new_material` 草稿材质）。
  核验归档 `build-check/package-v1.4-r15/`（manifest、玩家 DLL、`.zip.sha256`）。之后用户删掉 `new_material` 重新发布，仓库 pack0 已换成 148 项的 `753aaf1d…`；这份 ZIP 还没重打。被替换的 10-15 包（ZIP `a54856d9…`）没有保留，它的美术包作为回退点见 local-scratch。
  玩家说明的 r15 条目对比的仍是 `v1.4-r14-refresh`。
