using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版成年行为会在 HeroComesOfAgeEvent 中给英雄重新生成文化装备。
    /// 后缀必须挂在原版回调本身，而不是依赖多个事件监听器的注册顺序，
    /// 这样灰袍的新成年领主最后一定穿灰袍领主套装。
    /// </summary>
    [HarmonyPatch]
    internal static class GwpAdultCommanderLoadoutPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(
                typeof(AgingCampaignBehavior),
                "OnHeroComesOfAge",
                new[] { typeof(Hero) });

        [HarmonyPostfix]
        private static void Postfix(Hero hero)
        {
            try
            {
                PoliceResourceManager.EnsureCommanderLoadout(
                    hero,
                    "native_coming_of_age_postfix");
            }
            catch (Exception exception)
            {
                GwpAiDiagnostics.WriteHeroLifecycle(
                    hero,
                    "ADULT_COMMANDER_LOADOUT_FAILED",
                    exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
