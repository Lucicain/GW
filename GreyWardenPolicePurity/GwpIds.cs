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
        /// <summary>玩家练兵订单的随行练兵队。人和订单都在它名下，不占练兵官的名额。</summary>
        public const string TrainingCohortIdPrefix = "gwp_cohort_";

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

        // Visually identical NPC copy of the off-hand blade. A separate
        // id is what lets the native off-hand qualification be written to
        // the NPC blade alone, leaving the player's item untouched.
        public const string DualBladeOffhandAiItemId = "gwdualbladeoffhandai";

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
        /// 加入或重新加入灰袍时发给玩家的物品：全套指挥装（六件甲胄＋马具＋
        /// 指挥官黑色大盾），外加一副双刀。盾只发指挥官那面，普通 `wlarge_shield`
        /// 不在此列——发两面盾是多余的。
        ///
        /// 双刀曾一度改为 gwarcher 专属、不再发给玩家，现按用户要求恢复发放。
        /// 发的是玩家版 `gwdualblademainhand` / `gwdualbladeoffhand`，**不是**
        /// NPC 版 `gwdualbladeoffhandai`——后者带着原版副手资格标记，那是留给
        /// NPC 的，混进玩家背包会让双持判定串味。
        /// </summary>
        public static readonly IReadOnlyCollection<string> MembershipGrantItemIds =
            new HashSet<string>(CommanderSetItemIds, StringComparer.OrdinalIgnoreCase)
            {
                DualBladeMainhandItemId,
                DualBladeOffhandItemId
            };

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
