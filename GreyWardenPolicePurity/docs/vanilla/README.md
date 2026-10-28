# 原版机制手册

这里讲**骑砍原版是怎样运作的**：一个系统从哪里开始、按什么顺序调用、靠哪些条件和数值决定结果。
我们模组改成了什么样，只写在 `docs/state/`；本目录不写模组的裁定，也不写测试结果。

为什么要有它：骑砍原版庞大，托管层只能反编译、引擎层只能反汇编，常识推不出来。此前每次调查只在 state 留下
一句「不要…因为原版会…」，对原版的理解随会话消失，规则只增不减。手册让这份理解留下来，并替掉那些规则。
（依据：用户裁定 2026-10-28）

## 查原版的顺序

1. 本目录有没有这个系统的手册。
2. 原版源码库：仓库外 `../BannerlordSource/<游戏版本>/`，用 CodeGraph 查（MCP 的 `codegraph_explore` 带 `projectPath`）。
   库缺失或游戏升级时跑 `python tools/Build-VanillaSource.py`。
3. 引擎 C++ 层：反汇编 `TaleWorlds.Native.dll`，方法见 `docs/reference/investigation-methods.md`。

## 每篇手册的格式

```markdown
# <原版系统名>

> 游戏版本：v1.4.8
> 源码：`<程序集>/<命名空间>/<类型>.cs` 的 `方法名` …（库内相对路径）
> 模组接入：<链接到 state 的哪一节>

## 怎么运作
按实际调用顺序讲：谁触发、每一步做什么、什么条件走哪条分支、关键数值。

## 容易想错的地方
直觉会猜错、或我们踩过的点，每条一句并指到源码位置。
```

- 一篇只讲一个系统，不超过 150 行；超了就按子系统拆。（依据：设计）
- 只写从源码或反汇编查实的事。没查实的写「未查」，不写推测。（依据：用户裁定 2026-10-28）
- 游戏升级后，抽查手册里点名的方法是否变了，变了就改手册、改头部的游戏版本。（依据：设计）
- **写或扩一篇手册的同一轮**，把 state 里因为这个原版机制才存在的规则改成一句「原版见 vanilla/xxx.md」或删掉，
  用户裁定不动。规则总数是熵指标，`Check-StateFreshness` 报告它，集中清理时压回基线。（依据：用户裁定 2026-10-28）

## 目录

- [kingdom-membership](kingdom-membership.md)：家族入国动作、属性写入、外交评分与存档恢复的区别
- [town-scenes](town-scenes.md)：城镇与城堡的走动场景，无部队领主住哪、地点与点位、地点人物与切场景
- [town-alarm-and-flight](town-alarm-and-flight.md)：城镇任务里谁逃跑、谁还击，危险来源与逃进门
- [town-mission-troops](town-mission-troops.md)：在城镇任务里带兵，队伍归属、编队、命令表、中立不受伤与场内开打
- [tournaments](tournaments.md)：比武大会何时开、何时结，后台结算、跳过比赛与实际比赛的胜负与装备
- [raid-mechanics](../reference/raid-mechanics.md)：原版烧村与村庄守备（写于本目录之前，暂在 reference，下次改动时迁来）
