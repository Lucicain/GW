# 玩家罪行记录与声望通知

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[玩家声望](../state/player-justice.md)、[罪案登记](../state/crime-intake.md)。分组见[清单](text-review-inventory-20261104.md)第 10 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。本批中文基本没问题，主要是英文拼进句子后读不通。

## 核实到的情境（作者用，不进台词）

- 义举 / 罪行提示：`PlayerBehaviorMonitor` 战后结算，`VAR_1` 是义举或罪行短语，嵌进「灰袍记下了你的义举 / 罪行：……」。英文短语用的是标题大小写和祈使语气，拼进句子成了 “The Grey Wardens note your good deed: Protect the Village from Plunder.”。
- 介入选项：`TryBuildCivilianEncounterTexts` 拼「加入防守方，保护{VAR_1}」「加入攻击方，{VAR_1}」，`VAR_1` 取 `_030`–`_035`。
- 通缉（声望 ≤ −11）后是灰袍领主带队拦截（纠察队稿已核），信息条提示说「会对你立案」不准确。
- NPC 罪案：`PoliceCrimeMonitorEnhanced.Report` 方法体整段注释掉了，传给它的 `_002`、`_005`、`_008`、`_009` 不显示；`_001`、`_004`、`_007` 作为受害方名、`_003`、`_006`、`_010` 作为罪名存进案件，在台账里显示。

## 一、地图信息条的灰袍声望

[右下角信息条展开后的声望一项与悬浮提示。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_mapbar_reputation_title` | 灰袍声望 | 无 | 保留 | 保留 |
| `gwp_mapbar_reputation_hint` | 灰袍守卫对你的评价。犯罪扣分，行善加分。 | 无 | 保留 | 保留 |
| `gwp_mapbar_reputation_wanted` | 你已被通缉。灰袍领主会对你立案。 / You are wanted. Grey Warden lords will open a case against you. | 「立案」不准：通缉后是灰袍领主带兵来拦截 | 你已被通缉，灰袍领主会带兵来找你。 | You are wanted. Grey Warden lords will come for you. |

## 二、玩家做了好事

[正义战斗里个人击杀每满十人加一点声望时提示；协助灰袍缉拿、击溃在逃罪犯另有一句。`VAR_1` 是义举名称。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbehaviormonitor_014` | 保护村庄免遭劫掠 / Protect the Village from Plunder | 英文嵌进上一句后读不通（大小写、冠词）：“note your good deed: Protect the Village from Plunder.” | 保留 | protecting a village from raiders |
| `gwp_playerbehaviormonitor_015` | 解救商队 / Rescue the Caravan | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | rescuing a caravan |
| `gwp_playerbehaviormonitor_016` | 解救村民 / Rescue the Villagers | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | rescuing villagers |
| `gwp_playerbehaviormonitor_017` | 击败劫匪 / Defeat the robbers | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | defeating bandits |
| `gwp_playerbehaviormonitor_018` | 灰袍记下了你的义举：{VAR_1}。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_043` | 灰袍记下了你协助缉拿罪犯的义举。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_054` | 灰袍记下了你击溃在逃罪犯的义举。 / The Grey Wardens note that you broke a wanted outlaw's band. | 英文 “wanted outlaw”：NPC 有案，不是被通缉 | 保留 | The Grey Wardens note that you broke the band of an outlaw they were hunting. |

## 三、玩家犯了罪

[劫掠、强征、袭击商队村民、妨碍执法、袭击灰袍时提示；通缉中还给玩家立一条案，案名用 `_019`–`_023`、`_005`、`_010`、`_012`、`_044`、`_045`。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbehaviormonitor_005` | 劫掠村庄 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_006` | 未知村庄 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_007` | 灰袍记下了你劫掠{VAR_1}的罪行。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_010` | 强迫募兵 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_011` | 未知村庄 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_012` | 强征给养 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_013` | 未知村庄 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_019` | 劫掠村庄 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_020` | 强迫募兵 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_021` | 强征给养 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_022` | 劫掠商队 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_023` | 杀害村民 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_024` | 劫掠村庄 / Raid a village | 英文嵌进上一句后读不通（大小写、冠词）：“record your crime: Raid a village.” | 保留 | raiding a village |
| `gwp_playerbehaviormonitor_025` | 强迫募兵 / Force recruitment | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | forcing villagers into your ranks |
| `gwp_playerbehaviormonitor_026` | 强征给养 / Forced supplies | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | seizing supplies by force |
| `gwp_playerbehaviormonitor_027` | 劫掠商队 / Raid a caravan | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | raiding a caravan |
| `gwp_playerbehaviormonitor_028` | 杀害村民 / Kill villagers | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | killing villagers |
| `gwp_playerbehaviormonitor_029` | 灰袍记下了你的罪行：{VAR_1}。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_044` | 妨碍执法 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_045` | 帮助罪犯对抗灰袍守卫 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_046` | 灰袍记下了你妨碍执法的罪行。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_047` | 纠察队 / Picket | 英文 “Picket” 不对，嵌进「你袭击{VAR_1}」 | 保留 | a Grey Warden patrol |
| `gwp_playerbehaviormonitor_048` | 灰袍守卫 / Grey Wardens | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | the Grey Wardens |
| `gwp_playerbehaviormonitor_049` | 灰袍记下了你袭击{VAR_1}的罪行。 | 无 | 保留 | 保留 |

## 四、遇到正在发生的战斗时的介入选项

[原版加入战斗菜单里「帮助攻方 / 守方」的说明文字，按场面拼出（[现场介入](../state/player-justice.md#现场介入正在发生的犯罪与执法)）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_playerbehaviormonitor_030` | 村庄 / Village | 英文嵌进上一句后读不通（大小写、冠词）：“protect Village” | 保留 | the village |
| `gwp_playerbehaviormonitor_031` | 商队 / Caravan | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | the caravan |
| `gwp_playerbehaviormonitor_032` | 村民 / Villager | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | the villagers |
| `gwp_playerbehaviormonitor_033` | 参与劫掠村庄 / Participate in raiding village | 英文嵌进上一句后读不通（大小写、冠词）：“joins the attacker, Participate in raiding village.” | 保留 | raid the village |
| `gwp_playerbehaviormonitor_034` | 劫掠商队 / Raid Caravan | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | raid the caravan |
| `gwp_playerbehaviormonitor_035` | 攻击村民 / Attack villagers | 英文嵌进上一句后读不通（大小写、冠词） | 保留 | attack the villagers |
| `gwp_playerbehaviormonitor_036` | 加入防守方，保护{VAR_1}。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_037` | 加入攻击方，{VAR_1}。 / joins the attacker, {VAR_1}. | 英文 “joins the attacker,” 读不通 | 保留 | Join the attackers and {VAR_1}. |
| `gwp_playerbehaviormonitor_038` | 犯人 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_039` | 加入灰袍守卫一方，协助缉拿罪犯。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_040` | 加入 {VAR_1} 一方，对抗灰袍守卫。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_041` | 加入 {VAR_1} 一方，对抗灰袍守卫。 | 无 | 保留 | 保留 |
| `gwp_playerbehaviormonitor_042` | 加入灰袍守卫一方，协助缉拿罪犯。 | 无 | 保留 | 保留 |

## 五、罪名短语

[案件台账、宣战理由里把存下的罪名换成显示文字（`GwpText.CrimeType`）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_crime_aid_criminals` | 帮助罪犯对抗灰袍守卫 | 无 | 保留 | 保留 |
| `gwp_crime_attack_caravan` | 攻击商队 | 无 | 保留 | 保留 |
| `gwp_crime_attack_villagers` | 攻击村民 | 无 | 保留 | 保留 |
| `gwp_crime_forced_recruitment` | 强迫募兵 | 无 | 保留 | 保留 |
| `gwp_crime_forced_supplies` | 强征给养 | 无 | 保留 | 保留 |
| `gwp_crime_join_village_raid` | 参与劫掠村庄 | 无 | 保留 | 保留 |
| `gwp_crime_kill_villagers` | 杀害村民 | 无 | 保留 | 保留 |
| `gwp_crime_obstruct_law` | 妨碍执法 | 无 | 保留 | 保留 |
| `gwp_crime_raid_caravan` | 劫掠商队 | 无 | 保留 | 保留 |
| `gwp_crime_raid_village` | 劫掠村庄 | 无 | 保留 | 保留 |

## 六、NPC 罪案登记与王国决议理由

[NPC 劫掠、袭击时登记罪名与受害方名称；王国决议里灰袍家族不参与扩张战争时的理由。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_policecrimemonitorenhanced_001` | 村民 | 无 | 保留 | 保留 |
| `gwp_policecrimemonitorenhanced_002` | 攻击村民 | 不可达 | — | — |
| `gwp_policecrimemonitorenhanced_003` | 攻击村民 | 无 | 保留 | 保留 |
| `gwp_policecrimemonitorenhanced_004` | 商队 | 无 | 保留 | 保留 |
| `gwp_policecrimemonitorenhanced_005` | 攻击商队 | 不可达 | — | — |
| `gwp_policecrimemonitorenhanced_006` | 攻击商队 | 无 | 保留 | 保留 |
| `gwp_policecrimemonitorenhanced_007` | {VAR_1}村的村民 / {VAR_1} Villager | 英文嵌进上一句后读不通（大小写、冠词）：“Sturgia Villager” | 保留 | villagers of {VAR_1} |
| `gwp_policecrimemonitorenhanced_008` | 劫掠村庄(开始) | 不可达 | — | — |
| `gwp_policecrimemonitorenhanced_009` | 村庄={VAR_1} | 不可达 | — | — |
| `gwp_policecrimemonitorenhanced_010` | 劫掠村庄 | 无 | 保留 | 保留 |
| `gwp_policeantirecruitmentmodel_001` | 灰袍不为争夺领土开战。 | 无 | 保留 | 保留 |

## 代码问题（不在本稿改）

1. `PoliceCrimeMonitorEnhanced.Report` 方法体只剩一段注释掉的调试输出（还写着字面量 `"Unknown"`），三处调用仍在为它取文字。按「没人用的成员当场删」应连同 `_002`、`_005`、`_008`、`_009` 一起删掉，属代码改动，另批。
2. NPC 与玩家的罪名是以「当前语言的显示文字」存进案件的（`RegisterCrime(GwpText.Get(...))`、`TryAddPlayerCrime(crimeType, ...)`），存档后切换语言，旧案的罪名不会跟着变。是否改成存键，属代码改动。

## 本稿统计

共 68 条：不显示 4 条（不审），实审 64 条。建议改动 21 条（中英都改 1、只改英文 20），保留 43 条。

- 说法不准 1 条：信息条「灰袍领主会对你立案」。
- 英文拼句读不通 19 条，英文 “wanted” 1 条。
- 代码问题 2 处，见上。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
