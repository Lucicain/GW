# 开发版库房界面

[返回总览](README.md) · 13条。用户于2026-10-15批准并已应用。此页保留改写前后对照；日常维护见[玩家文字](../../state/player-text.md)。

## gwp_fs_armory_option

**拟改**。出现情境：仅开发版：有守村队的村庄菜单库房入口。

当前调用：[GwpFireswornArmoryView.cs:25](../../../GwpFireswornArmoryView.cs:25)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火：库房 | 查看教众库房 |
| 英文 | Eternal Fire: storeroom | Inspect the faithful's stores |

## gwp_fs_armory_close

**保留**。出现情境：仅开发版：库房明细关闭按钮。

当前调用：[GwpFireswornArmoryView.cs:57](../../../GwpFireswornArmoryView.cs:57)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 关闭 | 关闭 |
| 英文 | Close | Close |

## gwp_fs_armory_section_arms

**拟改**。出现情境：仅开发版：库房明细中的军械分组。

当前调用：[GwpFireswornArmoryView.cs:47](../../../GwpFireswornArmoryView.cs:47)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 军械库（给立了誓的人配的兵器护甲） | 军械 |
| 英文 | Arms for the sworn | Arms and Armour |

## gwp_fs_armory_section_goods

**拟改**。出现情境：仅开发版：村庄库房明细中的货仓分组。

当前调用：[GwpFireswornArmoryView.cs:50](../../../GwpFireswornArmoryView.cs:50)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 货仓（要拉进城卖的） | 待售货物 |
| 英文 | Stores to sell | Goods for Sale |

## gwp_fs_armory_section_inventory

**拟改**。出现情境：仅开发版：领主库房明细中的背包分组。

当前调用：[GwpFireswornArmoryView.cs:51](../../../GwpFireswornArmoryView.cs:51)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 背包 | 随队物资 |
| 英文 | Baggage | Baggage |

## gwp_fs_armory_section

**拟改**。出现情境：仅开发版：库房每个分组的数量与估值标题。

当前调用：[GwpFireswornArmoryView.cs:69](../../../GwpFireswornArmoryView.cs:69)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | —— {TITLE}（{COUNT}件，值{VALUE}）—— | {TITLE}（{COUNT}件，价值{VALUE}） |
| 英文 | —— {TITLE} ({COUNT} pieces, worth {VALUE}) —— | {TITLE} ({COUNT} pieces, worth {VALUE}) |

## gwp_fs_lord_armory_ask

**拟改**。出现情境：仅开发版：与带队灰袍领主正常交谈时查看物资的玩家选项。

当前调用：[GwpFireswornArmoryView.cs:35](../../../GwpFireswornArmoryView.cs:35)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 让我看看你们队伍的库房。 | 让我看看你们的随队物资。 |
| 英文 | Show me your company's storeroom. | Let me see your company's stores. |

## gwp_fs_lord_armory_reply

**拟改**。出现情境：仅开发版：领主回复后打开库房明细。

当前调用：[GwpFireswornArmoryView.cs:40](../../../GwpFireswornArmoryView.cs:40)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 都在这儿了，带着的东西、谁在穿，你看吧。 | 清单在这里。 |
| 英文 | Here. Everything we carry, and who wears it. | Here is the inventory. |

## gwp_fs_armory_item

**保留**。出现情境：仅开发版：库房物品逐项显示。

当前调用：[GwpFireswornArmoryView.cs:77](../../../GwpFireswornArmoryView.cs:77)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {NAME} × {COUNT} | {NAME} × {COUNT} |
| 英文 | {NAME} × {COUNT} | {NAME} × {COUNT} |

## gwp_fs_armory_village

**保留**。出现情境：仅开发版：村庄库房弹窗标题。

当前调用：[GwpFireswornArmory.cs:700](../../../GwpFireswornArmory.cs:700)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的军械库（{ITEMS}件） | {VILLAGE}的军械库（{ITEMS}件） |
| 英文 | Armory of {VILLAGE} ({ITEMS} pieces) | Armory of {VILLAGE} ({ITEMS} pieces) |

## gwp_fs_armory_party

**保留**。出现情境：仅开发版：领主部队库房弹窗标题。

当前调用：[GwpFireswornArmory.cs:702](../../../GwpFireswornArmory.cs:702)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {PARTY}的军械库（{ITEMS}件） | {PARTY}的军械库（{ITEMS}件） |
| 英文 | Armory of {PARTY} ({ITEMS} pieces) | Armory of {PARTY} ({ITEMS} pieces) |

## gwp_fs_armory_empty

**拟改**。出现情境：仅开发版：该持有者尚无军械账本时的说明。

当前调用：[GwpFireswornArmory.cs:712](../../../GwpFireswornArmory.cs:712)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 库里还什么都没有。 | 库房为空。 |
| 英文 | Nothing here yet. | The stores are empty. |

## gwp_fs_armory_summary

**拟改**。出现情境：仅开发版：库房摘要。MEN为配装记录数，ARMED为其中配有主手兵器的记录数。

当前调用：[GwpFireswornArmory.cs:714](../../../GwpFireswornArmory.cs:714)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 从这里领装备的有{MEN}人，其中{ARMED}人领到了兵器。另有{FREE}件留给下一批立誓的人。{SURPLUS}件没人用得上，会拉去卖掉。 | 这里为{MEN}人保管装备，其中{ARMED}人已配有兵器；另存{FREE}件备用装备，{SURPLUS}件余货将运往集市出售。 |
| 英文 | {MEN} of the sworn draw from this store; {ARMED} of them have a weapon from it. {FREE} pieces are kept back for those who will swear next. {SURPLUS} pieces nobody needs, and they will be sold. | These stores supply {MEN} people, of whom {ARMED} have weapons. Another {FREE} pieces are held in reserve, and {SURPLUS} surplus pieces will be sold at market. |
