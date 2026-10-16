# 灰袍装备：来源与原版品质

> 当前状态：便装模板摘盔已镜像，战斗头盔保留；入会品质已验收，最佳品质与自动换装保护待实测
> 最后验收：`eebeb33`（随机品质，2026-10-02 用户确认）
> 已复核至：`6855489`（2026-10-16）
> 覆盖源码：`_Module/ModuleData/items.xml` `_Module/ModuleData/crafting_templates.xml` `_Module/ModuleData/gwp_crafting_pieces.xml` `_Module/ModuleData/gw_equipment_sets.xml` `_Module/ModuleData/spspecialcharacters.xml` `GwpWardenAutoEquipmentPatch.cs` `tools/Verify-WardenAutoEquipment.ps1` `GwpWardenTroopGenderPatch.cs`
> 待实测：新档城堡/城镇大厅灰袍便装无头盔、战斗仍戴盔；重新入会后装备为最佳品质；RBM 环境进城装备保持，普通领主和玩家手动换装照常；战场上灰袍兵约一半是男的、男兵有胡子、同一个兵每场一样

本文件写"灰袍装备从哪来、带不带原版品质（`ItemModifier`，精良/大师/传奇等前缀）"。
`spspecialcharacters.xml` 中的领主外貌定义归 [灰袍家族](family.md)，本页覆盖其装备引用。（依据：职责划分）
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

## 城堡便装摘盔

灰袍领主便装摘掉头盔，其余甲胄保留，战斗仍戴盔；只改模板，不新增存档适配、读档补正或场景摘盔补丁。反馈截图 `C:/Users/lucif/Documents/Tencent Files/157652226/nt_qq/nt_data/Pic/2026-10/Ori/1f4cfb438e919396d0397636c1ec84c3.png` 是城堡全甲现象参考。（依据：用户裁定 2026-10-07）

回退：取回源码存档 `515a2eb` 中的 `gw_equipment_sets.xml` 并镜像，可恢复原便装 Head。修改前 live 模板与双端 DLL/PDB 备份至 `C:/Users/lucif/source/repos/GreyWardenPolicePurity/.codex_tmp/warden-civilian-head-before-20261007/`，DLL 保持原 SHA-256 `8ba222c1a650b894d2572525ae33140dc04c35ebfa8b90d4698bb191f2b796ff`。（依据：流程）

原版 1.4.8 的 `HeroAgentSpawnCampaignBehavior.CreateLocationCharacterForHero` 给城镇/城堡领主设置 `UseCivilianEquipment=true`，带队领主在村庄才为 false；`MissionAgentHandler.SpawnLocationCharacter` 把该选择传入 AgentBuildData。`Mission.SpawnAgent` 将英雄设为 FixedEquipment，按该选择克隆 `FirstCivilianEquipment` 或 `FirstBattleEquipment`；本链不检查 `ItemFlags.Civilian`，也不调用自动换装的 `CharacterHelper.CanUseItem`。XML 的 `Equipment.DeserializeNode` 只检查物品是否适合槽位；`Hero.CheckInvalidEquipmentsAndReplaceIfNeeded` 处理 Trash、未就绪物品/品质，不过滤便装栏里的盔甲。便装栏是独立的装备集合，不保证其中是普通衣服。（依据：C#反编译当前安装 v1.4.8，2026-10-07）

本模组 `gw_equipment_sets.xml` 的 `spc_gw_leader_0` 同时定义战斗套和 Civilian 套：战斗十槽含 `wcomhelmethv`，便装九槽无 Head，其余甲胄、武器和马具不变；六位创始领主在 `spspecialcharacters.xml` 同时引用这两套。生成/成年发装的 `PoliceResourceManager.EnsureCommanderLoadout` 也分别复制该模板的战斗和便装装备。五件指挥甲胄在 items.xml 中均为 `Civilian=false`，不改变 NPC 生成时直接取便装栏的行为。（依据：源码与 XML 核对）

整套士兵预设补丁只处理非英雄 Battle 装备；火誓者军械库补丁跳过 AgentCivilianEquipment；自动换装候选保护不改场景装备选择。城堡穿着沿用模板，没有场景换装覆盖。已有英雄的便装已存档，不保证随 XML 更新而改变，不为此添加兼容逻辑。新模板与 live XML SHA-256 均为 `5702a7295b982ed45cee7ddb034979972fa59b9eef9c44b94445800d38368144`。（依据：C#反编译；用户裁定 2026-10-07）

灰袍头盔面罩开口：指挥官翼盔 `wcomhelmethv`、士兵翼盔 `winfhelmethv`（重步兵、骑士）与无翼盔 `winfhelmet` 的开口已由用户在美术里下移，男女都能看到眼睛，用户实测确认（2026-10-14）。改前开口偏高，女性完全看不到眼睛；开口一圈只绑头部骨骼，转头不会错位。开发版 live 的 `Assets` 已是新模型，玩家包 `pack0.tpac` 尚未重新发布（见 [布料资源](cloth-assets.md)）。

## 灰袍 NPC 的自动换装候选

灰袍 NPC 不应有换装欲望；不得改其他模组的代码、配置或方法，也不挂钩第三方方法。（依据：用户裁定 2026-10-07）

`GwpWardenAutoEquipmentPatch`（v14 替换包与 main 都有）只挂钩原版 Helpers.CharacterHelper.CanUseItem(BasicCharacterObject, EquipmentElement) 两参数布尔重载：CharacterObject.HeroObject 属于 gw 家族时返回 false，其余角色保留原版。带 out TextObject reason 的三参数重载不改，玩家装备界面的带原因判定保留。普通士兵没有灰袍 HeroObject，不受此限制。

冲突来源：RBM（RBMCampaign.dll）的 LordEquipmentUpgrade 在 PartiesBuyHorseCampaignBehavior.OnSettlementEntered 后执行：符合条件的非玩家家族领主进非敌城镇可从城镇/背包按 Tierf 换更高阶装备，原装备退入背包，没有灰袍排除。实际配置 `C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml` 中 RBMCampaign.Enabled=1、LordEquipmentUpgradeEnabled=1。因此有明确冲突入口；现有监控没有逐件换装记录，且 RBM 自身 SpoilsLoggingEnabled=0，不能断言用户看到的每次换装都由该入口造成。

实际 RBM 4.5.4 的 FindBestUsable / FindBestAffordable 在购买、从背包移走物品及退旧装备之前都询问该原版入口。两条路径因此没有灰袍可选候选，普通领主仍能选择高阶物品。这是本模组的原版资格限制，不依赖 RBM DLL，也不挂钩 RBM 的 IsManaged、UpgradeGear 或装备评分。

不复写已穿装备，不做进城后的恢复或轮询；本模组成年/生成角色发装直接复制装备，不走这项候选判定。已被换走的装备不靠本补丁自动还原。本补丁不处理原版锻造订单直接写入武器等绕过此资格入口的机制。（依据：C# 反编译、设计）

诊断仅 GWP_DIAGNOSTICS：WARDEN_AUTO_EQUIPMENT_REJECTED，每个英雄实例最多一条（弱引用表），记录候选和真实调用栈；确认灰袍进城后保持装备时同轮退休。弱引用去重仅用于诊断，不是进行中的玩法状态，因此不写存档。

离线回归 `tools/Verify-WardenAutoEquipment.ps1`：用真实原版和本机 RBMCampaign.dll 只读调用候选搜索，有 RBM 23 项、无 RBM 11 项断言，覆盖 gw/GW、普通英雄、普通士兵、手动装备重载与库存不变。

回退：撤 `4e39db2`。

## 兵种原配

- 每个灰袍兵种的原配都带灰袍标识；从灰袍兵 `gwrecruit` 起用模组自制的灰袍装备，原配不改。（依据：用户裁定 2026-10-16）
- 狂信者 `gwzealot` 是修会最底层的信众，三套原配都是原版物品加灰袍肩部印章 `warchshoulder`：朝圣者（朝圣兜帽、麻布长衣、皮鞋、草叉）、
  修士（布兜帽、学者长袍、皮鞋、手斧）、苦修者（黑兜帽、破长袍、裹布鞋、镰刀）。袍子和兜帽随家族配色，物品男女都能穿。
  之后由军械库换更好的装备，见 [军械库](firesworn-armory.md)。（依据：用户裁定 2026-10-16；物品与性别标记核对 SandBoxCore items）
- 回退：取回 `6855489` 的 `_Module/ModuleData/spnpccharacters.xml`（狂信者旧原配：破袍/僧袍、皮鞋、农具，两套肩部是兜帽），
  同时恢复 `GwpArmoryAllocation.AllocateArmoryKit` 末尾给狂信者/新兵换印章、去马的那段。

## 男女混编

- 灰袍兵男女五五混编，只有灰袍领主是女性。（依据：用户裁定 2026-10-07：男性负责传教相关，女性负责大局统领）
- 兵种定义（`spnpccharacters.xml`）仍是女性，所以头像、百科、部队名单、地图图标都显示女性；只在战场上每个兵分男女。
- 原版生成兵时，`AgentBuildData` 指定了性别就用指定的，否则用兵种的 `is_female`；没指定长相就按兵种的脸型范围和兵种性别生成
  （C#反编译 v1.4.8 `Mission.SpawnAgent`、`BasicCharacterObject.GetBodyProperties`）。
  `GwpWardenTroopGenderPatch` 按兵的种子（原版定装备、长相用的同一个）分一半为男：指定性别为男，
  长相照原版 `FaceGen.GetRandomBodyProperties`，用原版帝国士兵的男性脸型范围 `fighter_empire`（兵种自己的 `townswoman_empire` 是女性范围，没有胡子）。
  排在军械库换装之后，按换好的装备算头发遮挡。不存档：每场按种子重算，同一个兵每场一样。
- 自制盔甲每件只有一个模型，用户判断男女穿着效果一样，没另做。（依据：用户裁定 2026-10-07）
