# 双刀动画片段配方

> 用途：在 Modding Kit 里重建双刀动画片段。骨骼动画可以从 FBX 导入，**片段（animation clip）不在 FBX 里**，只能照这张表在 Kit 里逐个建。
> 数据来源：`_Module/AssetPackages/gwp_dual_wield_animations.tpac`（2026-08-23 从 ROT 的 `pack0`–`pack7` 抽出的 64 个片段 + 4 段骨骼动画，见 journal 2026-08）；
> 用 `tools/tpac/tpac-export` 解析，清单在 `_Module/AssetSources/_reference/gwp_dual_wield_animations/manifest.json`。

## 骨骼动画（4 段）

源文件：`_Module/AssetSources/GreyWardenAnimations/*.fbx`，由 `tools/tpac/modkit-anim.py` 生成。骨架为原版 `human_skeleton`（28 根骨），
导入设置：只勾 Import skeletal animations，单位换算成 m，不转 Z-up；导入后在动画属性里把骨架指定为原版 `human_skeleton`
（只导入动画时 Kit 不会自动绑定）。2026-09-27 Kit 导入结果与原包逐帧比对：骨架相同，骨骼旋转 ≤ 0.08°（压缩精度），根位移 0.00 mm，帧数相同。

| FBX | 帧 | 原包名 |
|---|---|---|
| `dual_movement.fbx` | 0–456 | `human_skeleton_notused|dual_movement` |
| `swing_right_ready_new.fbx` | 0–536 | `human_skeleton_notused|swing_right_ready_new` |
| `swing_right_new.fbx` | 0–189 | `human_skeleton_notused|swing_right_new` |
| `thrust_right_new.fbx` | 0–139 | `human_skeleton_notused|thrust_right_new` |

另有 4 个片段用的是原版骨骼动画（表里标「原版」），不需要导入。

## 24 个手工片段

片段名必须与表中一致：`ModuleData/action_sets.xslt` 按这些名字引用。起始帧大于结束帧表示倒放。
「混合片段」「混合动作」「后续动作」里原先引用的是 ROT 的动作名（`act_dual_*`、`act_dual2_*`），我们的动作表里没有，
编辑器因此报 `get_action_code_index failed`；表里已按「播放同一片段」的对应关系换成我们的 `act_gwd*` 动作。
`act_release_thrust_1h` 等不带 `gwd` 的是原版动作，保持不变。

列名来自 TpacTool 的字段：「时长」`Duration`、「优先级」`Priority`、「步点」`StepPoints`、「音效」`SoundCode`、「混入/混出」`BlendIn/OutPeriod`、
「手势」`Left/RightHandPose`、「战斗参数」`CombatParameterId`、「混合动作」`BlendsWithAction`、「后续动作」`ContinueWithAction` 含义明确；
`Param1` 与「混合片段」（TpacTool 的 `UnknownClipName`）是推测的名字，在 Kit 界面上对应哪一栏要建第一个片段时确认。

| 片段名 | 骨骼动画 | 起始帧 → 结束帧 | 时长 | 优先级 | Param1 | 步点 | 音效 | 混入/混出 | 手势 左/右 | 战斗参数 | 混合片段 | 混合动作 | 后续动作 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `dual_run_forward_1h_with_hand_shield` | dual_movement | 300 → 324 | 0.8 | 0 | 0.4 | 0.4, 0.898039 | — | 0.4 / 0 | 3 / 3 | — | — | — | — |
| `dual_run_forward_1h_with_hand_shield_left_stance` | dual_movement | 330 → 354 | 0.8 | 0 | 0.4 | 0.4, 0.898039 | — | 0.4 / 0 | 3 / 3 | — | — | — | — |
| `dual_stand_1h` | dual_movement | 2 → 134 | 4.5 | 1 | 0.25 | — | — | 0.5 / 0 | 3 / 3 | — | — | — | — |
| `dual_stand_1h_left_stance` | dual_movement | 151 → 283 | 4.5 | 1 | 0.25 | — | — | 0.5 / 0 | 3 / 3 | — | — | — | — |
| `dual_walk_forward_1h` | dual_movement | 370 → 406 | 1.4 | 0 | 0 | 0.4, 0.898039 | — | 0.3 / 0 | 2 / 2 | — | — | — | — |
| `dual_walk_forward_1h_left_stance` | dual_movement | 420 → 456 | 1.4 | 0 | 0 | 0.4, 0.898039 | — | 0.3 / 0 | 2 / 2 | — | — | — | — |
| `ready_dual_slashleft_1h` | swing_right_ready_new | 135 → 268 | 4.5 | 10 | 0 | — | — | 0.3 / 0 | 3 / 3 | 1h_others | — | — | — |
| `ready_dual_thrust_1h` | swing_right_ready_new | 403 → 536 | 4.5 | 10 | 0 | — | — | 0.6 / 0 | 3 / 3 | gwp_1h_dual_others | — | — | — |
| `quick_dual_blocked_slashleft_1h` | swing_right_new | 180 → 82 | 1 | 15 | 0 | — | — | 0.08 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | `quick_dual_blocked_slashleft_1h_balanced` | act_gwd_quick_blocked_slashleft_1h_balanced | — |
| `quick_dual_blocked_slashleft_1h_balanced` | swing_right_new | 180 → 82 | 1 | 15 | 0 | — | — | 0.08 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | — | — | — |
| `quick_dual_blocked_slashright_1h` | swing_right_new | 180 → 82 | 1 | 15 | 0 | — | — | 0.08 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | `quick_dual_blocked_slashright_1h_balanced` | act_gwd_blocked_slashright_1h_balanced | — |
| `quick_dual_blocked_slashright_1h_balanced` | swing_right_new | 180 → 82 | 1 | 15 | 0 | — | — | 0.08 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | — | — | — |
| `quick_release_dual2_slashright_1h` | swing_right_new | 82 → 180 | 1.1 | 10 | 1.001 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | — | — | — |
| `quick_release_dual2_slashright_1h_balanced` | swing_right_new | 82 → 180 | 1.1 | 10 | 0.914849 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.35 | 3 / 3 | gwp_onehanded_dual_right_balanced | — | — | — |
| `quick_release_dual_slashleft_1h` | swing_right_new | 82 → 180 | 1.1 | 10 | 0.784226 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.2 | 3 / 3 | gwp_onehanded_dual_right | `quick_release_dual_slashleft_1h_balanced` | act_gwd_quick_release_slashleft_1h_balanced | — |
| `quick_release_dual_slashleft_1h_balanced` | swing_right_new | 82 → 180 | 1.1 | 10 | 0.781318 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.35 | 3 / 3 | gwp_onehanded_dual_right_balanced | — | — | — |
| `quick_dual_blocked_thrust_1h` | thrust_right_new | 139 → 2 | 1 | 15 | 0 | — | — | 0.05 / 0.2 | 3 / 3 | gwp_onehanded_dual_thrust | `quick_dual_blocked_thrust_1h` | — | — |
| `quick_dual_blocked_thrust_1h_balanced` | thrust_right_new | 139 → 2 | 1 | 15 | 0 | — | — | 0.05 / 0.2 | 3 / 3 | gwp_onehanded_dual_thrust | — | — | — |
| `quick_release_dual_thrust_1h` | thrust_right_new | 2 → 139 | 1.1 | 10 | 1.11793 | 0.2 | event:/mission/combat/swing/pierce | 0.3 / 0.3 | 3 / 3 | gwp_onehanded_dual_thrust | `quick_release_dual_thrust_1h` | — | act_quick_release_thrust_1h |
| `quick_release_dual_thrust_1h_balanced` | thrust_right_new | 2 → 139 | 1.1 | 10 | 1.11793 | 0.2 | event:/mission/combat/swing/pierce | 0.3 / 0.3 | 3 / 3 | gwp_onehanded_dual_thrust | — | — | act_quick_release_thrust_1h_balanced |
| `quick_release_dual_slashright_1h` | anim_slashright_onehanded_unbalance_a（原版） | 81 → 181 | 1.1 | 10 | 1.00106 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.2 | 3 / 3 | onehanded_right | `quick_release_dual_slashright_1h_balanced` | act_gwd_quick_release_slashright_1h_balanced | act_gwd2_quick_release_slashright_1h |
| `quick_release_dual_slashright_1h_balanced` | anim_slashright_onehanded_balance_a（原版） | 21 → 121 | 1.1 | 10 | 0.914849 | 0.2 | event:/mission/combat/swing/med | 0.3 / 0.35 | 3 / 3 | onehanded_right_balanced | — | — | — |
| `release_dual_thrust_1h` | anim_thrust_onehanded_new（原版） | 1 → 140 | 1.3 | 10 | 1.11793 | 0.2 | event:/mission/combat/swing/pierce | 0.3 / 0.3 | 3 / 3 | gwp_onehanded_dual_thrust | `release_dual_thrust_1h` | — | act_release_thrust_1h |
| `release_dual_thrust_1h_balanced` | anim_thrust_onehanded_new（原版） | 1 → 140 | 1.3 | 10 | 1.11793 | 0.2 | event:/mission/combat/swing/pierce | 0.3 / 0.3 | 3 / 3 | gwp_onehanded_dual_thrust | — | — | act_release_thrust_1h_balanced |

## 40 个数字名片段

名字形如 `16491159614761971101_0`…`_9` 的 40 个片段是混合片段：原包里由 4 个「混合片段」指向另一片段的片段（`quick_dual_blocked_slashleft/right_1h`、`quick_release_dual_slashleft/right_1h`）各派生 10 个（`GeneratedIndex` 0–9，
`ClipSource1/2` 指向原片段与被混合片段）。它们不在 `action_sets.xslt` 里被直接引用，预期由 Kit 按「混合片段」设置自动生成；
重建后要核对是否生成。

## ROT 动作名 → 我们的动作名

| 原包里的引用 | 换成 | 依据（我们播放同一片段的动作） |
|---|---|---|
| `act_dual2_quick_release_slashright_1h` | `act_gwd2_quick_release_slashright_1h` | 片段 `quick_release_dual2_slashright_1h` |
| `act_dual_quick_release_slashright_1h_balanced` | `act_gwd_quick_release_slashright_1h_balanced` | 片段 `quick_release_dual_slashright_1h_balanced` |
| `act_dual_quick_blocked_slashright_1h_balanced` | `act_gwd_blocked_slashright_1h_balanced` | 片段 `quick_dual_blocked_slashright_1h_balanced` |
| `act_dual_quick_blocked_slashleft_1h_balanced` | `act_gwd_quick_blocked_slashleft_1h_balanced` | 片段 `quick_dual_blocked_slashleft_1h_balanced` |
| `act_dual_quick_release_slashleft_1h_balanced` | `act_gwd_quick_release_slashleft_1h_balanced` | 片段 `quick_release_dual_slashleft_1h_balanced` |
