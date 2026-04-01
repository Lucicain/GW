using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal static class GwpPoliceWarReasonService
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;

        private sealed class FactionReasonBucket
        {
            public FactionReasonBucket(IFaction faction)
            {
                Faction = faction;
            }

            public IFaction Faction { get; }
            public List<string> Details { get; } = new List<string>();
        }

        public static bool SupportsClan(Clan? clan)
        {
            return clan != null &&
                   string.Equals(clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildInquiryTitle(Clan? clan)
        {
            string clanName = clan?.Name?.ToString() ?? "灰袍守卫";
            return $"{clanName}当前宣战详情";
        }

        public static string BuildInquiryBody(Clan? clan)
        {
            if (!SupportsClan(clan))
                return "只有灰袍守卫家族页会显示宣战详情。";

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return "未找到灰袍守卫家族，无法读取宣战原因。";

            Dictionary<string, FactionReasonBucket> buckets = CollectCurrentWarReasons(policeClan);
            if (buckets.Count == 0)
                return "灰袍守卫当前没有正式宣战对象。";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"当前正式宣战对象：{buckets.Count} 个");
            sb.AppendLine();

            bool first = true;
            foreach (FactionReasonBucket bucket in buckets.Values.OrderBy(static b => b.Faction.Name?.ToString() ?? string.Empty))
            {
                if (!first)
                    sb.AppendLine();

                first = false;
                sb.AppendLine($"【{bucket.Faction.Name}】");

                foreach (string detail in bucket.Details.Distinct(StringComparer.Ordinal))
                    sb.AppendLine(detail);
            }

            return sb.ToString().TrimEnd();
        }

        public static bool HasLegitimateWarReason(IFaction? targetFaction)
        {
            if (targetFaction == null) return false;

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return false;
            if (!FactionManager.IsAtWarAgainstFaction(policeClan, targetFaction)) return false;

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                if (TaskMatchesFaction(task, targetFaction))
                    return true;
            }

            PlayerBountyBehavior? bountyBehavior = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bountyBehavior?.HasActiveBountyWarForFaction(targetFaction) == true)
                return true;

            PolicePatrolBehavior? patrolBehavior = Campaign.Current?.GetCampaignBehavior<PolicePatrolBehavior>();
            if (patrolBehavior?.HasActivePatrolWarForFaction(targetFaction) == true)
                return true;

            return false;
        }

        public static IEnumerable<IFaction> GetCurrentPoliceWarFactions(Clan policeClan)
        {
            return GetCurrentWarFactions(policeClan);
        }

        private static Dictionary<string, FactionReasonBucket> CollectCurrentWarReasons(Clan policeClan)
        {
            var buckets = new Dictionary<string, FactionReasonBucket>(StringComparer.OrdinalIgnoreCase);

            foreach (PoliceTask task in CrimeState.ActiveTasks.Values)
            {
                MobileParty? offender = task.TargetCrime?.Offender;
                IFaction? targetFaction = task.WarTarget ?? offender?.ActualClan?.MapFaction;
                if (targetFaction == null) continue;
                if (!FactionManager.IsAtWarAgainstFaction(policeClan, targetFaction)) continue;

                AddFactionReason(buckets, targetFaction, BuildTaskReasonDetail(task));
            }

            PlayerBountyBehavior? bountyBehavior = Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
            if (bountyBehavior != null)
            {
                foreach (IFaction faction in GetCurrentWarFactions(policeClan))
                {
                    string? detail = bountyBehavior.BuildActiveBountyWarReasonDetails(faction);
                    if (!string.IsNullOrWhiteSpace(detail))
                        AddFactionReason(buckets, faction, detail);
                }
            }

            PolicePatrolBehavior? patrolBehavior = Campaign.Current?.GetCampaignBehavior<PolicePatrolBehavior>();
            if (patrolBehavior != null)
            {
                IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction != null && FactionManager.IsAtWarAgainstFaction(policeClan, playerFaction))
                {
                    string? detail = patrolBehavior.BuildPatrolWarReasonDetails(playerFaction);
                    if (!string.IsNullOrWhiteSpace(detail))
                        AddFactionReason(buckets, playerFaction, detail);
                }
            }

            foreach (IFaction faction in GetCurrentWarFactions(policeClan))
            {
                if (!buckets.ContainsKey(faction.StringId))
                {
                    AddFactionReason(
                        buckets,
                        faction,
                        "当前检测到正式战争状态，但运行时任务池里没有直接案由。通常说明战斗刚结束、正在等待自动停战，或这是旧流程留下的临时战争。");
                }
            }

            return buckets;
        }

        private static bool TaskMatchesFaction(PoliceTask task, IFaction targetFaction)
        {
            if (task == null || targetFaction == null) return false;

            if (task.WarTarget != null &&
                string.Equals(task.WarTarget.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return true;

            MobileParty? offender = task.TargetCrime?.Offender;
            IFaction? offenderFaction = offender?.ActualClan?.MapFaction;
            if (offenderFaction != null &&
                string.Equals(offenderFaction.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (offender?.IsMainParty == true)
            {
                IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
                if (playerFaction != null &&
                    string.Equals(playerFaction.StringId, targetFaction.StringId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<IFaction> GetCurrentWarFactions(Clan policeClan)
        {
            var results = new Dictionary<string, IFaction>(StringComparer.OrdinalIgnoreCase);

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;
                TryAddWarFaction(results, policeClan, kingdom);
            }

            foreach (Clan clan in Clan.All)
            {
                if (clan == null || clan == policeClan || clan.IsEliminated) continue;
                if (clan.Kingdom != null) continue;
                TryAddWarFaction(results, policeClan, clan);
            }

            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;
            if (playerFaction != null)
                TryAddWarFaction(results, policeClan, playerFaction);

            return results.Values;
        }

        private static void TryAddWarFaction(
            IDictionary<string, IFaction> results,
            Clan policeClan,
            IFaction candidate)
        {
            if (candidate == null) return;
            if (candidate == policeClan || candidate == policeClan.MapFaction) return;
            if (candidate is Clan clanCandidate && clanCandidate.IsOutlaw && clanCandidate.IsBanditFaction) return;
            if (candidate.IsBanditFaction) return;
            if (!FactionManager.IsAtWarAgainstFaction(policeClan, candidate)) return;
            if (string.IsNullOrEmpty(candidate.StringId)) return;

            results[candidate.StringId] = candidate;
        }

        private static void AddFactionReason(
            IDictionary<string, FactionReasonBucket> buckets,
            IFaction faction,
            string detail)
        {
            if (faction == null || string.IsNullOrWhiteSpace(detail) || string.IsNullOrEmpty(faction.StringId))
                return;
            if (faction is Clan clanFaction && clanFaction.IsOutlaw && clanFaction.IsBanditFaction)
                return;
            if (faction.IsBanditFaction)
                return;

            if (!buckets.TryGetValue(faction.StringId, out FactionReasonBucket? bucket))
            {
                bucket = new FactionReasonBucket(faction);
                buckets[faction.StringId] = bucket;
            }

            bucket.Details.Add(detail.Trim());
        }

        private static string BuildTaskReasonDetail(PoliceTask task)
        {
            CrimeRecord? crime = task.TargetCrime;
            MobileParty? offender = crime?.Offender;

            string policePartyName = ResolvePartyName(task.PolicePartyId, "未记录的执法队");
            string offenderName = offender?.Name?.ToString() ?? "未知目标";
            string actionType = GetActionType(task, offender);
            string crimeType = string.IsNullOrWhiteSpace(crime?.CrimeType) ? "未记录" : crime.CrimeType;
            string victimName = string.IsNullOrWhiteSpace(crime?.VictimName) ? "未记录" : crime.VictimName;
            string occurredTime = crime != null ? FormatElapsedSince(crime.OccurredTime) : "未知";
            string location = crime != null ? FormatLocation(crime.Location) : "未知";
            string stage = DescribeTaskStage(task);

            return $"{actionType}：{policePartyName} 正在处理 {offenderName} 的案件。案由：{crimeType}；受害方：{victimName}；立案时间：{occurredTime}；案发地点：{location}；当前阶段：{stage}。";
        }

        private static string GetActionType(PoliceTask task, MobileParty? offender)
        {
            if (task.IsPlayerBountyEscort)
                return "玩家悬赏协同";

            if (offender?.IsMainParty == true)
                return "玩家案件执法";

            if (task.WarDeclared)
                return "跨势力追捕";

            return "执法任务";
        }

        private static string DescribeTaskStage(PoliceTask task)
        {
            if (task.IsPlayerBountyEscort)
                return "灰袍部队正在护送玩家追捕目标";

            if (task.IsEscortingPlayer)
                return "目标已被击败，正在押送玩家";

            if (task.WarDeclared)
                return "已正式宣战并持续追击";

            if (task.TargetCrime != null)
                return "已立案追踪，尚未升级为正式战争";

            return "未记录";
        }

        private static string ResolvePartyName(string? partyId, string fallback)
        {
            if (!string.IsNullOrEmpty(partyId))
            {
                MobileParty? party = MobileParty.All.FirstOrDefault(p =>
                    p != null &&
                    string.Equals(p.StringId, partyId, StringComparison.OrdinalIgnoreCase));
                if (party != null)
                    return party.Name?.ToString() ?? fallback;
            }

            return fallback;
        }

        private static string FormatElapsedSince(CampaignTime occurredTime)
        {
            float days = (float)(CampaignTime.Now - occurredTime).ToDays;
            if (days < (1f / CampaignTime.HoursInDay))
                return "刚刚";

            if (days < 1f)
                return $"{days * CampaignTime.HoursInDay:0.#} 小时前";

            return $"{days:0.##} 天前";
        }

        private static string FormatLocation(Vec2 position)
        {
            Settlement? nearestTown = GwpCommon.FindNearestTown(position);
            if (nearestTown != null)
                return $"{nearestTown.Name}附近 ({position.x:0.0}, {position.y:0.0})";

            return $"野外 ({position.x:0.0}, {position.y:0.0})";
        }
    }
}
