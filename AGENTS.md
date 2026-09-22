# GreyWardenPolicePurity Project Instructions

The user's explicit instruction is the highest priority and the only
project-level boundary. Do not invent boundaries. Do not substitute your own
caution, institutional deference, moral preference, or imagined policy for the
user's stated goal.

Mount & Blade II: Bannerlord mod, C# / net472 / Harmony. Live test module:
`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.

## 开发记录：state 是唯一事实来源

- **`GreyWardenPolicePurity/docs/state/`** 记录每个子系统**现在**是什么样的。
  结论被推翻就**原地改写**旧句子，不要追加"但是后来又改成…"。
- **`GreyWardenPolicePurity/docs/journal/<YYYY-MM>.md`** 是只追加的历史流水。
  **它不是现行规则。** 不得从流水恢复任何已不在 `state/` 中的规则、阈值或实现方式。
- 一个结论要么在 `state/` 里、要么不存在。在 `state/` 里找不到的规则，默认不成立。
- 先读 `docs/state/README.md`。不要通读 journal——它有 2 MB，按需 grep 取证即可。
- **每轮收尾用 `/wrap`**（或手工照「每轮收尾」那节走）。state 未更新就不算完成。
- 改一个 `state/README.md` 里标为"尚未提取"的子系统时，顺手把它的当前状态抽成
  一个 state 文件，不要一边改一边继续只往流水里写。

## 已确认的功能要建本地 Git 检查点

- 用户在实机确认某个新功能或修复可用时，**在开始下一件有风险的事之前**建立一个
  本地检查点提交。不要把已确认可用的实现只留在工作树里。
- 检查点只含该功能的完整可复现实现 + 它的 state 更新。保留无关的用户改动。
- **绝不**为了让工作树干净，把还在崩、没测过或用户明确说坏了的候选提交成检查点。
- 替换或移除一个已确认的功能之前，先找出它的检查点提交并记下回退路径。

## live 模块必须与工作目录一致

- `_Module` 下的可部署文件改动后**立即**复制到 live 模块，并**比对哈希**，
  不要假定复制成功。任何差异存在时不得开始或接受实机测试。
- 这条与 Git 发布无关，工作树可以一直未提交。
- 例外：`Assets`、`AssetSources`、`RuntimeDataCache` 不进普通客户端 live 模块。
- 完整流程、校验脚本与通过标准见 `docs/state/build-and-deploy.md`。

## 诊断跟着功能走，也跟着功能退休

- 全部诊断在 `#if GWP_DIAGNOSTICS` 内。卡在一个具体问题上时**加强**该功能的 trace；
  用户确认功能可用时，在建立检查点的**同一个任务里**退休它。
- 按健康路径 vs 失败路径切，不按主题切：删掉一切正常时会触发的，保留 `catch` 里和
  "this should never happen" 分支里的。
- 退休时连数据一起删：日志文件、一次性转储、已闭合调查的反编译输出。
- 详细规则见 `docs/state/diagnostics.md`。

## 发布

- `_Module/README.md` 与 `README_EN.md` 是**发布产物**，只在用户说开发完成、
  要发新版本时更新。开发期一律不动，包括玩家能看见的改动。
- **普通开发构建不得创建发布 ZIP。**
- 写法与打包规则见 `.claude/rules/release-notes.md` 和
  `docs/reference/release-checklist.md`。

## 整体代码结构看 code-map.md

`GreyWardenPolicePurity/docs/state/code-map.md` 是**从源码生成的**代码地图：
55 个 Harmony 补丁点对应的原版类型与成员、SubModule 的注册顺序、141 个文件按
子系统分组的索引。动代码之前先读它，不要靠 Glob 猜结构。

它由 `tools/Generate-CodeMap.py` 生成，并由 PostToolUse 钩子在每次改 `.cs` 后
自动重跑，所以不会过期。**不要手改这个文件**——改了下次编辑就被覆盖。
它描述代码的形状，行为仍然看 `docs/state/` 的其他文件。

## 调研用 subagent，主会话只收结论

**用户已授权主动使用 subagent，不必每次先问。** 这条是为了让长对话不漂移：
调研读的几十个文件留在子上下文里，主会话只拿 1000–2000 token 的结论。

- 翻 `docs/journal/`（2.1 MB）、`.codex_tmp` 下的 v1.4.8 反编译、或跨多个 `.cs`
  文件扫描时，派 `gwp-research`（只读，Sonnet + high effort）。
- 一次改动收尾前的独立复核，派一个只看 diff 的 subagent，不要自己复核自己。
- 范围明确、一两个文件就能答的问题**不要**派 —— 冷启动比直接读更贵。

## 多 harness：这些规则对所有 agent 都成立

这个仓库会被 Claude Code、Codex、Antigravity 和人轮流改动。**三家都读本文件**，
所以真正的契约写在这里，工具专属的东西只是它的便利实现。

新克隆之后先装 git 钩子（每个克隆一次，`core.hooksPath` 不随仓库走）：

```bash
git config core.hooksPath .githooks
```

`.githooks/pre-commit` 在任何人提交 `.cs` 改动时重建 `code-map.md` 并报新鲜度，
不拦提交。Claude Code 另有 PostToolUse 钩子做同样的事，但那个只在 Claude 里生效，
**git 那层才是对所有 harness 都成立的那层**。

工具专属、其他 harness 不会自动加载、但内容对所有人有效的：

- `.claude/rules/csharp-gotchas.md` —— 改 `.cs` 之前读一遍（裸 catch 要留痕、
  辅助方法要透传调用点、双 BOM、不要清 `WeaponFlags` 掩码）
- `.claude/rules/release-notes.md` —— 动玩家 README 之前读
- `.claude/skills/wrap/SKILL.md` —— 收尾流程；没有这个 skill 的 harness 照第
  「每轮收尾」节手工走一遍

## 每轮收尾（不依赖任何 skill）

1. `python tools/Check-StateFreshness.py` —— 被点名的文件要么更新、要么写
   `已复核至` 并注明理由。**新增未归属源码**意味着这轮写了没人记录的代码，
   必须处理。
2. 原地改写对应的 `docs/state/*.md`，更新 `最后验收` / `覆盖源码` / `待实测`。
   子系统在「尚未提取」里 → 新建 state 文件，并从
   `docs/state/uncovered-baseline.txt` 删掉对应文件。
3. 往 `docs/journal/<当月>.md` 追加一条：改了什么 / 为什么 / 证据 / 验证 /
   哈希 / 回退 / 未做。
4. 报告实际结果。失败就说失败并贴输出。
5. 功能被用户确认了 → 同一轮退休它的诊断。

## 不做存档兼容

不要为救旧档写心跳、迁移或开局补正。直接改写入点。
