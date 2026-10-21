# 野外执法：肯不肯谈、两层谈判、兑现方式与结案

> 当前状态：强捕进入菜单、确认敌对及原版援军收集时序已反编译核对，援战缺口未修；已部署的俘虏排除和和平投案保持
> 最后验收：`04ff2d6`（2026-10-02 用户确认）
> 覆盖源码：`GwpFieldArrestBehavior.cs` `GwpFieldArrestBehavior.Grace.cs` `GwpFieldArrestLines.cs` `GwpNegotiationPolicy.cs` `GwpNegotiationResistance.cs` `GwpOffenderDesire.cs` `GwpNegotiationChancePatch.cs` `GwpFieldDialogueVoice.cs` `GwpFieldCollectionBarterable.cs` `GwpFieldFineBarterable.cs` `GwpBribeBarterable.cs` `GwpDuelOpponentMarkerPatch.cs`
> 待实测：对方的打算更看性格，有钱的领主不再多半照数全缴；第二层更难谈（实力差不再进第二层），性格越极端越难

玩家在野外拦下自己承办的罪犯之后的全部流程。罚金定价见 [`crime-intake.md`](crime-intake.md)；
交差、查账见 [`player-justice.md`](player-justice.md)。

玩家当面投案与使者投案共用GwpVoluntarySurrender入口；军团、战斗延后及原队原版收尾规则只在[使者投案衔接](case-envoy.md#投案与原版军团退队衔接)定义。没有新增保存字段。（依据：用户批准、生产回归2026-10-20）

## 入口

城内自由住城人物的执法尚未实现，不能把源码旧注释中的“另一套向总督交涉”当作已有入口；无部队旧案缺口统一见[人物追责](case-enforcement.md#藏城目标与战后连续交战)。（依据：用户反馈、入口及总督引用复核2026-10-21）

与承办目标（`GwpPlayerCaseAssignment.HasAuthority`）在野外单独对话；普通悬赏与[赎罪委派](atonement.md)共用入口，赎罪不要求入会。任一方在定居点里或对话目标已是俘虏时不走这条路；动手入口再复核目标非俘虏且仍在原队，防止把扣押队当作犯人队。第三方扣押改走[赎买与撤单](player-commission.md#目标不可追踪时的无责撤回)。取案按对话英雄ID，不拿同队另一嫌犯的案卷；宽限日志和处理结果由公共入口转交所属任务。（依据：源码复核2026-10-15）

玩家谈话对象已被俘时，旧入口会回退到 ConversationParty，动手目标变成扣押者，既污染对话战力比较又引向错误战斗；该入口及动手复核已封住。不调整抗性或说服概率。（依据：实机日志、源码与离线执行2026-10-20）

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
双方区域战力读取原版适配，观察者、参战资格及权重见[协力](case-assistance.md#区域宣战与持续接战)。他名义上属于某军团、还在赶去会合时只算他本人。
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
  - 记一次灰袍执法，登记震慑；和平结局计入执法/被捕履历的规则见[震慑](crime-intake.md#震慑与罪责结算的独立性)；
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

- 对话的AttackConsequence只登记目标和办案动手标记；ConversationEnded后StartBattleNow依次RestartPlayerEncounter、StartBattle、Update，创建MapEvent并进入原版遭遇菜单。这一段没有调用宣战，不等于已经进入战斗场景。（依据：生产源码、本机v1.4.8 C#反编译2026-10-20）
- 不能在对话的 consequence 里直接开战（那时对话任务还在跑，会被弹回遭遇界面）。
  先记下要打谁，等对话真正关掉再开打。

### 菜单确认、离开与附近援军

- 普通野外强捕把玩家设为攻方；原版CanMainPartyLeaveBattleCommonCondition允许攻方离开，军团非主办等额外菜单条件仍适用。MenuHelper.EncounterLeaveConsequence走Finish/FinalizeBattle/LeaveBattle收尾，没有宣战或自动讲和；若此前已经交战，离开不会撤销该战争。（依据：本机v1.4.8 C#反编译2026-10-20）
- 真正点击「进攻」走MenuHelper.EncounterAttackConsequence；「派兵进攻」走EncounterOrderAttack，两者都先ApplyEncounterHostileAction，随后才创建战斗Mission或模拟。普通领主且未敌对、不同势力、未豁免敌对动作时，原版此处扣关系并ApplyByPlayerHostility宣战。当前GwpHostilityRelationPatch仅记关系，不抑制该动作。旧正文所称整条强捕链不牵扯王国与当前实现不符，不能据此修改原版规则。（依据：生产补丁与本机v1.4.8 C#反编译2026-10-20）
- PlayerEncounter.StartBattleInternal在创建MapEvent返回后调用CheckNearbyPartiesToJoinPlayerMapEvent；已有未结束的同一MapEvent时再次调用StartBattle只重新收集附近队伍，不创建第二场。单纯Update/普通encounter菜单初始化（已有Battle）及外交变敌对本身，没有重扫援军调用。MapEventStarted发生在MapEvent构造内部，早于Current._mapEvent接到返回值，不能在该回调里重入StartBattle刷新名单。（依据：本机v1.4.8 C#反编译2026-10-20）
- 原版FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter按战斗位置收集普通野外半径3内的部队；通常要求同海陆侧、未驻城、未在另一战斗/围城中、非木筏、未挂靠，且部队类型合格。CanPartyJoinBattle要求与己方全部参战方不敌对、与对方全部参战方敌对；不检查是否战力占优。没有把范围外援军传送或拉近的移动动作。（依据：本机v1.4.8 C#反编译2026-10-20）
- 通过收集的队伍由原版MapEventSide.AddNearbyPartyToPlayerMapEvent实际写入MapEventSide；PartyBase.MapEventSide同步实际AttachedParties，名义在军团但未合兵者仍须独立满足附近收集条件。名单加入与移动接近不是同一动作。（依据：本机v1.4.8 C#反编译2026-10-20）
- 玩家求援尚缺「确认敌对后、Mission/模拟前，同步本案灰袍并重新收集」的路径，详见[协力已知缺陷](case-assistance.md#普通支援与玩家带路)。仅监听玩家WarDeclared会漏掉原先就已交战的情况，且仅改外交不会自行补齐既有遭遇名单；本轮只调查，没有新增钩子、改变外交或参战行为。（依据：用户要求先调研、源码及本机v1.4.8 C#反编译2026-10-20）

## 台词

- 认罚、交不出钱、抗法、回绝等台词，每类有多句备选（`GwpFieldDialogueVoice`）。
- 随机数用自己的 `System.Random`，不消耗玩法用的随机数。
- 同一次遭遇里已经显示过的台词缓存起来，推进一条说辞时只刷新对应的回应。

