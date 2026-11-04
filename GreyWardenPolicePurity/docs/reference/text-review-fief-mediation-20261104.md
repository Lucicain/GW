# 封地申诉、调停讲和与灰袍宣战理由

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[贵族事务：封地重议](../state/player-requests.md)、[灰袍出面调停](../state/warden-mediation.md)。分组见[清单](text-review-inventory-20261104.md)第 8 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 封地重议：费用 50,000 第纳尔、只由贵族事务本职收取、收后去争议封地游说并重开封地决议，台词与 `GreyWardenPlayerRequestBehavior` 一致。
- 调停：`PoliceAntiWarDeclaration.RecordMediationRequest` 登记时显示 `gwp_warden_mediation_owed`，代码传入了对方势力名，句子没用上。
- 宣战理由：`GwpPoliceWarReasonService.BuildTaskReasonDetail` 拼成一行，显示在事务界面「组织概况」（`GwpAffairsVM` 第 288 行）。`IsEscortingPlayer` 只在玩家被灰袍击败后置真（`PoliceEnforcementBehavior` 第 1304 行），灰袍随后把玩家押往最近的城堡。
- `GwpPoliceWarReasonService` 里的 `FormatLocation`、`FormatCampaignDate` 两个私有方法没有任何调用（事务界面用的是 `GwpCaseArchiveItemVM` 里同名的另一组），它们用的 8 条坐标 / 季节文字走不到。

## 一、封地重议申诉

[玩家参与攻下、却被分给别家的封地。玩家向灰袍领主申请，或灰袍贵族事务本职按概率主动找上门；她亲自来见玩家收 50,000 第纳尔进公库，玩家可以付、搁置或撤回；付了以后她去争议封地组织民间请愿，再开一次封地决议。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_fief_appeal_file` | 我想为一块封地申请重新审议。 | 无 | 保留 | 保留 |
| `gwp_fief_appeal_recorded` | 我会登记这份请愿。在收取任何费用前，贵族事务协调官会亲自来见你。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_insufficient` | 你没有准备好所需的五万第纳尔。可以暂时搁置请愿，也可以直接撤回，无需付款。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_offer` | 你的请愿已交到我手中。向灰袍公库支付五万第纳尔后，我会发动民间支持，并重新开启封地决议。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_accept` | 收下这五万第纳尔，开始行动吧。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_accepted` | 款项已经记入公库。我现在就前往争议封地。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_defer` | 先搁置这份请愿。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_deferred` | 好。请愿会继续保留，时机合适时我会再来找你。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_withdraw` | 撤回这份请愿。 | 无 | 保留 | 保留 |
| `gwp_fief_payment_withdrawn` | 那么无需支付费用，这份请愿就此关闭。 | 无 | 保留 | 保留 |
| `gwp_fief_select_hint` | 你参加了这场攻城战，但封地最终授予了其他家族。 | 无 | 保留 | 保留 |
| `gwp_fief_select_title` | 选择申诉封地 | 无 | 保留 | 保留 |
| `gwp_fief_select_description` | 选择一座近期攻下、需要重新审议归属的城镇或城堡。 | 无 | 保留 | 保留 |
| `gwp_file_petition` | 提交请愿 | 无 | 保留 | 保留 |
| `gwp_dispatch_fief_send` | 派出使者 | 无 | 保留 | 保留 |
| `gwp_cancel` | 取消 | 无 | 保留 | 保留 |
| `gwp_fief_auto_triggered` | 灰袍的贵族事务官正前来见你。 | 无 | 保留 | 保留 |
| `gwp_fief_request_filed` | 封地请愿已经登记，贵族事务官正前来见你。 | 无 | 保留 | 保留 |
| `gwp_fief_vote_reopened` | 灰袍已就{VAR_1}提出请愿，王国将重新商议封地归属，你在候选之列。 | 无 | 保留 | 保留 |
| `gwp_fief_defer_complete` | 贵族事务官将回来继续处理你的封地请愿。 | 无 | 保留 | 保留 |
| `gwp_fief_appeal_refunded` | 封地请愿未能提交，费用已全额退还。 | 无 | 保留 | 保留 |
| `gwp_fief_reconsideration_title` | 重新审议{SETTLEMENT_NAME}的归属 | 无 | 保留 | 保留 |
| `gwp_fief_reconsideration_support` | 灰袍已递交民间请愿，议会将重新审议谁应拥有{SETTLEMENT_NAME}。 | 无 | 保留 | 保留 |

## 二、调停与讲和

[替灰袍打出来的战争记进调停申请；玩家请灰袍出面后讲和，或替玩家清掉对受害势力的犯罪评级。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_helpers_001` | 经灰袍调停，你与{VAR_1}恢复了和平。 | 无 | 保留 | 保留 |
| `gwp_policepatrolbehavior_helpers_002` | 经灰袍调停，{VAR_1}不再追究你的罪行。 | 无 | 保留 | 保留 |
| `gwp_warden_mediation_owed` | 此战因替灰袍出手而起，可请灰袍出面调停。 / This war began when you fought for the Wardens. You may ask them to mediate. | 代码传了势力名 `VAR_1`，句子没用，玩家看不出是哪一场仗 | 你与{VAR_1}的这场仗，是替灰袍出手打起来的。可以请灰袍出面调停。 | Your war with {VAR_1} began when you fought for the Wardens. You may ask them to mediate. |
| `gwp_affairs_need_mediation` | 可请灰袍出面讲和 | 无 | 保留 | 保留 |

## 三、灰袍的宣战理由

[事务界面「组织概况」里列出灰袍当前每场战争的理由：`_029` 拼上行动类型（`_030`–`_033`）、承办队、目标、罪名和阶段（`_034`–`_038`）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwppolicewarreasonservice_007` | 追捕事由已了，灰袍正着手议和。 / No outstanding warrant remains. The Grey Wardens are arranging peace. | 英文 “warrant”，游戏里没有通缉令 | 保留 | Nothing remains to pursue. The Grey Wardens are arranging peace. |
| `gwp_gwppolicewarreasonservice_023` | 未记录的执法队 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_024` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_025` | 未记录 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_029` | {VAR_1}：{VAR_2} 正在追捕 {VAR_3}（{VAR_4}）—— {VAR_5} | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_030` | 玩家悬赏协同 / Player bounty collaboration | 「玩家」是开发用语 | 协助你办案 | Helping your case |
| `gwp_gwppolicewarreasonservice_031` | 玩家案件执法 / Player case enforcement | 「玩家」是开发用语；这一条的追捕对象就是玩家本人 | 追究你的案子 | Your own case |
| `gwp_gwppolicewarreasonservice_032` | 跨势力追缉 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_033` | 执法任务 / Law enforcement contract | 英文 “contract” 不对 | 保留 | Enforcement |
| `gwp_gwppolicewarreasonservice_034` | 灰袍部队正在护送玩家追缉目标 / A Grey Warden party is escorting the player in pursuit of the quarry | 「玩家」是开发用语 | 灰袍正陪你追捕目标 | A Grey Warden party is riding with you after the target |
| `gwp_gwppolicewarreasonservice_035` | 目标已被击败，正在押送玩家 / The target has been defeated and is escorting the player | 说法不准：条件是玩家本人被灰袍打败、正被押往城堡（`IsEscortingPlayer`），中文读成目标押送玩家，英文读成目标在护送玩家 | 你已被击败，正被押往城堡 | You have been defeated and are being taken to a castle |
| `gwp_gwppolicewarreasonservice_036` | 已正式宣战并持续追击 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_037` | 已立案追踪，尚未升级为正式战争 / A case has been filed for tracking, but it has not been upgraded to a formal war. | 「升级为正式战争」开发腔；英文冗长 | 已立案追踪，尚未宣战 | Case opened and tracked; no war declared yet |
| `gwp_gwppolicewarreasonservice_038` | 未记录 | 无 | 保留 | 保留 |
| `gwp_gwppolicewarreasonservice_042` | {VAR_1}附近 ({VAR_2}, {VAR_3}) | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_043` | 野外 ({VAR_1}, {VAR_2}) | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_047` | 春 | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_048` | 夏 | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_049` | 秋 | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_050` | 冬 | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_051` | 未知季 | 不可达 | — | — |
| `gwp_gwppolicewarreasonservice_052` | {VAR_1}年{VAR_2}季第{VAR_3}天 {VAR_4}:00 | 不可达 | — | — |

## 代码问题（不在本稿改）

1. **两个死方法**：`GwpPoliceWarReasonService.FormatLocation` 与 `FormatCampaignDate` 无人调用，连同 `_042`、`_043`、`_047`–`_052` 八条文字都是死代码。`Check-CodeHygiene` 报「0 个声明无人引用」，没查出这两个私有静态方法，可能是检查范围漏了。按「没人用的常量和成员当场删」规则应删除，属代码改动，另批。

## 本稿统计

共 49 条：不可达 8 条（不审），实审 41 条。建议改动 8 条（中英都改 6、只改英文 2），保留 33 条。

- 说法不准 1 条：`_035`（被押送的是玩家本人）。
- 开发用语 4 条：「玩家悬赏协同」「玩家案件执法」「护送玩家」「升级为正式战争」。
- 英文 “warrant / contract” 2 条；调停提示没用上势力名 1 条（只改文字即可，代码已传变量）。
- 代码问题 1 处（两个死方法）。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；`gwp_warden_mediation_owed` 中英同时加入 `{VAR_1}`，变量集合一致，不改代码。
