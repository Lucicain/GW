# 玩家使者与派遣

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[送信队 / 玩家使者](../state/dispatch.md)，事务显示见[事务界面](../state/affairs-interface.md#玩家与办案使者)。分组见[清单](text-review-inventory-20261104.md)第 7 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 入口：部队界面里对本队普通灰袍士兵说话（`GwpPartyScreenTroopTalkPatch`），或在事务界面点「派使者…」。五种差事：复命、求援、送练兵订单、请求讲和、递交封地重议。资格与禁用原因由 `GwpWardenDispatchDialogue` 判断，原因文字逐条对得上。
- 复命时，若交的钱货少于应缴（`CaseReportAmountDue`，即这桩案子的罚金），先弹窗问让派去的人怎么交代，选「撒谎」按玩家亲自瞒报同样处理（第 5 批交差、第 3 批查账）。弹窗按钮是界面操作，保留。
- 送达结果：封地申请登记后，贵族事务领主会来找玩家收费；求援 / 讲和 / 订单 / 复命各有「已无需」的退回。归队清单按实际人数、伤员、俘虏、物品、金币填。

## 一、在部队界面叫来士兵

[玩家在部队界面对普通灰袍士兵点「派这些人去办事」，士兵开口，玩家选差事。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_dispatch_talk_hint` | 派这些人去办事 | 无 | 保留 | 保留 |
| `gwp_dispatch_greeting` | 指挥官。您吩咐，我们就走。 | 无 | 保留 | 保留 |
| `gwp_dispatch_report` | 替我把这桩委托的账送到灰袍那里。 | 无 | 保留 | 保留 |
| `gwp_dispatch_support` | 去灰袍那里，请他们与我一同出阵。 / Ride to the Wardens and ask them to take the field with me. | 「他们」指灰袍，已批准文本用「她们」 | 去灰袍那里，请她们与我一同出阵。 | 保留 |
| `gwp_dispatch_troop_order` | 把我的调兵订单和款项一起送到灰袍手里。 | 无 | 保留 | 保留 |
| `gwp_dispatch_peace` | 请灰袍为我替他们卷入的战争出面讲和。 / Ask the Wardens to arrange peace in the wars I entered on their behalf. | 「为我替他们卷入的战争」读不通；「他们」指灰袍 | 请灰袍出面，把我替她们打的那几场仗讲和了。 | Ask the Wardens to make peace in the wars I fought on their behalf. |
| `gwp_dispatch_fief_appeal` | 替我递交封地重议申请。 | 无 | 保留 | 保留 |
| `gwp_dispatch_never_mind` | 暂时没事，回岗位去吧。 | 无 | 保留 | 保留 |
| `gwp_dispatch_confirm` | 那就请指挥官点出随我上路的人。 | 无 | 保留 | 保留 |

## 二、选兵、禁用原因与交代

[选好差事后在部队界面挑人；不能派时事务界面显示禁用原因。复命时交得比应缴少，会弹窗问派去的人怎么交代。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_dispatch_membership` | 需已加入灰袍，且灰袍声望达到20。 / Join the Grey Wardens and reach 20 reputation first. | 英文 “reputation”，别处都叫 “standing” | 保留 | Join the Grey Wardens and reach 20 standing with them first. |
| `gwp_affairs_dispatch_encounter` | 当前处于战斗或遭遇中，无法派遣使者。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_outside` | 请离开聚落后，在野外派遣使者。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_busy` | 请先完成当前的使者选兵或付款。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_need_soldiers` | 本队需要有可派遣的普通灰袍士兵。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_in_transit` | 已有办理同类差事的使者在途。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_no_report` | 当前没有可复命的委托。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_no_support` | 当前没有可以求援的委托。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_no_order` | 当前无法提交新的练兵订单。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_no_fief` | 暂无合资格封地，或已有重议申请在办理。 | 无 | 保留 | 保留 |
| `gwp_affairs_dispatch_no_peace` | 当前没有待讲和的势力。 | 无 | 保留 | 保留 |
| `gwp_dispatch_pick_someone` | 至少选择一名士兵。 | 无 | 保留 | 保留 |
| `gwp_dispatch_screen_report` | 送信队 | 无 | 保留 | 保留 |
| `gwp_dispatch_screen_support` | 求援骑手 | 无 | 保留 | 保留 |
| `gwp_dispatch_screen_order` | 选派订兵使者 | 无 | 保留 | 保留 |
| `gwp_dispatch_screen_peace` | 选派讲和使者 | 无 | 保留 | 保留 |
| `gwp_dispatch_screen_fief` | 选派封地申请使者 | 无 | 保留 | 保留 |
| `gwp_dispatch_word_title` | 让他们怎么说 | 无 | 保留 | 保留 |
| `gwp_dispatch_word_body` | 案子应缴 {VAR_2}，这次钱货合计 {VAR_1}。办案经过，让他们怎么交代？ | 无 | 保留 | 保留 |
| `gwp_dispatch_word_truth` | 如实交代 | 无 | 保留 | 保留 |
| `gwp_dispatch_word_lie` | 撒谎 | 无 | 保留 | 保留 |
| `gwp_dispatch_no_receiver` | 附近没有可以接收的灰袍队伍。 | 无 | 保留 | 保留 |
| `gwp_dispatch_no_spare_ship` | 你在海上，没有多余的船，靠岸后才能派出。 | 无 | 保留 | 保留 |

## 三、路上与回来

[使者队在地图上的名字、路上的消息、送达结果与归队清单。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_dispatch_sent` | 你派出的队伍已出发前往{VAR_1}处。 | 无 | 保留 | 保留 |
| `gwp_dispatch_name_report` | 灰袍送信队 | 无 | 保留 | 保留 |
| `gwp_dispatch_name_support` | 灰袍求援骑手 | 无 | 保留 | 保留 |
| `gwp_dispatch_name_order` | 订兵使者队 | 无 | 保留 | 保留 |
| `gwp_dispatch_name_peace` | 讲和使者队 | 无 | 保留 | 保留 |
| `gwp_dispatch_name_fief` | 封地申请使者队 | 无 | 保留 | 保留 |
| `gwp_dispatch_prisoners_sold` | 你的队伍在{VAR_2}卖出 {VAR_1} 名俘虏，得 {VAR_3} 第纳尔。 | 无 | 保留 | 保留 |
| `gwp_dispatch_trouble_battle` | 有消息传来：你派出去的人被堵上了，正在交战。 | 无 | 保留 | 保留 |
| `gwp_dispatch_trouble_mauled` | 有消息传来：你派出去的人伤亡过半。 | 无 | 保留 | 保留 |
| `gwp_dispatch_lost` | 你派出的队伍全军覆没，所带财物尽失。 | 无 | 保留 | 保留 |
| `gwp_dispatch_prisoner_failed` | 押送未能交接，俘虏仍在你手中。 | 无 | 保留 | 保留 |
| `gwp_dispatch_fief_filed` | 使者已递交封地重议申请，灰袍贵族事务领主将来与你会面。 | 无 | 保留 | 保留 |
| `gwp_dispatch_fief_moot` | 封地重议申请已无法登记，使者正在返回。 | 无 | 保留 | 保留 |
| `gwp_dispatch_support_moot` | 信已送到，但此事已无需援手。 | 无 | 保留 | 保留 |
| `gwp_dispatch_peace_done` | 信已送到，灰袍已代你与对方议和。 | 无 | 保留 | 保留 |
| `gwp_dispatch_peace_moot` | 信已送到，但已无需调停。 | 无 | 保留 | 保留 |
| `gwp_dispatch_order_moot` | 灰袍眼下无法接单，款项将随使者带回。 | 无 | 保留 | 保留 |
| `gwp_dispatch_report_moot` | 委托已经结清，所带财物将原样带回。 | 无 | 保留 | 保留 |
| `gwp_dispatch_returning` | 你派出的队伍已办完差事，正在返回。 | 无 | 保留 | 保留 |
| `gwp_dispatch_handover` | 派出的队伍已经归队：交回 {VAR_1} 人（其中 {VAR_2} 名伤员）、{VAR_3} 名俘虏、{VAR_4} 件物资和 {VAR_5} 第纳尔。 | 无 | 保留 | 保留 |

## 四、事务界面里的使者记录

[我的事务各分类下的使者条目与详情。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_report` | 复命使者 | 无 | 保留 | 保留 |
| `gwp_affairs_support_courier` | 求援使者 | 无 | 保留 | 保留 |
| `gwp_affairs_order_courier` | 订单使者 | 无 | 保留 | 保留 |
| `gwp_affairs_peace_courier` | 求和使者 | 无 | 保留 | 保留 |
| `gwp_affairs_receiver` | 收件领主 | 无 | 保留 | 保留 |
| `gwp_affairs_cargo` | 携带物资 | 无 | 保留 | 保留 |
| `gwp_affairs_gold` | 携带金币 | 无 | 保留 | 保留 |
| `gwp_affairs_prisoners` | 押送俘虏 | 无 | 保留 | 保留 |
| `gwp_affairs_departed` | 出发时间 | 无 | 保留 | 保留 |
| `gwp_affairs_waiting` | 等待主队恢复后归队 | 无 | 保留 | 保留 |
| `gwp_affairs_selling` | 途中前往城镇出售普通俘虏 | 无 | 保留 | 保留 |
| `gwp_affairs_pending` | 等待交割 | 无 | 保留 | 保留 |
| `gwp_affairs_status` | 当前情况 | 无 | 保留 | 保留 |
| `gwp_affairs_fief_courier` | 封地申请使者 | 无 | 保留 | 保留 |
| `gwp_affairs_fief_target` | 申请封地 | 无 | 保留 | 保留 |

## 本稿统计

共 67 条，全部实审。建议改动 3 条（中英都改 1、只改中文 1、只改英文 1），保留 64 条。

- 没有发现编造或说法不准。
- 改的是称呼（「他们」→「她们」）、一句读不通的玩家台词和一处英文用词。
- 没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
