namespace GreyWardenPolicePurity
{
    /// <summary>机会属于未结案卷，重新领取委托不新建机会。</summary>
    internal static class GwpCitySearchCasePolicy
    {
        internal static bool CanBegin(CrimeRecord? crime, bool supported) => crime?.HasOpenCase == true
            && (supported ? !crime.CitySearchSupportUsed : !crime.CitySearchPersonalUsed);

        internal static bool TryBegin(CrimeRecord? crime, bool supported)
        {
            if (!CanBegin(crime, supported)) return false;
            if (supported) crime!.CitySearchSupportUsed = true;
            else crime!.CitySearchPersonalUsed = true;
            return true;
        }

        internal static bool SupportSpent(CrimeRecord? crime) => crime?.CitySearchSupportUsed == true;
    }
}
