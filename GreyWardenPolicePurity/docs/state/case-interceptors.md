# 立即追截队：归队与多余的人

> 当前状态：归队改为并回主队、多余的人去信众村，已部署待实测
> 最后验收：未建立检查点
> 已复核至：`18779c6`（2026-10-15）
> 覆盖源码：`PoliceEnforcementBehavior.DelayPatrols.cs`
> 待实测：追截队回到主队时船当场还回、人和普通俘虏并入主队，主队装不下的人去最近的信众村进守村队（地图上不再有追截队往城里走）；agent 核对 `IMMEDIATE_CASE_INTERCEPTOR_REJOINED` 与 `DETACHMENT_SETTLED`

追截队、使者不算协力编队的在编力量，不靠它们保留军团（见 [案件](case-enforcement.md)「协力编队」）。
案件本身的生命周期见 [案件](case-enforcement.md)；船怎么借、怎么还见 [临时队](temporary-parties.md)。
[灰袍使者](case-envoy.md) 的出队（`CreateDetachment`、`DetachmentBatches`）和归队与追截队同一套；
使者归队时另把带回的钱交进公库、把投案的罪犯交给主队。

派队的玩家目标避让统一查 `GwpPlayerCaseAssignment.IsReservedTarget`，同时包括普通悬赏与赎罪；交接规则见[赎罪](atonement.md)和[使者](case-envoy.md)。（依据：源码复核2026-10-15）

立即追截队（`TrySpawnImmediateCaseInterceptor`）的人是从来源领主名册里抽出来的。返程时
先还来源领主；来源领主战败、被俘或队伍没了（`CanContinueLeadingPoliceTask` 为假），改交最近的
灰袍领主（`GwpCommon.FindNearestGreyWardenLordParty`），并把接收人写回
`DelayPatrolState.SourceTaskPolicePartyId`，之后不再换人。只有返程状态会改接收人，
承办中的重定目标冷却（只看未返程的队）不受影响。

碰到接收的主队时（用户裁定 2026-10-14：追截队必须回到主队，不带着船去城里解散）：
- 船当场交回主队（`PoliceResourceManager.ReturnLentShips`）；在海上还要靠船走，留到解散时再还。
- 主队编制装得下的人都并回去，灰袍兵先回，打完仗解救的非灰袍兵其次；不从主队换人出来。普通俘虏全部交给主队。
- 装不下的人走到最近的信众村（有祭司、安静、不与灰袍交战），在那里落脚：灰袍兵进守村队、非灰袍兵进原版民兵、
  俘虏进地牢、钱进公库（`GwpFireswornBehavior.SettleInFaithVillage`，与火誓者无领主队落村同一段处理，见 [队伍](firesworn-parties.md)）。
  一座能去的信众村都没有时才进最近的城解散。一位能接收的灰袍领主都没有时，整队照旧进城解散。

追截队被目标掉头吃掉后马上再派、不设冷却，是对的。（依据：用户裁定 2026-10-14）

## 回退

归队改法之前为 `c83fecb`：`git revert` 本次存档恢复「超编换出低阶兵、多余的人进城解散」。（依据：流程）
