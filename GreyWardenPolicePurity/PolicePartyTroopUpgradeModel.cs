using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
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
            // 领主已经被兵种配比指定了取向时，必须把判定交还原版——原版正是靠
            // PreferredUpgradeFormation 把命中的那条分支抬到 9999。在这里抢答 1f
            // 会把取向整个吞掉，配比就成了空转。
            if (IsGreyWardenParty(party) &&
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

        private static bool IsGreyWardenParty(PartyBase? party)
        {
            return string.Equals(
                party?.MobileParty?.ActualClan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
