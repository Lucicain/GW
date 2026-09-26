# TPAC 诊断工具

`tpac-diagnose` 读取 Bannerlord 的 `.tpac` 资产包，用于排查盾牌等资产的发布问题
（背景见 `GreyWardenPolicePurity/docs/reference/asset-pipeline.md`）。

它依赖开源库 [TpacTool](https://github.com/szszss/TpacTool) 的 `TpacTool.Lib`，
并且需要两处本地修改（`TpacTool.Lib/Data/ExternalLoader.cs`、`TpacTool.Lib/Package/AssetPackage.cs`）。
这两样原先只存在于被 git 忽略的 `.codex_tmp/` 里，2026-09-26 迁到这里。

## 重建

```bash
cd tools/tpac
git clone https://github.com/szszss/TpacTool.git TpacTool-src
cd TpacTool-src
git checkout b56b77a
git apply ../TpacTool-local.patch
cd ../tpac-diagnose
dotnet build
```

`TpacTool-src/` 被 `.gitignore` 忽略，只提交补丁。补丁已在干净的 `b56b77a` 上验证可以应用。
需要 .NET 6 SDK。

## 从 TPAC 重建可编辑源资源

`tpac-export` 把模组的 `.tpac` 解析回 Modding Kit 能导入的源文件，`fbx-verify` 把导出的 FBX
读回来核对。两者依赖上面同一份 `TpacTool-src/`，构建方法相同（`dotnet build -c Release`）。

```bash
N="D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/AssetPackages"
M=GreyWardenPolicePurity/_Module
dotnet tools/tpac/tpac-export/bin/Release/net6.0/tpac-export.dll \
  "$M/AssetSources/GreyWardenRebuild" "$M/AssetSources/_reference" \
  "$N/core_game.tpac" "$N/skeletons.tpac" \
  "$M/AssetPackages/gwp_inherited_legacy_assets.tpac" \
  "$M/AssetPackages/gwp_black_gold_shield.tpac" \
  "$M/AssetPackages/gwp_dual_wield_animations.tpac"
dotnet tools/tpac/fbx-verify/bin/Release/net6.0/fbx-verify.dll \
  "$M/AssetSources/GreyWardenRebuild/models" "$M/AssetSources/_reference/gwp_inherited_legacy_assets/manifest.json"
dotnet tools/tpac/fbx-verify/bin/Release/net6.0/fbx-verify.dll --obj2fbx \
  "$M/AssetSources/_reference/gwp_inherited_legacy_assets/physics" "$M/AssetSources/GreyWardenRebuild/physics"
```

- 骨骼取自原版：人体 `human_skeleton` 在 `core_game.tpac`，马 `horse_skeleton` 在 `skeletons.tpac`。
- TpacTool 的 `AssetManager.AddPackage` 不建资产索引，所以程序自带一个按 GUID 查全部资产的解析器；
  否则模型的材质引用解析不出来，FBX 导出会失败。
- 每个子网格的材质只填在两个槽之一，导出前把空槽补成另一个槽的值（只改内存，不写回 TPAC）。
- FBX 里的贴图路径写成 `../textures/<名字>.png`，指向旁边的贴图目录；只写文件名的话 Blender 找不到贴图，材质显示成紫色。
- 贴图文件名一律小写（Kit 用文件名当贴图名）。把大小写混合的贴图（`WInfMatObsh_d`）放进材质槽，材质编辑器会空指针崩溃；
  改成小写重新导入后，用户在 Kit 里给材质槽选贴图不再崩溃（2026-09-27 实机确认）。原版 1862 个材质名与黑金盾贴图也都是全小写。
- DXT1 贴图（`_d`、`_s`）导出成 RGB，不带 alpha。带全 255 的 alpha 时，Kit 会按 DXT5 / `B8G8R8A8_UNORM` 导入，
  往材质槽里放这种贴图会让材质编辑器空指针崩溃（2026-09-27）；黑金盾的同类贴图是 DXT1 / `B8G8R8`。
- BC5 法线贴图只存 X、Y，导出的 PNG 蓝色通道是 0；程序按 Z = √(1−X²−Y²) 补回蓝色通道，否则 Blender 里明暗全错。
- **导出后还要过一遍 `modkit-fbx.py`**（用 Blender 后台跑）。`tpac-export` 出来的 FBX 是一个子网格一个物体
  （`winfarmor.0`、`winfarmor.lod1.0` …），这些后缀是 Modding Kit 按材质拆分时自己生成的；它要的源文件是
  每级 LOD 一个带多个材质槽的物体，命名 `<名字>`、`<名字>.lod1` … `.lod5`（与黑金盾当初导入成功的规则一致）。
  脚本还把碰撞体 OBJ 转成 FBX，坐标轴按黑金盾的做法导出（Y 前、Z 上、不加末端骨骼）：

  ```bash
  B="D:/steam/steamapps/common/Blender/blender.exe"
  R="$M/AssetSources/GreyWardenRebuild"
  "$B" -b --factory-startup --python tools/tpac/modkit-fbx.py -- "$M/AssetSources/_reference/raw-fbx" "$R" --tex "$(cygpath -w "$R")"
  "$B" -b --factory-startup --python tools/tpac/modkit-fbx.py -- "$M/AssetSources/_reference/gwp_inherited_legacy_assets/physics" "$R"
  ```

  **布局**：FBX 与 PNG 放在同一个文件夹，FBX 里贴图只记文件名。`AssetSources` 下分多级子文件夹也可以（用户 2026-09-27 确认），
  现在用一层只是习惯。`tpac-export` 之后要：把 `GreyWardenRebuild/models/*.fbx` 挪到 `_reference/raw-fbx/`、
  `textures/*.png` 挪到转换输出的文件夹、`animations/` 挪到 `_reference/animations-fbx/`，再跑上面两条。
  FBX 里贴图只记文件名。
  转换后的大盾与当初成功导入的 `GreyWardenRecovery/dun.fbx` 包围盒逐位相同，说明轴向和单位没变。
- **布料块要焊接位置点**：`tpac-export` 给每个顶点单独一个位置，原包的布料网格在 UV 接缝两侧共用位置点。
  布料模拟靠共用位置点把网格连起来，不焊就会沿接缝散开、整片掉落（2026-09-27 用户在 Cloth Editor 里看到）。
  `modkit-fbx.py` 只对材质名以 `clo` 结尾的面做精确重合（1e-6）焊接：48 个布料块的位置点数与原包逐个相同，
  非布料部分不动（全焊会把原包故意分开的硬边和蒙皮接缝也合掉），法线、UV、布料权重（顶点色 alpha）、骨骼权重逐角核对无差异。
- `fbx-verify` 只比网格数、面数、材质名；顶点数会比原包少 1%～5%，因为 FBX 只带第一套 UV、
  不带切线，Assimp 读回时把这些属性相同的顶点合并了。

## 动画

`modkit-anim.py`（Blender 后台）把 `tpac-export` 写出的动画 FBX 转成 Kit 能导入的文件：只留骨架（原版 `human_skeleton`，28 根骨），
骨架对象改名 `human_skeleton_notused`、动作按原 take 命名，导入时不做 Blender 默认的 1 帧偏移，逐帧烘焙、不简化，轴向同模型。

```bash
"$B" -b --factory-startup --python tools/tpac/modkit-anim.py -- "$M/AssetSources/_reference/animations-fbx" "$M/AssetSources/GreyWardenAnimations"
```

- 根位移不要缩放：FBX 单位是厘米，Blender 读写都是 5.95 这类数，Kit 导入时换算成米，正好等于原包的 0.0595。
  曾经按 Assimp 的读数（它不管单位）乘了 0.01，结果 Kit 里根位移小了 100 倍。
- **验收以 Kit 导入后的动画为准**：把 `Assets/GreyWardenAnimations/*_geo.tpac` 里的 `SkeletalAnimation` 与原包逐帧比（骨骼旋转、根位移）。
  用 Assimp 读 FBX 比只能验骨骼旋转，根位移的单位它会看错。
- 帧号、动画名（take 名 `human_skeleton_notused|<动作>`）与骨骼数必须与原包一致；片段按帧号引用动画。
- 动画片段不在 FBX 里，要在 Kit 里照 `GreyWardenPolicePurity/docs/reference/dual-wield-clips.md` 逐个建。
