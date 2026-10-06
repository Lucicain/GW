# 无领主临时队：船只、口粮与兵员纯化

> 当前状态：用户已实测确认（2026-09-26）；同日补的解散入口还船、还不回去的船销毁、玩家被俘仍还给玩家三条是防线，正常游戏难触发
> 最后验收：`03964e4`（v1.4-r13 构建；2026-09-26 用户确认）
> 已复核至：2026-10-07 核对 v14 反馈会话、原版补兵预算及装备入口、实际 RBMCampaign.dll；军饷与第三方配置不改，v14 原版装备候选保护见 warden-gear.md
> 覆盖源码：`PoliceResourceManager.cs` `PoliceShipModels.cs` `GwpLeaderlessFoodModel.cs`
> 待实测：v14 / RBM 自动换装候选保护见 warden-gear.md；军饷/补兵预算暂不处理（用户裁定 2026-10-07）；船只既有功能无新增待实测。玩家被俘时送信队原地等、不会回来（见 [dispatch.md](dispatch.md)），只有它在这期间被打掉才会走"还给被俘玩家"；由诊断行 `SHIP_LOAN_UNRETURNABLE_DESTROYED` 兜底观察

## 船：只借真船，销毁前退回（2026-09-24 起）

用户裁定：所有船都是真船，要么领主借、要么玩家借；借不到时照常派出、只走陆路；
只有出发点本身在海上（出借方在海上）又借不到船时才不派。不凭空生成任何船。

**为什么不能凭空生成**：NavalDLC `NavalShipDistributionCampaignBehavior` 在队伍解散
（`OnPartyDisbanded`）或在陆上被销毁（`MobilePartyDestroyed`）时，先把船分给同家族的
领主队，分不出去的按 `ShipCostModel.GetShipTradeValue` 折成金币发给家族族长（玩家家族
还会弹出“从船只中回收了 X 金币”）。凭空的船因此变成白来的钱或白来的船。

| 临时队 | 出借方 | 在海上借不到时 |
|---|---|---|
| 随行练兵队 `gwp_cohort_` | 练兵官 | 不建练兵队，订单暂用练兵官名册 |
| 立即追截队（`TrySpawnImmediateCaseInterceptor`） | 来源领主 `sourceParty` | 不派 |
| 延迟纠察队 `gwp_enf_delay_` | 任务领主 `sourcePoliceParty`，没有则最近灰袍领主 | 在城镇出生，不涉及 |
| 纠察队 `gwp_patrol_`、招募使者队 `gwp_recruit_` | 最近的、在陆上、有空闲船的灰袍领主 | 在城镇出生，不涉及 |
| 送信队 `gwp_dispatch_` | 玩家 | 不派，提示“你在海上，没有多余的船可分” |

- **借**：`PoliceResourceManager.LendShips`。出借方至少留一条船；只借空闲船里交易价最低的，
  按计划人数每 50 人一条（练兵队按订单人数）。出借方正在打仗时不借。只要找到出借方，无论借到与否都登记
  出借方（存档键 `GWPP_ShipLoanBorrowers` / `GWPP_ShipLoanLenders`）。
- **还**：登记过的临时队身上**所有**船（借来的和路上缴获的）在销毁前交回出借方。出借方不能接收
  （已不在、在海上交战中、正在解散）时，交给最近的、能接收的灰袍领主；接收条件与 NavalDLC
  `CanSendShipToParty` 同一口径。出借方是玩家时，玩家被俘（主队不活跃）也照样还给玩家——
  原版被俘不动主队的船。
- **还不回去就销毁**：找不到接收方的船、以及没登记的无领主队（`GwpCommon.IsLeaderlessDutyParty`）
  身上的船，在陆上由还船前缀 `DestroyShipAction.Apply` 销毁并写 `SHIP_LOAN_UNRETURNABLE_DESTROYED`；
  在海上留给原版 `RemoveParty` 销毁（NavalDLC 在海上不分船、不折钱）。`DestroyShipAction.Apply`
  不给任何补偿（只有 `ApplyByDiscard` 触发船匠特长），也不经过分船/折金币。
- 不要让无领主队的船落进 NavalDLC 分船/折金币。（依据：用户裁定 2026-09-26；C#反编译 v1.4.8）
- 海战战败：原版在战斗结算 `LootDefeatedPartyShips` 里先把败方的船分给赢家或毁掉（赢家可能分到
  折价金币，这是赢家的战利品），之后才销毁队伍，还船前缀这时已经无船可还。陆上战败不缴获船，
  船留在败方，由还船前缀交回——与原版领主陆上战败时船回流本家族一致。
- **还船点有两个**。`GwpShipLoanReturnPatch` 挂 `DestroyPartyAction.ApplyInternal`：模组拆队
  （全部走 `DestroyPartyAction.Apply`）和原版战后销毁都经过它，NavalDLC 分船/折金币挂在它随后
  发出的 `MobilePartyDestroyed` 上。`GwpShipLoanReturnOnDisbandPatch` 挂
  `DestroyPartyAction.ApplyForDisbanding`：这一路先发 `OnPartyDisbanded`（NavalDLC 同样在这里
  分船/折金币）再进 `ApplyInternal`。
- 不要只留 `ApplyInternal` 一个还船点，解散那一路会晚一步。（依据：C#反编译 v1.4.8，2026-09-26）
- 原版走解散这一路的前提是队伍 `IsDisbanding`，进入目标城镇时由 `EnterSettlementAction` 触发。
  会把无英雄队标成解散中的只有读档时"军团里无领队的成员"，灰袍临时队不进军团，所以目前是防线。
- NavalDLC 折出的金币给 `ActualClan.Leader`：灰袍临时队进灰袍族长，送信队进玩家（玩家家族另乘
  出售折扣并弹提示）。在海上被销毁时 NavalDLC 不分船也不折钱。
- 海战战败时船按原版规则被赢家缴获（真船，正常损失）；不再有“战败销毁”补丁。
- 有船即具备海上通行能力（NavalDLC `NavalPartyNavigationModel` 看 `Ships.Count`）；船数只影响
  海上航速（原版超载/缺员减速）。
- 灰袍家族的船（`PoliceShipModels`）：不受航行磨损（`GetHourlyShipDamage` = 0，
  安全航行时长无限），战役航速固定为 3.5。
- **常驻领主不送船，走原版购船与缴获**：原版 NavalDLC `ShipTradeCampaignBehavior` 按家族
  理想船数买卖船；原版只在本家族封地里挑城，灰袍无封地，由 `GwpLandlessShipTradePatch`
  改为任意非敌对城镇，买不买、买哪条、卖不卖仍由原版判断。原版家族船数、买卖与家族内换船
  都只看 `clan.WarPartyComponents`（领主队）；借出去的船在借期内不计入出借方。

## 无领主队全表

按源码里全部建队调用核对（2026-09-25）：

| 前缀 | 队伍 | 人从哪来 | 结束时 |
|---|---|---|---|
| `gwp_cohort_` | 随行练兵队 | 练兵官名册 | 退回练兵官；练兵官缺席时交最近的灰袍领主 |
| `gwp_enf_delay_`（立即追截） | 追截队 | 来源领主名册 | 回来源领主；来源不能再带队时改交最近的灰袍领主（接收人记入状态，不再换） |
| `gwp_enf_delay_`（延迟） | 延迟纠察队 | 模板生成 | 进城销毁 |
| `gwp_patrol_` | 纠察队（含押送被俘玩家） | 模板生成 | 进城销毁 |
| `gwp_recruit_` | 招募使者 | 模板生成（重步兵），owner 为族长 | 回出发城销毁 |
| `gwp_dispatch_` | 玩家送信队（玩家家族） | 玩家名册 | 回玩家交割 |

判定入口 `GwpCommon.IsLeaderlessDutyParty`（按前缀，且 `LeaderHero == null`）。灰袍逃兵
（`GreyWardenDesertersCampaignBehavior`）是劫匪队，不在此列。结算队 `gwp_bounty_collect_`
已退役，只剩读档解散。"交最近的灰袍领主"用 `GwpCommon.FindNearestGreyWardenLordParty`
（活着、未被俘、不在战斗中的灰袍领主队）。

## 口粮：一律不吃饭

用户裁定 2026-09-25：上表六种一律不吃饭。

- `GwpLeaderlessFoodModel` 包在已注册的食物模型外面（`AddModel<T>` 设 BaseModel；NavalDLC
  的食物模型也这样包原版），只让 `DoesPartyConsumeFood` 对上表返回 false。原版每日进食
  由它把门，关掉后 `RemainingFoodPercentage` 不再变，队伍不会进入饥饿。
- 不得把伙食量改成 0，除零会把粮食天数变成异常整数并污染军团算粮。（依据：C#反编译 v1.4.8）
- 出生时按计划人数配 20 日谷物，只配一次（`OutfitTemporaryDutyParty` → 私有的
  `ProvisionTemporaryDutyParty`；练兵队按订单人数）。这份粮不减少，只为让原版看到
  "粮够吃"：`AiPatrollingBehavior` 在 `GetNumDaysForFoodToLast() <= 6` 时不给巡逻候选。
  （依据：C#反编译 v1.4.8）
- 送信队不带口粮、不带盘缠，出发不再查粮；只进城卖俘虏，不买粮。
  它不带粮，所以原版永远不给它巡逻候选；它办差期间始终有我们的意图，巡逻候选本来也被压到最低。
- 原版不会因此多出新欲望：无领主队的进城打分只有归并分和固定 1.6，与粮无关；原版买粮
  （`PartiesBuyFoodCampaignBehavior`）要求 `LeaderHero != null`。（依据：C#反编译 v1.4.8）
- 旧档里在升级前已经在饿的队，饥饿状态冻结，会一直每天有 25% 普通兵转伤；按"不做存档兼容"不补正。
- 原版饥饿规则备查：看 `RemainingFoodPercentage < 0`，只在每日进食时刷新；饥饿的移动队
  每天 25% 普通兵转伤。无领主自定义队不走原版逃兵。

## 兵员纯化

有灰袍领主的队伍每 6 小时一次、以及参战结束后：非灰袍兵按空余编制换成灰袍新兵，
超出编制的放走。无领主临时队不纯化——随行练兵队的外来兵由练兵队退回练兵官后在
练兵官队里纯化，见 [troop-orders.md](troop-orders.md)。

## v14 反馈环境中的兵力与装备

用户 2026-10-07 反馈部队数量少、武将换装备。当前 live 为 v14 工作树，不能套用 main 的火誓者零军饷或信众村招兵规则；RBM 配置与工资保持不变；装备候选保护见 warden-gear.md。

- AI 日志六位创始领主均出现，不能据此说开不了六支队伍。最近样本（00:36:58）弥瑟 34、约珥 41、圣铎 56、晨曦 16、暮光 105 人，包含伤员；梵蒂最后样本（00:36:04）8 人。梵蒂和暮光在 00:36:00～00:36:04 都记录战败/逃亡，因此这些样本不能证明六队在最后时刻都仍存活，兵力下降也不能全归因于预算。伤员分别另列，不从 men 重复扣除。
- 原版 MobileParty.GetAvailableWageBudget = PaymentLimit - TotalWage - 5；AiVisitSettlementBehavior 只在可用预算 >0 时评估招兵，RecruitmentCampaignBehavior 同样以预算限制人数。晨曦 16 人日薪 3878、预算 2540；约珥 41 人日薪 3706、预算 2540；圣铎 56 人日薪 6212、预算 2540，均无招兵预算。公库仍有 35964，私人金币足不能解除工资预算门槛；食物约 8～15 天、unpaidWages=0，未从日志证实饥饿或欠薪逃兵。
- 本机 RBMCampaign.dll（SHA-256 f0fe49608c5cca5b3186c2ac0387070ca329eaf4c0bf0fb1c7b0f8cda335e871）已反编译：TierBasedWageModel 对六阶步兵设日薪 240、骑兵 480；LordPartyWageLimit 在原版家族财务评估后将预算乘 10，仍可能跟不上灰袍高阶兵。原版预算按族长金币计算，需协调经济规则，不能只让私人的钱更多。
- 原版 1.4.8 的 PartiesBuyHorseCampaignBehavior.OnSettlementEntered 只对队伍背包买卖马匹、出售非食物战利品，不修改领主 BattleEquipment。原版也不是任何情况下都不改装备：成年生成、同伴封为领主、无效装备修复，以及提交锻造订单时满足严格比较条件后替换订单领主的武器均有入口。没有日常进城购买高阶装备的机制；RBM 的同名事件后置补丁新增了它。（依据：C# 反编译 2026-10-07）
- 同一 DLL 的 LordEquipmentUpgrade 在 PartiesBuyHorseCampaignBehavior.OnSettlementEntered 后执行：符合条件的非玩家家族领主进非敌城镇可从城镇/背包按 Tierf 换更高阶装备，原装备退入背包，没有灰袍排除。实际配置 `C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml` 中 RBMCampaign.Enabled=1、LordEquipmentUpgradeEnabled=1。因此有明确冲突入口；现有监控没有逐件换装记录，且 RBM 自身 SpoilsLoggingEnabled=0，不能断言用户看到的每次换装都由该入口造成。
- 本模组现有 EnsureCommanderLoadout 只在成年和读档时修生成角色装备，不在城镇访问后重置六位创始成员；未观察到本会话 ADULT_COMMANDER_LOADOUT_APPLIED。没有证据指向新加入的离国流程改装备或删除兵员，原版离国本身不重置装备或名册。（依据：源码、C# 反编译、实机诊断 2026-10-07）

灰袍自动换装候选保护已在 v14 部署，见 warden-gear.md。钱与军饷暂不处理，用户说明后续工资归零；没有把 main 的零军饷带回 v14。（依据：用户裁定 2026-10-07）

原版非玩家家族每日财务评估按族长金币 G 设置每队工资上限：普通家族 A=30000、B=90000，小势力家族 A=10000、B=30000。G≤A：200+400×(G/A)²；A<G≤B：600+600×(G-A)/(B-A)。这两档族长自己的队乘 1.5，再向下截成整数。G>B：直接 MaxWagePaymentLimit（原版 DefaultPartyWageModel 为 10000），该分支不再乘 1.5。可招兵预算仍为限额-当前日薪-5。本机 RBM 在该评估完成后将各非玩家队限额乘 10，因此原版 254 可成为日志里的 2540。不会按每位领主各自金币计算；工资归零后是否补满仍另受兵源、容量和 AI 选择影响。（依据：1.4.8 ClanVariablesCampaignBehavior / MobileParty、实际 RBMCampaign.dll C# 反编译 2026-10-07）
