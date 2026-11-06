using GreyWardenPolicePurity.Core.Negotiation;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// How he intends to discharge the penalty, once he has accepted that he owes one.
    /// This is the second layer only: whether he answers the warrant at all was decided
    /// before this, by his temper, the odds and the player's standing.
    ///
    /// Personality and available money weight a shared set of proposals; each proposal
    /// lands on machinery the mod already has - a payment, a short payment, the stock
    /// haggling table, the sparring duel, or the prisoner path. None of them is an escape;
    /// he is not riding away from this either way.
    /// </summary>
    /// <remarks>使者收尾把它按整数存档（EnvoyCase.PendingDesire），成员的数值不能变。</remarks>
    internal enum GwpOffenderDesire
    {
        /// <summary>勇武低、仁慈高：照数全缴，不作他想。</summary>
        PayInFull = 0,
        PayToAvoidBattle = 1,
        PayForPeace = 2,
        /// <summary>慷慨高：按罚金的 120% 交。</summary>
        PayGenerously = 3,
        /// <summary>荣誉高：钱留给部下，人跟你走。</summary>
        SurrenderSelf = 4,
        /// <summary>荣誉低：塞给你一点私钱，把案子抹了。</summary>
        BribeTheWarden = 5,
        /// <summary>算计高：骗你一道，只给五成。</summary>
        CunningHalf = 6,
        /// <summary>算计低（莽撞）：随口报个数，罚金的 50%～150%。</summary>
        RashMost = 7,
        /// <summary>勇武高：单挑定输赢，他赢免罚，他输全缴。</summary>
        DemandDuel = 8,
        /// <summary>仁慈低：只认自己做下的事，死了几个人不关他事；顺手拿手下泄愤。</summary>
        DeedsOnly = 9,
        /// <summary>慷慨低：上议价桌，最后只肯给七成。</summary>
        HaggleDown = 10,
        RequestGrace = 11
    }

    internal static class GwpOffenderDesires
    {
        // One weighted pool of distinct tools; trait extremes do not own fixed outcomes.
        // sharePercent 是他这次肯交罚金的几成，莽撞的比例在这里一并抽定，整场谈判沿用。
        internal static GwpOffenderDesire Roll(Hero offender, int fine, bool canGrace, bool hasTroops,
            out int sharePercent)
        {
            int[] weights = GwpNegotiationPolicy.Weights(
                offender.GetTraitLevel(DefaultTraits.Honor), offender.GetTraitLevel(DefaultTraits.Mercy),
                offender.GetTraitLevel(DefaultTraits.Valor), offender.GetTraitLevel(DefaultTraits.Calculating),
                offender.GetTraitLevel(DefaultTraits.Generosity), offender.PartyBelongedTo == null ? offender.Gold : GwpAssetPayment.Wealth(offender, offender.PartyBelongedTo.Party), fine, hasTroops, canGrace);
            var choices = new[] { GwpOffenderDesire.PayInFull, GwpOffenderDesire.HaggleDown,
                GwpOffenderDesire.CunningHalf, GwpOffenderDesire.SurrenderSelf, GwpOffenderDesire.DeedsOnly,
                GwpOffenderDesire.BribeTheWarden, GwpOffenderDesire.DemandDuel, GwpOffenderDesire.RequestGrace,
                GwpOffenderDesire.PayGenerously, GwpOffenderDesire.RashMost };
            int roll = MBRandom.RandomInt(weights.Sum());
            GwpOffenderDesire result = choices[GwpNegotiationPolicy.SelectDesireIndex(weights, roll)];
            int shareRoll = result == GwpOffenderDesire.RashMost
                ? MBRandom.RandomInt(GwpNegotiationPolicy.RashShareRollSpan) : -1;
            sharePercent = shareRoll >= 0 ? GwpNegotiationPolicy.RashSharePercent(shareRoll) : FixedSharePercent(result);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("TOOLBOX_CHOICE", "offender=" + offender.StringId
                + "; gold=" + offender.Gold + "; fine=" + fine + "; weights=" + string.Join(",", weights)
                + "; roll=" + roll + "; result=" + result + "; shareRoll=" + shareRoll + "; share=" + sharePercent);
#endif
            return result;
        }

        /// <summary>仁慈低的附带动作：他不认那些死掉的村民，转手杀几个自己的兵出气。返回杀了几个。</summary>
        internal static int SacrificeOwnMen(MobileParty? party)
        {
            if (party?.MemberRoster == null) return 0;
            int toKill = GwpTuning.FieldArrest.DeedsOnlyTroopsKilled;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
            {
                if (toKill <= 0) break;
                if (element.Character?.IsHero != false) continue;
                int take = Math.Min(toKill, element.Number);
                party.MemberRoster.AddToCounts(element.Character, -take);
                toKill -= take;
            }
            return GwpTuning.FieldArrest.DeedsOnlyTroopsKilled - toKill;
        }

        /// <summary>他拿钱了结、而且给的不少于罚金：不用讨价还价，直接数钱。</summary>
        internal static bool IsFullPayment(GwpOffenderDesire desire, int sharePercent) => sharePercent >= 100
            && (desire == GwpOffenderDesire.PayInFull || desire == GwpOffenderDesire.PayGenerously
                || desire == GwpOffenderDesire.RashMost);

        /// <summary>固定比例的方案肯交几成；莽撞的比例随抽签另定。不拿钱了结的方案记 100，不进讨价还价。</summary>
        private static int FixedSharePercent(GwpOffenderDesire desire) =>
            desire switch
            {
                GwpOffenderDesire.BribeTheWarden => GwpTuning.FieldArrest.BribeSharePercent,
                GwpOffenderDesire.CunningHalf => GwpTuning.FieldArrest.CunningSharePercent,
                GwpOffenderDesire.DeedsOnly => GwpTuning.FieldArrest.DeedsOnlySharePercent,
                GwpOffenderDesire.HaggleDown => GwpTuning.FieldArrest.HaggleSharePercent,
                GwpOffenderDesire.PayGenerously => GwpNegotiationPolicy.GenerousSharePercent,
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
                GwpOffenderDesire.RequestGrace => Math.Max(1, offender.GetTraitLevel(DefaultTraits.Calculating)),
                GwpOffenderDesire.SurrenderSelf => offender.GetTraitLevel(DefaultTraits.Honor),
                GwpOffenderDesire.BribeTheWarden => -offender.GetTraitLevel(DefaultTraits.Honor),
                GwpOffenderDesire.CunningHalf => offender.GetTraitLevel(DefaultTraits.Calculating),
                // 莽撞的人报了数就不想再谈。
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
                GwpOffenderDesire.RequestGrace => GwpText.Create(GwpTextCatalog.gwp_fa_desire_grace),
                GwpOffenderDesire.PayToAvoidBattle => GwpText.Create(GwpTextCatalog.gwp_fa_desire_cautious),
                GwpOffenderDesire.PayForPeace => GwpText.Create(GwpTextCatalog.gwp_fa_desire_merciful),
                GwpOffenderDesire.PayGenerously => GwpText.Create(GwpTextCatalog.gwp_fa_desire_generous),
                GwpOffenderDesire.SurrenderSelf =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_surrender),
                GwpOffenderDesire.BribeTheWarden =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_bribe),
                GwpOffenderDesire.CunningHalf =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_cunning),
                GwpOffenderDesire.RashMost =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_rash),
                GwpOffenderDesire.DemandDuel =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_duel),
                GwpOffenderDesire.DeedsOnly =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_deeds),
                GwpOffenderDesire.HaggleDown =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_haggle),
                _ =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_desire_payfull)
            };

        /// <summary>玩家决定照他说的办时，玩家那句话。</summary>
        internal static TextObject PlayerAcceptLine(GwpOffenderDesire desire) =>
            desire switch
            {
                GwpOffenderDesire.RequestGrace => GwpText.Create(GwpTextCatalog.gwp_fa_accept_grace),
                GwpOffenderDesire.SurrenderSelf =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_surrender),
                GwpOffenderDesire.BribeTheWarden =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_bribe),
                GwpOffenderDesire.CunningHalf =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_cunning),
                GwpOffenderDesire.RashMost =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_rash),
                GwpOffenderDesire.DemandDuel =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_duel),
                GwpOffenderDesire.DeedsOnly =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_deeds),
                GwpOffenderDesire.HaggleDown =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_haggle),
                _ =>
                    GwpText.Create(GwpTextCatalog.gwp_fa_accept_payfull)
            };
    }
}
