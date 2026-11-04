# 练兵订单与运兵

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[玩家练兵订单](../state/troop-orders.md)、[日常练兵与运兵](../state/warden-training.md)。分组见[清单](text-review-inventory-20261104.md)第 9 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 订单收款在登记前完成，交付不再收钱；交付时取消订单，`CancelTroopOrder` 收回随行练兵队、不退预付款（[练兵订单](../state/troop-orders.md)，用户 2026-10-21 澄清并批准文案）。
- 负责训练和送兵的是灰袍练兵本职，主文本里多数地方叫「练兵官」，英文一律 “Training Warden”。

## 一、向练兵官订兵

[入会玩家与普通灰袍领主对话下单，或派使者送订单；当场付清；练兵官带随行练兵队训练，练好后亲自送来，交付时玩家可以收下或取消（士兵留下、不退预付款，用户 2026-10-21 批准的说法）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_player_troop_order_file` | 我想让练兵官为我准备一批士兵。 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_title` | 提交调兵订单 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_description` | 你在灰袍中的声望决定订单上限和可选兵种。 / Your Grey Warden standing sets the maximum order and available branches. | 解释规则的句子（用户裁定 2026-10-18：界面不写解释规则的句子） | 选择要订的兵种和人数。 | Choose the troops and how many. |
| `gwp_player_troop_choice` | {VAR_1}名{VAR_2} — {VAR_3}第纳尔 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_confirm` | 发送订单 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_short` | 所付钱物不足订价，订单未成立。 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_unaffordable` | 你付不起这笔订购款，订单未成立。 | 无 | 保留 | 保留 |
| `gwp_player_troop_order_recorded` | 价钱当场结清，我这就把单子送出去。练兵长会把兵训好，亲自给你送来。 / Settle the price with me now, and I will send the order. The Training Warden will train real troops and bring them to you when they are ready. | 同一个人别处叫「练兵官」，这里叫「练兵长 / 练兵领主」；英文 “train real troops” 生硬 | 价钱当场结清，我这就把单子送出去。练兵官会把兵训好，亲自给你送来。 | Settle the price with me now, and I will send the order on. The Training Warden will train the soldiers and bring them to you herself. |
| `gwp_player_troop_order_filed_prepaid` | 订单与款项已交给灰袍，练兵领主训练完成后会亲自送来。 / The Wardens have received your order and payment. The training lord will deliver the soldiers when they are ready. | 同一个人别处叫「练兵官」，这里叫「练兵长 / 练兵领主」 | 订单和款项已经交给灰袍，练兵官练好兵后会亲自送来。 | The Wardens have your order and payment. The Training Warden will bring the soldiers herself once they are trained. |
| `gwp_player_troop_order_filed` | 练兵领主已收到订单，训练完成后会送到你身边。 / The Training Warden has your order and will bring the soldiers to you once they are ready. | 同一个人别处叫「练兵官」，这里叫「练兵长 / 练兵领主」 | 练兵官已收到订单，练好后会把兵送到你身边。 | 保留 |
| `gwp_troop_order_cohort_name` | 灰袍随行练兵队 | 无 | 保留 | 保留 |
| `gwp_troop_order_cohort_lost` | 为你训练的士兵在途中覆没，灰袍正重新训练一批。 | 无 | 保留 | 保留 |
| `gwp_player_troop_delivery_offer` | 你要的兵已经齐了：{VAR_1} 名{VAR_2}。{VAR_3} 第纳尔在你下单那天就结清了，不欠什么。 | 无 | 保留 | 保留 |
| `gwp_player_troop_delivery_accept` | 那就把他们交到我手上吧。 | 无 | 保留 | 保留 |
| `gwp_player_troop_delivery_accepted` | 人归你了。从今往后他们听你的。 | 无 | 保留 | 保留 |
| `gwp_player_troop_delivery_cancel` | 取消订单，士兵留给你。我知道预付款不会退还。 | 无 | 保留 | 保留 |
| `gwp_player_troop_delivery_cancelled` | 好吧。订单已取消，士兵会留在我们这里。 | 无 | 保留 | 保留 |

## 二、请灰袍出面调停

[与灰袍领主的外交对话里，有调停申请时出现（不要求入会）。台词键在练兵订单文件里，内容属第 8 批调停。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_warden_mediation_ask` | 我替你们打的那些仗，把我卷进战争里了。出面替我说句话。 | 无 | 保留 | 保留 |
| `gwp_warden_mediation_done` | 这事交给我们。今天就把话递出去，你替我们挑起来的那几桩，都了了。 | 无 | 保留 | 保留 |
| `gwp_warden_mediation_moot` | 没有什么要我们去递的了。你替我们担下的那些，早就平了。 | 无 | 保留 | 保留 |

## 三、练兵官的运兵队

[练兵官自己派出的运兵队：地图状态、遭遇对话、到村动态。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_training_transport_outbound` | 向{VILLAGE}运送老兵和军械 | 无 | 保留 | 保留 |
| `gwp_training_transport_settling` | 带兵前往{VILLAGE}落脚 | 无 | 保留 | 保留 |
| `gwp_training_transport_greeting` | 我们奉练兵官的命令运送老兵和军械，还得赶路。 | 无 | 保留 | 保留 |
| `gwp_notice_training` | {COUNT}名老兵加入守村队。 | 无 | 保留 | 保留 |

## 本稿统计

共 24 条，全部实审。建议改动 4 条（中英都改 3、只改中文 1），保留 20 条。

- 称呼不统一 3 条（「练兵长」「练兵领主」→「练兵官」）。
- 选单说明里解释规则 1 条。
- 没有发现编造，没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
