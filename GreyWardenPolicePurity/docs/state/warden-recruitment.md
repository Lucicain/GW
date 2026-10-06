# 灰袍招兵：只在信众村招，不领饷

> 当前状态：已部署待实测；经济暂不调整（用户裁定 2026-10-07）
> 最后验收：未建立检查点
> 已复核至：`58195c6`（2026-10-07；共享判断改调 GwpStrength / GwpCommon / GwpWardenDuties / 案件统一入口，本文件行为不变）
> 覆盖源码：`GwpWardenRecruitment.cs`
> 待实测：见文末

灰袍兵从哪来、发不发饷。信众村、祭司本身见 [`firesworn.md`](firesworn.md)；战后外来兵的纯化见
[`temporary-parties.md`](temporary-parties.md)；练兵与换兵见 [`warden-training.md`](warden-training.md)。

## 规则

- 灰袍领主不招原版兵，只招狂信者，兵只出自信众村。（依据：用户裁定 2026-10-05）
- 纯化保留：战后收进队里的外来兵，仍按空位换成灰袍新兵。（依据：用户裁定 2026-10-05）
- 灰袍兵不领金币工资；后续改消耗灰袍声望，费率及扣费规则尚未指定。（依据：用户裁定 2026-10-05、2026-10-07）
- 玩家招灰袍兵现在只走练兵官订单（`troop-orders.md`），不能在祭司那里招。
  以后可以在信众村招，门槛更高，例如声望 40 以上，具体数值未定。（依据：用户裁定 2026-10-05）

## 两条兵源

1. **祭司的志愿兵格子**：
   - 起步是灰袍新兵（`gwnewrecruit`，3 阶）；
   - 每天 6 格全部补满（原版一位要人一天约补 2–3 个）；
   - 格子里的兵每天另有 5% + power/2000 的概率升一级，能一直升到兵线尽头。原版只按 log2(power/阶)×1% 升，到 4 阶就停。
   
   只有灰袍家族成员能招，驻军和玩家都不能。原版 AI 在进门时把格子招空（`RecruitmentCampaignBehavior.OnBeforeSettlementEntered`）。（依据：C#反编译）
2. **直接招最低级狂信者**（`gwzealot`）：灰袍领主进信众村，用部队空位直接招，不花钱，扣户数。
   - 招 n 人扣 (n+1)/2 户，照原版村民队补人的算法（`VillagerCampaignBehavior.AddVillagersToParty`）；
   - 户数不往 150 以下扣；
   - 村子遭劫或战斗中时不招；
   - 祭司格子先招，空位剩下的才招狂信者。（依据：C#反编译；数值是试玩值）

## 关掉的原版补兵路

| 原版路子 | 怎么关 |
|---|---|
| 普通要人的志愿兵 | `GwpFireswornVolunteerModel.MaximumIndexHeroCanRecruitFromHero`：灰袍买非祭司、非灰袍买祭司，都返回 0 |
| 城镇、村子里的雇佣兵 | 前置补丁 `RecruitmentCampaignBehavior.ApplyRecruitMercenary`，灰袍领主队跳过。不能挡整个 `CheckRecruiting`：领主从要人那里招兵也在它里面（依据：C#反编译；实机诊断 2026-10-06：挡整段后祭司格子一周没人招） |
| 招降俘虏 | `GwpWardenPrisonerRecruitmentModel.ShouldPartyRecruitPrisoners` 对灰袍部队返回 false |

原版还有「小势力在野外凭空招兵」，只给 minor faction，灰袍不是，不受影响。（依据：C#反编译 v1.4.8）

## 招兵意愿回到信众村

- 原版估算「这里能招几个兵」时，不看能不能招到，只数每位要人的前 4 格
  （`AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement`）。
- 后置补丁对灰袍改成：不是信众村一律 0；信众村 = 祭司格子里的人 + 能直接招的狂信者（按部队空位封顶）。
  缺兵的灰袍领主因此只会被信众村吸引。（依据：C#反编译）
- **已知缺陷**：灰袍的平均候选工资返回 0 时，原版 `MathF.Floor(预算/平均候选工资)` 把正无穷转成 int，真实 .NET Framework 运行时得到 -2147483648，不是「不限人数」。零成本候选要改成按可用兵源和空位明确计算。（依据：C# 反编译、运行时反射 2026-10-07）

## 不领饷

- `GwpWardenWageModel`：
  - 单兵工资：灰袍兵为 0；
  - 部队总工资：把灰袍兵从名册里拿掉，剩下的交给原版算，英雄工资和各项 perk 照原版。
- 原版总工资是逐格读 `TroopWage` 累加的，所以只能拿掉人，不能事后减。
  不挂部队的名册加英雄没有副作用（`TroopRoster.AddToCountsAtIndex`）。（依据：C#反编译）
- 玩家部队里的灰袍兵同样不领饷。

## 灰袍领主不在城里卖俘虏和东西

普通俘虏、背包里的东西留着交给信众村（`GwpWardenKeepPrisonersPatch`、`GwpWardenKeepLootPatch`，见 `firesworn-economy.md`）。
缺兵时专程去信众村补兵的差事也在那里。

## 开局

开新档时，灰袍的初始家乡村（`Clan.InitialHomeSettlement`，即 `village_EN5_1`）就是第一座信众村，有一位祭司。
没有它，灰袍在第一次重建之前补不了兵。这是我的取舍，用户未裁定。

## 待实测

- 灰袍领主缺兵时会不会去信众村，进村后有没有招到新兵和狂信者，户数是否按招的人数减少。
- 灰袍领主不再出现原版兵种（纯化日志里外来兵只该来自战后收编）。
- 灰袍家族每日工资是否接近 0。
- 只有一座家乡信众村时，六位领主补兵是否跟得上。

## 回退

撤这一提交即回到「原版征兵 + 纯化」。

## 零金币工资与声望消耗的后续适配

后续灰袍士兵的金币工资归零，改为消耗灰袍声望；消费对象、时点、费率及余额不足时的规则用户还没定，尚未实现。原 v14 玩家包不含火誓者经济。其他模组的文件、配置和方法不改，也不挂钩第三方方法。（依据：用户裁定 2026-10-07）

原版非玩家家族每日财务评估按族长金币 G 设置每队工资上限：普通家族 A=30000、B=90000，小势力家族 A=10000、B=30000。G≤A：200+400×(G/A)²；A<G≤B：600+600×(G-A)/(B-A)。这两档族长自己的队乘 1.5，再向下截成整数。G>B：直接 MaxWagePaymentLimit（原版 DefaultPartyWageModel 为 10000），该分支不再乘 1.5。可招兵预算仍为限额-当前日薪-5。本机 RBM 在该评估完成后将各非玩家队限额乘 10，因此原版 254 可成为日志里的 2540。不会按每位领主各自金币计算；工资归零后是否补满仍另受兵源、容量和 AI 选择影响。（依据：1.4.8 ClanVariablesCampaignBehavior / MobileParty、实际 RBMCampaign.dll C# 反编译 2026-10-07）

“工资归零后就不受原版/RBM 限制”不成立，已查实的限制分别如下：

- **实际工资预算**：若最终 Campaign.Models.PartyWageModel 对灰袍各阶单兵返回 0，GetTotalWage 也不计这些兵，则加兵不再增长 TotalWage、升阶工资差也为 0。RBM TierBasedWageModel 只挂钩 DefaultPartyWageModel.GetCharacterWage；main 的 GwpWardenWageModel 灰袍分支直接返回 0，不调用 base/Inner，因而该分支不被 RBM 重算。普通兵、英雄仍有工资；混编队仍可能超过预算。当前 RBM 在 OnGameStart 只注册 WorkshopModel，没有替换 PartyWageModel。未来最终模型是否生效仍需运行时验收。
- **AI 的经济兵力目标**：原版 PartyBaseHelper.FindPartySizeNormalLimit 按 `(PaymentLimit - 英雄工资项) / Campaign.AverageWage + 1` 算经济人数，再夹到队伍容量并返回比例。Campaign.AverageWage 来自各主文化默认队伍模板，灰袍工资归零不会让其他文化兵的平均工资归零。AiVisitSettlementBehavior 和 RBMRecruitBiasBehavior 都用该目标，仍可能提早降低补兵意愿。原版驻军补员的 CalculateMobilePartySizeLimitWithFoodAndWage 也用预算/全局平均工资，RBMGarrisonRefillBehavior 调用此方法。因此真实日薪为 0 不代表按族长金币推导的 AI 兵力目标自动等于满编。
- **招兵现金门槛**：原版 CheckRecruiting 及 AI 访问评分仍检查 PartyTradeGold > `50+min(150,队伍人数)×20`，非族长队另要求家族金币 > `50+族长队实际工资+族长队人数×40`，并检查不挨饿、队伍空位、要人格子及关系、招募费用等。主线零工资包装保留 Inner.GetTroopRecruitmentCost；工资为 0 不等于完全无需现金招兵。直接由本模组生成狂信者的路径与原版要人格子路径要分别核对。
- **RBM 另计的费用/条件**：实际 RBMCampaign.dll SHA-256 f0fe49608c5cca5b3186c2ac0387070ca329eaf4c0bf0fb1c7b0f8cda335e871。SpoilsPool.DailyMaintenanceCost = round(装备含坐骑价值×人数×TroopMaintenanceFraction)，不读取工资；MaintenanceFinanceLine 通过家族财务每日扣未由战利品覆盖的部分。当前比例 0.005，仍会消耗族长金币并间接压低原版预算。SpoilsUpgradePatches 在自己的升级流程里直接按装备差价/战利品、钱袋、单队日升级限额（默认无自定义限制）、附近补给城镇与经验决定能否升级，不只是读取我们 PartyTroopUpgradeModel 的报价；当前 TroopUpgradeCostMultiplier=1、RequireSupplyTown=1、SupplyRadius=30。RecruitPool 在志愿兵填格后按聚落兵源池删回超额新增格子，当前 rbmCampaignEnabled=1。仅改变工资不会去掉这些限制。
- **零值边界**：RecruitmentCampaignBehavior 的雇佣兵批量、RecruitPrisonersCampaignBehavior 的 AI 招降、GarrisonRecruitmentCampaignBehavior 的自动补员有整数 `预算/单兵工资`，候选为零工资兵时会除零；并非每条路径当前都能碰到灰袍，实施时需按真实可达路径处理。原版 AI 的 `MathF.Floor(预算/平均候选工资)` 将正无穷转 int；真实 1.4.8 TaleWorlds.Library.dll / Windows .NET Framework 运行时反射结果为 -2147483648。因此主线现存的平均候选工资 0 不能用来表示“不限人数”，它可能产生负候选人数，需改成本模组明确的零成本计算。

实现声望经济时须同步处理原版的经济兵力目标、招兵资格及零成本算术，并明确 RBM 独立维护/升级费用与本模组声望维护是否叠加；只改金钱工资不足以完成这套设计。所需修正放在本模组的模型或原版入口实现，遵守不改第三方代码的底线。容量、兵源、经验和正常战败损失仍需保留。（依据：用户裁定；C# 反编译、运行时反射 2026-10-07）
