using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using System.Linq;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// An independent clan has no kingdom war containing landed fiefs, so the
    /// native Army lifecycle would otherwise disperse a valid police army for
    /// the kingdom-only "landed war" check. Every other dispersion rule remains
    /// native, including starvation, inactivity and ordinary AI cancellation.
    /// </summary>
    [HarmonyPatch(typeof(DisbandArmyAction),
        nameof(DisbandArmyAction.ApplyByNoActiveWar))]
    internal static class GwpAssistanceArmyNoWarDisbandPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Army army) =>
            !PoliceEnforcementBehavior.IsActiveAssistanceArmy(army);
    }

    /// <summary>
    /// All members of an enforcement army belong to the same Grey Warden clan.
    /// Same-clan joining already costs zero native influence; keep that formation
    /// at its current cohesion instead of applying the kingdom-army daily decay.
    /// </summary>
    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange))]
    internal static class GwpAssistanceArmyCohesionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Army army, bool includeDescriptions,
            ref ExplainedNumber __result)
        {
            if (PoliceEnforcementBehavior.IsActiveAssistanceArmy(army))
                __result = new ExplainedNumber(0f, includeDescriptions);
        }
    }

    /// <summary>
    /// A leaderless temporary support party has no Hero for Bannerlord's
    /// influence formula. It may join only an existing valid enforcement Army,
    /// so that one join costs zero; all ordinary Army costs remain native.
    /// </summary>
    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost))]
    internal static class GwpLeaderlessSupportArmyInfluencePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty armyLeaderParty,
            MobileParty party, ref int __result)
        {
            if (party == null || party.LeaderHero != null ||
                !GwpCommon.IsEnforcementDelayPatrolParty(party) ||
                party.Army?.LeaderParty != armyLeaderParty ||
                !PoliceEnforcementBehavior.IsActiveAssistanceArmy(party.Army))
                return true;

            __result = 0;
            return false;
        }
    }

    /// <summary>
    /// The native army encounter background assumes every Army owns a Kingdom
    /// culture. Independent enforcement armies intentionally do not, so use the
    /// stock fallback background when the player opens their encounter menu.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior),
        "army_encounter_background_on_init")]
    internal static class GwpAssistanceArmyEncounterBackgroundPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MenuCallbackArgs args)
        {
            MobileParty? encountered = PlayerEncounter.EncounteredMobileParty;
            if (!PoliceEnforcementBehavior.IsActiveAssistanceArmy(encountered?.Army))
                return true;

            args.MenuContext.SetBackgroundMeshName("wait_fallback");
            return false;
        }
    }

    /// <summary>
    /// Bannerlord's native "talk to other army members" row dereferences every
    /// attached party's LeaderHero before checking whether that Hero exists.
    /// Temporary enforcement support parties deliberately have no lord, so they
    /// are not conversation candidates and must not reach that native row.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior),
        "game_menu_army_talk_to_other_members_item_on_condition")]
    internal static class GwpLeaderlessSupportConversationItemPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MenuCallbackArgs args, ref bool __result)
        {
            MobileParty? encountered = PlayerEncounter.EncounteredMobileParty;
            if (!PoliceEnforcementBehavior.IsActiveAssistanceArmy(encountered?.Army))
                return true;

            MobileParty? member = args.MenuContext.GetCurrentRepeatableObject()
                as MobileParty;
            if (member == null || member.LeaderHero != null ||
                !GwpCommon.IsEnforcementDelayPatrolParty(member))
                return true;

            __result = false;
            return false;
        }
    }

    /// <summary>
    /// If an enforcement army contains no lord-led attached party at all, hide
    /// the parent submenu as well instead of opening an empty member list.
    /// </summary>
    [HarmonyPatch(typeof(EncounterGameMenuBehavior),
        "game_menu_army_talk_to_other_members_on_condition")]
    internal static class GwpLeaderlessSupportConversationMenuPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (!__result)
                return;

            MobileParty? encountered = PlayerEncounter.EncounteredMobileParty;
            Army? army = encountered?.Army;
            if (!PoliceEnforcementBehavior.IsActiveAssistanceArmy(army))
                return;

            __result = army!.LeaderParty.AttachedParties.Any(member =>
                member?.LeaderHero != null);
        }
    }

    /// <summary>
    /// Native AiEngagePartyBehavior skips every army leader whose ArmyType is
    /// not Defender. Grey Warden enforcement armies are deliberately Patrolling,
    /// so expose them as Defender only while that one native score producer runs.
    /// The real ArmyType is restored immediately; all other army behavior remains
    /// Patrolling and the resulting attack score is still entirely native.
    /// </summary>
    [HarmonyPatch(typeof(AiEngagePartyBehavior), "AiHourlyTick")]
    internal static class GwpAssistanceArmyNativeEngageDesirePatch
    {
        private readonly struct ArmyTypeState
        {
            internal ArmyTypeState(Army army, Army.ArmyTypes original)
            {
                Army = army;
                Original = original;
            }

            internal Army? Army { get; }
            internal Army.ArmyTypes Original { get; }
        }

        [HarmonyPrefix]
        private static void Prefix(MobileParty mobileParty,
            out ArmyTypeState __state)
        {
            Army? army = mobileParty.Army;
            __state = default;
            if (army != null && army.LeaderParty == mobileParty &&
                PoliceEnforcementBehavior.IsActiveAssistanceArmy(army))
            {
                __state = new ArmyTypeState(army, army.ArmyType);
                army.ArmyType = Army.ArmyTypes.Defender;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(ArmyTypeState __state)
        {
            if (__state.Army != null)
                __state.Army.ArmyType = __state.Original;
        }

        [HarmonyFinalizer]
        private static System.Exception? Finalizer(System.Exception? __exception,
            ArmyTypeState __state)
        {
            if (__state.Army != null)
                __state.Army.ArmyType = __state.Original;
            return __exception;
        }
    }
}
