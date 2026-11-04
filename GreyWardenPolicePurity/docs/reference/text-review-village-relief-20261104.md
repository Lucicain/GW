# 村庄打赏与重建

2026-11-04 审阅稿，**待用户审批**。本稿只出改稿，未改 `PlayerText/strings.json`、生成文件或代码。行为规则见[村庄善后、重建、请愿与村民打赏](../state/village-relief.md)。分组见[清单](text-review-inventory-20261104.md)第 14 批；收养提示在第 12 批。

场景说明只给审阅时看。没问题的只列中文，写「保留」。

## 一、村民酬谢

[玩家灰袍声望为正时每天累积一笔酬谢，在任何村子的菜单里都能领，可用滑条选领多少（[村民打赏](../state/village-relief.md#村民打赏greywardenvillagerewardbehavior)）。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_greywardenvillagerewardbehavior_001` | 领取村民酬谢（可领 {VAR_1} 第纳尔） / Receive rewards from villagers (you can receive {VAR_1} dinars) | 英文 “dinars” 拼错，句子冗长 | 保留 | Collect the villagers' reward ({VAR_1} denars available) |
| `gwp_greywardenvillagerewardbehavior_002` | 村民尚未筹出酬谢。 | 无 | 保留 | 保留 |
| `gwp_greywardenvillagerewardbehavior_003` | 你领取了村民的酬谢 {VAR_1} 第纳尔。 | 无 | 保留 | 保留 |
| `gwp_greywardenvillagerewardsliderscreen_001` | 设置领取金额 / Set the amount to be received | 英文生硬 | 保留 | Choose how much to collect |
| `gwp_greywardenvillagerewardsliderscreen_002` | 拖动滑块，选择本次要领取的村民酬谢。当前最多可领 {VAR_1} 第纳尔。 / Drag the slider to select the villager reward to be received this time. You can currently claim up to {VAR_1} dinars. | 英文 “dinars” 拼错 | 保留 | Drag the slider to choose how much of the villagers' reward to collect. Up to {VAR_1} denars are available. |
| `gwp_greywardenvillagerewardsliderscreen_003` | 确认 | 无 | 保留 | 保留 |
| `gwp_greywardenvillagerewardsliderscreen_004` | 取消 | 无 | 保留 | 保留 |
| `gwp_greywardenvillagerewardsliderscreen_005` | {VAR_1} 第纳尔 / {VAR_1} Dinar | 英文 “Dinar” 拼错且单数 | 保留 | {VAR_1} denars |

## 二、灰袍成员的请愿与重建

[灰袍成员在地图上的状态文字与重建完成提示。]

| 键 | 现中文 / 现英文 | 问题 | 建议中文 | 建议英文 |
|---|---|---|---|---|
| `gwp_reconstruction_001` | 无名村庄 | 无 | 保留 | 保留 |
| `gwp_reconstruction_002` | {VAR_1}完成了{VAR_2}的重建。 | 无 | 保留 | 保留 |
| `gwp_status_rebuilding` | 正在重建{VILLAGE}（还剩{HOURS}小时）。 | 无 | 保留 | 保留 |
| `gwp_status_petition` | 正在{SETTLEMENT}处理请愿（还剩{HOURS}小时）。 | 无 | 保留 | 保留 |

## 本稿统计

共 12 条，全部实审。建议改动 4 条（全部只改英文），保留 8 条。

- 中文没有问题；英文三处把 denars 拼成 “dinars / Dinar”，另有两句生硬。
- 没有发现编造，没有发现新的代码问题。

批准后的落地：只改 `PlayerText/strings.json` 里上表各键的 `en`，再跑 `python tools/Generate-PlayerText.py`；不新增键，变量不变，不改代码。
