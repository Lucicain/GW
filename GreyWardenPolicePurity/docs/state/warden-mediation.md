# 灰袍出面调停与战后讲和

> 当前状态：用户已实测确认（2026-09-26；诊断日志 15:12 先 `MEDIATION_PEACE_DEFERRED` 后 `MEDIATION_PEACE_APPLIED`，败方队伍战后未残留，故障日志干净）
> 最后验收：`834291c`（含 `e2f2a58` 的推迟讲和）
> 覆盖源码：`PoliceAntiWarDeclaration.cs`
> 待实测：无

## 调停申请（替灰袍打出来的战争）

`_mediationRequests`（存档键 `gwp_mediation_requests`）记着等灰袍出面了结的势力。
与灰袍领主对话、外交分支（`lord_talk_speak_diplomacy_2`）里的
`gwp_warden_mediation_ask` 在有申请时出现（不要求玩家入会），选后对每个仍在交战的
势力调原版 `MakePeaceAction`。玩家自己已经讲和或势力消失的申请自动作废。

### 战斗没收尾时只应下，收尾时再讲和

玩家所在的战斗还没 finalize（战后界面里找灰袍，或送信队此时送到）时，
`ApplyWardenMediation` 只记下这场战斗（`_mediationDeferredUntilBattleEnds`，不进存档），
回话照常说"已了结"；这场战斗的 `MapEventEnded` 里再真正调 `MakePeaceAction`。
此刻战斗已是 `WaitingRemoval`，原版不再动它。

- **不要在玩家的战斗 finalize 之前改变任何立场**（依据：C#反编译 v1.4.8 2026-09-25；实机诊断 2026-09-25）。
  任何立场变化（`MakePeaceAction`，连裸的 `FactionManager.SetNeutral` 也算，
  `StanceLink.StanceType` 的 setter 都会派发 `OnMapEventContinuityNeedsUpdate`）都会触发原版
  `PartyDiplomaticHandlerCampaignBehavior.CheckMapEvents`：未收尾的战斗里，进攻方中
  不再与对面**每一支**队伍交战的（`MapEvent.CanPartyJoinBattle`）直接被 `MapEventSide = null`
  摘出去。被摘的队伍跳过 `HandleMapEventEndForPartyInternal`，所以不会被销毁；进攻方摘空时
  战斗当场 `FinalizeEvent`。实机后果：普林多尔的部队领主已被俘、0 人，被摘出后留在地图上，
  玩家一碰就进原版会面对话，`GauntletMapConversationView.CreateConversationTableau` 空引用崩溃。
- 其他灰袍自己的 `TrySetNeutral`（结案、撤案、纠察回收）在同样时机调用也会触发这条原版路径，
  目前没有实机证据说明它们会在玩家的战斗里触发，未改。

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

## 战后讲和（灰袍与第三方）

`OnBattleEnded`：灰袍参战且对面是某个势力时，若该势力没有正当的持续交战理由
（`GwpPoliceWarReasonService.HasLegitimateWarReason`），灰袍与它讲和
（`POLICE_BATTLE_PEACE`）。纠察队的仗、延迟纠察队单独作战、灰袍刚击败正在执行对玩家
任务的玩家时不在这里讲和；玩家击败执行玩家任务的灰袍后，灰袍与玩家讲和。
土匪势力不处理。
