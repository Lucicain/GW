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
        public GwpCaseArchiveScreen(GwpAffairsState state) { _state = state; }
        internal static bool IsOpen => Game.Current?.GameStateManager.ActiveState is GwpAffairsState;
        internal static bool CanOpen => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
            ?.IsRecruitedByGreyWardens == true && GwpRuntimeState.Player.Reputation >= GwpTuning.Bounty.RecruitmentReputationThreshold;
        public static void Show()
        {
            if (!CanOpen || Game.Current == null) return;
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
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
            _vm = new GwpAffairsVM(Close);
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
            if (_layer == null) return;
            if (_layer.Input.IsHotKeyReleased("Exit")) Close();
            else if (_layer.Input.IsHotKeyReleased("SwitchToNextTab")) _vm?.SwitchTab(1);
            else if (_layer.Input.IsHotKeyReleased("SwitchToPreviousTab")) _vm?.SwitchTab(-1);
        }
    }
}
