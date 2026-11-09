# 灰袍语音接入调研

面向 Bannerlord v1.4.8 单人实景。现阶段是调研：没有修改游戏代码、打包或安装。Field Officer 的原作记录保留来源、触发和置信度；骑砍另建目标情境。两者按媒体 ID 关联，不能把原作状态名当成骑砍的现成事件。[灰袍语音场景与触发登记](warden-voice-scenes.md)是新增场景的明细，由 `tools/Build-WardenVoiceSceneCatalog.py` 对全部媒体生成逐录音试听页。

## 一个系统，三种入口

| 入口 | 负责什么 | 已查到的接入点 |
| --- | --- | --- |
| 原版发声换录音 | 攻击出力、跳跃、痛呼、倒地或死亡相关的原版声、战吼、胜利、逃跑、马术、原版确实发出的口令 | 独立 VoiceDefinition 和独立 FMOD 事件。实景 Agent 用 `Agent.AgentVisuals.SetVoiceDefinitionIndex` 绑声，以 `GetAgentVoiceDefinition` 验证；保留每类原版 `face_anim`，缺匹配的声音位继续指向原版事件。原生直调同样读取该索引，单 patch C# `MakeVoice` 会漏。见[原版语音机制](../vanilla/npc-voices.md#运行时语音索引与发声链)。 |
| 新增场景喊话 | AI 指挥、遇敌骑兵、求援、遭弓弩、杀敌、弹尽、援军、破门、接舷、胜负等 | 一个单人 MissionBehavior，接收已发生的游戏事件，确定发声者后播放 3D 音频；优先用已存在的 FMOD 事件路径和 `SoundEvent`，本模组已有场景声音实例。轮盘、对话等需要单独接入。 |
| 对白与主动表达 | 初见、重逢、战前、任务、感谢、嘲讽和鼓舞 | 对白跟随实际说话人、文本 ID、语言与玩家语境；角色专属 OGG 和索引。若原台词无语音对象，普通 VoiceOverModel 路径不会执行。玩家主动喊话可扩充单机入口；战术喊话与真正给编队下令分开。 |

原版女声的部分口令共用 `female/04`，灰袍使用自己命名的 VoiceDefinition 和 FMOD 路径。源码已证实在模组 `ModuleData/project.mbproj` 挂 `soln_voice_definitions` 会走合并入口。用独有定义名、复用已声明的原版 VoiceType；FMOD 官方流程在同一项目文件登记 `soln_sound_files`、`soln_sound_event_data`、`soln_sound_parameter_data`，生成对应 XML 和 Banks。见[语音配置](https://moddocs.bannerlord.com/audio-modding/audio-modding-systems/)及[FMOD 银行接入](https://moddocs.bannerlord.com/audio-modding/audio-modding-overview/)。新增短句的文件级声音也可沿用模组已有的 `module_sounds.xml` 路径；不能把文件级声音能播当成 VoiceDefinition 已经接通。

**还需验证**如何稳定解析定义名到索引、绑声后哪些外观刷新会重设索引、银行能否从模组目录加载并出声。不能把类的列表顺序或一个写死的数字当作定义 ID。独立 Bank 路径验证不过，仍可先做场景新增与对白；原生换声继续查加载，不用全局替换绕过去。

## 谁接入

- **凡蒂、六领主、未来具名要人**：个人声组优先。固定创始人使用英雄 ID；以后可由相应身份系统登记真实 Hero 和所用声组。继承人会换 `CharacterObject`（`GreyWardenFamilyBehavior.cs:451`），个人配音不要跟着角色模板名串到另一个人。婚嫁、加入玩家队伍、站在任一战斗方，都不应使已绑定的个人音色被队伍或旗帜覆盖。未来要人没有独立录音时可选适合的共享声组，具体归属由该角色的系统登记；候选资格不等于现在已经配好声音。
- **普通灰袍女兵**：按女兵定义绑定共享兵声池。当前六种为 `gwzealot`、`gwnewrecruit`、`gwrecruit`、`gwheavyinfantry`、`gwarcher`、`gwknight`，见模块 `spnpccharacters.xml`；未来可追加兵种和声组。按当前战斗角色区分近战、弓弩、骑兵和队长；同一人下马后可以换语境，音色不用变。不要让每个女兵都用凡蒂的指挥官台词。
- **身份和职位分别查**：按 Hero 或兵种决定声线；按本场的阵营、队长、总指挥、兵种、位置和目标决定哪句话可以说。用 `Origin` 辅助区分临时增援等具体情境。不能把穿灰袍、同队、入会的玩家、共享女声的外人自动当成目标；现有 `GwpCommon.IsGreyWardenLordAgent` 还包括自定义战斗替身，不直接等同配音身份。首次接入尽量只处理本场 Agent，不迁移存档脸码。

## 战场规则建议

| 场景 | 动作、条件与建议发声时点 |
| --- | --- |
| 发令 vs 士兵响应 | 显式发令保留 `OrderController.AfterSetOrder`。AI 可绕过它：观察已生效的编队战术或命令变化，挑同队真实 `Formation.Captain` / `Team.GeneralAgent`，且说话人已绑定并有资格发令。同一指令在同一编队只说一次。首选冲锋、坚守、集结和有秩序的撤退；原版冲锋后的士兵群声另算。 |
| 砍击、放箭、格挡、受伤 | 高频出力及痛呼保留原生选择；额外短句看有效战斗结果。 `Mission.OnScoreHit` 有实际伤害、武器、格挡和攻城来源，普通触碰或格挡不当作受伤。 `OnAgentShootMissile` 表示实际射出；弓弩投掷可按现用武器分池。盾牌挡住、武器招架、反击和重伤要拆开。 |
| 击倒、死亡、溃逃、胜利 | `OnAgentRemoved` 区分 `Killed`、`Unconscious` 与 `Routed`，在清理前快照说话人、位置和致命来源。死者短声与生者击杀反应分开，普通致伤台词让位。 `OnAgentFleeing` 和胜利有原版请求；不要把听令后撤、士气溃逃、胜后收兵当一回事。战斗胜负用 `OnMissionResultReady`，不能把玩家离开结果页当胜利时点。 |
| 骑兵、增援、弹尽 | 上/下马用 Mission 回调；催马、急刹、马死分别读对应动作、速度变化与坐骑状态，不搬 800 原作单位。援军用原版批次实际生成回调和本模组实际生成入口，合并去重。附近首次确认敌骑逼近后可给观察者发警告，不在看到静止马匹时喊。缺弹可先做从有弹到打空的边沿；“按了没发出”需另核失败输入。 |
| 攻城与海战 | 城门被摧毁用 `DestructableComponent.OnDestroyed` 等实际状态，和普通开门、发令攻门分开。推进冲车、登塔、摧毁攻城器械各挑真实活动。海战可区分下令登船、接舷、搭桥、夺船、沉船，`NavalShipsLogic` 有相应事件，需与原生海战 AI 喊声去重。 |
| 对话、轮盘、任务 | 初见按真实相遇节点；选一句问候时保证字幕和录音说的是同一回事。求援、感谢、嘲讽可做主动表达、战前交涉或实际任务互动。村民、领主、物资与纵火的录音只挂真的任务目标，不把围城或见到路人当通用触发。 |

新增喊话共用调度：按说话人、语义、事件和编队去重，按距离、优先级、在播状态、每队与场景预算限流。先短、近、有用，再考虑长句；一般士兵低频取近处少数人，优先让真正的指挥官说话。出力、受伤、死亡这类原版声仍由引擎按既有机制播放。新增声音跟着说话人位置，退出任务或循环条件消失时停掉；死亡反应只用已有位置快照，别持续持有已删除的 Agent。所有观察只产声音，不写 AI 指令、血量、阵型或士气。

## 整库分配路线

下表按源事件归档，`Auto` / `Manual` 是 Field Officer 源事件前缀的简称；各 bank 的 `Customization` 和 `Soliloquy` 分开计数。它是候选去向，不代表每个同池变体已听校或通过骑砍验证。同一录音可能同时出现在好几行，统计用媒体 ID 去重。

| 源录音族 | 骑砍候选去向与保留条件 |
| --- | --- |
| `Exertion`、`HighPriority`、`LowPriority` | 切换值逐条拆：挥砍、投掷、跳跃和普通痛呼优先替换；格挡、踢、撞、滑落和重创分开。可见火伤、真实跌落或持续喘息才用对应池。骑砍普通战场未查到可通用的同款耐力条，先不复制 30% 或用低血量冒充。 |
| Manual: `BattleCry`、`BattleCryRunning`、`BattleCry_Tenosia`、`Cheer` | 原版战吼、跑动战吼、胜利或主动欢呼。跑动和站立若共用原生 Yell 槽，先选两种姿势都成立的录音；要严格分池需另证发声时能取到跑动条件。阵营名变体单审。 |
| Manual: `FollowMe`、`Forward`、`Hold`、`Retreat`、`Retreat_Tenosia`、`PO4_Reform` | 对应跟随、前进、坚守、有序撤退、集结；按实词区分缓退和撤出。原版没有触发的 AI 口令另走战术变化。 |
| Auto: `Attack`、`AttackWinning`、`Attack_Tenosia`、`Defend`、`DefendWinning`、`DefendWinning_Tenosia`、`Defend_Tenosia`、`DefensePrepare`、`DefensePrepare_Tenosia`、`DefenseShields`、`OffenseCharge`、`OffenseFire` | 战斗开始、部署结束、攻守状态、真实冲锋、射击或盾墙口令。优势与劣势要由骑砍的可测状态判定，暂不设定强行每隔几秒喊一句。带专有阵营词的版本不自动改绑。 |
| Auto: `CavalryCharge`、`CavalryIncomingAlly`、`CavalryIncomingEnemy`、`CavalryIncomingEnemy_Tenosia`、`CavalryWarning`；Manual: `Incoming`、`Incoming_Tenosia` | 骑兵冲锋、己方援军和敌骑预警分开。普通士兵可以预警；集体冲锋口令由指挥者说。 |
| Auto: `HorseAccelerate`、`HorseControl`、`HorseDeath`、`HorseDecelerate`、`HorseMount`、`HorseSoothe` | 骑手口令或反应。普通停马不等于急刹，马死不等于主动下马；无安抚动作则保留相应素材。 |
| Auto: `GateAttack`、`GateDefend`、`GateLost`、`GateWon`、`HoldWalls`、`SiegeDefend`、`SiegeDestroy`、`SiegeFire`、`SiegeRam`、`SiegeRamStop`、`SiegeTower`、`SiegeTowerStop` | 城门攻守、城墙坚守、器械操作、保护、被毁、抵达或停止；只在匹配的攻守方和真实设施上播放。 |
| Auto: `Crippled`、`DeathArm`、`DeathGen`、`DeathLeg`、`Invalid`、`KillArchers`、`Negative`、`NoAmmo`、`Pain`、`Positive`、`Rage` | 通用无词短声可按语义试听归池；具体断肢、失血、用绷带台词需要真正对应的动作。击中敌人、遭射击、友军伤亡、误伤、弹尽分别以已发生事件识别。源 `Invalid` 不能直接拿来给所有骑砍操作失败配音。 |
| Manual: `Help`、`PO3_KillArchers`、`BegCower`、`BegCower_Tenosia` | 主动求援、命令射手优先目标、劝降或求饶。AI 新增发令仅在存在真实目标及正确权限时使用。 |
| Auto: `Revived`、`Reviving`；Manual: `AcceptDeath`、`RefuseDeath`、`RequestDeath`、`RequestHelp` | 源游戏的救人与倒地语音；骑砍普通昏迷并没有同款倒地轮盘和救起。可用于今后确有相应流程的任务、短剧情或角色互动，先保留触发缺口。 |
| Auto: `ObjBurn`、`ObjKill`、`ObjLordDefend`、`ObjLordKill`、`ObjLordKill_Tenosia`、`ObjPeasantDefend`、`ObjPeasantKill`、`ObjSupplyDefend`、`ObjSupplyDefend_Tenosia`、`ObjSupplySteal`、`ObjSupplySteal_Tenosia` | 对应真正的任务或战役交战目标。发声者所在阵营、受保护或袭击对象都必须匹配。 |
| Manual: `Hello`、`Welcome`、`Goodbye`、`No`、`Yes`、`Thanks`、`Oops`、`Laugh`、`PO1_Disappointment`、`PO2_Respect`、`PO2_Respect_Tenosia`、`PO5_GoAway`、`PO6_HowDareYou`、`Taunt`、`Taunt_Tenosia`、`Sit`；Auto: `Sorry`、`Thanks` | 会面、任务对白、主动互动、嘲讽、友伤道歉或战后感谢。保留每句原文；例如短问候不能盖掉报姓名和领地的动态字幕。坐下没有证实对应原作播放，暂不指定自动事件。 |
| `Hippodrome`、`Customization` × 2、`Soliloquy` × 2 | 专用竞技过场、试听、具名角色独白或短剧情。找不到原作请求的独白仍可在骑砍明确设计一个场景；这不改变其原作证据。预览重用的前进/跟随录音只需存一份。 |

计划产物为逐录音记录：`媒体 ID / 原文和原作事件 / 目标动作与状态 / 说话人资格 / 触发边沿 / 目标配音组 / 证据与验收`。每段要有去处（直接替换、可新增、剧情或主动语音、等待对应场景或措辞修订），允许多场景引用同一个文件。库里登记的录音可以都保留，自动播放只放在说得通的时机。像喊出梅森、阿加莎、泰诺西亚和地图地名的录音，逐句判定能否通过合适的场景使用；必要时再用已有 Field Officer 模型做同声线的新口播，不以原版骑砍素材训练。

## 实施顺序与验收

1. **先验证身份隔离及加载**：同一场普通战斗放凡蒂、灰袍女兵、原版共用女声音色的 NPC 作对照。两套独立声组用容易辨认的出力、痛呼、战吼，另测倒地与现有口令；确认读取到目标定义名、目标有声且旁人不变。旧档与新档、补员、切换上下马或刷新外观都要查。此步失败只影响原生换声路线。
2. **上齐核心战场**：逐项验收常见原版声音位，再加实际发令、集结、敌骑靠近、真实重伤和杀敌、弹尽和援军。记录请求、成功提交、冷却或过滤原因；同一命中、指令或批次不会连播，击倒后的角色不会继续喊。场景预算用正常游玩诊断确认。
3. **补齐攻城、海战、主动表达和会面**：先各跑通一条真实流程再扩展，区分场景缺口和资产缺口。每新增一套领主或要人配音只添身份绑定和音池，不重写触发机制。

验收关注实际游戏是否只给目标变声，谁在什么动作、哪一刻、对谁说了哪句话。静态证据证明可用的选择链和回调，尚未验证游戏加载、听感或完整播放矩阵。
