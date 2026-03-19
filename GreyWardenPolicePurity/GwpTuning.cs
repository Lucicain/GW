namespace GreyWardenPolicePurity
{
    internal static class GwpTuning
    {
        internal static class Bounty
        {
            public const float OfferCooldownDays = 2f;
            public const float IntelReportIntervalDays = 2f;
            public const int RewardPerTroop = 200;
            public const float EscortEngageDistance = 3f;
            public const int RecruitmentReputationThreshold = 20;
            public const int RecruitmentPatrolSize = 20;
        }

        internal static class TroopRequest
        {
            public const int MinimumReputation = 20;
            public const int VeteranReputation = 40;
            public const int KnightReputation = 60;
            public const int EliteDiscountReputation = 80;

            public const int RecruitBasePrice = 120;
            public const int HeavyInfantryBasePrice = 260;
            public const int ArcherBasePrice = 280;
            public const int KnightBasePrice = 750;
        }

        internal static class Enforcement
        {
            public const float WarDistance = 3f;
            public const float PlayerWarDistance = 15f;
            public const int ShelteredForceBattleIntervalHours = 6;
            public const float EscortPunishDistance = 3f;
            public const float AtonementIntelReportIntervalDays = 2f;
            public const float AtonementDeadlineDays = 45f;
        }

        internal static class Patrol
        {
            public const int FinePerPoint = 200;
            public const int NegotiationDivisor = 4;
            public const int RewardPerPointPerDay = 20;
            public const int PatrolSize = 10;
            public const float WarDistance = 15f;
        }

        internal static class Family
        {
            public const int MaxClanMembers = 15;
            public const float AdoptionCooldownDays = 365f;
            public const float VillageReliefStayHours = 72f;
            public const float VillageReliefArrivalDistance = 3f;
            public const int AdoptedGirlMinAge = 2;
            public const int AdoptedGirlMaxAge = 6;
        }
    }
}
