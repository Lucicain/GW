using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// War Sails 的船只买卖整套是按"家族有封地"写的，而灰袍是无封地的独立执法家族，
    /// 于是两头都空转：
    ///
    /// * 买：<c>GetTownToBuyShipFrom</c> 先在 <c>clan.MapFaction.Fiefs</c> 里挑城，灰袍永远落空，
    ///   只能走"任意非敌对城镇"的兜底，而兜底自带 <c>MBRandom.RandomFloat &lt; 0.2f</c>。
    ///   叠上 <c>ConsiderPurchasingShip</c> 外层的 <c>0.5</c>，全家族每天只有 10% 的机会去看一眼，
    ///   而且一天只买一条。六名领主各需三条，期望要一百八十天。
    /// * 卖：<c>GetTownToSellShip</c> 只看 <c>clan.MapFaction.Fiefs</c>，**连兜底都没有**，直接返回 null。
    ///
    /// 这里只补上"该挑哪座城"这一步——把无封地家族接回原版为有封地家族准备的那条路。
    /// 买哪条船、出多少钱、划不划算、要不要卖，全部仍由原版
    /// <c>TryPurchasingShipFromTown</c> / <c>ConsiderSellingShips</c> 判断：
    /// 预算闸门 <c>&lt; clan.Gold * 0.2</c>、<c>ShipDistributionModel</c> 的组合评分、
    /// <c>ClanShipOwnershipModel</c> 的家族理想船数，一个字都没碰。
    ///
    /// NavalDLC 没装时 <see cref="Prepare"/> 返回 false，整个补丁类跳过。本模组不引用
    /// NavalDLC 程序集，目标方法一律按名字在运行时解析。
    /// </summary>
    internal static class GwpLandlessShipTrade
    {
        private const string ShipTradeBehaviorTypeName =
            "NavalDLC.CampaignBehaviors.ShipTradeCampaignBehavior";
        private const string ShipyardBuildingTypeId = "building_shipyard";

        internal static MethodBase? FindShipTradeMethod(string methodName)
        {
            try
            {
                Type? behavior = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly =>
                    {
                        try { return assembly.GetType(ShipTradeBehaviorTypeName, false); }
                        catch { return null; }
                    })
                    .FirstOrDefault(type => type != null);

                return behavior == null
                    ? null
                    : AccessTools.Method(behavior, methodName, new[] { typeof(Clan) });
            }
            catch (Exception exception)
            {
                GwpFaultTrace.Write("LANDLESS_SHIP_TRADE_TARGET_FAILED",
                    details: methodName + " -> " +
                        exception.GetType().FullName + ":" + exception.Message);
                return null;
            }
        }

        internal static bool IsPoliceClan(Clan? clan) =>
            clan != null && !clan.IsEliminated &&
            string.Equals(clan.StringId, GwpIds.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 只有真正无封地时才接管。灰袍哪天拿到了封地，原版那条路自己就通了，
        /// 这里必须让开，不能变成永久绕过。
        /// </summary>
        private static bool ShouldSubstituteTown(Clan? clan, Town? currentResult) =>
            currentResult == null && IsPoliceClan(clan) &&
            (clan!.MapFaction?.Fiefs == null || clan.MapFaction.Fiefs.Count == 0);

        private static bool IsHostile(Clan clan, Town town) =>
            town.MapFaction != null && town.MapFaction.IsAtWarWith(clan);

        private static Town? PickRandom(IEnumerable<Town> candidates)
        {
            // 原版兜底用的是 GetRandomElementWithPredicate，这里保持同样的"随机挑一座"，
            // 而不是永远挑最近的那座——否则同一座城的存货会被反复扫空。
            List<Town> list = candidates.ToList();
            return list.Count == 0
                ? null
                : list[MBRandom.RandomInt(list.Count)];
        }

        /// <summary>原版 <c>CanClanBuyShipFromTown</c> 的同款条件，外加非敌对。</summary>
        internal static Town? FindTownToBuyFrom(Clan clan) =>
            PickRandom(Town.AllTowns.Where(town =>
                town != null && !town.IsUnderSiege &&
                town.AvailableShips != null && town.AvailableShips.Count > 0 &&
                !IsHostile(clan, town)));

        /// <summary>
        /// 原版卖船要求目标城有已建成的船坞。<c>Town.GetShipyard()</c> 是 NavalDLC 的扩展方法，
        /// 本模组不引用该程序集，因此按建筑 id 判定——与原先手写卖船逻辑用的是同一条件。
        /// </summary>
        internal static Town? FindTownToSellTo(Clan clan) =>
            PickRandom(Town.AllTowns.Where(town =>
                town != null && !town.IsUnderSiege &&
                town.Buildings != null &&
                town.Buildings.Any(building =>
                    building?.BuildingType != null &&
                    building.CurrentLevel > 0 &&
                    string.Equals(building.BuildingType.StringId,
                        ShipyardBuildingTypeId, StringComparison.OrdinalIgnoreCase)) &&
                !IsHostile(clan, town)));

        internal static void SubstituteBuyTown(Clan clan, ref Town? result)
        {
            if (!ShouldSubstituteTown(clan, result)) return;
            result = FindTownToBuyFrom(clan);
        }

        internal static void SubstituteSellTown(Clan clan, ref Town? result)
        {
            if (!ShouldSubstituteTown(clan, result)) return;
            result = FindTownToSellTo(clan);
        }
    }

    [HarmonyPatch]
    internal static class GwpLandlessShipPurchaseTownPatch
    {
        private static MethodBase? _target;

        private static bool Prepare()
        {
            _target ??= GwpLandlessShipTrade.FindShipTradeMethod("GetTownToBuyShipFrom");
            return _target != null;
        }

        private static MethodBase TargetMethod() => _target!;

        [HarmonyPostfix]
        private static void Postfix(Clan clan, ref Town? __result) =>
            GwpLandlessShipTrade.SubstituteBuyTown(clan, ref __result);
    }

    [HarmonyPatch]
    internal static class GwpLandlessShipSaleTownPatch
    {
        private static MethodBase? _target;

        private static bool Prepare()
        {
            _target ??= GwpLandlessShipTrade.FindShipTradeMethod("GetTownToSellShip");
            return _target != null;
        }

        private static MethodBase TargetMethod() => _target!;

        [HarmonyPostfix]
        private static void Postfix(Clan clan, ref Town? __result) =>
            GwpLandlessShipTrade.SubstituteSellTown(clan, ref __result);
    }
}
