# 野外执法：开场、劝说论据与罪犯的打算

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[野外执法](../state/field-arrest.md)，接单条件见[玩家声望](../state/player-justice.md#悬赏接单)。分组见[清单](text-review-inventory-20261104.md)第 1 批。

场景说明只给审阅时看，角色不会念出来。表里每条写键、现中文 / 现英文、问题和建议改稿；没问题的写「保留」。玩家的说辞是四选一（统御 / 魅力 / 诡计 / 交易），不是连着说。

## 核实到的情境（作者用，不进台词）

- 玩家在野外和自己承办的罪犯单独说话时进入这套对话（`GwpFieldArrestBehavior.FieldArrestAvailableCondition`）。普通悬赏要求**已入会、声望 ≥ 20、旗帜槽装着执勤旗**才能接（`HasCommissionAuthority` 要求 `_recruitmentAccepted`）；赎罪任务不要求入会，这时玩家声望为负。
- 罪犯先开口。玩家声望 < 0 时用「你替灰袍办事？」那组；声望 ≥ 40（`RespectedStanding`）或对方从前被灰袍抓过时用本稿的开场句；其余情况（声望 0–39 且没被抓过）用罪犯口吻池（第 2 批）。所以 `gwp_fa_open_plain` 走不到。
- 「被灰袍抓过」在代码里是 `TotalArrestCount > 0`：被押走、和平交罚金、塞私钱、约斗（他赢了也算）都会加一次（[罪案登记](../state/crime-intake.md)，用户裁定 2026-10-17）。所以台词不能断定他「被抓走过」，只能说他「跟灰袍了结过一回」。
- 第一层：罪犯提三条顾虑（`BuildReservations`）。第一条：玩家声望为负用「你的名声也没比我好多少」，否则用「你不是我的主君」。第二条：荣誉 ≤ 0 且死伤 < 20 用「那是我手下干的」；否则被抓过用「你们抓过我一回」；否则商队案用「打着仗呢」；否则用「我要是干脆不交呢」。第三条：多次犯案用「为什么把几次事情合在一起」，否则用「这个数哪儿来的」。
- 罚金 = 犯案次数 × 基数 + 他累积的负声望 × 100（`GwpFieldArrestPricing.AssessFine`），确实不只算这一次的事。
- 第二层：罪犯说出自己的打算（`GwpOffenderDesires.Line`），玩家劝他照数全缴；劝不动时玩家可以「照他说的办」（`PlayerAcceptLine`）。实际会抽到的只有 8 种：照数全缴、讨价还价（七成）、骗一道（五成）、人跟你走、只认自己的事（四成，当场杀自己 5 个兵）、塞私钱、单挑、请宽限（三天）。
- 单挑走切磋管线，是比武，不是死斗（[切磋](../state/sparring.md)）。
- 谈崩后 24 小时内再谈，罪犯只回一句按性子的回绝；回绝不得提时限（用户裁定 2026-09-26）。
- 游戏里没有纸面的「通缉令」，玩家拿的是灰袍交办的案子（见[核实稿](text-review-term-check-20261104.md)）。英文里的 “warrant” 同理改掉。

## 通用问题

1. **「你不是灰袍，是他们雇来的人」**：普通悬赏时玩家已经入会、扛着执勤旗，罪犯说他「不是灰袍」与事实不符；英文还写死了 “a man”。改成拿「替灰袍跑腿领赏」挖苦，办案费是真的。
2. **「通缉令」/ “warrant”**：六处，全部改成灰袍、罚金或案子。
3. **单挑说成拼命**：`gwp_terms_demandduel_lead` 说「拿自己的命去赌」，单挑是比武，不会死人。
4. **英文和中文不是一句话**：约 20 条英文是另一版旧稿，按中文重写英文（个别中文不如英文的，反过来改中文）。
5. **硬译腔**：「手下两个字就是这个意思」「这一半在你手上」「损失更可控」之类，改成口语。

## 一、罪犯开口

[玩家拦下罪犯，罪犯先说话。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_open_under_charge` | 你替灰袍办事？说吧，找我什么事。 / You work for the Wardens? Tell me what you want. | 无（玩家声望为负，多半是赎罪任务） | 保留 | 保留 |
| `gwp_fa_open_respected_known` | 又见面了。我知道你替灰袍办过不少事。这回为了什么？ / You again. Or your order, at least. What is it this time? | 说法不准：条件是他从前被灰袍抓过，不是见过玩家；英文另一句 | 又是灰袍的人。我听说你替她们办过不少事。这回又为了什么？ | The Wardens' people again. I hear you've done a good deal of work for them. What is it this time? |
| `gwp_fa_open_respected` | 我听说过你。既然受灰袍委托，就把来意说清楚。 / I know the grey. Speak, and speak plainly. | 英文另一句 | 保留 | I've heard of you. If the Wardens sent you, say plainly what you want. |
| `gwp_fa_open_known` | 灰袍又来了。上次的事，我可还记得。 / The Wardens again. I remember the last time. | 无 | 保留 | 保留 |
| `gwp_fa_open_plain` | 灰袍。找我什么事？ | 不可达（见上） | — | — |

## 二、谈崩以后再找他

[同一个罪犯，上次谈崩不久，玩家又上前搭话。按他最突出的性子取一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_rebuff_upright` | 我的答复已经给过你了。一句话，我不说两遍。 / You have my answer. I do not give a man two. | 英文写死 “a man” | 保留 | You have my answer. I don't give it twice. |
| `gwp_fa_rebuff_cold` | 上回谈完到现在，什么都没变。 / Nothing has changed since we last spoke. | 无 | 保留 | 保留 |
| `gwp_fa_rebuff_fierce` | 这事已经了断过一回。再逼我，就不是动嘴了。 | 无 | 保留 | 保留 |
| `gwp_fa_rebuff_soft` | 行行好，这事已经翻过一遍了，让它歇歇吧。 | 无 | 保留 | 保留 |
| `gwp_fa_rebuff_tight` | 你已经从我这儿讨过话了。话也是要本钱的，你花完了。 | 无（吝啬的人的刻薄话） | 保留 | 保留 |
| `gwp_fa_rebuff_plain` | 这事我们谈过了。我的答复不变。 | 无 | 保留 | 保留 |

## 三、第一层：他认不认这次执法

### 每条顾虑共用的回应

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_rebuff` | 这不算回答。 / That is no answer. | 无 | 保留 | 保留 |
| `gwp_fa_task_final` | 你没词了，灰袍。 / You have run out of words, Warden. | 赎罪时玩家没入会，叫「灰袍」不准 | 你没话可说了吧。 | You've run out of things to say. |
| `gwp_fa_task_later` | 这份案子，我不想再听你争辩了。 / I will hear no more arguments about this warrant. | 「这份案子」不通；英文 “warrant” | 这件事，我不想再跟你争了。 | I'll hear no more argument about this. |

### 「你不是我的主君」（玩家声望 ≥ 0，普通悬赏）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_authority` | 你不是我的主君，也不是灰袍。你是他们雇来的人。 / You are not my liege and you are not a Warden. You are a man they hired. | 说法不准：玩家已入会、扛着执勤旗；英文写死 “a man” | 你又不是我的主君。替灰袍跑腿领赏钱，就以为管得了我？ | You're not my liege. Running errands for the Wardens for pay doesn't give you any say over me. |
| `gwp_fa_arg_authority_lead` | 灰袍派我来处理这件事。我说的话，就是她们的要求。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_authority_charm` | 我受谁雇用，不会改变受害者的损失。我们先把这件事说清楚。 / Hired or sworn, the dead are still dead and someone had to come. | 英文另一句 | 保留 | Whoever pays me, the people you harmed still lost what they lost. Let's settle this first. |
| `gwp_fa_arg_authority_rogue` | 灰袍把案子交给我，就不会因为你不认我而撤回去。 / They hired me because they were tired of asking politely. | 英文另一句 | 保留 | The Wardens gave this case to me. They won't take it back just because you don't care for me. |
| `gwp_fa_arg_authority_trade` | 你认不认我，灰袍要求的赔偿都不会少。趁现在把事情解决，对你也省事。 | 收的是罚金，进灰袍公库，不是赔偿 | 你认不认我，灰袍要的罚金都不会少。趁现在把事情了了，对你也省事。 | 保留 |
| `gwp_rebuff_authority_lead` | 我可没向这份通缉令宣过誓…… | 见[核实稿](text-review-term-check-20261104.md#gwp_rebuff_authority_lead) | 见核实稿 | 见核实稿 |
| `gwp_rebuff_authority_charm` | 我听见你替他们说话了，但这还没说明我为什么该接受你的处置。 | 无 | 保留 | 保留 |
| `gwp_rebuff_authority_rogue` | 威胁谁都会说。我还没答应听你的。 | 无 | 保留 | 保留 |
| `gwp_rebuff_authority_trade` | 你一直在算罚金，可我还没认下这份指控。 | 无 | 保留 | 保留 |

### 「你的名声也没比我好多少」（玩家声望 < 0）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_under_charge` | 你的名声也没比我好多少，倒来替灰袍教训我了？ | 无 | 保留 | 保留 |
| `gwp_fa_arg_under_lead` | 我做过什么，不妨另说。眼前这些事，你得向灰袍交代。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_under_charm` | 那就别再添一个坏结果了。今天把事情解决，让大家都能回去。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_under_rogue` | 正因为如此，我更不会空手回去。你最好想清楚。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_under_trade` | 议论我值不了一枚第纳尔。你该付的钱不会因此减少。 | 无 | 保留 | 保留 |

### 「那是我手下干的」（他荣誉 ≤ 0，死伤不多）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_denial` | 那是我手下干的。我没下这个令，凭什么让我替他们的胃口付钱。 | 「替他们的胃口付钱」生硬 | 那是我手下干的。我没下这个令，凭什么要我替他们掏钱？ | 保留 |
| `gwp_fa_arg_denial_lead` | 那是你的兵。手下两个字就是这个意思。 / They are your men. That is what the word means. | 硬译 | 他们是你的兵，这事就得你担着。 | They're your soldiers. That makes it yours to answer for. |
| `gwp_fa_arg_denial_charm` | 村里没一个人分得清是你的手还是他们的手。 / Nobody in that village could tell your hand from theirs. | 说法不准：商队案也会抽到这条，不一定是村子 | 挨了刀的人，可分不清哪一刀是你砍的，哪一刀是他们砍的。 | The people on the other end couldn't tell your blade from theirs. |
| `gwp_fa_arg_denial_rogue` | 他们打着你的旗号行事。推给部下，灰袍也还是会找你。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_denial_trade` | 他们是你的部下。先把损失赔了，你回去怎么向他们追究，是你的事。 | 无 | 保留 | 保留 |

### 「你们抓过我一回」（他从前被灰袍办过）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_record` | 你们抓过我一回了。以后就这么没完了是吗？ / Your order has had me once already. Is this how it goes now - forever? | 说法不准：他可能只是交过罚金，没被抓走 | 我已经跟灰袍了结过一回了。以后就这么没完没了？ | I've already settled with the Wardens once. Is this how it goes now, forever? |
| `gwp_fa_arg_record_lead` | 你不再给他们理由，就完了。这一半在你手上。 | 硬译 | 你不再犯事，自然就完了。这得看你自己。 | It ends when you stop giving them cause. That part is up to you. |
| `gwp_fa_arg_record_charm` | 抓了一回还放你走的团，不多。 / They took you once and let you ride. Few orders would have. | 「团」生硬；同上，不一定被抓过 | 上回灰袍没为难你，换了别人可未必。 | Last time the Wardens let you go on your way. Not everyone would have. |
| `gwp_fa_arg_record_rogue` | 被捕是什么结果，你已经经历过。还要再来一回吗？ | 同上，不一定被捕过 | 上回是什么结果，你已经经历过。还要再来一回吗？ | You know how it went last time. Do you want to go through that again? |
| `gwp_fa_arg_record_trade` | 先结清眼下这笔。以后再犯的事，另有新的账。 | 无 | 保留 | 保留 |

### 「打着仗呢」（商队案）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_war` | 打着仗呢。拿敌方的东西不算犯罪，那叫干活。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_war_lead` | 你的仗轮不到赶车的头上。灰袍的律轮得到。 / Your war does not reach the drivers. The Wardens' law does. | 「灰袍的律」含糊，游戏里没有成文律法（核实稿：法令） | 你们打仗，跟赶车的人有什么相干？灰袍管的就是这种事。 | Your war has nothing to do with the drivers. This is exactly what the Wardens deal with. |
| `gwp_fa_arg_war_charm` | 车夫和护卫。没有一个是替你守过阵线的。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_war_rogue` | 你愿意叫它打仗就叫吧。灰袍这边写的是一支商队和几具尸首。 | 不一定有死人 | 你爱叫它打仗就叫吧。灰袍的案子上写的，是一支被你袭击的商队。 | Call it war if you like. The Wardens' case says you attacked a caravan. |
| `gwp_fa_arg_war_trade` | 仗会打完。账不会，除非有人付。 | 无 | 保留 | 保留 |

### 「我要是干脆不交呢」（其余情况）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_consequence` | 我要是干脆不交呢？你还能把我怎么样？ | 无 | 保留 | 保留 |
| `gwp_fa_arg_consequence_lead` | 我离开了，灰袍也不会就此罢手。今天总得有个交代。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_consequence_charm` | 那下一个穿灰的来就不是找你说话了，这对谁都不好。 | 无（玩家不办，案子回到灰袍手里，灰袍领主会带兵追捕） | 保留 | 保留 |
| `gwp_fa_arg_consequence_rogue` | 你可以赌我拿不住你。下回灰袍来多少人，你可说了不算。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_consequence_trade` | 这笔欠款会一直留着，不会因为你拖延就消失。 | 无 | 保留 | 保留 |

### 「为什么把几次事情合在一起」（多次犯案）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_tally` | 为什么把几次事情合在一起，收我这么一大笔钱？ / You are charging me for things I did months apart, as if they were one crime. | 英文「隔了几个月」是编的，几次犯案可能只隔几天 | 保留 | Why lump several things together and charge me this much for them? |
| `gwp_fa_arg_tally_lead` | 事情过去了，不等于责任也没了。灰袍要你一起承担。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_tally_charm` | 每一件里都有人。对你是隔了几个月，对他们不是。 | 同上，「几个月」是编的 | 每一件里都有人受害。你记不清了，他们可记得。 | Every one of them had people in it. You may have lost count; they haven't. |
| `gwp_fa_arg_tally_rogue` | 想一件一件算，早点被逮住就是了。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_tally_trade` | 今天一起付清，就不用为这些旧事反复停下队伍。 | 无 | 保留 | 保留 |

### 「这个数哪儿来的」（只犯过一次）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_price` | 这个数——哪儿来的？你说得像是天上掉下来的。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_price_lead` | 这是灰袍要求的罚金。我受托来执行，不是来随口开价的。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_price_charm` | 受害者已经承担了损失。别再为这件事添一场厮杀。 | 无 | 保留 | 保留 |
| `gwp_fa_arg_price_rogue` | 这是便宜那条路。贵的那条正看着你。 | 格言腔 | 交钱是便宜的那条路。另一条路，就站在你面前。 | Paying is the cheap way out. The other way is standing right in front of you. |
| `gwp_fa_arg_price_trade` | 损毁的财物、受害的人，都得有个交代。这个数不是只算你眼前这一次。 | 无（罚金确实计入他以往的负声望） | 保留 | 保留 |

## 四、第二层：他说出打算

[第一层谈下来后，罪犯说自己打算怎么了结。照数全缴的人若钱和货不够，改说「钱不够」那句（第 3 批）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_desire_payfull` | 也罢。全额，到此为止。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_haggle` | 我最多出七成。你愿意收，我们就此了结。 / I will give you seven tenths of the fine. That is my offer. | 无 | 保留 | 保留 |
| `gwp_fa_desire_cunning` | 一半。辎重里就这么多，你能拿到的也就这么多。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_surrender` | 那些银子是我部下的军饷。你带我走吧——我跟你，让他们回家。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_deeds` | 我只交四成。那些惹事的部下，我会当场处死，给你一个交代。 | 无（确实杀 5 个兵） | 保留 | 保留 |
| `gwp_fa_desire_bribe` | 我给你价值 {GWP_BRIBE_AMOUNT} 第纳尔的财物，归你自己。回去怎么说，你来想办法。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_duel` | 你我单挑。你赢了，我缴罚金；我赢了，你就不收这笔钱。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_grace` | 给我三天筹钱。到时候你再来，这笔罚金我会给你个交代。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_cautious` `gwp_fa_desire_merciful` `gwp_fa_desire_generous` `gwp_fa_desire_rash` | — | 不可达：`Roll` 从不抽这四种打算 | — | — |

## 五、第二层：玩家劝他照数全缴

[每种打算一组：玩家四选一劝说，没说动时罪犯回一句。中文回绝按技能分四句；英文玩家只看到 `_lead` 那句（代码问题 1）。]

### 讨价还价（七成）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_haggledown_lead` | 这是核定的罚金，我没有给你打七折的命令。 | 无 | 保留 | 保留 |
| `gwp_terms_haggledown_charm` | 别再为余款起争执了，照数交齐吧。 | 无 | 保留 | 保留 |
| `gwp_terms_haggledown_rogue` | 你以为我非卖给你不可？我还可以照令抓人。 / … I can still enforce the warrant. | 英文 “warrant”；「照令」含糊 | 你以为我非卖给你不可？我也可以直接拿人。 | You bargain as if I had to sell. I can still take you by force. |
| `gwp_terms_haggledown_trade` | 七成交了还剩三成。把余款也结清。 | 无 | 保留 | 保留 |
| `gwp_rebuff_haggledown_lead` | 你说没有打折的命令，我也没答应任你开价。 / You call it an exact account; I call it too much. My offer is still seven tenths. | 英文不对应 | 保留 | You say you have no orders to discount it. I never agreed to whatever price you name. |
| `gwp_rebuff_haggledown_charm` | 想少一次争执，你同样可以接受我给的七成。 | 无 | 保留 | — |
| `gwp_rebuff_haggledown_rogue` | 我知道你能动手。不必每次还价都拿这句话压我。 | 无 | 保留 | — |
| `gwp_rebuff_haggledown_trade` | 三成有多少，我算得明白。我争的就是这三成。 | 无 | 保留 | — |

### 骗一道（五成）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_cunninghalf_lead` | 那就把别的行李也打开。我不会凭一句话就认下只剩一半。 | 无 | 保留 | 保留 |
| `gwp_terms_cunninghalf_charm` | 别再拿钱袋当借口。把实情说清，事情也好收场。 / Show me enough trust to stop bargaining with a story about your purse. | 英文不对应 | 保留 | Stop hiding behind your purse. Tell me the truth and we can end this. |
| `gwp_terms_cunninghalf_rogue` | 刚好只剩一半？这套说辞我听过。把另一半找出来。 | 无 | 保留 | 保留 |
| `gwp_terms_cunninghalf_trade` | 付一半就还欠一半。你怎么解释钱袋，都改不了这个数。 | 无 | 保留 | 保留 |
| `gwp_rebuff_cunninghalf_lead` | 要翻行李就直说，但我不会因此改口答应全额。 / You suspect another purse… Half is my offer. | 英文不对应 | 保留 | If you want to search my baggage, say so. I still won't agree to the full sum. |
| `gwp_rebuff_cunninghalf_charm` | 信任不是你说一句，我就得掏出另一半。我的条件没变。 | 无 | 保留 | — |
| `gwp_rebuff_cunninghalf_rogue` | 听过这种说辞，就能断定我还藏着钱？你得拿出点别的。 | 无 | 保留 | — |
| `gwp_rebuff_cunninghalf_trade` | 我知道半款还剩多少。我是在给你条件，不是在请你算数。 | 无 | 保留 | — |

### 人跟你走

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_surrenderself_lead` | 你的部下也需要统帅。交钱了结，你仍能带着他们。 | 无 | 保留 | 保留 |
| `gwp_terms_surrenderself_charm` | 不必把自己人丢下。交清罚金，你就能留在他们身边。 | 无 | 保留 | 保留 |
| `gwp_terms_surrenderself_rogue` | 你放心把指挥权交给别人？钱可比你的位子容易找回来。 | 无 | 保留 | 保留 |
| `gwp_terms_surrenderself_trade` | 被关押也会拖累家族和队伍。用现钱了结，损失更可控。 | 「损失更可控」公文腔 | 被关起来，家里和队伍都要受拖累。拿钱了结，划算得多。 | Captivity costs your family and your men as well. Paying is the cheaper way. |
| `gwp_rebuff_surrenderself_lead` | 正因为我是统帅，才该由我跟你走。钱留给部下。 / My answer is still to go with you. That money stays with my men. | 英文不对应 | 保留 | Because I command them, I'm the one who goes with you. The money stays with my men. |
| `gwp_rebuff_surrenderself_charm` | 他们不会因为我被押走就没了活路，没了军饷才会。 | 无 | 保留 | — |
| `gwp_rebuff_surrenderself_rogue` | 我离开后谁来指挥，自有安排。你不必替我操心。 | 无 | 保留 | — |
| `gwp_rebuff_surrenderself_trade` | 你算的是我的损失，我想的是他们明天吃什么。 | 无 | 保留 | — |

### 只认自己的事（四成，杀自己的兵）

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_deedsonly_lead` | 杀自己的兵也免不了你的责任。把罚金交齐。 | 无 | 保留 | 保留 |
| `gwp_terms_deedsonly_charm` | 死的人已经够多了。留下他们的命，用钱了结。 | 无 | 保留 | 保留 |
| `gwp_terms_deedsonly_rogue` | 砍掉几个知情人，账就会变少？别拿这个糊弄我。 | 无 | 保留 | 保留 |
| `gwp_terms_deedsonly_trade` | 四成填不平这笔账。你部下的命也抵不了剩下的钱。 | 无 | 保留 | 保留 |
| `gwp_rebuff_deedsonly_lead` | 我的兵由我处置。你来收你的钱，别替我发号施令。 / Their punishment is mine to decide. I offered you two fifths, no more. | 无（英文意思接近） | 保留 | 保留 |
| `gwp_rebuff_deedsonly_charm` | 你想替他们求情？那也不会让我多交余下的罚金。 | 英文见[核实稿](text-review-term-check-20261104.md#gwp_rebuff_deedsonly_charm中文不改英文要动代码单列待定) | 保留 | — |
| `gwp_rebuff_deedsonly_rogue` | 知情人？他们是犯错的部下。别给这件事再添一份指控。 | 无 | 保留 | — |
| `gwp_rebuff_deedsonly_trade` | 我知道钱不够你报的数。我只认我答应的四成。 | 无 | 保留 | — |

### 塞私钱

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_bribethewarden_lead` | 灰袍让我来收罚金，不是收你的好处。该交多少就交多少。 / I carry a warrant, not a begging bowl. Pay what it names. | 英文 “warrant” | 保留 | The Wardens sent me for the fine, not for your gifts. Pay what is owed. |
| `gwp_terms_bribethewarden_charm` | 别把事情弄成这样。光明正大地交清，双方都能走。 | 无 | 保留 | 保留 |
| `gwp_terms_bribethewarden_rogue` | 拿这点私钱换我背锅？贿金收回去，罚金拿出来。 | 无 | 保留 | 保留 |
| `gwp_terms_bribethewarden_trade` | 私下给我的钱抵不了公库的账。该缴的总数并没变。 | 无 | 保留 | 保留 |
| `gwp_rebuff_bribethewarden_lead` | 别急着拿通缉令压人。我说的是给你个人的好处。 | 没有「通缉令」 | 别急着板起脸。我说的是给你个人的好处。 | Don't put on such a stern face. I'm offering something for you alone, not the fine. |
| `gwp_rebuff_bribethewarden_charm` | 你我都能好好离开，何必非要把钱送进公库？ | 无 | 保留 | — |
| `gwp_rebuff_bribethewarden_rogue` | 嫌这点钱不值得冒险？那也是私下谈价，不是让我照数缴罚。 | 无 | 保留 | — |
| `gwp_rebuff_bribethewarden_trade` | 公库的账是公库的账。我问的是你愿不愿意收这笔钱。 | 无 | 保留 | — |

### 单挑

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_demandduel_lead` | 这份通缉令不是比武请帖。别为它拿自己的命去赌。 / This warrant is not a contest. Meet the charge without risking your commander's life. | 没有「通缉令」；单挑是比武，不会丢命 | 灰袍要的是罚金，不是一场比武。何必为这个跟我动刀？ | The Wardens want the fine, not a contest. Why cross blades with me over it? |
| `gwp_terms_demandduel_charm` | 你不必向我证明勇气。双方都少受一次伤，不好吗？ | 无 | 保留 | 保留 |
| `gwp_terms_demandduel_rogue` | 拿单挑免掉别人的血债？这个赌法我不接。 | 无 | 保留 | 保留 |
| `gwp_terms_demandduel_trade` | 赢了也可能受伤耽误行程。罚金有定数，刀剑可没有。 | 无 | 保留 | 保留 |
| `gwp_rebuff_demandduel_lead` | 通缉令在你手上，剑也在你手上。为什么不敢亲自来了结？ | 没有「通缉令」 | 你有灰袍撑腰，手里也有剑。为什么不敢亲自来了结？ | You have the Wardens behind you and a sword in your hand. Why not settle it yourself? |
| `gwp_rebuff_demandduel_charm` | 你说不必证明勇气，我却不想靠掏钱结束这件事。 | 无 | 保留 | — |
| `gwp_rebuff_demandduel_rogue` | 你不肯接这个赌，我也没答应换个办法。 | 无 | 保留 | — |
| `gwp_rebuff_demandduel_trade` | 你把伤口和行程都算进去了。我看重的可不只是钱。 | 无 | 保留 | — |

### 请宽限三天

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_requestgrace_lead` | 灰袍等的是罚金，不是再拖几天。先把你眼下能交的拿出来。 | 无 | 保留 | 保留 |
| `gwp_terms_requestgrace_charm` | 今天能交多少就先解决多少，别让这件事一直压着你和部下。 | 无 | 保留 | 保留 |
| `gwp_terms_requestgrace_rogue` | 三天，够你走出多远？我更想先看看你现在的钱袋。 | 无 | 保留 | 保留 |
| `gwp_terms_requestgrace_trade` | 你我再跑一趟都要花钱。今天把能付的付了，少耽误些工夫。 | 无 | 保留 | 保留 |
| `gwp_rebuff_requestgrace_lead` | 我没说不交。我说的是筹钱需要时间。 / I asked for time to raise it. Repeating the amount will not fill my purse today. | 英文不对应 | 保留 | I didn't say I wouldn't pay. I said I need time to raise it. |
| `gwp_rebuff_requestgrace_charm` | 我也想今天就了结，可你总得容我把钱凑来。 | 无 | 保留 | — |
| `gwp_rebuff_requestgrace_rogue` | 你不信我，我也没办法。但催得再紧，钱也不会自己出现。 | 无 | 保留 | — |
| `gwp_rebuff_requestgrace_trade` | 再跑一趟是麻烦，可现在让我凑齐，同样办不到。 | 无 | 保留 | — |

### 第二轮：「我放弃条件又能换来什么」

[第一轮劝动以后，罪犯再问一次，玩家再选一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_terms_final_objection` | 就算照数交清，我放弃刚才的条件，又能换来什么？ | 无 | 保留 | 保留 |
| `gwp_terms_final_lead` | 你仍带你的队伍。我来是为了结清这笔账，不是再打一仗。 | 「再打一仗」像是前面打过 | 你的队伍还归你带。我要的是把这笔账结清，不是跟你打一仗。 | Your men stay under your command. I want this account settled, not a battle. |
| `gwp_terms_final_charm` | 双方的人都能活着离开。这比继续争执要紧。 | 无 | 保留 | 保留 |
| `gwp_terms_final_rogue` | 把这笔钱付了，我今天就没有拦你的理由。你还想赌上更多？ / You remove the reason I am standing in your path. Do you really want to wager more? | 英文不对应 | 保留 | Pay this, and I have no reason to stand in your way. Do you really want to wager more? |
| `gwp_terms_final_trade` | 交清了，回报时就不会还挂着一笔余款。 | 无 | 保留 | 保留 |

### 不可达

`gwp_terms_rashmost_lead/charm/rogue/trade`、`gwp_rebuff_rashmost_lead/charm/rogue/trade`：「八成了事」这种打算 `Roll` 从不抽到，不审措辞。

## 六、劝不动，玩家照他说的办

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_accept_payfull` | 全额。交出来。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_haggle` | 七成就七成。剩下的差额，我回去交代。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_cunning` | 我收这一半，剩下的差额也会写进报告。 / Half, then. The rest stays against your name. | 「写进报告」生硬；差额实际留在他的案底里，英文说对了 | 一半就一半。剩下那一半，还记在你账上。 | 保留 |
| `gwp_fa_accept_surrender` | 那就跟我走。你的人自己想办法。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_deeds` | 那就为你做下的事付钱。那些人命的账留着。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_bribe` | 拿过来。这段话没发生过。 | 「这段话没发生过」生硬 | 拿来。今天这番话，就当没说过。 | 保留 |
| `gwp_fa_accept_duel` | 那就拔剑。你我之间了结。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_grace` | 三天，只宽限这一次。我会回来收钱。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_rash` | — | 不可达 | — | — |

## 代码问题（不在本稿改）

1. **英文回绝不分技能**：`GwpFieldArrestLines.PaymentRebuff` 对每种打算只取 `_lead` 的英文，中文有四句、英文玩家永远看到同一句（[核实稿](text-review-term-check-20261104.md)已记 `deedsonly` 一例，实际 8 种打算都这样）。要英文分句，得让 `PaymentRebuff` 按技能取 `English_gwp_rebuff_<打算>_<技能>`，并给 24 条 `_charm/_rogue/_trade` 补英文（现英文都是 `_lead` 的复制）。这是代码改动，另批。
2. **从不抽到的打算**：`GwpOffenderDesire` 里 `PayToAvoidBattle`、`PayForPeace`、`PayGenerously`、`RashMost` 四种，`Roll` 的候选表里没有，`PoliceEnforcementBehavior.Envoy` 也走同一个 `Roll`。相关 14 条文字（本稿 13 条加第 2 批的四条 `gwp_voice_demand_*_alt`）都走不到。是删掉这四种、还是把它们放回候选表，属玩法裁定。
3. **`gwp_fa_open_plain` 走不到**：`Opening` 只在「声望不在 0–39 或被抓过」时调用，最后那条默认分支永远不会选中。

## 本稿统计

共 148 条：不可达 14 条（不审），见核实稿 1 条，实审 133 条。建议改动 36 条（中英都改 20、只改英文 12、只改中文 4），保留 97 条。

- 说法不准 / 编造 18 条：
  - 「不是灰袍，是雇来的人」1 条：`gwp_fa_task_authority`；
  - 「通缉令 / warrant」6 条：`gwp_fa_task_later`、`gwp_terms_haggledown_rogue`、`gwp_terms_bribethewarden_lead`（这三条只英文）、`gwp_rebuff_bribethewarden_lead`、`gwp_rebuff_demandduel_lead`、`gwp_terms_demandduel_lead`（后者同时把单挑说成拼命）；
  - 条件或身份说错 10 条：`gwp_fa_task_record`、`gwp_fa_arg_record_charm`、`gwp_fa_arg_record_rogue`（不一定被抓过）、`gwp_fa_open_respected_known`（没见过玩家）、`gwp_fa_task_final`（赎罪时玩家不是灰袍）、`gwp_fa_arg_authority_trade`（罚金不是赔偿）、`gwp_fa_arg_denial_charm`（商队案也会抽到「村里」）、`gwp_fa_arg_war_rogue`（不一定有尸首）、`gwp_fa_task_tally` 英文与 `gwp_fa_arg_tally_charm`（「隔了几个月」）；
  - 「灰袍的律」1 条：`gwp_fa_arg_war_lead`。
- 其余 18 条是中英不对应和硬译腔。
- 代码问题 3 处，见上。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。代码问题 1–3 另走代码改动流程。
