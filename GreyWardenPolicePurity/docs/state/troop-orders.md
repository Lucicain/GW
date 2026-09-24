# 玩家练兵订单与随行练兵队

> 当前状态：已部署待实测（双向换兵、练兵队升级只走订单分支、账簿与调货按订单名册计数）
> 最后验收：未建立检查点
> 覆盖源码：`GreyWardenTroopRequestBehavior.cs` `GreyWardenTroopRequestBehavior.Cohort.cs` `PolicePartyTroopUpgradeModel.cs`
> 待实测：小周存档类卡单能否自行恢复并进入交付；新订单练兵队不再练出非订单分支；账簿人数与实际交付一致。本机无法加载小周原档（缺其他模组），需用本机存档复现

本文件只写玩家向灰袍练兵官订兵这条线。灰袍领主日常练兵、兵种配比取向
（`GreyWardenTrainingBehavior`）尚未提取，见 [README](README.md)。

## 订单阶段

`PlayerTroopOrderStage`：`None=0`、`Training=1`、`Delivering=2`。存档键 `GWPP_PlayerTroopOrder*`。

- 有在办订单时不能再下新单；士兵派遣下单另要求没有在途练兵使者。订单卡住会连带
  让这些入口消失。
- 订单心跳：拉起/续上随行练兵队 → 数练兵队里**健康的目标兵**：
  不足则 `Training`（调货、补员、加经验、拆编重训）；够数则转 `Delivering`，
  练兵官主队去找玩家交付。
- 交付对话要求 `Delivering`、正确练兵官、足量目标兵。

## 随行练兵队

- 订单的兵装在无领主的随行练兵队里，跟随练兵官；练兵官手上至少留
  `CohortTrainerFloor = 15` 人。队伍丢失时重新拉起并提示玩家“路上折了”；
  空队会被原版清除，所以练兵官无人可拨时不建队。
- **“有用的人”**：灰袍、非英雄，且是目标兵、能升成目标兵，或是目标兵的下游
  （可拆编重训降回）。
- **补员**：空位 = 订单数 − 练兵队里有用的人数；从练兵官名册按“成品 → 能练上去的
  （低级优先） → 下游老兵”取健康兵。
- **退回（2026-09-23 新增）**：练兵队里不是“有用的人”的——练成别的分支的灰袍、
  战后收进来的外来兵——整批（含伤员）退回练兵官。外来兵在有领主的练兵官队里由
  `PoliceResourceManager` 纯化转成灰袍新兵，之后可再被补进练兵队。
  只有补员后练兵队里仍有有用的人时才退，避免练兵队变空被原版清除。
  诊断 `PLAYER_TROOP_ORDER_COHORT_FED` 带 `moved` / `returned`。
- 加经验：每 `PlayerOrderXpIntervalHours = 6` 小时，给练兵队里能升成目标的兵
  每人 `PlayerOrderXpPerTroop = 1000` 经验；能练上去的人不够时，每次最多拆编重训
  `PlayerOrderDowngradePerInterval = 4` 名下游兵。升级本身由原版升级器决定。

## 升级、账簿与调货

- **练兵队升级只走订单分支**：`PolicePartyTroopUpgradeModel` 先问
  `GreyWardenTroopRequestBehavior.CohortBranchServesOrder`：对随行练兵队里能练成订单兵种的兵，
  通往订单兵种的分支权重 1、其余 0；原版加权随机因此只会选订单分支。不是练兵队、
  或这个兵本来就练不成订单兵种时返回 null，交还原有规则（有取向的领主给原版、
  无取向的灰袍队三分支等权）。练兵官本人仍靠 `SteerUpgradePreference` 让位订单。
- **账簿** `GetPlayerOrderSnapshots` 的 `ReadyCount` 数 `ResolveOrderPool`（有练兵队数练兵队，
  否则数练兵官），与完成判定和交付同一名册。
- **调货** `AdvanceStockCollection` 的“已够数”判定与 `ExchangeStockAtRendezvous` 的缺额，
  都按 `CountOrderedOnHand` = 练兵官健康目标兵 + 练兵队健康目标兵。

## 海上

随行练兵队出生时向练兵官借真船（按订单人数），解散时退回；练兵官在海上又借不出船时先不建练兵队，
订单暂用练兵官名册。见 [temporary-parties.md](temporary-parties.md)。
**练兵官是领主，不送船**，靠原版购船与海战缴获获得船只（见 temporary-parties.md）；
恰好无船时，玩家在海上仍收不到交付，要等玩家上岸。

## 小周存档证据

见 [training-feedback.md](training-feedback.md)：订单 `gwheavyinfantry`×80 卡在 `Training`
约 76 天，练兵队 71 重步兵 + 5 灰袍弓手 + 4 外来兵占满 80 个名额。
