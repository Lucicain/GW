# 玩家送信队（使者）

> 当前状态：已部署待实测（送信队不吃饭、不带口粮与盘缠）
> 最后验收：未建立检查点
> 覆盖源码：`GwpWardenDispatch.cs` `GwpWardenDispatchBehavior.cs` `GwpDispatchCargo.cs`
> 待实测：出发不再要求带粮；长途送信回来不再饿伤；押着俘虏以外的情况不再拐进城

本文件只写送信队在地图上的这一趟：出发、找人、交付、回来交割、工资。对话入口
（`GwpWardenDispatchDialogue.cs`）、分兵界面与付款 barter（`GwpAssetPayment.cs`、
`GwpDispatchBarterScreen.cs`）尚未提取。

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
- 进城只为一件事：身上有俘虏时去最近的非交战城镇，让原版 `PartiesSellPrisonerCampaignBehavior`
  卖掉；办完或 24 小时没到就放弃，24 小时内不再进城。押着本案目标时一律不进城。
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
