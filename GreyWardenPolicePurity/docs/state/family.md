# 灰袍家族：成员、本职、婚姻与身份

> 当前状态：创始领主外貌用户已确认；其余规则现行
> 最后验收：婚姻 `2e7eb6c`（2026-10-14）；外貌工作树XML `05a90cfc3254ccd9…`（用户确认 2026-10-15）
> 已复核至：`1f84971`（2026-10-15）
> 覆盖源码：`GreyWardenFamilyBehavior.cs` `PoliceHeroCreationModel.cs` `PoliceMarriageModel.cs` `GwpWardenMarriageBehavior.cs` `PoliceClanTierModel.cs` `GreyWardenLeaderBalanceBehavior.cs` `GreyWardenLoreBehavior.cs` `GwpAdultCommanderLoadoutPatch.cs` `GreyWardenNotableRelationsBehavior.cs` `GwpEncyclopediaClanPageVM.cs` `GwpAchievementCompatibilityPatch.cs` `GwpPartySizeLimitModel.cs` `_Module/ModuleData/spspecialcharacters.xml`
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

替换 `spspecialcharacters.xml` 的脸码与预设 weight/build，保留各领主原有 age。外貌定义作用于新档英雄；已有英雄的 StaticBodyProperties 已存档，预览使用新档，不增加读档改脸逻辑。（依据：实现选择；C#反编译 v1.4.8 Hero 存档属性；用户裁定：不做版本间存档兼容）

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

- 已认识的灰袍领主见到玩家时，开场白按玩家灰袍声望变化（`GreyWardenLoreBehavior`），创始成员另有百科人物介绍。
- 灰袍家族百科页（`GwpEncyclopediaClanPageVM`）有灰袍事务入口，不设门槛，见 `map-bar-affairs.md`。

## 成就

- 原版检测到非官方模组时会停用成就。如果唯一的原因是本模组，就恢复成就
  （`GwpAchievementCompatibility`：后置 `DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved`，
  前置 `AchievementsCampaignBehavior.CheckAchievementSystemActivity`）。

装备反馈：当前 v14 / RBM 环境的领主自动换装入口与高军饷、低补兵预算已核对，见 temporary-parties.md；尚无具体装备槽位的实测因果记录。（依据：C# 反编译、实机诊断 2026-10-07）

## 待实测

- 求的是灰袍族长时，族长位子和灰袍公库先交给编号最前的另一位灰袍领主，灰袍事务照常运转。
