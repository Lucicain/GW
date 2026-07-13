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
                    $"{_hero.Name} 的灰袍震慑明细",
                    description,
                    true,
                    false,
                    "关闭",
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: true);
        }

        private void RefreshDeterrenceButtonState()
        {
            DeterrenceButtonText = "灰袍震慑";
            DeterrenceButtonHint = new HintViewModel(new TextObject("查看此人物当前的灰袍震慑调试信息。"));
        }

        private static string FormatLastEnforcement(GwpAiDeterrenceState.DeterrenceDetails details)
        {
            if (!details.HasEntry)
                return "无记录";

            if (details.DaysSinceLastEnforcement < (1f / CampaignTime.HoursInDay))
                return "刚刚";

            if (details.DaysSinceLastEnforcement < 1f)
            {
                float hours = details.DaysSinceLastEnforcement * CampaignTime.HoursInDay;
                return $"{hours:0.#} 小时前";
            }

            return $"{details.DaysSinceLastEnforcement:0.##} 天前";
        }

        private static string BuildDeterrenceDescription(
            GwpAiDeterrenceState.DeterrenceDetails details,
            DesireSuppressionDetails suppression)
        {
            return string.Join(
                "\n",
                new[]
                {
                    $"当前震慑值：{details.EffectivePenalty:0.##}",
                    $"个人犯罪被震慑次数：{details.EnforcementCount}",
                    $"连坐被震慑次数：{details.SharedDeterrenceCount}",
                    $"烧村欲望压制倍率：{suppression.RaidMultiplier:0.###}",
                    $"攻击村民欲望压制倍率：{suppression.VillagerMultiplier:0.###}",
                    $"攻击商队欲望压制倍率：{suppression.CaravanMultiplier:0.###}",
                    $"最近一次受震慑：{FormatLastEnforcement(details)}",
                    $"大地图状态：{details.MapStatus}",
                    $"具体位置：{details.MapLocation}"
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
