using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameComponents;
namespace GreyWardenPolicePurity
{
    [HarmonyPatch(typeof(DefaultPersuasionModel), nameof(DefaultPersuasionModel.GetChances))]
    internal static class GwpNegotiationChancePatch
    {
        private static void Prefix(PersuasionOptionArgs optionArgs, ref float difficultyMultiplier)
        {
            Campaign.Current?.GetCampaignBehavior<GwpCitySearchBehavior>()?.AdjustSearchDifficulty(
                optionArgs, ref difficultyMultiplier);
        }

        private static void Postfix(PersuasionOptionArgs optionArgs, ref float successChance,
            ref float critSuccessChance, ref float critFailChance, ref float failChance)
        {
            Campaign.Current?.GetCampaignBehavior<GwpFieldArrestBehavior>()?.AdjustNativeChances(
                optionArgs, ref successChance, ref critSuccessChance, ref critFailChance, ref failChance);
            Campaign.Current?.GetCampaignBehavior<GwpCitySearchBehavior>()?.AdjustSearchChances(
                optionArgs, ref successChance, ref critSuccessChance, ref critFailChance, ref failChance);
        }
    }
}
