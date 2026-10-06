# 本地临时目录 `.codex_tmp` 的保留清单

`.codex_tmp/` 被 git 忽略：**里面的东西不在任何存档里，也不在云端**，只存在这台机器上。
它装的是调查过程的中间产物：反编译输出、旧 DLL 备份、一次性脚本、调试输出、截图。

清理它是 agent 的职责（见 AGENTS.md「诊断跟着功能走」）。**删任何东西之前先看本清单。**
清单外的东西默认可以在结论写进 state 或 journal 之后删除；清单内的不删。

**登记规则**：顶层每一项都要被下面某张表的路径（可用 `*` 通配）认领。`Check-StateFreshness.py` 会列出没人认领的项和大小，收尾时要么删、要么登记，不留无主文件。一次性脚本不进这里：用完即删，反复用得上的迁进 `tools/`。（依据：流程）

用户裁定老版本不再回退（2026-10-03）。

## 不删（不可再生）

| 路径 | 是什么 | 为什么留 | SHA-256 前 16 位 |
|---|---|---|---|
| `published-assets/2026-07-16_001310/pack0_newly_published_031DC443.tpac` | 2026-07-16 发布时的盾牌资产包 | 盾牌资源回滚点，与仓库里的正式包不同 | `031dc4430da668d1` |
| `published-assets/2026-07-16_001310/pack0_replaced_old_D14AE4B3.tpac` | 同次发布被替换掉的旧包 | 同上 | `d14ae4b3f8576f96` |
| `legacy-packages-before-rebuild-20260927/`（三个 `.tpac`） | 重建前的三个运行时资产包；继承包是 2024 年盔甲的唯一原件 | 重建资源的回滚点；`pack0.tpac` 出问题时放回这三个 | `957dd525945e3b18` 等，见 `asset-pipeline.md` |
| `ds-asset-packages-v1.4-r14/pack0.tpac` | 2026-10-02 Kit 专用服务器发布产出的服务器资源包 | 日后联机适配要用；不在仓库与 live 里 | `87106276b84aa3a8` |
| `pre-six-lod-package/gwp_black_gold_shield_single_stable.tpac` | 六级 LOD 之前的单 LOD 稳定版黑金盾 | 同上；六级 LOD 方案出问题时回到这里 | `1606cad209d02a33` |
| `exit-crash-2026092*-*.txt`（6 份） | 客户端退出时原生崩溃的 cdb 取证 | 该崩溃仍在观察，原始转储已删 | — |
| `crash-23108-*.txt`（4 份） | 崩溃 23108 的 cdb 分析输出 | 原始转储已删，这几份是唯一的取证结论 | `0a4c7ec6…` `89eaf4fa…` `d4207048…` `4aae2707…` |
| `latest-5000-monitor.txt`、`messenger-disabled.png`、`notification-*.png` | 7 月的监控原始日志与 UI 截图 | 当时问题的原始证据 | — |
| `pre-refresh-v14-release-20261007/` | v1.4-r14 同号替换前公开过的原 ZIP、sha256 与原发布页元数据 | 玩家下载过的原包，无法从源码逐字节重建 | `779cc90b99c82f58` |

（依据：流程——2026-09-11 起的既有保留规则，原写在 `maintenance-plan.md`，拆分后只剩 journal 里有；
2026-09-26 移到这里，因为 journal 不算规则。）

**资产包的正式副本**在 `GreyWardenPolicePurity/_Module/AssetPackages/`（也被 git 忽略，见
`asset-pipeline.md`）。上面三个 `.tpac` 不是它们的重复。

## 被 state 文件引用的备份（删之前先改 state）

| 路径 | 引用它的文件 |
|---|---|
| `choke-spacing-candidate-not-deployed-20260921` | `battle-tactics.md` 的已隔离候选表（从未部署的候选实现，不是回滚点） |

## 在用的工作文件

| 路径 | 用途 |
|---|---|
| `mb148-decomp/`、`core148-decomp/`、`campaignsystem-full-decompile/` | v1.4.8 反编译，见 `investigation-methods.md`；可用 ilspycmd 重建 |
| `check-native-persuasion-direct.ps1` | `investigation-methods.md` 引用的运行时反射先例 |
| `helper-mesh/` | 弓箭手替身 FBX 与预览、原版替身对照图（CSV 可用 `tpac-diagnose --dump-helpers` 重导） |
| `cloth-gradient/warcharmor.original.fbx` | 写渐变之前的弓箭手源 FBX |
| `dotnet-home/` | 构建用的 .NET CLI 主目录 |
| `ritual-media-20261007/` | 仪式声音生命周期与 Native 取证、真实 WinMM 静音离线测试壳；配图颜色已验收并移除贴图专项输出，声音确认后退休 |
| `rbm-current-decompile/`、`rbm-campaign-current-decompile/` | 本机 RBM 反编译，灰袍声望工资实现时要核对它的独立维护与升级限制（`warden-recruitment.md`）；可重建 |
| `bellumcivile-source/` | BellumCivile 1.4.0 公开源码，入国保护的依据（`kingdom-membership.md`）；可重新下载 |

## 新产生的中间产物

反编译输出、隔离构建、DLL 备份、日志等，结论写进 state 或 journal、对应改动提交后即可删，git 历史就是回退点。

## 已迁进仓库、这里的副本不再是唯一的

| 原位置 | 现位置 |
|---|---|
| `analyze_native_refs.py` | `tools/native/find-string-refs.py`（修复了漏报；旧脚本不要再用） |
| `tpac-diagnose/Program.cs` 与项目文件 | `tools/tpac/tpac-diagnose/` |
| `TpacTool-src/` 的两处本地修改 | `tools/tpac/TpacTool-local.patch`（重建方法见 `tools/tpac/README.md`） |

有反复用得上的脚本，照这样迁进 `tools/`，不要让方法只活在临时目录里。

## 本地分支

| 分支 | 是什么 | 处置 |
|---|---|---|
| `coop-bridge` | 联机适配停工时的归档（见 `code-health.md`） | 保留 |
