# 物品与兵种名称

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。物品与装备规则见[装备](../state/warden-gear.md)。分组见[清单](text-review-inventory-20261104.md)第 15 批。

本批都是名字，逐条核过对应物品 / 兵种在模块 XML 里确实存在、类别对得上（如 `gwp_item_obsidian_great_shield` 是 `wlarge_shield_black`，灰袍领主战斗套和玩家入会套装都带它）。中英名称对应，没有发现说法不准或编造，全部保留。

## 待用户定

- **「灰袍守护者」与「灰袍守卫」**：家族名是「灰袍守卫」（已批准），而物品和兵种一律叫「灰袍守护者××」（如「灰袍守护者之铠」「灰袍守护者骑士」），英文两边都是 “Grey Warden”。如果要统一成「灰袍××」或「灰袍守卫××」，涉及 21 个名字，请先定方向再出改稿；不统一也不算错。

## 一、物品

[模块 `items.xml` 与 `gwp_crafting_pieces.xml` 里的物品名，背包、商店、交易界面显示。]

| 键 | 现中文 | 现英文 | 建议 |
|---|---|---|---|
| `gwp_item_armor` | 灰袍守护者之铠 | Grey Warden Harness | 保留 |
| `gwp_item_boots` | 灰袍守护者战靴 | Grey Warden Sabatons | 保留 |
| `gwp_item_commander_armor` | 灰袍指挥官之铠 | Grey Warden Commander's Harness | 保留 |
| `gwp_item_commander_boots` | 灰袍指挥官之靴 | Grey Warden Commander's Sabatons | 保留 |
| `gwp_item_commander_gloves` | 灰袍指挥官护手 | Grey Warden Commander's Gauntlets | 保留 |
| `gwp_item_commander_helmet` | 灰袍指挥官翼盔 | Grey Warden Commander's Winged Helm | 保留 |
| `gwp_item_commander_horse_armor` | 灰袍指挥官马铠 | Grey Warden Commander's Horse Armour | 保留 |
| `gwp_item_commander_shoulders` | 灰袍指挥官护肩 | Grey Warden Commander's Mantle | 保留 |
| `gwp_item_eagle_shoulders` | 灰袍守护者鹰肩 | Grey Warden Eagle Mantle | 保留 |
| `gwp_item_gloves` | 灰袍守护者护手 | Grey Warden Gauntlets | 保留 |
| `gwp_item_great_shield` | 灰袍守护者巨盾 | Grey Warden Great Shield | 保留 |
| `gwp_item_heavy_armor` | 灰袍守护者重铠 | Grey Warden Plate Harness | 保留 |
| `gwp_item_helmet` | 灰袍守护者之盔 | Grey Warden Helm | 保留 |
| `gwp_item_hood` | 灰袍守护者兜帽 | Grey Warden Hood | 保留 |
| `gwp_item_horse_armor` | 灰袍守护者马铠 | Grey Warden Horse Armour | 保留 |
| `gwp_item_warhorse` | 灰袍战马 | Grey Warden Warhorse | 保留 |
| `gwp_item_mace` | 灰袍卡拉德钉头锤 | Grey Warden Calradic Mace | 保留 |
| `gwp_item_arrows` | 灰袍穿刺箭 | Grey Warden Piercing Arrows | 保留 |
| `gwp_item_lance` | 灰袍守护者骑枪 | Grey Warden Lance | 保留 |
| `gwp_item_leather_boots` | 灰袍守护者皮靴 | Grey Warden Leather Boots | 保留 |
| `gwp_item_leather_gloves` | 灰袍守护者皮手套 | Grey Warden Leather Gloves | 保留 |
| `gwp_item_light_armor` | 灰袍守护者轻铠 | Grey Warden Light Harness | 保留 |
| `gwp_item_obsidian_great_shield` | 灰袍领主黑曜巨盾 | Grey Warden Lord's Obsidian Great Shield | 保留 |
| `gwp_item_shoulders` | 灰袍守护者护肩 | Grey Warden Mantle | 保留 |
| `gwp_item_sword_1h` | 灰袍守护者单手剑 | Grey Warden Arming Sword | 保留 |
| `gwp_item_sword_2h` | 灰袍守护者双手剑 | Grey Warden Greatsword | 保留 |
| `gwp_item_winged_helmet` | 灰袍守护者翼盔 | Grey Warden Winged Helm | 保留 |
| `gwp_item_dual_offhand` | 灰袍双持剑（副手） | Grey Warden Dual Blade (Off Hand) | 保留 |
| `gwp_item_dual_mainhand` | 灰袍双持剑（主手） | Grey Warden Dual Blade (Main Hand) | 保留 |
| `gwp_item_duty_banner` | 灰袍仪仗执勤旗 | Grey Warden Ceremonial Duty Standard | 保留 |

## 二、兵种

[模块 `spnpccharacters.xml` 里的灰袍兵种名。]

| 键 | 现中文 | 现英文 | 建议 |
|---|---|---|---|
| `gwp_troop_archer` | 灰袍守护者弓箭手 | Grey Warden Archer | 保留 |
| `gwp_troop_heavy_infantry` | 灰袍守护者重步兵 | Grey Warden Heavy Infantry | 保留 |
| `gwp_troop_knight` | 灰袍守护者骑士 | Grey Warden Knight | 保留 |
| `gwp_troop_light_infantry` | 灰袍守护者轻步兵 | Grey Warden Light Infantry | 保留 |
| `gwp_troop_new_recruit` | 灰袍新兵 | Grey Warden Recruit | 保留 |

## 三、自定义战斗

[原版自定义战斗里的灰袍指挥官。]

| 键 | 现中文 | 现英文 | 建议 |
|---|---|---|---|
| `gwp_custom_commander_yao` | 灰袍纠察官尧 | Grey Warden Provost Yao | 保留 |

## 本稿统计

共 36 条，全部实审。建议改动 0 条，保留 36 条；待用户定 1 处（「守护者 / 守卫」命名）。没有发现新的代码问题。
