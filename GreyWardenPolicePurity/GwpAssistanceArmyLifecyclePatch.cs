using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using System;
using System.Linq;
using System.Reflection;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// While the source assistance task remains active, formation lifetime is
    /// owned by the enforcement system rather than Bannerlord's kingdom-army
    /// lifecycle. Native no-war, food, inactivity, cohesion and other automatic
    /// dispersals are blocked. The enforcement system disbands the Army itself,
    /// through ApplyAuthorizedObjectiveFinished, when the source task is released.
    /// 带头部队没了（被打光销毁、军团长死了）时不拦：原版在部队销毁时（MobileParty.OnRemoveParty → Army.OnRemovePartyInternal）
    /// 解散军团，拦下以后部队接着被清空家族、移出全图名单，案件系统按编号再也找不到它，军团就成了没有带头部队的空壳；
    /// 每小时算凝聚力时 NavalDLC 查军团长文化特长，读到空的所属势力，游戏闪退（C#反编译 v1.4.8 MobileParty.RemoveParty / OnRemoveParty、
    /// Army.OnRemovePartyInternal、PartyBaseHelper.HasFeat、PartyBase.Culture；实机诊断 2026-10-08：gw_leader_2 带协力军团被俘、
    /// 部队归零，53 秒后闪退）。
    /// </summary>
    [HarmonyPatch]
    internal static class GwpAssistanceArmyDisbandGuardPatch
    {
        [ThreadStatic]
        private static int _authorizedDepth;

        private static MethodBase? TargetMethod() =>
            AccessTools.Method(typeof(DisbandArmyAction), "ApplyInternal");

        private static bool Prepare() => TargetMethod() != null;

        [HarmonyPrefix]
        private static bool Prefix(Army army,
            Army.ArmyDispersionReason reason)
        {
            if (_authorizedDepth > 0 ||
                !PoliceEnforcementBehavior.IsActiveAssistanceArmy(army) ||
                reason is Army.ArmyDispersionReason.LeaderPartyRemoved or Army.ArmyDispersionReason.ArmyLeaderIsDead ||
                army?.LeaderParty?.IsActive != true)
                return true;

            MobileParty? leader = army?.LeaderParty;
            if (leader?.IsActive == true)
                GwpAiDiagnostics.WriteAction(leader,
                    "ASSISTANCE_ARMY_NATIVE_DISBAND_BLOCKED",
                    "reason=" + reason);
            return false;
        }

        internal static void ApplyAuthorizedObjectiveFinished(Army army)
        {
            if (army == null)
                return;

            _authorizedDepth++;
            try
            {
                DisbandArmyAction.ApplyByObjectiveFinished(army);
            }
            finally
            {
                _authorizedDepth--;
            }
        }
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

#if GWP_DIAGNOSTICS
        // 依据：实机诊断2026-10-19 23:30，军团补凝聚力时关系模型收到空英雄。
        // 原栈没有军团/成员参数；只补失败现场，不吞异常、不改变费用或编队。
        [HarmonyFinalizer]
        private static Exception? Finalizer(MobileParty armyLeaderParty,
            MobileParty party, Exception? __exception)
        {
            if (__exception == null) return null;
            Army? army = armyLeaderParty?.Army;
            GwpFaultTrace.Write("ARMY_INFLUENCE_FAILURE", details:
                "leaderParty=" + (armyLeaderParty?.StringId ?? "-") +
                "; leaderHero=" + (armyLeaderParty?.LeaderHero?.StringId ?? "-") +
                "; leaderActive=" + (armyLeaderParty?.IsActive == true) +
                "; memberParty=" + (party?.StringId ?? "-") +
                "; memberHero=" + (party?.LeaderHero?.StringId ?? "-") +
                "; memberActive=" + (party?.IsActive == true) +
                "; memberIsDelay=" + GwpCommon.IsEnforcementDelayPatrolParty(party) +
                "; memberArmyLeader=" + (party?.Army?.LeaderParty?.StringId ?? "-") +
                "; armyKingdom=" + (army?.Kingdom?.StringId ?? "-") +
                "; activeAssistance=" + PoliceEnforcementBehavior.IsActiveAssistanceArmy(army) +
                "; members=" + (army == null ? "-" : string.Join(",",
                    army.Parties.Select(member => (member?.StringId ?? "-") + ":" +
                        (member?.LeaderHero?.StringId ?? "-")))) +
                "; error=" + __exception.GetType().FullName);
            return __exception;
        }
#endif
    }

#if GWP_DIAGNOSTICS
    /// <summary>失败时记录真正被计费的Army；不能用LeaderParty.Army代替，残留回调的两者可能不同。</summary>
    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel.CalculateTotalInfluenceCost))]
    internal static class GwpArmyInfluenceFailureTracePatch
    {
        [HarmonyFinalizer]
        private static Exception? Finalizer(Army army, float percentage,
            Exception? __exception)
        {
            if (__exception == null) return null;
            GwpFaultTrace.Write("ARMY_TOTAL_INFLUENCE_FAILURE", details:
                "leaderParty=" + (army?.LeaderParty?.StringId ?? "-") +
                "; leaderHero=" + (army?.LeaderParty?.LeaderHero?.StringId ?? "-") +
                "; kingdom=" + (army?.Kingdom?.StringId ?? "-") +
                "; leaderPointsToThisArmy=" + (army != null && army.LeaderParty?.Army == army) +
                "; activeAssistance=" + PoliceEnforcementBehavior.IsActiveAssistanceArmy(army) +
                "; percentage=" + percentage.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; members=" + (army == null ? "-" : string.Join(",", army.Parties.Select(member =>
                    (member?.StringId ?? "-") + ":hero=" + (member?.LeaderHero?.StringId ?? "-") +
                    ":delay=" + GwpCommon.IsEnforcementDelayPatrolParty(member) +
                    ":pointsToThisArmy=" + (member?.Army == army)))));
            return __exception;
        }
    }
#endif

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
