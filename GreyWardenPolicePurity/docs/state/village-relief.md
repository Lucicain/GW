# 村庄善后、收养、重建、请愿与村民打赏

> 当前状态：现行规则（2026-10-05 从源码提取）
> 最后验收：未建立检查点
> 已复核至：2026-10-05 删除无依据的 catch 与无人引用的成员（失败不再静默跳过，正常路径不变；见 `code-health.md`）
> 覆盖源码：`GreyWardenVillageAdoptionBehavior.cs` `GreyWardenAdoptionLogEntry.cs` `GreyWardenVillageReconstructionBehavior.cs` `GreyWardenIssueResolutionBehavior.cs` `GreyWardenVillageRewardBehavior.cs` `GreyWardenVillageRewardSliderScreen.cs`
> 待实测：无

灰袍在聚落里做的非执法差事。范围都是全大陆，不看村子归谁。谁来做、谁让谁，见 [`warden-ai.md`](warden-ai.md)。

## 善后与收养（`GreyWardenVillageAdoptionBehavior`）

- 村子被焚（原版 `VillageLooted`）时排一次善后。前提是家族还能收养：在世成员 < 15，且距上次收养满 1 年。
  全图同一时间只有一件善后，排着的或进行中的都算。
- 指派：从离村子最近的灰袍领主开始，逐个尝试占用（`TryReservePolicePartyForVillageRelief`）。
  - 占用会把她从普通案件上撤下，原来的案件退回案件池；玩家委托护送、`PLAYER_WANTED` 不撤。
  - 不选的人：纠察队、练兵排队中的、玩家请求或练兵订单占着的、已在善后的、正在战斗的。
- 被选中的领主先补给完，再去村子，到达后驻留 72 小时。
  中途她被俘、部队没了，或者村子失效，善后就作废，不收养。
- 驻留满 72 小时：如果仍能收养，收养一名 2–6 岁的女孩。
  - 以 `gw_leader_0` 为模板生成，出生地是这个村子，发童装；
  - 记一条收养日志（`GreyWardenAdoptionLogEntry`）；
  - 公库加 3,000（`CreditSuccessfulCaseCompletion`）。
  
  长大后的规矩见 `family.md`。
- 旧版本对收养的女孩发过原版「新生儿」通知。女孩没有生母引用，读档后这条通知会让原版界面崩溃，所以读档时会清掉它。

## 重建（`GreyWardenVillageReconstructionBehavior`）

- 只要重建本职还有在世的持有人，被焚村庄就进待重建名单，不过期。这条线断了，名单整个清空。（依据：设计）
- 谁去：
  - 重建本职优先认领；
  - 其他领主只在没有待派案件、也没有自己本职的活时来帮。被焚村庄不会过期，而案件有时效。
- 经费：
  - 每次花公库余额的 3%，取整到百，夹在 15,000–30,000 之间；
  - 扣完必须还剩 50,000 或 7 天工资（取大者），不够就不开工。
- 到村后施工 24 小时，然后：
  - 扣经费，把村子血量补满，原版据此把村子恢复成正常状态；
  - 公库加 3,000；
  - 安置一位永恒之火祭司，这座村子算作已传教（见 `firesworn.md`）。
  
  原版状态没有切回正常时，经费退还。
- 有待重建的村子时，重建本职不接普通案件；已经接了重建的人都保留着，不被普通案件抢走。

## 请愿（`GreyWardenIssueResolutionBehavior`）

- 全大陆所有城镇、村庄里原版要人尚未有人接的请愿（issue），只要 AI 能完成，都进请愿池。数量不限。
- 谁去：
  - 请愿本职优先认领；
  - 其他领主只在没有待派案件、没有待重建、也没有自己本职的活时来帮。请愿刻意排在案件之后。
- 走到发起人所在聚落、驻留 6 小时，然后用原版 AI 领主完成请愿的那条路办完，保证成功。
  办完后：
  - 村子户数 +5，城镇繁荣度 +5；
  - 公库加 3,000；
  - 计入玩家请求的顺延。

## 村民打赏（`GreyWardenVillageRewardBehavior`）

- 玩家灰袍声望为正时，每天按「声望 × 10」第纳尔累积一笔打赏，在任何村子的菜单里都能领。
- 领取时可以用滑条（`GreyWardenVillageRewardSliderScreen`）选领多少。
- 声望为 0 不再累积；声望转负时清空未领的部分。
