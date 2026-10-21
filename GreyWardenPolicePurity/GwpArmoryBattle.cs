using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        private sealed class ArmoryBattleLoadout
        {
            internal readonly string Holder;
            internal readonly ArmoryPlan Plan;
            // 0在场、1阵亡、2负伤、3溃逃；旧轮次阵亡/溃逃仍保留配装，不能在重排时再发一次。
            internal readonly Dictionary<int, int> Outcomes = new();
            internal readonly Dictionary<int, ArmoryKit> BySeed = new();
            internal ArmoryBattleLoadout(string holder, ArmoryPlan plan) { Holder = holder; Plan = plan; }
        }

        // 分装是库存的借用记录，保存物品/品质/槽位和战斗状态，不是第二份资源。
        // 原版Update会重建逐人编号，见CaptureBattleOutcomes/RebindBattleLoadout。
        private readonly Dictionary<MapEventParty, ArmoryBattleLoadout> _battleLoadouts = new();
        // 原版战后分俘会先RemovePartyLeader；归属必须在这之前记住，不能等MapEventEnded再认领主。
        private readonly Dictionary<MapEventParty, string> _battleOwners = new();

        private void OnArmoryBattleStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender) =>
            CaptureArmoryBattleOwners(mapEvent);

        private void CaptureArmoryBattleOwners(MapEvent mapEvent)
        {
            foreach (MapEventParty party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                if (_battleOwners.ContainsKey(party)) continue;
                string? holder = HolderOf(party.Party);
                if (holder != null && _armories.ContainsKey(holder))
                {
                    _battleOwners.Add(party, holder);
                    if (party.Troops != null) BattleLoadoutFor(holder, party);
                }
            }
        }

        private ArmoryBattleLoadout BattleLoadoutFor(string holder, MapEventParty party)
        {
            if (_battleLoadouts.TryGetValue(party, out ArmoryBattleLoadout? loadout)) return loadout;
            loadout = new ArmoryBattleLoadout(holder, new ArmoryPlan());
            _battleLoadouts.Add(party, loadout);
            RebindBattleLoadout(party);
            _battleOwners[party] = holder;
            return loadout;
        }

        private void CaptureBattleOutcomes(MapEventParty party)
        {
            if (!_battleLoadouts.TryGetValue(party, out ArmoryBattleLoadout? loadout) || party.Troops == null) return;
            foreach (var soldier in party.Troops)
                if (loadout.BySeed.ContainsKey(soldier.Descriptor.UniqueSeed))
                    loadout.Outcomes[soldier.Descriptor.UniqueSeed] = soldier.IsKilled ? 1 : soldier.IsRouted ? 3 : soldier.IsWounded ? 2 : 0;
        }

        private void RebindBattleLoadout(MapEventParty party)
        {
            if (!_battleLoadouts.TryGetValue(party, out ArmoryBattleLoadout? loadout) || party.Troops == null) return;
            var reusable = loadout.BySeed.Where(pair => loadout.Outcomes[pair.Key] == 0 || loadout.Outcomes[pair.Key] == 2)
                .OrderBy(pair => pair.Key).GroupBy(pair => (pair.Value.Troop, loadout.Outcomes[pair.Key]))
                .ToDictionary(group => group.Key, group => new Queue<ArmoryKit>(group.Select(pair => pair.Value)));
            foreach (int seed in loadout.BySeed.Keys.Where(seed => loadout.Outcomes[seed] == 0 || loadout.Outcomes[seed] == 2).ToList())
            {
                loadout.BySeed.Remove(seed);
                loadout.Outcomes.Remove(seed);
            }
            foreach (var soldier in party.Troops.OrderBy(soldier => soldier.Descriptor.UniqueSeed))
            {
                int outcome = soldier.IsKilled ? 1 : soldier.IsRouted ? 3 : soldier.IsWounded ? 2 : 0;
                int seed = soldier.Descriptor.UniqueSeed;
                if (loadout.BySeed.ContainsKey(seed)) continue;
                if (reusable.TryGetValue((soldier.Troop, outcome), out Queue<ArmoryKit>? kits) && kits.Count > 0)
                {
                    loadout.BySeed.Add(seed, kits.Dequeue());
                    loadout.Outcomes.Add(seed, outcome);
                }
            }
            ItemRoster available = new ItemRoster();
            available.Add(ItemsOf(_armories, loadout.Holder));
            foreach (ArmoryKit kit in loadout.BySeed.Values)
                foreach (var item in kit.Items.Where(item => !item.IsEmpty)) available.AddToCounts(item, -1);
            TroopRoster unassigned = TroopRoster.CreateDummyTroopRoster();
            foreach (var soldier in party.Troops.Where(soldier => !loadout.BySeed.ContainsKey(soldier.Descriptor.UniqueSeed)))
                unassigned.AddToCounts(soldier.Troop, 1);
            bool villagers = IsFaithVillager(party.Party.MobileParty);
            ArmoryPlan additions = BuildPlan(available, ArmoryTroops(unassigned, villagers), 0, villagers);
            foreach (var group in party.Troops.Where(soldier => !loadout.BySeed.ContainsKey(soldier.Descriptor.UniqueSeed))
                .OrderBy(soldier => soldier.Descriptor.UniqueSeed).GroupBy(soldier => soldier.Troop))
            {
                var kits = new Queue<ArmoryKit>(additions.Sets.Where(kit => kit.Troop == group.Key));
                foreach (var soldier in group)
                {
                    if (kits.Count == 0) break;
                    int seed = soldier.Descriptor.UniqueSeed;
                    loadout.BySeed.Add(seed, kits.Dequeue());
                    loadout.Outcomes.Add(seed, soldier.IsKilled ? 1 : soldier.IsRouted ? 3 : soldier.IsWounded ? 2 : 0);
                }
            }
            RefreshBattlePlan(party, loadout);
        }

        private static void RefreshBattlePlan(MapEventParty party, ArmoryBattleLoadout loadout)
        {
            loadout.Plan.Sets.Clear();
            loadout.Plan.Sets.AddRange(party.Troops.Where(soldier => loadout.BySeed.ContainsKey(soldier.Descriptor.UniqueSeed))
                .Select(soldier => loadout.BySeed[soldier.Descriptor.UniqueSeed]));
            loadout.Plan.TroopTiers.Clear();
            foreach (var group in loadout.Plan.Sets.GroupBy(kit => kit.Troop))
            {
                Tiers(group, out float weapon, out float armor);
                loadout.Plan.TroopTiers[group.Key] = (weapon, armor);
            }
        }

        internal static void BeforeArmoryRosterUpdate(MapEventParty party) => _instance?.CaptureBattleOutcomes(party);
        internal static void AfterArmoryRosterUpdate(MapEventParty party)
        {
            if (_instance == null) return;
            if (_instance._battleLoadouts.ContainsKey(party)) _instance.RebindBattleLoadout(party);
            else
            {
                string? holder = _instance._battleOwners.TryGetValue(party, out string? owner) ? owner : _instance.HolderOf(party.Party);
                if (holder != null && _instance._armories.ContainsKey(holder)) _instance.BattleLoadoutFor(holder, party);
            }
        }

        private void SettleBattleLosses(MapEvent mapEvent)
        {
            if (mapEvent.BattleState == BattleState.AttackerVictory || mapEvent.BattleState == BattleState.DefenderVictory)
            {
                MapEventSide loser = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
                MapEventSide winner = mapEvent.GetMapEventSide(mapEvent.WinningSide);
                ItemRoster? looter = winner.Parties.Any(entry => entry.Party == PartyBase.MainParty)
                    ? PartyBase.MainParty.ItemRoster : winner.LeaderParty?.ItemRoster;
                foreach (MapEventParty party in loser.Parties)
                {
                    string? holder = _battleOwners.TryGetValue(party, out string? owner) ? owner : HolderOf(party.Party);
                    if (holder == null || !_armories.ContainsKey(holder)) continue;
                    ArmoryBattleLoadout loadout = BattleLoadoutFor(holder, party);
                    ItemRoster ledger = ItemsOf(_armories, holder);
                    int lost = 0, looted = 0, men = 0;
                    CaptureBattleOutcomes(party);
                    foreach (var pair in loadout.BySeed)
                    {
                        int outcome = loadout.Outcomes[pair.Key];
                        if (outcome == 3 || mapEvent.EndedByRetreat && outcome == 0) continue;
                        ArmoryKit kit = pair.Value;
                        men++;
                        foreach (EquipmentElement item in kit.Items.Where(entry => !entry.IsEmpty))
                        {
                            ledger.AddToCounts(item, -1);
                            lost++;
                            if (looter != null && MBRandom.RandomFloat < GwpTuning.Firesworn.ArmoryLossLootShare)
                            {
                                looter.AddToCounts(item, 1);
                                looted++;
                            }
                        }
                    }
                    _plans.Remove(holder);
#if GWP_DIAGNOSTICS
                    GwpAiDiagnostics.WriteFaith("ARMORY_LOST_IN_DEFEAT",
                        "holder=" + holder + "; men=" + men + "; items=" + lost + "; lootedByWinner=" + looted +
                        "; winner=" + (winner.LeaderParty?.Name?.ToString() ?? "-") + "; mapEvent=" + mapEvent.EventType);
#endif
                }
            }
            foreach (MapEventParty party in mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties))
            {
                _battleLoadouts.Remove(party);
                _battleOwners.Remove(party);
            }
        }
    }
    [HarmonyPatch(typeof(MapEventParty), nameof(MapEventParty.Update))]
    internal static class GwpArmoryBattleRosterPatch
    {
        private static void Prefix(MapEventParty __instance) => GwpFireswornBehavior.BeforeArmoryRosterUpdate(__instance);
        private static void Postfix(MapEventParty __instance) => GwpFireswornBehavior.AfterArmoryRosterUpdate(__instance);
    }

}
