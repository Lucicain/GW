using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 军械库（用户裁定 2026-10-05，docs/state/firesworn-economy.md）：每支灰袍领主部队、每座信众村各一本账，
    /// 自动武装狂信者和灰袍新兵，一个兵一套。不配马（不武装成骑兵），肩部固定灰袍印章。
    ///
    /// 账本记「物品 + 品质 → 数量」，兵身上穿的也算在里面；配装是按现有兵力每小时重新分配算出来的
    /// （<see cref="ArmoryPlan"/>，不存档，随时可由账本和兵力推出）。所以我方守住战场时兵死了装备自然还在；
    /// 我方输了，死去的兵那一套照原版尸体缴获给胜方一部分（<see cref="SettleBattleLosses"/>）。
    ///
    /// 实战里在原版生成每个兵时换上分到的那一套（思路同开源模组 Dynamic Troop Equipment System：
    /// 前置补丁 Mission.SpawnAgent 改 AgentBuildData.Equipment）；自动结算不看装备，
    /// 所以另按分到的装备阶数给伤害加减（<see cref="GwpFaithCombatSimulationModel"/>）。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        private const int KitHead = 0, KitBody = 1, KitLeg = 2, KitGloves = 3, KitMain = 4, KitShield = 5, KitThrow = 6;
        private const int KitSlots = 7;

        /// <summary>一个持有者此刻的配装结果。由账本和兵力推出，不存档。</summary>
        private sealed class ArmoryPlan
        {
            public readonly List<EquipmentElement[]> Sets = new List<EquipmentElement[]>();
            public int Recruits;
            // 没分出去的：前一段是给狂信者增长留的余量，余量以外是多余，可以捐、可以卖。
            public readonly ItemRoster Free = new ItemRoster();
            public readonly ItemRoster Surplus = new ItemRoster();
            public float RecruitWeaponTier, RecruitArmorTier, ZealotWeaponTier, ZealotArmorTier;
        }

        // 每小时由账本和兵力重算，读档后第一个小时前由 PlanOf 现算。
        private readonly Dictionary<string, ArmoryPlan> _plans =
            new Dictionary<string, ArmoryPlan>(StringComparer.OrdinalIgnoreCase);

        // 一场实战里，每个持有者每个兵种已经发出去几套。只在这一场战斗里有意义，不存档（战斗中不能存档）。
        private readonly Dictionary<string, int> _issuedThisMission = new Dictionary<string, int>(StringComparer.Ordinal);
        private Mission? _issuedMission;

        internal static bool IsArmedTroop(CharacterObject? troop) =>
            troop != null && (troop.StringId == GwpIds.ZealotId || troop.StringId == GwpIds.NewRecruitId);

        // ---------------------------------------------------------------- 持有者与配装

        /// <summary>这支部队用哪本账：灰袍领主部队用自己的，信众村民兵用村子的。其余没有。</summary>
        private string? HolderOf(PartyBase? party)
        {
            MobileParty? mobile = party?.MobileParty;
            if (mobile == null)
                return null;
            if (mobile.IsMilitia && IsPreachedVillage(mobile.HomeSettlement))
                return mobile.HomeSettlement.StringId;
            return mobile.IsLordParty && GwpWardenRecruitment.IsWardenParty(mobile) ? mobile.StringId : null;
        }

        private ArmoryPlan PlanOf(string holder)
        {
            if (_plans.TryGetValue(holder, out ArmoryPlan? plan))
                return plan;
            plan = BuildPlanFor(holder);
            _plans[holder] = plan;
            return plan;
        }

        private ArmoryPlan BuildPlanFor(string holder)
        {
            ItemRoster ledger = ItemsOf(_armories, holder);
            if (_villages.TryGetValue(holder, out FaithVillage? row))
            {
                Settlement village = Settlement.Find(holder);
                int headroom = Math.Max(0, ZealotCap(village) - row.Zealots);
                int reserve = Math.Min(headroom, Math.Max(GwpTuning.Firesworn.ArmoryVillageReserveMinimum,
                    (int)(row.Zealots * GwpTuning.Firesworn.ArmoryReserveShare)));
                return BuildPlan(ledger, 0, row.Zealots, reserve);
            }
            MobileParty party = FindParty(holder)!;
            int recruits = party.MemberRoster.GetTroopRoster()
                .Where(element => element.Character.StringId == GwpIds.NewRecruitId).Sum(element => element.Number);
            int zealots = party.MemberRoster.GetTroopRoster()
                .Where(element => element.Character.StringId == GwpIds.ZealotId).Sum(element => element.Number);
            return BuildPlan(ledger, recruits, zealots,
                (int)Math.Ceiling((recruits + zealots) * GwpTuning.Firesworn.ArmoryReserveShare));
        }

        /// <summary>
        /// 每一类装备按阶数、价值从好到差排好，第 i 个兵拿每一类的第 i 件（新兵排在狂信者前面，先挑）。
        /// 盾只给拿单手兵器的人。分完剩下的先留够余量，再多的就是多余。马、马具、披风、弓弩箭、
        /// 女性不能用的、技能不够用的，都算多余（不配骑兵，肩部是灰袍印章，用户裁定）。
        /// </summary>
        private static ArmoryPlan BuildPlan(ItemRoster ledger, int recruits, int zealots, int reserve)
        {
            ArmoryPlan plan = new ArmoryPlan { Recruits = recruits };
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            List<EquipmentElement>[] buckets = Enumerable.Range(0, KitSlots).Select(_ => new List<EquipmentElement>()).ToArray();
            for (int i = 0; i < ledger.Count; i++)
            {
                ItemRosterElement element = ledger.GetElementCopyAtIndex(i);
                int kind = KitKind(element.EquipmentElement.Item, zealot);
                if (kind < 0)
                {
                    plan.Surplus.AddToCounts(element.EquipmentElement, element.Amount);
                    continue;
                }
                for (int n = 0; n < element.Amount; n++)
                    buckets[kind].Add(element.EquipmentElement);
            }
            foreach (List<EquipmentElement> bucket in buckets)
                bucket.Sort((a, b) => b.Item.Tier != a.Item.Tier
                    ? ((int)b.Item.Tier).CompareTo((int)a.Item.Tier)
                    : b.ItemValue.CompareTo(a.ItemValue));

            int men = recruits + zealots;
            int shieldsUsed = 0;
            for (int i = 0; i < men; i++)
            {
                EquipmentElement[] kit = new EquipmentElement[KitSlots];
                foreach (int kind in new[] { KitHead, KitBody, KitLeg, KitGloves, KitMain, KitThrow })
                    if (i < buckets[kind].Count)
                        kit[kind] = buckets[kind][i];
                if (!kit[KitMain].IsEmpty && kit[KitMain].Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon &&
                    shieldsUsed < buckets[KitShield].Count)
                    kit[KitShield] = buckets[KitShield][shieldsUsed++];
                plan.Sets.Add(kit);
            }
            for (int kind = 0; kind < KitSlots; kind++)
            {
                int used = kind == KitShield ? shieldsUsed : Math.Min(men, buckets[kind].Count);
                for (int i = used; i < buckets[kind].Count; i++)
                    (i < used + reserve ? plan.Free : plan.Surplus).AddToCounts(buckets[kind][i], 1);
            }

            Tiers(plan.Sets.Take(recruits), out plan.RecruitWeaponTier, out plan.RecruitArmorTier);
            Tiers(plan.Sets.Skip(recruits), out plan.ZealotWeaponTier, out plan.ZealotArmorTier);
            return plan;
        }

        private static int KitKind(ItemObject item, CharacterObject zealot)
        {
            if (item.ItemFlags.HasAnyFlag(ItemFlags.NotUsableByFemale))
                return -1;
            int kind = item.ItemType switch
            {
                ItemObject.ItemTypeEnum.HeadArmor => KitHead,
                ItemObject.ItemTypeEnum.BodyArmor => KitBody,
                ItemObject.ItemTypeEnum.LegArmor => KitLeg,
                ItemObject.ItemTypeEnum.HandArmor => KitGloves,
                ItemObject.ItemTypeEnum.OneHandedWeapon => KitMain,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => KitMain,
                ItemObject.ItemTypeEnum.Polearm => KitMain,
                ItemObject.ItemTypeEnum.Shield => KitShield,
                ItemObject.ItemTypeEnum.Thrown => KitThrow,
                _ => -1
            };
            if (kind >= KitMain && item.RelevantSkill != null &&
                item.Difficulty > zealot.GetSkillValue(item.RelevantSkill))
                return -1;
            return kind;
        }

        private static int TierOf(EquipmentElement element) => element.IsEmpty ? 0 : (int)element.Item.Tier + 1;

        private static void Tiers(IEnumerable<EquipmentElement[]> sets, out float weapon, out float armor)
        {
            List<EquipmentElement[]> list = sets.ToList();
            weapon = armor = 0f;
            if (list.Count == 0)
                return;
            weapon = (float)list.Average(kit => TierOf(kit[KitMain]));
            armor = (float)list.Average(kit =>
                (TierOf(kit[KitHead]) + TierOf(kit[KitBody]) + TierOf(kit[KitLeg]) + TierOf(kit[KitGloves])) / 4f);
        }

        private void RefreshArmoryPlans()
        {
            _plans.Clear();
            foreach (string holder in _armories.Keys.ToList())
                _plans[holder] = BuildPlanFor(holder);
        }

        /// <summary>
        /// 每天一次：信众村军械库多出来的进货仓，由狂信者队拉去卖；领主部队多出来的进部队原版仓库，
        /// 到信众村时捐掉（用户裁定）。
        /// </summary>
        private void RebalanceArmories()
        {
            RefreshArmoryPlans();
            foreach (KeyValuePair<string, ArmoryPlan> pair in _plans.ToList())
            {
                if (pair.Value.Surplus.Count == 0)
                    continue;
                bool village = _villages.ContainsKey(pair.Key);
                ItemRoster target = village ? ItemsOf(_goods, pair.Key) : FindParty(pair.Key)!.ItemRoster;
                int moved = MoveAll(pair.Value.Surplus, ItemsOf(_armories, pair.Key), target);
                GwpAiDiagnostics.WriteFaith(village ? "ARMORY_SURPLUS_TO_GOODS" : "ARMORY_SURPLUS_TO_INVENTORY",
                    "holder=" + pair.Key + "; items=" + moved + "; value=" + pair.Value.Surplus.TotalValue);
            }
            RefreshArmoryPlans();
        }

        /// <summary>把 <paramref name="what"/> 里列的东西从账本 <paramref name="from"/> 挪到 <paramref name="to"/>。</summary>
        private static int MoveAll(ItemRoster what, ItemRoster from, ItemRoster to)
        {
            int moved = 0;
            for (int i = 0; i < what.Count; i++)
            {
                ItemRosterElement element = what.GetElementCopyAtIndex(i);
                from.AddToCounts(element.EquipmentElement, -element.Amount);
                to.AddToCounts(element.EquipmentElement, element.Amount);
                moved += element.Amount;
            }
            return moved;
        }

        /// <summary>领主进信众村时，部队军械库里余量以外多出来的捐给村军械库。</summary>
        private int DonateArmorySurplus(string partyId, string villageId)
        {
            if (!_armories.ContainsKey(partyId))
                return 0;
            ArmoryPlan plan = BuildPlanFor(partyId);
            int moved = MoveAll(plan.Surplus, ItemsOf(_armories, partyId), ItemsOf(_armories, villageId));
            _plans.Remove(partyId);
            _plans.Remove(villageId);
            return moved;
        }

        /// <summary>
        /// 招兵连装备带走（用户裁定）：从村军械库没分出去的那部分里，按每一类从好到差给每个新兵拿一件，
        /// 挪进部队军械库。村里民兵身上的不动。
        /// </summary>
        private int MoveKits(string fromHolder, string toHolder, int men, string reason)
        {
            if (men <= 0 || !_armories.ContainsKey(fromHolder))
                return 0;
            ArmoryPlan plan = BuildPlanFor(fromHolder);
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            ItemRoster taken = new ItemRoster();
            int[] perKind = new int[KitSlots];
            List<ItemRosterElement> free = Enumerable.Range(0, plan.Free.Count)
                .Select(plan.Free.GetElementCopyAtIndex)
                .OrderByDescending(element => (int)element.EquipmentElement.Item.Tier)
                .ThenByDescending(element => element.EquipmentElement.ItemValue)
                .ToList();
            foreach (ItemRosterElement element in free)
            {
                int kind = KitKind(element.EquipmentElement.Item, zealot);
                int take = Math.Min(element.Amount, men - perKind[kind]);
                if (take <= 0)
                    continue;
                taken.AddToCounts(element.EquipmentElement, take);
                perKind[kind] += take;
            }
            int moved = MoveAll(taken, ItemsOf(_armories, fromHolder), ItemsOf(_armories, toHolder));
            _plans.Remove(fromHolder);
            _plans.Remove(toHolder);
            if (moved > 0)
                GwpAiDiagnostics.WriteFaith("KITS_MOVED",
                    "from=" + fromHolder + "; to=" + toHolder + "; men=" + men + "; items=" + moved + "; reason=" + reason);
            return moved;
        }

        // ---------------------------------------------------------------- 输了、没了

        /// <summary>
        /// 我方输了：死去的狂信者、新兵身上那一套从账本里去掉，其中一部分归胜方——玩家在胜方就进玩家背包，
        /// 否则进胜方领头部队的背包（用户裁定：照原版尸体缴获，AI 抢走一部分）。我方守住战场时什么都不丢。
        /// 灰袍领主部队被打没了不走这里：它的账本在销毁时已经整本送回信众村（<see cref="OnPartyGone"/>）。
        /// </summary>
        private void SettleBattleLosses(MapEvent mapEvent)
        {
            if (mapEvent.BattleState != BattleState.AttackerVictory && mapEvent.BattleState != BattleState.DefenderVictory)
                return;
            MapEventSide loser = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            MapEventSide winner = mapEvent.GetMapEventSide(mapEvent.WinningSide);
            ItemRoster? looter = winner.Parties.Any(entry => entry.Party == PartyBase.MainParty)
                ? PartyBase.MainParty.ItemRoster
                : winner.LeaderParty?.ItemRoster;
            foreach (MapEventParty party in loser.Parties)
            {
                string? holder = HolderOf(party.Party);
                if (holder == null || !_armories.ContainsKey(holder))
                    continue;
                int dead = 0;
                for (int i = 0; i < party.DiedInBattle.Count; i++)
                    if (IsArmedTroop(party.DiedInBattle.GetCharacterAtIndex(i)))
                        dead += party.DiedInBattle.GetElementNumber(i);
                if (dead <= 0)
                    continue;

                ArmoryPlan plan = PlanOf(holder);
                ItemRoster ledger = ItemsOf(_armories, holder);
                int lost = 0, looted = 0;
                foreach (EquipmentElement[] kit in plan.Sets.OrderBy(_ => MBRandom.RandomFloat).Take(dead))
                    foreach (EquipmentElement item in kit.Where(entry => !entry.IsEmpty))
                    {
                        ledger.AddToCounts(item, -1);
                        lost++;
                        if (looter != null && MBRandom.RandomFloat < GwpTuning.Firesworn.ArmoryLossLootShare)
                        {
                            looter.AddToCounts(item, 1);
                            looted++;
                        }
                    }
                _plans.Remove(holder);
                GwpAiDiagnostics.WriteFaith("ARMORY_LOST_IN_DEFEAT",
                    "holder=" + holder + "; dead=" + dead + "; items=" + lost + "; lootedByWinner=" + looted +
                    "; winner=" + (winner.LeaderParty?.Name?.ToString() ?? "-") + "; mapEvent=" + mapEvent.EventType);
            }
        }

        /// <summary>
        /// 部队没了：灰袍领主部队的军械库整本回到最近的信众村（用户裁定）；常驻队、差事、扒装备的记录清掉，
        /// 狂信者队身上的货跟着丢。
        /// </summary>
        private void OnPartyGone(MobileParty party)
        {
            if (_faithParties.TryGetValue(party.StringId, out FaithParty? record))
            {
                _faithParties.Remove(party.StringId);
                GwpAiDiagnostics.WriteFaith(record.Kind == FaithPartyKind.Disciples ? "DISCIPLES_LOST" : "CARAVAN_LOST",
                    "party=" + party.StringId + "; home=" + record.HomeId + "; phase=" + record.Phase +
                    "; cargoValue=" + party.ItemRoster.TotalValue + "; gold=" + party.PartyTradeGold);
            }
            _errands.Remove(party.StringId);
            _stripped.Remove(party.StringId);
            _plans.Remove(party.StringId);
            if (!_armories.TryGetValue(party.StringId, out ItemRoster? ledger))
                return;
            _armories.Remove(party.StringId);
            Settlement? village = _villages.Keys.Select(Settlement.Find)
                .OrderBy(settlement => settlement.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
            if (village == null)
                return;
            ItemRoster target = ItemsOf(_armories, village.StringId);
            int moved = 0;
            for (int i = 0; i < ledger.Count; i++)
            {
                ItemRosterElement element = ledger.GetElementCopyAtIndex(i);
                target.AddToCounts(element.EquipmentElement, element.Amount);
                moved += element.Amount;
            }
            _plans.Remove(village.StringId);
            GwpAiDiagnostics.WriteFaith("ARMORY_RELOCATED",
                "party=" + party.StringId + "; village=" + village.StringId + "; items=" + moved);
        }

        // ---------------------------------------------------------------- 实战、自动结算

        /// <summary>这个兵在这场实战里分到的那一套；没有就返回 null（照兵种原样）。</summary>
        internal static EquipmentElement[]? TakeKitForAgent(PartyBase? party, CharacterObject troop)
        {
            if (_instance == null || !IsArmedTroop(troop))
                return null;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return null;
            if (_instance._issuedMission != Mission.Current)
            {
                _instance._issuedMission = Mission.Current;
                _instance._issuedThisMission.Clear();
            }
            ArmoryPlan plan = _instance.PlanOf(holder);
            string key = holder + "|" + troop.StringId;
            _instance._issuedThisMission.TryGetValue(key, out int issued);
            _instance._issuedThisMission[key] = issued + 1;
            bool recruit = troop.StringId == GwpIds.NewRecruitId;
            int index = (recruit ? 0 : plan.Recruits) + issued;
            int end = recruit ? plan.Recruits : plan.Sets.Count;
            return index < end ? plan.Sets[index] : null;
        }

        /// <summary>自动结算里这一方这个兵种分到的装备平均阶数（兵器、护甲）。</summary>
        internal static bool TryGetArmoryTiers(PartyBase? party, CharacterObject troop, out float weapon, out float armor)
        {
            weapon = armor = 0f;
            if (_instance == null || !IsArmedTroop(troop))
                return false;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return false;
            ArmoryPlan plan = _instance.PlanOf(holder);
            bool recruit = troop.StringId == GwpIds.NewRecruitId;
            weapon = recruit ? plan.RecruitWeaponTier : plan.ZealotWeaponTier;
            armor = recruit ? plan.RecruitArmorTier : plan.ZealotArmorTier;
            return true;
        }

        // ---------------------------------------------------------------- 每日监控

        private void WriteDailySummary()
        {
            foreach (FaithVillage row in _villages.Values)
            {
                Settlement village = Settlement.Find(row.VillageId);
                Hero? priest = FindLivingPriest(row);
                ArmoryPlan plan = PlanOf(row.VillageId);
                string parties = string.Join(",", _faithParties.Values
                    .Where(entry => string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.Kind + ":" + entry.Phase + ":" + FindParty(entry.PartyId)!.MemberRoster.TotalManCount));
                GwpAiDiagnostics.WriteFaith("DAILY_VILLAGE",
                    "village=" + row.VillageId + "; hearth=" + village.Village.Hearth.ToString("0") +
                    "; zealots=" + row.Zealots + "; militia=" + MilitiaHeadcount(village) +
                    "; priest=" + (priest?.StringId ?? "-") +
                    "; priestPower=" + (priest?.Power.ToString("0") ?? "-") +
                    "; priestSlots=" + (priest == null ? "-" : string.Join(",", priest.VolunteerTypes.Select(troop => troop?.StringId ?? "_"))) +
                    "; dungeon=" + (_dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon) ? dungeon.TotalManCount : 0) +
                    "; armoryItems=" + (_armories.TryGetValue(row.VillageId, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armed=" + plan.Sets.Count(kit => !kit[KitMain].IsEmpty) + "/" + plan.Sets.Count +
                    "; weaponTier=" + plan.ZealotWeaponTier.ToString("0.0") + "; armorTier=" + plan.ZealotArmorTier.ToString("0.0") +
                    "; goodsValue=" + (_goods.TryGetValue(row.VillageId, out ItemRoster? goods) ? goods.TotalValue : 0) +
                    "; parties=" + (parties.Length == 0 ? "-" : parties) +
                    "; daysSinceDamnation=" + DaysSince(row.LastDamnation) + "; daysSinceCongregation=" + DaysSince(row.LastCongregation));
            }
            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties().Where(party => party.IsActive && party.IsLordParty).ToList())
            {
                ArmoryPlan? plan = _armories.ContainsKey(lord.StringId) ? PlanOf(lord.StringId) : null;
                GwpAiDiagnostics.WriteFaith("DAILY_LORD",
                    "party=" + lord.StringId + "; members=" + lord.Party.NumberOfAllMembers + "/" + lord.Party.PartySizeLimit +
                    "; troops=" + string.Join(",", lord.MemberRoster.GetTroopRoster().Where(element => !element.Character.IsHero)
                        .Select(element => element.Character.StringId + ":" + element.Number)) +
                    "; prisoners=" + CountRegularPrisoners(lord) + "/" + lord.Party.PrisonerSizeLimit +
                    "; armoryItems=" + (_armories.TryGetValue(lord.StringId, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armed=" + (plan == null ? "0/0" : plan.Sets.Count(kit => !kit[KitMain].IsEmpty) + "/" + plan.Sets.Count) +
                    "; inventoryValue=" + NonFoodValue(lord.ItemRoster) +
                    "; errand=" + (_errands.TryGetValue(lord.StringId, out Errand? errand) ? errand.VillageId + ":" + errand.Reason : "-") +
                    "; case=" + CrimePool.HasTask(lord.StringId) + "; wage=" + lord.TotalWage);
            }
            GwpAiDiagnostics.WriteFaith("DAILY_TOTALS",
                "faithVillages=" + _villages.Count + "; zealots=" + _villages.Values.Sum(row => row.Zealots) +
                "; priests=" + _villages.Values.Count(row => FindLivingPriest(row) != null) +
                "; dungeon=" + _dungeons.Values.Sum(roster => roster.TotalManCount) +
                "; treasury=" + PoliceResourceManager.GetJudicialTreasuryBalance() +
                "; errands=" + _errands.Count + "; activeParties=" +
                _faithParties.Values.Count(entry => entry.Phase != FaithPartyPhase.Home));
        }

        private static string DaysSince(double hour) =>
            hour <= Never ? "never" : ((CampaignTime.Now.ToHours - hour) / 24d).ToString("0.0");
    }

    /// <summary>
    /// 实战里给狂信者、灰袍新兵换上军械库分到的那一套（用户裁定：一个兵一套装备）。
    /// 原版在 Mission.SpawnAgent 里按 AgentBuildData 的装备生成兵（C#反编译 v1.4.8 Mission.SpawnAgent：
    /// 有覆盖装备就用覆盖的，没有就按兵种随机一套）。
    /// 分到的护甲、兵器换上去，没分到的那一格照兵种原样；肩部换成灰袍印章（用户裁定），不配马。
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
    internal static class GwpArmorySpawnPatch
    {
        private static void Prefix(AgentBuildData agentBuildData)
        {
            if (Campaign.Current == null || agentBuildData.AgentCivilianEquipment ||
                agentBuildData.AgentCharacter is not CharacterObject troop ||
                !GwpFireswornBehavior.IsArmedTroop(troop))
                return;
            PartyBase? party = agentBuildData.AgentOrigin switch
            {
                PartyAgentOrigin origin => origin.Party,
                PartyGroupAgentOrigin origin => origin.Party,
                SimpleAgentOrigin origin => origin.Party,
                _ => null
            };
            EquipmentElement[]? kit = GwpFireswornBehavior.TakeKitForAgent(party, troop);
            if (kit == null)
                return;

            Equipment equipment = agentBuildData.AgentOverridenSpawnEquipment?.Clone() ??
                                  Equipment.GetRandomEquipmentElements(troop, true, Equipment.EquipmentType.Battle,
                                      agentBuildData.AgentEquipmentSeed);
            EquipmentIndex[] armor = { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves };
            for (int i = 0; i < armor.Length; i++)
                if (!kit[i].IsEmpty)
                    equipment[armor[i]] = kit[i];
            if (!kit[4].IsEmpty)
            {
                for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                    equipment[slot] = EquipmentElement.Invalid;
                EquipmentIndex next = EquipmentIndex.WeaponItemBeginSlot;
                foreach (EquipmentElement weapon in new[] { kit[4], kit[5], kit[6] }.Where(entry => !entry.IsEmpty))
                    equipment[next++] = weapon;
            }
            equipment[EquipmentIndex.Cape] = CharacterObject.Find(GwpIds.NewRecruitId).FirstBattleEquipment[EquipmentIndex.Cape];
            equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
            agentBuildData.Equipment(equipment);
        }
    }

    /// <summary>
    /// 自动结算不看装备，只看兵种阶数（C#反编译 v1.4.8 DefaultCombatSimulationModel.SimulateHit：
    /// 伤害按双方兵种 GetTroopPower 之比算）。军械库也要在自动结算里算数（用户裁定）：
    /// 狂信者、新兵出手按分到的兵器阶数加伤害，挨打按分到的护甲阶数减伤害。
    /// </summary>
    public sealed class GwpFaithCombatSimulationModel : CombatSimulationModel
    {
        private CombatSimulationModel Inner => BaseModel;

        public override ExplainedNumber SimulateHit(CharacterObject strikerTroop, CharacterObject struckTroop,
            PartyBase strikerParty, PartyBase struckParty, float strikerAdvantage, MapEvent battle,
            float strikerSideMorale, float struckSideMorale)
        {
            ExplainedNumber damage = Inner.SimulateHit(strikerTroop, struckTroop, strikerParty, struckParty,
                strikerAdvantage, battle, strikerSideMorale, struckSideMorale);
            if (GwpFireswornBehavior.TryGetArmoryTiers(strikerParty, strikerTroop, out float weapon, out _))
                damage.AddFactor(weapon * GwpTuning.Firesworn.ArmoryWeaponTierDamage);
            if (GwpFireswornBehavior.TryGetArmoryTiers(struckParty, struckTroop, out _, out float armor))
                damage.AddFactor(1f / (1f + armor * GwpTuning.Firesworn.ArmoryArmorTierProtection) - 1f);
            return damage;
        }

        public override ExplainedNumber SimulateHit(Ship strikerShip, Ship struckShip, PartyBase strikerParty,
            PartyBase struckParty, SiegeEngineType siegeEngine, float strikerAdvantage, MapEvent battle,
            out int troopCasualties) =>
            Inner.SimulateHit(strikerShip, struckShip, strikerParty, struckParty, siegeEngine, strikerAdvantage,
                battle, out troopCasualties);

        public override (int defenderRounds, int attackerRounds) GetSimulationTicksForBattleRound(MapEvent mapEvent) =>
            Inner.GetSimulationTicksForBattleRound(mapEvent);

        public override int GetNumberOfEquipmentsBuilt(Settlement settlement) =>
            Inner.GetNumberOfEquipmentsBuilt(settlement);

        public override float GetMaximumSiegeEquipmentProgress(Settlement settlement) =>
            Inner.GetMaximumSiegeEquipmentProgress(settlement);

        public override float GetSettlementAdvantage(Settlement settlement) =>
            Inner.GetSettlementAdvantage(settlement);

        public override void GetBattleAdvantage(MapEvent mapEvent, out ExplainedNumber defenderAdvantage,
            out ExplainedNumber attackerAdvantage) =>
            Inner.GetBattleAdvantage(mapEvent, out defenderAdvantage, out attackerAdvantage);

        public override float GetShipSiegeEngineHitChance(Ship ship, SiegeEngineType siegeEngineType,
            BattleSideEnum battleSide) =>
            Inner.GetShipSiegeEngineHitChance(ship, siegeEngineType, battleSide);

        public override int GetPursuitRoundCount(MapEvent mapEvent) => Inner.GetPursuitRoundCount(mapEvent);

        public override float GetBluntDamageChance(CharacterObject strikerTroop, CharacterObject strikedTroop,
            PartyBase strikerParty, PartyBase strikedParty, MapEvent battle) =>
            Inner.GetBluntDamageChance(strikerTroop, strikedTroop, strikerParty, strikedParty, battle);

        public override CampaignTime GetSimulationTickInterval(MapEvent mapEvent) =>
            Inner.GetSimulationTickInterval(mapEvent);

        public override MBList<(Ship, MapEventParty)> GetSimulationShips(MapEvent mapEvent,
            MBList<MapEventParty> battleParties) =>
            Inner.GetSimulationShips(mapEvent, battleParties);

        public override int GetParticipatingTroopCount(MapEventSide side) => Inner.GetParticipatingTroopCount(side);
    }
}
