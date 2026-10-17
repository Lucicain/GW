# 灰袍装备：来源与原版品质

> 当前状态：用户确认执勤旗修复后未见问题，家徽切换、重入不发装与退会收旗进入正式版
> 最后验收：2026-10-17 用户确认当前旗帜构建可用（`f00ca9b`，FC098662开发DLL，后续仅退休诊断）
> 已复核至：`7573179`（2026-10-17）
> 覆盖源码：`_Module/ModuleData/items.xml` `_Module/ModuleData/crafting_templates.xml` `_Module/ModuleData/gwp_crafting_pieces.xml` `_Module/ModuleData/gw_equipment_sets.xml` `_Module/ModuleData/spspecialcharacters.xml` `GwpWardenAutoEquipmentPatch.cs` `tools/Verify-WardenAutoEquipment.ps1` `GwpWardenTroopGenderPatch.cs` `GwpDutyBanner.cs` `tools/tests/test_duty_banner.py`
> 待实测：新档城堡/城镇大厅修士长袍、鸟徽肩饰与露面，野外战斗原套装；旗手数量与分配沿用原版；最佳品质、RBM不换装与男女混编沿原待验收项

本文件写"灰袍装备从哪来、带不带原版品质（`ItemModifier`，精良/大师/传奇等前缀）"。
`spspecialcharacters.xml` 中的领主外貌定义归 [灰袍家族](family.md)，本页覆盖其装备引用。（依据：职责划分）
灰袍自己的武器效果（击倒、破防等，代码里叫 `GwpWeaponTraits`）是另一回事，见 [troop-combat.md](troop-combat.md)。

## 只有灰袍能拿到

- 玩家和 NPC 都不得通过购买、打造、战利品获得灰袍战斗装备；玩家的战斗套装只在首次入会时由灰袍发放。新执勤旗是可主动补领的职务凭证，领取规则见[玩家](player-justice.md)。（依据：用户裁定 2026-10-02；执勤旗设计 2026-10-17）
- 全部灰袍物品 `is_merchandise="false"`。原版战利品（`DefaultBattleRewardModel.GetRandomItem`）、作坊产出、商队、比武奖品、
  藏身处与地图拾取都跳过 `NotMerchandise`（依据：C#反编译 v1.4.8，2026-10-02）。不要把灰袍物品改成商品。（依据：用户裁定 2026-10-02）
- 双刀专用刀身 `gwp_vlandian_blade_3_dual` 是 `is_hidden="true"`，锻造里解锁不到。其他打造类武器用原版部件，
  玩家能打出外观相近的武器，但物品 id 不同，不带灰袍武器效果。

## 入会发装

首次加入时（`PlayerBountyBehavior.GiveCommanderEquipment`，发放清单 `GwpIds.MembershipGrantItemIds`：
五件指挥甲胄、马具、黑色大盾与一副双刀），每件取所在品质组里最好的原版品质：`ItemQuality` 最高，
同档取 `PriceMultiplier` 最高。各组最高都是传奇（剑、锤、枪 伤害+7 速度+3；板甲 护甲+12；链甲 +9；皮甲 +7；布甲 +5；盾 耐久+210）。
这是灰袍装备带上品质的唯一途径；品质机制为日后复刻辛迪加设定铺垫。（依据：用户裁定 2026-10-02）

重新加入不再发整套装备；加入发执勤旗，退出收回执勤旗，旗帜是接新委托的开关。执勤外形换原版帝国最高档三级旗，原先一级旗效果保持；装备时家族/地图/战场显示灰袍黑金鸟徽，卸下或退会恢复玩家原徽。首次获装仍保留，原入退会声望/次数与新兵规则未要求调整。（依据：用户裁定 2026-10-17；保留效果为本轮实现选择）

本轮回退基线`7573179`，范围为GwpDutyBanner.cs、入退会/发装方法、GwpIds清单、旗帜物品与相应主文本/生成语言文件；回退须保留此前未提交的差事调度、协力与人物追责改动，不整文件覆盖工作树。（依据：流程）

已发出去的装备不补掷（不做存档兼容）。执勤旗、双刀配对、武器效果都按物品 id，不看品质；接单不再要求指挥装。

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

## 宗教常服与执勤旗

灰袍领主平民装备统一使用原版修士长袍 `monk_robe`、灰袍鸟徽肩饰 `warchshoulder` 与皮鞋 `leather_shoes`；无头部、手部、兵器、盾、坐骑及马具，便于城堡/城镇大厅会面看清脸。战斗套的十槽原配保持。修士长袍有原版男女模型变体并随团队色着色，实际配色和肩饰搭配待目视验收。（依据：用户裁定宗教常服、实现选材；原版物品XML核对 2026-10-17）

常服回退：取回 `7573179` 的 `gw_equipment_sets.xml` 并镜像，可恢复上一版“去盔、其余全甲”便装；反馈截图 `C:/Users/lucif/Documents/Tencent Files/157652226/nt_qq/nt_data/Pic/2026-10/Ori/1f4cfb438e919396d0397636c1ec84c3.png` 保留为此前城堡全甲现象参考。（依据：流程）

原版 1.4.8 的 `HeroAgentSpawnCampaignBehavior.CreateLocationCharacterForHero` 给城镇/城堡领主设置 `UseCivilianEquipment=true`，带队领主在村庄才为 false；`MissionAgentHandler.SpawnLocationCharacter` 把该选择传入 AgentBuildData。`Mission.SpawnAgent` 将英雄设为 FixedEquipment，按该选择克隆 `FirstCivilianEquipment` 或 `FirstBattleEquipment`；本链不检查 `ItemFlags.Civilian`，也不调用自动换装的 `CharacterHelper.CanUseItem`。XML 的 `Equipment.DeserializeNode` 只检查物品是否适合槽位；`Hero.CheckInvalidEquipmentsAndReplaceIfNeeded` 处理 Trash、未就绪物品/品质，不过滤便装栏里的盔甲。便装栏是独立的装备集合，不保证其中是普通衣服。（依据：C#反编译当前安装 v1.4.8，2026-10-07）

`gw_equipment_sets.xml` 的 `spc_gw_leader_0` 分别定义战斗与 Civilian 套，六位创始领主共同引用；生成/成年发装的 `PoliceResourceManager.EnsureCommanderLoadout` 分别复制两套。平民与战斗装备均由原版保存，模板不是每次进场景强制覆盖装备的补丁。（依据：源码与 XML 核对）

整套士兵预设补丁只处理非英雄 Battle 装备；军械库补丁跳过 AgentCivilianEquipment；自动换装保护不改场景装备选择。已有英雄的便装已入档，不保证随XML更新改变，不新增旧档修复或场景强制换装；常服按新档验收。（依据：C#反编译；用户存档裁定）

执勤旗 `gwp_duty_banner` 用原版帝国最高档三级模型/预制体 `empire_tier_3_banner` 与tableau，无新增美术；外形升级，旗帜效果仍为原一级伤亡士气冲击减10%，不附加灰袍身份战斗加成。它是非卖品，入会发放、退出收回；具体领取和退还规则只在[玩家入会与悬赏](player-justice.md)维护。（依据：用户反馈与原版旗帜XML核对；效果保持为实现选择 2026-10-17）

`GwpDutyBannerItemModel`从原版奖励池排除执勤旗，并阻止自动升级。执勤时三个读取入口统一从`GwpDutyBanner.GetDutyEmblem`返回灰袍家徽的独立副本：`Clan.Banner`负责玩家家族显示及其通常战团纹章，`MobileParty.Banner`保证主队地图标记覆盖原版CustomBanner优先分支，`PartyAgentOrigin.Banner`保持主队场景纹章。其他家族/势力与其他队的特殊旗帜保留原版；不改政治身份、国旗或玩家保存的原徽。（依据：用户反馈、原版读取链与生产补丁回归 2026-10-17）

原版Clan保存的是`[SaveableField(84)] _banner`，序列化生成入口直接读该字段；临时展示不写字段，卸旗/退会即返回原版结果，无备份旗帜和迁移状态。原版旗帜编辑器直接修改传入Banner，因此其活动状态下Clan读取保留玩家原徽，避免修改灰袍共享图案；普通显示返回独立副本，也不允许原版染色写回灰袍家徽。原版库存DoneLogic即使只换装备也发PlayerInventoryExchange，监听后标脏玩家家族战团地图视觉；退会收旗后同样刷新。（依据：本机v1.4.8托管源码、编辑器重新反编译与生产回归 2026-10-17）

专项生产42断言核对入退会/重入、收旗、补领、防随机掉落、家族/主队/场景显示、卸旗恢复、独立副本、编辑器及地图缓存刷新；XML检查帝国最高档外形和原一级效果。装备/旗帜及入会状态沿原版已有保存，无新增进行中字段。用户2026-10-17确认当前游玩未见问题；旗手数量继续按原版编队/队长规则。（依据：离线回归 2026-10-17）

执勤旗回退基线 `7573179`：恢复 `items.xml`、`GwpIds.cs`、`GwpCommon.cs`、两份PlayerBountyBehavior源文件及主文本，删除 `GwpDutyBanner.cs` 和SubModule中的旗帜模型注册；事务界面回退见[玩家](player-justice.md)。恢复时保留本轮之前的人物追责与协力工作树改动。（依据：流程）

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
