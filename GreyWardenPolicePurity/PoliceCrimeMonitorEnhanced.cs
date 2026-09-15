using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 增强版犯罪监控 - 在原有基础上添加事件通知
    /// </summary>
    public class PoliceCrimeMonitorEnhanced : CampaignBehaviorBase
    {
        // 犯罪通知事件 - 供惩戒系统订阅
        public static event Action<string, MobileParty, Vec2, string>? OnCrimeDetected;

        public override void RegisterEvents()
        {
            // 攻击村民/商队
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);

            // 村庄劫掠
            CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);

            // 平民伤亡计数：罚金按尸体算，和灰袍给玩家定价的口径一致。
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEndedForCasualties);

            // 劫掠结束时按村庄少掉的人口折算人命。
            CampaignEvents.VillageLooted.AddNonSerializedListener(this, OnVillageLooted);
        }

        public override void SyncData(IDataStore dataStore) { }

        private readonly struct RaidSnapshot
        {
            internal RaidSnapshot(string raiderPartyId, float hearth, double startedHours)
            {
                RaiderPartyId = raiderPartyId;
                Hearth = hearth;
                StartedHours = startedHours;
            }

            internal string RaiderPartyId { get; }
            internal float Hearth { get; }
            internal double StartedHours { get; }
        }

        // 一次劫掠的起点人口。只在本次游戏会话内有效；存档中断的劫掠不补算，宁可少算不错算。
        /// <summary>一次劫掠的起点最多作数这么久，过期的不再往任何人账上算。</summary>
        private const double RaidSnapshotValidHours = 72d;

        private readonly Dictionary<string, RaidSnapshot> _raidHearthAtStart =
            new Dictionary<string, RaidSnapshot>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// What the burning actually cost the village, in people. A point of hearth is a
        /// score of villagers; whatever the raid took between starting and ending is put
        /// on the raider's account at that rate.
        /// </summary>
        private void OnVillageLooted(Village village)
        {
            if (village?.Settlement == null) return;

            try
            {
                if (!_raidHearthAtStart.TryGetValue(village.Settlement.StringId, out RaidSnapshot snapshot))
                    return;
                _raidHearthAtStart.Remove(village.Settlement.StringId);

                // 起点只在同一次劫掠里作数。一次被打断的劫掠不会走到这里，起点却留了下来，
                // 于是下一个来烧这个村子的人接手了上一次的基准：账上多出来的是这中间几天
                // 人口的自然涨落，记的还是上一个人的名字。超过这么久的起点一律作废。
                if (CampaignTime.Now.ToHours - snapshot.StartedHours > RaidSnapshotValidHours) return;

                MobileParty? raider = MobileParty.All.FirstOrDefault(party =>
                    string.Equals(party.StringId, snapshot.RaiderPartyId, StringComparison.OrdinalIgnoreCase));
                Hero? leader = raider?.LeaderHero;
                if (raider == null || leader == null) return;
                if (!IsChargeableOffender(raider, leader)) return;

                int hearthLost = (int)Math.Round(Math.Max(0f, snapshot.Hearth - village.Hearth));
                int lives = hearthLost * GwpTuning.FieldArrest.VillagersPerHearthPoint;
                if (lives <= 0) return;

                CrimePool.GetOrCreateHistory(leader).AddCivilianCasualties(lives);

                CrimeRecord? record = CrimePool.GetByOffenderId(snapshot.RaiderPartyId);
                if (record?.HasOpenCase == true)
                    record.CivilianCasualties += lives;

                GwpAiDiagnostics.WriteFieldArrest(
                    "CASUALTIES_RAID",
                    "offender=" + DescribeOffender(leader) +
                    "; village=" + village.Settlement.StringId +
                    "; hearthLost=" + hearthLost + "; lives=" + lives +
                    "; caseTotal=" + (record?.CivilianCasualties ?? 0) +
                    "; standing=" + CrimePool.GetOrCreateHistory(leader).NegativeStanding);
            }
            catch
            {
                // 人口折算失败只影响罚金，不能连累劫掠结算本身。
            }
        }

        /// <summary>
        /// A fight the offender started against villagers or a caravan is priced by what
        /// it cost them, not by the fact that it happened. Losses on the victim's side are
        /// added to the open case, so a lord who wipes out a caravan pays for every guard.
        /// </summary>
        private void OnMapEventEndedForCasualties(MapEvent mapEvent)
        {
            if (mapEvent == null) return;

            try
            {
                AccumulateCasualties(mapEvent, BattleSideEnum.Attacker);
                AccumulateCasualties(mapEvent, BattleSideEnum.Defender);

            }
            catch
            {
                // 伤亡计数失败只影响罚金数额，不能连累战斗结算。
            }
        }

        /// <summary>
        /// A man taken by force has paid with himself: the case closes and his standing
        /// with the Wardens goes with it. Only a prisoner in the player's own hands counts
        /// - beating him and letting him ride off settles nothing.
        /// </summary>
        private static void AccumulateCasualties(MapEvent mapEvent, BattleSideEnum offenderSide)
        {
            MapEventSide side = mapEvent.GetMapEventSide(offenderSide);
            MapEventSide otherSide = mapEvent.GetMapEventSide(
                offenderSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            if (side == null || otherSide == null) return;

            // 只数平民自己的死者，不数他们那一边的全部死者。
            //
            // 此前这里取的是 otherSide.TroopCasualties——**整边**的伤亡——只要那一边
            // 站着一支村民队、一支商队或村庄本身，另一边的领主部队、援军、驻军死多少，
            // 全部按"平民人命"记到攻方账上。一场有领主护着商队的仗，对面死的几十个
            // 正规兵就这样变成了几十条人命、上万第纳尔的罚金。这是这些人"杀了好多人"
            // 的主要来源之一。原版每支参战队伍各自记着 DiedInBattle，按队伍数就是了。
            int civilianDead = CountDead(otherSide, CivilianDied);
            int banditDead = CountDead(otherSide, BanditDied);

            // 领主之间互殴既不算罪，也不算功；只有对平民下手或替人剿匪才动标准。
            if (civilianDead <= 0 && banditDead <= 0) return;

            bool otherIsCivilian = civilianDead > 0;
            int losses = otherIsCivilian ? civilianDead : banditDead;

            foreach (KeyValuePair<MobileParty, int> assignment in ApportionLosses(side, losses))
            {
                MobileParty party = assignment.Key;
                int share = assignment.Value;
                Hero? leader = party.LeaderHero;
                if (share <= 0 || leader == null) continue;
                if (!IsChargeableOffender(party, leader)) continue;

                if (otherIsCivilian)
                {
                    CrimePool.GetOrCreateHistory(leader).AddCivilianCasualties(share);

                    CrimeRecord? record = CrimePool.GetByOffenderId(party.StringId);
                    if (record?.HasOpenCase == true)
                        record.CivilianCasualties += share;

                    GwpAiDiagnostics.WriteFieldArrest(
                        "CASUALTIES_BATTLE",
                        "offender=" + DescribeOffender(leader) +
                        "; killed=" + share + "/" + losses + " civilians" +
                        "; caseTotal=" + (record?.CivilianCasualties ?? 0) +
                        "; standing=" + CrimePool.GetOrCreateHistory(leader).NegativeStanding);
                }
                else if (mapEvent.Winner == side)
                {
                    // 同一条赎罪路径：砍匪能把自己的负声望赎回来，和玩家一样。
                    CrimePool.GetOrCreateHistory(leader).AddRedeemingKills(share);
                }
            }
        }

        private static bool CivilianDied(MapEventParty party) =>
            party?.Party?.MobileParty?.IsVillager == true ||
            party?.Party?.MobileParty?.IsCaravan == true ||
            party?.Party?.Settlement?.IsVillage == true;

        private static bool BanditDied(MapEventParty party) =>
            party?.Party?.MobileParty?.IsBandit == true ||
            party?.Party?.MobileParty?.ActualClan?.IsBanditFaction == true;

        /// <summary>这一边符合条件的队伍里，实际死了多少人。</summary>
        private static int CountDead(MapEventSide side, Func<MapEventParty, bool> matches)
        {
            int dead = 0;
            foreach (MapEventParty party in side.Parties)
            {
                if (party == null || !matches(party)) continue;
                dead += party.DiedInBattle?.TotalManCount ?? 0;
            }
            return Math.Max(0, dead);
        }

        /// <summary>
        /// 一场仗总共只死了这么多人。此前这里把对方整边的伤亡数原样记在己方
        /// 每一个参战方头上：三家联手打一仗、实际死 39 人，账上就成了 117 条人命。
        /// 现在按各家投入的兵力分摊，整除的零头给出兵最多的那家，总数对得上。
        ///
        /// 分摊时把不记账的队伍（玩家自己、巡逻队、强盗、灰袍）也算进分母：
        /// 他们砍的人不该转嫁给同一边的领主。
        /// </summary>
        private static Dictionary<MobileParty, int> ApportionLosses(MapEventSide side, int losses)
        {
            var weights = new List<KeyValuePair<MobileParty, int>>();
            int total = 0;

            foreach (MapEventParty eventParty in side.Parties)
            {
                PartyBase? partyBase = eventParty?.Party;
                MobileParty? party = partyBase?.MobileParty;
                if (party == null) continue;

                int weight = Math.Max(1, partyBase?.MemberRoster?.TotalManCount ?? 1);
                weights.Add(new KeyValuePair<MobileParty, int>(party, weight));
                total += weight;
            }

            var shares = new Dictionary<MobileParty, int>();
            if (weights.Count == 0 || total <= 0) return shares;

            int assigned = 0;
            foreach (KeyValuePair<MobileParty, int> entry in weights)
            {
                int share = losses * entry.Value / total;
                shares[entry.Key] = share;
                assigned += share;
            }

            int remainder = losses - assigned;
            if (remainder > 0)
            {
                MobileParty biggest = weights.OrderByDescending(entry => entry.Value).First().Key;
                shares[biggest] = shares[biggest] + remainder;
            }

            return shares;
        }

        /// <summary>
        /// 谁的账才记得下。和犯罪登记那边（OnMapEventStarted / OnVillageBeingRaided）
        /// 用同一套口径：玩家自己、巡逻队、强盗、灰袍自己人都不进账本。此前这里只挡
        /// 巡逻队，于是强盗打村民也在攒负声望，攒出一堆玩家永远处理不了的账。
        /// </summary>
        private static bool IsChargeableOffender(MobileParty party, Hero leader)
        {
            if (party.IsMainParty) return false;
            if (GwpCommon.ShouldIgnoreCrimeTracking(party)) return false;
            if (IsBanditParty(party)) return false;

            // 灰袍是执法的一方，不能被自己的账本立案。
            return !GwpCommon.IsGreyWardenClanMember(leader);
        }

        /// <summary>
        /// 是不是强盗。此前三处都写成 `IsOutlaw && IsBanditFaction`，而这两个标记
        /// 在原版是各自独立的存档字段：喀拉库吉特这类匪帮 `IsBanditFaction` 为真、
        /// `IsOutlaw` 却是假，与在一起就永远不成立。实机日志里一个匪首因此拿到了
        /// 正式案卷（`CharacterObject_1850(.../karakhuzaits); caseTotal=30`）。
        /// 判据只认 `IsBanditFaction`，另外补上队伍本身与所属势力两条。
        /// </summary>
        internal static bool IsBanditParty(MobileParty? party)
        {
            if (party == null) return false;
            if (party.IsBandit) return true;
            if (party.ActualClan?.IsBanditFaction == true) return true;
            return party.MapFaction?.IsBanditFaction == true;
        }

        /// <summary>诊断行里光有 id 认不出是谁，连名字和所属氏族一起写。</summary>
        private static string DescribeOffender(Hero leader) =>
            (leader.StringId ?? "-") +
            "(" + (leader.Name?.ToString() ?? "-") +
            "/" + (leader.Clan?.StringId ?? "-") + ")";

        /// <summary>
        /// 攻击村民/商队
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            if (mapEvent == null || attackerParty == null || defenderParty == null)
                return;

            MobileParty attacker = attackerParty.MobileParty;
            MobileParty defender = defenderParty.MobileParty;

            if (attacker == null || defender == null)
                return;

            // 警察全忙时静默

            // 巡逻队只算临时执勤单位，不进犯罪池，也不挂犯罪标记。
            if (GwpCommon.ShouldIgnoreCrimeTracking(attacker))
                return;

            // 过滤野怪
            if (IsBanditParty(attacker))
                return;

            // 获取位置（Vec2用于距离计算）
            Vec2 location = mapEvent.Position.ToVec2();

            // 攻击村民
            if (defender.PartyComponent is VillagerPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_policecrimemonitorenhanced_001}Villager");

                // 显示消息
                Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_002}Attack villager"), attacker, victimName, location);

                // 触发事件通知惩戒系统
                OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_003}Attack villager"), attacker, location, victimName);
                return;
            }

            // 攻击商队
            if (defender.PartyComponent is CaravanPartyComponent)
            {
                string victimName = defender.Name?.ToString() ?? GwpText.Get("{=gwp_policecrimemonitorenhanced_004}Caravan");

                Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_005}Attack caravan"), attacker, victimName, location);

                OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_006}Attack caravan"), attacker, location, victimName);
                return;
            }
        }

        /// <summary>
        /// 劫掠村庄 - 开始
        /// </summary>
        private void OnVillageBeingRaided(Village village)
        {
            if (village == null) return;

            Vec2 location = village.Settlement.Position.ToVec2();
            MobileParty offender = FindRaidingParty(village);

            if (offender == null) return;
            if (GwpCommon.ShouldIgnoreCrimeTracking(offender)) return;

            // 过滤野怪
            if (IsBanditParty(offender))
                return;

            string victimName = GwpText.Get("{=gwp_policecrimemonitorenhanced_007}{VAR_1} Villager", "VAR_1", village.Name);

            Report(GwpText.Get("{=gwp_policecrimemonitorenhanced_008}Raid village (start)"), offender, victimName, location, GwpText.Get("{=gwp_policecrimemonitorenhanced_009}Village={VAR_1}", "VAR_1", village.Name));

            OnCrimeDetected?.Invoke(GwpText.Get("{=gwp_policecrimemonitorenhanced_010}Raid Village"), offender, location, victimName);

            // 劫掠不产生战场伤亡，但村子照样死人。没有尸体可数时按固定人头计入案卷。
            // 烧村的人命账在劫掠结束时按村庄少掉的人口结算，这里只留一个起点。
            // 该事件在一次劫掠里可能反复触发；若每次都覆盖，基准会一路往下挪，
            // 最后只算到最后一跳的损失。所以第一跳之后不再改写。
            if (!_raidHearthAtStart.ContainsKey(village.Settlement.StringId))
                _raidHearthAtStart[village.Settlement.StringId] =
                    new RaidSnapshot(offender.StringId, village.Hearth, CampaignTime.Now.ToHours);
        }

        /// <summary>
        /// 显示犯罪消息
        /// </summary>
        private void Report(string type, MobileParty offender, string victimName, Vec2 location, string? extra = null)
        {
            string offenderName = offender?.Name?.ToString() ?? "Unknown";
            string extraInfo = string.IsNullOrEmpty(extra) ? "" : $" | {extra}";

            // 犯罪检测内部日志（开发调试，正式版不显示）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP Police] {type} | 犯罪者={offenderName} | 受害者={victimName} | 地点={location}{extraInfo}",
            //     Colors.Red
            // ));
        }

        /// <summary>
        /// 找到正在劫掠村庄的队伍
        /// </summary>
        private MobileParty? FindRaidingParty(Village village)
        {
            Settlement? target = village?.Settlement;
            if (target == null) return null;

            foreach (MobileParty p in MobileParty.All)
            {
                if (p == null || !p.IsActive || p.IsMainParty) continue;

                if (p.TargetSettlement != target) continue;

                if (p.DefaultBehavior == AiBehavior.RaidSettlement ||
                    p.ShortTermBehavior == AiBehavior.RaidSettlement)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
