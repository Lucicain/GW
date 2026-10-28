# 灰袍入国：灰袍家族不加入任何王国

> 当前状态：正式r15在本机可拦流亡家族2.5.5；新装性格大修／小修补的相关挂钩共存未使拦截失效，叛军建国走原版建国动作；玩家实际入国原因仍未闭合
> 最后验收：`9dc76be`（2026-10-07 用户确认读档离国可用）
> 覆盖源码：`PoliceAntiRecruitmentModel.cs` `PoliceAntiVanillaWarBehavior.cs` `GwpKingdomMembershipPatch.cs` `_Module/ModuleData/spclans.xml` `tools/Verify-KingdomMembership.ps1` `tools/Verify-Recruit*.ps1`
> 待实测：另存读档后仍保持独立、原案件能继续；修复后不再出现新的入国

灰袍是无国、无城镇城堡的贵族家族，必须始终独立。来源是玩家反馈：r12/r13 存档里灰袍加入了南帝国。

`spclans.xml` 的 gw 仍为 `is_noble=true`、`is_outlaw=false`、`culture=empire`；旗帜由 [clan-banner](clan-banner.md) 归属，banner_key 不参与上述入国判据。（依据：当前 XML 与归属入口复核 2026-10-15）

## 为什么只挡评分不够

- 原版动作、直接写归属与存档恢复的区别见[原版入国机制](../vanilla/kingdom-membership.md)。（依据：v1.4.8 C# 反编译）
- 内战 BellumCivile 1.4.0 的流亡收容 `IsRecoverableExileCandidate` 不排除灰袍，首次 `OnDailyTick` 与周结收容直接调用 `ApplyByJoinToKingdom`。（依据：C# 反编译，实际 DLL SHA-256 `3b88f285…9f29`，与公开源码 `941fc608` 一致）

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

本轮 r15 反馈（2026-10-28）：用户确认百科／王国家族列表已有归属，不能按第三方虚假通知结案。截图游戏为 v1.4.7，灰袍 v1.4.15；与此前 BellumCivile 反馈不是同一环境。截图启用 Recruit Exiled Clans 2.5.5、Personality Overhaul 7.7.1、RebellionCreateKingdom 1.0.0、MutliLittleFixes 1.7.10 等。本机随后安装了前者2.5.5，DLL SHA-256 `9135866F0AB5F528CDD3518AF6D7B12576816F79ABDD3D2DB3FA4BD4513F115D`。

Recruit Exiled Clans 2.5.5 的实装DLL三处入国调用都走 `ApplyByJoinToKingdom`，没有直接写家族归属或Harmony补丁。真实候选谓词默认纳入独立灰袍；在隔离的MCM内存设置把 `gw` 加入 `excludedClanIds` 后排除。真实 `JoinKingdomWithReason` 在正式r15拦截生效时不改归属／债务，却仍发成功通知；它不检查动作后的实际归属。这是已证实的候选与通知不协调，不足以解释本次实际入国。测试没有改第三方文件、设置或方法。（依据：实装DLL反编译与真实DLL方法执行2026-10-28）

用户随后提供战斗日志照片（`C:/Users/lucif/Documents/Tencent Files/157652226/nt_qq/nt_data/Pic/2026-10/Ori/ce91889ece236bedba210682019c490a.jpg`），显示 `灰袍守卫 joined 南帝国 (Joined most friendly kingdom)`。该完整格式与理由字符串精确对应Recruit Exiled Clans的 `GetJoinReason` → `JoinKingdomWithReason`，故本条提示及自动投国尝试的来源已定位。照片本身不显示家族归属；此前用户确认的百科／王国列表实际归属仍待存档／现场证据闭合，不能用已复现的虚假成功通知推翻用户反馈。（依据：用户照片与实装DLL字符串／控制流核对2026-10-28）

随后安装的三份DLL（2026-10-28）核对如下；模块同号不等于与玩家DLL逐字节一致。

| 模组 | 本机版本／SHA-256 | 与入国有关的核对结论 |
|---|---|---|
| PersonalityMatters／性格大修 | XML及DLL均1.0.0，与玩家截图7.7.1不同；`7EBD8570535B2F8C46ED4BA6AFB02CFCE9B68A9D238C3BE8DAB7695831F8308D` | 投国补丁只给原版评分乘倍率；本机灰袍覆盖模型仍返回拒绝评分；BetrayalSystem只用于战斗优势计算，未发现直接写入国家归属 |
| MutliLittleFixes／许多小修小补 | XML 1.7.10；`630702EB09F2A30DE3C31FAE239B8CA4A72E2FBB2BC0803B15672BB71DC367EB` | 混淆DLL的静态IL未见入国动作或Clan.Kingdom写入；实际挂钩涉及招募／叛国对话条件与流亡家族存续；相关挂钩共存、执行其卸载回调后r15拦截仍在 |
| RebellionCreateKingdom／叛军建国 | XML 1.0.0，DLL文件版本1.0.1；`08BC0B07BF101D4C78CA5720C9DE31D8AE4EBAF48F4FBBA5DA553A63C7B6A0C4` | 监听叛军家族解散事件，调用KingdomManager.CreateKingdom；归属最终走r15拦截的建国动作，原版时序见手册；未见招募普通家族进入既有王国的路径 |

上述是对应DLL的反编译、静态IL与局部方法执行结果，相关补丁共存测试没有启动完整战役或加载玩家存档；不据此排除其他版本或完整加载环境中的冲突。（依据：本机DLL及实际方法执行2026-10-28）

已知缺陷：玩家反馈 r15 灰袍实际加入他国，责任模组与写入路径未闭合；目前四份实装候选没有查到能解释实际入国的路径，正式r15在本机v1.4.8及相关挂钩共存时可拦自动投国。仍缺玩家实际灰袍DLL、性格大修7.7.1及v1.4.7战役DLL／存档运行证据，以区分补丁未绑定／被替换、版本或安装内容差异、其他归属写入与存档恢复时序；暂不改业务机制。（依据：用户确认、截图及实装DLL离线核对2026-10-28）

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

- 回退：撤 `c7ca2e0`（入国拦截）、`9dc76be`（读档离国）、`7c91b2d`（退休离国 trace）。
