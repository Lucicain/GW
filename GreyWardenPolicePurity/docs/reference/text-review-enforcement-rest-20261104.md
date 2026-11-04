# 灰袍领主执法其余：赎罪、押送、纠察队其余提示与使者约斗

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[赎罪委派](../state/atonement.md)、[纠察队](../state/patrol.md)、[使者](../state/case-envoy.md)。分组见[清单](text-review-inventory-20261104.md)第 11 批；纠察队与灰袍领主拦截的对话已在[纠察队与灰袍领主拦截审阅稿](text-review-player-enforcement-20261104.md)里。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 赎罪可以靠谈判、收款、接受投案或击败目标完成（纠察队稿已核）。交差选项 `_014` 与回话 `_015` 在 `EnforcementAtonementTurnInCondition` 里拼进声望数字；纠察队稿的处理是「数字留在任务日志里，领主不必再念」，这里照同一口径。
- 被灰袍领主打败：`IsEscortingPlayer` 置真，押往最近的城堡（取不到名字时用 `_009`），没交够罚金时 `TryAddPlayerCrime` 给玩家立案。
- 纠察队战争说明：`PolicePatrolBehavior.BuildPatrolWarReasonDetails` 拼成一行，进灰袍事务「组织概况」。纠察队阶段玩家没有被通缉。
- `DebugPatrol` 是 `private static readonly bool DebugPatrol = false;`，`DebugLog` 永远不显示。

## 一、赎罪任务

[声望 ≤ −11 时玩家认罪，领一桩赎罪任务（[赎罪](../state/atonement.md)）：任务标题、日志、情报、交差选项与结果。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_001` | 赎罪期限已过。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_003` | {VAR_1}的案件已处置，回去向灰袍领主复命。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_004` | 赎罪失败。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_001` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_002` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_003` | 灰袍赎罪令：追捕 {VAR_1} / the Grey Wardens Atonement: The Hunt {VAR_1} | 「赎罪令」像一纸命令，游戏里没有；只写「追捕」，谈判、收款、投案也能完成；英文 “The Hunt {VAR_1}” 读不通 | 灰袍赎罪：处置{VAR_1} | Grey Warden atonement: deal with {VAR_1} |
| `gwp_policeenforcementbehavior_atonementquest_005` | 赎罪完成：声望 +{VAR_1}，当前声望 {VAR_2}。 / Atonement completed: reputation +{VAR_1}, current reputation {VAR_2}. | 英文 “reputation”，别处叫 “standing” | 保留 | Atonement completed: standing +{VAR_1}, now {VAR_2}. |
| `gwp_policeenforcementbehavior_atonementquest_006` | 未知位置 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_009` | 未知位置 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_010` | 探子回报：{VAR_1} 最近出现在 {VAR_2} 附近（约 {VAR_3} 人）。 / Spy report: {VAR_1} recently appeared near {VAR_2} (about {VAR_3} people). | 英文 “Spy report … people” 生硬 | 保留 | Scouts report: {VAR_1} was seen near {VAR_2} recently (about {VAR_3} men). |
| `gwp_policeenforcementbehavior_atonementquest_011` | 赎罪的目标仍然在逃。继续追下去。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_012` | 赎罪的目标仍然在逃。继续追下去。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_013` | 赎罪的目标仍然在逃。继续追下去。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_014` | 关于赎罪任务（提交后恢复最多 {VAR_1} 点声望） / Concerning the atonement (up to {VAR_1} standing may be restored upon discharge) | 玩家选项里念机制（「提交后恢复最多 N 点声望」）；纠察队稿已定数字留在任务日志里 | 我来交代赎罪的差事。 | I've come to report on my atonement. |
| `gwp_policeenforcementbehavior_atonementquest_015` | 核验无误。你已完成赎罪任务，按案卷可恢复最多 {VAR_1} 点声望。 / The account is verified. Your atonement is complete, and the roll allows the restoration of up to {VAR_1} standing. | 「核验无误」「按案卷可恢复最多 N 点声望」是公文腔与机制说明 | 核过了，没有问题。这桩赎罪的差事，你办完了。 | It all checks out. You have carried out your atonement. |
| `gwp_policeenforcementbehavior_atonementquest_016` | 赎罪已完成。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_atonementquest_017` | 赎罪完成，经灰袍调停，你与{VAR_1}恢复了和平。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_dialogue_013` | 未知目标 | 无 | 保留 | 保留 |
| `gwp_atonement_closed_elsewhere` | 这宗案子已由他人了结，赎罪委托撤销，不计作你的过失。 | 无 | 保留 | 保留 |
| `gwp_atonement_report` | 说说这件事是怎样了结的，代收的钱货也一并交来。 | 无 | 保留 | 保留 |

## 二、被灰袍领主打败后押送

[玩家被灰袍领主击败，押往最近的城堡收罚金；没交够时给玩家立一条案（案名 `_005`、案情 `_006`）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_010` | 你被{VAR_1}击败，正被押往{VAR_2}。 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_009` | 堡垒 / FORT | 同上；英文全大写 “FORT” | 城堡 | a castle |
| `gwp_policeenforcementbehavior_005` | 罚款不足 / Insufficient fine | 「罚款」与别处「罚金」不一致（案名） | 罚金未缴足 | Fine not paid in full |
| `gwp_policeenforcementbehavior_006` | 押送罚款未缴清 / Escort fine unpaid | 同上（案情说明） | 押送后罚金未缴清 | Fine still unpaid after escort |
| `gwp_policeenforcementbehavior_007` | 堡垒 / Fortress | 押往的地方取不到名字时的替补；原版叫「城堡」，英文 “Fortress” 不对 | 城堡 | a castle |
| `gwp_policepatrolbehavior_040` | 最近城镇 / The nearest town | 英文句首大写，嵌进「被押往{VAR_1}」 | 保留 | the nearest town |

## 三、纠察队与追截队

[纠察队与追截队的队伍名、给玩家立案的案名、以及事务界面里纠察队战争的说明（`_encyclopedia_*`）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policeenforcementbehavior_delaypatrols_001` | 灰袍纠察支援队 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_delaypatrols_003` | 累计犯罪 | 无 | 保留 | 保留 |
| `gwp_policeenforcementbehavior_delaypatrols_004` | 声望已达 {VAR_1} / Reputation has reached {VAR_1} | 英文 “Reputation”，别处叫 “standing” | 保留 | Standing has reached {VAR_1} |
| `gwp_policeenforcementbehavior_delaypatrols_005` | 灰袍高速追截队 / Grey Warden pursuit detachment | 「高速」是开发用语（队伍名） | 灰袍追截队 | 保留 |
| `gwp_policepatrolbehavior_029` | 累计犯罪 | 无 | 保留 | 保留 |
| `gwp_policepatrolbehavior_030` | 声望已达 {VAR_1} / Reputation has reached {VAR_1} | 同上 | 保留 | Standing has reached {VAR_1} |
| `gwp_policepatrolbehavior_035` | 纠察队巡逻 / Picket Patrol | 队伍名「纠察队巡逻」不像名字，别处叫「灰袍纠察队」；英文 “Picket” | 灰袍纠察队 | Grey Warden Patrol |
| `gwp_policepatrolbehavior_encyclopedia_001` | 玩家已被纠察队控制，正在押送或等待处罚 / The player is in provost custody, under escort or awaiting judgment. | 「玩家」「模组声望」「执行强制缉拿」是开发用语；显示在灰袍事务「组织概况」的宣战理由里 | 你已被纠察队拿下，正被押走或等候处罚 | you have been taken by the patrol and are being escorted or awaiting judgment |
| `gwp_policepatrolbehavior_encyclopedia_002` | 地图上仍有 {VAR_1} 支纠察队在执行强制缉拿 / {VAR_1} provost patrols remain upon the map under orders of arrest. | 「玩家」「模组声望」「执行强制缉拿」是开发用语；显示在灰袍事务「组织概况」的宣战理由里 | 还有{VAR_1}支纠察队在追你 | {VAR_1} patrols are still out looking for you |
| `gwp_policepatrolbehavior_encyclopedia_003` | 已进入纠察队战争状态，正在等待收尾 / has entered the picket war state and is waiting for the end. | 「玩家」「模组声望」「执行强制缉拿」是开发用语；显示在灰袍事务「组织概况」的宣战理由里；英文 “picket war state” 机翻 | 纠察队已经撤回，这场仗正等着了结 | the patrols have withdrawn, and the war is about to end |
| `gwp_policepatrolbehavior_encyclopedia_004` | 纠察队执法：玩家在负声望执法阶段拒绝配合后，灰袍守卫会对玩家所属势力宣战以强制缉拿。当前阶段：{VAR_1}；当前模组声望：{VAR_2}。 / Provost enforcement: after the player refuses a lawful order while under warrant, the Grey Wardens declare war upon the player’s faction to compel arrest. Present stage: {VAR_1}; present standing: {VAR_2}. | 「玩家」「模组声望」「执行强制缉拿」是开发用语；显示在灰袍事务「组织概况」的宣战理由里；后半句解释规则；英文 “under warrant” 不对（纠察队阶段未通缉） | 纠察队在追捕你：{VAR_1}。你的灰袍声望：{VAR_2}。 | A Grey Warden patrol is after you: {VAR_1}. Your standing with the Wardens: {VAR_2}. |

## 四、灰袍使者约斗

[灰袍领主派使者交涉，罪犯要求单挑时改在比武大会上了结（[使者](../state/case-envoy.md)）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_envoy_party_name` | 灰袍使者 | 无 | 保留 | 保留 |
| `gwp_envoy_duel_called` | {VAR_1}向{VAR_2}提出，要在{VAR_3}的比武大会上了结灰袍的指控。 | 无 | 保留 | 保留 |
| `gwp_envoy_duel_moved` | {VAR_1}与{VAR_2}的比武改到{VAR_3}的比武大会。 | 无 | 保留 | 保留 |
| `gwp_envoy_duel_warden_won` | {VAR_1}在{VAR_3}的比武大会上名次压过了{VAR_2}，灰袍收取了{VAR_4}第纳尔。 | 无 | 保留 | 保留 |
| `gwp_envoy_duel_offender_won` | {VAR_1}在{VAR_3}的比武大会上名次压过了{VAR_2}，灰袍依约放他离开。 / {VAR_1} placed above {VAR_2} at the tournament in {VAR_3}, and the Wardens let him go as agreed. | 英文写死 “him” | 保留 | {VAR_1} placed above {VAR_2} at the tournament in {VAR_3}, and the Wardens let the offender go as agreed. |

## 五、不显示的调试输出

[`PolicePatrolBehavior.DebugLog` 只在常量 `DebugPatrol` 为真时显示，现在写死为 `false`，这 11 条玩家看不到。不审措辞。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policepatrolbehavior_001` | 已下发返程命令，等待到达定居点后销毁 | 不可达 | — | — |
| `gwp_policepatrolbehavior_002` | 已调度：返程至驻地后销毁纠察队 | 不可达 | — | — |
| `gwp_policepatrolbehavior_011` | 返程抑制中，拒绝再次进入纠察队对话 | 不可达 | — | — |
| `gwp_policepatrolbehavior_012` | 进入纠察队对话：party={VAR_1}, rep={VAR_2}, fine={VAR_3} | 不可达 | — | — |
| `gwp_policepatrolbehavior_023` | 对话结束，开始遣返。active={VAR_1}, returning={VAR_2} | 不可达 | — | — |
| `gwp_policepatrolbehavior_024` | PlayerEncounter.Finish(false) 已调用 | 不可达 | — | — |
| `gwp_policepatrolbehavior_037` | MapEventEnded 检测到玩家已被俘，进入押送流程 | 不可达 | — | — |
| `gwp_policepatrolbehavior_038` | MapEventEnded 无 winner，按投降路径处理为纠察队胜利 | 不可达 | — | — |
| `gwp_policepatrolbehavior_helpers_003` | 已下发返程命令，候选数量={VAR_1}，returning={VAR_2} | 不可达 | — | — |
| `gwp_policepatrolbehavior_helpers_004` | 纠察队已到达 {VAR_1}，执行销毁：{VAR_2} | 不可达 | — | — |
| `gwp_policepatrolbehavior_helpers_005` | 返程销毁已完成，恢复正常会话。 | 不可达 | — | — |

## 代码问题（不在本稿改）

1. **调试输出没有放进 `#if GWP_DIAGNOSTICS`**：`PolicePatrolBehavior.DebugLog` 靠写死的 `DebugPatrol = false` 关掉，11 条调试文字（含 `PlayerEncounter.Finish(false) 已调用`、`returning={VAR_2}` 之类）仍编进玩家包。按诊断规则应移进 `#if GWP_DIAGNOSTICS`，或连同这些文字一起删掉，属代码改动，另批。

## 本稿统计

共 53 条：不显示 11 条（调试输出，不审），实审 42 条。建议改动 19 条（中英都改 12、只改中文 1、只改英文 6），保留 23 条。

- 说法不准 / 编造 2 条：赎罪任务标题「赎罪令：追捕」（没有这道令、也不只追捕）、纠察队战争说明英文 “under warrant”。
- 机制说明 / 公文腔 2 条（赎罪交差选项与回话）。
- 开发用语 5 条（「玩家」「模组声望」「高速」等）；用词不一致 5 条（「罚款 / 罚金」、「堡垒 / 城堡」、队伍名）；英文用词与大小写 6 条（与上面有重叠）。
- 代码问题 1 处，见上。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；`_014`、`_015` 中英同时去掉 `{VAR_1}`，变量集合仍一致，代码照传参数、原版 TextObject 忽略不用的变量，不改代码。
