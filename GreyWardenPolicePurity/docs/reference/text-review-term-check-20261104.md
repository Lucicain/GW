# 可疑用词逐条核实（印/法令/卷宗/誓/审判/罚金）

2026-11-04 审阅稿，**待用户审批**。本稿只核实与提出改稿，未改 `PlayerText/strings.json`、生成文件或代码。

## 核实范围与方法

- 范围：`PlayerText/strings.json` 当前 1758 条，中文含「印、法令、卷宗、誓、审判、罚金」任一词的共 **91 条**。player-text 已知缺陷里记的是 92 条：差的 1 条是 `gwp_cs_arg_steward`「令状上有灰袍的印」，已在 2026-10-26 进城搜捕批次改掉。
- 方法：每条在源码里找实际引用位置和触发条件，核对这句话说的东西在游戏里有没有。引用分两种：直接引用 `GwpTextCatalog.<键>`，以及 `GwpFieldArrestLines.cs` 按「打算 + 技能」拼出来的 `gwp_terms_*`、`gwp_rebuff_*` 动态键。两种都查不到的算「无引用」。罚金是真实机制：纠察队按负声望每点 200、灰袍领主每点 100、战败押送每点 300 收取，NPC 罪犯按 `GwpFieldArrestPricing.AssessFine` 折算。
- 判定四档：**属实**＝游戏里确实有这回事；**说法不准**＝事情存在，但这句话说错了条件、身份或名称；**编造**＝说到了游戏里根本没有的东西；**无引用 / 不显示**＝玩家看不到，不审措辞。

## 统计

| 判定 | 条数 |
|---|---|
| 属实 | 61 |
| 说法不准 | 8 |
| 编造 | 1 |
| 无引用 | 20 |
| 不显示（默认名，调用方总会覆盖） | 1 |
| 合计 | 91 |

「属实」里有 2 条中英不对应（`gwp_det_ui_ledger_no_case`、`gwp_rebuff_deedsonly_charm`），2 条事实对但写法要改（`gwp_policeenforcementbehavior_dialogue_002`、`gwp_enforcement_companion_paid`），都放在下面或子系统稿里处理。

几个词的结论：

- **印**：2 条都在 AI 震慑台词里。游戏里没有灰袍的印信，`medium_impulsive` 的英文 “seal” 明说是印，判编造；`high_cruel` 的「印记」可以当比喻读，但和前一条同出一处，判说法不准。
- **法令**：灰袍领主和纠察队都说「按灰袍法令」。游戏里没有成文的灰袍法令，已批准文本里灰袍的规矩叫「戒律 / 诫命」。圣铎百科里的「旧帝国的法令」是人物描写，已随宗教稿批准，不改。
- **卷宗**：指灰袍的案件记录，事务界面里确实能看到案件、回执和账目，属实。
- **誓**：集会「新入教的人正在宣誓」属实（集会提示里的立誓户数就是实际新增的信众户）；事务界面「宣誓入会」不准，入会只是对话里答应一句，没有宣誓环节。
- **审判**：只出现在已批准的家族百科总介绍里，属实。
- **罚金**：真实机制。不准的两处是纠察队说「通缉撤销」、罪犯骂玩家「通缉犯」：纠察队只在声望 −1 至 −10 时出动，这时玩家没有被通缉（通缉从 −11 起，由正式灰袍承办）。
- **司法公库**（顺带核实）：家族百科页显示公库余额（`PoliceResourceManager.GetJudicialTreasuryBalance`），真实存在。

## 逐条核实表

| # | 键 | 出现位置 | 命中词 | 判定 | 核实说明 |
|---|---|---|---|---|---|
| 1 | `gwp_clan_description` | `_Module/ModuleData/spclans.xml:18` | 审判 | 属实 | 模组设定；2026-10-15 宗教审阅稿已批准。「审判」指灰袍惩戒罪行，游戏里确有追捕、罚金与押送。 |
| 2 | `gwp_enc_shengduo` | `GreyWardenLoreBehavior.cs:29` | 法令 | 属实 | 人物百科的叙事描写，宗教审阅稿已批准；不指向游戏里的物件或机制。 |
| 3 | `gwp_patrol_barter_name_cn` | `GwpBribeBarterable.cs:33` | 罚金 | 不显示 | `GwpBribeBarterable` 的默认名；两个调用方（纠察队、灰袍领主）都传入了自己的名称，这句实际不出现。 |
| 4 | `gwp_playerbountybehavior_dialogueandnotification_002` | `PlayerBountyBehavior.DialogueAndNotification.cs:49` | 罚金 | 属实 | 入会时确实交付甲胄与执勤旗；旗帜槽装备启用、卸下暂停，按用户 2026-10-17 要求明说。 |
| 5 | `gwp_bounty_ready_for_turnin` | `PlayerBountyBehavior.QuestAndNotification.cs:115` | 罚金 | 属实 | 罚金或俘虏可交任意灰袍领主，也可派人交付（玩家委托：亲自交与派人交相同）。 |
| 6 | `gwp_bounty_choice_label` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 7 | `gwp_bounty_select_description` | — | 卷宗、罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 8 | `gwp_bounty_contract_reward` | — | 卷宗、罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 9 | `gwp_bounty_contract_turnin` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 10 | `gwp_bounty_quest_reward` | `PlayerBountyBehavior.DialogueAndNotification.cs:737` | 罚金 | 属实 | 期限 `DeadlineDays = 45`；活捉另按原版赎金价收购；办案费另算。 |
| 11 | `gwp_bounty_contract_accepted` | `PlayerBountyBehavior.DialogueAndNotification.cs:743` | 罚金 | 属实 | 接悬赏的通知，罚金为该案应缴额。 |
| 12 | `gwp_playerbountybehavior_questandnotification_003` | `PlayerBountyBehavior.QuestAndNotification.cs:102` | 卷宗 | 属实 | 「卷宗」指灰袍的案件记录，事务界面里可见案件与回执。 |
| 13 | `gwp_policeenforcementbehavior_008` | `PoliceEnforcementBehavior.cs:472` | 罚金 | 属实 | 被正式灰袍击败后押到城堡，按每点 300 收现金后释放。未缴足时仍保持通缉，这句没说——见子系统稿。 |
| 14 | `gwp_policeenforcementbehavior_dialogue_001` | `PoliceEnforcementBehavior.Dialogue.cs:65` | 法令、罚金 | 说法不准 | 游戏里没有成文的「灰袍法令」；灰袍的规矩在已批准文本里叫「戒律/诫命」，罚金按罪行折算。改稿见子系统稿。 |
| 15 | `gwp_policeenforcementbehavior_dialogue_002` | `PoliceEnforcementBehavior.Dialogue.cs:83` | 罚金 | 属实 | 缴清后结案；「本轮案件」是开发口吻，见子系统稿。 |
| 16 | `gwp_policeenforcementbehavior_dialogue_003` | `PoliceEnforcementBehavior.Dialogue.cs:92` | 罚金 | 属实 | 出价不足回到选项。 |
| 17 | `gwp_policeenforcementbehavior_dialogue_005` | `PoliceEnforcementBehavior.Dialogue.cs:119` | 罚金 | 属实 | 没有可派的赎罪目标时出现。 |
| 18 | `gwp_policeenforcementbehavior_dialogue_011` | `PoliceEnforcementBehavior.Dialogue.cs:232` | 罚金 | 属实 | 罚金 = 负声望 × 100（`Enforcement.FinePerPoint`）。 |
| 19 | `gwp_policeenforcementbehavior_dialogue_012` | `PoliceEnforcementBehavior.Dialogue.cs:244` | 罚金 | 属实 | 声望 ≤ −11 才由正式灰袍拦截，缴清确实撤销通缉。 |
| 20 | `gwp_policeenforcementbehavior_dialogue_018` | `PoliceEnforcementBehavior.Dialogue.cs:374` | 罚金 | 属实 | 交易界面里的条目名。 |
| 21 | `gwp_policeenforcementbehavior_dialogue_019` | `PoliceEnforcementBehavior.Dialogue.cs:408` | 罚金 | 属实 | 缴清后撤销通缉。 |
| 22 | `gwp_enforcement_companion_charge` | `PoliceEnforcementBehavior.Dialogue.cs:233` | 罚金 | 属实 | 同行同伴有开放案件时灰袍拦下玩家。 |
| 23 | `gwp_enforcement_companion_insufficient` | `PoliceEnforcementBehavior.Dialogue.cs:227` | 罚金 | 属实 | 同伴罚金只能一次缴清。 |
| 24 | `gwp_enforcement_companion_pay` | `PoliceEnforcementBehavior.Dialogue.cs:256`、`PoliceEnforcementBehavior.Dialogue.cs:53` | 罚金 | 属实 | 玩家出钱替同伴结案。 |
| 25 | `gwp_enforcement_companion_paid` | `PoliceEnforcementBehavior.Dialogue.cs:292` | 罚金 | 属实 | 钱由玩家付，文中却写成同伴自己缴清，见子系统稿。 |
| 26 | `gwp_policepatrolbehavior_003` | `PolicePatrolBehavior.cs:161` | 法令、罚金 | 说法不准 | 同 dialogue_001，没有「灰袍法令」。 |
| 27 | `gwp_policepatrolbehavior_004` | `PolicePatrolBehavior.cs:179` | 罚金 | 属实 | 正式缴罚后声望归零；英文是机翻腔。 |
| 28 | `gwp_policepatrolbehavior_005` | `PolicePatrolBehavior.cs:188` | 罚金 | 属实 | 出价不足回到选项。 |
| 29 | `gwp_policepatrolbehavior_015` | `PolicePatrolBehavior.cs:301` | 罚金 | 说法不准 | 罚金属实（负声望 × 200）；但英文 “acts under warrant” 与实际不符：纠察队只在 −1 至 −10、尚未通缉时出动。中文把「负声望 N」念给玩家，见子系统稿。 |
| 30 | `gwp_policepatrolbehavior_016` | `PolicePatrolBehavior.cs:321` | 罚金 | 属实 | 缴清结束这次追捕。 |
| 31 | `gwp_policepatrolbehavior_020` | `PolicePatrolBehavior.cs:369` | 罚金 | 属实 | 交易界面里的条目名。 |
| 32 | `gwp_policepatrolbehavior_022` | `PolicePatrolBehavior.cs:400` | 罚金 | 说法不准 | 纠察队阶段玩家没有被通缉（通缉从 −11 起，由正式灰袍承办），「通缉撤销」不成立。 |
| 33 | `gwp_policepatrolbehavior_041` | `PolicePatrolBehavior.cs:994` | 罚金 | 属实 | 被纠察队击败后在城门按每点 200 收现金放人。 |
| 34 | `gwp_policepatrolbehavior_042` | `PolicePatrolBehavior.cs:1006` | 罚金 | 属实 | 同上，收取金额。 |
| 35 | `gwp_ai_det_audience_warden_high_cruel` | `GwpAiDeterrenceDialogueCatalog.cs:325` | 印 | 说法不准 | 中文「印记」可当比喻读，但同组英文 “seal”（见下一条）说明原意是印信。游戏里没有灰袍的印。 |
| 36 | `gwp_ai_det_audience_warden_medium_impulsive` | `GwpAiDeterrenceDialogueCatalog.cs:311` | 印 | 编造 | 英文 “their seal is behind you” 说玩家身后有灰袍的印信；游戏里没有这种东西，和已改掉的「令状上有灰袍的印」同类。 |
| 37 | `gwp_gwpcasearchivescreen_005` | — | 卷宗 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 38 | `gwp_gwpcasearchivescreen_057` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 39 | `gwp_fieldarrest_charge` | `GwpFieldArrestBehavior.cs:546` | 罚金 | 属实 | 野外拦下罪犯时宣读罪名、受害方与应缴额。 |
| 40 | `gwp_fieldarrest_msg_fine` | `GwpFieldArrestBehavior.cs:979` | 罚金 | 属实 | 收到的钱必须交给灰袍领主。 |
| 41 | `gwp_field_fine_handed_in` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 42 | `gwp_field_settle_notice_body` | `PlayerBountyBehavior.cs:167` | 罚金 | 属实 | 追捕结束后要交代并上缴代收罚金。 |
| 43 | `gwp_field_turnin_lord` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 44 | `gwp_fieldarrest_msg_partial` | `GwpFieldArrestBehavior.cs:980` | 罚金 | 属实 | 部分缴纳，差额记在罪犯名下。 |
| 45 | `gwp_fa_submit_upright` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 46 | `gwp_fa_submit_soft` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 47 | `gwp_fa_arg_price_lead` | `GwpFieldArrestLines.cs:312` | 罚金 | 属实 | 玩家受托收灰袍核定的罚金。 |
| 48 | `gwp_fa_desire_duel` | `GwpOffenderDesire.cs:145` | 罚金 | 属实 | 约斗：玩家赢收罚金，罪犯赢免罚（用户裁定 2026-10-17）。 |
| 49 | `gwp_report_open` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 50 | `gwp_report_ask` | — | 卷宗 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 51 | `gwp_report_no_money_reply` | — | 卷宗 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 52 | `gwp_report_result_short` | — | 卷宗 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 53 | `gwp_report_short` | — | 卷宗 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 54 | `gwp_det_ui_ledger_no_case` | `GwpEncyclopediaHeroPageVM.cs:193` | 罚金 | 属实 | 中文属实：无开放案件但仍有未赎人命时出现；打赢强盗每杀 10 人负声望 −1。英文是另一句话，中英不对应。 |
| 55 | `gwp_case_payment` | `GwpFieldFineBarterable.cs:27`、`GwpFieldFineBarterable.cs:28` | 罚金 | 属实 | 「司法公库」真实存在：家族百科页显示余额（`GetJudicialTreasuryBalance`）。 |
| 56 | `gwp_case_deliver_money` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 57 | `gwp_case_report_summary` | `PlayerBountyBehavior.CaseSettlement.cs:509` | 罚金 | 属实 | 交差时的说明。 |
| 58 | `gwp_case_cash_still_due` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 59 | `gwp_case_purse_short` | `GwpFieldArrestBehavior.cs:381`、`GwpFieldArrestBehavior.cs:393` | 罚金 | 属实 | 钱和物品合计不足时的台词，交易界面确实计入物品。 |
| 60 | `gwp_fa_payment_budget_lead` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 61 | `gwp_fa_payment_terms_lead` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 62 | `gwp_case_pending_encyclopedia` | `GwpEncyclopediaHeroPageVM.cs:177` | 罚金 | 属实 | 百科页显示应缴与已缴。 |
| 63 | `gwp_case_duel_post_win` | `GreyWardenSparringBehavior.cs:88`、`GreyWardenSparringBehavior.cs:89` | 罚金 | 属实 | 约斗胜后查看财物结清。 |
| 64 | `gwp_case_duel_post_loss` | `GreyWardenSparringBehavior.cs:96`、`GreyWardenSparringBehavior.cs:97` | 罚金 | 属实 | 约斗败后当场不收；案底仍在。 |
| 65 | `gwp_fa_desire_merciful` | `GwpOffenderDesire.cs:134` | 罚金 | 属实 | 「全缴求和」打算。 |
| 66 | `gwp_terms_surrenderself_charm` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键（`GwpFieldArrestLines` 拼 `gwp_terms_<打算>_<技能>`），劝想投案的人改为交钱。 |
| 67 | `gwp_terms_bribethewarden_lead` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键；劝想行贿的人照数交罚金。 |
| 68 | `gwp_terms_bribethewarden_rogue` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键；同上。 |
| 69 | `gwp_terms_demandduel_trade` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键；劝想约斗的人交钱。 |
| 70 | `gwp_terms_deedsonly_lead` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键；「杀自己的兵」对应该打算真的会杀 5 名部下。 |
| 71 | `gwp_terms_haggledown_lead` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键；对方开价七成（`HaggleSharePercent = 70`），「打七折」对得上。 |
| 72 | `gwp_rebuff_authority_lead` | `GwpFieldArrestLines.cs:242` 动态拼键 | 誓 | 说法不准 | 动态键（`gwp_rebuff_authority_<技能>`）。罪犯不是被「通缉」，他是有灰袍开的案子；游戏里也没有一纸通缉令。 |
| 73 | `gwp_rebuff_authority_trade` | `GwpFieldArrestLines.cs:242` 动态拼键 | 罚金 | 属实 | 动态键。 |
| 74 | `gwp_rebuff_deedsonly_charm` | `GwpFieldArrestLines.cs:242` 动态拼键 | 罚金 | 属实 | 动态键。中文属实；但代码 `PaymentRebuff` 对每种打算只取 `_lead` 的英文，英文玩家看到的是 lead 那句，中英不对应。 |
| 75 | `gwp_voice_demand_payforpeace_alt` | `GwpFieldDialogueVoice.cs:64` | 罚金 | 属实 | 「全缴求和」的另一说法。 |
| 76 | `gwp_voice_demand_demandduel_alt` | `GwpFieldDialogueVoice.cs:70` | 罚金 | 属实 | 约斗。 |
| 77 | `gwp_fa_desire_grace` | `GwpOffenderDesire.cs:132` | 罚金 | 属实 | 宽限 `GraceHours = 72`，即三天。 |
| 78 | `gwp_voice_grace_alt` | `GwpFieldDialogueVoice.cs:61` | 罚金 | 属实 | 同上。 |
| 79 | `gwp_grace_granted` | `GwpFieldArrestBehavior.Grace.cs:31` | 罚金 | 属实 | 同上。 |
| 80 | `gwp_terms_requestgrace_lead` | `GwpFieldArrestLines.cs` 动态拼键 | 罚金 | 属实 | 动态键。 |
| 81 | `gwp_duel_payment_pending` | `GwpFieldArrestBehavior.cs:1111` | 罚金 | 属实 | 约斗胜后收罚金。 |
| 82 | `gwp_duel_inspect_property` | `GreyWardenSparringBehavior.cs:92`、`GwpFieldArrestBehavior.cs:135` | 罚金 | 属实 | 同上。 |
| 83 | `gwp_duel_return_collect` | `GwpFieldArrestBehavior.cs:134`、`GwpFieldArrestBehavior.cs:447` | 罚金 | 属实 | 约斗赢了但没当场收，回来再收。 |
| 84 | `gwp_voice_plead_tight_0` | `GwpFieldDialogueVoice.cs:124` | 卷宗 | 属实 | 「卷宗」指灰袍案件记录。 |
| 85 | `gwp_voice_resist_scorn_1` | `GwpFieldDialogueVoice.cs:137` | 罚金 | 说法不准 | 条件是玩家灰袍声望 < 0（`WardenStandingTier.UnderCharge`），−1 至 −10 时玩家并未被通缉，「通缉犯」不准。 |
| 86 | `gwp_fs_ritual_congregation_text` | `GwpFireswornMenus.cs:64` | 誓 | 属实 | 集会期间信众户增加，「立誓户数等于实际信众户增量」；宗教审阅稿已批准。 |
| 87 | `gwp_case_envoy_collecting` | `GwpCaseArchiveItemVM.cs:429` | 罚金 | 属实 | 使者宽限到期去收钱。 |
| 88 | `gwp_envoy_outcome_paid` | `GwpCaseArchiveItemVM.cs:85` | 罚金 | 属实 | 使者交涉收款结案。 |
| 89 | `gwp_affairs_fine` | `GwpAffairsData.cs:113` | 罚金 | 属实 | 事务界面显示该案应缴额（`AssessFine`）；办案费按它封顶。 |
| 90 | `gwp_affairs_reward_note` | — | 罚金 | 无引用 | 源码、模块 XML 与动态拼键都查不到引用，玩家看不到；按 player-text 规则保留不删。 |
| 91 | `gwp_affairs_standing_gate` | `GwpAffairsNavigation.cs:45` | 誓 | 说法不准 | 入会只是对话里答一句「我接受，愿为灰袍守卫效力」，没有宣誓环节。 |

## 改稿（不属于纠察队 / 灰袍领主拦截的几条）

`gwp_policeenforcementbehavior_dialogue_001`、`gwp_policepatrolbehavior_003`、`_015`、`_022` 以及 `gwp_enforcement_companion_paid` 的改稿写在[纠察队与灰袍领主拦截审阅稿](text-review-player-enforcement-20261104.md)里，这里不重复。

### `gwp_ai_det_audience_warden_medium_impulsive`

- 出现情境：玩家已入会，去和一位从前被灰袍惩戒过的领主说话。约一半的概率，领主先有一句神态旁白，再说一段话：前半句讲自己当年的事，后半句（就是这条）冲着玩家说。这条在震慑中等、领主性子冲动（算计特质为负）时抽到。
- 现中文：不要因为身后有他们的印记，就让一次冒犯把执法变成报复。
- 现英文：Do not let an insult turn enforcement into vengeance simply because their seal is behind you.
- 问题：编造。游戏里没有灰袍的印信。句子也是格言腔。
- 建议中文：别仗着灰袍给你撑腰，被人顶撞一句就拔刀。
- 建议英文：Don't let the Wardens' backing turn every insult into a reason to draw steel.

### `gwp_ai_det_audience_warden_high_cruel`

- 出现情境：同上，震慑最高一档、领主仁慈特质为负时抽到。前半句可能是他本人的经历，也可能是他族人或同场战败者的经历（见文末附带发现）。
- 现中文：他们的印记留在我的家族身上，而你选择了站在他们一边。别指望我会把其中任何一样当成友谊。
- 现英文：Their mark is on my house, and you have chosen their side. Do not expect me to confuse either with friendship.
- 问题：说法不准。「印记」容易读成实物，而且只说「家族」，前半句是本人经历时对不上。「把其中任何一样当成友谊」是翻译腔。
- 建议中文：灰袍那回做的事我还记着，你倒站到她们那边去了。别指望我拿你当朋友。
- 建议英文：I have not forgotten what the Wardens did, and now you stand with them. Don't expect me to call you a friend.

### `gwp_rebuff_authority_lead`

- 出现情境：玩家受托在野外拦下罪犯。罪犯先质疑「你不是我的主君，也不是灰袍，你是她们雇来的人」，玩家选统御，说「灰袍派我来，我说的就是她们的要求」，没说动时罪犯这样回。
- 现中文：我可没向这份通缉令宣过誓。你得拿出更能让我信服的理由。
- 现英文：Your warrant is not a command I have sworn to obey. Give me a better reason.
- 问题：说法不准。罪犯是有灰袍开的案子，不是被「通缉」；游戏里也没有一纸通缉令。
- 建议中文：我又没向灰袍立过誓，凭什么听她们的？你得拿出更像样的理由。
- 建议英文：I never swore anything to the Wardens. Why should I answer to them? Give me a better reason.

### `gwp_voice_resist_scorn_1`

- 出现情境：玩家自己的灰袍声望为负，却去收罪犯的罚金，罪犯抗法时三句里随机抽一句（另两句是「你自己头上还挂着案子，倒来收我的？拔刀吧。」「先把你自己的名字弄干净，再照单子念我的。」）。
- 现中文：一个通缉犯跑来收罚金。我不向你交代。
- 现英文：A wanted man collecting fines. I will not answer to you.
- 问题：说法不准。条件是声望 < 0，−1 至 −10 时玩家并未被通缉；英文还写死了 “man”。
- 建议中文：你自己在灰袍那儿都不干净，倒来收我的罚金？我不向你交代。
- 建议英文：You're no cleaner with the Wardens than I am, and you come to collect my fine? I will not answer to you.
- 附带（不在 91 条内）：`gwp_voice_resist_scorn_0`「你自己头上还挂着案子」在 −1 至 −10 时也不准，可改为「你自己也欠着灰袍的账，倒来收我的？拔刀吧。」 / “You owe the Wardens yourself, and you come to collect mine? Draw.”

### `gwp_affairs_standing_gate`

- 出现情境：事务界面里，没入会或灰袍声望不够时点某一页，显示的锁定提示。
- 现中文：宣誓入会且灰袍声望达到 {STANDING} 后可查看。
- 现英文：Swear the oath and reach {STANDING} Grey Warden standing to open this screen.
- 问题：说法不准，入会没有宣誓环节。界面提示保留阈值，和已有的「入会且灰袍声望达到{STANDING}后，可在战斗外整理军械。」一致。
- 建议中文：加入灰袍且灰袍声望达到 {STANDING} 后可查看。
- 建议英文：Join the Grey Wardens and reach {STANDING} standing with them to open this screen.

### `gwp_det_ui_ledger_no_case`（只改英文）

- 出现情境：百科里查看一位 AI 领主，他没有正在追捕的案件，但名下还有没赎清的人命。
- 现中文：目前没有正在追捕的案件；若有待交付的罚金，交账后才会抵扣欠款。剿匪赎过也可能减少负声望。（属实，不改）
- 现英文：No case is open against him, but the charge for the dead stands until it is paid off or he is taken.
- 问题：英文和中文说的不是一回事，还写死了 “him”。
- 建议英文：No case is open at present. Fines still waiting to be handed in reduce the debt only once delivered. Fighting bandits can also reduce the negative standing.

### `gwp_rebuff_deedsonly_charm`（中文不改；英文要动代码，单列待定）

- 出现情境：罪犯打算只交四成、当场处死几个部下给交代；玩家用魅力劝他别杀人、照数交钱，没说动时他这样回。
- 现中文：你想替他们求情？那也不会让我多交余下的罚金。
- 现英文（strings.json）：Their punishment is mine to decide. I offered you two fifths, no more.
- 问题：`GwpFieldArrestLines.PaymentRebuff` 对每种打算只取 `_lead` 的英文，所以英文玩家不论用哪种技能都看到同一句，而中文是按技能分开的四句。改英文文本解决不了，要让 `PaymentRebuff` 按技能取英文——这是代码改动，需另批。
- 建议英文（代码改好后用）：Pleading for them won't get you a coin more of the fine.

## 附带发现：AI 震慑台词建议列为下一批

核实「印」时通读了 `GwpAiDeterrenceDialogueCatalog` 的 138 条（6 条旁白、66 条自述、66 条对玩家说的话）。这一组问题集中，建议下一批单独出稿：

- 「家族」来源除了同族，还包括同场战败的其他家族领主（`PoliceAIDeterrenceBehavior.ApplyBattleWitnessShock`），「我的族人越过了界」一类对他们不成立。
- 村庄暴力和商队袭击两套震慑合在一起选台词，劫过商队的领主也会说「烧毁一个村庄，不值得……」。
- 被押走、交罚金、约斗（罪犯赢了也算）、只被打垮都会让本人受震慑，「自从那次被捕以后」「锁链」只对其中一部分成立。
- 给入会玩家的那一半反复说「一纸通缉令」「纸上的墨迹」，游戏里玩家拿的是委托，没有纸质通缉令。
- 大量格言式空话，例如「权威在成为特权以前，首先是一份债务」「怒火消失的速度，总比后果更快」；给未入会玩家的版本默认玩家正要去烧村。
