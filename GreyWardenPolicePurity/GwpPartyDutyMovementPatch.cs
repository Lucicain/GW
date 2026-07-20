using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Campaign AI score listeners are stored by MbEvent as a head-inserted
    /// linked list, therefore a behavior registered last is invoked first.
    /// Once every native producer has finished, cap only assigned-party patrol
    /// candidates and add the slightly higher fallback duty immediately before
    /// AiPartyThinkBehavior resolves the winner. All non-patrol tuples remain
    /// untouched.
    /// </summary>
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.AiHourlyTick))]
    internal static class GwpFinalDesireAuctionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MobileParty party, PartyThinkParams partyThinkParams)
        {
            GreyWardenPartyDesireBehavior.ProcessFinalDesires(party, partyThinkParams);
        }
    }

    /// <summary>
    /// PartyThink has a branch for a PatrolAroundPoint winner but no branch for
    /// a GoToPoint winner. PatrolAroundPoint in turn never receives a matching
    /// short-term movement in MobileParty.RecalculateShortTermBehavior. Use the
    /// patrol candidate only as the auction carrier, then translate the winning
    /// Grey Warden location duty into the engine's real GoToPoint movement.
    /// </summary>
    [HarmonyPatch(typeof(SetPartyAiAction),
        nameof(SetPartyAiAction.GetActionForPatrollingAroundPoint))]
    internal static class GwpLocationDutyRefreshPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty owner, CampaignVec2 position,
            MobileParty.NavigationType navigationType)
        {
            if (owner?.IsActive != true ||
                !GreyWardenPartyDesireBehavior.TryGetLocationApproachTarget(
                    owner, out MobileParty? target) || target == null)
                return true;

            owner.SetMoveGoToPoint(position, navigationType);
            GwpAiDiagnostics.WriteAction(owner, "POINT_WINNER_TO_GOTOPOINT",
                $"target={target.StringId}; point={position.ToVec2()}");
            return false;
        }
    }

    /// <summary>
    /// Prevents a leaderless enforcement party's direct attack from being
    /// replaced by Bannerlord's ordinary hourly AI auction.
    /// </summary>
    [HarmonyPatch(typeof(AiPartyThinkBehavior), "PartyHourlyAiTick")]
    internal static class GwpPartyThinkResolvedDiagnosticsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty mobileParty)
        {
            // 无英雄临时执法队锁定敌对目标后跳过整个小时思考回合，
            // 避免欲望生成、强弱判断或空竞价解析覆盖首次 EngageParty。
            return !GreyWardenPartyDesireBehavior.HasDirectAttackLock(mobileParty);
        }

        [HarmonyPostfix]
        private static void Postfix(MobileParty mobileParty)
        {
            if (GwpAiDiagnostics.ShouldTraceParty(mobileParty))
                GwpAiDiagnostics.WriteResolved(mobileParty);
        }
    }

    /// <summary>
    /// Point travel is represented internally by Bannerlord's point-patrol
    /// behavior because the native desire resolver has no GoToPoint branch.
    /// Show the actual police duty to players instead of the misleading stock
    /// "patrolling" label while the party is travelling to the known location.
    /// </summary>
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.GetBehaviorText))]
    internal static class GwpLocationDutyBehaviorTextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MobileParty __instance, ref TextObject __result)
        {
            if (__instance?.IsActive != true ||
                __instance.DefaultBehavior != AiBehavior.GoToPoint ||
                MobileParty.IsFleeBehavior(__instance.ShortTermBehavior) ||
                !GreyWardenPartyDesireBehavior.TryGetLocationApproachTarget(
                    __instance, out MobileParty? target) || target == null)
                return;

            var text = new TextObject(
                "{=gwp_location_duty_travelling}Travelling toward the last known position of {TARGET_PARTY}.");
            text.SetTextVariable("TARGET_PARTY", target.Name);
            __result = text;
        }
    }
}
