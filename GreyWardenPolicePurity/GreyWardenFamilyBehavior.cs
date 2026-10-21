using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 统一灰袍家族后继成员的外观、命名与百科文案。
    /// </summary>
    public sealed class GreyWardenFamilyBehavior : CampaignBehaviorBase
    {
        internal enum DutyKind
        {
            LegacyOrdinary = 0,
            Reconstruction = 1,
            Training = 2,
            CaravanProtection = 3,
            VillageProtection = 4,
            IssueResolution = 5,
            PlayerRequests = 6
        }

        private const string DutyHeroIdsKey = "GWPP_DutyHeroIds";
        private const string DutyKindsKey = "GWPP_DutyKinds";
        private static GreyWardenFamilyBehavior? _instance;
        private readonly Dictionary<string, DutyKind> _dutyByHeroId =
            new Dictionary<string, DutyKind>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] CoreLeaderIds =
        {
            "gw_leader_0",
            "gw_leader_1",
            "gw_leader_2",
            "gw_leader_3",
            "gw_leader_4",
            "gw_leader_5"
        };

        private static readonly Dictionary<string, string> CoreLeaderNameTemplates =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["gw_leader_0"] = GwpTextCatalog.gwp_hero_vandi,
                ["gw_leader_1"] = GwpTextCatalog.gwp_hero_yoer,
                ["gw_leader_2"] = GwpTextCatalog.gwp_hero_mise,
                ["gw_leader_3"] = GwpTextCatalog.gwp_hero_shengduo,
                ["gw_leader_4"] = GwpTextCatalog.gwp_hero_chenxi,
                ["gw_leader_5"] = GwpTextCatalog.gwp_hero_muguang
            };

        private static readonly string[] GeneratedFemaleNames =
        {
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_001),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_002),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_003),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_004),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_005),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_006),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_007),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_008),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_009),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_010),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_011),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_012),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_013),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_014),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_015),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_016),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_017),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_018),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_019),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_020),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_021),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_022),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_023),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_024),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_025),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_026),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_027),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_028),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_029),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_030),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_031),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_032),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_033),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_034),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_035),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_036)
        };

        private static readonly string[] NameSuffixes =
        {
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_037),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_038),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_039),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_040),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_041),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_042),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_043),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_044),
            GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_045)
        };

        private static readonly HashSet<string> CoreLeaderIdSet =
            new HashSet<string>(CoreLeaderIds, StringComparer.OrdinalIgnoreCase);

        private static readonly FieldInfo? HeroCharacterObjectField =
            typeof(Hero).GetField("_characterObject", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CharacterHeroObjectField =
            typeof(CharacterObject).GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic);

        public GreyWardenFamilyBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? heroIds = null;
            List<int>? dutyKinds = null;
            if (dataStore.IsSaving)
            {
                heroIds = _dutyByHeroId.Keys.ToList();
                dutyKinds = heroIds.Select(id => (int)_dutyByHeroId[id]).ToList();
            }

            dataStore.SyncData(DutyHeroIdsKey, ref heroIds);
            dataStore.SyncData(DutyKindsKey, ref dutyKinds);

            if (!dataStore.IsLoading)
                return;

            _dutyByHeroId.Clear();
            int count = Math.Min(heroIds?.Count ?? 0, dutyKinds?.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                string heroId = heroIds![i];
                int rawKind = dutyKinds![i];
                if (!string.IsNullOrWhiteSpace(heroId) &&
                    Enum.IsDefined(typeof(DutyKind), rawKind))
                    _dutyByHeroId[heroId] = (DutyKind)rawKind;
            }
        }

        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            if (!IsGeneratedPoliceHero(hero))
            {
                return;
            }

            EnsurePoliceHeroIsFemale(hero);
        }

        private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
        {
            if (index == 0)
            {
                _dutyByHeroId.Clear();
                RefreshPoliceClanFamilyPresentation();
            }
        }

        private void OnDailyTick()
        {
            RefreshPoliceClanFamilyPresentation();
        }

        private void OnHeroComesOfAge(Hero hero)
        {
            if (hero != null && IsGeneratedPoliceHero(hero))
            {
                EnsureCoreDutyAssignments();
                AssignDutyIfNeeded(hero);
                RefreshPoliceClanFamilyPresentation();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter) => RefreshPoliceClanFamilyPresentation();

        private void OnSessionLaunched(CampaignGameStarter starter) => RefreshPoliceClanFamilyPresentation();

        internal static void RefreshPoliceClanFamilyPresentation()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
            {
                return;
            }

            _instance?.EnsureCoreDutyAssignments();
            _instance?.AssignMissingAdultDuties(policeClan);

            List<Hero> generatedMembers = policeClan.Heroes
                .Where(IsGeneratedPoliceHero)
                .OrderBy(hero => hero.StringId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (Hero hero in generatedMembers)
            {
                EnsurePoliceHeroIsFemale(hero);
            }

            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Hero hero in policeClan.Heroes.Where(GwpCommon.IsGreyWardenClanMember))
            {
                if (hero?.Name == null || !IsCoreLeader(hero))
                {
                    continue;
                }

                if (CoreLeaderNameTemplates.TryGetValue(hero.StringId, out string nameTemplate))
                {
                    TextObject localizedName = new TextObject(nameTemplate);
                    hero.SetName(localizedName, localizedName);
                }

                string existingName = hero.Name.ToString();
                if (!string.IsNullOrWhiteSpace(existingName))
                {
                    usedNames.Add(existingName);
                }
            }

            foreach (Hero hero in generatedMembers)
            {
                AssignStableFemaleName(hero, usedNames);
                hero.EncyclopediaText = BuildGeneratedMemberEncyclopedia(hero);
            }

        }

        internal static bool IsReconstructionHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.Reconstruction;

        internal static bool IsTrainingHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.Training;


        internal static bool IsPlayerRequestsHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.PlayerRequests;

        internal static Hero? GetLivingDutyHolder(DutyKind duty)
        {
            Clan? clan = PoliceStats.GetPoliceClan();
            return clan?.Heroes
                .Where(hero => hero != null && hero.IsAlive && IsAdult(hero) &&
                               TryGetDuty(hero, out DutyKind assigned) &&
                               assigned == duty)
                .OrderByDescending(hero => hero.IsActive)
                .ThenBy(hero => hero.StringId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        internal static bool IsIssueResolutionHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.IssueResolution;

        internal static bool IsIssueResolutionParty(MobileParty? party) =>
            IsIssueResolutionHero(party?.LeaderHero);

        internal static bool IsReconstructionOfficeParty(MobileParty? party) =>
            IsReconstructionHero(party?.LeaderHero);

        internal static bool HasLivingReconstructionHolder()
            => HasLivingDutyHolder(DutyKind.Reconstruction);

        internal static bool HasLivingDutyHolder(DutyKind duty)
        {
            Clan? clan = PoliceStats.GetPoliceClan();
            return clan != null && clan.Heroes.Any(hero =>
                hero != null && hero.IsAlive && IsAdult(hero) &&
                TryGetDuty(hero, out DutyKind assigned) && assigned == duty);
        }

        internal static bool TryGetDuty(Hero? hero, out DutyKind kind)
        {
            kind = DutyKind.LegacyOrdinary;
            return hero != null && _instance != null &&
                   _instance._dutyByHeroId.TryGetValue(hero.StringId, out kind);
        }

        internal static string GetDutyTitle(Hero? hero)
        {
            if (!TryGetDuty(hero, out DutyKind kind))
                return GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_054);

            return kind switch
            {
                DutyKind.Training => GwpText.Get(GwpTextCatalog.gwp_duty_training),
                DutyKind.CaravanProtection => GwpText.Get(GwpTextCatalog.gwp_duty_caravan),
                DutyKind.VillageProtection => GwpText.Get(GwpTextCatalog.gwp_duty_village),
                DutyKind.IssueResolution => GwpText.Get(GwpTextCatalog.gwp_duty_issues),
                DutyKind.Reconstruction => GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_053),
                DutyKind.PlayerRequests => GwpText.Get(GwpTextCatalog.gwp_duty_player),
                _ => GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_054)
            };
        }

        private void EnsureCoreDutyAssignments()
        {
            for (int i = 0; i < CoreLeaderIds.Length; i++)
            {
                _dutyByHeroId[CoreLeaderIds[i]] = i switch
                {
                    0 => DutyKind.Training,
                    1 => DutyKind.CaravanProtection,
                    2 => DutyKind.VillageProtection,
                    3 => DutyKind.IssueResolution,
                    4 => DutyKind.Reconstruction,
                    _ => DutyKind.PlayerRequests
                };
            }
        }

        private void AssignMissingAdultDuties(Clan policeClan)
        {
            foreach (Hero hero in policeClan.Heroes
                         .Where(IsGeneratedPoliceHero)
                         .Where(hero => hero.IsAlive && IsAdult(hero))
                         .OrderBy(hero => hero.StringId, StringComparer.OrdinalIgnoreCase))
                AssignDutyIfNeeded(hero);
        }

        private void AssignDutyIfNeeded(Hero hero)
        {
            if ((_dutyByHeroId.TryGetValue(hero.StringId, out DutyKind existing) &&
                 existing != DutyKind.LegacyOrdinary) || !hero.IsAlive || !IsAdult(hero))
                return;

            _dutyByHeroId.Remove(hero.StringId);

            Clan? clan = PoliceStats.GetPoliceClan();
            if (clan == null)
                return;

            // A duty line can be reinforced only while at least one adult holder remains alive.
            // Once its last holder dies, later children cannot resurrect that lost function.
            var livingCounts = Enum.GetValues(typeof(DutyKind))
                .Cast<DutyKind>()
                .Where(kind => kind != DutyKind.LegacyOrdinary)
                .Select(kind => new
                {
                    Kind = kind,
                    Count = clan.Heroes.Count(candidate =>
                        candidate != null && candidate.IsAlive && IsAdult(candidate) &&
                        _dutyByHeroId.TryGetValue(candidate.StringId, out DutyKind assigned) &&
                        assigned == kind)
                })
                .Where(row => row.Count > 0)
                .OrderBy(row => row.Count)
                .ThenBy(row => DutySuccessionPriority(row.Kind))
                .ToList();

            if (livingCounts.Count == 0)
                return;

            DutyKind selected = livingCounts[0].Kind;
            _dutyByHeroId[hero.StringId] = selected;
            if (hero.PartyBelongedTo != null)
                GwpAiDiagnostics.WriteAction(
                    hero.PartyBelongedTo,
                    "DUTY_ASSIGNED",
                    "hero=" + hero.StringId + "; duty=" + selected);
        }

        private static int DutySuccessionPriority(DutyKind kind) => kind switch
        {
            DutyKind.Reconstruction => 0,
            DutyKind.IssueResolution => 1,
            DutyKind.VillageProtection => 2,
            DutyKind.CaravanProtection => 3,
            DutyKind.Training => 4,
            DutyKind.PlayerRequests => 5,
            _ => 99
        };

        private static bool IsAdult(Hero hero)
        {
            return Campaign.Current != null &&
                   hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
        }

        private static bool IsCoreLeader(Hero hero) =>
            CoreLeaderIdSet.Contains(hero.StringId);

        internal static bool IsGeneratedPoliceHero(Hero? hero) =>
            hero != null && GwpCommon.IsGreyWardenClanMember(hero) && !IsCoreLeader(hero);

        private static void EnsurePoliceHeroIsFemale(Hero hero)
        {
            hero.IsFemale = true;

            CharacterObject? template = PickFemaleTemplate(hero);
            if (template != null && NeedsFemaleTemplateSwap(hero, template))
            {
                SwapCharacterTemplate(hero, template);
            }

            if (template != null)
            {
                ApplyFemaleBody(hero, template);
            }
        }

        private static bool NeedsFemaleTemplateSwap(Hero hero, CharacterObject template)
        {
            CharacterObject? current = hero.CharacterObject;
            if (current == null || !current.IsFemale)
            {
                return true;
            }

            CharacterObject? original = current.OriginalCharacter ?? current;
            return !CoreLeaderIdSet.Contains(original.StringId) &&
                   !string.Equals(original.StringId, template.StringId, StringComparison.OrdinalIgnoreCase);
        }

        private static CharacterObject? PickFemaleTemplate(Hero hero)
        {
            int index = StableHash(hero.StringId) % CoreLeaderIds.Length;
            for (int offset = 0; offset < CoreLeaderIds.Length; offset++)
            {
                string id = CoreLeaderIds[(index + offset) % CoreLeaderIds.Length];
                CharacterObject? template = CharacterObject.Find(id);
                if (template != null && template.IsFemale)
                {
                    return template;
                }
            }

            return CharacterObject.Find(GwpIds.CommanderTemplateCharacterId);
        }

        private static void SwapCharacterTemplate(Hero hero, CharacterObject template)
        {
            if (HeroCharacterObjectField == null || CharacterHeroObjectField == null)
            {
                return;
            }

            CharacterObject? oldCharacter = hero.CharacterObject;
            CharacterObject newCharacter = CharacterObject.CreateFrom(template);

            CharacterHeroObjectField.SetValue(newCharacter, hero);
            HeroCharacterObjectField.SetValue(hero, newCharacter);

            if (oldCharacter != null)
            {
                CharacterHeroObjectField.SetValue(oldCharacter, null);
            }
        }

        private static void ApplyFemaleBody(Hero hero, CharacterObject template)
        {
            BodyProperties generated = BodyProperties.GetRandomBodyProperties(
                template.Race,
                isFemale: true,
                template.GetBodyPropertiesMin(returnBaseValue: true),
                template.GetBodyPropertiesMax(returnBaseValue: true),
                0,
                StableHash(hero.StringId),
                template.BodyPropertyRange.HairTags,
                template.BodyPropertyRange.BeardTags,
                template.BodyPropertyRange.TattooTags);

            hero.StaticBodyProperties = generated.StaticProperties;
            hero.Weight = generated.Weight;
            hero.Build = generated.Build;
        }

        private static void AssignStableFemaleName(Hero hero, ISet<string> usedNames)
        {
            SetHeroName(hero, PickUniqueName(GeneratedFemaleNames, usedNames, StableHash(hero.StringId)));
        }

        /// <summary>
        /// 从名池里取一个 <paramref name="usedNames"/> 里没有的名字并登记进去：先按 seed 起点轮一圈，
        /// 名池用完再给起点那个名字加序数后缀。
        /// </summary>
        private static string PickUniqueName(IReadOnlyList<string> pool, ISet<string> usedNames, int seed)
        {
            int baseIndex = seed % pool.Count;
            for (int offset = 0; offset < pool.Count; offset++)
            {
                string candidate = pool[(baseIndex + offset) % pool.Count];
                if (usedNames.Add(candidate))
                {
                    return candidate;
                }
            }

            string fallbackBase = pool[baseIndex];
            for (int i = 0; i < NameSuffixes.Length; i++)
            {
                string candidate = fallbackBase + NameSuffixes[i];
                if (usedNames.Add(candidate))
                {
                    return candidate;
                }
            }

            string finalCandidate = fallbackBase + seed.ToString();
            usedNames.Add(finalCandidate);
            return finalCandidate;
        }

        private static void SetHeroName(Hero hero, string name)
        {
            TextObject text = new TextObject(name);
            hero.SetName(text, text);
        }

        private static TextObject BuildGeneratedMemberEncyclopedia(Hero hero)
        {
            string name = hero.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_046);

            if (GreyWardenVillageAdoptionBehavior.TryGetAdoptionOrigin(hero.StringId, out string villageName))
            {
                if (hero.Age < 12f)
                {
                    return new TextObject(
                        GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_047, "VAR_1", name, "VAR_2", villageName));
                }

                if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                {
                    return new TextObject(
                        GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_048, "VAR_1", name, "VAR_2", villageName));
                }

                return BuildAdultEncyclopedia(hero, name,
                    GwpText.Get(GwpTextCatalog.gwp_adult_adopted_origin, "VAR_1", name, "VAR_2", villageName,
                        "VAR_3", GetDutyBiographyHint(hero)));
            }

            if (hero.Age < 12f)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_050, "VAR_1", name));
            }

            if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
            {
                return new TextObject(
                    GwpText.Get(GwpTextCatalog.gwp_greywardenfamilybehavior_051, "VAR_1", name));
            }

            return BuildAdultEncyclopedia(hero, name,
                GwpText.Get(GwpTextCatalog.gwp_adult_born_origin,
                    "VAR_1", name, "VAR_2", GetDutyBiographyHint(hero)));
        }

        private static TextObject BuildAdultEncyclopedia(Hero hero, string name, string text)
        {
            _ = hero;
            _ = name;
            return new TextObject(text);
        }

        private static string GetDutyBiographyHint(Hero hero)
        {
            if (!TryGetDuty(hero, out DutyKind kind))
                return GwpText.Get(GwpTextCatalog.gwp_bio_hint_law);

            return kind switch
            {
                DutyKind.Training => GwpText.Get(GwpTextCatalog.gwp_bio_hint_training),
                DutyKind.CaravanProtection => GwpText.Get(GwpTextCatalog.gwp_bio_hint_caravan),
                DutyKind.VillageProtection => GwpText.Get(GwpTextCatalog.gwp_bio_hint_village),
                DutyKind.IssueResolution => GwpText.Get(GwpTextCatalog.gwp_bio_hint_issues),
                DutyKind.Reconstruction => GwpText.Get(GwpTextCatalog.gwp_bio_hint_rebuild),
                DutyKind.PlayerRequests => GwpText.Get(GwpTextCatalog.gwp_bio_hint_player),
                _ => GwpText.Get(GwpTextCatalog.gwp_bio_hint_law)
            };
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (string.IsNullOrEmpty(value))
                {
                    return hash;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash & int.MaxValue;
            }
        }
    }
}
