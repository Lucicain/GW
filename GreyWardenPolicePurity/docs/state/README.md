# 当前状态（state）

**这个目录是唯一可执行的事实来源。**

## 三个目录的分工

| 目录 | 是什么 | 怎么写 | 能否据此决策 |
|---|---|---|---|
| `docs/state/` | 现在有效的行为、阈值、实现方式 | **原地改写**。结论被推翻就直接改掉旧句子，不追加"但是后来又改成…" | ✅ 唯一依据 |
| `docs/journal/` | 按月归档的开发流水 | 只追加，写完不再修改 | ❌ 仅供取证 |
| `docs/reference/` | 不随开发变动的资料（资产管线、发布清单、原始计划） | 需要时改 | ✅ 流程依据 |

**不得从 `docs/journal/` 恢复任何已不在 `docs/state/` 中的规则、阈值或实现方式。**
流水里充满了"当时的想法"：被用户否决的方案、被推翻的公式、已撤回的补丁。它们保留
下来是为了回答"这条路走过没有、为什么放弃"，不是为了被重新实施。

`code-map.md` 是唯一的例外：它由脚本生成、由钩子刷新，不遵循下面的手写格式，
也不要手改。

## 每个 state 文件的固定开头

```
> 当前状态：<已部署待实测 / 用户已实测确认 / 调查中未改代码 / 已明确不做>
> 最后验收：<commit hash 或"未建立检查点">
> 已复核至：<可选；读过但确认不影响本文件的更晚提交>
> 覆盖源码：<反引号包起来的文件名或 glob>
> 待实测：<一句话；没有就写"无">
```

`最后验收` 是**人在游戏里确认行为**的那次提交，重构不要动它。纯重构、加诊断这类
读过并确认无影响的改动写进 `已复核至`。

## 怎么知道 state 文件已经过期

```bash
python tools/Check-StateFreshness.py
```

比对每个文件的基线和它 `覆盖源码` 的 git 历史，报出「验收之后源码又改过」的文件。
它**不判断文字对不对**，只说该去哪里看。`/wrap` 的第 0 步会跑它。

一个结论要么在这里、要么不存在。如果某条规则你在 state 里找不到，默认它**不成立**，
不要去流水里翻。

## 已提取的子系统

| 文件 | 覆盖范围 |
|---|---|
| [`code-map.md`](code-map.md) | **自动生成**：Harmony 补丁点、注册顺序、文件索引 |
| [`music.md`](music.md) | 辛迪加分段战场配乐：资格、强度映射、过渡、渲染 |
| [`battle-support.md`](battle-support.md) | 战场援军：三层触发、战力预算、兵种配比、原生耗尽判定 |
| [`warden-resolve.md`](warden-resolve.md) | 灰袍死战不退 |
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
- 使者送单与交兵（`GwpWardenDispatchBehavior`、barter 付款模型）
- 练兵与随行训练队（`GreyWardenTrainingBehavior`、拆编重训）
- 野外切磋（`GreyWardenFieldSparringMissionController`）
- 家族、婚姻、村庄收养与重建
- 船运贸易与经济
- 本地化与内容键（`Verify-ContentKeys` 覆盖的范围）
- 灰袍分驻六地、入会装备与新兵发放
