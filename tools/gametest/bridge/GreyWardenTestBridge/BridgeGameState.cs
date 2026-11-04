using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 主线程上判断游戏处在哪个阶段（PROTOCOL.md §5 status 的 state），以及是什么界面在等玩家处理。
    /// 只读原版状态，不点任何按钮（PROTOCOL.md §7.5）。
    /// </summary>
    internal sealed class BridgeGameState
    {
        private bool _seenMainMenu;
        private int _mapFrames;
        private string? _inquiryTitle;
        private string? _inquiryText;
        private string? _sceneNotificationTitle;

        internal BridgeGameState()
        {
            // 询问框和场景通知的标题只能从显示事件里拿到；记下来给 blocked / load_failed 当证据。
            InformationManager.OnShowInquiry += (data, pause, prioritize) =>
            {
                _inquiryTitle = data.TitleText;
                _inquiryText = data.Text;
            };
            InformationManager.OnHideInquiry += () =>
            {
                _inquiryTitle = null;
                _inquiryText = null;
            };
            MBInformationManager.OnShowMultiSelectionInquiry += (data, pause, prioritize) =>
            {
                _inquiryTitle = data.TitleText;
                _inquiryText = null;
            };
            MBInformationManager.OnShowSceneNotification += data => _sceneNotificationTitle = data.TitleText?.ToString();
            MBInformationManager.OnHideSceneNotification += () => _sceneNotificationTitle = null;
        }

        /// <summary>每帧开头调用一次。</summary>
        internal void Observe()
        {
            GameState? active = GameStateManager.Current?.ActiveState;
            if (active is InitialState) _seenMainMenu = true;
            _mapFrames = active is MapState && Campaign.Current != null ? _mapFrames + 1 : 0;
        }

        /// <summary>地图状态已连续跑了几帧（读档回复要等进入地图并跑完第一帧，PROTOCOL.md §5 load_save）。</summary>
        internal int MapFrames => _mapFrames;

        /// <summary>主菜单：全局状态机停在 InitialState，且没有游戏在跑。</summary>
        // TODO(本地核对)：主菜单时 GameStateManager.Current 是 Module.CurrentModule.GlobalGameStateManager、
        // ActiveState 是 InitialState，且 Game.Current 为 null。
        internal bool IsMainMenu => GameStateManager.Current?.ActiveState is InitialState && Game.Current == null;

        /// <summary>地图上，可以执行战役命令。</summary>
        internal bool IsCampaignMap => GameStateManager.Current?.ActiveState is MapState && Campaign.Current != null;

        /// <summary>战役已建好、可以读战役时间（地图、任务或地图上的其他界面）。</summary>
        internal bool HasLiveCampaign
        {
            get
            {
                GameState? active = GameStateManager.Current?.ActiveState;
                return Campaign.Current != null && active != null && !(active is GameLoadingState) && !(active is InitialState);
            }
        }

        internal string Describe()
        {
            GameState? active = GameStateManager.Current?.ActiveState;
            if (active is GameLoadingState) return "loading";
            if (active is MissionState) return "mission";
            if (IsCampaignMap) return "campaign";
            if (active is InitialState) return "main_menu";
            if (!_seenMainMenu) return "starting";
            return "other";
        }

        internal static JObject Time(CampaignTime time) => new JObject
        {
            ["hours"] = time.ToHours,
            ["text"] = time.ToString(),
        };

        /// <summary>询问框或场景通知这类必须玩家点掉的弹窗；没有返回 null。</summary>
        internal JObject? ModalBlocker()
        {
            if (InformationManager.IsAnyInquiryActive())
                return Blocker("inquiry", _inquiryTitle, _inquiryText);
            if (MBInformationManager.GetIsAnySceneNotificationActive() == true)
                return Blocker("scene_notification", _sceneNotificationTitle, null);
            return null;
        }

        /// <summary>推进时间停住时，是什么在等玩家（PROTOCOL.md §5 advance 的 blocked.reason）。</summary>
        internal JObject DescribeBlocker()
        {
            JObject? modal = ModalBlocker();
            if (modal != null) return modal;

            GameState? active = GameStateManager.Current?.ActiveState;
            if (active is MissionState) return Blocker("mission", null, null);
            if (!(active is MapState mapState) || Campaign.Current == null)
                return Blocker("state", active?.GetType().Name, null);

            Campaign campaign = Campaign.Current;
            if (campaign.ConversationManager?.IsConversationInProgress == true)
                return Blocker("conversation", campaign.ConversationManager.OneToOneConversationHero?.StringId, null);
            if (PlayerEncounter.Current != null)
                return Blocker("encounter", campaign.CurrentMenuContext?.GameMenu?.StringId, null);
            if (mapState.AtMenu)
                return Blocker("menu", campaign.CurrentMenuContext?.GameMenu?.StringId, null);
            if (campaign.TimeControlModeLock)
                return Blocker("time_locked", null, null);
            return Blocker("stalled", null, null);
        }

        private static JObject Blocker(string reason, string? title, string? text)
        {
            var result = new JObject { ["reason"] = reason, ["title"] = title };
            if (text != null) result["text"] = text;
            return result;
        }
    }
}
