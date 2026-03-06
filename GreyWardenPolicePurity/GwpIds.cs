using System;
using System.Collections.Generic;

namespace GreyWardenPolicePurity
{
    internal static class GwpIds
    {
        public const string PoliceClanId = "gw";
        public const string PatrolIdPrefix = "gwp_patrol_";
        public const string EnforcementDelayPatrolIdPrefix = "gwp_enf_delay_";
        public const string RecruitmentPatrolPrefix = "gwp_recruit_";

        public const string HeavyInfantryId = "gwheavyinfantry";
        public const string ArcherId = "gwarcher";
        public const string KnightId = "gwknight";
        public const string PoliceRecruitId = "gwrecruit";
        public const string CommanderTemplateCharacterId = "gw_leader_0";
        public const string GrainItemId = "grain";

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
                "wharnesscom"
            };
    }
}
