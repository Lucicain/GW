using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
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
            public float VillageMultiplier { get; init; }
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
            DesireSuppressionDetails suppression = GetDesireSuppressionDetails(_hero);
            Settlement? locationSettlement = GwpAiDeterrenceState.GetTrackingSettlement(_hero);
            string description = BuildDeterrenceDescription(
                details,
                suppression,
                locationSettlement);

            GwpLinkedInquiryState.Begin();
            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get(
                        "{=gwp_gwpencyclopediaheropagevm_001}{VAR_1}: Grey Warden criminal record and deterrence",
                        "VAR_1", _hero.Name),
                    description,
                    true,
                    false,
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_002}Close"),
                    string.Empty,
                    GwpLinkedInquiryState.End,
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
            DesireSuppressionDetails suppression,
            Settlement? locationSettlement)
        {
            TextObject locationText = locationSettlement?.EncyclopediaLinkWithName
                                      ?? GwpText.Create(
                                          "{=!}{VAR_1}",
                                          "VAR_1", details.MapLocation);

            return string.Join(
                "\n",
                new[]
                {
                    GwpText.Get("{=gwp_det_ui_record}Recorded crimes: {VAR_1} | Arrested by Grey Wardens: {VAR_2}",
                        "VAR_1", details.TotalCrimeCount, "VAR_2", details.TotalArrestCount),
                    string.Empty,
                    GwpText.Get("{=gwp_det_ui_village_heading}Harm against villagers"),
                    GwpText.Get("{=gwp_det_ui_villager_desire}Desire to attack villagers: {VAR_1}% of normal (suppressed {VAR_2}%)",
                        "VAR_1", FormatDesirePercent(suppression.VillageMultiplier),
                        "VAR_2", FormatSuppressionPercent(suppression.VillageMultiplier)),
                    GwpText.Get("{=gwp_det_ui_raid_desire}Desire to raid villages: {VAR_1}% of normal (the same suppression as attacks on villagers)",
                        "VAR_1", FormatDesirePercent(suppression.VillageMultiplier)),
                    BuildSourceLine(details.VillageDirectPenalty, details.VillageSharedPenalty),
                    BuildRecoveryLine(details.VillageEffectivePenalty,
                        details.VillageRecoveryFloor,
                        details.VillageRecoveryDaysRemaining, details.RecoveryPaused),
                    string.Empty,
                    GwpText.Get("{=gwp_det_ui_caravan_heading}Harm against caravans"),
                    GwpText.Get("{=gwp_det_ui_caravan_desire}Desire to attack caravans: {VAR_1}% of normal (suppressed {VAR_2}%)",
                        "VAR_1", FormatDesirePercent(suppression.CaravanMultiplier),
                        "VAR_2", FormatSuppressionPercent(suppression.CaravanMultiplier)),
                    BuildSourceLine(details.CaravanDirectPenalty, details.CaravanSharedPenalty),
                    BuildRecoveryLine(details.CaravanEffectivePenalty,
                        details.CaravanRecoveryFloor,
                        details.CaravanRecoveryDaysRemaining, details.RecoveryPaused),
                    string.Empty,
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_015}Latest enforcement: {VAR_1}", "VAR_1", FormatLastEnforcement(details)),
                    GwpText.Get("{=gwp_gwpencyclopediaheropagevm_016}Map status: {VAR_1}", "VAR_1", details.MapStatus),
                    GwpText.Get(
                        "{=gwp_gwpencyclopediaheropagevm_017}Location: {VAR_1}",
                        "VAR_1", locationText)
                });
        }

        private static string BuildSourceLine(float personal, float transmitted)
        {
            string leading = personal > transmitted + GwpTuning.Deterrence.ForgetThreshold
                ? GwpText.Get("{=gwp_det_ui_source_personal}mainly personal experience")
                : transmitted > personal + GwpTuning.Deterrence.ForgetThreshold
                    ? GwpText.Get("{=gwp_det_ui_source_transmitted}mainly warnings passed on by clan members or witnesses")
                    : personal <= GwpTuning.Deterrence.ForgetThreshold &&
                      transmitted <= GwpTuning.Deterrence.ForgetThreshold
                        ? GwpText.Get("{=gwp_det_ui_source_none}no active deterrence")
                        : GwpText.Get("{=gwp_det_ui_source_balanced}personal experience and passed-on warnings are similar");

            return GwpText.Get(
                "{=gwp_det_ui_source}Source of suppression: personal {VAR_1} | passed on {VAR_2} ({VAR_3})",
                "VAR_1", GwpText.Format(personal, "0.##"),
                "VAR_2", GwpText.Format(transmitted, "0.##"),
                "VAR_3", leading);
        }

        private static string BuildRecoveryLine(
            float currentPenalty,
            float recoveryFloor,
            float daysRemaining,
            bool recoveryPaused)
        {
            if (recoveryFloor > GwpTuning.Deterrence.ForgetThreshold)
            {
                string floor = GwpText.Format(recoveryFloor, "0.##");
                if (currentPenalty <=
                    recoveryFloor + GwpTuning.Deterrence.RecoveryFloorTolerance)
                    return GwpText.Get(
                        "{=gwp_det_ui_recovery_floor_complete}Minimum suppression: permanently fixed at level {VAR_1}",
                        "VAR_1", floor);

                string floorDuration = FormatRecoveryDuration(daysRemaining);
                return recoveryPaused
                    ? GwpText.Get(
                        "{=gwp_det_ui_recovery_floor_paused}Estimated recovery to the level-{VAR_1} minimum: recovery is currently paused; about {VAR_2} after it resumes",
                        "VAR_1", floor, "VAR_2", floorDuration)
                    : GwpText.Get(
                        "{=gwp_det_ui_recovery_floor_active}Estimated recovery to the level-{VAR_1} minimum: about {VAR_2}",
                        "VAR_1", floor, "VAR_2", floorDuration);
            }

            if (currentPenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return GwpText.Get(
                    "{=gwp_det_ui_recovery_complete}Estimated return to normal: already restored");

            string duration = FormatRecoveryDuration(daysRemaining);

            return recoveryPaused
                ? GwpText.Get(
                    "{=gwp_det_ui_recovery_paused}Estimated return to normal: recovery is currently paused; about {VAR_1} after it resumes",
                    "VAR_1", duration)
                : GwpText.Get(
                    "{=gwp_det_ui_recovery_active}Estimated return to normal: about {VAR_1}",
                    "VAR_1", duration);
        }

        private static string FormatRecoveryDuration(float daysRemaining) =>
            daysRemaining < 1f
                ? GwpText.Get(
                    "{=gwp_det_ui_recovery_hours}{VAR_1} hours",
                    "VAR_1", GwpText.Format(
                        daysRemaining * CampaignTime.HoursInDay, "0.#"))
                : GwpText.Get(
                    "{=gwp_det_ui_recovery_days}{VAR_1} days",
                    "VAR_1", GwpText.Format(daysRemaining, "0.#"));

        private static string FormatDesirePercent(float multiplier) =>
            GwpText.Format(MathF.Max(0f, MathF.Min(1f, multiplier)) * 100f, "0.#");

        private static string FormatSuppressionPercent(float multiplier) =>
            GwpText.Format((1f - MathF.Max(0f, MathF.Min(1f, multiplier))) * 100f, "0.#");

        private static DesireSuppressionDetails GetDesireSuppressionDetails(Hero? hero)
        {
            float villageMultiplier = GwpAiDeterrenceState.GetCrimeDesireMultiplier(hero,
                GwpCrimeCategory.VillageViolence);
            float caravanMultiplier = GwpAiDeterrenceState.GetCrimeDesireMultiplier(hero,
                GwpCrimeCategory.CaravanAttack);

            return new DesireSuppressionDetails
            {
                VillageMultiplier = villageMultiplier,
                CaravanMultiplier = caravanMultiplier
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
