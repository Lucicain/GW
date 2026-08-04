using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace GreyWardenPolicePurity
{
    internal sealed class GwpEncyclopediaClanPageExtension
    {
        private readonly Clan? _clan;

        internal GwpEncyclopediaClanPageExtension(Clan? clan)
        {
            _clan = clan;
            WarReasonButtonText = GwpText.Get(
                "{=gwp_gwpencyclopediaclanpagevm_002}Declaration of War Details");
            WarReasonButtonHint = new HintViewModel(new TextObject(GwpText.Get(
                "{=gwp_gwpencyclopediaclanpagevm_003}View the grounds for each war presently prosecuted by the Grey Wardens.")));
            CaseArchiveButtonText = GwpText.Get(
                "{=gwp_gwpencyclopediaclanpagevm_004}Case ledger");
            CaseArchiveButtonHint = new HintViewModel(new TextObject(GwpText.Get(
                "{=gwp_gwpencyclopediaclanpagevm_005}View currently assigned cases by latest offence time and tracker.")));
        }

        internal bool IsVisible => GwpPoliceWarReasonService.SupportsClan(_clan);

        internal string WarReasonButtonText { get; }

        internal HintViewModel WarReasonButtonHint { get; }

        internal string CaseArchiveButtonText { get; }

        internal HintViewModel CaseArchiveButtonHint { get; }

        internal void ExecuteOpenWarReasonDetails()
        {
            if (!IsVisible)
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

        internal void ExecuteOpenCaseArchive()
        {
            if (IsVisible)
                GwpCaseArchiveScreen.Show();
        }
    }

    [HarmonyPatch]
    internal static class GwpEncyclopediaClanPageExtensionPatch
    {
        private static readonly ConditionalWeakTable<
            EncyclopediaClanPageVM,
            GwpEncyclopediaClanPageExtension> Extensions = new();

        private static MethodBase? TargetMethod() =>
            AccessTools.Constructor(
                typeof(EncyclopediaClanPageVM),
                new[] { typeof(EncyclopediaPageArgs) });

        [HarmonyPostfix]
        private static void AttachGreyWardenControls(
            EncyclopediaClanPageVM __instance,
            object[] __args)
        {
            Clan? clan = __args.Length > 0 && __args[0] is EncyclopediaPageArgs args
                ? args.Obj as Clan
                : null;
            Extensions.Remove(__instance);
            Extensions.Add(
                __instance,
                new GwpEncyclopediaClanPageExtension(clan));
        }

        internal static bool TryGetExtension(
            EncyclopediaClanPageVM viewModel,
            out GwpEncyclopediaClanPageExtension extension) =>
            Extensions.TryGetValue(viewModel, out extension!);
    }

    /// <summary>
    /// Extend the compiled native clan page with Grey Warden commands without
    /// replacing its view model or maintaining a copy of the native prefab.
    /// </summary>
    [HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Load))]
    internal static class GwpEncyclopediaClanPageWidgetPatch
    {
        private const string ClanPageMovieName = "EncyclopediaClanPage";
        private const string RightSidePanelId = "RightSideScrollablePanel";
        private const string WarReasonButtonId = "GwpWarReasonButton";
        private const string CaseArchiveButtonId = "GwpCaseArchiveButton";

        [HarmonyPostfix]
        private static void AddGreyWardenButtons(
            string movieName,
            IViewModel datasource,
            IGauntletMovie __result)
        {
            if (!string.Equals(movieName, ClanPageMovieName, StringComparison.Ordinal)
                || datasource is not EncyclopediaClanPageVM clanPage
                || __result?.RootWidget == null
                || !GwpEncyclopediaClanPageExtensionPatch.TryGetExtension(
                    clanPage,
                    out GwpEncyclopediaClanPageExtension extension)
                || !extension.IsVisible
                || GwpGauntletWidgetUtility.FindById(
                    __result.RootWidget,
                    WarReasonButtonId) != null)
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

            AddButton(
                parent,
                WarReasonButtonId,
                extension.WarReasonButtonText,
                extension.WarReasonButtonHint,
                28f,
                1,
                extension.ExecuteOpenWarReasonDetails);
            AddButton(
                parent,
                CaseArchiveButtonId,
                extension.CaseArchiveButtonText,
                extension.CaseArchiveButtonHint,
                188f,
                2,
                extension.ExecuteOpenCaseArchive);
        }

        private static void AddButton(
            Widget parent,
            string id,
            string label,
            HintViewModel hintViewModel,
            float marginRight,
            int navigationIndex,
            Action execute)
        {
            UIContext context = parent.Context;
            var button = new ButtonWidget(context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = 150f,
                SuggestedHeight = 48f,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                MarginRight = marginRight,
                MarginTop = 10f,
                Brush = context.GetBrush("Popup.Done.Button.NineGrid"),
                DoNotPassEventsToChildren = true,
                UpdateChildrenStates = true,
                GamepadNavigationIndex = navigationIndex
            };

            button.AddChild(new TextWidget(context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                Brush = context.GetBrush("Popup.Button.Text"),
                Text = label,
                IsEnabled = false
            });

            var hint = new HintWidget(context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent
            };
            button.AddChild(hint);

            button.EventFire += (widget, commandName, args) =>
            {
                if (commandName == "Click")
                    execute();
            };
            hint.EventFire += (widget, commandName, args) =>
            {
                if (commandName == "HoverBegin")
                    hintViewModel.ExecuteBeginHint();
                else if (commandName == "HoverEnd")
                    hintViewModel.ExecuteEndHint();
            };

            parent.AddChild(button);
        }
    }
}
