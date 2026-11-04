# 野外执法：收款、宽限、约斗与结果

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[野外执法](../state/field-arrest.md)，交差与查账见[玩家承办委托](../state/player-commission.md)。分组见[清单](text-review-inventory-20261104.md)第 3 批，前两批是[开场与劝说](text-review-field-arrest-open-20261104.md)、[罪犯口吻](text-review-field-arrest-voice-20261104.md)。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 对话树在 `GwpFieldArrestBehavior.AddDialogs`：入口 → 罪犯开口 → 玩家念罪名 → 第一层 → 问打算 → 第二层 → 收钱 / 照他说的办 / 动手；任何一步可以走开或动手。
- 罪名短语只有两种（`DescribeCrime`）：商队案和其余（村庄暴力），只用在念罪名这一句里，改短语不影响别处。
- 宽限日志由 `GwpPlayerCaseAssignment.RecordFieldGrace` 写进当前任务：普通悬赏写悬赏任务，赎罪写赎罪任务。赎罪不是灰袍的「委托」。
- 收钱走原版交易界面（`GwpAssetPayment`），交到公库的罚金另记一个条目（`GwpFieldFineBarterable`）；`gwp_patrol_barter_name_cn` 是默认名，两个调用方都传自己的名称，实际不显示（见[核实稿](text-review-term-check-20261104.md)）。
- 查账：`GwpFieldReportLedger.ResolveExcess` / `ResolveAudit` 按概率核查，查出才扣声望并提示。

## 通用问题

1. 玩家「强制执行通缉令」：没有通缉令，且是公文腔。
2. 念罪名那句「灰袍指控你曾袭击灰袍保护的商队，受害方是……」：连说两遍灰袍，「受害方」是公文用语；罪名短语和替补词一起改。
3. 英文有四处写死 “him/his”，罪犯可能是女领主。

## 一、玩家开口，念出罪名

[玩家在野外选中承办的罪犯，选这句开始；罪犯开口（第 1、2 批）后，玩家念罪名。`VAR_1` 是罪名短语，`VAR_2` 是受害方（村名、商队名，查不到时用替补词），`VAR_3` 是罚金。谈崩不久再来时，玩家改说 `gwp_fa_charge_cooled`。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fieldarrest_open` | 我受灰袍委托，为你名下的一件案子而来。 / Stand where you are. I speak for the Grey Wardens. | 英文另一句 | 保留 | The Grey Wardens sent me about a case against you. |
| `gwp_fieldarrest_charge` | 灰袍指控你曾{VAR_1}，受害方是{VAR_2}。应缴罚金 {VAR_3} 第纳尔，我受托来处理这件事。 / The Grey Wardens charge you with {VAR_1}, against {VAR_2}. The fine is {VAR_3} denars. They sent me to settle this. | 「受害方」公文腔；与罪名短语一起读重复「灰袍」 | 灰袍查明你{VAR_1}，受害的是{VAR_2}。罚金{VAR_3}第纳尔，她们托我来跟你了结这件事。 | The Grey Wardens have found that you {VAR_1}, and the ones who suffered were {VAR_2}. The fine is {VAR_3} denars, and they sent me to settle it with you. |
| `gwp_fieldarrest_crime_caravan` | 袭击灰袍保护的商队 / attacking a caravan under Warden protection | 嵌进上句后「灰袍查明你袭击灰袍保护的商队」重复；随上句改写 | 袭击了商队 | attacked a caravan |
| `gwp_fieldarrest_crime_village` | 对灰袍保护的村民动手 / violence against villagers under Warden protection | 同上 | 对村民动了手 | attacked villagers |
| `gwp_fieldarrest_victim_unknown` | 灰袍记录在案的受害人 / the Wardens' own record | 嵌进罪名句读成「受害的是灰袍记录在案的受害人」；英文另一句 | 无辜的人 | innocent people |
| `gwp_field_villagers` | 附近的村民 | 无 | 保留 | 保留 |
| `gwp_fa_charge_cooled` | 这桩案子，我们再谈一次。 | 无 | 保留 | 保留 |

## 二、玩家走开或动手

[任何一步玩家都可以走开或动手；走开时罪犯回一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fieldarrest_walk_away` | 算了，你走吧。 | 无 | 保留 | 保留 |
| `gwp_fa_leave_it` | 那你走吧。这事没完。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_dismissed` | 那就别耽误我的工夫。 | 无 | 保留 | 保留 |
| `gwp_fa_take_by_force` | 我要强制执行通缉令，拿下你。 / I will enforce the warrant by force. | 没有「通缉令」；「强制执行」公文腔 | 那就别怪我动手了。拿下！ | Then I'll take you by force. |
| `gwp_fa_take_anyway` | 你的银子留着。人我带走。 | 无 | 保留 | 保留 |

## 三、第一层

[罪犯拒谈（refuse）或肯听（hear）；某条说辞没说动时再问一次（retry）；大失败封掉对话；谈成（done）或谈崩（failed，随后 lost）。各句与第 2 批另一说法交替。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_refuse_strength` | 灰袍这次的要求，我不接受。我们没什么好谈的。 | 无 | 保留 | 保留 |
| `gwp_fa_hear_charge` | 指控我听见了。不过，你先说说，我为什么要接受你的处置？ | 无 | 保留 | 保留 |
| `gwp_fa_retry_authority` | 这还不足以让我接受你的处置。你还有别的理由吗？ | 无 | 保留 | 保留 |
| `gwp_fa_critical_rebuff` | 够了，这件事我不想再谈。 | 无 | 保留 | 保留 |
| `gwp_fa_l1_done` | 好，我接受这次处置。接下来谈谈怎么了结。 | 无 | 保留 | 保留 |
| `gwp_fa_l1_failed` | 我不接受这份指控，你也休想凭几句话让我低头。 / Enough. Your order has no claim on me, and neither do you. | 英文另一句 | 保留 | I don't accept this charge, and a few words from you won't make me bow. |
| `gwp_fa_l1_lost` | 我不接受灰袍这次处置。你执意动手，就拔剑吧。 | 无 | 保留 | 保留 |

## 四、第二层

[玩家问他打算怎么了结；不是照数全缴的，玩家可以要求照数谈。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_request_terms` | 那就说吧，你打算怎么了结？ | 无 | 保留 | 保留 |
| `gwp_fa_counter_offer` | 这个条件我不能接受。我们谈谈照数缴清的事。 | 无 | 保留 | 保留 |
| `gwp_fa_retry_payment` | 我还是坚持刚才的条件。你还有别的说法吗？ | 无 | 保留 | 保留 |
| `gwp_fa_l2_done` | ……那就全额。随你。 | 无 | 保留 | 保留 |
| `gwp_fa_l2_failed` | 全额我不答应。我能接受的条件已经告诉你了。 / No. I have told you how this will go. | 英文与中文不对应 | 保留 | I won't agree to the full sum. I've told you what I can accept. |
| `gwp_fa_accept_count` | 把钱和东西拿出来，能抵多少，我们当面说清。 | 无 | 保留 | 保留 |
| `gwp_fa_desire_done_line` | 那咱们就算说明白了。 | 无 | 保留 | 保留 |

## 五、收钱

[走原版交易界面。他的钱袋全部可见，但交易按钮拒绝超过谈定数目的报价；交易界面的条目名和报价栏也在这里。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fa_count_money` | 钱和东西都在这里，你来挑。 | 无 | 保留 | 保留 |
| `gwp_case_collect_amount` | 拿出价值 {VAR_1} 第纳尔的财物。实收和短缺我会回报。 / Present property worth {VAR_1} denars. I will report the payment and any shortfall. | 「实收和短缺我会回报」是机制说明 | 把价值{VAR_1}第纳尔的钱和东西拿出来。 | Hand over money and goods worth {VAR_1} denars. |
| `gwp_case_purse_short` | 罚金我认，可我身边的钱和东西合起来只值 {VAR_1} 第纳尔。现在能交的就是这些。 | 无 | 保留 | 保留 |
| `gwp_fa_collection_paid` | 钱点清了。今天就到这里。 | 无 | 保留 | 保留 |
| `gwp_fa_collection_empty` | 你自己看吧，已经没有能交给你的钱和东西了。 | 无 | 保留 | 保留 |
| `gwp_fa_accept_empty` | 我会如实回报你付不起钱，欠账仍记在你名下。 | 无 | 保留 | 保留 |
| `gwp_fa_collection_cancel` | 钱还没交出去。你到底打算怎么办？ | 无 | 保留 | 保留 |
| `gwp_collection_agreement` | 履行约定 | 无 | 保留 | 保留 |
| `gwp_asset_gold` | 第纳尔 | 无 | 保留 | 保留 |
| `gwp_asset_value` | {VAR_1}（每件 {VAR_2} 第纳尔） | 无 | 保留 | 保留 |
| `gwp_field_collection_money` | 犯人钱袋中的第纳尔 | 无 | 保留 | 保留 |
| `gwp_case_payment` | 向灰袍司法公库上缴罚金 | 无 | 保留 | 保留 |
| `gwp_offer_changed` | 交付的钱物与约定不符，请重新调整。 | 无 | 保留 | 保留 |
| `gwp_dispatch_manifest_offer` | 装载 {VAR_1} / {VAR_2} | 无 | 保留 | 保留 |
| `gwp_patrol_barter_name_cn` | 罚金条款 | 不可达 | — | — |

## 六、了结后的地图提示

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fieldarrest_msg_fine` | {VAR_1}缴清了 {VAR_2} 第纳尔罚金，须交给灰袍领主。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_msg_partial` | {VAR_1}只缴了 {VAR_2} 第纳尔，应缴 {VAR_3} 第纳尔。所收罚金须交给灰袍领主。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_msg_surrender` | {VAR_1}投降了。请将他押交灰袍领主。 / {VAR_1} surrenders. Deliver him to a Warden lord. | 英文写死 “him” | 保留 | {VAR_1} surrenders. Deliver the prisoner to a Warden lord. |
| `gwp_case_capture_failed` | 目标未被俘获，案件尚未了结。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_msg_bribe` | {VAR_1}私下塞给你价值 {VAR_2} 第纳尔的财物。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_msg_own_men` | 他当着你的面处决了几名手下顶罪。 | 无 | 保留 | 保留 |

## 七、宽限三天

[玩家答应宽限后写进任务日志（悬赏或赎罪任务都写）；到期提示一次；期间再遇见他，玩家可以等或收钱。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_grace_granted` | 你给了{VAR_1}三天筹措罚金。委托尚未完成，届时仍须收钱或拿人。 / You allowed {VAR_1} three days to raise the fine. The commission remains open; return to collect it or take the offender into custody. | 说法不准：赎罪任务也写进这句，赎罪不是「委托」；后半句是机制说明 | 你给了{VAR_1}三天筹措罚金。到期后再去找他收钱，或者拿人。 | You gave {VAR_1} three days to raise the fine. When they are up, go back to collect it or take the offender in. |
| `gwp_grace_due` | 你给{VAR_1}的宽限已经到期。可以前去收钱或拿人。 / The time you allowed {VAR_1} has expired. Seek payment or take him into custody; no payment has been received automatically. | 英文念机制（“no payment has been received automatically”），写死 “him” | 保留 | The time you gave {VAR_1} is up. Go and collect the fine, or take the offender in. |
| `gwp_grace_return_early` | 说好的日子还没到。到那天之前，我这儿拿不出什么给你。 | 无 | 保留 | 保留 |
| `gwp_grace_return_due` | 说好的日子到了。你看看我现在能拿出多少吧。 | 无 | 保留 | 保留 |
| `gwp_fa_grace_wait` | 那我就等到说好的那天再来。 | 无 | 保留 | 保留 |
| `gwp_grace_collect` | 把现在能交的钱拿出来看看。 | 无 | 保留 | 保留 |

## 八、单挑

[他要求单挑、玩家答应后进野外切磋；赛后结果提示，玩家赢了要回来再找他收钱。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_duel_failed` | 决斗未能开始。 | 无 | 保留 | 保留 |
| `gwp_fieldarrest_msg_duel_lost` | {VAR_1}赢了决斗，你依约放他离开。 / {VAR_1} wins the duel, and you let him go as agreed. | 英文写死 “him” | 保留 | {VAR_1} wins the duel, and you let the offender go as agreed. |
| `gwp_duel_payment_pending` | 你赢了决斗，可以收缴他的罚金。 / You win the duel. You may now collect his fine. | 英文写死 “his” | 保留 | You win the duel. You may now collect the fine. |
| `gwp_duel_return_collect` | 单挑是你赢了。照约定，我还欠着你这笔罚金。 | 无 | 保留 | 保留 |

## 九、交差以后灰袍查账

[交差后过一阵按概率核查：对已经受过惩处的人动手，或上交的账有短缺，查出来扣声望。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_report_excess_found` | 灰袍查明{VAR_1}已受过惩处，你仍对他动了手。 | 无 | 保留 | 保留 |
| `gwp_report_audit_found` | 灰袍查出你上交的账目短缺 {VAR_1} 第纳尔。 | 无 | 保留 | 保留 |

## 本稿统计

共 59 条：不显示 1 条（不审），实审 58 条。建议改动 14 条（中英都改 7、只改英文 7），保留 44 条。

- 说法不准 / 编造 2 条：`gwp_fa_take_by_force`（通缉令）、`gwp_grace_granted`（赎罪也写成「委托」）。
- 机制说明 2 条：`gwp_case_collect_amount`、`gwp_grace_due` 英文。
- 其余是公文腔（念罪名一组 4 条）、中英不对应（3 条）和英文写死性别（3 条）。
- 没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
