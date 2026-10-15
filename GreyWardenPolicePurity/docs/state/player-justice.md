# 玩家声望、入会与悬赏

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：`4f31993`（2026-10-15）
> 覆盖源码：`PlayerBehaviorMonitor.cs` `PlayerBountyBehavior.cs` `PlayerBountyBehavior.DialogueAndNotification.cs` `PlayerBountyBehavior.Support.cs` `PlayerBountyBehavior.QuestAndNotification.cs` `PlayerBountyBehavior.Encyclopedia.cs` `GwpCaseSettlementRules.cs` `GwpCaseReceipt.cs` `GwpFieldReportLedger.cs` `GwpCaseSettlementEntryPatch.cs` `GwpMapBarReputationPatch.cs` `GwpCaseArchiveScreen.cs`
> 待实测：无

事务台账中的日常练兵项显示实体运兵出程或改道落村阶段，不再有带新兵归队阶段；本页声望/赎罪规则不涉及此变更，业务见 [运兵](warden-training.md)。（依据：源码复核 2026-10-15）

玩家与灰袍之间的账：声望怎么涨跌、什么时候被通缉、怎么入会退会、怎么接悬赏、
交差时查账。委托结果判定和交差金额见 [`player-commission.md`](player-commission.md)；
纠察队见 [`patrol.md`](patrol.md)；NPC 罪犯的立案和罚金见 [`crime-intake.md`](crime-intake.md)。

负声望赎罪的权限、占案、完成与申报单独见[赎罪委派](atonement.md)。普通悬赏与赎罪通过 `GwpPlayerCaseAssignment` 汇总，灰袍事务界面也用这个口径显示玩家占案；案件池不把NPC已有宽限/收款/比武约定的案子再次给玩家选择。（依据：源码复核2026-10-15）

读档与回执业务直接执行，移除无依据的通用吞异常包装；故障观察器不改变结果判定。规则见 [代码健康](code-health.md)。（依据：源码复核2026-10-14）

## 声望（`PlayerBehaviorPool`，在 `GwpData.cs`）

- 范围 −100 到 100，新档为 0。声望 ≤ −11 即通缉（`IsWanted`），由正式灰袍承办 `PLAYER_WANTED`。
  −1 到 −10 只派纠察队，见 `patrol.md`。
- 新档必须清空：声望和案件池都是静态的，开新档不会触发 `SyncData`。
  所以 `OnNewGameCreated` 手动 `ClearAll`，否则新档会继承上一个存档的声望。（依据：实机诊断）

### 扣分

- 玩家劫掠村庄：固定 −2。肇事者从 `VillageStateChanged` 带来的劫掠者读取，不再猜。
  以前按「目标是这座村子的队伍」去猜，玩家只是站在村里就会背锅。（依据：实机诊断 2026-08-28）
- 打败村民队或商队、劫掠得手、强征新兵或补给：按失败方战前人数，每 10 人（向上取整）−1。
  打劫无人防守的村子，按民兵和驻军人数算；一个人没有就不扣。
- 强征新兵、强征补给另各记一条罪行，−1。
- 打败灰袍或纠察队：按灰袍阵亡数，每 10 人（向上取整）−1。
- 帮罪犯打灰袍：按灰袍阵亡数，每 10 人（向上取整）−1，通缉时记「妨碍执法」。
- 已在通缉状态时，以上罪行都会追加到 `PLAYER_WANTED` 案。

### 加分

- 玩家在胜方，且败方有强盗，或胜方有村民队、商队，或守住了被劫掠的村子：
  按玩家亲手击倒人数累计，每满 10 人 +1。余数跨战斗保留、存进存档。
- 亲手击倒还开着案的罪犯部队，同样按每 10 人 +1 计。
  协助灰袍抓人走另一条结算路径，两条互斥，同一场战斗不会重复记分。
- 遭遇菜单有两个选项，无视外交直接加入：
  - 「强制介入」：先对攻方宣战，再站到防守方。
  - 「落井下石」：站到攻方。
  
  原版遭遇说明会把玩家写成劫掠者，加入后改写这段文字。

### 显示

- 右下角原版信息条里，影响力旁边多一项「灰袍声望」。图标是影响力图标的副本，染成冷银蓝；
  只有通缉时标红（`GwpMapBarReputation`）。这项在信息条展开时才看得到。（依据：用户要求放在影响力旁边）
- 灰袍事务界面（`GwpCaseArchiveScreen`，入口见 `map-bar-affairs.md`）汇总：
  玩家声望、档位与通缉时的应缴数、家族规模与收养冷却、各场战争的理由、当前所有开放案件和差事队列。

### 声望图标的交互边界与待办

- 当前位置：`SecondaryInfoItems`，插在 `influence` 之后（按用户要求"挨着影响力"）。
- **代价已告知用户**：那一排只在信息条展开（`IsInfoBarExtended`）时显示；
  `PrimaryInfoItems`（第纳尔所在）才是常驻。用户在"挨着影响力"与"常驻可见"之间
  选了前者。
- **点击打开事务界面：做不到。** `MapBar.xml` 两排的 `ItemTemplate` 外层是
  `HintWidget : Widget`，反射确认 `Widget` 没有任何 click 事件（只有 `EventFire`
  与各类 `PropertyChanged`），点击是 `ButtonWidget` 独有。金币和影响力自己也点不动。
  要改只能整份覆盖 SandBox 的 `MapBar.xml`（219 行），会在游戏更新时失效并与任何
  改地图条的模组冲突。
- **可行的最小方案（未做，等用户点头）**：照 `GwpCaseArchiveScreen.Open()` 的现成套路
  （`new GauntletLayer` + `AddLayer` + `LoadMovie`）加一个常驻小层，一个
  `ButtonWidget` 带图标与数字，`Command.Click` 调 `GwpCaseArchiveScreen.Show()`，
  `IsFocusLayer = false`、不设 `InputRestrictions`。不覆盖任何原版文件。

## 入会与退会

- 声望 ≥ 20 且从未被招募过：从离玩家最近的城镇派一支 20 人的招募使者队来找玩家。
  使者不吃饭。追了 5 天还没碰上，使者就进离自己最近的城解散；下一次资格检查再从离玩家最近的城重新派一支，
  所以派出点会跟着玩家走。
- 接受：
  - 发放黑袍指挥官全套、黑曜指挥官盾、一副双刀；
  - 发 5 名 `gwnewrecruit`。
  
  装备品质规则见 `warden-gear.md`。拒绝后不再派使者，但可以去找任一灰袍领主申请。
- 退会：找任一普通灰袍领主。退会只撤职权，已经代收的罚金、已经押着的犯人仍要交。
  重新入会的门槛按 20 / 40 / 60 递增；自愿退会满 3 次后永久关闭。
- 「入会」的判据是 `PlayerBountyBehavior.IsRecruitedByGreyWardens`，地图栏按钮、祭司招兵等都看这个。

## 悬赏（接单）

- 接单要同时满足三条：已入会、声望 ≥ 20、穿着指挥官全套。每 2 天提示一次，右侧出现通知图标。
- 从案件池里所有开着、能追到人的案件中，挑三份给玩家选：最近的、最难的、最容易的。
  难度看对方战力与玩家战力之比：≤ 0.75 算易，≥ 1.25 算难。对方用接案门槛同一个数（`GwpStrength.CaseTarget`：
  他所在战斗组，挂靠在军团里就算整支军团），玩家用自己的战斗组。
- 接单后这件案子归玩家：原承办的灰袍撤走，期限 45 天。
  每 2 天往任务日志写一条目标的位置情报。目标换队或重建部队时，按英雄本人重新找。
- 求援（`PlayerBountyBehavior.Support`）：当面开口或派兵送信，都算求援。
  - 送到之前，灰袍不为这件案子开战、也不去追人。
  - 送到之后，由收到求援的那名领主接手支援，放下手上其他事；他已经不在时，改派离玩家最近的合格领主。
  - 灰袍为此案开战时，玩家阵营一并对犯人宣战。这场战争记进调停申请通道（`PoliceAntiWarDeclaration.RecordMediationRequest`），不再自动讲和，见 `warden-mediation.md`。（依据：实机诊断 2026-09-16）
- 三个常量 `Bounty.EasyReward/StandardReward/HardReward` 已无人引用。报酬按罚金封顶算，见 `player-commission.md`。

## 交差与查账（`GwpFieldReportLedger`）

- 犯人那一侧的账当场就清，按他实际交了多少算：实缴先抵基础罚，余款每 100 抵 1 点负声望。
  罚金到上限 20,000 且全额交清时，负声望直接清零。玩家之后交不交、交多少，不回头影响犯人。
- 玩家只要把收到的钱如实上缴就算办妥。第二层谈成的折扣、对方确实没钱造成的短缺，都不是玩家的错。（依据：设计）
- 私留：收到的比上缴的多，差额进查账队列，7 天后掷骰。
  - 被查的概率：基础 0.08，加上 0.04 ×（近 10 次里谎报次数 − 1）²。
  - 声望超过 40 的部分每点降 1%，最多降 75%；负声望每点加 0.5%。
  - 最低 2%，最高 95%。
  - 查出来：按差额每 100 第纳尔扣 1 点声望。
- 过度执法：对方已经缴过钱或兑现过处置，玩家又把他打垮。赏照领、案照结，但单独排一次查账。
- 办案报酬（`GwpCaseSettlementRules.Reward`）= 400 + 60 × 玩家阵亡数 + 120 × 罪责点，以应缴罚金封顶。
  人不是玩家打下的（犯人死了、被别人抓了）：只付 400 + 60 × 阵亡数。
- 收据（`GwpCaseReceipt`）只记物品 ID 和词条 ID，不存对象引用：缺了某个模组的物品，也不会毁掉旧账。

## 案件目标不能进城躲

- 案件已宣战后，目标的进城动作会被拒绝（`GwpCaseSettlementEntryPatch`，前置 `EnterSettlementAction.ApplyForParty`）。
  同时命令它（在军团里时命令军团领队）去进攻承办的灰袍。
  案件结束或战争回退后，限制自动消失。
