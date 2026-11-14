# 原版要人志愿兵的招募格子

> 游戏版本：v1.5.7，玩家的索引判断亦核对 v1.4.8
> 源码：`TaleWorlds.CampaignSystem/Helpers/HeroHelper.cs` 的 `HeroCanRecruitFromHero`；`TaleWorlds.CampaignSystem.ViewModelCollection/TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment/RecruitVolunteerVM.cs`、`RecruitVolunteerTroopVM.cs`、`RecruitmentVM.cs`；`TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/RecruitmentCampaignBehavior.cs` 的 `RecruitVolunteersFromNotable`、同目录 `GarrisonRecruitmentCampaignBehavior.cs` 的 `RepopulateVolunteerListCache`。
> 模组接入：[信众村的两条兵源](../state/warden-recruitment.md#信众村的两条)

## 怎么运作

1. `RecruitVolunteerVM` 从零遍历要人的 `VolunteerTypes`，通过 `HeroHelper.HeroCanRecruitFromHero` 检查本格。玩家用的判断是 `index <= MaximumIndexHeroCanRecruitFromHero(...)`，返回 `0` 仍允许选中第一格。返回 `-1` 时有兵的格子也不在可选范围内。
2. 「全部招募」逐个调用 `RecruitVolunteerTroopVM.ExecuteRecruit`，与点单格共用 `CanBeRecruited`。完成招募后 `RecruitmentVM` 从要人格子移除已选志愿兵并交给玩家。
3. AI 领主的 `RecruitVolunteersFromNotable` 自行遍历格子，在索引 `>=` 模型返回值时停止。驻军自动招募用另一个 `MaximumIndexGarrisonCanRecruitFromHero`，遍历 `i <` 其返回值，因此驻军得到 `0` 即没有候选。

## 兵源与资格

格子里的兵存于要人的 `VolunteerTypes`，`VolunteerModel` 的最大索引决定买方可选范围。可以限制玩家的资格，同时让同一份格子继续给有资格的 AI 使用。
