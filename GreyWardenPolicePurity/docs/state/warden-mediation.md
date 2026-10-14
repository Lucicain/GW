# 灰袍出面调停与战后讲和

> 当前状态：所有讲和收归 `GwpPeace`、战后结算提前中立的空引用已修复，开战扣的关系当场还回；均经用户实测与日志确认（2026-10-14）
> 最后验收：`c576fd7`（推迟讲和与开战关系恢复，用户实测与日志确认 2026-10-14）
> 已复核至：`cf53ba5`（2026-10-14）
> 覆盖源码：`PoliceAntiWarDeclaration.cs` `GwpPeace.cs` `tools/tests/test_battle_peace.py`
> 待实测：无

## 调停申请（替灰袍打出来的战争）

`_mediationRequests`（存档键 `gwp_mediation_requests`）记着等灰袍出面了结的势力。
与灰袍领主对话、外交分支（`lord_talk_speak_diplomacy_2`）里的
`gwp_warden_mediation_ask` 在有申请时出现（不要求玩家入会），选后对每个仍在交战的
势力讲和（`GwpPeace.Make`）。玩家自己已经讲和或势力消失的申请自动作废。
玩家战斗没收尾时（战后界面里找灰袍，或送信队此时送到）回话照常说"已了结"，讲和按下文「讲和的唯一入口与时序」推迟。

**只登记“替灰袍打的”战争**，满足其一：
1. 制止正在发生的案件：本场是村庄劫掠，或玩家这一侧有村民队/商队；
2. 玩家这一侧有灰袍部队（加入灰袍的仗）；
3. 对面是玩家自己承办的委托目标。

登记入口：

- **原版“玩家敌对”宣战**（`DeclareWarDetail.CausedByPlayerHostility`）。原版只在玩家本人是
  进攻方时宣这种战（`BeHostileAction.ApplyEncounterHostileAction`，加入一场已在进行的仗后
  点“进攻”也走这里）。
  - 宣战当刻玩家已在战场一侧（`MobileParty.MainParty.MapEvent`）→ **当场判定并登记**。
  - 玩家在地图上主动开打（宣战在前、战斗在后）→ 记为待定，玩家这场战斗
    `MapEventStarted` 时双方到齐再判。
  - 战斗结束时仍未判定的，作最后一次兜底判定，并清除待定标记。
- 委托结案（`bounty_case_closed`）、支援宣战（`case_support_declaration`）由
  `PlayerBountyBehavior` 直接调 `RecordMediationRequest` 登记。

2026-09-24 之前只在战斗结束（`MapEventEnded`）时判定，两个问题：原版要等玩家离开战后
界面才发这个事件，玩家在战后界面里马上找灰袍时申请还没登记；而且战后界面里释放/俘虏
败方领主后，败方队伍已不在战场一侧，“对面有这个势力”的检查失败，申请干脆没登记。
（实机：00:57:11 战后与暮光对话无选项，00:58:43 玩家改用原版向可汗求和。）

接战前就已存在的战争不会产生新的宣战，因此不登记——那不是替灰袍打出来的。

**开战那一下扣的关系当场还回去**：原版对没在交战的势力动手时，先扣玩家与对方统治者 10 点关系（落在双方族长之间），再宣战
（C#反编译 v1.4.8 `BeHostileAction.ApplyEncounterHostileAction`、`DefaultDiplomacyModel.GetHeroesForEffectiveRelation`）。
`GwpHostilityRelationPatch` 在动手前记下这对关系的原值；同一次遭遇里判定为替灰袍打的仗（上面三条之一），登记调停申请的同时把关系设回原值，
不弹提示（用户裁定 2026-10-14：提示不说关系）。只还这一下：之后战斗里杀领主、处决、调停讲和对关系的影响都照原版，不还。判定不成立或玩家这场战斗结束，记录就丢掉，不进存档（遭遇中不能存档）。
（依据：用户裁定 2026-10-14）

## 讲和的唯一入口与时序：`GwpPeace`

灰袍相关的讲和全部走 `GwpPeace`，别处直接调 `FactionManager.SetNeutral` 或 `MakePeaceAction.Apply`
会被 `Check-CodeHygiene` 拦下（依据：用户裁定 2026-10-07「共享的判断只有一处」）：

- `EndPoliceWar(对方)`：灰袍与第三方结束交战。对方仍有合法交战理由（`GwpPoliceWarReasonService.HasLegitimateWarReason`）
  时保留战争并返回 false，这是唯一返回 false 的情况；结案、撤案、追截回收、反原版宣战、战后讲和、延迟纠察清理、
  玩家接手委托后的重试都用它。
- `Make(甲, 乙)`：已了结场合的讲和，不看灰袍交战理由（调停、赎罪、玩家缴罚或受罚获释、玩家击败执行玩家任务的灰袍）。
- 一方是玩家势力时用原版 `MakePeaceAction`（裸 `SetNeutral` 不标记定居点重绘，领地在地图上一直红着，也不派发原版和平事件），
  否则用 `FactionManager.SetNeutral`。
- `SettlePlayerCase`：玩家的灰袍案件了结时与灰袍讲和；受害势力已正式交战的讲和，只有犯罪评级的清评级，然后清空受害势力。
  灰袍领主押送释放与纠察队惩戒走同一个方法。

- **不要在玩家的战斗 finalize 之前改变任何立场**（依据：C#反编译 v1.4.8 2026-09-25；实机诊断 2026-09-25）。
  任何立场变化（`MakePeaceAction`，连裸的 `FactionManager.SetNeutral` 也算，
  `StanceLink.StanceType` 的 setter 都会派发 `OnMapEventContinuityNeedsUpdate`）都会触发原版
  `PartyDiplomaticHandlerCampaignBehavior.CheckMapEvents`：未收尾的战斗里，进攻方中
  不再与对面**每一支**队伍交战的（`MapEvent.CanPartyJoinBattle`）直接被 `MapEventSide = null`
  摘出去。被摘的队伍跳过 `HandleMapEventEndForPartyInternal`，所以不会被销毁；进攻方摘空时
  战斗当场 `FinalizeEvent`。实机后果：普林多尔的部队领主已被俘、0 人，被摘出后留在地图上，
  玩家一碰就进原版会面对话，`GauntletMapConversationView.CreateConversationTableau` 空引用崩溃。
- 推迟的做法：玩家战斗（`PlayerEncounter.Battle`，主队暂时脱离时仍持有遭遇；或主队地图战斗）未 finalize 时，
  `GwpPeace` 只记下这对势力（同一对正反只记一次，不进存档：遭遇进行中不能存档），视为已讲和返回；
  之后任一战斗 `MapEventEnded` 且玩家战斗已收尾（此时已 `WaitingRemoval`，原版不再动它）时逐对重办，
  灰袍对第三方的这时再看一次交战理由。（依据：C#反编译 v1.4.8 与离线回归 2026-10-14）
- 2026-10-14 10:20:38 实机在捕获 `lord_6_10` 后结案，原版 `DoApplyMapEventResults` 随后空引用；RGL已提前记出玩家战斗进入WaitingRemoval。原版外交检查会调用 `PlayerEncounter.Finish` 清空正在使用的 `_mapEvent`，旧代码与原版结果方法的离线反例可复现此状态破坏。（依据：实机日志、C#反编译 v1.4.8 与 `test_battle_peace.py` 2026-10-14）

## 战后讲和（灰袍与第三方）

`OnBattleEnded`：灰袍参战且对面是某个势力时，若该势力没有正当的持续交战理由
（`GwpPoliceWarReasonService.HasLegitimateWarReason`），灰袍与它讲和
（`POLICE_BATTLE_PEACE`）。纠察队的仗、延迟纠察队单独作战、灰袍刚击败正在执行对玩家
任务的玩家时不在这里讲和；玩家击败执行玩家任务的灰袍后，灰袍与玩家讲和。
土匪势力不处理。

回退：讲和入口统一前为 `0614808`（`GwpCommon.TrySetNeutral` 与各处直接 `MakePeaceAction`）；`git revert` 本次存档即可退回，会恢复已确认的提前中立崩溃。（依据：流程）
