using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    [HarmonyPatch(typeof(EncyclopediaFamilyMemberVM), nameof(EncyclopediaFamilyMemberVM.RefreshValues))]
    internal static class GwpEncyclopediaFamilyRolePatch
    {
        private enum Role
        {
            Parent,
            Child,
            Sibling,
            Grandparent,
            Grandchild
        }

        private static readonly (string Property, Role Role)[] BellumGroups =
        {
            ("Parents", Role.Parent),
            ("Children", Role.Child),
            ("Grandparents", Role.Grandparent),
            ("Grandchildren", Role.Grandchild)
        };

        [HarmonyPostfix]
        private static void Postfix(EncyclopediaFamilyMemberVM __instance, Hero? ____baseHero)
        {
            if (____baseHero == null || __instance.Hero is not { } relative)
                return;

            Role role = HasParent(____baseHero, relative) ? Role.Parent :
                ____baseHero.Siblings.Contains(relative) ? Role.Sibling :
                HasParent(relative, ____baseHero) ? Role.Child : Role.Grandchild;
            if (GetRole(____baseHero, relative, role) is { } text)
                __instance.Role = GwpText.Get(text);
        }

        internal static void AttachBellumFamily(EncyclopediaHeroPageVM page, Hero hero)
        {
            if (page.GetPropertyValue("BellumFamily") is not ViewModel family ||
                family.GetType().FullName != "BellumCivile.UI.EncyclopediaHeroFamilyVM")
                return;

            Type? portraitType = family.GetType().Assembly.GetType("BellumCivile.UI.EncyclopediaFamilyPortraitVM");
            FieldInfo? roleField = portraitType?.GetField("_role", BindingFlags.Instance | BindingFlags.NonPublic);
            if (roleField == null || roleField.FieldType != typeof(TextObject))
                return;

            // UIExtenderEx 已在原版人物页构造结束前挂好内战的显示数据。
            // 内战刷新家人栏时会重建头像，只更新每张头像独有的 TextObject。
            foreach ((string property, Role role) in BellumGroups)
            {
                if (family.GetPropertyValue(property) is not IMBBindingList portraits)
                    continue;

                foreach (object? portrait in portraits)
                    SetBellumRole(portrait, hero, role, roleField);

                portraits.ListChanged += (_, change) =>
                {
                    if (change.ListChangedType == ListChangedType.ItemAdded)
                        SetBellumRole(portraits[change.NewIndex], hero, role, roleField);
                };
            }
        }

        private static void SetBellumRole(object? item, Hero hero, Role role, FieldInfo roleField)
        {
            if (item is not HeroVM { Hero: { } relative } portrait ||
                roleField.DeclaringType?.IsInstanceOfType(portrait) != true ||
                GetRole(hero, relative, role) is not { } title ||
                roleField.GetValue(portrait) is not TextObject text)
                return;

            text.Value = title;
            text.CacheTokens();
            portrait.OnPropertyChanged("Role");
        }

        private static string? GetRole(Hero hero, Hero relative, Role role) => role switch
        {
            Role.Child when GreyWardenFamilyBehavior.IsAdoptiveParent(hero, relative) =>
                GwpTextCatalog.gwp_family_role_taken_in,
            Role.Parent when GreyWardenFamilyBehavior.IsAdoptiveParent(relative, hero) =>
                GwpTextCatalog.gwp_family_role_caregiver,
            Role.Sibling when SharesAdoptiveParent(hero, relative) =>
                GwpTextCatalog.gwp_family_role_peer,
            Role.Grandchild when IsAdoptiveGrandparent(hero, relative) =>
                GwpTextCatalog.gwp_family_role_junior,
            Role.Grandparent when IsAdoptiveGrandparent(relative, hero) =>
                GwpTextCatalog.gwp_family_role_senior,
            _ => null
        };

        private static bool SharesAdoptiveParent(Hero hero, Hero relative)
        {
            Hero? parent = hero.Father ?? hero.Mother;
            return parent != null && HasParent(relative, parent) &&
                (GreyWardenFamilyBehavior.IsAdoptiveParent(parent, hero) ||
                 GreyWardenFamilyBehavior.IsAdoptiveParent(parent, relative));
        }

        private static bool IsAdoptiveGrandparent(Hero grandparent, Hero grandchild)
        {
            bool? maternalLine = AdoptionThrough(grandparent, grandchild.Mother, grandchild);
            bool? paternalLine = AdoptionThrough(grandparent, grandchild.Father, grandchild);
            return (maternalLine == true || paternalLine == true) && maternalLine != false && paternalLine != false;
        }

        private static bool? AdoptionThrough(Hero grandparent, Hero? parent, Hero grandchild)
        {
            if (parent == null || !HasParent(parent, grandparent))
                return null;

            return GreyWardenFamilyBehavior.IsAdoptiveParent(grandparent, parent) ||
                GreyWardenFamilyBehavior.IsAdoptiveParent(parent, grandchild);
        }

        private static bool HasParent(Hero child, Hero parent) => child.Mother == parent || child.Father == parent;
    }
}
