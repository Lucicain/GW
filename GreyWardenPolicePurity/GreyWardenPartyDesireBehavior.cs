using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍大地图职责接入层：有案件时只把原版巡逻候选压到原版可执行阈值，
    /// 再加入职责候选。普通案件使用 0.99，玩家委托使用独立的最高优先分；
    /// 除巡逻外的全部原版欲望和分数保持不变。无职责时完全不改竞价。
    /// </summary>
    public sealed class GreyWardenPartyDesireBehavior : CampaignBehaviorBase
    {
        private enum IntentKind { Approach, Pursue, Escort, Visit, Rush, Beat }

        private sealed class Intent
        {
            public IntentKind Kind;
            public MobileParty? Party;
            public Settlement? Settlement;
            public float Priority;
            public double ExpiresAt;
            // 这次跟随的对象是案件罪犯，不是自己人。只用于把大地图上的行为文字
            // 从原版的"正在跟随"改写成"正在追捕"，不参与任何 AI 判定。
            public bool PursuesOffender;
        }

        // AiPartyThinkBehavior 对 PatrolAroundPoint 的原版执行阈值是 0.03。
        // 办案期间只把高于该值的巡逻候选封顶到这里。
        private const float AssignedPatrolScoreCeiling = 0.03f;
        // 1.4.7 实测的普通原版进城候选多在 0.35～0.85，明确维护需求从约 1
        // 开始并可在缺粮/重伤时升至 3.7～19.6。固定 0.99 让低分日常访问
        // 让位于案件，同时保持所有较强的原版维护需求优先。
        private const float AssignedDutyScore = 0.99f;
        // 玩家委托是灰袍任务体系中的最高优先级。该分值只参加原版欲望拍卖，
        // 不冻结 AI；战斗、逃跑等引擎强制状态仍由原版处理。
        internal const float PlayerRequestScore = 10f;
        // 正规灰袍领主承办玩家案件时使用原版竞价中的固定 1.0 欲望；
        // 这不会覆盖更高的补给、疗伤等原版维护欲望。
        private const float PlayerEnforcementScore = 1f;
        private static readonly Dictionary<string, Intent> Intents =
            new Dictionary<string, Intent>(StringComparer.OrdinalIgnoreCase);
        // 无英雄的一次性纠察/支援队进入 Pursue 阶段后完全关闭欲望生成，
        // 只在锁定目标时下达一次原版 EngageParty。集合只保存本次运行时的 AI 锁。
        private static readonly HashSet<string> DirectAttackLocks =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 地图战斗可能以一方撤退结束并清空当前移动状态。目标仍存活时只在
        // 战后补发一次 EngageParty，不恢复欲望，也不变成每小时重复命令。
        private static readonly HashSet<string> DirectAttackRefreshPending =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 灰袍自己派出的每一种跟随差事都不吃原版"跟着被护送方的步子走"的降速。
        // 集合在每轮欲望解析时维护；速度补丁另外还要求 DefaultBehavior 确实是
        // EscortParty，所以残留条目不会误伤已经改做别的事的队伍。
        private static readonly HashSet<string> EscortSpeedUncapped =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(this, OnPartyDisbandStarted);
            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, OnPartyDisbanded);
            CampaignEvents.CharacterBecameFugitiveEvent.AddNonSerializedListener(this, OnCharacterBecameFugitive);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnHeroTeleportationRequestedEvent.AddNonSerializedListener(this, OnHeroTeleportationRequested);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                Intents.Clear();
                DirectAttackLocks.Clear();
                DirectAttackRefreshPending.Clear();
                EscortSpeedUncapped.Clear();
            }
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            _ = starter;
            DirectAttackLocks.Clear();
            DirectAttackRefreshPending.Clear();
            EscortSpeedUncapped.Clear();
            GwpAiDiagnostics.StartSession();
            // 旧存档可能仍保留上一版本已经选中的巡逻目标。只要求原版在
            // 下一次部分小时 AI tick 重新拍卖，不直接写入任何目的地。
            foreach (MobileParty party in MobileParty.All.Where(IsManagedParty))
                RequestImmediateRethink(party);
        }

        private static void OnPartyDisbandStarted(MobileParty party) =>
            GwpAiDiagnostics.WritePartyLifecycle(party, "PARTY_DISBAND_STARTED", string.Empty);

        private static void OnPartyDisbanded(MobileParty party, Settlement settlement) =>
            GwpAiDiagnostics.WritePartyLifecycle(party, "PARTY_DISBANDED",
                "settlement=" + (settlement?.StringId ?? "-"));

        private static void OnCharacterBecameFugitive(Hero hero, bool showNotification) =>
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "HERO_BECAME_FUGITIVE",
                "showNotification=" + showNotification);

        private static void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification) =>
            GwpAiDiagnostics.WriteHeroLifecycle(victim, "HERO_KILLED",
                "killer=" + (killer?.StringId ?? "-") + "; detail=" + detail +
                "; showNotification=" + showNotification);

        private static void OnHeroTeleportationRequested(Hero hero, Settlement targetSettlement,
            MobileParty targetParty, TeleportHeroAction.TeleportationDetail detail) =>
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "HERO_TELEPORT_REQUESTED",
                "targetSettlement=" + (targetSettlement?.StringId ?? "-") +
                "; targetParty=" + (targetParty?.StringId ?? "-") +
                "; detail=" + detail);

        private static void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner) =>
            GwpAiDiagnostics.WriteHeroLifecycle(prisoner, "HERO_PRISONER_TAKEN",
                "capturer=" + (capturer?.MobileParty?.StringId ??
                    capturer?.Settlement?.StringId ?? "-"));

        internal static void RequestApproach(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Approach, Party = target,
                Priority = NormalizePriority(priority),
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestPursuit(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            if (IsDisposableEnforcementParty(party))
            {
                SetDirectAttackIntent(party, target, validHours);
                return;
            }

            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Pursue, Party = target,
                Priority = NormalizePriority(priority),
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        /// <summary>
        /// 全速赶到某支部队跟前。护送会跟着对方的步子走，办差的人不该这么慢；这里下注的是
        /// 原版用来直扑目标的 <see cref="AiBehavior.EngageParty"/>，由原版自己解算路径和速度。
        /// 仍然只是竞价里的一个候选——补给、疗伤这些原版欲望照常可以压过它。
        /// </summary>
        internal static void RequestRush(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Rush, Party = target,
                Priority = NormalizePriority(priority),
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestEscort(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Escort, Party = target,
                Priority = NormalizePriority(priority),
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestVisit(MobileParty party, Settlement target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Visit, Settlement = target,
                Priority = NormalizePriority(priority),
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void ClearIntent(MobileParty? party)
        {
            if (party == null) return;
            ReleaseDirectAttackLock(party);
            if (Intents.Remove(party.StringId)) RequestImmediateRethink(party);
        }

        internal static void RequestImmediateRethink(MobileParty? party)
        {
            if (party?.IsActive != true) return;

            if (DirectAttackLocks.Contains(party.StringId)) return;

            try
            {
                // 仅解除旧版本遗留冻结并要求原版重新拍卖；不清空原版短期战术，
                // 不写入目的地，也不人为重置进攻/逃跑判断。
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.RethinkAtNextHourlyTick = true;
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        /// <summary>
        /// 这支队伍此刻的差事就是全速赶到那支部队跟前。动作桥接靠它判断要不要把
        /// GoAroundParty 翻译成 EngageParty。
        /// </summary>
        internal static bool IsRushingTo(MobileParty? party, MobileParty? target)
        {
            if (party?.IsActive != true || target?.IsActive != true) return false;
            return Intents.TryGetValue(party.StringId, out Intent? intent) &&
                   intent.Kind == IntentKind.Rush &&
                   intent.ExpiresAt >= CampaignTime.Now.ToHours &&
                   intent.Party == target;
        }

        internal static bool IsAuthorizedAttackTarget(MobileParty? party, MobileParty? target)
        {
            if (!IsManagedParty(party) || target?.IsActive != true) return true;
            // 办差的队伍谁也不主动打，劫匪也不打。它是去送东西的，不是去清野的；
            // 挨打时原版照样自卫，只是不会自己追上去。
            if (GwpWardenDispatchBehavior.IsDispatchParty(party)) return false;
            if (target.IsBandit) return true;

            if (PoliceEnforcementBehavior.IsAuthorizedAssistanceTarget(party, target))
                return true;

            if (party != null && Intents.TryGetValue(party.StringId, out Intent? intent) &&
                intent.ExpiresAt >= CampaignTime.Now.ToHours &&
                (intent.Kind == IntentKind.Approach || intent.Kind == IntentKind.Pursue) &&
                intent.Party == target)
                return true;

            PoliceTask? task = party == null ? null : CrimePool.GetTask(party.StringId);
            return task?.TargetCrime?.Offender == target;
        }

        internal static bool TryGetLocationApproachTarget(MobileParty? party,
            out MobileParty? target)
        {
            target = null;
            if (!IsManagedParty(party) || party == null) return false;

            Intent? intent = ResolveIntent(party);
            if (intent?.Kind != IntentKind.Approach ||
                intent.Party?.IsActive != true)
                return false;

            target = intent.Party;
            return true;
        }

        /// <summary>
        /// 本队此刻是在用原版跟随追罪犯。原版会把这种状态显示成"正在跟随 XXX"，
        /// 对着一个通缉犯读起来像是在给他护航，所以大地图文字要另行改写。
        /// </summary>
        internal static bool TryGetPursuitEscortTarget(MobileParty? party,
            out MobileParty? target)
        {
            target = null;
            if (!IsManagedParty(party) || party == null) return false;

            Intent? intent = ResolveIntent(party);
            if (intent?.Kind != IntentKind.Escort || !intent.PursuesOffender ||
                intent.Party?.IsActive != true)
                return false;

            target = intent.Party;
            return true;
        }

        private static void SetIntent(MobileParty? party, Intent intent)
        {
            if (party?.IsActive != true) return;

            if (Intents.TryGetValue(party.StringId, out Intent? current) &&
                IsSameIntent(current, intent))
            {
                // 任务拥有者可以每小时续期，但不因此反复打断原版当前欲望。
                current.Priority = NormalizePriority(intent.Priority);
                current.ExpiresAt = intent.ExpiresAt;
                return;
            }

            Intents[party.StringId] = intent;
            RequestImmediateRethink(party);
        }

        private static void SetDirectAttackIntent(MobileParty? party,
            MobileParty? target, double validHours)
        {
            if (party?.IsActive != true || target?.IsActive != true || party == target)
                return;

            double expiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours);
            if (DirectAttackLocks.Contains(party.StringId) &&
                Intents.TryGetValue(party.StringId, out Intent? current) &&
                current.Kind == IntentKind.Pursue && current.Party == target)
            {
                // 正常小时维护只续期。只有地图战斗中断了追击，才补发一次命令。
                current.ExpiresAt = expiresAt;
                if (party.MapEvent == null &&
                    DirectAttackRefreshPending.Remove(party.StringId))
                    StartDirectAttack(party, target);
                return;
            }

            Intents[party.StringId] = new Intent
            {
                Kind = IntentKind.Pursue,
                Party = target,
                Priority = AssignedDutyScore,
                ExpiresAt = expiresAt
            };
            DirectAttackLocks.Add(party.StringId);
            StartDirectAttack(party, target);
        }

        private static void StartDirectAttack(MobileParty party, MobileParty target)
        {
            if (!party.IsActive || !target.IsActive || party == target ||
                party.MapEvent != null)
                return;

            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                // 恢复欲望整理前无英雄支援队的完整战术配置：正常进攻主动性、
                // 零逃避主动性，长期保持。它不产生任何战略欲望或强弱判断。
                party.Ai.SetInitiative(1f, 0f, 999f);
                ResolveNavigation(party, target,
                    out MobileParty.NavigationType navigation, out _);
                party.SetMoveEngageParty(target, navigation);
                party.Ai.SetDoNotMakeNewDecisions(true);
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        private static void ReleaseDirectAttackLock(MobileParty? party)
        {
            if (party == null) return;
            DirectAttackRefreshPending.Remove(party.StringId);
            if (!DirectAttackLocks.Remove(party.StringId)) return;
            if (!party.IsActive) return;

            try
            {
                party.Ai.SetDoNotMakeNewDecisions(false);
                party.Ai.RethinkAtNextHourlyTick = true;
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
        }

        private static bool IsDisposableEnforcementParty(MobileParty? party)
        {
            return party?.IsActive == true && party.LeaderHero == null &&
                   (GwpCommon.IsPatrolParty(party) ||
                    GwpCommon.IsEnforcementDelayPatrolParty(party));
        }

        private void OnHourlyTickParty(MobileParty party)
        {
            if (!IsManagedParty(party)) return;

            if (DirectAttackLocks.Contains(party.StringId))
            {
                if (!Intents.TryGetValue(party.StringId, out Intent? directIntent) ||
                    directIntent.Kind != IntentKind.Pursue ||
                    directIntent.ExpiresAt < CampaignTime.Now.ToHours ||
                    directIntent.Party?.IsActive != true)
                {
                    ReleaseDirectAttackLock(party);
                    Intents.Remove(party.StringId);
                }
                else if (party.MapEvent == null &&
                         DirectAttackRefreshPending.Remove(party.StringId))
                {
                    StartDirectAttack(party, directIntent.Party);
                }
                GwpAiDiagnostics.WriteState(party, "DIRECT_ATTACK_STATE");
                return;
            }

            try
            {
                // 旧存档清锁只做一次；正常运行不再每小时强制重算或清空短期 AI。
                if (party.Ai.DoNotMakeNewDecisions)
                    RequestImmediateRethink(party);
            }
            catch (Exception gwpQuietFailure) { GwpFaultTrace.WriteQuiet(gwpQuietFailure); }
            GwpAiDiagnostics.WriteState(party, "HOURLY_STATE");
        }

        /// <summary>
        /// Must run after CampaignEventDispatcher has invoked every native
        /// AiHourlyTick score producer. MbEvent listeners are LIFO, so merely
        /// registering this behavior last actually ran it first and made both
        /// filtering and raw-score diagnostics ineffective. The Harmony
        /// dispatcher postfix is the guaranteed final auction hook.
        /// </summary>
        internal static void ProcessFinalDesires(MobileParty party, PartyThinkParams think)
        {
            if (party == null || think == null || !IsManagedParty(party)) return;
            if (DirectAttackLocks.Contains(party.StringId)) return;

            RemoveExpired();
            List<(AIBehaviorData, float)> rawScores = think.AIBehaviorScores.ToList();
            foreach (var entry in rawScores)
            {
                if (entry.Item1.AiBehavior == AiBehavior.GoAroundParty && PlayerBountyBehavior.IsReservedTarget(entry.Item1.Party as MobileParty))
                {
                    AIBehaviorData candidate = entry.Item1;
                    think.SetBehaviorScore(in candidate, 0f);
                }
            }
            Intent? intent = ResolveIntent(party);
            TrackEscortSpeedUncap(party, intent);
            float originalPatrolCeiling = GetPatrolCeiling(rawScores);
            // 设计边界：只要本队当前存在任何有效任务意图，无论普通案件、
            // 协力组长、协力支援、练兵还是玩家委托，所有原版巡逻候选都必须
            // 压到最低执行阈值。这里不存在“保留巡逻”的任务类型；需要保留的
            // 是补给、疗伤、访问等非巡逻原版欲望及其原始分数。
            int suppressedPatrolCount = intent == null
                ? 0
                : SuppressAssignedPatrolScores(think, rawScores);
            // 无领主的派遣队会被原版每小时刷出一整排"进聚落"候选（实测每个聚落固定
            // 1.6 分，村庄、城堡、城镇全都有），那是原版给无主部队安排的归并/解散出路，
            // 不是补给欲望。它稳压 0.99 的差事分，于是使者一路钻进村里不走。
            // 办差期间把这类候选一并压到最低；我们自己为"进城办事"下的访问候选不在此列。
            // 原版 AiVisitSettlementBehavior 把真正的补给评分锁在
            // `leaderHero != null && IsLordParty` 之后；无领主队能拿到的只有
            // CalculateMergeScoreForDisbandingParty —— 归并/解散，不是补给。
            // 这排候选稳压 0.99 的差事分，所以**每一支**无领主办差队都要压，
            // 不只是使者。拦截队从前漏在门外，于是出去之后被原版拐进定居点，
            // 到了那里又被我们自己的 CurrentSettlement 分支销毁，连人带马一起没。
            if (intent != null && party.LeaderHero == null &&
                (GwpWardenDispatchBehavior.IsDispatchParty(party) ||
                 GwpCommon.IsEnforcementDelayPatrolParty(party) ||
                 GwpCommon.IsTrainingCohortParty(party)))
                suppressedPatrolCount += SuppressLeaderlessMergeScores(think, rawScores, intent);
            float patrolCeiling = GetPatrolCeiling(think.AIBehaviorScores);
            float dutyScore = intent == null
                ? 0f
                : NormalizePriority(intent.Priority);
            float minimumPositiveNonPatrolScore = GetMinimumPositiveNonPatrolScore(rawScores);
            int nonPatrolAtOrBelowDutyCount = intent == null ? 0 : rawScores.Count(entry =>
                entry.Item1.AiBehavior != AiBehavior.PatrolAroundPoint &&
                entry.Item2 > 0f && entry.Item2 <= dutyScore);
            string dutyAdded = "none";

            if (intent?.Kind == IntentKind.Approach && intent.Party?.IsActive == true)
            {
                if (PoliceEnforcementBehavior.IsPlayerEnforcementApproach(
                        party, intent.Party))
                {
                    // Player enforcement is still an ordinary score-1 desire,
                    // but its winning action is native EngageParty rather than
                    // a snapshot GoToPoint.  The action bridge below performs
                    // that translation only for this player warrant.
                    AddDutyCandidate(think, Create(party, intent.Party,
                        AiBehavior.GoAroundParty), PlayerEnforcementScore);
                    dutyAdded = "PlayerEnforcementEngage:" + intent.Party.StringId;
                }
                else
                {
                    // Other approach duties still travel to the target's last
                    // known position and retain their existing point behavior.
                    ResolveNavigation(party, intent.Party,
                        out MobileParty.NavigationType navigation,
                        out bool isFromPort);
                    AddDutyCandidate(think,
                        CreatePoint(intent.Party.Position, navigation, isFromPort),
                        dutyScore);
                    dutyAdded = "ApproachPoint:" + intent.Party.StringId;
                }
            }
            else if (intent?.Kind == IntentKind.Pursue && intent.Party?.IsActive == true)
            {
                // 宣战后切换为原版用于追逐敌军的 GoAroundParty。原版会把它落实
                // 为持续更新目标位置的短期移动，并继续自行决定逃跑或是否接战；
                // 不再误用面向友军的 EscortParty。
                AddDutyCandidate(think, Create(party, intent.Party, AiBehavior.GoAroundParty),
                    dutyScore);
                dutyAdded = "PursueParty:" + intent.Party.StringId;
            }
            else if (intent?.Kind == IntentKind.Rush && intent.Party?.IsActive == true)
            {
                // 原版 PartyHourlyAiTick 没有 EngageParty 的落地分支——它只为
                // PatrolAroundPoint / EscortParty / GoAroundParty 等几种赢家调用
                // SetPartyAiAction。直接下注 EngageParty 会赢了也不动，最后退回 Hold。
                // 所以沿用本仓库既有做法：下注有落地分支的 GoAroundParty，再由
                // GwpPlayerEnforcementEngageActionPatch 把这一次的动作翻译成原版 EngageParty。
                AddDutyCandidate(think, Create(party, intent.Party, AiBehavior.GoAroundParty),
                    dutyScore);
                dutyAdded = "RushParty:" + intent.Party.StringId;
            }
            else if (intent?.Kind == IntentKind.Escort && intent.Party?.IsActive == true)
            {
                AddDutyCandidate(think, Create(party, intent.Party, AiBehavior.EscortParty),
                    dutyScore);
                dutyAdded = "EscortParty:" + intent.Party.StringId;
            }
            else if (intent?.Kind == IntentKind.Visit && intent.Settlement != null)
            {
                AddDutyCandidate(think, Create(intent.Settlement, AiBehavior.GoToSettlement), dutyScore);
                dutyAdded = "VisitSettlement:" + intent.Settlement.StringId;
            }
            else if (intent?.Kind == IntentKind.Beat && intent.Settlement != null)
            {
                // 巡区用的就是原版巡逻行为，只是圆心由我们指定。原版那排巡逻候选
                // 已经被压到执行阈值，所以赢的一定是这一个。
                AddDutyCandidate(think,
                    Create(intent.Settlement, AiBehavior.PatrolAroundPoint), dutyScore);
                dutyAdded = "PatrolBeat:" + intent.Settlement.StringId;
            }

            GwpAiDiagnostics.WriteAuction(party, rawScores,
                think.AIBehaviorScores.ToList(), DescribeIntent(intent),
                originalPatrolCeiling, patrolCeiling, dutyScore,
                suppressedPatrolCount, minimumPositiveNonPatrolScore,
                nonPatrolAtOrBelowDutyCount, dutyAdded);
        }

        /// <summary>
        /// 记录本队当前正在执行灰袍自己的跟随差事。<c>MobileParty.CalculateSpeed</c>
        /// 的原版护送分支只会把跟随方**降速**到被跟随方的速度，从不提速；办差的人
        /// 被降到目标的步子上，就永远保持接手时的那段距离。解除封顶只是跟得更紧，
        /// 不改路径、不改交战语义。
        /// </summary>
        private static void TrackEscortSpeedUncap(MobileParty party, Intent? intent)
        {
            if (party?.StringId == null) return;
            if (intent?.Kind == IntentKind.Escort)
                EscortSpeedUncapped.Add(party.StringId);
            else
                EscortSpeedUncapped.Remove(party.StringId);
        }

        internal static bool ShouldUncapEscortSpeed(MobileParty? party) =>
            party?.StringId != null && EscortSpeedUncapped.Contains(party.StringId);

        private static string DescribeIntent(Intent? intent)
        {
            if (intent == null) return "none";
            return intent.Kind + ":" +
                (intent.Party?.StringId ?? intent.Settlement?.StringId ?? "-");
        }

        private static Intent? ResolveIntent(MobileParty party)
        {
            if (PlayerBountyBehavior.AwaitingSupportRequest(CrimePool.GetTask(party.StringId)))
                return new Intent { Kind = IntentKind.Escort, Party = MobileParty.MainParty,
                    Priority = PlayerRequestScore, ExpiresAt = double.MaxValue };
            if (PoliceEnforcementBehavior.TryGetAssistanceDuty(
                    party, out MobileParty? assistanceTarget,
                    out AiBehavior assistanceBehavior,
                    out bool playerBountyEscort,
                    out bool assistanceOffenderPursuit) &&
                assistanceTarget?.IsActive == true)
            {
                return new Intent
                {
                    Kind = assistanceBehavior == AiBehavior.EscortParty
                        ? IntentKind.Escort
                        : IntentKind.Pursue,
                    Party = assistanceTarget,
                    Priority = playerBountyEscort
                        ? PlayerRequestScore
                        : AssignedDutyScore,
                    ExpiresAt = double.MaxValue,
                    PursuesOffender = assistanceOffenderPursuit
                };
            }

            if (Intents.TryGetValue(party.StringId, out Intent? external) &&
                external.ExpiresAt >= CampaignTime.Now.ToHours && IsValid(external))
                return external.Kind == IntentKind.Pursue && PlayerBountyBehavior.IsReservedTarget(external.Party) ? null : external;

            PoliceTask? task = CrimePool.GetTask(party.StringId);
            if (task == null) return ResolvePatrolBeat(party);
            if (task.IsEscortingPlayer && task.EscortSettlement != null)
                return new Intent { Kind = IntentKind.Visit, Settlement = task.EscortSettlement,
                    Priority = AssignedDutyScore, ExpiresAt = double.MaxValue };

            // 必须通过案件的实时 Offender 解析目标。领主被俘、释放或重建部队后，
            // 保存的旧 PartyId 可能已经失效；只按旧 ID 搜索会让“已有承办人”的
            // 案件暂时被当成无职责，原版巡逻欲望便会重新出现。
            MobileParty? criminal = task.TargetCrime?.Offender;
            return criminal?.IsActive != true || PlayerBountyBehavior.IsReservedTarget(criminal)
                ? ResolvePatrolBeat(party)
                : new Intent {
                Kind = ResolveUndeclaredPursuitKind(task, criminal),
                Party = criminal, Priority = AssignedDutyScore,
                ExpiresAt = double.MaxValue, PursuesOffender = true };
        }

        /// <summary>
        /// 手上什么差事都没有时的巡区。
        ///
        /// 原版 `AiPatrollingBehavior` 的防御性巡逻分里有
        /// `avgTownDistance * 5f / max(distance(HomeSettlement, settlement), avgTownDistance)`
        /// ——**巡逻分与离家距离成反比**，而灰袍六人的 `HomeSettlement` 是同一个，
        /// 于是闲下来全挤在出生点附近，出了事要横穿半张地图。
        ///
        /// 这里不去改"家"（灰袍是流动编制：新人会加入、老人会老死，开档时钉死的
        /// 驻地很快就不对了），而是**每次解析时按当前在编名单现算**：把在编的灰袍
        /// 领主按 StringId 定序，各取一个不同文化的城做巡区圆心。人员一变，分配
        /// 自己就重排，永远不会两个人守同一处。
        ///
        /// 巡区分值与普通差事同档（0.99）：原版那排巡逻候选会被压到执行阈值，所以
        /// 赢的是巡区；而真正的补给／招兵需求（实测 1.0~8.0）照样压得过它，缺兵的
        /// 人仍然先去补兵。
        /// </summary>
        private static Intent? ResolvePatrolBeat(MobileParty party)
        {
            if (party.LeaderHero?.IsActive != true || !party.IsLordParty) return null;
            if (!string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            List<MobileParty> wardens = PoliceStats.GetAllPoliceParties()
                .Where(candidate => candidate?.IsActive == true &&
                    candidate.IsLordParty && candidate.LeaderHero?.IsActive == true)
                .OrderBy(candidate => candidate.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int index = wardens.FindIndex(candidate =>
                string.Equals(candidate.StringId, party.StringId,
                    StringComparison.OrdinalIgnoreCase));
            if (index < 0) return null;

            List<Settlement> beats = Town.AllTowns
                .Where(town => town?.Settlement != null && !town.IsCastle)
                .GroupBy(town => town.Settlement.Culture?.StringId ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(town => town.Settlement.StringId,
                        StringComparer.OrdinalIgnoreCase)
                    .First().Settlement)
                .ToList();
            if (beats.Count == 0) return null;

            return new Intent
            {
                Kind = IntentKind.Beat,
                Settlement = beats[index % beats.Count],
                Priority = AssignedDutyScore,
                ExpiresAt = double.MaxValue
            };
        }

        /// <summary>
        /// 未宣战阶段改用原版跟随。Approach 下注的是拍卖那一刻的位置快照，而原版
        /// <c>AiPartyThinkBehavior.PartyHourlyAiTick</c> 对普通领主队是 <c>HourCounter % 6</c>
        /// ——每 6 小时才重新拍卖一次，目标全速跑满这一轮足以甩出二十几格，承办人
        /// 于是一直走向对方六小时前的位置。<c>EscortParty</c> 由 <c>MobilePartyAi.Tick</c>
        /// 的短期周期（AiCheckInterval 0.25 × 0.6~0.7 ≈ 0.16 小时）持续刷新到目标真实
        /// 位置，且不含交战语义：<c>EncounterManager</c> 只对 <c>EngageParty</c> 发起遭遇，
        /// 跟随中立目标不会提前开战。宣战之后仍然交还 <c>GoAroundParty</c> 与原版 initiative。
        /// </summary>
        private static IntentKind ResolveUndeclaredPursuitKind(PoliceTask task,
            MobileParty criminal)
        {
            if (task.WarDeclared) return IntentKind.Pursue;
            return GwpCommon.IsShelteredOffender(criminal)
                ? IntentKind.Approach
                : IntentKind.Escort;
        }

        private static float NormalizePriority(float priority) =>
            priority > 0f && !float.IsNaN(priority) && !float.IsInfinity(priority)
                ? priority
                : AssignedDutyScore;

        private static float GetPatrolCeiling(
            IEnumerable<(AIBehaviorData, float)> scores)
        {
            float result = 0f;
            foreach ((AIBehaviorData behavior, float score) in scores)
                if (score > result && behavior.AiBehavior == AiBehavior.PatrolAroundPoint)
                    result = score;
            return result;
        }

        private static float GetMinimumPositiveNonPatrolScore(
            IEnumerable<(AIBehaviorData, float)> scores)
        {
            float result = float.MaxValue;
            foreach ((AIBehaviorData behavior, float score) in scores)
                if (behavior.AiBehavior != AiBehavior.PatrolAroundPoint &&
                    score > 0f && score < result)
                    result = score;
            return result == float.MaxValue ? 0f : result;
        }

        private static int SuppressAssignedPatrolScores(PartyThinkParams think,
            IEnumerable<(AIBehaviorData, float)> scores)
        {
            int changed = 0;
            foreach ((AIBehaviorData behavior, float score) in scores)
            {
                if (behavior.AiBehavior != AiBehavior.PatrolAroundPoint ||
                    score <= AssignedPatrolScoreCeiling)
                    continue;

                AIBehaviorData candidate = behavior;
                think.SetBehaviorScore(in candidate, AssignedPatrolScoreCeiling);
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// 压掉原版给无主部队安排的"进聚落归并"候选。只对办差中的派遣队生效，且放过
        /// 本队此刻真正要去的那个聚落——那是我们自己下的进城办事欲望。
        /// </summary>
        private static int SuppressLeaderlessMergeScores(PartyThinkParams think,
            IEnumerable<(AIBehaviorData, float)> scores, Intent intent)
        {
            Settlement? allowed = intent.Kind == IntentKind.Visit ? intent.Settlement : null;
            int changed = 0;
            foreach ((AIBehaviorData behavior, float score) in scores)
            {
                if (behavior.AiBehavior != AiBehavior.GoToSettlement ||
                    score <= AssignedPatrolScoreCeiling)
                    continue;
                if (allowed != null && ReferenceEquals(behavior.Party, allowed.Party))
                    continue;

                AIBehaviorData candidate = behavior;
                think.SetBehaviorScore(in candidate, AssignedPatrolScoreCeiling);
                changed++;
            }
            return changed;
        }

        private static AIBehaviorData Create(IMapPoint target, AiBehavior behavior) =>
            new AIBehaviorData(target, behavior, MobileParty.NavigationType.Default,
                false, false, false);

        private static AIBehaviorData Create(MobileParty owner, MobileParty target,
            AiBehavior behavior)
        {
            ResolveNavigation(owner, target, out MobileParty.NavigationType navigation,
                out bool isFromPort);
            return new AIBehaviorData(target, behavior, navigation,
                false, isFromPort, false);
        }

        private static AIBehaviorData CreatePoint(CampaignVec2 target,
            MobileParty.NavigationType navigationType, bool isFromPort) =>
            new AIBehaviorData(target, AiBehavior.PatrolAroundPoint,
                navigationType == MobileParty.NavigationType.None
                    ? MobileParty.NavigationType.Default
                    : navigationType,
                false, isFromPort, false);

        private static void ResolveNavigation(MobileParty owner, MobileParty target,
            out MobileParty.NavigationType navigationType, out bool isFromPort)
        {
            AiHelper.GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(
                owner, target, out navigationType, out _);
            if (navigationType == MobileParty.NavigationType.None)
                navigationType = owner.NavigationCapability;

            isFromPort = owner.CurrentSettlement?.HasPort == true &&
                !owner.IsCurrentlyAtSea && target.IsCurrentlyAtSea &&
                navigationType != MobileParty.NavigationType.Default;
        }

        private static void AddDutyCandidate(PartyThinkParams think,
            AIBehaviorData behavior, float score) =>
            // 即使原版恰好已经生成同目标、同行为的候选，也另加案件候选，
            // 绝不通过 SetBehaviorScore 改写原版元组。
            think.AddBehaviorScore((behavior, score));

        private static bool IsSameIntent(Intent left, Intent right) =>
            left.Kind == right.Kind && left.Party == right.Party && left.Settlement == right.Settlement;

        private static bool IsValid(Intent intent) => intent.Kind == IntentKind.Visit
            ? intent.Settlement != null : intent.Party?.IsActive == true;

        private static void RemoveExpired()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (string key in Intents.Where(x => x.Value.ExpiresAt < now || !IsValid(x.Value))
                .Select(x => x.Key).ToList())
            {
                MobileParty? party = MobileParty.All.FirstOrDefault(x =>
                    string.Equals(x.StringId, key, StringComparison.OrdinalIgnoreCase));
                ReleaseDirectAttackLock(party);
                Intents.Remove(key);
            }
        }

        private static bool IsManagedParty(MobileParty? party)
        {
            if (party?.IsActive != true || party.IsMainParty) return false;
            return Intents.ContainsKey(party.StringId) || GwpCommon.IsPatrolParty(party) ||
                GwpCommon.IsEnforcementDelayPatrolParty(party) ||
                string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HasDirectAttackLock(MobileParty? party) =>
            party?.IsActive == true && DirectAttackLocks.Contains(party.StringId);

        internal static string GetDiagnosticIntent(MobileParty? party)
        {
            if (party?.IsActive != true) return "none";
            return DescribeIntent(ResolveIntent(party));
        }

        internal static MobileParty? GetDiagnosticTargetParty(MobileParty? party)
        {
            if (party?.IsActive != true) return null;
            return ResolveIntent(party)?.Party;
        }

        internal static void RequestDirectAttackRefreshAfterBattle(
            MobileParty? party, MobileParty? target)
        {
            if (party?.IsActive != true || target?.IsActive != true) return;
            if (!DirectAttackLocks.Contains(party.StringId)) return;
            if (!Intents.TryGetValue(party.StringId, out Intent? intent) ||
                intent.Kind != IntentKind.Pursue || intent.Party != target)
                return;

            DirectAttackRefreshPending.Add(party.StringId);
        }
    }
}
