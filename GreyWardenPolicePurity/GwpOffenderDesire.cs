using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// How he intends to discharge the penalty, once he has accepted that he owes one.
    /// This is the second layer only: whether he answers the warrant at all was decided
    /// before this, by his temper, the odds and the player's standing.
    ///
    /// Each end of each personality trait wants one thing, and every one of those things
    /// lands on machinery the mod already has - a payment, a short payment, the stock
    /// haggling table, the sparring duel, or the prisoner path. None of them is an escape;
    /// he is not riding away from this either way.
    /// </summary>
    internal enum GwpOffenderDesire
    {
        /// <summary>勇武低、仁慈高、慷慨高：照数全缴，不作他想。</summary>
        PayInFull,
        PayToAvoidBattle,
        PayForPeace,
        PayGenerously,
        /// <summary>荣誉高：钱留给部下，人跟你走。</summary>
        SurrenderSelf,
        /// <summary>荣誉低：塞给你一点私钱，把案子抹了。</summary>
        BribeTheWarden,
        /// <summary>算计高：骗你一道，只给五成。</summary>
        CunningHalf,
        /// <summary>算计低：懒得算计，给八成了事。</summary>
        RashMost,
        /// <summary>勇武高：单挑定输赢，他赢免罚，他输全缴。</summary>
        DemandDuel,
        /// <summary>仁慈低：只认自己做下的事，死了几个人不关他事；顺手拿手下泄愤。</summary>
        DeedsOnly,
        /// <summary>慷慨低：上议价桌，最后只肯给七成。</summary>
        HaggleDown
    }

    internal static class GwpOffenderDesires
    {
        private readonly struct Candidate
        {
            internal Candidate(GwpOffenderDesire desire, int weight)
            {
                Desire = desire;
                Weight = weight;
            }

            internal GwpOffenderDesire Desire { get; }
            internal int Weight { get; }
        }

        /// <summary>
        /// Ten trait extremes, eight ways to settle: paying in full is where the timid,
        /// the merciful and the open-handed all arrive by different roads. The draw is
        /// weighted by how strong the trait is, with a fixed weight for plain payment
        /// beside them, so no lord is reduced to a single trick.
        /// </summary>
        internal static GwpOffenderDesire Roll(Hero offender, int fine)
        {
            var pool = new List<Candidate>
            {
                new Candidate(GwpOffenderDesire.PayInFull, GwpTuning.FieldArrest.PlainDemandWeight)
            };

            int honor = offender.GetTraitLevel(DefaultTraits.Honor);
            int calculating = offender.GetTraitLevel(DefaultTraits.Calculating);
            int valor = offender.GetTraitLevel(DefaultTraits.Valor);
            int mercy = offender.GetTraitLevel(DefaultTraits.Mercy);
            int generosity = offender.GetTraitLevel(DefaultTraits.Generosity);

            Add(pool, GwpOffenderDesire.SurrenderSelf, honor);
            Add(pool, GwpOffenderDesire.BribeTheWarden, -honor);
            Add(pool, GwpOffenderDesire.CunningHalf, calculating);
            Add(pool, GwpOffenderDesire.RashMost, -calculating);
            Add(pool, GwpOffenderDesire.DemandDuel, valor);
            Add(pool, GwpOffenderDesire.PayToAvoidBattle, -valor);
            Add(pool, GwpOffenderDesire.PayForPeace, mercy);
            Add(pool, GwpOffenderDesire.DeedsOnly, -mercy);
            Add(pool, GwpOffenderDesire.PayGenerously, generosity);
            Add(pool, GwpOffenderDesire.HaggleDown, -generosity);

            // A neutral personality can still bargain without becoming corrupt or cruel.
            if (honor == 0 && calculating == 0 && valor == 0 && mercy == 0 && generosity == 0)
                pool.Add(new Candidate(GwpOffenderDesire.HaggleDown, 1));

            // 掏不出全额的人提不起"少给一点"这种方案——他本来就给不出全额，
            // 那种情形走的是既有的短款分支。
            if (offender.Gold < fine || fine <= 0)
                pool.RemoveAll(entry =>
                    entry.Desire == GwpOffenderDesire.CunningHalf
                    || entry.Desire == GwpOffenderDesire.RashMost
                    || entry.Desire == GwpOffenderDesire.DeedsOnly
                    || entry.Desire == GwpOffenderDesire.HaggleDown
                    || entry.Desire == GwpOffenderDesire.BribeTheWarden);

            int total = pool.Sum(entry => entry.Weight);
            if (total <= 0) return GwpOffenderDesire.PayInFull;

            int roll = MBRandom.RandomInt(total);
            foreach (Candidate candidate in pool)
            {
                roll -= candidate.Weight;
                if (roll < 0)
                {
                    return candidate.Desire;
                }
            }

            return GwpOffenderDesire.PayInFull;
        }

        internal static bool IsFullPayment(GwpOffenderDesire desire) => desire == GwpOffenderDesire.PayInFull
            || desire == GwpOffenderDesire.PayToAvoidBattle || desire == GwpOffenderDesire.PayForPeace
            || desire == GwpOffenderDesire.PayGenerously;

        private static void Add(List<Candidate> pool, GwpOffenderDesire desire, int traitLevel)
        {
            if (traitLevel <= 0) return;
            pool.Add(new Candidate(desire, traitLevel * GwpTuning.FieldArrest.DesireWeightPerTraitPoint));
        }

        /// <summary>他打算交多少。全缴以外的方案都是打折，比例集中在调参里。</summary>
        internal static int OfferedSharePercent(GwpOffenderDesire desire) =>
            desire switch
            {
                GwpOffenderDesire.BribeTheWarden => GwpTuning.FieldArrest.BribeSharePercent,
                GwpOffenderDesire.CunningHalf => GwpTuning.FieldArrest.CunningSharePercent,
                GwpOffenderDesire.RashMost => GwpTuning.FieldArrest.RashSharePercent,
                GwpOffenderDesire.DeedsOnly => GwpTuning.FieldArrest.DeedsOnlySharePercent,
                GwpOffenderDesire.HaggleDown => GwpTuning.FieldArrest.HaggleSharePercent,
                _ => 100
            };

        /// <summary>
        /// 他咬住自己这套方案的力度。特质越极端越难被拉回正规全缴；这是第二层
        /// 谈判的难度来源。
        /// </summary>
        internal static int Insistence(Hero offender, GwpOffenderDesire desire)
        {
            int level = desire switch
            {
                GwpOffenderDesire.SurrenderSelf => offender.GetTraitLevel(DefaultTraits.Honor),
                GwpOffenderDesire.BribeTheWarden => -offender.GetTraitLevel(DefaultTraits.Honor),
                GwpOffenderDesire.CunningHalf => offender.GetTraitLevel(DefaultTraits.Calculating),
                GwpOffenderDesire.RashMost => -offender.GetTraitLevel(DefaultTraits.Calculating),
                GwpOffenderDesire.DemandDuel => offender.GetTraitLevel(DefaultTraits.Valor),
                GwpOffenderDesire.DeedsOnly => -offender.GetTraitLevel(DefaultTraits.Mercy),
                GwpOffenderDesire.HaggleDown => -offender.GetTraitLevel(DefaultTraits.Generosity),
                _ => 0
            };

            return Math.Max(0, level);
        }

        /// <summary>他把自己的打算说出口。</summary>
        internal static TextObject Line(GwpOffenderDesire desire) =>
            desire switch
            {
                GwpOffenderDesire.PayToAvoidBattle => GwpText.Create("{=gwp_fa_desire_cautious}Keep your weapons lowered. I will pay what you ask; there need be no battle."),
                GwpOffenderDesire.PayForPeace => GwpText.Create("{=gwp_fa_desire_merciful}Enough people have suffered. Take the full fine, and let there be no more bloodshed."),
                GwpOffenderDesire.PayGenerously => GwpText.Create("{=gwp_fa_desire_generous}I can spare the silver. Take the full amount and see that this account is settled."),
                GwpOffenderDesire.SurrenderSelf =>
                    GwpText.Create("{=gwp_fa_desire_surrender}That silver is my men's wages. Take me instead - I ride with you, and they go home."),
                GwpOffenderDesire.BribeTheWarden =>
                    GwpText.Create("{=gwp_fa_desire_bribe}Fifteen percent, for you alone. Let the Wardens hear whatever story you choose."),
                GwpOffenderDesire.CunningHalf =>
                    GwpText.Create("{=gwp_fa_desire_cunning}Half. That is what the baggage carries, and that is what you are getting."),
                GwpOffenderDesire.RashMost =>
                    GwpText.Create("{=gwp_fa_desire_rash}Take four fifths of the fine and let us finish this."),
                GwpOffenderDesire.DemandDuel =>
                    GwpText.Create("{=gwp_fa_desire_duel}Fight me alone. If you win, I pay the fine; if I win, you collect nothing."),
                GwpOffenderDesire.DeedsOnly =>
                    GwpText.Create("{=gwp_fa_desire_deeds}I will pay two fifths. The men I blame will answer to me with their lives."),
                GwpOffenderDesire.HaggleDown =>
                    GwpText.Create("{=gwp_fa_desire_haggle}I will give you seven tenths of the fine. That is my offer."),
                _ =>
                    GwpText.Create("{=gwp_fa_desire_payfull}Very well. The full sum, and we are finished here.")
            };

        /// <summary>玩家决定照他说的办时，玩家那句话。</summary>
        internal static TextObject PlayerAcceptLine(GwpOffenderDesire desire) =>
            desire switch
            {
                GwpOffenderDesire.SurrenderSelf =>
                    GwpText.Create("{=gwp_fa_accept_surrender}Then ride with me. Your men can find their own way."),
                GwpOffenderDesire.BribeTheWarden =>
                    GwpText.Create("{=gwp_fa_accept_bribe}Hand it over. This conversation did not happen."),
                GwpOffenderDesire.CunningHalf =>
                    GwpText.Create("{=gwp_fa_accept_cunning}Half, then. The rest stays against your name."),
                GwpOffenderDesire.RashMost =>
                    GwpText.Create("{=gwp_fa_accept_rash}Most of it will do. The remainder stays on the account."),
                GwpOffenderDesire.DemandDuel =>
                    GwpText.Create("{=gwp_fa_accept_duel}Draw, then. Let us settle it between us."),
                GwpOffenderDesire.DeedsOnly =>
                    GwpText.Create("{=gwp_fa_accept_deeds}Pay for the deed, then. The dead stay on your record."),
                GwpOffenderDesire.HaggleDown =>
                    GwpText.Create("{=gwp_fa_accept_haggle}Seven tenths, then. I will have to account for the shortfall."),
                _ =>
                    GwpText.Create("{=gwp_fa_accept_payfull}The full sum. Hand it over.")
            };
    }
}
