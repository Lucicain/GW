using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

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
            DeterrenceButtonText = GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_003);
            DeterrenceButtonHint = new HintViewModel(new TextObject(
                GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_004)));
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
                _hero,
                details,
                suppression,
                locationSettlement);

            GwpLinkedInquiryState.Begin();
            InformationManager.ShowInquiry(
                new InquiryData(
                    GwpText.Get(
                        GwpTextCatalog.gwp_gwpencyclopediaheropagevm_001,
                        "VAR_1", _hero.Name),
                    description,
                    true,
                    false,
                    GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_002),
                    string.Empty,
                    GwpLinkedInquiryState.End,
                    null),
                pauseGameActiveState: true);
        }

        private static string FormatLastEnforcement(GwpAiDeterrenceState.DeterrenceDetails details)
        {
            if (!details.HasEntry || (details.TotalArrestCount <= 0 && details.SharedDeterrenceCount <= 0))
                return GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_005);

            if (details.DaysSinceLastEnforcement < (1f / CampaignTime.HoursInDay))
                return GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_006);

            if (details.DaysSinceLastEnforcement < 1f)
            {
                float hours = details.DaysSinceLastEnforcement * CampaignTime.HoursInDay;
                return GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_007, "VAR_1", GwpText.Format(hours, "0.#"));
            }

            return GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_008, "VAR_1", GwpText.Format(details.DaysSinceLastEnforcement, "0.##"));
        }

        private static string BuildDeterrenceDescription(
            Hero hero,
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
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_record,
                        "VAR_1", details.TotalCrimeCount, "VAR_2", details.TotalArrestCount),
                    BuildLedgerBlock(hero),
                    string.Empty,
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_village_heading),
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_villager_desire,
                        "VAR_1", FormatDesirePercent(suppression.VillageMultiplier),
                        "VAR_2", FormatSuppressionPercent(suppression.VillageMultiplier)),
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_raid_desire,
                        "VAR_1", FormatDesirePercent(suppression.VillageMultiplier)),
                    BuildSourceLine(details.VillageDirectPenalty, details.VillageSharedPenalty),
                    BuildRecoveryLine(details.VillageEffectivePenalty,
                        details.VillageRecoveryFloor,
                        details.VillageRecoveryDaysRemaining, details.RecoveryPaused),
                    string.Empty,
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_caravan_heading),
                    GwpText.Get(GwpTextCatalog.gwp_det_ui_caravan_desire,
                        "VAR_1", FormatDesirePercent(suppression.CaravanMultiplier),
                        "VAR_2", FormatSuppressionPercent(suppression.CaravanMultiplier)),
                    BuildSourceLine(details.CaravanDirectPenalty, details.CaravanSharedPenalty),
                    BuildRecoveryLine(details.CaravanEffectivePenalty,
                        details.CaravanRecoveryFloor,
                        details.CaravanRecoveryDaysRemaining, details.RecoveryPaused),
                    string.Empty,
                    GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_015, "VAR_1", FormatLastEnforcement(details)),
                    GwpText.Get(GwpTextCatalog.gwp_gwpencyclopediaheropagevm_016, "VAR_1", details.MapStatus),
                    GwpText.Get(
                        GwpTextCatalog.gwp_gwpencyclopediaheropagevm_017,
                        "VAR_1", locationText)
                });
        }

        /// <summary>
        /// 这个人此刻欠灰袍多少，以及为什么欠这么多。账分两段，口径完全不同：
        /// **案底**按件累加——做下这件事本身就有；**声望罚款**只按杀掉的人命折算，
        /// 每十条人命一点、每点按调参计费，总额封顶。被警察拿下会把两段一并抹平；
        /// 犯人缴款当场抵账，与玩家以后是否交差无关。
        /// </summary>
        private static string BuildLedgerBlock(Hero? hero)
        {
            if (hero == null) return string.Empty;
            if (hero == Hero.MainHero)
                return PlayerBehaviorPool.GetReputationDisplay();

            HeroCrimeStats? history = CrimePool.GetHistory(hero);
            int standing = Math.Max(0, history?.NegativeStanding ?? 0);
            int lives = Math.Max(0, history?.UnredeemedLives ?? 0);

            CrimeRecord? crime = CrimePool.GetRecord(hero);
            bool openCase = crime?.HasOpenCase == true;

            int pending = GwpFieldReportLedger.Instance?.PendingAssessedFor(hero.StringId) ?? 0;
            if (standing <= 0 && !openCase && pending <= 0)
                return GwpText.Get(GwpTextCatalog.gwp_det_ui_ledger_clean);

            var lines = new List<string>
            {
                string.Empty,
                GwpText.Get(GwpTextCatalog.gwp_det_ui_ledger_heading)
            };

            if (openCase)
            {
                lines.Add(GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_ledger_case,
                    "VAR_1", crime!.IncidentCount.ToString(),
                    "VAR_2", Math.Max(0, crime.CivilianCasualties).ToString()));
            }

            if (pending > 0)
                lines.Add(GwpText.Get(GwpTextCatalog.gwp_case_pending_encyclopedia,
                    "VAR_1", pending, "VAR_2", GwpFieldReportLedger.Instance?.PendingCollectedFor(hero.StringId) ?? 0));

            // 罚金收的是他名下全部没赎回的人命，不只是这件案子里的。两个数字过去
            // 各说各话，账面上就成了"死了 40 个人，罚 5400"。这里按人命报数。
            if (standing > 0)
                lines.Add(GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_ledger_standing_lives,
                    "VAR_1", lives.ToString()));

            if (openCase)
                lines.Add(GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_ledger_total,
                    "VAR_1", GwpFieldArrestPricing.AssessFine(crime!).ToString()));
            else if (standing > 0)
                lines.Add(GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_ledger_no_case));

            return string.Join("\n", lines);
        }

        private static string BuildSourceLine(float personal, float transmitted)
        {
            string leading = personal > transmitted + GwpTuning.Deterrence.ForgetThreshold
                ? GwpText.Get(GwpTextCatalog.gwp_det_ui_source_personal)
                : transmitted > personal + GwpTuning.Deterrence.ForgetThreshold
                    ? GwpText.Get(GwpTextCatalog.gwp_det_ui_source_transmitted)
                    : personal <= GwpTuning.Deterrence.ForgetThreshold &&
                      transmitted <= GwpTuning.Deterrence.ForgetThreshold
                        ? GwpText.Get(GwpTextCatalog.gwp_det_ui_source_none)
                        : GwpText.Get(GwpTextCatalog.gwp_det_ui_source_balanced);

            return GwpText.Get(
                GwpTextCatalog.gwp_det_ui_source,
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
                        GwpTextCatalog.gwp_det_ui_recovery_floor_complete,
                        "VAR_1", floor);

                string floorDuration = FormatRecoveryDuration(daysRemaining);
                return recoveryPaused
                    ? GwpText.Get(
                        GwpTextCatalog.gwp_det_ui_recovery_floor_paused,
                        "VAR_1", floor, "VAR_2", floorDuration)
                    : GwpText.Get(
                        GwpTextCatalog.gwp_det_ui_recovery_floor_active,
                        "VAR_1", floor, "VAR_2", floorDuration);
            }

            if (currentPenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_recovery_complete);

            string duration = FormatRecoveryDuration(daysRemaining);

            return recoveryPaused
                ? GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_recovery_paused,
                    "VAR_1", duration)
                : GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_recovery_active,
                    "VAR_1", duration);
        }

        private static string FormatRecoveryDuration(float daysRemaining) =>
            daysRemaining < 1f
                ? GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_recovery_hours,
                    "VAR_1", GwpText.Format(
                        daysRemaining * CampaignTime.HoursInDay, "0.#"))
                : GwpText.Get(
                    GwpTextCatalog.gwp_det_ui_recovery_days,
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
        private static readonly ConditionalWeakTable<
            EncyclopediaHeroPageVM,
            GwpEncyclopediaHeroPageExtension> Extensions = new();

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
            Extensions.Remove(__instance);
            Extensions.Add(
                __instance,
                new GwpEncyclopediaHeroPageExtension(hero));
        }

        internal static bool TryGetExtension(
            EncyclopediaHeroPageVM viewModel,
            out GwpEncyclopediaHeroPageExtension extension) =>
            Extensions.TryGetValue(viewModel, out extension!);
    }

    /// <summary>
    /// Bannerlord uses a compiled prefab for the native hero page, so a loose
    /// same-name XML file is skipped. Add only the Grey Warden control after
    /// the original page has been instantiated, preserving the native layout.
    /// </summary>
    [HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Load))]
    internal static class GwpEncyclopediaHeroPageWidgetPatch
    {
        private const string HeroPageMovieName = "EncyclopediaHeroPage";
        private const string ButtonId = "GwpDeterrenceButton";
        private const string RightSidePanelId = "RightSideScrollablePanel";

        [HarmonyPostfix]
        private static void AddDeterrenceButton(
            string movieName,
            IViewModel datasource,
            IGauntletMovie __result)
        {
            if (!string.Equals(
                    movieName,
                    HeroPageMovieName,
                    StringComparison.Ordinal)
                || datasource is not EncyclopediaHeroPageVM heroPage
                || __result?.RootWidget == null
                || !GwpEncyclopediaHeroPageExtensionPatch.TryGetExtension(
                    heroPage,
                    out GwpEncyclopediaHeroPageExtension extension)
                || GwpGauntletWidgetUtility.FindById(
                    __result.RootWidget,
                    ButtonId) != null)
            {
                return;
            }

            Widget? scrollablePanel = GwpGauntletWidgetUtility.FindById(
                __result.RootWidget,
                RightSidePanelId);
            Widget? parent = scrollablePanel == null
                ? null
                : GwpGauntletWidgetUtility.FindAncestorChildOf<BrushWidget>(
                    scrollablePanel);
            if (parent == null)
                return;

            UIContext context = __result.RootWidget.Context;
            var button = new ButtonWidget(context)
            {
                Id = ButtonId,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = 150f,
                SuggestedHeight = 48f,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                MarginRight = 28f,
                MarginTop = 10f,
                Brush = context.GetBrush("Popup.Done.Button.NineGrid"),
                DoNotPassEventsToChildren = true,
                UpdateChildrenStates = true,
                GamepadNavigationIndex = 1
            };

            var text = new TextWidget(context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                Brush = context.GetBrush("Popup.Button.Text"),
                Text = extension.DeterrenceButtonText,
                IsEnabled = false
            };
            button.AddChild(text);

            var hint = new HintWidget(context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent
            };
            button.AddChild(hint);

            button.EventFire += (widget, commandName, args) =>
            {
                if (commandName == "Click")
                    extension.ExecuteOpenDeterrenceDetails();
            };
            hint.EventFire += (widget, commandName, args) =>
            {
                if (commandName == "HoverBegin")
                    extension.DeterrenceButtonHint.ExecuteBeginHint();
                else if (commandName == "HoverEnd")
                    extension.DeterrenceButtonHint.ExecuteEndHint();
            };

            parent.AddChild(button);
        }
    }

}
