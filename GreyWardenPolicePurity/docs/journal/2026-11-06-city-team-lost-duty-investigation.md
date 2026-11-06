# 瓦拉戈斯堡搜捕队转巡逻调查

用户要求先调研，未修改业务代码、启动游戏或部署。调查基线 `51e2115`，工作树原为空。

## 实际现场

来源：本机 Documents/Mount and Blade II Bannerlord/GreyWarden-AI-Diagnostics.log，2026-11-06 17:44～17:48；读取时 SHA256 为 `8270B01A510B9373C356F97F1D3A86259822C092DEB308D8EFE289BB79E33F52`。以下为字段摘录，时间为日志的 +11:00。

| 日志行 | 游戏小时 | 证据 |
|---|---|---|
| 3180 | 2186138.51 | CITY_CASE_SENT，队伍 gwp_enf_delay_1309，crime=CharacterObject_1588，source=gw_leader_5_party_1，5 人，player=False |
| 3186 | 2186138.66 | intent=Visit:castle_EN1 |
| 3268 | 2186144.55 | 同一目标已存在 CharacterObject_1588_party_1，灰袍另派 gwp_enf_delay_1314 野外使者 |
| 3269、3369 | 2186144.64、2186150.62 | 搜捕队仍为 Visit:castle_EN1，default=GoToSettlement；3369 的短期仍是 GoToSettlement，低分 Flee 候选不等于已逃跑 |
| 3433 | 2186156.62 | intent=none，但旧 default=GoToSettlement、targetSettlement=castle_EN1 尚保留 |
| 3496 | 2186162.67 | intent=none，default=PatrolAroundPoint，targetSettlement=town_K1；short=FleeToPoint，逃兵 deserters_1069 |
| 3790～4086 | 2186186.73～2186210.74 | 逃离后 short=GoToPoint，长期仍 PatrolAroundPoint，意图仍 none |

原版模块 XML 明确 `castle_EN1` 名称为 Varagos Castle（瓦拉戈斯堡）。目标身份为喀拉库吉特的促班；之后日志仍记录其带队攻击村民。没有 CITY_CASE_RESULT 可以把这支队伍视为已正常完成收款。

## 源码链条及证据边界

`CrimeRecord.ShelterOf` 只接受无部队且确实在城镇/城堡的有效英雄；重新领军时返回 null。`CityTeamRules.Decide` 对 ShelterKnown=false 返回 Wait；执行器 Wait 不下达或续期任何移动意图。Visit 默认只活 8 游戏小时，`RemoveExpired` 会移除；`ResolveIntent` 不从 CityCrimeId 恢复城内任务，无领主队又没有普通承办任务或领主巡区兜回。`ProcessFinalDesires` 只有 intent 非空才压低原版巡逻候选，因此意图为空后原版巡逻可成为长期目标。

现场证实了：目标从住城转为有部队；搜捕意图消失；巡逻替换长期目标并持续。源码明确存在“任务保留但移动意图不再维持”的缺口，现场与过期链条相符。但现有日志没记录每小时 CityTeamDecision、最后一次续期/清除及 CityCrimeId/Returning，不能声称已逐帧证明唯一清除调用或某次领主迁移事件。逃兵不是已证实的清任务者：日志先出现 intent=none，后出现巡逻与真实 Flee。

对照 `702a953^`：旧执行器已有 `town == null` 直接 continue 的同一缺口，上轮分层将该行为保留为 Wait，没有新增这个分支，也没有解决它。

## 原版迁移与后续方向

已核对本机 v1.4.8 的 HeroSpawnCampaignBehavior.OnHeroDailyTick/CanHeroMoveToAnotherSettlement/FindASuitableSettlementToTeleportForHero/GetMoveScoreForHero 及 Hero.CanMoveToSettlement，查实事实补入 vanilla/town-scenes.md。无部队的在役英雄符合资格时按日重选聚落并直接瞬移；每日 30% 仅属于逃亡/获释分支，不是所有住城领主共同门槛。原城三倍权重不保证留下，选择没有距离或旅行时长项。此次目标重新领军与城间瞬移是不同路径，不能混称同一事件。

建议后续修复由城内任务归属方表达“暂时没有住城目标”时的明确行动，生命周期保持身份、决定与移动一致；不要简单加高分、永久续期旧城指令或冻结原版逃跑。先补齐该分支、意图最后续期及清除原因的有限诊断，核对案件是否仍开放/是否返回，再确定重新领军后等待、返程或交接的实际规则；本轮只登记缺口，不擅自改玩法。
