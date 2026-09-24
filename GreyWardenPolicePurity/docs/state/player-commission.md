# 玩家承办委托：结果判定与交差

> 当前状态：已部署待实测（打垮即完成、押人另付赎金、打垮后不再误判为“被别人了结”）
> 最后验收：未建立检查点
> 覆盖源码：`PlayerBountyBehavior.CaseSettlement.cs`
> 待实测：打垮目标后复命直接领办案费、不被追问“比他交给你的少”；押人复命额外得到赎金价；不再出现“已经不在你能追到的地方”

本文件只写委托**结果怎么判**、何时可以交差、交差拿多少。接单、支援、野外罚金谈判与
账本查账尚未提取（`PlayerBountyBehavior*.cs` 其余文件仍在 [README](README.md) 未提取清单）。

## 用户规则

沿用 [case-enforcement.md](case-enforcement.md) 的裁定：**部队被打光、人被抓，都算案件了结**。
用户 2026-09-24 补充：打垮对方部队就算完成委托、照付办案费；能把人押来更好，
押人时灰袍另按原版赎金价向玩家收购。

## 判定顺序

`ReconcileCaseOnTick` 每秒一次（对话中、玩家在战斗或遭遇中时跳过）：

1. `ReconcileAssignedCase`
   - 目标在玩家队里当俘虏 → 按拘捕结案（`NotifyCaseClosedByCapture`）。
   - 仍在追捕，且目标死亡 / 被俘 / 案卷不在 / 犯人部队已不存在，**并且玩家没有在战场上
     打垮过他**（`_caseTargetDefeated` 为假）→ `WithdrawCommissionClosedElsewhere`：
     视为被别人了结，提示“已经不在你能追到的地方了”，只领辛苦费。
   - 目标重新拉起队伍时刷新追踪的队伍 ID。
2. 本场战斗的结算（`_caseBattleToReconcile`，由战斗结束登记）
   - 目标在玩家队里当俘虏 → 震慑计拘捕。
   - 玩家打垮了他但人跑了（`_caseTargetDefeated`）→ 震慑照上但不计拘捕；**停止追踪、
     转入交差状态、清除期限、撤回护送**；任务标为可交差，提示“你在野外击溃了…回去复命吧”。
   - 没打垮、人还活着 → 委托继续。

`_caseTargetDefeated` 在战斗结束时由 `NotifyCaseBattleOutcome`（玩家胜且目标在败方）置真。
原版战后会解散败军、领主可能逃脱，所以“犯人部队不存在”在打垮后必然成立——第 1 步的
`!_caseTargetDefeated` 条件就是为此。2026-09-24 之前缺这个条件，打垮后下一秒就被第 1 步
误判为被别人了结，第 2 步来不及运行（日志：`CASE_BATTLE_OUTCOME defeated=True` 后 12 秒
`CASE_WITHDRAWN_CLOSED_ELSEWHERE prisoner=False`）。

## 期限

期限到时：已打垮或已和平了结（`HasCompletedEnforcement`）→ 转入交差，不作废；
否则委托作废并撤回护送。

## 交差入口与报酬

与任意灰袍领主对话“我来汇报委托”，条件只有“有委托在身”（`HasBountyTask`）。派人复命同口径。

- **办案费** `CalculateCaseFee(cap)` = `GwpCaseSettlementRules.Reward`：底薪 + 玩家阵亡 × 抚恤
  + 罪责点 × 单价，封顶为本案应缴额 `CaseAmountDue`（指定罚金、押人估价、野外账本估价取最大）。
- **打垮未俘**：交钱复命即可领整案办案费。打垮过的案子，只有交出的钱少于**实际从他手里收到的**
  钱（`CaseReportSuggestedPayment`）才会被追问去向；没收过钱就不追问。未打垮的案子仍以应缴额为准。
- **押人**：`PrisonerCaseFee` = 押人估价封顶的办案费 + 原版
  `RansomValueCalculationModel.PrisonerRansomValue(俘虏, 玩家)`。亲自交与派人交相同。
- **被别人了结**：只付辛苦费（底薪 + 阵亡抚恤），不含罪责段。
- 所有报酬从灰袍司法公库（族长金币）支付，公库不足时只付得出多少付多少。

接单说明 `gwp_bounty_contract_turnin` 写明：击溃部队、收罚金或俘虏均可复命，活捉另按赎金价收购。
