# 野外执法：肯不肯谈、两层谈判、兑现方式与结案

> 当前状态：战力按"当场会一起上阵"口径、冷却台词不提时限，用户已实测确认（2026-10-02）
> 最后验收：`04ff2d6`（2026-10-02 用户确认）
> 已复核至：`1f84971`（2026-10-15）
> 覆盖源码：`GwpFieldArrestBehavior.cs` `GwpFieldArrestBehavior.Grace.cs` `GwpFieldArrestLines.cs` `GwpNegotiationPolicy.cs` `GwpNegotiationResistance.cs` `GwpOffenderDesire.cs` `GwpNegotiationChancePatch.cs` `GwpFieldDialogueVoice.cs` `GwpFieldCollectionBarterable.cs` `GwpFieldFineBarterable.cs` `GwpBribeBarterable.cs` `GwpDuelOpponentMarkerPatch.cs`
> 待实测：对方的打算更看性格，有钱的领主不再多半照数全缴；第二层更难谈（实力差不再进第二层），性格越极端越难

玩家在野外拦下自己承办的罪犯之后的全部流程。罚金定价见 [`crime-intake.md`](crime-intake.md)；
交差、查账见 [`player-justice.md`](player-justice.md)。

## 入口

与承办目标（`PlayerBountyBehavior.HasCommissionAuthority`）在野外单独对话；任一方在定居点里
不走这条路。

## 肯不肯谈

`PrepareFieldArrest` 时掷一次：

- `GwpNegotiationPolicy.RefusalChance`：钱够付罚金、荣誉+仁慈+慷慨 ≥ 2 且荣誉、仁慈都不为负的人
  一律肯听；否则 `clamp((第一层抗性 − 45) / 65, 0, 0.8)`。
- 概率 ≥ 0.55 就拒谈（有宽限约定时不拒）。拒谈直接进"动手或走人"。

两层抗性的公式在 `GwpNegotiationResistance`（读游戏人物）；不碰引擎的纯规则在 `GwpNegotiationPolicy`，
它也编进 `tools/CaseSettlement.Tests`，不得引用引擎类型（依据：流程 2026-10-15，测试项目因此编不过）。[灰袍使者](case-envoy.md) 替灰袍领主谈时用同一套。
第一层抗性 `LayerOneResistance`（0–100）= 基线 50 + 实力摆幅 + 性格 + 玩家分量 − 被抓过的教训
+ 名望差与劣迹压力 + 钱包压力 − 慷慨×4。实力摆幅 = `(他/(他+我) − 0.5) × 2 × 45`，
所以实力比是最大的单项，口径错了会直接决定拒不拒谈。

## 战力口径

和灰袍现场宣战同一个判断（`GwpStrength.Compare`，见 `infrastructure.md`「战力判断」）：
我方 = 玩家所在战斗组 + 身边肯一起打的同势力部队；对方 = 他所在战斗组 + 身边会来帮他的部队
（加入半径内全额、外圈折算）。他名义上属于某军团、还在赶去会合时只算他本人。
第一层、第二层抗性和诊断都用它。

2026-09-26 之前用的是原版 `GetTotalLandStrengthWithFollowers`：在队伍护送别人时（去军团会合就是 `EscortParty`）
它算被护送的那支，还把没汇合的军团成员一起算。实机：本人弱于玩家的目标赶去军团途中算出 1281 对 149，抗性 90、拒谈。
当时改成本地写的一套，2026-10-07 统一到 `GwpStrength`。

- 不要用 `GetTotalLandStrengthWithFollowers` 判断谈判双方实力。（依据：C#反编译 v1.4.8，2026-09-26；实机诊断 2026-09-26）

## 谈崩冷却

拒谈或第一层没谈拢时记 24 小时（`NegotiationCooldownHours`），期间对他只能"再谈一次"，
得到一句按性格的回绝（`GwpFieldArrestLines.Rebuffed`）。

回绝台词只表达"答复不变"。

- 回绝台词不得提今天、明天、天亮等时限，那是把冷却机制念给玩家听。（依据：用户裁定 2026-09-26）

## 诊断

`TALK_WILLINGNESS`（双方战力、抗性、拒谈概率）、`NEGOTIATION_CHANCE_CONTEXT`、
`NEGOTIATION_COOLDOWN_SET`，写案件日志。

## 两层谈判

说服用原版 persuasion 那一套（和求爱同一套），口才、统御、诡计、交易都用得上。
每条要点最多 4 次说辞（`GwpTuning.FieldArrest.ArgumentsPerReservation`）。

- **第一层：认不认这次执法**。抗性就是上面的 `LayerOneResistance`。
- **第二层：按正规方式全额缴，还是照他自己的打算办**。性格至上，实力差不进这一层（第一层已经看过）（用户裁定 2026-10-15）。
  抗性 `LayerTwoResistance`（0–100）的组成：
  - 基数 30；
  - 加执着 × `InsistenceStep`（20，试玩值），执着按他的打算取对应性格的绝对强度（`GwpOffenderDesires.Insistence`）；
  - 加肉痛 × 15，肉痛 = 罚金 / 他的家当，夹在 0–2；
  - 加魅力差 ±10；
  - 加名望与劣迹压力的一半。
- 成功率：原版 `GetChances` 算出的值，按当前层的抗性重新换算（`GwpNegotiationChancePatch` → `AdjustNativeChances`），
  大成功、大失败的占比不变。
- 只有真的达到目标才算谈成；说辞用光却没到目标，算谈崩。（依据：实机诊断）
  大失败会封掉剩下所有选项，结束整段对话。
- 谈完之后，不论哪一层、谈没谈成、缴没缴清，玩家仍握着执法权，随时可以回头拿人。

## 他打算怎么了结（`GwpOffenderDesire`）

开口前按性格给各方案加权，抽一个（`GwpNegotiationPolicy.Weights`）。家当不给任何方案加权（用户裁定 2026-10-15：
原版领主家当动辄是罚金的几十倍，「有钱就照数全缴」让性格几乎不起作用）；家当只用来排除做不到的方案：
身无分文不会讨价还价、塞私钱、拿兵出气，交不起罚金的不会骗一道。性格极端只是更可能抽到某个方案，不会锁死结果。
[灰袍使者](case-envoy.md) 抽到什么就照办，第二层只在交钱的方案上讨价还价。

| 方案 | 主要来自 | 他肯给 |
|---|---|---|
| 照数全缴 | 勇武低、仁慈高、慷慨高 | 100% |
| 讨价还价 | 慷慨低 | 70% |
| 骗一道 | 算计高 | 50% |
| 只认自己的事 | 仁慈低；还会杀自己 5 个兵泄愤 | 40% |
| 塞私钱抹案 | 荣誉低；钱进玩家口袋、不进公库，案底一点不少 | 15% |
| 人跟你走 | 荣誉高；钱留给部下，人当俘虏，部队照原版自行解散 | — |
| 单挑 | 勇武高 | 他赢免罚，玩家赢全缴 |
| 请宽限 | 有条件给宽限时 | 三天后再收 |

- 第二层谈崩，玩家可以选「照他说的办」。打折的差额留在他的案底里：公库没收到钱，人就没赎。

## 结案（`Settle`）

- 收钱走原版交易界面：
  - 他的钱袋全部可见，但交易按钮拒绝超过谈定数目的报价（`GwpFieldCollectionBarterable`）。
  - 交到公库的罚金另用一个专门条目记实缴数（`GwpFieldFineBarterable`），普通买卖、赠礼、取消的报价都不能冒充罚金。
- 结案时：
  - 犯人这一侧的账当场按实缴清；
  - 记一次灰袍执法（启动他的震慑收敛期，但不记被捕履历）；
  - 销案，玩家声望 +2；
  - 罚金跟着玩家走，交给任一灰袍领主才算交差。
- 结案对话会主动结束遭遇，免得刚缴完罚金的人站在开战界面里。

## 宽限（`GwpFieldArrestBehavior.Grace`）

- 给 72 小时。到期前玩家只有两条路：现在就强办，或者等到期再来；不给「先收一点」。
- 到期只提示一次，不会自动收钱。

## 单挑

- 交给野外切磋流程，见 [`sparring.md`](sparring.md)。等待结果的状态不跨存档，打到一半存档退出等于放弃。
- 他赢：按他开出的条件免罚，案子销掉，案底照留。
  玩家赢：离开决斗场景后，再找他检查财产收款。
- 决斗场景里原版名牌按战役外交给领主着色；这里只把本场对手加上敌方标记，不在战役里宣战（`GwpDuelOpponentMarkerPatch`）。

## 拒捕或玩家动手

- 当场把遭遇转成战斗，不宣战，不把他的王国牵扯进来：灰袍管的是个人，不是国家。（依据：设计 2026-09-11）
- 不能在对话的 consequence 里直接开战（那时对话任务还在跑，会被弹回遭遇界面）。
  先记下要打谁，等对话真正关掉再开打。

## 台词

- 认罚、交不出钱、抗法、回绝等台词，每类有多句备选（`GwpFieldDialogueVoice`）。
- 随机数用自己的 `System.Random`，不消耗玩法用的随机数。
- 同一次遭遇里已经显示过的台词缓存起来，推进一条说辞时只刷新对应的回应。

