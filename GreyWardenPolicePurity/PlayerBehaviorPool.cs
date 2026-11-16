using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
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
        internal const int LegacyGoodDeedKillsPerPoint = 10;

        public static int Reputation { get; private set; } = 0;
        internal static int ReputationUnits => Reputation * WardenReputationRules.ReputationUnitsPerPoint + _reputationRemainder;
        internal static decimal PreciseReputation => ReputationUnits / (decimal)WardenReputationRules.ReputationUnitsPerPoint;
        private static int RemainingReputationUnits => MaxReputation * WardenReputationRules.ReputationUnitsPerPoint - ReputationUnits;
        private static int _reputationRemainder;
        private static int _goodDeedRewardProgressUnits;
        private static int _wardenUpgradeProgress;
        private static int _prisonerTradeProgress;
        private static int _troopTradeProgress;
        private static int _armoryTradeProgress;
        public static bool IsWanted => PreciseReputation <= -11;
        public static bool HasAtonementTask { get; private set; } = false;
        public static IReadOnlyCollection<IFaction> VictimFactions => _victimFactions;

        public static void ClearVictimFactions() => _victimFactions.Clear();

        public static void AddVictimFactionOnLoad(IFaction faction)
        {
            if (faction != null) _victimFactions.Add(faction);
        }

        public static void AddCrime(string type, Vec2 location, string detail, IFaction? victimFaction = null)
        {
            SetReputationUnits(ReputationUnits - WardenReputationRules.ReputationUnitsPerPoint);

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

        internal static string FormatReputationUnits(int units) =>
            (units / (decimal)WardenReputationRules.ReputationUnitsPerPoint).ToString("0.#", CultureInfo.CurrentCulture);

        public static string GetReputationDisplay() => GwpText.Get(GwpTextCatalog.gwp_gwpdata_005, "VAR_1", FormatReputationUnits(ReputationUnits));

        public static void ResetReputation(int value)
        {
            int before = Reputation;
            SetReputationUnits(value * WardenReputationRules.ReputationUnitsPerPoint);
            GwpAiDiagnostics.WritePlayerJusticeState(
                "CUSTOM_REPUTATION_RESET",
                "before=" + before + "; requested=" + value + "; after=" + Reputation);
        }
        private static (int ReputationUnits, int Remainder) QuoteGoodDeedReputation((int Personal, int MainParty) weights)
        {
            int weightUnits = WardenReputationRules.BattleRewardWeightUnits(weights.Personal, weights.MainParty);
            return weightUnits > 0
                ? WardenReputationRules.QuoteRewardProgress(_goodDeedRewardProgressUnits, weightUnits,
                    WardenReputationRules.BattleWeightUnitsPerPoint, RemainingReputationUnits)
                : (0, _goodDeedRewardProgressUnits);
        }

        internal static int PreviewGoodDeedReputation((int Personal, int MainParty) weights) =>
            QuoteGoodDeedReputation(weights).ReputationUnits;

        internal static bool TryAwardGoodDeedReputation((int Personal, int MainParty) weights)
        {
            var award = QuoteGoodDeedReputation(weights);
            _goodDeedRewardProgressUnits = award.Remainder;
            if (award.ReputationUnits <= 0) return false;

            ChangeReputationUnits(award.ReputationUnits);
            return true;
        }
        public static void ChangeReputation(int delta)
        {
            ChangeReputationUnits(delta * WardenReputationRules.ReputationUnitsPerPoint);
        }

        private static void ChangeReputationUnits(int delta)
        {
            int before = ReputationUnits;
            SetReputationUnits(before + delta);
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WritePlayerJusticeState(
                "CUSTOM_REPUTATION_CHANGED",
                "before=" + FormatReputationUnits(before) + "; delta=" + FormatReputationUnits(delta) + "; after=" + FormatReputationUnits(ReputationUnits));
#endif
        }

        private static void SetReputationUnits(int units)
        {
            const int scale = WardenReputationRules.ReputationUnitsPerPoint;
            int clamped = Math.Max(MinReputation * scale, Math.Min(MaxReputation * scale, units));
            Reputation = WardenReputationRules.WholePointsFromUnits(clamped);
            _reputationRemainder = clamped - Reputation * scale;
        }

        internal static bool TrySpendReputationUnits(int cost)
        {
            if (cost <= 0 || cost > ReputationUnits) return false;
            ChangeReputationUnits(-cost);
            return true;
        }

        internal static int WardenUpgradeCost(int count) =>
            WardenReputationRules.UpgradeCost(_wardenUpgradeProgress, count);

        internal static int AffordableWardenUpgrades =>
            WardenReputationRules.AffordableUpgrades(Reputation, _wardenUpgradeProgress);

        internal static void PayForWardenUpgrades(int count)
        {
            int cost = WardenUpgradeCost(count);
            _wardenUpgradeProgress = WardenReputationRules.UpgradeProgress(_wardenUpgradeProgress, count);
            if (cost > 0) ChangeReputation(-cost);
        }

        internal static (int ReputationUnits, int Remainder) QuoteFireswornTrade(FireswornTradeKind kind, long units) =>
            FireswornTradeRules.Quote(kind, FireswornTradeProgress(kind), units, RemainingReputationUnits);

        internal static (int ReputationUnits, int Remainder) CompleteFireswornTrade(FireswornTradeKind kind, long units)
        {
            var quote = QuoteFireswornTrade(kind, units);
            FireswornTradeProgress(kind) = quote.Remainder;
            if (quote.ReputationUnits > 0) ChangeReputationUnits(quote.ReputationUnits);
            return quote;
        }

        private static ref int FireswornTradeProgress(FireswornTradeKind kind)
        {
            switch (kind)
            {
                case FireswornTradeKind.Prisoners: return ref _prisonerTradeProgress;
                case FireswornTradeKind.Troops: return ref _troopTradeProgress;
                case FireswornTradeKind.Armory: return ref _armoryTradeProgress;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void SyncReputationIncomeProgress(IDataStore dataStore)
        {
            bool usesTierWeights = dataStore.IsSaving;
            dataStore.SyncData("gwp_player_tier_reward_weights", ref usesTierWeights);
            bool hasBattleWeightUnits = dataStore.SyncData("gwp_good_deed_reward_weight_units", ref _goodDeedRewardProgressUnits);
            dataStore.SyncData("gwp_player_prisoner_trade_progress", ref _prisonerTradeProgress);
            dataStore.SyncData("gwp_player_troop_trade_progress", ref _troopTradeProgress);
            dataStore.SyncData("gwp_player_armory_trade_progress", ref _armoryTradeProgress);

            if (!dataStore.IsLoading) return;
            if (!hasBattleWeightUnits)
            {
                int legacyBattleProgress = 0;
                dataStore.SyncData("gwp_good_deed_kill_progress", ref legacyBattleProgress);
                if (!usesTierWeights)
                    legacyBattleProgress = RescaleLegacyRewardProgress(legacyBattleProgress, LegacyGoodDeedKillsPerPoint);
                _goodDeedRewardProgressUnits = legacyBattleProgress * WardenReputationRules.TroopBattleRewardDivisor;
            }
            if (usesTierWeights) return;
            // 旧档的余数按还差多少才满一分折回新权重，已经入账的声望不参与换算。
            _prisonerTradeProgress = RescaleLegacyRewardProgress(_prisonerTradeProgress, 5);
            _troopTradeProgress = RescaleLegacyRewardProgress(_troopTradeProgress, 5);
        }

        private static int RescaleLegacyRewardProgress(int progress, int oldRate) =>
            Math.Max(0, Math.Min(oldRate - 1, progress)) * WardenReputationRules.TroopWeightPerPoint / oldRate;

        public static void SetAtonementTaskActive(bool active) => HasAtonementTask = active;

        public static void ClearAll()
        {
            Reputation = 0;
            _reputationRemainder = 0;
            _goodDeedRewardProgressUnits = 0;
            _wardenUpgradeProgress = 0;
            _prisonerTradeProgress = 0;
            _troopTradeProgress = 0;
            _armoryTradeProgress = 0;
            HasAtonementTask = false;
            _victimFactions.Clear();
        }
        public static void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                int reputation = Reputation;
                dataStore.SyncData("gwp_reputation", ref reputation);
                dataStore.SyncData("gwp_reputation_order_remainder", ref _reputationRemainder);

                dataStore.SyncData("gwp_warden_upgrade_progress", ref _wardenUpgradeProgress);
                SyncReputationIncomeProgress(dataStore);

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
            int reputationRemainder = 0;
            dataStore.SyncData("gwp_reputation_order_remainder", ref reputationRemainder);
            SetReputationUnits(ReputationUnits + reputationRemainder);

            dataStore.SyncData("gwp_warden_upgrade_progress", ref _wardenUpgradeProgress);
            SyncReputationIncomeProgress(dataStore);

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
