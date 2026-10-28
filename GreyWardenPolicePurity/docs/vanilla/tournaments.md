# 比武大会：何时开、何时结、怎么算胜负

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.TournamentGames/TournamentCampaignBehavior.cs`（`ConsiderStartOrEndTournament`）、
> `TournamentManager.cs`（`ResolveTournament`、`SimulateTournament`）、`FightTournamentGame.cs`、
> `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultTournamentModel.cs`、
> `SandBox/SandBox.Tournaments.MissionLogics/TournamentFightMissionController.cs`（`PrepareForMatch`、`Simulate`）、
> `NavalDLC/NavalDLC.GameComponents/NavalDLCTournamentModel.cs`
> 模组接入：[灰袍使者](../state/case-envoy.md)「单挑：比武大会」

## 怎么运作

**1. 后台大会不是一场持续的战斗。** 大会只是挂在城镇上的一个 `TournamentGame` 对象，没人进场时不跑任何任务场景。
`TournamentCampaignBehavior` 在每个城镇的每日结算（`DailyTickSettlementEvent`）里调 `ConsiderStartOrEndTournament`。

**2. 每日检查的顺序。**
- 本行为在这座城上次新建大会不到 15 天：整个检查直接返回，既不新开也不结束。
- 已有大会且创建满 `RemoveTournamentAfterDays`（`FightTournamentGame` 为 15）天：`ResolveTournament` 打完它。
- 没有大会：按 `GetTournamentStartChance` 掷骰新开。被围城为 0；城镇 `StringId` 哈希模 3 须等于当季第几周，否则为 0；
  否则为 0.1 ×（城里领主部队数＋城里无部队且已成年、单手或双手技能超过 100 的英雄数）− 0.2。
- 已有大会且未满 15 天：按 `GetTournamentEndChance` 掷骰结束，概率为 max(0, (创建以来天数 − 10) × 0.05)，
  所以 10 天内自然结束的概率为 0。

**3. 无人进场的结算。** `ResolveTournament`：城被围则取消大会；否则取参赛者（不含玩家），
`SimulateTournament` 给每个英雄参赛者加技能经验，再按
`GetTournamentSimulationScore × (0.75 + 0.25 × 随机)` 取最高者为冠军，只得出冠军，不排其他名次。
分数为（英雄 1、非英雄 0.4）×［max(单手, 双手, 长杆) ＋ 运动 ＋ 骑术］× 0.01。
`NavalDLCTournamentModel` 对这个分数只委托回基础模型。

**4. 玩家进场后跳过的比赛。** `TournamentFightMissionController.Simulate` 用每个活着的参赛者的
`CharacterObject.GetSimulationAttackPower(…, MatchEquipment)` 计算攻防，不创建实际 Agent，因此也没有命中、踢击、击倒之类事件。
它和第 3 步的整场分数是两套算法。

**5. 实际演出的比赛。** `PrepareForMatch` 按城镇文化与队伍人数模板给每人发 `MatchEquipment`（武器与坐骑），
再从 `GetParticipantArmor` 取护甲，把 5～9 号槽（护甲）复制进去；比赛模式下 `GetParticipantArmor` 返回参赛者自己的
`RandomBattleEquipment`。生成 Agent 时显式传 `MatchEquipment`。所以原配的武器、盾和坐骑通常被比赛模板替代，护甲则来自参赛者本人。

## 容易想错的地方

- 「大会开着」不等于有人在城里打：后台大会只在每日检查时可能被一次性结算（第 1、2 步）。
- 本行为在某城新开大会后，15 天内连结束检查都跳过（`ConsiderStartOrEndTournament` 开头按 `_lastCreatedTournamentDatesInTowns` 返回），第 15 天被强制结算；
  只有那座城不在这张表里或上次新建已满 15 天时，别处加的大会（例如模组用 `AddTournament` 新开的）才会在第 11～15 天按结束概率提前结束。
- 后台结算只有冠军，没有第二名；要比较两个都没夺冠的人，只能自己再算（第 3 步）。
- 比赛里「护甲被剥掉」不对：剥掉的是武器、盾和坐骑，护甲 5～9 号槽保留本人的（第 5 步）。
- 跳过比赛的结果（第 4 步）和后台整场结算（第 3 步）不是同一个公式，不能互相套用。
