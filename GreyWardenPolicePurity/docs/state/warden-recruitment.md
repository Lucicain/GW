# 灰袍招兵：原版招兵照常，信众村另有两条，灰袍兵不领饷

> 当前状态：已部署待实测
> 最后验收：未建立检查点
> 已复核至：2026-10-07 原版招兵（要人志愿兵、雇佣兵、招降俘虏）对灰袍全部放开，纯化删掉；直接招狂信者改为只招守村队现有的
> 覆盖源码：`GwpWardenRecruitment.cs`
> 待实测：见文末

灰袍兵从哪来、发不发饷。信众村、祭司本身见 [`firesworn.md`](firesworn.md)；练兵与换兵见 [`warden-training.md`](warden-training.md)。

## 规则

- 灰袍领主照原版招兵：要人的志愿兵、城里的雇佣兵、招降俘虏全部放开；纯化删掉，原版兵留在灰袍队里。
  （依据：用户裁定 2026-10-07，取代 2026-10-05 的「只招狂信者、兵只出自信众村」「纯化保留」）
- 在信众村，祭司格子和直接招狂信者两条都留；直接招的是守村队里现有的火誓者，不凭空从村民里变出来。（依据：用户裁定 2026-10-07）
- 现有火誓者不能被招完，地方要留足抵抗入侵的力量。（依据：用户裁定 2026-10-07）
- 灰袍兵不领金币工资；后续改消耗灰袍声望，费率及扣费规则尚未指定。原版兵照原版领饷。（依据：用户裁定 2026-10-05、2026-10-07）
- 玩家招灰袍兵现在只走练兵官订单（`troop-orders.md`），不能在祭司那里招。
  以后可以在信众村招，门槛更高，例如声望 40 以上，具体数值未定。（依据：用户裁定 2026-10-05）

## 原版招兵

灰袍领主和原版领主一样招兵，我们不拦（依据：C#反编译 v1.4.8 `RecruitmentCampaignBehavior.CheckRecruiting`、`RecruitVolunteersFromNotable`、`HeroHelper`）：
- 进门那一刻招：部队金币要 > 50 + 20 × min(150, 人数)；非族长另要家族金币 > 50 + 族长队工资 + 族长队人数 × 40；
- 每个兵付招兵费、工资预算够才招；能招到要人的第几格看关系；
- 城里有雇佣兵时按金币、空位概率雇；招降俘虏照原版的服从度。

原版兵在灰袍队里照原版领饷、不吃军械库的装备，灰袍兵专属的战场规则（援军、死战不退等）也不管他们。
以前的纯化（每 6 小时、每场战斗后把非灰袍兵按空位换成灰袍新兵）已删。

## 信众村的两条

1. **祭司的志愿兵格子**：格子里是守村队里最精锐、配装最好的灰袍兵；格子数、补人概率、排序都照原版。
   招走的人从守村队里扣，连同他那一套装备。只有灰袍家族成员能招，驻军和玩家都不能。细节见 `firesworn-economy.md`「祭司的势力与格子」。
2. **直接招狂信者**（`gwzealot`）：灰袍领主进信众村，用部队空位直接招守村队里现有的狂信者，不花钱。
   - 他们离开村子，户数照原版村民队补人的算法扣：n 人扣 (n+1)/2 户（`VillagerCampaignBehavior.AddVillagersToParty`）；
   - 每人带走他那一套装备；村子遭劫或战斗中时不招；
   - 祭司格子先招（进门时原版先跑），空位剩下的才直接招。

两条加起来，守村队至少留一半、不少于 5 人守村（`GuardKeptFromPriestSlots`，试玩值）：能走的人数先扣掉格子里占着的，剩下的才给直接招。
（依据：用户裁定 2026-10-07；实机诊断 2026-10-07：以前直接招从村民里出、不看火誓者人数，一座 220 户的村能招 140 人）

## 招兵意愿

- 原版估算「这里能招几个兵」时不看招不招得到，只数每位要人的前几格（同势力 4 格、否则 2 格），平均工资是整数除法
  （`AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement`）。灰袍照原版数；
  信众村再加上能直接招的狂信者（按部队空位封顶）。规则在 `GwpWardenVisitDesire`，见 `warden-ai.md`「进聚落的意愿」。
- 领主正在这座信众村或刚走到村口时不加：招兵只在进门那一刻，站在村口的招兵分只会让她原地待命。（依据：C#反编译；实机诊断 2026-10-07）
- 平均工资至少报 1：格子里全是不领饷的灰袍兵时原版平均是 0，`MathF.Floor(预算/0)` 转出 -2147483648，招兵意愿整个变成负数。
  （依据：C#反编译、运行时反射 2026-10-07）

## 不领饷

- `GwpWardenWageModel`：
  - 单兵工资：灰袍兵为 0；
  - 部队总工资：把灰袍兵从名册里拿掉，剩下的交给原版算，英雄工资和各项 perk 照原版。
- 原版总工资是逐格读 `TroopWage` 累加的，所以只能拿掉人，不能事后减。
  不挂部队的名册加英雄没有副作用（`TroopRoster.AddToCountsAtIndex`）。（依据：C#反编译）
- 玩家部队里的灰袍兵同样不领饷。

## 灰袍领主不在城里卖俘虏和东西

普通俘虏、背包里的东西留着交给信众村（`GwpWardenKeepPrisonersPatch`、`GwpWardenKeepLootPatch`，见 `firesworn-economy.md`）。
俘虏也可能在路上被原版招降。灰袍领主什么时候进信众村见 `warden-ai.md`「进聚落的意愿」。

## 开局

开新档时，灰袍的初始家乡村（`Clan.InitialHomeSettlement`，即 `village_EN5_1`）就是第一座信众村：照重建给 5% 狂信者，再拉起一位祭司。
这是我的取舍，用户未裁定。灰袍补兵主要走原版招兵；信众村的两条随守村队长大才多起来。

## 待实测

- 灰袍领主照原版去城里村里招兵、雇佣兵、招降俘虏，队里留着原版兵；灰袍家族工资随原版兵增加。
- 信众村里直接招的狂信者从守村队里扣，守村队不低于一半、5 人；户数按招走的人数减少。
- 祭司格子里的兵种都来自守村队。

## 回退

撤这一提交回到「只招狂信者 + 纯化」那一版（`83e9b39`）。

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
