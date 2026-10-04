# 公共底座：ID、调参、文本与本地化、存档类型

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：2026-10-05 删除无依据的 catch 与无人引用的成员（失败不再静默跳过，正常路径不变；见 `code-health.md`）
> 覆盖源码：`GwpCommon.cs` `GwpIds.cs` `GwpTuning.cs` `GwpText.cs` `GwpTextKeys.cs` `GwpSaveableTypeDefiner.cs` `GwpLegacySave.cs` `GwpFlowStates.cs` `GwpGauntletWidgetUtility.cs` `GwpSingleQueryLinkPatch.cs` `_Module/ModuleData/Languages/**` `tools/Verify-ContentKeys.ps1`
> 待实测：无

各子系统共用的东西。行为规则不在这里写，在各自的 state 文件里。

## 身份判据（`GwpCommon`、`GwpIds`）

- 灰袍家族 id `gw`。领主 `gw_leader_*`；`gw_leader_0` 兼作领主与收养女孩的模板。
- 灰袍兵种：ID 以 `gw` 开头即算（`IsGreyWardenTroopId`），不区分大小写。
  影响面：灰袍队纯化不动它、不当逃兵、算灰袍战场 NPC（触发援军、死战不退、配乐资格）、练兵加经验、派遣只许派这些兵。
  新加以 `gw` 开头的兵种会自动获得以上全部待遇。
- 「灰袍关联角色」= 灰袍兵种、自定义战斗灰袍指挥官、灰袍家族成员（`IsGreyWardenAffiliatedCharacter`）。
- 无领主办差队按 ID 前缀认：练兵队 `gwp_cohort_`、纠察队 `gwp_patrol_`、追截/延迟纠察 `gwp_enf_delay_`、
  招募使者 `gwp_recruit_`、送信队 `gwp_dispatch_`、门徒 `gwp_disciple_`（`IsLeaderlessDutyParty`）。这些队一律不吃饭。
- `TryDestroyParty` 等收拢 try/catch 的辅助方法透传 `Caller*` 参数，故障留痕记在真实调用点。

## 调参（`GwpTuning`）

数值集中在这里，按子系统分组。数值的含义写在各子系统的 state 文件里；改数值时同步改对应 state。

## 文本与本地化

- 代码里写英文原文并带键：`GwpText.Get("{=gwp_xxx}English text", "VAR", value)`。
  中文在 `_Module/ModuleData/Languages/CNs/std_gwp_strings_xml-zho-CN.xml`。英文没有单独的语言文件，用代码里的原文。
- 变量：TextObject 原样嵌入，int 按整数，float 保留两位，其余转成字符串（`GwpText.Set`）。
- 对话里用的整句变量名集中在 `GwpTextKeys`。
- 罪名在案卷里存的是写入时那门语言的文字，显示时由 `GwpText.CrimeType` 中英两种写法都认，再翻成当前语言。
- `Verify-ContentKeys.ps1`（pwsh）：XML、字面量、生成的谈判键都必须在中文文件里有条目，`MISSING=0` 才算通过。
  复用原版键（例如 `{=2YYRyrOO}`）也会被报缺失，要用自有键。
  没被引用的字符串不能当作死内容的证据，永不删除。（依据：流程）

## 存档

- 本模组的存档类型由 `GwpSaveableTypeDefiner` 注册，基数 2,894,632，原版占 1–1,000,000：
  1 悬赏任务、2 悬赏地图通知、3 赎罪任务、4 收养日志、5 封地重议决议。
  发布后已有的 localId 不能改，否则旧档里对应的对象读不出来。
- 其余状态都用行为自己的 `SyncData`，存成基础类型的并列列表（`List<string>`、`List<int>`、`List<double>`），不注册容器类型。
  这样不会和别的模组的容器定义冲突。
- 不做存档兼容：不为旧档写心跳、迁移或开局补正。（依据：用户裁定）
  现存的旧格式读取只有两处，是这条规则之前写的：
  - `GwpLegacySave.ReadFlag`：bool 与 int 两种存法都认，只用在 `PlayerBountyBehavior.CaseSettlement` 的求援标志上；
  - `GwpFieldReportLedger` 保留旧版本的存档键。
  不要再新增这类代码。（依据：用户裁定）

## 界面小工具

- `GwpSingleQueryLinkPatch`：原版单按钮弹窗本来就渲染富文本。只对灰袍记录详情的弹窗，给它挂上链接笔刷，
  并把链接事件转到战役百科；弹窗清空时撤掉。
- `GwpGauntletWidgetUtility`：在 Gauntlet 控件树里按 id 找子控件的辅助方法。
- `GwpFlowStates`：赎罪、悬赏、承办任务三套流程的状态枚举。存进存档的数值不能改。
