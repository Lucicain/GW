using System;

namespace GreyWardenPolicePurity
{
    internal static class GwpDispatchSupplyRules
    {
        internal const int TargetDays = 12;
        internal const int RefillDays = 3;
        internal const double RetryHours = 24;

        internal static int TargetFood(float dailyConsumption) =>
            Math.Max(1, (int)Math.Ceiling(Math.Max(0.01f, dailyConsumption) * TargetDays));

        internal static bool NeedsFood(float food, float dailyConsumption) =>
            food < Math.Max(0.01f, dailyConsumption) * RefillDays;

        internal static int PurchaseCount(float food, float dailyConsumption,
            int stock, int spendable, int price) => price <= 0 ? 0 :
            Math.Max(0, Math.Min(Math.Min(stock, spendable / price),
                (int)Math.Ceiling(TargetFood(dailyConsumption) - food)));
    }
}
