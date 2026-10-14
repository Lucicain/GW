# 村庄、教士与仪式

[返回总览](README.md) · 19条。用户于2026-10-15批准并已应用。此页保留改写前后对照；日常维护见[玩家文字](../../state/player-text.md)。

## gwp_troop_zealot

**拟改**。出现情境：兵种树、名册、战场兵种名；现有二阶步兵。

当前调用：[_Module/ModuleData/spnpccharacters.xml:8](../../../_Module/ModuleData/spnpccharacters.xml:8)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 火誓狂热者 | 灰袍狂信者 |
| 英文 | Firesworn Zealot | Grey Warden Zealot |

## gwp_fs_priest_name

**拟改**。出现情境：村中教士姓名、名牌与百科链接。

当前调用：[GwpFireswornBehavior.cs:700](../../../GwpFireswornBehavior.cs:700)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火祭司{FIRSTNAME} | 灰袍教士{FIRSTNAME} |
| 英文 | Eternal Fire Priest {FIRSTNAME} | Grey Warden Priest {FIRSTNAME} |

## gwp_fs_priest_arrived

**拟改**。出现情境：新教士在村中落脚时的左下角灰色提示。

当前调用：[GwpFireswornBehavior.cs:688](../../../GwpFireswornBehavior.cs:688)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {PRIEST}在{VILLAGE}住了下来，照看永恒之火。 | {PRIEST}已在{VILLAGE}定居传教。 |
| 英文 | {PRIEST} has settled in {VILLAGE} to tend the Eternal Fire. | {PRIEST} has settled in {VILLAGE} to preach. |

## gwp_fs_ritual_congregation_title

**拟改**。出现情境：布道进行时进村，仪式菜单标题。

当前调用：[GwpFireswornMenus.cs:51](../../../GwpFireswornMenus.cs:51)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 集会传教 | 布道集会 |
| 英文 | Congregation | Congregation |

## gwp_fs_ritual_damnation_title

**拟改**。出现情境：火刑期间进村，仪式菜单标题。

当前调用：[GwpFireswornMenus.cs:50](../../../GwpFireswornMenus.cs:50)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 烈火责罚 | 火刑 |
| 英文 | Damnation | Burning at the Stake |

## gwp_fs_ritual_congregation_text

**拟改**。出现情境：布道期间的锁村画面；HOST无教士时会回退成村名，故使用中性的“主持方”。

当前调用：[GwpFireswornMenus.cs:64](../../../GwpFireswornMenus.cs:64)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的广场上挤满了人，{HOST}站在永恒之火前宣读火的戒律。要立誓的人一个接一个走上前，把手伸到火上起誓；围着的乡亲跟着他念。集会传教正在进行，这里没人顾得上你。大约还要{HOURS}小时。 | {VILLAGE}的村民聚在广场上听讲戒律，新入教的人正在宣誓。集会尚未结束，暂时无法办理村中事务。还需约{HOURS}小时。主持：{HOST}。 |
| 英文 | The square of {VILLAGE} is packed. {HOST} stands before the Eternal Fire reading out the laws of the flame, and those who would swear step forward one by one and hold their hands over the fire to take the oath; the villagers around them speak the words with him. A Congregation is under way, and nobody here has time for you. It will last about {HOURS} more hours. | The villagers of {VILLAGE} have gathered to hear the commandments, and new believers are taking their vows. Village business is suspended until the congregation ends, in about {HOURS} hours. Host: {HOST}. |

## gwp_fs_ritual_damnation_text

**拟改**。出现情境：火刑期间的锁村画面；不把火写成能裁定罪名的神，也不把实际被烧者一律称为罪犯。

当前调用：[GwpFireswornMenus.cs:62](../../../GwpFireswornMenus.cs:62)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的广场上燃着火堆。{HOST}正在举行永恒之火的烈火责罚：绑上火堆的，是火判了罪的人。立了誓的人围着火唱，乡亲们往火里添柴。烟呛得人睁不开眼。大约{HOURS}小时内，这里没人会搭理你。 | {VILLAGE}的广场上正在执行火刑。人被绑在木架上，围观的教众齐声祷告，浓烟飘过屋顶。村中事务暂时停办，还需约{HOURS}小时。主持：{HOST}。 |
| 英文 | A pyre is burning in the square of {VILLAGE}. {HOST} is holding a Damnation of the Eternal Fire: those bound to it are the ones the flame has judged. The sworn sing around the pyre while the villagers feed it wood. The smoke stings your eyes. Nobody here will deal with you for about {HOURS} more hours. | People are bound to stakes in the square of {VILLAGE}, where a burning is under way. The gathered believers pray as smoke drifts over the roofs. Village business is suspended for about {HOURS} more hours. Host: {HOST}. |

## gwp_fs_village_option

**拟改**。出现情境：有灰袍教众记录时的村庄菜单入口。

当前调用：[GwpFireswornMenus.cs:89](../../../GwpFireswornMenus.cs:89)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火：{HOUSEHOLDS} 户信众（{SHARE}%），{GUARD} 人执兵 | 灰袍教众：{HOUSEHOLDS}户（{SHARE}%），守村{GUARD}人 |
| 英文 | Eternal Fire: {HOUSEHOLDS} sworn hearths ({SHARE}%), {GUARD} under arms | Grey Warden faithful: {HOUSEHOLDS} households ({SHARE}%), {GUARD} defenders |

## gwp_fs_village_info

**拟改**。出现情境：点击村庄教众入口后的详情正文。

当前调用：[GwpFireswornMenus.cs:101](../../../GwpFireswornMenus.cs:101)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {VILLAGE}的{HEARTH}户人家里，有{HOUSEHOLDS}户向永恒之火立了誓，这些人家有{GUARD}个儿女拿着兵器。火的规矩由祭司和立了誓的人守着；村里人照常种地过日子。劫掠的人来了，拿起长矛的就是这些立了誓的男男女女。 | {VILLAGE}共有{HEARTH}户，其中{HOUSEHOLDS}户奉行灰袍的戒律，{GUARD}名教众负责守村。他们信奉上天，听从教士的教诲，也为灰袍提供兵员。 |
| 英文 | {HOUSEHOLDS} of the {HEARTH} hearths of {VILLAGE} have sworn themselves to the Eternal Fire, and {GUARD} of their sons and daughters stand under arms. The priest and the sworn keep the flame's laws; the rest of the village works its fields as it always has. When raiders come, it is these sworn men and women who take up the spear. | Of the {HEARTH} households in {VILLAGE}, {HOUSEHOLDS} follow the Grey Wardens' rule, and {GUARD} believers defend the village. They worship Heaven, hear the priest's teachings, and provide recruits for the Wardens. |

## gwp_fs_village_priest

**拟改**。出现情境：教众详情中存在在世教士的分支。

当前调用：[GwpFireswornMenus.cs:104](../../../GwpFireswornMenus.cs:104)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {PRIEST}在这里照看永恒之火。灰袍要人上阵时，村里立了誓的儿女们就从他的火堆边跟着走。 | {PRIEST}在此传教，并照管教众。灰袍来征募时，村中愿意从军的人由他引荐。 |
| 英文 | {PRIEST} tends the Eternal Fire here. When the Wardens need fighters, the sworn sons and daughters of this village go with them from his hearth. | {PRIEST} preaches here and tends to the faithful. When the Wardens recruit, he introduces villagers willing to serve. |

## gwp_fs_village_no_priest

**拟改**。出现情境：教众详情中没有在世教士的分支。

当前调用：[GwpFireswornMenus.cs:106](../../../GwpFireswornMenus.cs:106)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 这里没有永恒之火的祭司。 | 村中暂时没有灰袍教士。 |
| 英文 | No priest of the Eternal Fire lives here. | There is currently no Grey Warden priest in the village. |

## gwp_fs_village_info_title

**拟改**。出现情境：村庄教众详情弹窗标题。

当前调用：[GwpFireswornMenus.cs:112](../../../GwpFireswornMenus.cs:112)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火 | 灰袍教众 |
| 英文 | The Eternal Fire | The Grey Warden Faithful |

## gwp_fs_village_info_ok

**保留**。出现情境：村庄教众详情弹窗关闭按钮。

当前调用：[GwpFireswornMenus.cs:113](../../../GwpFireswornMenus.cs:113)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 关闭 | 关闭 |
| 英文 | Close | Close |

## gwp_fs_ritual_leave

**保留**。出现情境：仪式锁村画面的离开按钮。

当前调用：[GwpFireswornMenus.cs:29](../../../GwpFireswornMenus.cs:29)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 离开…… | 离开…… |
| 英文 | Leave... | Leave... |

## gwp_fs_militia_change

**拟改**。出现情境：民兵每日变化的说明条目：抵消灰袍守村兵对应的原版退役项。

当前调用：[GwpFireswornMenus.cs:182](../../../GwpFireswornMenus.cs:182)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火立了誓的人不会离开民兵 | 灰袍教众继续留守 |
| 英文 | The Eternal Fire's sworn do not leave the militia | The Grey Warden faithful remain on watch |

## gwp_fs_militia_stopped

**拟改**。出现情境：教士势力已达本村最强时，民兵变化的说明条目。

当前调用：[GwpFireswornMenus.cs:176](../../../GwpFireswornMenus.cs:176)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火：这里由立了誓的人守着，不再募新民兵 | 灰袍教众负责守卫，停止征募新民兵 |
| 英文 | Eternal Fire: the sworn keep this village; no new militia is raised | The Grey Warden faithful guard the village; no new militia is raised |

## gwp_fs_priest_lore

**拟改**。出现情境：村中灰袍教士的百科介绍；宗教与分工说明。

当前调用：[GwpFireswornBehavior.cs:704](../../../GwpFireswornBehavior.cs:704)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | {PRIEST}在{VILLAGE}照看永恒之火。他传的教义说：统领这把火的是灰袍的女人们，走村串户替它传话的是祭司；谁都可以到火前求它照亮，火不问出身。在他的照看下，村里的男男女女向火立誓、拿起长矛，守着这座村子。 | {PRIEST}在{VILLAGE}传授灰袍的戒律。他教导村民敬畏上天，接济穷人，并服从修会的惩戒。灰袍的女统领掌管武装和巡察，他则召集教众、主持礼拜，为守村与从军的人备办衣食。 |
| 英文 | {PRIEST} keeps the Eternal Fire burning in {VILLAGE}. The faith he preaches holds that the women of the Grey Wardens lead the flame, and that its priests carry its word from village to village; anyone may come before it and ask for its light, and it does not ask where they were born. Under him the men and women of the village swear themselves to the fire and take up the spear to guard it. | {PRIEST} teaches the Grey Wardens' rule in {VILLAGE}. He urges villagers to revere Heaven, aid the poor, and submit to the order's discipline. The Wardens' women command its troops and patrols; he gathers the faithful, conducts worship, and helps provide for those who defend the village or join the ranks. |

## gwp_fs_village_stores

**拟改**。出现情境：村庄教众详情最后一段，显示地牢、军械与货物。

当前调用：[GwpFireswornMenus.cs:109](../../../GwpFireswornMenus.cs:109)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 地窖里关着{PRISONERS}个等着上火堆的人。库房里存着{ARMORY}件兵器甲胄，留给立了誓的人。还有值{GOODS}第纳尔的货，等着装车运去城里。 | 地窖关押着{PRISONERS}人，等候火刑。库房存有{ARMORY}件兵器甲胄，另有价值{GOODS}第纳尔的货物待运往集市。 |
| 英文 | {PRISONERS} wretches wait in the cellar for the pyre. The storeroom holds {ARMORY} pieces of arms and armour for the sworn. Goods worth {GOODS} denars wait to be carted to market. | The cellar holds {PRISONERS} people awaiting the stake. The stores contain {ARMORY} pieces of arms and armour, with goods worth {GOODS} denars awaiting transport to market. |

## gwp_fs_healing_hearth

**拟改**。出现情境：部队治疗速度的解释条目；这是加成来源，不是地图地点。

当前调用：[GwpWardenVisitDesire.cs:248](../../../GwpWardenVisitDesire.cs:248)、[GwpWardenVisitDesire.cs:257](../../../GwpWardenVisitDesire.cs:257)

| 语言 | 原文 | 改稿 |
|---|---|---|
| 中文 | 永恒之火的火堆边 | 灰袍教众的照料 |
| 英文 | Hearth of the Eternal Fire | Care from the Grey Warden faithful |
