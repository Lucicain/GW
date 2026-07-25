using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Keeps the native single-query popup and only supplies the missing command
    /// that lets its RichTextWidget route an inline encyclopedia link.
    /// </summary>
    internal static class GwpLinkedInquiryState
    {
        private static bool _isActive;

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

    internal sealed class GwpSingleQueryLinkExtension
    {
        public void ExecuteLink(string link)
        {
            GwpLinkedInquiryState.ExecuteLink(link);
        }
    }

    [HarmonyPatch(
        typeof(SingleQueryPopUpVM),
        MethodType.Constructor,
        typeof(Action))]
    internal static class GwpSingleQueryPopupConstructorPatch
    {
        [HarmonyPostfix]
        private static void AttachLinkCommand(SingleQueryPopUpVM __instance)
        {
            GwpNativeViewModelExtension.Attach(
                __instance,
                new GwpSingleQueryLinkExtension());
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
