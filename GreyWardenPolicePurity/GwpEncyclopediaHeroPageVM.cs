using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    [EncyclopediaViewModel(typeof(Hero))]
    public sealed class GwpEncyclopediaHeroPageVM : EncyclopediaHeroPageVM
    {
        private readonly Hero? _hero;
        private string _deterrenceButtonText = string.Empty;
        private HintViewModel? _deterrenceButtonHint;

        private readonly struct DesireSuppressionDetails
        {
            public float RaidMultiplier { get; init; }
            public float VillagerMultiplier { get; init; }
            public float CaravanMultiplier { get; init; }
        }

        public GwpEncyclopediaHeroPageVM(EncyclopediaPageArgs args)
            : base(args)
        {
            _hero = args.Obj as Hero;
            RefreshDeterrenceButtonState();
        }

        [DataSourceProperty]
        public string DeterrenceButtonText
        {
            get => _deterrenceButtonText;
            set
            {
                if (value != _deterrenceButtonText)
                {
                    _deterrenceButtonText = value;
                    OnPropertyChangedWithValue(value, nameof(DeterrenceButtonText));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel? DeterrenceButtonHint
        {
            get => _deterrenceButtonHint;
            set
            {
                if (value != _deterrenceButtonHint)
                {
                    _deterrenceButtonHint = value;
                    OnPropertyChangedWithValue(value, nameof(DeterrenceButtonHint));
                }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            RefreshDeterrenceButtonState();
        }

        public override void Refresh()
        {
            base.Refresh();
            RefreshDeterrenceButtonState();
        }

        public void ExecuteOpenDeterrenceDetails()
        {
            if (_hero == null)
                return;

            GwpAiDeterrenceState.DeterrenceDetails details = GwpAiDeterrenceState.GetDeterrenceDetails(_hero);
            DesireSuppressionDetails suppression = GetDesireSuppressionDetails(_hero, details);
            string description = BuildDeterrenceDescription(details, suppression);

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_001}{VAR_1}: Grey Warden deterrence record", "VAR_1", _hero.Name),
                    description,
                    true,
                    false,
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_002}Close"),
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: true);
        }

        private void RefreshDeterrenceButtonState()
        {
            DeterrenceButtonText = GwpText.Get("{=gwp_gwpencyclopediaheropagevm_003}Deterrence");
            DeterrenceButtonHint = new HintViewModel(new TextObject(GwpText.Get("{=gwp_gwpencyclopediaheropagevm_004}View this character’s present Grey Warden deterrence record.")));
        }

        private static string FormatLastEnforcement(GwpAiDeterrenceState.DeterrenceDetails details)
        {
            if (!details.HasEntry)
                return GwpText.Get("{=gwp_gwpencyclopediaheropagevm_005}No record");

            if (details.DaysSinceLastEnforcement < (1f / CampaignTime.HoursInDay))
                return GwpText.Get("{=gwp_gwpencyclopediaheropagevm_006}Just");

            if (details.DaysSinceLastEnforcement < 1f)
            {
                float hours = details.DaysSinceLastEnforcement * CampaignTime.HoursInDay;
                return GwpText.Get("{=gwp_gwpencyclopediaheropagevm_007}{VAR_1} hours ago", "VAR_1", GwpText.Format(hours, "0.#"));
            }

            return GwpText.Get("{=gwp_gwpencyclopediaheropagevm_008}{VAR_1} days ago", "VAR_1", GwpText.Format(details.DaysSinceLastEnforcement, "0.##"));
        }

        private static string BuildDeterrenceDescription(
            GwpAiDeterrenceState.DeterrenceDetails details,
            DesireSuppressionDetails suppression)
        {
            return string.Join(
                "\n",
                new[]
                {
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_009}Current shock value: {VAR_1}", "VAR_1", GwpText.Format(details.EffectivePenalty, "0.##")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_010}Number of times individual crimes were shocked: {VAR_1}", "VAR_1", details.EnforcementCount),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_011}Successive chastisements: {VAR_1}", "VAR_1", details.SharedDeterrenceCount),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_012}The desire suppression multiplier for burning villages: {VAR_1}", "VAR_1", GwpText.Format(suppression.RaidMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_013}The desire suppression multiplier for attacking villagers: {VAR_1}", "VAR_1", GwpText.Format(suppression.VillagerMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_014}The desire suppression multiplier for attacking caravans: {VAR_1}", "VAR_1", GwpText.Format(suppression.CaravanMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_015}The latest shock: {VAR_1}", "VAR_1", FormatLastEnforcement(details)),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_016}Large map status: {VAR_1}", "VAR_1", details.MapStatus),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_017}Specific location: {VAR_1}", "VAR_1", details.MapLocation)
                });
        }

        private static DesireSuppressionDetails GetDesireSuppressionDetails(
            Hero? hero,
            GwpAiDeterrenceState.DeterrenceDetails details)
        {
            float multiplier = GwpAiDeterrenceState.GetCrimeDesireMultiplier(hero);

            return new DesireSuppressionDetails
            {
                RaidMultiplier = multiplier,
                VillagerMultiplier = multiplier,
                CaravanMultiplier = multiplier
            };
        }
    }
}
