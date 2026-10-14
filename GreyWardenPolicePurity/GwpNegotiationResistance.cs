using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 两层谈判的抗性与掷骰，读游戏里的人物和状态。纯数字的规则留在 <see cref="GwpNegotiationPolicy"/>
    /// （它也编进脱离游戏的 CaseSettlement.Tests，所以不能引用引擎类型）。玩家当面谈和灰袍使者谈都调这里。
    /// </summary>
    internal static class GwpNegotiationResistance
    {
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
                + GwpNegotiationPolicy.PurseResistance(wealth, fine) - offender.GetTraitLevel(DefaultTraits.Generosity) * 4f, 0f, 100f);
        }

        /// <summary>
        /// 第二层：他咬着自己那套兑现方式不放的力度。性格至上（用户裁定 2026-10-15）：对应特质越极端越难谈，
        /// 实力差不进这一层（第一层已经看过）；罚金越让他肉痛越难。
        /// </summary>
        internal static int LayerTwoResistance(Hero offender, GwpOffenderDesire desire,
            int counterpartCharm, float counterpartRenown, int wealth, int fine)
        {
            float rhetoric = MBMath.ClampFloat((offender.GetSkillValue(DefaultSkills.Charm) - counterpartCharm) / 20f, -10f, 10f);
            float pain = MBMath.ClampFloat(fine / (float)Math.Max(1, wealth), 0f, 2f);
            return (int)MBMath.ClampFloat(30f + GwpOffenderDesires.Insistence(offender, desire) * GwpTuning.FieldArrest.InsistenceStep
                + pain * 15f + rhetoric + OffenderStandingPressure(offender, counterpartRenown) * 0.5f, 0f, 100f);
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
            float success = GwpNegotiationPolicy.SuccessChance(native, resistance, firstLayer);
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
    }
}
