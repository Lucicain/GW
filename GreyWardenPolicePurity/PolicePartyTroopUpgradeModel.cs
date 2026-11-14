using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Native AI troop upgrading gives one branch a weight of 9999 and every
    /// other branch a weight of 1, based on the leader's preferred formation or
    /// a stable per-leader hash. That is useful for native lords whose rosters
    /// draw from several troop trees, but it turns the Grey Wardens' single
    /// three-way regular tree into an almost permanent one-branch pipeline.
    ///
    /// Keep native experience and equipment requirements. The party screen
    /// settles the player’s Warden upgrades in reputation on confirmation.
    /// </summary>
    public sealed class PolicePartyTroopUpgradeModel :
        DefaultPartyTroopUpgradeModel
    {
        public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party,
            CharacterObject characterObject, CharacterObject upgradeTarget) =>
            GwpPlayerWardenTroops.IsPlayerWardenTroop(party, characterObject)
                ? new ExplainedNumber(0f)
                : base.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);

        public override bool IsTroopUpgradeable(PartyBase party,
            CharacterObject character)
        {
            if (!GwpFireswornUpgradePayment.SelectTroop(party, character))
                return false;

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
            // 随行练兵队只替玩家订单练兵：通往订单兵种的分支独占。
            if (upgradeTargetIndex >= 0 &&
                upgradeTargetIndex < troop.UpgradeTargets.Length &&
                GreyWardenTroopRequestBehavior.CohortBranchServesOrder(party,
                    troop, troop.UpgradeTargets[upgradeTargetIndex]) is bool serves)
                return serves ? 1f : 0f;

            // 领主已经被兵种配比指定了取向时，必须把判定交还原版——原版正是靠
            // PreferredUpgradeFormation 把命中的那条分支抬到 9999。在这里抢答 1f
            // 会把取向整个吞掉，配比就成了空转。
            if (GwpCommon.IsGreyWardenClanParty(party?.MobileParty) &&
                GwpCommon.IsGreyWardenTroop(troop) &&
                troop.UpgradeTargets.Length > 1 &&
                upgradeTargetIndex >= 0 &&
                upgradeTargetIndex < troop.UpgradeTargets.Length &&
                !HasSteeredPreference(party))
            {
                // 没有取向可依据时（无领主队按 party.Id 哈希被原版永久锁死在一条
                // 分支）才拉平三条分支，这正是本模型当初要解决的问题。
                return 1f;
            }

            return base.GetUpgradeChanceForTroopUpgrade(
                party, troop, upgradeTargetIndex);
        }

        private static bool HasSteeredPreference(PartyBase? party)
        {
            Hero? leader = party?.MobileParty?.LeaderHero;
            return leader != null &&
                   leader.PreferredUpgradeFormation !=
                       FormationClass.NumberOfAllFormations;
        }
    }

    [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "GetPossibleUpgradeTargets")]
    internal static class GwpTroopOrderUpgradeReservePatch
    {
        private static void Prefix(PartyBase party, ref TroopRosterElement element)
        {
            // The native method uses this copy to cap how many healthy troops it can upgrade.
            int healthy = element.Number - element.WoundedNumber;
            int available = GreyWardenTroopRequestBehavior.AvailableForOrderUpgrade(party, element.Character, healthy);
            element.Number -= healthy - available;
        }
    }
}
