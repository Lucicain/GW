using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    [EncyclopediaViewModel(typeof(Clan))]
    public sealed class GwpEncyclopediaClanPageVM : EncyclopediaClanPageVM
    {
        private readonly Clan? _clan;
        private string _warReasonButtonText = string.Empty;
        private HintViewModel? _warReasonButtonHint;
        private bool _isWarReasonButtonVisible;

        public GwpEncyclopediaClanPageVM(EncyclopediaPageArgs args)
            : base(args)
        {
            _clan = args.Obj as Clan;
            RefreshWarReasonButtonState();
        }

        [DataSourceProperty]
        public string WarReasonButtonText
        {
            get => _warReasonButtonText;
            set
            {
                if (value != _warReasonButtonText)
                {
                    _warReasonButtonText = value;
                    OnPropertyChangedWithValue(value, nameof(WarReasonButtonText));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel? WarReasonButtonHint
        {
            get => _warReasonButtonHint;
            set
            {
                if (value != _warReasonButtonHint)
                {
                    _warReasonButtonHint = value;
                    OnPropertyChangedWithValue(value, nameof(WarReasonButtonHint));
                }
            }
        }

        [DataSourceProperty]
        public bool IsWarReasonButtonVisible
        {
            get => _isWarReasonButtonVisible;
            set
            {
                if (value != _isWarReasonButtonVisible)
                {
                    _isWarReasonButtonVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsWarReasonButtonVisible));
                }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            RefreshWarReasonButtonState();
        }

        public override void Refresh()
        {
            base.Refresh();
            RefreshWarReasonButtonState();
        }

        public void ExecuteOpenWarReasonDetails()
        {
            if (!GwpPoliceWarReasonService.SupportsClan(_clan))
                return;

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpPoliceWarReasonService.BuildInquiryTitle(_clan),
                    GwpPoliceWarReasonService.BuildInquiryBody(_clan),
                    true,
                    false,
                    "关闭",
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: true);
        }

        private void RefreshWarReasonButtonState()
        {
            IsWarReasonButtonVisible = GwpPoliceWarReasonService.SupportsClan(_clan);
            WarReasonButtonText = "宣战详情";
            WarReasonButtonHint = new HintViewModel(new TextObject("查看灰袍守卫当前对各势力宣战的详细原因。"));
        }
    }
}
