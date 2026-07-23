using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// A paid Grey Warden petition reopens the native fief election without
    /// temporarily making the settlement ownerless.  The player clan is placed
    /// beside the two strongest native claimants; every vote and the final owner
    /// change are still handled by Bannerlord's normal kingdom decision code.
    /// </summary>
    public sealed class GwpSettlementReconsiderationDecision :
        SettlementClaimantDecision
    {
        [SaveableField(500)]
        private readonly int _publicSupportPercent;

        public int PublicSupportPercent => _publicSupportPercent;

        public GwpSettlementReconsiderationDecision(Clan proposerClan,
            TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
            int publicSupportPercent)
            : base(proposerClan, settlement, Hero.MainHero, null)
        {
            _publicSupportPercent = Math.Max(0,
                Math.Min(50, publicSupportPercent));
        }

        public override bool IsAllowed()
        {
            return Settlement?.MapFaction == Kingdom &&
                   Clan.PlayerClan?.Kingdom == Kingdom &&
                   !Clan.PlayerClan.IsUnderMercenaryService;
        }

        public override TextObject GetGeneralTitle()
        {
            var text = new TextObject(
                "{=gwp_fief_reconsideration_title}Petition to reconsider {SETTLEMENT_NAME}");
            text.SetTextVariable("SETTLEMENT_NAME", Settlement.Name);
            return text;
        }

        public override TextObject GetSupportTitle() => GetGeneralTitle();

        public override TextObject GetSupportDescription()
        {
            var text = new TextObject(
                "{=gwp_fief_reconsideration_support}The Grey Wardens have presented a popular petition. The council will reconsider who should hold {SETTLEMENT_NAME}.");
            text.SetTextVariable("SETTLEMENT_NAME", Settlement.Name);
            return text;
        }

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            List<ClanAsDecisionOutcome> native = base.DetermineInitialCandidates()
                .OfType<ClanAsDecisionOutcome>()
                .ToList();
            ClanAsDecisionOutcome? player = native.FirstOrDefault(candidate =>
                candidate.Clan == Clan.PlayerClan);
            if (player == null)
                player = new ClanAsDecisionOutcome(Clan.PlayerClan);

            IEnumerable<ClanAsDecisionOutcome> others = native
                .Where(candidate => candidate.Clan != Clan.PlayerClan)
                .OrderByDescending(candidate =>
                    base.CalculateMeritOfOutcome(candidate))
                .ThenBy(candidate => candidate.Clan.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .Take(2);

            return new DecisionOutcome[] { player }.Concat(others);
        }

    }
}
