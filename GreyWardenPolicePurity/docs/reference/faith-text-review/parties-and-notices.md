# 地图队伍、对话与短通知

[返回总览](README.md) · 39条。用户于2026-10-15批准并已应用。此页保留改写前后对照；日常维护见[玩家文字](../../state/player-text.md)。

## gwp_fs_caravan_party

**拟改**。出现情境：卖货队的大地图队名。

当前调用：[GwpFireswornParties.cs:197](../../../GwpFireswornParties.cs:197)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的狂信者 | {VILLAGE}的教众货队 |
| 英文 | Zealots of {VILLAGE} | Faithful Traders of {VILLAGE} |

## gwp_fs_party_pretalk

**保留**。出现情境：地图遇到教众队伍，买卖取消后回到主对话。

当前调用：[GwpFireswornPartyDialogs.cs:29](../../../GwpFireswornPartyDialogs.cs:29)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 还有什么事？ | 还有什么事？ |
| 英文 | Anything else? | Anything else? |

## gwp_fs_party_buy

**拟改**。出现情境：只有卖货队有货、总价大于零且玩家付得起时显示的购买选项。

当前调用：[GwpFireswornPartyDialogs.cs:32](../../../GwpFireswornPartyDialogs.cs:32)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 你们车上是什么？我买了。 | 你们带了什么货？ |
| 英文 | What are you carrying? I'll buy it. | What goods are you carrying? |

## gwp_fs_party_offer

**拟改**。出现情境：卖货队报价，不把钱说成归某团火所有。

当前调用：[GwpFireswornPartyDialogs.cs:34](../../../GwpFireswornPartyDialogs.cs:34)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 有{PRODUCTS}。全拿走一共{TOTAL_PRICE}{GOLD_ICON}，钱归永恒之火。 | 有{PRODUCTS}，共计{TOTAL_PRICE}{GOLD_ICON}。这些钱要带回去供养教众。 |
| 英文 | We have {PRODUCTS}. All of it for {TOTAL_PRICE}{GOLD_ICON}, and the coin goes to the Eternal Fire. | We have {PRODUCTS}, for {TOTAL_PRICE}{GOLD_ICON} in all. The money will go home to support the faithful. |

## gwp_fs_party_buy_yes

**拟改**。出现情境：玩家确认买下整批货物。

当前调用：[GwpFireswornPartyDialogs.cs:37](../../../GwpFireswornPartyDialogs.cs:37)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 成交，这是{TOTAL_PRICE}{GOLD_ICON}。 | 成交，这是{TOTAL_PRICE}{GOLD_ICON}。 |
| 英文 | Done. Here is {TOTAL_PRICE}{GOLD_ICON}. | Agreed. Here is {TOTAL_PRICE}{GOLD_ICON}. |

## gwp_fs_party_buy_no

**保留**。出现情境：玩家取消购买。

当前调用：[GwpFireswornPartyDialogs.cs:39](../../../GwpFireswornPartyDialogs.cs:39)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 算了。 | 算了。 |
| 英文 | Never mind. | Never mind. |

## gwp_fs_party_threaten

**保留**。出现情境：玩家威胁任意教众队伍。

当前调用：[GwpFireswornPartyDialogs.cs:42](../../../GwpFireswornPartyDialogs.cs:42)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 把东西都交出来。 | 把东西都交出来。 |
| 英文 | Hand over everything you have. | Hand over everything you have. |

## gwp_fs_party_refuse

**拟改**。出现情境：教众拒绝玩家威胁；没有交钱或投降分支。

当前调用：[GwpFireswornPartyDialogs.cs:44](../../../GwpFireswornPartyDialogs.cs:44)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 火不会向你低头。想要我们的东西，就从我们身上踏过去。 | 我们不会交给你。要抢，就拔剑。 |
| 英文 | The fire does not bow to you. If you want anything of ours, take it over our bodies. | We will not hand it over. Draw your sword if you mean to take it. |

## gwp_fs_party_fight

**拟改**。出现情境：威胁被拒后，玩家结束对话回到遭遇菜单；此项本身不直接发起战斗。

当前调用：[GwpFireswornPartyDialogs.cs:47](../../../GwpFireswornPartyDialogs.cs:47)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 那就这样。 | 那就没什么可谈的了。 |
| 英文 | So be it. | Then we have nothing more to discuss. |

## gwp_fs_party_back_off

**拟改**。出现情境：玩家收回威胁并离开遭遇。

当前调用：[GwpFireswornPartyDialogs.cs:49](../../../GwpFireswornPartyDialogs.cs:49)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 算了，走吧。 | 算了，你们走吧。 |
| 英文 | Never mind. Go on your way. | Leave, then. |

## gwp_fs_party_leave

**拟改**。出现情境：正常结束教众队伍对话并离开遭遇。

当前调用：[GwpFireswornPartyDialogs.cs:52](../../../GwpFireswornPartyDialogs.cs:52)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 那你们走吧。 | 你们继续赶路吧。 |
| 英文 | Carry on, then. | Carry on your way. |

## gwp_fs_party_greet_cart_out

**拟改**。出现情境：地图遇到向集市出发的卖货队。

当前调用：[GwpFireswornPartyDialogs.cs:74](../../../GwpFireswornPartyDialogs.cs:74)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 我们是{HOME}立了誓的人。车上是村里的货，要拉去{TARGET}，卖得的钱归永恒之火。 | 我们从{HOME}来，要把这车货送到{TARGET}去卖，所得用来供养村里的教众。 |
| 英文 | We are the sworn of {HOME}. These are the village's goods, bound for {TARGET}; what they fetch belongs to the Eternal Fire. | We come from {HOME}, taking these goods to sell in {TARGET}. The proceeds will support the faithful back home. |

## gwp_fs_party_greet_mission

**拟改**。出现情境：地图遇到前往新村的传教队。

当前调用：[GwpFireswornPartyDialogs.cs:68](../../../GwpFireswornPartyDialogs.cs:68)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 愿火照亮你的路。我们是{HOME}立了誓的人，要去{TARGET}把火带过去，从此留在那里。 | 我们从{HOME}来，要到{TARGET}传教，此后就在那里住下。 |
| 英文 | May the fire light your road. We are the sworn of {HOME}, and we go to {TARGET} to carry the fire there and stay. | We come from {HOME}, bound for {TARGET} to preach and settle there. |

## gwp_fs_party_greet_balance

**拟改**。出现情境：地图遇到运械队；抵达后人也留下。

当前调用：[GwpFireswornPartyDialogs.cs:71](../../../GwpFireswornPartyDialogs.cs:71)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 我们是{HOME}立了誓的人。这些兵器是给{TARGET}立誓的人的，送到了我们就留在那里。 | 我们从{HOME}来，为{TARGET}的教众运送兵器。送到以后，我们也留在那里守村。 |
| 英文 | We are the sworn of {HOME}. These arms are for the sworn of {TARGET}, and we stay with them once they are delivered. | We come from {HOME}, carrying arms for the faithful of {TARGET}. Once they are delivered, we will stay to help guard the village. |

## gwp_fs_mission_party

**拟改**。出现情境：传教队的大地图队名。

当前调用：[GwpFireswornParties.cs:194](../../../GwpFireswornParties.cs:194)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火门徒 | 灰袍传教队 |
| 英文 | Disciples of the Eternal Fire | Grey Warden Mission |

## gwp_fs_balance_party

**保留**。出现情境：运械队的大地图队名。

当前调用：[GwpFireswornParties.cs:196](../../../GwpFireswornParties.cs:196)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的运械队 | {VILLAGE}的运械队 |
| 英文 | Armsbearers of {VILLAGE} | Armsbearers of {VILLAGE} |

## gwp_fs_status_balance

**拟改**。出现情境：运械队出发阶段的部队行为说明。

当前调用：[GwpFireswornBehavior.cs:615](../../../GwpFireswornBehavior.cs:615)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 正把{HOME}的兵器运给{TARGET}立誓的人。 | 正从{HOME}运送兵器给{TARGET}的教众。 |
| 英文 | Carrying arms from {HOME} to the sworn of {TARGET}. | Carrying arms from {HOME} to the faithful of {TARGET}. |

## gwp_notice_prisoners

**拟改**。出现情境：领主向村庄地牢交人后，村庄名牌上的短通知。

当前调用：[GwpFireswornDungeon.cs:148](../../../GwpFireswornDungeon.cs:148)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 交出 {COUNT} 名俘虏 | 交付{COUNT}名俘虏 |
| 英文 | Handed over {COUNT} prisoners | Handed over {COUNT} prisoners |

## gwp_notice_donated

**拟改**。出现情境：领主交物资后村庄名牌短通知；包含军械与货物，不全是装备。

当前调用：[GwpFireswornDungeon.cs:151](../../../GwpFireswornDungeon.cs:151)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 捐出 {COUNT} 件东西 | 捐赠{COUNT}件物资 |
| 英文 | Donated {COUNT} items | Donated {COUNT} items |

## gwp_notice_congregation

**拟改**。出现情境：布道完成后的村庄名牌通知；COUNT来自信众新增户数，原中文“人”单位不符。

当前调用：[GwpFireswornRituals.cs:94](../../../GwpFireswornRituals.cs:94)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {COUNT} 人向永恒之火立誓 | {COUNT}户加入灰袍教众 |
| 英文 | {COUNT} swore to the Eternal Fire | {COUNT} households joined the Grey Warden faithful |

## gwp_notice_damnation

**拟改**。出现情境：火刑处理后的村庄名牌通知；BURNED由人数与户数片段组合。

当前调用：[GwpFireswornRituals.cs:266](../../../GwpFireswornRituals.cs:266)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {BURNED}上了火堆，{SWORN} 户立誓 | {BURNED}遭火刑，另有{SWORN}户入教 |
| 英文 | {BURNED} went to the pyre; {SWORN} households swore | {BURNED} were burned at the stake; {SWORN} more households joined the faith |

## gwp_notice_burned_people

**拟改**。出现情境：火刑通知中的俘虏与民兵人数片段。

当前调用：[GwpFireswornRituals.cs:262](../../../GwpFireswornRituals.cs:262)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {COUNT} 人 | {COUNT}人 |
| 英文 | {COUNT} people | {COUNT} people |

## gwp_notice_burned_homes

**拟改**。出现情境：火刑通知中的不信教户数片段，不改写成人数。

当前调用：[GwpFireswornRituals.cs:264](../../../GwpFireswornRituals.cs:264)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {COUNT} 户 | {COUNT}户 |
| 英文 | {COUNT} households | {COUNT} households |

## gwp_notice_mission

**拟改**。出现情境：传教队抵达后村庄名牌短通知；COUNT是实际留下的兵员人数。

当前调用：[GwpFireswornParties.cs:469](../../../GwpFireswornParties.cs:469)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 带来 {COUNT} 名立誓者，留在此地 | {COUNT}名教众在此定居 |
| 英文 | Brought {COUNT} sworn to stay | {COUNT} believers settled here |

## gwp_notice_balance

**拟改**。出现情境：运械队抵达后的村庄名牌通知。

当前调用：[GwpFireswornParties.cs:470](../../../GwpFireswornParties.cs:470)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 运来 {COUNT} 件兵器护甲 | 运来{COUNT}件兵器甲胄 |
| 英文 | Brought {COUNT} pieces of arms | Delivered {COUNT} pieces of arms and armour |

## gwp_notice_market

**拟改**。出现情境：卖货队在城镇卖货后的城镇名牌通知。

当前调用：[GwpFireswornParties.cs:670](../../../GwpFireswornParties.cs:670)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 卖出 {COUNT} 件货物 | 售出{COUNT}件货物 |
| 英文 | Sold {COUNT} goods | Sold {COUNT} goods |

## gwp_notice_rebuilt

**拟改**。出现情境：灰袍完成重建后的村庄名牌通知。

当前调用：[GreyWardenVillageReconstructionBehavior.cs:380](../../../GreyWardenVillageReconstructionBehavior.cs:380)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 重建了这座村子 | 重建了这座村子 |
| 英文 | Rebuilt the village | Rebuilt this village |

## gwp_notice_issue_solved

**拟改**。出现情境：灰袍完成要人事务后的聚落名牌通知。

当前调用：[GreyWardenIssueResolutionBehavior.cs:268](../../../GreyWardenIssueResolutionBehavior.cs:268)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 解决了一个请愿 | 处理了一件请愿 |
| 英文 | Solved an issue | Resolved a petition |

## gwp_fs_status_mission

**拟改**。出现情境：传教队出发阶段的部队行为说明。

当前调用：[GwpFireswornBehavior.cs:613](../../../GwpFireswornBehavior.cs:613)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 正把永恒之火带往{TARGET}。 | 正前往{TARGET}传教。 |
| 英文 | Carrying the Eternal Fire to {TARGET}. | Travelling to {TARGET} to preach. |

## gwp_fs_status_market

**拟改**。出现情境：卖货队出发阶段的部队行为说明。

当前调用：[GwpFireswornBehavior.cs:617](../../../GwpFireswornBehavior.cs:617)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 正把{HOME}的货运去{TARGET}的集市。 | 正把{HOME}的货物运往{TARGET}集市。 |
| 英文 | Taking the goods of {HOME} to market in {TARGET}. | Taking goods from {HOME} to market in {TARGET}. |

## gwp_fs_status_returning

**保留**。出现情境：卖货队返回阶段的部队行为说明。

当前调用：[GwpFireswornBehavior.cs:620](../../../GwpFireswornBehavior.cs:620)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 正返回{HOME}。 | 正返回{HOME}。 |
| 英文 | Returning to {HOME}. | Returning to {HOME}. |

## gwp_fs_status_home

**拟改**。出现情境：已登记教众队处于本村阶段的部队行为说明。

当前调用：[GwpFireswornBehavior.cs:621](../../../GwpFireswornBehavior.cs:621)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 在{HOME}守着火。 | 驻守{HOME}。 |
| 英文 | Keeping watch in {HOME}. | Keeping watch in {HOME}. |

## gwp_fs_status_visit

**拟改**。出现情境：灰袍领主在村庄停留且有本次交人、捐物或招兵记录时的行为说明。

当前调用：[GwpFireswornBehavior.cs:629](../../../GwpFireswornBehavior.cs:629)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 在{VILLAGE}的永恒之火旁：交出{PRISONERS}名俘虏、{DONATED}件装备，收了{RECRUITS}名立誓者入伍。 | 在{VILLAGE}交付{PRISONERS}名俘虏、捐赠{DONATED}件物资，招募{RECRUITS}名教众。 |
| 英文 | At the Eternal Fire in {VILLAGE}: handed over {PRISONERS} prisoners and {DONATED} pieces of gear, took {RECRUITS} sworn into the ranks. | In {VILLAGE}: handed over {PRISONERS} prisoners, donated {DONATED} items, and recruited {RECRUITS} believers. |

## gwp_fs_status_healing

**拟改**。出现情境：灰袍领主在教众村庄停留、无本次交割记录且有伤兵时。

当前调用：[GwpFireswornBehavior.cs:632](../../../GwpFireswornBehavior.cs:632)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 在{VILLAGE}的永恒之火旁照料伤员。 | 在{VILLAGE}照料伤员。 |
| 英文 | Tending the wounded at the Eternal Fire in {VILLAGE}. | Tending the wounded in {VILLAGE}. |

## gwp_fs_status_resting

**拟改**。出现情境：灰袍领主在教众村庄停留、无本次交割记录且没有伤兵时。

当前调用：[GwpFireswornBehavior.cs:635](../../../GwpFireswornBehavior.cs:635)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 在{VILLAGE}的永恒之火旁休整。 | 在{VILLAGE}休整。 |
| 英文 | Resting at the Eternal Fire in {VILLAGE}. | Resting in {VILLAGE}. |

## gwp_fs_party_greet_cart_back

**拟改**。出现情境：地图遇到返村阶段的卖货队；返程可能仍有余货，故不再说全部售完。

当前调用：[GwpFireswornPartyDialogs.cs:76](../../../GwpFireswornPartyDialogs.cs:76)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 货卖完了，我们回{HOME}去。 | 我们在集市卖过货，正返回{HOME}。 |
| 英文 | The goods are sold. We are going home to {HOME}. | We have traded at market and are returning to {HOME}. |

## gwp_fs_party_products_separator

**保留**。出现情境：货物列表与火刑人数/户数组合所用分隔符。

当前调用：[GwpFireswornPartyDialogs.cs:103](../../../GwpFireswornPartyDialogs.cs:103)、[GwpFireswornRituals.cs:267](../../../GwpFireswornRituals.cs:267)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 、 | 、 |
| 英文 | ,  | ,  |

## gwp_fs_party_product

**保留**。出现情境：卖货队报价中的单个货物条目。

当前调用：[GwpFireswornPartyDialogs.cs:106](../../../GwpFireswornPartyDialogs.cs:106)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {COUNT}件{ITEM} | {COUNT}件{ITEM} |
| 英文 | {COUNT} {ITEM} | {COUNT} {ITEM} |

## gwp_fs_party_products_more

**保留**。出现情境：卖货报价超过四种货物时追加的省略片段。

当前调用：[GwpFireswornPartyDialogs.cs:109](../../../GwpFireswornPartyDialogs.cs:109)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 等 | 等 |
| 英文 |  and more |  and more |
