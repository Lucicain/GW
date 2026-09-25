# 无领主临时队：船只、口粮与兵员纯化

> 当前状态：已部署待实测（借真船；无领主队一律不吃饭）
> 最后验收：未建立检查点
> 覆盖源码：`PoliceResourceManager.cs` `PoliceShipModels.cs` `GwpLeaderlessFoodModel.cs`
> 待实测：送信队回来不再多出“船折金币”；玩家在海上且只有一条船时不能派送信队；练兵队/纠察队向灰袍领主借船后，解散时船回到出借领主；新练兵队、送信队、招募使者长时间在外不再饿伤

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
  按计划人数每 50 人一条（练兵队按订单人数）。出借方正在打仗时不借。无论借到与否都登记
  出借方（存档键 `GWPP_ShipLoanBorrowers` / `GWPP_ShipLoanLenders`）。
- **还**：`GwpShipLoanReturnPatch` 前缀挂在原版 `DestroyPartyAction.ApplyInternal`——模组拆队、
  原版战后销毁、解散都经过它，而原版分船/折金币挂在它随后发出的事件上。登记过的临时队
  身上**所有**船（借来的和路上缴获的）交回出借方；出借方已不在，交给最近的灰袍领主。
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
