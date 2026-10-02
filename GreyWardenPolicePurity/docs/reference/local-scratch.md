# 本地临时目录 `.codex_tmp` 的保留清单

`.codex_tmp/` 被 git 忽略：**里面的东西不在任何存档里，也不在云端**，只存在这台机器上。
它装的是调查过程的中间产物：反编译输出、旧 DLL 备份、一次性脚本、调试输出、截图。

清理它是 agent 的职责（见 AGENTS.md「诊断跟着功能走」）。**删任何东西之前先看本清单。**
清单外的东西默认可以在结论写进 state 或 journal 之后删除；清单内的不删。

2026-09-26 盘点：387 项、约 470 MB，其中只有下表这些是不可再生的。

## 不删（不可再生）

| 路径 | 是什么 | 为什么留 | SHA-256 前 16 位 |
|---|---|---|---|
| `published-assets/2026-07-16_001310/pack0_newly_published_031DC443.tpac` | 2026-07-16 发布时的盾牌资产包 | 盾牌资源回滚点，与仓库里的正式包不同 | `031dc4430da668d1` |
| `published-assets/2026-07-16_001310/pack0_replaced_old_D14AE4B3.tpac` | 同次发布被替换掉的旧包 | 同上 | `d14ae4b3f8576f96` |
| `legacy-packages-before-rebuild-20260927/`（三个 `.tpac`） | 重建前的三个运行时资产包；继承包是 2024 年盔甲的唯一原件 | 重建资源的回滚点；`pack0.tpac` 出问题时放回这三个 | `957dd525945e3b18` 等，见 `asset-pipeline.md` |
| `ds-asset-packages-v1.4-r14/pack0.tpac` | 2026-10-02 Kit 专用服务器发布产出的服务器资源包 | 日后联机适配要用；不在仓库与 live 里 | `87106276b84aa3a8` |
| `pre-six-lod-package/gwp_black_gold_shield_single_stable.tpac` | 六级 LOD 之前的单 LOD 稳定版黑金盾 | 同上；六级 LOD 方案出问题时回到这里 | `1606cad209d02a33` |
| `crash-23108-analysis.txt` / `-contact.txt` / `-function.txt` / `-threads.txt` | 崩溃 23108 的 cdb 分析输出 | 原始转储已删，这几份是唯一的取证结论 | `0a4c7ec6…` `89eaf4fa…` `d4207048…` `4aae2707…` |
| `latest-5000-monitor.txt`、`messenger-disabled.png`、`notification-encyclopedia.png`、`notification-issue.png` | 7 月的监控原始日志与 UI 截图 | 当时问题的原始证据 | — |

（依据：流程——2026-09-11 起的既有保留规则，原写在 `maintenance-plan.md`，拆分后只剩 journal 里有；
2026-09-26 移到这里，因为 journal 不算规则。）

**资产包的正式副本**在 `GreyWardenPolicePurity/_Module/AssetPackages/`（也被 git 忽略，见
`asset-pipeline.md`）。上面三个 `.tpac` 不是它们的重复。

## 被 state 文件引用的回滚备份（删之前先改 state）

| 路径 | 引用它的文件 |
|---|---|
| `horse-ownership-prechange-20260925` | `troop-combat.md` 的回退说明（骑乘效果从 `gwknight` 转到 `gw_warhorse` 之前） |
| `weapon-traits-prechange-20260925` | `troop-combat.md` 的回退说明（武器词条之前） |
| `choke-spacing-candidate-not-deployed-20260921` | `battle-tactics.md` 的已隔离候选表 |
| `cloth-restore-20260927/before/` | `cloth-assets.md` 的三件服装 live 布料设置修改前的唯一副本 |

对应功能验收并提交后，这些二进制备份就被 git 历史取代了。那时先把 state 里的回退说明
改成指向提交，再删目录。

## 可以重新生成

- **反编译输出**（`*decomp*`、`*decompile*`、`il-*`、`game-decomp` 等，约 90 MB）：用 ilspycmd 随时重建，
  见 `investigation-methods.md`。当前在用的是 v1.4.8 的 `mb148-decomp/`、`core148-decomp/`、
  `campaignsystem-full-decompile/`；其余多是早期或重复的。删前先确认调查结论已写进 state。
- **隔离构建与 DLL 备份**（`before-*`、`*-disabled-*`、`*isolated*`）：对应改动已提交的，
  git 历史就是回退点，备份可删。

## 已迁进仓库、这里的副本不再是唯一的

| 原位置 | 现位置 |
|---|---|
| `analyze_native_refs.py` | `tools/native/find-string-refs.py`（修复了漏报；旧脚本不要再用） |
| `tpac-diagnose/Program.cs` 与项目文件 | `tools/tpac/tpac-diagnose/` |
| `TpacTool-src/` 的两处本地修改 | `tools/tpac/TpacTool-local.patch`（重建方法见 `tools/tpac/README.md`） |

有反复用得上的脚本，照这样迁进 `tools/`，不要让方法只活在临时目录里。
