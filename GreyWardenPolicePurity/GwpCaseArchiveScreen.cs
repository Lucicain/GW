using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace GreyWardenPolicePurity
{
    public sealed class GwpAffairsState : GameState
    {
        public override bool IsMenuState => true;
        // Presentation selection is rebuilt on opening; no ongoing campaign work lives here.
    }

    [GameStateScreen(typeof(GwpAffairsState))]
    public sealed class GwpCaseArchiveScreen : ScreenBase, IGameStateListener
    {
        private readonly GwpAffairsState _state;
        private GauntletLayer? _layer;
        private GwpAffairsVM? _vm;
        private SpriteCategory? _sprites;
        private int _pageOnReturn;
        public GwpCaseArchiveScreen(GwpAffairsState state) { _state = state; }
        internal static bool IsOpen => Game.Current?.GameStateManager.ActiveState is GwpAffairsState;
        internal static bool CanOpen => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
            ?.IsRecruitedByGreyWardens == true && GwpRuntimeState.Player.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold;
        public static void Show()
        {
            Open(false);
        }
        internal static void ShowFromClanPage()
        {
            Open(true);
        }
        private static void Open(bool clanEasterEgg)
        {
            // 家族百科入口是未入会也能发现的彩蛋；接案等业务另查自身资格。
            if (Campaign.Current == null || Game.Current == null || !clanEasterEgg && !CanOpen) return;
            if (IsOpen) return;
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            GwpAffairsState state = Game.Current.GameStateManager.CreateState<GwpAffairsState>();
            Game.Current.GameStateManager.PushState(state);
        }
        private void Close()
        {
            if (Game.Current?.GameStateManager.ActiveState == _state) Game.Current.GameStateManager.PopState();
        }
        void IGameStateListener.OnInitialize() { }
        void IGameStateListener.OnActivate()
        {
            base.OnActivate();
            _sprites = UIResourceManager.LoadSpriteCategory("ui_clan");
            _layer = new GauntletLayer("GwpAffairs", 1, true) { IsFocusLayer = true, ActiveCursor = CursorType.Default };
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            // The map bar reads campaign-panel keys from this focused layer, including NavalDLC's fleet key.
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
            _vm = new GwpAffairsVM(Close, _pageOnReturn);
            _vm.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
            AddLayer(_layer);
            _layer.LoadMovie("GwpCaseArchive", _vm);
            ScreenManager.TrySetFocus(_layer);
        }
        void IGameStateListener.OnDeactivate()
        {
            base.OnDeactivate();
            if (_layer == null) return;
            ScreenManager.TryLoseFocus(_layer);
            _layer.InputRestrictions.ResetInputRestrictions();
            RemoveLayer(_layer);
            _pageOnReturn = _vm?.CurrentPage ?? 0;
            _vm?.OnFinalize();
            _vm = null;
            _layer = null;
            _sprites?.Unload();
            _sprites = null;
        }
        void IGameStateListener.OnFinalize() { }
        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            // Encyclopedia is an overlay on this screen; its focused layer owns Esc and tab input.
            if (_layer == null || ScreenManager.FocusedLayer != _layer) return;
            if (_layer.Input.IsHotKeyReleased("Exit") || _layer.Input.IsHotKeyReleased("Confirm")) Close();
            else if (_layer.Input.IsHotKeyReleased("SwitchToNextTab")) _vm?.SwitchTab(1);
            else if (_layer.Input.IsHotKeyReleased("SwitchToPreviousTab")) _vm?.SwitchTab(-1);
        }
    }
}
