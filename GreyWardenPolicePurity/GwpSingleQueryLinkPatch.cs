using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;

namespace GreyWardenPolicePurity
{
    internal static class GwpLinkedInquiryState
    {
        private static bool _isActive;

        internal static bool IsActive => _isActive;

        internal static void Begin()
        {
            _isActive = true;
        }

        internal static void End()
        {
            _isActive = false;
        }

        internal static void ExecuteLink(string link)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(link))
                return;

            _isActive = false;
            InformationManager.HideInquiry();
            Campaign.Current?.EncyclopediaManager?.GoToLink(link);
        }
    }

    /// <summary>
    /// The native popup already renders rich text. For Grey Warden record
    /// details only, give that native widget a link brush and route its link
    /// event to the campaign encyclopedia.
    /// </summary>
    [HarmonyPatch(typeof(GauntletMovie), nameof(GauntletMovie.Load))]
    internal static class GwpSingleQueryPopupWidgetPatch
    {
        private const string SingleQueryMovieName = "SingleQueryPopup";
        private const string DescriptionId = "Description";

        [HarmonyPostfix]
        private static void EnableGreyWardenLinks(
            string movieName,
            IViewModel datasource,
            IGauntletMovie __result)
        {
            if (!GwpLinkedInquiryState.IsActive
                || !string.Equals(
                    movieName,
                    SingleQueryMovieName,
                    StringComparison.Ordinal)
                || datasource is not SingleQueryPopUpVM
                || __result?.RootWidget == null
                || GwpGauntletWidgetUtility.FindById(
                    __result.RootWidget,
                    DescriptionId) is not RichTextWidget description)
            {
                return;
            }

            description.Brush = description.Context.GetBrush(
                "Gwp.Popup.Description.Linked.Text");
            description.EventFire += (widget, commandName, args) =>
            {
                if (commandName == "LinkClick"
                    && args.Length > 0
                    && args[0] is string link)
                {
                    GwpLinkedInquiryState.ExecuteLink(link);
                }
            };
        }
    }

    [HarmonyPatch(
        typeof(SingleQueryPopUpVM),
        nameof(SingleQueryPopUpVM.OnClearData))]
    internal static class GwpSingleQueryPopupClearPatch
    {
        [HarmonyPostfix]
        private static void ClearLinkedInquiryState()
        {
            GwpLinkedInquiryState.End();
        }
    }
}
