# 构建、校验与部署

> 当前状态：现行流程
> 最后验收：`131abb0`（v1.4-r12，含远端下载核验）
> 覆盖源码：`GreyWardenPolicePurity.csproj` `tools/*.ps1`
> 待实测：无

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
| `Verify-LiveModule.ps1` | **双向**比对 live 测试模块与仓库模块 | 无缺失、无差异、无散落文件；两份玩家 README 一致 |
| `Verify-ContentKeys.ps1` | XML / 字面量 / 生成的本地化键，只读 | `MISSING=0`。未被引用的字符串**不是死内容的证据，永不删除** |
| `Verify-CrimeReceipts.ps1` | 犯罪回执检查，只读，不打开或修改存档 | PASS |
| `Watch-GreyWardenAI.ps1` | 实机 AI 诊断日志跟看 | — |
| `Generate-CodeMap.py` | 从源码重建 `docs/state/code-map.md`；PostToolUse 钩子自动跑 | 不要手改产物（依据：流程） |
| `Check-StateFreshness.py` | 报 state 过期、新增未归属源码、改到未提取子系统、state 堆积流水、豁免数 | 被点名的当轮处理 |

脚本用 Windows PowerShell 5.1 运行。

## 测试工程（`tools/`）

| 工程 | 数量 | 覆盖 |
|---|---|---|
| `BattleSceneTests` | 138 | 援军三层/预算/Origin/原生耗尽 + 死战不退 18 项 |
| `DualBladeTests` | 18 | 双刀 AI 与踢/盾击互斥 |
| `MusicTests` | 58770640 断言（多为逐采样） | FLAC 逐位无损、Wwise 曲线与片段包络、输出限制器、升档节奏、过渡规则、静音设备生命周期。`--fingerprint` 固定场景指纹；`--compare-old` 与旧预渲染对照；`--dump` 导出混音测响度 |
| `CaseSettlement.Tests` | 164 | 案件结算 |
| `ChokePointTests` | — | 隘口候选已撤回，其 bin/obj 仅为被忽略的生成缓存，不参与构建或部署 |

测试都是引擎边界桩：**不验证真实战场位置、AI 行军、原生物理或游戏内观感。**

## live 镜像是硬要求

`_Module` 下的可部署运行时文件是开发期事实来源。改动后**立即**复制同一版本到
`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`。

- 这条与 Git 发布无关，工作树可以一直未提交。
- 每次部署后**比对哈希**，不要假定复制成功。（依据：流程）
- 任何可部署源文件与 live 对应文件有差异时，**不得开始或接受实机测试**。（依据：流程）
- 例外：编辑器专用的 `Assets`、`AssetSources`、`RuntimeDataCache` 不进普通客户端
  live 模块，否则客户端不加载 `AssetPackages`。生成的 `bin` 与 `Shaders` 可以只存在
  于 live 模块。
- 单文件改动（如新增中文键）先单文件镜像并比 hash，再随构建全量同步。

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
  `Assets`、`AssetSources`、`RuntimeDataCache`、`bin/Win64_Shipping_wEditor/`、
  Client `.pdb`、`shader_compile_report.log`。
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
