# 家族、百科与婚姻收养

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[家族](../state/family.md)、[罪案登记与震慑](../state/crime-intake.md)。分组见[清单](text-review-inventory-20261104.md)第 12 批；六位创始领主的百科介绍、家族总介绍已在宗教稿里批准，不在本批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 核实到的情境（作者用，不进台词）

- 人物百科「案底与震慑」（`GwpEncyclopediaHeroPageVM`）：「被灰袍抓获」一栏读 `TotalArrestCount`，和平交罚金、塞私钱、约斗都会加一（[罪案登记](../state/crime-intake.md)，用户裁定 2026-10-17）。来源一栏「族人转述或亲眼目击」与 `ApplyBattleWitnessShock` 一致。
- 本职名（`GreyWardenFamilyBehavior.GetDutyTitle`）：练兵、商路、乡土、请愿、重建、贵族事务六类，取不到时用「灰袍巡法官」。
- 求婚：灰袍领主都是女性，「做我的妻子吧」成立。

## 一、灰袍成员的名字

[开局生成的灰袍成员从这组名字里取；重名时加序数（`_037`–`_045`，英文前面带空格）；`_046` 是取不到名字时百科里的替补代词。六位创始领主的名字单列。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_greywardenfamilybehavior_001` | 澄音 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_002` | 祈安 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_003` | 望舒 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_004` | 清岚 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_005` | 静禾 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_006` | 霁月 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_007` | 朝露 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_008` | 星祷 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_009` | 守真 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_010` | 昭宁 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_011` | 清律 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_012` | 若岚 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_013` | 听雪 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_014` | 慈光 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_015` | 霜晨 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_016` | 明祷 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_017` | 兰序 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_018` | 雅宁 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_019` | 晨铃 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_020` | 书弦 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_021` | 怀澄 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_022` | 澜音 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_023` | 芷宁 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_024` | 祷月 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_025` | 安禾 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_026` | 静澜 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_027` | 思律 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_028` | 清珑 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_029` | 雨谣 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_030` | 远星 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_031` | 采祷 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_032` | 露祈 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_033` | 映岚 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_034` | 书宁 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_035` | 凝光 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_036` | 夕晨 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_037` | 二 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_038` | 三 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_039` | 四 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_040` | 五 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_041` | 六 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_042` | 七 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_043` | 八 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_044` | 九 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_045` | 十 | 无 | 保留 | 保留 |
| `gwp_hero_chenxi` | 晨曦 | 无 | 保留 | 保留 |
| `gwp_hero_mise` | 弥瑟 | 无 | 保留 | 保留 |
| `gwp_hero_muguang` | 暮光 | 无 | 保留 | 保留 |
| `gwp_hero_shengduo` | 圣铎 | 无 | 保留 | 保留 |
| `gwp_hero_vandi` | 梵蒂 | 无 | 保留 | 保留 |
| `gwp_hero_yoer` | 约珥 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_046` | 她 | 无 | 保留 | 保留 |

## 二、本职名称

[百科与事务里显示的灰袍本职。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_duty_training` | 灰袍练兵者 / Training Warden | 称呼不统一：对话和事务里都叫「练兵官」，只有本职名叫「练兵者」（第 9 批已把订单台词统一为「练兵官」） | 灰袍练兵官 | 保留 |
| `gwp_duty_caravan` | 灰袍商路巡法者 | 无 | 保留 | 保留 |
| `gwp_duty_village` | 灰袍乡土巡法者 | 无 | 保留 | 保留 |
| `gwp_duty_issues` | 灰袍请愿巡行者 | 无 | 保留 | 保留 |
| `gwp_duty_player` | 贵族事务协调官 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_053` | 灰袍村庄重建官 | 无 | 保留 | 保留 |
| `gwp_greywardenfamilybehavior_054` | 灰袍巡法官 | 无 | 保留 | 保留 |

## 三、人物百科：案底与震慑

[任一人物百科页上的「案底与震慑」按钮，弹出详情。这是信息面板，按设计显示倍率、等级与恢复时间。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwpencyclopediaheropagevm_001` | {VAR_1} 的灰袍案底与震慑 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_002` | 关闭 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_003` | 案底与震慑 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_004` | 查看此人物永久保留的犯罪案底与当前灰袍震慑。 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_005` | 无记录 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_006` | 刚刚 / Just | 英文 “Just” 不成词 | 保留 | Just now |
| `gwp_gwpencyclopediaheropagevm_007` | {VAR_1} 小时前 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_008` | {VAR_1} 天前 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_015` | 最近一次执法：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_016` | 大地图状态：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaheropagevm_017` | 所在位置：{VAR_1} | 无 | 保留 | 保留 |
| `gwp_det_ui_record` | 累计犯罪：{VAR_1} \| 被灰袍抓获：{VAR_2} / Recorded crimes: {VAR_1} \| Arrested by Grey Wardens: {VAR_2} | 说法不准：这个数是 `TotalArrestCount`，和平交罚金、塞私钱、约斗都算一次，不全是「抓获」 | 累计犯罪：{VAR_1} \| 被灰袍惩处：{VAR_2} | Recorded crimes: {VAR_1} \| Dealt with by the Grey Wardens: {VAR_2} |
| `gwp_det_ui_village_heading` | 【侵害村民】 | 无 | 保留 | 保留 |
| `gwp_det_ui_villager_desire` | 攻击村民欲望：原版的 {VAR_1}%（已压制 {VAR_2}%） | 无 | 保留 | 保留 |
| `gwp_det_ui_raid_desire` | 烧村欲望：原版的 {VAR_1}%（与攻击村民共用同一压制） | 无 | 保留 | 保留 |
| `gwp_det_ui_caravan_heading` | 【侵害商队】 | 无 | 保留 | 保留 |
| `gwp_det_ui_caravan_desire` | 攻击商队欲望：原版的 {VAR_1}%（已压制 {VAR_2}%） | 无 | 保留 | 保留 |
| `gwp_det_ui_source` | 压制来源：本人 {VAR_1} \| 他人传递 {VAR_2}（{VAR_3}） | 无 | 保留 | 保留 |
| `gwp_det_ui_source_personal` | 本人经历为主 | 无 | 保留 | 保留 |
| `gwp_det_ui_source_transmitted` | 族人转述或亲眼目击为主 | 无 | 保留 | 保留 |
| `gwp_det_ui_source_none` | 当前没有有效压制 | 无 | 保留 | 保留 |
| `gwp_det_ui_source_balanced` | 本人经历与他人传递接近 | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_complete` | 预计恢复原状：已经恢复 | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_hours` | {VAR_1} 小时 | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_days` | {VAR_1} 天 | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_paused` | 预计恢复原状：当前暂停恢复；恢复后约需 {VAR_1} | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_active` | 预计恢复原状：约 {VAR_1} | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_floor_complete` | 最低压制：永久固定为 {VAR_1} 级 | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_floor_paused` | 预计恢复至最低压制 {VAR_1} 级：当前暂停恢复；恢复后约需 {VAR_2} | 无 | 保留 | 保留 |
| `gwp_det_ui_recovery_floor_active` | 预计恢复至最低压制 {VAR_1} 级：约 {VAR_2} | 无 | 保留 | 保留 |
| `gwp_det_ui_ledger_clean` | 灰袍账目：没有未清的账。 | 无 | 保留 | 保留 |
| `gwp_det_ui_ledger_heading` | 灰袍账目 | 无 | 保留 | 保留 |
| `gwp_det_ui_ledger_case` | 未结案件：{VAR_1} 起；仅这件案子就死了 {VAR_2} 人。 | 无 | 保留 | 保留 |
| `gwp_det_ui_ledger_standing_lives` | 尚未赎清的人命：{VAR_1} 条。 / Civilian lives still on his record: {VAR_1}. | 英文写死 “his” | 保留 | Civilian lives still on their record: {VAR_1}. |
| `gwp_det_ui_ledger_total` | 合计应缴：{VAR_1} 第纳尔。 | 无 | 保留 | 保留 |
| `gwp_case_pending_encyclopedia` | 本次罚金：{VAR_1} 第纳尔。犯人已缴：{VAR_2} 第纳尔。 | 无 | 保留 | 保留 |
| `gwp_det_ui_ledger_no_case` | 目前没有正在追捕的案件；若有待交付的罚金，交账后才会抵扣欠款。剿匪赎过也可能减少负声望。 | 英文见[核实稿](text-review-term-check-20261104.md) | 保留 | 见核实稿 |

## 四、收养与婚姻

[灰袍从受灾村庄收养孩子的提示与人物日志；玩家向灰袍领主求婚。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_adoption_chat_notice` | {HERO.LINK}被灰袍守卫从{VILLAGE}收留了。 | 无 | 保留 | 保留 |
| `gwp_adoption_log_entry` | {HERO.LINK}被灰袍守卫从{VILLAGE}收留，并带回内院抚养。 | 无 | 保留 | 保留 |
| `gwp_greywardenadoptionlogentry_003` | 未知村庄 | 无 | 保留 | 保留 |
| `gwp_greywardenvillageadoptionbehavior_001` | 无名村庄 | 无 | 保留 | 保留 |
| `gwp_warden_marry_ask` | 做我的妻子吧。 | 无 | 保留 | 保留 |
| `gwp_warden_marry_yes` | 那就这么定了。我是你的，你也是我的。 | 无 | 保留 | 保留 |

## 五、家族百科、声望与杂项

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_gwpdata_001` | 你已被灰袍通缉。 | 无 | 保留 | 保留 |
| `gwp_gwpdata_002` | 灰袍已撤销对你的通缉。 | 无 | 保留 | 保留 |
| `gwp_gwpdata_003` | 灰袍记下了你的罪行：{VAR_1}（{VAR_2}）。 | 无 | 保留 | 保留 |
| `gwp_gwpdata_005` | 声望：{VAR_1} / Reputation: {VAR_1} | 英文 “Reputation”，别处叫 “standing” | 保留 | Standing: {VAR_1} |
| `gwp_policeresourcemanager_001` | 你的钱不够，灰袍没收了价值 {VAR_1} 第纳尔的物品。 | 无 | 保留 | 保留 |
| `gwp_gwpencyclopediaclanpagevm_005` | 查看对外战争与开战依据、案件与差事总池、司法公库、你的声望，以及家族名册。 / Standing wars and their grounds, the case and duty pool, the judicial treasury, your standing, and the family roll. | 「差事总池」是开发用语 | 查看对外战争与开战依据、案件与差事、司法公库、你的声望，以及家族名册。 | 保留 |

## 本稿统计

共 108 条：英文见核实稿 1 条，实审 107 条。建议改动 6 条（只改中文 2、中英都改 1、只改英文 3），保留 101 条。

- 说法不准 1 条：百科「被灰袍抓获」（实际包括和平了结）。
- 称呼不统一 1 条（「练兵者 / 练兵官」）；开发用语 1 条；英文 3 条。
- 名字、序数与创始领主名都保留。没有发现编造，没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `zh`/`en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
