using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Who the man is decides what he says. The Wardens are not an army and the player is
    /// not one of them - he carries their warrant, nothing more - so nobody in this
    /// conversation is being marched anywhere: the charge is stated, the fine is owed, and
    /// refusing it is what turns the meeting into a fight.
    ///
    /// Three things choose the words: the offender's strongest personality trait, what the
    /// case itself says (what he did, how many he killed, whether the Wardens have taken
    /// him before), and what the player's own record is worth to him.
    /// </summary>
    internal static class GwpFieldArrestLines
    {
        internal enum Temperament
        {
            /// <summary>Honour: he will answer a charge, but not an insult.</summary>
            Upright,
            /// <summary>Calculating: everything is a sum, including this.</summary>
            Cold,
            /// <summary>Valor: paying feels like flinching.</summary>
            Fierce,
            /// <summary>Mercy: the dead are real to him.</summary>
            Soft,
            /// <summary>Generosity at its low end: the purse is the wound.</summary>
            Tight,
            Plain
        }

        internal static Temperament Read(Hero? offender)
        {
            if (offender == null) return Temperament.Plain;

            int honor = offender.GetTraitLevel(DefaultTraits.Honor);
            int calculating = offender.GetTraitLevel(DefaultTraits.Calculating);
            int valor = offender.GetTraitLevel(DefaultTraits.Valor);
            int mercy = offender.GetTraitLevel(DefaultTraits.Mercy);
            int generosity = offender.GetTraitLevel(DefaultTraits.Generosity);

            var ranked = new List<(Temperament Kind, int Weight)>
            {
                (Temperament.Upright, honor),
                (Temperament.Cold, calculating),
                (Temperament.Fierce, valor),
                (Temperament.Soft, mercy),
                (Temperament.Tight, -generosity)
            };

            (Temperament Kind, int Weight) top = ranked.OrderByDescending(entry => entry.Weight).First();
            return top.Weight >= 1 ? top.Kind : Temperament.Plain;
        }

        /// <summary>
        /// 今天已经谈崩过一次，他不肯再谈。按性子给不同的回绝，措辞里不出现任何
        /// 性格标签或数值——玩家只该看见这个人说话的样子。
        /// </summary>
        internal static TextObject Rebuffed(Temperament temperament) =>
            temperament switch
            {
                Temperament.Upright =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_upright),
                Temperament.Cold =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_cold),
                Temperament.Fierce =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_fierce),
                Temperament.Soft =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_soft),
                Temperament.Tight =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_tight),
                _ =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_rebuff_plain)
            };

        #region 开场：他先看你是谁

        internal static TextObject Opening(
            GwpFieldArrestBehavior.WardenStandingTier tier,
            bool hasBeenTakenBefore) =>
            tier switch
            {
                GwpFieldArrestBehavior.WardenStandingTier.UnderCharge =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_open_under_charge),
                GwpFieldArrestBehavior.WardenStandingTier.Respected when hasBeenTakenBefore =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_open_respected_known),
                GwpFieldArrestBehavior.WardenStandingTier.Respected =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_open_respected),
                _ when hasBeenTakenBefore =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_open_known),
                _ =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_open_plain)
            };

        #endregion

        #region 结局：认罚、赖账、抗法

        // 认罚、交不出钱、抗法三种结局的台词池都挂在 GwpFieldDialogueVoice 上，
        // 与开场白、接受论点等共用同一套"按性子取池、不连续重样、整场缓存"的取词规则。
        // 这里只负责把性子翻成池名。
        internal static string SubmitKey(Temperament temper) => "submit_" + temper;

        internal static string PleadKey(Temperament temper) => "plead_" + temper;

        internal static string ResistKey(
            Temperament temper,
            GwpFieldArrestBehavior.WardenStandingTier tier) =>
            tier == GwpFieldArrestBehavior.WardenStandingTier.UnderCharge
                ? "resist_undercharge"
                : "resist_" + temper;

        #endregion

        #region 顾虑池：按案情与他的性子挑三条

        private const string ImmediateFailKey = GwpTextCatalog.gwp_fa_task_rebuff;
        private const string FinalFailKey = GwpTextCatalog.gwp_fa_task_final;
        private const string TryLaterKey = GwpTextCatalog.gwp_fa_task_later;

        /// <summary>
        /// Six objections exist; three are used. Which three depends on the case and the
        /// man: a lord the Wardens have taken before raises that, a dishonourable one
        /// denies the deed, and a Warden under his own charge hears about it first.
        /// </summary>
        internal static List<PersuasionTask> BuildReservations(
            Hero offender,
            CrimeRecord crime,
            GwpFieldArrestBehavior.WardenStandingTier tier,
            bool hasBeenTakenBefore,
            PersuasionArgumentStrength strength)
        {
            int honor = offender.GetTraitLevel(DefaultTraits.Honor);
            bool caravan = crime.CrimeCategory == GwpCrimeCategory.CaravanAttack;
            bool manyDead = crime.CivilianCasualties >= 20;
            bool repeatDeeds = crime.IncidentCount > 1;

            var chosen = new List<Func<PersuasionArgumentStrength, PersuasionTask>>();

            if (tier == GwpFieldArrestBehavior.WardenStandingTier.UnderCharge)
                chosen.Add(AuthorityUnderCharge);
            else
                chosen.Add(Authority);

            if (honor <= 0 && !manyDead)
                chosen.Add(Denial);
            else if (hasBeenTakenBefore)
                chosen.Add(Record);
            else if (caravan)
                chosen.Add(WarExcuse);
            else
                chosen.Add(Consequence);

            chosen.Add(repeatDeeds ? Tally : Price);

            var tasks = new List<PersuasionTask>();
            for (int i = 0; i < chosen.Count; i++)
            {
                PersuasionTask task = chosen[i](strength);
                tasks.Add(task);
            }

            return tasks;
        }

        internal static List<PersuasionTask> BuildPaymentReservations(GwpOffenderDesire desire, PersuasionArgumentStrength strength)
        {
            // Every counterargument answers the terms the offender actually offered.
            string key = desire.ToString().ToLowerInvariant();
            string[] lines = PaymentArguments(desire);
            var first = Make(GwpOffenderDesires.Line(desire), strength,
                GwpText.Create("{=gwp_terms_" + key + "_lead}" + lines[0]),
                GwpText.Create("{=gwp_terms_" + key + "_charm}" + lines[1]),
                GwpText.Create("{=gwp_terms_" + key + "_rogue}" + lines[2]),
                GwpText.Create("{=gwp_terms_" + key + "_trade}" + lines[3]));
            var second = Make(GwpText.Create(GwpTextCatalog.gwp_terms_final_objection), strength,
                GwpText.Create(GwpTextCatalog.gwp_terms_final_lead),
                GwpText.Create(GwpTextCatalog.gwp_terms_final_charm),
                GwpText.Create(GwpTextCatalog.gwp_terms_final_rogue),
                GwpText.Create(GwpTextCatalog.gwp_terms_final_trade));
            return new List<PersuasionTask> { first, second };
        }

        private static string[] PaymentArguments(GwpOffenderDesire desire) => desire switch
        {
            GwpOffenderDesire.RequestGrace => new[] {
                GwpTextCatalog.English_gwp_terms_requestgrace_lead,
                GwpTextCatalog.English_gwp_terms_requestgrace_charm,
                GwpTextCatalog.English_gwp_terms_requestgrace_rogue,
                GwpTextCatalog.English_gwp_terms_requestgrace_trade },
            GwpOffenderDesire.SurrenderSelf => new[] {
                GwpTextCatalog.English_gwp_terms_surrenderself_lead,
                GwpTextCatalog.English_gwp_terms_surrenderself_charm,
                GwpTextCatalog.English_gwp_terms_surrenderself_rogue,
                GwpTextCatalog.English_gwp_terms_surrenderself_trade },
            GwpOffenderDesire.BribeTheWarden => new[] {
                GwpTextCatalog.English_gwp_terms_bribethewarden_lead,
                GwpTextCatalog.English_gwp_terms_bribethewarden_charm,
                GwpTextCatalog.English_gwp_terms_bribethewarden_rogue,
                GwpTextCatalog.English_gwp_terms_bribethewarden_trade },
            GwpOffenderDesire.CunningHalf => new[] {
                GwpTextCatalog.English_gwp_terms_cunninghalf_lead,
                GwpTextCatalog.English_gwp_terms_cunninghalf_charm,
                GwpTextCatalog.English_gwp_terms_cunninghalf_rogue,
                GwpTextCatalog.English_gwp_terms_cunninghalf_trade },
            GwpOffenderDesire.RashMost => new[] {
                GwpTextCatalog.English_gwp_terms_rashmost_lead,
                GwpTextCatalog.English_gwp_terms_rashmost_charm,
                GwpTextCatalog.English_gwp_terms_rashmost_rogue,
                GwpTextCatalog.English_gwp_terms_rashmost_trade },
            GwpOffenderDesire.DemandDuel => new[] {
                GwpTextCatalog.English_gwp_terms_demandduel_lead,
                GwpTextCatalog.English_gwp_terms_demandduel_charm,
                GwpTextCatalog.English_gwp_terms_demandduel_rogue,
                GwpTextCatalog.English_gwp_terms_demandduel_trade },
            GwpOffenderDesire.DeedsOnly => new[] {
                GwpTextCatalog.English_gwp_terms_deedsonly_lead,
                GwpTextCatalog.English_gwp_terms_deedsonly_charm,
                GwpTextCatalog.English_gwp_terms_deedsonly_rogue,
                GwpTextCatalog.English_gwp_terms_deedsonly_trade },
            GwpOffenderDesire.HaggleDown => new[] {
                GwpTextCatalog.English_gwp_terms_haggledown_lead,
                GwpTextCatalog.English_gwp_terms_haggledown_charm,
                GwpTextCatalog.English_gwp_terms_haggledown_rogue,
                GwpTextCatalog.English_gwp_terms_haggledown_trade },
            _ => new[] { GwpTextCatalog.Raw_terms_default_lead, GwpTextCatalog.Raw_terms_default_charm, GwpTextCatalog.Raw_terms_default_rogue, GwpTextCatalog.Raw_terms_default_trade }
        };

        internal static TextObject ArgumentRebuff(PersuasionOptionArgs option, GwpOffenderDesire desire, bool payment)
        {
            string skill = option.SkillUsed == DefaultSkills.Leadership ? "lead"
                : option.SkillUsed == DefaultSkills.Charm ? "charm"
                : option.SkillUsed == DefaultSkills.Roguery ? "rogue" : "trade";
            string topic = payment ? desire.ToString().ToLowerInvariant() : "authority";
            return GwpText.Create("{=gwp_rebuff_" + topic + "_" + skill + "}"
                + (payment ? PaymentRebuff(desire, skill) : skill switch {
                    "lead" => GwpTextCatalog.English_gwp_rebuff_authority_lead,
                    "charm" => GwpTextCatalog.English_gwp_rebuff_authority_charm,
                    "rogue" => GwpTextCatalog.English_gwp_rebuff_authority_rogue,
                    _ => GwpTextCatalog.English_gwp_rebuff_authority_trade }));
        }

        private static string PaymentRebuff(GwpOffenderDesire desire, string skill) => desire switch
        {
            GwpOffenderDesire.RequestGrace => GwpTextCatalog.English_gwp_rebuff_requestgrace_lead,
            GwpOffenderDesire.SurrenderSelf => GwpTextCatalog.English_gwp_rebuff_surrenderself_lead,
            GwpOffenderDesire.BribeTheWarden => GwpTextCatalog.English_gwp_rebuff_bribethewarden_lead,
            GwpOffenderDesire.CunningHalf => GwpTextCatalog.English_gwp_rebuff_cunninghalf_lead,
            GwpOffenderDesire.RashMost => GwpTextCatalog.English_gwp_rebuff_rashmost_lead,
            GwpOffenderDesire.DemandDuel => GwpTextCatalog.English_gwp_rebuff_demandduel_lead,
            GwpOffenderDesire.DeedsOnly => GwpTextCatalog.English_gwp_rebuff_deedsonly_lead,
            GwpOffenderDesire.HaggleDown => GwpTextCatalog.English_gwp_rebuff_haggledown_lead,
            _ => GwpTextCatalog.Raw_rebuff_default };

        private static PersuasionTask Make(
            TextObject spoken,
            PersuasionArgumentStrength strength,
            TextObject leadership,
            TextObject charm,
            TextObject roguery,
            TextObject trade)
        {
            var task = new PersuasionTask(0)
            {
                SpokenLine = spoken,
                ImmediateFailLine = GwpText.Create(ImmediateFailKey),
                FinalFailLine = GwpText.Create(FinalFailKey),
                TryLaterLine = GwpText.Create(TryLaterKey)
            };

            task.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Leadership, DefaultTraits.Honor, TraitEffect.Positive, strength,
                givesCriticalSuccess: false, leadership, null, false, true));
            task.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, strength,
                givesCriticalSuccess: false, charm, null, false, true));
            task.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Negative, strength,
                givesCriticalSuccess: true, roguery, null, false, true));
            task.AddOptionToTask(new PersuasionOptionArgs(
                DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, strength,
                givesCriticalSuccess: false, trade, null, false, true));
            return task;
        }

        private static PersuasionTask Authority(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_authority),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_authority_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_authority_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_authority_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_authority_trade));

        private static PersuasionTask AuthorityUnderCharge(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_under_charge),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_under_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_under_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_under_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_under_trade));

        private static PersuasionTask Price(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_price),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_price_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_price_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_price_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_price_trade));

        private static PersuasionTask Tally(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_tally),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_tally_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_tally_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_tally_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_tally_trade));

        private static PersuasionTask Denial(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_denial),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_denial_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_denial_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_denial_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_denial_trade));

        private static PersuasionTask WarExcuse(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_war),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_war_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_war_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_war_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_war_trade));

        private static PersuasionTask Consequence(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_consequence),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_consequence_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_consequence_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_consequence_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_consequence_trade));

        private static PersuasionTask Record(PersuasionArgumentStrength s) => Make(
            GwpText.Create(GwpTextCatalog.gwp_fa_task_record),
            s,
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_record_lead),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_record_charm),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_record_rogue),
            GwpText.Create(GwpTextCatalog.gwp_fa_arg_record_trade));

        #endregion
    }
}
