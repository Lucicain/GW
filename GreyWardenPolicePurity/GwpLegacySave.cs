using System;
using TaleWorlds.CampaignSystem;

namespace GreyWardenPolicePurity
{
    internal static class GwpLegacySave
    {
        internal static bool ReadFlag(IDataStore store, string key, bool storedAsBool)
        {
            if (storedAsBool)
            {
                bool flag = false;
                try { store.SyncData(key, ref flag); return flag; }
                catch (Exception gwpQuietFailure) { /* 依据：实机诊断 2026-09-15（GUARDED_FAILURE 4 次：旧档 gwp_case_support_requested 存成了另一种类型） */ GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            }
            int value = 0;
            try { store.SyncData(key, ref value); return value != 0; }
            catch (InvalidCastException)
            {
                // 依据：实机诊断 2026-09-15（GUARDED_FAILURE 4 次：旧档 gwp_case_support_requested 存成了另一种类型）
                // The key was written as a bool; this is the format probe, not a failure.
                bool flag = false;
                store.SyncData(key, ref flag);
                return flag;
            }
        }
    }
}
