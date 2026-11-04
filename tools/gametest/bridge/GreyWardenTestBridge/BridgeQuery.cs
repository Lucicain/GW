using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 只读查询（PROTOCOL.md §5 query）。不改游戏状态：只读字段和原版的普通属性，不调会更新状态的评分方法。
    /// 灰袍的共享概念（谁算灰袍、战力、差事占用、案件状态）只经归属方的唯一入口读取，桥接不另写判断。
    /// </summary>
    internal static partial class BridgeQuery
    {
        private static readonly string[] Kinds = { "kinds", "party", "cases", "envoys", "case_parties", "sentinels" };

        internal static BridgeReply Run(JObject args, BridgeGameState game)
        {
            JToken? kindToken = args["kind"];
            if (kindToken == null || kindToken.Type != JTokenType.String)
                return BridgeReply.Fail("bad_request", "query 需要字符串参数 kind");
            string kind = kindToken.Value<string>()!;
            JToken? innerToken = args["args"];
            JObject inner = innerToken as JObject ?? new JObject();
            if (innerToken != null && innerToken.Type != JTokenType.Null && !(innerToken is JObject))
                return BridgeReply.Fail("bad_request", "query 的 args 必须是对象");

            if (kind == "kinds") return BridgeReply.Ok(new JObject { ["kinds"] = new JArray(Kinds.Cast<object>().ToArray()) });
            if (!Kinds.Contains(kind)) return BridgeReply.Fail("unknown_kind", "桥接不认识 query kind " + kind);
            // 哨兵计数按本次启动累计，不依赖战役；其他 kind 要有战役。
            if (kind != "sentinels" && (!game.HasLiveCampaign || Campaign.Current == null))
                return BridgeReply.Fail("invalid_state", "没有进行中的战役，当前 " + game.Describe());

            switch (kind)
            {
                case "party":
                    return Party(inner);
                default:
                    return GreyWardenKind(kind);
            }
        }

        private static BridgeReply Party(JObject args)
        {
            JToken? idToken = args["id"];
            if (idToken == null || idToken.Type != JTokenType.String)
                return BridgeReply.Fail("bad_request", "party 需要字符串参数 id（部队 StringId）");
            string id = idToken.Value<string>()!;
            MobileParty? party = FindParty(id);
            if (party == null) return BridgeReply.Fail("not_found", "没有部队 " + id);
            return BridgeReply.Ok(new JObject { ["party"] = PartySnapshot(party) });
        }

        internal static MobileParty? FindParty(string? id) =>
            string.IsNullOrEmpty(id)
                ? null
                : MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, id, StringComparison.Ordinal));

        /// <summary>单个部队：位置、人数、行为、目标。只读原版属性。</summary>
        internal static JObject PartySnapshot(MobileParty party)
        {
            var snapshot = new JObject
            {
                ["id"] = party.StringId,
                ["name"] = party.Name?.ToString(),
                ["is_active"] = party.IsActive,
                ["is_main_party"] = party.IsMainParty,
                ["position"] = BridgeSnapshot.Value(party.GetPosition2D),
                ["at_sea"] = party.IsCurrentlyAtSea,
                ["is_moving"] = party.IsMoving,
                ["leader_hero"] = party.LeaderHero?.StringId,
                ["clan"] = party.ActualClan?.StringId,
                ["map_faction"] = party.MapFaction?.StringId,
                ["members"] = new JObject
                {
                    ["total"] = party.MemberRoster.TotalManCount,
                    ["healthy"] = party.MemberRoster.TotalHealthyCount,
                    ["wounded"] = party.MemberRoster.TotalWounded,
                    ["heroes"] = party.MemberRoster.TotalHeroes,
                },
                ["prisoners"] = party.PrisonRoster.TotalManCount,
                ["default_behavior"] = party.DefaultBehavior.ToString(),
                ["short_term_behavior"] = party.ShortTermBehavior.ToString(),
                ["target_settlement"] = party.TargetSettlement?.StringId,
                ["target_party"] = party.TargetParty?.StringId,
                ["short_term_target_party"] = party.ShortTermTargetParty?.StringId,
                ["target_position"] = BridgeSnapshot.Value(party.TargetPosition),
                ["current_settlement"] = party.CurrentSettlement?.StringId,
                ["besieged_settlement"] = party.BesiegedSettlement?.StringId,
                ["in_map_event"] = party.MapEvent != null,
                ["army_leader_party"] = party.Army?.LeaderParty?.StringId,
            };
            AddGreyWardenPartyFields(snapshot, party);
            return snapshot;
        }

        static partial void AddGreyWardenPartyFields(JObject snapshot, MobileParty party);
    }
}
