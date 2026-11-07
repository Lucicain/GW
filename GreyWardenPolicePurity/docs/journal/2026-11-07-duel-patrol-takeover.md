# 达努斯提卡约斗与同城巡逻接管调查

## 原始证据

读取对象均未改写，时间为日志中的 +11:00。

另对 live 的灰袍 DLL 用 ilspycmd 只读核对，实际安装的 `HoldPartyAt` 条件与工作树相同，DLL SHA-256 为 `F7BACFAA2391849FDD130FC9628F8854261EAD539CF338524EFBA1B7681C0229`。原版源码库 manifest 的 CampaignSystem 哈希与安装 DLL 一致，均为 `1F8E33E2ED73E6EC653D7629180AFB70649DDC6E5BD1657A802A264EFDA1C3AE`。

| 对象 | SHA-256 |
|---|---|
| Documents/Mount and Blade II Bannerlord/Game Saves/gwp_test_smoke.sav，2026-11-07 14:04:32 | `77C29DF0079C0D9713907CA19D4C3261016788ADF69A00CD0ED685372A464DB9` |
| GreyWarden-AI-Diagnostics.log，本轮 14:02～14:04 | `CF0A13AEE917179F45F7050A2DC0B6830DB4914C06DBA2BDDD10507A2DB41F55` |
| GreyWarden-Diagnostics-Archive/GreyWarden-AI-Diagnostics.log.20261107T0302123710436Z.8abc35a222d24f23acd1e64d5a962a04.log，前轮 21:35～21:40 | `4A0E43F7567BABAC5E2C92A12F6C00845C528725BBF9D9518601AA7E91061638` |

## 先于约斗的城外位置

前轮日志第 4524 行，2026-11-06 21:40:54.171，游戏小时 2186775.63，记录得巴娜选中 DemandDuel。第 4526 行同一游戏时刻记录约定达努斯提卡成立。

当时约珥的位置为 `(663.4468, 268.4555)`，到得巴娜的直线距离记为 6.54。达努斯提卡城门坐标为 `(658.7773, 277.1635)`，约珥到城门直线距离为 9.880966。计入日志两位小数的舍入，得巴娜距城门至少 3.335966。

海陆状态不在当时的目标字段中，另核对港口坐标排除歧义。该进程 RGL 第 3419 行加载 SandBox 的 settlements.xml；存档模块列表没有 NavalDLC。该 XML 未给达努斯提卡指定港口坐标，原版 `Settlement.Deserialize` 将 PortPosition 初始化为城市位置 `(661.191, 276.466)`，约珥距此 8.322064，得巴娜距此至少 1.777064。同样不可能位于城内使用的港口点。

距离字段由双方 `GetPosition2D` 相减取得；原版 `GetPosition2D` 直接返回 `Position.ToVec2()`，进城时 `CurrentSettlement` setter 会把 Position 写成 GatePosition 或 PortPosition。该时刻得巴娜没有军团、挂靠、战斗或围城，两种城内坐标均已排除，因此可以确认约斗成立时她已在城外。上一轮把调查问题表述为“首次出城触发”过于具体，本次证据不支持她先赴约进城再离城的顺序，也不能排除约定后她曾短暂入城再出城。

## 存档里的真实行为

按原版 `FileDriver` 的元数据长度与 Deflate 格式、`GameData.Read` 和 `ArchiveDeserializer.LoadFrom` 的记录格式，只读解析存档原始字段。未启动游戏、未构造替身部队、未修改存档。

| 对象与字段 | 读取值 |
|---|---|
| 达努斯提卡 Settlement 对象 | 65，StringId 为 town_ES1 |
| 得巴娜 MobileParty 对象 | 3954，StringId 为 lord_1_39_party_1 |
| CurrentSettlement，SaveableField 1001 | -1，空引用 |
| LastVisitedSettlement，SaveableProperty 1002 | 65 |
| TargetSettlement，SaveableField 1049 | 65 |
| DefaultBehavior，SaveableField 218 | PatrolAroundPoint |
| ShortTermBehavior，SaveableProperty 1050 | GoToPoint |
| Ai，SaveableProperty 1019 | MobilePartyAi 对象 29047 |
| DoNotMakeNewDecisions，Ai SaveableProperty 11 | True |
| _isDisabled，Ai SaveableField 2 | False |
| HourCounter，Ai SaveableField 6 | 2 |

该状态代入生产 `HoldPartyAt` 的条件 `CurrentSettlement != settlement && TargetSettlement != settlement`，两项分别为 True、False，整项为 False，进城命令跳过；最后仍写入暂停重选 True。此结果由原始字段与生产条件直接确定，不是离线战役模拟。

## 为什么暂停后仍在跑

原版 `SetMovePatrolAroundSettlement` 既下达巡逻，也保留聚落为 TargetSettlement。原版 `AiPartyThinkBehavior.PartyHourlyAiTick` 是普通领主小时竞价落实该命令的入口。

`DoNotMakeNewDecisions` 会让这个小时重选入口提前返回，但 `MobilePartyAi.TickInternal` 的移动决策仍运行。`GetBehaviors` 从保留的 DefaultBehavior 开始，把 PatrolAroundPoint 交给 `GetLandPatrolBehavior` 展开为 GoToPoint，继续选择绕城可达点。它不会因为暂停重选而停步，也不会因为 TargetSettlement 相同就改成进城。

生产约斗每小时都会再调 `HoldPartyAt`，相同目标城使其再次跳过，形成持续不赴约的故障。2026-11-06 的内联 `HoldForDuel` 与当前提取后的 `HoldPartyAt` 在这一条件和暂停顺序上相同，提取重构没有引入该错误。

## 与约珥及大会结束的关系

约珥使用 8 分 Visit 欲望，保留原版短期主动行动。当前日志第 1020、1225 行分别记录长期 GoToSettlement、短期 EngageParty 指向 looters_2535，且 CurrentSettlement 为空。后续采样回到城内。她实际出城追劫匪的路径有现场证据，与得巴娜保留同城巡逻的错误分开处理。

原版胜者日志对象 915 保存玩家和约珥参赛、崔斯坦尼娅获胜，得巴娜不在英雄参赛名单。城镇活动大会列表仍包含对象 91，其创建时间晚于该胜者日志约 0.81 游戏小时。生产 `OnTournamentFinished` 要求双方结束时均在城才登记 DuelDecided，未决定且未到期时 `TendDuel` 会补开大会并继续续赴约意图，故约珥在玩家打完后仍等下一场。

## 已闭合与剩余边界

已闭合的是同城巡逻被约斗错误接管并持续保留的生产条件，以及暂停重选后巡逻仍会走的原版执行链。两次约珥出城也有明确的短期追敌目标。

没有首次巡逻命令的调用栈，也没有本案第一次调用 HoldForDuel 前的完整目标快照，因此不宣称观察到了第一次跳过指令的现场；从现存巡逻状态反推它恰在哪个小时下达，仍属于推断。约斗成立时人在城外已由独立日志证明。这一缺口不改变存档中已实际触发的错误条件，但不足以把历史每一步都还原成现场记录。

本轮只调查并更新结论，尚未修复、未部署。当前行为与待实测继续由 case-envoy state 维护。

## 巡逻身份与派案续接复核

用户继续追问灰袍为何有案却巡逻，以及是否读档时就带着巡逻，并要求首场大会结束总要给结果。

原始巡逻存档对象为 `lord_1_39_party_1`，领袖是南帝国罪犯得巴娜。约珥是 `gw_leader_1_party_1`，当前一轮 19 条 AUCTION 全是 Visit:town_ES1，第一条即如此；同轮六位灰袍共 121 条 AUCTION 的原版巡逻封顶均为 0.03。没有证据表明本次约珥因压制失效而回去巡逻。

前一轮六位灰袍共 370 条 AUCTION，同样全部压到 0.03；其中只有 5 条差事意图为 Beat，都是旧任务收尾后的短暂空档。梵蒂两次、约珥一次、暮光一次、弥瑟一次，下一次有案记录分别在 0.69、0.13、0.37、0.25、0.54 游戏小时内出现。旧案对应罪犯已被俘或抓捕结清，不是案子仍在手却被巡逻覆盖。

当前巡区由 GwpPatrolBeats 按人员 StringId 排序，分到每种文化 StringId 最小的非城堡城；没有按全图“无警察覆盖点”实时重新评分。ResolveIntent 先恢复约斗/其他有效差事，再看已承办案；没有任务或当前目标不可执行时才退到巡区。案件池中有记录也不等于每位灰袍可立刻接到，目标在城、被俘、已有人承办、玩家占案、宽限与差事占用等门槛仍在。

本轮修复采用实际行为和目标共同识别已有进城命令，避免重置有效的既有路径。首场大会按实际轮次或参赛名单给结果，取消仍在城门槛与未到齐补开的分支；缺席排在参赛者后，同名次沿用原版评分。这些结果的原生执行与同版存读接续仍待正常游玩取证。
