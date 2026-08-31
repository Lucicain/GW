namespace GreyWardenPolicePurity
{
    internal static class GwpTuning
    {
        internal static class Bounty
        {
            public const float OfferCooldownDays = 2f;
            public const float IntelReportIntervalDays = 2f;
            public const float DeadlineDays = 45f;
            public const float EasyStrengthRatio = 0.75f;
            public const float HardStrengthRatio = 1.25f;
            public const int EasyReward = 10000;
            public const int StandardReward = 20000;
            public const int HardReward = 30000;
            public const float CollectionCourierDelayDays = 5f;
            public const int CollectionCourierPatrolSize = 10;
            public const int RecruitmentReputationThreshold = 20;
            public const int ReadmissionReputationStep = 20;
            public const int MaximumVoluntaryExits = 3;
            public const int RecruitmentPatrolSize = 20;
            public const float RecruitmentContactDistance = 3f;
            public const float RecruitmentPursuitTimeoutDays = 5f;
        }

        internal static class TroopRequest
        {
            public const int MinimumReputation = 20;
            public const int VeteranReputation = 40;
            public const int KnightReputation = 60;
            public const int EliteDiscountReputation = 80;

            public const int RecruitBasePrice = 80;
            public const int HeavyInfantryBasePrice = 180;
            public const int ArcherBasePrice = 190;
            public const int KnightBasePrice = 450;
            public const int LowStandingOrderLimit = 20;
            public const int VeteranOrderLimit = 40;
            public const int KnightOrderLimit = 60;
            public const int EliteOrderLimit = 80;
            public const float PlayerOrderXpIntervalHours = 6f;
            public const int PlayerOrderXpPerTroop = 1000;
            public const float ContactDistance = 3f;
        }

        internal static class Training
        {
            public const float ExperienceIntervalHours = 6f;
            public const int ExperiencePerTroopPerInterval = 250;
            public const float ExchangeStayHours = 2f;
            public const float MovementIntentHours = 8f;
        }

        internal static class PlayerRequests
        {
            public const int FiefAppealPrice = 50000;
            public const float FiefAppealWindowDays = 30f;
            public const float LobbyingHours = 24f;
            public const float MovementIntentHours = 8f;
            public const float ContactDistance = 3f;
            public const float DeferredContactHours = 12f;
            public const int DeferredOrdinaryTasks = 2;
        }

        internal static class Enforcement
        {
            /// <summary>
            /// The fine a Grey Warden lord levies per point of negative
            /// standing.  It was a literal in the enforcement dialogue; the
            /// case ledger has to quote the same figure, so both read it here.
            /// A provost patrol settles for less - see Patrol.FinePerPoint.
            /// </summary>
            public const int FinePerPoint = 300;

            public const float WarDistance = 3f;
            public const float PlayerWarDistance = 15f;
            public const int ShelteredForceBattleIntervalHours = 6;
            public const float ShelteredForceBattleDistance = 1.5f;
            // 城外围堵点可直接触发既有驱逐流程，不要求军团贴到城门脚下。
            public const float ShelteredGateDistance = 12f;
            public const int ShelteredGateHoldHours = 1;
            public const float ShelteredGateStopTolerance = 0.35f;
            public const float EscortPunishDistance = 3f;
            public const float AtonementIntelReportIntervalDays = 2f;
            public const float AtonementDeadlineDays = 45f;
        }

        internal static class Patrol
        {
            /// <summary>
            /// A patrol settles for less than a Warden lord does; both fines
            /// scale with the same standing.  See Enforcement.FinePerPoint.
            /// </summary>
            public const int FinePerPoint = 200;
            public const int NegotiationDivisor = 4;
            public const int RewardPerPointPerDay = 20;
            public const int PatrolSize = 10;
            public const float WarDistance = 15f;
        }


        internal static class Family
        {
            public const int MaxClanMembers = 15;
            public const float AdoptionCooldownYears = 1f;
            public const float VillageReliefStayHours = 72f;
            public const float VillageReliefArrivalDistance = 3f;
            public const int AdoptedGirlMinAge = 2;
            public const int AdoptedGirlMaxAge = 6;
        }

        internal static class VillageReward
        {
            public const int DenarsPerReputationPerDay = 10;
        }

        internal static class Reconstruction
        {
            public const float WorkHours = 24f;
            public const float ArrivalDistance = 3f;
            public const float TreasuryShare = 0.03f;
            public const int MinimumCost = 15000;
            public const int MaximumCost = 30000;
            public const int MinimumTreasuryReserve = 50000;
            public const int WageReserveDays = 7;
        }

        internal static class Deterrence
        {
            public const float RaidPenaltyCap = 9f;
            public const float MaxPenaltyGainPerCapture = 9f;
            public const float RaidScoreMultiplierPerPoint = 0.65f;
            public const float RaidScoreMultiplierFloor = 0f;
            // Deterrence first fell to one tenth of the original rate; the
            // current balance halves that final recovery speed once more.
            public const float BaseRecoveryPerDay = 0.009f;
            public const float MinRecoveryPerDay = 0.004f;
            public const float MaxRecoveryPerDay = 0.0175f;
            public const float RecoverySpeedMultiplier = 0.5f;
            public const float ActiveDialogueThreshold = 0.25f;
            public const float ForgetThreshold = 0.05f;
            public const float RecoveryFloorTolerance = 0.0001f;
            public const float CleanupGraceDays = 3f;
        }

        internal static class IssueResolution
        {
            public const float WorkHours = 6f;
            public const float ArrivalDistance = 2.5f;
            public const float LocalDevelopmentGain = 5f;
        }
    }
}
