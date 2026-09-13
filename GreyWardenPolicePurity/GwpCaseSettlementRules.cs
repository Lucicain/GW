using System;

namespace GreyWardenPolicePurity
{
    internal static class GwpCaseSettlementRules
    {
        internal static int Reward(int cap, int casualties, int severity)
        {
            long amount = GwpTuning.FieldArrest.HandInBaseFee
                + (long)Math.Max(0, casualties) * GwpTuning.FieldArrest.HandInCompensationPerCasualty
                + (long)Math.Max(0, severity) * GwpTuning.FieldArrest.HandInPerSeverityPoint;
            return (int)Math.Min(Math.Max(0, cap), amount);
        }

        internal static int ShortfallPenalty(int assessed, int delivered) =>
            Math.Max(0, assessed - Math.Max(0, delivered)) / GwpTuning.Enforcement.FinePerPoint;
    }
}
