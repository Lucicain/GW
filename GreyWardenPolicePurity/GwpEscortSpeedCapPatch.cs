using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 原版 <c>MobileParty.CalculateSpeed</c> 的护送分支只做一件事：跟随方比被跟随方
    /// 快时，把自己降到对方的速度。它没有反向的提速，所以对"追人"来说是纯粹的枷锁
    /// ——办差的灰袍一旦被降到目标的步子上，接手时差多远就永远差多远，一步都拉不近。
    ///
    /// 灰袍自己派出的跟随差事全部解除这个封顶：练兵队跟教官、协办人跟组长、拦截队
    /// 归队、承办人跟罪犯，都只是跟得更紧，路径、导航与交战语义全部不变——遭遇仍然
    /// 只由 <c>EngageParty</c> 触发，跟随不会提前开战。
    ///
    /// 只放行我们自己登记过跟随差事的队伍，并且必须当前确实处于原版
    /// <see cref="AiBehavior.EscortParty"/>；其它任何部队的速度一概不碰。军团阵型速度
    /// 在原版里排在护送分支之前，这里同样跳过，不干扰军团行军队形。
    /// </summary>
    [HarmonyPatch(typeof(MobileParty), "CalculateSpeed")]
    internal static class GwpEscortSpeedCapPatch
    {
        private static readonly Func<MobileParty, float>? UnifiedSpeed = ResolveUnifiedSpeed();

        private static Func<MobileParty, float>? ResolveUnifiedSpeed()
        {
            try
            {
                return AccessTools.MethodDelegate<Func<MobileParty, float>>(
                    AccessTools.Method(typeof(MobileParty), "CalculateSpeedForPartyUnified"));
            }
            catch (Exception exception)
            {
                // 未来版本改名就安静退回原版封顶，不能让速度计算抛异常。
                GwpFaultTrace.Write("ESCORT_SPEED_UNCAP_UNAVAILABLE",
                    details: exception.GetType().FullName + ":" + exception.Message);
                return null;
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(MobileParty __instance, ref float __result)
        {
            // Speed 是每帧被反复读取的热属性，判定顺序按"最便宜且最常为假"排列：
            // 绝大多数队伍在第一个条件就走掉了。
            if (UnifiedSpeed == null ||
                __instance?.IsActive != true ||
                __instance.DefaultBehavior != AiBehavior.EscortParty ||
                __instance.Army != null ||
                __instance.TargetParty == null ||
                !GreyWardenPartyDesireBehavior.ShouldUncapEscortSpeed(__instance))
                return true;

            __result = UnifiedSpeed(__instance);
            return false;
        }
    }
}
