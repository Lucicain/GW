using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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
                ["gw_leader_0"] = "{=gwp_hero_vandi}Aethelflaed",
                ["gw_leader_1"] = "{=gwp_hero_yoer}Cyneburh",
                ["gw_leader_2"] = "{=gwp_hero_mise}Mildthryth",
                ["gw_leader_3"] = "{=gwp_hero_shengduo}Wynflaed",
                ["gw_leader_4"] = "{=gwp_hero_chenxi}Eadgifu",
                ["gw_leader_5"] = "{=gwp_hero_muguang}Wulfhild"
            };

        private static readonly string[] GeneratedFemaleNames =
        {
            GwpText.Get("{=gwp_greywardenfamilybehavior_001}Aebbe"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_002}Aeffe"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_003}Eadgyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_004}Eadleofu"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_005}Eadwyn"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_006}Ealdgyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_007}Ealhburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_008}Ealhflaed"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_009}Ealhgyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_010}Ealhswith"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_011}Ealhthryth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_012}Ealhwaru"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_013}Ealhwyn"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_014}Eanburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_015}Eanflaed"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_016}Eanswith"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_017}Eormenburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_018}Eormenhild"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_019}Folcburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_020}Frithugyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_021}Frithuswith"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_022}Godgifu"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_023}Godwife"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_024}Heahburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_025}Heregyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_026}Hereswith"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_027}Leofcwen"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_028}Leofflaed"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_029}Leofgifu"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_030}Leofrun"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_031}Mildburh"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_032}Wulfflaed"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_033}Wulfgifu"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_034}Wulfgyth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_035}Wulfthryth"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_036}Wulfwyn")
        };

        private static readonly string[] NameSuffixes =
        {
            GwpText.Get("{=gwp_greywardenfamilybehavior_037} II"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_038} III"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_039} IV"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_040} V"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_041} VI"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_042} VII"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_043} VIII"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_044} IX"),
            GwpText.Get("{=gwp_greywardenfamilybehavior_045} X")
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
            foreach (Hero hero in policeClan.Heroes.Where(IsPoliceClanHero))
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

        internal static bool IsIssueResolutionHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.IssueResolution;

        internal static bool IsIssueResolutionParty(MobileParty? party) =>
            IsIssueResolutionHero(party?.LeaderHero);

        internal static bool IsCaravanProtectionHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.CaravanProtection;

        internal static bool IsVillageProtectionHero(Hero? hero) =>
            TryGetDuty(hero, out DutyKind kind) && kind == DutyKind.VillageProtection;

        internal static bool IsReconstructionParty(MobileParty? party) =>
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
                return GwpText.Get("{=gwp_greywardenfamilybehavior_054}Grey Warden Lawkeeper");

            return kind switch
            {
                DutyKind.Training => GwpText.Get("{=gwp_duty_training}Training Warden"),
                DutyKind.CaravanProtection => GwpText.Get("{=gwp_duty_caravan}Caravan Protection Warden"),
                DutyKind.VillageProtection => GwpText.Get("{=gwp_duty_village}Village Protection Warden"),
                DutyKind.IssueResolution => GwpText.Get("{=gwp_duty_issues}Petition Warden"),
                DutyKind.Reconstruction => GwpText.Get("{=gwp_greywardenfamilybehavior_053}Village Reconstruction Warden"),
                DutyKind.PlayerRequests => GwpText.Get("{=gwp_duty_player}Public Petition Warden"),
                _ => GwpText.Get("{=gwp_greywardenfamilybehavior_054}Grey Warden Lawkeeper")
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

        private static bool IsPoliceClanHero(Hero? hero)
        {
            return hero?.Clan != null &&
                   string.Equals(hero.Clan.StringId, GwpIds.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCoreLeader(Hero hero) =>
            CoreLeaderIdSet.Contains(hero.StringId);

        private static bool IsGeneratedPoliceHero(Hero? hero) =>
            hero != null && IsPoliceClanHero(hero) && !IsCoreLeader(hero);

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
            int baseIndex = StableHash(hero.StringId) % GeneratedFemaleNames.Length;
            for (int offset = 0; offset < GeneratedFemaleNames.Length; offset++)
            {
                string candidate = GeneratedFemaleNames[(baseIndex + offset) % GeneratedFemaleNames.Length];
                if (usedNames.Add(candidate))
                {
                    SetHeroName(hero, candidate);
                    return;
                }
            }

            string fallbackBase = GeneratedFemaleNames[baseIndex];
            for (int i = 0; i < NameSuffixes.Length; i++)
            {
                string candidate = fallbackBase + NameSuffixes[i];
                if (usedNames.Add(candidate))
                {
                    SetHeroName(hero, candidate);
                    return;
                }
            }

            string finalCandidate = fallbackBase + StableHash(hero.StringId).ToString();
            usedNames.Add(finalCandidate);
            SetHeroName(hero, finalCandidate);
        }

        private static void SetHeroName(Hero hero, string name)
        {
            TextObject text = new TextObject(name);
            hero.SetName(text, text);
        }

        private static TextObject BuildGeneratedMemberEncyclopedia(Hero hero)
        {
            string name = hero.Name?.ToString() ?? GwpText.Get("{=gwp_greywardenfamilybehavior_046}she");

            if (GreyWardenVillageAdoptionBehavior.TryGetAdoptionOrigin(hero.StringId, out string villageName))
            {
                if (hero.Age < 12f)
                {
                    return new TextObject(
                        GwpText.Get("{=gwp_greywardenfamilybehavior_047}After {VAR_2} was sacked and put to the torch, the Grey Wardens took the girl {VAR_1} into their inner ward. Though still young, she has grown accustomed to patrol calls, copied depositions, and the discipline of the order. To the Wardens she is no common kinswoman, but a sworn daughter carried forward from calamity.", "VAR_1", name, "VAR_2", villageName));
                }

                if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                {
                    return new TextObject(
                        GwpText.Get("{=gwp_greywardenfamilybehavior_048}The Grey Wardens adopted {VAR_1} in childhood, after {VAR_2} was burned. She now learns to read case rolls, copy testimony, discern offences, and observe the rites of the road. In her elders’ eyes, the orphan who survived the flames is becoming an heir trained within the inner ward.", "VAR_1", name, "VAR_2", villageName));
                }

                return BuildAdultEncyclopedia(hero, name,
                    GwpText.Get("{=gwp_adult_adopted_origin}When criminals sacked {VAR_2}, {VAR_1} lost the life she had known and was taken in by the Grey Wardens. {VAR_3}", "VAR_1", name, "VAR_2", villageName,
                        "VAR_3", GetDutyBiographyHint(hero)));
            }

            if (hero.Age < 12f)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greywardenfamilybehavior_050}{VAR_1} was born within the Grey Wardens’ inner ward. She has grown among patrol calls, old Imperial case rolls, and the order’s discipline, and her elders regard her as one of their young heirs. Such girls are not reared as the daughters of an ordinary noble house, but to keep the roads, record the cases, and bear the law.", "VAR_1", name));
            }

            if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
            {
                return new TextObject(
                    GwpText.Get("{=gwp_greywardenfamilybehavior_051}{VAR_1} belongs to the Grey Wardens’ rising generation. She already studies case rolls, copied testimony, the signs of crime, and the rites of patrol. Outsiders call them daughters of the house; within the order, they are known as heirs still under instruction, who will one day wear the grey in earnest.", "VAR_1", name));
            }

            return BuildAdultEncyclopedia(hero, name,
                GwpText.Get("{=gwp_adult_born_origin}{VAR_1} was raised amid the old Imperial law, family blood, and ordered discipline preserved by the Grey Wardens. {VAR_2}",
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
                return GwpText.Get("{=gwp_bio_hint_law}She follows the case rolls wherever the order has need of another steady hand.");

            return kind switch
            {
                DutyKind.Training => GwpText.Get("{=gwp_bio_hint_training}She spends long seasons in the drill yard, turning fresh levies into soldiers who can hold formation when the road turns bloody."),
                DutyKind.CaravanProtection => GwpText.Get("{=gwp_bio_hint_caravan}She is most often seen at bridges, ferries, and merchant crossings, following those who prey upon caravans until the roads are safe again."),
                DutyKind.VillageProtection => GwpText.Get("{=gwp_bio_hint_village}She rides where peasants have been attacked or fields put to the torch, and has made the protection of village life the measure of her service."),
                DutyKind.IssueResolution => GwpText.Get("{=gwp_bio_hint_issues}She travels from village headmen to town merchants and even the harder figures of the alleys, hearing unresolved petitions and leaving fewer grievances behind her."),
                DutyKind.Reconstruction => GwpText.Get("{=gwp_bio_hint_rebuild}She is repeatedly found among blackened roofs and abandoned wells, remaining after the fighting until ruined villages can receive their people once more."),
                DutyKind.PlayerRequests => GwpText.Get("{=gwp_bio_hint_player}She keeps an uncommon watch for petitions that fall outside the old rolls, especially those carried directly to the order by travellers of consequence."),
                _ => GwpText.Get("{=gwp_bio_hint_law}She follows the case rolls wherever the order has need of another steady hand.")
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
