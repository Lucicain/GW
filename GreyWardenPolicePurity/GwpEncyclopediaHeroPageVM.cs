using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal sealed class GwpEncyclopediaHeroPageExtension
    {
        private readonly Hero? _hero;

        private readonly struct DesireSuppressionDetails
        {
            public float RaidMultiplier { get; init; }
            public float VillagerMultiplier { get; init; }
            public float CaravanMultiplier { get; init; }
        }

        internal GwpEncyclopediaHeroPageExtension(Hero? hero)
        {
            _hero = hero;
            DeterrenceButtonText = GwpText.Get("{=gwp_gwpencyclopediaheropagevm_003}Record and deterrence");
            DeterrenceButtonHint = new HintViewModel(new TextObject(
                GwpText.Get("{=gwp_gwpencyclopediaheropagevm_004}View this character's permanent criminal record and current Grey Warden deterrence.")));
        }

        public string DeterrenceButtonText { get; }

        public HintViewModel DeterrenceButtonHint { get; }

        public void ExecuteOpenDeterrenceDetails()
        {
            if (_hero == null)
                return;

            GwpAiDeterrenceState.DeterrenceDetails details = GwpAiDeterrenceState.GetDeterrenceDetails(_hero);
            DesireSuppressionDetails suppression = GetDesireSuppressionDetails(_hero, details);
            string description = BuildDeterrenceDescription(details, suppression);

            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_001}{VAR_1}: Grey Warden criminal record and deterrence", "VAR_1", _hero.Name),
                    description,
                    true,
                    false,
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_002}Close"),
                    string.Empty,
                    null,
                    null),
                pauseGameActiveState: true);
        }

        private static string FormatLastEnforcement(GwpAiDeterrenceState.DeterrenceDetails details)
        {
            if (!details.HasEntry || (details.TotalArrestCount <= 0 && details.SharedDeterrenceCount <= 0))
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
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_009}Recorded crimes: {VAR_1}", "VAR_1", details.TotalCrimeCount),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_010}Grey Warden arrests: {VAR_1}", "VAR_1", details.TotalArrestCount),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_011}Personal deterrence: {VAR_1}", "VAR_1", GwpText.Format(details.DirectPenalty, "0.##")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_018}Clan deterrence: {VAR_1}", "VAR_1", GwpText.Format(details.SharedPenalty, "0.##")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_019}Current total deterrence: {VAR_1}", "VAR_1", GwpText.Format(details.EffectivePenalty, "0.##")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_012}Village-raiding desire multiplier: {VAR_1}", "VAR_1", GwpText.Format(suppression.RaidMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_013}Villager-attack desire multiplier: {VAR_1}", "VAR_1", GwpText.Format(suppression.VillagerMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_014}Caravan-attack desire multiplier: {VAR_1}", "VAR_1", GwpText.Format(suppression.CaravanMultiplier, "0.###")),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_015}Latest enforcement: {VAR_1}", "VAR_1", FormatLastEnforcement(details)),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_016}Map status: {VAR_1}", "VAR_1", details.MapStatus),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_017}Location: {VAR_1}", "VAR_1", details.MapLocation)
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

    [HarmonyPatch]
    internal static class GwpEncyclopediaHeroPageExtensionPatch
    {
        private static MethodBase? TargetMethod()
        {
            return AccessTools.Constructor(
                typeof(EncyclopediaHeroPageVM),
                new[] { typeof(EncyclopediaPageArgs) });
        }

        [HarmonyPostfix]
        private static void AttachGreyWardenControls(
            EncyclopediaHeroPageVM __instance,
            object[] __args)
        {
            Hero? hero = __args.Length > 0 && __args[0] is EncyclopediaPageArgs args
                ? args.Obj as Hero
                : null;
            GwpNativeViewModelExtension.Attach(
                __instance,
                new GwpEncyclopediaHeroPageExtension(hero));
        }
    }
}
