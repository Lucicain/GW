using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    // ===================== 玩家行为数据 =====================
    /// <summary>
    /// 玩家行为池：玩家的灰袍声望与受害势力
    /// 声望：正数=好人，0=中立，负数=坏人（≤-11 通缉，由正式灰袍承办；-1~-10 派纠察队）
    /// </summary>
    public static class PlayerBehaviorPool
    {
        private static readonly HashSet<IFaction> _victimFactions = new HashSet<IFaction>();

        public const int MaxReputation =  100; // 声望上限
        public const int MinReputation = -100; // 声望下限

        public static int Reputation { get; private set; } = 0;
        public static int GoodDeedKillProgress { get; private set; } = 0;
        public static bool IsWanted => Reputation <= -11;
        public static bool HasAtonementTask { get; private set; } = false;
        public static IReadOnlyCollection<IFaction> VictimFactions => _victimFactions;

        public static void ClearVictimFactions() => _victimFactions.Clear();

        public static void AddVictimFactionOnLoad(IFaction faction)
        {
            if (faction != null) _victimFactions.Add(faction);
        }

        public static void AddCrime(string type, Vec2 location, string detail, IFaction? victimFaction = null)
        {
            Reputation = Math.Max(Reputation - 1, MinReputation);

            if (victimFaction != null) _victimFactions.Add(victimFaction);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get(GwpTextCatalog.gwp_gwpdata_003, "VAR_1", type, "VAR_2", detail), Colors.Red));

            if (IsWanted)
                CrimePool.TryAddPlayerCrime(type, location, detail);
        }

        /// <summary>
        /// 只记受害势力，不扣声望、不弹通知、不触发追捕：声望由调用方在战斗结束后按击败人数结算。
        /// </summary>
        public static void TrackVictimFaction(IFaction? victimFaction)
        {
            if (victimFaction != null) _victimFactions.Add(victimFaction);
        }

        public static string GetReputationDisplay() => GwpText.Get(GwpTextCatalog.gwp_gwpdata_005, "VAR_1", Reputation);

        public static void ResetReputation(int value)
        {
            int before = Reputation;
            Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, value));
            GwpAiDiagnostics.WritePlayerJusticeState(
                "CUSTOM_REPUTATION_RESET",
                "before=" + before + "; requested=" + value + "; after=" + Reputation);
        }
        public static void ResetGoodDeedKillProgress(int value) => GoodDeedKillProgress = Math.Max(0, Math.Min(9, value));

        public static int AccumulateGoodDeedKills(int killCount)
        {
            if (killCount <= 0) return 0;

            int accumulated = GoodDeedKillProgress + killCount;
            int reputationGain = accumulated / 10;
            GoodDeedKillProgress = accumulated % 10;
            return reputationGain;
        }
        public static void ChangeReputation(int delta)
        {
            int before = Reputation;
            Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, Reputation + delta));
            GwpAiDiagnostics.WritePlayerJusticeState(
                "CUSTOM_REPUTATION_CHANGED",
                "before=" + before + "; delta=" + delta + "; after=" + Reputation);
        }
        public static void SetAtonementTaskActive(bool active) => HasAtonementTask = active;

        public static void ClearAll()
        {
            Reputation = 0;
            GoodDeedKillProgress = 0;
            HasAtonementTask = false;
            _victimFactions.Clear();
        }
        public static void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                int reputation = Reputation;
                dataStore.SyncData("gwp_reputation", ref reputation);

                int goodDeedKillProgress = GoodDeedKillProgress;
                dataStore.SyncData("gwp_good_deed_kill_progress", ref goodDeedKillProgress);

                List<string> victimFactionIds = VictimFactions
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

            // 只恢复本池；案卷由 PoliceEnforcementBehavior.SyncData 独立恢复。
            ClearAll();

            int loadedReputation = 0;
            dataStore.SyncData("gwp_reputation", ref loadedReputation);
            ResetReputation(loadedReputation);

            int loadedGoodDeedKillProgress = 0;
            dataStore.SyncData("gwp_good_deed_kill_progress", ref loadedGoodDeedKillProgress);
            ResetGoodDeedKillProgress(loadedGoodDeedKillProgress);

            int victimCountOnLoad = 0;
            dataStore.SyncData("gwp_victim_count", ref victimCountOnLoad);
            for (int i = 0; i < victimCountOnLoad; i++)
            {
                string factionId = string.Empty;
                dataStore.SyncData($"gwp_victim_{i}", ref factionId);

                IFaction? faction = FindFactionById(factionId);
                if (faction != null)
                    AddVictimFactionOnLoad(faction);
            }
        }

        private static IFaction? FindFactionById(string factionId)
        {
            if (string.IsNullOrEmpty(factionId)) return null;

            return (IFaction?)Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == factionId)
                ?? Clan.All.FirstOrDefault(clan => clan.StringId == factionId);
        }

    }
}
