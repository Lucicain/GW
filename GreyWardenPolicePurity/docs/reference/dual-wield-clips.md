# 双刀动画片段配方

> 用途：在 Modding Kit 里重建双刀动画片段。骨骼动画可以从 FBX 导入，**片段（animation clip）不在 FBX 里**。
> 2026-09-27 起片段由 `tools/tpac/clip-gen` 直接生成（见 `tools/tpac/README.md`），24 个与原包逐字段核对一致；下面的表留作人工核对与以后修改的依据。
> 数据来源：`_Module/AssetPackages/gwp_dual_wield_animations.tpac`（2026-08-23 从 ROT 的 `pack0`–`pack7` 抽出的 64 个片段 + 4 段骨骼动画，见 journal 2026-08）；
> 用 `tools/tpac/tpac-export` 解析，清单在 `_Module/AssetSources/_reference/gwp_dual_wield_animations/manifest.json`。

## 骨骼动画（4 段）

源文件：`_Module/AssetSources/GreyWardenAnimations/*.fbx`，由 `tools/tpac/modkit-anim.py` 生成。骨架为原版 `human_skeleton`（28 根骨），
导入设置：只勾 Import skeletal animations，单位换算成 m，不转 Z-up；导入后在动画属性里把骨架指定为原版 `human_skeleton`
（只导入动画时 Kit 不会自动绑定）。2026-09-27 Kit 导入结果与原包逐帧比对：骨架相同，骨骼旋转 ≤ 0.08°（压缩精度），根位移 0.00 mm，帧数相同。

| FBX | 帧 | 原包名 |
|---|---|---|
| `dual_movement.fbx` | 0–456 | `human_skeleton_notused\|dual_movement` |
| `swing_right_ready_new.fbx` | 0–536 | `human_skeleton_notused\|swing_right_ready_new` |
| `swing_right_new.fbx` | 0–189 | `human_skeleton_notused\|swing_right_new` |
| `thrust_right_new.fbx` | 0–139 | `human_skeleton_notused\|thrust_right_new` |

另有 4 个片段用的是原版骨骼动画（表里标「原版」），不需要导入。

## 24 个手工片段

片段名必须与下面一致：`ModuleData/action_sets.xslt` 按这些名字引用。栏位名就是 Kit 片段编辑器上的名字；没列出的栏位保持默认（0 / 空 / 不勾）。
Source 1 大于 Source 2 表示倒放。Sample Rate 由 Kit 自动算，不用填。新建片段默认叫 `new_animation_clip`，**记得改名**。
2026-09-27 第一个片段 `dual_stand_1h` 照本表建好后与原包逐字段比对：除名字外全部相同（含 Flags、Loading Type=0）。

「Blends with action」「Continue to action」原包引用的是 ROT 的动作名（`act_dual_*`、`act_dual2_*`），我们的动作表里没有，编辑器因此报
`get_action_code_index failed`；下面已按「播放同一片段」换成我们的 `act_gwd*`。`act_release_thrust_1h` 等不带 `gwd` 的是原版动作，保持不变。
Flags 与 Clip usages 来自原包片段数据（TpacTool `AnimationClip.Flags` / `ClipUsages`）；Flags 的名字与原版 `AnimFlags` 枚举（`anf_*`）一致。

### `dual_run_forward_1h_with_hand_shield`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 0.8 |
| Source 1 | 300 |
| Source 2 | 324 |
| Param 1 | 0.4（不要点 Auto-Compute） |
| Step points X/Y/Z/W | 0.4 / 0.898039 / -1 / -1 |
| Left / Right hand pose | 3 / 3 |
| Blend in / out period | 0.4 / 0 |
| Flags（勾选） | make walk sound |
| Clip usages | 添加 `bip_mov_ik`：Loop displacement 3；Adjusting start 0.74 / 0.24；Snapping start 0.1 / 0.63；Snapping duration 0.19 / 0.17 |

### `dual_run_forward_1h_with_hand_shield_left_stance`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 0.8 |
| Source 1 | 330 |
| Source 2 | 354 |
| Param 1 | 0.4（不要点 Auto-Compute） |
| Step points X/Y/Z/W | 0.4 / 0.898039 / -1 / -1 |
| Left / Right hand pose | 3 / 3 |
| Blend in / out period | 0.4 / 0 |
| Flags（勾选） | make walk sound |
| Clip usages | 添加 `bip_mov_ik`：Loop displacement 3；Adjusting start 0.74 / 0.24；Snapping start 0.1 / 0.63；Snapping duration 0.19 / 0.17 |

### `dual_stand_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 4.5 |
| Source 1 | 2 |
| Source 2 | 134 |
| Param 1 | 0.25（不要点 Auto-Compute） |
| Priority | 1 |
| Left / Right hand pose | 3 / 3 |
| Blend in / out period | 0.5 / 0 |
| Flags（勾选） | cyclic, allow head movement |

### `dual_stand_1h_left_stance`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 4.5 |
| Source 1 | 151 |
| Source 2 | 283 |
| Param 1 | 0.25（不要点 Auto-Compute） |
| Priority | 1 |
| Left / Right hand pose | 3 / 3 |
| Blend in / out period | 0.5 / 0 |
| Flags（勾选） | cyclic, allow head movement |

### `dual_walk_forward_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 1.4 |
| Source 1 | 370 |
| Source 2 | 406 |
| Step points X/Y/Z/W | 0.4 / 0.898039 / -1 / -1 |
| Left / Right hand pose | 2 / 2 |
| Blend in / out period | 0.3 / 0 |
| Flags（勾选） | make walk sound |
| Clip usages | 添加 `bip_mov_ik`：Loop displacement 1.8；Adjusting start 0.65 / 0.2；Snapping start 0.1 / 0.65；Snapping duration 0.3 / 0.3 |

### `dual_walk_forward_1h_left_stance`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|dual_movement` |
| Duration | 1.4 |
| Source 1 | 420 |
| Source 2 | 456 |
| Step points X/Y/Z/W | 0.4 / 0.898039 / -1 / -1 |
| Left / Right hand pose | 2 / 2 |
| Blend in / out period | 0.3 / 0 |
| Flags（勾选） | make walk sound |
| Clip usages | 添加 `bip_mov_ik`：Loop displacement 1.8；Adjusting start 0.65 / 0.2；Snapping start 0.1 / 0.65；Snapping duration 0.3 / 0.3 |

### `ready_dual_slashleft_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_ready_new` |
| Duration | 4.5 |
| Source 1 | 135 |
| Source 2 | 268 |
| Priority | 10 |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `1h_others` |
| Blend in / out period | 0.3 / 0 |
| Flags（勾选） | client prediction, enable hand spring ik, cyclic, enforce root rotation, allow head movement, affected by movement |

### `ready_dual_thrust_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_ready_new` |
| Duration | 4.5 |
| Source 1 | 403 |
| Source 2 | 536 |
| Priority | 10 |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_1h_dual_others` |
| Blend in / out period | 0.6 / 0 |
| Flags（勾选） | client prediction, enable hand spring ik, cyclic, enforce root rotation, allow head movement, affected by movement, blend main item bone entitially |

### `quick_dual_blocked_slashleft_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1 |
| Source 1 | 180 |
| Source 2 | 82 |
| Priority | 15 |
| Blends with action | `act_gwd_quick_blocked_slashleft_1h_balanced` |
| Blends with animation | `quick_dual_blocked_slashleft_1h_balanced` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.08 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_dual_blocked_slashleft_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1 |
| Source 1 | 180 |
| Source 2 | 82 |
| Priority | 15 |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.08 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_dual_blocked_slashright_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1 |
| Source 1 | 180 |
| Source 2 | 82 |
| Priority | 15 |
| Blends with action | `act_gwd_blocked_slashright_1h_balanced` |
| Blends with animation | `quick_dual_blocked_slashright_1h_balanced` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.08 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |

### `quick_dual_blocked_slashright_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1 |
| Source 1 | 180 |
| Source 2 | 82 |
| Priority | 15 |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.08 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |

### `quick_release_dual2_slashright_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1.1 |
| Source 1 | 82 |
| Source 2 | 180 |
| Param 1 | 1.001（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.3 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_release_dual2_slashright_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1.1 |
| Source 1 | 82 |
| Source 2 | 180 |
| Param 1 | 0.914849（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right_balanced` |
| Blend in / out period | 0.3 / 0.35 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_release_dual_slashleft_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1.1 |
| Source 1 | 82 |
| Source 2 | 180 |
| Param 1 | 0.784226（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Blends with action | `act_gwd_quick_release_slashleft_1h_balanced` |
| Blends with animation | `quick_release_dual_slashleft_1h_balanced` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right` |
| Blend in / out period | 0.3 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_release_dual_slashleft_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|swing_right_new` |
| Duration | 1.1 |
| Source 1 | 82 |
| Source 2 | 180 |
| Param 1 | 0.781318（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_right_balanced` |
| Blend in / out period | 0.3 / 0.35 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_dual_blocked_thrust_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|thrust_right_new` |
| Duration | 1 |
| Source 1 | 139 |
| Source 2 | 2 |
| Priority | 15 |
| Blends with animation | `quick_dual_blocked_thrust_1h` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.05 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation, blend main item bone entitially |

### `quick_dual_blocked_thrust_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|thrust_right_new` |
| Duration | 1 |
| Source 1 | 139 |
| Source 2 | 2 |
| Priority | 15 |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.05 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation, blend main item bone entitially |

### `quick_release_dual_thrust_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|thrust_right_new` |
| Duration | 1.1 |
| Source 1 | 2 |
| Source 2 | 139 |
| Param 1 | 1.11793（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/pierce` |
| Blends with animation | `quick_release_dual_thrust_1h` |
| Continue to action | `act_quick_release_thrust_1h` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.3 / 0.3 |
| Flags（勾选） | client prediction, blend main item bone entitially |

### `quick_release_dual_thrust_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `human_skeleton_notused\|thrust_right_new` |
| Duration | 1.1 |
| Source 1 | 2 |
| Source 2 | 139 |
| Param 1 | 1.11793（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/pierce` |
| Continue to action | `act_quick_release_thrust_1h_balanced` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.3 / 0.3 |
| Flags（勾选） | client prediction, blend main item bone entitially |

### `quick_release_dual_slashright_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `（原版）anim_slashright_onehanded_unbalance_a` |
| Duration | 1.1 |
| Source 1 | 81 |
| Source 2 | 181 |
| Param 1 | 1.00106（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Blends with action | `act_gwd_quick_release_slashright_1h_balanced` |
| Blends with animation | `quick_release_dual_slashright_1h_balanced` |
| Continue to action | `act_gwd2_quick_release_slashright_1h` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `onehanded_right` |
| Blend in / out period | 0.3 / 0.2 |
| Flags（勾选） | client prediction, enforce root rotation |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `quick_release_dual_slashright_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `（原版）anim_slashright_onehanded_balance_a` |
| Duration | 1.1 |
| Source 1 | 21 |
| Source 2 | 121 |
| Param 1 | 0.914849（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/med` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `onehanded_right_balanced` |
| Blend in / out period | 0.3 / 0.35 |
| Flags（勾选） | client prediction, enforce root rotation, blend main item bone entitially |
| Loading Type（推断） | 原包该字段为 2，Kit 默认 `Always keep in memory` 存为 0；选下拉框第 3 项，建好后核对 |

### `release_dual_thrust_1h`

| 栏位 | 值 |
|---|---|
| Animation source | `（原版）anim_thrust_onehanded_new` |
| Duration | 1.3 |
| Source 1 | 1 |
| Source 2 | 140 |
| Param 1 | 1.11793（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/pierce` |
| Blends with animation | `release_dual_thrust_1h` |
| Continue to action | `act_release_thrust_1h` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.3 / 0.3 |
| Flags（勾选） | client prediction, enforce root rotation, blend main item bone entitially |

### `release_dual_thrust_1h_balanced`

| 栏位 | 值 |
|---|---|
| Animation source | `（原版）anim_thrust_onehanded_new` |
| Duration | 1.3 |
| Source 1 | 1 |
| Source 2 | 140 |
| Param 1 | 1.11793（不要点 Auto-Compute） |
| Priority | 10 |
| Step points X/Y/Z/W | 0.2 / -1 / -1 / -1 |
| Sound code | `event:/mission/combat/swing/pierce` |
| Continue to action | `act_release_thrust_1h_balanced` |
| Left / Right hand pose | 3 / 3 |
| Combat parameter id | `gwp_onehanded_dual_thrust` |
| Blend in / out period | 0.3 / 0.3 |
| Flags（勾选） | client prediction, enforce root rotation, blend main item bone entitially |

## 40 个数字名片段

名字形如 `16491159614761971101_0`…`_9` 的 40 个片段是混合片段：原包里由 4 个「混合片段」指向另一片段的片段（`quick_dual_blocked_slashleft/right_1h`、`quick_release_dual_slashleft/right_1h`）各派生 10 个（`GeneratedIndex` 0–9，
`ClipSource1/2` 指向原片段与被混合片段）。它们不在 `action_sets.xslt` 里被直接引用。**在 Kit 里打开父片段点 Save，Kit 就把 10 个派生片段写进父片段的 `_anm.tpac`**，
名字与原包完全相同（2026-09-27 确认）。与原包比，只有 `quick_dual_blocked_slashright_1h` 那组的一个未命名字段（疑似 Loading Type）为 0、原包为 2：
Kit 取父片段的值，而原包父片段本身就是 0。

## ROT 动作名 → 我们的动作名

| 原包里的引用 | 换成 | 依据（我们播放同一片段的动作） |
|---|---|---|
| `act_dual2_quick_release_slashright_1h` | `act_gwd2_quick_release_slashright_1h` | 片段 `quick_release_dual2_slashright_1h` |
| `act_dual_quick_release_slashright_1h_balanced` | `act_gwd_quick_release_slashright_1h_balanced` | 片段 `quick_release_dual_slashright_1h_balanced` |
| `act_dual_quick_blocked_slashright_1h_balanced` | `act_gwd_blocked_slashright_1h_balanced` | 片段 `quick_dual_blocked_slashright_1h_balanced` |
| `act_dual_quick_blocked_slashleft_1h_balanced` | `act_gwd_quick_blocked_slashleft_1h_balanced` | 片段 `quick_dual_blocked_slashleft_1h_balanced` |
| `act_dual_quick_release_slashleft_1h_balanced` | `act_gwd_quick_release_slashleft_1h_balanced` | 片段 `quick_release_dual_slashleft_1h_balanced` |
