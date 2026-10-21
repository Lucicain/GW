using System.Linq;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>仅标记玩家正在办的案，不改变敌对关系、可见范围或地图实体名称。</summary>
    // RefreshBinding读取CampaignUIHelper的静态字段，启动期Harmony预编译会在文本加载前
    // 触发其初始化并永久失败（2026-10-20实机）。只挂不依赖该帮助类的属性写入点。
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.FullName), MethodType.Setter)]
    internal static class GwpCaseTargetNameplatePatch
    {
        internal static bool IsTarget(PartyNameplateVM marker)
        {
            MobileParty? party = marker.Party;
            if (party == null || party.IsMainParty) return false;
            return GwpPlayerCaseAssignment.IsAssignedCase(party.LeaderHero) ||
                party.Army?.LeaderParty == party && party.AttachedParties.Any(member =>
                    GwpPlayerCaseAssignment.IsAssignedCase(member.LeaderHero));
        }

        [HarmonyPrefix]
        private static void SetName(PartyNameplateVM __instance, ref string value, string ____fullNameBind)
        {
            if (!IsTarget(__instance)) return;
            // 原版控件会把显示名称回写属性。始终从原版原名缓存生成一次标签，
            // 不给回写值再加标签，否则递归通知直至栈溢出（2026-10-20实机）。
            value = GwpText.Get(GwpTextCatalog.gwp_case_target_nameplate, "NAME", ____fullNameBind);
        }
    }

    [HarmonyPatch(typeof(NameplateVM), nameof(NameplateVM.FactionColor), MethodType.Setter)]
    internal static class GwpCaseTargetColorPatch
    {
        [HarmonyPrefix]
        private static void SetColor(NameplateVM __instance, ref string value)
        {
            if (__instance is PartyNameplateVM marker && GwpCaseTargetNameplatePatch.IsTarget(marker))
                value = PartyNameplateVM.NegativeIndicator;
        }
    }
}
