# 当前状态（state）

**这个目录是唯一可执行的事实来源。**

## 三个目录的分工

| 目录 | 是什么 | 怎么写 | 能否据此决策 |
|---|---|---|---|
| `docs/state/` | 现在有效的行为、阈值、实现方式 | **原地改写**。结论被推翻就直接改掉旧句子，不追加"但是后来又改成…" | ✅ 唯一依据 |
| `docs/journal/` | 按月归档的开发流水：当时为什么这么想 | 只追加，写完不再修改 | ❌ 仅供追溯思路来源 |
| `docs/reference/` | 不随开发变动的资料（资产管线、发布清单、原始计划） | 需要时改 | ✅ 流程依据 |

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
> 已复核至：<可选；读过但确认不影响本文件的更晚提交>
> 覆盖源码：<反引号包起来的文件名或 glob>
> 待实测：<一句话；没有就写"无">
```

`最后验收` 是**人在游戏里确认行为**时对应的提交，重构不要动它。提交只在用户宣布功能开发结束时做，所以大多数时候这里写「未建立检查点」——新鲜度检查会退而用本文件自己最后一次入库的提交做比较基准。纯重构、加诊断这类
读过并确认无影响的改动写进 `已复核至`。

## 怎么知道 state 文件已经过期

```bash
python tools/Check-StateFreshness.py
```

报五类问题：覆盖的源码改了而本文件没跟着改（主要看工作树，因为提交很少）；
没有任何 state 声明覆盖的新源码；改到了尚未提取的子系统文件；state 文件在堆积
带日期的过程（超过 250 行或 3 个以上带日期的小节）；以及有多少文件声明了
「不做自动核对」。它**不判断文字对不对**，只说该去哪里看。`/wrap` 的第 0 步会跑它。

「不做自动核对」是出口，不是默认。专项调查的过程应进 journal，不要写成一个
豁免核对的 state 文件。

一个结论要么在这里、要么不存在。如果某条规则你在 state 里找不到，默认它**不成立**，
不要去流水里翻。

## 已提取的子系统

| 文件 | 覆盖范围 |
|---|---|
| [`progress.md`](progress.md) | 进度总览、待实测与收尾欠账；建议不等于新开发指令 |
| [`temporary-parties.md`](temporary-parties.md) | 无领主临时队向领主/玩家借船与退还、口粮、兵员纯化 |
| [`warden-mediation.md`](warden-mediation.md) | 替灰袍打出来的战争由灰袍出面调停；灰袍战后讲和 |
| [`player-commission.md`](player-commission.md) | 玩家承办委托的结果判定与交差（其余悬赏流程未提取） |
| [`dispatch.md`](dispatch.md) | 玩家送信队的地图行程：出发、找人、交付、交割、工资（对话与付款未提取） |
| [`troop-orders.md`](troop-orders.md) | 玩家练兵订单、随行练兵队补员与退回、已知缺口 |
| [`training-feedback.md`](training-feedback.md) | 小周练兵交付反馈、存档导入及源码缺口；专项调查，未完成练兵全系统提取 |
| [`code-map.md`](code-map.md) | **自动生成**：Harmony 补丁点、注册顺序、文件索引 |
| [`music.md`](music.md) | 辛迪加分段战场配乐：资格、强度映射、过渡、渲染 |
| [`battle-support.md`](battle-support.md) | 战场援军：三层触发、战力预算、兵种配比、原生耗尽判定 |
| [`warden-resolve.md`](warden-resolve.md) | 灰袍死战不退 |
| [`troop-combat.md`](troop-combat.md) | 灰袍兵种等级、整套预设装备、弓手/重步兵/骑士战斗加成 |
| [`dual-blade.md`](dual-blade.md) | 双刀 AI、踢/盾击与换武器互斥、双刀武器分类事实 |
| [`battle-tactics.md`](battle-tactics.md) | 隘口/盾墙踱步问题：**已裁定不改原版** |
| [`case-enforcement.md`](case-enforcement.md) | 案件生命周期：立案门槛、结案口径、协力编成、战争跟随 |
| [`build-and-deploy.md`](build-and-deploy.md) | 构建开关、预检脚本、live 镜像、发布包 |
| [`diagnostics.md`](diagnostics.md) | 在役诊断系统与各自的退休条件 |
| [`code-health.md`](code-health.md) | 异常留痕、性能分层结论、有意不做的重构 |

## 尚未提取（journal 仍是唯一记录）

下列子系统的当前状态**还没有从流水里抽出来**。改动它们之前，先读流水里最新的相关
条目，并把结论补成一个 state 文件——不要一边改一边继续只往流水里写。

- 巡逻与巡区（`PolicePatrolBehavior`、巡逻"恋家"定位）
- 玩家悬赏与结案（`PlayerBountyBehavior`、`GwpCaseArchiveScreen`）
- 使者的对话入口与付款 barter（`GwpWardenDispatchDialogue`、`GwpAssetPayment`）；地图行程已提取到 [`dispatch.md`](dispatch.md)
- 灰袍领主日常练兵与兵种配比取向（`GreyWardenTrainingBehavior`）；玩家订单与随行练兵队已提取到 [`troop-orders.md`](troop-orders.md)
- 野外切磋（`GreyWardenFieldSparringMissionController`）
- 家族、婚姻、村庄收养与重建
- 船运贸易与经济
- 本地化与内容键（`Verify-ContentKeys` 覆盖的范围）
- 灰袍分驻六地、入会装备与新兵发放
