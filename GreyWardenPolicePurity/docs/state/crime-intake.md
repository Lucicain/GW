# 立案、伤亡计价、震慑与外交封锁

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：`25cf8a0`（2026-10-15）
> 覆盖源码：`PoliceCrimeMonitorEnhanced.cs` `GwpCrimeCategory.cs` `GwpCrimeDesireAuction.cs` `PoliceRaidDeterrenceModel.cs` `PoliceMobilePartyAIModel.cs` `GwpAiDeterrenceState.cs` `GwpAiDeterrenceDialogueCatalog.cs` `GwpPoliceWarReasonService.cs` `PoliceAntiVanillaWarBehavior.cs` `PoliceAntiRecruitmentModel.cs` `PolicePrisonerImmunityBehavior.cs`
> 待实测：无

案件怎么进池、罚金怎么算、被惩戒的领主之后怎么收敛、灰袍与原版外交的边界。
案件进池之后的派单、追捕、结案见 [`case-enforcement.md`](case-enforcement.md)；
野外当面交涉见 [`field-arrest.md`](field-arrest.md)；玩家本人的罪行见 [`player-justice.md`](player-justice.md)。

灰袍独立身份同时受四类原版入国动作入口保护；`PoliceAntiRecruitmentModel.IsGwClan` 供本模组补丁共用，判据不变。`PoliceAntiVanillaWarBehavior` 额外监听读档完成，有王国归属就走原版普通/佣兵退出动作；原宣战监听及合理战争判据不变。入口、实测与待验收项统一见 [灰袍入国](kingdom-membership.md)。（依据：用户裁定 2026-10-06、2026-10-07；C# 反编译）

## 立案：只有三个入口（`PoliceCrimeMonitorEnhanced`）

| 入口 | 原版事件 | 案件类别 |
|---|---|---|
| 袭击村民队 | `MapEventStarted`，防守方是 `VillagerPartyComponent` | 村庄暴力 |
| 袭击商队 | `MapEventStarted`，防守方是 `CaravanPartyComponent` | 商队袭击 |
| 劫掠村庄 | `VillageBeingRaided`，按村子找正在劫掠的队伍 | 村庄暴力 |

- 范围是全大陆，不看村子或商队属于谁。
- 不立案的攻方：强盗、巡逻队、玩家本人、灰袍自己。
  - 强盗只认 `IsBanditFaction`，再加上队伍本身和所属势力两条，不用 `IsOutlaw && IsBanditFaction`。
    卡拉库吉特匪帮这类势力 `IsOutlaw` 为假，与在一起会让匪首拿到正式案卷。（依据：实机诊断 2026-09-12）
  - 玩家本人走 `PlayerBehaviorMonitor`。
- 案件按罪犯归并：同一罪犯再犯只加件数（`CrimePool.TryAdd`），不另开案。
- 同一伙人连着劫掠同一村，只记一次。中断的劫掠由每小时检查退掉。
- 案件类别靠罪名文字判断（`GwpCrimeCategoryClassifier`）：
  - 含 caravan 或「商队」的算商队袭击；
  - 含 village、villager、raid 或「村民」「村庄」「劫掠」「烧村」的算村庄暴力；
  - 玩家案单独一类。

## 人命与声望

- 劫掠：从开始到结束，村子少掉 1 点户数就算 1 条人命。（依据：用户裁定 2026-09-12）
  不要按 1 点户数 20 人折算，那会折出十几万第纳尔的罚金。（依据：用户裁定 2026-09-12）
- 战斗：只数平民（村民队、商队、村庄本身）自己死了多少人。（依据：实机诊断 2026-09-12）
  - 不要把平民那一边的整边伤亡都记上，那会把护商队的领主正规兵算成人命。（依据：实机诊断 2026-09-12）
  - 多家联手时按投入兵力分摊，零头给出兵最多的一家。
  - 分母里包含不记账的队伍（玩家、巡逻队、强盗、灰袍），他们杀的人不转嫁给同一边的领主。
- 领主之间互殴既不算罪，也不算功。
- 记账照玩家的规矩：每 10 条人命，负声望 +1。
  打赢强盗，每杀 10 人负声望 −1，最低到 0。
  这既记在长期档案 `HeroCrimeStats` 里，也记在当前开着的案件上。

## 罚金（`GwpFieldArrestPricing`）

- 一个案件的罚金 = 每件基础罚 1,000 × 件数 + 负声望 × 100（`Enforcement.FinePerPoint`），封顶 20,000。
- 被捕次数不进罚金，只影响震慑。

## 震慑（`GwpAiDeterrenceState`）

- 村庄暴力、商队袭击各有一套点数，上限都是 9。每套都分两部分：本人被捕得到的直接震慑，和家族转述来的震慑。
- 每次被捕，本类直接震慑增加的点数等于本类累计被捕次数，单次最多 9。
  总量到顶时，先压缩家族转述那部分。
- 只有人确实落在灰袍手里（灰袍的队伍或灰袍的据点），被捕才会清账：负声望清零，开着的案件当场结案。
  这一段的 `catch` 还是旧式的空 `catch`，待改成留痕写法。
- 恢复：每天按性格恢复。基础 0.009，勇武越高恢复越快，荣誉、仁慈、算计越高越慢。
  再夹在 0.004–0.0175 之间，最后乘 0.5。
  被俘期间不恢复。直接与家族两部分按当时比例一起衰减。
- 恢复下限：本类第 n 次被捕后，下限是 n−1 级，最高 9 级。
- 怎么起作用：倍率 = 0.65 的「震慑点数」次方。点数 ≤ 0.05 时不打折。用在三个地方：
  - 原版劫掠目标评分（`PoliceRaidDeterrenceModel`，只管 Raider 任务）；
  - 长期欲望里袭击村民队、商队的候选（`GwpCrimeDesireAuction`）；
  - 短期 AI 是否动手（`PoliceMobilePartyAIModel`）：按「攻方×目标×时间段」的哈希掷一次，在同一时间段内结果固定。
- 灰袍自己的倍率恒为 0：灰袍短期 AI 只打承办的目标和野怪。
- 震慑总量 ≥ 0.25 时，和领主对话会有专门的说辞：按主导来源（本人/家族）、档位（≤3、≤6、更高）、
  性格权重抽一种口吻，玩家是灰袍时措辞不同（`GwpAiDeterrenceDialogueCatalog`）。

## 外交封锁

- 灰袍家族不加入王国、不当佣兵，原版外交 AI 也不会替灰袍主动宣战（`PoliceAntiRecruitmentModel`）。
- 任何与灰袍之间的宣战，如果没有正当理由就立即改回中立（`PoliceAntiVanillaWarBehavior` → `GwpPeace.EndPoliceWar`）。玩家所在势力不受这条约束。
- 正当理由只有三种（`GwpPoliceWarReasonService.HasLegitimateWarReason`）：
  - 承办案件已进入战时追捕（`WarPursuit`）。只是在案件池里、已派单或还在跟踪都不算。（依据：设计）
  - 玩家悬赏宣战；
  - 纠察队宣战。
- 案件台账里「为什么在打仗」的那一栏，数据也来自这里。

## 被俘豁免（`PolicePrisonerImmunityBehavior`）

- 灰袍家族成员被俘，或在战败的一方，立即变成逃亡状态，不进俘虏营。
- 例外：被玩家俘虏，或玩家在战胜的一方。（依据：设计）
- 战败的灰袍要等遍历完参战名单后再处理，否则会改动正在遍历的名单。
