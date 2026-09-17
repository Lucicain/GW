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
            public const int RecruitmentReputationThreshold = 20;
            public const int ReadmissionReputationStep = 20;
            public const int MaximumVoluntaryExits = 3;
            // 入会时随装备一并发下的灰袍新兵。灰袍兵种在此之前只能找练兵官讨，
            // 新入会的玩家手上一个都没有。
            public const int JoinRecruitCount = 5;
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
            /// <summary>
            /// 每个训练间隔里最多把多少名老兵降回订单要的低级兵。升级树是单向的，
            /// 最低级兵没有任何上游，光靠喂经验永远凑不出来；只能从下游降回去。
            /// 一次只降几个，让它像"拆编重训"而不是瞬间变出人来。
            /// </summary>
            public const int PlayerOrderDowngradePerInterval = 4;
        }

        internal static class Training
        {
            public const float ExperienceIntervalHours = 6f;
            public const int ExperiencePerTroopPerInterval = 250;
            public const float ExchangeStayHours = 2f;
            public const float MovementIntentHours = 8f;

            /// <summary>
            /// 灰袍常备军的兵种配比：骑兵 : 步兵 : 弓手。骑兵翻倍是因为现场追截队
            /// 只抽骑兵，消耗明显快于另外两种。
            /// 实现上不改原版升级公式，只写 <see cref="TaleWorlds.CampaignSystem.Hero.PreferredUpgradeFormation"/>，
            /// 由原版 GetUpgradeChanceForTroopUpgrade 把缺口最大的那条分支抬到 9999 权重。
            /// </summary>
            public const int CavalryShare = 2;
            public const int InfantryShare = 1;
            public const int ArcherShare = 1;
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
            public const int FinePerPoint = 100;

            /// <summary>
            /// 办案领主改追更近罪犯的门槛：新目标必须近到当前目标距离的这个比例以下。
            /// 留出余量是为了不让两个此消彼长的移动目标把承办人来回拉扯。
            /// </summary>
            public const float RetargetImprovementRatio = 0.6f;

            /// <summary>
            /// 同一名承办人两次改追之间的最短间隔，防止目标乱跑时反复换人。
            /// </summary>
            public const float RetargetCooldownHours = 6f;

            public const float WarDistance = 3f;
            public const float PlayerWarDistance = 15f;
            // 协力组按目标现场战力动态缩编。入组门槛是"我方不占优"（committed <= target），
            // 放人门槛要求放完之后仍然稳稳压住目标，两个数不一样才有回滞——否则一个
            // 路过的领主来回走，协力组就会跟着反复拆装。
            public const float AssistanceReleaseMargin = 1.5f;
            // 放人还要求富余连续成立这么多轮小时维护。目标战力本来就会随
            // 身边的人来去而抖，只看单轮就放人会变成反复拉人又反复放人。
            public const int AssistanceSurplusConfirmTicks = 4;
            // 刚拉来就被放走，地图上看是"来了又走"的抽搐。最短在编时长挡住这种来回。
            public const float AssistanceMinimumMemberHours = 12f;
            // 灰袍这一轮凑不出兵，案子退回台账而不是销案。冷却期避免下一小时
            // 又被指派给同样凑不出兵的人，来回空转。
            public const float AssistanceFailureCooldownHours = 72f;
            // 接案门槛。案件难度按目标本队/军团的战力算（不含身边路过的人，
            // 那一档由协力编成负责），超过全家族能凑出来的战力就不立案——
            // 否则案子会被反复接起又反复因凑不出兵退回。留一成余量。
            public const float CaseIntakeStrengthMargin = 0.9f;
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

        /// <summary>
        /// A field arrest is the player's side of enforcement: state the charge, read
        /// the man, and settle it without a war declaration. The three weights below
        /// sum to 1 and decide how much the odds, his temper and the player's standing
        /// each count; the two thresholds cut that score into submit / argue / fight.
        /// </summary>
        internal static class FieldArrest
        {
            public const float RefusalStrengthRatio = 4f;
            public const int ReportAuditDelayDays = 7;
            public const int MaximumCaseFine = 20000;
            public const float ReportAuditChance = 0.08f;
            /// <summary>首次谎报保持低风险，此后按此前谎报次数的平方加速增长。</summary>
            public const float AuditChancePerRepeatSquared = 0.04f;
            public const int AuditTrustStanding = 40;
            public const float AuditTrustReductionPerPoint = 0.01f;
            public const float AuditMaximumTrustReduction = 0.75f;
            public const float AuditChancePerNegativeStandingPoint = 0.005f;
            public const float AuditChanceFloor = 0.02f;
            public const float AuditChanceCeiling = 0.95f;
            /// <summary>谎报记录只看最近这么多次委托。</summary>
            public const int RecentReportMemory = 10;
            /// <summary>
            /// The player's own Grey Warden standing gates nothing and scores nothing: he
            /// may be a respected hunter or a man working off his own record, and the
            /// warrant reads the same either way. It only decides which excuses the
            /// offender reaches for - see GwpFieldArrestBehavior.WardenStandingTier.
            /// </summary>
            public const int RespectedStanding = 40;

            /// <summary>
            /// Below this gap between his best and second-best option he is genuinely
            /// torn, and that is the only case where argument can move him. Raise it and
            /// more encounters open a negotiation; lower it and more resolve on the spot.
            /// </summary>
            public const float DecisionMargin = 0.25f;

            /// <summary>
            /// Deterrence points that count as a man thoroughly taught. Only used to put
            /// the existing deterrence scale onto the 0-1 range this model reasons in.
            /// </summary>
            public const float DeterrenceReference = 60f;

            /// <summary>
            /// What a lying offender claims to be carrying, as a share of the fine. Low
            /// enough to sound ruinous, high enough that believing him is a real choice
            /// rather than an obvious swindle.
            /// </summary>
            public const int PleadedPursePercent = 35;

            /// <summary>
            /// 他自己那套兑现方式各自肯给多少。全缴以外都是打折，差额留在他名下的
            /// 案底里——账没进公库，人就没赎。
            /// </summary>
            public const int BribeSharePercent = 15;
            public const int CunningSharePercent = 50;
            public const int RashSharePercent = 80;
            public const int DeedsOnlySharePercent = 40;
            public const int HaggleSharePercent = 70;
            /// <summary>仁慈低的人拿自己手下出气，杀这么多个。</summary>
            public const int DeedsOnlyTroopsKilled = 5;

            /// <summary>势均力敌时第一层抗性的起点。性格、声望、震慑都从这里加减。</summary>
            public const float DecisionBaseline = 50f;
            /// <summary>
            /// 实力差最多能把第一层抗性推开多少。实力悬殊到极致也就 ±这个数，
            /// 免得它一个维度把性格与声望全压死。
            /// </summary>
            public const float StrengthSwing = 45f;
            /// <summary>第一层抗性里每点性格特质的分量。</summary>
            public const float TraitResistStep = 7f;
            /// <summary>玩家声望档位对第一层抗性的摆动幅度。</summary>
            public const float StandingSwing = 12f;
            /// <summary>被灰袍抓过的经历能压下多少抗性。</summary>
            public const float DeterrenceSwing = 20f;
            /// <summary>第二层里他每点执着增加的抗性。</summary>
            public const float InsistenceStep = 12f;

            /// <summary>
            /// Weight of "he just argues the charge" in the demand draw, against each
            /// trait point's own weight. Raise the plain weight and special demands get
            /// rarer; raise the per-point weight and strong personalities push harder.
            /// </summary>
            public const int PlainDemandWeight = 1;
            public const int DesireWeightPerTraitPoint = 3;

            /// <summary>How much over the fine an eager payer offers.</summary>
            public const int OverPayPercent = 125;

            /// <summary>At most this share of the persuasion goal can be granted before the first argument.</summary>
            public const float InitialProgressShare = 0.5f;

            /// <summary>
            /// The same two halves the Wardens charge the player: a base charge for the
            /// deed, which climbs with every prior case, and the offender's own negative
            /// standing at Enforcement.FinePerPoint each. Standing is earned and repaid by
            /// the player's rules - see HeroCrimeStats.AddCivilianCasualties / AddRedeemingKills.
            /// Arrest history deliberately plays no part here; it drives deterrence instead.
            /// </summary>
            public const int BaseChargeVillageViolence = 1000;
            public const int BaseChargeCaravanAttack = 1000;

            /// <summary>
            /// 村庄少掉一点人口就算一条人命。原先按一点二十人折算，实机日志里一次
            /// 劫掠动辄损失一两百点人口，折出三五千条人命、十几万第纳尔的罚金，
            /// 与设想的量级完全不符，故改为一比一。民兵的死亡另由战斗结算计入。
            /// </summary>
            public const int VillagersPerHearthPoint = 1;

            /// <summary>
            /// What the order pays the Warden who brought the case in: a flat fee for the
            /// work, compensation for the men he lost doing it, and a share for how bad the
            /// case was. The total is capped by the fine itself - the order never pays out
            /// more than it took in, which is what leaves pocketing the fine attractive.
            /// </summary>
            public const int HandInBaseFee = 400;
            public const int HandInCompensationPerCasualty = 60;
            public const int HandInPerSeverityPoint = 120;

            public const int StandingGainOnSubmit = 2;
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
