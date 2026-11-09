# 普通 NPC 对白与战斗语音

> 游戏版本：v1.4.8
> 源码：`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Conversation/ConversationManager.cs`、`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultVoiceOverModel.cs`、`TaleWorlds.Localization/TaleWorlds.Localization/LocalizedVoiceManager.cs`、`TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/{OrderController,Agent,SkinVoiceManager}.cs`
> 模组接入：[家族的六人声音](../state/family.md#六人声音与性格)

## 对白

1. 对话节点先按会面情境选择文字。普通领主有初见、戒备初见、重逢、友好重逢、敌对、战前交涉等分支，对应不同文本 ID。`ConversationTagHelper` 的语域和人物 Persona 也参与部分对白标签的判断。`CharacterObject.GetPersona` 在未设置时用 `softspoken`。
2. `ConversationManager.ProcessSentence` 更新说话人和当前文本，用 `MBTextManager.TryGetVoiceObject` 查录音候选。各模块的 `ModuleData/Languages/language_data.xml` 可用 `VoiceFile` 注册语音索引。当前语音语言下，`LocalizedVoiceManager` 按文本 ID 合并各模块的路径。SandBox 的英文索引是 `VoicedLines/sandbox_voiced_lines_en.xml`，录音是 `VoicedLines/EN/PC` 下的独立 OGG。
3. `DefaultVoiceOverModel.GetSoundPathForCharacter` 先找包含说话人角色 ID 的路径，带**玩家性别**后缀的匹配优先。随后按说话人的口音、性别、Persona 分层查共享录音。同一层的 Persona 与 `generic` 可同时进入候选池；英雄用自身的随机值从中选一条。部分句子只有放宽口音、性别或 Persona 后才匹配到候选。
4. 帝国口音分 `imperial_high` 和 `imperial_low`，取决于 `ConversationTagHelper.UsesHighRegister`。其他文化和多种强盗也有各自的口音类。普通领主和通用 NPC 多用共享音组；角色有无单独录音，要逐句检查索引。
5. 匹配到路径后，场景对话和地图对话分别由 `CampaignMissionComponent.OnConversationPlay`、`MapConversationTableau.OnConversationPlay` 播放外部语音。缺少语音对象或匹配路径时，这条链只处理文字和动作。

## 战场和场景内喊声

1. `Native/ModuleData/voice_definitions.xml` 的人类语音定义把 `VoiceType` 映射为 FMOD 事件，并可设音高范围、脸部动作和 NPC 专用标志。成人女性的 `skins.xml` 列出 `female_01` 至 `female_05`。战斗声音索引与音高来自脸码的独立字段，人物 XML 的 `voice` 则用于对白 Persona。反汇编证据见[家族的战斗声音与脸码](../state/family.md#战斗声音与脸码)。
2. `OrderController.PlayFormationSelectedGesture` 让选择编队的人喊步兵、骑兵、弓箭手等点名。`PlayOrderGestures` 用发令者 `Owner` 喊冲锋、跟随、停火、盾墙等口令。XML 登记的口令是可用事件，AI 自动下令时是否发声取决于对应路径是否发出请求。
3. 攻击出力、受伤、死亡、惊恐、攀爬、欢呼、控马和溺水等也有对应类型。部分喊声由托管层主动触发，例如 `AgentMoraleInteractionLogic.OnAgentFleeing` 和城镇的 `AlarmedBehaviorGroup`。`Agent.MakeVoice` 把类型、位置和预测模式交给原生引擎。原生动作的发声时机尚未逐项反汇编。
4. 本机 Native 人类语音表有 8 套男声和 5 套女声。每套登记 14 种动作或反应、6 种编队点名、20 种通用命令、8 种阵型、2 种海战命令、9 种联机短语。多个声组可指向同一事件，例如 `female_01` 至 `female_04` 的冲锋、盾墙和全体点名共用 `female/04` 事件。 [TaleWorlds 音频系统](https://moddocs.bannerlord.com/audio-modding/audio-modding-systems/)

### 运行时语音索引与发声链

- `Native/ModuleData/project.mbproj` 用 `soln_voice_definitions` 登记语音 XML。各加载模块的 `XmlResource.GetMbprojxmls` 收集同类登记，`MBObjectManager.GetMergedXmlForNative` 读取并合并。原生回调 `Module.CreateProcessedVoiceDefinitionsXMLForNative` 把语音类型声明并成一个块，同名语音定义合并子项，再把 XML 交给引擎；本机 Native 在 `0x64ACB8` 取回 XML，随后解析 `voice_definitions`、`voice_type_declarations`。这是独立模组登记的读取路径，和 `SubModule.xml` 的托管对象注册是两套入口。
- `Agent.AgentVisuals` 取场上 Agent 的原生视觉对象；`MBAgentVisuals.SetVoiceDefinitionIndex(index, pitch)` 不是只影响预览。原生接口 319 走 `0x6FC7B0 → 0x622820`，核对索引范围后写视觉对象的 `+0x8C4` 索引和 `+0x8C8` 音高；接口 49 `GetAgentVoiceDefinition` 在 `0x6F71C0` 从 Agent 的 `+0x880` 取同一视觉对象，以该索引返回定义名。原生外观初始化也在 `0x6204EB` 调用同一个设置函数。只改托管 `Agent.UpdateBodyProperties` 会改 `BodyPropertiesValue`，不会立即执行这个原生设置函数。
- 接口 157 `MakeVoice` 在 `0x6F8650` 转至 `0x5E9CB0`，再用该 Agent 的视觉对象进入 `0x621510`。后者按语音索引和 VoiceType 查脸部动作及声音事件（`0x62153E`、`0x6215EC`）。引擎自身另有直接调用同一发声函数的路径，例如 `0x60120D` 与 `0x60163C`；只观察托管 `Agent.MakeVoice` 会漏掉这类请求。这些地址对应 v1.4.8、本机 `TaleWorlds.Native.dll` SHA-256 `2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`，证明选音链，未逐项证明原生动作何时叫声。
- `OrderController.AfterSetOrder` 在有 Owner、手势开启时走口令；`PlayOrderGestures` 内部的口令分支检查加载窗口，后面的编队音效/冲锋响应还有自己条件。AI 的 `BehaviorCharge.TickOccasionally` 与 `BehaviorRetreat.TickOccasionally` 直接改 Formation，绕过 `OrderController`。因此能替换一条口令的录音，并不等于战役 AI 使用该战术时原版就会喊。
- `AgentMoraleInteractionLogic.OnAgentFleeing` 在相应条件下以 70% 概率请求 `Debacle`；`AgentVictoryLogic` 胜方 AI 人类有原版胜利动作与请求，随机等待 1–8 秒。单人默认 O 是欢呼轮盘，长按后选动作；同一界面的文字喊话列表只在联机客户端分支构建。用单人的现成动作或新增语音入口时要区分这两种路径。

## 容易想错的地方

- 首次问候和下一句动态自报家门是两个节点。`LordConversationsCampaignBehavior` 的自我介绍外层文本 ID 是 `B7rEq40B`，填写称号、家族或领地等变量。本机三份英文语音索引均无该外层 ID，也无君主模板 `tOanKs9o`。
- 战斗事件路径和音频银行流编号是不同的索引。本机 `voice.bank` 可以从 XML 中的事件 GUID 沿银行记录追到时间线、播放列表、波形和 FSB 流。`female_04` 的 59 个事件共连到 405 个不同流，多数流名以 `jbw_` 开头，溺水走共享女声。`Climb`、`Focus` 和 `AttackGate` 的事件在这份银行未连到录音。`BoardAtWill` 与 `AvoidBoarding` 分别复用移动、停止的录音池。银行记录给出录音候选，原生触发和实机音高仍需分别核对。
- 对白索引可将同一段录音挂到多个文字 ID 或情境。按文件数量估算已有素材，按文本 ID 与说话人判断覆盖。
- 文件夹里也有索引未收录的录音。本机 SandBox 的英文 PC 目录多出 7 条，包括 5 条森林强盗录音和贵族直率组男女各一条 `003`。StoryMode 多出 14 条剧情录音，NavalDLC 的同目录与索引一致。这些差集只说明未列入所查索引。
