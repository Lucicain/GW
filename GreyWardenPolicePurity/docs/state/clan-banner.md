# 灰袍家族旗帜

> 当前状态：黑底浅金鸟徽图案已验收。低画质清晰度待复核
> 最后验收：2026-10-15 用户实机确认（旗帜工作树，尚无对应提交检查点）
> 覆盖源码：`_Module/ModuleData/spclans.xml` `_Module/ModuleData/banner_icons.xml` `_Module/SubModule.xml` `tools/Prepare-ClanBanner.py` `tools/Verify-ClanBanner.ps1` `tools/Build-ClanBannerMaterial.py`
> 待实测：贴图质量为低时，核对原版战报大旗、地图小旗和百科的鸟徽轮廓

## 当前图案与原版接入

用户已认可以②指挥官胸甲鸟徽补全的[完整图 v1](../reference/banner-review/commander-banner-complete-v1.png)，要求在游戏中使用；图案为单首、双展翼、双爪和卷曲尾饰，黑底浅金。图由 imagegen 照胸甲纹理半幅补绘，不是原模型正视纹章的精确复原。当前配置使用原版 BannerIcon 路径，只有纯底与鸟徽两层。`SubModule.xml` 显式注册 BannerIcons，`spclans.xml` 的 gw 使用新旗帜码；不依赖运行时材质替换或 C# 补丁。（依据：用户裁定、生成记录 2026-10-15；设计、源码复核）

| 项目 | 当前值 |
|---|---|
| 组 / 图标 | 63 / 63010；reserved，NPC专用，不进入随机旗帜池或玩家选图列表 |
| 图集 / 材质 | `gwp_commander_banner`；4×4图集的第0格 |
| 底纹 / 颜色 | 原版11；自定义63001，`#1A1A1A` |
| 图标颜色 | 自定义63002，`#FDDC88` |
| 图标尺寸 / 位置 | 512×512 / `(764,764)`；底为1528×1528 |

旗帜码：`11.63001.63001.1528.1528.764.764.1.0.0.63010.63002.63002.512.512.764.764.0.0.0`。

原版 Clan.Deserialize 记录旗帜底色与首图标色后，会经 UpdateBannerColorsAccordingToKingdom 调 ChangeIconColors，把所有图标染成首图标颜色。当前只一个金色图标，归一后仍为金色，内部切口靠透明度而非深色图层分隔。不能凭 clan XML Color/Color2=FFFFFFFF 推断旗帜为白色。Clan 的 Banner 属于原版存档字段，初始 XML 不负责改写已有档的已保存旗帜；本次新档验收，不加跨版本迁移。同版本存读接续使用原版保存行为。（依据：v1.4.8 C#反编译、实际 DLL 离线调用；用户存档裁定）

## 可编辑素材与静态材质

- 批准成图 SHA-256：`bb0acfb06f960e8930f39276ebfd90cef1bb41b46da5f9bd3caef4a39a93c784`。提示词在同目录 commander-banner-complete-v1-prompt.txt。
- 旗帜源：`D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/GreyWarden/AssetSources/BannerIcons/gwp_commander_banner.dds`，身份为现用旗帜可编辑源；2048×2048 RGBA，源DDS单mip、未压缩，SHA-256 `09d8bc3729c58cca75efcb391d7b9c8fd0782dd57616ef5284e8a5b663c979e2`。
- `tools/Prepare-ClanBanner.py` 仅做通道与图集编码；G=255、R/B=0、alpha承载形状，第0格占用，其余透明。绿色是正常mask预览，不应把它涂金以代替旗帜染色。
- 本地测试用纹理位于 live `Assets/BannerIcons/gwp_commander_banner_tex.tpac`，GUID `721dc80a-4131-4fee-b809-8e24b326cf6b`，Source 指向上述 DDS。Kit 导入为 2048×2048 DXT5、12 层 mip，安装版原生旗帜图集为 2048×2048 BC7、1 层 mip。用户战报截图及 `texture_quality = 0` 下出现细节模糊。本地测试纹理采用原版界面贴图 `encounter_interface` 使用的 `dont_degrade` 标记。TpacTool 已核对尺寸、格式、标记及资源身份，两个数据段与旧纹理逐字节一致。测试纹理 TPAC SHA-256 `a78df13ed411c0adc518014e2409e02999b3922e7cf6dde59dfe1df4ed3084ba`。（依据：用户截图、实机配置及安装版资源对照 2026-11-16）
- 静态成品材质位于 live `Assets/BannerIcons/gwp_commander_banner_mtl.tpac`，材质GUID `a1b6fa4a-4464-5124-9a30-2e642ecc314d`、包GUID `6094d742-e51e-5d65-a264-f906ac744474`；SHA-256 `c27be4a2d4aa753b23feac95f10b92f8110c59fac3235ed7402c03454817290f`。构建源及工具见[接入说明](../reference/banner-review/kit-input/IMPORT.md)。

材质由 `tools/Build-ClanBannerMaterial.py` 复制 Native `custom_banner_icons_07` 完整239字节元数据，只换 diffuse GUID；资源/包使用独立稳定身份。shader 为 `gui_color_and_stroke`，slot0 为本图集，blend=modulate、two_sided、alpha_test=0.5，layout无可选项，两组factor RGBA=1。除 diffuse GUID 外，元数据与 Native 逐字节相同，独立 TpacTool 库读回一致。用户要求当前无需关闭 Kit；采用新增独立资源文件/名称/GUID，不覆盖 Kit 已加载的 `new_material` 草稿。该草稿仍是用户当前编辑工作，不供旗帜引用；已读回其保存后的 shader 为 gui_color_and_stroke。新增资源与XML供下次完整加载使用，不宣称当前进程已热加载。（依据：Native shader 源码、资源记录与元数据，脚本与独立库读取；用户反馈、实际资源读取 2026-10-15）

## 装备纹章参考素材

- 黑金盾原图：`D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/GreyWarden/AssetSources/GreyWardenRecovery/gwp_black_gold_shield_d.png`，4096×4096，身份为现用装备可编辑源。模型 `wlarge_shield_black_static` → 材质 `gwp_black` → slot0纹理GUID `f9509818-df3c-49d7-9b9f-3267ab6fa64e`；有独立法线/高光。
- 胸甲原图：`D:/steam/steamapps/common/Mount & Blade II Bannerlord/Modules/GreyWarden/AssetSources/GreyWardenRebuild/wcommat_d.png`，4096×4096，身份为现用装备可编辑源。`wcomarmorhv.2` → 材质 `wcommat` → slot0纹理GUID `e0e55f31-b199-4e7e-9bc6-6f28e793eb46`；UV只含鸟徽半幅，不能说成模型完整正视图。（依据：live资源链与原图目视）

Kit 的上述 `_tex.tpac` 为源图与导入元数据，不含内嵌像素；参考图使用其 Source 指向的 PNG。[原图局部对照](../reference/banner-review/equipment-emblems-current.png)来自裁切，未改装备原图、模型、法线或高光。

## 验证与发行状态

- `Verify-ClanBanner.ps1 -Installed` 核对安装版 XSD、Native/NavalDLC ID 冲突、实际 Banner DLL 解析两层、图标材质/格号、精确黑金色、颜色归一、旗帜码往返、reserved、gw 引用、唯一注册与 XML 镜像哈希。旗帜只改 XML 与资源，不改 DLL。[512/128/64像素参考](../reference/banner-review/kit-input/native-mask-preview.png)是 DDS 解码图，非引擎截图。
- 本机 `rgl_log_29940.txt` 记录客户端加载 `Modules/GreyWarden/Assets`。开发模块使用该目录中的可编辑纹理与 DDS 源，玩家包的 `pack0` 留在[发行暂存目录](build-and-deploy.md)。低画质下的实际渲染待实测。（依据：实机日志及目录核对 2026-11-16）
- 玩家美术包 `pack0` 已含 `gwp_commander_banner` 材质及初版纹理。下一次玩家包发布时，在 Kit 内保留 `dont_degrade` 并重新发布该纹理。哈希只在[构建与部署](build-and-deploy.md)与资源流程维护。（依据：TPAC 资产清单对照 2026-10-16、2026-11-16）

回退：取回 `4f31993` 的 `_Module/ModuleData/spclans.xml`、`_Module/SubModule.xml` 并镜像，撤去新增运行时 banner_icons.xml。新旗帜源与成图可保留作素材；需要撤下新材质时，在 Kit 退出后移除对应独立资源，不影响装备素材。
