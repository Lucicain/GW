using System;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    // Owns the native BarterScreen outside a conversation. No custom trading UI.
    internal sealed class GwpDispatchBarterScreen
    {
        private static GwpDispatchBarterScreen? _active;
        private readonly ScreenBase _host;
        private readonly Campaign _campaign;
        private readonly GauntletLayer _layer;
        private readonly GauntletMapConversationBarterView _view;
        private bool _closed;
#if GWP_DIAGNOSTICS
        internal static bool DiagnosticsActive => _active != null;
        internal static string DiagnosticsState => _active == null ? "dispatchView=none"
            : "dispatchView=" + _active._view.IsCreated + "; closed=" + _active._closed
                + "; host=" + _active._host.GetType().FullName + "; hostFinalized=" + _active._host.IsFinalized
                + "; layerFinalized=" + _active._layer.IsFinalized + "; hostHasLayer=" + _active._host.HasLayer(_active._layer);
#endif

        private GwpDispatchBarterScreen(ScreenBase host, Campaign campaign)
        {
            _host = host;
            _campaign = campaign;
            _layer = new GauntletLayer("GwpDispatchBarter", 240, false) { IsFocusLayer = true, ActiveCursor = CursorType.Default };
            _view = new GauntletMapConversationBarterView(_layer, _ => { });
        }

        internal static void Open(BarterData data)
        {
            if (_active != null) throw new InvalidOperationException("A dispatch trade is already open");
            var screen = new GwpDispatchBarterScreen(ScreenManager.TopScreen
                ?? throw new InvalidOperationException("No map screen for dispatch trade"), Campaign.Current);
            _active = screen;
            try
            {
                screen._layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                screen._layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
                screen._layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
                screen._host.AddLayer(screen._layer);
                screen._campaign.BarterManager.Closed += screen.Close;
                screen._view.CreateBarterView(data);
                ScreenManager.TrySetFocus(screen._layer);
                screen._campaign.TimeControlMode = CampaignTimeControlMode.Stop;
            }
            catch { screen.Close(); throw; }
        }

        internal static void Tick()
        {
            var screen = _active;
            if (screen == null) return;
            if (Campaign.Current != screen._campaign)
            {
                screen.Close();
                return;
            }
            if (ScreenManager.TopScreen != screen._host)
            {
                screen._campaign.BarterManager.Close();
                return;
            }
            screen._campaign.TimeControlMode = CampaignTimeControlMode.Stop;
            BarterItemVM.IsFiveStackModifierActive = screen._layer.Input.IsHotKeyDown("FiveStackModifier");
            BarterItemVM.IsEntireStackModifierActive = screen._layer.Input.IsHotKeyDown("EntireStackModifier");
            screen._view.TickInput();
        }

        internal static void Reset() => _active?.Close();

        private void Close()
        {
            if (_closed) return;
            _closed = true;
            _campaign.BarterManager.Closed -= Close;
            try
            {
                if (_view.IsCreated) _view.DestroyBarterView();
            }
            finally
            {
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                // 宿主屏幕被弹出时，引擎已经把它的层一并终结，但 HasLayer 仍然为真。
                // 这时再 RemoveLayer 会触发 ScreenBase 的 "Screen layer is already
                // finalized" 断言（实机 fault 日志里那一条即出自此处）。
                if (!_layer.IsFinalized && !_host.IsFinalized && _host.HasLayer(_layer))
                    _host.RemoveLayer(_layer);
                if (ReferenceEquals(_active, this)) _active = null;
            }
        }
    }
}
