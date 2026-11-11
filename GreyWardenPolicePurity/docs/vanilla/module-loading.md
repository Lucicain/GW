# 原版启动器与模块加载顺序

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.MountAndBlade.Launcher.Library/TaleWorlds.MountAndBlade.Launcher.Library/LauncherModsVM.cs` 的 `LoadSubModules`、`ChangeLoadingOrderOf`、`ModuleListCode`，同目录 `LauncherUI.cs` 的 `AdditionalArgs`；
> `TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/ModuleInfo.cs` 的 `LoadWithFullPath`，同目录 `ModuleHelper.cs` 的 `GetDependentModulesOf`、`InitializeModules`；
> `TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/Module.cs` 的 `Initialize`、`LoadSubModules`、`CollectSubModules`；
> `TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs` 的 `GetMergedXmlForManaged`、`CreateMergedXmlFile`、`MergeElements`。
> 模组接入：[与其他模组共存](../state/infrastructure.md#与其他模组共存)

## 怎么运作

1. 原版启动器读取 `LauncherData.xml` 的列表和勾选状态，把新发现的模块补到待排序列表。单人与多人各存一份列表。
2. `ModuleInfo.LoadWithFullPath` 读各模块的 `SubModule.xml`。`DependedModules` 声明本模块的前置，`ModulesToLoadAfterThis` 声明应在本模块之后加载的模块。前置缺失且没有 `Optional="true"` 时，原版启动器禁止勾选依赖它的模块。可选前置出现在排序列表里时也参与排序。
3. 启动器刷新、拖动和开始游戏时，都按这两种声明做拓扑排序。它按输入列表访问各模块，先排入该模块的前置，再排入该模块。两个模块的相对位置由整个依赖图和输入顺序共同决定。
4. `ModuleListCode` 从排好序的列表中取出勾选项，将模块 ID 按顺序写入游戏启动参数。游戏取得这份顺序，在 `InitializeModules` 按 ID 装入模块列表。`Module.LoadSubModules` 按此顺序登记 XML 并加载子模块程序集、构造实例。正常启动时先完成这一批 DLL 的加载与实例创建，再调用各子模块的 `OnSubModuleLoad`。
5. 开新局或读档时，`MBGameManager.OnGameStart` 按当前模块列表调用各子模块的战役启动回调。注册同一种战役模型时，游戏选择最后注册的实例。该实例可以委托给先前的模型，具体见[战役模型的注册顺序](campaign-lifecycle.md#战役模型的注册顺序)。

## 容易想错的地方

- 启动器只按清单计算模块关系。两个模块都能勾选、相对位置都合法时，仍可能在战役里注册同一种模型。排序器无从判断模型是否会合用。
- 托管 XML 按注册顺序合并。`CreateMergedXmlFile` 从首个 XML 建立底稿，后续每项先应用 XSLT，再合并该项的 XML。对于按 XSD 唯一属性配对的元素，后给出的同名属性可覆盖前值，独立元素可追加。参见[官方 XML 合并说明](https://moddocs.bannerlord.com/bestpractices/merging_module_xml_files_with_native/)。
- 原版启动器的依赖图来自第 2 步的声明。`DependedModuleMetadatas` 是社区扩展格式，由支持它的启动器读取，例如[启用 Extended Sorting 的 BLSE LauncherEx](https://github.com/BUTR/Bannerlord.BLSE#community-dependency-metadata)。
