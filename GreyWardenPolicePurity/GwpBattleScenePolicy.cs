using System;
using System.Collections.Generic;

namespace GreyWardenPolicePurity
{
    // Mission-only rules shared by campaign battles and Custom Battle.
    internal static class GwpBattleScenePolicy
    {
        internal const float MusicShare = .50f;
        internal static bool MusicEligible(int wardens, int soldiers) =>
            soldiers > 0 && wardens >= soldiers * MusicShare;

        private static float ReputationProgress(int reputation) =>
            Math.Max(0, Math.Min(1, (reputation - 20) / 80f));

        internal static float PowerShare(bool playerSide, int reputation) =>
            playerSide ? .20f + .30f * ReputationProgress(reputation) : .30f;

        internal static float Chance(int reputation, int stage)
        {
            return .10f + .20f * Math.Max(0, Math.Min(2, stage)) + .20f * ReputationProgress(reputation);
        }

        internal static List<int> Plan(float enemyPower, float share, float infantry, float archer, float cavalry)
        {
            var result = new List<int>();
            if (share <= 0 || share > .50f || float.IsNaN(share)) return result;
            double budget = Math.Max(0, enemyPower) * share;
            var costs = new[] { infantry, archer, cavalry };
            foreach (float cost in costs)
                if (cost <= 0 || float.IsNaN(cost) || float.IsInfinity(cost)) return result;
            if (double.IsNaN(budget) || double.IsInfinity(budget)) return result;
            int[] pattern = { 0, 1, 0, 1, 2, 0, 1, 0, 1, 2 };
            while (true)
            {
                int kind = pattern[result.Count % pattern.Length];
                if (costs[kind] > budget)
                {
                    kind = -1;
                    for (int i = 0; i < costs.Length; i++)
                        if (costs[i] <= budget && (kind < 0 || costs[i] < costs[kind])) kind = i;
                }
                if (kind < 0) return result;
                result.Add(kind); budget -= costs[kind];
            }
        }
    }

    internal sealed class GwpBattleSupportChecks
    {
        internal int Attempts { get; private set; }
        internal bool Succeeded { get; private set; }
        internal bool Complete => Succeeded || Attempts >= 3;

        internal bool Try(int opening, int alive, int reserves, bool eligible, Func<float> roll, int reputation = 100)
        {
            if (Complete || !eligible || opening <= 0 || alive <= 0) return false;
            long remaining = (long)alive + Math.Max(0, reserves);
            bool lastSurvivor = alive == 1 && reserves <= 0;
            while (Attempts < 3)
            {
                // Integer comparisons avoid floating-point percentage boundaries.
                // Tiny battles reaching one survivor get all untried chances too.
                bool reached = lastSurvivor || (Attempts == 0 ? remaining * 5 <= opening
                    : Attempts == 1 && remaining * 10 <= opening);
                if (!reached) break;
                int stage = Attempts++;
                if (roll() < GwpBattleScenePolicy.Chance(reputation, stage)) return Succeeded = true;
            }
            return false;
        }
    }
}
