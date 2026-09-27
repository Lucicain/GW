# 重建资源的布料设置

> 当前状态：2026-09-27 已把 live 的六件服装/马甲、八块布料的设置按重建前原包核对；三件服装的差异已修正并部署，待实机验收
> 最后验收：未建立检查点
> 已复核至：`34cc6b0` 后的 live `Assets/GreyWardenRebuild/*_geo.tpac`
> 覆盖源码：`tools/tpac/tpac-diagnose/Program.cs`
> 待实测：弓箭手衣摆，以及轻步兵、重步兵、骑士和马披的布料观感；改动三件服装后尚未进游戏

本页所说的“原包”是 `.codex_tmp/legacy-packages-before-rebuild-20260927/gwp_inherited_legacy_assets.tpac`。本机客户端使用 live 模组的 `Assets` 开发版资源；`_Module/AssetPackages/pack0.tpac` 是上次 Kit 发布结果，**尚未包含本轮三件服装的修正**。再发布前应让 Kit 重新发布，并核对发布包。live 三份修改前的原件在 `.codex_tmp/cloth-restore-20260927/before/`，可逐文件回退。（依据：流程；2026-09-27 资产包离线核对）

## 弓箭手的结论

`warcharmor.2` 在原包与当前 live 都直接启用 `uses_cloth_simulation`，预设都是 `medium_leather`，完整的 11 项布料参数逐值相同，最大距离字段都是 1，碰撞体都是 `human_body`。原包与重建包的 `ClothMetamesh` 都是空 GUID，没有单独的 Simulation mesh；Cloth Editor 截图的 Helper mesh 也是 `None`。原包并没有以简化的独立模型代替弓箭手衣服模拟。（依据：2026-09-27 原包与 live TPAC 元数据；用户截图）

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
