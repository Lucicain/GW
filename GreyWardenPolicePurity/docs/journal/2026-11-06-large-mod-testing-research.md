# 骑砍大型模组：复杂度、随机性与故障调查

## 调查范围与问题

用户指出 AI 玩游戏能力不足，长局状态和概率触发使“装监控后等玩家复现”成本很高，要求继续调查骑砍大型、复杂模组的实际开发经验。本轮聚焦骑砍 2 的 RBM、The Old Realms、Banner Kings、Diplomacy，并以骑砍 1/战团的 TLD 补充。没有启动游戏、下载执行外部代码或修改业务实现。

上一轮调研见 [分层与测试方向调研](2026-11-06-layering-review.md)。这轮修正它的优先级建议：先处理可控输入与随机分支，测试桥的可控场景只解决需要真实引擎的部分；不能把扩大游戏内自动化当作所有难复现问题的答案。以下为外部证据与 agent 建议，不是新业务裁定。

公开源码可以证明某种工具或测试存在，不能证明作者内部一直使用、覆盖全部功能、实际缺陷率低或已彻底解决随机复现。未看到的私有 Discord 流程不作推测。

## 取样版本

2026-11-06 经 GitHub API 读取默认分支的完整树（均未截断），再读取相关源码、说明与工作流；记录版本便于追溯，不以测试目录名推算覆盖率。

| 项目 | 分支 | 取样提交 |
|---|---|---|
| Fellow93/RealisticBattleProject | master | `4e491d967832fd08c9ab2ef1d70b63d44fa59864` |
| TheOldRealms/TOR_Core | development | `5ccd0b9135a72764f7a80dbc37119b4ca8ac7050` |
| R-Vaccari/bannerlord-banner-kings | main | `0c0184662ca8be7c1ad07d9c7f73c9d8e5da22f9` |
| DiplomacyTeam/Bannerlord.Diplomacy | dev | `29217d984ce2a39e167f9fdfe44289dfa404658c` |
| tldmod/tldmod | master | `da9cfdf4f894dcf15643efdea1eb73674379c2f1` |

## RBM：把随机结果作为规则的输入

[战役测试说明](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/RBMCampaign.Tests/README.md) 明确说明：配置值和随机值由游戏侧读取后传入规则，数字公式在不运行游戏的情况下测试；访问战役、聚落或部队仍需真实游戏。

[BattleLootSpecs](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/RBMCampaign.Tests/BattleLootSpecs.cs) 有具体例子：同一件有四分之一机会被注意到的战利品，传入 0.2 时取得、0.3 时落空。也检查携带上限、分配及取整守恒。这是在测试生产规则，成功和失败均不靠等待抽中。

[AI 测试说明](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/RBMAI.Tests/README.md) 对边界同样明确：阵容判断、运动估算、目标切换等纯值规则可以测试，读取 Mission/Formation/query 的部分仍须运行游戏。

[战斗日志检查器](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/tools/BattleLogCheck/README.md) 自动查停滞、后退、命令反复改变等表现，报告时间、队伍、数值和证据行。[BattleReplay](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/tools/BattleReplay/README.md) 能回看位置与事件，但其沙盒和检查明确标为近似，不能当真实引擎回归。

[测试战斗入口](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/RBMCampaign/Diagnostics/TestBattleCommand.cs) 可指定双方兵种范围与数量直接建立战斗；存在状态恢复范围和限制，不能据此推导“适合在任意玩家存档反复自动测试”。[战役架构说明](https://github.com/Fellow93/RealisticBattleProject/blob/4e491d967832fd08c9ab2ef1d70b63d44fa59864/RBMCampaign/ARCHITECTURE.md) 记录战利品账本的持有者、收入/支出入口及模块依赖，还将自动结算拆成独立项目以解除对 Campaign 开关的依赖。

可借鉴：随机输入可指定、生产规则复用、明确账本归属、已有记录自动提炼证据。不能照搬其近似沙盒来替代本项目已经否决的假游戏测试。

## The Old Realms：直接进入需要检查的局面

[控制台源码](https://github.com/TheOldRealms/TOR_Core/blob/5ccd0b9135a72764f7a80dbc37119b4ca8ac7050/CSharpSourceCode/Utilities/TORConsoleCommands.cs) 提供直接打开测试任务、打开/重载 Ink 故事、赋予施法能力与法术、生成敌对部队、查看 AI 同伴状态等入口。这证明其具备绕过正常成长和找目标过程的开发工具，并不证明这些入口已组成无人值守端到端测试。

[决策器](https://github.com/TheOldRealms/TOR_Core/blob/5ccd0b9135a72764f7a80dbc37119b4ca8ac7050/CSharpSourceCode/BattleMechanics/AI/CommonAIFunctions/DecisionManager.cs) 汇总各施法行为的效用候选再统一选最高分。可借鉴候选与选择职责的区分；这个局部实现不能代表整库有完全独立的纯逻辑层。

两个故障记录值得区分：

- [当前仓库 issue #19](https://github.com/TheOldRealms/TOR_Core/issues/19) 的报告者通过静态核对 XML 与脚本发现疑似效果 ID 不匹配、参数数量不符、死亡条件相互排斥等问题；另有概率效果可能施加到攻击者的报告。该 issue 仍开放，是问题报告而非维护者确认的完整修复证据。它说明有些“效果好像从不触发”可以先查配置连通性，不必靠游玩统计猜测。
- [历史仓库 issue #162](https://github.com/TheOldRealms/TOW_Core/issues/162) 明确保存了模组版本、异常报告、视频和出事前存档，记录自动结算与手动战斗结束均能触发。它是可复现事故包的实例，不能推广为所有概率故障都有现成存档复现。

此外，名为 TORTests 的类在这次取样中只是主动触发损坏内存异常的入口；不能因为名字含 Tests 就声称 TOR 有完整单元测试体系。

## Banner Kings：缩小更新批次，并提供跳过进度的工具

[作者的 Changelog](https://github.com/R-Vaccari/bannerlord-banner-kings/wiki/Changelog) 在 2025 年规划中说明，之前同时推进太多未完成工作，今后倾向一次做一个功能、以更小更新交给贡献者测试；这是作者公开的开发经验，不能改写成已经完成全面自动化。

[开发命令](https://github.com/R-Vaccari/bannerlord-banner-kings/blob/0c0184662ca8be7c1ad07d9c7f73c9d8e5da22f9/BannerKings/BannerKingsCheats.cs) 可直接触发叛乱、完成宣称、推进文化时代、授予头衔。[缺陷模板](https://github.com/R-Vaccari/bannerlord-banner-kings/blob/0c0184662ca8be7c1ad07d9c7f73c9d8e5da22f9/.github/ISSUE_TEMPLATE/bug_report.md) 要求版本、尽量减少其他模组的证据及崩溃栈。

可借鉴：控制同时变化的机制数量、缩短到达测试条件的过程、收集明确版本的现场。它仍然依赖测试者；不应把大型项目的名气当成其已解决难复现问题的证明。

## Diplomacy：检查状态变化和一次性操作

[招募动作](https://github.com/DiplomacyTeam/Bannerlord.Diplomacy/blob/29217d984ce2a39e167f9fdfe44289dfa404658c/src/Bannerlord.Diplomacy/CivilWar/Actions/RecruitFactionSupportAction.cs) 将资格检查、开始交涉、完成交涉与尝试记录分开；完成前再次确认资格及领袖身份，已经消费的尝试不能再次完成。

[对应测试](https://github.com/DiplomacyTeam/Bannerlord.Diplomacy/blob/29217d984ce2a39e167f9fdfe44289dfa404658c/tests/Recruitment.Tests/RecruitmentTests.cs) 覆盖费用和冷却、重复完成、开始后目标/阵营条件变化、承诺到期等情形。[CI](https://github.com/DiplomacyTeam/Bannerlord.Diplomacy/blob/29217d984ce2a39e167f9fdfe44289dfa404658c/.github/workflows/TestBuild.yml) 实际调用招募与装备兼容测试，再构建支持的游戏版本。

限制：这些测试使用替身对象；其中“加载”案例模拟管理器重建与同步，不是 Bannerlord 原生存档文件的真实往返。本项目可以借鉴要测哪些状态变化，不照搬替身世界，也不把此类通过当作原生存读档验证。

## TLD：测试入口早于自动化测试框架

[菜单源码](https://github.com/tldmod/tldmod/blob/da9cfdf4f894dcf15643efdea1eb73674379c2f1/ModuleSystem/module_game_menus.py) 包含开发快速开局、指定测试双方兵种数量、巨魔/座狼专项战斗、场景选择及状态检查菜单。其价值是把漫长正常流程缩短成直接入口，许多检查仍需人在游戏内观察。

[Nightly 工作流](https://github.com/tldmod/tldmod/blob/da9cfdf4f894dcf15643efdea1eb73674379c2f1/.github/workflows/tldmod-buildbot.yml) 调用构建打包脚本并上传产物。这证明自动构建分发，不能据此声称全战役行为自动回归。

## 对本项目的推论与建议

以下是 agent 结合证据提出的方向，不声称所有受访项目都这样做，也未直接实施。

### 先分清三种随机问题

| 问题 | 应检查什么 | 不需要依靠什么 |
|---|---|---|
| 这个低概率分支是否正确 | 指定抽签值，覆盖区间边界和每个合法结果，执行同一生产规则 | 等待自然抽中 |
| 分支在真实世界里是否有资格进入 | 查入口条件、配置引用、事件挂接及必要的真实引擎契约 | 把“未出现”解释为概率低 |
| 长局/事件顺序怎样造成坏状态 | 保存相关状态变化与原因，针对重复、失效、取消、加载等顺序核对所有者和动作结果 | AI 重走整局或玩家重复猜路径 |

指定随机值测分支，不等于改变玩家版概率；概率分布是否符合设计是另一项检查。固定一个全局种子也不能直接保证整个 Bannerlord 可回放：外部系统消费随机数的顺序、事件先后及未记录的原生状态都可能不同。建议控制和记录本模组可拥有的输入，不先许诺全游戏确定性回放。

### 分层先解决可控制性

优先挑随机规则明确、已有归属、结果可用数据表达的机制，将状态采集、概率/权重计算、抽签选择、动作执行的职责辨清；只有实际需要替换输入时才加相应接口。核心输出需要带理由及必要数据，避免到每个调用点重新推导同一结论。涉及兵员/钱款/案件的写入仍走现有唯一归属，不因分层另建一套账本。

迁移应保留原有抽签次数与位置等可观察行为；移出随机读取若改变短路求值或消费次序，也可能改变玩法，不能仅以同一概率公式认定完全等价。

### 用本项目谈判抽签作为可对照点

本轮只读 `GwpOffenderDesires.Roll`：它采集英雄特质/财富，调用权重规则，直接调用 MBRandom，再从候选中选结果。已有 TOOLBOX_CHOICE 日志记录权重与结果，未记录被逐项扣减前的原始随机值；它不是完备的随机决策回放记录。该源码现状与分层调查入口归 state/code-health.md。

建议在完成当前试点之后，把这种边界清楚的抽签作为候选：针对实际候选及其权重遍历边界，复用生产选择逻辑。原台账已有不在候选表中的枚举分支问题，不在本轮擅自改变其可达性。

### 监控以留下可回答的问题为目标

建议优先保存某次决定前的业务输入、有效候选与权重、抽签值、决定原因、动作结果和对象稳定 ID。跨事件故障再加关联 ID 与有限的前序变化；记录版本与配置，错误出现时保留相关证据。无需让全部子系统每 tick 打大日志，也不能在记录随机值时再抽一次。

对于资金、兵员、任务占用，可以从一次操作的前后账目和状态约束直接发现异常；对于动画、战斗物理和原生崩溃，仍需要真实引擎现场。回看工具用于提取已经发生的证据，和重新执行真实规则的回放验证明确区分。

## 建议优先级

1. 明确纯生产规则测试与假引擎模拟的区别及适用范围，处理现有文字和试点实现之间已登记的口径差异。
2. 完成当前试点的职责边界和真实输入回放，避免长期保留新旧两份业务；对随机机制补可指定抽签值的边界设计。
3. 针对容易产生补丁堆积的生命周期操作，列出开始后失效、重复完成、取消、读档等状态变化，确认同一归属处能处理。
4. 为确实需要真实引擎的部分增加专用调试入口；保留现有测试桥边界，不依赖 AI 操作键鼠或玩家反复撞概率。
5. 将失败记录变成可以再次检查的数据，再考虑扩大无人值守长局压力测试；不把无人值守运行等同于覆盖所有玩法。
