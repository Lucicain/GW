# 家族加入与离开王国

> 游戏版本：v1.4.8；v1.4.7 本轮未取得对应 DLL，不能套用为已验证
> 源码：`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/ChangeKingdomAction.cs`；`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Clan.cs`
> 建国入口源码：`managed/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/KingdomManager.cs` 的 `CreateKingdom`；同程序集 `Kingdom.cs` 的 `InitializeKingdom`
> 模组接入：[灰袍入国保护](../state/kingdom-membership.md)

## 怎么运作

普通加入、叛国加入、佣兵加入和建国的公开入口分别为 `ApplyByJoinToKingdom`、
`ApplyByJoinToKingdomByDefection`、`ApplyByJoinFactionAsMercenary`、`ApplyByCreateKingdom`。
它们调用私有 `ApplyInternal`，动作本身不再查询外交招募评分。

`ApplyInternal` 先清债务，再按动作调整停留期限与外交立场。普通加入、叛国加入和建国写入
`clan.Kingdom`；佣兵加入交给 `StartMercenaryServiceAction`。最后调整部队移动并发送
`OnClanChangedKingdom`。叛国公开入口在内部动作返回之后还会发送 `OnClanDefected`。

`Clan.Kingdom` 的公开 setter 直接调用 `SetKingdomInternal`。后者先移出旧国，写 `_kingdom`，
再加入新国，更新旗色与阵营变化时间。进入王国时同步王国的家族、英雄、封地和部队缓存；
离开时移除相应缓存，并使部队离开军团。这条属性写入路径不回调上述四个公开动作。

`_kingdom` 是 `SaveableField(57)`，已有归属由存档恢复。只拦动作不能阻止恢复存档里的旧归属。

`KingdomManager.CreateKingdom` 先注册一个新王国，初始化名称、文化、旗色、默认政策等，再调用
`ApplyByCreateKingdom(founderClan, kingdom, false)` 处理创建者家族入国。初始化王国本身不写家族归属。
因此只拦建国动作能阻止家族入国，但不撤销此前已经创建的王国对象；动作之后仍会发送建国事件。

## 容易想错的地方

- 招募评分降低只约束查询模型的调用者；直接调用动作仍会入国。
- 只跳过 `ApplyInternal` 不能阻止叛国公开入口后续发送事件。
- `clan.Kingdom = target` 可以绕过四个动作拦截；这是可行路径，不证明某个第三方模组实际使用了它。
- 第三方模组可以在动作返回后自行显示“加入成功”；通知是否出现与实际归属要分别取证。
- 本手册不覆盖其他模组的 Harmony 补丁，也不证明某个玩家环境的补丁已成功绑定。
