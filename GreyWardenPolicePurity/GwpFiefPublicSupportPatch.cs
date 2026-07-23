using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Converts the petition's locked Grey Warden standing into fixed public
    /// support points. Bannerlord then adds the player's ordinary influence
    /// vote on top instead of letting the public contribution shrink to keep
    /// the same percentage.
    /// </summary>
    [HarmonyPatch(typeof(KingdomElection),
        nameof(KingdomElection.DetermineOfficialSupport))]
    internal static class GwpFiefPublicSupportPatch
    {
        private static readonly FieldInfo? DecisionField =
            AccessTools.Field(typeof(KingdomElection), "_decision");

        private static readonly PropertyInfo? TotalSupportProperty =
            AccessTools.Property(typeof(DecisionOutcome),
                nameof(DecisionOutcome.TotalSupportPoints));

        private static readonly PropertyInfo? WinChanceProperty =
            AccessTools.Property(typeof(DecisionOutcome),
                nameof(DecisionOutcome.WinChance));

        private static void Postfix(KingdomElection __instance)
        {
            if (DecisionField?.GetValue(__instance) is not
                    GwpSettlementReconsiderationDecision decision ||
                TotalSupportProperty == null || WinChanceProperty == null)
                return;

            var outcomes = __instance.PossibleOutcomes.ToList();
            SettlementClaimantDecision.ClanAsDecisionOutcome? playerOutcome =
                outcomes.OfType<
                        SettlementClaimantDecision.ClanAsDecisionOutcome>()
                    .FirstOrDefault(outcome =>
                        outcome.Clan == Clan.PlayerClan);
            if (playerOutcome == null) return;

            float desiredShare = Math.Max(0f,
                Math.Min(0.5f, decision.PublicSupportPercent / 100f));
            if (desiredShare <= 0f) return;

            float nativeTotal = outcomes.Sum(outcome =>
                Math.Max(0f, outcome.TotalSupportPoints));
            float playerPoints = Math.Max(0f,
                playerOutcome.TotalSupportPoints);
            float playerVotePoints = outcomes.Sum(outcome =>
                outcome.SupporterList
                    .Where(supporter => supporter.IsPlayer)
                    .Sum(supporter => Math.Max(0,
                        (int)supporter.SupportWeight - 1)));
            float playerVotePointsOnPlayer = playerOutcome.SupporterList
                .Where(supporter => supporter.IsPlayer)
                .Sum(supporter => Math.Max(0,
                    (int)supporter.SupportWeight - 1));
            float baselineTotal = Math.Max(0f,
                nativeTotal - playerVotePoints);
            float baselinePlayerPoints = Math.Max(0f,
                playerPoints - playerVotePointsOnPlayer);
            float nativeShare = baselineTotal > 0.0001f
                ? baselinePlayerPoints / baselineTotal
                : 0f;
            float addedPoints = 0f;

            if (baselineTotal <= 0.0001f)
            {
                const float syntheticTotal = 10f;
                float remaining = syntheticTotal * (1f - desiredShare);
                var others = outcomes.Where(outcome =>
                    outcome != playerOutcome).ToList();
                TotalSupportProperty.SetValue(playerOutcome,
                    syntheticTotal * desiredShare +
                    playerVotePointsOnPlayer);
                addedPoints = syntheticTotal * desiredShare;
                if (others.Count > 0)
                {
                    float each = remaining / others.Count;
                    foreach (DecisionOutcome outcome in others)
                    {
                        float outcomePlayerVote = outcome.SupporterList
                            .Where(supporter => supporter.IsPlayer)
                            .Sum(supporter => Math.Max(0,
                                (int)supporter.SupportWeight - 1));
                        TotalSupportProperty.SetValue(outcome,
                            each + outcomePlayerVote);
                    }
                }
            }
            else
            {
                if (nativeShare + 0.0001f < desiredShare)
                {
                    float publicPoints =
                        (desiredShare * baselineTotal -
                         baselinePlayerPoints) /
                        (1f - desiredShare);
                    addedPoints = Math.Max(0f, publicPoints);
                    TotalSupportProperty.SetValue(playerOutcome,
                        playerPoints + addedPoints);
                }
            }

            float adjustedTotal = outcomes.Sum(outcome =>
                Math.Max(0f, outcome.TotalSupportPoints));
            if (adjustedTotal <= 0.0001f) return;
            foreach (DecisionOutcome outcome in outcomes)
            {
                WinChanceProperty.SetValue(outcome,
                    Math.Max(0f, outcome.TotalSupportPoints) /
                    adjustedTotal);
            }

            float finalShare = Math.Max(0f,
                playerOutcome.TotalSupportPoints) / adjustedTotal;
            Hero? holder = GreyWardenFamilyBehavior.GetLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.PlayerRequests);
            MobileParty? coordinator = holder?.PartyBelongedTo;
            if (coordinator?.IsActive == true)
            {
                GwpAiDiagnostics.WriteAction(coordinator,
                    "PLAYER_FIEF_PUBLIC_SUPPORT_APPLIED",
                    "settlement=" + decision.Settlement.StringId +
                    "; configuredPercent=" +
                    decision.PublicSupportPercent +
                    "; nativePercent=" +
                    Math.Round(nativeShare * 100f, 2) +
                    "; playerVotePoints=" +
                    Math.Round(playerVotePoints, 2) +
                    "; finalPercent=" +
                    Math.Round(finalShare * 100f, 2) +
                    "; addedPoints=" +
                    Math.Round(addedPoints, 2));
            }
        }
    }
}
