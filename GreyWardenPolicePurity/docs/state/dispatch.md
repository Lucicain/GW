# 玩家送信队（使者）

> 当前状态：用户已实测确认（2026-10-02：按路程选城、卖掉才回去送信、卖俘虏提示、押送不逃）
> 最后验收：`6b9f090`（2026-10-02 用户确认）
> 已复核至：2026-10-05 删除无依据的 catch 与无人引用的成员（失败不再静默跳过，正常路径不变；见 `code-health.md`）
> 覆盖源码：`GwpWardenDispatch.cs` `GwpWardenDispatchBehavior.cs` `GwpDispatchCargo.cs` `GwpDispatchPrisonerCustody.cs` `GwpWardenDispatchDialogue.cs` `GwpPartyScreenTroopTalkPatch.cs` `GwpAssetPayment.cs` `GwpAssetAutoOfferPatch.cs` `GwpAssetOfferValidationPatch.cs` `GwpDispatchBarterScreen.cs` `GwpDispatchBarterFaultDiagnostics.cs`
> 待实测：无

送信队从开口到回来的全过程：对话入口、选人、定单子、地图上的一趟、交割、工资。

## 四种差事

`GwpDispatchPurpose`：`Report`（带钱物、必要时押俘虏去复命）、`Support`（求援，送到才成立）、
`TroopOrder`（带练兵订单和订金，送到才立单；练好的兵仍由练兵官亲自送）、`PeaceRequest`
（请灰袍出面调停，空手去空手回）。阶段 `Outbound → Returning → Rejoined`，存档键 `gwp_dispatch_state`。

## 出发

- 队伍是真从玩家队里分出去的：`CustomPartyComponent.CreateCustomPartyWithTroopRoster`，属玩家家族，
  owner 为玩家，无英雄领队，前缀 `gwp_dispatch_`。
- 出发前要有走得到的灰袍领主（`FindReceiver`），否则不派。玩家在海上又分不出船时不派。
  船向玩家借，见 [temporary-parties.md](temporary-parties.md)。
- 随队的案件款记为 `CaseGoldFloor`，路上不许动；货物清单 `CargoState` 记下要交的物品与作价。
- **不吃饭，不带口粮、不带盘缠**，出发不查粮（用户裁定 2026-09-25，见 temporary-parties.md）。
  背包里没粮，原版永远不给它巡逻候选；办差期间它始终有我们的意图，巡逻候选本来也被压到最低。

## 路上

- 出程直扑（`RequestRush`）；回程跟随玩家（`RequestEscort`），不用直扑，否则原版会拉出一场遭遇。
- 攻击倾向 0.2、6 小时，每小时续：贴身弱敌照打，不为远处目标丢下差事。
- 收件人每小时重选最近且**走得到**的灰袍领主；新目标要近 25 以上才改道。
  `CanReach` 必需：目标位置不合本队通行方式时，引擎把目的地换成自己脚下，队伍原地不动。
- 12 小时没有真的靠近收件人就换一个；换不出来就带着东西回玩家身边。
- 进城只为一件事：身上有**普通兵俘虏**时去最近的非交战、未被围城镇，让原版 `PartiesSellPrisonerCampaignBehavior`
  在进城那一刻卖掉。一旦去卖就一直去，卖掉才转回送信，不按时限放弃。（依据：用户裁定 2026-10-02）
- 英雄俘虏不为之进城：原版只把英雄卖给与其交战的城，可能哪座都不收；他们随队回来交给玩家。（依据：C#反编译 v1.4.8）
- 进过城还剩普通兵（这座城不收）时，离城并隔 24 小时再找。押着本案目标时一律不进城。
- 卖俘虏的钱由原版直接给玩家，城不属玩家时原版不提示；我们在 `OnPrisonerSoldEvent` 里按原版赎金模型算出金额，
  提示"在某城卖掉几名俘虏、多少第纳尔已入钱袋"。
- 送信队押着的俘虏不逃跑：跳过原版 `PrisonerReleaseCampaignBehavior` 的 `DailyHeroTick`（英雄每日逃跑，
  野外 2 人小队约 15%/天）与 `HourlyPartyTick`（超容量每小时 10%）。议和放人、队伍覆灭、赎回照原版。（依据：用户裁定 2026-10-02）
- "最近"按原版 `SettlementHelper.FindNearestTownToMobileParty` 以本队通行方式量路程（城门或港口）。
  中心点地形因城而异，曾把附近的城整批筛掉，送信队掉头去 118 外的城，24 小时没到又折回，循环往复。
- 不要用城镇中心点 `Settlement.Position` 判断城镇能否到达。（依据：C#反编译 v1.4.8 + 实机诊断 2026-10-02）
- 险情通知：被卷入战斗、伤员过半，各报一次。

## 交付与交割

- 距离 3 以内交付。`Report`：交出案件款与清单货物、押的人，拿办案费（同样受保护）回来；
  委托已不在时原样带回。`TroopOrder`：带的钱不够订价或练兵官不能接单时原样带回。
- 回程最后 15 的路上设为不可交互，距玩家 3 以内交割：人（含伤员与经验）、俘虏、全部物品、
  剩下的钱全部还给玩家，然后销毁队伍。万一还是撞成遭遇，下一帧补做交割并关掉遭遇。
- 玩家被俘（主队失效）时原地等；玩家进城时跟进城交割。
- 队伍被消灭：记录删除，提示"带的东西一起没了"。

## 工资

每天由玩家付 `party.TotalWage`；付不起时队伍士气 −1。案件款不参与。

## 开口：跟队里的灰袍兵说话

- 原版队伍界面只有英雄有「交谈」按钮。这里只给「自己队里的灰袍兵」也开一个，接到派遣对话上（`GwpPartyScreenTroopTalkPatch`）。
  原版 `ExecuteTalk` 只处理英雄，普通士兵走进去会空引用，所以命中灰袍兵时整条拦下。
- 时序（`GwpWardenDispatchDialogue`，由派遣行为的心跳每 0.25 秒推进）：
  1. 点按钮只登记请求；
  2. 队伍界面关完之后，下一次心跳再开地图对话；
  3. 对话结束之后，再开分兵界面。
  
  只在地图界面就绪、没有别的对话时才动。没有待办就完全不做事，读档期间这条路是惰性的。
- 判断「这是不是我方开的对话」，只看我方是否武装着：开对话那一刻置位，对话结束或超时就撤下。
  不能比对 `OneToOneConversationCharacter`：地图对话是延迟启动的，那个属性可能还没填，
  判错会让玩家进到一场一句话都没有、出不来的对话。（依据：实机诊断）
- 选人用原版分兵界面，只许选灰袍兵。被选的人在最终确认前仍留在主队，那份名单只是选择，取消就原样还回去。
- 原版分兵界面写死了俘虏不可转移。所以界面关掉后再单问一句：交钱还是押人。
  押人只能押本案目标本人，直接转到这支队伍名下。
- 交多少、怎么说，出发前由玩家定死，士兵不替他临场决定。
- 练兵订单的订金按案件款带走，受 `CaseGoldFloor` 保护，路上不许拿去发饷。

## 付款与交割：原版交易界面（`GwpAssetPayment`）

- 不做自制交易界面，借原版 `BarterScreen`，可以在对话之外打开（`GwpDispatchBarterScreen`）。
  宿主屏幕被弹出时引擎已终结它的层，但 `HasLayer` 仍为真，不能再 `RemoveLayer`。
- 钱和物共用一张收据、一个上限：
  - 物品按 `ItemValue` 计价，表里列出单价；英雄身上穿着的装备不算。
  - 家当（`Wealth`）= 金币 + 行李里全部物品的价值；野外执法判断「他付不付得起」也用这个数。
- 一键配平（`GwpAssetAutoOfferPatch`）按上限从金币、物品依次填。复命模式按收据原样带：收了什么交什么。
- 交易按钮由 `GwpAssetOfferValidationPatch` 把关：报价超过约定、或者含有已经不在手上的物品时，拒绝并提示。
  只选人不付钱、或者复命时，直接按选择办完，不触发原版交易事件（不涨关系、不加交易经验、不进冷却）。
  取消就关界面，什么都不动。
- 失败路径诊断：`DISPATCH_BARTER_PERSONA_FAILED`（`GwpDispatchBarterFaultDiagnostics`，finalizer 挂在 `PersonaSoftspokenTag.IsApplicableTo`）。
  健康时不出声，不退休。

