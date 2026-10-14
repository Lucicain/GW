using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Library;

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

        /// <summary>
        /// 第一层：他认不认这次执法。三个维度——性格、实力差、来谈的人在灰袍这边的分量。数值越高越难说服。
        /// 玩家当面谈和灰袍使者谈用同一个公式（用户裁定 2026-10-15：使者第一层看灰袍主队实力）。
        /// </summary>
        internal static int LayerOneResistance(Hero offender, GwpCrimeCategory category, float mine, float his,
            GwpFieldArrestBehavior.WardenStandingTier standing, float counterpartRenown, int wealth, int fine)
        {
            // 打得过你的人不容易低头——但也仅此而已。以势均力敌为基线，实力差只作一个有限摆幅，
            // 性格、分量、实力三个维度才谈得上共同决定。
            float edge = MBMath.ClampFloat(his / (his + mine), 0f, 1f) - 0.5f;
            float odds = GwpTuning.FieldArrest.DecisionBaseline
                         + edge * 2f * GwpTuning.FieldArrest.StrengthSwing;

            int valor = offender.GetTraitLevel(DefaultTraits.Valor);
            int honor = offender.GetTraitLevel(DefaultTraits.Honor);
            int mercy = offender.GetTraitLevel(DefaultTraits.Mercy);
            int calculating = offender.GetTraitLevel(DefaultTraits.Calculating);

            // 勇武的人硬；讲荣誉、有恻隐、会算计的人肯听。
            float temper = (valor - honor - mercy - calculating)
                           * GwpTuning.FieldArrest.TraitResistStep;

            // 来人的分量：有威望的压得住场，戴罪之身反而被顶。
            float badge = standing switch
            {
                GwpFieldArrestBehavior.WardenStandingTier.Respected => -GwpTuning.FieldArrest.StandingSwing,
                GwpFieldArrestBehavior.WardenStandingTier.UnderCharge => GwpTuning.FieldArrest.StandingSwing,
                _ => 0f
            };

            // 被灰袍抓过的人知道这本账的分量。
            float taught = MBMath.ClampFloat(
                GwpAiDeterrenceState.GetCurrentPenalty(offender, category)
                / Math.Max(1f, GwpTuning.FieldArrest.DeterrenceReference),
                0f, 1.2f) * GwpTuning.FieldArrest.DeterrenceSwing;

            return (int)MBMath.ClampFloat(odds + temper + badge - taught + OffenderStandingPressure(offender, counterpartRenown)
                + PurseResistance(wealth, fine) - offender.GetTraitLevel(DefaultTraits.Generosity) * 4f, 0f, 100f);
        }

        /// <summary>第二层：他咬着自己那套兑现方式不放的力度。特质越极端越难拉回正规全缴，罚金越让他肉痛越难。</summary>
        internal static int LayerTwoResistance(Hero offender, GwpOffenderDesire desire, float ours, float theirs,
            int counterpartCharm, float counterpartRenown, int wealth, int fine)
        {
            float force = (theirs / (theirs + ours) - 0.5f) * 16f;
            float rhetoric = MBMath.ClampFloat((offender.GetSkillValue(DefaultSkills.Charm) - counterpartCharm) / 20f, -10f, 10f);
            float pain = MBMath.ClampFloat(fine / (float)Math.Max(1, wealth), 0f, 2f);
            return (int)MBMath.ClampFloat(30f + GwpOffenderDesires.Insistence(offender, desire) * 12f
                + pain * 15f + force + rhetoric + OffenderStandingPressure(offender, counterpartRenown) * 0.5f, 0f, 100f);
        }

        private static float OffenderStandingPressure(Hero offender, float counterpartRenown)
        {
            float fameGap = ((offender.Clan?.Renown ?? 0f) - counterpartRenown) / 150f;
            var history = CrimePool.GetHistory(offender);
            return MBMath.ClampFloat(fameGap, -8f, 8f)
                + Math.Min(8f, (history?.NegativeStanding ?? 0) / 10f)
                - Math.Min(10f, (history?.TotalArrestCount ?? 0) * 2f);
        }

        // 玩家当面谈时第一层有三条要点、第二层两条（GwpFieldArrestLines.BuildReservations / BuildPaymentReservations）。
        internal const int LayerOneReservations = 3;
        internal const int LayerTwoReservations = 2;

        /// <summary>
        /// 灰袍使者替承办领主谈：没有对话，按玩家说服的规则掷骰。原版 AI 之间结婚也不走对话、按概率直接结成同一个结局
        /// （C#反编译 v1.4.8 RomanceCampaignBehavior.CheckNpcMarriages；用户裁定 2026-10-15）。
        /// 起手进度与玩家相同；每条要点最多若干次说辞，成功一次进一格，大失败结束整层。单次说辞的原版成功率照
        /// DefaultPersuasionModel.GetDefaultSuccessChance / GetChances（Normal 说辞、不计特质），技能换成承办领主的，
        /// 再按抗性换算（与玩家的 GwpNegotiationChancePatch 同一个 SuccessChance）。
        /// </summary>
        internal static bool SimulatePersuasion(int reservations, int resistance, bool firstLayer, int skill)
        {
            float ease = MBMath.ClampFloat((100 - resistance) / 100f, 0f, 1f);
            float progress = reservations * GwpTuning.FieldArrest.InitialProgressShare * ease;
            float difficulty = Campaign.Current.Models.PersuasionModel.GetDifficulty(PersuasionDifficulty.Medium);
            float baseChance = MBMath.ClampFloat((100f - difficulty / (0.01f * (100f + skill * 0.6f))) / 100f, 0.1f, 1f);
            float bonus = 1f + MBMath.ClampFloat(
                skill / (float)Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus * 0.2f, 0f, 0.2f);
            float native = MBMath.ClampFloat(baseChance * 0.55f * bonus, 0f, 1f);
            float nativeCriticalFailure = MBMath.ClampFloat(baseChance * 0.15f * (2f - bonus), 0f, 1f);
            float success = SuccessChance(native, resistance, firstLayer);
            float criticalFailure = (1f - success) * MBMath.ClampFloat(nativeCriticalFailure / Math.Max(0.0001f, 1f - native), 0f, 1f);
            for (int point = 0; point < reservations && progress < reservations; point++)
                for (int attempt = 0; attempt < GwpTuning.FieldArrest.ArgumentsPerReservation; attempt++)
                {
                    float roll = MBRandom.RandomFloat;
                    if (roll < success) { progress += 1f; break; }
                    if (roll < success + criticalFailure) return false;
                }
            return progress >= reservations;
        }

        internal static float PurseResistance(int gold, int fine)
        {
            float affordability = Math.Min(2f, Math.Max(0, gold) / (float)Math.Max(1, fine));
            return (1f - affordability) * 10f;
        }
    }
}
