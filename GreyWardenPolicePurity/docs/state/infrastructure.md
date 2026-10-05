# 公共底座：ID、调参、文本与本地化、存档类型

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：2026-10-06 `GwpTuning.Firesworn` 加仪式弹窗距离、狂信者队多城卖货两个数；2026-10-05 `GwpTuning.Firesworn` 换成信众村经济整套数值；`GwpIds.ZealotCaravanPrefix`；狂信者队算无领主办差队（不吃饭）；2026-10-05 `GwpTuning.Firesworn` 换掉狂信者上限比例，加直接招狂信者的户数下限和祭司练兵概率（见 `warden-recruitment.md`）；2026-10-05 删除无依据的 catch 与无人引用的成员（失败不再静默跳过，正常路径不变；见 `code-health.md`）
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
- 版本之间不兼容：不为旧档写心跳、迁移、开局补正或旧格式读取，更新后开新档。（依据：用户裁定 2026-10-05）
  反序列化按当前格式严格解析（例如派遣记录固定 15 段），读到别的格式就报错，不做容错。
- 同一版本必须接得上：进行中的任务、委托、差事、仪式、冷却、待办都进存档，读档后行为与存档前一致。（依据：用户裁定 2026-10-05）
  不存的只有每小时重新推导的状态（欲望意图）、场景或遭遇内的临时状态（遭遇进行中不能存档），以及注释里写明理由的防抖、去重计数。
  2026-10-05 补存了：案件「凑不出兵」冷却、讲和重试待办、悬赏提示冷却、火誓者每日「只烧不信教者」那一掷。

## 界面小工具

- `GwpSingleQueryLinkPatch`：原版单按钮弹窗本来就渲染富文本。只对灰袍记录详情的弹窗，给它挂上链接笔刷，
  并把链接事件转到战役百科；弹窗清空时撤掉。
- `GwpGauntletWidgetUtility`：在 Gauntlet 控件树里按 id 找子控件的辅助方法。
- `GwpFlowStates`：赎罪、悬赏、承办任务三套流程的状态枚举。存进存档的数值不能改。
