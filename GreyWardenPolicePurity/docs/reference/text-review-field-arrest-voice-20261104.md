# 野外执法：罪犯的口吻与回应

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[野外执法](../state/field-arrest.md)。分组见[清单](text-review-inventory-20261104.md)第 2 批，前一批是[开场与劝说](text-review-field-arrest-open-20261104.md)。

`GwpFieldDialogueVoice` 是罪犯台词的备选池：同一处有两三句，随机取、不连着重样，同一次遭遇里缓存。场景说明只给审阅时看。表里没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 取词入口在 `GwpFieldArrestBehavior`：开场 `OpeningCondition`，罪名后 `SetVoice("hear"/"refuse")`，层末 `done1/lost1/done2/lost2`，说辞成功 `success_<性子>`，认罚 `PrepareSubmitCondition`，动手 `PrepareFightReply`（收过钱用 betray，否则 fight）。
- 「跟灰袍了结过」＝`TotalArrestCount > 0`，被押走、和平交罚金、塞私钱、约斗都算（[罪案登记](../state/crime-intake.md)）。
- 两层都谈成后玩家仍可以动手（`gwp_fa_take_anyway`），这时他已答应交钱、钱还没到手。
- 罚金交进灰袍公库，不给受害者。
- 委托是灰袍交办的案子，没有可以出示的文书。执勤旗装在旗帜槽时，玩家的地图标记和家族显示灰袍黑金鸟徽（[装备](../state/warden-gear.md)），所以「旗子我看见了」通常成立；卸旗后仍可办已接的案子，这时这句不准，但只是少数情况，保留。
- 第二层的打算实际只抽 8 种，`PayToAvoidBattle`、`PayForPeace`、`PayGenerously`、`RashMost` 抽不到（第 1 批代码问题 2），对应四句另一说法不可达。

## 通用问题

1. 本批是性格台词，大多口语、具体，问题集中在几句说错了条件：把「跟灰袍了结过」说成「被带走过」，把罚金说成给受害者，把动手时机说成「钱拿不走」。
2. 第 1 批改了 `gwp_fa_task_authority`（「你不是灰袍」），本批两句另一说法本来就没有这个问题，保留。

## 一、罪犯开口（另一说法）

[与第 1 批开场句同条件，随机换用这两句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_open_under_charge_variant_1` | 你也替灰袍办事了？这倒新鲜。找我什么事？ | 无 | 保留 | 保留 |
| `gwp_fa_open_under_charge_variant_2` | 听过你的名声。先说吧，灰袍让你来干什么？ | 无 | 保留 | 保留 |
| `gwp_fa_open_respected_known_variant_1` | 是你。上次见到灰袍可不是什么好事，这回又怎么了？ / You. My last meeting with the Wardens was no pleasure. What is it now? | 说法不准：条件是他从前跟灰袍了结过，不是见过玩家 | 又是灰袍。上回跟你们打交道可不是什么好事，这回又怎么了？ | The Wardens again. My last dealings with your people were no pleasure. What is it now? |
| `gwp_fa_open_respected_known_variant_2` | 灰袍又找上我了？说吧，我听着。 | 无 | 保留 | 保留 |
| `gwp_fa_open_respected_variant_1` | 你的名声我听过。有什么事，直说吧。 | 无 | 保留 | 保留 |
| `gwp_fa_open_respected_variant_2` | 灰袍派你来，看来是有事要谈。说吧。 | 无 | 保留 | 保留 |
| `gwp_fa_open_known_variant_1` | 上次被你们带走的滋味，我可没忘。这回要做什么？ / I have not forgotten being taken by your people. What do you want this time? | 说法不准：「被灰袍办过」包括和平交罚金，不一定被带走过 | 上回跟灰袍打交道的事，我可没忘。这回要做什么？ | I have not forgotten my last dealings with your people. What do you want this time? |
| `gwp_fa_open_known_variant_2` | 又拦我？灰袍这次有什么事？ | 无 | 保留 | 保留 |

## 二、罪犯开口（声望 0–39、没跟灰袍了结过）

[按他最突出的性子取一句。前一句是玩家的开场白（第 3 批 `gwp_fieldarrest_open`）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_open_upright_0` | 把委托给我看看。该答的话，我会答。 / Show me the warrant. I will answer a proper charge. | 编造：委托没有一纸文书可以给人看；英文 “warrant” | 灰袍托你来的？那就说是什么罪名。该答的话，我会答。 | The Wardens sent you? Then name the charge. I will answer a proper charge. |
| `gwp_voice_open_upright_1` | 你既然代表灰袍，就把事情说清楚。 | 无 | 保留 | 保留 |
| `gwp_voice_open_cold_0` | 你专程追上来，总不会只是问路。说吧。 | 无 | 保留 | 保留 |
| `gwp_voice_open_cold_1` | 灰袍找我？先说是什么事，再谈别的。 | 无 | 保留 | 保留 |
| `gwp_voice_open_fierce_0` | 拦我的路，总得有个说法吧。 | 无 | 保留 | 保留 |
| `gwp_voice_open_fierce_1` | 旗子我看见了。你找我做什么？ | 无 | 保留 | 保留 |
| `gwp_voice_open_soft_0` | 有什么事在这里说，别惊动后面的人。 | 无 | 保留 | 保留 |
| `gwp_voice_open_soft_1` | 灰袍来了……又有谁受了苦？ | 无 | 保留 | 保留 |
| `gwp_voice_open_tight_0` | 先说清楚，我可没答应给你什么。 | 无 | 保留 | 保留 |
| `gwp_voice_open_tight_1` | 你们找上门来，准没什么便宜事。 | 无 | 保留 | 保留 |
| `gwp_voice_open_plain_0` | 你是替灰袍办事的？找我有什么事？ | 无 | 保留 | 保留 |
| `gwp_voice_open_plain_1` | 等一下。你刚才说，灰袍派你来的？ | 无 | 保留 | 保留 |

## 三、回头再找已经交过钱的罪犯

[玩家收了他的钱、还没交差，又上前搭话。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_paid_0` | 钱已经当面点过了。你回来还有什么事？ | 无 | 保留 | 保留 |
| `gwp_voice_paid_1` | 我已经把钱交给你了，还有哪里没说清？ | 无 | 保留 | 保留 |

## 四、第一层：他听完罪名

[玩家念完罪名后，他肯谈（hear）或拒谈（refuse）；第一层谈成（done1）或谈崩（lost1）。各有原句（第 3 批）和这一句另一说法，交替使用。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_hear_alt` | 罪名我听见了。可我凭什么听你处置？ | 无 | 保留 | 保留 |
| `gwp_voice_refuse_alt` | 让我听你处置？不。让开。 | 无 | 保留 | 保留 |
| `gwp_voice_done1_alt` | 行，这件案子我认。至于怎么了结，我们还得谈。 | 无 | 保留 | 保留 |
| `gwp_voice_lost1_alt` | 你还没说出让我服气的理由。别再绕了。 | 无 | 保留 | 保留 |

## 五、第一层顾虑（另一说法）

[与第 1 批三条顾虑同条件，随机换用。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_task_authority_variant_1` | 灰袍管得着这件事，也不见得轮得到你来管我。 | 无 | 保留 | 保留 |
| `gwp_fa_task_authority_variant_2` | 你不过是替灰袍跑腿的。我凭什么听你的？ | 无 | 保留 | 保留 |
| `gwp_fa_task_under_charge_variant_1` | 你自己也没什么好名声，倒来教训我了？ | 无 | 保留 | 保留 |
| `gwp_fa_task_under_charge_variant_2` | 换个人来，我兴许还愿意听。你？先管好自己吧。 | 无 | 保留 | 保留 |
| `gwp_fa_task_price_variant_1` | 这么多钱？灰袍凭什么向我要这个数？ | 无 | 保留 | 保留 |
| `gwp_fa_task_price_variant_2` | 你开口就是这么大一笔，总得给我个说法吧。 | 无 | 保留 | 保留 |
| `gwp_fa_task_tally_variant_1` | 过去的事也要一起追究？你们到底想追到什么时候？ | 无 | 保留 | 保留 |
| `gwp_fa_task_tally_variant_2` | 那些事都隔了多久了，现在倒一起来找我？ / Those incidents were long apart. Now you bring them all to me at once? | 「隔了多久」是编的，几次犯案可能只隔几天 | 那些事各是各的，现在倒一起来找我？ | Those were separate matters. Now you bring them all to me at once? |
| `gwp_fa_task_denial_variant_1` | 我的兵做了什么，你找他们去。我没叫他们这么干。 | 无 | 保留 | 保留 |
| `gwp_fa_task_denial_variant_2` | 部下惹的事，凭什么全要我来担？ | 无 | 保留 | 保留 |
| `gwp_fa_task_war_variant_1` | 打仗还不能拿敌人的东西？灰袍未免管得太宽了。 | 无 | 保留 | 保留 |
| `gwp_fa_task_war_variant_2` | 那是敌人的商队。我在打仗，不是在拦路抢劫。 | 无 | 保留 | 保留 |
| `gwp_fa_task_consequence_variant_1` | 我今天就是不交，你打算怎么办？ | 无 | 保留 | 保留 |
| `gwp_fa_task_consequence_variant_2` | 我带着人走我的路。灰袍还能一直追着我不成？ | 无 | 保留 | 保留 |
| `gwp_fa_task_record_variant_1` | 灰袍以前已经抓过我一次了，还不够吗？ | 无 | 保留 | 保留 |
| `gwp_fa_task_record_variant_2` | 又是灰袍。难道我往后走到哪里，你们都要跟着？ | 无 | 保留 | 保留 |

## 六、劝说说中一句

[玩家某条说辞成功，他按性子回一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_success_upright_0` | 这句话有道理，我认。 | 无 | 保留 | 保留 |
| `gwp_voice_success_upright_1` | 这一点我没法反驳你。 | 无 | 保留 | 保留 |
| `gwp_voice_success_cold_0` | 这倒是我得考虑的事。 | 无 | 保留 | 保留 |
| `gwp_voice_success_cold_1` | 这样算，确实不同。 | 无 | 保留 | 保留 |
| `gwp_voice_success_fierce_0` | 行，这话我听进去了。 | 无 | 保留 | 保留 |
| `gwp_voice_success_fierce_1` | 这句话还算有些分量。 | 无 | 保留 | 保留 |
| `gwp_voice_success_soft_0` | 你说的这些人，我不能不管。 | 无 | 保留 | 保留 |
| `gwp_voice_success_soft_1` | 是……这件事我该多想一想。 | 无 | 保留 | 保留 |
| `gwp_voice_success_tight_0` | 照你这么说，我也不是一点好处都没有。 | 无 | 保留 | 保留 |
| `gwp_voice_success_tight_1` | 这么算倒还说得过去。 | 无 | 保留 | 保留 |
| `gwp_voice_success_plain_0` | 这倒也是。 | 无 | 保留 | 保留 |
| `gwp_voice_success_plain_1` | 嗯，这一点你说服我了。 | 无 | 保留 | 保留 |

## 七、第二层：他说出打算（另一说法）

[与第 1 批 `gwp_fa_desire_*` 交替使用。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_demand_payinfull_alt` | 照数付清就是了。把这件事了结吧。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_paytoavoidbattle_alt` | 先叫你的人别动。钱我照付，犯不着在这里死人。 | 不可达 | — | — |
| `gwp_voice_demand_payforpeace_alt` | 已经够多人受罪了。我把罚金付清，别再牵连别人。 | 不可达 | — | — |
| `gwp_voice_demand_paygenerously_alt` | 这些钱我出得起，全部拿去，把账结清。 | 不可达 | — | — |
| `gwp_voice_demand_surrenderself_alt` | 钱得留着养活这些人。你要交差，就带我一个走。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_bribethewarden_alt` | 这份好处值 {GWP_BRIBE_AMOUNT} 第纳尔，比白跑一趟强。拿了，我们就把话说到这里。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_cunninghalf_alt` | 你要的数目我凑不齐。只有一半，你要就拿去。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_rashmost_alt` | 八成，拿去吧。剩下两成就别再费口舌了。 | 不可达 | — | — |
| `gwp_voice_demand_demandduel_alt` | 我们两个来比一场。你赢，罚金照付；我赢，你空手走。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_deedsonly_alt` | 我只出四成。那几个办坏事的，我自己会杀给你看。 | 无 | 保留 | 保留 |
| `gwp_voice_demand_haggledown_alt` | 七成已经够多了。这个价钱，你好好想想。 | 无 | 保留 | 保留 |
| `gwp_voice_grace_alt` | 我需要三天把钱凑起来。我求的是宽限，不是免掉罚金。 | 无 | 保留 | 保留 |

## 八、第二层收尾

[第二层谈成（done2）或谈崩（lost2）；第二轮顾虑的另一说法。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_done2_alt` | 好，按你说的数目来。别再加别的条件了。 | 无 | 保留 | 保留 |
| `gwp_voice_lost2_alt` | 这一步我不让。刚才开的条件仍然算数。 | 无 | 保留 | 保留 |
| `gwp_terms_final_objection_variant_1` | 我把条件全让了，对我又有什么好处？ | 无 | 保留 | 保留 |
| `gwp_terms_final_objection_variant_2` | 照你说的全部交清，我为什么要吃这个亏？ | 无 | 保留 | 保留 |

## 九、两层都谈成，他认罚

[随后玩家选择收钱。钱和货不够时改说「钱不够」那句（第 3 批）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_submit_upright_0` | 这笔罚是公道的。拿去吧，记上一笔：我是站着交的。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_upright_1` | 我欠的账我不争。点吧。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_upright_2` | 那就是欠着的，欠着的我就还。给。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_cold_0` | 比另一条路便宜。就这一个理由，你我都清楚。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_cold_1` | 这个数比麻烦小。拿走。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_cold_2` | 我算过了。这一栏是你赢。点你的钱去。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_soft_0` | 拿去吧。什么也换不回来，但还是拿去吧。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_soft_1` | 要是这样能给他们一个了断，那就拿去。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_soft_2` | 给。与其留在我手里，不如给他们。 / Here. I would rather it went to them than stayed with me. | 说法不准：罚金交进灰袍公库，不是给受害的人 | 给。这钱留在我手里，我也不安心。 | Here. I'd have no peace keeping it. |
| `gwp_voice_submit_tight_0` | 一个子儿不少。但愿你们那个修会点钱噎着。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_tight_1` | 在这儿。点两遍，也不会少一个子儿。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_tight_2` | 拿了就快走。这事多拖一刻我就多亏一刻。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_fierce_0` | 行，给钱。别当成我怕了你。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_fierce_1` | 拿你的钱。你我之间什么也没了结。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_fierce_2` | 给，你也别再说了。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_plain_0` | 在这儿。点清楚，然后走。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_plain_1` | 好吧。你报的那个数，一分不多。 | 无 | 保留 | 保留 |
| `gwp_voice_submit_plain_2` | 拿去。这事我不想再有麻烦。 | 无 | 保留 | 保留 |

## 十、玩家动手

[没收过他的钱时用 fight，收过钱再动手用 betray。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_fight_upright_0` | 那就让在场的人看清楚，是谁先拔的剑。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_upright_1` | 我不会束手就擒。你要拿人，尽管来。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_cold_0` | 话说到这里，也没什么可算的了。列阵。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_cold_1` | 你选了这条路。希望你算过代价。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_fierce_0` | 好！刚才那些话早该省了。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_fierce_1` | 来啊，看你有没有这个本事！ | 无 | 保留 | 保留 |
| `gwp_voice_fight_soft_0` | 一定要流血才肯罢休吗？那我也只能迎战。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_soft_1` | 退后，别站在我们中间。看来今天是躲不过了。 / Stand back, away from us. It seems there is no avoiding this. | 「别站在我们中间」不知对谁说 | 都退后。看来今天是躲不过了。 | Everyone back. It seems there is no avoiding this. |
| `gwp_voice_fight_tight_0` | 钱拿不走，就来抢人？你试试看。 / Cannot take the money, so you take me? Try it. | 说法不准：他答应交钱以后玩家照样能动手，这时不是「钱拿不走」 | 钱还没到手，就急着抢人？你试试看。 | Not even paid yet, and you grab for me? Try it. |
| `gwp_voice_fight_tight_1` | 想拿我去交差？没那么容易。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_plain_0` | 那就没什么好说的了。拔剑吧。 | 无 | 保留 | 保留 |
| `gwp_voice_fight_plain_1` | 我可不会自己伸手让你绑。 | 无 | 保留 | 保留 |
| `gwp_voice_betray_upright_0` | 钱你收了，约定却不认了？在场的人都听见了！ | 无 | 保留 | 保留 |
| `gwp_voice_betray_upright_1` | 我照约定付了钱。你就是这样替灰袍办事的？ | 无 | 保留 | 保留 |
| `gwp_voice_betray_cold_0` | 原来收钱还不算完。是我把你的话看得太重了。 | 无 | 保留 | 保留 |
| `gwp_voice_betray_cold_1` | 钱进了你的口袋，我还得进你的囚车？好一笔买卖。 | 无 | 保留 | 保留 |
| `gwp_voice_betray_fierce_0` | 拿了钱还敢翻脸？把剑拔出来！ | 无 | 保留 | 保留 |
| `gwp_voice_betray_fierce_1` | 我付钱可不是怕你！现在你就会知道。 | 无 | 保留 | 保留 |
| `gwp_voice_betray_soft_0` | 我交钱是为了让大家平安离开。你连这个也不肯？ | 无 | 保留 | 保留 |
| `gwp_voice_betray_soft_1` | 钱已经给了，为什么还要逼这些人拼命？ | 无 | 保留 | 保留 |
| `gwp_voice_betray_tight_0` | 把刚才的钱还来！收了钱还要抓我，你倒会做生意！ | 无 | 保留 | 保留 |
| `gwp_voice_betray_tight_1` | 我一枚一枚数给你的，现在你说不算？ | 无 | 保留 | 保留 |
| `gwp_voice_betray_plain_0` | 等等，钱不是已经给你了吗？你怎么说翻脸就翻脸？ | 无 | 保留 | 保留 |
| `gwp_voice_betray_plain_1` | 刚才可不是这么说的！钱你也拿了，还要怎样？ | 无 | 保留 | 保留 |

## 十一、走不到的两组：交不出钱、抗法

[`ChargeCondition` 把这两组设成 `GWP_FIELD_ARREST_PLEAD` / `GWP_FIELD_ARREST_RESIST` 变量，但没有任何对话行用这两个变量，玩家看不到（代码问题 1）。不审措辞。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_voice_plead_cold_0` | 你开的这个数我拿不出来。军饷、草料、换马——你指给我看它藏在哪一栏。 | 不可达 | — | — |
| `gwp_voice_plead_cold_1` | 这个数超出我的能耐。你要是不信，账本可以给你看。 | 不可达 | — | — |
| `gwp_voice_plead_cold_2` | 你要的东西不在我钱袋里。这就是个算术问题。 | 不可达 | — | — |
| `gwp_voice_plead_tight_0` | 我不是什么富人，你们卷宗上怎么写是你们的事。这钱你在我身上找不到。 | 不可达 | — | — |
| `gwp_voice_plead_tight_1` | 你愿意搜就搜。这儿没有那么多。 | 不可达 | — | — |
| `gwp_voice_plead_tight_2` | 有人把我的家底跟你说岔了。我付不出。 | 不可达 | — | — |
| `gwp_voice_plead_upright_0` | 我不会答应我付不出的东西。这个数我够不着。 | 不可达 | — | — |
| `gwp_voice_plead_upright_1` | 有的话我就付了。我没有。 | 不可达 | — | — |
| `gwp_voice_plead_soft_0` | 我得先让手下的人吃上饭，才轮到你的钱。已经没剩下什么了。 | 不可达 | — | — |
| `gwp_voice_plead_soft_1` | 别拿这些人还要用的东西。这个数我凑不出。 | 不可达 | — | — |
| `gwp_voice_plead_fierce_0` | 我没有这笔钱，也不打算装作有。 | 不可达 | — | — |
| `gwp_voice_plead_fierce_1` | 按我有的要，别按我没有的要。 | 不可达 | — | — |
| `gwp_voice_plead_plain_0` | 那个数？我身上没带。你想翻辎重就翻。 | 不可达 | — | — |
| `gwp_voice_plead_plain_1` | 这个数我凑不出来。今天不行，就凭我手上这点更不行。 | 不可达 | — | — |
| `gwp_voice_resist_upright_0` | 我不认的罪，我不会掏钱。你们要办就办吧。 | 不可达 | — | — |
| `gwp_voice_resist_upright_1` | 把我带到有资格裁断这件事的人跟前。在那之前，不行。 | 不可达 | — | — |
| `gwp_voice_resist_fierce_0` | 我从来没花钱买过平安。拔刀。 | 不可达 | — | — |
| `gwp_voice_resist_fierce_1` | 我站着的时候，你从我身上拿不走任何东西。来吧。 | 不可达 | — | — |
| `gwp_voice_resist_fierce_2` | 话说够了。让刀来分。 | 不可达 | — | — |
| `gwp_voice_resist_cold_0` | 你那一列我数过，我这一列我也数过。不行。 | 不可达 | — | — |
| `gwp_voice_resist_cold_1` | 这个胜算还不够你张这个口。我的答复是不。 | 不可达 | — | — |
| `gwp_voice_resist_cold_2` | 不。你愿意的话，可以试试这个答复。 | 不可达 | — | — |
| `gwp_voice_resist_tight_0` | 一个第纳尔你也别想拿到。一个都没有。 | 不可达 | — | — |
| `gwp_voice_resist_tight_1` | 要我交那个数，我宁可流血。你试试。 | 不可达 | — | — |
| `gwp_voice_resist_tight_2` | 一个子儿也没有。有本事从我尸首上摸。 | 不可达 | — | — |
| `gwp_voice_resist_soft_0` | 我的人还要用的东西，我不能给你。我不给。 | 不可达 | — | — |
| `gwp_voice_resist_soft_1` | 走到这一步我也难过，但我的答复是不。 | 不可达 | — | — |
| `gwp_voice_resist_plain_0` | 不。有本事就来拿。 | 不可达 | — | — |
| `gwp_voice_resist_plain_1` | 我的答复是不。你该怎么办就怎么办。 | 不可达 | — | — |
| `gwp_voice_resist_scorn_0` | 你自己头上还挂着案子，倒来收我的？拔刀吧。 | 不可达 | — | — |
| `gwp_voice_resist_scorn_1` | 一个通缉犯跑来收罚金。我不向你交代。 | 不可达 | — | — |
| `gwp_voice_resist_scorn_2` | 先把你自己的名字弄干净，再照单子念我的。 | 不可达 | — | — |

## 代码问题（不在本稿改）

1. **抗法、交不出钱两组台词没人用**：`GwpFieldArrestBehavior.ChargeCondition` 每次都给 `GWP_FIELD_ARREST_RESIST` 和 `GWP_FIELD_ARREST_PLEAD` 设值，但注册的对话行里没有一句用这两个变量；交不出钱实际说的是 `gwp_case_purse_short`（第 3 批），抗法没有对应台词，直接进入动手。所以 `gwp_voice_resist_*`（17 条）和 `gwp_voice_plead_*`（15 条）玩家看不到。[可疑用词核实稿](text-review-term-check-20261104.md)把 `gwp_voice_resist_scorn_1`、`gwp_voice_plead_tight_0` 判成会显示，并给 `resist_scorn_0/1` 出了改稿，按这里的核实，这两组要先接回对话才需要改字。是接回去还是连同变量一起删，属玩法裁定。
2. 四句 `gwp_voice_demand_*_alt` 不可达，原因同第 1 批代码问题 2。

## 本稿统计

共 144 条：不可达 36 条（抗法 17、交不出钱 15、打算另一说法 4），实审 108 条。建议改动 7 条（全部中英同改），保留 101 条。

- 说法不准 / 编造 6 条：`gwp_voice_open_upright_0`（出示委托文书，编造）、`gwp_fa_open_respected_known_variant_1`、`gwp_fa_open_known_variant_1`（不一定被带走过）、`gwp_voice_fight_tight_0`（钱拿不走）、`gwp_voice_submit_soft_2`（罚金给受害者）、`gwp_fa_task_tally_variant_2`（隔了很久）。
- 其余 1 条是说话对象不清（`gwp_voice_fight_soft_1`）。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，不改代码。
