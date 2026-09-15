#if GWP_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GreyWardenPolicePurity
{
    // Engine assertions can open an error dialog without throwing an Exception.
    // Observe the arguments and caller without suppressing the original dialog.
    [HarmonyPatch]
    internal static class GwpEngineAssertDiagnostics
    {
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(TaleWorlds.Library.Debug).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                if (method.Name == "FailedAssert") yield return method;
        }

        private static void Prefix(object[] __args)
        {
            try
            {
                string message = string.Join(" | ", __args);
                lock (Seen)
                {
                    if (Seen.Count >= 40 || !Seen.Add(message)) return;
                }
                GwpFaultTrace.Write("ENGINE_ASSERT", details: message + " | " + Environment.StackTrace);
            }
            catch { }
        }
    }
}
#endif
