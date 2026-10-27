using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace GreyWardenPolicePurity
{
    /// <summary>现场预览缓存idle为-1、当前原版查询为4008：只重新解析已失效的默认动作。</summary>
    internal static class GwpCharacterPreviewActions
    {
        private static bool _installed;

        // 动作表加载后才改写返回ActionIndexCache的原版方法，避免修复本身提前初始化缓存。
        internal static void Install()
        {
            if (_installed) return;
            new Harmony(SubModule.HarmonyId).Patch(
                AccessTools.Method(typeof(CharacterTableau), "GetIdleAction"),
                postfix: new HarmonyMethod(typeof(GwpCharacterPreviewActions), nameof(ResolveIdle)));
            _installed = true;
        }

        private static void ResolveIdle(ref ActionIndexCache __result)
        {
            if (__result.Index < 0)
                __result = ActionIndexCache.Create("act_inventory_idle_start");
        }
    }
}
