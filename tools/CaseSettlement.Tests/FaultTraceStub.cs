// This suite exercises settlement rules; diagnostic output is an engine boundary.
// Signature mirrors the non-diagnostic GwpFaultTrace.WriteQuiet in
// GwpDualBladeActionSetPatch.cs, which pulls in Harmony and engine types.
namespace GreyWardenPolicePurity
{
    internal static class GwpFaultTrace
    {
        internal static void WriteQuiet(
            System.Exception? error,
            [System.Runtime.CompilerServices.CallerFilePath] string? file = null,
            [System.Runtime.CompilerServices.CallerMemberName] string? member = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
        }
    }
}
