using System;

namespace GreyWardenPolicePurity
{
    // Pure policy: the index order is full, bargain, deception, surrender,
    // sacrifice, bribe, duel, grace. Personality changes odds, not guarantees.
    internal static class GwpNegotiationPolicy
    {
        internal static float SuccessChance(float nativeChance, int resistance, bool firstLayer) =>
            Math.Max(0.05f, Math.Min(0.95f, nativeChance + (50 - resistance) * (firstLayer ? 0.01f : 0.008f)));
        private static int Pos(int n) => Math.Max(0, n);
        internal static int[] Weights(int honor, int mercy, int valor, int calculating,
            int generosity, int gold, int fine, bool hasTroops, bool canGrace)
        {
            bool poor = gold < Math.Max(1, fine);
            bool wealthy = gold / 2 >= Math.Max(1, fine);
            int[] w = {
                5 + 6 * Pos(honor) + 5 * Pos(mercy) + 4 * Pos(generosity) + 3 * Pos(-valor),
                3 + 5 * Pos(-generosity) + 2 * Pos(calculating),
                1 + 5 * Pos(-honor) + 3 * Pos(calculating),
                1 + 4 * Pos(honor) + 2 * Pos(mercy) + 2 * Pos(-valor),
                Pos(-mercy) * 5 + Pos(-honor),
                Pos(-honor) * 6 + Pos(calculating),
                1 + 5 * Pos(valor) + 2 * Pos(-calculating),
                1 + 3 * Pos(calculating) + 2 * Pos(honor)
            };
            if (wealthy) { w[0] += 12; w[3] = Math.Max(1, w[3] / 3); w[7] = Math.Max(1, w[7] / 3); }
            if (poor) { w[3] += 5; w[7] += 14; w[1] += 2; }
            // Genuine poverty is never the "I only have half" lie. A rich liar
            // can conceal funds; an empty purse cannot fund a bribe or a bargain.
            if (gold <= 0) { w[1] = 0; w[4] = 0; w[5] = 0; }
            if (poor || honor > 0) w[2] = 0;
            if (honor > 0) w[5] = 0;
            if (mercy >= 0 || !hasTroops) w[4] = 0;
            if (!canGrace) w[7] = 0;
            return w;
        }

        internal static double RefusalChance(int resistance, int honor, int mercy,
            int generosity, int gold, int fine)
        {
            // A well-disposed, solvent lord always hears the charge, even when
            // much stronger. Hearing it is not yet agreeing to pay it.
            if (gold >= Math.Max(1, fine) && honor + mercy + generosity >= 2
                && honor >= 0 && mercy >= 0) return 0;
            return Math.Max(0, Math.Min(0.8, (resistance - 45) / 65.0));
        }

        internal static float PurseResistance(int gold, int fine)
        {
            float affordability = Math.Min(2f, Math.Max(0, gold) / (float)Math.Max(1, fine));
            return (1f - affordability) * 10f;
        }
    }
}
