# 地图栏「灰袍事务」按钮

> 当前状态：已部署待实测
> 最后验收：未建立检查点
> 覆盖源码：`GwpAffairsNavigation.cs` `_Module/GUI/Icons/gwp_affairs.png` `tools/ui-icons/make_affairs_icon.py`
> 待实测：按钮出现在家族与王国之间、图标正常显示；未入会时灰显并提示原因；入会后点开灰袍事务界面；从家族/部队等界面直接点它也能打开

## 位置与门槛

- 地图左下角导航栏，排在「家族」之后、「王国」之前；装了 NavalDLC 时舰队按钮照原样插在部队之后。
- 只有宣誓入会（`PlayerBountyBehavior.IsRecruitedByGreyWardens`）才可点；未入会时灰显，悬停显示原因。
  百科灰袍家族页的「灰袍事务」入口不设门槛。（依据：用户裁定 2026-10-02）
- 界面已打开时按钮显示为选中、不可再点；导航栏整体被锁（存档中、战斗模拟、军团管理等）时同原版一样不可点。
- 从家族、部队等全屏界面点它，先按原版 `MapNavigationHelper.SwitchToANewScreen` 退回地图，再打开灰袍事务浮层。
- 右键（原版"跳转链接"）打开灰袍家族的百科页。

## 实现

- 原版按钮列表来自 `MapNavigationHandler.OnCreateElements()`（protected virtual）；NavalDLC 也是重写它插入舰队按钮。
  我们用 Harmony 后置补丁在 `clan` 之后插入 `GwpAffairsNavigationElement`（继承 `MapNavigationElementBase`，id `gwp_affairs`）。
  NavalDLC 的重写先调基类（已含我们的按钮）再按下标插入，顺序不受影响。（依据：C#反编译 v1.4.8，2026-10-02）
- 按钮两层图：`IconOffsetButtonWidget` 按 id 从 `MapBar.Left.Button.Backgrounds` 取底板，`IconBrushWidget` 从
  `MapBar.Left.Icons` 取图标（60×40 的框，按 Contain 缩放，样式随图层复制）。补丁在建列表时往这两个原版画刷各加一层
  `gwp_affairs`：参数照抄 `clan` 层，底板沿用家族石板，图标换成我们的精灵。
- 图标在运行时从 `GUI/Icons/gwp_affairs.png` 读字节，`TaleWorlds.Engine.Texture.CreateFromMemory` 建贴图（原版头像缩略图同法），
  包成整张贴图的 `GwpTextureSprite`（继承抽象 `Sprite`；界面绘制只依赖抽象类，`SpriteGeneric` 只在字体里被强转）。
  精灵宽高取 PNG 文件头，不读引擎贴图（可能还没载完）。不经 Modding Kit 打精灵表。
- 读图失败写 `AFFAIRS_ICON_MISSING` / `AFFAIRS_ICON_NOT_PNG` / `AFFAIRS_ICON_DECODE_FAILED`，按钮照常出现但没有图标。

## 图标

- 来源：指挥甲胸口的金色狮鹫剪影（`AssetSources/GreyWardenRebuild/wcommat_d.png`）。用户认可（2026-10-02）。
- 生成：`python tools/ui-icons/make_affairs_icon.py <wcommat_d.png> <原版图标目录> GreyWardenPolicePurity/_Module/GUI/Icons/gwp_affairs.png`。
  按亮度把每档映射到原版图标在该亮度的平均颜色，色调分布匹配原版家族狮头（`mapbar_icon7`），输出 3 倍尺寸（约 60×120）。
  原版 `ColorFactor 0.9` 由引擎在绘制时施加，不要烘进图里。（依据：原版 `MapBar.xml` 画刷 `MapBar.Left.Generic.Icon` 2026-10-02）
- 原版图标与底板来自 `SandBox/AssetPackages/gauntlet_ui.tpac` 的 `ui_mapbar_1`（BC7，4096×128），坐标见 `SandBoxSpriteData.xml`。
  TpacTool 导不出 BC7 的 PNG：先导 DDS，再用 Pillow 解码裁切。

## 回退

撤回本功能的提交即可：删 `GwpAffairsNavigation.cs`、`_Module/GUI/Icons/`、`GwpCaseArchiveScreen.IsOpen` 与语言串 `gwp_affairs_nav_need_membership`。
