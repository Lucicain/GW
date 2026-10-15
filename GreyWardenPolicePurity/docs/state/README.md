# 当前状态（state）

**这个目录是唯一可执行的事实来源。**

## 三个目录的分工

| 目录 | 是什么 | 怎么写 | 能否据此决策 |
|---|---|---|---|
| `docs/state/` | 现在有效的行为、阈值、实现方式 | **原地改写**。结论被推翻就直接改掉旧句子，不追加"但是后来又改成…" | ✅ 唯一依据 |
| `docs/journal/` | 按月归档的开发流水：当时为什么这么想 | 只追加，写完不再修改 | ❌ 仅供追溯思路来源 |
| `docs/reference/` | 不随开发变动的资料（资产管线、发布清单、原始计划、调查方法、本地临时目录保留清单） | 需要时改 | ✅ 流程依据 |

**不得从 `docs/journal/` 恢复任何已不在 `docs/state/` 中的规则、阈值或实现方式。**
流水里充满了"当时的想法"：被用户否决的方案、被推翻的公式、已撤回的补丁。它们保留
下来是为了回答"这条路走过没有、为什么放弃"，不是为了被重新实施。

`code-map.md` 是唯一的例外：它由脚本生成、由钩子刷新，不遵循下面的手写格式，
也不要手改。

## 硬规则要标依据

「不要…」「不得…」「不要恢复…」这类约束以后开发的句子，句末写上它靠什么成立：

```
- 不要放行单独收副手……（依据：C++反汇编 2026-09-24；实机诊断 2026-09）
```

| 依据 | 谁能推翻 |
|---|---|
| `用户裁定` `设计` | 只有用户 |
| `流程` | 用户改流程时 |
| `C++反汇编` | 游戏版本变化时重查 |
| `C#反编译` `运行时反射` | C++ 反汇编，或游戏版本变化 |
| `崩溃转储` `实机诊断` `实机观察` | 任何更强的方法；实机结论还可能被当时的 bug 污染 |

有了更强的调查方法时，`grep -n '依据：' docs/state/*.md` 找出依据比它弱、又是它能回答的规则，
逐条复查。先例：2026-09-24 反汇编 Native 之后，双刀的「分帧补刀」依据被标为待复核，「单独收副手」从经验升级为结构上必需。方法见 `../reference/investigation-methods.md`。

## 每个 state 文件的固定开头

```
> 当前状态：<已部署待实测 / 用户已实测确认 / 调查中未改代码 / 已明确不做>
> 最后验收：<commit hash 或"未建立检查点">
> 已复核至：`abcdef0`（YYYY-MM-DD）
> 覆盖源码：<反引号包起来的文件名或 glob>
> 待实测：<一句话；多条就写"见文末"并在文末列「## 待实测」；没有就写"无">
```

`最后验收` 是**人在游戏里确认行为**时对应的提交，重构不要动它；未验收就写「未建立检查点」。
`已复核至` 可选，只写实际核对过的源码提交与复核日期，不代表实机验收，也不冒充本轮未产生的提交。
头部格式错误或提交不存在会得到提示，不增加提交硬闸。新鲜度检查同时参考该文件最近提交；它不能证明正文正确。
提交时机只按仓库根目录 [AGENTS.md](../../../AGENTS.md)「提交可以自己做，发版和推送等用户提」，本页不另设规则。（依据：流程）

头部每项一行，超过 200 字会被检查点名：`已复核至` 只留最新一次，历次经过写 journal。
文档内容涉及多个职责时按归属拆分，不能通过拼长行或删有效规则保持在 250 行以内；同一行为的其他提及改成链接。（依据：用户裁定 2026-10-08）
`待实测` 只写在子系统自己的文件里；`progress.md` 的「待实测汇总」由检查脚本从这些头部生成，不手写。

## 怎么知道 state 文件已经过期

```bash
python tools/Check-StateFreshness.py
```

它先重建 `code-map.md` 和 `progress.md` 的待实测汇总，再报：覆盖的源码改了而本文件没跟着改
（主要看工作树，因为提交很少）；`覆盖源码` 里的路径已不存在；没有任何 state 声明覆盖的新源码；
改到了尚未提取的子系统文件；state 文件在堆积过程（超过 250 行、3 个以上带日期的小节、头部某项超过 200 字）；
有多少文件声明了「不做自动核对」；硬规则缺依据；引用的临时文件已不存在；journal 重复条目；
遗留分支、工作树、stash；临时目录里没登记的项；Claude 私有记忆里没指回仓库的项目规则。
这些是给 agent 的提示；提交时只拦 journal 重复和代码卫生。
它**不判断文字对不对**，只说该去哪里看。收尾第 1 步会跑它。

`覆盖源码` 里不带斜杠的文件名和 `_Module/…` 相对模块目录，`tools/…` 相对仓库根目录。
只写真实路径，说明放在正文里——括号里顺手写的文件名也会被当成覆盖路径。

「不做自动核对」是出口，不是默认。专项调查的过程应进 journal，不要写成一个
豁免核对的 state 文件。

一个结论要么在这里、要么不存在。如果某条规则你在 state 里找不到，默认它**不成立**，
不要去流水里翻。

## 子系统索引

2026-10-05 起全部源码都有 state 覆盖（`uncovered-baseline.txt` 已清空）。新写的源码必须同轮写进某个 state 文件的 `覆盖源码`。

### 总览与流程

| 文件 | 覆盖范围 |
|---|---|
| [`kingdom-membership.md`](kingdom-membership.md) | v14 入国保护、通用读档离国及外交宣战监听、反馈环境与回退路径 |
| [`progress.md`](progress.md) | 进度总览、待实测与收尾欠账；建议不等于新开发指令 |
| [`build-and-deploy.md`](build-and-deploy.md) | 构建开关、预检脚本、live 镜像、发布包 |
| [`diagnostics.md`](diagnostics.md) | 在役诊断系统与各自的退休条件 |
| [`code-health.md`](code-health.md) | 异常留痕、性能分层结论、有意不做的重构 |
| [`infrastructure.md`](infrastructure.md) | 身份判据、调参、文本与本地化、存档类型 |
| [`player-text.md`](player-text.md) | 玩家词句的独立中英文本入口、生成与核对 |
| [`code-map.md`](code-map.md) | **自动生成**：Harmony 补丁点、注册顺序、文件索引 |

### 执法：案件从立案到结案

| 文件 | 覆盖范围 |
|---|---|
| [`crime-intake.md`](crime-intake.md) | 三个立案入口、人命与声望、罚金、震慑、外交封锁、被俘豁免 |
| [`case-enforcement.md`](case-enforcement.md) | 案件生命周期：派单门槛、结案口径、协力编成、战争跟随 |
| [`case-interceptors.md`](case-interceptors.md) | 立即追截队：归队、船与多余的人去信众村 |
| [`case-envoy.md`](case-envoy.md) | 灰袍使者：宣战前派骑兵去谈、谈判结局、比武大会单挑 |
| [`warden-ai.md`](warden-ai.md) | 灰袍谁拿到什么活、欲望竞价怎么落到原版动作、进信众村、巡区、协力军团引擎补丁 |
| [`warden-mediation.md`](warden-mediation.md) | 替灰袍打出来的战争由灰袍出面调停；灰袍战后讲和 |
| [`deserters.md`](deserters.md) | 战后逃兵：照搬原版，但灰袍兵不当逃兵 |

### 玩家与灰袍

| 文件 | 覆盖范围 |
|---|---|
| [`player-justice.md`](player-justice.md) | 玩家声望涨跌、通缉、入会退会、接悬赏、交差查账 |
| [`player-commission.md`](player-commission.md) | 玩家承办委托的结果判定与交差金额 |
| [`atonement.md`](atonement.md) | 负声望赎罪：权限与占案归属、交接、谈判/战斗结果、申报交差 |
| [`field-arrest.md`](field-arrest.md) | 野外执法：肯不肯谈、两层谈判、对方的兑现方式、宽限、单挑、结案 |
| [`patrol.md`](patrol.md) | 玩家纠察队：声望分档、罚金、收尾 |
| [`dispatch.md`](dispatch.md) | 玩家送信队：开口、选人、付款交易界面、地图行程、交割、工资 |
| [`troop-orders.md`](troop-orders.md) | 玩家练兵订单、随行练兵队补员与退回 |
| [`player-requests.md`](player-requests.md) | 贵族事务：封地重议申诉 |
| [`sparring.md`](sparring.md) | 与灰袍领主切磋（竞技场与野外） |
| [`map-bar-affairs.md`](map-bar-affairs.md) | 地图导航栏的「灰袍事务」按钮 |
| [`warden-gear.md`](warden-gear.md) | 灰袍装备只由入会发放、发放时的原版品质、各物品品质组 |

### 灰袍家族与世界

| 文件 | 覆盖范围 |
|---|---|
| [`family.md`](family.md) | 成员、六个本职与继承、婚姻、与要人的关系、成就兼容 |
| [`clan-banner.md`](clan-banner.md) | 黑底金鸟旗帜、自定义图标、静态材质与美术源 |
| [`village-relief.md`](village-relief.md) | 被焚村庄的善后与收养、重建、请愿、村民打赏 |
| [`warden-training.md`](warden-training.md) | 练兵官实体运兵、守村队换兵、兵种配比取向 |
| [`warden-recruitment.md`](warden-recruitment.md) | 灰袍照原版招兵、信众村的祭司格子与直接招狂信者、灰袍兵不领饷 |
| [`economy.md`](economy.md) | 司法公库收支、无封地家族的船只买卖 |
| [`temporary-parties.md`](temporary-parties.md) | 无领主临时队：借船与退还、口粮 |
| [`firesworn.md`](firesworn.md) | 火誓者设定、子系统入口、待定设计 |
| [`firesworn-villages.md`](firesworn-villages.md) | 信众户、守村队、祭司、势力与志愿兵格子 |
| [`firesworn-rituals.md`](firesworn-rituals.md) | 集会、烈火责罚、锁村、冒烟与存档 |
| [`firesworn-media.md`](firesworn-media.md) | 已验收的仪式配图、地图声与菜单声、素材身份 |
| [`firesworn-armory.md`](firesworn-armory.md) | 军械账本、配装、缴获、战损与开发版查看 |
| [`firesworn-economy.md`](firesworn-economy.md) | 捐赠、地牢、货仓、守村收益、保护费与公库 |
| [`firesworn-parties.md`](firesworn-parties.md) | 传教、运械、卖货 |

### 战斗

| 文件 | 覆盖范围 |
|---|---|
| [`troop-combat.md`](troop-combat.md) | 灰袍兵种等级、整套预设装备、弓手/重步兵/骑士战斗加成 |
| [`dual-blade.md`](dual-blade.md) | 双刀 AI、踢/盾击与换武器互斥、双刀武器分类、左手砍击 |
| [`battle-support.md`](battle-support.md) | 战场援军：三层触发、战力预算、兵种配比、原生耗尽判定 |
| [`warden-resolve.md`](warden-resolve.md) | 灰袍死战不退 |
| [`music.md`](music.md) | 辛迪加分段战场配乐：资格、强度映射、过渡、渲染 |
| [`battle-tactics.md`](battle-tactics.md) | 隘口/盾墙踱步问题：**已裁定不改原版** |
| [`cloth-assets.md`](cloth-assets.md) | 重建资源八块布料与原包对比、当前 live 设置 |

### 专项调查（已闭合）

| 文件 | 覆盖范围 |
|---|---|
| [`training-feedback.md`](training-feedback.md) | 小周练兵交付反馈、存档导入及源码缺口 |

原版婚姻 API 笔记在 [`../reference/vanilla-marriage-reference.md`](../reference/vanilla-marriage-reference.md)，未按 v1.4.8 复核。
