using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 运行时状态薄封装。
    /// 目标不是立刻替换底层 static 池，而是先统一访问入口和生命周期操作。
    /// </summary>
    internal static class GwpRuntimeState
    {
        public static CrimeState Crime { get; } = new CrimeState();
        public static PlayerState Player { get; } = new PlayerState();

        public static void ResetForNewGame()
        {
            Player.ResetForNewGame();
            Crime.ResetForNewGame();
        }

        public static void SyncPlayerBehaviorData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                int reputation = Player.Reputation;
                dataStore.SyncData("gwp_reputation", ref reputation);

                List<string> victimFactionIds = Player.VictimFactions
                    .Where(static faction => faction != null)
                    .Select(static faction => faction.StringId)
                    .ToList();

                int victimCount = victimFactionIds.Count;
                dataStore.SyncData("gwp_victim_count", ref victimCount);
                for (int i = 0; i < victimFactionIds.Count; i++)
                {
                    string factionId = victimFactionIds[i];
                    dataStore.SyncData($"gwp_victim_{i}", ref factionId);
                }

                return;
            }

            if (!dataStore.IsLoading) return;

            // CrimePool 已由 PoliceEnforcementBehavior.SyncData 恢复，这里只处理玩家行为状态。
            Player.ClearAll();

            int loadedReputation = 0;
            dataStore.SyncData("gwp_reputation", ref loadedReputation);
            Player.ResetReputation(loadedReputation);

            int victimCountOnLoad = 0;
            dataStore.SyncData("gwp_victim_count", ref victimCountOnLoad);
            for (int i = 0; i < victimCountOnLoad; i++)
            {
                string factionId = string.Empty;
                dataStore.SyncData($"gwp_victim_{i}", ref factionId);

                IFaction? faction = FindFactionById(factionId);
                if (faction != null)
                    Player.AddVictimFactionOnLoad(faction);
            }
        }

        private static IFaction? FindFactionById(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;

            return (IFaction?)Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == factionId)
                ?? Clan.All.FirstOrDefault(clan => clan.StringId == factionId);
        }

        internal sealed class CrimeState
        {
            public bool IsAccepting => CrimePool.IsAccepting;
            public bool IsDispatchReady => CrimePool.IsDispatchReady;
            public bool IsPlayerHunted => CrimePool.IsPlayerHunted;
            public IReadOnlyDictionary<string, PoliceTask> ActiveTasks => CrimePool.ActiveTasks;

            public void ResetForNewGame() => CrimePool.ClearAll();
            public void SyncData(IDataStore dataStore) => CrimePool.SyncData(dataStore);
            public void RefreshAccepting() => CrimePool.RefreshAccepting();
            public void Clean() => CrimePool.Clean();
            public bool HasTask(string policePartyId) => CrimePool.HasTask(policePartyId);
            public PoliceTask? GetTask(string policePartyId) => CrimePool.GetTask(policePartyId);
            public void BeginTask(string policePartyId, CrimeRecord crime) => CrimePool.BeginTask(policePartyId, crime);
            public void EndTask(string policePartyId) => CrimePool.EndTask(policePartyId);
            public void EndPlayerHunt() => CrimePool.EndPlayerHunt();
            public bool TryAdd(string crimeType, MobileParty offender, TaleWorlds.Library.Vec2 location, string victimName) =>
                CrimePool.TryAdd(crimeType, offender, location, victimName);
            public bool TryAddPlayerCrime(string crimeType, TaleWorlds.Library.Vec2 location, string detail) =>
                CrimePool.TryAddPlayerCrime(crimeType, location, detail);
            public CrimeRecord? GetNearest(TaleWorlds.Library.Vec2 position) => CrimePool.GetNearest(position);
            public CrimeRecord? GetNearestNonPlayer(TaleWorlds.Library.Vec2 position) => CrimePool.GetNearestNonPlayer(position);
            public CrimeRecord? GetNearestNonPlayerFromAll(TaleWorlds.Library.Vec2 position) => CrimePool.GetNearestNonPlayerFromAll(position);
            public CrimeRecord? GetByOffenderId(string? partyStringId) => CrimePool.GetByOffenderId(partyStringId);
            public CrimeRecord? GetPlayerCrime() => CrimePool.GetPlayerCrime();
            public string? GetPlayerTaskPolicePartyId() => CrimePool.GetPlayerTaskPolicePartyId();
            public string? GetAssignedPolicePartyId(string? offenderStringId) => CrimePool.GetAssignedPolicePartyId(offenderStringId);
            public void SetBountyEscortFlag(string policePartyId, bool value) => CrimePool.SetBountyEscortFlag(policePartyId, value);
            public bool TryAssignPlayerCrimeToPolice(string policePartyId) => CrimePool.TryAssignPlayerCrimeToPolice(policePartyId);
            public bool RemovePendingCrimeByOffenderId(string? offenderStringId) => CrimePool.RemovePendingCrimeByOffenderId(offenderStringId);
            public List<MobileParty> GetAllTrackedOffenders(bool includePlayer = false) => CrimePool.GetAllTrackedOffenders(includePlayer);
            public List<MobileParty> GetTrackedOffendersByFaction(IFaction? faction) => CrimePool.GetTrackedOffendersByFaction(faction);
        }

        internal sealed class PlayerState
        {
            public int Reputation => PlayerBehaviorPool.Reputation;
            public bool IsWanted => PlayerBehaviorPool.IsWanted;
            public bool HasAtonementTask => PlayerBehaviorPool.HasAtonementTask;
            public IReadOnlyCollection<IFaction> VictimFactions => PlayerBehaviorPool.VictimFactions;

            public void ResetForNewGame() => PlayerBehaviorPool.ClearAll();
            public void ClearAll() => PlayerBehaviorPool.ClearAll();
            public void ResetReputation(int value) => PlayerBehaviorPool.ResetReputation(value);
            public void ChangeReputation(int delta) => PlayerBehaviorPool.ChangeReputation(delta);
            public void AddCrime(string type, TaleWorlds.Library.Vec2 location, string detail, IFaction? victimFaction = null) =>
                PlayerBehaviorPool.AddCrime(type, location, detail, victimFaction);
            public void AddCrimeRecord(string type, TaleWorlds.Library.Vec2 location, string detail, IFaction? victimFaction = null) =>
                PlayerBehaviorPool.AddCrimeRecord(type, location, detail, victimFaction);
            public void AddGoodDeed(string type, TaleWorlds.Library.Vec2 location, string detail) =>
                PlayerBehaviorPool.AddGoodDeed(type, location, detail);
            public string GetReputationDisplay() => PlayerBehaviorPool.GetReputationDisplay();
            public void SetAtonementTaskActive(bool active) => PlayerBehaviorPool.SetAtonementTaskActive(active);
            public void AddVictimFactionOnLoad(IFaction faction) => PlayerBehaviorPool.AddVictimFactionOnLoad(faction);
            public void ClearVictimFactions() => PlayerBehaviorPool.ClearVictimFactions();
        }
    }
}
