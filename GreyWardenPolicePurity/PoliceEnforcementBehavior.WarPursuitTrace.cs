#if GWP_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 宣战追捕为什么两天还抓不到（用户 2026-10-08：两天支援队是保底，宣战两天还没落网很奇怪，要看清原因）。
    /// 已宣战、有灰袍承办的案子，从这一局第一次看到它在宣战追捕算起，每满 24 小时记一条 WAR_PURSUIT_STALLED：
    /// 双方速度、距离（和上一条比）、战力、罪犯在不在聚落 / 军团 / 战斗 / 围城、双方此刻的原版行为，
    /// 以及还在外面的追截队、支援队。只读，不改任何东西；起算时间不存档，读档后从读档那一刻重新算。
    /// </summary>
    public partial class PoliceEnforcementBehavior
    {
        private sealed class WarPursuitWatch
        {
            public double Since;
            public float LastDistance = -1f;
            public int Reported;
            public string InterceptorGate = "not_observed";
        }

        private readonly Dictionary<string, WarPursuitWatch> _warPursuitWatch =
            new Dictionary<string, WarPursuitWatch>(StringComparer.OrdinalIgnoreCase);

        private void TraceInterceptorGate(MobileParty source, MobileParty target, string reason)
        {
            if (_warPursuitWatch.TryGetValue(source.StringId + ">" + target.StringId, out WarPursuitWatch? watch))
                watch.InterceptorGate = reason;
            else if (CrimeState.GetTask(source.StringId)?.WarDeclared == false)
                GwpAiDiagnostics.WriteAction(source, "CASE_DECLARATION_WAITING_INTERCEPTOR",
                    "target=" + target.StringId + "; reason=" + reason);
        }

        private void TraceStalledWarPursuits()
        {
            double now = CampaignTime.Now.ToHours;
            HashSet<string> live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (task.FlowState != PoliceTaskFlowState.WarPursuit || string.IsNullOrEmpty(task.PolicePartyId))
                    continue;
                MobileParty? offender = task.TargetCrime?.Offender;
                MobileParty? police = MobileParty.All.FirstOrDefault(party =>
                    string.Equals(party.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase));
                if (offender?.IsActive != true || police?.IsActive != true)
                    continue;
                string key = police.StringId + ">" + offender.StringId;
                live.Add(key);
                if (!_warPursuitWatch.TryGetValue(key, out WarPursuitWatch? watch))
                {
                    _warPursuitWatch[key] = new WarPursuitWatch { Since = now };
                    continue;
                }
                int due = (int)((now - watch.Since) / 24d);
                if (due <= watch.Reported)
                    continue;
                watch.Reported = due;
                float distance = police.GetPosition2D.Distance(offender.GetPosition2D);
                float armyStrength = police.Army == null
                    ? GwpStrength.Party(police)
                    : police.Army.Parties.Sum(GwpStrength.Party);
                GwpAiDiagnostics.WriteAction(police, "WAR_PURSUIT_STALLED",
                    "target=" + offender.StringId + "; hours=" + (now - watch.Since).ToString("0") +
                    "; distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; lastDistance=" + watch.LastDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                    "; lastInterceptorGate=" + watch.InterceptorGate +
                    "; policeSpeed=" + police.Speed.ToString("0.00", CultureInfo.InvariantCulture) +
                    "; targetSpeed=" + offender.Speed.ToString("0.00", CultureInfo.InvariantCulture) +
                    "; policeStrength=" + armyStrength.ToString("0", CultureInfo.InvariantCulture) +
                    "; targetStrength=" + GwpStrength.CaseTarget(task.TargetCrime).ToString("0", CultureInfo.InvariantCulture) +
                    "; policeArmy=" + (police.Army?.LeaderParty?.StringId ?? "-") +
                    "; targetSettlement=" + (offender.CurrentSettlement?.StringId ?? "-") +
                    "; targetArmy=" + (offender.Army?.LeaderParty?.StringId ?? "-") +
                    "; targetMapEvent=" + (offender.MapEvent?.EventType.ToString() ?? "-") +
                    "; targetSiege=" + (offender.BesiegedSettlement?.StringId ?? "-") +
                    "; targetBehavior=" + offender.DefaultBehavior +
                    "; policeBehavior=" + police.DefaultBehavior +
                    "; policeTargetParty=" + (police.TargetParty?.StringId ?? "-") +
                    "; policeTargetSettlement=" + (police.TargetSettlement?.StringId ?? "-") +
                    "; liveInterceptors=" + _delayPatrolStates.Values.Count(state =>
                        state.IsImmediateInterceptor && state.TargetPartyId == offender.StringId) +
                    "; liveSupports=" + _delayPatrolStates.Values.Count(state =>
                        !state.IsImmediateInterceptor && state.TargetPartyId == offender.StringId));
                watch.LastDistance = distance;
            }
            foreach (string key in _warPursuitWatch.Keys.Where(key => !live.Contains(key)).ToList())
                _warPursuitWatch.Remove(key);
        }
    }
}
#endif
