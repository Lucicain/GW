# 战役启动、新档事件与行为存读

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Campaign.cs` 的 `OnInitialize`、`OnNewGameCreated`；
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignGameStarter.cs` 的 `AddModel`、`GetModel`；
> `TaleWorlds.Core/TaleWorlds.Core/GameModelsManager.cs` 的 `GetGameModel`；
> `TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/MBGameManager.cs` 的 `OnGameStart`；
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/ClanVariablesCampaignBehavior.cs` 的 `OnNewGameCreated`
> 模组接入：[公共底座](../state/infrastructure.md#战役状态生命周期)、[玩家声望](../state/player-justice.md#声望playerbehaviorpool)

## 怎么运作

`Campaign.OnInitialize` 为新战役和读档共同执行。关键顺序如下：

1. 创建战役事件、管理器与 `CampaignGameStarter`，初始化原版 starter。
2. 调用 `GameManager.OnGameStart`。`MBGameManager` 在这里逐个调用各模块的 `OnGameStart`，
   模块可以添加模型和行为。此时还未执行行为数据的恢复。
3. 设置模型、初始化战役时间和对象；经 `SandBoxManager.OnCampaignStart` 继续建立战役。
4. 新档建立 `CampaignBehaviorManager`；读档则调用 `InitializeCampaignBehaviors`、
   `LoadBehaviorData`、`RegisterEvents`，依次创建本次行为、恢复其保存数据、注册监听。
5. 完成对象初始化，最后才调 `Game.OnGameStart`（游戏 handler 回调）和模块的
   `OnGameInitializationFinished`。它们不能与第 2 步的模块 `OnGameStart` 混用。

新档另走 `Campaign.OnNewGameCreated`：先 `OnNewGameCreatedInternal`，再调用模块的
`OnNewGameCreated`，然后才由 `CampaignEventDispatcher` 分发行为监听的 `OnNewGameCreatedEvent`。
新档没有旧档行为数据可恢复，不能依靠 `SyncData(IsLoading)` 归零进程级静态字段。

原版 `ClanVariablesCampaignBehavior.OnNewGameCreated` 会遍历非强盗家族并调用
`UpdateGovernorsOfClan`。因此总督分配会在新档事件期间发生，并非必须等到第一个日结。
仅在较晚的行为监听器中清除旧状态，无法保证较早的初始化和候选检查不读到它。

## 开局孩子与家谱

主新档事件完成后，`CampaignEvents` 依序分发补充事件索引 0 到 99，然后分发 `OnNewGameCreatedPartialFollowUpEndEvent`。原版 `InitialChildGenerationCampaignBehavior` 在索引 0 为符合条件的非玩家家族补孩子，目标数为成年领主数的一半向上取整。它用 `HeroCreator.CreateChild` 借用人物模板生成 2 至 17 岁的英雄，指定家族和出生地。

`CreateChild` 和 `CreateSpecialHero` 都可生成没有父母的英雄。静态 `Heroes` XML 中的 `mother`、`father` 由 `Hero.Deserialize` 读取。运行时写 `Hero.Mother` 或 `Hero.Father` 会同时向对应父母的 `Children` 加入孩子，重复赋值会重复加入，旧父母记录也不会由 setter 自动移除。父母和孩子列表使用原版保存字段。（依据：安装版 v1.4.8 C# 核对 2026-11-11）

人物百科 `EncyclopediaHeroPageVM` 从这些父母字段及子女列表收集亲属。页面信息可见时，符合英雄百科收录条件的亲属会显示，称谓由 `EncyclopediaFamilyMemberVM` 按同一套家谱生成。家族百科列家族成员，个人亲属称谓在人物页查看。原版对玩家还未获知详情的人物隐藏亲属区，人物页创建时读取这一状态。（依据：安装版 v1.4.8 `EncyclopediaHeroPageVM`、`DefaultInformationRestrictionModel`、`DefaultEncyclopediaHeroPage` 核对 2026-11-11）

## 战役模型的注册顺序

各模块按[当前模块列表的加载顺序](module-loading.md#怎么运作)，依次在 `OnGameStart` 向同一个 `CampaignGameStarter` 注册模型。泛型 `AddModel<T>` 从后往前找到
已注册的最后一个兼容模型，调用新模型的 `Initialize` 将它设为 `BaseModel`，再追加新模型。非泛型重载只追加。
战役从该列表建立 `GameModels`，其中 `GetGameModel<T>` 也从后往前取第一个匹配项。
因此属性（如 `SettlementMilitiaModel`）返回最后注册的兼容模型。此前注册的实例仍在列表中，
新模型可以通过 `BaseModel` 委托给前一个模型，也可以自行覆盖其规则。

## 容易想错的地方

- 回到主菜单不等于重启进程；C# 静态字段不会因为新建 `CampaignBehavior` 自动消失。
- 模块 `OnGameStart` 同时用于新档和读档，位于 `LoadBehaviorData` 之前；清空旧进程状态与随后恢复存档是两个阶段。
- 开始初始化时可以清空自有集合，但不能据此假定英雄、聚落和模型已经全部可用。
- 这里说明托管层调用顺序；不证明任意模组的字段保存完整，也不代替真实存档往返验证。
