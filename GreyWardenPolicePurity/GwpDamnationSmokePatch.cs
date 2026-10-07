using HarmonyLib;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 烧过人的信众村在地图上冒烟，用原版村子被劫掠后的那股烟（用户裁定 2026-10-07）。
    /// 原版在 SettlementVisual.RefreshPartyIcon 里，村子被劫掠过（Settlement.IsRaided）就挂 map_icon_village_plunder_fx，
    /// 正在被劫就挂 psys_fire_smoke_env_point；这一步只在村子的图标被标脏（PartyBase.SetVisualAsDirty）时重画
    /// （C#反编译 v1.4.8 SandBox.View.Map.Visuals.SettlementVisual、TaleWorlds.CampaignSystem.Party.PartyBase）。
    /// 不改 VillageState：它连着生产、民兵、户数。烧人时和烟散时由 GwpFireswornBehavior 把图标标脏。
    /// </summary>
    [HarmonyPatch(typeof(SettlementVisual), "RefreshPartyIcon")]
    internal static class GwpDamnationSmokePatch
    {
        private static void Prefix(SettlementVisual __instance, out bool __state) =>
            __state = __instance.MapEntity.IsVisualDirty;

        private static void Postfix(SettlementVisual __instance, bool __state)
        {
            Settlement? settlement = __instance.MapEntity.Settlement;
            if (!__state || settlement?.IsVillage != true || settlement.IsRaided ||
                __instance.MapEntity.MapEvent?.IsRaid == true || !GwpFireswornBehavior.IsSmoking(settlement))
                return;
            // 照原版挂烟：让它跟着走时间，从预显示里拿出来。
            GameEntity entity = __instance.StrategicEntity;
            entity.EntityFlags &= ~EntityFlags.DoNotTick;
            entity.AddParticleSystemComponent("map_icon_village_plunder_fx");
            if ((entity.EntityFlags & EntityFlags.Ignore) != 0)
                entity.RemoveFromPredisplayEntity();
        }
    }
}
