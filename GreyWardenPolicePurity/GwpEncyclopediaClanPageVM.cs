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
        private string _caseArchiveButtonText = string.Empty;
        private HintViewModel? _caseArchiveButtonHint;
        private bool _isCaseArchiveButtonVisible;

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

        [DataSourceProperty]
        public string CaseArchiveButtonText
        {
            get => _caseArchiveButtonText;
            set
            {
                if (value != _caseArchiveButtonText)
                {
                    _caseArchiveButtonText = value;
                    OnPropertyChangedWithValue(value, nameof(CaseArchiveButtonText));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel? CaseArchiveButtonHint
        {
            get => _caseArchiveButtonHint;
            set
            {
                if (value != _caseArchiveButtonHint)
                {
                    _caseArchiveButtonHint = value;
                    OnPropertyChangedWithValue(value, nameof(CaseArchiveButtonHint));
                }
            }
        }

        [DataSourceProperty]
        public bool IsCaseArchiveButtonVisible
        {
            get => _isCaseArchiveButtonVisible;
            set
            {
                if (value != _isCaseArchiveButtonVisible)
                {
                    _isCaseArchiveButtonVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsCaseArchiveButtonVisible));
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
                    GwpText.Get("{=gwp_gwpencyclopediaclanpagevm_001}Close"),
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: true);
        }

        public void ExecuteOpenCaseArchive()
        {
            if (!GwpPoliceWarReasonService.SupportsClan(_clan))
                return;

            GwpCaseArchiveScreen.Show();
        }

        private void RefreshWarReasonButtonState()
        {
            IsWarReasonButtonVisible = GwpPoliceWarReasonService.SupportsClan(_clan);
            WarReasonButtonText = GwpText.Get("{=gwp_gwpencyclopediaclanpagevm_002}Declaration of War Details");
            WarReasonButtonHint = new HintViewModel(new TextObject(GwpText.Get("{=gwp_gwpencyclopediaclanpagevm_003}View the grounds for each war presently prosecuted by the Grey Wardens.")));
            IsCaseArchiveButtonVisible = IsWarReasonButtonVisible;
            CaseArchiveButtonText = GwpText.Get("{=gwp_gwpencyclopediaclanpagevm_004}Case ledger");
            CaseArchiveButtonHint = new HintViewModel(new TextObject(GwpText.Get("{=gwp_gwpencyclopediaclanpagevm_005}View currently assigned cases by latest offence time and tracker.")));
        }
    }
}
