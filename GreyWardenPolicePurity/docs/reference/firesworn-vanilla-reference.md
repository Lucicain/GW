# 火誓者所用的原版接口证据

以下是原版行为与挂点资料，不是模组当前行为。模组规则从 [火誓者总览](../state/firesworn.md) 进入；游戏版本变化时复查原版结论。

- 招兵：兵出自要人的志愿兵格子，**每位要人固定 6 格**（`Hero.VolunteerTypes = new CharacterObject[6]`，招兵循环按 6 取模），
  兵种由 `VolunteerModel.GetBasicVolunteer` 决定。每天每格按概率补一个兵：0.75 × p^(格序+1)，p 在 0.7–0.9 之间，
  所以一位要人一天大约补 2–3 个兵。要多出兵，只能加要人或者把概率拉满。（依据：C#反编译）
- AI 领主进聚落时（`RecruitmentCampaignBehavior.OnBeforeSettlementEntered`）最多跑 7 轮，每轮每位要人招 1 个，
  条件是部队没满、钱够、工资预算够。招兵是进门那一刻的事；待在村里每小时只查雇佣兵（`CheckRecruiting`）。（依据：C#反编译）
- 要人生成：每个聚落每 7 天跑一次 `SettlementHelper.SpawnNotablesIfNeeded`。村子的目标是头人 1 位、乡绅 2 位
  （`DefaultNotableSpawnModel`），按缺额比例掷骰补一位。这条路只补头人和乡绅，**不补传教者**：
  祭司按比例生成要我们自己做。（依据：C#反编译）
- 原版有 `Occupation.Preacher`：`CanHaveRecruits` 允许他出兵，有专用对话；原版不生成，每地目标数为 0。
  村子里的要人都站在通用位置，祭司不需要专门的出生点。（依据：C#反编译）
- 原版会随机删掉 power 低的要人（`NotablesCampaignBehavior.CheckAndMakeNotableDisappear` → `KillCharacterAction.ApplyByRemove`，人是死了），祭司也走这条（用户裁定 2026-10-07）。
  要人死时 power ≥ 100，原版从该文化同职业模板里随机补一位亲属（`OnHeroKilled` → `CreateRelativeNotableHero`），男女都可能。（依据：C#反编译）
- 原版村民队（`VillagerCampaignBehavior`）是常驻的：每村一支，挂在 `Village.VillagerPartyComponent` 上，
  卖完货回村待着，下次出发时从户数里补人（补 n 人扣 (n+1)/2 户）。只有健康人数为 0，或者它已经不是本村登记的那支时，才销毁。
  新建只在没有队、或队伍离村超过 7 天时发生。（依据：C#反编译）
- 领主进不进聚落（`AiVisitSettlementBehavior`，托管代码，反编译即可，不涉及原生层）：
  - 基础分 1.6；不缺人、不重伤时乘 0.16。
  - 伤员占比超过 20% 时，城堡、城镇按伤员比例乘 1–5。
  - 缺粮时城镇、村子都加分；村子只在不在军团里时算。
  - 缺兵（部队没满、有钱有预算）时按可招人数加分。
  - **俘虏只给城堡、城镇加分**：乘 1 + 2×(俘虏数 / 俘虏上限)。这是整数除法，俘虏没满就是 0，满了才一下子 ×3。
  - 村子没有俘虏这一项。

  所以满编的灰袍领主几乎不会进村。（依据：C#反编译；实机诊断 2026-10-05：约 2,500 个领主小时里，85% 满编，进村 0 小时，进城/城堡 100 小时）
- 俘虏去向：
  - AI 部队进入不交战的城堡、城镇，就把普通俘虏和敌对英雄全部卖掉（`PartiesSellPrisonerCampaignBehavior`），
    普通俘虏就此消失，换成赎金。
  - 超出俘虏上限的部分，每人每小时 10% 逃跑（`PrisonerReleaseCampaignBehavior`）。
  - 讲和只放英雄俘虏。

  实机里灰袍领主常带 60–110 名俘虏，进城时一次清空。（依据：C#反编译；实机诊断 2026-10-05）
- 村庄民兵是一支真实队伍，劫掠时站防守方（`RaidEventComponent`）。每日变化 = +0.5 + 户数/400 − 当前民兵×2.5%，
  当前民兵按整支民兵队人数算；被洗劫的村子不涨；减员按比例从各兵种扣（`Settlement.RemoveMilitiasFromParty`）。（依据：C#反编译）
- 进城的那些事原版各自按聚落类型把关（依据：C#反编译）：
  - 卖俘虏只在城堡、城镇（`PartiesSellPrisonerCampaignBehavior`）；
  - 卖战利品只在城镇，按城镇金库买得起多少卖多少（`PartiesSellLootCampaignBehavior`）；
  - 买粮城镇、村子都行（`PartiesBuyFoodCampaignBehavior`）；
  - 疗伤城堡、城镇加速，刚离开的村子也有一点加成（`DefaultPartyHealingModel`）。

  村子的金库每天封顶 1,000（`Village.DailyTick`），买不起战利品。
  原版村民队会把村子货仓（`Settlement.ItemRoster`）里的东西拉去贸易城镇卖掉（`SellGoodsForTradeAction.ApplyByVillagerTrade`），
  卖得的钱回村后记成村子的贸易税，归村子的领主，不归灰袍。
- 兵种等级 = ⌈(等级−5)/5⌉（`DefaultCharacterStatsModel.GetTier`）：`gwzealot` 2 阶（等级 11），`gwnewrecruit` 3 阶，
  `gwrecruit` 4 阶，重步兵、弓手、骑士 6 阶；原版村民 0 阶、村庄民兵 2 阶、老兵民兵 3 阶。
  原版志愿兵格子：每天每格按概率补人，空格补 `GetBasicVolunteer`，有人的格子按 log2(power/阶)×1% 升一级、到 `MaxVolunteerTier` = 4 阶为止，
  有变动就按等级从低到高重排（`RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`）。（依据：C#反编译）
- 要人势力（`DefaultNotablePowerModel`）：普通 < 100、有影响力 100–200、强大 > 200；初始 20% 在 50–100、60% 在 100–200、20% 在 200–400；
  每天只变零点几，100 以上每天回落 (势力−100)/500。势力 < 100 的要人每天有 (100−势力)/100×2% 的概率被原版删掉
  （`NotablesCampaignBehavior.CheckAndMakeNotableDisappear`）。（依据：C#反编译）
- 逐兵换装：战场上每个兵的装备在 `Mission.SpawnAgent(AgentBuildData)` 时定，可以前置补丁改 `AgentBuildData.Equipment`。
  开源模组 Dynamic Troop Equipment System 就是这么做的：部队有一个物品计数的军械库，开战时按兵种参考装备给每个兵配一套，
  活着的退回、死了的就丢了。战役层的兵仍然只是「兵种 × 人数」，没有个体。
  自动结算不看装备，只看兵种的 `GetPower`（按阶），所以军械库只在有玩家参与的实战里起作用，除非另改模拟结算。（依据：C#反编译；开源代码阅读）
- 战后分赃（`MapEvent.CalculateAndCommitMapEventResults`，`DefaultBattleRewardModel`）（依据：C#反编译）：
  - 民兵、驻军不参与分敌方尸体的装备、敌方背包、敌方手里的俘虏；
  - 在村子里的民兵也不俘虏敌兵。

  所以**村子自己守赢，什么都缴获不到**：打败的敌兵不被俘、装备不掉，背包留在敌方；
  有领主帮守时，这些全归帮守的领主。
- AI 领主从敌方尸体上拿不到装备，只拿钱：每具尸体约 7.25×等级² 的价值 × 1/55。只有玩家能从尸体上捡装备。
  AI 领主背包里的物品只来自敌方部队原本的背包，多是货物，不是兵器。（依据：C#反编译）
- 自动结算每一下伤害走 `CombatSimulationModel.SimulateHit(进攻兵种, 被打兵种, 进攻方部队, 挨打方部队, …)`，
  按两边兵种的 `GetTroopPower` 比值算。能拿到部队，可以按部队或村子的军械库覆盖率给狂信者加成。（依据：C#反编译）
- 物品有 `NotUsableByFemale` / `NotUsableByMale` 标志；灰袍兵男女混编，军械库只配男女都能用的。（依据：C#反编译）
- 洗劫后的锁定是打开村庄菜单时跳到 `village_looted`，不改村子状态；传教期间的锁定照这个做。
  不要改 `VillageState` 来锁村，它会影响生产、民兵、户数。（依据：C#反编译）
- 比武大会：每座城每季只有固定的一周能开赛（城镇编号决定），两次至少隔 15 天，开赛概率 = 城里领主与合格英雄数×10% − 20%，
  最多持续 15 天、第 10 天后每天结束概率 +5%。（依据：C#反编译）
