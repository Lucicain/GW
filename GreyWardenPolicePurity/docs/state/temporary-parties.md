# 无领主临时队：船只与口粮

> 当前状态：原借船规则已验收；共享编号与调兵状态搬运已修复部署待实测
> 最后验收：`03964e4`（v1.4-r13 构建；2026-09-26 用户确认）
> 已复核至：`2a2d075`（2026-10-14）
> 覆盖源码：`PoliceResourceManager.cs` `PoliceShipModels.cs` `GwpLeaderlessFoodModel.cs` `GwpPartyIdentity.cs` `GwpTroopTransfer.cs` `tools/Verify-PartyTransfers.ps1`
> 待实测：练兵/订单/追截/送信归队保留伤兵及累计经验；反复建队不重号；同版存读接续

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

兵员纯化已删（用户裁定 2026-10-07：灰袍照原版招原版兵），见 [warden-recruitment.md](warden-recruitment.md)。

## 共享编号与兵员搬运

- `GwpPartyIdentity.Next` 统一用于火誓者、送信、纠察、招募使者、练兵与两处追截建队。先查询原版 `CampaignObjectManager.FindNextUniqueStringId<MobileParty>`，再核对候选是否实际存在；碰撞时严格递增后缀。原版工厂先以 `quest_party_template_1` 注册，再改 `StringId` 不更新最大后缀，因此原版返回值本身不足以证明改名后的唯一性。公共入口没有持久计数器、随机重试或旧档迁移。（依据：C#反编译 v1.4.8、实际注册表连续建队回归 2026-10-14）
- `GwpTroopTransfer.Move` 在两份实体名册间搬指定兵种：健康兵先搬，`healthyOnly` 不搬伤兵；同时扣入对应伤兵与经验。部分搬运沿用原版 `PartyScreenLogic.TransferTroopToLeaderSlot` 的 `人数×整除后人均经验`，整批带走整个经验池及余数；`MoveBatches` 只协调批次，不另写搬运算法。选兵、额度、编制与经验发放仍由各功能定义。（依据：C#反编译 v1.4.8、实际名册状态守恒回归 2026-10-14）
- 创建追截失败时，按同步调用中记录的来源、人数和精确经验归还；这份记录不跨存档，没有新增持久状态。巡逻新战斗经验供村内的用户裁定仍由 [村庄](firesworn-villages.md) 定义，搬运原有经验不改变该归属。（依据：设计、源码复核 2026-10-14）
- 无实体民兵时，自有守村记录仍只记人数，其经验持有边界见 [村庄](firesworn-villages.md)，不新增另一份经验库。（依据：设计）

整理前源码基线为 `2a2d075`：相关调用文件见本页及练兵、订单、案件、送信、火誓者 state；取回基线对应调用文件并移除基线没有的两份公共入口，再构建部署。基线也会撤去尚未存档的其他相关开发，需按目标差异取回，不整批覆盖工作树。（依据：流程）
