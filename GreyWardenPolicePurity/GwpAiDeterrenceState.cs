using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal static class GwpAiDeterrenceState
    {
        internal readonly struct DeterrenceDetails
        {
            public bool HasEntry { get; init; }
            public float EffectivePenalty { get; init; }
            public int EnforcementCount { get; init; }
            public int SharedDeterrenceCount { get; init; }
            public float RaidScoreMultiplier { get; init; }
            public float DaysSinceLastEnforcement { get; init; }
            public string MapStatus { get; init; }
            public string MapLocation { get; init; }
        }

        private sealed class DeterrenceEntry
        {
            public string Key { get; set; } = string.Empty;
            public string HeroId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public int EnforcementCount { get; set; }
            public int SharedDeterrenceCount { get; set; }
            public float PenaltyPoints { get; set; }
            public float LastUpdatedHours { get; set; }
            public float LastEnforcementHours { get; set; }
        }

        private static readonly Dictionary<string, DeterrenceEntry> _entries =
            new Dictionary<string, DeterrenceEntry>(StringComparer.OrdinalIgnoreCase);
        private static string _trackedHeroId = string.Empty;
        private static float _lastTrackingReportHours = -9999f;

        public static void ClearAll()
        {
            _entries.Clear();
            _trackedHeroId = string.Empty;
            _lastTrackingReportHours = -9999f;
        }

        public static void RegisterEnforcementDefeat(MobileParty offender)
        {
            _ = RegisterDirectDeterrence(offender);
        }

        public static void RegisterEnforcementDefeat(Hero leader, MobileParty? sourceParty = null)
        {
            _ = RegisterDirectDeterrence(leader, sourceParty);
        }

        public static float RegisterDirectDeterrence(MobileParty offender)
        {
            string? maybeKey = GetKey(offender);
            if (string.IsNullOrEmpty(maybeKey))
                return 0f;

            Hero? leader = offender?.LeaderHero;
            string key = maybeKey!;
            float nowHours = (float)CampaignTime.Now.ToHours;
            string displayName = leader?.Name?.ToString() ?? offender?.Name?.ToString() ?? key;
            DeterrenceEntry entry = GetOrCreateEntry(key, leader, displayName, nowHours);

            float effectivePenalty = GetEffectivePenaltyInternal(entry, leader, nowHours, updateEntry: true);
            entry.EnforcementCount++;
            float penaltyGain = MathF.Min((float)entry.EnforcementCount, GwpTuning.Deterrence.MaxPenaltyGainPerCapture);
            entry.HeroId = leader?.StringId ?? entry.HeroId;
            entry.DisplayName = displayName;
            entry.PenaltyPoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap,
                effectivePenalty + penaltyGain);
            entry.LastUpdatedHours = nowHours;
            entry.LastEnforcementHours = nowHours;
            return entry.PenaltyPoints;
        }

        public static float RegisterDirectDeterrence(Hero leader, MobileParty? sourceParty = null)
        {
            if (Campaign.Current == null || leader == null || string.IsNullOrWhiteSpace(leader.StringId))
                return 0f;

            float nowHours = (float)CampaignTime.Now.ToHours;
            string displayName = leader.Name?.ToString()
                              ?? sourceParty?.Name?.ToString()
                              ?? leader.StringId;
            DeterrenceEntry entry = GetOrCreateEntry(leader.StringId, leader, displayName, nowHours);

            float effectivePenalty = GetEffectivePenaltyInternal(entry, leader, nowHours, updateEntry: true);
            entry.EnforcementCount++;
            float penaltyGain = MathF.Min((float)entry.EnforcementCount, GwpTuning.Deterrence.MaxPenaltyGainPerCapture);
            entry.HeroId = leader.StringId;
            entry.DisplayName = displayName;
            entry.PenaltyPoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap,
                effectivePenalty + penaltyGain);
            entry.LastUpdatedHours = nowHours;
            entry.LastEnforcementHours = nowHours;
            return entry.PenaltyPoints;
        }

        public static float RegisterSharedFamilyDeterrence(Hero leader, float penaltyGain)
        {
            if (Campaign.Current == null || leader == null || string.IsNullOrWhiteSpace(leader.StringId))
                return 0f;

            if (penaltyGain <= 0f)
                return 0f;

            float nowHours = (float)CampaignTime.Now.ToHours;
            string displayName = leader.Name?.ToString() ?? leader.StringId;
            DeterrenceEntry entry = GetOrCreateEntry(leader.StringId, leader, displayName, nowHours);

            float effectivePenalty = GetEffectivePenaltyInternal(entry, leader, nowHours, updateEntry: true);
            entry.HeroId = leader.StringId;
            entry.DisplayName = displayName;
            entry.SharedDeterrenceCount++;
            entry.PenaltyPoints = MathF.Min(
                GwpTuning.Deterrence.RaidPenaltyCap,
                effectivePenalty + penaltyGain);
            entry.LastUpdatedHours = nowHours;
            entry.LastEnforcementHours = nowHours;
            return entry.PenaltyPoints;
        }

        public static float GetRaidScoreMultiplier(MobileParty party)
        {
            return GetCrimeDesireMultiplier(party);
        }

        public static float GetCrimeDesireMultiplier(MobileParty? party)
        {
            if (party == null)
                return 1f;

            if (IsGreyWardenPoliceParty(party.ActualClan))
                return 0f;

            return GetCrimeDesireMultiplier(party.LeaderHero);
        }

        public static float GetCrimeDesireMultiplier(Hero? hero)
        {
            NormalizeEntries();

            if (Campaign.Current == null || hero == null)
                return 1f;

            if (IsGreyWardenPoliceHero(hero))
                return 0f;

            DeterrenceEntry? entry = FindEntryByHero(hero);
            if (entry == null)
                return 1f;

            float effectivePenalty = GetEffectivePenaltyInternal(
                entry,
                hero,
                (float)CampaignTime.Now.ToHours,
                updateEntry: false);

            if (effectivePenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return 1f;

            float multiplier = MathF.Pow(GwpTuning.Deterrence.RaidScoreMultiplierPerPoint, effectivePenalty);
            return MathF.Max(GwpTuning.Deterrence.RaidScoreMultiplierFloor, multiplier);
        }

        public static float GetCurrentPenalty(Hero? hero)
        {
            NormalizeEntries();

            if (Campaign.Current == null || hero == null)
                return 0f;

            DeterrenceEntry? entry = FindEntryByHero(hero);
            if (entry == null)
                return 0f;

            return GetEffectivePenaltyInternal(entry, hero, (float)CampaignTime.Now.ToHours, updateEntry: false);
        }

        public static DeterrenceDetails GetDeterrenceDetails(Hero? hero)
        {
            NormalizeEntries();

            if (Campaign.Current == null || hero == null)
            {
                return new DeterrenceDetails
                {
                    HasEntry = false,
                    EffectivePenalty = 0f,
                    EnforcementCount = 0,
                    SharedDeterrenceCount = 0,
                    RaidScoreMultiplier = 1f,
                    DaysSinceLastEnforcement = 0f,
                    MapStatus = GwpText.Get("{=gwp_gwpaideterrencestate_001}Unknown status"),
                    MapLocation = GwpText.Get("{=gwp_gwpaideterrencestate_002}Unknown location")
                };
            }

            MobileParty? party = hero.PartyBelongedTo;
            string mapStatus = BuildTrackingStatus(hero, party);
            string mapLocation = BuildTrackingLocation(hero, party);

            DeterrenceEntry? entry = FindEntryByHero(hero);
            if (entry == null)
            {
                return new DeterrenceDetails
                {
                    HasEntry = false,
                    EffectivePenalty = 0f,
                    EnforcementCount = 0,
                    SharedDeterrenceCount = 0,
                    RaidScoreMultiplier = 1f,
                    DaysSinceLastEnforcement = 0f,
                    MapStatus = mapStatus,
                    MapLocation = mapLocation
                };
            }

            float nowHours = (float)CampaignTime.Now.ToHours;
            float effectivePenalty = GetEffectivePenaltyInternal(entry, hero, nowHours, updateEntry: false);
            float raidScoreMultiplier = GetCrimeDesireMultiplier(hero);

            return new DeterrenceDetails
            {
                HasEntry = true,
                EffectivePenalty = effectivePenalty,
                EnforcementCount = entry.EnforcementCount,
                SharedDeterrenceCount = entry.SharedDeterrenceCount,
                RaidScoreMultiplier = raidScoreMultiplier,
                DaysSinceLastEnforcement = MathF.Max(0f, (nowHours - entry.LastEnforcementHours) / CampaignTime.HoursInDay),
                MapStatus = mapStatus,
                MapLocation = mapLocation
            };
        }

        public static bool TryBuildPainDialogue(Hero hero, out TextObject intro, out TextObject followup)
        {
            intro = new TextObject(string.Empty);
            followup = new TextObject(string.Empty);
            if (Campaign.Current == null) return false;
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

            string heroName = hero.Name?.ToString() ?? entry.DisplayName ?? GwpText.Get("{=gwp_gwpaideterrencestate_003}This person");

            int painLevel = Math.Max(1, Math.Min(9, (int)MathF.Ceiling(effectivePenalty)));
            intro = BuildPainIntro(heroName, painLevel);
            followup = BuildPainFollowup(painLevel);

            return true;
        }

        private static TextObject BuildPainIntro(string heroName, int painLevel)
        {
            string text = painLevel switch
            {
                9 => GwpText.Get("{=gwp_ai_deterrence_intro_9}{HERO_NAME} has gone pale, fingers pressed unconsciously against an old wound, and does not hear your approach."),
                8 => GwpText.Get("{=gwp_ai_deterrence_intro_8}{HERO_NAME} stares into the distance, held fast by a memory best left unbidden."),
                7 => GwpText.Get("{=gwp_ai_deterrence_intro_7}{HERO_NAME} stands lost in thought, brow knotted as though some weight still lies upon the breast."),
                6 => GwpText.Get("{=gwp_ai_deterrence_intro_6}{HERO_NAME} looks grim, and only slowly turns toward you when you speak."),
                5 => GwpText.Get("{=gwp_ai_deterrence_intro_5}{HERO_NAME} seems occupied by some bitter thought, and only after a time notices you standing near."),
                4 => GwpText.Get("{=gwp_ai_deterrence_intro_4}{HERO_NAME} looks weary, as though many nights have passed without sound sleep."),
                3 => GwpText.Get("{=gwp_ai_deterrence_intro_3}{HERO_NAME} stares dully ahead, as though the shadow of an old defeat has not yet lifted."),
                2 => GwpText.Get("{=gwp_ai_deterrence_intro_2}{HERO_NAME} appears distracted, some unwelcome memory newly returned."),
                _ => GwpText.Get("{=gwp_ai_deterrence_intro_1}{HERO_NAME} pauses before seeming to wake from a brief reverie.")
            };

            TextObject intro = new TextObject(text);
            intro.SetTextVariable("HERO_NAME", heroName);
            return intro;
        }

        private static TextObject BuildPainFollowup(int painLevel)
        {
            string text = painLevel switch
            {
                9 => GwpText.Get("{=gwp_ai_deterrence_followup_9}...What? Ah, it is you. Forgive me. I dreamt of the Grey Wardens again. That battle is a nail driven into my thoughts; I cannot draw it out."),
                8 => GwpText.Get("{=gwp_ai_deterrence_followup_8}Hm? What did you say? Forgive me—I was remembering the Grey Wardens. Their hand was so heavy that even now the memory turns my blood cold."),
                7 => GwpText.Get("{=gwp_ai_deterrence_followup_7}Ah, it is you. Forgive me; my thoughts strayed. The Grey Wardens taught me a cruel lesson, and that day returns to me still."),
                6 => GwpText.Get("{=gwp_ai_deterrence_followup_6}Hm? Speak. I was thinking of the Grey Wardens. After such a defeat, one learns to weigh whether a deed is worth one’s life."),
                5 => GwpText.Get("{=gwp_ai_deterrence_followup_5}Speak. The Grey Wardens came suddenly to mind. Since the reckoning they gave me, I stop before I act and consider twice."),
                4 => GwpText.Get("{=gwp_ai_deterrence_followup_4}What? Forgive me; I was recalling old matters. I have not forgotten the Grey Wardens’ lesson, and I have kept a tighter rein upon myself since."),
                3 => GwpText.Get("{=gwp_ai_deterrence_followup_3}Yes, speak. I was only thinking of the Grey Wardens. Once bitten, a person learns which deeds ought to be stayed."),
                2 => GwpText.Get("{=gwp_ai_deterrence_followup_2}Ah, it is you. Nothing is amiss; I merely remembered the Grey Wardens. Since that day, I have been more cautious than before."),
                _ => GwpText.Get("{=gwp_ai_deterrence_followup_1}Hm? Speak. I was thinking of the Grey Wardens. One chastening is enough to teach the memory pain.")
            };

            return new TextObject(text);
        }

        public static bool TryBuildHighestDeterrenceSnapshot(out string text)
        {
            text = string.Empty;
            if (Campaign.Current == null)
                return false;

            float nowHours = (float)CampaignTime.Now.ToHours;
            if (!TryBuildTrackingReport(nowHours, out text))
                return false;

            _lastTrackingReportHours = nowHours;
            return true;
        }

        public static bool TryGetTrackingReport(float intervalHours, out string text)
        {
            text = string.Empty;
            if (Campaign.Current == null)
                return false;

            float nowHours = (float)CampaignTime.Now.ToHours;

            if (nowHours - _lastTrackingReportHours < intervalHours)
                return false;

            if (!TryBuildTrackingReport(nowHours, out text))
                return false;

            _lastTrackingReportHours = nowHours;
            return true;
        }

        public static void DailyCleanup()
        {
            NormalizeEntries();

            if (Campaign.Current == null) return;
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
                NormalizeEntries();

                if (Campaign.Current != null)
                    DailyCleanup();

                int count = _entries.Count;
                dataStore.SyncData("gwp_det_count", ref count);
                dataStore.SyncData("gwp_det_track_hero", ref _trackedHeroId);
                dataStore.SyncData("gwp_det_track_last_report", ref _lastTrackingReportHours);

                int index = 0;
                foreach (DeterrenceEntry entry in _entries.Values)
                {
                    string key = entry.Key;
                    string heroId = entry.HeroId;
                    string displayName = entry.DisplayName;
                    int enforcementCount = entry.EnforcementCount;
                    int sharedDeterrenceCount = entry.SharedDeterrenceCount;
                    float penalty = entry.PenaltyPoints;
                    float lastUpdated = entry.LastUpdatedHours;
                    float lastEnforcement = entry.LastEnforcementHours;

                    dataStore.SyncData($"gwp_det_{index}_key", ref key);
                    dataStore.SyncData($"gwp_det_{index}_hero", ref heroId);
                    dataStore.SyncData($"gwp_det_{index}_name", ref displayName);
                    dataStore.SyncData($"gwp_det_{index}_hits", ref enforcementCount);
                    dataStore.SyncData($"gwp_det_{index}_shared_hits", ref sharedDeterrenceCount);
                    dataStore.SyncData($"gwp_det_{index}_penalty", ref penalty);
                    dataStore.SyncData($"gwp_det_{index}_updated", ref lastUpdated);
                    dataStore.SyncData($"gwp_det_{index}_last_hit", ref lastEnforcement);
                    index++;
                }
            }
            else if (dataStore.IsLoading)
            {
                _entries.Clear();
                _trackedHeroId = string.Empty;
                _lastTrackingReportHours = -9999f;

                int count = 0;
                dataStore.SyncData("gwp_det_count", ref count);
                dataStore.SyncData("gwp_det_track_hero", ref _trackedHeroId);
                dataStore.SyncData("gwp_det_track_last_report", ref _lastTrackingReportHours);
                for (int i = 0; i < count; i++)
                {
                    string key = string.Empty;
                    string heroId = string.Empty;
                    string displayName = string.Empty;
                    int enforcementCount = 0;
                    int sharedDeterrenceCount = 0;
                    float penalty = 0f;
                    float lastUpdated = 0f;
                    float lastEnforcement = 0f;

                    dataStore.SyncData($"gwp_det_{i}_key", ref key);
                    dataStore.SyncData($"gwp_det_{i}_hero", ref heroId);
                    dataStore.SyncData($"gwp_det_{i}_name", ref displayName);
                    dataStore.SyncData($"gwp_det_{i}_hits", ref enforcementCount);
                    dataStore.SyncData($"gwp_det_{i}_shared_hits", ref sharedDeterrenceCount);
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
                        EnforcementCount = enforcementCount,
                        SharedDeterrenceCount = sharedDeterrenceCount,
                        PenaltyPoints = penalty,
                        LastUpdatedHours = lastUpdated,
                        LastEnforcementHours = lastEnforcement
                    };
                }

                NormalizeEntries();

            }
        }

        private static float GetEffectivePenaltyInternal(
            DeterrenceEntry entry,
            Hero? leader,
            float nowHours,
            bool updateEntry)
        {
            float elapsedDays = CanRecoverPenalty(leader)
                ? MathF.Max(0f, (nowHours - entry.LastUpdatedHours) / CampaignTime.HoursInDay)
                : 0f;
            float recoveryPerDay = GetRecoveryPerDay(leader);
            float effectivePenalty = MathF.Max(0f, entry.PenaltyPoints - elapsedDays * recoveryPerDay);

            if (updateEntry)
            {
                entry.PenaltyPoints = effectivePenalty;
                entry.LastUpdatedHours = nowHours;
            }

            return effectivePenalty;
        }

        private static DeterrenceEntry GetOrCreateEntry(string key, Hero? leader, string displayName, float nowHours)
        {
            if (_entries.TryGetValue(key, out DeterrenceEntry? existingEntry))
                return existingEntry;

            DeterrenceEntry entry = new DeterrenceEntry
            {
                Key = key,
                HeroId = leader?.StringId ?? string.Empty,
                DisplayName = displayName,
                EnforcementCount = 0,
                SharedDeterrenceCount = 0,
                PenaltyPoints = 0f,
                LastUpdatedHours = nowHours,
                LastEnforcementHours = nowHours
            };

            _entries[key] = entry;
            return entry;
        }

        private static void NormalizeEntries()
        {
            if (_entries.Count <= 1)
                return;

            Dictionary<string, DeterrenceEntry> normalized =
                new Dictionary<string, DeterrenceEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (DeterrenceEntry entry in _entries.Values)
            {
                string canonicalKey = !string.IsNullOrWhiteSpace(entry.HeroId)
                    ? entry.HeroId
                    : entry.Key;

                if (string.IsNullOrWhiteSpace(canonicalKey))
                    continue;

                if (!normalized.TryGetValue(canonicalKey, out DeterrenceEntry? merged))
                {
                    normalized[canonicalKey] = new DeterrenceEntry
                    {
                        Key = canonicalKey,
                        HeroId = !string.IsNullOrWhiteSpace(entry.HeroId) ? entry.HeroId : canonicalKey,
                        DisplayName = entry.DisplayName,
                        EnforcementCount = entry.EnforcementCount,
                        SharedDeterrenceCount = entry.SharedDeterrenceCount,
                        PenaltyPoints = entry.PenaltyPoints,
                        LastUpdatedHours = entry.LastUpdatedHours,
                        LastEnforcementHours = entry.LastEnforcementHours
                    };
                    continue;
                }

                if (string.IsNullOrWhiteSpace(merged.HeroId) && !string.IsNullOrWhiteSpace(entry.HeroId))
                    merged.HeroId = entry.HeroId;

                if (string.IsNullOrWhiteSpace(merged.DisplayName) && !string.IsNullOrWhiteSpace(entry.DisplayName))
                    merged.DisplayName = entry.DisplayName;

                merged.EnforcementCount = Math.Max(merged.EnforcementCount, entry.EnforcementCount);
                merged.SharedDeterrenceCount = Math.Max(merged.SharedDeterrenceCount, entry.SharedDeterrenceCount);
                merged.PenaltyPoints = Math.Max(merged.PenaltyPoints, entry.PenaltyPoints);
                merged.LastUpdatedHours = Math.Max(merged.LastUpdatedHours, entry.LastUpdatedHours);
                merged.LastEnforcementHours = Math.Max(merged.LastEnforcementHours, entry.LastEnforcementHours);
            }

            _entries.Clear();
            foreach (KeyValuePair<string, DeterrenceEntry> kvp in normalized)
                _entries[kvp.Key] = kvp.Value;
        }

        private static bool CanRecoverPenalty(Hero? leader)
        {
            if (leader == null)
                return false;

            if (leader.IsPrisoner || leader.PartyBelongedToAsPrisoner != null)
                return false;

            MobileParty? party = leader.PartyBelongedTo;
            if (party != null && party.IsActive)
                return true;

            if (leader.CurrentSettlement != null || leader.StayingInSettlement != null)
                return true;

            return false;
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

        private static DeterrenceEntry? FindNextTrackedEntry(float nowHours)
        {
            DeterrenceEntry? best = null;
            float bestPenalty = float.MinValue;
            float bestEnforcement = float.MinValue;

            foreach (DeterrenceEntry entry in _entries.Values)
            {
                Hero? hero = FindHeroById(entry.HeroId);
                float effectivePenalty = GetEffectivePenaltyInternal(entry, hero, nowHours, updateEntry: false);
                if (effectivePenalty <= GwpTuning.Deterrence.ForgetThreshold)
                    continue;

                if (effectivePenalty > bestPenalty ||
                    (MathF.Abs(effectivePenalty - bestPenalty) < 0.001f &&
                     entry.LastEnforcementHours > bestEnforcement))
                {
                    bestPenalty = effectivePenalty;
                    bestEnforcement = entry.LastEnforcementHours;
                    best = entry;
                }
            }

            return best;
        }

        private static bool TryBuildTrackingReport(float nowHours, out string text)
        {
            text = string.Empty;
            DeterrenceEntry? entry = FindNextTrackedEntry(nowHours);
            if (entry == null)
                return false;

            Hero? hero = FindHeroById(entry.HeroId);
            float effectivePenalty = GetEffectivePenaltyInternal(entry, hero, nowHours, updateEntry: false);
            if (effectivePenalty <= GwpTuning.Deterrence.ForgetThreshold)
                return false;

            MobileParty? party = hero?.PartyBelongedTo ?? FindPartyById(entry.Key);
            string heroName = hero?.Name?.ToString() ?? entry.DisplayName ?? GwpText.Get("{=gwp_gwpaideterrencestate_022}Unknown Lord");
            string status = BuildTrackingStatus(hero, party);
            string location = BuildTrackingLocation(hero, party);
            int activeCount = CountActiveDeterrenceTargets(nowHours);

            text = GwpText.Get("{=gwp_gwpaideterrencestate_023}Test record: most-deterrent Warden {VAR_1}; present state {VAR_2}; location {VAR_3}; highest deterrence {VAR_4}; lords currently deterred {VAR_5}.", "VAR_1", heroName, "VAR_2", status, "VAR_3", location, "VAR_4", GwpText.Format(effectivePenalty, "0.##"), "VAR_5", activeCount);
            return true;
        }

        private static int CountActiveDeterrenceTargets(float nowHours)
        {
            int count = 0;

            foreach (DeterrenceEntry entry in _entries.Values)
            {
                Hero? hero = FindHeroById(entry.HeroId);
                float effectivePenalty = GetEffectivePenaltyInternal(entry, hero, nowHours, updateEntry: false);
                if (effectivePenalty > GwpTuning.Deterrence.ForgetThreshold)
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildTrackingLocation(Hero? hero, MobileParty? knownParty = null)
        {
            MobileParty? party = knownParty ?? hero?.PartyBelongedTo;
            PartyBase? captorParty = hero?.PartyBelongedToAsPrisoner;

            if (hero?.IsPrisoner == true && captorParty != null)
            {
                if (captorParty.IsSettlement && captorParty.Settlement != null)
                    return GwpText.Get("{=gwp_gwpaideterrencestate_024}Within {VAR_1} {VAR_2}", "VAR_1", captorParty.Settlement.Name, "VAR_2", FormatPosition(captorParty.Settlement.GetPosition2D));

                MobileParty? captorMobile = captorParty.MobileParty;
                if (captorMobile != null && captorMobile.IsActive)
                {
                    if (captorMobile.CurrentSettlement != null)
                        return GwpText.Get("{=gwp_gwpaideterrencestate_025}Within {VAR_1} {VAR_2}", "VAR_1", captorMobile.CurrentSettlement.Name, "VAR_2", FormatPosition(captorMobile.CurrentSettlement.GetPosition2D));

                    Settlement? nearestCaptorTown = GwpCommon.FindNearestTown(captorMobile);
                    return nearestCaptorTown != null
                        ? GwpText.Get("{=gwp_gwpaideterrencestate_026}Near {VAR_1} {VAR_2}", "VAR_1", nearestCaptorTown.Name, "VAR_2", FormatPosition(captorMobile.GetPosition2D))
                        : GwpText.Get("{=gwp_gwpaideterrencestate_027}Wild {VAR_1}", "VAR_1", FormatPosition(captorMobile.GetPosition2D));
                }
            }

            if (party != null && party.IsActive)
            {
                if (party.CurrentSettlement != null)
                    return GwpText.Get("{=gwp_gwpaideterrencestate_028}Within {VAR_1} {VAR_2}", "VAR_1", party.CurrentSettlement.Name, "VAR_2", FormatPosition(party.CurrentSettlement.GetPosition2D));

                Settlement? nearestTown = GwpCommon.FindNearestTown(party);
                return nearestTown != null
                    ? GwpText.Get("{=gwp_gwpaideterrencestate_029}Near {VAR_1} {VAR_2}", "VAR_1", nearestTown.Name, "VAR_2", FormatPosition(party.GetPosition2D))
                    : GwpText.Get("{=gwp_gwpaideterrencestate_030}In the wild {VAR_1}", "VAR_1", FormatPosition(party.GetPosition2D));
            }

            if (hero?.CurrentSettlement != null)
                return GwpText.Get("{=gwp_gwpaideterrencestate_031}Within {VAR_1} {VAR_2}", "VAR_1", hero.CurrentSettlement.Name, "VAR_2", FormatPosition(hero.CurrentSettlement.GetPosition2D));

            return GwpText.Get("{=gwp_gwpaideterrencestate_032}Unknown location");
        }

        private static string FormatPosition(Vec2 position)
        {
            return $"({position.X:0.0}, {position.Y:0.0})";
        }

        private static string BuildTrackingStatus(Hero? hero, MobileParty? knownParty = null)
        {
            if (hero == null)
                return GwpText.Get("{=gwp_gwpaideterrencestate_033}Unknown status");

            PartyBase? captorParty = hero.PartyBelongedToAsPrisoner;
            if (hero.IsPrisoner)
            {
                if (captorParty?.IsSettlement == true && captorParty.Settlement != null)
                    return GwpText.Get("{=gwp_gwpaideterrencestate_034}is imprisoned in {VAR_1}", "VAR_1", captorParty.Settlement.Name);

                if (captorParty?.IsMobile == true && captorParty.MobileParty != null)
                    return GwpText.Get("{=gwp_gwpaideterrencestate_035}is captured by {VAR_1}", "VAR_1", captorParty.MobileParty.Name);

                return GwpText.Get("{=gwp_gwpaideterrencestate_036}is in a captive state");
            }

            MobileParty? party = knownParty ?? hero.PartyBelongedTo;
            if (party != null && party.IsActive)
            {
                if (party.CurrentSettlement != null)
                    return GwpText.Get("{=gwp_gwpaideterrencestate_037}holding position with a party");

                return GwpText.Get("{=gwp_gwpaideterrencestate_038}travelling with a party");
            }

            Settlement? settlement = hero.CurrentSettlement ?? hero.StayingInSettlement;
            if (settlement != null)
                return GwpText.Get("{=gwp_gwpaideterrencestate_039}rests in {VAR_1}", "VAR_1", settlement.Name);

            if (hero.IsFugitive)
                return GwpText.Get("{=gwp_gwpaideterrencestate_040}On the run");

            if (hero.IsReleased)
                return GwpText.Get("{=gwp_gwpaideterrencestate_041}Just released");

            if (hero.IsTraveling)
                return GwpText.Get("{=gwp_gwpaideterrencestate_042}On the way");

            if (hero.IsNotSpawned)
                return GwpText.Get("{=gwp_gwpaideterrencestate_043}Not yet reappeared");

            return GwpText.Get("{=gwp_gwpaideterrencestate_044}Status unknown");
        }

        private static DeterrenceEntry? FindEntryByHeroId(string? heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId))
                return null;

            return _entries.Values.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.HeroId) &&
                string.Equals(e.HeroId, heroId, StringComparison.OrdinalIgnoreCase));
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

        private static DeterrenceEntry? FindEntryByOffender(MobileParty? offender)
        {
            if (offender == null)
                return null;

            string? key = GetKey(offender);
            if (!string.IsNullOrEmpty(key))
            {
                string safeKey = key!;
                if (_entries.TryGetValue(safeKey, out DeterrenceEntry? entryByKey))
                    return entryByKey;
            }

            if (offender.LeaderHero != null)
                return FindEntryByHero(offender.LeaderHero);

            if (!string.IsNullOrEmpty(offender.StringId) &&
                _entries.TryGetValue(offender.StringId, out DeterrenceEntry? entryByParty))
            {
                return entryByParty;
            }

            return null;
        }

        private static Hero? FindHeroById(string? heroId)
        {
            if (Campaign.Current == null) return null;
            if (string.IsNullOrWhiteSpace(heroId)) return null;
            return Hero.FindFirst(h => string.Equals(h.StringId, heroId, StringComparison.OrdinalIgnoreCase));
        }

        private static MobileParty? FindPartyById(string? partyId)
        {
            if (string.IsNullOrWhiteSpace(partyId))
                return null;

            return MobileParty.All?.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.StringId) &&
                string.Equals(p.StringId, partyId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGreyWardenPoliceHero(Hero? hero)
        {
            return hero?.Clan != null &&
                   IsGreyWardenPoliceParty(hero.Clan);
        }

        private static bool IsGreyWardenPoliceParty(Clan? clan)
        {
            return string.Equals(
                clan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
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

