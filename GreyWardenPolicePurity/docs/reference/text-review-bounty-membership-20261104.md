# 悬赏委托、入会退会与执勤旗

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[玩家声望、入会与悬赏](../state/player-justice.md)、[玩家承办委托](../state/player-commission.md)、[装备](../state/warden-gear.md)。分组见[清单](text-review-inventory-20261104.md)第 5 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 入会：声望 ≥ 20 且从没被招募过时派招募使者；首次接受发黑袍指挥官全套、盾、双刀、执勤旗和 5 名新兵；重入只发执勤旗。退会收回全部执勤旗；自愿退会满 3 次永久关闭；重入门槛 20 / 40 / 60 递增。
- 执勤旗装在旗帜槽时，玩家家族、地图标记和战场显示灰袍黑金鸟徽（[装备](../state/warden-gear.md)），旗帜是接新委托的开关；卸下不影响已接的委托。
- 调停申请（替灰袍打出来的战争）在与灰袍领主的外交对话里随时可提，不要求入会，也不要求先交差（[调停](../state/warden-mediation.md)）。
- 退会会失去的：接委托、调兵（`CanFileTroopOrder` 要求入会）、战场援军（`GwpBattleReinforcementBehavior` 要求入会）。
- 「受托猎手」在游戏里不是任何身份或称号，只出现在这两句里。
- `gwp_playerbountybehavior_encyclopedia_*` 名字像百科，实际由 `GwpPoliceWarReasonService` 收进灰袍事务的宣战理由一栏。

## 用户 2026-10-17 的要求与本稿的处理

player-text 记着用户 2026-10-17 要求入会、补旗、重入的说明「明确旗帜槽装备启用、背包停用、卸旗不取消已有委托、退出收旗及重入不再发整套装备」。本稿保留这些内容，只把它们改成领主当面说的话；唯一拿掉的是领主念出「地图左下角的“灰袍事务”」这个界面位置，以及退会提示里的声望数字（2026-10-26 硬要求：不让角色念机制和阈值）。如果仍要保留界面位置或数字，请在批复里说明。

## 一、招募使者找上门，玩家入会

[声望 ≥ 20 且从没被招募过，灰袍从最近的城镇派一支招募使者队来找玩家（地图提示 `_002`）。见面后使者开口，玩家接受或拒绝；接受后领主交装备并说明执勤旗。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbountybehavior_001` | 灰袍招募使者 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_002` | 灰袍的招募使者正从{VAR_1}前来见你。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_009` | 灰袍听说了你的事迹。愿不愿意替我们追捕罪犯？办妥了，自有酬劳。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_001` | 我接受，愿为灰袍守卫效力。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_002` | 收下这套甲胄与执勤旗。装备旗帜，我们就知道你愿意接新委托；卸下便暂停。收来的罚金或捉到的罪犯，仍交给灰袍领主。 / Take this harness and duty standard. Equip the standard when you are ready for new warrants; unequip it to pause. Bring collected fines or prisoners to a Warden-lord. | 英文 “warrant”，游戏里没有通缉令 | 保留 | Take this harness and the duty standard. Carry the standard and we'll know you're ready for new commissions; take it down to stop. Bring any fines you collect, or prisoners you take, to a Warden lord. |
| `gwp_playerbountybehavior_dialogueandnotification_003` | 若因此惹上战事，交差后我们会出面调停。 /  If the pursuit brings war upon you, we will speak for you when the matter is settled. | 说法不准：调停申请随时可以找灰袍领主提，不必等交差；英文开头多一个空格是为了接在上句后面，保留 | 若因此惹上战事，来找我们，我们会出面替你讲和。 |  If the pursuit drags you into a war, come to us and we will make peace for you. |
| `gwp_playerbountybehavior_dialogueandnotification_004` | 不，我不感兴趣。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_005` | 随你。这名使者不会再来打扰；以后若改变主意，可以亲自找一位灰袍领主谈。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_014` | 你已加入灰袍，装备与执勤旗已交付。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_015` | 你回绝了灰袍的招募。日后可以向灰袍领主请求加入。 | 无 | 保留 | 保留 |
| `gwp_join_recruits_granted` | {VAR_1} 名灰袍新兵加入了你的队伍。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_006` | 我们的事务已了结，请继续前行。 | 无 | 保留 | 保留 |

## 二、补领执勤旗、重新入会、退会

[都在和普通灰袍领主的对话里。补领：已入会、没装旗、行囊里也没有旗时出现。退会：自愿退会满 3 次永久关闭，最后一次用 `_final`。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_duty_banner_request` | 我需要补领一面执勤旗。 | 无 | 保留 | 保留 |
| `gwp_duty_banner_issued` | 旗已交给你。装备后以灰袍黑金鸟徽执勤，可接新委托；留在行囊里便是暂不执勤。退出修会时须交还。 / Here is your standard. Equip it to serve under the Grey Wardens’ black and gold bird emblem and receive new warrants; keep it in your baggage when off duty. Return it when you leave the order. | 英文 “warrants”；「以……执勤」生硬 | 旗给你。把它扛起来，你的队伍就打灰袍的黑金鸟徽，可以接新委托；收进行囊就是暂不执勤。哪天退出修会，旗要交回来。 | Here is your standard. Carry it and your company flies the Grey Wardens' black and gold bird, and you can take new commissions; keep it in your baggage when you're off duty. If you ever leave the order, the standard comes back to us. |
| `gwp_membership_rejoin_player` | 我想再次申请成为灰袍受托猎手。 / I wish to petition for a place among the Grey Warden sworn hunters again. | 「受托猎手」不是游戏里的身份 | 我想重新加入灰袍，替你们办案。 | I want to rejoin the Grey Wardens and take cases for you again. |
| `gwp_membership_rejoin_response` | 你的申请获得批准。执勤旗已交给你，原来的装备继续使用。装备旗帜便可接新委托，卸下则暂停；也可从地图左下角的“灰袍事务”领取委托。 / Your petition is accepted. Take your duty standard and keep using your existing equipment. Equip the standard for new warrants, or unequip it to pause. You can also choose a warrant through Grey Warden Affairs at the bottom left of the map. | 领主念界面位置（「地图左下角的“灰袍事务”」）；英文 “warrant” | 好，你回来了。执勤旗给你，原来的装备照旧用。扛起旗就能接新委托，收起来就暂停。想挑案子，就看看灰袍的事务。 | Very well, you're back. Here is your duty standard; keep using your old equipment. Carry the standard to take new commissions, put it away to pause. If you want to choose a case, look through the Grey Wardens' affairs. |
| `gwp_membership_leave_player` | 我想退出灰袍，不再担任受托猎手。 / I wish to relinquish my place with the Grey Wardens. | 「受托猎手」不是游戏里的身份 | 我想退出灰袍，不再替你们办案。 | I want to leave the Grey Wardens and stop taking cases for you. |
| `gwp_membership_leave_prompt_higher` | 退出后，你将失去通缉任务、兵员征调与战场支援。再次加入需要达到 {VAR_1} 声望，并亲自向灰袍领主申请。你确定这是最后决定吗？ 退出时收回执勤旗。 / If you withdraw, your warrants, detachments, and battlefield relief end. Readmission will require {VAR_1} standing and a personal appeal to a Warden-lord. Is this your final decision? Your duty standard will be returned when you leave. | 说法不准：「通缉任务」实为委托；领主念出声望门槛；句末「 退出时收回执勤旗。」前多一个空格 | 你一走，委托、调兵和战场上的援手就都断了。想再回来，得先在灰袍这儿攒下更好的名声，再亲自来找我们。你想好了？退出时把执勤旗交回来。 | If you leave, the commissions, the troops we lend and our help in battle all end. To come back you'll need a better name with the Wardens, and you'll have to ask us yourself. Are you certain? Hand back the duty standard when you go. |
| `gwp_membership_leave_prompt_final` | 这次退出后，你的名字将被永久从名册上划去，任何灰袍领主都不会再接纳你。你确定这是最后决定吗？ 退出时收回执勤旗。 / If you withdraw this time, your name will be struck permanently and no Warden-lord will readmit you. Is this your final decision? Your duty standard will be returned when you leave. | 句末「 退出时收回执勤旗。」前多一个空格，读起来是拼接的 | 这回退出，你的名字就从名册上永远划掉，再没有哪位灰袍领主会收你。你想好了？退出时把执勤旗交回来。 | If you leave this time, your name is struck for good and no Warden lord will take you back. Are you certain? Hand back the duty standard when you go. |
| `gwp_membership_leave_confirm` | 确定。把我的名字从名册上划掉吧。 | 无 | 保留 | 保留 |
| `gwp_membership_leave_done` | 已经办妥。此前交给你的装备仍归你所有，但它不再代表任何灰袍权限。 / It is done. The equipment already entrusted to you remains yours, but it no longer grants any Grey Warden authority. | 「灰袍权限」是开发用语 | 办好了。给你的甲胄你留着，可它不再代表灰袍了。 | It's done. Keep the armour we gave you, but it no longer speaks for the Grey Wardens. |
| `gwp_membership_leave_cancel` | 不，我继续留下。 | 无 | 保留 | 保留 |
| `gwp_membership_leave_cancelled` | 那么你在名册上的位置维持不变。 | 无 | 保留 | 保留 |
| `gwp_bounty_membership_ended` | 你已经退出灰袍，正在执行的悬赏契约随之撤销。 | 无 | 保留 | 保留 |

## 三、接悬赏

[在灰袍事务里选案件并接受。条件：已入会、声望 ≥ 20、旗帜槽装着执勤旗。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_nav_need_membership` | 加入灰袍后才能领取委托。 | 无 | 保留 | 保留 |
| `gwp_bounty_duty_busy` | 已有在办委托 | 无 | 保留 | 保留 |
| `gwp_bounty_duty_requirements` | 需装备执勤旗，且声望达到{STANDING}。 | 无 | 保留 | 保留 |
| `gwp_bounty_duty_ready` | 可领取委托 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_012` | 眼下没有可接的悬赏。 | 无 | 保留 | 保留 |
| `gwp_bounty_difficulty_easy` | 较简单 | 无 | 保留 | 保留 |
| `gwp_bounty_difficulty_standard` | 标准 | 无 | 保留 | 保留 |
| `gwp_bounty_difficulty_hard` | 较难 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_dialogueandnotification_030` | 目标已不在，悬赏取消。 | 无 | 保留 | 保留 |
| `gwp_bounty_case_is_yours` | 你接下了这桩悬赏。需要援手时可派人向灰袍求援。 | 无 | 保留 | 保留 |
| `gwp_bounty_contract_accepted` | 你接下了悬赏：{VAR_1}。罚金 {VAR_3} 第纳尔。 | 无 | 保留 | 保留 |

## 四、任务日志与侦情

[接单后的任务日志；每 2 天写一条目标位置情报。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbountybehavior_questandnotification_002` | 灰袍委托：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_questandnotification_001` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_common_unknown_target` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_bounty_quest_target` | 目标：{VAR_1}。评估难度：{VAR_2}。  | 无 | 保留 | 保留 |
| `gwp_bounty_quest_sheltered` | 当前所在：{VAR_1}的领主大厅，没有带兵。  / Current whereabouts: the lord's hall of {VAR_1}; he leads no party.  | 英文写死 “he” | 保留 | Current whereabouts: the lord's hall of {VAR_1}, with no party.  |
| `gwp_playerbountybehavior_dialogueandnotification_034` | 最后目击位置：{VAR_1} 附近。  / Last sighted location: Near {VAR_1}.  | 英文 “Near” 大写 | 保留 | Last sighted: near {VAR_1}.  |
| `gwp_bounty_quest_reward` | 在45天内交涉结案、击溃目标部队或俘获目标。本案应缴罚金为{VAR_1}第纳尔。办妥后向灰袍领主复命，交回代收的罚金或俘虏；办案费另行核算，活捉目标还可领取赎金。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_020` | 未知位置 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_004` | 灰袍侦察队 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_005` | 【侦情】{VAR_1} 的探子来报：目标在 {VAR_2} 附近目击。 / [Investigation] The spies from {VAR_1} came to report: The target was sighted near {VAR_2}. | 英文 “spies … came to report” 生硬 | 保留 | [Investigation] Scouts from {VAR_1} report the target sighted near {VAR_2}. |
| `gwp_bounty_intel_sheltered` | 【侦情】目标仍未带兵，住在{VAR_1}的领主大厅。 | 无 | 保留 | 保留 |
| `gwp_bounty_intel_lost` | 【侦情】探子跟丢了，这一次没有目击可报。 | 无 | 保留 | 保留 |

## 五、灰袍事务里的委托状态

[事务界面当前委托一栏。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_affairs_support` | 办案支援 | 无 | 保留 | 保留 |
| `gwp_affairs_escort` | 支援领主 | 无 | 保留 | 保留 |
| `gwp_affairs_pursuing` | 追踪目标 | 无 | 保留 | 保留 |
| `gwp_affairs_collect` | 待复命 | 无 | 保留 | 保留 |
| `gwp_affairs_deadline` | 委托期限 | 无 | 保留 | 保留 |
| `gwp_case_custody_held` | 由{HOLDER}扣押 | 无 | 保留 | 保留 |
| `gwp_case_custody_help` | 可向扣押者商谈移交，或撤回委托。 | 无 | 保留 | 保留 |
| `gwp_affairs_sheltered_help` | 他没有带兵。到他所在的城镇或城堡，在菜单里选择带兵进城搜捕。 / He leads no party. Go to that town or castle and choose the search from its menu. | 英文写死 “He” | 保留 | They lead no party. Go to that town or castle and choose the search from its menu. |
| `gwp_case_target_missing` | 目标暂时失去踪迹 | 无 | 保留 | 保留 |
| `gwp_case_target_missing_help` | 目标正在逃亡或尚未重新带队。可继续等待，或无责撤回；未结案件仍保留在案件池。 / The target has no active party. Wait for their return, or withdraw without penalty; the unresolved case remains in the pool. | 「案件池」是开发用语 | 目标正在逃，或还没重新带队。可以继续等，也可以撤回委托，不受责罚；他的案子灰袍照旧记着。 | The target is on the run or has not raised a new party. Wait, or withdraw the commission without penalty; the Wardens keep the case open. |

## 六、事务里的宣战理由

[灰袍为玩家的案子开战时，事务界面「战后讲和」一栏的说明，由 `_006` 拼上随行部队名和阶段。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbountybehavior_encyclopedia_006` | 玩家悬赏协同：当前目标是 {VAR_1}，敌对势力为 {VAR_2}。护送部队：{VAR_3}；当前阶段：{VAR_4}。 / Player bounty collaboration: The current target is {VAR_1}, and the hostile forces are {VAR_2}. Escort force: {VAR_3}; current stage: {VAR_4}. | 「玩家悬赏协同」是开发用语；显示在灰袍事务的宣战理由里 | 协助你办案：目标{VAR_1}，对方是{VAR_2}，随行灰袍：{VAR_3}。{VAR_4} | Helping your case: the target is {VAR_1}, the other side is {VAR_2}, Warden escort: {VAR_3}. {VAR_4} |
| `gwp_playerbountybehavior_encyclopedia_001` | 未指定护送部队 / Unspecified escort unit | 「未指定护送部队」公文腔 | 没有随行部队 | no escort |
| `gwp_playerbountybehavior_encyclopedia_002` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_encyclopedia_003` | 目标仍在逃逸，灰袍部队正在护送玩家追捕 / The target is still escaping, and the Grey Wardens troops are escorting the player to pursue | 「护送玩家」开发用语；英文机翻 | 目标还在逃，灰袍正陪你追。 | The target is still on the run, and the Wardens are riding with you. |
| `gwp_playerbountybehavior_encyclopedia_004` | 目标已经被击败，等待玩家领取赏金并由灰袍善后 / The target has been defeated, waiting for the player to receive the bounty and the Grey Wardens will deal with the aftermath | 「等待玩家领取赏金」「由灰袍善后」开发用语；英文机翻 | 目标已被击败，等你回去交差。 | The target has been defeated. Go back and report. |
| `gwp_playerbountybehavior_encyclopedia_005` | 悬赏流程仍在进行 / The bounty process is still in progress | 「悬赏流程」开发用语 | 追捕还在进行。 | The pursuit is still under way. |

## 七、求援

[玩家当面或派人向灰袍领主求援；灰袍为此对罪犯宣战时玩家一并参战。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_request_case_support` | 我需要你们帮忙拿下那个人。跟我一起上。 | 无 | 保留 | 保留 |
| `gwp_request_case_support_reply` | 那就走吧。我们跟着你，动起手来不会落在你后面。 | 无 | 保留 | 保留 |
| `gwp_support_no_responder` | 眼下没有灰袍领主能够前来。 | 无 | 保留 | 保留 |
| `gwp_support_requested_log` | 你已请灰袍出兵相助。 | 无 | 保留 | 保留 |
| `gwp_support_granted_notice` | {VAR_1}已应允，正赶来与你会合。 | 无 | 保留 | 保留 |
| `gwp_support_player_war` | 灰袍向{VAR_1}宣战，你随之参战。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_003` | 随行的灰袍已离开，悬赏仍然有效。 | 无 | 保留 | 保留 |

## 八、办完、期满或撤回

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_defeat_recorded` | 你在野外击溃了{VAR_1}。回去复命吧。 | 无 | 保留 | 保留 |
| `gwp_case_capture_missed` | 目标未被俘获，委托尚未完成。 | 无 | 保留 | 保留 |
| `gwp_case_captive_ready` | 目标已被你俘获。将他押交任意一位灰袍领主即可领取赏金。 / You hold the target captive. Deliver him to any Warden lord to collect the reward. | 英文写死 “him” | 保留 | You hold the target captive. Deliver the prisoner to any Warden lord to collect the reward. |
| `gwp_field_settle_notice_title` | 外勤处理结束，待交付 / Case settled on the road | 「外勤处理」是开发用语；英文另一句 | 案子了结，回去交差 | Case closed, time to report |
| `gwp_field_settle_notice_body` | 追捕已结束。你需要交代这次办案的结果，并上缴作为罚金收到的 {VAR_1} 第纳尔。请找灰袍领主交付；未收足的部分也要说明。 | 无 | 保留 | 保留 |
| `gwp_common_understood` | 知道了 | 无 | 保留 | 保留 |
| `gwp_bounty_ready_for_turnin` | 外勤处理已结束。请将代收罚金或受托追捕的俘虏交给任意灰袍领主；你也可以派自己的人送去。 / The field operation has ended. Deliver the fines or the assigned prisoner to any Grey Warden lord, in person or by your own men. | 「外勤处理」是开发用语 | 追捕已经了结。把代收的罚金或受托追捕的俘虏交给任意一位灰袍领主；也可以派自己的人送去。 | The pursuit is over. Deliver the fines you collected, or the prisoner you were sent for, to any Grey Warden lord, in person or by your own men. |
| `gwp_bounty_contract_expired_but_done` | 追捕期限已过，但目标已受惩处。向任意一位灰袍领主复命即可。 | 无 | 保留 | 保留 |
| `gwp_bounty_contract_timed_out` | 悬赏期限已到，追捕结束。 | 无 | 保留 | 保留 |
| `gwp_bounty_contract_timed_out_log` | 悬赏契约到期时目标仍未被击败，任务失败。 / The bounty contract expired before the quarry was defeated. | 说法不准：谈判、俘获也能完成，不只「击败」；「契约」与别处「委托/悬赏」不一致 | 期限到了，这桩悬赏没能办成。 | The time ran out before the bounty was carried out. |
| `gwp_case_closed_elsewhere` | 关于{VAR_1}的委托已撤回。请向灰袍领主复命，或派使者代为回报。 | 无 | 保留 | 保留 |

## 九、第三方扣押：赎买接管

[目标被别的领主俘虏时，玩家去找扣押者。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_custody_request` | 你押着的{NAME}是我正在追查的案犯。我想接管此人。 | 无 | 保留 | 保留 |
| `gwp_case_custody_offer` | 人可以交给你，先谈谈价钱。 | 无 | 保留 | 保留 |
| `gwp_case_custody_buy` | 谈谈赎金。 | 无 | 保留 | 保留 |
| `gwp_case_custody_done` | 成交。人归你押送。 | 无 | 保留 | 保留 |
| `gwp_case_custody_declined` | 这笔交易没有谈成。 | 无 | 保留 | 保留 |
| `gwp_case_custody_withdrawn` | 委托已撤回。案犯仍将被追责。 / The commission has been withdrawn. The offender remains wanted. | 英文 “wanted”：NPC 有案，不是被通缉 | 保留 | The commission has been withdrawn. The offender's case remains open. |

## 十、交差

[与任意灰袍领主「我来交代」；走原版交易界面上交钱物或俘虏。交得比从罪犯那里收到的少时，领主追问，玩家可以认或瞒（瞒的会按概率被查账，见第 3 批）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_case_report_open` | 我来交代上次受托办的案子。 | 无 | 保留 | 保留 |
| `gwp_case_report_summary` | 本案罚金为{VAR_1}第纳尔。请交回代收的财物或押来的犯人，并说明办案经过。 | 无 | 保留 | 保留 |
| `gwp_case_pay_any` | 我来交差，请清点结账。 | 无 | 保留 | 保留 |
| `gwp_case_resume_report` | 钱物已经交过了，我来说明办案经过。 | 无 | 保留 | 保留 |
| `gwp_case_report_later` | 钱和人还没有准备好，我改日再来。 | 无 | 保留 | 保留 |
| `gwp_case_settle_expenses` | 这桩委托已经了结，把该付你的报酬结清就行。 | 无 | 保留 | 保留 |
| `gwp_case_report_withdrawn_summary` | 这份委托已经撤回，我来结清你的辛苦费。 | 无 | 保留 | 保留 |
| `gwp_case_barter_count` | 让我清点一下你带来的东西和人。 | 无 | 保留 | 保留 |
| `gwp_receipt_unknown` | 请选择要上交的钱物。 | 无 | 保留 | 保留 |
| `gwp_case_barter_accepted` | 钱物清点好了，这份委托可以结了。 / We have counted the payment. Your report will be entered. | 英文与中文不对应 | 保留 | The payment is counted. This commission can be closed. |
| `gwp_case_barter_cancelled` | 这次没有入账。你准备好了，我们再点。 | 无 | 保留 | 保留 |
| `gwp_case_nothing_concealed` | 他交到你手上的就是这些，一分不少地都在这儿了。你的汇报会照实登记。 | 无 | 保留 | 保留 |
| `gwp_case_short_question` | 这宗案子还有需要交代的地方。说说经过。 | 无 | 保留 | 保留 |
| `gwp_case_short_truth` | 如实交代 / Tell the truth | 选项写成「如实交代」，玩家看到的是界面标签不是自己的话 | 我收的不止这些，差的那部分我认。 | I collected more than this. I'll answer for the difference. |
| `gwp_case_short_lie` | 撒谎 / Lie | 同上，选项直接写「撒谎」 | 他就交了这么多，我全带来了。 | This is all they paid. I've brought every coin. |
| `gwp_case_report_acknowledge` | 把结清的账目给我吧。 | 无 | 保留 | 保留 |
| `gwp_case_report_kept_result` | 这 {VAR_1} 第纳尔已经登记。你的办案费是 {VAR_2} 第纳尔。委托就此结清。 | 无 | 保留 | 保留 |
| `gwp_case_report_complete` | 交付已经记下。公库付给你 {VAR_1} 第纳尔办案酬劳，这份委托结清了。 | 无 | 保留 | 保留 |
| `gwp_case_withdrawn_result` | 这份委托已撤回。公库付给你{VAR_1}第纳尔辛苦费，账目已结清。 | 无 | 保留 | 保留 |
| `gwp_playerbountybehavior_questandnotification_003` | 报告与交付已记入卷宗，这份委托已经结清。 | 无 | 保留 | 保留 |
| `gwp_dispatch_report_done` | 委托已结清，你的队伍正带着 {VAR_1} 第纳尔赏金返回。 | 无 | 保留 | 保留 |

## 本稿统计

共 108 条，全部实审。建议改动 27 条（中英都改 19、只改英文 8），保留 81 条。

- 说法不准 / 编造 5 条：「受托猎手」2 条（游戏里没有这个身份）、`_003`（调停不必等交差）、`gwp_membership_leave_prompt_higher`（「通缉任务」）、`gwp_bounty_contract_timed_out_log`（只说击败）。
- 英文 “warrant” 3 条、英文写死性别 3 条、英文 “wanted” 1 条。
- 开发用语 / 界面标签 10 条：「外勤处理」2、「玩家悬赏协同」一组 5、「案件池」「灰袍权限」、交差时选项直接写「如实交代 / 撒谎」（计 2 条）。
- 领主念界面位置或阈值 2 条；拼接留空格 1 条；英文不对应或生硬 2 条。
- 没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
