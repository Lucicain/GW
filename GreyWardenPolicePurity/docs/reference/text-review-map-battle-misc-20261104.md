# 战场援军、介入与地图杂项

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。分组见[清单](text-review-inventory-20261104.md)第 16 批（最后一批）。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 一、战场援军

[玩家在场的战斗里，灰袍一方人手吃紧时按概率来援（[战场援军](../state/battle-support.md)）。玩家是本方接收者、第一次判定没成时提示「默默祈祷」；援军到场时按敌我各一句。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwpbattlereinforcementbehavior_001` | 你默默祈祷援军到来。 | 无 | 保留 | 保留 |
| `gwp_gwpbattlereinforcementbehavior_002` | 灰袍援军赶到了！ | 无 | 保留 | 保留 |
| `gwp_battle_support_enemy` | 敌方的灰袍援军抵达战场！ | 无 | 保留 | 保留 |

## 二、介入正在发生的战斗

[地图上遇到村民 / 商队 / 灰袍执法的战斗，选边后进入准备菜单（[现场介入](../state/player-justice.md#现场介入正在发生的犯罪与执法)）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_intervention_attack` | 进攻。 | 无 | 保留 | 保留 |
| `gwp_intervention_send` | 派兵进攻。 | 无 | 保留 | 保留 |
| `gwp_intervention_back` | 返回选择。 | 无 | 保留 | 保留 |
| `gwp_intervention_leave` | 不要介入。 | 无 | 保留 | 保留 |
| `gwp_intervention_prepare` | 你准备帮助{ALLY}对抗{ENEMY}。 友方兵力：{ALLIES} 对方兵力：{ENEMIES} | 无 | 保留 | 保留 |
| `gwp_intervention_unfit` | 你的部队目前无法出战。 | 无 | 保留 | 保留 |

## 三、地图杂项

[灰袍成员追捕时的地图状态；城镇 / 城堡菜单里的进城搜捕入口（进城搜捕稿已审，这两条键不在那份稿里）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_location_duty_travelling` | 正在前往{TARGET_PARTY}最后出现的位置。 | 无 | 保留 | 保留 |
| `gwp_location_duty_pursuing` | 正在追捕{TARGET_PARTY}。 | 无 | 保留 | 保留 |
| `gwp_city_search_option` | 带兵进城搜捕{TARGET} | 无 | 保留 | 保留 |
| `gwp_city_search_wounded` | 你受了伤，不能带兵搜捕。 | 无 | 保留 | 保留 |

## 本稿统计

共 13 条，全部实审。建议改动 0 条，全部保留。没有发现编造、说法不准或新的代码问题。

## 全部 16 批汇总

| # | 子系统 | 条数 | 不可达 / 不显示 | 建议改动 | 说法不准 / 编造 |
|---|---|---|---|---|---|
| 1 | [野外执法：开场与劝说](text-review-field-arrest-open-20261104.md) | 148 | 14 | 36 | 18 |
| 2 | [野外执法：罪犯口吻](text-review-field-arrest-voice-20261104.md) | 144 | 36 | 7 | 6 |
| 3 | [野外执法：收款与结果](text-review-field-arrest-result-20261104.md) | 59 | 1 | 14 | 2 |
| 4 | [AI 领主震慑台词](text-review-ai-deterrence-20261104.md) | 146 | 0 | 121 | 58 |
| 5 | [悬赏委托与入会退会](text-review-bounty-membership-20261104.md) | 108 | 0 | 27 | 5 |
| 6 | [灰袍事务界面](text-review-affairs-ui-20261104.md) | 188 | 0 | 14 | 0（另与宗教稿不一致 3） |
| 7 | [玩家使者与派遣](text-review-envoy-dispatch-20261104.md) | 67 | 0 | 3 | 0 |
| 8 | [封地申诉与调停讲和](text-review-fief-mediation-20261104.md) | 49 | 8 | 8 | 1 |
| 9 | [练兵订单与运兵](text-review-troop-training-20261104.md) | 24 | 0 | 4 | 0 |
| 10 | [玩家罪行记录与声望通知](text-review-player-conduct-20261104.md) | 68 | 4 | 21 | 1 |
| 11 | [赎罪与纠察队其余提示](text-review-enforcement-rest-20261104.md) | 53 | 11 | 19 | 2 |
| 12 | [家族百科与婚姻收养](text-review-family-encyclopedia-20261104.md) | 108 | 0 | 6 | 1 |
| 13 | [切磋与约斗场](text-review-sparring-20261104.md) | 20 | 0 | 3 | 2 |
| 14 | [村庄打赏与重建](text-review-village-relief-20261104.md) | 12 | 0 | 4 | 0 |
| 15 | [物品与兵种名称](text-review-items-troops-20261104.md) | 36 | 0 | 0 | 0 |
| 16 | 战场援军、介入与地图杂项（本稿） | 13 | 0 | 0 | 0 |
| | 合计 | 1243 | 74 | 287 | |

各批发现的代码问题汇总进了 `workflow.md` 清理待办。
