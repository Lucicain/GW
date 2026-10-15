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

        internal static class LordCombat
        {
            // 用户裁定 2026-10-16：致命伤后锁 1 血的时长（秒，起初 5 秒，同日改 10 秒），锁血期间战斗成长的倍数。
            public const float LastStandSeconds = 10f;
            public const float LastStandGrowthMultiplier = 1.5f;
        }

        internal static class Training
        {
            // 用户裁定 2026-10-15：超过一半可派，超过四分之三概率翻倍，派至剩余队伍一半。
            public const double VeteranDispatchTriggerShare = 0.5d;
            public const double VeteranDispatchBoostShare = 0.75d;
            public const float VeteranDispatchChanceMultiplier = 2f;
            public const double VeteranDispatchTargetShare = 0.5d;
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
            /// 灰袍使者（用户裁定 2026-10-15）：宣战前从承办领主队里抽人组成（骑兵优先），先去和目标谈。人数、超时为试玩值。
            /// </summary>
            public const int EnvoySize = 5;
            public const double EnvoyTimeoutHours = 72d;
            /// <summary>
            /// 约到比武大会后最多等这么多天（试玩值）。到期两人都在城里就按原版结算把那场打完，有人没到就按原版比武评分比一次。
            /// 原版新开的比武要满 10 天才可能自行结束（DefaultTournamentModel.GetTournamentEndChance），所以这里必须自己收尾。
            /// </summary>
            public const double TournamentDuelTimeoutDays = 5d;
            /// <summary>"附近有比武"指离双方中点最近的这几座能进的城（用户裁定 2026-10-15：附近没有就在最近的城新开一场）。</summary>
            public const int TournamentDuelNearbyTowns = 3;
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
            /// Deterrence points that count as a man thoroughly taught. Only used to put
            /// the existing deterrence scale onto the 0-1 range this model reasons in.
            /// </summary>
            public const float DeterrenceReference = 60f;

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
            /// <summary>请宽限给的时间（玩家当面与灰袍使者相同）。</summary>
            public const double GraceHours = 72d;
            /// <summary>每条要点最多几次说辞（玩家当面与灰袍使者相同）。</summary>
            public const int ArgumentsPerReservation = 4;
            /// <summary>第二层抗性：他打算对应的那项特质每一级加多少（性格至上，用户裁定 2026-10-15；数值为试玩值）。</summary>
            public const float InsistenceStep = 20f;

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
        }

        internal static class IssueResolution
        {
            public const float WorkHours = 6f;
            public const float ArrivalDistance = 2.5f;
            public const float LocalDevelopmentGain = 5f;
        }

        /// <summary>
        /// 火誓者第一批底层牌。用户只定了机制没定数值（state/firesworn.md「还没定」），
        /// 这里是第一版试玩数值。
        /// </summary>
        internal static class Firesworn
        {
            public const float ArrivalDistance = 3f;


            // 原版村庄民兵每天的退役项：现有民兵 × 2.5%（C#反编译 v1.4.8 DefaultSettlementMilitiaModel）。
            // 守村队的灰袍兵站在民兵队里，原版会把他们也算进去，信众村的民兵模型把这部分加回来。
            public const float VanillaMilitiaRetireShare = 0.025f;

            // 重建把当地 5% 的人换成狂信者（用户裁定 2026-10-07）。
            // 守村队补兵：按两户一兵上限减在村与本村无人队灰袍兵的总缺额，每天补此比例（试玩值）。
            // 用户裁定 2026-10-09：保留5%试玩值，明确为原版2.5%缺额速度的两倍。
            public const float GuardRegrowDailyChance = VanillaMilitiaRetireShare * 2f;
            // 灰袍所到之处：每支领主队在村里或村口待一天，增加这么多信众户，不即时产兵（试玩值）。
            public const int WardenPresenceDailySworn = 2;
            public const float ReconstructionSwornShare = 0.05f;
            // 没有祭司的村子：火誓者占户数到这个比例，每周按这个概率来一位祭司。
            public const float PriestZealotShare = 0.1f;
            /// <summary>
            /// 每位在编灰袍领主的巡区容纳几座信众村，全图上限即灰袍领主数的这么多倍；软上限，只挡新来祭司和接任（用户裁定 2026-10-15）。
            /// </summary>
            public const int FaithVillagesPerWarden = 3;
            // 祭司最多拿全村要人势力的一半，其他要人留着给原版领主招兵（用户裁定 2026-10-14）。
            public const float PriestMaxPowerShare = 0.5f;
            // 守村队：两户信众出一个兵（用户裁定 2026-10-08：约为原版民兵平衡点的 3 倍，仍和信众户挂钩）。
            // 这是自己长的上限：烧人生出来的、别村来的可以超过它，超过的不删、只是不再自己长。
            public const int HouseholdsPerGuard = 2;
            // 卖货队、传教队按路上敌人加人的上限（试玩值）。
            public const int EscortMaximumSize = 25;
            public const float PriestWeeklyChance = 0.5f;
            public const int PriestStartingGold = 2000;
            // 祭司格子只能从守村队拉多出来的人：守村队至少留这个比例（且不少于派队时家里留的人数）守村。
            public const float GuardKeptFromPriestSlots = 0.5f;

            // 集会传教：只增加信众户，按独立仪式冷却触发；士兵另按总缺额增长。
            public const float CongregationHours = 24f;
            public const float CongregationHearthShare = 0.04f;
            public const int CongregationMinimumGain = 5;

            // 运械队、传教队照比武大会派（用户裁定 2026-10-07）：只在本村每季固定的一周里，离上次同一件事至少 15 天，每天固定概率（试玩值）。
            public const float FestivalIntervalDays = 15f;
            public const float FestivalDailyChance = 0.35f;
            // 本村传教、烧人比比武大会勤一点，各算各的（用户裁定 2026-10-07：频率稍高、不共用间隔）：
            // 不限本村那一周，离上次同一件事至少 10 天，每天固定概率（试玩值；平均约 13 天一次）。
            public const float RitualIntervalDays = 10f;
            public const float RitualDailyChance = 0.35f;
            // 大地图上仪式的闪卡音效放完一遍，隔这么多秒（现实时间）再放（试玩值）。
            public const float RitualMapSoundGapSeconds = 15f;

            // 烈火责罚：俘虏、原版民兵烧死两个同等级的人生成一个同等级的灰袍兵。
            // 和集会传教一样锁村 24 小时，这 24 小时里村子在地图上冒烟（用户裁定 2026-10-07）。
            public const float DamnationLockHours = 24f;
            // 烧不信教的户（用户裁定 2026-10-07）：现有信众户每 3 户烧 1 户，烧 1 户换 1 户信教。
            public const int DamnationSwornPerBurnedHousehold = 3;

            // 传教队：祭司已是本地最强的信众村照比武大会派，全图同时最多 4 队在外，一队 10 人；派任何队家里都至少留 5 人。
            // 先去 150 距离内有火誓者、还没有祭司、比例不到来祭司门槛的村子里比例最高的，没有就去最近的还没有火誓者的。
            public const int MaxActiveMissions = 4;
            public const int MissionSize = 10;
            public const int MissionMinimumLeftBehind = 5;
            public const float MissionMaxDistance = 150f;

            // 运械队：照比武大会派，看 300 距离内有火誓者的村子，守村队人数（按户数比）拉平要差 5 人以上，
            // 或拿到主兵器的比例比家里低 0.2 以上、拉平要差 5 套以上，才去；至少 5 人押车，人数差多少就多带多少人。
            public const int BalanceEscortSize = 5;
            public const int BalanceMinimumMen = 5;
            public const float BalanceMaxDistance = 300f;
            public const float BalanceMinimumArmedGap = 0.2f;
            public const int BalanceMinimumSets = 5;

            // 卖货：货仓里的东西值 2,000 以上、隔 3 天出一次车。
            public const int CaravanSize = 8;
            public const int CaravanMinimumGoodsValue = 2000;
            public const float CaravanIntervalDays = 3f;
            // 一趟最多卖几座城；下一座城金库至少要有这么多钱才去。
            public const int CaravanMaxTowns = 3;
            public const int CaravanMinimumTownGold = 5000;

            // 无领主队出门的竞价分：压过原版给无领主队刷出的进聚落归并候选（1.6）。
            public const float FaithPartyVisitScore = 8f;
            public const float FaithPartyTravelTimeoutHours = 120f;


            // 信众村对灰袍按城算（GwpWardenVisitDesire）：俘虏、伤员、要捐的东西合起来的「需要」高出 1 的那部分乘这个数
            // （用户裁定 2026-10-05：灰袍优先做和信众村相关的事）。不乘在底分上：原版闲逛分约 1.6 × 0.16 × 导航 1.5 = 0.38，
            // 乘 3 就是 1.15，压过普通案件的 0.99，领主没事也往信众村跑（实机诊断 2026-10-07）。
            public const float FaithVillageVisitPriority = 3f;
            // 每日异常检查用的线：领主俘虏到上限的 90%、兵力低于 60%。
            public const float AnomalyLowStrength = 0.6f;

            // 案子发生在信众村这个距离内，算信众村的案子，优先派人。
            public const float FaithCaseRadius = 6f;

            // 信众村保护费：只收信众村（用户裁定：不再全图收），每户每天。
            public const float FaithProtectionPerHearth = 1f;

            // 输了时死去的兵身上的东西，每件有这个概率归胜方，其余毁了。
            public const float ArmoryLossLootShare = 0.33f;
            // 自动结算：兵器每阶加 6% 伤害；护甲平均阶数每阶让挨的伤害除以 (1 + 8%)。
            public const float ArmoryWeaponTierDamage = 0.06f;
            public const float ArmoryArmorTierProtection = 0.08f;
        }
    }
}
