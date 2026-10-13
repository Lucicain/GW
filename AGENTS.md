# GreyWardenPolicePurity Project Instructions

The user's explicit instruction is the highest priority and the only
project-level boundary. Do not invent boundaries. Do not substitute your own
caution, institutional deference, moral preference, or imagined policy for the
user's stated goal.

Mount & Blade II: Bannerlord mod, C# / net472 / Harmony. Live test module:
`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.

## 开工先读

- `GreyWardenPolicePurity/docs/state/README.md` —— 各子系统**现在**的样子，以及哪些还没提取。
- `GreyWardenPolicePurity/docs/state/code-map.md` —— 从源码生成的代码地图。**只通读前两节**
  （Harmony 补丁点、SubModule 注册顺序，约 9 KB）；后面的类型索引和文件索引是查表用的，
  `grep -n 'CrimePool' docs/state/code-map.md` 这样查，不要整份读。这个仓库里约一半的类型
  所在文件名不含类型名，找类型查这张表，不要靠文件名猜。不要手改它。
- `GreyWardenPolicePurity/docs/state/progress.md` —— 进度、欠账，以及从各 state 头部生成的「待实测汇总」。
- `docs/journal/` 超过 2 MB，**不要通读**。先 `grep -n '^## ' docs/journal/2026-09.md`
  看条目标题，再用 `sed -n '起,止p'` 只读需要的那段。

## 开发记录

- `docs/state/` 是唯一事实来源。结论变了就**原地改写**，不要追加"后来又改成…"。
  过程归 journal；超过 250 行或混入多个职责时，按职责拆分，索引链接过去，不靠合并长行、删有效规则压到阈值以下。
  同一行为只在所属 state 定义，其他页面引用它。（依据：用户裁定 2026-10-08）
- `docs/journal/<YYYY-MM>.md` 只追加，记录**当时为什么这么想**，不代表那样想是对的。
  它的用处是以后纠错时能追到某个思路从哪来、错在哪一步。**它不是规则**：不得从 journal
  恢复任何已不在 state 里的规则、阈值或实现。state 里找不到的规则，默认不成立。
- state 里的硬规则（「不要…」「不得…」）句末标依据：`用户裁定`、`设计`、`流程`、`C++反汇编`、
  `C#反编译`、`实机诊断` 等。用户裁定只有用户能改；其余的，出现更强的调查方法时要复查。
- 用户对模组和项目流程的新反馈、裁定和新给的资源，当轮写进对应 state：裁定标 `用户裁定 YYYY-MM-DD`；
  仓库外的素材（卡牌、配乐、美术源文件）写绝对路径和身份（如「过渡素材，之后替换」）。
  推翻旧裁定就原地改掉旧句子。不得只留在对话或某个 agent 的私有记忆里。
  agent 的权限、审批、工具设置是会话设置，不写进仓库。（依据：流程）
- state 只写现在，经过写 journal。发现了但没修的缺陷写成一行 `已知缺陷：…`，progress.md 会自动汇总。
- 改到 `state/README.md` 里「尚未提取」的子系统时，顺手把它提取成 state 文件。
  专项调查的过程写 journal，不要写成豁免核对的 state 文件。

## 原版行为不明时先查证

当前村庄后台开发范围按 [火誓者总览](GreyWardenPolicePurity/docs/state/firesworn.md) 的冻结裁定执行，接手先核对该页。（依据：用户裁定 2026-10-14）

不要靠猜测反复实测去试出原版机制，也不要让用户实测来回答本该从原版代码里查到的问题。
先反编译（托管层）或反汇编（`TaleWorlds.Native.dll`），查实规则再改代码。
工具、步骤和证据强弱见 `docs/reference/investigation-methods.md`。

## 每轮收尾

任何 harness 都照做（Claude Code 里可用 `/wrap`，内容相同）：

1. `python tools/Check-StateFreshness.py`，让项目回到「项目的整洁标准」。点名项当轮处理，
   确实不该动的在报告里说明。不要用 `--no-verify` 绕过 git 钩子。
2. 原地改写对应 state 文件的头部：`当前状态` / `最后验收` / `已复核至` / `覆盖源码` / `待实测`。
   每项一行；`已复核至` 只留一个真实提交号与复核日期，格式为 `` `abcdef0`（YYYY-MM-DD）``；
   它表示复核到的源码基线，不冒充本轮尚未产生的提交。经过写 journal，格式检查只提示、不拦提交。（依据：用户裁定 2026-10-08）
   待实测只写在子系统自己的文件里，
   progress.md 的汇总是生成的。
   提取了子系统就从 `docs/state/uncovered-baseline.txt` 删掉对应文件。
3. 往当月 journal 追加一条：改了什么 / 为什么 / 证据 / 验证 / 哈希 / 回退 / 未做。
4. 报告实际结果，失败就说失败并贴输出。
5. 用户确认功能可用 → 同一轮退休它的诊断。
6. **收尾不等于提交。** 只有这件事到了下节说的稳定点才提交；没到就留在工作树里，下一轮接着做。

## 项目的整洁标准

整洁是项目的状态，不是某个 agent 的习惯。无论哪个 harness、什么时候接手，项目都应该是下面这样；
一件事结束时，项目回到这个状态。（依据：用户裁定 2026-10-07）

- **事实只有一份。** 现在成立的写在 `docs/state/`，经过只在 journal。对话、harness 的记忆和配置里
  不存项目知识，最多存一个指回仓库的指针。
- **生成的东西与源头一致。** code-map、progress.md 的待实测与已知缺陷汇总都由脚本重建，不手写。
- **journal 只追加，不重复。**
- **共享的判断只有一处。** 战力、谁算灰袍、案件状态、差事占用各有一个归属方（`docs/state/code-health.md`
  「共享概念的归属」）。新功能用到这些概念就调归属方，缺什么补在归属方里；出现新的共享概念，先给它一个归属。
  同一个判断写两份迟早分叉，修补又只修出问题的那份。（依据：用户裁定 2026-10-07）
- **没有无主的东西。** 分支、工作树、stash，以及 `.codex_tmp/`、`build-check/` 里的每一项，要么属于一件
  还在进行的事、在 state 或 `docs/reference/local-scratch.md` 里写明，要么不存在。state 里的回退指向
  提交号，不指向临时文件。

`python tools/Check-StateFreshness.py` 报出项目哪里偏离这个标准。git 钩子只拦机械错误
（journal 重复、代码卫生），其余由接手的人判断处理。

## 提交可以自己做，发版和推送等用户提

- **提交时机是"一件事到了稳定点"，不是"一轮结束"。** 稳定点指下面任一种：
  - 用户实测确认可用；
  - 一个功能或修复做完：构建通过、测试通过、已部署到 live，等用户实测；
  - 马上要动一处有风险的改动，需要一个能退回去的点。
- **不单独提交的**：只改了 journal 或 state 的轮次、一次调查里的中间一步、一个功能做到一半。
  这些留在工作树里，跟着这件事的下一个稳定点一起提交。
  同一问题仍在试方案、预计继续改或撤的部署也属于中间步骤；构建通过不自动等于方案稳定。
  完整独立修复仍可在部署后等待用户实测时提交；不按每日提交数设配额，不为凑次数改写已有历史。（依据：用户裁定 2026-10-08）
- **粒度**：一个提交只含一个功能（或一个修复）和它的 state、journal 更新，不要把几件无关的事打成一包——
  以后只想撤一个时才撤得开。同一件事做一整天，通常也只有一两个提交。
  反例（依据：用户裁定 2026-09-27）：09-27 一天 39 个提交，双刀动画流水线 50 分钟提交了 11 次，
  其中不少只改了 journal 的几行。
- **推送、打 tag、建 Release、出 ZIP、改 `_Module/README.md` / `README_EN.md`**：
  只在用户提出新版本时做。普通开发构建永远不出 ZIP。「上一版」是哪个提交，以
  `docs/state/build-and-deploy.md`「已发布版本与下一版的对比基线」为准，不要只看 tag 名。发版按
  `.claude/rules/release-notes.md` 与 `docs/reference/release-checklist.md` 做。
- 仓库 `Lucicain/GW` 是公开的，但玩家只看 Release 页：单独推送不产生玩家可见的东西。
  云端历史不删、不改写已推送的提交；本地 `.git` 可以瘦身。（依据：用户裁定）
- 替换或移除已有功能前，先在 state 文件里记下回退路径（哪个提交、哪些文件）。

## live 模块必须与工作目录一致

- `_Module` 下的可部署文件改动后**立即**复制到 live 模块并**比对哈希**。
  有任何差异时不得开始或接受实机测试。
- 例外：`Assets`、`AssetSources`、`RuntimeDataCache` 不在镜像范围内；本机 live 一直保留它们（开发版布局），
  发布或测试后都不挪走。`AssetPackages`、`DsAssetPackages` 也不向 live 镜像；开发版 live 不保留打包资源目录，
  Client/Editor DLL 保留监控。玩家包使用打包美术、无监控，不含可编辑目录；发行版布局只在发布 ZIP 里做。（依据：用户裁定 2026-10-07）
- 流程、校验脚本与通过标准见 `docs/state/build-and-deploy.md`。
- 实机测试只由用户做：agent 不启动游戏、不操控游戏界面。交付时给构建、部署、哈希、离线测试结果
  和一份「进游戏看什么」。部署时 DLL 被占用说明用户在玩：保留工作树，等关游戏再部署。（依据：用户裁定 2026-09-25）

## 诊断跟着功能走，也跟着功能退休

- 诊断的加、删、加强和日志清理归 agent 负责，不等用户吩咐，也不请用户读日志。
  难解的问题和体验问题，先加强诊断取证，配合反编译 / 反汇编查实原版规则。
  用户说"测过了"时，agent 自己读在役日志核对待观察项。
- 全部诊断在 `#if GWP_DIAGNOSTICS` 内。卡在具体问题上时加强该功能的 trace；
  用户确认功能可用时，同一轮退休它。
- 按健康路径 vs 失败路径切：删掉一切正常时会触发的，保留有依据的 `catch` 里的
  （没有依据的 catch 本身就不该存在，见「兜底与死代码」）。退休时连日志、转储、已闭合调查的反编译输出一起删。
- 详见 `docs/state/diagnostics.md`。
- 清理本地 `.codex_tmp/` 之前先看 `docs/reference/local-scratch.md` 的保留清单：
  那里有唯一副本的盾牌资产包和崩溃取证结论，删了就找不回来。

## 兜底与死代码

用户裁定（2026-10-05）：兜底必须真的兜住过东西，没触发过的就是垃圾；没人用的常量、成员同理。

- 不写「以防万一」的 try/catch。只有故障实际发生过，才在那一处加 catch，并在 catch 里写 `依据：`（方法、日期、抓到了什么）。
- 改掉一套机制时，它的常量、文本键、辅助方法一起删，不留旧旋钮。
- `python tools/Check-CodeHygiene.py` 检查这两条，预提交钩子遇到违规会拒绝提交。不要绕过钩子。

## 改 `.cs` 之前

读 `.claude/rules/csharp-gotchas.md`（Claude Code 自动加载，其他 harness 不会）。要点：

- 不写没有依据的 `catch`（见上节）；有依据的写成 `catch (Exception gwpQuietFailure)`，
  catch 里写 `依据：` 并调 `GwpFaultTrace.WriteQuiet`
- 不要为了分类清除 `WeaponFlags` 掩码
- 不要为了打印再调一次原版评分方法（会改它的状态）

## 接入 harness

- 本文件是唯一入口，项目规则只写在这里。不为某个 harness 另写一份项目说明：**仓库目录里**
  （本目录及其上级目录）不要新建 `CLAUDE.md`、`.claude/CLAUDE.md`、`CLAUDE.local.md` 或 `GEMINI.md`，
  前三个会让 Claude Code 不再读本文件，`GEMINI.md` 在 Antigravity 里优先于本文件。用户主目录的
  `~/.claude/CLAUDE.md` 不在此列，它放跨项目的个人习惯。
- harness 自带的记忆、规则、技能只放个人习惯或指回仓库的指针。Codex 的全局记忆对本项目关闭
  （`.codex/config.toml`）；Claude Code 项目记忆里的 project/feedback 条目要写 `仓库位置：<相对路径>`，
  检查脚本会点名没写的。（依据：用户裁定 2026-10-07）
- 新 harness 接进来时，确认三件事：它读本文件；它能跑 `tools/` 下的 Python 和 git 钩子；它自己的记忆
  按上一条设好。各 harness 的便利功能（Claude Code 的 `/wrap`、`.claude/rules/`、code-map 刷新钩子）
  只是加速，不是规则来源。
- 新克隆后设一次 `git config core.hooksPath .githooks`。

## 跟用户说话

- 分清三种来源：用户说过的、原作（卡牌、游戏）原文、agent 的推断。只有用户明确说过的才能叫「你定的」。
  （依据：用户裁定 2026-09-26）
- 讲 git 用「存档 / 读档」：`git log --oneline` 看存档，`git show <hash>` 看内容，
  `git checkout <hash> -- 文件` 取回单个文件，`git revert` 安全撤销；`reset --hard` 先警告会丢东西。

## 存档：版本之间不兼容，同一版本必须接得上

用户裁定（2026-10-05）：

- **不做版本之间的兼容。** 不要为救旧档写心跳、迁移、开局补正或旧格式读取，直接改写入点。
  更新后旧档读不进来或状态不对，就开新档。
- **同一个版本里，存档再读档，游玩必须原样接上。** 进行中的任务、委托、差事、仪式、冷却、待办都要进存档；
  读档后行为与存档前一致。只放在内存里、读档就丢的进行中状态是 bug。
  确实不必存的（例如每小时重新推导的欲望意图、战斗场景内的临时状态），在代码里写一句为什么不存。
