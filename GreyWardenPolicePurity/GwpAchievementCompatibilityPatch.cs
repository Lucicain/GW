using System;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using StoryMode.GameComponents.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;

namespace GreyWardenPolicePurity
{
    internal static class GwpAchievementCompatibility
    {
        private const string ModuleId = "GreyWarden";
        private const string UnofficialModulesReasonId = "R0AbAxqX";

        private static readonly FieldInfo? DeactivatedField = AccessTools.Field(
            typeof(AchievementsCampaignBehavior),
            "_deactivateAchievements");

        internal static void RestoreIfOnlyGreyWardenBlocked(
            AchievementsCampaignBehavior behavior)
        {
            if (DeactivatedField?.GetValue(behavior) is not true)
            {
                return;
            }

            if (DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved(
                    out _))
            {
                DeactivatedField.SetValue(behavior, false);
            }
        }

        internal static bool ShouldIgnoreUnofficialModuleFailure(
            TextObject? reason)
        {
            if (reason == null ||
                !string.Equals(
                    reason.GetID(),
                    UnofficialModulesReasonId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            Campaign? campaign = Campaign.Current;
            if (campaign?.PreviouslyUsedModules == null)
            {
                return false;
            }

            var officialModuleIds = ModuleHelper.GetOfficialModuleIds();
            bool foundGreyWarden = false;
            foreach (string moduleSet in campaign.PreviouslyUsedModules)
            {
                string[] modules = moduleSet.Split(
                    MBSaveLoad.ModuleCodeSeperator);
                foreach (string module in modules)
                {
                    string[] fields = module.Split(
                        MBSaveLoad.ModuleVersionSeperator);
                    if (fields.Length == 0 ||
                        string.IsNullOrWhiteSpace(fields[0]))
                    {
                        return false;
                    }

                    string moduleId = fields[0];
                    bool isOfficial = false;
                    foreach (string officialModuleId in officialModuleIds)
                    {
                        if (string.Equals(
                                moduleId,
                                officialModuleId,
                                StringComparison.InvariantCultureIgnoreCase))
                        {
                            isOfficial = true;
                            break;
                        }
                    }

                    if (isOfficial)
                    {
                        continue;
                    }

                    if (!string.Equals(
                            moduleId,
                            ModuleId,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        return false;
                    }

                    foundGreyWarden = true;
                }
            }

            return foundGreyWarden;
        }
    }

    [HarmonyPatch(
        typeof(DumpIntegrityCampaignBehavior),
        nameof(DumpIntegrityCampaignBehavior.IsGameIntegrityAchieved))]
    internal static class GwpDumpIntegrityAchievementPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            ref bool __result,
            ref TextObject reason)
        {
            if (__result ||
                !GwpAchievementCompatibility
                    .ShouldIgnoreUnofficialModuleFailure(reason))
            {
                return;
            }

            __result = true;
            reason = null!;
        }
    }

    [HarmonyPatch(
        typeof(AchievementsCampaignBehavior),
        nameof(AchievementsCampaignBehavior.CheckAchievementSystemActivity))]
    internal static class GwpAchievementActivityPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            AchievementsCampaignBehavior __instance)
        {
            GwpAchievementCompatibility
                .RestoreIfOnlyGreyWardenBlocked(__instance);
        }
    }
}
