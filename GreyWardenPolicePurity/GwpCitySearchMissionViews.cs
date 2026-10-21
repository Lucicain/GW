using System.Collections.Generic;
using SandBox.View;
using SandBox.View.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 进城搜捕场景的界面：原版城镇中心那一组（对话、名牌、按住 Tab 离开等）加上指挥部下的界面。
    /// 原版 TownCenter 不带下令界面，原版 CombatWithDialogue 带下令但没有离开与名牌（SandBox.View v1.4.8 反编译 2026-10-21）。
    /// 原版按任务名收集带 ViewCreatorModule 的类，只认启用模组的程序集。
    /// </summary>
    [ViewCreatorModule]
    public static class GwpCitySearchMissionViews
    {
        [ViewMethod(GwpCitySearchMission.MissionName)]
        public static MissionView[] Open(Mission mission) => new List<MissionView>
        {
            new MissionCampaignView(),
            new MissionConversationCameraView(),
            SandBoxViewCreator.CreateMissionConversationView(mission),
            ViewCreator.CreateMissionSingleplayerEscapeMenu(CampaignOptions.IsIronmanMode),
            ViewCreator.CreateOptionsUIHandler(),
            ViewCreator.CreateMissionMainAgentEquipDropView(mission),
            ViewCreator.CreateMissionAgentLabelUIHandler(mission),
            ViewCreator.CreateMissionOrderUIHandler(null),
            new OrderTroopPlacer(null),
            new MissionSingleplayerViewHandler(),
            ViewCreator.CreateMissionAgentStatusUIHandler(mission),
            ViewCreator.CreateMissionMainAgentEquipmentController(mission),
            ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
            ViewCreator.CreateMissionAgentLockVisualizerView(mission),
            SandBoxViewCreator.CreateMissionNameMarkerUIHandler(mission),
            new MissionItemContourControllerView(),
            new MissionAgentContourControllerView(),
            ViewCreator.CreateMissionBoundaryCrossingView(),
            ViewCreator.CreateMissionLeaveView(),
            ViewCreator.CreatePhotoModeView(),
            SandBoxViewCreator.CreateMissionBarterView(),
            ViewCreator.CreateSingleplayerMissionKillNotificationUIHandler(),
            new MissionBoundaryWallView(),
            ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
            new MissionFormationTargetSelectionHandler(),
            ViewCreator.CreateMissionSpectatorControlView(mission),
            new MissionCampaignBattleSpectatorView()
        }.ToArray();
    }
}
