# 构建、校验与部署

> 当前状态：原 v14 同号整合包已上线并远端复核；live 回归合入修复的 main 火誓者开发版
> 最后验收：`03964e4`（v1.4-r13，含远端下载核验）
> 覆盖源码：`GreyWardenPolicePurity.csproj` `tools/*.ps1`
> 待实测：无

## 两个开关决定一切

用户要求将本轮修复直接整合到尚未公布的原 v1.4-r14，同号更新发布页和下载包，不建立 v15；完成后回归 main 火誓者开发，现有经济源码本轮不调整，第三方代码不改。修复源码存档 `89a9972` 已推送；原 v14 标签和提交 `ac66f86` 保留，自动审批拒绝移动已推送标签后采用资产替换方案。（依据：用户裁定 2026-10-07；流程）

当前 live 部署来源为主工作树 `C:/Users/lucif/source/repos/GreyWardenPolicePurity`，main 已合入入国入口保护、通用读档退出和自动装备候选保护，保留火誓者完整成果并已重新部署。部署前回退：原开发双端 DLL/PDB 与 runtime 在 `.codex_tmp/pre-v14-live-20261006/`；v14 整合包开发 DLL 在 `.codex_tmp/v14-before-main-resume-20261007/`。已恢复四个火誓者仪式图片/声音文件；Assets、AssetSources、RuntimeDataCache 共 339 文件部署前后逐项哈希相同，镜像以 main 为准。（依据：流程）

主线部署验证：开发及隔离无诊断构建均 0 警告/0 错误；GameCompat/PREFLIGHT 开发 663 类型/79 补丁、无诊断 655/76，成员与补丁失败均 0；116 项修复回归 PASS；ContentKeys MISSING=0；live 70 个运行时文件无缺失/差异/散落。Client/Editor DLL SHA-256 均 `dfcfbef6ce33808b9159256c1ade71973b05c11f7914f5aab1ef73b4a9ad8c7c`，与主线隔离开发构建一致；RBM 等第三方文件及经济源码本轮未变。实机功能待验收清单仍由各子系统维护。

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
| `Verify-LiveModule.ps1` | **双向**比对 live 测试模块与仓库模块 | 无缺失、无差异、无散落文件；两份玩家 README 一致 |
| `Verify-ContentKeys.ps1` | XML / 字面量 / 生成的本地化键，只读 | `MISSING=0`。未被引用的字符串**不是死内容的证据，永不删除** |
| `Verify-CrimeReceipts.ps1` | 犯罪回执检查，只读，不打开或修改存档 | PASS |
| `Watch-GreyWardenAI.ps1` | 实机 AI 诊断日志跟看 | — |
| `Generate-CodeMap.py` | 从源码重建 `docs/state/code-map.md`；PostToolUse 钩子自动跑 | 不要手改产物（依据：流程） |
| `Check-StateFreshness.py` | 报 state 过期、新增未归属源码、改到未提取子系统、state 堆积流水、豁免数 | 被点名的当轮处理 |
| `Check-CodeHygiene.py` | 每个不重新抛出的 catch 都带 `依据：`；每个常量、字段、属性、方法都有人引用。预提交钩子据此拒绝提交 | `0 个 catch 缺依据 / 0 个声明无人引用` |
| `Analyze-Firesworn.py` | 只读：汇总最近一次启动游戏以来的火誓者监控行，主动列出平衡问题（见 `firesworn-economy.md`「监控」） | 不是闸门，读结果 |

`Invoke-ModuleLoadPreflight.ps1` 只能用 Windows PowerShell 5.1：它在 .NET Framework 里加载游戏程序集，pwsh 下 Harmony 撞上已移除的 BinaryFormatter，68 个补丁全部报失败（2026-10-05 实测）。`Verify-GameCompat.ps1` 与 `Verify-ContentKeys.ps1` 只能用 PowerShell 7（pwsh）——前者需要 System.Reflection.Metadata，后者在 5.1 下读 XML 报解析错误（2026-09-26 实测）。`Verify-LiveModule.ps1`、`Verify-CrimeReceipts.ps1` 两边都能跑。两个 `.py` 检查用 python。

## 测试工程（`tools/`）

| 工程 | 数量 | 覆盖 |
|---|---|---|
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
- 任何可部署源文件与 live 对应文件有差异时，**不得开始或接受实机测试**。（依据：流程）
- 本机 live 一直是开发版布局：编辑器专用的 `Assets`、`AssetSources`、`RuntimeDataCache`
  常驻 live 模块，随时能进 Modding Kit 继续改；发布后、实机测试前都不挪走。（依据：用户裁定）
  这三个目录不属于 `_Module` 的镜像范围，`Verify-LiveModule` 忽略它们。此布局下客户端加载
  `Assets` 而非 `AssetPackages`。发行版布局只存在于发布 ZIP 里（下节已排除这三个目录），
  不靠改动 live 来做。生成的 `bin` 与 `Shaders` 可以只存在于 live 模块。详见 `../reference/asset-pipeline.md`。
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


## v14 同号替换结果

原发布页 https://github.com/Lucicain/GW/releases/tag/v1.4-r14 已替换下载包并重新上线，ZIP 434464792 字节，SHA-256 `c5d8e2f4652d9eb55ead640bdb615b51b42ec52b13b5473e0d215dcc0bdb8e0a`；玩家 DLL `99f3c068373ce0a9c227cf42fb0a6c69bd1bdd8a23c23cc8a6a4ce0e53a46e8f`，版本 1.4.14。远端重新下载后 68 文件逐项哈希及五组监控串 ASCII/UTF-16LE 全 absent，开发阳性对照全 present。玩家 GameCompat/PREFLIGHT 606 类型/68 补丁、失败 0；43+39+23+11=116 项离线修复回归通过。包中三类开发目录、服务器资源、工具、日志与 PDB 均排除。

玩家美术继续使用原已验收 v14 TPAC，SHA-256 `bcaae1d2553d69bdb69960560848f7363f8cf2d1a44eeec9ee76e1e869516e09`，与旧用户包完全相同；未改动的 runtime 文件也逐字节保持。中英玩家说明与发布正文只写游戏体验，正文从 README 自动抽取。旧 ZIP/sha256 保留 `.codex_tmp/pre-refresh-v14-release-20261007/`；原/新发布证据分别在 `build-check/package-v1.4-r14-original/`、`build-check/package-v1.4-r14/`。

外发检查已生成本地 Vanguard/MELD/Greyscope 报告，技能封装器因旧 DACTYL 字段缺失报 KeyError，未称通过；内容另经人工事实与提示词泄漏核对。用户已切换完全访问，2026-10-07 确认 UI mode=full-access，随后会话权限更新为 danger-full-access / approval_policy=never，自动审批不再触发；agent 未修改用户配置或应用内部状态。后续本项目已授权操作直接完成，不重复询问。（依据：用户裁定 2026-10-07；会话权限）
