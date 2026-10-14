# 灰袍家族：成员、本职、婚姻与身份

> 当前状态：六位创始领主声音与正面性格已区分并部署待试听；已选外貌保留
> 最后验收：婚姻 `2e7eb6c`（2026-10-14）；外貌 `7a7c779`（用户确认 2026-10-15）
> 已复核至：`72d6712`（2026-10-15）
> 覆盖源码：`GreyWardenFamilyBehavior.cs` `PoliceHeroCreationModel.cs` `PoliceMarriageModel.cs` `GwpWardenMarriageBehavior.cs` `PoliceClanTierModel.cs` `GreyWardenLeaderBalanceBehavior.cs` `GreyWardenLoreBehavior.cs` `GwpAdultCommanderLoadoutPatch.cs` `GreyWardenNotableRelationsBehavior.cs` `GwpEncyclopediaClanPageVM.cs` `GwpAchievementCompatibilityPatch.cs` `GwpPartySizeLimitModel.cs` `_Module/ModuleData/spspecialcharacters.xml` `GwpFounderVoiceRegisterPatch.cs` `tools/Verify-FounderProfiles.ps1`
> 待实测：见文末

灰袍是一个独立家族（`gw`，文化 empire，初始家乡 `village_EN5_1`），不加入王国（见 `crime-intake.md` 外交封锁）。
新成员从村庄收养来，见 [`village-relief.md`](village-relief.md)；地图上谁去做什么，见 [`warden-ai.md`](warden-ai.md)。

## 成员

**创始领主外貌**

族长梵蒂 `gw_leader_0` 的完整 BodyProperties 保留原样；排除[社区候选②](../reference/community-female-face-shortlist.md)，其余五款按领主编号顺序分配。（依据：用户裁定 2026-10-15；顺序分配为实现选择）

| 领主 | 使用候选 |
|---|---|
| 约珥 `gw_leader_1` | ①甜美圆脸 |
| 弥瑟 `gw_leader_2` | ③原帖1号 |
| 圣铎 `gw_leader_3` | ④原帖5号 |
| 晨曦 `gw_leader_4` | ⑤原帖6号 |
| 暮光 `gw_leader_5` | ⑥原帖7号 |

已选脸码与预设weight/build沿用，保留各领主原有age。本轮只调整五人的声音位，族长完整BodyProperties仍不变；规则见本页「六人声音与性格」。外貌定义作用于新档英雄；已有英雄的 StaticBodyProperties 已存档，预览使用新档，不增加读档改脸逻辑。（依据：实现选择；C#反编译 v1.4.8 Hero 存档属性；用户裁定：不做版本间存档兼容）

回退：取回存档 `1f84971` 的 `_Module/ModuleData/spspecialcharacters.xml` 并镜像到 live，即恢复替换前六人的定义。（依据：流程）

灰袍加入其他国家的反馈已在 v14 基线修复，并合入 main。四类入国入口和内战流亡候选已排除灰袍，读档完成后有王国归属则统一走原版退出；用户与日志确认南帝国归属退出及军团关联 4→0。来源、存档、核心模组与载入阻塞结论统一见 [灰袍入国](kingdom-membership.md)，不从日志过程恢复规则。灰袍不加入王国，只在读档完成时检查归属一次，无存档身份特例或周期轮询。（依据：用户裁定 2026-10-06、2026-10-07）

- 六位创始领主 `gw_leader_0`…`gw_leader_5`，名字每次刷新时按本地化模板重写：
  Aethelflaed、Cyneburh、Mildthryth、Wynflaed、Eadgifu、Wulfhild。
- 后继成员全是女性：生成时如果用了男性模板就换成女性模板和体型，名字从固定名池里取，带后缀以免重名。
  百科介绍也是生成的，按本职附一句生平。每天、读档、会话启动时都刷新一遍。
- 所有成年在世的灰袍成员都按战斗员处理，不走原版「武器技能低就当非战斗员」的分工（`PoliceHeroCreationModel`）。
- 创始领主的技能表（`GreyWardenLeaderBalanceBehavior`）用原版帝国强力领主的专精配置，各有侧重。
  只在开新档时写一次；已经嫁进玩家家族的人不再套模板。
  不要在读档时重写：`SyncData` 没有「已应用」标记，读档重写会每次清空 perk、把技能打回模板值。（依据：实机诊断；用户裁定：不做存档兼容）
- 刚成年的灰袍最后一定穿灰袍领主套装。后置补丁挂在原版成年回调本身上，不依赖事件监听器的注册顺序（`GwpAdultCommanderLoadoutPatch`）。
- 部队上限：家族可组建的部队数至少等于成年灰袍领主人数（`PoliceClanTierModel`）。
- 无领主的随行练兵队按订单人数抬高编制上限。原版无领主队只有 20 人的底数，超编会大幅减速（`GwpPartySizeLimitModel`，见 `troop-orders.md`）。
  火誓者的无领主队同样不低于现有人数（见 `firesworn-parties.md`）。

## 本职

六个位子，开局按创始领主依次指定（`EnsureCoreDutyAssignments`）：

| 领主 | 本职 | 做什么 |
|---|---|---|
| `gw_leader_0` | 练兵 | 练自己队里的灰袍兵，练满后去信众村和守村队换兵，见 `warden-training.md` |
| `gw_leader_1` | 商队护卫 | 派案时先接商队袭击案 |
| `gw_leader_2` | 护村 | 派案时先接村庄暴力案 |
| `gw_leader_3` | 请愿 | 处理原版要人请愿（issue），见 `village-relief.md` |
| `gw_leader_4` | 重建 | 重建被焚村庄，见 `village-relief.md` |
| `gw_leader_5` | 贵族事务 | 玩家的封地申诉等请求，见 `player-requests.md` |

- 新成员成年时补位：选在世持有人最少的本职；人数相同时按重建 → 请愿 → 护村 → 商队 → 练兵 → 贵族事务的顺序。
- 某个本职的最后一位持有人死了，这条线就断了，后来的孩子不会再补上。（依据：设计）
- 本职名称和百科生平提示随本职变化。

## 婚姻

- 灰袍女性只能嫁给玩家，不和任何 AI 家族成员结婚（`PoliceMarriageModel`）。（依据：设计）
- 新档（角色创建结束）时玩家与灰袍的关系设为 100。原版两位英雄的关系落在双方族长之间，所以这就是玩家与每位灰袍领主的关系；之后照原版涨落。
  （依据：用户裁定 2026-10-14；C#反编译 v1.4.8 DefaultDiplomacyModel.GetHeroesForEffectiveRelation）
- 玩家能和任意一位成年灰袍领主结婚：领主对话主选项「做我的妻子吧」，当场照原版 `MarriageAction` 成婚，不走原版求爱说服和嫁妆谈判；
  她按原版婚姻进玩家家族（队伍解散、成为流亡者，等玩家去接）。条件照原版婚姻模型（性别、未婚、不在战斗或军团里、未与他人求爱到高阶段）。
  原版不让两位族长成婚，玩家永远是族长：求的是灰袍族长时只看其余条件，成婚前照原版 `ChangeClanLeaderAction` 把族长位子交给本家族另一位活着的成年灰袍领主
  （创始领主按编号先），族长身上的钱也随之交过去——灰袍公库就是族长的钱（`PoliceResourceManager`）。没有人可以接位时不提供求婚。
  （依据：用户裁定 2026-10-14：要能随便和任意一位灰袍领主结婚，以后给每人做故事线；C#反编译 v1.4.8 DefaultMarriageModel、MarriageAction、ChangeClanLeaderAction）

## 与要人的关系

- 灰袍家族成员与所有聚落要人的关系恒定 100（`GreyWardenNotableRelationsBehavior`）：
  每天、读档、新英雄出生、成年时都刷新；后置补丁挡住原版改写这份关系。
  目的是让灰袍没有封地也能招兵、拜访、和要人交互。

## 对话与百科

宗教背景与文本方向统一见 [火誓者总览](firesworn.md)「人物与玩家文本」；旧「世俗警察家族」文字已按获批改稿替换，当前词句见[玩家文字](player-text.md)，前后对照见[文本审阅记录](../reference/faith-text-review/README.md)。（依据：用户反馈 2026-10-15）

- 已认识的灰袍领主见到玩家时，开场白按玩家灰袍声望变化（`GreyWardenLoreBehavior`），创始成员另有百科人物介绍。
- 灰袍家族百科页（`GwpEncyclopediaClanPageVM`）有灰袍事务入口，不设门槛，见 `map-bar-affairs.md`。

## 成就

- 原版检测到非官方模组时会停用成就。如果唯一的原因是本模组，就恢复成就
  （`GwpAchievementCompatibility`：后置 `DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved`，
  前置 `AchievementsCampaignBehavior.CheckAchievementSystemActivity`）。

装备反馈：当前 v14 / RBM 环境的领主自动换装入口与高军饷、低补兵预算已核对，见 temporary-parties.md；尚无具体装备槽位的实测因果记录。（依据：C# 反编译、实机诊断 2026-10-07）

## 待实测

- 新档分别会面六人，听原版配音台词的声线与口吻；百科可见各自性格，战斗叫喊按六套声音/音高组合区分。文字自写台词没有新增录音。
- 求的是灰袍族长时，族长位子和灰袍公库先交给编号最前的另一位灰袍领主，灰袍事务照常运转。

## 六人声音与性格

用户要求初始六人的外貌、声音和性格各不相同，性格尽量积极向上，为后续个人剧情保留区别。（依据：用户裁定2026-10-15）

回退：本轮开始前为 `72d6712`，取回 `_Module/ModuleData/spspecialcharacters.xml`、`PlayerText/strings.json` 及生成的文字文件，删除本轮声音语体补丁及专项验证脚本；此前用户选定的六张脸仍在该基线中。（依据：流程）


五项性格顺序为仁慈/勇气/荣誉/慷慨/谋略。正值表示相应倾向，0为中性；六组组合互不相同，数值均为0至2，不再使用12/9等被原版截成2的越界值。（依据：用户方向、实现选择；C#反编译与实际Hero.SetTraitLevel回归2026-10-15）

| 初始领主 | 性格定位 | 五项性格 | 原版对话配音组 | 战斗音色/音高档 |
|---|---|---|---|---|
| 梵蒂 `gw_leader_0` | 沉稳担当、以身作则 | 1/2/2/1/2 | imperial_high_female_curt | female_04 / 16 |
| 约珥 `gw_leader_1` | 爽朗勇敢、重情义 | 1/2/1/2/0 | imperial_high_female_earnest | female_01 / 40 |
| 弥瑟 `gw_leader_2` | 温和体贴、护持弱者 | 2/1/1/1/0 | imperial_high_female_softspoken | female_02 / 32 |
| 圣铎 `gw_leader_3` | 机敏幽默、公正守信 | 1/1/2/1/2 | imperial_high_female_ironic | female_03 / 25 |
| 晨曦 `gw_leader_4` | 热心务实、乐于助人 | 2/1/1/2/1 | imperial_low_female_earnest | female_05 / 36 |
| 暮光 `gw_leader_5` | 温和坚定、慎诺守约 | 1/1/2/1/1 | imperial_low_female_softspoken | female_01 / 22 |

以上为本轮实现选择，用户只指定六人互异、尽量积极向上；不冒充用户逐人钦定。人物简述在PlayerText/strings.json的六个gwp_enc_*键中，与各人本职接续，不另写完整个人剧情。性格由原版新建Hero从XML写入并存档，不在读档/每天刷新中重设。（依据：用户裁定、设计；C#反编译v1.4.8 Hero.SetInitialValuesFromCharacter）

### 对话声音

原先族长curt、另外五人全为softspoken，六人均为帝国贵族口吻，故五人会选到相同配音组。`NPCCharacter.voice`是四种Persona，不是战斗声线。`ConversationTagHelper.EducatedClass`同时决定原版语体标签和DefaultVoiceOverModel的imperial_high/imperial_low选音。晨曦与暮光的固定角色ID在该helper后置补丁中改用日常语体，保持原版字幕与录音一致；不改文化、贵族身份、家族或职责，嫁入玩家家族后仍按角色ID认。其他角色及后继成员照原版。（依据：C#反编译、85项实际游戏DLL回归2026-10-15）

原版SandBox的voice_strings.xml与sandbox_voiced_lines_en.xml包含上述六组各自的首次会面录音。这里复用原版已有配音；模组自写且没有VoiceObject的台词保持无录音，不拿无关录音覆盖。原版素材不在仓库另存一份。（依据：当前v1.4.8语言资源、ConversationManager.ProcessSentence）

### 战斗声音与脸码

成人woman skin有female_01至female_05共五个战斗音色，六人使用音色加不同音高的组合；不能声称有六名独立战斗演员。原始六人战斗索引为3/4/3/3/3/3，后三人的音高同为53，正是相同声音的第二个来源。（依据：原版Native/ModuleData/skins.xml、原始脸码解析）

v1.4.8原生接口401 GetParamsFromKey入口RVA 0x7027B0调用0x58E9A0；415 ProduceNumericKeyWithParams入口0x702A10调用0x590040。解码分别在0x58EC27与0x58EC84取BodyProperties+0x48，即StaticBodyProperties.KeyPart8的低6位声音索引、25至30位音高；音高为档值/63。反向写入在0x5901F7及0x59028F使用同样字段。仅改掩码 `0x7E00003F` 内声音位，所有可见脸部、头发、身材与其他位保持，族长连声音位也原样保留。（依据：C++反汇编与前后XML结构/掩码核对2026-10-15；Native.dll SHA-256 2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a）

专项脚本 `powershell -NoProfile -File tools/Verify-FounderProfiles.ps1` 使用真实游戏的Hero性格截断、ConversationTagHelper、DefaultVoiceOverModel.GetAccentClass和生产Harmony补丁，验证85项：六个不同对话配音组、六组有效战斗声线/音高、六组合法正面特质、婚后仍保留、后继及原版贵族不被覆盖。不启动游戏。主观音色与语气待用户试听。（依据：离线回归2026-10-15）
