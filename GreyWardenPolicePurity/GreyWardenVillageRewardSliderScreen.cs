using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Overlay attached to the current village menu screen so the map remains visible behind the popup.
    /// </summary>
    public sealed class GreyWardenVillageRewardSliderScreen
    {
        private static GreyWardenVillageRewardSliderScreen? _activeOverlay;

        private readonly ScreenBase _hostScreen;
        private readonly Action<int> _onConfirmed;
        private readonly GreyWardenVillageRewardSliderVM _dataSource;
        private GauntletLayer? _gauntletLayer;

        private GreyWardenVillageRewardSliderScreen(
            ScreenBase hostScreen,
            int maxAmount,
            int initialAmount,
            Action<int> onConfirmed)
        {
            _hostScreen = hostScreen;
            _onConfirmed = onConfirmed;
            _dataSource = new GreyWardenVillageRewardSliderVM(
                maxAmount,
                initialAmount,
                ConfirmAndClose,
                Close);
        }

        public static void Show(int maxAmount, int initialAmount, Action<int> onConfirmed)
        {
            ScreenBase? hostScreen = ScreenManager.TopScreen;
            if (hostScreen == null)
            {
                return;
            }

            CloseActive();

            GreyWardenVillageRewardSliderScreen overlay =
                new GreyWardenVillageRewardSliderScreen(hostScreen, maxAmount, initialAmount, onConfirmed);
            _activeOverlay = overlay;
            overlay.Open();
        }

        public static void CloseActive()
        {
            _activeOverlay?.Close();
        }

        private void Open()
        {
            _gauntletLayer = new GauntletLayer("GwpVillageRewardOverlay", 220, false)
            {
                IsFocusLayer = true,
                ActiveCursor = CursorType.Default
            };

            // This popup must accept mouse and keyboard input itself, while blocking the village menu beneath it.
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);

            _hostScreen.AddLayer(_gauntletLayer);
            _gauntletLayer.LoadMovie("GwpVillageRewardSlider", _dataSource);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        private void ConfirmAndClose()
        {
            _onConfirmed(_dataSource.SelectedAmount);
            Close();
        }

        private void Close()
        {
            if (_gauntletLayer != null)
            {
                ScreenManager.TryLoseFocus(_gauntletLayer);
                _gauntletLayer.InputRestrictions.ResetInputRestrictions();

                if (_hostScreen.HasLayer(_gauntletLayer))
                {
                    _hostScreen.RemoveLayer(_gauntletLayer);
                }

                _gauntletLayer = null;
            }

            if (ReferenceEquals(_activeOverlay, this))
            {
                _activeOverlay = null;
            }
        }
    }

    public sealed class GreyWardenVillageRewardSliderVM : ViewModel
    {
        private readonly Action _onConfirm;
        private readonly Action _onCancel;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _confirmText = string.Empty;
        private string _cancelText = string.Empty;
        private string _selectionText = string.Empty;
        private int _selectedAmount;
        private int _maxAmount;

        public GreyWardenVillageRewardSliderVM(int maxAmount, int initialAmount, Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _maxAmount = Math.Max(1, maxAmount);
            _selectedAmount = Math.Max(0, Math.Min(initialAmount, _maxAmount));
            RefreshTexts();
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, nameof(Title));
                }
            }
        }

        [DataSourceProperty]
        public string Description
        {
            get => _description;
            set
            {
                if (value != _description)
                {
                    _description = value;
                    OnPropertyChangedWithValue(value, nameof(Description));
                }
            }
        }

        [DataSourceProperty]
        public string ConfirmText
        {
            get => _confirmText;
            set
            {
                if (value != _confirmText)
                {
                    _confirmText = value;
                    OnPropertyChangedWithValue(value, nameof(ConfirmText));
                }
            }
        }

        [DataSourceProperty]
        public string CancelText
        {
            get => _cancelText;
            set
            {
                if (value != _cancelText)
                {
                    _cancelText = value;
                    OnPropertyChangedWithValue(value, nameof(CancelText));
                }
            }
        }

        [DataSourceProperty]
        public string SelectionText
        {
            get => _selectionText;
            set
            {
                if (value != _selectionText)
                {
                    _selectionText = value;
                    OnPropertyChangedWithValue(value, nameof(SelectionText));
                }
            }
        }

        [DataSourceProperty]
        public int SelectedAmount
        {
            get => _selectedAmount;
            set
            {
                int clamped = Math.Max(0, Math.Min(value, MaxAmount));
                if (clamped != _selectedAmount)
                {
                    _selectedAmount = clamped;
                    OnPropertyChangedWithValue(clamped, nameof(SelectedAmount));
                    RefreshSelectionText();
                }
            }
        }

        [DataSourceProperty]
        public int MaxAmount
        {
            get => _maxAmount;
            set
            {
                int clamped = Math.Max(1, value);
                if (clamped != _maxAmount)
                {
                    _maxAmount = clamped;
                    OnPropertyChangedWithValue(clamped, nameof(MaxAmount));
                    if (SelectedAmount > clamped)
                    {
                        SelectedAmount = clamped;
                    }

                    RefreshSelectionText();
                }
            }
        }

        public void ExecuteConfirm()
        {
            _onConfirm();
        }

        public void ExecuteCancel()
        {
            _onCancel();
        }

        private void RefreshTexts()
        {
            Title = "设置领取金额";
            Description = $"拖动滑块，选择本次要领取的村民酬谢。当前最多可领 {MaxAmount} 第纳尔。";
            ConfirmText = "确认";
            CancelText = "取消";
            RefreshSelectionText();
        }

        private void RefreshSelectionText()
        {
            SelectionText = $"{SelectedAmount} 第纳尔";
        }
    }
}
