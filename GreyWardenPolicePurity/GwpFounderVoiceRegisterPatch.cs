using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Native imperial women have four personas in each of two registers.
    /// Use the everyday register for Chenxi and Muguang so all six founders
    /// have different recorded speakers. This changes conversation tags and
    /// voice selection together, without changing their culture or nobility.
    /// </summary>
    [HarmonyPatch(typeof(ConversationTagHelper), nameof(ConversationTagHelper.EducatedClass))]
    internal static class GwpFounderVoiceRegisterPatch
    {
        private static void Postfix(CharacterObject character, ref bool __result)
        {
            // C#反编译 v1.4.8: both voice group tags and DefaultVoiceOverModel
            // derive their register from this helper. Override only these two
            // original characters, including after marriage into another clan.
            if (character.StringId is "gw_leader_4" or "gw_leader_5")
                __result = false;
        }
    }
}
