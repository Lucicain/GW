---
name: wrap
description: 收尾一轮开发：跑影响分析与整洁检查、只在结论变了时更新 docs/state、报告构建与校验结果。每轮改动结束时用。
disable-model-invocation: true
---

收尾这一轮。**步骤和边界以 `AGENTS.md`「改动流程」「收尾」和 `docs/state/workflow.md` 为准**，这里只放命令和模板。
不在这里重复规则：Codex 和 Antigravity 读不到本文件，两处各写一份迟早对不上。

## 0. 跑检查，点名项当轮处理

```bash
python tools/Check-ChangeImpact.py
python tools/Check-StateFreshness.py
```

第一个列出这次改动牵连的子系统和它们的硬规则，逐条确认仍成立；第二个报文档引用失效和项目整洁问题。

## 1. state

只在规则、阈值、行为结论、待实测、已知缺陷变了时改所属 state。头部格式见
`GreyWardenPolicePurity/docs/state/README.md`「每个 state 文件的固定开头」。

## 2. journal（只在有调查过程、被否决的方案或不显然的取舍时写）

```markdown
## <YYYY-MM-DD> <一句话标题>

- **为什么**：<用户要求原文，或实测证据>
- **考虑过什么**：<备选方案及放弃的原因>
- **证据**：<日志行、反编译路径、实测读数>
- **回退**：<提交号 + 涉及文件>
```

普通实现和验证结果写在提交信息里，不另写 journal。

## 3. 报告给用户

- 改了哪几个文件（markdown 链接）
- 影响分析点名了哪些规则、复核结论
- 构建 / 测试 / 预检 / 镜像校验的实际结果，失败就说失败并贴输出
- 部署了没有、提交了没有
- 需要用户进游戏看什么
