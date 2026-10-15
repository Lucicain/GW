# 灰袍入国：灰袍家族不加入任何王国

> 当前状态：入国拦截、读档离国已上线（v1.4-r14 同号替换包与 main 均含）；读档离国经用户确认可用
> 最后验收：`9dc76be`（2026-10-07 用户确认读档离国可用）
> 已复核至：`1f84971`（2026-10-15）
> 覆盖源码：`PoliceAntiRecruitmentModel.cs` `PoliceAntiVanillaWarBehavior.cs` `GwpKingdomMembershipPatch.cs` `_Module/ModuleData/spclans.xml` `tools/Verify-KingdomMembership.ps1`
> 待实测：另存读档后仍保持独立、原案件能继续；修复后不再出现新的入国

灰袍是无国、无城镇城堡的贵族家族，必须始终独立。来源是玩家反馈：r12/r13 存档里灰袍加入了南帝国。

`spclans.xml` 的 gw 仍为 `is_noble=true`、`is_outlaw=false`、`culture=empire`；旗帜由 [clan-banner](clan-banner.md) 归属，banner_key 不参与上述入国判据。（依据：当前 XML 与归属入口复核 2026-10-15）

## 为什么只挡评分不够

- 外交模型只影响评分；原版 1.4.8 `ChangeKingdomAction` 的入国动作不再查询模型。（依据：C# 反编译）
- 内战 BellumCivile 1.4.0 的流亡收容 `IsRecoverableExileCandidate` 不排除灰袍，首次 `OnDailyTick` 与周结收容直接调用 `ApplyByJoinToKingdom`。（依据：C# 反编译，实际 DLL SHA-256 `3b88f285…9f29`，与公开源码 `941fc608` 一致）
- `Clan._kingdom` 是 `SaveableField(57)`；读档恢复已有王国不走任何入国动作，所以只拦写入纠正不了已有存档。（依据：C# 反编译）

## 现在的做法

- `GwpKingdomMembershipPatch` 在普通加入、叛国加入、佣兵加入、建国四个公开入口执行前，拦截 gw 家族的非空目标王国；债务、阵营关系、佣兵状态与事件都不写入。普通家族与灰袍离国入口保持原版。叛国入口在内部动作之后还会发 `OnClanDefected`，所以不能只跳过 `ApplyInternal`。（依据：C# 反编译、离线回归）
- `GwpBellumExileCandidatePatch` 在内战的候选入口排除 gw，避免选收容国、询问玩家和虚假通知。它通过反射可选挂钩，没有 DLL 依赖，没装内战时跳过；内战程序集若在灰袍挂补丁之后才加载，候选补丁绑不上，但四个原版入口仍然挡住。（依据：设计）
- 灰袍的王国归属只在读档完成时检查一次（`PoliceAntiVanillaWarBehavior.OnGameLoadFinished`）：普通成员走 `ApplyByLeaveKingdom`，佣兵走 `ApplyByLeaveKingdomAsMercenary`，关闭通知。缓存、军团、债务、佣兵状态、封地归还都交给原版收尾。已经独立就不调用。
- 不得按存档身份做特例，不得加两日、日结、周结或心跳轮询，不得读旧格式或加迁移标记。（依据：用户裁定 2026-10-06）
- 不改第三方模组的代码、配置或方法；修正只放在本模组的模型或原版入口。（依据：用户裁定 2026-10-07）

## 同一行为里的宣战监听

`PoliceAntiVanillaWarBehavior` 还监听 `WarDeclared`：只认灰袍家族对象或 ID 为 gw 的一方。对方是玩家家族当前 MapFaction 时保留原行为；其他对手交给 `GwpPeace.EndPoliceWar`：有正当理由才保留战争。这里不含入国或离国动作。

SyncData 无状态：本行为只响应宣战与读档完成事件，没有进行中的工作；归属与外交状态由原版存档负责。

## 诊断

`WARDEN_KINGDOM_JOIN_BLOCKED` / `WARDEN_BELLUM_EXILE_EXCLUDED`（`GWP_DIAGNOSTICS` 内）记录被挡的目标与调用栈，用来确认新入国的真实来源。用户确认不再出现新入国后退休。读档离国的 trace 已在用户确认后退休。

## 反馈环境

反馈存档在 `C:/Users/lucif/Documents/Mount and Blade II Bannerlord/Game Saves/GW-kingdom-report-20261006.sav`（原件 `C:/Users/lucif/Downloads/saveauto1.sav`，SHA-256 `14be06c7…afda5`）。agent 不编辑这两个文件。

存档头：游戏 v1.4.8.119303，灰袍 r12（玩家口头说是 r13）。存档里的模组及与本机的差异：

| 中文名 / ID | 存档版本 | 本机 |
|---|---|---|
| 家谱 / Bannerlord.FamilyTree | 1.4.8 | 同 |
| 收获与生产 / HarvestAndProduction | 1.6.17 | 同 |
| 内战 / BellumCivile | 1.4.0 | 同 |
| 誓约亲卫 / Comitatus | 0.8.9 | 0.9.67 |
| FireArrows | 1.0.0 | 缺 |
| 打击感强化 / HitstopEffects | 1.0.0 | 缺 |
| 涅墨西斯 / LT_Nemesis | 1.6.1 | 同 |
| 婚姻和生育 / MarriageFertility | 0.6.7 | 同 |
| RTS 战略视角 / RTSCamera（含指挥系统） | 5.4.16 | 同 |
| RBM：真实战斗 / RBM | 4.5.2 | 4.5.4 |
| 女性头模 / xxFemaleHead、Harmony | — | 同 |
| ButterLib / UIExtenderEx / MCM | 2.11.1 / 2.13.3 / 5.12.2 | 2.12.0 / 3.0.1 / 5.12.3 |

存档没有 Bannerlord.Diplomacy；本机虽然装了，不算进反馈环境。第三方模组由用户自己下载。（依据：用户裁定 2026-10-06）

反馈档载入时的两次崩溃（种马场工坊类型缺定义、投掷斧模板）都来自缺第三方模组，装齐后消失。不要在灰袍里加假定义或吞读档异常。（依据：崩溃转储、C# 反编译；用户裁定 2026-10-05）

## 测试与回退

- 离线回归 `tools/Verify-KingdomMembership.ps1`：真实游戏程序集加实际内战 DLL 43 项断言，不带内战 39 项。离国动作的边界用桩替代，佣兵与真实封地两个分支还没有实机样本。
- 回退：撤 `c7ca2e0`（入国拦截）、`9dc76be`（读档离国）、`7c91b2d`（退休离国 trace）。
