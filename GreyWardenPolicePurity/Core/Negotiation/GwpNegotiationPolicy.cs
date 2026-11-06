using System;

namespace GreyWardenPolicePurity.Core.Negotiation
{
    // Pure policy: the index order is full, bargain, deception, surrender,
    // sacrifice, bribe, duel, grace, generous, rash, repair. Personality changes odds, not guarantees.
    public static class GwpNegotiationPolicy
    {
        /// <summary>慷慨高：按罚金的这个比例交，多出的两成照现行实缴规则抵案底（用户裁定 2026-11-06）。</summary>
        public const int GenerousSharePercent = 120;
        /// <summary>算计低（莽撞）：第二层随口报一个罚金的比例，均匀落在这个闭区间（用户裁定 2026-11-06）。</summary>
        public const int RashShareMin = 50;
        public const int RashShareMax = 150;
        /// <summary>抽莽撞比例时传给原版整数随机数的上界（不含）。</summary>
        public const int RashShareRollSpan = RashShareMax - RashShareMin + 1;

        public static float SuccessChance(float nativeChance, int resistance, bool firstLayer) =>
            Math.Max(0.05f, Math.Min(0.95f, nativeChance + (50 - resistance) * (firstLayer ? 0.01f : 0.008f)));
        private static int Pos(int n) => Math.Max(0, n);
        /// <param name="canRepair">犯的是村庄类案子，本国眼下有废村可以带人去修。</param>
        public static int[] Weights(int honor, int mercy, int valor, int calculating,
            int generosity, int gold, int fine, bool hasTroops, bool canGrace, bool canRepair)
        {
            // 打算按性格抽，家当不再给某个方案加权（用户裁定 2026-10-15）；下面只剩"做不做得到"的排除。
            bool poor = gold < Math.Max(1, fine);
            // 仁慈高：罚金照交，再带人修本国的废村（用户裁定 2026-11-06、2026-11-07）。没有可修的村或交不起罚金时，这份仁慈仍落在照数全缴上。
            int mercyRepair = canRepair && !poor ? 5 * Pos(mercy) : 0;
            int[] w = {
                5 + 6 * Pos(honor) + 5 * Pos(mercy) - mercyRepair + 3 * Pos(-valor),
                3 + 5 * Pos(-generosity) + 2 * Pos(calculating),
                1 + 5 * Pos(-honor) + 3 * Pos(calculating),
                1 + 4 * Pos(honor) + 2 * Pos(mercy) + 2 * Pos(-valor),
                Pos(-mercy) * 5 + Pos(-honor),
                Pos(-honor) * 6 + Pos(calculating),
                1 + 5 * Pos(valor) + 2 * Pos(-calculating),
                1 + 3 * Pos(calculating) + 2 * Pos(honor),
                4 * Pos(generosity),
                4 * Pos(-calculating),
                mercyRepair
            };
            // Genuine poverty is never the "I only have half" lie. A rich liar
            // can conceal funds; an empty purse cannot fund a bribe or a bargain.
            if (gold <= 0) { w[1] = 0; w[4] = 0; w[5] = 0; w[9] = 0; }
            if (poor || honor > 0) w[2] = 0;
            if (honor > 0) w[5] = 0;
            if (mercy >= 0 || !hasTroops) w[4] = 0;
            if (!canGrace) w[7] = 0;
            // 慷慨只在真拿得出多给的那两成时成立。
            if ((long)gold * 100 < (long)Math.Max(1, fine) * GenerousSharePercent) w[8] = 0;
            return w;
        }

        /// <summary>莽撞的比例：一次实际抽签值（0 到 RashShareRollSpan-1）对应 50%～150%。</summary>
        public static int RashSharePercent(int roll)
        {
            if (roll < 0 || roll >= RashShareRollSpan) throw new ArgumentOutOfRangeException(nameof(roll));
            return RashShareMin + roll;
        }

        /// <summary>按已有权重和一次实际抽签值选择方案下标，区间为左闭右开。</summary>
        public static int SelectDesireIndex(int[] weights, int roll)
        {
            if (roll < 0) throw new ArgumentOutOfRangeException(nameof(roll));
            int remaining = roll;
            for (int index = 0; index < weights.Length; index++)
            {
                remaining -= weights[index];
                if (remaining < 0) return index;
            }
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "抽签值超出方案总权重。");
        }

        public static double RefusalChance(int resistance, int honor, int mercy,
            int generosity, int gold, int fine)
        {
            // A well-disposed, solvent lord always hears the charge, even when
            // much stronger. Hearing it is not yet agreeing to pay it.
            if (gold >= Math.Max(1, fine) && honor + mercy + generosity >= 2
                && honor >= 0 && mercy >= 0) return 0;
            return Math.Max(0, Math.Min(0.8, (resistance - 45) / 65.0));
        }

        public static float PurseResistance(int gold, int fine)
        {
            float affordability = Math.Min(2f, Math.Max(0, gold) / (float)Math.Max(1, fine));
            return (1f - affordability) * 10f;
        }
    }
}
