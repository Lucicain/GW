# 纠察队与灰袍领主拦截玩家：按游玩顺序审阅台词

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[纠察队](../state/patrol.md)和[玩家声望](../state/player-justice.md)，赎罪见[赎罪委派](../state/atonement.md)。

场景说明只给审阅时看，角色不会念出来。表里每条写明键、现中文 / 现英文、问题和建议改稿；玩家回应是可选分支，不是连着说。

## 核实到的情境（作者用，不进台词）

- 玩家灰袍声望在 −1 至 −10 之间、且不在放行保护期内，灰袍派一支纠察队追玩家。这时玩家**没有被通缉**。纠察队开的罚金是负声望 × 200；也可以私下给四分之一放行：声望不变，四天内不再派队。拒绝或私了没谈成就宣战开打。
- 声望降到 −11 及以下，纠察队收回，改由正式灰袍领主带队追捕（`PLAYER_WANTED`，即通缉）。领主开的罚金是负声望 × 100；钱不够时可以认罪领一个赎罪任务。另有一种情况：玩家队里的同伴有开放案件，领主会拦下要同伴的罚金，必须由玩家一次付清。
- 被纠察队打败：押到城门，按每点 200 收身上的现金后放人，足额才归零；被正式灰袍打败：押到城堡，按每点 300 收，没交够仍保持通缉。
- 纠察队返回驻地途中被玩家撞上，有专门的一句。返程的原因可能是案子结了、收了私了的钱，**也可能是玩家降到 −11、改由正式灰袍接手**。
- 赎罪任务可以靠谈判、收款、接受投案或击败目标来完成，不是只能打败目标。
- 「钱不够就拿货物来补」（`_014`、`dialogue_009` 改稿）沿用现文「可在谈判界面继续出价」的意思，即原版交易界面里能放物品补差。这一点是 agent 推断：云端没有原版源码库，没有逐行核实。实装前要在源码库里确认原版交易会给这两处交易列出物品；列不出就改回「钱不够，能拿多少先拿出来」。

## 通用问题

1. 角色把「你当前负声望 N」「你当前携带 N 金」「可在谈判界面继续出价」「（声望不变）」念给玩家，是机制说明和界面用语，不是人话。改稿把这些拿掉；玩家的钱在屏幕上本来就看得到，交易界面会自己显示。
2. 「本轮案件」「拒绝执法已记录」「拒捕记录在案」「不再受理交涉」是公文和开发口吻。
3. 「按灰袍法令」：游戏里没有成文法令（见[核实稿](text-review-term-check-20261104.md)）。
4. 纠察队阶段说「通缉撤销」不对，这时玩家没被通缉。
5. 纠察队那一路的英文大量机翻（“Penalty confirmation. This round of cases is concluded.”“Refusal to Enforcement Recorded. Pickets, make arrests!”），全部重写。

**去掉的变量**：`_013`、`_014`、`_015` 的携带金额 / 负声望，`dialogue_008`、`_009`、`_010`、`_015`、`gwp_enforcement_companion_insufficient` 的相应数字，中英两边同时去掉，变量名集合仍一致。代码照旧传这些参数，原版 TextObject 不用的变量会忽略，不用改代码。

## 一、纠察队追上来（声望 −1 至 −10，未通缉）

### 地图提示

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_028` | 灰袍纠察队正前来缉拿你。 / A Grey Warden patrol is coming for you. | 无 | 保留 | 保留 |
| `gwp_policepatrolbehavior_036` | 灰袍纠察队已从{VAR_1}出发追捕你。 / A Grey Warden patrol has set out from {VAR_1} after you. | 无 | 保留 | 保留 |

### 纠察队开口

[纠察队拦住玩家，强制进入对话。开场一句由 `_015` 加上 `_013` 或 `_014` 拼成，看玩家身上的钱够不够。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_015` | 站住！纠察队正在执法。你当前负声望 {VAR_1}，正式罚金 {VAR_2} 金。{VAR_3} / Stand! This provost patrol acts under warrant. Your standing is {VAR_1}; the lawful fine is {VAR_2} denars. {VAR_3} | 念负声望数值；英文 “under warrant” 与未通缉不符 | 站住！你这些日子干的事，灰袍都知道了。交{VAR_2}第纳尔罚金，这事就算了。{VAR_3} | Halt! The Wardens know what you've been up to. Pay a fine of {VAR_2} denars and the matter ends here. {VAR_3} |
| `gwp_policepatrolbehavior_013` | 你当前携带 {VAR_1} 金，可直接缴清。 / You carry {VAR_1} denars, enough to pay in full. | 机制说明 | 你身上的钱够数。 | You have enough on you. |
| `gwp_policepatrolbehavior_014` | 你当前携带 {VAR_1} 金，可在谈判界面继续出价或选择拒绝。 / You carry {VAR_1} denars. You may raise your offer at the table or refuse the order. | 界面用语 | 钱不够，就拿货物来补。 | If you're short of coin, make it up in goods. |

### 玩家回应

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_016` | 缴纳正式罚金（{VAR_1} 金，结束本轮追捕） / Pay the lawful fine ({VAR_1} denars; end this pursuit) | 公文腔、机制括注 | 好，罚金我交。（{VAR_1}第纳尔） | All right, I'll pay the fine. ({VAR_1} denars) |
| `gwp_policepatrolbehavior_017` | 谈判放行（目标 {VAR_1} 金，声望不变） / Negotiate passage ({VAR_1} denars sought; standing unchanged) | 「声望不变」是机制说明 | 这点钱你们拿去，就当没见过我。（{VAR_1}第纳尔） | Take this and forget you saw me. ({VAR_1} denars) |
| `gwp_policepatrolbehavior_009` | 我拒绝执法。 / I refuse enforcement. | 不是人话 | 要钱没有。想抓我，就来试试。 | You'll get nothing from me. Take me if you can. |

待用户定：`_017` 去掉「声望不变」以后，按钮上看不出私了和正式交罚的区别（私了保留负声望，换四天不派队）。如果要保留这个提示，建议改成「（{VAR_1}第纳尔，账还记着）」/ “({VAR_1} denars; the debt stands)”。

### 交罚金

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_003` | 按灰袍法令，先把正式罚金缴清。 / According to the Grey Wardens decree, the formal fine must be paid first. | 没有「灰袍法令」 | 那就点清楚，一个子儿也不能少。 | Then count it out. Every coin. |
| `gwp_policepatrolbehavior_020` | 缴纳正式罚金 / Pay the formal fine | 交易界面条目名，公文腔 | 交给纠察队的罚金 | Fine paid to the patrol |
| `gwp_policepatrolbehavior_004` | 罚金确认。本轮案件到此结案。 / Penalty confirmation. This round of cases is concluded. | 开发口吻、英文机翻 | 数目对了。这事到此为止，你走吧。 | That's the full sum. This ends here. Go. |
| `gwp_policepatrolbehavior_022` | 罚金已缴清，通缉撤销，纠察队撤离。 / The fine is paid and you are no longer wanted. The patrol withdraws. | 说法不准：这时并未通缉 | 罚金已缴清，纠察队撤走了。 | The fine is paid. The patrol withdraws. |
| `gwp_policepatrolbehavior_005` | 你的出价低于正式罚金。你可以继续出价，或拒绝执法。 / Your bid is less than the formal penalty. You can continue bidding, or refuse enforcement. | 界面用语、英文机翻 | 不够数。要么补齐，要么就别怪我们动手。 | That's short. Make up the rest, or we do this the hard way. |

### 私下给钱放行

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_006` | 可以，报出你的放行价码。 / Yes, quote your release price. | 纠察队不会这样请人报价 | 哦？想私了？拿出来看看。 | Oh? Settle it quietly? Let's see it. |
| `gwp_policepatrolbehavior_019` | 缴纳通融费 / Pay the facilitation fee | 交易界面条目名，「通融费」生硬 | 私下塞给纠察队的钱 | Money slipped to the patrol |
| `gwp_policepatrolbehavior_007` | 够了。你可以走了。 / That will do. You may go. | 可以更像收了钱的人 | 行，今天没见过你。走吧。 | Fine. I never saw you today. Go. |
| `gwp_policepatrolbehavior_021` | 纠察队同意放行。 / The patrol lets you pass. | 无大问题，补上收钱这件事 | 纠察队收了钱，放你过去了。 | The patrol takes your money and lets you pass. |
| `gwp_policepatrolbehavior_008` | 这点钱可不够。放下武器！ / That is not enough. Lay down your weapons! | 可保留，稍改口语 | 就这么点？打发谁呢。拿下！ | That's all? Who do you take us for? Seize them! |

### 拒绝，或私了没谈成

[`_010` 是纠察队队长的话；`_018` 和 `_034` 是开打时的地图提示。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_010` | 拒绝执法已记录。纠察队，执行抓捕！ / Refusal to Enforcement Recorded. Pickets, make arrests! | 公文腔、英文机翻 | 好，那就别怪我们了。拿下！ | So be it. Take them! |
| `gwp_policepatrolbehavior_018` | 你拒绝伏法，纠察队将强行缉拿。 / You refuse to submit. The patrol will take you by force. | 与 `_034` 同句 | 纠察队要动手抓你了。 | The patrol moves to take you by force. |
| `gwp_policepatrolbehavior_034` | 同上 | 私了没谈成时也出这句，「你拒绝伏法」不准 | 纠察队要动手抓你了。 | The patrol moves to take you by force. |

代码层面的附带问题（不在本稿改）：玩家选择拒绝时，`OnRefuseConsequence` 先在宣战时显示 `_034`，紧接着又显示 `_018`，同一句话连出两遍。已记入清理待办。

### 被纠察队打败

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_039` | 你被纠察队击败，正被押往{VAR_1}。 / The patrol has defeated you. You are being taken to {VAR_1}. | 无 | 保留 | 保留 |
| `gwp_common_garrison` | 驻地点 / garrison | `_039` 找不到出发地时的替补词，「押往驻地点」不通 | 纠察队的驻地 | the patrol's quarters |
| `gwp_policepatrolbehavior_042` | 纠察队收取了罚金 {VAR_1} 第纳尔。 / The patrol collects a fine of {VAR_1} denars. | 无 | 保留 | 保留 |
| `gwp_policepatrolbehavior_041` | 你被押送到{VAR_1}，缴纳罚金 {VAR_3} 第纳尔后获释。 / You are brought to {VAR_1} and released after paying {VAR_3} denars. | 无大问题 | 你被押到{VAR_1}，交了{VAR_3}第纳尔罚金后放了出来。 | You are brought to {VAR_1} and let go after paying {VAR_3} denars. |

### 撞上正在返程的纠察队

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_patrol_returning_dialogue` | 本轮案卷已结，纠察队正奉命返回驻地。此刻不再受理交涉。 / This patrol has rendered its account and is returning to quarters. No further hearing will be held. | 说法不准：玩家降到 −11 改由灰袍接手时，纠察队也在返程，「案卷已结」不对；公文腔 | 我们奉命回驻地，没工夫跟你多说。 | We have orders to return to quarters. We've no time for you. |

### 声望变化的地图提示

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_031` | 灰袍已下令缉拿你。 / The Grey Wardens have ordered your arrest. | 无（降到 −11 时出现） | 保留 | 保留 |
| `gwp_policepatrolbehavior_027` | 灰袍已撤销对你的通缉。 / The Grey Wardens no longer seek you. | 无（通缉中声望回正时出现） | 保留 | 保留 |

## 二、灰袍领主带队拦下玩家（声望 −11 及以下，已通缉）

### 领主开口

[拦下的是灰袍领主本人。开场由 `dialogue_010` + `dialogue_011` + `dialogue_008` 或 `_009` 拼成，代码直接相连，中间不加空格，所以英文 `_010` 结尾要留一个空格。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_dialogue_010` | 站住！灰袍守卫正在执行逮捕。你当前负声望 {VAR_1}， / Stand! The Grey Wardens come under warrant. Your present standing is {VAR_1}, | 念负声望数值；公文腔 | 站住。灰袍一直在找你。 | Halt. The Wardens have been looking for you.（结尾留一个空格） |
| `gwp_policeenforcementbehavior_dialogue_011` | 本案正式罚金 {VAR_1} 金。{VAR_2} / and the lawful fine in this case is {VAR_1} denars. {VAR_2} | 公文腔 | 你欠下的这些，罚金是{VAR_1}第纳尔。{VAR_2} | For what you have done, the fine is {VAR_1} denars. {VAR_2} |
| `gwp_policeenforcementbehavior_dialogue_008` | 你当前携带 {VAR_1} 金，可直接缴清。 / You carry {VAR_1} denars, enough to pay in full. | 机制说明 | 你身上的钱够数。 | You have enough on you. |
| `gwp_policeenforcementbehavior_dialogue_009` | 你当前携带 {VAR_1} 金，可在谈判界面继续出价，或改选认罪认罚。 / You carry {VAR_1} denars. You may make another offer at the table, or confess and accept judgment. | 界面用语 | 钱不够，就拿货物来补。要不然，认下罪，替灰袍办一桩案子来抵。 | If you're short of coin, make it up in goods. Or admit your guilt and work it off on a case for the Wardens. |

### 拦下的是玩家队里有案的同伴

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_enforcement_companion_charge` | 站住！与你同行的 {VAR_1} 有灰袍未结案件。本案罚金为 {VAR_2} 金。{VAR_3} / Stand! {VAR_1} travels with you under a Grey Warden warrant. The fine for {VAR_1}'s case is {VAR_2} denars. {VAR_3} | 公文腔；同伴不是被通缉，是有案子 | 站住。你队伍里的{VAR_1}，在灰袍那儿还有一桩案子没了。罚金{VAR_2}第纳尔。{VAR_3} | Halt. {VAR_1}, who rides with you, still has a case open with the Wardens. The fine is {VAR_2} denars. {VAR_3} |
| `gwp_enforcement_companion_insufficient` | 你当前携带 {VAR_1} 金。凑齐罚金后可回来一次缴清。 / You carry {VAR_1} denars. Return when you can pay the fine in full. | 机制说明 | 你身上的钱不够。凑齐了再来，一次交清。 | You don't have enough on you. Come back when you can pay it all at once. |

### 玩家回应

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_dialogue_012` | 缴纳正式罚金（{VAR_1} 金，清除通缉） / Pay the lawful fine ({VAR_1} denars; clear the warrant) | 公文腔、机制括注 | 罚金我交。（{VAR_1}第纳尔） | I'll pay the fine. ({VAR_1} denars) |
| `gwp_enforcement_companion_pay` | 替这名同伴缴纳罚金（{VAR_1} 金） / Pay this companion's fine ({VAR_1} denars) | 公文腔 | 这笔罚金我替同伴交。（{VAR_1}第纳尔） | I'll pay my companion's fine. ({VAR_1} denars) |
| `gwp_enforcement_companion_leave` | 我现在无法替这名同伴结案。 / I cannot settle this companion's case now. | 公文腔 | 这笔钱我现在拿不出来。 | I can't pay that right now. |
| `gwp_policeenforcementbehavior_dialogue_004` | 我认罪认罚，请给我赎罪任务。 / I confess the offence and accept judgment. Grant me a charge of atonement. | 「赎罪任务」是游戏界面词 | 我认罪。让我替灰袍办一桩案子，将功折罪。 | I admit it. Let me take a case for the Wardens and make amends. |
| `gwp_policeenforcementbehavior_dialogue_006` | 拒绝执法，开战。 / I refuse the order. Let arms decide. | 不是人话 | 这罚金我不交。要动手就来吧。 | I won't pay. If you mean to fight, come on. |

### 交罚金

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_dialogue_001` | 按灰袍法令，先把正式罚金缴清。 / By Grey Warden ordinance, the lawful fine must first be paid. | 没有「灰袍法令」 | 那就当面点清。 | Then count it out here. |
| `gwp_policeenforcementbehavior_dialogue_018` | 缴纳正式罚金 / Pay the lawful fine | 交易界面条目名 | 交给灰袍的罚金 | Fine paid to the Wardens |
| `gwp_policeenforcementbehavior_dialogue_002` | 罚金确认。本轮案件结案，你可以离开了。 / The fine is acknowledged. This case is closed; you may depart. | 开发口吻 | 数目没错。你的案子了了，走吧。别再让我们找上门。 | The sum is right. Your case is closed. Go, and don't give us cause to find you again. |
| `gwp_policeenforcementbehavior_dialogue_019` | 罚金 {VAR_1} 第纳尔已缴清，通缉撤销。 / The fine of {VAR_1} denars is paid. You are no longer wanted. | 无（地图提示，这时确实撤销通缉） | 保留 | 保留 |
| `gwp_policeenforcementbehavior_dialogue_003` | 你的出价低于正式罚金。你可以继续出价，或选择其他处理方式。 / Your offer falls below the lawful fine. You may raise it or choose another response. | 界面用语 | 不够数。补齐，或者另想办法。 | That's short. Make up the rest, or find another way. |
| `gwp_enforcement_companion_paid` | {VAR_1}已缴清罚金 {VAR_2} 第纳尔，案件了结。 / {VAR_1} has paid the fine of {VAR_2} denars. The case is closed. | 说法不准：钱是玩家付的 | 你替{VAR_1}交了{VAR_2}第纳尔罚金，这桩案子了结了。 | You paid {VAR_2} denars on {VAR_1}'s behalf. The case is closed. |

### 认罪领赎罪任务

[玩家选「认罪」。能派到目标时，领主说一段话（`dialogue_014` + `_015` + `_016` 拼成），随后地图提示 `_017`；派不到时领主说 `_005`，回到选项。任务日志另有期限、目标人数和可恢复的声望（`gwp_policeenforcementbehavior_atonementquest_007`、`_014`），数字留在日志里，领主不必再念。`VAR_1` 是目标的部队名。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_dialogue_014` | 赎罪任务已下达：追捕 {VAR_1}（接案规模 {VAR_2} 人）。 / A charge of atonement has been issued: pursue {VAR_1} (strength when assigned: {VAR_2}). | 是系统提示，不是领主的话 | 好。去找{VAR_1}，他们大约有{VAR_2}人。 | Very well. Go after {VAR_1}. They number about {VAR_2}. |
| `gwp_policeenforcementbehavior_dialogue_015` | 完成可恢复最多 {VAR_1} 点声望（最高恢复到 0）； / Fulfilment may restore up to {VAR_1} standing, but never above 0; | 机制说明 | 办成了，你欠灰袍的账能抵掉一些； | Do it, and part of your debt is forgiven; |
| `gwp_policeenforcementbehavior_dialogue_016` | 失败将追加 5 点负声望。 / failure will impose a further loss of 5 standing. | 机制说明 | 办砸了，罪加一等。 | fail, and it will go worse for you. |
| `gwp_policeenforcementbehavior_dialogue_017` | 灰袍命你在 {VAR_2} 天内击败{VAR_1}以赎罪。 / The Wardens charge you to defeat {VAR_1} within {VAR_2} days in atonement. | 说法不准：谈判、收款、投案也能完成，不只击败 | 赎罪：在{VAR_2}天内处置{VAR_1}，再回灰袍领主处复命。 | Atonement: deal with {VAR_1} within {VAR_2} days, then report to a Warden lord. |
| `gwp_policeenforcementbehavior_dialogue_005` | 当前无法分配赎罪任务。你可以继续缴纳罚金，或拒绝执法。 / No charge of atonement can presently be assigned. You may pay the fine or refuse the order. | 系统口吻 | 眼下没有能交给你的案子。要么交罚金，要么就动手。 | There's no case I can give you right now. Pay the fine, or we settle this another way. |

拼接后的中文：「好。去找{VAR_1}，他们大约有{VAR_2}人。办成了，你欠灰袍的账能抵掉一些；办砸了，罪加一等。」英文 `_014` 结尾、`_015` 结尾各留一个空格。

### 拒捕

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_dialogue_007` | 拒捕记录在案。灰袍守卫，执行抓捕！ / Resistance is entered upon the roll. Grey Wardens—make the arrest! | 公文腔 | 既然如此——灰袍的人，拿下！ | Then so be it. Wardens, take them! |
| `gwp_policeenforcementbehavior_dialogue_020` | 你拒绝伏法，灰袍将强行缉拿。 / You refuse to submit. The Wardens will take you by force. | 地图提示，稍改 | 灰袍要动手抓你了。 | The Wardens move to take you by force. |

### 被正式灰袍打败

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_008` | 你被押送到{VAR_1}，缴纳罚金 {VAR_3} 第纳尔后获释。 / You are brought to {VAR_1} and released after paying {VAR_3} denars. | 没交够时仍保持通缉，这句没说；要说清需按是否足额分两句，属代码改动 | 你被押到{VAR_1}，交了{VAR_3}第纳尔罚金后放了出来。 | You are brought to {VAR_1} and let go after paying {VAR_3} denars. |

待用户定：是否另加一句「罚金没交够，灰袍还会再来找你。」/ “You could not pay it all. The Wardens will come for you again.”，在未足额时代替上面这句。这需要新键和代码分支，批准后另做。

## 本稿统计

共审 53 条：纠察队 28 条（改写 22、保留 6），灰袍领主 25 条（改写 24、保留 1）。改写的 46 条里，说法不准 9 条：两条「按灰袍法令」（`_003`、`dialogue_001`），以及条件或身份说错的 7 条（`_015` 英文、`_022`、`_034`、`gwp_patrol_returning_dialogue`、`gwp_enforcement_companion_charge`、`gwp_enforcement_companion_paid`、`dialogue_017`）；其余 37 条是机制说明、公文腔和机翻英文。待用户定 2 处：私了按钮要不要保留「账还记着」，押送没交够时要不要另加一句。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，不改代码。两处「待用户定」和拒绝时提示连出两遍的问题，如果要做，另走代码改动流程。
