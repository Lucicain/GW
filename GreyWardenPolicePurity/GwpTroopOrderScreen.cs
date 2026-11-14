using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class GwpTroopOrderState : GameState
    {
        public override bool IsMenuState => true;
        internal IReadOnlyList<CharacterObject> Troops { get; set; } = Array.Empty<CharacterObject>();
        internal int EscortCount { get; set; }
        internal Action<string>? Confirm { get; set; }
        internal Action? Cancel { get; set; }

        internal static void Open(IReadOnlyList<CharacterObject> troops, int escortCount, Action<string> confirm, Action? cancel)
        {
            var manager = Game.Current.GameStateManager;
            var state = manager.CreateState<GwpTroopOrderState>();
            state.Troops = troops;
            state.EscortCount = escortCount;
            state.Confirm = confirm;
            state.Cancel = cancel;
            manager.PushState(state);
        }
    }

    [GameStateScreen(typeof(GwpTroopOrderState))]
    public sealed class GwpTroopOrderScreen : ScreenBase, IGameStateListener
    {
        private readonly GwpTroopOrderState _state;
        private readonly GwpTroopOrderVM _view;
        private GauntletLayer? _layer;
        private bool _closed;

        public GwpTroopOrderScreen(GwpTroopOrderState state)
        {
            _state = state;
            _view = new GwpTroopOrderVM(state.Troops, state.EscortCount, Submit, Cancel);
        }

        private void Submit(string composition)
        {
            if (_closed || GameStateManager.Current.ActiveState != _state) return;
            _closed = true;
            GameStateManager.Current.PopState();
            _state.Confirm?.Invoke(composition);
        }

        private void Cancel()
        {
            if (_closed || GameStateManager.Current.ActiveState != _state) return;
            _closed = true;
            GameStateManager.Current.PopState();
            _state.Cancel?.Invoke();
        }

        void IGameStateListener.OnInitialize() { }

        void IGameStateListener.OnActivate()
        {
            base.OnActivate();
            _layer = new GauntletLayer("GwpTroopOrder", 1, true) { IsFocusLayer = true, ActiveCursor = CursorType.Default };
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
            AddLayer(_layer);
            _layer.LoadMovie("GwpTroopOrder", _view);
            ScreenManager.TrySetFocus(_layer);
        }

        void IGameStateListener.OnDeactivate()
        {
            base.OnDeactivate();
            if (_layer == null) return;
            ScreenManager.TryLoseFocus(_layer);
            _layer.InputRestrictions.ResetInputRestrictions();
            RemoveLayer(_layer);
            _layer = null;
        }

        void IGameStateListener.OnFinalize()
        {
            _view.OnFinalize();
            if (!_closed) _state.Cancel?.Invoke();
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            if (_layer != null && ScreenManager.FocusedLayer == _layer && _layer.Input.IsHotKeyReleased("Exit")) Cancel();
        }
    }

    internal sealed class GwpTroopOrderVM : ViewModel
    {
        private readonly Action<string> _confirm;
        private readonly Action _cancel;
        private readonly int _escortCount;

        internal GwpTroopOrderVM(IReadOnlyList<CharacterObject> troops, int escortCount, Action<string> confirm, Action cancel)
        {
            _escortCount = escortCount;
            _confirm = confirm;
            _cancel = cancel;
            foreach (CharacterObject troop in troops) Rows.Add(new GwpTroopOrderRowVM(troop, UpdateQuote));
        }

        [DataSourceProperty] public MBBindingList<GwpTroopOrderRowVM> Rows { get; } = new();
        [DataSourceProperty] public string Title => GwpText.Get(GwpTextCatalog.gwp_player_troop_order_title);
        [DataSourceProperty] public string Description => GwpText.Get(GwpTextCatalog.gwp_player_troop_order_description);
        [DataSourceProperty] public string ConfirmText => GwpText.Get(GwpTextCatalog.gwp_player_troop_order_confirm);
        [DataSourceProperty] public string CancelText => GwpText.Get(GwpTextCatalog.gwp_cancel);
        [DataSourceProperty] public bool CanConfirm => Count > 0 && Quote is { Balance: >= 0, HasRoom: true };
        [DataSourceProperty] public string Summary => GwpText.Get(GwpTextCatalog.gwp_player_troop_order_summary,
            "COUNT", Count, "COST", PlayerBehaviorPool.FormatReputationUnits(Cost),
            "REPUTATION", PlayerBehaviorPool.FormatReputationUnits(PlayerBehaviorPool.ReputationUnits));
        [DataSourceProperty] public string Restriction => Count == 0 ? string.Empty :
            GreyWardenTroopRequestBehavior.GetOrderRestriction(Quote)?.ToString() ?? string.Empty;

        private int Count => Rows.Sum(row => row.Quantity);
        private int Cost => Rows.Sum(row => row.Quantity * row.PriceUnits);
        private (int Balance, bool HasRoom) Quote => GreyWardenTroopRequestBehavior.QuoteReputationOrder(Cost, Count, _escortCount);

        private void UpdateQuote()
        {
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(Restriction));
        }

        public void ExecuteConfirm()
        {
            if (CanConfirm)
                _confirm(GreyWardenTroopRequestBehavior.EncodeOrder(Rows.Where(row => row.Quantity > 0)
                    .Select(row => (row.Troop.StringId, row.Quantity))));
        }

        public void ExecuteCancel() => _cancel();
    }

    internal sealed class GwpTroopOrderRowVM : ViewModel
    {
        private readonly Action _changed;
        private int _quantity;
        internal CharacterObject Troop { get; }
        internal int PriceUnits { get; }

        internal GwpTroopOrderRowVM(CharacterObject troop, Action changed)
        {
            Troop = troop;
            PriceUnits = GreyWardenTroopRequestBehavior.GetUnitCost(troop);
            _changed = changed;
        }

        [DataSourceProperty] public string Name => Troop.Name.ToString();
        [DataSourceProperty] public string Price => GwpText.Get(GwpTextCatalog.gwp_player_troop_order_unit_price,
            "COST", PlayerBehaviorPool.FormatReputationUnits(PriceUnits));
        [DataSourceProperty] public int MaxQuantity => GwpPlayerWardenTroops.MaximumCapacity;

        [DataSourceProperty]
        public int Quantity
        {
            get => _quantity;
            set
            {
                int quantity = Math.Max(0, Math.Min(value, MaxQuantity));
                if (quantity == _quantity) return;
                _quantity = quantity;
                OnPropertyChanged(nameof(Quantity));
                _changed();
            }
        }

        public void ExecuteAdd() => Quantity++;
        public void ExecuteRemove() => Quantity--;
    }
}
