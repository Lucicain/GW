using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Native AI troop upgrading gives one branch a weight of 9999 and every
    /// other branch a weight of 1, based on the leader's preferred formation or
    /// a stable per-leader hash. That is useful for native lords whose rosters
    /// draw from several troop trees, but it turns the Grey Wardens' single
    /// three-way regular tree into an almost permanent one-branch pipeline.
    ///
    /// Keep the native upgrader, costs, requirements, timing, and batch size.
    /// Only make the available branches of Grey Warden regulars equally likely
    /// when a Grey Warden AI party performs the native weighted selection.
    /// </summary>
    public sealed class PolicePartyTroopUpgradeModel :
        DefaultPartyTroopUpgradeModel
    {
        public override bool IsTroopUpgradeable(PartyBase party,
            CharacterObject character)
        {
            if (GreyWardenTroopRequestBehavior
                .IsOrderedTroopUpgradeLocked(party, character))
                return false;

            return base.IsTroopUpgradeable(party, character);
        }

        public override float GetUpgradeChanceForTroopUpgrade(
            PartyBase party,
            CharacterObject troop,
            int upgradeTargetIndex)
        {
            if (IsGreyWardenParty(party) &&
                GwpCommon.IsGreyWardenTroop(troop) &&
                troop.UpgradeTargets.Length > 1 &&
                upgradeTargetIndex >= 0 &&
                upgradeTargetIndex < troop.UpgradeTargets.Length)
            {
                return 1f;
            }

            return base.GetUpgradeChanceForTroopUpgrade(
                party, troop, upgradeTargetIndex);
        }

        private static bool IsGreyWardenParty(PartyBase? party)
        {
            return string.Equals(
                party?.MobileParty?.ActualClan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
