# 重建资源的布料设置

> 当前状态：六件服装/马甲、八块布料按重建前原包设置；用户实测确认重建资源与马甲正常（2026-10-02）
> 最后验收：2026-10-02 用户实测（live 开发版）
> 已复核至：2026-10-02 r14 发布包 `pack0.tpac`（`bcaae1d2…`）与 live `Assets`：弓箭手衣服的布料、蒙皮、顶点色与材质
> 覆盖源码：`tools/tpac/tpac-diagnose/Program.cs`
> 待实测：无

本页所说的“原包”是 `.codex_tmp/legacy-packages-before-rebuild-20260927/gwp_inherited_legacy_assets.tpac`。本机客户端使用 live 模组的 `Assets` 开发版资源；`_Module/AssetPackages/pack0.tpac` 是上次 Kit 发布结果，**尚未包含本轮三件服装的修正**。再发布前应让 Kit 重新发布，并核对发布包。live 三份修改前的原件在 `.codex_tmp/cloth-restore-20260927/before/`，可逐文件回退。（依据：流程；2026-09-27 资产包离线核对）

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

`warcharmor.2` 在原包与当前 live 都直接启用 `uses_cloth_simulation`，预设都是 `medium_leather`，完整的 11 项布料参数逐值相同，最大距离字段都是 1，碰撞体都是 `human_body`。原包与重建包的 `ClothMetamesh` 都是空 GUID，没有单独的 Simulation mesh；Cloth Editor 截图的 Helper mesh 也是 `None`。原包并没有以简化的独立模型代替弓箭手衣服模拟。（依据：2026-09-27 原包与 live TPAC 元数据；用户截图）

2026-10-02 用户实机觉得弓箭手衣服物理与旧版不同，再核对 r14 发布包与 live `Assets` 的 `warcharmor`：布料参数、三角面连接、
锚定 alpha、碰撞体（`--verify-cloth` PASS）；布料块 357 个点的蒙皮骨骼与权重逐点相同（最大差 6×10⁻⁸）；两套顶点色全部通道相同；
三个材质 `warchmatarc`/`warchmatarcclo`/`warchmatobsh` 的着色器、混合、透明测试、标志与参数相同。弓箭手全套只有 `warcharmor.2`
一块布料（LOD0），新旧一致。数据层面没有找到差异来源。（依据：2026-10-02 `tpac-diagnose --verify-cloth` / `--compare-skin` / `--compare-material`）

布料块在两包里均为 357 个位置点、663 个三角面。按空间位置一一匹配后，三角面连接与每个位置点的顶点色 alpha 均相同；最大位置偏差约 `1.4×10⁻⁷`。因此没有发现能解释弓箭手衣摆观感差异的**布料配置或模拟网格**差异；游戏内效果仍需实机观察。（依据：2026-09-27 `tpac-diagnose --compare`）

## 八块布料的当前设置

| 模型 | live 布料子网格 | 预设 | 最大距离字段 | 碰撞体 | 与原包设置 |
|---|---|---|---:|---|---|
| `winfarmor` | `.1`（原包 `.2`，Kit 改了子网格顺序） | `horse_scale_armor` | 1 | `legs` | 已一致 |
| `warcharmor` | `.2` | `medium_leather` | 1 | `human_body` | 原本一致，未改 |
| `wcomarmorhv` | `.1` / `.3` | `horse_scale_armor` | 1 / 0.1 | `legs` | 已一致 |
| `winfarmorhv` | `.1` / `.3` | `heavy_leather` | 1 / 1 | `legs` | 已一致 |
| `wharness` | `.1` | `horse_scale_armor` | 0.1 | `horse_body` | 原本一致，未改 |
| `wharnesscom` | `.1` | `horse_scale_armor` | 0.1 | `horse_body` | 原本一致，未改 |

八块布料的位置点都能与原包在 `1e-5` 范围内一一配对；以配对后的位置点编号比较，三角面连接与逐位置点的顶点色 alpha 均无差异，最大最近点距离小于 `1.9×10⁻⁷`。部分模型的**渲染顶点数**不同，来自导入时焊接 UV 接缝；不能把按顶点序号的差异误报成模拟网格变化。（依据：2026-09-27 `tpac-diagnose --compare`）

本轮用原包字段修正 live 的三处差异：`winfarmor` 碰撞体从空值回 `legs`，`wcomarmorhv` 碰撞体从 `human_body` 回 `legs`、`.1` 最大距离从 0.1 回 1，`winfarmorhv` 碰撞体从 `human_body` 回 `legs`。工具保留原包预设的全部数值，修改前后 18/24/24 个子网格的位置、法线、UV、切线、顶点色与面索引对比均无差异；live 与临时生成文件的 SHA-256 逐个相同。（依据：2026-09-27 离线诊断与哈希）
