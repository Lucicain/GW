# 灰袍装备：来源与原版品质

> 当前状态：入会发装按原版产出档掷品质、两面大盾改用战役 `shield` 品质组，已部署待实测
> 最后验收：未建立检查点
> 覆盖源码：`_Module/ModuleData/items.xml` `_Module/ModuleData/crafting_templates.xml` `_Module/ModuleData/gwp_crafting_pieces.xml`
> 待实测：入会或重新入会后，背包里的灰袍装备按件带上原版品质（多数无前缀，少数精良/大师/传奇）；大盾也可能带品质

本文件写"灰袍装备从哪来、带不带原版品质（`ItemModifier`，精良/大师/传奇等前缀）"。
灰袍自己的武器效果（击倒、破防等，代码里叫 `GwpWeaponTraits`）是另一回事，见 [troop-combat.md](troop-combat.md)。

## 只有灰袍能拿到

- 玩家和 NPC 都不得通过购买、打造、战利品或任何其他途径获得灰袍装备；玩家只在入会时由灰袍发放。（依据：用户裁定 2026-10-02）
- 全部灰袍物品 `is_merchandise="false"`。原版战利品（`DefaultBattleRewardModel.GetRandomItem`）、作坊产出、商队、比武奖品、
  藏身处与地图拾取都跳过 `NotMerchandise`（依据：C#反编译 v1.4.8，2026-10-02）。不要把灰袍物品改成商品。（依据：用户裁定 2026-10-02）
- 双刀专用刀身 `gwp_vlandian_blade_3_dual` 是 `is_hidden="true"`，锻造里解锁不到。其他打造类武器用原版部件，
  玩家能打出外观相近的武器，但物品 id 不同，不带灰袍武器效果。

## 入会发装

加入或重新加入时（`PlayerBountyBehavior.GiveCommanderEquipment`，发放清单 `GwpIds.MembershipGrantItemIds`：
五件指挥甲胄、马具、黑色大盾、一副双刀），每件按原版作坊/店铺的**产出档**
`ItemModifierGroup.GetRandomItemModifierProductionScoreBased()` 各掷一次品质（同 `WorkshopsCampaignBehavior`）。
这是灰袍装备带上品质的唯一途径。

原版产出档（`item_modifiers_groups.xml` / `item_modifiers.xml`，剑、锤、枪、箭、各类甲、盾同一组权重）：
无前缀 75/107，第三档（平衡/精良/加厚等）15，大师/华贵 10，传奇 5，两档负面各 1。

已发出去的装备不补掷（不做存档兼容）。判断"穿没穿全套指挥装"、双刀配对、武器效果都按物品 id，不看品质。

## 品质组

| 物品 | 品质组 |
|---|---|
| 打造类武器 | 沿用模板：`OneHandedSword`/`TwoHandedSword`/双刀模板 = `sword`，`TwoHandedPolearm` = `polearm`，`Mace` = `mace` |
| `gwarrows` | `arrow` |
| 甲胄、马具 | `plate` / `leather` / `cloth` / `chain`（逐件见 `items.xml`） |
| `wlarge_shield`、`wlarge_shield_black` | `shield` |
| `gwwarhorse` | 无（不发给玩家） |

- 大盾不要用 `shield_metal`：它只在多人 `mpitems.xml` 里出现，战役没有定义这个组，盾永远掷不出品质。（依据：原版 XML 核对 2026-10-02）
- 灰袍士兵上战场时，原版逐件掷**掉落档**品质（`GwpWholePresetEquipmentPatch` 照做，见 troop-combat.md），只影响那一场。
  大盾改组后，士兵的盾也会按原版掷到加厚/破损等，耐久随之浮动。

## 回退

撤回本功能的提交：`GiveCommanderEquipment` 恢复 `new EquipmentElement(item)`，两面大盾恢复 `shield_metal`。
