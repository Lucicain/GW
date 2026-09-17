#if GWP_DIAGNOSTICS
using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;

namespace GreyWardenPolicePurity
{
    // Retain only the silent failure path after the confirmed UI fix.
    [HarmonyPatch(typeof(PersonaSoftspokenTag), nameof(PersonaSoftspokenTag.IsApplicableTo))]
    internal static class GwpDispatchBarterFaultDiagnostics
    {
        private static void Finalizer(CharacterObject character, Exception __exception)
        {
            if (__exception == null || !GwpDispatchBarterScreen.DiagnosticsActive) return;
            try
            {
                GwpFaultTrace.Write("DISPATCH_BARTER_PERSONA_FAILED", details:
                    "argument=" + (character?.StringId ?? "null") + "; " + GwpDispatchBarterScreen.DiagnosticsState
                    + "; exception=" + __exception + "; observerStack=" + Environment.StackTrace);
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }
    }
}
#endif
