using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal static class GwpAiDeterrenceState
    {
        private sealed class DeterrenceEntry
        {
            public string Key { get; set; } = string.Empty;
            public string HeroId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public float PenaltyPoints { get; set; }
            public float LastUpdatedHours { get; set; }
            public float LastEnforcementHours { get; set; }
        }

        private static readonly Dictionary<string, DeterrenceEntry> _entries =
            new Dictionary<string, DeterrenceEntry>(StringComparer.OrdinalIgnoreCase);

        public static void ClearAll() => _entries.Clear();

        public static void RegisterEnforcementDefeat(MobileParty offender)
        {
            string? maybeKey = GetKey(offender);
            if (string.IsNullOrEmpty(maybeKey)) return;
            string key = maybeKey!;

            float nowHours = (float)CampaignTime.Now.ToHours;
            Hero? leader = offender.LeaderHero;

            if (!_entries.TryGetValue(key, out DeterrenceEntry? entry))
            {
                entry = new DeterrenceEntry
                {
                    Key = key,
                    HeroId = leader?.StringId ?? string.Empty,
                    DisplayName = leader?.Name?.ToString() ?? offender.Name?.ToString() ?? key,
                    PenaltyPoints = 0f,
                    LastUpdatedHours = nowHours,
                    LastEnforcementHours = nowHours
                };
                _entries[key] = entry;
            }

            float effectivePenalty = GetEffectivePenaltyInternal(entry, leader, nowHours, updateEntry: true);
            entry.HeroId = leader?.StringId ?? entry.HeroId;
            entry.DisplayName = leader?.Name?.ToString() ?? offender.Name?.ToString() ?? entry.DisplayName;
            entry.PenaltyPoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap,
                effectivePenalty + GwpTuning.Deterrence.RaidPenaltyPerCapture);
            entry.LastUpdatedHours = nowHours;
            entry.LastEnforcementHours = nowHours;
        }

        public static float GetRaidScoreMultiplier(MobileParty party)
        {
            if (party?.LeaderHero == null) return 1f;

            DeterrenceEntry? entry = FindEntryByHero(party.LeaderHero);
            if (entry == null) return 1f;

            float effectivePenalty = GetEffectivePenaltyInternal(
                entry,
                party.LeaderHero,
                (float)CampaignTime.Now.ToHours,
                updateEntry: false);

            if (effectivePenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return 1f;

            float multiplier = MathF.Pow(GwpTuning.Deterrence.RaidScoreMultiplierPerPoint, effectivePenalty);
            return MathF.Max(GwpTuning.Deterrence.RaidScoreMultiplierFloor, multiplier);
        }

        public static bool TryBuildPainDialogue(Hero hero, out TextObject text)
        {
            text = new TextObject(string.Empty);
            if (hero == null) return false;

            DeterrenceEntry? entry = FindEntryByHero(hero);
            if (entry == null) return false;

            float effectivePenalty = GetEffectivePenaltyInternal(
                entry,
                hero,
                (float)CampaignTime.Now.ToHours,
                updateEntry: false);

            if (effectivePenalty < GwpTuning.Deterrence.ActiveDialogueThreshold)
                return false;

            string heroName = hero.Name?.ToString() ?? entry.DisplayName ?? "此人";

            if (effectivePenalty >= 2.5f)
            {
                text = new TextObject(
                    "{=gwp_ai_deterrence_high}灰袍上次那顿教训还没过去。我折了人手，也丢了胆气。至少这阵子，我不想再去碰那些村庄。");
            }
            else if (effectivePenalty >= 1.25f)
            {
                text = new TextObject(
                    "{=gwp_ai_deterrence_mid}灰袍前些日子把我打得够呛，损兵折将之后，连最莽的人也得学会收着些。");
            }
            else
            {
                text = new TextObject(
                    "{=gwp_ai_deterrence_low}{HERO_NAME}最近还记得灰袍给的那顿苦头。伤口未好，行事自然比从前谨慎。");
                text.SetTextVariable("HERO_NAME", heroName);
            }

            return true;
        }

        public static void DailyCleanup()
        {
            float nowHours = (float)CampaignTime.Now.ToHours;
            List<string> keysToRemove = new List<string>();

            foreach (KeyValuePair<string, DeterrenceEntry> kvp in _entries)
            {
                Hero? hero = FindHeroById(kvp.Value.HeroId);
                float effectivePenalty = GetEffectivePenaltyInternal(kvp.Value, hero, nowHours, updateEntry: true);
                float daysSinceEnforcement = (nowHours - kvp.Value.LastEnforcementHours) / CampaignTime.HoursInDay;

                if (effectivePenalty <= GwpTuning.Deterrence.ForgetThreshold &&
                    daysSinceEnforcement >= GwpTuning.Deterrence.CleanupGraceDays)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in keysToRemove)
                _entries.Remove(key);
        }

        public static void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                DailyCleanup();

                int count = _entries.Count;
                dataStore.SyncData("gwp_det_count", ref count);

                int index = 0;
                foreach (DeterrenceEntry entry in _entries.Values)
                {
                    string key = entry.Key;
                    string heroId = entry.HeroId;
                    string displayName = entry.DisplayName;
                    float penalty = entry.PenaltyPoints;
                    float lastUpdated = entry.LastUpdatedHours;
                    float lastEnforcement = entry.LastEnforcementHours;

                    dataStore.SyncData($"gwp_det_{index}_key", ref key);
                    dataStore.SyncData($"gwp_det_{index}_hero", ref heroId);
                    dataStore.SyncData($"gwp_det_{index}_name", ref displayName);
                    dataStore.SyncData($"gwp_det_{index}_penalty", ref penalty);
                    dataStore.SyncData($"gwp_det_{index}_updated", ref lastUpdated);
                    dataStore.SyncData($"gwp_det_{index}_last_hit", ref lastEnforcement);
                    index++;
                }
            }
            else if (dataStore.IsLoading)
            {
                _entries.Clear();

                int count = 0;
                dataStore.SyncData("gwp_det_count", ref count);
                for (int i = 0; i < count; i++)
                {
                    string key = string.Empty;
                    string heroId = string.Empty;
                    string displayName = string.Empty;
                    float penalty = 0f;
                    float lastUpdated = 0f;
                    float lastEnforcement = 0f;

                    dataStore.SyncData($"gwp_det_{i}_key", ref key);
                    dataStore.SyncData($"gwp_det_{i}_hero", ref heroId);
                    dataStore.SyncData($"gwp_det_{i}_name", ref displayName);
                    dataStore.SyncData($"gwp_det_{i}_penalty", ref penalty);
                    dataStore.SyncData($"gwp_det_{i}_updated", ref lastUpdated);
                    dataStore.SyncData($"gwp_det_{i}_last_hit", ref lastEnforcement);

                    if (string.IsNullOrWhiteSpace(key) || penalty <= 0f)
                        continue;

                    _entries[key] = new DeterrenceEntry
                    {
                        Key = key,
                        HeroId = heroId,
                        DisplayName = displayName,
                        PenaltyPoints = penalty,
                        LastUpdatedHours = lastUpdated,
                        LastEnforcementHours = lastEnforcement
                    };
                }

                DailyCleanup();
            }
        }

        private static float GetEffectivePenaltyInternal(
            DeterrenceEntry entry,
            Hero? leader,
            float nowHours,
            bool updateEntry)
        {
            float elapsedDays = MathF.Max(0f, (nowHours - entry.LastUpdatedHours) / CampaignTime.HoursInDay);
            float recoveryPerDay = GetRecoveryPerDay(leader);
            float effectivePenalty = MathF.Max(0f, entry.PenaltyPoints - elapsedDays * recoveryPerDay);

            if (updateEntry)
            {
                entry.PenaltyPoints = effectivePenalty;
                entry.LastUpdatedHours = nowHours;
            }

            return effectivePenalty;
        }

        private static float GetRecoveryPerDay(Hero? leader)
        {
            float recovery = GwpTuning.Deterrence.BaseRecoveryPerDay;
            if (leader == null)
                return recovery;

            recovery += leader.GetTraitLevel(DefaultTraits.Valor) * 0.025f;
            recovery -= leader.GetTraitLevel(DefaultTraits.Honor) * 0.02f;
            recovery -= leader.GetTraitLevel(DefaultTraits.Mercy) * 0.02f;
            recovery -= leader.GetTraitLevel(DefaultTraits.Calculating) * 0.015f;

            return MBMath.ClampFloat(
                recovery,
                GwpTuning.Deterrence.MinRecoveryPerDay,
                GwpTuning.Deterrence.MaxRecoveryPerDay);
        }

        private static DeterrenceEntry? FindEntryByHero(Hero hero)
        {
            if (hero == null) return null;

            if (!string.IsNullOrEmpty(hero.StringId) &&
                _entries.TryGetValue(hero.StringId, out DeterrenceEntry? entryByHero))
            {
                return entryByHero;
            }

            MobileParty? party = hero.PartyBelongedTo;
            if (party != null && !string.IsNullOrEmpty(party.StringId) &&
                _entries.TryGetValue(party.StringId, out DeterrenceEntry? entryByParty))
            {
                return entryByParty;
            }

            return _entries.Values.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.HeroId) &&
                string.Equals(e.HeroId, hero.StringId, StringComparison.OrdinalIgnoreCase));
        }

        private static Hero? FindHeroById(string? heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId)) return null;
            return Hero.FindFirst(h => string.Equals(h.StringId, heroId, StringComparison.OrdinalIgnoreCase));
        }

        private static string? GetKey(MobileParty offender)
        {
            string? leaderId = offender?.LeaderHero?.StringId;
            if (!string.IsNullOrEmpty(leaderId))
                return leaderId;

            string? partyId = offender?.StringId;
            if (!string.IsNullOrEmpty(partyId))
                return partyId;

            return null;
        }
    }
}
