# 灰袍鸟徽：静态材质与接入

当前状态、资源身份、哈希与待实测项以 [clan-banner state](../../../state/clan-banner.md) 为准。运行时 XML 与材质已经安装到 live；本目录为可重建参考输出，不是额外运行时模块。

## 工具

- `python tools/Prepare-ClanBanner.py`：批准成图转为原版绿色mask图集，并生成配置参考。源DDS写入 live AssetSources，PNG参考与manifest写本目录；不修改运行时XML。
- `python tools/Build-ClanBannerMaterial.py`：复制 Native custom_banner_icons_07 的239字节材质元数据，仅替换 diffuse GUID。输出本目录 gwp_commander_banner_mtl.tpac；材质与包具有各自独立的稳定GUID，和 Kit 当前 new_material 草稿不同。工具只产出文件，不覆盖 live。
- `powershell.exe -NoProfile -File tools/Verify-ClanBanner.ps1 -Installed`：实际游戏DLL、XSD、颜色归一、序列化、家族引用、注册与XML镜像核对。

原版材质参数：gui_color_and_stroke；Diffuse slot0为本图集；modulate、two_sided、alpha_test=0.5；两组factor RGBA=1；layout无可选标志。新材质除 diffuse GUID 外与 Native 元数据逐字节相同，另由 TpacTool 独立解析核对。

新增资源使用独立文件名与资源/包GUID，可以在 Kit 打开期间落盘而不覆盖已打开的草稿；这不表示当前进程已热加载新资源。若以后覆盖已经加载的成品材质，先退出 Kit，避免内存旧记录覆盖磁盘。用户的 new_material 是当前编辑草稿，不供旗帜引用。

## 纹理

源：`D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/GreyWarden/AssetSources/BannerIcons/gwp_commander_banner.dds`。

DDS 源是 2048×2048 RGBA、单层未压缩。4×4 网格的第 0 格有鸟徽，G=255、R/B=0，alpha 承载轮廓。原版 shader 用 `alpha *= draw_stroke * R + G`，填色来自旗帜配置。

这里归档测试用的单层纹理。关闭游戏与 Kit 后，将 `gwp_commander_banner_tex.tpac` 复制到 live 的 `Assets/BannerIcons/`，同包 GUID 的 `E97ADA98-FDCF-499F-B830-2135638BA024.rdc` 复制到 live 的 `RuntimeDataCache/`。两个文件成对部署并验证。RDC 的 DXT5 像素取自 Kit 原缓存的最高分辨率层。

## 验收与后续打包

下次完整加载游戏、开新档，看地图小旗、百科和战场旗帜是否黑底金鸟、方向正确、爪尾完整；同版本存读后保持。旧档的 Banner 属于原版保存状态，不通过新增迁移改写。

本地开发版使用 live 的 `Assets` 与对应的 `RuntimeDataCache`。r17 的打包美术使用同一份单层图集像素，当前素材身份与验收记录见 [clan-banner state](../../../state/clan-banner.md#验证与发行状态)。

离线参考：[512/128/64像素](native-mask-preview.png)，非引擎截图。格式参照 [TaleWorlds 自定义旗帜图标文档](https://moddocs.bannerlord.com/asset-management/creating_custom_banner_icons/)。
