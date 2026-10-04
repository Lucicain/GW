# 灰袍地图 AI：派活、欲望竞价与巡区

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：2026-10-05 删除无依据的 catch 与无人引用的成员（失败不再静默跳过，正常路径不变；见 `code-health.md`）
> 覆盖源码：`GreyWardenPartyDesireBehavior.cs` `GreyWardenDutyScheduler.cs` `GwpPartyDutyMovementPatch.cs` `GwpEscortSpeedCapPatch.cs` `GwpPlayerRequestDeferral.cs` `GwpAssistanceArmyLifecyclePatch.cs` `GwpAssistanceArmyTooltipPatch.cs`
> 待实测：无

灰袍领主每小时在做什么，由谁决定。案件本身的派单规则见 [`case-enforcement.md`](case-enforcement.md)；
各本职见 [`family.md`](family.md)。

## 谁拿到活（派活顺序）

各差事系统各自挑人，互相用「是否已被占用」的谓词避让。实际的优先次序：

1. **玩家相关**（最高）：
   - 玩家委托与求援（欲望分 10，`PlayerRequestScore`）；
   - 承办 `PLAYER_WANTED`（分 1）；
   - 玩家贵族事务（见 `player-requests.md`）。
2. **强制差事**：村庄被焚后的善后会把领主从普通案件上撤下来（见 `village-relief.md`）。
3. **本职优先的活**：重建本职在有待重建的村子时、请愿本职在有待办请愿时，不接普通案件。
   练兵本职在练兵排队时不接新差事。
4. **普通案件**（`PoliceEnforcementBehavior.AssignTasks`）分两轮：
   - 第一轮：商队本职只接最近的商队案，护村本职只接最近的村庄案；
   - 第二轮：所有还能接普通案件的领主，不分本职，接最近的任何案件。
   
   案件池覆盖全大陆，打仗时几乎不空，所以六个人大部分时间都在办案。
5. **帮忙的活**：别的本职闲下来，也去帮重建、请愿。条件是没有待派案件，也没有自己本职的活（`GreyWardenDutyScheduler.HasPreferredWork`）。
6. **火誓者传教**：条件比上一条还严。第一版的条件见 `firesworn.md`。
7. **巡区**：什么都没有时去巡区。

## 欲望竞价（`GreyWardenPartyDesireBehavior`）

- 灰袍不冻结原版 AI：每个差事只是往原版小时竞价里加一个候选，补给、疗伤这些原版欲望照样能压过它。
- 竞价在 `CampaignEventDispatcher.AiHourlyTick` 的后置补丁里做（`GwpFinalDesireAuctionPatch`），保证在所有原版评分之后。
  MbEvent 监听是头插链表，最后注册的反而最先跑，所以不能靠注册顺序。（依据：C#反编译）
- 有差事时：
  - 原版巡逻候选一律压到执行阈值 0.03；
  - 无领主办差队（派遣队、练兵队、追截队等），原版每小时刷出的「进聚落归并/解散」候选（每个 1.6 分）也一并压掉。
    原版把真正的补给评分锁在「有领主」之后，无领主队能拿到的只有归并或解散，这排候选会压过差事分。（依据：C#反编译、实机诊断）
- 分值：
  - 普通差事和巡区 0.99。原版普通进城候选实测 0.35–0.85；缺粮、重伤时 1–19.6，照样压得过差事分。（依据：实机诊断 1.4.7）
  - 玩家委托 10。
  - 承办 `PLAYER_WANTED` 1。
- 意图种类：Approach、Pursue、Escort、Visit、Rush、Beat。由各差事系统每小时续期（`RequestVisit` 等）。
  意图只存在内存里：读档后由各系统重新下达，旧档残留的巡逻目标只会触发一次重新竞价。
- 意图怎么落到原版动作上：
  - **追人，未宣战**：用原版跟随（Escort），由短期 AI 每约 0.16 小时刷新目标位置。
    Approach 只记竞价那一刻的位置，原版领主每 6 小时才重新竞价，会一直追向六小时前的位置。（依据：C#反编译）
  - **追人，已宣战**：换成原版 `GoAroundParty`，逃跑或接战由原版决定。
  - **去某个地点**：原版竞价没有 GoToPoint 的落地分支。借巡逻候选去竞价，赢了再翻译成真正的 GoToPoint 移动（`GwpLocationDutyRefreshPatch`）。
    地图上的行为文字改写成实际差事；追罪犯时也不再显示「正在跟随」（`GwpLocationDutyBehaviorTextPatch`）。
  - **全速赶到某支部队跟前**（玩家通缉、Rush）：下注 `GoAroundParty`，再翻译成原版 `EngageParty`（`GwpPlayerEnforcementEngageActionPatch`）。
    原版竞价没有 EngageParty 的落地分支，直接下注会赢了也不动。
  - **无领主的一次性执法队锁定目标后**：跳过原版整个小时思考，只下一次 `EngageParty`；
    战斗把追击打断时，只补发一次（`GwpPartyThinkResolvedDiagnosticsPatch`）。
- 灰袍短期 AI 只主动打承办的目标和野怪；办差的队伍谁都不主动打，只自卫（`IsAuthorizedAttackTarget`）。
- 跟随不降速：原版护送分支只会把跟随方降到被跟随方的速度。灰袍登记过的跟随差事、并且当前确实是 EscortParty 时，解除这个封顶（`GwpEscortSpeedCapPatch`）。
  路径和交战语义不变；军团行军时不动。

## 巡区

- 没有任何差事时，每位灰袍领主各守一座不同文化的城，作为巡逻圆心。
  分配方法：领主按 StringId 排序；每种文化取 StringId 最小的那座非城堡城；按序号取模分配。
  在编名单一变，分配跟着重排（`ResolvePatrolBeat`）。
- 起因：原版防御巡逻分与离家距离成反比，而六位灰袍的家乡是同一个，闲下来都挤在出生点附近。（依据：C#反编译、实机诊断 2026-09-17）
  这里不改「家」，因为灰袍成员会新增、会老死。

## 协力军团（引擎侧补丁）

协力怎么编、怎么缩见 `case-enforcement.md`。这里是让原版不把它弄坏的补丁（`GwpAssistanceArmyLifecyclePatch`）：

- 承办任务还在时，军团的生死由执法系统管：原版的无战争、缺粮、闲置、凝聚力等自动解散一律挡掉。
- 同族军团不掉凝聚力；无领主支援队加入现成的执法军团，影响力费用为 0。
- 军团没有王国文化：遭遇菜单背景改用原版的后备背景；「与其他成员交谈」跳过无领主的队，全是无领主队时整个子菜单隐藏。
- 原版 `AiEngagePartyBehavior` 只给 Defender 类型的军团长打分。协力军团是 Patrolling，只在这一个评分器运行期间临时显示为 Defender，跑完立即还原。
- 悬停提示：协力军团是直接 `new Army(...)` 造出来的，`AiBehaviorObject` 为 null，原版提示框会因此抛异常。
  只在这种情况下返回空文本（`GwpAssistanceArmyTooltipPatch`）。（依据：实机诊断 2026-09-16/17）

## 玩家请求的顺延计数

- 只有成功完成的普通差事，才计入玩家请求的顺延（`GwpPlayerRequestDeferral`）。取消、被挤掉、失效的不算。
