#if GWP_DIAGNOSTICS
using System;
using System.Runtime.CompilerServices;
#endif
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    // 自动换装候选询问此无原因重载；带 out reason 的玩家装备界面判定不改。
    // RBM 的背包与城镇候选均在交易、退旧装备前调用原版此入口。
    // 不修改或挂钩第三方方法，不重写装备，不需要存档状态或周期检查。
    // 依据：用户裁定 2026-10-07；1.4.8 CharacterHelper / RBM 4.5.4 实际 DLL 反编译。
    [HarmonyPatch(typeof(CharacterHelper), nameof(CharacterHelper.CanUseItem),
        new[] { typeof(BasicCharacterObject), typeof(EquipmentElement) })]
    internal static class GwpWardenAutoEquipmentPatch
    {
#if GWP_DIAGNOSTICS
        private static readonly ConditionalWeakTable<Hero, object> ObservedHeroes = new();
#endif

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(BasicCharacterObject currentCharacter,
            EquipmentElement itemRosterElement, ref bool __result)
        {
            if (currentCharacter is not CharacterObject character ||
                !PoliceResourceManager.IsPoliceClanHero(character.HeroObject))
                return true;

#if GWP_DIAGNOSTICS
            Hero hero = character.HeroObject;
            if (!ObservedHeroes.TryGetValue(hero, out _))
            {
                ObservedHeroes.Add(hero, new object());
                GwpFaultTrace.Write("WARDEN_AUTO_EQUIPMENT_REJECTED", details:
                    "hero=" + hero.StringId + "; candidate=" + itemRosterElement.Item?.StringId
                    + "; caller=" + Environment.StackTrace);
            }
#endif
            __result = false;
            return false;
        }
    }
}
