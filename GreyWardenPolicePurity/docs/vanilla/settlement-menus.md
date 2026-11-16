# 原版聚落菜单与剧情入口

> 游戏版本：v1.4.8、v1.5.7
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/PlayerTownVisitCampaignBehavior.cs`、`TaleWorlds.CampaignSystem/Helpers/InventoryScreenHelper.cs`、`StoryMode/StoryMode.GameComponents.CampaignBehaviors/TutorialPhaseCampaignBehavior.cs`、`StoryMode/StoryMode.Quests.TutorialPhase/VillagersInNeed.cs`
> 模组接入：[信众村菜单](../state/firesworn-villages.md#菜单与存档)、[玩家向祭司交付](../state/firesworn-economy.md#玩家向祭司交付)

## 怎么运作

1. 城镇的酒馆区从 `town` 切换到 `town_backstreet`，实际进酒馆场景、赎金和雇佣兵各有子选项。返回镇中心调用 `GameMenu.SwitchToMenu("town")`，玩家仍驻留该聚落。两版均走这套目录。
2. 平常进村打开 `village`。`recruit_volunteers` 打开原版招募页。`trade` 在中文里显示购买商品，后果处理器 `game_menu_ui_village_buy_good_on_consequence` 调用 `InventoryScreenHelper.OpenScreenAsTrade`，用村庄物品名册和市场数据，与玩家背包进行双向金币交易。到村里走走及要人交谈走各自的入口。
3. 剧情开局仍限制交互时，`TutorialPhaseCampaignBehavior` 有独立的 `storymode_tutorial_village_game_menu`，登记教程专用的招募、购买和进场选项。剧情支线 `VillagersInNeed` 也会在普通 `village` 增加 `talk_to_villager`。1.5.7 继续用这两个入口。该支线还检查当前菜单 ID 以推进剧情。

## 容易想错的地方

- 安装在普通 `village` 的菜单选项只随这张菜单出现。剧情教程的村庄菜单由剧情交互限制控制。
- 酒馆区的子菜单与打开酒馆场景是两步。赎金选择也可直接从子菜单打开部队界面。
