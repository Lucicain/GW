# 公共底座：ID、调参、文本与本地化、存档类型

> 当前状态：现行规则；灰袍中立操作由公共入口延后至玩家战斗收尾
> 最后验收：未建立检查点
> 已复核至：`93d5bd6`（2026-10-15）
> 覆盖源码：`GwpCommon.cs` `GwpStrength.cs` `GwpIds.cs` `GwpTuning.cs` `GwpText.cs` `GwpTextKeys.cs` `GwpSaveableTypeDefiner.cs` `GwpFlowStates.cs` `GwpGauntletWidgetUtility.cs` `GwpSingleQueryLinkPatch.cs` `tools/Verify-ContentKeys.ps1`
> 待实测：同一存档存读接续——进行中的悬赏、送信队、灰袍差事、火誓者仪式，读档后原样接上；左下角提示按「弹给玩家的提示怎么写」改写（不再出现声望数值、保护期天数、「梁子」之类），用户看是否还有别扭的说法

练兵官自队额外经验和旧军械 25%/最小 10 件旋钮已随机制删除；玩家订单经验保留，见 [运兵](warden-training.md) 和 [军械库](firesworn-armory.md)。（依据：用户裁定、源码复核 2026-10-15）

各子系统共用的东西。行为规则不在这里写，在各自的 state 文件里。

未组队战力归属整理回退点为 `a898237`：`GwpStrength.cs`、`GwpFireswornParties.cs` 和 `tools/Check-CodeHygiene.py`。只迁移未组队士兵的估算入口，兵源、顺序、人数与估算参数不变。

讲和只走 `GwpPeace`，规则只在 [调停与战后讲和](warden-mediation.md) 定义。（依据：源码复核 2026-10-14）

## 身份判据（`GwpCommon`、`GwpIds`）

「谁算灰袍」只在 `GwpCommon` 有一份：`IsGreyWardenClan`（家族）、`IsGreyWardenClanMember`（英雄）、
`IsGreyWardenLord`、`IsGreyWardenClanParty`（归灰袍家族的部队）、`IsGreyWardenLordParty`、
`IsGreyWardenEnforcementParty`（灰袍家族的部队加玩家纠察队）、`IsPlayerGreyWardenMember`（玩家入会）、
`IsEnforcementConversation` / `IsOrdinaryGreyWardenLordConversation`（对话场合）、`FindGreyWardenSpokesperson`
（代表灰袍出面的人：族长，出不了面时第一个能出面的成员）。别处不直接比家族 ID。
（依据：设计 2026-10-07；`Check-CodeHygiene.py` 的归属检查拦截）

- 封地申诉多要求玩家是某王国的正式封臣（`IsPlayerGreyWardenVassal`，见 `player-requests.md`）；
  军团混乱豁免多认练兵队和送信队（`GwpArmyExitDisorganizedPatch`）。额外条件写在各自的名字里。
- 灰袍家族 id `gw`。领主 `gw_leader_*`；`gw_leader_0` 兼作领主与收养女孩的模板。
- 灰袍兵种：ID 以 `gw` 开头即算（`IsGreyWardenTroopId`），不区分大小写。
  影响面：不当逃兵、算灰袍战场 NPC（触发援军、死战不退、配乐资格）、练兵加经验、派遣只许派这些兵。
  新加以 `gw` 开头的兵种会自动获得以上全部待遇。
- 「灰袍关联角色」= 灰袍兵种、自定义战斗灰袍指挥官、灰袍家族成员（`IsGreyWardenAffiliatedCharacter`）。
- 无领主办差队按 ID 前缀认：练兵队 `gwp_cohort_`、纠察队 `gwp_patrol_`、追截/延迟纠察 `gwp_enf_delay_`、
  招募使者 `gwp_recruit_`、送信队 `gwp_dispatch_`、门徒 `gwp_disciple_`（`IsLeaderlessDutyParty`）。这些队一律不吃饭。
- `TryDestroyParty` 等收拢 try/catch 的辅助方法透传 `Caller*` 参数，故障留痕记在真实调用点。

## 战力判断（`GwpStrength`）

大地图上「谁比谁强」只在这里算：立案门槛、悬赏难度、派单、协力编成、现场宣战、野外执法都调它。
（依据：设计 2026-10-07；归属检查拦截直接读 `EstimatedStrength` / `GetPowerOfParty` / `GetTroopPower` / `GetTotalLandStrength*`）

- 口径照原版 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior`：单队用 `PartyBase.EstimatedStrength`；
  战斗组 = 攻城营总和 / 已挂靠的军团 / 本队；区域战力加加入半径内圈全额、外到两倍半径线性折算的
  周边可参战部队，已在打仗时按 `MapEvent` 实际参战方，追同一目标的友军全额。（依据：C#反编译 2026-07-24/25）
- 名义上属于某军团、还在赶去会合的部队**不**算进那支军团，只算它自己——原版 `Army.EstimatedStrength`
  也只数军团长和已挂靠的成员。（依据：C#反编译 2026-10-07；实机诊断 2026-09-26）
- `CaseTarget`（目标本身多强，不含路过的人）给立案门槛和悬赏难度；`Compare`（现场双方）给现场宣战和野外执法。
- `LeaderlessRoster` 给尚未建队的无领主健康士兵估算战力；调用方只提供兵种、顺序与人数，本入口累加原版 `GetTroopPower`（Defender、Estimated、领主修正 0f）。已有部队仍用 `Party`，不绕过部队修正。
- 战斗场景内的兵力（配乐、战场支援）按 Agent 算，是另一回事，不走这里。

## 调参（`GwpTuning`）

练兵老兵派出/剩余占比与高占比概率翻倍参数集中在 `Training.VeteranDispatchTriggerShare/TargetShare/BoostShare/ChanceMultiplier`，行为只在 [运兵](warden-training.md) 定义。（依据：用户裁定、源码复核 2026-10-15）

数值集中在这里，按子系统分组。数值的含义写在各子系统的 state 文件里；改数值时同步改对应 state。

火誓者补兵系数复用已查证的原版民兵退役系数乘用户选定的加速倍数；取消的烧民兵额外限额常量已删除。当前行为只在 [村庄](firesworn-villages.md) 与 [仪式](firesworn-rituals.md) 维护。（依据：源码复核、用户裁定2026-10-09）

## 文本与本地化

- 中英文词句、情境及生成规则只在[玩家文字](player-text.md)维护。行为代码用 `GwpText.Get(GwpTextCatalog.gwp_xxx, "VAR", value)`，英文默认文本由编译常量带入，中文由生成的原版语言XML提供。
- 变量：TextObject 原样嵌入，int 按整数，float 保留两位，其余转成字符串（`GwpText.Set`）。
- 对话里用的整句变量名集中在 `GwpTextKeys`。
- 罪名在案卷里存的是写入时那门语言的文字，显示时由 `GwpText.CrimeType` 中英两种写法都认，再翻成当前语言。
- `Verify-ContentKeys.ps1`（pwsh）：XML、字面量、生成的谈判键都必须在中文文件里有条目，`MISSING=0` 才算通过。
  复用原版键（例如 `{=2YYRyrOO}`）也会被报缺失，要用自有键。
  不能仅凭「没被引用」就删字符串；删掉一套机制或一条提示时，它自己的文本键随之删除（AGENTS.md「兜底与死代码」）。（依据：流程）

### 弹给玩家的提示怎么写

左下角提示（`InformationManager.DisplayMessage`、`MBInformationManager.AddQuickInformation`）照下面四条写（依据：用户裁定 2026-10-14：提示有提示词泄露、用语像 AI）：

1. 只提示会影响玩家下一步的事。关系、声望这类界面上看得到的数值不弹提示，也不在提示后面拼「声望：X（±N）」。
2. 不写机制用语：声望数值、区间、保护期天数、原版、候选名单、卷宗、任务面板、协办、实缴应缴的计算方法之类。
3. 书面、简短、陈述句，照原版提示的口气（「某某加入了你的队伍」）；不用口语和俚语（「梁子」「了了」「想怎么样就怎么样」「不记这笔账」）。
4. 称呼统一：组织叫「灰袍」，领主叫「灰袍领主」，纠察队叫「灰袍纠察队」；不用「灰袍守卫」「灰袍警察」「练兵官」。

宗教设定与统一方向见 [火誓者](firesworn.md)「人物与玩家文本」；获批改稿已应用，当前词句独立维护于[玩家文字](player-text.md)，开发标识保持原名。（依据：用户反馈与整理状态 2026-10-15）

## 存档

- 本模组的存档类型由 `GwpSaveableTypeDefiner` 注册，基数 2,894,632，原版占 1–1,000,000：
  1 悬赏任务、2 悬赏地图通知、3 赎罪任务、4 收养日志、5 封地重议决议。
  发布后已有的 localId 不能改，否则旧档里对应的对象读不出来。
- 其余状态都用行为自己的 `SyncData`，存成基础类型的并列列表（`List<string>`、`List<int>`、`List<double>`），不注册容器类型。
  这样不会和别的模组的容器定义冲突。
- 版本之间不兼容：不为旧档写心跳、迁移、开局补正或旧格式读取，更新后开新档。（依据：用户裁定 2026-10-05）
  反序列化按当前格式严格解析（例如派遣记录固定 15 段），读到别的格式就报错，不做容错。
  灰袍王国归属统一在读档完成时走一次原版离国，是用户 2026-10-06 明确允许的身份检查；只在 v14 工作树实现，不检查旧版本或存档身份，不放宽旧格式解析，不加周期轮询。（依据：用户裁定 2026-10-06）
- 同一版本必须接得上：进行中的任务、委托、差事、仪式、冷却、待办都进存档，读档后行为与存档前一致。（依据：用户裁定 2026-10-05）
  不存的只有每小时重新推导的状态（欲望意图）、场景或遭遇内的临时状态（遭遇进行中不能存档），以及注释里写明理由的防抖、去重计数。
  2026-10-05 补存了：案件「凑不出兵」冷却、讲和重试待办、悬赏提示冷却、火誓者每日「只烧不信教者」那一掷。

## 界面小工具

- `GwpSingleQueryLinkPatch`：原版单按钮弹窗本来就渲染富文本。只对灰袍记录详情的弹窗，给它挂上链接笔刷，
  并把链接事件转到战役百科；弹窗清空时撤掉。
- `GwpGauntletWidgetUtility`：在 Gauntlet 控件树里按 id 找子控件的辅助方法。
- `GwpFlowStates`：赎罪、悬赏、承办任务三套流程的状态枚举。存进存档的数值不能改。
