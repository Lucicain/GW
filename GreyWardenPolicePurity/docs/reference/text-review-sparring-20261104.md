# 切磋与约斗场

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[切磋](../state/sparring.md)、[野外执法单挑](../state/field-arrest.md#单挑)。分组见[清单](text-review-inventory-20261104.md)第 13 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 切磋没有奖励，不影响关系或声望；只有执法单挑会把胜负交回执法流程结算。
- 执法单挑：罪犯赢，按他开出的条件免罚，案子销掉，案底照留；玩家赢，离开场景后再找他检查财产收款（`GreyWardenSparringBehavior` 赛后对话、`PrepareDuelPropertyCollection`）。
- 野外比试场景 `GreyWardenFieldSparringMissionController` 不区分是切磋还是执法单挑，开场提示是同一句。

## 一、与灰袍领主切磋

[玩家对灰袍领主提出切磋（[切磋](../state/sparring.md)）；野外切磋先选骑战或步战，赛后对方说一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_sparring_challenge` | 愿意和我切磋一下吗？我想试试自己的水平。 | 无 | 保留 | 保留 |
| `gwp_sparring_accept` | 当然。只是友好切磋，不伤和气。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_centre` | 你想怎么打？ | 无 | 保留 | 保留 |
| `gwp_sparring_field_mounted` | 骑马打。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_foot` | 步战。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_post_win` | 打得很好，你赢得很漂亮。继续保持。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_post_loss` | 别灰心，你打得不错。下次再来，我相信你会更强。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_post_reply` | 谢谢。下次再切磋。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_post_rule_violation` | 说好步战，你却一直不肯下马。这场算你输。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_post_rule_reply` | 我明白了。 | 无 | 保留 | 保留 |

## 二、野外比试场景

[野外比试场景里的交谈提示、开场与胜负提示。野外执法里罪犯要求的单挑也走这个场景。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_sparring_talk` | 交谈 | 无 | 保留 | 保留 |
| `gwp_sparring_begin` | 切磋开始。 / The match has started. | 执法单挑也走这条管线，开场都提示「切磋开始」 | 比试开始。 | The bout begins. |
| `gwp_sparring_field_rule_loss` | 你没有按时下马，这场算你输。 | 无 | 保留 | 保留 |
| `gwp_sparring_field_win` | {OPPONENT}认输了，你赢了。 / {OPPONENT} gives up. You won. | 英文时态不一致 | 保留 | {OPPONENT} gives up. You win. |
| `gwp_sparring_field_loss` | 你输给了{OPPONENT}。 | 无 | 保留 | 保留 |

## 三、执法单挑赛后

[野外执法单挑结束后与罪犯对话：玩家赢了当场或稍后收罚金；罪犯赢了按约免罚（[野外执法](../state/field-arrest.md#单挑)）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_duel_post_win` | 你赢了，我认。照约定，看看我的财物，把罚金结清吧。 | 无 | 保留 | 保留 |
| `gwp_duel_inspect_property` | 把你的财物拿出来，现在结清罚金。 | 无 | 保留 | 保留 |
| `gwp_duel_collect_later` | 我稍后再来收。 | 无 | 保留 | 保留 |
| `gwp_case_duel_post_loss` | 你输了。按约定，今天你不能向我收这笔罚金。 / You lost. As agreed, you collect no fine from me today. | 说法不准：罪犯赢了按约免罚、案子销掉，不是「今天」不能收 | 你输了。照约定，这笔罚金你不能再收了。 | You lost. As agreed, you collect no fine from me. |
| `gwp_case_duel_post_reply` | 我会把结果向灰袍回报。 | 无 | 保留 | 保留 |

## 本稿统计

共 20 条，全部实审。建议改动 3 条（中英都改 2、只改英文 1），保留 17 条。

- 说法不准 2 条：罪犯赢后「今天你不能收」（实际是案子销掉）、执法单挑也提示「切磋开始」。
- 英文时态 1 条。没有发现编造，没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
