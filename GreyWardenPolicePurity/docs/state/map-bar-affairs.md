# 灰袍事务：入口与界面定位

> 当前状态：导航按钮门槛保持；家族百科按钮恢复未入会也能进入的彩蛋入口，已部署待实测
> 最后验收：`a3dc3c9`（2026-10-02 用户确认）
> 覆盖源码：`GwpAffairsNavigation.cs` `_Module/GUI/Icons/gwp_affairs.png` `tools/ui-icons/make_affairs_icon.py`
> 待实测：未入会从灰袍家族百科进入事务；地图导航仍按入会与声望门槛，见事务界面

## 界面定位与改版讨论

当前实现是独立全屏事务菜单，具体显示与输入注册归[事务界面](affairs-interface.md)，领单业务归[玩家入会与悬赏](player-justice.md#悬赏接单)。

- 用户反馈：事务界面此前定位不清，许多内容可以整合进来，未来包括玩家军械库；原版家族界面可参考，但目前功能不足以照搬其规模。（依据：用户反馈 2026-10-18）
- 玩家军械库已接入第四页，实际库存/存取见[军械库](firesworn-armory.md#玩家军械库)，本页负责导航准入。
- 社区反馈提出总部/教堂场景，以及左侧分层人物姓名头像、中间任务信息、右侧据点余量的布局。截图中用户提到暂无制作总部新地图与教堂模型的条件、界面可以做；用户后续批准的实际界面范围见事务页，场景建议尚未实施。（依据：用户提供的反馈截图与后续裁定 2026-10-18）

用户要求借鉴原版设计、先严格规划并展示预期；整体五页签、六职能卷宗、玩家使者与宗教/军械内容的归属、交互和验收条件统一在[事务界面方案](affairs-interface.md)，用户已要求按现有职能先实现，当前实现和待验收范围见该页。（依据：用户裁定 2026-10-18）

### 反馈素材

以下均为本轮用户提供的反馈/布局参考，不是游戏资产，也不是改版验收图：

- 群聊长截图：`C:/Users/lucif/AppData/Local/Temp/codex-clipboard-3b67c67d-a3d8-4947-8138-51db72657c01.png`（临时路径；关键反馈已转录于本节）。
- 原版家族界面分区标注：`C:/Users/lucif/Documents/Tencent Files/157652226/nt_qq/nt_data/Pic/2026-10/Ori/8751c51c937a9202c9e2568fda74f40e.png`。
- 当前事务台账实机截图：`C:/Users/lucif/Documents/Tencent Files/157652226/nt_qq/nt_data/Pic/2026-10/Ori/4e620ce18fada305eb61618f85b041b2.png`。

## 位置与门槛

- 地图左下角导航栏，排在「家族」之后、「王国」之前；装了 NavalDLC 时舰队按钮照原样插在部队之后。
- 地图导航经GwpCaseArchiveScreen.CanOpen，要求已入会且声望≥20；灰袍家族百科页经ShowFromClanPage保留彩蛋，不限入会与声望。具体页面及业务权限归[事务界面](affairs-interface.md)，彩蛋不会自动入会或授予接案资格。（依据：用户裁定2026-10-18，明确家族页例外）
- 界面已打开时按钮显示为选中、不可再点；导航栏整体被锁（存档中、战斗模拟、军团管理等）时同原版一样不可点。
- 从家族、部队等全屏界面点它，先按原版 `MapNavigationHelper.SwitchToANewScreen` 退回地图，再打开灰袍事务独立菜单。
- 右键（原版"跳转链接"）打开灰袍家族的百科页。
- 页面内可主动领取委托，不依赖右侧提醒；执勤旗、声望与已有任务门槛只在[玩家入会与悬赏](player-justice.md#悬赏接单)维护，界面开关验收归[事务界面](affairs-interface.md)。（依据：用户反馈、实现 2026-10-17）

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
