# 项目流程

> 当前状态：按改动走的流程已启用，state 头部取消 `已复核至`；state 已移出验证流水与按轮回退（原文在 journal 2026-10-state-archive.md）
> 覆盖源码：`tools/Check-ChangeImpact.py` `.githooks/*` `.claude/skills/wrap/SKILL.md`
> 待实测：无

[AGENTS.md](../../../AGENTS.md) 是入口，只放每次都要用的流程和硬约束；本页是它们的细则。
项目管理方式的来由（社区做法调研与取舍）见 journal 2026-10-21「项目管理方式」。

## 规则分三级

同一条约束，能机械检查就不写成文字，写成文字的要在改动时被点名复核。迁移是渐进的，见表下。（依据：用户裁定 2026-10-21；渐进迁移为设计——老代码一次补齐全套检查会被告警淹没）

| 级别 | 放在哪 | 谁保证它被遵守 |
|---|---|---|
| 机械约束 | `tools/Check-CodeHygiene.py`（`OWNERS`、兜底、重复函数体）、纯规则测试、构建预检 | 预提交钩子和测试，违反就拒绝 |
| 判断型规则 | 所属 state 的硬规则（带 `依据：`） | `tools/Check-ChangeImpact.py` 在改到它或它的调用方时点名，agent 逐条复核 |
| 背景与过程 | journal、git 历史 | 不约束开发；不得从中恢复规则（依据：流程） |

迁移是渐进的：一条判断型规则被违反过、或这次改动碰到了它且能写成固定判断，就顺手把它变成检查，
并把 state 里那句改成指向检查。不做一次性全量迁移。

## 改动流程的细节

- **影响分析**：`python tools/Check-ChangeImpact.py`（默认对比 `HEAD`，`--base <提交>` 换基线，
  `--depth 2` 多追一层调用方，`--no-rules` 只列文件）。它从 diff 找到改动落在哪些符号上，沿 CodeGraph 的
  调用/引用边找依赖它们的代码，按 `覆盖源码` 映射到 state 并列出硬规则；改到共享概念归属方、删了文件会单独提醒。
  调用图不含 Harmony 绑定、反射和原生调用，没列出的子系统不等于不受影响。（依据：流程）
- **复核与文档**：点名的每条规则，改后仍成立就过；违反了就改代码；规则本身该变（用户新裁定、调查推翻旧结论）就原地改那一句并更新依据。state 只在规则、阈值、行为结论、待实测、已知缺陷变了时才改，源码动了而结论没变不碰文档；journal 只写调查过程、被否决的方案、不显然的取舍，普通实现与验证结果写在提交信息里。（依据：用户裁定 2026-10-21）
- **审查**：涉及共享概念、跨子系统或删改既有机制的改动，提交前由 agent 自行派一个不带编写上下文的独立审查者
  （子 agent 或新会话）只看 diff 和影响报告，不需要再问用户；编写者在同一上下文里按清单自查不算。不指定具体 harness 工具。（依据：设计——不带上下文的审查者更容易发现编写者的盲区；用户裁定 2026-11-06）

## 开发批次与故障修复

- **一次收束一个机制**：每批明确行为变化、唯一归属、验证方式和未闭合证据；玩法调整、分层和通用工具建设不一起扩张。分层先完成当前试点，只有下一处真实改动需要时再迁移；切换后删旧分支、专用常量和临时设施，不长期维护双轨。（依据：用户裁定 2026-11-06）
- **先钉死根因**：从玩家描述、在役日志、转储与原版 C#/C++ 出发；证据不足时只在诊断条件编译内补能回答问题的字段，并在所属 state 写明缺哪段经过。agent 自己读日志与设计取证，玩家正常游玩，不安排专项挂机或概率分支复现。根因闭合可以来自现场加源码，不强制 AI 重演整局。（依据：用户裁定 2026-10-21、2026-10-28、2026-11-06）
- **修在归属方**：资源和案件状态沿唯一入口改变；跨事件操作核对实际相关的重复完成、取消、目标失效与同版本加载接续。能做负对照就保留旧失败、新通过的证据，注明规则级或引擎级；做不到的登记缺口，不以编译或局部通过冒充完整验证。（依据：用户裁定 2026-11-06）
- **分开报告结论**：现象已记录、根因已证实、修复已验证到哪一层、真实相关路径经过 N 次/复发 M 次；复发观测按[诊断](diagnostics.md#加与删的判据)办理。未经过的路径保留待实测，次数不能代替根因证明。用户确认只覆盖其确认的功能范围。（依据：用户裁定 2026-11-06）

### 验证如何选

| 范围 | 使用什么 | 能说明什么 |
|---|---|---|
| 构建与接入 | 编译、补丁绑定/游戏兼容、配置引用与现有机械检查 | 结构与静态接入是否成立，不证明真实执行 |
| 自有纯规则 | 直接调用生产规则，指定业务输入与已抽取的随机值；期望来自现行规则或已查实故障 | 分支、边界与优先级；不证明输入采集、动作成功或原生加载 |
| 游戏集成 | 正常游玩的相关诊断、转储、原版机制调查与必要现场 | 已发生的真实操作及其限制；不把没出现当作通过 |
| 工具自身 | 工具自检 | 检查工具有没有漏报；不称为玩法覆盖 |

不恢复模拟游戏的离线测试；也不因它被否决而丢掉同一份生产纯规则的确定性验证。随机测试不改变玩家版抽签次数、位置和消费顺序；日志记录已经取得的值，不额外抽签。通用测试桥已经退休，局部调试入口只有具体问题需要时再建；不增加新的通用测试或监控平台。（依据：用户裁定 2026-11-06）

## 原版知识

骑砍原版庞大，托管层只能反编译、引擎层只能反汇编，常识推不出来。此前每个 agent 把要用的 DLL 临时反编译进 `.codex_tmp/`，读一段、留一句规则，对原版的理解随会话消失，state 只增不减。现在原版知识分两层常驻。

- **原版源码库**：`tools/Build-VanillaSource.py` 把当前安装版本的全部官方托管 DLL 反编译到仓库外的 `../BannerlordSource/<游戏版本>/managed/`，官方模块的 XML（英文）复制到 `moduledata/`，并建 CodeGraph 索引。只由脚本生成，不手改，不进 git；游戏升级生成新版本目录，旧目录在没有手册引用后删除。官方 DLL 不再反编译进 `.codex_tmp/`；第三方模组（RBM 等）仍按 local-scratch 登记。（依据：用户裁定 2026-10-28）
- **原版机制手册** `docs/vanilla/`：一篇讲一个原版系统怎么运作，标游戏版本与源码位置，格式与上限见其 README。它回答「原版是怎样的」；我们改成什么样仍只写在 state。写或扩一篇手册的同一轮，把 state 里因为这个原版机制才存在的规则（「不要…因为原版会…」）改成一句指向手册，或在手册已说清时删掉；用户裁定不动，原版事实不在 state 里重复。规则总数的管法见清理台账。（依据：用户裁定 2026-10-28）

## 文档

- `docs/state/` 是唯一事实来源，结论变了就原地改写，不追加「后来又改成…」。同一行为只在所属 state 定义，
  其他页面链接过去。每份只写硬规则（带依据）、当前设计要点、待实测、已知缺陷；测试条数、断言计数和验证流水写进提交信息，不进 state。超过 250 行或混入多个职责时
  按职责拆分，不靠合并长行或删有效规则压到阈值以下。（依据：用户裁定 2026-10-08）
- 头部固定格式见 [README](README.md#每个-state-文件的固定开头)。
- 约束以后开发的句子句末标依据，写法和推翻权限见 [README](README.md#硬规则要标依据)；检查脚本按行判断，一条规则写在一行里。`docs/journal/<YYYY-MM>.md` 只追加，记录当时为什么这么想，不代表那样想是对的；不得从 journal 恢复已不在 state 里的规则、阈值或实现，state 里找不到的规则默认不成立。用户对模组和流程的新裁定、新给的资源，当轮写进对应 state：裁定标 `用户裁定 YYYY-MM-DD`；仓库外素材写绝对路径和身份（如「过渡素材，之后替换」）。推翻旧裁定就原地改掉旧句子，不得只留在对话或 agent 私有记忆里。（依据：流程）
- agent 的权限、审批、工具设置是会话设置，不写进仓库。
- 发现了但没修的缺陷写成一行，以 `已知缺陷` 加中文冒号开头，progress.md 自动汇总。新写的源码当轮写进某个 state 的 `覆盖源码`。
- 生成的东西（code-map、progress.md 的待实测与已知缺陷汇总）只由脚本重建，不手写。**没有无主的东西。** 分支、工作树、stash，以及 `.codex_tmp/`、`build-check/` 里的每一项，要么属于一件还在进行的事、
  在 state 或 `docs/reference/local-scratch.md` 里写明，要么不存在。state 里的回退指向提交号，不指向临时文件。（依据：用户裁定 2026-10-07）

## 提交与发版

- **提交时机是一件事到了稳定点**，不是一轮结束。稳定点：用户实测确认可用；一个功能或修复做完（构建、测试通过，
  已部署 live 等用户实测）；马上要动有风险的改动，需要一个能退回去的点。
  仍在试方案、预计继续改或撤的部署属于中间步骤，构建通过不自动等于方案稳定。（依据：用户裁定 2026-10-08）
- **粒度**：不强求一个提交只做一件事，相关或同期的改动可以一起提交，提交信息写清包含什么；但不要碎提交（09-27 一天 39 个提交，双刀动画流水线 50 分钟提交了 11 次，不少只改了 journal 几行），未提交的改动也不要跨好几天堆积：下一个接手的 agent 分不清哪些做完了。（依据：用户裁定 2026-09-27、2026-10-21）
- **推送、打 tag、建 Release、出 ZIP、改 `_Module/README.md` / `README_EN.md`**：只在用户提出新版本时做，普通开发构建永远不出 ZIP。「上一版」是哪个提交，以 [build-and-deploy](build-and-deploy.md)「已发布版本与下一版的对比基线」为准，不要只看 tag 名；发版按 `.claude/rules/release-notes.md` 与 `docs/reference/release-checklist.md` 做。仓库 `Lucicain/GW` 是公开的，但玩家只看 Release 页；云端历史不删、不改写已推送的提交，本地 `.git` 可以瘦身。（依据：用户裁定）
- 替换或移除已有功能前，先在 state 里记下回退路径（哪个提交、哪些文件）。未提交改动的回退统一是：相对 `HEAD` 按这件事的方法差异撤销，不整体覆盖多件事共用的文件。state 里不为每轮写「本轮回退基线」。（依据：流程 2026-10-21）

## 多 agent 协作

- 同一块耦合区域同一时间只有一个 agent 在写；其他 agent 只读、调研或审查。（依据：用户裁定 2026-10-21）
- 互不相干的区域可以并行，各自用 worktree，开工前按领域（子系统）划好分工，不按文件临时分。
  worktree 只隔离文件，不隔离设计决定：两个并行任务碰到同一个共享概念，就改成先后做。（依据：设计）
- 交接只经仓库（state、提交、待实测），不经对话或某个 harness 的记忆。
- 云端会话：只做不需要构建和游戏的活（文档、注释、Python 工具），各自从最新 `origin/main` 开分支并开 PR，不推 main；主会话看不到也指挥不了云端会话，只经 GitHub 收成果，在本地审查、合并、构建、测试、部署后再推 main。云端会话做完由用户删掉并提醒主会话来收。（依据：用户裁定 2026-11-04）

## 定期集中清理

每次发版前，或用户要求时，集中做一次：`Check-CodeHygiene` 全量结果、绕过归属方的写法、重复实现、死代码、
state 里引用已失效的文件和符号、超长的 state、`.codex_tmp/` 与 `build-check/` 的无主项、遗留分支和 stash。
平时每轮只处理 `Check-StateFreshness` 点名的项，不做额外的整洁仪式。
集中清理哪个 agent 都可以做，但同一时间只有一个在做，它就是这次的专职；开发中的 agent 不为此停下来。
漏网的死代码、文档错位、缺审查留到清理时处理，不为此再加机械检查。（依据：用户裁定 2026-10-21、2026-10-22）

跨 agent 的共识只经下面这份台账，不靠哪个 agent 的记忆或对话：

- **增量清理**：清理只看 `git diff <上次基线>..HEAD` 和清理待办，不每次通读全库；做完把基线改成清理提交、删掉处理完的待办。
- **顺手登记**：开发中发现却不在本轮处理的整洁问题（关掉机制留下的代码、错位的文档段落、缺的审查、可合并的重复实现），
  写一行进清理待办，写清在哪、为什么，不在对话里说说就算。
- **收尾可见**：`Check-StateFreshness` 每次报告距上次清理多少提交、几项待办，只提示，不拦提交。

### 清理台账

上次集中清理：`2cf7b69`（2026-10-28，Claude Code；规则总数 1145→959：city-search、case-envoy、warden-ai、workflow、code-health、firesworn-villages/armory、case-enforcement/assistance、player-justice 按主题合并，新写原版手册 tournaments.md）

熵指标：state 规则总数（手写 state 里「依据」出现的次数，`Check-StateFreshness` 报告），基线 959（2026-10-28 集中清理后）。新裁定会让它上升，这不拦；手册替掉规则、合并重复让它下降。每次集中清理把它压回上次基线以下并更新基线，压不下就在台账写明原因。

清理待办：

- 玩家文字分批审阅（2026-11-04）附带的代码问题，都不在审阅稿里改，详见各稿「代码问题」：野外执法 `GwpOffenderDesire` 的 `PayToAvoidBattle/PayForPeace/PayGenerously/RashMost` 不在 `Roll` 候选表，18 条文字走不到（第 1、2 批）；`GwpFieldArrestBehavior.ChargeCondition` 设了 `GWP_FIELD_ARREST_RESIST/PLEAD` 却没有对话行使用，抗法、交不出钱 32 条台词走不到（第 2 批）；`gwp_fa_open_plain` 走不到（第 1 批）；`GwpPoliceWarReasonService.FormatLocation/FormatCampaignDate` 无人调用、`Check-CodeHygiene` 未查出（第 8 批）；`PoliceCrimeMonitorEnhanced.Report` 方法体整段注释、调用仍在取文字，罪名以显示文字存进案件、切语言不随动（第 10 批）；`PolicePatrolBehavior.DebugLog` 用写死的 `DebugPatrol=false` 关闭而不在 `#if GWP_DIAGNOSTICS` 内，11 条调试文字编进玩家包（第 11 批）；`GwpCaseArchiveItemVM` 战争理由 `_075` 若按 10-18 界面裁定删掉需改一行（第 6 批，待用户定）。清单见 `docs/reference/text-review-inventory-20261104.md`。（2026-11-04 登记）
- 玩家文字附带的两处代码问题：`PolicePatrolBehavior.OnRefuseConsequence` 宣战时先显示 `gwp_policepatrolbehavior_034`，紧接着又显示同句的 `_018`，同一句提示连出两遍；`GwpFieldArrestLines.PaymentRebuff` 对每种打算只取 `_lead` 的英文，`gwp_rebuff_<打算>_charm/rogue/trade` 的英文玩家看到的都是 lead 那句。详见 `docs/reference/text-review-term-check-20261104.md` 与 `text-review-player-enforcement-20261104.md`。（2026-11-04 登记）
- 规则数继续压：2026-10-29 又清了 affairs-interface、firesworn-parties、firesworn、firesworn-rituals、firesworn-villagers、family（959→901），11-04 云端会话又清了 case-interceptors、diagnostics、infrastructure、crime-intake 与 player-text、warden-gear 等九页（→802）；第二批云端会话又清了另外二十四页并核对 progress 与索引；warden-ai 的原版评分段落等手册写成后替掉。（2026-10-28 登记）
- reference 里还有两份原版资料未迁入手册：`firesworn-vanilla-reference.md`（村庄民兵、志愿兵格子）和 `vanilla-marriage-reference.md`（2026-03 写的婚姻 API，未按 v1.4.8 复核）。迁入前按源码库逐条核实，核不实的不写。（2026-10-29 登记）
- 原版机制手册续写：战场增援出兵（MissionAgentSpawnLogic、TroopSupplier 排序）、竞技场单挑流程、地图部队小时决策（MobilePartyAi.GetBehaviors、initiative、AiVisitSettlementBehavior 评分；warden-ai 与 case-assistance 里的原版评分段落可随之替成指向手册）；场景切换、逃跑、在城镇任务里带兵、比武大会、烧村五篇已写。（2026-10-28 登记）

## 接入 harness

- AGENTS.md 是唯一入口。仓库目录里（本仓库及其上级目录）不要新建 `CLAUDE.md`、`.claude/CLAUDE.md`、`CLAUDE.local.md` 或 `GEMINI.md`。（依据：用户裁定 2026-10-07）
  前三个会让 Claude Code 不再读 AGENTS.md，`GEMINI.md` 在 Antigravity 里优先于它。用户主目录的 `~/.claude/CLAUDE.md` 不在此列，它放跨项目的个人习惯。
- harness 自带的记忆、规则、技能只放个人习惯或指回仓库的指针。Codex 的全局记忆对本项目关闭（`.codex/config.toml`）；
  Claude Code 项目记忆里的 project/feedback 条目要写 `仓库位置：<相对路径>`，检查脚本会点名没写的。（依据：用户裁定 2026-10-07）
- 新 harness 接进来时确认三件事：它读 AGENTS.md；它能跑 `tools/` 下的 Python 和 git 钩子；它自己的记忆按上一条设好。
  各 harness 的便利功能（Claude Code 的 `/wrap`、`.claude/rules/`、code-map 刷新钩子、CodeGraph MCP）只是加速，不是规则来源。
- 新克隆后设一次 `git config core.hooksPath .githooks`。
- CodeGraph：`codegraph status --json` 看索引与工作树差异，有变化时 `codegraph sync`；索引包含未提交源码，
  不是已发布版本的证明；本地数据库不入 git。MCP 查询失败用 CLI，CLI 也失败就用 code-map 与 `rg`，不把工具故障当成业务结论。（依据：流程）
