using System;
using System.Linq;
using GreyWardenPolicePurity;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GreyWardenTestBridge
{
    /// <summary>
    /// 搭场景（PROTOCOL.md §5 act）：用原版动作造出某个玩法的前提条件，之后的派队、补派、结案都由
    /// 游戏和灰袍的真实逻辑自己决定。和只读的 query 分开：act 会改战役状态，只该在测试存档上用。
    /// </summary>
    internal static class BridgeAct
    {
        private static readonly string[] Kinds = { "kinds", "shelter_offender", "destroy_party" };

        internal static BridgeReply Run(JObject args, BridgeGameState game)
        {
            string? kind = args.Value<string>("kind");
            JObject inner = args["args"] as JObject ?? new JObject();
            if (kind == "kinds") return BridgeReply.Ok(new JObject { ["kinds"] = new JArray(Kinds.Cast<object>().ToArray()) });
            if (kind == null || !Kinds.Contains(kind)) return BridgeReply.Fail("unknown_kind", "桥接不认识 act kind " + kind);
            if (!game.HasLiveCampaign || Campaign.Current == null)
                return BridgeReply.Fail("invalid_state", "没有进行中的战役，当前 " + game.Describe());
            return kind == "shelter_offender" ? ShelterOffender(inner) : DestroyParty(inner);
        }

        /// <summary>
        /// 让一宗开着的 NPC 案子的罪犯离开部队、住进离他最近的一座对灰袍中立的城，
        /// 即「无部队住城旧案」（docs/state/city-search.md）。默认挑没有承办任务的案子；
        /// 用原版 <c>TeleportHeroAction.ApplyImmediateTeleportToSettlement</c>，它会把英雄移出原部队。
        /// </summary>
        private static BridgeReply ShelterOffender(JObject args)
        {
            string? crimeId = args.Value<string>("crime_id");
            Clan? police = PoliceStats.GetPoliceClan();
            if (police == null) return BridgeReply.Fail("invalid_state", "战役里没有灰袍家族");
            CrimeRecord? record = CrimePool.LedgerRecords.FirstOrDefault(r => crimeId != null
                ? r.CrimeId == crimeId
                : r.HasOpenCase && r.CrimeId != CrimePool.PlayerCrimeId && Eligible(r.OffenderHero)
                  && !CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == r.CrimeId));
            Hero? hero = record?.OffenderHero;
            if (record == null || !Eligible(hero))
            {
                var open = new JArray(CrimePool.LedgerRecords.Where(r => r.HasOpenCase && r.CrimeId != CrimePool.PlayerCrimeId)
                    .Select(r => (object)(r.CrimeId + ": " + Why(r.OffenderHero)
                        + (CrimePool.ActiveTasks.Values.Any(task => task.TargetCrimeId == r.CrimeId) ? "，有承办任务" : string.Empty)))
                    .ToArray());
                return BridgeReply.Fail("not_found", crimeId == null ? "没有可用的开放 NPC 案子（罪犯须活着、未被俘、在部队里、不在战斗中，且案子没有承办任务）"
                    : "案子 " + crimeId + " 的罪犯不满足条件", new JObject { ["open_cases"] = open });
            }
            var from = hero!.PartyBelongedTo!.GetPosition2D;
            Settlement? town = Settlement.All
                .Where(s => s.IsTown && s.MapFaction != null && s.SiegeEvent == null
                            && !FactionManager.IsAtWarAgainstFaction(police, s.MapFaction))
                .OrderBy(s => s.GetPosition2D.Distance(from)).FirstOrDefault();
            if (town == null) return BridgeReply.Fail("not_found", "没有对灰袍中立的城镇");
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, town);
            return BridgeReply.Ok(new JObject
            {
                ["crime_id"] = record.CrimeId,
                ["hero"] = hero.StringId,
                ["town"] = town.StringId,
                ["searchable"] = record.IsOffenderSearchableInSettlement(),
            });
        }

        private static string Why(Hero? hero) =>
            hero == null ? "没有罪犯英雄" : !hero.IsAlive ? "已死" : hero.IsPrisoner ? "被俘" : hero == Hero.MainHero ? "是玩家"
            : GwpCommon.IsGreyWardenClanMember(hero) ? "是灰袍成员" : hero.PartyBelongedTo == null ? "不在部队里"
            : !hero.PartyBelongedTo.IsActive ? "部队已失效" : hero.PartyBelongedTo.MapEvent != null ? "正在战斗" : "可用";

        private static bool Eligible(Hero? hero) =>
            hero != null && hero.IsAlive && !hero.IsPrisoner && hero != Hero.MainHero
            && !GwpCommon.IsGreyWardenClanMember(hero)
            && hero.PartyBelongedTo is { IsActive: true, MapEvent: null };

        /// <summary>直接销毁一支部队（模拟路上被打光），用原版 <c>DestroyPartyAction.Apply</c>。</summary>
        private static BridgeReply DestroyParty(JObject args)
        {
            string? id = args.Value<string>("id");
            MobileParty? party = BridgeQuery.FindParty(id);
            if (party == null) return BridgeReply.Fail("not_found", "没有部队 " + id);
            if (party.MapEvent != null) return BridgeReply.Fail("invalid_state", "部队 " + id + " 正在战斗，不能销毁");
            if (party == MobileParty.MainParty) return BridgeReply.Fail("refused", "不销毁玩家主队");
            DestroyPartyAction.Apply(null, party);
            return BridgeReply.Ok(new JObject { ["destroyed"] = id, ["still_active"] = party.IsActive });
        }
    }
}
