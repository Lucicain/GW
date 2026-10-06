# 重建资源的布料设置

> 当前状态：三件服装与弓箭手下摆按用户认可的手感定稿（2026-10-03，非原包值）；弓箭手不用替身，直接模拟，下摆为竖向渐变
> 最后验收：2026-10-03 用户在 Kit 定稿三件服装与弓箭手布料（live `Assets`，缓存同步）
> 已复核至：`64e36ec`（2026-10-07）
> 覆盖源码：`tools/tpac/tpac-diagnose/Program.cs`
> 待实测：弓箭手下摆定稿手感（用户进游戏体验）

本页所说的“原包”是 `.codex_tmp/legacy-packages-before-rebuild-20260927/gwp_inherited_legacy_assets.tpac`。本机客户端使用 live 模组的 `Assets` 开发版资源；`_Module/AssetPackages/pack0.tpac` 是上次 Kit 发布结果，**不含 2026-10-03 定稿的三件服装修正**。再发布前应让 Kit 重新发布，并核对发布包。（依据：流程；2026-09-27 资产包离线核对）

## 只在 Kit 里改布料参数

- 不要用 TPAC 工具写布料参数或布料子网格数据，`--restore-cloth` / `--cloth-gradient` 只作离线实验，不写进 live。（依据：实机诊断 2026-10-03）
  9-27 工具写回三件服装的原包设置后，工具自己读回无误，但用户在 Kit 里打开模拟、什么都不调直接保存，这些子网格的拉伸/弯曲/剪切刚度就成了 0.5：
  `winfarmor`、`wcomarmorhv` 拉伸 0.8→0.5，`winfarmorhv` 三项 1→0.5；Kit 自己导入的原件
  与未经工具的 `warcharmor` 始终是正确值。工具直接写 TPAC 也不会更新 `RuntimeDataCache`，开发版会继续用旧缓存。
  顶点色等网格数据改源 FBX，再由 Kit 重导。（依据：实机诊断 2026-10-03；资源包读回对比）

## 定稿设置（用户认可，2026-10-03）

用户在 Kit 里按自己认可的手感调定，不再以原包为准。三件服装碰撞体是 `legs`，弓箭手是 `human_body`。（依据：用户裁定 2026-10-03）

| 子网格 | 预设 | 最大距离 | 拉伸 | 弯曲 | 剪切 | 阻尼 | 重力 | 惯性 | 空气阻力 | 风 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `winfarmor.1` | `horse_scale_armor` | 1 | 0.8 | 0.5 | 0.5 | 0.4 | 9.81 | 1 | 0.5 | 1 |
| `winfarmorhv.1` | `heavy_leather` | 1 | 0.8 | 0.5 | 0.5 | 0.82 | 9 | 0.1 | 0.1 | 0.1 |
| `winfarmorhv.3` | `horse_scale_armor` | 1 | 1 | 1 | 1 | 0.4 | 9.81 | 1 | 0.5 | 1 |
| `wcomarmorhv.1` | `horse_scale_armor` | 1 | 0.5 | 0.5 | 0.5 | 0.4 | 9.81 | 1 | 0.5 | 1 |
| `wcomarmorhv.3` | `horse_scale_armor` | 0.1 | 1 | 1 | 1 | 0.4 | 9.81 | 1 | 0.5 | 1 |
| `warcharmor.2` | `medium_leather` | 1 | 1 | 1 | 1 | 0.13 | 9.81 | 0.3 | 0.2 | — |

锚定刚度均 0.5。弓箭手下摆顶点色是 `tools/tpac/cloth-gradient-fbx.py` 写的竖向渐变（钉住点保持 0，其余从顶 0 到底 1），源文件 `warcharmor.fbx` `6e606081…`。下文「八块布料的当前设置」表中的预设与碰撞体仍对，刚度以本表为准。

## 替身网格（helper mesh，原版做法；目前未用）

用户裁定弓箭手不用替身（2026-10-03）：替身留作以后做性能优化的手段。生成脚本 `tools/tpac/make-cloth-helper.py` 按下列规则出替身，用时再跑。

- 原版大量衣服用替身：本体 Metamesh 的布料网格字段（TpacTool `ClothMetamesh`）指向另一个只有一块、只有 LOD0 的低模 Metamesh，
  例 `battanian_scale_armor_b` → `clo_battanian_scale_armor_b`（`meshes_shared_1.tpac`，288 点 / 512 面，带蒙皮，最大距离 0.75）。
  预处理布料数据（`b97e0f10-…`）在替身上；本体 LOD0 打布料开关，并带贴合数据（`af41613e-…`，TpacTool 的 `ClothMappingData`）。
- 模拟只在替身上算。本体顶点：alpha > 0 跟替身走，= 0 照常蒙皮；跟替身走的顶点位置 = 替身某三角形三点重心插值 + 沿替身法线的固定偏移
  （`Shaders/Sources/shared_vertex_functions.rsh` 的 `USE_MAPPED_CLOTH_SIMULATION` 分支）。所以本体 alpha 只是开关，摆动幅度画在替身的 alpha 上；
  多层布贴同一张替身，层间距保持不变。原版 13 个 `clo_*` 替身中 11 个画了渐变（例：36 点为 0、其余 9 级过渡、无 1）。
- 替身只能有一个子网格（引擎报错 "Simulation meshes for cloth simulation must have single submesh"）。本体可以多材质。
- 替身要紧贴本体，不得盖在本体外面。官方文档要求本体任何点都不穿到替身里侧；原版 57 件带替身的服装实测是贴着布面走（本体点到替身的距离中位数 0，前后 2–4 cm 内）。（依据：官方文档 moddocs.bannerlord.com/editor/resource-editors/cloth_simulation/；原版资源包统计 2026-10-03）
- 本体在两腿间开衩的，替身也要在开衩处断开，左右两半各自只从同侧的布取蒙皮；连成一片会让两条腿隔着替身互相拽。例：`sturgian_fortified_armor`、`veteran_mercenary_armor`、`sturgian_lamellar_gambeson`。另一块布（前片）跨过开衩的那一段，替身保持连着。（依据：原版资源包 2026-10-03）
- 替身权重量级：原版从顶上 0 按深度线性涨到底边，底边只有 9–70/255（铠甲下摆多在 28–48），布料最大距离仍是 1，即下摆最多离开蒙皮位置约 4–27 cm。不要把替身底边画到 255。（依据：原版资源包统计 2026-10-03）
- Kit 里替身挂在布料编辑器的 Simulation mesh 栏（不是预览用的 Helper mesh），最大距离与预设设在本体各行上，然后 Re-cook、Save mesh settings。
- 材质要支持贴合式模拟（报错 "Material %s does not support mapped cloth simulation"）。原版 `battanian_scale_armor_a` 与我们 `warchmatarcclo`
  同为着色器 `328d3572-…`；原版另有 `disable_vertex_color_alpha` 标志。（依据：原版资源包与着色器源码 2026-10-03；C++反汇编字符串）

## 引擎怎么用布料设置（v1.4.8）

- 布料在显卡计算着色器里模拟：`TaleWorlds.Native.dll` 中引用 `"Cloth simulation"` 的函数（`0x180100110`）经
  `ID3D11DeviceContext` 虚表 `+0x218`/`+0x220`/`+0x228`/`+0x148`（CSSetShaderResources / CSSetUnorderedAccessViews /
  CSSetShader / Dispatch）提交；求解器源码随游戏附带：`Shaders/Sources/gpucloth_solver.rs`。（依据：C++反汇编 2026-10-02；着色器源码）
- 顶点色 alpha 是**连续权重**：Kit 预处理把每个布料点的 alpha 存成 `alpha/255` 的浮点（包内 Metamesh 数据段类型 `b97e0f10-df49-4666-8f1d-ada6630d4b4e`，
  原包 `warcharmor` 有 0、1 与 27 种中间值；`wharness` 最大距离 0.1，存的仍是 0~1）。求解器每帧
  `max_distance = max_distances[i] * max_distance_multiplier`，把点限制在以骨骼蒙皮位置为球心、该半径的球内（`gpucloth_solver.rs` 第 698–702 行）。
  0 = 钉在蒙皮位置，1 = 可离开到子网格设的最大距离，中间值按比例。（依据：着色器源码；资源包数据 2026-10-02）
- 碰撞只针对碰撞体胶囊（`gpucloth_solver.rs` 第 389–394 行把点推出胶囊），不针对身上其他服装网格。原版 `Native/ModuleData/cloth_bodies.xml`：
  `human_body` 13 个胶囊，大腿半径 0.10、小腿 0.09/0.08、骨盆 0.12，还有手臂与脊柱；`legs` 只有 4 个腿部胶囊，大腿 0.15/0.17、小腿 0.14/0.10，
  比 `human_body` 的腿粗约一半。下摆穿进护腿等服装属这一机制的结果。（依据：原版 XML 2026-10-02）

## 弓箭手的结论

`warcharmor` 的下摆是只挡在身前的围裙：里层主布（下半段从 z≈0.84 起在两腿间分成左右两片），外层左侧一块米色前片，尖端搭在开衩上。
原包没有替身，现在也不用替身，直接模拟，设置见上方定稿表。试过的替身 `clo_warcharmor`（开衩处断开、底边 40/255）用户认为效果可以，
但不需要；已从仓库与 live 的 `AssetSources` 撤下，Kit 资源 `clo_warcharmor_geo.tpac` 用户已删。要恢复就用脚本重新生成（参数见 journal 2026-10-03）。
