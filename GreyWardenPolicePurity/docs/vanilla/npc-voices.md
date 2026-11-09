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

## 容易想错的地方

- 首次问候和下一句动态自报家门是两个节点。`LordConversationsCampaignBehavior` 的自我介绍外层文本 ID 是 `B7rEq40B`，填写称号、家族或领地等变量。本机三份英文语音索引均无该外层 ID，也无君主模板 `tOanKs9o`。
- 战斗事件路径和音频银行流编号是不同的索引。XML 加事件 GUID 可查事件，`voice.bank` 可枚举和解码流；哪个事件会随机选到哪些流，现有盘点还未逐项对上。
- 对白索引可将同一段录音挂到多个文字 ID 或情境。按文件数量估算已有素材，按文本 ID 与说话人判断覆盖。
- 文件夹里也有索引未收录的录音。本机 SandBox 的英文 PC 目录多出 7 条，包括 5 条森林强盗录音和贵族直率组男女各一条 `003`。StoryMode 多出 14 条剧情录音，NavalDLC 的同目录与索引一致。这些差集只说明未列入所查索引。
