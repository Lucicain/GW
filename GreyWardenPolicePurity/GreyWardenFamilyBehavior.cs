using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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
        private static readonly string[] CoreLeaderIds =
        {
            "gw_leader_0",
            "gw_leader_1",
            "gw_leader_2",
            "gw_leader_3",
            "gw_leader_4",
            "gw_leader_5"
        };

        private static readonly string[] GeneratedFemaleNames =
        {
            "澄音",
            "祈安",
            "望舒",
            "清岚",
            "静禾",
            "霁月",
            "朝露",
            "星祷",
            "守真",
            "昭宁",
            "清律",
            "若岚",
            "听雪",
            "慈光",
            "霜晨",
            "明祷",
            "兰序",
            "雅宁",
            "晨铃",
            "书弦",
            "怀澄",
            "澜音",
            "芷宁",
            "祷月",
            "安禾",
            "静澜",
            "思律",
            "清珑",
            "雨谣",
            "远星",
            "采祷",
            "露祈",
            "映岚",
            "书宁",
            "凝光",
            "夕晨"
        };

        private static readonly string[] NameSuffixes =
        {
            "二",
            "三",
            "四",
            "五",
            "六",
            "七",
            "八",
            "九",
            "十"
        };

        private static readonly HashSet<string> CoreLeaderIdSet =
            new HashSet<string>(CoreLeaderIds, StringComparer.OrdinalIgnoreCase);

        private static readonly FieldInfo? HeroCharacterObjectField =
            typeof(Hero).GetField("_characterObject", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? CharacterHeroObjectField =
            typeof(CharacterObject).GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic);

        public override void RegisterEvents()
        {
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
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
            string name = hero.Name?.ToString() ?? "她";

            if (GreyWardenVillageAdoptionBehavior.TryGetAdoptionOrigin(hero.StringId, out string villageName))
            {
                if (hero.Age < 12f)
                {
                    return new TextObject(
                        $"{name}是在{villageName}遭劫掠焚毁后，被灰袍守卫带回内院收养的女娃。她尚年幼，却已经在耳濡目染中听惯了巡路口令、案卷誊录与修会戒律。对灰袍而言，她不是寻常意义上的家族血亲，而是从灾厄中被接续下来的下一代誓女。");
                }

                if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                {
                    return new TextObject(
                        $"{name}幼时曾在{villageName}遭焚后被灰袍守卫收养。如今的她已开始学习识卷、抄录口供、辨识罪案与巡路礼仪，在长辈眼中，她正从劫后余生的孤女逐渐成长为灰袍内院受训的新一代后继者。");
                }

                return new TextObject(
                    $"{name}幼年时因{villageName}毁于罪犯劫掠而失去旧日生活，后被灰袍守卫收养。她的来历常被灰袍内部视作这支执法修会存在意义的证明之一：缉捕罪犯并非终点，替灾后幸存者续起秩序与归宿，同样是灰袍法职责的一部分。");
            }

            if (hero.Age < 12f)
            {
                return new TextObject(
                    $"{name}生于灰袍守卫的内院。她自幼在巡路口令、旧帝国案卷与修会戒律之间长大，被长辈视作灰袍下一代的幼年继承者。对灰袍而言，像她这样的女孩并不是寻常贵族家族的孩子，而是未来要学会守路、记案与持法而行的人。");
            }

            if (hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
            {
                return new TextObject(
                    $"{name}属于灰袍守卫的新生一代。她已经开始学习识卷、抄录口供、辨识罪案与巡路礼仪，在灰袍长辈眼中，她迟早会披上真正的灰袍。外人常把她们当作家族子女，灰袍内部却更习惯把这一代视作仍在受训的后继者。");
            }

            return new TextObject(
                $"{name}成长于灰袍守卫所维系的旧帝国法统、家族血脉与修会戒律之中。她既是灰袍血脉的延续者，也是这支女性警察家族的新一代执法者。对许多百姓而言，她所代表的不是门第本身，而是灰袍仍会继续巡察道路、缉捕罪犯并守住秩序。");
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
