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

        /// <summary>
        /// 案子在玩家手上被别人了结了——罪犯死了、被别的势力拿下了、或者卷宗已经不在。
        /// 灰袍不欠他罚金，但他确实跑了这一趟、死了这些人，这笔辛苦费照付。
        /// 不含按罪责严重度给的那一段：人不是他带回来的。
        /// </summary>
        internal static int WithdrawnCaseCompensation(int casualties)
        {
            long amount = GwpTuning.FieldArrest.HandInBaseFee
                + (long)Math.Max(0, casualties) * GwpTuning.FieldArrest.HandInCompensationPerCasualty;
            return (int)Math.Max(0, Math.Min(int.MaxValue, amount));
        }

        internal static int ShortfallPenalty(int assessed, int delivered) =>
            Math.Max(0, assessed - Math.Max(0, delivered)) / GwpTuning.Enforcement.FinePerPoint;
    }
}
