---
name: wrap
description: 收尾一轮开发：更新 docs/state、往当月 journal 追加一条、报告构建与校验结果。每轮改动结束时用。
disable-model-invocation: true
---

收尾这一轮。**步骤和边界以 `AGENTS.md`「每轮收尾」「提交可以自己做」为准**，这里只放两份模板。
不在这里重复规则：Codex 和 Antigravity 读不到本文件，两处各写一份迟早对不上。

## 0. 跑检查，点名项当轮处理

```bash
python tools/Check-StateFreshness.py
```

## 1. state 文件头部

格式和各字段含义见 `GreyWardenPolicePurity/docs/state/README.md`「每个 state 文件的固定开头」。

## 2. journal 条目模板（追加到 `docs/journal/<当月>.md` 末尾）

```markdown
## <YYYY-MM-DD> <一句话标题>（<已部署待验 / 仅调查未改代码 / 已撤回>）

- **改了什么**：<一两句>
- **为什么**：<用户要求原文，或实测证据>
- **证据**：<日志行、反编译路径、实测读数；没有就写"仅构建期验证">
- **验证**：构建 <0 警告 0 错误>；<测试工程> <N> 项；预检 <类型/成员/加载/补丁绑定>；
  `Verify-LiveModule` <结果>
- **哈希**：隔离预检 / live 客户端 / live 编辑器 DLL SHA256 = `<hash>`
- **回退**：<提交或备份 + 源码回退步骤>
- **未做**：<有意跳过的，及原因>
- **state**：已更新 `state/<file>.md`
```

写进 journal 的是**这一轮发生了什么**；写进 state 的是**现在是什么样**。

## 3. 报告给用户

- 改了哪几个文件（markdown 链接）
- 构建 / 测试 / 预检 / 镜像校验的实际结果，失败就说失败并贴输出
- 部署了没有、提交了没有
- 需要用户进游戏看什么
