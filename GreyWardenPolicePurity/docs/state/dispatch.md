# 玩家送信队（使者）

> 当前状态：复命/求援归入当前委托，订单/讲和/封地各归业务；五种真实使者与封地递送保持
> 最后验收：2026-10-21 用户反馈前轮无功能问题；日志确认订兵/讲和/复命选兵，封地送达与存读边界仍待测
> 覆盖源码：`GwpWardenDispatch.cs` `GwpWardenDispatchBehavior.cs` `GwpDispatchCargo.cs` `GwpDispatchPrisonerCustody.cs` `GwpWardenDispatchDialogue.cs` `GwpPartyScreenTroopTalkPatch.cs` `GwpAssetPayment.cs` `GwpAssetAutoOfferPatch.cs` `GwpAssetOfferValidationPatch.cs` `GwpDispatchBarterScreen.cs` `GwpDispatchBarterFaultDiagnostics.cs` `GwpWardenDispatchBehavior.Affairs.cs` `tools/tests/test_affairs_dispatch.py` `tools/tests/test_fief_courier.py`
> 待实测：委托内复命/求援及行程归类；封地选定/出发/送达/失效返回及存读接续；归队伤兵和累计经验；反复建队不重号

送信队从开口到回来的全过程：对话入口、选人、定单子、地图上的一趟、交割、工资。

五种差事各在[我的事务](affairs-interface.md#玩家与办案使者)对应业务子页发起并显示行程，不再有整合使者页；本队灰袍兵对话同时支持这五类。正常选兵/付款/交割及真实资源归本行为。（依据：用户裁定与实现2026-10-21）

## 五种差事

`GwpDispatchPurpose`：`Report=0`带钱物/本案俘虏复命、`Support=1`送达求援、`TroopOrder=2`带订单和全额预付送达立单、`PeaceRequest=3`请求调停、`FiefAppeal=4`递交选定封地重议申请。前四项数值保持。阶段Outbound→Returning→Rejoined，存档键仍gwp_dispatch_state。船只/人员归队见[临时队](temporary-parties.md)。（依据：原业务与本轮新增目的2026-10-21）

### 封地申请使者

先选护卫，归还这份临时选兵，再由[封地归属方](player-requests.md)列合资格分封；最终选定后复核精确SettlementId+CaptureHours、当前申请、同类在途和真实护卫，再实扣人员建队。取消任一步没有丢兵/伤员/经验；没有预扣5万。封地负载只代表待递交申请，不占为已登记阶段。（依据：用户裁定、设计及生产验证2026-10-21）

随队保存FiefSettlementId与FiefCaptureHours，原15段字符串末尾追加两段；旧记录无两段按无封地负载读取，前15段及已有差事/钱物/俘虏/订单不清空，原目的ID不变。精确分封身份防止重攻同城后把旧信递成新案。未新增保存类型ID或顶层键，不据此冒称完整r13升级已完成。（依据：存档裁定、设计及编解码回归2026-10-21）

通过原海陆交接门槛后调用FileCourierAppeal，重新核对会员/正式封臣、30天、原分封记录及封地现状、无已有申请；成功进入原SeekingPlayerForPayment，贵族事务本人再找玩家收费/游说/投票。超时、失去封地资格、重攻、退会/佣兵或已手动登记均不另立申请，使者沿原返程带回人员/物资。申请失效提示与申请已登记提示分开；返程与归队沿原规则。（依据：设计与实际交付方法执行2026-10-21）

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
- 攻击倾向 0.2、6 小时，每小时续：贴身弱敌照打，不为远处目标丢下差事。火誓者自建的传教队与村间转运队也用 `KeepCourierDisposition`；售货村民走原版村民AI，见[村民队](firesworn-villagers.md)。（依据：源码复核 2026-10-19）
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

- 出程交付及回程归队均须通过[公共海陆交接门槛](temporary-parties.md#海陆交接资格)。距离已到但尚不能交接时，保留差事与货物，继续直扑收件人或跟随玩家；不提前转人、转钱、还船或销毁。实际生产DLL验证见[追截](case-interceptors.md#上下船与归队交接)。（依据：实际DLL复现与离线回归 2026-10-19）
- 距离 3 以内交付。`Report`：交出案件款与清单货物、押的人，拿办案费（同样受保护）回来；
  委托已不在时原样带回。`TroopOrder`：带的钱不够订价或练兵官不能接单时原样带回。
- 回程最后 15 的路上设为不可交互，距玩家 3 以内交割：人（含伤员与经验）、俘虏、全部物品、
  剩下的钱全部还给玩家，然后销毁队伍。万一还是撞成遭遇，下一帧补做交割并关掉遭遇。
- 玩家被俘（主队失效）时原地等；玩家进城时跟进城交割。
- 队伍被消灭：记录删除，提示"带的东西一起没了"。

## 工资

每天由玩家付 `party.TotalWage`；付不起时队伍士气 −1。案件款不参与。

## 开口：我的事务或本队灰袍兵

事务入口由AffairsDispatchBlockReason统一检查：正常事务会员/声望资格、派遣行为存在、主队有效且无战斗/遭遇、野外无聚落菜单、无当前选兵/付款/交谈待办、有普通灰袍兵、同类使者不在途，以及原四目的及FiefAppeal资格。不可用返回具体原因，按钮显示并在TryStartFromAffairs再核对；其他入口继续各自既有资格，不把事务声望门槛强加到原士兵对话。（依据：用户澄清、共享归属与源码2026-10-21）

按钮成功时只记录目的和_selectionQueued并关闭事务页。SubModule的应用心跳在地图暂停时也运行，Pump须等ActiveState为MapState且无菜单/遭遇/对话再进入原OpenTroopSelection；不会在事务屏上直接打开PartyScreen。确认前只是无兵钱变更的界面意向，读档/会话重开清空这类临时待办；已在途使者沿原记录存读，不清资源。原选兵取消ReturnSelection归还人数/伤员/经验，复命和订单继续后续付款/押人；封地继续选择具体分封再最终派出。（依据：设计、原版衔接与53断言原生命令执行2026-10-21）

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
- 练兵订单的预付款按案件款带走，受 `CaseGoldFloor` 保护，路上不许拿去发饷。

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

