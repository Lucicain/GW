using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍大地图职责接入层：有案件时只把原版巡逻候选压到原版可执行阈值，
    /// 再加入固定为 0.99 的案件候选。除巡逻外的全部原版欲望和分数保持不变；
    /// 低于 0.99 的普通访问会让位于办案，高于 0.99 的补给、招兵、交易、
    /// 疗伤、交俘、修船及安全需求继续由原版优先执行。无案件时完全不改竞价。
    /// </summary>
    public sealed class GreyWardenPartyDesireBehavior : CampaignBehaviorBase
    {
        private enum IntentKind { Approach, Pursue, Escort, Visit }

        private sealed class Intent
        {
            public IntentKind Kind;
            public MobileParty? Party;
            public Settlement? Settlement;
            public double ExpiresAt;
        }

        // AiPartyThinkBehavior 对 PatrolAroundPoint 的原版执行阈值是 0.03。
        // 办案期间只把高于该值的巡逻候选封顶到这里。
        private const float AssignedPatrolScoreCeiling = 0.03f;
        // 1.4.7 实测的普通原版进城候选多在 0.35～0.85，明确维护需求从约 1
        // 开始并可在缺粮/重伤时升至 3.7～19.6。固定 0.99 让低分日常访问
        // 让位于案件，同时保持所有较强的原版维护需求优先。
        private const float AssignedDutyScore = 0.99f;
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

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                Intents.Clear();
                DirectAttackLocks.Clear();
                DirectAttackRefreshPending.Clear();
            }
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            _ = starter;
            DirectAttackLocks.Clear();
            DirectAttackRefreshPending.Clear();
            GwpAiDiagnostics.StartSession();
            // 旧存档可能仍保留上一版本已经选中的巡逻目标。只要求原版在
            // 下一次部分小时 AI tick 重新拍卖，不直接写入任何目的地。
            foreach (MobileParty party in MobileParty.All.Where(IsManagedParty))
                RequestImmediateRethink(party);
        }

        internal static void RequestApproach(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            _ = priority; // 保留旧调用签名；职责统一按固定案件分参与原版拍卖。
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Approach, Party = target,
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestPursuit(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            _ = priority;
            if (IsDisposableEnforcementParty(party))
            {
                SetDirectAttackIntent(party, target, validHours);
                return;
            }

            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Pursue, Party = target,
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestEscort(MobileParty party, MobileParty target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            _ = priority;
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Escort, Party = target,
                ExpiresAt = CampaignTime.Now.ToHours + Math.Max(2d, validHours) });
        }

        internal static void RequestVisit(MobileParty party, Settlement target,
            float priority = AssignedDutyScore, double validHours = 8d)
        {
            _ = priority;
            ReleaseDirectAttackLock(party);
            SetIntent(party, new Intent { Kind = IntentKind.Visit, Settlement = target,
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
            catch { }
        }

        internal static bool IsAuthorizedAttackTarget(MobileParty? party, MobileParty? target)
        {
            if (!IsManagedParty(party) || target?.IsActive != true) return true;
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

        private static void SetIntent(MobileParty? party, Intent intent)
        {
            if (party?.IsActive != true) return;

            if (Intents.TryGetValue(party.StringId, out Intent? current) &&
                IsSameIntent(current, intent))
            {
                // 任务拥有者可以每小时续期，但不因此反复打断原版当前欲望。
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
            catch { }
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
            catch { }
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
            catch { }
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
            Intent? intent = ResolveIntent(party);
            float originalPatrolCeiling = GetPatrolCeiling(rawScores);
            int suppressedPatrolCount = intent == null
                ? 0
                : SuppressAssignedPatrolScores(think, rawScores);
            float patrolCeiling = GetPatrolCeiling(think.AIBehaviorScores);
            float dutyScore = intent == null ? 0f : AssignedDutyScore;
            float minimumPositiveNonPatrolScore = GetMinimumPositiveNonPatrolScore(rawScores);
            int nonPatrolAtOrBelowDutyCount = intent == null ? 0 : rawScores.Count(entry =>
                entry.Item1.AiBehavior != AiBehavior.PatrolAroundPoint &&
                entry.Item2 > 0f && entry.Item2 <= dutyScore);
            string dutyAdded = "none";

            if (intent?.Kind == IntentKind.Approach && intent.Party?.IsActive == true)
            {
                // 原版 AiEngagePartyBehavior 只扫描本队附近约“最大接触距离×45”
                // 的可定位部队，并不提供横跨大陆的目标搜索。远距离阶段因此不
                // 直接跟随一个可能不在感知范围内的 Party，而是把罪犯本小时的
                // 已知坐标作为地点候选送进同一场原版欲望拍卖。
                ResolveNavigation(party, intent.Party, out MobileParty.NavigationType navigation,
                    out bool isFromPort);
                AddDutyCandidate(think, CreatePoint(intent.Party.Position, navigation, isFromPort),
                    dutyScore);
                dutyAdded = "ApproachPoint:" + intent.Party.StringId;
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

            GwpAiDiagnostics.WriteAuction(party, rawScores,
                think.AIBehaviorScores.ToList(), DescribeIntent(intent),
                originalPatrolCeiling, patrolCeiling, dutyScore,
                suppressedPatrolCount, minimumPositiveNonPatrolScore,
                nonPatrolAtOrBelowDutyCount, dutyAdded);
        }

        private static string DescribeIntent(Intent? intent)
        {
            if (intent == null) return "none";
            return intent.Kind + ":" +
                (intent.Party?.StringId ?? intent.Settlement?.StringId ?? "-");
        }

        private static Intent? ResolveIntent(MobileParty party)
        {
            if (Intents.TryGetValue(party.StringId, out Intent? external) &&
                external.ExpiresAt >= CampaignTime.Now.ToHours && IsValid(external))
                return external;

            PoliceTask? task = CrimePool.GetTask(party.StringId);
            if (task == null) return null;
            if (task.IsEscortingPlayer && task.EscortSettlement != null)
                return new Intent { Kind = IntentKind.Visit, Settlement = task.EscortSettlement,
                    ExpiresAt = double.MaxValue };

            // 必须通过案件的实时 Offender 解析目标。领主被俘、释放或重建部队后，
            // 保存的旧 PartyId 可能已经失效；只按旧 ID 搜索会让“已有承办人”的
            // 案件暂时被当成无职责，原版巡逻欲望便会重新出现。
            MobileParty? criminal = task.TargetCrime?.Offender;
            return criminal?.IsActive != true ? null : new Intent {
                Kind = task.WarDeclared ? IntentKind.Pursue : IntentKind.Approach,
                Party = criminal, ExpiresAt = double.MaxValue };
        }

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
