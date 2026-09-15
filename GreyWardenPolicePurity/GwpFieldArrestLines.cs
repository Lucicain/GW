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

        #region 开场：他先看你是谁

        internal static TextObject Opening(
            GwpFieldArrestBehavior.WardenStandingTier tier,
            bool hasBeenTakenBefore) =>
            tier switch
            {
                GwpFieldArrestBehavior.WardenStandingTier.UnderCharge =>
                    GwpText.Create("{=gwp_fa_open_under_charge}You work for the Wardens? Tell me what you want."),
                GwpFieldArrestBehavior.WardenStandingTier.Respected when hasBeenTakenBefore =>
                    GwpText.Create("{=gwp_fa_open_respected_known}You again. Or your order, at least. What is it this time?"),
                GwpFieldArrestBehavior.WardenStandingTier.Respected =>
                    GwpText.Create("{=gwp_fa_open_respected}I know the grey. Speak, and speak plainly."),
                _ when hasBeenTakenBefore =>
                    GwpText.Create("{=gwp_fa_open_known}The Wardens again. I remember the last time."),
                _ =>
                    GwpText.Create("{=gwp_fa_open_plain}The Wardens. What do you want with me?")
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

        private const string ImmediateFailKey = "{=gwp_fa_task_rebuff}That is no answer.";
        private const string FinalFailKey = "{=gwp_fa_task_final}You have run out of words, Warden.";
        private const string TryLaterKey = "{=gwp_fa_task_later}I will hear no more arguments about this warrant.";

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
            var second = Make(GwpText.Create("{=gwp_terms_final_objection}And if I pay the full amount, what do I gain by giving up my terms?"), strength,
                GwpText.Create("{=gwp_terms_final_lead}Your command stays with you. I need the account settled, not another battle."),
                GwpText.Create("{=gwp_terms_final_charm}We both leave with our people alive. That is worth more than prolonging this quarrel."),
                GwpText.Create("{=gwp_terms_final_rogue}You remove the reason I am standing in your path. Do you really want to wager more?"),
                GwpText.Create("{=gwp_terms_final_trade}An exact payment leaves no remainder to argue over when I deliver the account."));
            return new List<PersuasionTask> { first, second };
        }

        private static string[] PaymentArguments(GwpOffenderDesire desire) => desire switch
        {
            GwpOffenderDesire.RequestGrace => new[] {
                "The Wardens expect payment, not another delay. Show me what you have now.",
                "Let us settle what you can today. There is no need to leave this hanging over your people.",
                "Three days to disappear? I would rather see the money you carry now.",
                "Waiting costs both our parties. Pay what you can now and save another journey." },
            GwpOffenderDesire.SurrenderSelf => new[] {
                "Your men also need their commander. Paying keeps you at their head.",
                "You need not leave your people behind. Pay, and stay with them.",
                "Do you trust whoever commands in your absence? Silver is easier to replace.",
                "Captivity will cost your household and command as well. Settle in silver today." },
            GwpOffenderDesire.BribeTheWarden => new[] {
                "I carry a warrant, not a begging bowl. Pay what it names.",
                "Do not put this between us. Settle openly and we can both ride away.",
                "You want me to risk my name for your loose change. Keep the bribe and bring the fine.",
                "A private purse does not settle a public debt. The full account still needs paying." },
            GwpOffenderDesire.CunningHalf => new[] {
                "Then open the other baggage. I will not accept your word for half an account.",
                "Show me enough trust to stop bargaining with a story about your purse.",
                "Half conveniently missing? I have heard that trick before. Find the rest.",
                "Half the payment leaves half the account. Your story does not change the sum." },
            GwpOffenderDesire.RashMost => new[] {
                "If you want this finished, finish the payment. Do not leave the last fifth hanging.",
                "We are close enough to agreement. Do not throw it away over the remainder.",
                "You risk the whole purse to keep its last fifth. Think before you refuse.",
                "You have offered four fifths. Add the remaining fifth and there is no balance left." },
            GwpOffenderDesire.DemandDuel => new[] {
                "This warrant is not a contest. Meet the charge without risking your commander's life.",
                "You need not prove your courage to me. Spare both our people another loss.",
                "A duel lets you gamble away somebody else's debt. I will not take that wager.",
                "Even victory costs wounds and time. The fine is a known price; a duel is not." },
            GwpOffenderDesire.DeedsOnly => new[] {
                "Killing your own soldiers will not discharge your responsibility. Pay the full fine.",
                "There have been enough deaths. Keep your men alive and settle this in silver.",
                "Cutting down witnesses does not make your account smaller. Do not pretend it does.",
                "Two fifths will not cover this account. Your men's lives are not a substitute for the rest." },
            GwpOffenderDesire.HaggleDown => new[] {
                "This is an assessed fine. I have no order to discount it by three tenths.",
                "Let us end this without another quarrel. Pay the remainder as well.",
                "You are bargaining as though I must sell. I can still enforce the warrant.",
                "Seven tenths leaves three tenths unpaid. Close the balance now." },
            _ => new[] { "Pay the sum on the warrant.", "Let us settle this peacefully.", "Do not leave a debt behind.", "Settle the whole account." }
        };

        internal static TextObject ArgumentRebuff(PersuasionOptionArgs option, GwpOffenderDesire desire, bool payment)
        {
            string skill = option.SkillUsed == DefaultSkills.Leadership ? "lead"
                : option.SkillUsed == DefaultSkills.Charm ? "charm"
                : option.SkillUsed == DefaultSkills.Roguery ? "rogue" : "trade";
            string topic = payment ? desire.ToString().ToLowerInvariant() : "authority";
            return GwpText.Create("{=gwp_rebuff_" + topic + "_" + skill + "}"
                + (payment ? PaymentRebuff(desire, skill) : skill switch {
                    "lead" => "Your warrant is not a command I have sworn to obey. Give me a better reason.",
                    "charm" => "I hear your concern for them. That does not settle whether I answer to you.",
                    "rogue" => "Threats are easy to make. I have not agreed to your authority.",
                    _ => "You keep counting the fine before I have accepted the charge." }));
        }

        private static string PaymentRebuff(GwpOffenderDesire desire, string skill) => desire switch
        {
            GwpOffenderDesire.RequestGrace => "I asked for time to raise it. Repeating the amount will not fill my purse today.",
            GwpOffenderDesire.SurrenderSelf => "My answer is still to go with you. That money stays with my men.",
            GwpOffenderDesire.BribeTheWarden => "You can put a brave face on it. I am offering you a private payment, not the full fine.",
            GwpOffenderDesire.CunningHalf => "You suspect another purse, but suspicion does not put more money on this table. Half is my offer.",
            GwpOffenderDesire.RashMost => "I offered most of it to end this quickly. Now you want to keep arguing over the rest.",
            GwpOffenderDesire.DemandDuel => "All those reasons to avoid a duel. I still want this settled between the two of us.",
            GwpOffenderDesire.DeedsOnly => "Their punishment is mine to decide. I offered you two fifths, no more.",
            GwpOffenderDesire.HaggleDown => "You call it an exact account; I call it too much. My offer is still seven tenths.",
            _ => "That does not change the terms I offered." };

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
            GwpText.Create("{=gwp_fa_task_authority}You are not my liege and you are not a Warden. You are a man they hired."),
            s,
            GwpText.Create("{=gwp_fa_arg_authority_lead}The Wardens sent me to deal with this. I am conveying their demand."),
            GwpText.Create("{=gwp_fa_arg_authority_charm}Hired or sworn, the dead are still dead and someone had to come."),
            GwpText.Create("{=gwp_fa_arg_authority_rogue}They hired me because they were tired of asking politely."),
            GwpText.Create("{=gwp_fa_arg_authority_trade}Whether you accept me or not, the Wardens will still demand payment. Settling now saves you trouble."));

        private static PersuasionTask AuthorityUnderCharge(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_under_charge}Your reputation is scarcely better than mine. Now you lecture me for the Wardens?"),
            s,
            GwpText.Create("{=gwp_fa_arg_under_lead}My conduct is a separate matter. You must answer to the Wardens for this."),
            GwpText.Create("{=gwp_fa_arg_under_charm}Then let us avoid another bad outcome. Settle this today so everyone can go home."),
            GwpText.Create("{=gwp_fa_arg_under_rogue}That gives me all the more reason not to return empty-handed. Think carefully."),
            GwpText.Create("{=gwp_fa_arg_under_trade}Talking about me saves you not one denar. It does not reduce what you owe."));

        private static PersuasionTask Price(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_price}And that number - where did it come from? You say it as if it fell from the sky."),
            s,
            GwpText.Create("{=gwp_fa_arg_price_lead}This is the fine the Wardens demand. I was sent to enforce it, not invent a price."),
            GwpText.Create("{=gwp_fa_arg_price_charm}The victims have already borne the loss. Do not add another fight to it."),
            GwpText.Create("{=gwp_fa_arg_price_rogue}It is the cheap answer. You are looking at the expensive one."),
            GwpText.Create("{=gwp_fa_arg_price_trade}The damage and the people harmed both need answering for. This sum covers more than this one encounter."));

        private static PersuasionTask Tally(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_tally}You are charging me for things I did months apart, as if they were one crime."),
            s,
            GwpText.Create("{=gwp_fa_arg_tally_lead}Time passing does not remove your responsibility. The Wardens hold you answerable for all of it."),
            GwpText.Create("{=gwp_fa_arg_tally_charm}Each one had people in it. They were months apart for you, not for them."),
            GwpText.Create("{=gwp_fa_arg_tally_rogue}If you wanted them charged separately you should have been caught sooner."),
            GwpText.Create("{=gwp_fa_arg_tally_trade}Settle these offences together today and spare your party repeated interruptions over them."));

        private static PersuasionTask Denial(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_denial}My men did that. I did not give the order, and I will not pay for their appetite."),
            s,
            GwpText.Create("{=gwp_fa_arg_denial_lead}They are your men. That is what the word means."),
            GwpText.Create("{=gwp_fa_arg_denial_charm}Nobody in that village could tell your hand from theirs."),
            GwpText.Create("{=gwp_fa_arg_denial_rogue}They acted under your banner. Blame your men, and the Wardens will still come for you."),
            GwpText.Create("{=gwp_fa_arg_denial_trade}They are your men. Pay for the harm first; how you recover it from them is your affair."));

        private static PersuasionTask WarExcuse(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_war}There is a war on. Taking an enemy's goods is not a crime, it is the work."),
            s,
            GwpText.Create("{=gwp_fa_arg_war_lead}Your war does not reach the drivers. The Wardens' law does."),
            GwpText.Create("{=gwp_fa_arg_war_charm}Carters and guards. Not one of them was holding your line."),
            GwpText.Create("{=gwp_fa_arg_war_rogue}Call it war if it helps. The Wardens call it a caravan and a body count."),
            GwpText.Create("{=gwp_fa_arg_war_trade}Wars end. Accounts do not, until someone pays them."));

        private static PersuasionTask Consequence(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_consequence}And if I simply refuse to pay? What can you do about it?"),
            s,
            GwpText.Create("{=gwp_fa_arg_consequence_lead}My departure would not make the Wardens drop this. We need an answer today."),
            GwpText.Create("{=gwp_fa_arg_consequence_charm}Then the next grey rider is not here to talk, and neither of us wants that."),
            GwpText.Create("{=gwp_fa_arg_consequence_rogue}You can wager that I cannot take you. You cannot choose how many Wardens come next time."),
            GwpText.Create("{=gwp_fa_arg_consequence_trade}The debt remains until it is settled. Waiting does not make it disappear."));

        private static PersuasionTask Record(PersuasionArgumentStrength s) => Make(
            GwpText.Create("{=gwp_fa_task_record}Your order has had me once already. Is this how it goes now - forever?"),
            s,
            GwpText.Create("{=gwp_fa_arg_record_lead}It goes until you stop giving them cause. That part is yours."),
            GwpText.Create("{=gwp_fa_arg_record_charm}They took you once and let you ride. Few orders would have."),
            GwpText.Create("{=gwp_fa_arg_record_rogue}You know how an arrest ends. Do you want to go through that again?"),
            GwpText.Create("{=gwp_fa_arg_record_trade}Pay what is assessed now. Any future offence will have its own reckoning."));

        #endregion
    }
}
