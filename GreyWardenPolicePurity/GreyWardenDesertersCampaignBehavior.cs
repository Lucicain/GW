using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    public sealed class GreyWardenDesertersCampaignBehavior : CampaignBehaviorBase
    {
        private const int MinimumDeserterPartyCount = 15;
        private const int MaximumDeserterPartyCount = 40;
        private const int MaxDeserterPartyCountAfterBattle = 3;
        private const int MaxDeserterPartyCountAfterArmyBattle = 5;

        private Clan? _deserterClan;

        private static int MergePartiesMaxSize => 120;

        private float DesertersSpawnRadiusAroundVillages =>
            0.2f * Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay;

        private Clan? DeserterClan
        {
            get
            {
                _deserterClan ??= Clan.FindFirst(static x => x.StringId == "deserters");
                return _deserterClan;
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, MapEventEnded);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyTickParty);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void HourlyTickParty(MobileParty party)
        {
            if (!GwpCommon.IsDeserterParty(party) || !CanPartyMerge(party) || party.MemberRoster.TotalRegulars >= MergePartiesMaxSize)
                return;

            LocatableSearchData<MobileParty> data = MobileParty.StartFindingLocatablesAroundPosition(
                party.Position.ToVec2(),
                GetMergeDistance(party));

            for (MobileParty mobileParty = MobileParty.FindNextLocatable(ref data);
                 mobileParty != null;
                 mobileParty = MobileParty.FindNextLocatable(ref data))
            {
                if (!GwpCommon.IsDeserterParty(mobileParty)
                    || mobileParty == party
                    || !CanPartyMerge(mobileParty)
                    || mobileParty.MemberRoster.TotalRegulars + party.MemberRoster.TotalRegulars > MergePartiesMaxSize
                    || MBRandom.RandomFloat >= 0.05f)
                {
                    continue;
                }

                MergeParties(party, mobileParty);
                break;
            }
        }

        private static bool CanPartyMerge(MobileParty mobileParty)
        {
            if (!mobileParty.IsActive || mobileParty.MapEvent != null || mobileParty.IsCurrentlyUsedByAQuest || mobileParty.ShortTermBehavior == AiBehavior.EngageParty)
                return false;

            return !mobileParty.IsFleeing();
        }

        private static void MergeParties(MobileParty party, MobileParty nearbyParty)
        {
            Debug.Print($"Deserter parties {party.StringId} of {party.MemberRoster.TotalManCount} and {nearbyParty.StringId} of {nearbyParty.MemberRoster.TotalManCount} merged.");
            party.MemberRoster.Add(nearbyParty.MemberRoster);
            foreach (TroopRosterElement item in nearbyParty.PrisonRoster.GetTroopRoster())
            {
                if (item.Character.HeroObject != null)
                    TransferPrisonerAction.Apply(item.Character, nearbyParty.Party, party.Party);
            }

            if (party.PrisonRoster.Count > 0)
                party.PrisonRoster.Add(nearbyParty.PrisonRoster);

            party.PartyTradeGold += nearbyParty.PartyTradeGold;
            party.ItemRoster.Add(nearbyParty.ItemRoster);
            DestroyPartyAction.Apply(null, nearbyParty);
            PartyBaseHelper.SortRoster(party);
        }

        private void MapEventEnded(MapEvent mapEvent)
        {
            Clan? deserterClan = DeserterClan;
            if (mapEvent.IsNavalMapEvent
                || (!mapEvent.IsFieldBattle && !mapEvent.IsSiegeAssault && !mapEvent.IsSiegeOutside && !mapEvent.IsSallyOut)
                || !mapEvent.HasWinner
                || deserterClan == null
                || deserterClan.WarPartyComponents.Count >= Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(deserterClan))
            {
                return;
            }

            MapEventSide mapEventSide = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (MapEventParty party in mapEventSide.Parties)
            {
                if (!CanPartyGenerateDeserters(party))
                    continue;

                troopRoster.Add(party.RoutedInBattle);
                troopRoster.Add(party.DiedInBattle);
            }

            if (MBRandom.RandomFloat >= 0.9f)
                return;

            troopRoster.RemoveIf(static x => x.Character.IsHero);
            GwpCommon.RemoveGreyWardenTroops(troopRoster);
            if (troopRoster.TotalManCount < MinimumDeserterPartyCount)
                return;

            TrySpawnDeserters(mapEvent, troopRoster);
        }

        private static bool CanPartyGenerateDeserters(MapEventParty mapEventParty)
        {
            return mapEventParty.Party.IsMobile
                   && mapEventParty.Party.MobileParty.IsLordParty
                   && mapEventParty.Party.MobileParty.ActualClan != null
                   && !mapEventParty.Party.MobileParty.ActualClan.IsMinorFaction;
        }

        private void TrySpawnDeserters(MapEvent mapEvent, TroopRoster routedTroops)
        {
            int maxDeserterPartyCountForMapEvent = GetMaxDeserterPartyCountForMapEvent(mapEvent);
            List<TroopRoster> rostersSuitableForDeserters = GetRostersSuitableForDeserters(routedTroops, maxDeserterPartyCountForMapEvent);
            List<Settlement> settlements = SelectRandomSettlementsForDeserters(mapEvent, rostersSuitableForDeserters.Count);
            for (int i = 0; i < rostersSuitableForDeserters.Count; i++)
            {
                SpawnDesertersParty(rostersSuitableForDeserters[i], settlements[i]);
            }
        }

        private static int GetMaxDeserterPartyCountForMapEvent(MapEvent mapEvent)
        {
            bool attackerArmy = mapEvent.AttackerSide.Parties.Any(static x =>
                CanPartyGenerateDeserters(x)
                && x.Party.MobileParty.Army != null
                && (x.Party.MobileParty.AttachedTo != null || x.Party.MobileParty.Army.LeaderParty == x.Party.MobileParty));
            bool defenderArmy = mapEvent.DefenderSide.Parties.Any(static x =>
                CanPartyGenerateDeserters(x)
                && x.Party.MobileParty.Army != null
                && (x.Party.MobileParty.AttachedTo != null || x.Party.MobileParty.Army.LeaderParty == x.Party.MobileParty));
            return attackerArmy && defenderArmy ? MaxDeserterPartyCountAfterArmyBattle : MaxDeserterPartyCountAfterBattle;
        }

        private List<TroopRoster> GetRostersSuitableForDeserters(TroopRoster routedTroops, int maxPartyCount)
        {
            Clan? deserterClan = DeserterClan;
            if (deserterClan == null)
                return new List<TroopRoster>();

            int maxSupported = Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(deserterClan);
            int availableParties = Math.Min(maxPartyCount, maxSupported - deserterClan.WarPartyComponents.Count);
            int countByTroops = routedTroops.TotalManCount / MinimumDeserterPartyCount;
            int partyCount = Math.Min(availableParties, countByTroops);
            List<TroopRoster> result = new List<TroopRoster>();
            for (int i = 0; i < partyCount; i++)
            {
                result.Add(routedTroops.RemoveNumberOfNonHeroTroopsRandomly(
                    Math.Min(routedTroops.TotalManCount / (partyCount - i), MaximumDeserterPartyCount)));
            }

            return result;
        }

        private void SpawnDesertersParty(TroopRoster troops, Settlement settlement)
        {
            Clan? deserterClan = DeserterClan;
            if (deserterClan == null)
                return;

            CampaignVec2 spawnPosition = GetDeserterSpawnPosition(settlement);
            MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(
                deserterClan.StringId + "_1",
                deserterClan,
                settlement,
                isBossParty: false,
                null,
                spawnPosition);
            mobileParty.MemberRoster.Add(troops);
            InitializeDeserterParty(mobileParty, deserterClan);
            mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, MobileParty.NavigationType.Default);
            PartyBaseHelper.SortRoster(mobileParty);
            Debug.Print(mobileParty.StringId + " deserter party was created around: " + settlement.Name);
        }

        private List<Settlement> SelectRandomSettlementsForDeserters(MapEvent mapEvent, int count)
        {
            List<Settlement> settlements = FindSettlementsAroundPoint(
                mapEvent.Position,
                static x => x.IsVillage,
                MobileParty.NavigationType.Default,
                GetMaxVillageDistance());
            if (settlements.Count > count)
            {
                settlements.Shuffle();
                return settlements.Take(count).ToList();
            }

            if (settlements.Count == 0)
                settlements.Add(SettlementHelper.FindNearestSettlementToPoint(mapEvent.Position, static x => x.IsVillage));

            int initialCount = settlements.Count;
            for (int i = 0; i < count - initialCount; i++)
                settlements.Add(settlements[MBRandom.RandomInt(0, initialCount - 1)]);

            return settlements;
        }

        private static List<Settlement> FindSettlementsAroundPoint(in CampaignVec2 point, Func<Settlement, bool>? condition, MobileParty.NavigationType navCapabilities, float maxDistance)
        {
            List<Settlement> list = new List<Settlement>();
            foreach (Settlement item in Settlement.All)
            {
                if ((condition == null || condition(item)) && item.Position.Distance(point) < maxDistance)
                    list.Add(item);
            }

            return list;
        }

        private float GetMaxVillageDistance()
        {
            return Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay / 2f;
        }

        private CampaignVec2 GetDeserterSpawnPosition(Settlement settlement)
        {
            CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(
                settlement.GatePosition,
                MobileParty.NavigationType.Default,
                DesertersSpawnRadiusAroundVillages);
            float maxDistanceSquared = MobileParty.MainParty.SeeingRange * MobileParty.MainParty.SeeingRange;
            if (campaignVec.DistanceSquared(MobileParty.MainParty.Position) < maxDistanceSquared)
            {
                for (int i = 0; i < 15; i++)
                {
                    CampaignVec2 candidate = NavigationHelper.FindReachablePointAroundPosition(
                        campaignVec,
                        MobileParty.NavigationType.Default,
                        DesertersSpawnRadiusAroundVillages);
                    if (!NavigationHelper.IsPositionValidForNavigationType(candidate, MobileParty.NavigationType.Default))
                        continue;

                    float landRatio;
                    float distance = DistanceHelper.FindClosestDistanceFromMobilePartyToPoint(
                        MobileParty.MainParty,
                        candidate,
                        MobileParty.NavigationType.Default,
                        out landRatio);
                    if (distance * distance <= maxDistanceSquared)
                        continue;

                    campaignVec = candidate;
                    break;
                }
            }

            return campaignVec;
        }

        private static void InitializeDeserterParty(MobileParty banditParty, Clan deserterClan)
        {
            banditParty.Party.SetVisualAsDirty();
            banditParty.ActualClan = deserterClan;
            banditParty.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
            CreatePartyTrade(banditParty);
            GiveFoodToBanditParty(banditParty);
        }

        private static void CreatePartyTrade(MobileParty banditParty)
        {
            int initialGold = (int)(10f * banditParty.Party.MemberRoster.TotalManCount * (0.5f + MBRandom.RandomFloat));
            banditParty.InitializePartyTrade(initialGold);
        }

        private static void GiveFoodToBanditParty(MobileParty banditParty)
        {
            foreach (ItemObject item in Items.All)
            {
                if (!item.IsFood)
                    continue;

                int count = MBRandom.RoundRandomized(
                    banditParty.MemberRoster.TotalManCount
                    * (1f / item.Value)
                    * 8f
                    * MBRandom.RandomFloat
                    * MBRandom.RandomFloat
                    * MBRandom.RandomFloat
                    * MBRandom.RandomFloat);
                if (count > 0)
                    banditParty.ItemRoster.AddToCounts(item, count);
            }
        }

        private static float GetMergeDistance(MobileParty mobileParty)
        {
            return mobileParty.Speed * 2f;
        }
    }
}
