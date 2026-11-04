using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
#if GWP_DIAGNOSTICS
using GreyWardenPolicePurity;
#endif

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 灰袍模组状态的查询。模组内部成员是 internal 的，主 DLL 只在诊断版（GWP_DIAGNOSTICS）对本程序集开放，
    /// 所以非诊断构建一律回 unavailable（PROTOCOL.md §5 query）。
    /// 私有的进度表（使者 <c>_envoyCases</c>、在外小队 <c>_delayPatrolStates</c>）没有 internal 读取入口，
    /// 按字段名反射只读；字段改名时这里抛异常，按协议回 internal，不静默返回空表。
    /// </summary>
    internal static partial class BridgeQuery
    {
#if GWP_DIAGNOSTICS
        private static BridgeReply GreyWardenKind(string kind)
        {
            switch (kind)
            {
                case "cases":
                    return BridgeReply.Ok(new JObject { ["cases"] = Cases() });
                case "envoys":
                    return BridgeReply.Ok(Envoys());
                case "case_parties":
                    return BridgeReply.Ok(new JObject { ["case_parties"] = CaseParties() });
                default:
                    return BridgeReply.Ok(Sentinels());
            }
        }

        /// <summary>
        /// 案件池（<see cref="CrimePool.LedgerRecords"/>）里的每宗案卷，连同承办任务和使者进度。
        /// 玩家是否占案只经归属方 <see cref="GwpPlayerCaseAssignment"/> 读。
        /// </summary>
        private static JArray Cases()
        {
            var cases = new JArray();
            foreach (CrimeRecord record in CrimePool.LedgerRecords.OrderBy(r => r.CrimeId, StringComparer.Ordinal))
            {
                JArray tasks = new JArray(CrimePool.ActiveTasks.Values
                    .Where(t => string.Equals(t.TargetCrimeId, record.CrimeId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.PolicePartyId, StringComparer.Ordinal)
                    .Select(t => (object)BridgeSnapshot.Fields(t)).ToArray());
                PoliceEnforcementBehavior.EnvoyStatus? envoy = PoliceEnforcementBehavior.GetEnvoyStatus(record.CrimeId);
                cases.Add(new JObject
                {
                    ["id"] = record.CrimeId,
                    ["state"] = record.HasOpenCase ? "open" : "closed",
                    ["held_by_player"] = GwpPlayerCaseAssignment.IsCaseHeldByPlayer(record.OffenderHeroId),
                    ["tasks"] = tasks,
                    ["envoy"] = envoy.HasValue
                        ? new JObject
                        {
                            ["stage"] = envoy.Value.Stage.ToString(),
                            ["collect_only"] = envoy.Value.CollectOnly,
                            ["due_hours"] = envoy.Value.DueHours,
                            ["town"] = envoy.Value.TownId,
                        }
                        : null,
                    ["fields"] = BridgeSnapshot.Fields(record),
                });
            }

            return cases;
        }

        /// <summary>使者进度表（按案卷记，谈崩记录留到销案）和最近的谈判结果。</summary>
        private static JObject Envoys()
        {
            var envoys = new JArray();
            foreach (object envoyCase in PrivateTable("_envoyCases").Values)
            {
                JObject fields = BridgeSnapshot.Fields(envoyCase);
                MobileParty? party = FindParty(fields.Value<string>("EnvoyPartyId"));
                fields["party"] = party == null ? null : PartySnapshot(party);
                envoys.Add(fields);
            }

            var reports = new JArray(PoliceEnforcementBehavior.GetEnvoyReports()
                .Select(r => (object)BridgeSnapshot.Fields(r)).ToArray());
            return new JObject { ["envoys"] = envoys, ["reports"] = reports };
        }

        /// <summary>
        /// 主队派出的在外小队（<c>DelayPatrolState</c>）：城内办案队（<c>CityCrimeId</c> 非空）、即时追截队、
        /// 使者队、支援队都在这张表里，按原始字段区分，桥接不另作分类。
        /// </summary>
        private static JArray CaseParties()
        {
            var parties = new JArray();
            foreach (object state in PrivateTable("_delayPatrolStates").Values)
            {
                JObject fields = BridgeSnapshot.Fields(state);
                MobileParty? party = FindParty(fields.Value<string>("PatrolPartyId"));
                fields["party"] = party == null ? null : PartySnapshot(party);
                parties.Add(fields);
            }

            return parties;
        }

        /// <summary>
        /// 复发哨兵计数（docs/state/diagnostics.md「复发哨兵」）。主项目目前还没有哨兵登记表，
        /// 所以诊断版回空表；第一个哨兵落地时给它一个归属方，再在这里读。
        /// </summary>
        // TODO(哨兵落地后)：主项目建哨兵登记表后从它读 passes / triggers。
        private static JObject Sentinels() => new JObject
        {
            ["sentinels"] = new JObject(),
            ["note"] = "主项目尚无复发哨兵登记表",
        };

        private static IDictionary PrivateTable(string fieldName)
        {
            PoliceEnforcementBehavior behavior = Campaign.Current.GetCampaignBehavior<PoliceEnforcementBehavior>()
                ?? throw new InvalidOperationException("战役里没有 PoliceEnforcementBehavior");
            FieldInfo field = typeof(PoliceEnforcementBehavior).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(nameof(PoliceEnforcementBehavior), fieldName);
            return (IDictionary)field.GetValue(behavior);
        }

        /// <summary>部队的灰袍字段：身份、战力、差事占用、承办任务，各经归属方读取。</summary>
        static partial void AddGreyWardenPartyFields(JObject snapshot, MobileParty party)
        {
            PoliceTask? task = CrimePool.GetTask(party.StringId);
            snapshot["grey_warden"] = new JObject
            {
                ["clan_party"] = GwpCommon.IsGreyWardenClanParty(party),
                ["lord_party"] = GwpCommon.IsGreyWardenLordParty(party),
                ["enforcement_party"] = GwpCommon.IsGreyWardenEnforcementParty(party),
                ["strength"] = GwpStrength.Party(party),
                ["duties"] = new JArray(HeldDuties(party).Cast<object>().ToArray()),
                ["task"] = task == null ? null : BridgeSnapshot.Fields(task),
            };
        }

        /// <summary>
        /// 差事占用只问归属方 <see cref="GwpWardenDuties.HoldsAny"/>；可问的差事取它私有的 Holders 表的键，
        /// 不在桥接里另抄一份清单。
        /// </summary>
        private static string[] HeldDuties(MobileParty party)
        {
            FieldInfo field = typeof(GwpWardenDuties).GetField("Holders", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(nameof(GwpWardenDuties), "Holders");
            return ((IDictionary)field.GetValue(null)).Keys.Cast<WardenDuty>()
                .Where(duty => GwpWardenDuties.HoldsAny(party, duty))
                .Select(duty => duty.ToString())
                .ToArray();
        }
#else
        private static BridgeReply GreyWardenKind(string kind) =>
            BridgeReply.Fail("unavailable", "query kind " + kind + " 只在诊断版构建里可用（主 DLL 只在诊断版开放内部成员）");
#endif
    }
}
