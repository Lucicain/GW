using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
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
            GwpCrimeDesireAuction.Apply(party, partyThinkParams);
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
    /// The native desire auction has no EngageParty winner branch: it normally
    /// materializes GoAroundParty through this action helper.  For a peaceful
    /// player warrant, translate that one winning candidate to the native
    /// EngageParty command so a persistent Warden follows the moving player
    /// from any distance while retaining the score-1 auction semantics.
    /// </summary>
    [HarmonyPatch(typeof(SetPartyAiAction),
        nameof(SetPartyAiAction.GetActionForGoingAroundParty))]
    internal static class GwpPlayerEnforcementEngageActionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty owner, MobileParty mobileParty,
            MobileParty.NavigationType navigationType, bool isFromPort)
        {
            bool playerWarrant = PoliceEnforcementBehavior.IsPlayerEnforcementApproach(
                owner, mobileParty);
            // 办差的队伍要的是全速赶到人跟前，而不是绕着走或跟着对方的步子。
            bool rushingDuty = !playerWarrant &&
                GreyWardenPartyDesireBehavior.IsRushingTo(owner, mobileParty);
            if (!playerWarrant && !rushingDuty)
                return true;

            owner.SetMoveEngageParty(mobileParty, navigationType);
            GwpAiDiagnostics.WriteAction(owner,
                rushingDuty ? "DUTY_RUSH_ENGAGE_WINNER" : "PLAYER_ENFORCEMENT_ENGAGE_WINNER",
                "target=" + mobileParty.StringId +
                "; navigation=" + navigationType +
                "; fromPort=" + isFromPort);
            return false;
        }
    }

    /// <summary>
    /// Prevents a leaderless enforcement party's direct attack from being
    /// replaced by Bannerlord's ordinary hourly AI auction.
    /// </summary>
    [HarmonyPatch(typeof(AiPartyThinkBehavior), "PartyHourlyAiTick")]
    internal static class GwpPartyThinkDirectAttackLockPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MobileParty mobileParty)
        {
            // 无英雄临时执法队锁定敌对目标后跳过整个小时思考回合，
            // 避免欲望生成、强弱判断或空竞价解析覆盖首次 EngageParty。
            return !GreyWardenPartyDesireBehavior.HasDirectAttackLock(mobileParty);
        }
    }

#if GWP_DIAGNOSTICS
    /// <summary>
    /// Captures the native short-term initiative winner for both managed Grey
    /// Warden parties and the current case targets. This is observation only:
    /// no score, target or returned behavior is changed.
    /// </summary>
    [HarmonyPatch(typeof(DefaultMobilePartyAIModel),
        nameof(DefaultMobilePartyAIModel.GetBestInitiativeBehavior))]
    internal static class GwpInitiativeDiagnosticsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MobileParty mobileParty,
            ref AiBehavior bestInitiativeBehavior,
            ref MobileParty bestInitiativeTargetParty,
            ref float bestInitiativeBehaviorScore,
            ref Vec2 averageEnemyVec)
        {
            GwpAiDiagnostics.CaptureInitiative(mobileParty,
                bestInitiativeBehavior, bestInitiativeTargetParty,
                bestInitiativeBehaviorScore, averageEnemyVec);
        }
    }
#endif

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
                MobileParty.IsFleeBehavior(__instance.ShortTermBehavior))
                return;

            // 灰袍在聚落里、在原地办的事写清楚，原版只会写「原地待命」「在某地等待」
            // （C#反编译 v1.4.8 MobileParty.GetBehaviorText；用户裁定 2026-10-07：用状态文字，不弹消息）。
            if (__instance.MapEvent == null &&
                (GreyWardenVillageReconstructionBehavior.TryDescribeActivity(__instance, out TextObject activity) ||
                 GreyWardenIssueResolutionBehavior.TryDescribeActivity(__instance, out activity) ||
                 GwpFireswornBehavior.TryDescribeActivity(__instance, out activity)))
            {
                __result = activity;
                return;
            }
            if (__instance.DefaultBehavior == AiBehavior.Hold && GreyWardenFamilyBehavior.IsTrainingParty(__instance))
            {
                __result = new TextObject(GwpTextCatalog.gwp_status_training);
                return;
            }

            // 未宣战追捕现在走原版跟随，原版会把它读成"正在跟随 XXX"——对着一个
            // 通缉犯像是在给他护航。目标躲进定居点时才退回定点，那一档保留原措辞。
            if (__instance.DefaultBehavior == AiBehavior.EscortParty &&
                GreyWardenPartyDesireBehavior.TryGetPursuitEscortTarget(
                    __instance, out MobileParty? quarry) && quarry != null)
            {
                var pursuit = new TextObject(
                    GwpTextCatalog.gwp_location_duty_pursuing);
                pursuit.SetTextVariable("TARGET_PARTY", quarry.Name);
                __result = pursuit;
                return;
            }

            if (__instance.DefaultBehavior != AiBehavior.GoToPoint ||
                !GreyWardenPartyDesireBehavior.TryGetLocationApproachTarget(
                    __instance, out MobileParty? target) || target == null)
                return;

            var text = new TextObject(
                GwpTextCatalog.gwp_location_duty_travelling);
            text.SetTextVariable("TARGET_PARTY", target.Name);
            __result = text;
        }
    }
}
