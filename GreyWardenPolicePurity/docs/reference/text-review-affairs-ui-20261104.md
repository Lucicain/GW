# 灰袍事务界面与案件台账

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为与显示规则见[事务界面](../state/affairs-interface.md)。分组见[清单](text-review-inventory-20261104.md)第 6 批；当前委托状态、宣战理由里的委托说明在[第 5 批](text-review-bounty-membership-20261104.md)，使者派遣的界面文字在第 7 批。

本批几乎都是界面标签和状态短语，按用户 2026-10-18 的裁定核对：「界面文字只写玩家要用的状态、地点、人数、金额和操作。不写解释规则或系统行为的句子，不写自明的状态说明。」没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 五个页签：我的事务、灰袍成员、职能事务、教务、军械库（`GwpAffairsVM`）。教务页的仪式名来自 `GwpAffairsData`，和村庄菜单（`GwpFireswornMenus`）用的是两组不同的键。
- 宗教稿（用户 2026-10-15 批准）把村庄菜单里的仪式改称「布道集会」「火刑」，教士称「灰袍教士」；事务界面是 10-18 新加的，仍写「集会传教」「烈火责罚」「祭司」。
- 战争理由一栏（`_071`–`_075`）：灰袍为某势力开战时列出每条理由；`GwpPeace.EndPoliceWar` 在最后一条理由了结后讲和，`_075` 说的是真的，但它是解释系统行为的句子。
- `_017` 是取不到玩家名字时的替补；台账在由玩家承办时写玩家名字。

## 待用户定

- `gwp_gwpcasearchivescreen_075`「依据全部消失后即恢复和平。」按 10-18 裁定应删掉（详情为空），这要改一行代码（`GwpCaseArchiveItemVM` 第 33 行不再赋值）；如果想留着，本稿给了一个口语版。
- `gwp_player_armory_rules`「每类按部队编制择优保留，多余或不适用的物品退回背包。」也是规则说明，但它是 2026-10-20 按玩家反馈加的，本稿保留，请确认。

## 一、框架、页签与按钮

[事务全屏界面的页签、侧栏分类、按钮与通用格式。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_line` | {LABEL}：{VALUE} | 无 | 保留 | 保留 |
| `gwp_affairs_amount` | {VALUE} 第纳尔 | 无 | 保留 | 保留 |
| `gwp_affairs_row` | {A}  \|  {B} | 无 | 保留 | 保留 |
| `gwp_affairs_unknown` | 尚不明确 | 无 | 保留 | 保留 |
| `gwp_affairs_children` | 未成年成员 | 无 | 保留 | 保留 |
| `gwp_affairs_processing` | 办理记录 | 无 | 保留 | 保留 |
| `gwp_affairs_incident` | 案情记录 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaclanpagevm_004` | 灰袍事务 | 无 | 保留 | 保留 |
| `gwp_affairs_mine` | 我的事务 | 无 | 保留 | 保留 |
| `gwp_affairs_members` | 灰袍成员 | 无 | 保留 | 保留 |
| `gwp_affairs_duties` | 职能事务 | 无 | 保留 | 保留 |
| `gwp_affairs_armory` | 军械库 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_001` | 灰袍事务 | 无 | 保留 | 保留 |
| `gwp_affairs_standing` | 声望：{VALUE} | 无 | 保留 | 保留 |
| `gwp_affairs_empty` | 当前没有此类事务。 | 无 | 保留 | 保留 |
| `gwp_player_armory_summary` | 编制：{CAPACITY}人 \| 军械：{COUNT}件 | 无 | 保留 | 保留 |
| `gwp_player_armory_empty` | 军械库为空。 | 无 | 保留 | 保留 |
| `gwp_affairs_fief_empty` | 暂无在办重议或在途封地申请使者。 | 无 | 保留 | 保留 |
| `gwp_player_armory_locked` | 入会且灰袍声望达到{STANDING}后，可在战斗外整理军械。 | 无 | 保留 | 保留 |
| `gwp_player_armory_manage` | 整理军械 | 无 | 保留 | 保留 |
| `gwp_affairs_browse` | 领取委托 | 无 | 保留 | 保留 |
| `gwp_affairs_accept` | 接受委托 | 无 | 保留 | 保留 |
| `gwp_affairs_view_hero` | 查看人物 | 无 | 保留 | 保留 |
| `gwp_affairs_view_place` | 查看地点 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_003` | 刷新 | 无 | 保留 | 保留 |
| `gwp_affairs_done` | 完成 | 无 | 保留 | 保留 |
| `gwp_case_custody_withdraw` | 无责撤回委托 | 无 | 保留 | 保留 |
| `gwp_case_custody_withdraw_prompt` | 目标被俘、逃亡或暂时无法带队。无责撤回委托，将未结案件退回案件池？ / The target is captive, in hiding, or waiting to form a new party. Withdraw this commission without penalty and return the unresolved case to the pool? | 「退回案件池」是开发用语 | 目标被俘、逃亡或暂时没有带队。撤回这份委托，不受责罚？他的案子灰袍照旧记着。 | The target is captive, on the run, or has no party yet. Withdraw this commission without penalty? The Wardens keep the case open. |
| `gwp_case_custody_leave` | 暂且不谈了。 | 无 | 保留 | 保留 |
| `gwp_affairs_current` | 当前委托 | 无 | 保留 | 保留 |
| `gwp_affairs_orders` | 调兵订单 | 无 | 保留 | 保留 |
| `gwp_affairs_mediation` | 战后讲和 | 无 | 保留 | 保留 |
| `gwp_affairs_petitions` | 封地重议 | 无 | 保留 | 保留 |
| `gwp_affairs_religion` | 村落教务与物资 | 无 | 保留 | 保留 |
| `gwp_affairs_organization` | 组织概况 | 无 | 保留 | 保留 |
| `gwp_affairs_current_location` | 当前所在 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_056` | 司法公库：{VAR_1} 第纳尔 | 无 | 保留 | 保留 |
| `gwp_affairs_family` | 家族与收养 | 无 | 保留 | 保留 |
| `gwp_affairs_send_report` | 派使者复命 | 无 | 保留 | 保留 |
| `gwp_affairs_send_support` | 派使者求援 | 无 | 保留 | 保留 |
| `gwp_affairs_send_order` | 派使者订兵 | 无 | 保留 | 保留 |
| `gwp_affairs_send_peace` | 派使者请求讲和 | 无 | 保留 | 保留 |
| `gwp_affairs_send_fief` | 派使者申请重议 | 无 | 保留 | 保留 |
| `gwp_player_armory_rules` | 每类按部队编制择优保留，多余或不适用的物品退回背包。 | 无 | 保留 | 保留 |
| `gwp_affairs_work` | 当前工作 | 无 | 保留 | 保留 |
| `gwp_affairs_queued_work` | 待办事项 | 无 | 保留 | 保留 |
| `gwp_affairs_negotiations` | 近期交涉回报 | 无 | 保留 | 保留 |
| `gwp_affairs_refresh_changed` | 案件或资格已变化，请查看刷新后的详情。 | 无 | 保留 | 保留 |
| `gwp_affairs_standing_gate` | 宣誓入会且灰袍声望达到 {STANDING} 后可查看。 | 见[核实稿](text-review-term-check-20261104.md#gwp_affairs_standing_gate) | 见核实稿 | 见核实稿 |

## 二、案件详情

[选中一份案件时右侧详情；目标所在地、强度、罚金上限与办案费。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_location` | 目标现所在地 | 无 | 保留 | 保留 |
| `gwp_affairs_near` | {PLACE}附近 | 无 | 保留 | 保留 |
| `gwp_affairs_reward` | 预计办案费 | 无 | 保留 | 保留 |
| `gwp_affairs_fine` | 应缴罚金上限 | 无 | 保留 | 保留 |
| `gwp_affairs_formation` | 所在编组 | 无 | 保留 | 保留 |
| `gwp_affairs_army` | 随军团行动 | 无 | 保留 | 保留 |
| `gwp_affairs_party` | 独立部队 | 无 | 保留 | 保留 |
| `gwp_affairs_men` | 目标本队兵员 | 无 | 保留 | 保留 |
| `gwp_affairs_numbers` | {TOTAL} 人，健康 {HEALTHY} 人 | 无 | 保留 | 保留 |
| `gwp_affairs_strength` | 目标战斗组战力 | 无 | 保留 | 保留 |
| `gwp_affairs_own_strength` | 你的战斗组战力 | 无 | 保留 | 保留 |
| `gwp_affairs_sheltered` | 藏身于{PLACE}的领主大厅 | 无 | 保留 | 保留 |
| `gwp_affairs_unavailable` | 当前无法追查 | 无 | 保留 | 保留 |

## 三、教务与军械库

[教务页与「我的军械库」。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_player_armory` | 我的军械库 | 无 | 保留 | 保留 |
| `gwp_player_armory_count` | 库存：{COUNT} | 无 | 保留 | 保留 |
| `gwp_affairs_faith` | 教务 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_none` | 暂无仪式 | 无 | 保留 | 保留 |
| `gwp_affairs_stores` | 库房与外出队伍 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_priest` | 祭司 / Priest | 宗教稿已把「祭司」改称「教士」 | 教士 | 保留 |
| `gwp_affairs_faith_homes` | 信教人家 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_share` | 信众比例 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_guard` | 在村守兵 | 无 | 保留 | 保留 |
| `gwp_affairs_congregation` | 集会传教 / Congregation | 与已批准的村庄菜单仪式名不一致（宗教稿：「布道集会」） | 布道集会 | 保留 |
| `gwp_affairs_damnation` | 烈火责罚 / Fiery punishment | 与已批准的村庄菜单仪式名不一致（宗教稿：「火刑」 / “Burning at the Stake”） | 火刑 | Burning at the Stake |
| `gwp_affairs_faith_ritual` | 当前仪式 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_end` | 剩余约 {HOURS} 小时 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_dungeon` | 地牢人数 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_armory` | 村庄军械件数 | 无 | 保留 | 保留 |
| `gwp_affairs_faith_goods` | 货仓作价 | 无 | 保留 | 保留 |

## 四、案件台账（成员、职能、当前委托、订单、封地、战争理由）

[`GwpCaseArchiveItemVM` 把各类事项写成列表条目与详情；使者进度与回报也在这里。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwpcasearchivescreen_071` | 对外战争 —— {VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_072` | 未具名势力 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_073` | 开战依据 {VAR_1}/{VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_074` | 开战依据 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_075` | 依据全部消失后即恢复和平。 / Peace returns once no ground remains. | 解释系统行为的句子（用户裁定 2026-10-18：界面不写解释规则的句子）；见「待用户定」 | 这些理由都了结后，灰袍就会与对方讲和。 | Once all these grounds are settled, the Wardens make peace. |
| `gwp_gwpcasearchivescreen_006` | 未结案 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_008` | {VAR_1} — {VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_009` | 最近案发：{VAR_1} \| 立案时间：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_010` | 未记录 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_011` | 未记录 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_012` | 罪行：{VAR_1} \| 受害者：{VAR_2} \| 地点：{VAR_3} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_048` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_047` | 未知灰袍队伍 | 无 | 保留 | 保留 |
| `gwp_envoy_report_header` | 使者回报：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_envoy_report_time` | 回报时间：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_envoy_report_warden` | 派出：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_grace` | 交涉达成：同意宽限缴款。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_duel_called` | 交涉达成：约在{VAR_1}比武。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_paid` | 交涉达成：已缴罚金{VAR_1}第纳尔，结案。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_paid_share` | 交涉达成：缴纳{VAR_1}第纳尔后结案。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_surrender` | 交涉达成：对方已投案。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_bribed` | 交涉结束：承办人私收{VAR_1}第纳尔后销案。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_duel_won` | 依约结案：灰袍比武获胜，对方缴纳{VAR_1}第纳尔。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_duel_lost` | 依约结案：对方比武获胜。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_refused` | 交涉未成：对方拒绝谈判。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_talks_failed` | 交涉未成：未能达成协议。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_grace_broke` | 交涉未成：对方未按期缴款。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_lost` | 交涉未成：使者途中失踪。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_war` | 交涉未成：双方已开战。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_duel_cancelled` | 交涉未成：比武约定作废。 | 无 | 保留 | 保留 |
| `gwp_envoy_outcome_failed` | 交涉未成。 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_032` | 协力任务 — {VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_033` | 分配时间：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_034` | 协办：{VAR_1} \| 主办：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_035` | 协办案件：{VAR_1}，{VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_036` | 村庄善后 — {VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_037` | 登记时间：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_038` | 承办：{VAR_1} \| {VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_039` | 待分配 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_040` | 善后剩余：{VAR_1}小时 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_041` | 村庄善后 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_049` | 村庄重建 — {VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_050` | 承办：{VAR_1}（{VAR_2}） \| {VAR_3} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_051` | 等待重建官 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_052` | 暂无重建官 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_053` | 剩余：{VAR_1}小时 \| 预计拨款：{VAR_2} \| 公库留存：{VAR_3} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_054` | 预计拨款：{VAR_1} \| 公库留存：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_issue_ledger_header` | 请愿：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_issue_ledger_assignment` | 承办：{VAR_1}（{VAR_2}） \| {VAR_3} | 无 | 保留 | 保留 |
| `gwp_issue_ledger_waiting` | 待分配 | 无 | 保留 | 保留 |
| `gwp_issue_ledger_review` | 请愿人：{VAR_1} \| 地点：{VAR_2} \| 剩余：{VAR_3}小时 | 无 | 保留 | 保留 |
| `gwp_issue_ledger_detail` | 请愿人：{VAR_1} \| 地点：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_training_ledger_settle` | 前往{VAR_1}驻防 | 无 | 保留 | 保留 |
| `gwp_player_request_unknown_liaison` | 未知的贵族事务协调官 | 无 | 保留 | 保留 |
| `gwp_player_request_ledger_header` | 玩家封地申诉 — {VAR_1} / Player fief appeal — {VAR_1} | 「玩家」是开发用语，界面上应写「你」 | 你的封地申诉 — {VAR_1} | Your fief appeal — {VAR_1} |
| `gwp_player_request_ledger_assignment` | 协调官：{VAR_1} \| 当前阶段：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_player_request_stage_deferred` | 请愿已搁置 | 无 | 保留 | 保留 |
| `gwp_player_request_ledger_lobbying` | 任务类型：玩家封地申诉 \| 费用：{VAR_1} \| 民间支持：{VAR_2}% \| 剩余驻留：{VAR_3}小时 / Task type: player fief appeal \| Fee: {VAR_1} \| Public support: {VAR_2}% \| Remaining stay: {VAR_3} hours | 「玩家」是开发用语，界面上应写「你」；「任务类型」与标题重复 | 费用：{VAR_1} \| 民间支持：{VAR_2}% \| 剩余驻留：{VAR_3}小时 | Fee: {VAR_1} \| Public support: {VAR_2}% \| Remaining stay: {VAR_3} hours |
| `gwp_player_request_fee_paid` | 已交入公库 | 无 | 保留 | 保留 |
| `gwp_player_request_fee_unpaid` | 尚未收取 | 无 | 保留 | 保留 |
| `gwp_player_request_ledger_detail` | 任务类型：玩家封地申诉 \| 费用：{VAR_1} \| 民间支持：{VAR_2}% / Task type: player fief appeal \| Fee: {VAR_1} \| Public support: {VAR_2}% | 「玩家」是开发用语，界面上应写「你」；「任务类型」与标题重复 | 费用：{VAR_1} \| 民间支持：{VAR_2}% | Fee: {VAR_1} \| Public support: {VAR_2}% |
| `gwp_player_troop_ledger_header` | 调兵：{VAR_1}名{VAR_2} | 无 | 保留 | 保留 |
| `gwp_player_troop_ledger_assignment` | 练兵官：{VAR_1} \| {VAR_2} | 无 | 保留 | 保留 |
| `gwp_player_order_wait_support` | 等待支援结束 | 无 | 保留 | 保留 |
| `gwp_player_order_wait_case` | 等待执法任务结束 | 无 | 保留 | 保留 |
| `gwp_player_order_wait_duel` | 等待比武结束 | 无 | 保留 | 保留 |
| `gwp_player_troop_ledger_detail` | 已备：{VAR_1}/{VAR_2} \| 已付：{VAR_3} 第纳尔 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_017` | 玩家 / Player | 「玩家」是开发用语，界面上应写「你」（取不到玩家名字时的替补） | 你 | You |
| `gwp_case_owner_player` | 追踪情况：{VAR_1} \| 当前阶段：已交由玩家承办 / Tracking: {VAR_1} \| Stage: commissioned to the player | 「玩家」是开发用语，界面上应写「你」 | 追踪情况：{VAR_1} \| 当前阶段：由你承办 | Tracking: {VAR_1} \| Stage: commissioned to you |
| `gwp_case_owner_player_supported` | 追踪情况：{VAR_1} \| 支援：{VAR_2} \| 当前阶段：{VAR_3} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_013` | 待分配 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_015` | {VAR_1}（{VAR_2}） | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_016` | 承办：{VAR_1} \| {VAR_2} | 无 | 保留 | 保留 |
| `gwp_case_helper_paused` | {VAR_1} \| 暂停：正在协助{VAR_2} | 无 | 保留 | 保留 |
| `gwp_case_envoy_collecting` | 宽限到期，去收罚金 | 无 | 保留 | 保留 |
| `gwp_case_envoy_travelling` | 出使中 | 无 | 保留 | 保留 |
| `gwp_case_envoy_failed` | 交涉未成 | 无 | 保留 | 保留 |
| `gwp_case_envoy_grace` | 已同意宽限至{VAR_1} | 无 | 保留 | 保留 |
| `gwp_case_envoy_duel` | 已约定在{VAR_1}比武 | 无 | 保留 | 保留 |
| `gwp_case_envoy_duel_waiting` | 比武已约定，等待赴会 | 无 | 保留 | 保留 |
| `gwp_case_envoy_suffix` | {VAR_1} \| 使者：{VAR_2} | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_018` | 支援受托的玩家 / supporting the commissioned player | 「玩家」是开发用语，界面上应写「你」 | 支援你办案 | supporting your case |
| `gwp_gwpcasearchivescreen_019` | 抓捕后护送 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_020` | 准备出动 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_021` | 战时追捕 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_022` | 追踪目标 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_042` | 等待分配 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_043` | 暂缓，先履行已有约定 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_044` | 前往村庄 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_045` | 驻村善后 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_046` | 未知 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_055` | 驻村重建中 | 无 | 保留 | 保留 |
| `gwp_issue_stage_travel` | 前往任务发布人 / travelling to the issuer | 「任务发布人」是原版任务系统的开发用语，这里是请愿人 | 前往请愿人处 | travelling to the petitioner |
| `gwp_issue_stage_review` | 驻留审理六小时 / reviewing for six hours | 状态名里写死「六小时」，剩余时间另一栏已显示 | 驻留审理 | reviewing the petition |
| `gwp_training_stage_settle` | 回村落脚 | 无 | 保留 | 保留 |
| `gwp_player_request_stage_payment` | 正在前往玩家身边收款 / travelling to collect payment | 「玩家」是开发用语，界面上应写「你」 | 正前来向你收款 | coming to you to collect payment |
| `gwp_player_request_stage_travel` | 正在前往争议封地 | 无 | 保留 | 保留 |
| `gwp_player_request_stage_lobbying` | 正在组织民间请愿 | 无 | 保留 | 保留 |
| `gwp_player_request_stage_vote` | 新的封地投票已经开启 | 无 | 保留 | 保留 |
| `gwp_player_troop_stage_training` | 训练中 | 无 | 保留 | 保留 |
| `gwp_player_troop_stage_delivery` | 正在送兵 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_023` | 春季 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_024` | 夏季 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_025` | 秋季 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_026` | 冬季 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_027` | 未知季节 | 无 | 保留 | 保留 |
| `gwp_gwpcasearchivescreen_028` | {VAR_1}年{VAR_2}第{VAR_3}日 {VAR_4}:00 | 无 | 保留 | 保留 |

## 五、地图名牌与使者往返

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_target_nameplate` | 【办案目标】{NAME} | 无 | 保留 | 保留 |
| `gwp_affairs_outbound` | 正在递送 | 无 | 保留 | 保留 |
| `gwp_affairs_returning` | 正在归队 | 无 | 保留 | 保留 |

## 本稿统计

共 188 条：见核实稿 1 条，实审 187 条。建议改动 14 条（中英都改 12、只改中文 2），保留 173 条。

- 与已批准宗教文本不一致 3 条：`gwp_affairs_congregation`、`gwp_affairs_damnation`、`gwp_affairs_faith_priest`。
- 开发用语 9 条：「玩家」7 条、「案件池」1 条、「任务发布人」1 条。
- 状态名写死时长 1 条（`gwp_issue_stage_review`）；解释系统行为 1 条（`_075`，待定）。
- 没有发现编造，没有发现新的代码问题（`_075` 若删需改一行代码）。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变。`_075` 若选删除，另走代码改动。
