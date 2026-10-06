# 灰袍装备：来源与原版品质

> 当前状态：入会发装带原版品质经用户实测确认（2026-10-02）；最佳品质发装与 v14 自动换装候选保护已部署待实测
> 最后验收：`eebeb33`（随机品质，2026-10-02 用户确认）
> 已复核至：2026-10-07 灰袍自动装备候选保护（v14 工作树，待实机）
> 覆盖源码：`_Module/ModuleData/items.xml` `_Module/ModuleData/crafting_templates.xml` `_Module/ModuleData/gwp_crafting_pieces.xml` `GwpWardenAutoEquipmentPatch.cs` `tools/Verify-WardenAutoEquipment.ps1`
> 待实测：重新入会后，灰袍装备每件都是该组最好的品质（剑、甲、盾为传奇）；v14 / RBM 环境灰袍进城后装备保持，普通领主自动换装与玩家手动换装照常

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
五件指挥甲胄、马具、黑色大盾、一副双刀），每件取所在品质组里最好的原版品质：`ItemQuality` 最高，
同档取 `PriceMultiplier` 最高。各组最高都是传奇（剑、锤、枪 伤害+7 速度+3；板甲 护甲+12；链甲 +9；皮甲 +7；布甲 +5；盾 耐久+210）。
这是灰袍装备带上品质的唯一途径；品质机制为日后复刻辛迪加设定铺垫。（依据：用户裁定 2026-10-02）

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

## 灰袍 NPC 的自动换装候选

用户要求压制灰袍的换装欲望，并明确“我们不动其他模组代码，这是底线”。本轮没有修改第三方文件、配置或方法，也没有对第三方方法挂补丁。（依据：用户裁定 2026-10-07）

当前 live 的 v14 工作树新增 GwpWardenAutoEquipmentPatch，仅挂钩原版 Helpers.CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement) 两参数布尔重载：CharacterObject.HeroObject 属于 gw 家族时返回 false，其余角色保留原版。带 out TextObject reason 的三参数重载不改，玩家装备界面的带原因判定保留。普通士兵没有灰袍 HeroObject，不受此限制。

实际 RBM 4.5.4 的 FindBestUsable / FindBestAffordable 在购买、从背包移走物品及退旧装备之前都询问该原版入口。两条路径因此没有灰袍可选候选，普通领主仍能选择高阶物品。这是本模组的原版资格限制，不依赖 RBM DLL，也不挂钩 RBM 的 IsManaged、UpgradeGear 或装备评分。

不复写已穿装备，不做进城后的恢复或轮询；本模组成年/生成角色发装直接复制装备，不走这项候选判定。已被换走的装备不靠本补丁自动还原。本补丁不处理原版锻造订单直接写入武器等绕过此资格入口的机制。（依据：C# 反编译、设计）

诊断仅 GWP_DIAGNOSTICS：WARDEN_AUTO_EQUIPMENT_REJECTED，每个英雄实例最多一条（弱引用表），记录候选和真实调用栈；确认灰袍进城后保持装备时同轮退休。弱引用去重仅用于诊断，不是进行中的玩法状态，因此不写存档。

验证：开发/无诊断隔离构建均 0 警告/0 错误；GameCompat/PREFLIGHT PASS，614 类型、71 补丁、失败 0。Verify-WardenAutoEquipment.ps1 使用真实原版与当前 RBMCampaign.dll，只读调用实际候选搜索；有 RBM 23 / 无 RBM 11 断言 PASS，涵盖 gw/GW、普通英雄、普通士兵、BasicCharacterObject、手动装备重载及库存不变。测试未挂钩第三方方法。

当前 Client/Editor DLL SHA-256：`5d70be4e58c566a10a1c7b60e46944b366a35951ba560c5261d060a295b31c12`；66 个仓库运行时文件镜像无缺失/差异/散落。回退：删除 v14 工作树 GwpWardenAutoEquipmentPatch.cs 后重新构建部署，或恢复 `C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/v14-before-auto-equipment-20261007/` 的两端 DLL/PDB（SHA-256 `7e8b5c00180b6dddd890923b7da9ebb7f010f81c4cadaa943ca4474e7bd6008c`）；本轮前本地存档点 `7c91b2d`。尚待用户实际进城验证。
