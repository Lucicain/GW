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
                catch (InvalidCastException) { }
            }
            int value = 0;
            try { store.SyncData(key, ref value); return value != 0; }
            catch (InvalidCastException)
            {
                bool flag = false;
                store.SyncData(key, ref flag);
                return flag;
            }
        }
    }
}
