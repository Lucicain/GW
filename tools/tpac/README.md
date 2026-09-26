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
- BC5 法线贴图只存 X、Y，导出的 PNG 蓝色通道是 0；程序按 Z = √(1−X²−Y²) 补回蓝色通道，否则 Blender 里明暗全错。
- `fbx-verify` 只比网格数、面数、材质名；顶点数会比原包少 1%～5%，因为 FBX 只带第一套 UV、
  不带切线，Assimp 读回时把这些属性相同的顶点合并了。
