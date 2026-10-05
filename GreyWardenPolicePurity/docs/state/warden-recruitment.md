# 灰袍招兵：只在信众村招，不领饷

> 当前状态：第一步已部署待实测（2026-10-05）
> 最后验收：未建立检查点
> 已复核至：2026-10-05 加两个补丁：灰袍领主城里不卖普通俘虏、不卖背包东西；招兵连装备带走（见 `firesworn-economy.md`）；2026-10-05 新建
> 覆盖源码：`GwpWardenRecruitment.cs`
> 待实测：见文末

灰袍兵从哪来、发不发饷。信众村、祭司本身见 [`firesworn.md`](firesworn.md)；战后外来兵的纯化见
[`temporary-parties.md`](temporary-parties.md)；练兵与换兵见 [`warden-training.md`](warden-training.md)。

## 规则

- 灰袍领主不招原版兵，只招狂信者，兵只出自信众村。（依据：用户裁定 2026-10-05）
- 纯化保留：战后收进队里的外来兵，仍按空位换成灰袍新兵。（依据：用户裁定 2026-10-05）
- 灰袍兵是信仰势力，不领饷。（依据：用户裁定 2026-10-05）
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
| 城镇、村子里的雇佣兵 | 前置补丁 `RecruitmentCampaignBehavior.CheckRecruiting`，灰袍领主队跳过 |
| 招降俘虏 | `GwpWardenPrisonerRecruitmentModel.ShouldPartyRecruitPrisoners` 对灰袍部队返回 false |

原版还有「小势力在野外凭空招兵」，只给 minor faction，灰袍不是，不受影响。（依据：C#反编译 v1.4.8）

## 招兵意愿回到信众村

- 原版估算「这里能招几个兵」时，不看能不能招到，只数每位要人的前 4 格
  （`AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement`）。
- 后置补丁对灰袍改成：不是信众村一律 0；信众村 = 祭司格子里的人 + 能直接招的狂信者（按部队空位封顶）。
  缺兵的灰袍领主因此只会被信众村吸引。（依据：C#反编译）
- 平均工资返回 0。原版只在工资预算大于 0 时才拿它做除数，结果是正无穷，不限人数。

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
