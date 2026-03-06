using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal static class GwpCommon
    {
        public const string PatrolIdPrefix = GwpIds.PatrolIdPrefix;
        public const string EnforcementDelayPatrolIdPrefix = GwpIds.EnforcementDelayPatrolIdPrefix;
        public const string HeavyInfantryId = GwpIds.HeavyInfantryId;
        public const string ArcherId = GwpIds.ArcherId;
        public const string KnightId = GwpIds.KnightId;

        public static bool IsPatrolParty(MobileParty? party)
        {
            return party?.StringId?.StartsWith(PatrolIdPrefix, StringComparison.Ordinal) == true;
        }

        public static bool IsEnforcementDelayPatrolParty(MobileParty? party)
        {
            return party?.StringId?.StartsWith(EnforcementDelayPatrolIdPrefix, StringComparison.Ordinal) == true;
        }

        public static Settlement? FindNearestTown(MobileParty? party)
        {
            return party == null ? null : FindNearestTown(party.GetPosition2D);
        }

        public static Settlement? FindNearestTown(Vec2 position)
        {
            Settlement? nearest = null;
            float minDistance = float.MaxValue;

            foreach (Settlement settlement in Settlement.All)
            {
                if (!settlement.IsTown) continue;

                float distance = position.Distance(settlement.GetPosition2D);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = settlement;
                }
            }

            return nearest;
        }

        public static void TrySetNeutral(IFaction? left, IFaction? right)
        {
            if (left == null || right == null) return;
            if (!FactionManager.IsAtWarAgainstFaction(left, right)) return;

            try { FactionManager.SetNeutral(left, right); } catch { }
        }

        public static void TrySetAggressiveAi(MobileParty? party)
        {
            if (party == null || !party.IsActive) return;
            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.SetInitiative(1f, 1f, 1f);
            }
            catch { }
        }

        public static void TryResetAi(MobileParty? party)
        {
            if (party == null || !party.IsActive) return;
            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.SetInitiative(0f, 0f, 0f);
            }
            catch { }
        }

        public static void TryFinishPlayerEncounter()
        {
            try
            {
                if (!PlayerEncounter.IsActive) return;
                PlayerEncounter.LeaveEncounter = true;
                PlayerEncounter.Finish(false);
            }
            catch { }
        }
    }
}
