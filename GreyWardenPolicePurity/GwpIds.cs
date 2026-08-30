using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyWardenPolicePurity
{
    internal static class GwpIds
    {
        public const string PoliceClanId = "gw";
        public const string PatrolIdPrefix = "gwp_patrol_";
        public const string EnforcementDelayPatrolIdPrefix = "gwp_enf_delay_";
        public const string RecruitmentPatrolPrefix = "gwp_recruit_";
        public const string BountyCollectionCourierPrefix = "gwp_bounty_collect_";

        public const string HeavyInfantryId = "gwheavyinfantry";
        public const string ArcherId = "gwarcher";
        public const string KnightId = "gwknight";
        public const string PoliceRecruitId = "gwrecruit";
        public const string NewRecruitId = "gwnewrecruit";
        public const string LeaderCharacterIdPrefix = "gw_leader_";
        public const string CommanderTemplateCharacterId = "gw_leader_0";
        // Keep the native commander_2 entry intact; the Grey Warden commander
        // is an additional Custom Battle character inserted at slot one.
        public const string CustomBattleCommanderId = "gwp_custom_commander";
        public const string LargeShieldItemId = "wlarge_shield";
        public const string BlackLargeShieldItemId = "wlarge_shield_black";
        public const string GrainItemId = "grain";
        public const string DualBladeOffhandItemId = "gwdualbladeoffhand";
        public const string DualBladeMainhandItemId = "gwdualblademainhand";
        public const string DualBladeOffhandCraftingTemplateId =
            "GwpOneHandedSwordDualOffhand";
        public const string DualBladeMainhandCraftingTemplateId =
            "GwpOneHandedSwordDualMainhand";

        public const string BountyQuestPrefix = "gwp_bounty_quest_";
        public const string BountyQuestFallbackId = "gwp_bounty_quest_0";
        public const string BountySpecialQuestType = "GwpBountyHunterQuest";
        public const string AtonementQuestPrefix = "gwp_atonement_quest_";
        public const string AtonementQuestFallbackId = "gwp_atonement_quest_0";
        public const string AtonementSpecialQuestType = "GwpPlayerAtonementQuest";

        public static readonly IReadOnlyCollection<string> CommanderSetItemIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "wcomlegs",
                "wcomgloves",
                "wcomarmorhv",
                "wcomshoulder",
                "wcomhelmethv",
                "wharnesscom",
                BlackLargeShieldItemId
            };

        /// <summary>
        /// 加入或重新加入灰袍时发给玩家的物品。双刀现在专属于
        /// gwarcher，不再作为玩家入会装备发放。
        /// </summary>
        public static readonly IReadOnlyCollection<string> MembershipGrantItemIds =
            new HashSet<string>(CommanderSetItemIds, StringComparer.OrdinalIgnoreCase);

        public static readonly IReadOnlyCollection<string> DualBladeCraftingTemplateIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DualBladeOffhandCraftingTemplateId,
                DualBladeMainhandCraftingTemplateId
            };

        public static bool IsGreyWardenLargeShieldItemId(string? itemId)
        {
            return string.Equals(
                       itemId,
                       LargeShieldItemId,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       itemId,
                       BlackLargeShieldItemId,
                       StringComparison.OrdinalIgnoreCase);
        }

    }
}
